using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Module : MonoBehaviour
    {
        public float temperature;

        public bool isBurning;

        //private const float IgnitionTemperature = 150.0f;
        //private const float AttenuationTemperature = 450.0f;
        //private const float WoodDensity = 800.0f;
        
        //private Rigidbody _rigidbody;
        //private CapsuleCollider _capsuleCollider;

        private void Awake()
        {
            //_rigidbody = GetComponent<Rigidbody>();
            //_capsuleCollider = GetComponent<CapsuleCollider>();
        }

        /*
        private void FixedUpdate()
        {
            if (temperature > IgnitionTemperature && temperature < AttenuationTemperature && _rigidbody.mass > 0.0f)
            {
                var reactionRate = CalculateReactionRate();
                var surfaceArea = 2.0f * Mathf.PI * _capsuleCollider.radius * _capsuleCollider.height;

                var looseMass = reactionRate * surfaceArea;
                
                _rigidbody.mass -= looseMass;

                _capsuleCollider.radius =
                    Mathf.Sqrt(_rigidbody.mass / (WoodDensity * Mathf.PI * _capsuleCollider.height));

                temperature += (1.2f) * looseMass;

                Debug.Log(looseMass);
            }
        }
        */

        /*
        private float CalculateReactionRate()
        {
            var argument = (temperature - IgnitionTemperature) / (AttenuationTemperature - IgnitionTemperature);
            var reactionRate = 3.0f * Mathf.Pow(argument, 2.0f) - 2.0f * Mathf.Pow(argument, 3.0f);
            return reactionRate;
        }
        */
    }
}