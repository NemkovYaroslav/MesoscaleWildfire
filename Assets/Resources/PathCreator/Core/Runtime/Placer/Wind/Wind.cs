using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer.Wind
{
    [System.Serializable]
    public class Wind
    {
        [Range(0.1f, 100.0f)] public float strength = 10.0f;
        [Range(0.01f, 20.0f)] public float turbulence = 5.0f;
    
        public Vector3 direction = Vector3.forward;
        public float windDirectionRandomization = 0.5f;
        public float directionChangeSpeed = 1.0f;

        public Vector3 GetWindForceAtPosition(Vector3 position)
        {
            var randomWindDirection 
                = new Vector3(
                    Mathf.PerlinNoise(Time.time * directionChangeSpeed, 0.0f) - 0.5f, 
                    0.0f, 
                    Mathf.PerlinNoise(0.0f, Time.time * directionChangeSpeed) - 0.5f
                );
        
            var windDirection 
                = direction.normalized * (1.0f - windDirectionRandomization) + randomWindDirection * windDirectionRandomization;
        
            var windForce 
                = windDirection * (Mathf.PerlinNoise(Time.time * turbulence + position.x, Time.time * turbulence + position.y) * strength);
        
            return windForce;
        }
    }
}