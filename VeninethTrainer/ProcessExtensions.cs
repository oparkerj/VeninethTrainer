using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// Note: Please be careful when modifying this because it could break existing components!
// http://stackoverflow.com/questions/1456785/a-definitive-guide-to-api-breaking-changes-in-net

namespace VeninethTrainer;

public class ProcessModuleWow64Safe
{
    public IntPtr BaseAddress { get; set; }
    public IntPtr EntryPointAddress { get; set; }
    public string? FileName { get; init; }
    public int ModuleMemorySize { get; set; }
    public string? ModuleName { get; init; }
    
    public FileVersionInfo? FileVersionInfo => FileName != null ? FileVersionInfo.GetVersionInfo(FileName) : default;

    public override string? ToString()
    {
        return ModuleName ?? base.ToString();
    }
}

public enum ReadStringType
{
    AutoDetect,
    Ascii,
    Utf8,
    Utf16
}

public static class ProcessExtensions
{
    private static readonly Dictionary<int, List<ProcessModuleWow64Safe>> ModuleCache = new();

    public static ProcessModuleWow64Safe MainModuleWow64Safe(this Process p)
    {
        return p.ModulesWow64Safe().First();
    }

    public static List<ProcessModuleWow64Safe> ModulesWow64Safe(this Process p)
    {
        if (ModuleCache.Count > 100)
        {
            ModuleCache.Clear();
        }

        const int listModulesAll = 0x3;
        const int maxPathLength = 260;
        const int moduleCount = 1024;

        var hModules = new IntPtr[moduleCount];
        var cb = unchecked((uint) (IntPtr.Size * moduleCount));

        if (!WinApi.EnumProcessModulesEx(p.Handle, hModules, cb, out var cbNeeded, listModulesAll))
        {
            throw new Win32Exception();
        }
        
        var numMods = cbNeeded / (uint) IntPtr.Size;
        var hash = p.StartTime.GetHashCode() + p.Id + (int) numMods;
        if (ModuleCache.TryGetValue(hash, out var cachedModule))
        {
            return cachedModule;
        }

        var ret = new List<ProcessModuleWow64Safe>();

        // everything below is fairly expensive, which is why we cache!
        var sb = new StringBuilder(maxPathLength);
        for (var i = 0; i < numMods; i++)
        {
            sb.Clear();
            if (WinApi.GetModuleFileNameEx(p.Handle, hModules[i], sb, (uint) sb.Capacity) == 0)
            {
                throw new Win32Exception();
            }
            var fileName = sb.ToString();

            sb.Clear();
            if (WinApi.GetModuleBaseName(p.Handle, hModules[i], sb, (uint) sb.Capacity) == 0)
            {
                throw new Win32Exception();
            }
            var baseName = sb.ToString();

            ModuleInfo moduleInfo = default;
            if (!WinApi.GetModuleInformation(p.Handle, hModules[i], out moduleInfo, (uint) Marshal.SizeOf(moduleInfo)))
            {
                throw new Win32Exception();
            }

            ret.Add(new ProcessModuleWow64Safe
            {
                FileName = fileName,
                BaseAddress = moduleInfo.lpBaseOfDll,
                ModuleMemorySize = (int) moduleInfo.SizeOfImage,
                EntryPointAddress = moduleInfo.EntryPoint,
                ModuleName = baseName
            });
        }

        ModuleCache.Add(hash, ret);
        return ret;
    }

    public static IEnumerable<MemoryBasicInformation> MemoryPages(this Process process, bool all = false)
    {
        // hardcoded values because GetSystemInfo / GetNativeSystemInfo can't return info for remote process
        const ulong min = 0x10000UL;
        var max = process.Is64Bit() ? 0x00007FFFFFFEFFFFUL : 0x7FFEFFFFUL;

        var mbiSize = (UIntPtr) Marshal.SizeOf<MemoryBasicInformation>();

        var addr = min;
        do
        {
            if (WinApi.VirtualQueryEx(process.Handle, (IntPtr) addr, out var mbi, mbiSize) == 0)
            {
                break;
            }
            addr += mbi.RegionSize;

            // don't care about reserved/free pages
            if (mbi.State != MemPageState.MEM_COMMIT) continue;

            // probably don't care about guarded pages
            if (!all && (mbi.Protect & MemPageProtect.PAGE_GUARD) != 0) continue;

            // probably don't care about image/file maps
            if (!all && mbi.Type != MemPageType.MEM_PRIVATE) continue;

            yield return mbi;

        } while (addr < max);
    }

