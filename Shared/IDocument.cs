using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Shared;

public interface IDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId), BsonElement("_id")]
    public string Id { get; set; }
}