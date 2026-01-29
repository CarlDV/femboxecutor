using System.Text.Json.Serialization;

namespace VelocityExecutor.Services;

public class ScriptBloxGame
{
	[JsonPropertyName("_id")]
	public string Id { get; set; }

	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonPropertyName("imageUrl")]
	public string ImageUrl { get; set; }
}
