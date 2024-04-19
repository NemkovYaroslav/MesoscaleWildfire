using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModuleData : MonoBehaviour
    {
        [SerializeField] [Range(0.0f, 1.0f)] private float radius = 0.1f;
        
        public float Radius => radius;
    }
}