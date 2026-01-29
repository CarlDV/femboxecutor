using System;
using System.IO;
using System.Text.Json;
using VelocityExecutor.Services;

namespace VelocityExecutor;

public class AppSettings
{
	private static AppSettings _instance;

	public static AppSettings Instance => _instance ?? (_instance = Load());

	public bool RpcEnabled { get; set; } = true;

	public string RpcDetails { get; set; } = "Scripting";

	public string RpcState { get; set; } = "Fembox";

	public bool RpcTimestamp { get; set; } = true;

	public bool ShowGameInRpc { get; set; } = true;

	public string RpcStyleText { get; set; } = "Velocity Reskinned";

	public bool AutoAttach { get; set; }

	public bool AutoUpdate { get; set; }

	public string BackgroundImagePath { get; set; } = "Images/background.png";

	public string SettingsBackground { get; set; } = "";

	public string ConsoleBackground { get; set; } = "";

	public string CustomTitle { get; set; } = "Fembox";

	public bool TopMost { get; set; }

	public string BorderColor { get; set; } = "#FF69B4";

	public double PanelOpacity { get; set; } = 0.4;

	public double WindowOpacity { get; set; } = 0.95;

	public string AnalyticsUserId { get; set; } = "";

	public string ProfileAuthToken { get; set; } = "";

	public string ButtonColor { get; set; } = "#00000000";

	public bool CloudSyncEnabled { get; set; } = true;

	public DateTime? LastCloudSync { get; set; }

	public bool AutoSyncOnStartup { get; set; } = true;

	public bool AutoSyncOnChange { get; set; } = true;

	private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor", "settings.json");

	public static AppSettings Load()
	{
		try
		{
			if (File.Exists(SettingsPath))
			{
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	public void Save()
	{
		try
		{
			string dir = Path.GetDirectoryName(SettingsPath);
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(SettingsPath, json);
			if (CloudSyncEnabled && AutoSyncOnChange)
			{
				CloudSyncService.UploadSettings();
			}
		}
		catch
		{
		}
	}
}
