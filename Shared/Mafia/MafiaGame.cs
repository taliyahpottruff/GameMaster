using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Shared.Mafia;

[Serializable]
public class MafiaGame : IDocument
{
	[BsonId, BsonRepresentation(BsonType.ObjectId), BsonElement("_id")]
	public string Id { get; set; } = string.Empty;

	public string Name
	{
		get => _name;
		set
		{
			_name = value;
			Updated?.Invoke();
		}
	}

	public string SanitizedName { get; set; } = string.Empty;
	public ulong Guild { get; set; }
	public ulong ControlPanel { get; set; }
	public ulong ControlPanelMessage { get; set; }
	/// <summary>
	/// The primary game chat. Also known as a "day chat"
	/// </summary>
	public ulong Channel { 
		get => _channel;
		set
		{
			_channel = value;
			Updated?.Invoke();
		} 
	}
	/// <summary>
	/// Any additional channels including scum chats
	/// </summary>
	public GameChatStatus ChatStatus { 
		get => _chatStatus;
		set
		{
			_chatStatus = value;
			Updated?.Invoke();
		} 
	}

	public List<ulong> GameChannels
	{
		get => _gameChannels;
		set
		{
			_gameChannels.Clear();
			_gameChannels.AddRange(value);
			Updated?.Invoke();
		}
	}

	public ulong GM { get; set; }

	public List<ulong> Players
	{
		get => _players;
		set
		{
			_players.Clear();
			_players.AddRange(value);
			Updated?.Invoke();
		}
	}

	public bool VotingOpen
	{
		get => _votingOpen;
		set
		{
			_votingOpen = value;
			Updated?.Invoke();
		}
	}

	public List<Vote> Votes
	{
		get => _votes;
		set
		{
			_votes.Clear();
			_votes.AddRange(value);
			Updated?.Invoke();
		}
	}

	private string _name = string.Empty;
	private ulong _channel;
	private GameChatStatus _chatStatus;
	private List<ulong> _gameChannels = new();
	private List<ulong> _players = new();
	private bool _votingOpen;
	private List<Vote> _votes = new();
	
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
	
	[BsonIgnore]
	public Action? Updated { get; set; }

	public string ChatStatusAsString()
	{
		return ChatStatus switch
		{
			GameChatStatus.NotCreated => "Not created",
			GameChatStatus.Unviewable => "Not viewable",
			GameChatStatus.Closed => "Closed",
			GameChatStatus.Open => "Open",
			_ => "[Error]"
		};
	}

	public class Vote
	{
		public ulong From { get; init; }
		public ulong Against { get; set; }
	}

	public enum GameChatStatus
	{
		NotCreated = 0,
		Unviewable = 1,
		Closed = 2,
		Open = 3,
	}
}