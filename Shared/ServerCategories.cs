namespace GameMaster.Shared;

public class ServerCategories(ulong guildId, string guildName, string guildIconUrl)
{
    public ulong GuildId { get; } = guildId;
    public string GuildName { get; } = guildName;
    public string GuildIconUrl { get; } = guildIconUrl;
    public List<Category> Categories { get; } = [];

    public class Category(ulong id, string name)
    {
        public ulong Id { get; set; } = id;
        public string Name { get; set; } = name;
    }
}