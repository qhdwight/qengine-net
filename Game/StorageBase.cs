using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Game;

public abstract class StorageBase
{
    protected const int NilEntity = -1;
    protected readonly List<int> _dense = new();

    protected int[] _sparse = Array.Empty<int>();

    public bool With(Entity entity) => entity.Index < _sparse.Length
                                    && _sparse[entity.Index] < _dense.Count
                                    && _sparse[entity.Index] != NilEntity;

    public bool Without(Entity entity) { return !With(entity); }
}

// TODO: add pagination to avoid large sparse size
public class Storage<T> : StorageBase where T : struct
{
    private T[] _components = Array.Empty<T>();

    public ref T Add(Entity entity, T component)
    {
        int denseIdx = _dense.Count;
        int sparseIdx = entity.Index;
        _dense.Add(sparseIdx);
        if (sparseIdx >= _sparse.Length)
        {
            int oldLength = _sparse.Length;
            Array.Resize(ref _sparse, sparseIdx + 1);
            for (int i = oldLength; i < _sparse.Length; i++)
                _sparse[i] = NilEntity;
            Array.Resize(ref _components, sparseIdx + 1);
        }
        _sparse[sparseIdx] = denseIdx;
        _components[denseIdx] = component;
        return ref _components[denseIdx];
    }

    public void Remove(Entity entity)
    {
        // TODO: validate
        int denseIdx = _sparse[entity.Index];
        _dense[denseIdx] = _dense[^1];
        _sparse[_dense[^1]] = denseIdx;
        _dense.RemoveAt(_dense.Count - 1);
        _sparse[entity.Index] = NilEntity;
    }

    public ref T Get(Entity entity)
    {
        int denseIdx = _sparse[entity.Index];
        Debug.Assert(denseIdx != NilEntity);
        return ref _components[denseIdx];
    }
}