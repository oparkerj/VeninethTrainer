using System;
using System.Diagnostics;
using System.Linq;

namespace VeninethTrainer;

// This class provides the interface to the game's memory
public class GameHookManager
{
    public const string ProcessName = "Game-Win64-Shipping";
    
    private Process? _game;

    private readonly DeepPointer _playerOffsets = new(0x02F6BA98, 0x0, 0xE8, 0x398, 0x0);
    private readonly DeepPointer _gravityOffsets = new("PhysX3_x64.dll", 0x191434);
    private readonly DeepPointer _playerControllerOffsets = new(0x02F6C030, 0x30, 0x0);
    private readonly DeepPointer _worldSettingsOffsets = new(0x02F8AB60, 0x30, 0x240, 0x0);
    
    private IntPtr _positionPointer;
    private IntPtr _velocityPointer;
    private IntPtr _gravityPointer;
    private IntPtr _viewPointer;
    private IntPtr _gameSpeedPointer;

    public bool Hooked => _game?.HasExited == false;
    
    private bool _fly;
    public bool Fly
    {
        get => _fly;
        set
        {
            _fly = value;
            if (value) _game?.WriteBytes(_gravityPointer, 0x90, 0x90, 0x90, 0x90, 0x90);
            else _game?.WriteBytes(_gravityPointer, 0xF3, 0x0F, 0x11, 0x69, 0x08);
        }
    }

    public Vector3F Position
    {
        get => _game?.ReadValue<Vector3F>(_positionPointer, out var pos) == true ? pos : default;
        set => _game?.WriteValue(_positionPointer, value);
    }

    public Vector3F Velocity
    {
        get => _game?.ReadValue<Vector3F>(_velocityPointer, out var vel) == true ? vel : default;
        set => _game?.WriteValue(_velocityPointer, value);
    }

    public Vector2F Camera
    {
        get => _game?.ReadValue<Vector2F>(_viewPointer, out var view) == true ? view : default;
        set => _game?.WriteValue(_viewPointer, value);
    }

    public float GameSpeed
    {
        get => _game?.ReadValue<float>(_gameSpeedPointer, out var speed) == true ? speed : default;
        set => _game?.WriteValue(_gameSpeedPointer, value);
    }

    private bool SetupGamePointers()
    {
        var success = true;
        success &= InitPointer(_playerOffsets, 0xA0, out _positionPointer);
        success &= InitPointer(_playerOffsets, 0xD0, out _velocityPointer);
        success &= InitPointer(_gravityOffsets, 0, out _gravityPointer);
        success &= InitPointer(_playerControllerOffsets, 0x2A8, out _viewPointer);
        success &= InitPointer(_worldSettingsOffsets, 0x308, out _gameSpeedPointer);
        return success;
    }

    private void Reset()
    {
        _game = null;
        _positionPointer = IntPtr.Zero;
        _velocityPointer = IntPtr.Zero;
        _gravityPointer = IntPtr.Zero;
        _viewPointer = IntPtr.Zero;
        _gameSpeedPointer = IntPtr.Zero;
        _fly = false;
    }
    
    private bool InitPointer(DeepPointer pointer, int offset, out IntPtr address)
    {
        if (_game == null)
        {
            address = IntPtr.Zero;
            return false;
        }

        try
        {
            pointer.DerefOffsets(_game, out var result);
            address = result + offset;
            return true;
        }
        catch (Exception)
        {
            address = IntPtr.Zero;
            return false;
        }
    }

    private bool TryHook()
    {
        Reset();
        
        var processes = Process.GetProcesses()
            .Where(process => process.ProcessName.Contains(ProcessName))
            .ToList();

        if (processes.Count == 0)
        {
            return false;
        }

        _game = processes[0];
        if (!_game.HasExited) return true;
        _game = null;
        return false;
    }

    public bool UpdateHookStatus()
    {
        if (!Hooked && !TryHook()) return false;

        return SetupGamePointers();
    }
}