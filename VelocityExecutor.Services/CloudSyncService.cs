using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VelocityExecutor.Services;

public static class CloudSyncService
{
	private const string WORKER_URL = "https://velocity-helper-bot.renern.workers.dev";

	private static readonly HttpClient _client = new HttpClient();

	private static SyncStatus _status = new SyncStatus();

	private static bool _autoSyncEnabled = true;

	public static async Task<bool> UploadSettings()
	{
		try
		{
			_status.IsUploading = true;
			_status.ErrorMessage = null;
			string deviceId = HardwareID.GetDeviceID();
			CloudSettingsData cloudSettingsData = new CloudSettingsData();
			cloudSettingsData.DeviceId = deviceId;
			cloudSettingsData.DeviceName = Environment.MachineName;
			cloudSettingsData.Settings = AppSettings.Instance;
			cloudSettingsData.FemboxecutorFiles = GetFemboxecutorFiles();
			cloudSettingsData.WorkspaceFiles = GetWorkspaceFiles();
			cloudSettingsData.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			cloudSettingsData.Version = "v2.0.2";
			cloudSettingsData.Settings.ProfileAuthToken = null;
			string json = JsonSerializer.Serialize(cloudSettingsData, new JsonSerializerOptions
			{
				WriteIndented = false,
				DefaultIgnoreCondition = JsonIgnoreCondition.Never
			});
			HttpResponseMessage response = await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/sync/settings/" + deviceId, new StringContent(json, Encoding.UTF8, "application/json"));
			if (response.IsSuccessStatusCode)
			{
				_status.IsSynced = true;
				_status.LastSyncTime = DateTime.Now;
				_status.IsUploading = false;
				return true;
			}
			_status.ErrorMessage = $"Upload failed: {response.StatusCode}";
			_status.IsUploading = false;
			return false;
		}
		catch (Exception ex)
		{
			_status.ErrorMessage = "Upload error: " + ex.Message;
			_status.IsUploading = false;
			return false;
		}
	}

