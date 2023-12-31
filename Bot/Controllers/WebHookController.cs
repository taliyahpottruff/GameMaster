using System.Net.Http.Headers;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using GameMaster.Bot.Mafia;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace GameMaster.Bot.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WebHookController : Controller
{
    private DataService Data { get; }
    private IConfiguration Configuration { get; }
    private DiscordSocketClient Client { get; }
    private MafiaControlService MafiaControls { get; }

    public WebHookController(DataService data, IConfiguration configuration, DiscordSocketClient client, MafiaControlService mafiaControls)
    {
        Data = data;
        Configuration = configuration;
        Client = client;
        MafiaControls = mafiaControls;
    }
    
    #if DEBUG
    [HttpGet]
    public async Task<ActionResult> Test()
    {
        //https://discord.com/channels/713828118883860612/806994343101595648
        var channel = await Client.GetChannelAsync(806994343101595648) as SocketTextChannel;

        if (channel is null)
            return BadRequest("That channel doesn't exist");

        await channel.SendMessageAsync("This was sent from a webhook!");
        return Ok("Message sent");
    }
    #endif

    [HttpPost("createdaychat")]
    public async Task<ActionResult> CreateDayChat([FromQuery]ulong controlpanel)
    {
        //https://discord.com/channels/713828118883860612/1190803784985751724
        var channel = await Client.GetChannelAsync(controlpanel);

        if (channel is null)
            return StatusCode(500);
        
        await MafiaControls.CreateDayChat((ITextChannel)channel);

        return Ok();
    }
}