    public static bool Is64Bit(this Process process)
    {
        WinApi.IsWow64Process(process.Handle, out var procWow64);
        return Environment.Is64BitOperatingSystem && !procWow64;
    }

    public static bool ReadValue<T>(this Process process, IntPtr addr, out T val)
        where T : struct
    {
        var type = typeof(T);
        type = type.IsEnum ? Enum.GetUnderlyingType(type) : type;

        val = default;
        if (!ReadValue(process, addr, type, out var obj)) return false;

        val = (T) obj;
        return true;
    }

    public static bool ReadValue(Process process, IntPtr addr, Type type, out object val)
    {
        var size = type == typeof(bool) ? 1 : Marshal.SizeOf(type);
        
        if (!ReadBytes(process, addr, size, out var bytes))
        {
            val = new object();
            return false;
        }

        val = ResolveToType(bytes, type);
        return true;
    }

    public static bool ReadBytes(this Process process, IntPtr addr, int count, out byte[] val)
    {
        var bytes = new byte[count];

        var length = (nuint) count;
        var readData = WinApi.ReadProcessMemory(process.Handle, addr, bytes, length, out var read);

        if (!readData || read != length)
        {
            val = Array.Empty<byte>();
            return false;
        }

        val = bytes;
        return true;
    }

    public static bool ReadPointer(this Process process, IntPtr addr, out IntPtr val)
    {
        return ReadPointer(process, addr, process.Is64Bit(), out val);
    }

    public static bool ReadPointer(this Process process, IntPtr addr, bool is64Bit, out IntPtr val)
    {
        if (!process.ReadBytes(addr, is64Bit ? 8 : 4, out var bytes))
        {
            val = IntPtr.Zero;
            return false;
        }

        val = is64Bit ? (IntPtr) BitConverter.ToUInt64(bytes) : (IntPtr) BitConverter.ToUInt32(bytes);
        return true;
    }

    public static bool ReadString(this Process process, IntPtr addr, int numBytes, out string str)
    {
        return ReadString(process, addr, ReadStringType.AutoDetect, numBytes, out str);
    }

    public static bool ReadString(this Process process, IntPtr addr, ReadStringType type, int numBytes, out string str)
    {
        var sb = new StringBuilder(numBytes);
        
        if (!ReadString(process, addr, type, sb))
        {
            str = string.Empty;
            return false;
        }

        str = sb.ToString();
        return true;
    }

    public static bool ReadString(this Process process, IntPtr addr, StringBuilder sb)
    {
        return ReadString(process, addr, ReadStringType.AutoDetect, sb);
    }

    public static bool ReadString(this Process process, IntPtr addr, ReadStringType type, StringBuilder sb)
    {
        var bytes = new byte[sb.Capacity];
        var length = (nuint) bytes.Length;

        var readData = WinApi.ReadProcessMemory(process.Handle, addr, bytes, length, out var read);

        if (!readData || read != length) return false;

        if (type == ReadStringType.AutoDetect)
        {
            if (read.ToUInt64() >= 2 && bytes[1] == '\x0')
            {
                sb.Append(Encoding.Unicode.GetString(bytes));
            }
            else
            {
                sb.Append(Encoding.UTF8.GetString(bytes));
            }
        }
        else if (type == ReadStringType.Utf8)
        {
            sb.Append(Encoding.UTF8.GetString(bytes));
        }
        else if (type == ReadStringType.Utf16)
        {
            sb.Append(Encoding.Unicode.GetString(bytes));
        }
        else
        {
            sb.Append(Encoding.ASCII.GetString(bytes));
        }

        for (var i = 0; i < sb.Length; i++)
        {
            if (sb[i] != '\0') continue;
            sb.Remove(i, sb.Length - i);
            break;
        }

        return true;
    }

