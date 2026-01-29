using System;

namespace VelocityExecutor.Services;

public class LoadstringHistoryEntry
{
	public string Url { get; set; } = "";

	public string ContentHash { get; set; } = "";

	public DateTime Timestamp { get; set; }

	public int ContentLength { get; set; }

	public string Type { get; set; } = "";

	public bool IsFavorite { get; set; }

	public string Name { get; set; } = "";

	public string Tags { get; set; } = "";
}
