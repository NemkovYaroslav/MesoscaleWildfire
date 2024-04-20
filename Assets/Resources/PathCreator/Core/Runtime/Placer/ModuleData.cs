using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModuleData : MonoBehaviour
    {
        [SerializeField] [Range(0.0f, 1.0f)] private float radius;

        public float Radius
        {
            get => radius;
            set => radius = value;
        }
    }
}