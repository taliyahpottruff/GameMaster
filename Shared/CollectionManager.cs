using System.Linq.Expressions;
using MongoDB.Driver;

namespace GameMaster.Shared;

internal class CollectionManager<T> where T : IDocument
{
    private IMongoCollection<T> Collection { get; }
    private List<T> Cache { get; }
    
    public CollectionManager(IMongoDatabase db, string collectionName)
    {
        Collection = db.GetCollection<T>(collectionName);
        Cache = new List<T>(Collection.Find(x => true).ToList());
    }

    public List<T> FindAll(Predicate<T> filter)
    {
        return Cache.FindAll(filter);
    }

    public T? Find(Predicate<T> filter)
    {
        return Cache.Find(filter);
    }

    public async Task Add(T obj)
    {
        Cache.Add(obj);
        await Collection.InsertOneAsync(obj);
    }

    public async Task Update(T obj)
    {
        var result = await Collection.ReplaceOneAsync(x => x.Id == obj.Id, obj);
        
        if (result.MatchedCount <= 0)
            throw new Exception("The object attempting to update does not exist in the collection");
        
        if (!Cache.Contains(obj))
            Cache.Add(obj);
    }

    public async Task<ulong> Delete(T obj)
    {
        var result = await Collection.DeleteManyAsync(x => x.Id == obj.Id);
        Cache.Remove(obj);
        return (ulong)result.DeletedCount;
    }

    public async Task<ulong> Delete(Predicate<T> filter)
    {
        var obj = Cache.Find(filter);
        
        if (obj is null)
            return 0;
        
        var result = await Collection.DeleteManyAsync(x => x.Id == obj.Id);
        Cache.Remove(obj);
        return (ulong)result.DeletedCount;
    }
}