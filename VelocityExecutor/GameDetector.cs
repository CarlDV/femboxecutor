using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VelocityExecutor;

public class GameDetector
{
	private string _logDir;

	private string _lastLogFile = "";

	private DispatcherTimer _timer;

	private HttpClient _http;

	public string CurrentGameName { get; private set; } = "Unknown Game";

	public string CurrentGameIconUrl { get; private set; } = "";

	public long CurrentPlaceId { get; private set; }

	public event EventHandler OnGameChanged;

	public GameDetector()
	{
		_logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
		_http = new HttpClient();
		_timer = new DispatcherTimer();
		_timer.Interval = TimeSpan.FromSeconds(5.0);
		_timer.Tick += CheckLogs;
		CheckLogs(null, null);
		_timer.Start();
	}

	private async void CheckLogs(object? sender, EventArgs? e)
	{
		try
		{
			if (!Directory.Exists(_logDir))
			{
				return;
			}
			FileInfo latestLog = (from f in new DirectoryInfo(_logDir).GetFiles("*.log")
				orderby f.LastWriteTime descending
				select f).FirstOrDefault();
			if (latestLog == null)
			{
				return;
			}
			if (latestLog.FullName != _lastLogFile)
			{
				_lastLogFile = latestLog.FullName;
				CurrentPlaceId = 0L;
				CurrentGameName = "Roblox";
				CurrentGameIconUrl = "";
				this.OnGameChanged?.Invoke(this, EventArgs.Empty);
				try
				{
					File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] New log detected: {latestLog.Name}. State reset.\n");
				}
				catch
				{
				}
			}
			await ParseLog(_lastLogFile);
		}
		catch (Exception ex)
		{
			try
			{
				File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] CheckLogs Error: {ex.Message}\n");
			}
			catch
			{
			}
		}
	}

	private async Task ParseLog(string path)
	{
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using StreamReader reader = new StreamReader(stream);
			string content = await reader.ReadToEndAsync();
			string[] obj = new string[3] { "placeid:?\\s*(\\d+)", "place\\s+(\\d+)", "placeId\"\\s*:\\s*(\\d+)" };
			Match bestMatch = null;
			string[] array = obj;
			foreach (string pattern in array)
			{
				MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
				if (matches.Count > 0)
				{
					Match lastMatch = matches[matches.Count - 1];
					if (bestMatch == null || lastMatch.Index > bestMatch.Index)
					{
						bestMatch = lastMatch;
					}
				}
			}
			if (bestMatch != null && long.TryParse(bestMatch.Groups[1].Value, out var placeId))
			{
				try
				{
					File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] Found PlaceId: {placeId} in {Path.GetFileName(path)}\n");
				}
				catch
				{
				}
				if (placeId != CurrentPlaceId)
				{
					CurrentPlaceId = placeId;
					await FetchGameDetails(placeId);
				}
			}
			else
			{
				try
				{
					File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] No PlaceId found in {Path.GetFileName(path)}\n");
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			try
			{
				File.AppendAllText("c:\\test\\debug_game_detection.txt", $"[{DateTime.Now}] Parse Error: {ex.Message}\n");
			}
			catch
			{
			}
		}
	}

	private async Task FetchGameDetails(long placeId)
	{
		try
		{
			string universeUrl = $"https://apis.roblox.com/universes/v1/places/{placeId}/universe";
			string json = await _http.GetStringAsync(universeUrl);
			long universeId = 0L;
			using (JsonDocument doc = JsonDocument.Parse(json))
			{
				if (doc.RootElement.TryGetProperty("universeId", out var uIdProp))
				{
					universeId = uIdProp.GetInt64();
				}
			}
			if (universeId == 0L)
			{
				CurrentGameName = "Unknown Game";
				CurrentGameIconUrl = "";
				this.OnGameChanged?.Invoke(this, EventArgs.Empty);
				return;
			}
			string gameUrl = $"https://games.roblox.com/v1/games?universeIds={universeId}";
			using (JsonDocument doc2 = JsonDocument.Parse(await _http.GetStringAsync(gameUrl)))
			{
				if (doc2.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 && data[0].TryGetProperty("name", out var nameProp))
				{
					CurrentGameName = nameProp.GetString() ?? "Unknown";
				}
			}
			try
			{
				string iconUrl = $"https://thumbnails.roblox.com/v1/games/icons?universeIds={universeId}&returnPolicy=PlaceHolder&size=512x512&format=Png&isCircular=false";
				using JsonDocument doc3 = JsonDocument.Parse(await _http.GetStringAsync(iconUrl));
				if (doc3.RootElement.TryGetProperty("data", out var data2) && data2.ValueKind == JsonValueKind.Array && data2.GetArrayLength() > 0 && data2[0].TryGetProperty("imageUrl", out var urlProp))
				{
					CurrentGameIconUrl = urlProp.GetString() ?? "";
				}
			}
			catch
			{
				CurrentGameIconUrl = "";
			}
			this.OnGameChanged?.Invoke(this, EventArgs.Empty);
		}
		catch
		{
			CurrentGameName = "Unknown Game";
			CurrentGameIconUrl = "";
			this.OnGameChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
