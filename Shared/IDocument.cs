using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameMaster.Shared;

public interface IDocument
{
    public string Id { get; set; }
    public Action? Updated { get; set; }
}