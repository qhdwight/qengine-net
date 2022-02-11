using System;
using System.Collections.Generic;
using Silk.NET.Maths;

namespace Game.Voxels.Collections;

using Vector3Int = Vector3D<int>;

public partial class Octree<T>
{
    public record struct Leaf(T Value, Vector3Int Pos);

    private class Node
    {
        private const int NumValsAllowed = 8;

        private int _minSize;
        private BoundingBox _bounds = default;
        private readonly List<Leaf> _leaves = new();
        private Node[]? _children;
        private BoundingBox[]? _childBounds;
        private Vector3Int _actualBoundsSize;

        public Vector3Int Center { get; private set; }
        public int SideLength { get; private set; }

        private bool HasChildren => _children is not null;

        public Node(int baseLengthVal, int minSizeVal, in Vector3Int centerVal)
            => SetValues(baseLengthVal, minSizeVal, centerVal);

        public bool Add(T obj, in Vector3Int pos)
        {
            if (!Encapsulates(_bounds, pos))
                return false;

            SubAdd(obj, pos);
            return true;
        }

        public bool Remove(T obj)
        {
            var removed = false;

            for (var i = 0; i < _leaves.Count; i++)
            {
                if (_leaves[i].Value!.Equals(obj))
                {
                    removed = _leaves.Remove(_leaves[i]);
                    break;
                }
            }

            if (!removed && _children is not null)
            {
                for (var i = 0; i < 8; i++)
                {
                    removed = _children[i].Remove(obj);
                    if (removed) break;
                }
            }

            if (removed && _children is not null)
            {
                // Check if we should merge nodes now that we've removed an item
                if (ShouldMerge())
                    Merge();
            }

            return removed;
        }

        public bool Remove(in Vector3Int pos) => Encapsulates(_bounds, pos) && SubRemove(pos);

        // public void GetNearby(ref Ray ray, float maxDistance, List<T> result)
        // {
        //     // Does the ray hit this node at all?
        //     // Note: Expanding the bounds is not exactly the same as a real distance check, but it's fast.
        //     // TODO: Does someone have a fast AND accurate formula to do this check?
        //     _bounds.Expand(new Vector3Int(maxDistance * 2, maxDistance * 2, maxDistance * 2));
        //     bool intersected = _bounds.IntersectRay(ray);
        //     _bounds.Size = _actualBoundsSize;
        //     if (!intersected)
        //     {
        //         return;
        //     }
        //
        //     // Check against any objects in this node
        //     for (var i = 0; i < _objects.Count; i++)
        //     {
        //         if (SqrDistanceToRay(ray, _objects[i].Pos) <= (maxDistance * maxDistance))
        //         {
        //             result.Add(_objects[i].Obj);
        //         }
        //     }
        //
        //     // Check children
        //     if (_children is not null)
        //     {
        //         for (var i = 0; i < 8; i++)
        //         {
        //             _children[i].GetNearby(ref ray, maxDistance, result);
        //         }
        //     }
        // }

        public bool TryGet(in Vector3Int pos, out T? val)
        {
            if (!HasChildren)
            {
                foreach ((T leafVal, Vector3Int leafPos) in _leaves)
                {
                    if (pos == leafPos)
                    {
                        val = leafVal;
                        return true;
                    }   
                }
                val = default;
                return false;
            }

            int bestFit = BestFitChild(pos);
            return _children![bestFit].TryGet(pos, out val);
        }

        public void GetNearby(in Vector3Int position, float maxDistance, ICollection<T> result)
        {
            var maxDistanceInt = (int)Scalar.Ceiling(maxDistance);
            // Does the node contain this position at all?
            _bounds.Expand(new Vector3Int(maxDistanceInt * 2, maxDistanceInt * 2, maxDistanceInt * 2));
            bool contained = _bounds.Contains(position);
            _bounds.Size = _actualBoundsSize;
            if (!contained)
                return;

            // Check against any objects in this node
            foreach (Leaf octree in _leaves)
                if (Vector3D.Distance(position, octree.Pos) <= maxDistance)
                    result.Add(octree.Value);

            // Check children
            if (_children is not null)
                for (var i = 0; i < 8; i++)
                    _children[i].GetNearby(position, maxDistanceInt, result);
        }

        public void GetAll(ICollection<Leaf> result)
        {
            // add directly contained objects
            foreach (Leaf leaf in _leaves)
                result.Add(leaf);

            // add children objects
            if (_children is not null)
                for (var i = 0; i < 8; i++)
                    _children[i].GetAll(result);
        }

