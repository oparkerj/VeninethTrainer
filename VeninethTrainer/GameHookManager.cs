using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
    private readonly DeepPointer _mapNameOffsets = new(0x02F8AB60, 0x3F8, 0x0);
    private readonly DeepPointer _playerPawnOffsets = new(0x2F8AB60, 0x170, 0x38, 0x0, 0x30, 0x270, 0x0);
    
    private IntPtr _positionPointer;
    private IntPtr _velocityPointer;
    private IntPtr _gravityPointer;
    private IntPtr _viewPointer;
    private IntPtr _gameSpeedPointer;
    private IntPtr _mapNamePointer;
    private IntPtr _ballTypePointer;

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

    private readonly StringBuilder _nameBuffer = new(255);
    public string Map
    {
        get
        {
            _nameBuffer.Length = 0;
            if (_game?.ReadString(_mapNamePointer, _nameBuffer) != true) return string.Empty;
            return TranslateName(_nameBuffer.Replace("/Game/Maps/", "").Replace("Secrets/", "").ToString());
        }
    }

    public BallType CurrentBallType => _game?.ReadValue<BallType>(_ballTypePointer, out var type) == true ? type : BallType.Unknown;
    
    private string TranslateName(string gameName)
    {
        return gameName switch
        {
            "Level00" => "Aita",
            "Level01" => "Orakh",
            "Level02" => "Init",
            "Level03" => "Opak",
            "Level04" => "Vikaros",
            "Level05" => "Tulan",
            "Level06" => "Hecto",
            "Level07" => "Ion",
            "Level08" => "Varr",
            "Level09" => "Ama",
            "Level10" => "Takot",
            "Trials/Vorbit00" => "Init Orbital",
            "Trials/Vorbit01" => "Opak Orbital (Right)",
            "Trials/Vorbit02" => "Opak Orbital (Left)",
            "Trials/Vring01" => "Tulan Orbital (Left)",
            "Trials/Vring02" => "Tulan Orbital (Right)",
            "Trials/Volt01" => "Hecto Orbital (Right)",
            "Trials/Volt02" => "Hecto Orbital (Left)",
            "Trials/Volt03" => "Hecto Orbital (Back)",
            "Trials/Volt04" => "Hecto Orbital (Front)",
            "Trials/Vrain01" => "Ion Orbital (Left)",
            "Trials/Vrain02" => "Ion Orbital (Right)",
            "ApexOutro" => "Venineth",
            "SecretLevel0X" => "Secret Ending",
            "SecretLevel0X_Outro" => "Secret Ending Outro",
            "SecretLevel01" => "Trinoid G",
            "SecretLevel02" => "Trinoid Y",
            "SecretLevel03" => "Trinoid B",
            "SecretLevel04" => "Trinoid S",
            "SecretLevel05" => "Trinoid R",
            "SecretLevel06" => "Trinoid V",
            _ => gameName
        };
    }
    
    public enum BallType : byte
    {
        Unknown = 0,
        Core = 1,
        Fire = 2,
        Green = 3,
        Red = 4,
        Mirror = 5,
        Ice = 6,
        Void = 7,
    }

    public static (string? Name, string? Asset) GetBallInfo(BallType type)
    {
        return type switch
        {
            BallType.Core => ("Metal Ball", "metal_ball"),
            BallType.Fire => ("Fire Ball", "fire_ball"),
            BallType.Green => ("Green Ball (+)", "green_ball"),
            BallType.Red => ("Red Ball (-)", "red_ball"),
            BallType.Mirror => ("Mirror Ball", "mirror_ball"),
            BallType.Ice => ("Ice Ball", "ice_ball"),
            BallType.Void => ("Void Ball", "void_ball"),
            _ => (null, null)
        };
    }

    private bool SetupGamePointers()
    {
        var success = true;
        success &= InitPointer(_playerOffsets, 0xA0, out _positionPointer);
        success &= InitPointer(_playerOffsets, 0xD0, out _velocityPointer);
        success &= InitPointer(_gravityOffsets, 0, out _gravityPointer);
        success &= InitPointer(_playerControllerOffsets, 0x2A8, out _viewPointer);
        success &= InitPointer(_worldSettingsOffsets, 0x308, out _gameSpeedPointer);
        success &= InitPointer(_mapNameOffsets, 0, out _mapNamePointer);
        success &= InitPointer(_playerPawnOffsets, 0x440, out _ballTypePointer);
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