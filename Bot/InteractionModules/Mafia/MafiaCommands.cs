using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GameMaster.Bot.Extensions;
using GameMaster.Bot.Services.Mafia;
using GameMaster.Shared;
using GameMaster.Shared.Mafia;

namespace GameMaster.Bot.InteractionModules.Mafia;

public class MafiaCommands : InteractionModuleBase
{
	private readonly DiscordSocketClient _client;
	private readonly DataService _db;
	private MafiaCommandService Service { get; }
	
	public MafiaCommands(DiscordSocketClient client, DataService db, MafiaCommandService service)
	{
		_client = client;
		_db = db;
		Service = service;
	}

	public static async Task HandleMessages(DiscordSocketClient client, DataService db, SocketMessage ctx)
	{
		var msg = (SocketUserMessage)ctx;
		var content = msg.CleanContent;
		if (content.ToLower().StartsWith("lynch"))
		{
			var mafiaGame = await db.GetMafiaGame(msg.Channel.Id);
			if (mafiaGame is null)
				return;

			var guild = client.GetGuild(mafiaGame.Guild);
			
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
			await db.UpdateMafiaVotes(mafiaGame);
			
			// Inform the players of the new count
			string tally = await FormatTally(client, mafiaGame);
			await msg.Channel.SendMessageAsync(tally);
		}
		else if (content.ToLower().StartsWith("unlynch"))
		{
			var mafiaGame = await db.GetMafiaGame(msg.Channel.Id);
			if (mafiaGame is null)
				return;

			var guild = client.GetGuild(mafiaGame.Guild);

			var existingVote = mafiaGame.Votes.Find(x => x.From == ctx.Author.Id);
			
			if (existingVote is null)
				return;

			mafiaGame.Votes.Remove(existingVote);
			await db.UpdateMafiaVotes(mafiaGame);
			string tally = await FormatTally(client, mafiaGame);
			await msg.Channel.SendMessageAsync(tally);
		}
	}


	[SlashCommand("newmafiagame", "Start new mafia game")]
	private async Task NewGame(string name, bool createChannel = false)
	{
		await DeferAsync(true);
		
		var guild = Context.Guild;
		if (guild is null)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "This command can only be run in a server");
			return;
		}

		var result = await Service.NewMafiaGame((ITextChannel)Context.Channel, Context.User, name, createChannel);
		
		if (result.Success)
			await ModifyOriginalResponseAsync(x => x.Content = $"`{name}` has been created. Go to your control panel at https://discord.com/channels/{guild.Id}/{result.Payload} to continue setup of the game.");
	}

	[SlashCommand("startvote", "Start a new vote in this channel")]
	private async Task StartCount()
	{
		if (Context.Guild is null)
			return;

		var game = new MafiaGame
		{
			Guild = Context.Guild.Id,
			Channel = Context.Channel.Id,
			GM = Context.User.Id,
		};

		await DeferAsync(true);
		var success = await _db.CreateNewMafiaGame(game);

		if (!success)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "A count is already going on in this channel. Use /stopvote to stop it or /resetvote to reset the tally.");
			return;
		}

		await ModifyOriginalResponseAsync(x => x.Content = "You've started a vote in this channel.");
	}

	[SlashCommand("stopvote", "Stop the vote in this channel")]
	private async Task StopCount()
	{
		await DeferAsync(true);

		var game = await _db.GetMafiaGame(Context.Channel.Id);

		if (game is null)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "There is no active game right now");
			return;
		}

		if (game.GM != Context.User.Id)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "You are not the GM");
			return;
		}

		var success = await _db.DeleteMafiaGame(Context.Channel.Id);

		if (!success)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "It looks like there wasn't an active count in this channel.");
			return;
		}

		await ModifyOriginalResponseAsync(x => x.Content = "Successfully stopped the count");
	}


	[SlashCommand("resetvote", "Clear all of the votes going on in this channel")]
	private async Task ResetCount()
	{
		await DeferAsync(true);

		var game = await _db.GetMafiaGame(Context.Channel.Id);

		if (game is null)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "It seems there is no active count in this channel");
			return;
		}

		if (game.GM != Context.User.Id)
		{
			await ModifyOriginalResponseAsync(x => x.Content = "You are not the GM");
			return;
		}
		
		game.Votes.Clear();
		await _db.UpdateMafiaVotes(game);
		await ModifyOriginalResponseAsync(x => x.Content = "The count has been reset");
	}

	[SlashCommand("tally", "Show the vote tally in this channel")]
	private async Task VoteTally()
	{
		await DeferAsync();

		var game = await _db.GetMafiaGame(Context.Channel.Id);

		if (game is null)
		{
			await ModifyOriginalResponseAsync(x =>
				x.Content =
					"There's no count happening in this channel. If this is a mistake yell at your GM, not me.");
			return;
		}

		await ModifyOriginalResponseAsync(async (x) => x.Content = await FormatTally(_client,  game));
	}

	private static async Task<string> FormatTally(DiscordSocketClient client, MafiaGame game)
	{
		var guild = client.GetGuild(game.Guild);
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

		return content;
	}
}