        public void SetChildren(Node[] childOctrees)
        {
            if (childOctrees.Length != 8)
            {
                Console.Error.WriteLine($"Child octree array must be length 8. Was length: {childOctrees.Length}");
                return;
            }
            _children = childOctrees;
        }

        /// <summary>
        /// We can shrink the octree if:
        /// - This node is >= double minLength in length
        /// - All objects in the root node are within one octant
        /// - This node doesn't have children, or does but 7/8 children are empty
        /// We can also shrink it if there are no objects left at all!
        /// </summary>
        public Node ShrinkIfPossible(int minLength)
        {
            if (SideLength < 2 * minLength)
                return this;
            if (_leaves.Count == 0 && (_children is null || _children.Length == 0))
                return this;

            // Check objects in root
            int bestFit = -1;
            for (var i = 0; i < _leaves.Count; i++)
            {
                Leaf curObj = _leaves[i];
                int newBestFit = BestFitChild(curObj.Pos);
                if (i == 0 || newBestFit == bestFit)
                {
                    if (bestFit < 0)
                        bestFit = newBestFit;
                }
                else
                {
                    return this; // Can't reduce - objects fit in different octants
                }
            }

            // Check objects in children if there are any
            if (_children is not null)
            {
                var childHadContent = false;
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i].HasAnyObjects())
                    {
                        if (childHadContent)
                            return this; // Can't shrink - another child had content already
                        if (bestFit >= 0 && bestFit != i)
                            return this; // Can't reduce - objects in root are in a different octant to objects in child
                        childHadContent = true;
                        bestFit = i;
                    }
                }
            }

            // Can reduce
            if (_children is null)
            {
                // We don't have any children, so just shrink this node to the new size
                // We already know that everything will still fit in it
                SetValues(SideLength / 2, _minSize, _childBounds![bestFit].Center);
                return this;
            }

            // We have children. Use the appropriate child as the new root node
            return _children[bestFit];
        }

        /// <summary>
        /// Find which child node this object would be most likely to fit in.
        /// </summary>
        /// <param name="pos">The object's position.</param>
        /// <returns>One of the eight child octants.</returns>
        public int BestFitChild(in Vector3Int pos) => (pos.X <= Center.X ? 0 : 1)
                                                    + (pos.Y >= Center.Y ? 0 : 4)
                                                    + (pos.Z <= Center.Z ? 0 : 2);

        /// <summary>
        /// Checks if this node or anything below it has something in it.
        /// </summary>
        /// <returns>True if this node or any of its children, grandchildren etc have something in them</returns>
        public bool HasAnyObjects()
        {
            if (_leaves.Count > 0)
                return true;

            if (_children is not null)
                for (var i = 0; i < 8; i++)
                    if (_children[i].HasAnyObjects())
                        return true;

            return false;
        }

        // public static float SqrDistanceToRay(Ray ray, Vector3Int point) { return Point.Cross(ray.Direction, point - ray.Origin).SqrMagnitude; }

        private void SetValues(int baseLengthVal, int minSizeVal, in Vector3Int centerVal)
        {
            SideLength = baseLengthVal;
            _minSize = minSizeVal;
            Center = centerVal;

            // Create the bounding box.
            _actualBoundsSize = new Vector3Int(SideLength, SideLength, SideLength);
            _bounds = new BoundingBox(Center, _actualBoundsSize);

            int quarter = SideLength / 4;
            int childActualLength = SideLength / 2;
            var childActualSize = new Vector3Int(childActualLength, childActualLength, childActualLength);
            _childBounds = new BoundingBox[8];
            _childBounds[0] = new BoundingBox(Center + new Vector3Int(-quarter, quarter, -quarter), childActualSize);
            _childBounds[1] = new BoundingBox(Center + new Vector3Int(quarter, quarter, -quarter), childActualSize);
            _childBounds[2] = new BoundingBox(Center + new Vector3Int(-quarter, quarter, quarter), childActualSize);
            _childBounds[3] = new BoundingBox(Center + new Vector3Int(quarter, quarter, quarter), childActualSize);
            _childBounds[4] = new BoundingBox(Center + new Vector3Int(-quarter, -quarter, -quarter), childActualSize);
            _childBounds[5] = new BoundingBox(Center + new Vector3Int(quarter, -quarter, -quarter), childActualSize);
            _childBounds[6] = new BoundingBox(Center + new Vector3Int(-quarter, -quarter, quarter), childActualSize);
            _childBounds[7] = new BoundingBox(Center + new Vector3Int(quarter, -quarter, quarter), childActualSize);
        }

        private void SubAdd(T val, in Vector3Int pos)
        {
            // We know it fits at this level if we've got this far

            // We always put things in the deepest possible child
            // So we can skip checks and simply move down if there are children already
            if (!HasChildren)
            {
                // Just add if few objects are here, or children would be below min size
                if (_leaves.Count < NumValsAllowed || SideLength / 2 < _minSize)
                {
                    var newObj = new Leaf(val, pos);
                    _leaves.Add(newObj);
                    return; // We're done. No children yet
                }

                // Enough objects in this node already: Create the 8 children
                if (_children is null)
                {
                    Split();
                    if (_children is null)
                    {
                        Console.Error.WriteLine("Child creation failed for an unknown reason. Early exit.");
                        return;
                    }

                    // Now that we have the new children, move this node's existing objects into them
                    for (int i = _leaves.Count - 1; i >= 0; i--)
                    {
                        Leaf existingObj = _leaves[i];
                        // Find which child the object is closest to based on where the
                        // object's center is located in relation to the octree's center
                        int bestFitChild = BestFitChild(existingObj.Pos);
                        _children[bestFitChild].SubAdd(existingObj.Value, existingObj.Pos); // Go a level deeper					
                        _leaves.Remove(existingObj);                                        // Remove from here
                    }
                }
            }

            // Handle the new object we're adding now
            int bestFit = BestFitChild(pos);
            _children![bestFit].SubAdd(val, pos);
        }

        private bool SubRemove(in Vector3Int pos)
        {
            var wasRemoved = false;

            for (var i = 0; i < _leaves.Count; i++)
            {
                if (_leaves[i].Pos == pos)
                {
                    wasRemoved = _leaves.Remove(_leaves[i]);
                    break;
                }
            }

            if (!wasRemoved && _children is not null)
            {
                int bestFitChild = BestFitChild(pos);
                wasRemoved = _children[bestFitChild].SubRemove(pos);
            }

            // Check if we should merge nodes now that we've removed an item
            if (wasRemoved && _children is not null && ShouldMerge())
                Merge();

            return wasRemoved;
        }

        /// <summary>
        /// Splits the octree into eight children.
        /// </summary>
        private void Split()
        {
            int quarter = SideLength / 4;
            int newLength = SideLength / 2;
            _children = new Node[8];
            _children[0] = new Node(newLength, _minSize, Center + new Vector3Int(-quarter, quarter, -quarter));
            _children[1] = new Node(newLength, _minSize, Center + new Vector3Int(quarter, quarter, -quarter));
            _children[2] = new Node(newLength, _minSize, Center + new Vector3Int(-quarter, quarter, quarter));
            _children[3] = new Node(newLength, _minSize, Center + new Vector3Int(quarter, quarter, quarter));
            _children[4] = new Node(newLength, _minSize, Center + new Vector3Int(-quarter, -quarter, -quarter));
            _children[5] = new Node(newLength, _minSize, Center + new Vector3Int(quarter, -quarter, -quarter));
            _children[6] = new Node(newLength, _minSize, Center + new Vector3Int(-quarter, -quarter, quarter));
            _children[7] = new Node(newLength, _minSize, Center + new Vector3Int(quarter, -quarter, quarter));
        }

        /// <summary>
        /// Merge all children into this node - the opposite of Split.
        /// Note: We only have to check one level down since a merge will never happen if the children already have children,
        /// since THAT won't happen unless there are already too many objects to merge.
        /// </summary>
        private void Merge()
        {
            // Note: We know children is not null or we wouldn't be merging
            for (var i = 0; i < 8; i++)
            {
                Node curChild = _children![i];
                int numObjects = curChild._leaves.Count;
                for (int j = numObjects - 1; j >= 0; j--)
                {
                    Leaf curObj = curChild._leaves[j];
                    _leaves.Add(curObj);
                }
            }
            // Remove the child nodes (and the objects in them - they've been added elsewhere now)
            _children = null;
        }

        private static bool Encapsulates(in BoundingBox outerBounds, in Vector3Int point) => outerBounds.Contains(point);

        /// <summary>
        /// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
        /// </summary>
        /// <returns>True there are less or the same amount of objects in this and its children than <see cref="NumValsAllowed"/>.</returns>
        private bool ShouldMerge()
        {
            int totalObjects = _leaves.Count;
            if (_children is not null)
            {
                foreach (Node child in _children)
                {
                    if (child._children is not null)
                    {
                        // If any of the *children* have children, there are definitely too many to merge,
                        // or the child would have been merged already
                        return false;
                    }
                    totalObjects += child._leaves.Count;
                }
            }
            return totalObjects <= NumValsAllowed;
        }
    }
}