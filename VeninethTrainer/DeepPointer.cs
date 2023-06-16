using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// Note: Please be careful when modifying this because it could break existing components!

namespace VeninethTrainer;

public class DeepPointer
{
    private string _module;
    private IntPtr _base;
    private bool _absolute;

    private List<int> _offsets;

    private DeepPointer(string module, IntPtr @base, bool absolute, int[] offsets)
    {
        _base = @base;
        _absolute = absolute;
        
        _offsets = new List<int> {0};
        _offsets.AddRange(offsets);

        _module = module;
    }

    public DeepPointer(IntPtr @base, params int[] offsets)
        : this(string.Empty, @base, true, offsets) { }
    
    public DeepPointer(int @base, params int[] offsets)
        : this(string.Empty, @base, false, offsets) { }

    public DeepPointer(string module, int @base, params int[] offsets)
        : this(module, @base, false, offsets) { }

    public T Deref<T>(Process process, T @default = default)
        where T : struct
    {
        if (!Deref(process, out T val))
        {
            val = @default;
        }
        return val;
    }

    public bool Deref<T>(Process process, out T value)
        where T : struct
    {
        if (DerefOffsets(process, out var ptr) && process.ReadValue(ptr, out value)) return true;
        value = default;
        return false;
    }

    public byte[]? DerefBytes(Process process, int count)
    {
        if (!DerefBytes(process, count, out var bytes))
        {
            bytes = null;
        }
        return bytes;
    }

    public bool DerefBytes(Process process, int count, out byte[]? value)
    {
        IntPtr ptr;
        if (DerefOffsets(process, out ptr) && process.ReadBytes(ptr, count, out value)) return true;
        value = null;
        return false;
    }

    public string? DerefString(Process process, int numBytes, string? @default = null)
    {
        if (!DerefString(process, ReadStringType.AutoDetect, numBytes, out var str))
        {
            str = @default;
        }
        return str;
    }

    public string? DerefString(Process process, ReadStringType type, int numBytes, string? @default = null)
    {
        if (!DerefString(process, type, numBytes, out var str))
        {
            str = @default;
        }
        return str;
    }

    public bool DerefString(Process process, int numBytes, out string str)
    {
        return DerefString(process, ReadStringType.AutoDetect, numBytes, out str);
    }

    public bool DerefString(Process process, ReadStringType type, int numBytes, out string? str)
    {
        var sb = new StringBuilder(numBytes);
        if (!DerefString(process, type, sb))
        {
            str = null;
            return false;
        }
        str = sb.ToString();
        return true;
    }

    public bool DerefString(Process process, StringBuilder sb)
    {
        return DerefString(process, ReadStringType.AutoDetect, sb);
    }

    public bool DerefString(Process process, ReadStringType type, StringBuilder sb)
    {
        return DerefOffsets(process, out var ptr) && process.ReadString(ptr, type, sb);
    }

    public bool DerefOffsets(Process process, out IntPtr ptr)
    {
        var is64Bit = process.Is64Bit();

        if (!string.IsNullOrEmpty(_module))
        {
            var module = process.ModulesWow64Safe().FirstOrDefault(m => m.ModuleName?.ToLower() == _module);
            if (module == null)
            {
                ptr = IntPtr.Zero;
                return false;
            }

            ptr = module.BaseAddress + _base;
        }
        else if (_absolute)
        {
            ptr = _base;
        }
        else
        {
            ptr = process.MainModuleWow64Safe().BaseAddress + _base;
        }

        for (var i = 0; i < _offsets.Count - 1; i++)
        {
            if (!process.ReadPointer(ptr + _offsets[i], is64Bit, out ptr) || ptr == IntPtr.Zero)
            {
                return false;
            }
        }

        ptr += _offsets[^1];
        return true;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Vector3f
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public int IX => (int) X;
    public int IY => (int) Y;
    public int IZ => (int) Z;

    public Vector3f(float x, float y, float z) : this()
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float Distance(Vector3f other)
    {
        var result = (X - other.X) * (X - other.X) +
                     (Y - other.Y) * (Y - other.Y) +
                     (Z - other.Z) * (Z - other.Z);
        return (float) Math.Sqrt(result);
    }

    public float DistanceXY(Vector3f other)
    {
        var result = (X - other.X) * (X - other.X) +
                     (Y - other.Y) * (Y - other.Y);
        return (float) Math.Sqrt(result);
    }

    public bool BitEquals(Vector3f other)
    {
        return X.BitEquals(other.X) && Y.BitEquals(other.Y) && Z.BitEquals(other.Z);
    }

    public bool BitEqualsXY(Vector3f other)
    {
        return X.BitEquals(other.X) && Y.BitEquals(other.Y);
    }

    public override string ToString()
    {
        return $"{X} {Y} {Z}";
    }
}