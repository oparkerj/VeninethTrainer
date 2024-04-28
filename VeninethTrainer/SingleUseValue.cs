using System;

namespace VeninethTrainer;

public class SingleUseValue<T>
{
    private T _current = default!;
    private bool _hasValue;

    public void Set(T value)
    {
        _current = value;
        _hasValue = true;
    }

    public bool TrySet(T value)
    {
        if (_hasValue) return false;
        Set(value);
        return true;
    }
    
    public bool TryGet(out T value)
    {
        if (_hasValue)
        {
            value = _current;
            _hasValue = false;
            return true;
        }
        value = default!;
        return false;
    }
}