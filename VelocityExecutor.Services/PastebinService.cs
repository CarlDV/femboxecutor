using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VelocityExecutor.Services;

public static class PastebinService
{
	private const string WORKER_URL = "https://velocity-helper-bot.renern.workers.dev";

	private const int MAX_RETRIES = 3;

	public static async Task<string> UploadScriptAsync(string content)
	{
		string cachedUrl = LoadstringCache.GetCachedUrl(content);
		if (cachedUrl != null)
		{
			return cachedUrl;
		}
		Exception lastException = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using HttpClient client = new HttpClient();
				client.DefaultRequestHeaders.Add("User-Agent", "VelocityExecutor/1.0");
				HttpResponseMessage response = await client.PostAsync("https://velocity-helper-bot.renern.workers.dev/upload", new StringContent(content));
				if (response.IsSuccessStatusCode)
				{
					string rawUrl = (await response.Content.ReadAsStringAsync()).Trim();
					LoadstringCache.AddUpload(content, rawUrl);
					return rawUrl;
				}
				string err = await response.Content.ReadAsStringAsync();
				throw new Exception($"Worker Error: {response.StatusCode} - {err}");
			}
			catch (Exception ex)
			{
				lastException = ex;
				if (attempt < 3)
				{
					await Task.Delay(1000 * attempt);
				}
			}
		}
		throw new Exception($"Failed to upload after {3} attempts: {lastException?.Message}", lastException);
	}

	public static async Task<string> FetchScriptAsync(string url)
	{
		url = NormalizeUrl(url);
		Exception lastException = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using HttpClient client = new HttpClient();
				client.DefaultRequestHeaders.Add("User-Agent", "VelocityExecutor/1.0");
				client.Timeout = TimeSpan.FromSeconds(15.0);
				HttpResponseMessage response = await client.GetAsync(url);
				if (response.IsSuccessStatusCode)
				{
					string content = await response.Content.ReadAsStringAsync();
					LoadstringCache.AddFetch(url, content);
					return content;
				}
				lastException = new Exception($"HTTP {response.StatusCode}");
			}
			catch (HttpRequestException ex)
			{
				lastException = ex;
				if (attempt < 3)
				{
					await Task.Delay(1000 * attempt);
				}
			}
			catch (TaskCanceledException)
			{
				lastException = new Exception("Fetch timed out");
				if (attempt < 3)
				{
					await Task.Delay(1000 * attempt);
				}
			}
			catch (Exception ex3)
			{
				throw new Exception("Failed to fetch script from URL: " + ex3.Message, ex3);
			}
		}
		throw new Exception($"Failed to fetch after {3} attempts: {lastException?.Message}", lastException);
	}

	private static string NormalizeUrl(string url)
	{
		url = url.Trim();
		if (url.Contains("pastebin.com/") && !url.Contains("/raw/"))
		{
			Match match = Regex.Match(url, "pastebin\\.com/([a-zA-Z0-9]+)");
			if (match.Success)
			{
				return "https://pastebin.com/raw/" + match.Groups[1].Value;
			}
		}
		if (url.StartsWith("http://"))
		{
			url = "https://" + url.Substring(7);
		}
		else if (!url.StartsWith("https://"))
		{
			url = "https://" + url;
		}
		return url;
	}

	public static string? ExtractUrlFromLoadstring(string content)
	{
		string[] array = new string[7] { "loadstring\\s*\\(\\s*game\\s*:\\s*HttpGet\\s*\\(\\s*[\"']([^\"']+)[\"']\\s*\\)\\s*\\)\\s*\\(\\s*\\)", "game\\s*:\\s*HttpGet\\s*\\(\\s*[\"']([^\"']+)[\"']\\s*\\)", "game\\s*:\\s*HttpGetAsync\\s*\\(\\s*[\"']([^\"']+)[\"']\\s*\\)", "require\\s*\\(\\s*\\d+\\s*\\)\\s*\\(\\s*[\"']([^\"']+)[\"']\\s*\\)", "getfenv\\s*\\(\\s*\\)\\s*\\.loadstring\\s*\\(\\s*[\"']([^\"']+)[\"']\\s*\\)", "(https?://[^\\s\"'<>]+)", "[\"']([^\\s\"']+(?:pastebin|rentry|githubusercontent|rawgit)[^\\s\"']*)[\"']" };
		foreach (string pattern in array)
		{
			Match match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
			if (match.Success && match.Groups.Count > 1)
			{
				string url = match.Groups[1].Value;
				if (url.Contains("://") || url.Contains("pastebin") || url.Contains("rentry"))
				{
					return url;
				}
			}
		}
		return null;
	}

	public static (bool isValid, string? error) ValidateLuaSyntax(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return (isValid: false, error: "Script is empty");
		}
		List<string> errors = new List<string>();
		int parenCount = 0;
		int braceCount = 0;
		int bracketCount = 0;
		bool inString = false;
		char stringChar = '\0';
		for (int i = 0; i < content.Length; i++)
		{
			char c = content[i];
			if ((c == '"' || c == '\'') && (i == 0 || content[i - 1] != '\\'))
			{
				if (!inString)
				{
					inString = true;
					stringChar = c;
				}
				else if (c == stringChar)
				{
					inString = false;
				}
			}
			if (!inString)
			{
				switch (c)
				{
				case '(':
					parenCount++;
					break;
				case ')':
					parenCount--;
					break;
				case '{':
					braceCount++;
					break;
				case '}':
					braceCount--;
					break;
				case '[':
					bracketCount++;
					break;
				case ']':
					bracketCount--;
					break;
				}
			}
		}
		if (parenCount != 0)
		{
			errors.Add("Unbalanced parentheses");
		}
		if (braceCount != 0)
		{
			errors.Add("Unbalanced braces");
		}
		if (bracketCount != 0)
		{
			errors.Add("Unbalanced brackets");
		}
		if (inString)
		{
			errors.Add("Unclosed string");
		}
		if (Regex.IsMatch(content, "\\bend\\s+end\\b"))
		{
			errors.Add("Multiple consecutive 'end' statements");
		}
		if (errors.Any())
		{
			return (isValid: false, error: string.Join(", ", errors));
		}
		return (isValid: true, error: null);
	}
}
