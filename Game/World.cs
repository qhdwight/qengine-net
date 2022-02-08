using System;
using System.Collections.Generic;

namespace Game
{
    public class World
    {
        private readonly Dictionary<Type, StorageBase> _components = new();
    }
}