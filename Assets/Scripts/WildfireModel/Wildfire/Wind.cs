using UnityEngine;

namespace WildfireModel.Wildfire
{
    [System.Serializable]
    public class Wind
    {
        [Range(0.0f, 15.0f)] public float intensity ;
    
        public Vector3 direction = Vector3.forward;
    }
}