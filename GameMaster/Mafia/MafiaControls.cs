using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Xml.Linq;

namespace GameMaster.Mafia;

public class MafiaControls : InteractionModuleBase
{
	private readonly DiscordSocketClient _client;
	private readonly DataService _db;

	public MafiaControls(DiscordSocketClient client, DataService db)
	{
		_client = client;
		_db = db;
	}

	private async Task UpdateControlPanelMessage(MafiaGame game)
	{
		// Get the message
		var guild = _client.GetGuild(game.Guild);
		var channel = (ITextChannel)await _client.GetChannelAsync(game.ControlPanel);
		await channel.ModifyMessageAsync(game.ControlPanelMessage, x => {
			x.Embed = new EmbedBuilder()
				.WithTitle(game.Name)
				.AddField(new EmbedFieldBuilder().WithName("Players").WithIsInline(true).WithValue(game.Players.Count > 0 ? String.Join('\n', game.Players.Select(p => guild.GetUser(p).DisplayName)) : "*None*"))
				.AddField(new EmbedFieldBuilder().WithName("Game Chat Open").WithIsInline(true).WithValue(game.ChatStatusAsString()))
				.AddField(new EmbedFieldBuilder().WithName("Voting").WithIsInline(true).WithValue(game.VotingOpen ? "Open" : "Closed"))
				.Build();

        });
	}

	#region Button Handlers

	[ComponentInteraction("endGame")]
	private async Task EndGameInitial()
	{
		await RespondAsync(text: "What would you like to do with your game channels?", components: new ComponentBuilder()
			.AddRow(new ActionRowBuilder()
				.WithButton("Keep them (will be viewable)", "endGame-keep", ButtonStyle.Success)
				.WithButton("Delete them (irreversible)", "endGame-delete", ButtonStyle.Danger))
			.Build());

		await Task.Delay(60000);
		try
		{
			await DeleteOriginalResponseAsync();
        } 
		catch (Exception)
        {

		}
	}

	[ComponentInteraction("endGame-*")]
	private async Task EndGameKeep(string option)
	{
		await DeferAsync();
		
		var game = await _db.GetMafiaGame(Context.Channel.Id);
		bool success = await _db.DeleteMafiaGame(Context.Channel.Id);

		if (!success || game is null)
		{
			await ModifyOriginalResponseAsync(x => x.Content =
				"Something went wrong. There doesn't seem to be an active mafia game using this channel. Please delete it manually.");
			return;
		}

		await ModifyOriginalResponseAsync(x => x.Content = "Game removed from database... please wait.");

		var guild = _client.GetGuild(game.Guild);

		if (game.Channel > ulong.MinValue)
		{
			if (await _client.GetChannelAsync(game.Channel) is ITextChannel channel)
			{
				if (option == "keep")
					await channel.SyncPermissionsAsync();
				else
					await channel.DeleteAsync();
			}
		}

		foreach (var channelId in game.GameChannels)
		{
			if (await _client.GetChannelAsync(game.Channel) is SocketTextChannel channel)
			{
				if (option == "keep")
					await channel.RemovePermissionOverwriteAsync(guild.EveryoneRole);
                else
                    await channel.DeleteAsync();
            }
		}

		//await button.ModifyOriginalResponseAsync(x => x.Content= "Now deleting control panel...");
		await ((SocketGuildChannel)Context.Channel).DeleteAsync();
	}

	[ComponentInteraction("createChannel")]
	private async Task CreateChannel()
	{
		await DeferAsync(true);

		var game = await _db.GetMafiaGame(Context.Channel.Id);
		if (game is null) return;

		if (game.Channel > ulong.MinValue)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "There's already a primary game channel for this game!");
			return;
		}

		var guild = Context.Guild;
        var category = ((SocketGuild)guild).CategoryChannels.First(x => x.Channels.FirstOrDefault(x => x.Id == Context.Channel.Id) is not null).Id;
        var gameChannel = await guild.CreateTextChannelAsync(game.SanitizedName, x =>
        {
            x.PermissionOverwrites = new List<Overwrite>()
            {
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(sendMessages: PermValue.Deny, addReactions: PermValue.Deny)),
                new Overwrite(_client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
                new Overwrite(Context.User.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow)),
            };
            x.CategoryId = category;
        });

		game.Channel = gameChannel.Id;
		await _db.SetMafiaGameChannel(game.ControlPanel, game.Channel);

		game.ChatStatus = MafiaGame.GameChatStatus.Unviewable;
		await _db.SetMafiaGameChatStatus(game.ControlPanel, game.ChatStatus);
		
		await UpdateControlPanelMessage(game);
    }

	[ComponentInteraction("addPlayer:*")]
	private async Task AddPlayerButton(string playerIdString)
	{
		var isPlayerId = ulong.TryParse(playerIdString, out var playerId);
		if (!isPlayerId)
			return;

		await DeferAsync(true);

		var game = await _db.GetMafiaGame(Context.Channel.Id);
		if (game is null) return;

		var success = await _db.AddPlayerToMafiaGame(Context.Channel.Id, playerId);
		if (!success) return;
        game.Players.Add(playerId);
        var gameChannel = await Context.Guild.GetTextChannelAsync(game.Channel);
		var guildUser = await Context.Guild.GetUserAsync(playerId);
		if (game.Channel > ulong.MinValue)
			await gameChannel.AddPermissionOverwriteAsync(guildUser, new OverwritePermissions(sendMessages: PermValue.Allow, addReactions: PermValue.Inherit));

        // Update control panel message
        await UpdateControlPanelMessage(game);

        await DeleteOriginalResponseAsync();
	}

	[ComponentInteraction("deleteMessage:*,*")]
	private async Task DeleteMessage(string channelId, string messageId)
	{
		var channel = (ITextChannel)await _client.GetChannelAsync(ulong.Parse(channelId));
		await channel.DeleteMessageAsync(ulong.Parse(messageId));
	}
	#endregion

	#region Slash Commands

	[SlashCommand("addplayer", "Add a new player to a mafia game (must be used in the control panel)")]
	private async Task AddPlayer(string playerName)
	{
		await DeferAsync();
		
		playerName = playerName.ToLower();
		var foundUsers = await Context.Guild.SearchUsersAsync(playerName);
		if (foundUsers.Count < 1)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "No user with that name was found");
			return;
		}

		var user = foundUsers.First();
		var message = await ModifyOriginalResponseAsync(x =>
		{
			x.Content = $"Confirm adding {user.DisplayName} to the game?";
		});
		await ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder()
                .AddRow(
                    new ActionRowBuilder()
                        .WithButton("Yes", $"addPlayer:{user.Id}", ButtonStyle.Success)
                        .WithButton("No", $"deleteMessage:{Context.Channel.Id},{message.Id}", ButtonStyle.Danger)
				).Build()
		);
	}

	#endregion
}