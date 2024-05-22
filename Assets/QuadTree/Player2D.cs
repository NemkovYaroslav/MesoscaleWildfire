using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace QuadTree
{
    public class Player2D : MonoBehaviour
    {
        private QuadTree _linkedQuadTree;
        [SerializeField] private float    obstacleSearchRange = 30.0f;

        private Vector3  _cachedPosition;
        private Vector2? _cached2DPosition;

        private HashSet<ISpatialData2D> _nearbyObstacles;

        private bool HasMoved => !Mathf.Approximately((transform.position - _cachedPosition).sqrMagnitude, 0.0f);

        private void Start()
        {
            _linkedQuadTree = GameObject.FindWithTag("Terrain").GetComponent<QuadTree>();
        }

        private void Update()
        {
            if (_cached2DPosition == null || HasMoved)
            {
                _cachedPosition   = transform.position;
                _cached2DPosition = new Vector2(_cachedPosition.x, _cachedPosition.z);

                HighlightNearbyObstacles();
            }
        }

        private void HighlightNearbyObstacles()
        {
            Debug.Assert(_cached2DPosition != null, nameof(_cached2DPosition) + " != null");
            var candidateObstacles = _linkedQuadTree.FindDataInRange(_cached2DPosition.Value, obstacleSearchRange);
            
            // identify removals
            if (_nearbyObstacles != null)
            {
                foreach (var oldObstacle in _nearbyObstacles)
                {
                    if (candidateObstacles.Contains(oldObstacle))
                    {
                        continue;
                    }

                    ProcessRemoveObstacle(oldObstacle);
                }
            }

            if (candidateObstacles == null)
            {
                return;
            }

            // first time finding obstacles?
            if (_nearbyObstacles == null)
            {
                _nearbyObstacles = candidateObstacles;
                foreach (var newObstacle in _nearbyObstacles)
                {
                    ProcessAddObstacle(newObstacle);
                }

                return;
            }

            // identify additions
            foreach(var newObstacle in candidateObstacles)
            {
                if (_nearbyObstacles.Contains(newObstacle))
                {
                    continue;
                }

                ProcessAddObstacle(newObstacle);
            }

            _nearbyObstacles = candidateObstacles;
        }
        
        private static void ProcessAddObstacle(ISpatialData2D addedObstacle)
        {
            (addedObstacle as Obstacle2D)?.AddHighlight();
        }
        private static void ProcessRemoveObstacle(ISpatialData2D removedObstacle)
        {
            (removedObstacle as Obstacle2D)?.RemoveHighlight();
        }
    }
}
