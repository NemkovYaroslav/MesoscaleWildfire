using UnityEngine;

namespace QuadTree
{
    public class Obstacle2D : MonoBehaviour, ISpatialData2D
    {
        [SerializeField] private MeshRenderer linkedMeshRender;

        [SerializeField] private float radius = 0.5f;

        private readonly Color _oldColour = Color.white;
        
        private Rect?    _cachedBounds;
        private Vector2? _cached2DPosition;
        
        public void AddHighlight()
        {
            linkedMeshRender.material.color = Color.red;
        }
        public void RemoveHighlight()
        {
            linkedMeshRender.material.color = _oldColour;
        }

        /*
        private void Update()
        {
            GetBounds();
            
            if (_cachedBounds != null)
            {
                const float height = -6.5f;
                
                var minMin = new Vector3(_cachedBounds.Value.xMin, height, _cachedBounds.Value.yMin);
                var minMax = new Vector3(_cachedBounds.Value.xMin, height, _cachedBounds.Value.yMax);
                var maxMin = new Vector3(_cachedBounds.Value.xMax, height, _cachedBounds.Value.yMin);
                var maxMax = new Vector3(_cachedBounds.Value.xMax, height, _cachedBounds.Value.yMax);
                
                Debug.DrawLine(minMin, minMin + new Vector3(0,1,0), Color.green);
                Debug.DrawLine(minMax, minMax + new Vector3(0,1,0), Color.green);
                Debug.DrawLine(maxMin, maxMin + new Vector3(0,1,0), Color.green);
                Debug.DrawLine(maxMax, maxMax + new Vector3(0,1,0), Color.green);
            }
        }
        */
        
        public Vector2 GetLocation()
        {
            if (_cached2DPosition == null)
            {
                CachePositionData();
            }

            Debug.Assert(_cached2DPosition != null, nameof(_cached2DPosition) + " != null");
            return _cached2DPosition.Value;
        }
        public Rect GetBounds()
        {
            if (_cachedBounds == null)
            {
                CachePositionData();
            }

            Debug.Assert(_cachedBounds != null, nameof(_cachedBounds) + " != null");
            return _cachedBounds.Value;
        }
        public float GetRadius()
        {
            return radius;
        }
        
        private void CachePositionData()
        {
            var position = transform.position;
            _cached2DPosition = new Vector2(position.x, position.z);
            var halfRadius = radius / 2.0f;
            _cachedBounds = new Rect(position.x - halfRadius, position.z - halfRadius, radius, radius);
        }
    }
}
