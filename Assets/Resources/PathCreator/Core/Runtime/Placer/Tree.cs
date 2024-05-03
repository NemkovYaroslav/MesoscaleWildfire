using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Tree : MonoBehaviour
    {
        private bool _isBurning;
        
        public void ActivateColliders()
        {
            if (!_isBurning)
            {
                var colliders = transform.GetComponentsInChildren<CapsuleCollider>();
                foreach (var capsule in colliders)
                {
                    capsule.enabled = true;
                }
                _isBurning = true;
            }
        }
    }
}