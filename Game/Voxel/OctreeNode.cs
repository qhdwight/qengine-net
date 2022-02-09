using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.Maths;

namespace Game.Voxel;

using Vector3Int = Vector3D<int>;

public partial class PointOctree<T>
{
    private class Node
    {
        private const int NumObjectsAllowed = 8;

        private int _minSize;
        private BoundingBox _bounds = default;
        private readonly List<OctreeObject> _objects = new();
        private Node[]? _children;
        private BoundingBox[]? _childBounds;
        private Vector3Int _actualBoundsSize;

        public Vector3Int Center { get; private set; }
        public int SideLength { get; private set; }

        private bool HasChildren => _children is not null;

        private record struct OctreeObject(T Obj, Vector3Int Pos);

        public Node(int baseLengthVal, int minSizeVal, in Vector3Int centerVal)
            => SetValues(baseLengthVal, minSizeVal, centerVal);

        public bool Add(T obj, in Vector3Int objPos)
        {
            if (!Encapsulates(_bounds, objPos))
                return false;

            SubAdd(obj, objPos);
            return true;
        }

        public bool Remove(T obj)
        {
            var removed = false;

            for (var i = 0; i < _objects.Count; i++)
            {
                if (_objects[i].Obj!.Equals(obj))
                {
                    removed = _objects.Remove(_objects[i]);
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

        public bool Remove(T obj, in Vector3Int objPos) => Encapsulates(_bounds, objPos) && SubRemove(obj, objPos);

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

        public void GetNearby(ref Vector3Int position, float maxDistance, ICollection<T> result)
        {
            var maxDistanceInt = (int) Scalar.Ceiling(maxDistance);
            // Does the node contain this position at all?
            _bounds.Expand(new Vector3Int(maxDistanceInt * 2, maxDistanceInt * 2, maxDistanceInt * 2));
            bool contained = _bounds.Contains(position);
            _bounds.Size = _actualBoundsSize;
            if (!contained)
                return;

            // Check against any objects in this node
            foreach (OctreeObject octree in _objects)
                if (Vector3D.Distance(position, octree.Pos) <= maxDistance)
                    result.Add(octree.Obj);

            // Check children
            if (_children is not null)
                for (var i = 0; i < 8; i++)
                    _children[i].GetNearby(ref position, maxDistanceInt, result);
        }

        public void GetAll(List<T> result)
        {
            // add directly contained objects
            result.AddRange(_objects.Select(o => o.Obj));

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
            if (_objects.Count == 0 && (_children is null || _children.Length == 0))
                return this;

            // Check objects in root
            int bestFit = -1;
            for (var i = 0; i < _objects.Count; i++)
            {
                OctreeObject curObj = _objects[i];
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
        /// <param name="objPos">The object's position.</param>
        /// <returns>One of the eight child octants.</returns>
        public int BestFitChild(in Vector3Int objPos)
            => (objPos.X <= Center.X ? 0 : 1) + (objPos.Y >= Center.Y ? 0 : 4) + (objPos.Z <= Center.Z ? 0 : 2);

        /// <summary>
        /// Checks if this node or anything below it has something in it.
        /// </summary>
        /// <returns>True if this node or any of its children, grandchildren etc have something in them</returns>
        public bool HasAnyObjects()
        {
            if (_objects.Count > 0)
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

        private void SubAdd(T obj, in Vector3Int objPos)
        {
            // We know it fits at this level if we've got this far

            // We always put things in the deepest possible child
            // So we can skip checks and simply move down if there are children already
            if (!HasChildren)
            {
                // Just add if few objects are here, or children would be below min size
                if (_objects.Count < NumObjectsAllowed || SideLength / 2 < _minSize)
                {
                    var newObj = new OctreeObject(obj, objPos);
                    _objects.Add(newObj);
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
                    for (int i = _objects.Count - 1; i >= 0; i--)
                    {
                        OctreeObject existingObj = _objects[i];
                        // Find which child the object is closest to based on where the
                        // object's center is located in relation to the octree's center
                        int bestFitChild = BestFitChild(existingObj.Pos);
                        _children[bestFitChild].SubAdd(existingObj.Obj, existingObj.Pos); // Go a level deeper					
                        _objects.Remove(existingObj);                                     // Remove from here
                    }
                }
            }

            // Handle the new object we're adding now
            int bestFit = BestFitChild(objPos);
            _children![bestFit].SubAdd(obj, objPos);
        }

        private bool SubRemove(T obj, in Vector3Int objPos)
        {
            var removed = false;

            for (var i = 0; i < _objects.Count; i++)
            {
                if (_objects[i].Obj!.Equals(obj))
                {
                    removed = _objects.Remove(_objects[i]);
                    break;
                }
            }

            if (!removed && _children is not null)
            {
                int bestFitChild = BestFitChild(objPos);
                removed = _children[bestFitChild].SubRemove(obj, objPos);
            }

            if (removed && _children is not null)
                // Check if we should merge nodes now that we've removed an item
                if (ShouldMerge())
                    Merge();

            return removed;
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
                int numObjects = curChild._objects.Count;
                for (int j = numObjects - 1; j >= 0; j--)
                {
                    OctreeObject curObj = curChild._objects[j];
                    _objects.Add(curObj);
                }
            }
            // Remove the child nodes (and the objects in them - they've been added elsewhere now)
            _children = null;
        }

        private static bool Encapsulates(in BoundingBox outerBounds, in Vector3Int point) => outerBounds.Contains(point);

        /// <summary>
        /// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
        /// </summary>
        /// <returns>True there are less or the same amount of objects in this and its children than <see cref="NumObjectsAllowed"/>.</returns>
        private bool ShouldMerge()
        {
            int totalObjects = _objects.Count;
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
                    totalObjects += child._objects.Count;
                }
            }
            return totalObjects <= NumObjectsAllowed;
        }
    }
}