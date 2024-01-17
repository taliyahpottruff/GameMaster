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

    public async Task<ServiceResult<ulong>> NewMafiaGame(ICategoryChannel category, IUser gm, string name, bool createChannel = false)
    {
        var sanitizedName = name.Sanitize().ToLower().Replace(" ", "-");

        var guild = category.Guild;
		var controlPanelChannel = await guild.CreateTextChannelAsync($"{sanitizedName}-control", x =>
		{
			x.PermissionOverwrites = new List<Overwrite>()
			{
				new(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
				new(Client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
				new(gm.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
			};
			x.CategoryId = category.Id;
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
                new(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny, addReactions: PermValue.Deny)),
                new(Client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
                new(gm.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
            };
                x.CategoryId = category.Id;
            });
			newGame.Channel = gameChannel.Id;
			newGame.ChatStatus = MafiaGame.GameChatStatus.Unviewable;
		}

		await Data.CreateNewMafiaGame(newGame);

		return new ServiceResult<ulong>(true, controlPanelChannel.Id);
    }
    
    public async Task<ServiceResult<ulong>> NewMafiaGame(ulong guildId, ulong categoryId, ulong gmId, string name, bool createChannel = false)
    {
	    var guild = Client.GetGuild(guildId);
	    var category = guild.CategoryChannels.First(x => x.Id == categoryId);
	    var gm = await Client.GetUserAsync(gmId);

	    if (gm is null)
		    return new ServiceResult<ulong>(false, ulong.MinValue);

	    return await NewMafiaGame((ICategoryChannel)category, gm, name, createChannel);
    }

    public async Task<List<ServerCategories>> GetAvailableServerCategories(ulong userId)
    {
	    var guilds = Client.Guilds;

	    if (guilds is null)
		    return [];

	    List<ServerCategories> list = [];
	    foreach (var guild in guilds)
	    {
		    var users = await guild.GetUsersAsync().FlattenAsync();
		    
		    // This can't be efficient
		    if (users.All(user => user.Id != userId))
			    continue;
		    
		    ServerCategories obj = new(guild.Id, guild.Name, guild.IconUrl);

		    foreach (var category in guild.CategoryChannels)
		    {
			    obj.Categories.Add(new ServerCategories.Category(category.Id, category.Name));
		    }
		    
		    list.Add(obj);
	    }

	    return list;
    }
}