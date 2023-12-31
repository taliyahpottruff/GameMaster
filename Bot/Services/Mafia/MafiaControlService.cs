using Discord;
using Discord.WebSocket;
using GameMaster.Models.Mafia;
using GameMaster.Shared;

namespace GameMaster.Bot.Services.Mafia;

public class MafiaControlService
{
    private DataService Data { get; }
    private DiscordSocketClient Client { get; }
    
    public MafiaControlService(DataService data, DiscordSocketClient client)
    {
        Data = data;
        Client = client;
    }

    public async Task<MafiaGame?> CreateDayChat(ITextChannel controlPanelChannel)
    {
        var game = await Data.GetMafiaGame(controlPanelChannel.Id);
        if (game is null) return null;

        if (game.Channel > ulong.MinValue)
        {
            return null;
        }

        var guild = controlPanelChannel.Guild;
        var category = ((SocketGuild)guild).CategoryChannels.First(x => x.Channels.FirstOrDefault(x => x.Id == controlPanelChannel.Id) is not null).Id;
        var gameChannel = await guild.CreateTextChannelAsync(game.SanitizedName, x =>
        {
            x.PermissionOverwrites = new List<Overwrite>()
            {
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny, addReactions: PermValue.Deny)),
                new Overwrite(Client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
                new Overwrite(game.GM, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
            };
            x.CategoryId = category;
        });

        game.Channel = gameChannel.Id;
        await Data.SetMafiaGameChannel(game.ControlPanel, game.Channel);

        game.ChatStatus = MafiaGame.GameChatStatus.Unviewable;
        await Data.SetMafiaGameChatStatus(game.ControlPanel, game.ChatStatus);

        return game;
    }
}