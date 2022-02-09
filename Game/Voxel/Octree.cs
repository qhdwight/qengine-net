using System;
using System.Collections.Generic;
using Silk.NET.Maths;

namespace Game.Voxel;

using Vector3Int = Vector3D<int>;

public partial class PointOctree<T>
{
    private Node _rootNode;
    private readonly int _initialSize;
    private readonly int _minSize;

    public int Count { get; private set; }

    public BoundingBox MaxBounds => new(_rootNode.Center, new Vector3Int(_rootNode.SideLength, _rootNode.SideLength, _rootNode.SideLength));

    public PointOctree(int initialWorldSize, in Vector3Int initialWorldPos, int minNodeSize)
    {
        if (minNodeSize > initialWorldSize)
        {
            Console.WriteLine($"Minimum node size must be at least as big as the initial world size. Was: {minNodeSize} Adjusted to: {initialWorldSize}");
            minNodeSize = initialWorldSize;
        }
        Count = 0;
        _initialSize = initialWorldSize;
        _minSize = minNodeSize;
        _rootNode = new Node(_initialSize, _minSize, initialWorldPos);
    }

    public void Add(T obj, in Vector3Int objPos)
    {
        // Add object or expand the octree until it can be added
        var count = 0; // Safety check against infinite/excessive growth
        while (!_rootNode.Add(obj, objPos))
        {
            Grow(objPos - _rootNode.Center);
            if (++count > 20)
            {
                Console.Error.WriteLine($"Aborted Add operation as it seemed to be going on forever ({count - 1}) attempts at growing the octree.");
                return;
            }
        }
        Count++;
    }

    public bool Remove(T obj)
    {
        bool removed = _rootNode.Remove(obj);

        // See if we can shrink the octree down now that we've removed the item
        if (removed)
        {
            Count--;
            Shrink();
        }

        return removed;
    }

    public bool Remove(T obj, in Vector3Int objPos)
    {
        bool removed = _rootNode.Remove(obj, objPos);
        // See if we can shrink the octree down now that we've removed the item
        if (removed)
        {
            Count--;
            Shrink();
        }
        return removed;
    }

    // public T[] GetNearby(Ray ray, float maxDistance)
    // {
    //     var collidingWith = new List<T>();
    //     _rootNode.GetNearby(ref ray, maxDistance, collidingWith);
    //     return collidingWith.ToArray();
    // }

    public T[] GetNearby(Vector3Int position, float maxDistance)
    {
        var collidingWith = new List<T>();
        _rootNode.GetNearby(ref position, maxDistance, collidingWith);
        return collidingWith.ToArray();
    }

    public ICollection<T> GetAll()
    {
        var objects = new List<T>(Count);
        _rootNode.GetAll(objects);
        return objects;
    }

    private void Grow(in Vector3Int direction)
    {
        int xDirection = direction.X >= 0 ? 1 : -1;
        int yDirection = direction.Y >= 0 ? 1 : -1;
        int zDirection = direction.Z >= 0 ? 1 : -1;
        Node oldRoot = _rootNode;
        int half = _rootNode.SideLength / 2;
        int newLength = _rootNode.SideLength * 2;
        Vector3Int newCenter = _rootNode.Center + new Vector3Int(xDirection * half, yDirection * half, zDirection * half);

        // Create a new, bigger octree root node
        _rootNode = new Node(newLength, _minSize, newCenter);

        if (oldRoot.HasAnyObjects())
        {
            // Create 7 new octree children to go with the old root as children of the new root
            int rootPos = _rootNode.BestFitChild(oldRoot.Center);
            var children = new Node[8];
            for (var i = 0; i < 8; i++)
            {
                if (i == rootPos)
                {
                    children[i] = oldRoot;
                }
                else
                {
                    xDirection = i % 2 == 0 ? -1 : 1;
                    yDirection = i > 3 ? -1 : 1;
                    zDirection = i is < 2 or > 3 and < 6 ? -1 : 1;
                    Vector3Int center = newCenter + new Vector3Int(xDirection * half, yDirection * half, zDirection * half);
                    children[i] = new Node(oldRoot.SideLength, _minSize, center);
                }
            }

            // Attach the new children to the new root node
            _rootNode.SetChildren(children);
        }
    }

    private void Shrink() => _rootNode = _rootNode.ShrinkIfPossible(_initialSize);
}