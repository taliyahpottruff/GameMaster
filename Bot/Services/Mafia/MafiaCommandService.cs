using Discord;
using Discord.WebSocket;
using GameMaster.Bot.Extensions;
using GameMaster.Shared;
using GameMaster.Shared.Mafia;

namespace GameMaster.Bot.Services.Mafia;

public class MafiaCommandService
{
    private DataService Data { get; }
    private DiscordSocketClient Client { get; }

    public MafiaCommandService(DataService data, DiscordSocketClient client)
    {
        Data = data;
        Client = client;
    }

    public async Task<ServiceResult<ulong>> NewMafiaGame(ITextChannel channel, IUser gm, string name, bool createChannel = false)
    {
        var sanitizedName = name.Sanitize().ToLower().Replace(" ", "-");

        var guild = channel.Guild;
		var category = ((SocketGuild)guild).CategoryChannels.First(x => x.Channels.FirstOrDefault(x => x.Id == channel.Id) is not null).Id;
		var controlPanelChannel = await guild.CreateTextChannelAsync($"{sanitizedName}-control", x =>
		{
			x.PermissionOverwrites = new List<Overwrite>()
			{
				new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
				new Overwrite(Client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
				new Overwrite(gm.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
			};
			x.CategoryId = category;
		});
		
		// Send base control panel message
		var controlPanelMessage = await controlPanelChannel.SendMessageAsync(embed: new EmbedBuilder()
			.WithTitle(name)
			.AddField(new EmbedFieldBuilder().WithName("Players").WithIsInline(true).WithValue("*None*"))
			.AddField(new EmbedFieldBuilder().WithName("Game Chat Open").WithIsInline(true).WithValue("Not created"))
			.AddField(new EmbedFieldBuilder().WithName("Voting").WithIsInline(true).WithValue("Closed"))
			.Build()
		, components: new ComponentBuilder()
			.AddRow(new ActionRowBuilder()
				.WithButton("Create day chat", "createChannel")
				.WithButton("Open game chat", "chat:open")
				.WithButton("Close game chat", "chat:close")
				.WithButton("End Game", "endGame", ButtonStyle.Danger)
			).Build()
		);

		MafiaGame newGame = new() { 
			GM = gm.Id,
			Guild = guild.Id,
			ControlPanel = controlPanelChannel.Id,
			ControlPanelMessage = controlPanelMessage.Id,
			Name = name,
			SanitizedName = sanitizedName,
		};

		if (createChannel)
		{
            var gameChannel = await guild.CreateTextChannelAsync(sanitizedName, x =>
            {
                x.PermissionOverwrites = new List<Overwrite>()
            {
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny, addReactions: PermValue.Deny)),
                new Overwrite(Client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
                new Overwrite(gm.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
            };
                x.CategoryId = category;
            });
			newGame.Channel = gameChannel.Id;
        }

		await Data.CreateNewMafiaGame(newGame);

		return new ServiceResult<ulong>(true, controlPanelChannel.Id);
    }
}