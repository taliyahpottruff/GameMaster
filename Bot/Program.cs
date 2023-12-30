﻿// See https://aka.ms/new-console-template for more information

using System.Configuration;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot;
using GameMaster.Bot.Mafia;
using GameMaster.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;



var dataService = new DataService();
var client = new DiscordSocketClient(new DiscordSocketConfig()
{
	GatewayIntents = GatewayIntents.All
});

var botBuilder = new ServiceCollection();
botBuilder.AddSingleton(dataService);
botBuilder.AddSingleton(client);
var serviceProvider = botBuilder.BuildServiceProvider();

client.Log += Log;

var token = ConfigurationManager.AppSettings["DiscordToken"];

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

var mafiaCommands = new MafiaCommands(client, dataService);
_ = new MafiaControls(client, dataService);

var interactionService = new InteractionService(client);
await interactionService.AddModuleAsync<MafiaCommands>(serviceProvider);
await interactionService.AddModuleAsync<MafiaControls>(serviceProvider);
client.InteractionCreated += async (x) =>
{
	var ctx = new SocketInteractionContext(client, x);
	await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
};

client.MessageReceived += async (x) =>
{
	await MafiaCommands.HandleMessages(client, dataService, x);
};

client.Ready += async () =>
{
	await interactionService.RegisterCommandsGloballyAsync();
};

#region Web

var webBuilder = WebApplication.CreateBuilder(args);
webBuilder.Services.AddSingleton(dataService);
webBuilder.Services.AddControllers();
var app = webBuilder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

#endregion

// Block this task until the program is closed.
await Task.Delay(-1);

Task Log(LogMessage msg)
{
	Console.WriteLine(msg.ToString());
	return Task.CompletedTask;
}