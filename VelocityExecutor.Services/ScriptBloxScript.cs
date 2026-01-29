using System.Text.Json.Serialization;

namespace VelocityExecutor.Services;

public class ScriptBloxScript
{
	[JsonPropertyName("_id")]
	public string Id { get; set; }

	[JsonPropertyName("title")]
	public string Title { get; set; }

	[JsonPropertyName("game")]
	public ScriptBloxGame Game { get; set; }

	[JsonPropertyName("slug")]
	public string Slug { get; set; }

	[JsonPropertyName("verified")]
	public bool Verified { get; set; }

	[JsonPropertyName("key")]
	public bool Key { get; set; }

	[JsonPropertyName("views")]
	public int Views { get; set; }

	[JsonPropertyName("scriptType")]
	public string ScriptType { get; set; }

	[JsonPropertyName("isUniversal")]
	public bool IsUniversal { get; set; }

	[JsonPropertyName("isPatched")]
	public bool IsPatched { get; set; }

	[JsonPropertyName("image")]
	public string Image { get; set; }

	[JsonPropertyName("createdAt")]
	public string CreatedAt { get; set; }

	[JsonPropertyName("updatedAt")]
	public string UpdatedAt { get; set; }

	[JsonPropertyName("script")]
	public string ScriptContent { get; set; }
}
