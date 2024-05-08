using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Module : MonoBehaviour
    {
        public float temperature = 0.0f;

        public bool isStealer;

        /*
        public bool isSelfSupported;

        private const float IgnitionTemperature = 150.0f;
        private const float AttenuationTemperature = 450.0f;

        private const float WoodDensity = 800.0f;

        public Rigidbody rigidBody;
        private CapsuleCollider _capsuleCollider;

        private void Awake()
        {
            rigidBody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
        }

        // calculate lost mass depend on module temperature
        public float CalculateLostMass()
        {
            var surfaceArea = 2.0f * Mathf.PI * _capsuleCollider.radius * _capsuleCollider.height;

            var reactionRate = 1.0f;

            if (!isSelfSupported)
            {
                if (temperature < 150.0f)
                {
                    reactionRate = 0.0f;
                }
                if (temperature >= 150 && temperature <= 450.0f)
                {
                    var argument = (temperature - IgnitionTemperature) / (AttenuationTemperature - IgnitionTemperature);
                    reactionRate = 3.0f * Mathf.Pow(argument, 2.0f) - 2.0f * Mathf.Pow(argument, 3.0f);
                }
                if (temperature > 450.0f)
                {
                    reactionRate = 1;

                    isSelfSupported = true;
                }
            }

            var lostMass = reactionRate * surfaceArea;

            return lostMass;
        }

        public void RecalculateCharacteristics(float lostMass)
        {
            rigidBody.mass -= lostMass;
            _capsuleCollider.radius = Mathf.Sqrt(rigidBody.mass / (Mathf.PI * WoodDensity * _capsuleCollider.height));
        }
        */
    }
}