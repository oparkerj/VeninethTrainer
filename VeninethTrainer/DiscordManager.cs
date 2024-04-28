using System;
using System.Diagnostics.CodeAnalysis;
using VeninethTrainer.DiscordSdk;

namespace VeninethTrainer;

public class DiscordManager
{
    private const long ClientId = 1233917167016214561L;
    
    // Make this readonly once ClearActivity is fixed
    private Discord? _discord;

    private string? _mapName;
    private Activity? _activity;
    
    public void TryConnect()
    {
        try
        {
            _discord ??= new Discord(ClientId, (ulong) CreateFlags.NoRequireDiscord);
            _discord.GetActivityManager().UpdateActivity(_activity.GetValueOrDefault(), _ => { });
        }
        catch
        {
            // ignored
        }
    }

    public void Reset()
    {
        _discord?.Dispose();
        _discord = null;
    }

    public void Update()
    {
        try
        {
            _discord?.RunCallbacks();
        }
        catch (Exception)
        {
            Reset();
        }
    }

    public bool SetMap(string name)
    {
        if (name == _mapName) return false;
        _mapName = name;

        if (name == string.Empty)
        {
            _activity = null;
            _discord?.GetActivityManager().UpdateActivity(default, _ => { });
            return true;
        }

        var assetName = name.Replace(' ', '_')
            .Replace("(", "")
            .Replace(")", "")
            .ToLower();

        _activity = new Activity
        {
            Type = ActivityType.Playing,
            Details = name,
            Assets = new ActivityAssets
            {
                LargeImage = assetName,
                LargeText = name
            },
            Timestamps = new ActivityTimestamps
            {
                Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };
        _discord?.GetActivityManager().UpdateActivity(_activity.Value, _ => { });
        return true;
    }

    ~DiscordManager()
    {
        Reset();
    }
}