	public static async Task<bool> DownloadSettings(bool applyImmediately = false)
	{
		try
		{
			string deviceId = HardwareID.GetDeviceID();
			HttpResponseMessage response = await _client.GetAsync("https://velocity-helper-bot.renern.workers.dev/sync/settings/" + deviceId);
			if (!response.IsSuccessStatusCode)
			{
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					return false;
				}
				_status.ErrorMessage = $"Download failed: {response.StatusCode}";
				return false;
			}
			CloudSettingsData data = JsonSerializer.Deserialize<CloudSettingsData>(await response.Content.ReadAsStringAsync());
			if (data == null)
			{
				_status.ErrorMessage = "Invalid cloud data";
				return false;
			}
			if (applyImmediately)
			{
				ApplyCloudSettings(data);
			}
			_status.IsSynced = true;
			_status.LastSyncTime = DateTime.Now;
			return true;
		}
		catch (Exception ex)
		{
			_status.ErrorMessage = "Download error: " + ex.Message;
			return false;
		}
	}

	public static async Task<bool> MergeSyncFiles()
	{
		try
		{
			_status.State = SyncState.Syncing;
			_status.ErrorMessage = null;
			_status.FilesUploaded = 0;
			_status.FilesDownloaded = 0;
			string deviceId = HardwareID.GetDeviceID();
			HttpResponseMessage cloudResponse = await _client.GetAsync("https://velocity-helper-bot.renern.workers.dev/sync/settings/" + deviceId);
			CloudSettingsData cloudData = null;
			if (cloudResponse.IsSuccessStatusCode)
			{
				cloudData = JsonSerializer.Deserialize<CloudSettingsData>(await cloudResponse.Content.ReadAsStringAsync());
			}
			List<SyncedFile> localFembox = GetFemboxecutorFiles();
			List<SyncedFile> localWorkspace = GetWorkspaceFiles();
			List<SyncedFile> mergedFembox = new List<SyncedFile>();
			List<SyncedFile> mergedWorkspace = new List<SyncedFile>();
			if (cloudData?.FemboxecutorFiles != null)
			{
				mergedFembox.AddRange(cloudData.FemboxecutorFiles);
				foreach (SyncedFile cloudFile in cloudData.FemboxecutorFiles)
				{
					if (!localFembox.Any((SyncedFile f) => f.RelativePath == cloudFile.RelativePath))
					{
						RestoreSingleFemboxFile(cloudFile);
						_status.FilesDownloaded++;
					}
				}
				foreach (SyncedFile localFile in localFembox)
				{
					if (!cloudData.FemboxecutorFiles.Any((SyncedFile f) => f.RelativePath == localFile.RelativePath))
					{
						mergedFembox.Add(localFile);
						_status.FilesUploaded++;
					}
				}
			}
			else
			{
				mergedFembox = localFembox;
				_status.FilesUploaded = localFembox.Count;
			}
			if (cloudData?.WorkspaceFiles != null)
			{
				mergedWorkspace.AddRange(cloudData.WorkspaceFiles);
				foreach (SyncedFile cloudFileWorkspace in cloudData.WorkspaceFiles)
				{
					if (!localWorkspace.Any((SyncedFile f) => f.RelativePath == cloudFileWorkspace.RelativePath))
					{
						RestoreSingleWorkspaceFile(cloudFileWorkspace);
						_status.FilesDownloaded++;
					}
				}
				foreach (SyncedFile localFileWorkspace in localWorkspace)
				{
					if (!cloudData.WorkspaceFiles.Any((SyncedFile f) => f.RelativePath == localFileWorkspace.RelativePath))
					{
						mergedWorkspace.Add(localFileWorkspace);
						_status.FilesUploaded++;
					}
				}
			}
			else
			{
				mergedWorkspace = localWorkspace;
				_status.FilesUploaded += localWorkspace.Count;
			}
			string uploadJson = JsonSerializer.Serialize(new CloudSettingsData
			{
				DeviceId = deviceId,
				DeviceName = Environment.MachineName,
				Settings = AppSettings.Instance,
				FemboxecutorFiles = mergedFembox,
				WorkspaceFiles = mergedWorkspace,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				Version = "v2.0.2"
			});
			HttpResponseMessage uploadResponse = await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/sync/settings/" + deviceId, new StringContent(uploadJson, Encoding.UTF8, "application/json"));
			if (uploadResponse.IsSuccessStatusCode)
			{
				_status.State = SyncState.Synced;
				_status.IsSynced = true;
				_status.LastSyncTime = DateTime.Now;
				return true;
			}
			_status.State = SyncState.Error;
			_status.ErrorMessage = $"Upload failed: {uploadResponse.StatusCode}";
			return false;
		}
		catch (Exception ex)
		{
			_status.State = SyncState.Error;
			_status.ErrorMessage = "Merge sync error: " + ex.Message;
			return false;
		}
	}

	private static void ApplyCloudSettings(CloudSettingsData data)
	{
		try
		{
			AppSettings settings = AppSettings.Instance;
			AppSettings cloudSettings = data.Settings;
			PropertyInfo[] properties = typeof(AppSettings).GetProperties();
			foreach (PropertyInfo prop in properties)
			{
				if (prop.CanWrite && prop.Name != "Instance" && prop.Name != "ProfileAuthToken" && prop.Name != "AnalyticsUserId")
				{
					try
					{
						object value = prop.GetValue(cloudSettings);
						prop.SetValue(settings, value);
					}
					catch
					{
					}
				}
			}
			settings.Save();
			if (data.FemboxecutorFiles != null && data.FemboxecutorFiles.Any())
			{
				RestoreFemboxecutorFiles(data.FemboxecutorFiles);
			}
			if (data.WorkspaceFiles != null && data.WorkspaceFiles.Any())
			{
				RestoreWorkspaceFiles(data.WorkspaceFiles);
			}
		}
		catch (Exception ex)
		{
			_status.ErrorMessage = "Apply error: " + ex.Message;
		}
	}

	private static List<SyncedFile> GetFemboxecutorFiles()
	{
		List<SyncedFile> files = new List<SyncedFile>();
		try
		{
			string femboxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor");
			if (Directory.Exists(femboxDir))
			{
				string[] allFiles = Directory.GetFiles(femboxDir, "*", SearchOption.AllDirectories);
				foreach (string file in allFiles)
				{
					try
					{
						string relativePath = Path.GetRelativePath(femboxDir, file);
						bool isBinary = IsBinaryFile(file);
						string content = (isBinary ? Convert.ToBase64String(File.ReadAllBytes(file)) : File.ReadAllText(file));
						files.Add(new SyncedFile
						{
							RelativePath = relativePath,
							Content = content,
							IsBinary = isBinary
						});
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return files;
	}

	private static List<SyncedFile> GetWorkspaceFiles()
	{
		List<SyncedFile> files = new List<SyncedFile>();
		try
		{
			string workspaceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workspace");
			if (Directory.Exists(workspaceDir))
			{
				string[] allFiles = Directory.GetFiles(workspaceDir, "*", SearchOption.AllDirectories);
				foreach (string file in allFiles)
				{
					try
					{
						string relativePath = Path.GetRelativePath(workspaceDir, file);
						bool isBinary = IsBinaryFile(file);
						string content = (isBinary ? Convert.ToBase64String(File.ReadAllBytes(file)) : File.ReadAllText(file));
						files.Add(new SyncedFile
						{
							RelativePath = relativePath,
							Content = content,
							IsBinary = isBinary
						});
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return files;
	}

	private static void RestoreFemboxecutorFiles(List<SyncedFile> files)
	{
		try
		{
			string femboxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor");
			Directory.CreateDirectory(femboxDir);
			foreach (SyncedFile file in files)
			{
				try
				{
					string fullPath = Path.Combine(femboxDir, file.RelativePath);
					string dir = Path.GetDirectoryName(fullPath);
					if (!string.IsNullOrEmpty(dir))
					{
						Directory.CreateDirectory(dir);
					}
					if (file.IsBinary)
					{
						byte[] bytes = Convert.FromBase64String(file.Content);
						File.WriteAllBytes(fullPath, bytes);
					}
					else
					{
						File.WriteAllText(fullPath, file.Content);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static void RestoreWorkspaceFiles(List<SyncedFile> files)
	{
		try
		{
			string workspaceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workspace");
			Directory.CreateDirectory(workspaceDir);
			foreach (SyncedFile file in files)
			{
				try
				{
					string fullPath = Path.Combine(workspaceDir, file.RelativePath);
					string dir = Path.GetDirectoryName(fullPath);
					if (!string.IsNullOrEmpty(dir))
					{
						Directory.CreateDirectory(dir);
					}
					if (file.IsBinary)
					{
						byte[] bytes = Convert.FromBase64String(file.Content);
						File.WriteAllBytes(fullPath, bytes);
					}
					else
					{
						File.WriteAllText(fullPath, file.Content);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static void RestoreSingleFemboxFile(SyncedFile file)
	{
		try
		{
			string fullPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor"), file.RelativePath);
			string dir = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			if (file.IsBinary)
			{
				byte[] bytes = Convert.FromBase64String(file.Content);
				File.WriteAllBytes(fullPath, bytes);
			}
			else
			{
				File.WriteAllText(fullPath, file.Content);
			}
		}
		catch
		{
		}
	}

	private static void RestoreSingleWorkspaceFile(SyncedFile file)
	{
		try
		{
			string fullPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workspace"), file.RelativePath);
			string dir = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			if (file.IsBinary)
			{
				byte[] bytes = Convert.FromBase64String(file.Content);
				File.WriteAllBytes(fullPath, bytes);
			}
			else
			{
				File.WriteAllText(fullPath, file.Content);
			}
		}
		catch
		{
		}
	}

	private static bool IsBinaryFile(string filePath)
	{
		switch (Path.GetExtension(filePath).ToLowerInvariant())
		{
		case ".exe":
		case ".dll":
		case ".png":
		case ".jpg":
		case ".gif":
		case ".bmp":
		case ".ico":
		case ".zip":
		case ".rar":
		case ".mp3":
		case ".mp4":
		case ".avi":
		case ".wav":
		case ".jpeg":
		case ".webp":
		case ".7z":
			return true;
		default:
			return false;
		}
	}

	public static SyncStatus GetSyncStatus()
	{
		return _status;
	}

	public static void SetAutoSync(bool enabled)
	{
		_autoSyncEnabled = enabled;
	}

	public static bool IsAutoSyncEnabled()
	{
		return _autoSyncEnabled;
	}

	public static async Task<bool> HasCloudSettings()
	{
		try
		{
			string deviceId = HardwareID.GetDeviceID();
			return (await _client.GetAsync("https://velocity-helper-bot.renern.workers.dev/sync/settings/" + deviceId)).IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
