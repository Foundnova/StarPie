using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinPieGestures;

public class ReleaseInfo
{
	public string TagName { get; set; } = "";

	public Version? ParsedVersion { get; set; }

	public string Title { get; set; } = "";

	public string Body { get; set; } = "";

	public DateTime PublishedAt { get; set; }

	public bool IsPrerelease { get; set; }

	public string HtmlUrl { get; set; } = "";

	public bool IsNewerVersion { get; set; }

	public string? StandaloneAssetUrl { get; set; }

	public long StandaloneAssetSize { get; set; }

	public string? LightweightAssetUrl { get; set; }

	public long LightweightAssetSize { get; set; }
}

public class UpdateProgressInfo
{
	public int Percent { get; set; }

	public long BytesReceived { get; set; }

	public long TotalBytesToReceive { get; set; }

	public double SpeedBytesPerSecond { get; set; }

	public string FormattedSpeed => SpeedBytesPerSecond switch
	{
		>= 1024 * 1024 => $"{SpeedBytesPerSecond / (1024 * 1024):F1} MB/s",
		>= 1024 => $"{SpeedBytesPerSecond / 1024:F0} KB/s",
		_ => $"{SpeedBytesPerSecond:F0} B/s"
	};

	public string FormattedProgress => TotalBytesToReceive > 0
		? $"{BytesReceived / (1024.0 * 1024.0):F1} MB / {TotalBytesToReceive / (1024.0 * 1024.0):F1} MB"
		: $"{BytesReceived / (1024.0 * 1024.0):F1} MB";
}

public class UpdateManager
{
	private static readonly Lazy<UpdateManager> _instance = new Lazy<UpdateManager>(() => new UpdateManager());
	public static UpdateManager Instance => _instance.Value;

	private readonly HttpClient _httpClient;
	private const string RepoOwner = "SoftBlack42";
	private const string RepoName = "StarPie";

