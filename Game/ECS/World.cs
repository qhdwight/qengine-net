using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Game.ECS;

public partial class World
{
    private readonly Dictionary<Type, StorageBase> _components = new();
    private readonly List<Entity> _entities = new();

    public Entity AddEntity()
    {
        var entity = new Entity(_entities.Count);
        _entities.Add(entity);
        return entity;
    }

    public ref T AddComp<T>(Entity entity, T component)
    {
        Type type = typeof(T);
        if (!_components.ContainsKey(type)) _components[type] = new Storage<T>();
        var storage = (Storage<T>)_components[type];
        Debug.Assert(storage.Without(entity));
        return ref storage.Add(entity, component);
    }

    public ref T GetComp<T>(Entity entity)
    {
        Type type = typeof(T);
        Debug.Assert(_components.ContainsKey(type));
        var storage = (Storage<T>)_components[type];
        return ref storage.Get(entity);
    }

    public bool HasComp(Entity entity, Type type)
        => _components.TryGetValue(type, out StorageBase? storage) && storage.With(entity);

    public bool HasComp<T>(Entity entity)
        => HasComp(entity, typeof(T));

    public bool HasView(Entity entity, IEnumerable<Type> types)
    {
        foreach (Type type in types)
            if (!HasComp(entity, type)) return false;
        return true;
    }

    public EntityEnumerable View(IEnumerable<Type> types) => new(this, types);

    public EntityEnumerable View<T>()
    {
        Type[] types = { typeof(T) };
        return new EntityEnumerable(this, types);
    }

    public EntityEnumerable View<T1, T2>()
    {
        Type[] types = { typeof(T1), typeof(T2) };
        return new EntityEnumerable(this, types);
    }

    public EntityEnumerable View<T1, T2, T3>()
    {
        Type[] types = { typeof(T1), typeof(T2), typeof(T3) };
        return new EntityEnumerable(this, types);
    }

    public EntityEnumerable View<T1, T2, T3, T4>()
    {
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
        return new EntityEnumerable(this, types);
    }

    public bool All<T>(Func<World, T, bool> predicate)
    {
        foreach (Entity ent in View<T>())
            if (!predicate(this, GetComp<T>(ent)))
                return false;
        return true;
    }
}