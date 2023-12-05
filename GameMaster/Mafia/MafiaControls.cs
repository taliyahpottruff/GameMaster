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
		switch (component.Data.CustomId)
		{
			case "endGame":
				await EndGameInitial(component);
				break;
		}
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
	#endregion
}