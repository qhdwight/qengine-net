namespace Game;

public record ValBase
{
}

public record Val<T> : ValBase where T : struct
{
    private T _value;

    public Val(in T value) { _value = value; }
}

public record OptVal<T> : Val<T> where T : struct
{
    private bool _hasValue;

    public OptVal(in T value) : base(value) { _hasValue = true; }
}