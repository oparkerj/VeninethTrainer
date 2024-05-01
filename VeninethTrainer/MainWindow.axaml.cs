using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace VeninethTrainer;

public partial class MainWindow : Window
{
    public const float FlySpeed = 50;
    
    private readonly GlobalKeyListener _keyListener = new();
    private readonly GameHookManager _game = new();
    private readonly DiscordManager _discordManager;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _discordTimer;

    private readonly SingleUseValue<float> _gameSpeed = new();

    private readonly TimeSpan _highTickRate = TimeSpan.FromSeconds(1) / 60;
    private readonly TimeSpan _lowTickRate = TimeSpan.FromSeconds(0.5);

    private bool _flyForward;
    private float? _zHold;
    private Vector3F _lastPos;
    
    private readonly Dictionary<string, TeleportInfo> _teleports = new();

    public MainWindow()
    {
        InitializeComponent();
        
        _keyListener.HookedKeys[Key.W] = OnWKey;
        _keyListener.SetDownHandler(Key.F1, ToggleFly);
        _keyListener.SetDownHandler(Key.F4, ChangeGameSpeed);
        _keyListener.SetDownHandler(Key.F5, SavePosition);
        _keyListener.SetDownHandler(Key.F6, TeleportOnly);
        _keyListener.SetDownHandler(Key.F7, TeleportWithVelocity);

        _discordManager = new DiscordManager();

        _updateTimer = new DispatcherTimer(_lowTickRate, DispatcherPriority.Normal, Update);
        _updateTimer.Start();
        _discordTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, _) => _discordManager.TryConnect());
        _discordTimer.Start();
    }

    private bool SetTickRate(TimeSpan interval)
    {
        if (_updateTimer.Interval == interval) return false;
        _updateTimer.Interval = interval;
        return true;
    }

    private void Update(object? sender, EventArgs e)
    {
        if (!_game.UpdateHookStatus())
        {
            // Game not hooked
            SetTickRate(_lowTickRate);
            _discordManager.SetMap(string.Empty);
            _discordManager.Update();
            return;
        }

        if (SetTickRate(_highTickRate))
        {
            GameSpeedLabel.Content = $"{_game.GameSpeed:0.0}x";
        }
        
        MapText.Text = _game.Map;
        if (_discordManager.SetMap(MapText.Text))
        {
            _zHold = null;
        }
        _discordManager.SetBallType(_game.CurrentBallType);
        
        if (_gameSpeed.TryGet(out var gameSpeed))
        {
            _game.GameSpeed = gameSpeed;
            GameSpeedLabel.Content = $"{gameSpeed:0.0}x";
        }

        var newLine = Environment.NewLine;
        var position = _game.Position;
        var (x, y, z) = position;
        PositionText.Text = $"{FormatUnits(x)}{newLine}{FormatUnits(y)}{newLine}{FormatUnits(z)}";
        
        // Reset Z hold if horizontal position moved
        if (Math.Abs(x - _lastPos.X) > 0.01f || Math.Abs(y - _lastPos.Y) > 0.01f)
        {
            _zHold = null;
        }
        _lastPos = position;

        var (xv, yv, _) = _game.Velocity;
        var horizontalVelocity = Math.Round(Math.Sqrt(xv * xv + yv * yv), MidpointRounding.ToPositiveInfinity);
        SpeedText.Text = $"{FormatUnits((float) horizontalVelocity)} m/s";

        if (_game.Fly)
        {
            if (_flyForward)
            {
                var (v, h) = _game.Camera;
                var angles = new Vector(h, v) * (Math.PI / 180d);
            
                var verticalVelocity = UnitVector(angles.Y);
                var nonUpMultiplier = 1f - Math.Abs(verticalVelocity.Y);
                verticalVelocity *= 10000;
                var zVelocity = (float) verticalVelocity.Y;

                var newHorizontal = UnitVector(angles.X) * (FlySpeed * 100 * nonUpMultiplier);
                var xVelocity = (float) newHorizontal.X;
                var yVelocity = (float) newHorizontal.Y;
            
                _game.Velocity = new Vector3F(xVelocity, yVelocity, zVelocity);
            }
            else
            {
                _zHold ??= z;
                _game.Velocity = default;
                _game.Position = new Vector3F(x, y, _zHold.Value);
            }
        }
        ToggleLabel(_game.Fly, FlyToggleLabel);
        
        _discordManager.Update();
        return;

        Vector UnitVector(double radians) => new(Math.Cos(radians), Math.Sin(radians));
        string FormatUnits(float value) => $"{value / 100f:0.00}";
    }
    
    private bool OnWKey(bool down)
    {
        _flyForward = down;
        _zHold = null;
        return _game.Fly;
    }

    private void ToggleFly()
    {
        _zHold = null;
        _game.Fly = !_game.Fly;
    }

    private void ChangeGameSpeed()
    {
        var newSpeed = _game.GameSpeed switch
        {
            < 1f => 1f,
            < 1.5f => 2f,
            < 3f => 4f,
            > 3f => 0.5f,
            _ => 1f,
        };
        _gameSpeed.Set(newSpeed);
    }

    private void SavePosition()
    {
        var map = _game.Map;
        if (map.Length == 0) return;
        _teleports[map] = new TeleportInfo(_game.Position, _game.Velocity, _game.Camera);
    }

    private void Teleport(bool withVelocity = false)
    {
        var map = _game.Map;
        if (!_teleports.TryGetValue(map, out var teleport)) return;
        
        _game.Position = teleport.Position;
        _game.Velocity = withVelocity ? teleport.Velocity : default;
        _game.Camera = teleport.View;
    }

    private void TeleportOnly() => Teleport();

    private void TeleportWithVelocity() => Teleport(true);

    private void OnFlyModeClicked(object sender, RoutedEventArgs args)
    {
        ToggleFly();
    }

    private void OnGameSpeedClicked(object sender, RoutedEventArgs args)
    {
        ChangeGameSpeed();
    }
    
    private void OnSavePositionClicked(object sender, RoutedEventArgs args)
    {
        SavePosition();
    }

    private void OnTeleportClicked(object sender, RoutedEventArgs args)
    {
        TeleportOnly();
    }
    
    private void OnTeleportVelocityClicked(object sender, RoutedEventArgs args)
    {
        TeleportWithVelocity();
    }

    private void ToggleLabel(bool state, Label label)
    {
        if (state)
        {
            label.Content = "ON";
            label.Foreground = Brushes.Green;
        }
        else
        {
            label.Content = "OFF";
            label.Foreground = Brushes.Red;
        }
    }
}