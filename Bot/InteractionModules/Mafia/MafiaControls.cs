using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using GameMaster.Shared.Mafia;

namespace GameMaster.Bot.InteractionModules.Mafia;

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
		
		var game = _db.GetMafiaGame(Context.Channel.Id);

		if (game is null)
		{
			/*Console.WriteLine($"{Context.Channel.Name}: Something went wrong. There doesn't seem to be an active mafia game using this channel.");
            await ((SocketGuildChannel)Context.Channel).DeleteAsync();*/
			await RespondAsync(
				"Something went wrong. There doesn't seem to be an active mafia game using this channel.",
				ephemeral: true);
            return;
		}

		var message = await ReplyAsync("Game removed from database... please wait.");

		var result = await Service.DeleteGame(game, option == "keep");

		if (!result.Success)
		{
			await message.ModifyAsync(x => x.Content = result.Payload);
		}
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

		var game = _db.GetMafiaGame(Context.Channel.Id);
		if (game is null) return;

		var result = await Service.AddPlayerToGame(game, playerId);

		if (!result.Success)
		{
			await RespondAsync(result.Payload);
		}

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

		var game = _db.GetMafiaGame(Context.Channel.Id);
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
		
		await DeferAsync();

		var result = await Service.SetDayChat((ITextChannel)Context.Channel, status);

		if (result.Success)
			await UpdateControlPanelMessage((MafiaGame)result.Payload);
		else
			await RespondAsync(result.Payload.ToString());
	}
	#endregion

	#region Slash Commands

	[SlashCommand("addplayer", "Add a new player to a mafia game (must be used in the control panel)")]
	private async Task AddPlayer(string playerName)
	{
		await DeferAsync();

		var game = _db.GetMafiaGame(Context.Channel.Id, false);
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

		var game = _db.GetMafiaGame(Context.Channel.Id, false);
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