    public static T ReadValue<T>(this Process process, IntPtr addr, T @default = default)
        where T : struct
    {
        if (!process.ReadValue(addr, out T val))
        {
            val = @default;
        }
        return val;
    }

    public static byte[] ReadBytes(this Process process, IntPtr addr, int count)
    {
        return process.ReadBytes(addr, count, out var bytes) ? bytes : Array.Empty<byte>();
    }

    public static IntPtr ReadPointer(this Process process, IntPtr addr, IntPtr @default = default)
    {
        return !process.ReadPointer(addr, out var ptr) ? @default : ptr;
    }

    public static string ReadString(this Process process, IntPtr addr, int numBytes, string @default)
    {
        return process.ReadString(addr, numBytes, out var str) ? str : @default;
    }

    public static string ReadString(this Process process, IntPtr addr, ReadStringType type, int numBytes, string @default)
    {
        return process.ReadString(addr, type, numBytes, out var str) ? str : @default;
    }

    public static bool WriteValue<T>(this Process process, IntPtr addr, T obj)
        where T : struct
    {
        var size = Marshal.SizeOf(obj);
        var arr = new byte[size];

        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(obj, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);

        return process.WriteBytes(addr, arr);
    }

    public static bool WriteBytes(this Process process, IntPtr addr, params byte[] bytes)
    {
        var length = (nuint) bytes.Length;
        var readData = WinApi.WriteProcessMemory(process.Handle, addr, bytes, length, out var written);
        
        return readData && written == length;
    }

    private static bool WriteJumpOrCall(Process process, IntPtr addr, IntPtr dest, bool call)
    {
        var x64 = process.Is64Bit();

        var jmpLen = x64 ? 12 : 5;

        var instruction = new List<byte>(jmpLen);
        if (x64)
        {
            instruction.AddRange(new byte[] { 0x48, 0xB8 }); // mov rax immediate
            instruction.AddRange(BitConverter.GetBytes(dest));
            instruction.AddRange(new byte[] { 0xFF, call ? (byte) 0xD0 : (byte) 0xE0 }); // jmp/call rax
        }
        else
        {
            var offset = unchecked(dest - (addr + jmpLen));
            instruction.AddRange(new[] { call ? (byte) 0xE8 : (byte) 0xE9 }); // jmp/call immediate
            instruction.AddRange(BitConverter.GetBytes(offset));
        }
        
        process.VirtualProtect(addr, jmpLen, MemPageProtect.PAGE_EXECUTE_READWRITE, out var oldProtect);
        var success = process.WriteBytes(addr, instruction.ToArray());
        process.VirtualProtect(addr, jmpLen, oldProtect);

        return success;
    }

    public static bool WriteJumpInstruction(this Process process, IntPtr addr, IntPtr dest)
    {
        return WriteJumpOrCall(process, addr, dest, false);
    }

    public static bool WriteCallInstruction(this Process process, IntPtr addr, IntPtr dest)
    {
        return WriteJumpOrCall(process, addr, dest, true);
    }

