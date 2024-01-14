using Discord;
using Discord.WebSocket;
using GameMaster.Shared;
using GameMaster.Shared.Mafia;

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
        var game = Data.GetMafiaGame(controlPanelChannel.Id);
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
        game.Updated?.Invoke();

        return game;
    }

    public async Task<MafiaGame?> CreateDayChat(ulong controlPanelChannel)
    {
        var channel = await Client.GetChannelAsync(controlPanelChannel) as ITextChannel;

        if (channel is null)
            return null;

        return await CreateDayChat(channel);
    }

    public async Task<ServiceResult<object>> SetDayChat(ITextChannel controlPanelChannel, string status)
    {
        bool open = status == "open";
        var game = Data.GetMafiaGame(controlPanelChannel.Id);
        if (game is null) return new ServiceResult<object>(false, "Game could not be found");

        if (game.Channel == ulong.MinValue) return new ServiceResult<object>(false, "The day chat does not yet exist");
        var guild = Client.GetGuild(game.Guild);
        var channel = (ITextChannel)await Client.GetChannelAsync(game.Channel);

        await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Inherit, sendMessages: PermValue.Deny, addReactions: PermValue.Deny));
        foreach (var playerId in game.Players)
        {
            var user = await Client.GetUserAsync(playerId);
            await channel.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: open ? PermValue.Allow : PermValue.Deny, addReactions: PermValue.Inherit));
        }
        game.ChatStatus = open ? MafiaGame.GameChatStatus.Open : MafiaGame.GameChatStatus.Closed;
        await Data.SetMafiaGameChatStatus(game.ControlPanel, game.ChatStatus);
        game.Updated?.Invoke();

        return new ServiceResult<object>(true, game);
    }

    public async Task<ServiceResult<object>> SetDayChat(ulong controlPanelChannelId, string status)
    {
        var channel = await Client.GetChannelAsync(controlPanelChannelId) as ITextChannel;

        if (channel is null)
            return new ServiceResult<object>(false, "The supplied channel doesn't exist");

        return await SetDayChat(channel, status);
    }

    public List<IGuildUser> GetResolvedPlayerList(string gameId)
    {
        List<IGuildUser> list = new();
        var game = Data.GetMafiaGame(gameId);

        if (game is null)
            return list;

        var guild = Client.GetGuild(game.Guild);
        foreach (var playerId in game.Players)
        {
            var user = guild.GetUser(playerId);
            list.Add(user);
        }

        return list;
    }

    public async Task<IEnumerable<IGuildUser>> GetAllGuildUsers(MafiaGame game)
    {
        var guild = Client.GetGuild(game.Guild);
        return await guild.GetUsersAsync().FlattenAsync();
    }

    public async Task<ServiceResult<string>> AddPlayerToGame(MafiaGame game, ulong playerId)
    {
        var guild = (IGuild)Client.GetGuild(game.Guild);
        var success = await Data.AddPlayerToMafiaGame(game.ControlPanel, playerId);
        if (!success) return new ServiceResult<string>(false, "Player already is in the game");
        var gameChannel = await guild.GetTextChannelAsync(game.Channel);
        var guildUser = await guild.GetUserAsync(playerId);
        if (game.Channel > ulong.MinValue)
            await gameChannel.AddPermissionOverwriteAsync(guildUser, new OverwritePermissions(viewChannel: (game.ChatStatus != MafiaGame.GameChatStatus.Unviewable) ? PermValue.Allow : PermValue.Deny, sendMessages: (game.ChatStatus == MafiaGame.GameChatStatus.Open) ? PermValue.Allow : PermValue.Deny, addReactions: PermValue.Inherit));

        return new ServiceResult<string>(true, "Successfully added player");
    }
}