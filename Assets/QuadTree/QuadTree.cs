#define QUADTREE_TrackStats

using System.Collections.Generic;
using UnityEngine;

namespace QuadTree
{
    public interface ISpatialData2D
    {
        Vector2 GetLocation();
        Rect    GetBounds();
        float   GetRadius();
    }

    public class QuadTree : MonoBehaviour
    {
        private class Node
        {
            private Rect         _bounds;
            private Node[]       _children;
            
            private readonly int _layer = -1;

            private HashSet<ISpatialData2D> _data;

            public Node(Rect inBounds, int inLayer = 0)
            {
                _bounds = inBounds;
                _layer  = inLayer;
            }

            public void AddData(QuadTree owner, ISpatialData2D datum)
            {
                if (_children == null)
                {
                    // first data for node?
                    if (_data == null)
                    {
                        _data = new HashSet<ISpatialData2D>();
                    }

                    // reached the split point and permitted to?
                    if (((_data.Count + 1) >= owner.PreferredMaxDataPerNode) && CanSplit(owner))
                    {
                        SplitNode(owner);

                        AddDataToChildren(owner, datum);
                    }
                    else
                    {
                        _data.Add(datum);
                    }

                    return;
                }

                AddDataToChildren(owner, datum);
            }
            // new bound > 4x4 metres?
            private bool CanSplit(QuadTree owner)
            {
                return (_bounds.width >= (owner.MinimumNodeSize * 2)) && (_bounds.height >= (owner.MinimumNodeSize * 2));
            }

            private void SplitNode(QuadTree owner)
            {
                var halfWidth  = _bounds.width / 2.0f;
                var halfHeight = _bounds.height / 2.0f;
                var newLayer    = _layer + 1;
                
#if QUADTREE_TrackStats
                owner.NewNodesCreated(4, newLayer);
#endif // QUADTREE_TrackStats

                // node can have only 4 children
                _children = new Node[]
                {
                    new(new Rect(  _bounds.xMin,               _bounds.yMin,              halfWidth, halfHeight), newLayer),
                    new(new Rect(_bounds.xMin + halfWidth,   _bounds.yMin,              halfWidth, halfHeight), newLayer),
                    new(new Rect(  _bounds.xMin,             _bounds.yMin + halfHeight, halfWidth, halfHeight), newLayer),
                    new(new Rect(_bounds.xMin + halfWidth, _bounds.yMin + halfHeight, halfWidth, halfHeight), newLayer)
                };

                // distribute the data
                foreach(var datum in _data)
                {
                    AddDataToChildren(owner, datum);
                }

                _data = null;
            }

            private void AddDataToChildren(QuadTree owner, ISpatialData2D datum)
            {
                foreach(var child in _children)
                {
                    if (child.Overlaps(datum.GetBounds()))
                    {
                        child.AddData(owner, datum);
                    }
                }
            }

            private bool Overlaps(Rect other)
            {
                return _bounds.Overlaps(other);
            }

            public void FindDataInBox(Rect searchRect, HashSet<ISpatialData2D> outFoundData)
            {
                if (_children == null)
                {
                    if (_data == null || _data.Count == 0)
                    {
                        return;
                    }

                    outFoundData.UnionWith(_data);

                    return;
                }

                foreach(var child in _children)
                {
                    if (child.Overlaps(searchRect))
                    {
                        child.FindDataInBox(searchRect, outFoundData);
                    }
                }
            }

            public void FindDataInRange(Vector2 searchLocation, float searchRange, HashSet<ISpatialData2D> outFoundData)
            {
                if (_layer != 0)
                {
                    throw new System.InvalidOperationException("FindDataInRange cannot be run on anything other than the root node.");
                }

                var searchRect = new Rect(searchLocation.x - searchRange, searchLocation.y - searchRange, searchRange * 2.0f, searchRange * 2.0f);

                FindDataInBox(searchRect, outFoundData);

                outFoundData.RemoveWhere(
                    datum => 
                    {
                        var testRange = searchRange + datum.GetRadius();

                        return (searchLocation - datum.GetLocation()).sqrMagnitude > (testRange * testRange);
                    }
                );
            }
        }
        
        [field: SerializeField] public int PreferredMaxDataPerNode { get; private set; } = 50;
        [field: SerializeField] public int MinimumNodeSize { get; private set; } = 2;

        
        private Node _rootNode;
        
        public void PrepareTree(Rect bounds)
        {
            _rootNode = new Node(bounds);
            
#if QUADTREE_TrackStats
            _numNodes = 0;
            _maxLayer = -1;
#endif // QUADTREE_TrackStats
        }
        
        // add single element to node
        public void AddData(ISpatialData2D datum)
        {
            _rootNode.AddData(this, datum);
        }
        // add several elements to node
        public void AddData(List<ISpatialData2D> data)
        {
            foreach(var datum in data)
            {
                AddData(datum);
            }
        }
        
        public void ShowStats()
        {
#if QUADTREE_TrackStats
            Debug.Log($"Max Depth: {_maxLayer}");
            Debug.Log($"Num Nodes: {_numNodes}");
#endif // QUADTREE_TrackStats
        }
        
        public HashSet<ISpatialData2D> FindDataInRange(Vector2 searchLocation, float searchRange)
        {
#if QUADTREE_TrackStats
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
#endif // QUADTREE_TrackStats
            
            var foundData = new HashSet<ISpatialData2D>();
            _rootNode.FindDataInRange(searchLocation, searchRange, foundData);
            
#if QUADTREE_TrackStats
            stopWatch.Stop();
            Debug.Log($"Search found {foundData.Count} results in {stopWatch.ElapsedMilliseconds} ms");
#endif // QUADTREE_TrackStats

            return foundData;
        }
        
#if QUADTREE_TrackStats
        private int _maxLayer = -1;
        private int _numNodes = 0;

        public void NewNodesCreated(int numAdded, int nodeLayer)
        {
            _numNodes += numAdded;
            _maxLayer = Mathf.Max(_maxLayer, nodeLayer);
        }
#endif // QUADTREE_TrackStats
    }
}