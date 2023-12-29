using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using GameMaster.Shared;
using Newtonsoft.Json.Linq;

namespace Web.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : Controller
{
    private readonly HttpClient _http;
    private readonly DataService _data;

    public AuthController(HttpClient http, DataService data)
    {
        _http = http;
        _data = data;
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

        Response.Cookies.Append("access-token", response.AccessToken, new CookieOptions() { Expires = DateTimeOffset.Now.AddSeconds(response.ExpiresIn), Secure = true });

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.AccessToken);
        var userInfoString = await _http.GetStringAsync("https://discord.com/api/users/@me");
        var userInfo = JObject.Parse(userInfoString);

        var discordId = (ulong)userInfo["id"];
        await _data.AddUser(discordId, response.RefreshToken);
        return Redirect("/dashboard");
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