    public static IntPtr WriteDetour(this Process process, IntPtr src, int overwrittenBytes, IntPtr dest)
    {
        var jmpLen = process.Is64Bit() ? 12 : 5;
        if (overwrittenBytes < jmpLen)
        {
            throw new ArgumentOutOfRangeException(nameof(overwrittenBytes), $"must be >= length of jmp instruction ({jmpLen})");
        }

        // allocate memory to store the original src prologue bytes we overwrite with jump to dest
        // along with the jump back to src
        if (process.AllocateMemory(jmpLen + overwrittenBytes) is var gate && gate == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            // read the original bytes from the prologue of src
            var origSrcBytes = process.ReadBytes(src, overwrittenBytes);
            if (origSrcBytes == null)
            {
                throw new Win32Exception();
            }

            // write the original prologue of src into the start of gate
            if (!process.WriteBytes(gate, origSrcBytes))
            {
                throw new Win32Exception();
            }

            // write the jump from the end of the gate back to src
            if (!process.WriteJumpInstruction(gate + overwrittenBytes, src + overwrittenBytes))
            {
                throw new Win32Exception();
            }

            // finally write the jump from src to dest
            if (!process.WriteJumpInstruction(src, dest))
            {
                throw new Win32Exception();
            }

            // nop the leftover bytes in the src prologue
            var extraBytes = overwrittenBytes - jmpLen;
            if (extraBytes > 0)
            {
                var nops = Enumerable.Repeat((byte) 0x90, extraBytes).ToArray();
                if (!process.VirtualProtect(src + jmpLen, nops.Length, MemPageProtect.PAGE_EXECUTE_READWRITE, out var oldProtect))
                {
                    throw new Win32Exception();
                }
                if (!process.WriteBytes(src + jmpLen, nops))
                {
                    throw new Win32Exception();
                }
                process.VirtualProtect(src + jmpLen, nops.Length, oldProtect);
            }
        }
        catch
        {
            process.FreeMemory(gate);
            throw;
        }

        return gate;
    }

    private static object ResolveToType(byte[] bytes, Type type)
    {
        if (type == typeof(int))
        {
            return BitConverter.ToInt32(bytes, 0);
        }
        if (type == typeof(uint))
        {
            return BitConverter.ToUInt32(bytes, 0);
        }
        if (type == typeof(float))
        {
            return BitConverter.ToSingle(bytes, 0);
        }
        if (type == typeof(double))
        {
            return BitConverter.ToDouble(bytes, 0);
        }
        if (type == typeof(byte))
        {
            return bytes[0];
        }
        if (type == typeof(bool))
        {
            return bytes[0] != 0;
        }
        if (type == typeof(short))
        {
            return BitConverter.ToInt16(bytes, 0);
        }
        
        // probably a struct
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type) ?? new object();
        }
        finally
        {
            handle.Free();
        }
    }

    public static IntPtr AllocateMemory(this Process process, int size)
    {
        return WinApi.VirtualAllocEx(
            process.Handle,
            IntPtr.Zero,
            (UIntPtr) size,
            (uint) MemPageState.MEM_COMMIT,
            MemPageProtect.PAGE_EXECUTE_READWRITE);
    }

    public static bool FreeMemory(this Process process, IntPtr addr)
    {
        const uint memRelease = 0x8000;
        return WinApi.VirtualFreeEx(process.Handle, addr, UIntPtr.Zero, memRelease);
    }

    public static bool VirtualProtect(this Process process, IntPtr addr, int size, MemPageProtect protect, out MemPageProtect oldProtect)
    {
        return WinApi.VirtualProtectEx(process.Handle, addr, (UIntPtr) size, protect, out oldProtect);
    }

    public static bool VirtualProtect(this Process process, IntPtr addr, int size, MemPageProtect protect)
    {
        return WinApi.VirtualProtectEx(process.Handle, addr, (UIntPtr) size, protect, out _);
    }

    public static IntPtr CreateThread(this Process process, IntPtr startAddress, IntPtr parameter)
    {
        return WinApi.CreateRemoteThread(process.Handle, IntPtr.Zero, 0, startAddress, parameter, 0, out _);
    }

    public static IntPtr CreateThread(this Process process, IntPtr startAddress)
    {
        return CreateThread(process, startAddress, IntPtr.Zero);
    }

    public static void Suspend(this Process process)
    {
        WinApi.NtSuspendProcess(process.Handle);
    }

    public static void Resume(this Process process)
    {
        WinApi.NtResumeProcess(process.Handle);
    }

    public static float ToFloatBits(this uint i)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
    }

    public static uint ToUInt32Bits(this float f)
    {
        return BitConverter.ToUInt32(BitConverter.GetBytes(f), 0);
    }

    public static bool BitEquals(this float f, float o)
    {
        return ToUInt32Bits(f) == ToUInt32Bits(o);
    }
}