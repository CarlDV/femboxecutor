using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VelocityExecutor.Services;

public static class LoadstringCache
{
	private static readonly string CacheFilePath;

	private static List<LoadstringHistoryEntry> _history;

	private const int MaxHistorySize = 100;

	static LoadstringCache()
	{
		CacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Femboxecutor", "loadstring_cache.json");
		_history = new List<LoadstringHistoryEntry>();
		LoadFromDisk();
	}

	public static string ComputeHash(string content)
	{
		using SHA256 sha256 = SHA256.Create();
		return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(content.Trim())));
	}

	public static string? GetCachedUrl(string content)
	{
		string hash = ComputeHash(content);
		return _history.FirstOrDefault((LoadstringHistoryEntry e) => e.ContentHash == hash && e.Type == "uploaded" && !string.IsNullOrEmpty(e.Url))?.Url;
	}

	public static void AddUpload(string content, string url)
	{
		string hash = ComputeHash(content);
		_history.RemoveAll((LoadstringHistoryEntry e) => e.ContentHash == hash || e.Url == url);
		_history.Insert(0, new LoadstringHistoryEntry
		{
			Url = url,
			ContentHash = hash,
			Timestamp = DateTime.Now,
			ContentLength = content.Length,
			Type = "uploaded"
		});
		TrimHistory();
		SaveToDisk();
	}

	public static void AddFetch(string url, string content)
	{
		string hash = ComputeHash(content);
		_history.RemoveAll((LoadstringHistoryEntry e) => e.Url == url);
		_history.Insert(0, new LoadstringHistoryEntry
		{
			Url = url,
			ContentHash = hash,
			Timestamp = DateTime.Now,
			ContentLength = content.Length,
			Type = "fetched"
		});
		TrimHistory();
		SaveToDisk();
	}

	public static List<LoadstringHistoryEntry> GetAllHistory()
	{
		return _history.ToList();
	}

	public static List<LoadstringHistoryEntry> GetHistory(int count = 20)
	{
		return _history.Take(count).ToList();
	}

	public static List<LoadstringHistoryEntry> GetFavorites()
	{
		return (from e in _history
			where e.IsFavorite
			orderby e.Timestamp descending
			select e).ToList();
	}

	public static bool ToggleFavorite(string url, string? customName = null, string? tags = null)
	{
		LoadstringHistoryEntry entry = _history.FirstOrDefault((LoadstringHistoryEntry e) => e.Url == url);
		if (entry != null)
		{
			entry.IsFavorite = !entry.IsFavorite;
			if (entry.IsFavorite)
			{
				if (!string.IsNullOrEmpty(customName))
				{
					entry.Name = customName;
				}
				if (!string.IsNullOrEmpty(tags))
				{
					entry.Tags = tags;
				}
			}
			SaveToDisk();
			return entry.IsFavorite;
		}
		return false;
	}

	public static List<LoadstringHistoryEntry> SearchHistory(string? searchText = null, string? typeFilter = null, DateTime? fromDate = null, DateTime? toDate = null, bool? favoritesOnly = null)
	{
		IEnumerable<LoadstringHistoryEntry> results = _history.AsEnumerable();
		if (!string.IsNullOrWhiteSpace(searchText))
		{
			string search = searchText.ToLower();
			results = results.Where((LoadstringHistoryEntry e) => e.Url.ToLower().Contains(search) || e.Name.ToLower().Contains(search) || e.Tags.ToLower().Contains(search));
		}
		if (!string.IsNullOrWhiteSpace(typeFilter))
		{
			results = results.Where((LoadstringHistoryEntry e) => e.Type == typeFilter);
		}
		if (fromDate.HasValue)
		{
			results = results.Where((LoadstringHistoryEntry e) => e.Timestamp >= fromDate.Value);
		}
		if (toDate.HasValue)
		{
			results = results.Where((LoadstringHistoryEntry e) => e.Timestamp <= toDate.Value);
		}
		if (favoritesOnly == true)
		{
			results = results.Where((LoadstringHistoryEntry e) => e.IsFavorite);
		}
		return results.OrderByDescending((LoadstringHistoryEntry e) => e.Timestamp).ToList();
	}

	public static bool DeleteEntry(string url)
	{
		LoadstringHistoryEntry entry = _history.FirstOrDefault((LoadstringHistoryEntry e) => e.Url == url);
		if (entry != null)
		{
			_history.Remove(entry);
			SaveToDisk();
			return true;
		}
		return false;
	}

	public static void ClearHistory(bool keepFavorites = false)
	{
		if (keepFavorites)
		{
			_history = _history.Where((LoadstringHistoryEntry e) => e.IsFavorite).ToList();
		}
		else
		{
			_history.Clear();
		}
		SaveToDisk();
	}

	private static void TrimHistory()
	{
		List<LoadstringHistoryEntry> favorites = _history.Where((LoadstringHistoryEntry e) => e.IsFavorite).ToList();
		List<LoadstringHistoryEntry> nonFavorites = _history.Where((LoadstringHistoryEntry e) => !e.IsFavorite).Take(100 - favorites.Count).ToList();
		_history = (from e in favorites.Concat(nonFavorites)
			orderby e.Timestamp descending
			select e).ToList();
	}

	private static void LoadFromDisk()
	{
		try
		{
			if (File.Exists(CacheFilePath))
			{
				_history = JsonSerializer.Deserialize<List<LoadstringHistoryEntry>>(File.ReadAllText(CacheFilePath)) ?? new List<LoadstringHistoryEntry>();
			}
		}
		catch
		{
			_history = new List<LoadstringHistoryEntry>();
		}
	}

	private static void SaveToDisk()
	{
		try
		{
			string dir = Path.GetDirectoryName(CacheFilePath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(CacheFilePath, json);
		}
		catch
		{
		}
	}
}