	private UpdateManager()
	{
		HttpClientHandler handler = new HttpClientHandler
		{
			AllowAutoRedirect = true,
			MaxAutomaticRedirections = 5
		};
		_httpClient = new HttpClient(handler)
		{
			Timeout = TimeSpan.FromSeconds(25)
		};
		_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StarPie-Updater", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2"));
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
	}

	public bool IsCurrentInstallationStandalone()
	{
		try
		{
			string appDir = AppDomain.CurrentDomain.BaseDirectory;
			return !File.Exists(Path.Combine(appDir, "StarPie.dll"));
		}
		catch
		{
			return true;
		}
	}

	public Version GetCurrentVersion()
	{
		return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 6, 2);
	}

	public string GetProxiedDownloadUrl(string rawUrl, string proxySource, string customProxy = "")
	{
		if (string.IsNullOrWhiteSpace(rawUrl)) return "";

		return proxySource?.ToLowerInvariant() switch
		{
			"ghproxy" => $"https://ghproxy.net/{rawUrl}",
			"moeyy" => $"https://github.moeyy.xyz/{rawUrl}",
			"custom" when !string.IsNullOrWhiteSpace(customProxy) => $"{customProxy.TrimEnd('/')}/{rawUrl}",
			_ => rawUrl
		};
	}

	public async Task<ReleaseInfo?> CheckForUpdateAsync(string channel = "Stable", string proxySource = "ghproxy", string customProxy = "", CancellationToken ct = default)
	{
		try
		{
			bool isBetaChannel = string.Equals(channel, "Beta", StringComparison.OrdinalIgnoreCase);
			string apiUrl = isBetaChannel
				? $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases"
				: $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

			string? json = null;

			// 尝试 1：直接访问 GitHub API
			try
			{
				using HttpResponseMessage response = await _httpClient.GetAsync(apiUrl, ct).ConfigureAwait(false);
				if (response.IsSuccessStatusCode)
				{
					json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				AppLogger.LogWarn($"Direct GitHub API check failed: {ex.Message}");
			}

			// 尝试 2：如果直接连接失败且配置了镜像源，尝试通过代理请求
			if (string.IsNullOrEmpty(json) && !string.Equals(proxySource, "direct", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string proxiedApiUrl = GetProxiedDownloadUrl(apiUrl, proxySource, customProxy);
					using HttpResponseMessage proxyResponse = await _httpClient.GetAsync(proxiedApiUrl, ct).ConfigureAwait(false);
					if (proxyResponse.IsSuccessStatusCode)
					{
						json = await proxyResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					AppLogger.LogWarn($"Proxied GitHub API check failed: {ex.Message}");
				}
			}

			if (string.IsNullOrEmpty(json))
			{
				return null;
			}

			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;

			if (isBetaChannel && root.ValueKind == JsonValueKind.Array)
			{
				// 在所有 releases 中筛选最新版本
				ReleaseInfo? bestRelease = null;
				foreach (JsonElement item in root.EnumerateArray())
				{
					ReleaseInfo? rel = ParseReleaseElement(item);
					if (rel == null) continue;

					if (bestRelease == null || (rel.ParsedVersion != null && bestRelease.ParsedVersion != null && rel.ParsedVersion > bestRelease.ParsedVersion))
					{
						bestRelease = rel;
					}
				}
				return bestRelease;
			}
			else if (root.ValueKind == JsonValueKind.Object)
			{
				return ParseReleaseElement(root);
			}

			return null;
		}
		catch (Exception ex)
		{
			AppLogger.LogError("CheckForUpdateAsync exception", ex);
			return null;
		}
	}

	private ReleaseInfo? ParseReleaseElement(JsonElement elem)
	{
		try
		{
			string tagName = elem.TryGetProperty("tag_name", out JsonElement tagElem) ? tagElem.GetString() ?? "" : "";
			string title = elem.TryGetProperty("name", out JsonElement nameElem) ? nameElem.GetString() ?? "" : tagName;
			string body = elem.TryGetProperty("body", out JsonElement bodyElem) ? bodyElem.GetString() ?? "" : "";
			string htmlUrl = elem.TryGetProperty("html_url", out JsonElement urlElem) ? urlElem.GetString() ?? "" : "";
			bool isPrerelease = elem.TryGetProperty("prerelease", out JsonElement preElem) && preElem.GetBoolean();
			DateTime publishedAt = elem.TryGetProperty("published_at", out JsonElement pubElem) && pubElem.TryGetDateTime(out DateTime dt) ? dt : DateTime.Now;

			Version? parsedVer = ParseVersionFromTag(tagName);
			Version currentVer = GetCurrentVersion();
			bool isNewer = parsedVer != null && parsedVer > currentVer;

			ReleaseInfo info = new ReleaseInfo
			{
				TagName = tagName,
				ParsedVersion = parsedVer,
				Title = string.IsNullOrWhiteSpace(title) ? tagName : title,
				Body = body,
				PublishedAt = publishedAt,
				IsPrerelease = isPrerelease,
				HtmlUrl = htmlUrl,
				IsNewerVersion = isNewer
			};

			if (elem.TryGetProperty("assets", out JsonElement assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement asset in assetsElem.EnumerateArray())
				{
					string assetName = asset.TryGetProperty("name", out JsonElement an) ? an.GetString() ?? "" : "";
					string downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement ad) ? ad.GetString() ?? "" : "";
					long size = asset.TryGetProperty("size", out JsonElement asz) ? asz.GetInt64() : 0;

					if (assetName.IndexOf("Standalone", StringComparison.OrdinalIgnoreCase) >= 0 && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					{
						info.StandaloneAssetUrl = downloadUrl;
						info.StandaloneAssetSize = size;
					}
					else if (assetName.IndexOf("Lightweight", StringComparison.OrdinalIgnoreCase) >= 0 && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					{
						info.LightweightAssetUrl = downloadUrl;
						info.LightweightAssetSize = size;
					}
				}
			}

			return info;
		}
		catch (Exception ex)
		{
			AppLogger.LogError("ParseReleaseElement error", ex);
			return null;
		}
	}

	public static Version? ParseVersionFromTag(string tag)
	{
		if (string.IsNullOrWhiteSpace(tag)) return null;
		string clean = tag.Trim().TrimStart('v', 'V');
		int dashIdx = clean.IndexOf('-');
		if (dashIdx > 0)
		{
			clean = clean.Substring(0, dashIdx);
		}
		if (Version.TryParse(clean, out Version? ver))
		{
			return ver;
		}
		return null;
	}

	public async Task DownloadAssetAsync(string downloadUrl, string destinationZipPath, IProgress<UpdateProgressInfo>? progress, CancellationToken ct)
	{
		string tempDir = Path.GetDirectoryName(destinationZipPath)!;
		if (!Directory.Exists(tempDir))
		{
			Directory.CreateDirectory(tempDir);
		}

		if (File.Exists(destinationZipPath))
		{
			try { File.Delete(destinationZipPath); } catch { }
		}

		using HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		long? totalBytes = response.Content.Headers.ContentLength;
		long totalBytesVal = totalBytes.GetValueOrDefault(-1);

		await using Stream contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		await using FileStream fileStream = new FileStream(destinationZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

		byte[] buffer = new byte[65536];
		long totalRead = 0;
		int read;
		Stopwatch stopwatch = Stopwatch.StartNew();
		long lastBytes = 0;
		double lastSpeed = 0;
		DateTime lastSpeedTime = DateTime.UtcNow;

		while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
		{
			await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
			totalRead += read;

			DateTime now = DateTime.UtcNow;
			double elapsedSeconds = (now - lastSpeedTime).TotalSeconds;
			if (elapsedSeconds >= 0.3)
			{
				lastSpeed = (totalRead - lastBytes) / elapsedSeconds;
				lastBytes = totalRead;
				lastSpeedTime = now;
			}

			int percent = totalBytesVal > 0 ? (int)((totalRead * 100) / totalBytesVal) : 0;
			progress?.Report(new UpdateProgressInfo
			{
				Percent = Math.Min(100, percent),
				BytesReceived = totalRead,
				TotalBytesToReceive = totalBytesVal,
				SpeedBytesPerSecond = lastSpeed
			});
		}

		progress?.Report(new UpdateProgressInfo
		{
			Percent = 100,
			BytesReceived = totalRead,
			TotalBytesToReceive = totalRead,
			SpeedBytesPerSecond = 0
		});
	}

	public bool RestartAndApplyUpdate(string downloadedZipPath)
	{
		try
		{
			Process currentProc = Process.GetCurrentProcess();
			int pid = currentProc.Id;
			string currentExe = currentProc.MainModule?.FileName ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
			string targetDir = Path.GetDirectoryName(currentExe)!;

			string scriptPath = Path.Combine(Path.GetTempPath(), $"StarPie_Updater_{pid}.cmd");

			string scriptContent = $@"@echo off
chcp 65001 >nul
echo ========================================================
echo        StarPie 自动更新管理器 (Updating StarPie)
echo ========================================================
echo.
echo [1/3] 等待主进程退出 (PID: {pid})...

:wait_loop
tasklist /fi ""PID eq {pid}"" 2>nul | findstr ""{pid}"" >nul
if %ERRORLEVEL% equ 0 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

echo [2/3] 正在解压新版本文件并覆盖安装目录...
timeout /t 1 /nobreak >nul

tar -xf ""{downloadedZipPath}"" -C ""{targetDir}"" 2>nul
if %ERRORLEVEL% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command ""Expand-Archive -Path '{downloadedZipPath}' -DestinationPath '{targetDir}' -Force""
)

echo [3/3] 重启新版 StarPie...
start """" ""{currentExe}""

echo 更新完成！
del ""{downloadedZipPath}"" 2>nul
(goto) 2>nul & del ""%~f0""
";

			File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.UTF8);

			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = $"/c \"{scriptPath}\"",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = true
			};

			Process.Start(psi);

			AppLogger.LogInfo("Update script launched, shutting down current instance.");
			System.Windows.Application.Current.Dispatcher.Invoke(() =>
			{
				System.Windows.Application.Current.Shutdown();
			});

			return true;
		}
		catch (Exception ex)
		{
			AppLogger.LogError("RestartAndApplyUpdate failed", ex);
			return false;
		}
	}
}
