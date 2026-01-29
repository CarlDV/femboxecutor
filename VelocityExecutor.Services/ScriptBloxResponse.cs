using System.Text.Json.Serialization;

namespace VelocityExecutor.Services;

public class ScriptBloxResponse
{
	[JsonPropertyName("result")]
	public ScriptBloxResult Result { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; }
}
