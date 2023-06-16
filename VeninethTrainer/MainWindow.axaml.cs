using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace VeninethTrainer;

public partial class MainWindow : Window
{
    internal GlobalKeyListener kbHook = new();
    public DispatcherTimer updateTimer;
    public Process? game;
    public bool hooked = false;
    //DeepPointer cheatManagerDP, capsuleDP, charMoveCompDP, playerControllerDP, playerCharacterDP, worldDP, gameModeDP, worldSettingsDP;
    
    public DeepPointer sphereDP = new(0x02F6BA98, 0x0, 0xE8, 0x398, 0x0);
    public DeepPointer gravityDP = new("PhysX3_x64.dll", 0x191434);
    public DeepPointer characterControllerDP = new(0x02F6C030, 0x30, 0x0);
    public DeepPointer worldSettingsDP = new(0x02F8AB60, 0x30, 0x240, 0x0);

    public IntPtr xVelPtr, yVelPtr, zVelPtr, xPosPtr, yPosPtr, zPosPtr, gameSpeedPtr, gravityPtr, hLookPtr, vLookPtr;
    public byte[] gravityCode = new byte[5] { 0xF3, 0x0F, 0x11, 0x69, 0x08 };
    public byte[] gravityNop = new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 };

    public const float flySpeed = 50;

    public double forward, side = 0, up = 0;

    // Variables used for the speed tracker
    private bool _speedTracker, _speedReady;
    private string? _speedDir;
    private List<SpeedPoint>? _speedPoints;
    private Stopwatch _speedWatch;
    private DispatcherTimer _speedTimer;

    private void gameSpeedBtn_Click(object sender, RoutedEventArgs e)
    {
        ChangeGameSpeed();
    }

    private void teleBtn_Click(object sender, RoutedEventArgs e)
    {
        Teleport();
    }
    
    private void teleVelBtn_Click(object sender, RoutedEventArgs e)
    {
        Teleport(true);
    }

    private void saveBtn_Click(object sender, RoutedEventArgs e)
    {
        StorePosition();
    }

    private void noclipBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleNoclip();
    }


    float xVel, yVel, zVel, xPos, yPos, zPos, vLook, hLook, gameSpeed, prefGameSpeed;
    bool ghost, god, noclip;
    int charLFS;
    float[] storedPos = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };


    public MainWindow()
    {
        InitializeComponent();
        
        AddKeyListener(Key.W);
        AddKeyListener(Key.F1);
        AddKeyListener(Key.F4);
        AddKeyListener(Key.F5);
        AddKeyListener(Key.F6);
        AddKeyListener(Key.F7);
        AddKeyListener(Key.F8);

        prefGameSpeed = 1.0f;

        updateTimer = new DispatcherTimer(TimeSpan.FromSeconds(1) / 60, DispatcherPriority.Normal, Update);
        updateTimer.Start();
    }

    private void AddKeyListener(Key key)
    {
        kbHook.HookedKeys[key] = down => down ? InputKeyDown(key) : InputKeyUp(key);
    }

    private void Update(object? sender, EventArgs e)
    {
        if (game == null || game.HasExited)
        {
            game = null;
            hooked = false;
        }
        if (!hooked)
            hooked = Hook();
        if (!hooked)
            return;
        try
        {
            DerefPointers();
        }
        catch (Exception)
        {
            return;
        }

        game.ReadValue(xPosPtr, out xPos);
        game.ReadValue(yPosPtr, out yPos);
        game.ReadValue(zPosPtr, out zPos);

        game.ReadValue(xVelPtr, out xVel);
        game.ReadValue(yVelPtr, out yVel);
        game.ReadValue(zVelPtr, out zVel);
        var hVel = Math.Floor(Math.Sqrt(xVel * xVel + yVel * yVel) + 0.5f) / 100;

        game.ReadValue(vLookPtr, out vLook);
        game.ReadValue(hLookPtr, out hLook);

        game.ReadValue(gameSpeedPtr, out gameSpeed);

        if (_speedTracker)
        {
            if (_speedDir == null)
            {
                noclipBtn.Content = "[F1] Choose Directory";
                SetLabel("", Brushes.Black);
            }
            else
            {
                if (_speedPoints != null)
                {
                    noclipBtn.Content = "[F1] Stop";
                    if (_speedWatch?.IsRunning == true)
                    {
                        SetLabel("REC", Brushes.DarkRed);

                        if (_speedReady)
                        {
                            _speedReady = false;
                            // Update timer
                            _speedPoints.Add(new SpeedPoint
                            {
                                Time = _speedWatch.Elapsed.TotalSeconds,
                                Speed = hVel
                            });
                        }
                    }
                    else SetLabel("WAITING", Brushes.Goldenrod);
                }
                else
                {
                    noclipBtn.Content = "[F1] Ready";
                    SetLabel("READY", Brushes.DarkGreen);
                }
            }
        }
        else
        {
            noclipBtn.Content = "[F1] NoClip";
            SetLabel(noclip, noclipLabel);
        }

        SetGameSpeed();

        gameSpeedLabel.Content = prefGameSpeed.ToString("0.0") + "x";

        positionBlock.Text = (xPos / 100).ToString("0.00") + "\n" + (yPos / 100).ToString("0.00") + "\n" + (zPos / 100).ToString("0.00");
        speedBlock.Text = hVel.ToString("0.00") + " m/s";

        if (noclip)
        {
            var radian = hLook * (Math.PI / 180);
            var direction = new Vector(Math.Cos(radian), Math.Sin(radian));
            Debug.WriteLine("{0}, {1}", forward, side);
            var inputVector = new Vector(side, forward);
            if(inputVector.Length > 1)
                inputVector.Normalize();

            Debug.WriteLine(inputVector);
            var newXVel = (float)direction.X * (float)inputVector.Y * flySpeed * 100;
            var newYVel = (float)direction.Y * (float)inputVector.Y * flySpeed * 100;

            Debug.WriteLine("XVEL {0}, YVEL {1}", newXVel, newYVel);


            radian = vLook * (Math.PI / 180);
            direction = new Vector(Math.Cos(radian), Math.Sin(radian));
            Debug.WriteLine("up vector: " + direction);
            var newZVel = (float)(direction.Y * forward * 10000);
            game.WriteBytes(zVelPtr, BitConverter.GetBytes(newZVel));
            var nonUpMultiplier = 1f - Math.Abs((float)direction.Y);
            game.WriteBytes(xVelPtr, BitConverter.GetBytes(newXVel*nonUpMultiplier));
            game.WriteBytes(yVelPtr, BitConverter.GetBytes(newYVel*nonUpMultiplier));
        }
    }

    private bool Hook()
    {
        var processList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName.Contains("Game-Win64-Shipping"));
        if (processList.Count == 0)
        {
            game = null;
            return false;
        }
        game = processList[0];

        return !game.HasExited;
    }

    private void DerefPointers()
    {
        sphereDP.DerefOffsets(game, out var basePtr);
        xPosPtr = basePtr + 0xA0;
        yPosPtr = basePtr + 0xA4;
        zPosPtr = basePtr + 0xA8;
        xVelPtr = basePtr + 0xD0;
        yVelPtr = basePtr + 0xD4;
        zVelPtr = basePtr + 0xD8;
        gravityDP.DerefOffsets(game, out gravityPtr);

        characterControllerDP.DerefOffsets(game, out var characterControllerPtr);
        vLookPtr = characterControllerPtr + 0x2A8;
        hLookPtr = characterControllerPtr + 0x2AC;

        worldSettingsDP.DerefOffsets(game, out var worldSettingsPtr);
        gameSpeedPtr = worldSettingsPtr + 0x308;
    }

    private bool WKeyHandled => noclip && !_speedTracker;

    private bool InputKeyDown(Key key)
    {
        switch (key)
        {
            case Key.F1:
                HandleActionButton();
                break;
            case Key.F4:
                ChangeGameSpeed();
                break;
            case Key.F5:
                StorePosition();
                break;
            case Key.F6:
                Teleport();
                break;
            case Key.F7:
                Teleport(true);
                break;
            case Key.F8:
                _speedTracker = !_speedTracker;
                if (_speedTracker && noclip) ToggleNoclip();
                break;
            case Key.W:
                forward = 1;
                CheckTimer();
                return WKeyHandled;
            default:
                return false;
        }
        return true;
    }

    private void CheckTimer()
    {
        if (_speedTracker && _speedPoints != null && !_speedWatch.IsRunning)
        {
            _speedWatch.Start();
            _speedTimer.Start();
        }
    }

    private void HandleActionButton()
    {
        if (_speedTracker)
        {
            if (_speedDir == null)
            {
                var dialog = new OpenFolderDialog();
                _speedDir = dialog.ShowAsync(this).Result;
            }
            else if (_speedPoints == null)
            {
                _speedPoints = new List<SpeedPoint>();
                _speedWatch = new Stopwatch();
                _speedTimer = new DispatcherTimer(TimeSpan.FromSeconds(1) / 30, DispatcherPriority.Normal, (sender, args) =>
                {
                    _speedReady = true;
                });
                _speedReady = true;
            }
            else
            {
                if (_speedWatch.IsRunning)
                {
                    _speedWatch.Stop();
                    _speedTimer.Stop();

                    var num = 0;
                    var path = Path.Combine(_speedDir, $"speeds_{num}.csv");
                    while (File.Exists(path))
                    {
                        num++;
                        path = Path.Combine(_speedDir, $"speeds_{num}.csv");
                    }
                    File.WriteAllLines(path, _speedPoints.Select(point => point.ToString()));
                }

                _speedPoints = null;
                _speedWatch = null;
                _speedTimer = null;
            }
        }
        else
        {
            ToggleNoclip();
        }
    }

    private void ToggleNoclip()
    {
        noclip = !noclip;
        if (noclip)
        {
            game.WriteBytes(gravityPtr, gravityNop);
            // kbHook.HookedKeys.Add(Keys.W);
        }
        else
        {
            // kbHook.HookedKeys.Remove(Keys.W);
            game.WriteBytes(gravityPtr, gravityCode);
            forward = 0;
        }

    }

    private void Teleport(bool velocity = false)
    {
        game.WriteBytes(xPosPtr, BitConverter.GetBytes(storedPos[0]));
        game.WriteBytes(yPosPtr, BitConverter.GetBytes(storedPos[1]));
        game.WriteBytes(zPosPtr, BitConverter.GetBytes(storedPos[2]));

        game.WriteBytes(vLookPtr, BitConverter.GetBytes(storedPos[3]));
        game.WriteBytes(hLookPtr, BitConverter.GetBytes(storedPos[4]));

        if (velocity)
        {
            game.WriteBytes(xVelPtr, BitConverter.GetBytes(storedPos[5]));
            game.WriteBytes(yVelPtr, BitConverter.GetBytes(storedPos[6]));
            game.WriteBytes(zVelPtr, BitConverter.GetBytes(storedPos[7]));
        }
        else
        {
            game.WriteBytes(xVelPtr, BitConverter.GetBytes(0f));
            game.WriteBytes(yVelPtr, BitConverter.GetBytes(0f));
            game.WriteBytes(zVelPtr, BitConverter.GetBytes(0f));
        }
    }

    private void SetLabel(bool state, Label label)
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

    private void SetLabel(string text, IBrush color)
    {
        noclipLabel.Content = text;
        noclipLabel.Foreground = color;
    }

    private void StorePosition()
    {
        storedPos = new[] { xPos, yPos, zPos, vLook, hLook, xVel, yVel, zVel };
    }

    private bool InputKeyUp(Key key)
    {
        switch (key)
        {
            case Key.W:
                forward = 0;
                return WKeyHandled;
            case Key.S:
                forward = 0;
                break;
            case Key.A:
                side = 0;
                break;
            case Key.D:
                side = 0;
                break;
            default:
                return false;
        }
        return true;
    }

    private void ChangeGameSpeed()
    {
        switch (prefGameSpeed)
        {
            case 1.0f:
                prefGameSpeed = 2.0f;
                break;
            case 2.0f:
                prefGameSpeed = 4.0f;
                break;
            case 4.0f:
                prefGameSpeed = 0.5f;
                break;
            case 0.5f:
                prefGameSpeed = 1.0f;
                break;
            default:
                prefGameSpeed = 1.0f;
                break;
        }
    }

    private void SetGameSpeed()
    {
        Debug.WriteLine("{0} want {1}", gameSpeed, prefGameSpeed);
        if ((gameSpeed == 1.0f || gameSpeed == 2.0f || gameSpeed == 4.0f || gameSpeed == 0.5f) && gameSpeed != prefGameSpeed)
        {
            game.WriteBytes(gameSpeedPtr, BitConverter.GetBytes(prefGameSpeed));
        }
    }
}