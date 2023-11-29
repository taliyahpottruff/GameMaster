// See https://aka.ms/new-console-template for more information

using System.Configuration;
using DSharpPlus;

var discord = new DiscordClient(new DiscordConfiguration()
{
	Token = ConfigurationManager.AppSettings["DiscordToken"] ?? string.Empty,
	TokenType = TokenType.Bot,
	Intents = DiscordIntents.All
});

discord.MessageCreated += async (sender, e) =>
{
	if (e.Author.IsBot)
		return;

	if (e.Message.Content.ToLower().StartsWith("ping"))
	{
		await e.Message.RespondAsync("Pong");
	}
};

await discord.ConnectAsync();
await Task.Delay(-1);