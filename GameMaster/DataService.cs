using System.Configuration;
using GameMaster.Mafia;
using MongoDB.Driver;

namespace GameMaster;

public class DataService
{
	private MongoClient _client;

	private readonly IMongoCollection<MafiaGame> _mafiaCollection;
	
	public DataService()
	{
		_client = new MongoClient(ConfigurationManager.AppSettings["MongoURI"] ?? string.Empty);

		var db = _client.GetDatabase("gamemaster");
		_mafiaCollection = db.GetCollection<MafiaGame>("mafia-games");
	}

	public async Task<bool> CreateNewMafiaGame(MafiaGame game)
	{
		/*var count = await _mafiaCollection.CountDocumentsAsync(x => x.Guild == game.Guild && x.Channel == game.Channel);

		if (count > 0)
			return false;*/

		await _mafiaCollection.InsertOneAsync(game);
		return true;
	}

	public async Task<MafiaGame?> GetMafiaGame(ulong channel)
	{
		var result = await _mafiaCollection.Find(x => x.Channel == channel || x.ControlPanel == channel).FirstOrDefaultAsync();
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
}