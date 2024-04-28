using System;

namespace VeninethTrainer;

public class SingleUseValue<T>
    where T : notnull
{
    private T? _current;

    public void Set(T value) => _current = value;

    public bool TrySet(T value)
    {
        if (_current is not null) return false;
        _current = value;
        return true;
    }
    
    public bool TryGet(out T value)
    {
        if (_current is null)
        {
            value = default!;
            return false;
        }
        value = _current;
        return true;
    }
}