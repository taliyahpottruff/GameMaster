using Discord;
using Discord.WebSocket;

namespace GameMaster.Mafia;

public class MafiaCommands : IDiscordHandler
{
	private readonly DiscordSocketClient _client;
	private readonly DataService _db;
	
	public MafiaCommands(DiscordSocketClient client, DataService db)
	{
		_client = client;
		_db = db;
	}

	public async Task RegisterCommands()
	{
		await _client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder()
			.WithName("startvote")
			.WithDescription("Start a vote in this channel")
			.Build());
		await _client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder()
			.WithName("stopvote")
			.WithDescription("Ends voting in this channel")
			.Build());
		await _client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder()
			.WithName("resetvote")
			.WithDescription("Wipe all votes")
			.Build());
		await _client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder()
			.WithName("tally")
			.WithDescription("Get the current vote tally")
			.Build());
		await _client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder()
			.WithName("newmafiagame")
			.WithDescription("Start a new mafia game")
			.AddOption(new SlashCommandOptionBuilder().WithName("name").WithDescription("Name of the mafia game").WithType(ApplicationCommandOptionType.String).WithRequired(true))
			.Build());
	}

	public async Task HandleSlashCommands(SocketSlashCommand command)
	{
		switch (command.Data.Name)
		{
			case "startvote":
				await StartCount(command);
				break;
			case "stopvote":
				await StopCount(command);
				break;
			case "resetvote":
				await ResetCount(command);
				break;
			case "tally":
				await VoteTally(command);
				break;
			case "newmafiagame":
				await NewGame(command);
				break;
		}
	}

	public async Task HandleMessages(SocketMessage ctx)
	{
		var msg = (SocketUserMessage)ctx;
		var content = msg.CleanContent;
		if (content.ToLower().StartsWith("lynch"))
		{
			var mafiaGame = await _db.GetMafiaGame(msg.Channel.Id);
			if (mafiaGame is null)
				return;

			var guild = _client.GetGuild(mafiaGame.Guild);
			
			var mentionedUsers = msg.MentionedUsers;
			SocketUser? votingAgainst = null;
			
			if (mentionedUsers.Count < 1)
			{
				var parts = content.ToLower().Split(" ");
				if (parts.Length < 2)
				{
					await msg.ReplyAsync("Please specify who you want to lynch");
					return;
				}
				
				var userToSearchFor = parts.RebuildParts(1);
				var usersInChannel = await msg.Channel.GetUsersAsync().FlattenAsync();
				SocketGuildUser? currentUser = null;
				foreach (var user in usersInChannel)
				{
					var guildUser = guild.GetUser(user.Id);

					if (guildUser is null)
						continue;

					if (guildUser.DisplayName.ToLower().Contains(userToSearchFor) && !guildUser.IsBot)
					{
						currentUser = guildUser;
					}
				}

				if (currentUser is null)
				{
					await msg.ReplyAsync(
						"I don't know who that is. Try @ mentioning or checking your spelling.");
					return;
				}

				votingAgainst = currentUser;
			}
			else
			{
				votingAgainst = mentionedUsers.First();
			}
			
			// See if the user already voted and change their vote if so
			var alreadyVoted = mafiaGame.Votes.Find(x => x.From == msg.Author.Id);
			if (alreadyVoted is null)
			{
				mafiaGame.Votes.Add(new MafiaGame.Vote()
				{
					From = msg.Author.Id,
					Against = votingAgainst.Id
				});
			}
			else
			{
				alreadyVoted.Against = votingAgainst.Id;
			}
			
			// Update the DB
			await _db.UpdateMafiaVotes(mafiaGame);
			
			// Inform the players of the new count
			await SendTally(msg.Channel, mafiaGame);
		}
		else if (content.ToLower().StartsWith("unlynch"))
		{
			var mafiaGame = await _db.GetMafiaGame(msg.Channel.Id);
			if (mafiaGame is null)
				return;

			var guild = _client.GetGuild(mafiaGame.Guild);

			var existingVote = mafiaGame.Votes.Find(x => x.From == ctx.Author.Id);
			
			if (existingVote is null)
				return;

			mafiaGame.Votes.Remove(existingVote);
			await _db.UpdateMafiaVotes(mafiaGame);
			await SendTally(ctx.Channel, mafiaGame);
		}
	}

	private async Task NewGame(SocketSlashCommand cmd)
	{
		await cmd.DeferAsync(true);
		var nameOption = cmd.Data.Options.FirstOrDefault(x => x.Name == "name");

		if (nameOption is null) return;

		var gameName = (string)nameOption.Value;
		var sanitizedName = gameName.Sanitize().ToLower().Replace(" ", "-");

		var guild = _client.GetGuild(cmd.GuildId ?? ulong.MinValue);
		if (guild is null) return;
		var controlPanelChannel = await guild.CreateTextChannelAsync($"{sanitizedName}-control", x =>
		{
			x.PermissionOverwrites = new List<Overwrite>()
			{
				new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
				new Overwrite(_client.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
				new Overwrite(cmd.User.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
			};
			x.CategoryId = guild.CategoryChannels.First(x => x.Channels.FirstOrDefault(x => x.Id == cmd.Channel.Id) is not null).Id;
		});
		
		// Send base control panel message
		await controlPanelChannel.SendMessageAsync(embed: new EmbedBuilder()
			.WithTitle(gameName)
			.AddField(new EmbedFieldBuilder().WithName("Players").WithIsInline(true).WithValue("*None*"))
			.AddField(new EmbedFieldBuilder().WithName("Game Chat Open").WithIsInline(true).WithValue("Not created"))
			.AddField(new EmbedFieldBuilder().WithName("Voting").WithIsInline(true).WithValue("Closed"))
			.Build()
		, components: new ComponentBuilder()
			.AddRow(new ActionRowBuilder()
				.WithButton("End Game", "endGame")
			).Build()
		);

		MafiaGame newGame = new() { 
			GM = cmd.User.Id,
			Guild = cmd.GuildId ?? ulong.MinValue,
			ControlPanel = controlPanelChannel.Id,
			Name = gameName,
			SanitizedName = sanitizedName,
		};

		await _db.CreateNewMafiaGame(newGame);
		await cmd.ModifyOriginalResponseAsync(x => x.Content = $"`{gameName}` has been created. Go to your control panel at https://discord.com/channels/{guild.Id}/{controlPanelChannel.Id} to continue setup of the game.");
	}

	private async Task StartCount(SocketSlashCommand ctx)
	{
		var game = new MafiaGame
		{
			Guild = ctx.GuildId ?? ulong.MinValue,
			Channel = ctx.ChannelId ?? ulong.MinValue,
			GM = ctx.User.Id,
		};

		await ctx.DeferAsync(true);
		var success = await _db.CreateNewMafiaGame(game);

		if (!success)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "A count is already going on in this channel. Use /stopvote to stop it or /resetvote to reset the tally.");
			return;
		}

		await ctx.ModifyOriginalResponseAsync(x => x.Content = "You've started a vote in this channel.");
	}

	private async Task StopCount(SocketSlashCommand ctx)
	{
		await ctx.DeferAsync(true);

		var game = await _db.GetMafiaGame(ctx.Channel.Id);

		if (game is null)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "There is no active game right now");
			return;
		}

		if (game.GM != ctx.User.Id)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "You are not the GM");
			return;
		}

		var success = await _db.DeleteMafiaGame(ctx.GuildId ?? ulong.MinValue, ctx.ChannelId ?? UInt64.MinValue);

		if (!success)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "It looks like there wasn't an active count in this channel.");
			return;
		}

		await ctx.ModifyOriginalResponseAsync(x => x.Content = "Successfully stopped the count");
	}

	private async Task ResetCount(SocketSlashCommand ctx)
	{
		await ctx.DeferAsync(true);

		var game = await _db.GetMafiaGame(ctx.Channel.Id);

		if (game is null)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "It seems there is no active count in this channel");
			return;
		}

		if (game.GM != ctx.User.Id)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "You are not the GM");
			return;
		}
		
		game.Votes.Clear();
		await _db.UpdateMafiaVotes(game);
		await ctx.ModifyOriginalResponseAsync(x => x.Content = "The count has been reset");
	}

	private async Task VoteTally(SocketSlashCommand ctx)
	{
		await ctx.DeferAsync();

		var game = await _db.GetMafiaGame(ctx.Channel.Id);

		if (game is null)
		{
			await ctx.ModifyOriginalResponseAsync(x =>
				x.Content =
					"There's no count happening in this channel. If this is a mistake yell at your GM, not me.");
			return;
		}

		await ctx.DeleteOriginalResponseAsync();
		await SendTally(ctx.Channel, game);
	}

	private async Task SendTally(ISocketMessageChannel channel, MafiaGame game)
	{
		var guild = _client.GetGuild(game.Guild);
		string content = "The vote count is now:";
		foreach (var vote in game.Tally)
		{
			var nickname = guild.GetUser(vote.Key).DisplayName;
			content += $"\n{nickname} ({vote.Value.Count}) - ";
			for (int i = 0; i < vote.Value.Count; i++)
			{
				var voter = vote.Value[i];
				var voterName = guild.GetUser(voter).DisplayName;
				content += voterName;

				if (i < vote.Value.Count - 1)
					content += ", ";
			}
		}

		await channel.SendMessageAsync(content);
	}
}