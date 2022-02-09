using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Game;

public class World
{
    private readonly Dictionary<Type, StorageBase> _components = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Type> _cachedTypes = new();

    public Entity AddEntity()
    {
        var entity = new Entity(_entities.Count);
        _entities.Add(entity);
        return entity;
    }

    public ref T Add<T>(Entity entity, T component) where T : struct
    {
        Type type = typeof(T);
        if (!_components.ContainsKey(type))
            _components[type] = new Storage<T>();
        var storage = (Storage<T>)_components[type];
        Debug.Assert(storage.Without(entity));
        ref T inPlaceComp = ref storage.Add(entity, new T());
        inPlaceComp = component;
        return ref inPlaceComp;
    }

    public ref T Get<T>(Entity entity) where T : struct
    {
        Type type = typeof(T);
        Debug.Assert(_components.ContainsKey(type));
        var storage = (Storage<T>)_components[type];
        return ref storage.Get(entity);
    }

    public bool HasView(Entity entity, IEnumerable<Type> types)
        => types.All(type => _components[type].With(entity));

    public EntityEnumerable View(IEnumerable<Type> types) => new(this, types);

    public EntityEnumerable View<T>()
    {
        _cachedTypes.Clear();
        _cachedTypes.Add(typeof(T));
        return new EntityEnumerable(this, _cachedTypes);
    }

    public EntityEnumerable View<T1, T2>()
    {
        _cachedTypes.Clear();
        _cachedTypes.Add(typeof(T1));
        _cachedTypes.Add(typeof(T2));
        return new EntityEnumerable(this, _cachedTypes);
    }

    public record struct EntityEnumerable(World _world, IEnumerable<Type> _types)
        : IEnumerator<Entity>, IEnumerable
    {
        private readonly World _world = _world;
        private readonly IEnumerable<Type> _types = _types;
        private int _headIndex = 0;

        public bool MoveNext()
        {
            while (_headIndex < _world._entities.Count)
            {
                Entity entity = _world._entities[_headIndex++];
                if (_world.HasView(entity, _types))
                    return true;
            }
            return false;
        }

        public void Reset() => _headIndex = 0;

        public Entity Current => _world._entities[_headIndex - 1];

        object IEnumerator.Current => Current;

        public IEnumerator GetEnumerator() => this;

        public void Dispose() { }
    }
}