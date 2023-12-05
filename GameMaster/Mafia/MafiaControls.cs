using Discord;
using Discord.WebSocket;
using MongoDB.Bson;

namespace GameMaster.Mafia;

public class MafiaControls : IDiscordHandler
{
	private readonly DiscordSocketClient _client;
	private readonly DataService _db;

	public MafiaControls(DiscordSocketClient client, DataService db)
	{
		_client = client;
		_db = db;

		_client.ButtonExecuted += HandleInteractions;
	}

	public Task RegisterCommands()
	{
		throw new NotImplementedException();
	}

	public Task HandleSlashCommands(SocketSlashCommand command)
	{
		throw new NotImplementedException();
	}

	public Task HandleMessages(SocketMessage message)
	{
		throw new NotImplementedException();
	}

	public async Task HandleInteractions(SocketMessageComponent component)
	{
		await Task.Run(() =>
		{
			switch (component.Data.CustomId)
			{
				case "endGame":
					_ = EndGameInitial(component);
					break;
				case "endGame-keep":
					_ = EndGameKeep(component);
					break;
				case "endGame-delete":
					_ = EndGameDelete(component);
					break;
			}
		});
	}

	#region Button Handlers

	private async Task EndGameInitial(SocketMessageComponent button)
	{
		await button.RespondAsync(text: "What would you like to do with your game channels?", components: new ComponentBuilder()
			.AddRow(new ActionRowBuilder()
				.WithButton("Keep them (will be viewable)", "endGame-keep", ButtonStyle.Success)
				.WithButton("Delete them (irreversible)", "endGame-delete", ButtonStyle.Danger))
			.Build());

		await Task.Delay(60000);
		await button.DeleteOriginalResponseAsync();
	}

	private async Task EndGameKeep(SocketMessageComponent button)
	{
		await button.DeferAsync();
		
		var game = await _db.GetMafiaGame(button.Channel.Id);
		bool success = await _db.DeleteMafiaGame(button.Channel.Id);

		if (!success || game is null)
		{
			await button.ModifyOriginalResponseAsync(x => x.Content =
				"Something went wrong. There doesn't seem to be an active mafia game using this channel. Please delete it manually.");
			return;
		}

		await button.ModifyOriginalResponseAsync(x => x.Content = "Game removed from database... please wait.");

		var guild = _client.GetGuild(game.Guild);

		if (game.Channel > ulong.MinValue)
		{
			if (await _client.GetChannelAsync(game.Channel) is SocketTextChannel channel)
			{
				await channel.RemovePermissionOverwriteAsync(guild.EveryoneRole);
				await channel.RemovePermissionOverwriteAsync(button.User);
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
		await ((SocketGuildChannel)button.Channel).DeleteAsync();
	}

	private async Task EndGameDelete(SocketMessageComponent button)
	{
		await button.RespondAsync("Not yet implemented");
	}
	#endregion
}