// See https://aka.ms/new-console-template for more information

using System.Configuration;
using Discord;
using Discord.WebSocket;
using GameMaster;
using GameMaster.Mafia;
using Microsoft.Extensions.DependencyInjection;

/*var builder = new ServiceCollection();
builder.AddSingleton<DataService>();
builder.BuildServiceProvider();*/

var dataService = new DataService();
var client = new DiscordSocketClient(new DiscordSocketConfig()
{
	GatewayIntents = GatewayIntents.All
});

client.Log += Log;

var token = ConfigurationManager.AppSettings["DiscordToken"];

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

var mafiaCommands = new MafiaCommands(client, dataService);
_ = new MafiaControls(client, dataService);

client.SlashCommandExecuted += mafiaCommands.HandleSlashCommands;
client.MessageReceived += mafiaCommands.HandleMessages;
client.Ready += async () => await mafiaCommands.RegisterCommands();

// Block this task until the program is closed.
await Task.Delay(-1);

Task Log(LogMessage msg)
{
	Console.WriteLine(msg.ToString());
	return Task.CompletedTask;
}