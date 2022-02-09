using System;
using System.Linq;

namespace Game;

public static class Extensions
{
    public static bool All<T>(this World world, Func<World, T, bool> predicate) where T : struct
        => world.View<T>()
                .Cast<Entity>()
                .All(ent => predicate(world, world.GetComp<T>(ent)));
}