using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModulePrototypeData : MonoBehaviour
    {
        [Range(0.0f, 1.0f)] public float step;
        public float radius;
        public float previousRadius;
    }
}