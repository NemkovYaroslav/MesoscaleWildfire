using UnityEngine;

namespace Common.Wildfire
{
    public class ModuleStats : MonoBehaviour
    {
        public CapsuleCollider capsuleCollider;
        
        public FixedJoint fixedJoint;
        
        public bool isBurning = false;

        public float woodHeatCapacity;

        public float woodStrength;

        private void Start()
        {
            if (TryGetComponent(out FixedJoint fj))
            {
                fixedJoint = fj;
            }

            if (TryGetComponent(out CapsuleCollider cc))
            {
                capsuleCollider = cc;
            }

            woodHeatCapacity = 1.0f;
            woodStrength = capsuleCollider.radius * 200.0f;
        }
    }
}
