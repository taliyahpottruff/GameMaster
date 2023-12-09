using Discord;
using Discord.Interactions;
using Discord.WebSocket;

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
		await DeleteOriginalResponseAsync();
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
			if (await _client.GetChannelAsync(game.Channel) is SocketTextChannel channel)
			{
				await channel.RemovePermissionOverwriteAsync(guild.EveryoneRole);
				await channel.RemovePermissionOverwriteAsync(Context.User);
			}
		}

		foreach (var channelId in game.GameChannels)
		{
			if (await _client.GetChannelAsync(game.Channel) is SocketTextChannel channel)
			{
				await channel.RemovePermissionOverwriteAsync(guild.EveryoneRole);
			}
		}

		//await button.ModifyOriginalResponseAsync(x => x.Content= "Now deleting control panel...");
		await ((SocketGuildChannel)Context.Channel).DeleteAsync();
	}
	#endregion
}