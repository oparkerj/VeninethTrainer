using System;

namespace VeninethTrainer;

public class SingleUseValue<T>
    where T : notnull
{
    private T _current = default!;
    public bool HasValue { get; private set; }
    public bool WasRead { get; private set; }

    public T Value
    {
        get
        {
            WasRead = true;
            return _current;
        }
        set
        {
            _current = value;
            HasValue = true;
            WasRead = false;
        }
    }

    public bool TryGet(out T value)
    {
        if (!HasValue)
        {
            value = default!;
            return false;
        }
        value = _current;
        HasValue = false;
        WasRead = true;
        return true;
    }

    public bool TrySet(T value)
    {
        if (HasValue) return false;
        Value = value;
        return true;
    }

    public bool TrySetNew(T value)
    {
        if (WasRead) return false;
        Value = value;
        return true;
    }

    public void SetDefaultValue(Func<T> getter)
    {
        if (!WasRead)
        {
            _current = getter();
        }
    }
}