using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace GameMaster;

public class MafiaCommands
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
			.WithName("test")
			.WithDescription("Testing command")
			.Build());
	}

	public async Task SlashCommandHandler(SocketSlashCommand command)
	{
		switch (command.Data.Name)
		{
			case "startvote":
				await StartCount(command);
				break;
		}
	}

	public async Task HandleMessages(SocketMessage ctx)
	{
		var msg = (SocketUserMessage)ctx;
		if (msg.Content.ToLower().StartsWith("lynch"))
		{
			var mafiaGame = await _db.GetMafiaGame(msg.Channel.Id);
			if (mafiaGame is null)
				return;

			var guild = _client.GetGuild(mafiaGame.Guild);
			
			var mentionedUsers = msg.MentionedUsers;
			SocketUser? votingAgainst = null;
			
			if (mentionedUsers.Count < 1)
			{
				// TODO Find users by name
				var parts = msg.Content.Split(" ");
				if (parts.Length < 2)
				{
					await msg.ReplyAsync("Please specify who you want to lynch");
					return;
				}
				
				var userToSearchFor = parts[1].ToLower();
				var usersInChannel = await msg.Channel.GetUsersAsync().FlattenAsync();
				SocketGuildUser? currentUser = null;
				foreach (var user in usersInChannel)
				{
					var guildUser = guild.GetUser(user.Id);

					if (guildUser is null)
						continue;

					if (guildUser.DisplayName.ToLower().Contains(userToSearchFor))
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
			string content = "The vote count is now:";
			foreach (var vote in mafiaGame.Tally)
			{
				var nickname = guild.GetUser(vote.Key).DisplayName;
				content += $"\n{nickname} - {vote.Value}";
			}

			await msg.Channel.SendMessageAsync(content);
		}
	}

	private async Task StartCount(SocketSlashCommand ctx)
	{
		var game = new MafiaGame
		{
			Guild = ctx.GuildId ?? ulong.MinValue,
			Channel = ctx.ChannelId ?? ulong.MinValue,
		};

		await ctx.DeferAsync(true);
		var success = await _db.CreateNewMafiaGame(game);

		if (!success)
		{
			await ctx.ModifyOriginalResponseAsync(x => x.Content = "A count is already going on in this channel. Use /stopcount to stop it or /resetcount to reset the tally.");
			return;
		}

		await ctx.ModifyOriginalResponseAsync(x => x.Content = "You've started a vote in this channel.");
	}
}