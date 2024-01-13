using System.Configuration;
using GameMaster.Shared.Mafia;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace GameMaster.Shared;

public class DataService
{
	private MongoClient _client;

	private readonly CollectionManager<MafiaGame> _mafiaCollection;
	private readonly CollectionManager<User> _userCollection;
	private IConfiguration Configuration { get; }
	
	public DataService(IConfiguration configuration)
	{
		Configuration = configuration;
		var connectionString = Configuration.GetValue<string>("MongoURI");
		_client = new MongoClient(connectionString);

		var db = _client.GetDatabase("gamemaster");
		_mafiaCollection = new CollectionManager<MafiaGame>(db, "mafia-games");
		_userCollection = new CollectionManager<User>(db, "users");
	}

	public async Task<bool> CreateNewMafiaGame(MafiaGame game)
	{
		await _mafiaCollection.Add(game);
		return true;
	}

	/// <summary>
	/// Get a mafia game by channel ID
	/// </summary>
	/// <param name="channel">ID of the control panel or game channel (if allowed)</param>
	/// <param name="allowGameChannel">Allow finding by the game channel</param>
	/// <returns>The mafia game if it exists</returns>
	public MafiaGame? GetMafiaGame(ulong channel, bool allowGameChannel = true)
	{
		var result = _mafiaCollection.Find(x => (x.Channel == channel && allowGameChannel) || x.ControlPanel == channel);
		return result;
	}

	public MafiaGame? GetMafiaGame(string id)
	{
		var result = _mafiaCollection.Find(x => x.Id == id);
		return result;
	}

	public List<MafiaGame> GetAllMafiaGamesManagedByUser(ulong user)
	{
		var result = _mafiaCollection.FindAll(x => x.GM == user);
		return result;
	}

	public async Task UpdateMafiaGame(MafiaGame game)
	{
		await _mafiaCollection.Update(game);
	}

	public async Task SetMafiaGameChannel(ulong controlPanel, ulong channel)
	{
		var game = _mafiaCollection.Find(x => x.ControlPanel == controlPanel);
		
		if (game is null)
			return;
		
		game.Channel = channel;
		await _mafiaCollection.Update(game);
	}
	
	/// <summary>
	/// Set the game chat status
	/// </summary>
	/// <param name="channel">Either the control panel or game chat ID</param>
	/// <param name="status">The status you want to set</param>
	public async Task SetMafiaGameChatStatus(ulong channel, MafiaGame.GameChatStatus status)
	{
		var game = _mafiaCollection.Find(x => x.ControlPanel == channel || x.Channel == channel);
		
		if (game is null)
			return;

		game.ChatStatus = status;
		await _mafiaCollection.Update(game);
	}

	public async Task<bool> DeleteMafiaGame(ulong channel)
	{
		var result = await _mafiaCollection.Delete(x => x.Channel == channel || x.ControlPanel == channel);
		return result > 0;
	}

	public bool MafiaGameExists(ulong channel, bool gameChannelAllowed = true)
	{
		var game = _mafiaCollection.Find(x => (x.Channel == channel && gameChannelAllowed) || x.ControlPanel == channel);
		return game is not null;
	}

	public async Task<bool> AddPlayerToMafiaGame(ulong controlPanel, ulong player)
	{
		var game = _mafiaCollection.Find(x => x.ControlPanel == controlPanel);

		if (game is null)
			return false;
		
		game.Players.Add(player);
		await _mafiaCollection.Update(game);
		return true;
	}
	
	public async Task<bool> RemovePlayerFromMafiaGame(ulong controlPanel, ulong player)
	{
		var game = _mafiaCollection.Find(x => x.ControlPanel == controlPanel);

		if (game is null)
			return false;
		
		game.Players.Remove(player);
		await _mafiaCollection.Update(game);
		return true;
	}

	#region User

	public async Task<bool> AddUser(ulong discordId, string refreshToken)
	{
		var existing = _userCollection.Find(x => x.DiscordId == discordId);

		if (existing is not null)
			return false;

		await _userCollection.Add(new User()
		{
			DiscordId = discordId,
			RefreshToken = refreshToken,
		});
		return true;
	}
	
	#endregion
}