using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Shared;

public class User : IDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId), BsonElement("_id")]
    public string Id
    {
        get => _id;
        set
        {
            _id = value;
            Updated?.Invoke();
        }
    }

    public ulong DiscordId
    {
        get => _discordId;
        set
        {
            _discordId = value;
            Updated?.Invoke();
        }
    }

    public string RefreshToken
    {
        get => _refreshToken;
        set
        {
            _refreshToken = value;
            Updated?.Invoke();
        }
    }

    private string _id = string.Empty;
    private ulong _discordId = ulong.MinValue;
    private string _refreshToken = string.Empty;
    
    [BsonIgnore]
    public Action? Updated { get; set; }
}