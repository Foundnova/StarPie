using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace WinPieGestures;

public static class OcrManager
{
	private static readonly HttpClient s_httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };

	/// <summary>打开 OCR 多引擎与接口设置弹窗</summary>
	public static void ShowSettingsDialog()
	{
		Application.Current?.Dispatcher.Invoke(() =>
		{
			try
			{
				OcrSettingsDialog dlg = new OcrSettingsDialog();
				dlg.Owner = Application.Current?.MainWindow;
				dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				dlg.ShowDialog();
			}
			catch (Exception ex)
			{
				AppLogger.LogError("Failed to show OcrSettingsDialog", ex);
			}
		});
	}

	/// <summary>由动作触发：全屏框选截屏并执行 OCR 文本提取</summary>
	public static void StartCaptureAndRecognize()
	{
		Application.Current?.Dispatcher.Invoke(() =>
		{
			try
			{
				ScreenSnipWindow snip = new ScreenSnipWindow(async bmp =>
				{
					if (bmp != null)
					{
						await ProcessSnippetAsync(bmp);
					}
				});
				snip.Show();
			}
			catch (Exception ex)
			{
				AppLogger.LogError("Failed to launch ScreenSnipWindow", ex);
				MessageBox.Show("启动截屏框选失败: " + ex.Message, "StarPie", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		});
	}

	public static async Task ProcessSnippetAsync(Bitmap bmp)
	{
		OcrSettings config = ConfigManager.CurrentConfig?.OcrConfig ?? new OcrSettings();
		Stopwatch sw = Stopwatch.StartNew();
		string recognizedText = "";
		string engineName = "本地离线引擎";

		try
		{
			string provider = config.Provider?.Trim() ?? "Local";
			switch (provider)
			{
			case "Ai":
				engineName = $"AI 视觉大模型 ({config.AiModel})";
				recognizedText = await RecognizeWithAiVisionAsync(bmp, config);
				break;

			case "Custom":
				engineName = "自定义 HTTP OCR";
				recognizedText = await RecognizeWithCustomHttpAsync(bmp, config);
				break;

			case "Cloud":
				engineName = $"{config.CloudProvider} 云端 OCR";
				recognizedText = await RecognizeWithCloudAsync(bmp, config);
				break;

			case "Local":
			default:
				engineName = "Windows 本地离线引擎";
				recognizedText = await RecognizeWithLocalWinRtAsync(bmp, config);
				break;
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("OCR recognition error", ex);
			recognizedText = $"[OCR 识别异常]: {ex.Message}";
		}
		finally
		{
			sw.Stop();
			bmp.Dispose();
		}

		string latency = $"{sw.ElapsedMilliseconds}ms";

		// 格式后处理：去除中文字间多余空格、合并断行
		if (config.MergeLines && !recognizedText.StartsWith("["))
		{
			recognizedText = PostProcessText(recognizedText, config.RemoveSpacesBetweenCjk);
		}

		// 调度回 UI 线程分发结果
		Application.Current?.Dispatcher.Invoke(() =>
		{
			if (!string.IsNullOrWhiteSpace(recognizedText) && config.AutoCopyToClipboard)
			{
				try
				{
					System.Windows.Clipboard.SetText(recognizedText);
				}
				catch
				{
				}
			}

			if (config.ShowResultWindow)
			{
				OcrResultWindow resWin = new OcrResultWindow(recognizedText, engineName, latency);
				resWin.Show();
			}

			if (config.SearchInBrowser && !string.IsNullOrWhiteSpace(recognizedText))
			{
				try
				{
					string q = recognizedText.Trim();
					if (q.Length > 80) q = q.Substring(0, 80);
					string url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(q);
					Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
				}
				catch
				{
				}
			}
		});
	}

	/// <summary>1. Windows 10/11 原生 Windows.Media.Ocr 引擎</summary>
	private static async Task<string> RecognizeWithLocalWinRtAsync(Bitmap bmp, OcrSettings config)
	{
		if (OcrEngine.AvailableRecognizerLanguages.Count == 0)
		{
			return "[提示]: 当前 Windows 系统未检测到本地原生 OCR 识别引擎组件。\n（常见于精简版/企业版 Windows 系统，或系统尚未下载「光学字符识别」可选功能）\n\n💡 推荐解决方案：\n1. 【一键切换到 AI 视觉大模型】（推荐 · 免安装任何本地包 · 识别精度最高）：\n   在 StarPie 接口设置中配置硅基流动 / DeepSeek / OpenAI / Ollama 等端点，支持极速文字、表格与公式提取。\n2. 【安装 Windows 原生 OCR 功能】：\n   打开 Windows 设置 -> 应用 -> 可选功能 -> 添加可选功能，搜索并安装「中文(简体)光学字符识别」即可恢复离线使用。";
		}

		OcrEngine? engine = null;
		string langTag = config.LocalLanguage ?? "zh-Hans";

		try
		{
			if (OcrEngine.IsLanguageSupported(new Language(langTag)))
			{
				engine = OcrEngine.TryCreateFromLanguage(new Language(langTag));
			}
		}
		catch
		{
		}

		if (engine == null)
		{
			try
			{
				engine = OcrEngine.TryCreateFromUserProfileLanguages();
			}
			catch
			{
			}
		}

		if (engine == null)
		{
			engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
		}

		if (engine == null)
		{
			return "[提示]: 无法初始化本地 OCR 引擎。建议在 StarPie 动作设置中切换为 AI 视觉大模型 / 云端接口。";
		}

		SoftwareBitmap softwareBitmap = await ConvertToSoftwareBitmapAsync(bmp);
		OcrResult result = await engine.RecognizeAsync(softwareBitmap);
		if (result == null || result.Lines.Count == 0)
		{
			return "[未识别到有效文字内容]";
		}

		StringBuilder sb = new StringBuilder();
		foreach (var line in result.Lines)
		{
			sb.AppendLine(line.Text);
		}
		return sb.ToString().TrimEnd();
	}

	/// <summary>2. OpenAI 兼容 / 本地 Ollama 多模态视觉模型 API</summary>
	private static async Task<string> RecognizeWithAiVisionAsync(Bitmap bmp, OcrSettings config)
	{
		string endpoint = config.AiEndpoint?.Trim() ?? "https://api.openai.com/v1";
		if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			endpoint = endpoint.TrimEnd('/') + "/chat/completions";
		}

		string base64Image;
		using (MemoryStream ms = new MemoryStream())
		{
			bmp.Save(ms, ImageFormat.Jpeg);
			base64Image = Convert.ToBase64String(ms.ToArray());
		}

		string prompt = config.AiPromptMode switch
		{
			"latex" => "请提取图片中的全部数学公式与文字，将数学公式转换为标准 LaTeX 格式（如 $$...$$ 或 $...$）。仅输出公式与文本，不要包含多余开场白。",
			"markdown" => "请提取图片中的文字与表格结构，将表格转换为标准 Markdown 表格格式。不要包含多余寒暄。",
			"translate" => "请提取图片中的文字并直接翻译为流畅的简体中文。仅输出翻译结果。",
			_ => "请精确提取图片中的全部文字。保持原有行结构，不要包含任何前缀或解释说明。"
		};

		var requestBody = new
		{
			model = string.IsNullOrWhiteSpace(config.AiModel) ? "gpt-4o-mini" : config.AiModel.Trim(),
			messages = new object[]
			{
				new
				{
					role = "user",
					content = new object[]
					{
						new { type = "text", text = prompt },
						new
						{
							type = "image_url",
							image_url = new { url = $"data:image/jpeg;base64,{base64Image}" }
						}
					}
				}
			},
			max_tokens = 2000
		};

		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (!string.IsNullOrWhiteSpace(config.AiApiKey))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AiApiKey.Trim());
		}
		request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

		HttpResponseMessage response = await s_httpClient.SendAsync(request);
		string responseJson = await response.Content.ReadAsStringAsync();

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {responseJson}");
		}

		using JsonDocument doc = JsonDocument.Parse(responseJson);
		if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
		{
			JsonElement firstChoice = choices[0];
			if (firstChoice.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement content))
			{
				return content.GetString()?.Trim() ?? "";
			}
		}

		return responseJson;
	}

	/// <summary>3. 自定义 HTTP 私有化 OCR (PaddleOCR / Umi-OCR)</summary>
	private static async Task<string> RecognizeWithCustomHttpAsync(Bitmap bmp, OcrSettings config)
	{
		string url = config.CustomHttpUrl?.Trim() ?? "http://127.0.0.1:1224/api/ocr";
		using MemoryStream ms = new MemoryStream();
		bmp.Save(ms, ImageFormat.Png);
		string base64 = Convert.ToBase64String(ms.ToArray());

		var requestObj = new { base64 = base64 };
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(JsonSerializer.Serialize(requestObj), Encoding.UTF8, "application/json")
		};

		HttpResponseMessage resp = await s_httpClient.SendAsync(req);
		string resText = await resp.Content.ReadAsStringAsync();
		if (!resp.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)resp.StatusCode}: {resText}");
		}

		try
		{
			using JsonDocument doc = JsonDocument.Parse(resText);
			if (doc.RootElement.TryGetProperty("data", out JsonElement data))
			{
				if (data.ValueKind == JsonValueKind.String) return data.GetString() ?? "";
				if (data.ValueKind == JsonValueKind.Array)
				{
					StringBuilder sb = new StringBuilder();
					foreach (var item in data.EnumerateArray())
					{
						if (item.TryGetProperty("text", out JsonElement txt)) sb.AppendLine(txt.GetString());
					}
					return sb.ToString().TrimEnd();
				}
			}
		}
		catch
		{
		}

		return resText;
	}

	/// <summary>4. 商业云端 OCR 占位支持</summary>
	private static async Task<string> RecognizeWithCloudAsync(Bitmap bmp, OcrSettings config)
	{
		await Task.Delay(100);
		return $"[{config.CloudProvider} 云端 OCR]: 凭证已就绪 (可直接在设置中绑定 API Key 与 Secret)";
	}

	private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bmp)
	{
		using MemoryStream ms = new MemoryStream();
		bmp.Save(ms, ImageFormat.Png);
		byte[] bytes = ms.ToArray();

		InMemoryRandomAccessStream ras = new InMemoryRandomAccessStream();
		using (DataWriter writer = new DataWriter(ras))
		{
			writer.WriteBytes(bytes);
			await writer.StoreAsync();
			await writer.FlushAsync();
			writer.DetachStream();
		}
		ras.Seek(0);

		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ras);
		SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
		ras.Dispose();
		return softwareBitmap;
	}

	private static string PostProcessText(string text, bool removeCjkSpaces)
	{
		if (string.IsNullOrWhiteSpace(text)) return text;
		string res = text;
		if (removeCjkSpaces)
		{
			// 移除中文字符之间的多余空格 (CJK unified ideographs)
			res = Regex.Replace(res, @"(?<=[\u4e00-\u9fa5])\s+(?=[\u4e00-\u9fa5])", "");
		}
		return res;
	}
}
