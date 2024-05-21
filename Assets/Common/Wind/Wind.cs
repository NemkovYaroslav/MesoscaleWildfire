using UnityEngine;

namespace Common.Wind
{
    [System.Serializable]
    public class Wind
    {
        [Range(0.0f, 30.0f)] public float intensity = 0.0f;
        //[Range(0.01f, 20.0f)] public float turbulence = 5.0f;
    
        public Vector3 direction = Vector3.forward;
        //public float randomization = 0.5f;
        //public float changeSpeed = 1.0f;
        
        /*
        public Vector3 GetWindForceAtPosition()
        {
            var randomWindDirection 
                = new Vector3(
                    Mathf.PerlinNoise(Time.time * changeSpeed, 0.0f) - 0.5f, 
                    0.0f, 
                    Mathf.PerlinNoise(0.0f, Time.time * changeSpeed) - 0.5f
                );
        
            var windDirection 
                = direction.normalized * (1.0f - randomization) + randomWindDirection * randomization;
        
            var windForce 
                = windDirection * (Mathf.PerlinNoise(Time.time * turbulence, Time.time * turbulence) * strength);
        
            return windForce;
        }
        */
    }
}