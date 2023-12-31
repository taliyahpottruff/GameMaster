using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

var webBuilder = WebApplication.CreateBuilder(args);
webBuilder.Services.AddSingleton<DataService>();
webBuilder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig()
{
    GatewayIntents = GatewayIntents.All
}));
webBuilder.Services.AddSingleton<MafiaControlService>();
webBuilder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
webBuilder.Services.AddHostedService<BotService>();
webBuilder.Services.AddControllers();
webBuilder.WebHost.UseUrls("https://localhost:5002");
var app = webBuilder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();