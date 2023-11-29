using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster;

[Serializable]
public class MafiaGame
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string _id { get; set; } = string.Empty;
	public ulong Guild { get; set; }
	public ulong Channel { get; set; }
	public List<Vote> Votes { get; set; } = new();

	[BsonIgnore]
	public Dictionary<ulong, uint> Tally
	{
		get
		{
			Dictionary<ulong, uint> tally = new();
			foreach (var vote in Votes)
			{
				if (tally.ContainsKey(vote.Against))
				{
					tally[vote.Against] += 1;
				}
				else
				{
					tally.Add(vote.Against, 1);
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