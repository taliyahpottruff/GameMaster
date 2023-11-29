using System.Configuration;
using MongoDB.Driver;

namespace GameMaster;

public class DataService
{
	private MongoClient _client;
	
	public DataService()
	{
		_client = new MongoClient(ConfigurationManager.AppSettings["MongoURI"] ?? string.Empty);
	}
}