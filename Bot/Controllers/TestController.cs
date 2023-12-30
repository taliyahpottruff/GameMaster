using System.Net.Http.Headers;
using GameMaster.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace GameMaster.Bot.Controllers;

[Route("[controller]")]
[ApiController]
public class TestController : Controller
{
    private readonly DataService _data;
    private IConfiguration Configuration { get; }

    public TestController(DataService data, IConfiguration configuration)
    {
        _data = data;
        Configuration = configuration;
    }
    
    [HttpGet]
    public ActionResult Test()
    {
        return Ok("Test complete");
    }
}