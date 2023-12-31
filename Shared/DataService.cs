using System.Configuration;
using GameMaster.Shared.Mafia;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace GameMaster.Shared;

public class DataService
{
	private MongoClient _client;

	private readonly IMongoCollection<MafiaGame> _mafiaCollection;
	private readonly IMongoCollection<User> _userCollection;
	private IConfiguration Configuration { get; }
	
	public DataService(IConfiguration configuration)
	{
		Configuration = configuration;
		var connectionString = Configuration.GetValue<string>("MongoURI");
		_client = new MongoClient(connectionString);

		var db = _client.GetDatabase("gamemaster");
		_mafiaCollection = db.GetCollection<MafiaGame>("mafia-games");
		_userCollection = db.GetCollection<User>("users");
	}

	public async Task<bool> CreateNewMafiaGame(MafiaGame game)
	{
		/*var count = await _mafiaCollection.CountDocumentsAsync(x => x.Guild == game.Guild && x.Channel == game.Channel);

		if (count > 0)
			return false;*/

		await _mafiaCollection.InsertOneAsync(game);
		return true;
	}

	/// <summary>
	/// Get a mafia game by channel ID
	/// </summary>
	/// <param name="channel">ID of the control panel or game channel (if allowed)</param>
	/// <param name="allowGameChannel">Allow finding by the game channel</param>
	/// <returns>The mafia game if it exists</returns>
	public async Task<MafiaGame?> GetMafiaGame(ulong channel, bool allowGameChannel = true)
	{
		var result = await _mafiaCollection.Find(x => (x.Channel == channel && true) || x.ControlPanel == channel).FirstOrDefaultAsync();
		return result;
	}

	public async Task UpdateMafiaVotes(MafiaGame game)
	{
		var filter = Builders<MafiaGame>.Update.Set("Votes", game.Votes);
		await _mafiaCollection.UpdateOneAsync(x => x.Guild == game.Guild && x.Channel == game.Channel,
			filter);
	}

	public async Task SetMafiaGameChannel(ulong controlPanel, ulong channel)
	{
		var update = Builders<MafiaGame>.Update.Set("Channel", channel);
		await _mafiaCollection.UpdateOneAsync(x => x.ControlPanel == controlPanel, update);
	}

	/// <summary>
	/// Set the game chat status
	/// </summary>
	/// <param name="channel">Either the control panel or game chat ID</param>
	/// <param name="status">The status you want to set</param>
	public async Task SetMafiaGameChatStatus(ulong channel, MafiaGame.GameChatStatus status)
	{
		var update = Builders<MafiaGame>.Update.Set("ChatStatus", status);
		await _mafiaCollection.UpdateOneAsync(x => x.ControlPanel == channel || x.Channel == channel, update);
	}

	public async Task<bool> DeleteMafiaGame(ulong channel)
	{
		var result = await _mafiaCollection.DeleteManyAsync(x => x.Channel == channel || x.ControlPanel == channel);
		return result.DeletedCount > 0;
	}

	public async Task<bool> MafiaGameExists(ulong channel)
	{
		var count = await _mafiaCollection.CountDocumentsAsync(x => x.Channel == channel);
		return count > 0;
	}

	public async Task<bool> AddPlayerToMafiaGame(ulong controlPanel, ulong player)
	{
		var update = Builders<MafiaGame>.Update.AddToSet("Players", player);
		var count = await _mafiaCollection.UpdateOneAsync(x => x.ControlPanel == controlPanel, update);
		return count.MatchedCount > 0;
	}
	
	public async Task<bool> RemovePlayerFromMafiaGame(ulong controlPanel, ulong player)
	{
		var update = Builders<MafiaGame>.Update.Pull("Players", player);
		var count = await _mafiaCollection.UpdateOneAsync(x => x.ControlPanel == controlPanel, update);
		return count.MatchedCount > 0;
	}

	#region User

	public async Task<bool> AddUser(ulong discordId, string refreshToken)
	{
		var existing = await _userCollection.Find(x => x.DiscordId == discordId).FirstOrDefaultAsync();

		if (existing is not null)
			return false;

		await _userCollection.InsertOneAsync(new User()
		{
			DiscordId = discordId,
			RefreshToken = refreshToken,
		});
		return true;
	}
	
	#endregion
}