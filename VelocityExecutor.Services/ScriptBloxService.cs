using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VelocityExecutor.Services;

public class ScriptBloxService
{
	private readonly HttpClient _httpClient;

	private const string BASE_URL = "https://scriptblox.com/api/script";

	public ScriptBloxService()
	{
		_httpClient = new HttpClient();
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "VelocityExecutor/1.0");
	}

	public async Task<ScriptBloxResponse> FetchRecentScriptsAsync(int page = 1)
	{
		_ = 1;
		try
		{
			string url = $"{"https://scriptblox.com/api/script"}/fetch?page={page}";
			HttpResponseMessage obj = await _httpClient.GetAsync(url);
			obj.EnsureSuccessStatusCode();
			return JsonSerializer.Deserialize<ScriptBloxResponse>(await obj.Content.ReadAsStringAsync());
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error fetching recent scripts: " + ex.Message);
			return null;
		}
	}

	public async Task<ScriptBloxResponse> SearchScriptsAsync(string query, int page = 1, string mode = "free")
	{
		_ = 1;
		try
		{
			string encodedQuery = WebUtility.UrlEncode(query);
			string url = $"{"https://scriptblox.com/api/script"}/search?q={encodedQuery}&page={page}&mode={mode}";
			HttpResponseMessage obj = await _httpClient.GetAsync(url);
			obj.EnsureSuccessStatusCode();
			return JsonSerializer.Deserialize<ScriptBloxResponse>(await obj.Content.ReadAsStringAsync());
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error searching scripts: " + ex.Message);
			return null;
		}
	}
}
