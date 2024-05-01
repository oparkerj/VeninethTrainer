using System;
using System.Diagnostics.CodeAnalysis;
using VeninethTrainer.DiscordSdk;

namespace VeninethTrainer;

public class DiscordManager
{
    private const long ClientId = 1233917167016214561L;
    
    // Make this readonly once ClearActivity is fixed
    private Discord? _discord;
    
    private Activity _activity;
    private bool _updated;

    private long _startTime;
    private string? _mapName;
    private GameHookManager.BallType _ballType = GameHookManager.BallType.Unknown;

    public void TryConnect()
    {
        try
        {
            if (_discord is null)
            {
                _updated = true;
            }
            _discord ??= new Discord(ClientId, (ulong) CreateFlags.NoRequireDiscord);
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
            UpdateActivity();
            _discord?.RunCallbacks();
        }
        catch (Exception)
        {
            Reset();
        }
    }

    private void UpdateActivity()
    {
        if (!_updated) return;
        _updated = false;

        if (string.IsNullOrEmpty(_mapName))
        {
            _activity = default;
            _discord?.GetActivityManager().UpdateActivity(_activity, _ => { });
            return;
        }
        
        var assetName = _mapName.Replace(' ', '_')
            .Replace("(", "")
            .Replace(")", "")
            .ToLower();

        var (ballName, ballAsset) = GameHookManager.GetBallInfo(_ballType);
        
        _activity = new Activity
        {
            Type = ActivityType.Playing,
            Details = _mapName,
            Assets = new ActivityAssets
            {
                LargeImage = assetName,
                LargeText = _mapName,
                SmallImage = ballAsset,
                SmallText = ballName
            },
            Timestamps = new ActivityTimestamps
            {
                Start = _startTime
            }
        };
        
        _discord?.GetActivityManager().UpdateActivity(_activity, _ => { });
    }

    public bool SetMap(string name)
    {
        if (name == _mapName) return false;
        _mapName = name;
        _startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _updated = true;
        return true;
    }

    public void SetBallType(GameHookManager.BallType type)
    {
        if (type == _ballType) return;
        _ballType = type;
        _updated = true;
    }

    ~DiscordManager()
    {
        Reset();
    }
}