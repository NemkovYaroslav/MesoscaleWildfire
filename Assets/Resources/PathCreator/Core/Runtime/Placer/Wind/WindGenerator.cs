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
            _trees = GameObject.FindGameObjectsWithTag("Module");
        }
    
        private void FixedUpdate()
        {
            foreach (var tree in _trees)
            {
                foreach (Transform branch in tree.transform)
                {
                    if (branch.gameObject.TryGetComponent(out Rigidbody rb))
                    {
                        foreach (var wind in winds)
                        {
                            var windForce = wind.GetWindForceAtPosition(rb.position) * masterWindStrength;
                            if (branch.TryGetComponent(out FixedJoint joint))
                            {
                                rb.AddForce(windForce, ForceMode.Force);
                        
                                ///*
                                var position = rb.position;
                                var direction = windForce.normalized;
                                Debug.DrawLine(position - direction, position, Color.green, 0.1f);
                                //*/
                            }
                            /*
                            else
                            {
                                rb.drag = 1000.0f;
                                rb.angularDrag = 1000.0f;
                            }
                            */
                        }
                    }
                }
            }
        }
    }
}