using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Shared;

public class User : IDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId), BsonElement("_id")]
    public string Id { get; set; } = string.Empty;
    public ulong DiscordId { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
}