using UnityEngine;
using UnityEngine.VFX;

namespace WildfireModel.Wildfire
{
    public class Module : MonoBehaviour
    {
        // constants
        private const int WoodDensity                = 800;
        private const float IgnitionTemperature      = 0.15f;
        private const float AttenuationTemperature   = 0.45f;
        private const float StopCombustionMassFactor = 0.1f;
        private const float CharIsolatedFactor       = 0.01f;
        
        private Wildfire _wildfire;

        [HideInInspector] public GameObject      cachedGameObject;
        [HideInInspector] public Transform       cachedTransform;
        [HideInInspector] public Rigidbody       cachedRigidbody;
        [HideInInspector] public CapsuleCollider cachedCapsuleCollider;
        [HideInInspector] public FixedJoint      cachedFixedJoint;
        [HideInInspector] public VisualEffect    cachedVisualEffect;
        
        public Module cachedPreviousModule;
        public Module cachedNextModule;
        
        public float temperature;
        
        public float stopCombustionMass;

        public bool isTrunk;

        [HideInInspector] public bool isIsolatedByCoal;

        public bool isBurned;
        
        private void Awake()
        {
            _wildfire = GameObject.FindWithTag("WildfireArea").GetComponent<Wildfire>();

            cachedGameObject      = gameObject;
            cachedTransform       = GetComponent<Transform>();
            cachedRigidbody       = GetComponent<Rigidbody>();
            cachedCapsuleCollider = GetComponent<CapsuleCollider>();
            cachedFixedJoint      = GetComponent<FixedJoint>();

            // get neighbour from joint
            if (cachedFixedJoint.connectedBody.TryGetComponent(out Module module))
            {
                cachedPreviousModule = module;
            }


            var childCount = cachedTransform.parent.childCount;
            var siblingIndex = cachedTransform.GetSiblingIndex();
            if (siblingIndex < childCount - 1)
            {
                cachedNextModule = cachedTransform.parent.GetChild(siblingIndex + 1).GetComponent<Module>();
            }
            
            
            stopCombustionMass = cachedRigidbody.mass * StopCombustionMassFactor;
            
            cachedVisualEffect = GetComponent<VisualEffect>();
            cachedVisualEffect.SetVector3("direction", _wildfire.WindDirection * _wildfire.WindIntensity);
            cachedVisualEffect.SetFloat("cone radius", cachedCapsuleCollider.radius);
            cachedVisualEffect.SetFloat("cone height", cachedCapsuleCollider.height);
        }
        
        public float CalculateLostMass()
        {
            var reactionRate = 1.0f;
            
            if (temperature is > IgnitionTemperature and < AttenuationTemperature)
            {
                var argument = (temperature - IgnitionTemperature) / (AttenuationTemperature - IgnitionTemperature);
                reactionRate = 3.0f * Mathf.Pow(argument, 2.0f) - 2.0f * Mathf.Pow(argument, 3.0f);
            }
            if (temperature > AttenuationTemperature)
            {
                reactionRate = 1;
            }
            
            var pyrolysisFrontArea = 2.0f * Mathf.PI * cachedCapsuleCollider.radius * cachedCapsuleCollider.height;
            
            var lostMass = reactionRate * pyrolysisFrontArea * CharIsolatedFactor;

            return lostMass;
        }

        public void RecalculateCharacteristics(float lostMass)
        {
            var mass = cachedRigidbody.mass - lostMass;
            cachedRigidbody.mass = mass;
            cachedCapsuleCollider.radius = Mathf.Sqrt(mass / (Mathf.PI * WoodDensity * cachedCapsuleCollider.height));
        }
    }
}