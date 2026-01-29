using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VelocityExecutor.Services;

public static class CatboxService
{
	private const string API_URL = "https://catbox.moe/user/api.php";

	public static async Task<string> UploadScriptAsync(string content)
	{
		_ = 1;
		try
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.UserAgent.ParseAdd("VelocityExecutor");
			using MultipartFormDataContent form = new MultipartFormDataContent();
			ByteArrayContent streamContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
			form.Add(new StringContent("fileupload"), "reqtype");
			form.Add(streamContent, "fileToUpload", $"script_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.lua");
			HttpResponseMessage response = await client.PostAsync("https://catbox.moe/user/api.php", form);
			string responseString = await response.Content.ReadAsStringAsync();
			if (response.IsSuccessStatusCode)
			{
				if (Uri.TryCreate(responseString, UriKind.Absolute, out Uri _))
				{
					return responseString.Trim();
				}
				throw new Exception("Unexpected response from Catbox: " + responseString);
			}
			throw new Exception($"Catbox API Error: {response.StatusCode} - {responseString}");
		}
		catch (Exception ex)
		{
			throw new Exception("Catbox Upload Failed: " + ex.Message, ex);
		}
	}
}
