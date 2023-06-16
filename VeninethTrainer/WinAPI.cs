using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VeninethTrainer;

public enum MemPageState : uint
{
    MEM_COMMIT = 0x1000,
    MEM_RESERVE = 0x2000,
    MEM_FREE = 0x10000,
}

public enum MemPageType : uint
{
    MEM_PRIVATE = 0x20000,
    MEM_MAPPED = 0x40000,
    MEM_IMAGE = 0x1000000
}

[Flags]
public enum MemPageProtect : uint
{
    PAGE_NOACCESS = 0x01,
    PAGE_READONLY = 0x02,
    PAGE_READWRITE = 0x04,
    PAGE_WRITECOPY = 0x08,
    PAGE_EXECUTE = 0x10,
    PAGE_EXECUTE_READ = 0x20,
    PAGE_EXECUTE_READWRITE = 0x40,
    PAGE_EXECUTE_WRITECOPY = 0x80,
    PAGE_GUARD = 0x100,
    PAGE_NOCACHE = 0x200,
    PAGE_WRITECOMBINE = 0x400,
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryBasicInformation // MEMORY_BASIC_INFORMATION
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public MemPageProtect AllocationProtect;
    public UIntPtr RegionSize;
    public MemPageState State;
    public MemPageProtect Protect;
    public MemPageType Type;
}

[StructLayout(LayoutKind.Sequential)]
public struct ModuleInfo
{
    public IntPtr lpBaseOfDll;
    public uint SizeOfImage;
    public IntPtr EntryPoint;
}

internal static class WinApi
{
    public const string User32 = "user32";
    public const string Kernel32 = "kernel32";
    public const string ProcessStatus = "psapi";
    public const string NativeApi = "ntdll";

    public const string User32Dll = $"{User32}.dll";
    public const string Kernel32Dll = $"{Kernel32}.dll";
    public const string ProcessStatusDll = $"{ProcessStatus}.dll";
    public const string NativeApiDll = $"{NativeApi}.dll";
    
    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        UIntPtr nSize,
        out UIntPtr lpNumberOfBytesRead);
    
    [DllImport(Kernel32Dll, SetLastError = true, EntryPoint = "ReadProcessMemory")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemorySpan(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        Span<byte> lpBuffer,
        UIntPtr nSize,
        out UIntPtr lpNumberOfBytesRead);

    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        UIntPtr nSize,
        out UIntPtr lpNumberOfBytesWritten);

    [DllImport(ProcessStatusDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumProcessModulesEx(
        IntPtr hProcess,
        IntPtr[] lphModule,
        uint cb,
        out uint lpcbNeeded,
        uint dwFilterFlag);

    [DllImport(ProcessStatusDll, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetModuleFileNameEx(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpBaseName,
        uint nSize);

    [DllImport(ProcessStatusDll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetModuleInformation(
        IntPtr hProcess,
        IntPtr hModule,
        out ModuleInfo lpmodinfo,
        uint cb);

    [DllImport(ProcessStatusDll, CharSet = CharSet.Unicode)]
    public static extern uint GetModuleBaseName(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpBaseName,
        uint nSize);

    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process(
        IntPtr hProcess,
        [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern UIntPtr VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        out MemoryBasicInformation lpBuffer,
        UIntPtr dwLength);

    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint flAllocationType,
        MemPageProtect flProtect);

    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport(Kernel32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualProtectEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UIntPtr dwSize,
        MemPageProtect flNewProtect,
        out MemPageProtect lpflOldProtect);

    [DllImport(NativeApiDll, SetLastError = true)]
    public static extern IntPtr NtSuspendProcess(IntPtr hProcess);

    [DllImport(NativeApiDll, SetLastError = true)]
    public static extern IntPtr NtResumeProcess(IntPtr hProcess);

    [DllImport(Kernel32Dll, SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        UIntPtr dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        out IntPtr lpThreadId);
}