using Discord.WebSocket;

namespace GameMaster;

public interface IDiscordHandler
{
	public Task RegisterCommands();
	public Task HandleSlashCommands(SocketSlashCommand command);
	public Task HandleMessages(SocketMessage message);
}