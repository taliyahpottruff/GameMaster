﻿namespace GameMaster.Web.Shared;

public class State
{
    private string _discordId = String.Empty;
    private string _discordUsername = String.Empty;
    private string _discordDisplayName = String.Empty;
    private string _discordAvatar = string.Empty;
    
    public string DiscordId { 
        get => _discordId;
        set
        {
            _discordId = value;
            OnStateChanged?.Invoke();
        }
    }

    public string DiscordUsername
    {
        get => _discordUsername;
        set
        {
            _discordUsername = value;
            OnStateChanged?.Invoke();
        }
    }

    public string DiscordDisplayName
    {
        get => _discordDisplayName;
        set
        {
            _discordDisplayName = value;
            OnStateChanged?.Invoke();
        }
    }

    public string DiscordAvatar
    {
        get => _discordAvatar;
        set
        {
            _discordAvatar = value;
            OnStateChanged?.Invoke();
        }
    }

    public event Action? OnStateChanged;
}