using System;
using System.Collections.Generic;

namespace Game;

public abstract class StorageBase
{
}

// TODO: add pagination to avoid large sparse size
public class Storage<T> : StorageBase where T : struct
{
    private const int NilEntity = -1;

    private T[] _components = Array.Empty<T>();
    private int[] _sparse = Array.Empty<int>();
    private readonly List<int> _dense = new();

    public void Add(Entity entity, T component)
    {
        int index = _dense.Count;
        _dense.Add(entity.Index);
        if (index >= _sparse.Length)
        {
            int oldLength = _sparse.Length;
            Array.Resize(ref _sparse, index + 1);
            for (int i = oldLength; i < _sparse.Length; i++)
                _sparse[i] = NilEntity;
            Array.Resize(ref _components, index + 1);
        }
        _sparse[entity.Index] = index;
        _components[index] = component;
    }

    public void Remove(Entity entity)
    {
        // TODO: validate
        int index = _sparse[entity.Index];
        _dense[index] = _dense[^1];
        _sparse[_dense[^1]] = index;
        _dense.RemoveAt(_dense.Count - 1);
        _sparse[entity.Index] = NilEntity;
    }

    public ref T Get(Entity entity)
    {
        int index = _sparse[entity.Index];
        return ref _components[index];
    }

    public bool With(Entity entity) => entity.Index < _sparse.Length
                                    && _sparse[entity.Index] < _dense.Count
                                    && _sparse[entity.Index] == NilEntity;
}