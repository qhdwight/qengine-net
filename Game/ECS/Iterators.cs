using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.ECS;

public partial class World
{
    // public record struct EntityEnumerable<T>(World _world)
    //     : IEnumerator<T>, IEnumerable<T>
    // {
    //     private readonly World _world = _world;
    //     private int _headIndex = 0;
    //
    //     public bool MoveNext()
    //     {
    //         while (_headIndex < _world._entities.Count)
    //         {
    //             Entity entity = _world._entities[_headIndex++];
    //             if (_world.HasComp<T>(entity))
    //                 return true;
    //         }
    //         return false;
    //     }
    //
    //     public void Reset() => _headIndex = 0;
    //     public T Current => _world.GetComp<T>(_world._entities[_headIndex - 1]);
    //     object IEnumerator.Current => Current!;
    //     IEnumerator<T> IEnumerable<T>.GetEnumerator() => this;
    //     public IEnumerator GetEnumerator() => this;
    //     public void Dispose() { }
    // }

    public record struct EntityEnumerable(World _world, IEnumerable<Type> _types)
        : IEnumerator<Entity>, IEnumerable<Entity>
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
        IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator() => this;
        public IEnumerator GetEnumerator() => this;
        public void Dispose() { }
    }
}