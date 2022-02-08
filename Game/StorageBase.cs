using System;
using System.Collections.Generic;

namespace Game;

public abstract class StorageBase
{
}

public class Storage<T> : StorageBase where T : ValBase
{
    private const int DefaultSparseSize = 64, InvalidEntry = -1;

    private readonly List<T> _components = new();
    private List<int> _sparse = new(DefaultSparseSize);
    private readonly List<int> _dense = new();

    public void Add(Entity entity, T component)
    {
        int sparseIndex = _dense.Count;
        _dense.Add(entity.Index);
        _sparse.Capacity = Math.Max(_sparse.Capacity, sparseIndex + 1);
        while (sparseIndex >= _sparse.Count)
            _sparse.Add(InvalidEntry);
        _sparse[entity.Index] = sparseIndex;
        _components.Add(component);
    }

    public void Remove(Entity entity)
    {
        int sparseIndex = _sparse[entity.Index];
        _dense[sparseIndex] = _dense[^1];
        _sparse[_dense[^1]] = sparseIndex;
        _dense.RemoveAt(_dense.Count - 1);
        _sparse[entity.Index] = -1;
    }

    public bool TryGet(Entity entity, out T component)
    {
        int sparseIndex = _sparse[entity.Index];
        if (sparseIndex == InvalidEntry)
        {
            component = default;
            return false;
        }
        component = _components[sparseIndex];
        return true;
    }
}