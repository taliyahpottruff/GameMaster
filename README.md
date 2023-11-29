# Game Master
Game Master is an open-source Discord bot dedicated to managing interactive text-based games for your server.

## Using the source code
Any and all private tokens and URIs must be in `./GameMaster/app.config`. Use the following template:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <appSettings>
        <add key="DiscordToken" value="[Your Discord bot token]" />
        <add key="MongoURI" value="[Your mongo connection string]" />
    </appSettings>
</configuration>
```

## Contributing
Contributions are welcome. Please check the Issues page for a list of features and bugs that need work. For new features, be sure to branch off of the `dev` branch.
