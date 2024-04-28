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
    private readonly DispatcherTimer _updateTimer;

    private readonly SingleUseValue<float> _gameSpeed = new();

    private readonly TimeSpan _highTickRate = TimeSpan.FromSeconds(1) / 60;
    private readonly TimeSpan _lowTickRate = TimeSpan.FromSeconds(0.5);

    private double _forward;
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

        _updateTimer = new DispatcherTimer(_lowTickRate, DispatcherPriority.Normal, Update);
        _updateTimer.Start();
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
            return;
        }

        if (SetTickRate(_highTickRate))
        {
            GameSpeedLabel.Content = $"{_game.GameSpeed:0.0}x";
        }
        
        if (_gameSpeed.TryGet(out var gameSpeed))
        {
            _game.GameSpeed = gameSpeed;
            GameSpeedLabel.Content = $"{gameSpeed:0.0}x";
        }

        var newLine = Environment.NewLine;
        var (x, y, z) = _game.Position;
        PositionText.Text = $"{FormatUnits(x)}{newLine}{FormatUnits(y)}{newLine}{FormatUnits(z)}";

        var (xv, yv, _) = _game.Velocity;
        var horizontalVelocity = Math.Round(Math.Sqrt(xv * xv + yv * yv), MidpointRounding.ToPositiveInfinity);
        SpeedText.Text = $"{FormatUnits((float) horizontalVelocity)} m/s";

        if (_game.Fly)
        {
            var (v, h) = _game.Camera;
            var angles = new Vector(h, v) * (Math.PI / 180d);
            
            var verticalVelocity = UnitVector(angles.Y);
            var nonUpMultiplier = 1f - Math.Abs(verticalVelocity.Y);
            verticalVelocity *= _forward * 10000;
            var zVelocity = (float) verticalVelocity.Y;

            var newHorizontal = UnitVector(angles.X) * (_forward * FlySpeed * 100 * nonUpMultiplier);
            var xVelocity = (float) newHorizontal.X;
            var yVelocity = (float) newHorizontal.Y;
            
            _game.Velocity = new Vector3F(xVelocity, yVelocity, zVelocity);
        }
        ToggleLabel(_game.Fly, FlyToggleLabel);

        MapText.Text = _game.Map;
        return;

        Vector UnitVector(double radians) => new(Math.Cos(radians), Math.Sin(radians));
        string FormatUnits(float value) => $"{value / 100f:0.00}";
        
    }
    
    private bool OnWKey(bool down)
    {
        _forward = down ? 1d : 0d;
        return _game.Fly;
    }

    private void ToggleFly()
    {
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