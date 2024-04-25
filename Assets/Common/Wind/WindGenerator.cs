using Unity.VisualScripting;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer.Wind
{
    public class WindGenerator : MonoBehaviour
    {
        [Header("Wind Options")]
        [SerializeField] [Range(0.0f, 100.0f)] private float masterWindStrength = 10.0f;
    
        [SerializeField] private Wind[] winds;

        private GameObject[] _trees;
    
        private void Start()
        {
            _trees = GameObject.FindGameObjectsWithTag("Tree");
        }
    
        private void FixedUpdate()
        {
            foreach (var tree in _trees)
            {
                var rigidbodies = tree.GetComponentsInChildren<Rigidbody>();
                foreach (var body in rigidbodies)
                {
                    foreach (var wind in winds)
                    {
                        var windForce = wind.GetWindForceAtPosition(body.position) * masterWindStrength;
                        body.AddForce(windForce, ForceMode.Force);
                    }
                }
            }
        }
    }
}