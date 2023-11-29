using System.Configuration;
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
		var count = await _mafiaCollection.CountDocumentsAsync(x => x.Guild == game.Guild && x.Channel == game.Channel);

		if (count > 0)
			return false;

		await _mafiaCollection.InsertOneAsync(game);
		return true;
	}

	public async Task<MafiaGame?> GetMafiaGame(ulong channel)
	{
		var result = await _mafiaCollection.Find(x => x.Channel == channel).FirstOrDefaultAsync();
		return result;
	}

	public async Task UpdateMafiaVotes(MafiaGame game)
	{
		var filter = Builders<MafiaGame>.Update.Set("Votes", game.Votes);
		await _mafiaCollection.UpdateOneAsync(x => x.Guild == game.Guild && x.Channel == game.Channel,
			filter);
	}

	public async Task DeleteMafiaGame(ulong guild, ulong channel)
	{
		await _mafiaCollection.DeleteManyAsync(x => x.Guild == guild && x.Channel == channel);
	}

	public async Task<bool> MafiaGameExists(ulong channel)
	{
		var count = await _mafiaCollection.CountDocumentsAsync(x => x.Channel == channel);
		return count > 0;
	}
}