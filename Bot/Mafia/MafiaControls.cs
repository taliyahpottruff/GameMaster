using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Models.Mafia;
using GameMaster.Shared;

namespace GameMaster.Bot.Mafia;

public class MafiaControls : InteractionModuleBase
{
	private readonly DiscordSocketClient _client;
	private readonly DataService _db;
	private MafiaControlService Service { get; }

	public MafiaControls(DiscordSocketClient client, DataService db, MafiaControlService service)
	{
		_client = client;
		_db = db;
		Service = service;
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
			Console.WriteLine($"{Context.Channel.Name}: Something went wrong. There doesn't seem to be an active mafia game using this channel.");
            await ((SocketGuildChannel)Context.Channel).DeleteAsync();
            return;
		}

		await ModifyOriginalResponseAsync(x => x.Content = "Game removed from database... please wait.");

		var guild = _client.GetGuild(game.Guild);

		if (game.Channel > ulong.MinValue)
		{
			if (await _client.GetChannelAsync(game.Channel) is ITextChannel channel)
			{
				if (option == "keep")
				{
					try
					{
						await channel.RemovePermissionOverwriteAsync(guild.EveryoneRole);
						foreach (var playerId in game.Players)
						{
							var player = guild.GetUser(playerId);
							await channel.RemovePermissionOverwriteAsync(player);
						}
					}
					catch (Exception e)
					{
						Console.WriteLine($"There may be permissions errors in {guild.Id}");
						Console.WriteLine(e.ToString());
					}
				}
				else
				{
					try
					{
						await channel.DeleteAsync();
					}
					catch 
					{
						Console.WriteLine($"Can't delete channel in {guild.Id}");
					}
				}
				
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
		await DeferAsync();

		var game = await Service.CreateDayChat((ITextChannel)Context.Channel);

		if (game is null)
		{
			await RespondAsync("You are not allowed to do this");
			return;
		}
		
		await UpdateControlPanelMessage(game);
		await RespondAsync("Created");
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
			await gameChannel.AddPermissionOverwriteAsync(guildUser, new OverwritePermissions(viewChannel: (game.ChatStatus != MafiaGame.GameChatStatus.Unviewable) ? PermValue.Allow : PermValue.Deny, sendMessages: (game.ChatStatus == MafiaGame.GameChatStatus.Open) ? PermValue.Allow : PermValue.Deny, addReactions: PermValue.Inherit));

        // Update control panel message
        await UpdateControlPanelMessage(game);

        await DeleteOriginalResponseAsync();
	}
	
	[ComponentInteraction("removePlayer:*")]
	private async Task RemovePlayerButton(string playerIdString)
	{
		var isPlayerId = ulong.TryParse(playerIdString, out var playerId);
		if (!isPlayerId)
			return;

		await DeferAsync(true);

		var game = await _db.GetMafiaGame(Context.Channel.Id);
		if (game is null) return;

		var success = await _db.RemovePlayerFromMafiaGame(Context.Channel.Id, playerId);
		if (!success) return;
		game.Players.Remove(playerId);
		var gameChannel = await Context.Guild.GetTextChannelAsync(game.Channel);
		var guildUser = await Context.Guild.GetUserAsync(playerId);
		if (game.Channel > ulong.MinValue)
			await gameChannel.RemovePermissionOverwriteAsync(guildUser);
			
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

	[ComponentInteraction("chat:*")]
	private async Task SetChat(string status)
	{
		bool open = status == "open";
		await DeferAsync();

		var game = await _db.GetMafiaGame(Context.Channel.Id);
		if (game is null) return;

		if (game.Channel == ulong.MinValue) return;
		var guild = _client.GetGuild(game.Guild);
		var channel = (ITextChannel)await _client.GetChannelAsync(game.Channel);

		await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Inherit, sendMessages: PermValue.Deny, addReactions: PermValue.Deny));
		foreach (var playerId in game.Players)
		{
			var user = await _client.GetUserAsync(playerId);
			await channel.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: open ? PermValue.Allow : PermValue.Deny, addReactions: PermValue.Inherit));
		}
		game.ChatStatus = open ? MafiaGame.GameChatStatus.Open : MafiaGame.GameChatStatus.Closed;
		await _db.SetMafiaGameChatStatus(game.ControlPanel, game.ChatStatus);

		await UpdateControlPanelMessage(game);
	}
	#endregion

	#region Slash Commands

	[SlashCommand("addplayer", "Add a new player to a mafia game (must be used in the control panel)")]
	private async Task AddPlayer(string playerName)
	{
		await DeferAsync();

		var game = await _db.GetMafiaGame(Context.Channel.Id, false);
        if (game is null)
        {
			await ModifyOriginalResponseAsync(x => x.Content = "You can only use this command in a game of mafia");
			return;
        }

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

	[SlashCommand("removeplayer", "Remove a player from the current mafia game (control panel only)")]
	private async Task RemovePlayer(string playerName)
	{
		await DeferAsync();

		var game = await _db.GetMafiaGame(Context.Channel.Id, false);
		if (game is null)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "You can only use this command in a game of mafia");
			return;
		}

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
			x.Content = $"Confirm removing {user.DisplayName} from the game?";
		});
		await ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder()
			.AddRow(
				new ActionRowBuilder()
					.WithButton("Yes", $"removePlayer:{user.Id}", ButtonStyle.Success)
					.WithButton("No", $"deleteMessage:{Context.Channel.Id},{message.Id}", ButtonStyle.Danger)
			).Build()
		);
	}
	#endregion
}