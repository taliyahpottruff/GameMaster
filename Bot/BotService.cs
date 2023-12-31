using System.Configuration;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot.Mafia;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameMaster.Bot;

public class BotService : IHostedService
{
    private DataService Data { get; }
    private DiscordSocketClient Client { get; }
    private InteractionService InteractionService { get; }
    private MafiaControlService MafiaControls { get; }
    private IConfiguration Configuration { get; }
    
    public BotService(DataService data, DiscordSocketClient client, InteractionService interactionService, MafiaControlService mafiaControls, IConfiguration configuration)
    {
        Data = data;
        Client = client;
        InteractionService = interactionService;
        MafiaControls = mafiaControls;
        Configuration = configuration;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var botBuilder = new ServiceCollection();
        botBuilder.AddSingleton(Data);
        botBuilder.AddSingleton(Client);
        botBuilder.AddSingleton(MafiaControls);
        var serviceProvider = botBuilder.BuildServiceProvider();

        Client.Log += Log;

        var token = Configuration.GetValue<string>("DiscordToken");

        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        var mafiaCommands = new MafiaCommands(Client, Data);

        await InteractionService.AddModuleAsync<MafiaCommands>(serviceProvider);
        await InteractionService.AddModuleAsync<MafiaControls>(serviceProvider);
        Client.InteractionCreated += async (x) =>
        {
            var ctx = new SocketInteractionContext(Client, x);
            await InteractionService.ExecuteCommandAsync(ctx, serviceProvider);
        };

        Client.MessageReceived += async (x) =>
        {
            await MafiaCommands.HandleMessages(Client, Data, x);
        };

        Client.Ready += async () =>
        {
            await InteractionService.RegisterCommandsGloballyAsync();
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Client.StopAsync();
    }
    
    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}