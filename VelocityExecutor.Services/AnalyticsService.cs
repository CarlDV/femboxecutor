using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;

namespace VelocityExecutor.Services;

public static class AnalyticsService
{
	private const string WORKER_URL = "https://velocity-helper-bot.renern.workers.dev";

	private static readonly HttpClient _client = new HttpClient();

	private static AppSettings _settings;

	private static DateTime _sessionStart = DateTime.MinValue;

	private static Timer _heartbeatTimer;

	private static string _authToken;

	public static void Initialize(AppSettings settings)
	{
		_settings = settings;
		string regValue = "UserId";
		string oldId = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Femboxecutor", regValue, null);
		string deviceId = (string.IsNullOrEmpty(oldId) ? HardwareID.GetDeviceID() : oldId);
		if (_settings.AnalyticsUserId != deviceId)
		{
			_settings.AnalyticsUserId = deviceId;
			_settings.Save();
		}
	}

	public static async void StartSession()
	{
		await StartSessionAsync();
	}

	public static async Task<string> StartSessionAsync()
	{
		try
		{
			if (_settings == null)
			{
				return null;
			}
			_sessionStart = DateTime.UtcNow;
			if (JsonSerializer.Deserialize<JsonElement>(await (await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/api/session/start/" + _settings.AnalyticsUserId, null)).Content.ReadAsStringAsync()).TryGetProperty("authToken", out var token))
			{
				_authToken = token.GetString();
				_settings.ProfileAuthToken = _authToken;
				_settings.Save();
				return _authToken;
			}
			_heartbeatTimer = new Timer(300000.0);
			_heartbeatTimer.Elapsed += async delegate
			{
				await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/api/session/heartbeat/" + _settings.AnalyticsUserId, null);
			};
			_heartbeatTimer.Start();
			return _authToken;
		}
		catch
		{
			return null;
		}
	}

	public static async void EndSession()
	{
		try
		{
			if (_settings != null && !(_sessionStart == DateTime.MinValue))
			{
				_heartbeatTimer?.Stop();
				_heartbeatTimer?.Dispose();
				StringContent content = new StringContent(JsonSerializer.Serialize(new
				{
					durationMinutes = (int)(DateTime.UtcNow - _sessionStart).TotalMinutes
				}), Encoding.UTF8, "application/json");
				await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/api/session/end/" + _settings.AnalyticsUserId, content);
			}
		}
		catch
		{
		}
	}

	public static async void Track(string action, string details)
	{
		try
		{
			if (_settings != null)
			{
				string json = JsonSerializer.Serialize(new
				{
					user_id = _settings.AnalyticsUserId,
					username = Environment.UserName,
					action = action,
					details = details,
					version = "v2.0.2"
				});
				await _client.PostAsync("https://velocity-helper-bot.renern.workers.dev/analytics", new StringContent(json, Encoding.UTF8, "application/json"));
			}
		}
		catch
		{
		}
	}

	public static void TrackScript(string scriptContent, string action = "Execute", string gameName = null)
	{
		string hash = LoadstringCache.ComputeHash(scriptContent);
		string shortHash = hash.Substring(0, Math.Min(12, hash.Length));
		string details = "Script:" + shortHash;
		if (!string.IsNullOrEmpty(gameName))
		{
			details = "Script:" + shortHash + "|Game:" + gameName;
		}
		Track(action, details);
	}

	public static void TrackError(string errorType, string details)
	{
		Track("Error", errorType + ": " + details);
	}

	public static void TrackWarning(string warningType, string details)
	{
		Track("Warning", warningType + ": " + details);
	}
}
