using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Mafia;

[Serializable]
public class MafiaGame
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string _id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string SanitizedName { get; set; } = string.Empty;
	public ulong Guild { get; set; }
	public ulong ControlPanel { get; set; }
	public ulong Channel { get; set; }
	public ulong GM { get; set; }
	public List<ulong> Players { get; set; } = new();
	public List<Vote> Votes { get; set; } = new();

	[BsonIgnore]
	public Dictionary<ulong, List<ulong>> Tally
	{
		get
		{
			Dictionary<ulong, List<ulong>> tally = new();
			foreach (var vote in Votes)
			{
				if (tally.ContainsKey(vote.Against))
				{
					tally[vote.Against].Add(vote.From);
				}
				else
				{
					tally.Add(vote.Against, new List<ulong>() { vote.From });
				}
			}

			return tally;
		}
	}

	public class Vote
	{
		public ulong From { get; set; }
		public ulong Against { get; set; }
	}
}