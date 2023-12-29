using Microsoft.AspNetCore.Mvc;

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
            {"redirect_uri", "https://localhost:5001/auth"},
            {"client_id", "713823987255476307"},
            {"client_secret", System.Configuration.ConfigurationManager.AppSettings["DiscordClientSecret"]}
        };
        var result = await _http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(requestBody));

        await Task.Delay(0);
        return Ok(await result.Content.ReadAsStringAsync());
    }
}