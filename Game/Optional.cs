namespace Game;

public record Optional<T>(in T _value) where T : struct
{
    private bool _hasValue = true;
    private T _value = _value;

    public T Value
    {
        set
        {
            _value = value;
            _hasValue = true;
        }
    }

    public bool With(out T value)
    {
        if (_hasValue)
        {
            value = _value;
            return true;
        }
        value = default;
        return false;
    }
}