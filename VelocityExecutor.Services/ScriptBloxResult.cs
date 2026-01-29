using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VelocityExecutor.Services;

public class ScriptBloxResult
{
	[JsonPropertyName("totalPages")]
	public int TotalPages { get; set; }

	[JsonPropertyName("nextPage")]
	public int? NextPage { get; set; }

	[JsonPropertyName("max")]
	public int Max { get; set; }

	[JsonPropertyName("scripts")]
	public List<ScriptBloxScript> Scripts { get; set; }
}
