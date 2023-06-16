using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace VeninethTrainer;

/// <summary>
/// A class that manages a global low level keyboard hook
/// </summary>
public class GlobalKeyListener
{
	#region Constant, Structure and Delegate Definitions

	public delegate void KeyEvent(Key key, bool down);

	public delegate bool KeyHandler(bool down);
	
	/// <summary>
	/// defines the callback type for the hook
	/// </summary>
	private delegate int KeyboardHookProc(int code, int wParam, ref KeyboardHookStruct lParam);
	private readonly KeyboardHookProc _hookProcDelegate;

	public struct KeyboardHookStruct
	{
		public int vkCode;
		public int scanCode;
		public int flags;
		public int time;
		public int dwExtraInfo;
	}

	public const int dc = 999;
	const int WH_KEYBOARD_LL = 13;
	const int WM_KEYDOWN = 0x100;
	const int WM_KEYUP = 0x101;
	const int WM_SYSKEYDOWN = 0x104;
	const int WM_SYSKEYUP = 0x105;
	#endregion

	#region Instance Variables
	/// <summary>
	/// The collections of keys to watch for
	/// </summary>
	public readonly Dictionary<Key, KeyHandler> HookedKeys = new();
	
	/// <summary>
	/// Handle to the hook, need this to unhook and call the next hook
	/// </summary>
	private IntPtr _hook = IntPtr.Zero;
	#endregion

	#region Events
	/// <summary>
	/// Occurs when one of the hooked keys is pressed
	/// </summary>
	public event KeyEvent? KeyDown;
	
	/// <summary>
	/// Occurs when one of the hooked keys is released
	/// </summary>
	public event KeyEvent? KeyUp;
	#endregion

	#region Constructors and Destructors
	/// <summary>
	/// Initializes a new instance of the <see cref="GlobalKeyListener"/> class and installs the keyboard hook.
	/// </summary>
	public GlobalKeyListener()
	{
		_hookProcDelegate = HookProc;
		Hook();
	}

	/// <summary>
	/// Releases unmanaged resources and performs other cleanup operations before the
	/// <see cref="GlobalKeyListener"/> is reclaimed by garbage collection and uninstalls the keyboard hook.
	/// </summary>
	~GlobalKeyListener()
	{
		Unhook();
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Installs the global hook
	/// </summary>
	public void Hook()
	{
		var hInstance = LoadLibrary(WinApi.User32);
		_hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProcDelegate, hInstance, 0);
	}

	/// <summary>
	/// Uninstalls the global hook
	/// </summary>
	public void Unhook()
	{
		UnhookWindowsHookEx(_hook);
	}

	/// <summary>
	/// The callback for the keyboard hook
	/// </summary>
	/// <param name="code">The hook code, if it isn't >= 0, the function shouldn't do anyting</param>
	/// <param name="wParam">The event type</param>
	/// <param name="lParam">The keyhook event information</param>
	/// <returns></returns>
	private int HookProc(int code, int wParam, ref KeyboardHookStruct lParam)
	{
		if (code >= 0)
		{
			// var keyData = (lParam.scanCode << 16) | ((lParam.flags & 1) << 24);
			// Key key = KeyInterop.KeyFromVirtualKey(lParam.vkCode, keyData);
	
			// Temporary fix until Avalonia 11 is released
			if (!Enum.TryParse<Key>(((VirtualKeys) lParam.vkCode).ToString(), out var key))
			{
				goto Next;
			}
				
			if (HookedKeys.TryGetValue(key, out var handler))
			{
				var handled = false;
				if (wParam is WM_KEYDOWN or WM_SYSKEYDOWN)
				{
					KeyDown?.Invoke(key, true);
					handled |= handler(true);
				}
				else if (wParam is WM_KEYUP or WM_SYSKEYUP)
				{
					KeyUp?.Invoke(key, false);
					handled |= handler(false);
				}
				
				if (handled)
				{
					return 1;
				}
			}
		}
			
		Next:
		return CallNextHookEx(_hook, code, wParam, ref lParam);
	}
	#endregion

	#region DLL imports
	/// <summary>
	/// Sets the windows hook, do the desired event, one of hInstance or threadId must be non-null
	/// </summary>
	/// <param name="idHook">The id of the event you want to hook</param>
	/// <param name="callback">The callback.</param>
	/// <param name="hInstance">The handle you want to attach the event to, can be null</param>
	/// <param name="threadId">The thread you want to attach the event to, can be null</param>
	/// <returns>a handle to the desired hook</returns>
	[DllImport(WinApi.User32Dll)]
	private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHookProc callback, IntPtr hInstance, uint threadId);

	/// <summary>
	/// Unhooks the windows hook.
	/// </summary>
	/// <param name="hInstance">The hook handle that was returned from SetWindowsHookEx</param>
	/// <returns>True if successful, false otherwise</returns>
	[DllImport(WinApi.User32Dll)]
	private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

	/// <summary>
	/// Calls the next hook.
	/// </summary>
	/// <param name="idHook">The hook id</param>
	/// <param name="nCode">The hook code</param>
	/// <param name="wParam">The wparam.</param>
	/// <param name="lParam">The lparam.</param>
	/// <returns></returns>
	[DllImport(WinApi.User32Dll)]
	private static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, ref KeyboardHookStruct lParam);

	/// <summary>
	/// Loads the library.
	/// </summary>
	/// <param name="lpFileName">Name of the library</param>
	/// <returns>A handle to the library</returns>
	[DllImport(WinApi.Kernel32Dll, CharSet = CharSet.Unicode)]
	private static extern IntPtr LoadLibrary(string lpFileName);
	#endregion
}