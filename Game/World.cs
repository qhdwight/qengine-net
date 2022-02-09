using System;
using System.Collections.Generic;

namespace Game;

public class World
{
    private readonly Dictionary<Type, StorageBase> _components = new();
    private readonly List<Entity> _entities = new();

    public Entity AddEntity()
    {
        var entity = new Entity(_entities.Count);
        _entities.Add(entity);
        return entity;
    }

    public ref T AddComponent<T>(Entity entity) where T : struct
    {
        Type type = typeof(T);
        if (!_components.ContainsKey(type))
            _components[type] = new Storage<T>();

        var storage = (Storage<T>)_components[type];
        storage.Add(entity, new T());
        return ref storage.Get(entity);
    }
}