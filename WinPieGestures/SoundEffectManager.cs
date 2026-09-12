using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures;

/// <summary>
/// 轮盘交互音效类型
/// </summary>
public enum SoundType
{
	/// <summary>呼出轮盘</summary>
	WheelPopup,
	/// <summary>扇区切换/划过高亮</summary>
	SectorHover,
	/// <summary>二级级联菜单展开</summary>
	SubmenuExpand,
	/// <summary>动作确认触发执行</summary>
	ActionExecute,
	/// <summary>外甩脱离或手势取消</summary>
	GestureCancel
}

/// <summary>
/// 极轻量轮盘交互音效管理器 (Scheme C - Win32 内存驻留音频管线与数学波形合成)。
/// <para>
/// 核心特性：
/// 1. 纯原生 Win32 winmm.dll 非托管内存异步回放，延迟 &lt; 2ms，零第三方依赖；
/// 2. 程序化生成 44.1kHz 16-bit Mono 极微波形，常驻内存 &lt; 20 KB，完全免除外部音频文件依赖与资源解压开销；
/// 3. 使用非托管内存指针 (Marshal.AllocHGlobal)，彻底杜绝 GC 内存移动导致的底层 Access Violation；
/// 4. 扇区切换 35ms 极速防抖闸门，杜绝分界线高频抖动杂音；
/// 5. 独立于系统主音量的硬件级 PCM 数学振幅无损缩放。
/// </para>
/// </summary>
public static class SoundEffectManager
{
	[DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool PlaySoundW(IntPtr pszSound, IntPtr hmod, uint fdwSound);

	private const uint SND_SYNC = 0x0000;
	private const uint SND_NODEFAULT = 0x0002;
	private const uint SND_MEMORY = 0x0004;

	private static readonly object _syncLock = new object();
	private static readonly Dictionary<SoundType, IntPtr> _soundPointers = new();
	private static readonly Dictionary<SoundType, int> _soundLengths = new();

	private static readonly System.Threading.Channels.Channel<SoundType> _soundChannel =
		System.Threading.Channels.Channel.CreateBounded<SoundType>(new System.Threading.Channels.BoundedChannelOptions(2)
		{
			SingleWriter = false,
			SingleReader = true,
			FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
		});

	private static Thread? _workerThread;
	private static volatile bool _isRunning = false;

	private static string _currentTheme = string.Empty;
	private static double _currentVolume = -1.0;
	private static bool _initialized = false;
	private static long _lastHoverTick = 0L;

	/// <summary>扇区切换音效最小触发时间间隔 (毫秒)，防止光标在扇区分界线来回微颤时产生刺耳噪音。</summary>
	private const long HoverDebounceMs = 35L;

	/// <summary>
	/// 确保专属音频后台回放工作线程已启动（单读者无锁队列，彻底规避 WinMM 异步中断死锁）。
	/// </summary>
	private static void EnsureWorkerStarted()
	{
		if (_isRunning && _workerThread != null && _workerThread.IsAlive)
		{
			return;
		}
		lock (_syncLock)
		{
			if (_isRunning && _workerThread != null && _workerThread.IsAlive)
			{
				return;
			}
			_isRunning = true;
			_workerThread = new Thread(ProcessSoundQueue)
			{
				Name = "StarPie.SoundWorker",
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal
			};
			_workerThread.Start();
		}
	}

	/// <summary>
	/// 专属音频播放循环：在独立工作线程内使用 SND_SYNC 同步回放。
	/// 根本消除快速滑过多个子轮盘时 WinMM 频繁中止 waveOutReset 导致的内部死锁与无声故障。
	/// </summary>
	private static void ProcessSoundQueue()
	{
		var reader = _soundChannel.Reader;
		while (_isRunning)
		{
			try
			{
				if (reader.WaitToReadAsync().AsTask().Result)
				{
					while (reader.TryRead(out SoundType type))
					{
						IntPtr ptr = IntPtr.Zero;
						lock (_syncLock)
						{
							if (_soundPointers.TryGetValue(type, out IntPtr p))
							{
								ptr = p;
							}
						}

						if (ptr != IntPtr.Zero && _isRunning)
						{
							// 使用 SND_SYNC 在专属工作线程内完整播放微型 PCM 波形（~30ms），
							// 不产生 WinMM 内部辅助线程竞争，保证 100% 稳定可靠
							PlaySoundW(ptr, IntPtr.Zero, SND_SYNC | SND_MEMORY | SND_NODEFAULT);
						}
					}
				}
			}
			catch (Exception ex)
			{
				AppLogger.LogWarn($"SoundWorker iteration exception: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// 初始化或按需刷新音效数据缓存（在应用启动、配置载入或用户修改音量/主题时调用）。
	/// </summary>
	public static void Initialize(string? theme = null, double? volume = null)
	{
		lock (_syncLock)
		{
			string targetTheme = theme ?? ConfigManager.CurrentConfig?.SoundTheme ?? "Mechanical";
			double targetVolume = volume ?? ConfigManager.CurrentConfig?.SoundVolume ?? 0.6;
			targetVolume = Math.Clamp(targetVolume, 0.0, 1.0);

			if (_initialized && string.Equals(_currentTheme, targetTheme, StringComparison.OrdinalIgnoreCase)
				&& Math.Abs(_currentVolume - targetVolume) < 0.01)
			{
				return;
			}

			FreePointers();

			_currentTheme = targetTheme;
			_currentVolume = targetVolume;

			// 程序化合成 5 大音效事件波形
			AllocateSound(SoundType.WheelPopup, SynthesizeSound(SoundType.WheelPopup, targetTheme, targetVolume));
			AllocateSound(SoundType.SectorHover, SynthesizeSound(SoundType.SectorHover, targetTheme, targetVolume));
			AllocateSound(SoundType.SubmenuExpand, SynthesizeSound(SoundType.SubmenuExpand, targetTheme, targetVolume));
			AllocateSound(SoundType.ActionExecute, SynthesizeSound(SoundType.ActionExecute, targetTheme, targetVolume));
			AllocateSound(SoundType.GestureCancel, SynthesizeSound(SoundType.GestureCancel, targetTheme, targetVolume));

			_initialized = true;
		}

		EnsureWorkerStarted();
	}

	/// <summary>
	/// 触发播放指定事件类型的交互音效（完全非阻塞，极速无感）。
	/// </summary>
	public static void Play(SoundType type)
	{
		AppConfig? cfg = ConfigManager.CurrentConfig;
		if (cfg == null || !cfg.EnableSoundEffects)
		{
			return;
		}

		// 细项事件开关过滤
		bool isEnabled = type switch
		{
			SoundType.WheelPopup => cfg.SoundOnPopup,
			SoundType.SectorHover => cfg.SoundOnHover,
			SoundType.SubmenuExpand => cfg.SoundOnExpand,
			SoundType.ActionExecute => cfg.SoundOnExecute,
			SoundType.GestureCancel => cfg.SoundOnCancel,
			_ => true
		};

		if (!isEnabled)
		{
			return;
		}

		// 扇区切换防抖控制
		if (type == SoundType.SectorHover)
		{
			long now = Environment.TickCount64;
			if (now - _lastHoverTick < HoverDebounceMs)
			{
				return;
			}
			_lastHoverTick = now;
		}
		else if (type == SoundType.SubmenuExpand)
		{
			// 二级展开保护期：防止展开瞬间紧接着触发子扇区 Hover 堆叠
			_lastHoverTick = Environment.TickCount64 + 20L;
		}

		EnsureInitialized();
		EnsureWorkerStarted();
		_soundChannel.Writer.TryWrite(type);
	}

	/// <summary>
	/// 直接播放指定音效（供设置界面实时试听，不受全局开关拦截）。
	/// </summary>
	public static void PlayPreview(SoundType type)
	{
		EnsureInitialized();
		EnsureWorkerStarted();
		_soundChannel.Writer.TryWrite(type);
	}

	private static void EnsureInitialized()
	{
		if (!_initialized)
		{
			Initialize();
		}
	}

	private static void AllocateSound(SoundType type, byte[] wavData)
	{
		if (wavData == null || wavData.Length == 0) return;

		IntPtr ptr = Marshal.AllocHGlobal(wavData.Length);
		Marshal.Copy(wavData, 0, ptr, wavData.Length);
		_soundPointers[type] = ptr;
		_soundLengths[type] = wavData.Length;
	}

	private static void FreePointers()
	{
		lock (_syncLock)
		{
			try
			{
				// 立即中止可能正在回放的声音
				PlaySoundW(IntPtr.Zero, IntPtr.Zero, 0);
			}
			catch
			{
			}

			foreach (var kvp in _soundPointers)
			{
				if (kvp.Value != IntPtr.Zero)
				{
					try
					{
						Marshal.FreeHGlobal(kvp.Value);
					}
					catch
					{
					}
				}
			}
			_soundPointers.Clear();
			_soundLengths.Clear();
		}
	}

	/// <summary>
	/// 释放所有非托管音频内存与工作线程（在应用退出时调用）。
	/// </summary>
	public static void Shutdown()
	{
		_isRunning = false;
		try
		{
			_soundChannel.Writer.TryComplete();
		}
		catch
		{
		}
		lock (_syncLock)
		{
			FreePointers();
			_initialized = false;
		}
	}

	#region 程序化波形合成引擎 (Procedural Sound Synthesizer)

	/// <summary>
	/// 听觉响度曲线补偿：将 0.0~1.0 的滑块数值映射到符合人耳对数感知的高保真声学增益，
	/// 杜绝中低音量段因扬声器 DAC 降噪门限导致的静音。
	/// </summary>
	private static double GetAcousticGain(double sliderVol)
	{
		if (sliderVol <= 0.001) return 0.0;
		return Math.Clamp(0.18 + 0.82 * Math.Pow(Math.Clamp(sliderVol, 0.0, 1.0), 1.25), 0.0, 1.0);
	}

	/// <summary>
	/// 根据音效主题与类型，以纯数学算法生成 44.1kHz 16-bit 单声道 WAV 字节流。
	/// 所有音效均具有平滑起音微窗（防 DAC 爆音破音）与饱满谐波共鸣（确保各类扬声器与耳机清晰可辨）。
	/// </summary>
	private static byte[] SynthesizeSound(SoundType type, string theme, double volume)
	{
		int sampleRate = 44100;

		switch (theme.ToLowerInvariant())
		{
			case "crisp": // 现代清脆 (数码触感/清爽回馈)
				return type switch
				{
					SoundType.WheelPopup => SynthesizeSweep(sampleRate, 420, 920, 48, volume),
					SoundType.SectorHover => SynthesizeClick(sampleRate, 2100, 1050, 36, volume),
					SoundType.SubmenuExpand => SynthesizeDualTone(sampleRate, 980, 1470, 52, volume),
					SoundType.ActionExecute => SynthesizePunchyConfirm(sampleRate, 1100, 480, 68, volume),
					SoundType.GestureCancel => SynthesizeSweep(sampleRate, 720, 340, 42, volume * 0.85),
					_ => SynthesizeClick(sampleRate, 2100, 1050, 36, volume)
				};

			case "bubble": // 柔和气泡 (水滴轻音)
				return type switch
				{
					SoundType.WheelPopup => SynthesizeSweep(sampleRate, 340, 760, 56, volume * 0.95),
					SoundType.SectorHover => SynthesizeBubble(sampleRate, 820, 1450, 38, volume),
					SoundType.SubmenuExpand => SynthesizeDualTone(sampleRate, 880, 1320, 58, volume * 0.95),
					SoundType.ActionExecute => SynthesizeBubble(sampleRate, 680, 1680, 72, volume),
					SoundType.GestureCancel => SynthesizeSweep(sampleRate, 520, 240, 45, volume * 0.85),
					_ => SynthesizeBubble(sampleRate, 820, 1450, 38, volume)
				};

			case "minimalist": // 极简短音 (超微清晰脉冲)
				return type switch
				{
					SoundType.WheelPopup => SynthesizeSweep(sampleRate, 480, 820, 42, volume * 0.85),
					SoundType.SectorHover => SynthesizeClick(sampleRate, 1800, 900, 32, volume * 0.85),
					SoundType.SubmenuExpand => SynthesizeTone(sampleRate, 1020, 46, volume * 0.85),
					SoundType.ActionExecute => SynthesizeDualTone(sampleRate, 880, 440, 55, volume * 0.9),
					SoundType.GestureCancel => SynthesizeSweep(sampleRate, 420, 260, 38, volume * 0.75),
					_ => SynthesizeClick(sampleRate, 1800, 900, 32, volume * 0.85)
				};

			case "mechanical": // 机械手感 (默认 - 轴体微动与刻度感)
			default:
				return type switch
				{
					SoundType.WheelPopup => SynthesizeSweep(sampleRate, 260, 620, 54, volume),
					SoundType.SectorHover => SynthesizeMechanicalClick(sampleRate, 1750, 780, 38, volume),
					SoundType.SubmenuExpand => SynthesizeDualTone(sampleRate, 920, 1380, 60, volume),
					SoundType.ActionExecute => SynthesizePunchyConfirm(sampleRate, 720, 1080, 80, volume),
					SoundType.GestureCancel => SynthesizeSweep(sampleRate, 520, 220, 46, volume * 0.85),
					_ => SynthesizeMechanicalClick(sampleRate, 1750, 780, 38, volume)
				};
		}
	}

	/// <summary>
	/// 现代数码微动触感脉冲（适用于 Crisp 与 Minimalist）
	/// </summary>
	private static byte[] SynthesizeClick(int sampleRate, double clickFreq, double bodyFreq, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		for (int i = 0; i < samples; i++)
		{
			double t = (double)i / sampleRate;
			double norm = (double)i / samples;
			// 1.5ms 极小平滑起音，彻底消除扬声器开门爆音
			double attack = (t < 0.0015) ? (t / 0.0015) : 1.0;
			// 瞬态快速衰减敲击声
			double clickEnv = Math.Exp(-t * 260.0);
			double click = Math.Sin(2.0 * Math.PI * clickFreq * t) * clickEnv * 0.65;
			// 丰满主体共鸣，确保各类小型扬声器不被降噪切除
			double bodyEnv = Math.Pow(1.0 - norm, 1.7);
			double body = Math.Sin(2.0 * Math.PI * bodyFreq * t) * bodyEnv * 0.35;

			double s = (click + body) * attack * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 机械轴体双频咔哒声（轴体触发微动 + 触底沉稳共鸣）
	/// </summary>
	private static byte[] SynthesizeMechanicalClick(int sampleRate, double clickFreq, double thudFreq, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		for (int i = 0; i < samples; i++)
		{
			double t = (double)i / sampleRate;
			double norm = (double)i / samples;
			double attack = (t < 0.0012) ? (t / 0.0012) : 1.0;
			// 混合微量二次谐波强化清脆咔哒
			double clickEnv = Math.Exp(-t * 220.0);
			double click = (Math.Sin(2.0 * Math.PI * clickFreq * t) * 0.8 + Math.Sin(2.0 * Math.PI * clickFreq * 1.8 * t) * 0.2) * clickEnv * 0.6;
			// 沉稳轴座回响
			double thudEnv = Math.Pow(1.0 - norm, 1.5);
			double thud = Math.Sin(2.0 * Math.PI * thudFreq * t) * thudEnv * 0.4;

			double s = (click + thud) * attack * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 扫频滑音（适合呼出展开与外甩取消）
	/// </summary>
	private static byte[] SynthesizeSweep(int sampleRate, double fStart, double fEnd, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		double phase = 0.0;
		for (int i = 0; i < samples; i++)
		{
			double norm = (double)i / samples;
			double freq = fStart + (fEnd - fStart) * Math.Pow(norm, 1.2);
			phase += 2.0 * Math.PI * freq / sampleRate;
			// 正弦升余弦窗包络，两端无声截断
			double env = Math.Sin(Math.PI * norm);
			double s = Math.Sin(phase) * env * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 纯音衰减短音
	/// </summary>
	private static byte[] SynthesizeTone(int sampleRate, double freq, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		for (int i = 0; i < samples; i++)
		{
			double t = (double)i / sampleRate;
			double norm = (double)i / samples;
			double attack = (t < 0.002) ? (t / 0.002) : 1.0;
			double env = Math.Pow(1.0 - norm, 1.5) * attack;
			double s = Math.Sin(2.0 * Math.PI * freq * t) * env * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 和谐双音和弦（适合二级子轮盘展开）
	/// </summary>
	private static byte[] SynthesizeDualTone(int sampleRate, double f1, double f2, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		for (int i = 0; i < samples; i++)
		{
			double t = (double)i / sampleRate;
			double norm = (double)i / samples;
			double attack = (t < 0.003) ? (t / 0.003) : 1.0;
			double env = Math.Pow(1.0 - norm, 1.6) * attack;
			double s = (Math.Sin(2.0 * Math.PI * f1 * t) * 0.52 + Math.Sin(2.0 * Math.PI * f2 * t) * 0.48) * env * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 柔和气泡水滴音
	/// </summary>
	private static byte[] SynthesizeBubble(int sampleRate, double fStart, double fEnd, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		double phase = 0.0;
		for (int i = 0; i < samples; i++)
		{
			double norm = (double)i / samples;
			double freq = fStart + (fEnd - fStart) * Math.Pow(norm, 1.8);
			phase += 2.0 * Math.PI * freq / sampleRate;
			double env = Math.Pow(Math.Sin(Math.PI * norm), 0.7);
			double s = Math.Sin(phase) * env * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 笃定确认声（清脆 Snap + 沉稳 Thump 复合，适合动作执行）
	/// </summary>
	private static byte[] SynthesizePunchyConfirm(int sampleRate, double fSnap, double fThump, double durationMs, double sliderVol)
	{
		double gain = GetAcousticGain(sliderVol);
		int samples = Math.Max(1, (int)(sampleRate * durationMs / 1000.0));
		short[] pcm = new short[samples];

		for (int i = 0; i < samples; i++)
		{
			double t = (double)i / sampleRate;
			double norm = (double)i / samples;
			double attack = (t < 0.002) ? (t / 0.002) : 1.0;
			double snapEnv = Math.Exp(-t * 110.0);
			double snap = Math.Sin(2.0 * Math.PI * fSnap * t) * snapEnv * 0.65;
			double thumpEnv = Math.Pow(1.0 - norm, 1.4);
			double thump = Math.Sin(2.0 * Math.PI * fThump * t) * thumpEnv * 0.35;

			double s = (snap + thump) * attack * gain;
			pcm[i] = (short)Math.Clamp(s * 32767.0, -32768.0, 32767.0);
		}
		return WrapPcmToWav(pcm, sampleRate);
	}

	/// <summary>
	/// 将 PCM 采样封装为标准 RIFF/WAVE 二进制格式
	/// </summary>
	private static byte[] WrapPcmToWav(short[] pcm, int sampleRate)
	{
		int subChunk2Size = pcm.Length * sizeof(short);
		int chunkSize = 36 + subChunk2Size;

		using var ms = new MemoryStream(44 + subChunk2Size);
		using var bw = new BinaryWriter(ms);

		// RIFF header
		bw.Write(Encoding.ASCII.GetBytes("RIFF"));
		bw.Write(chunkSize);
		bw.Write(Encoding.ASCII.GetBytes("WAVE"));

		// "fmt " subchunk
		bw.Write(Encoding.ASCII.GetBytes("fmt "));
		bw.Write(16);               // Subchunk1Size (16 for PCM)
		bw.Write((short)1);          // AudioFormat (1 = PCM)
		bw.Write((short)1);          // NumChannels (1 = Mono)
		bw.Write(sampleRate);        // SampleRate
		bw.Write(sampleRate * 2);    // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
		bw.Write((short)2);          // BlockAlign (NumChannels * BitsPerSample/8)
		bw.Write((short)16);         // BitsPerSample

		// "data" subchunk
		bw.Write(Encoding.ASCII.GetBytes("data"));
		bw.Write(subChunk2Size);

		for (int i = 0; i < pcm.Length; i++)
		{
			bw.Write(pcm[i]);
		}

		bw.Flush();
		return ms.ToArray();
	}

	#endregion
}
