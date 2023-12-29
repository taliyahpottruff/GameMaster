using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Web.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : Controller
{
    private readonly HttpClient _http;

    public AuthController(HttpClient http)
    {
        _http = http;
    }
    
    [HttpGet]
    public async Task<ActionResult> Auth([FromQuery]string code)
    {
        var requestBody = new Dictionary<string, string?>()
        {
            {"grant_type", "authorization_code"},
            {"code", code},
            #if DEBUG
            {"redirect_uri", "https://localhost:5001/auth"},
            #else
            {"redirect_uri", "https://discordgamemaster.com/auth"},
            #endif
            {"client_id", "713823987255476307"},
            {"client_secret", System.Configuration.ConfigurationManager.AppSettings["DiscordClientSecret"]}
        };
        var result = await _http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(requestBody));

        if (!result.IsSuccessStatusCode)
            return BadRequest("Failed to authenticate that Discord account");
        string json = await result.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<DiscordTokenResponse>(json);

        if (response is null)
            return StatusCode(500);

        return Ok($"Access Token: {response.AccessToken}\nRefresh Token: {response.RefreshToken}");
    }

    private class DiscordTokenResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; } = String.Empty;
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = String.Empty;
        [JsonProperty("expires_in")]
        public uint ExpiresIn { get; set; }
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = String.Empty;
        [JsonProperty("scope")]
        public string Scope { get; set; } = String.Empty;
    }
}