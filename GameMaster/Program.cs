// See https://aka.ms/new-console-template for more information

using System.Configuration;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster;
using GameMaster.Mafia;
using Microsoft.Extensions.DependencyInjection;



var dataService = new DataService();
var client = new DiscordSocketClient(new DiscordSocketConfig()
{
	GatewayIntents = GatewayIntents.All
});

var builder = new ServiceCollection();
builder.AddSingleton<DataService>();
builder.AddSingleton<DiscordSocketClient>(client);
var serviceProvider = builder.BuildServiceProvider();

client.Log += Log;

var token = ConfigurationManager.AppSettings["DiscordToken"];

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

var mafiaCommands = new MafiaCommands(client, dataService);
_ = new MafiaControls(client, dataService);

//client.SlashCommandExecuted += mafiaCommands.HandleSlashCommands;
client.MessageReceived += mafiaCommands.HandleMessages;
//client.Ready += async () => await mafiaCommands.RegisterCommands();

var interactionService = new InteractionService(client);
await interactionService.AddModuleAsync<MafiaCommands>(serviceProvider);
client.InteractionCreated += async (x) =>
{
	var ctx = new SocketInteractionContext(client, x);
	await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
};

// Block this task until the program is closed.
await Task.Delay(-1);

Task Log(LogMessage msg)
{
	Console.WriteLine(msg.ToString());
	return Task.CompletedTask;
}