# Game Master
Game Master is an open-source Discord bot dedicated to managing interactive text-based games for your server.

## Using the source code
Any and all private tokens and URIs must be in `./Server/appsettings.json`. Use the following template:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DiscordToken": "[Bot token for connecting to API]",
  "DiscordClientSecret": "[API client secret]",
  "MongoURI": "[MongoDB URI]"
}
```

## Contributing
Contributions are welcome. Please check the Issues page for a list of features and bugs that need work. For new features, be sure to branch off of the `dev` branch.
