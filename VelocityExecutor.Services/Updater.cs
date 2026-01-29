using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace VelocityExecutor.Services;

public static class Updater
{
	private const string REPO_OWNER = "CarlDV";

	private const string REPO_NAME = "femboxecutor";

	private const string GITHUB_API = "https://api.github.com/repos/CarlDV/femboxecutor/releases/latest";

	private const string CONFIG_URL = "https://raw.githubusercontent.com/CarlDV/femboxecutor/main/update-config.json";

	public static async Task CheckRemoteForceUpdate(string currentVersion, bool autoUpdate = false)
	{
		try
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VelocityExecutor", "1.0"));
			HttpResponseMessage configResponse = await client.GetAsync("https://raw.githubusercontent.com/CarlDV/femboxecutor/main/update-config.json");
			if (configResponse.IsSuccessStatusCode && (JsonSerializer.Deserialize<UpdateConfig>(await configResponse.Content.ReadAsStringAsync())?.ForceUpdate ?? false))
			{
				await SilentUpdate(currentVersion);
				return;
			}
		}
		catch (Exception)
		{
		}
		await CheckForUpdatesAsync(currentVersion, autoUpdate);
	}

	private static async Task SilentUpdate(string currentVersion)
	{
		_ = 2;
		try
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VelocityExecutor", "1.0"));
			HttpResponseMessage response = await client.GetAsync("https://api.github.com/repos/CarlDV/femboxecutor/releases/latest");
			if (!response.IsSuccessStatusCode)
			{
				return;
			}
			using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			JsonElement root = doc.RootElement;
			string tag = root.GetProperty("tag_name").GetString();
			string input = (tag.StartsWith("v") ? tag.Substring(1) : tag);
			string cleanCurrent = (currentVersion.StartsWith("v") ? currentVersion.Substring(1) : currentVersion);
			if (!Version.TryParse(input, out Version vTag) || !Version.TryParse(cleanCurrent, out Version vCurrent) || !(vTag > vCurrent))
			{
				return;
			}
			string downloadUrl = null;
			if (root.TryGetProperty("assets", out var assets))
			{
				foreach (JsonElement asset in assets.EnumerateArray())
				{
					if (asset.GetProperty("name").GetString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					{
						downloadUrl = asset.GetProperty("browser_download_url").GetString();
						break;
					}
				}
			}
			if (!string.IsNullOrEmpty(downloadUrl))
			{
				File.WriteAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "pending_update.txt"), tag);
				await DownloadAndInstall(downloadUrl, silent: true);
			}
		}
		catch (Exception)
		{
		}
	}

	public static async Task CheckForUpdatesAsync(string currentVersion, bool autoUpdate = false)
	{
		_ = 2;
		try
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VelocityExecutor", "1.0"));
			HttpResponseMessage response = await client.GetAsync("https://api.github.com/repos/CarlDV/femboxecutor/releases/latest");
			if (!response.IsSuccessStatusCode)
			{
				return;
			}
			using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			JsonElement root = doc.RootElement;
			string tag = root.GetProperty("tag_name").GetString();
			string input = (tag.StartsWith("v") ? tag.Substring(1) : tag);
			string cleanCurrent = (currentVersion.StartsWith("v") ? currentVersion.Substring(1) : currentVersion);
			if (!Version.TryParse(input, out Version vTag) || !Version.TryParse(cleanCurrent, out Version vCurrent) || !(vTag > vCurrent))
			{
				return;
			}
			bool shouldUpdate = autoUpdate;
			if (!autoUpdate)
			{
				shouldUpdate = MessageBox.Show("New version " + tag + " is available. Update now?", "Update Found", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes;
			}
			if (!shouldUpdate)
			{
				return;
			}
			string downloadUrl = null;
			if (root.TryGetProperty("assets", out var assets))
			{
				foreach (JsonElement asset in assets.EnumerateArray())
				{
					if (asset.GetProperty("name").GetString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					{
						downloadUrl = asset.GetProperty("browser_download_url").GetString();
						break;
					}
				}
			}
			if (!string.IsNullOrEmpty(downloadUrl))
			{
				File.WriteAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "pending_update.txt"), tag);
				await DownloadAndInstall(downloadUrl, autoUpdate);
			}
			else if (!autoUpdate)
			{
				MessageBox.Show("Could not find a .zip asset in the latest release.", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		catch (Exception)
		{
		}
	}

	public static async Task ManualCheck(string currentVersion)
	{
		_ = 3;
		try
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VelocityExecutor", "1.0"));
			HttpResponseMessage response = await client.GetAsync("https://api.github.com/repos/CarlDV/femboxecutor/releases/latest");
			if (!response.IsSuccessStatusCode)
			{
				MessageBox.Show("Could not connect to update server.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			JsonElement root = doc.RootElement;
			root.GetProperty("tag_name").GetString();
			string tag = root.GetProperty("tag_name").GetString();
			string input = (tag.StartsWith("v") ? tag.Substring(1) : tag);
			string cleanCurrent = (currentVersion.StartsWith("v") ? currentVersion.Substring(1) : currentVersion);
			if (Version.TryParse(input, out Version vTag) && Version.TryParse(cleanCurrent, out Version vCurrent))
			{
				if (vTag > vCurrent)
				{
					await CheckForUpdatesAsync(currentVersion);
				}
				else
				{
					MessageBox.Show("You are using the latest version.", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			else if (tag != currentVersion)
			{
				await CheckForUpdatesAsync(currentVersion);
			}
			else
			{
				MessageBox.Show("You are using the latest version.", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Check failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public static void CheckPostUpdateNotification()
	{
		try
		{
			string pendingUpdateFile = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "pending_update.txt");
			if (File.Exists(pendingUpdateFile))
			{
				string version = File.ReadAllText(pendingUpdateFile).Trim();
				File.Delete(pendingUpdateFile);
				MessageBox.Show("Updated to " + version + "!\n\nJoin our Discord for support and updates!", "Update Successful", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://discord.gg/FfT5Nn7X7m",
					UseShellExecute = true
				});
			}
		}
		catch
		{
		}
	}

	private static async Task DownloadAndInstall(string url, bool silent)
	{
		_ = 2;
		try
		{
			string tempPath = Path.GetTempPath();
			string zipPath = Path.Combine(tempPath, "VelocityUpdate.zip");
			string extractPath = Path.Combine(tempPath, "VelocityUpdateExtracted");
			if (Directory.Exists(extractPath))
			{
				Directory.Delete(extractPath, recursive: true);
			}
			if (File.Exists(zipPath))
			{
				File.Delete(zipPath);
			}
			using (HttpClient client = new HttpClient())
			{
				await File.WriteAllBytesAsync(zipPath, await client.GetByteArrayAsync(url));
			}
			ZipFile.ExtractToDirectory(zipPath, extractPath);
			string currentExe = Process.GetCurrentProcess().MainModule.FileName;
			string currentDir = Path.GetDirectoryName(currentExe);
			string batchPath = Path.Combine(currentDir, "update.bat");
			string batchScript = $"\r\n@echo off\r\ntimeout /t 2 /nobreak > NUL\r\ntaskkill /F /IM \"{Path.GetFileName(currentExe)}\" > NUL 2>&1\r\nxcopy /Y /E \"{extractPath}\\*\" \"{currentDir}\"\r\nstart \"\" \"{currentExe}\"\r\ndel \"%~f0\"\r\n";
			await File.WriteAllTextAsync(batchPath, batchScript);
			Process.Start(new ProcessStartInfo
			{
				FileName = batchPath,
				UseShellExecute = true,
				CreateNoWindow = true
			});
			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			if (!silent)
			{
				MessageBox.Show("Update installation failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}
}
