using System;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Module : MonoBehaviour
    {
        public float temperature = 0.0f;
        
        private const float IgnitionTemperature = 0.15f;
        private const float AttenuationTemperature = 0.45f;

        public const float WoodDensity = 800.0f;

        public bool isSelfSupported;

        public float stopCombustionMass = 0.0f;

        public Rigidbody rigidBody;
        public CapsuleCollider capsuleCollider;

        private void Awake()
        {
            rigidBody = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            stopCombustionMass = rigidBody.mass * 0.1f;
        }

        // calculate lost mass depend on module temperature
        public float CalculateLostMass()
        {
            var surfaceArea = 2.0f * Mathf.PI * capsuleCollider.radius * capsuleCollider.height;

            var reactionRate = 1.0f;
            
            if (temperature < IgnitionTemperature)
            {
                reactionRate = 0.0f;
            }
            if (temperature >= IgnitionTemperature && temperature <= AttenuationTemperature)
            {
                var argument = (temperature - IgnitionTemperature) / (AttenuationTemperature - IgnitionTemperature);
                reactionRate = 3.0f * Mathf.Pow(argument, 2.0f) - 2.0f * Mathf.Pow(argument, 3.0f);
            }
            if (temperature > AttenuationTemperature)
            {
                reactionRate = 1;
            }

            const float thickness = 0.01f;
            
            var lostMass = reactionRate * surfaceArea * thickness * 2.0f;

            return lostMass;
        }

        public void RecalculateCharacteristics(float lostMass)
        {
            rigidBody.mass -= lostMass;
            
            capsuleCollider.radius = Mathf.Sqrt(rigidBody.mass / (Mathf.PI * WoodDensity * capsuleCollider.height));
        }
    }
}