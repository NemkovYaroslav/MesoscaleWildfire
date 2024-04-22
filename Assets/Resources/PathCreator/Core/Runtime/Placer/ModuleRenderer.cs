using System.Collections.Generic;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private Material materialToInstantiate;
        
        [SerializeField] private Mesh meshToInstantiate;

        private ComputeBuffer _modulesBuffer;

        private List<List<Matrix4x4>> _batches = new List<List<Matrix4x4>>();
        
        private void OnDrawGizmos()

        {
            UnityEditor.Handles.color = Color.yellow;
            var joints = transform.GetComponentsInChildren<FixedJoint>();
            foreach (var joint in joints)
            {
                var current = joint.gameObject;
                var previous = joint.connectedBody.gameObject;
                UnityEditor.Handles.DrawLine(current.transform.position, previous.transform.position);
            }
        }

        private void Start()
        {
            _batches.Add(new List<Matrix4x4>());
            var addedMatrices = 0;
            var colliders = gameObject.GetComponentsInChildren<CapsuleCollider>();
            foreach (var capsule in colliders)
            {
                if (addedMatrices < 1000)
                {
                    var capsuleTransform = capsule.gameObject.transform;
                    var position = capsuleTransform.position + capsuleTransform.TransformVector(capsule.center);
                    var rotation = capsuleTransform.rotation;
                    var radius = capsule.radius;
                    var scale = new Vector3(radius * 2.0f, capsule.height / 2.0f, radius * 2.0f);
                    _batches[^1].Add(Matrix4x4.TRS(position, rotation, scale));
                    addedMatrices++;
                }
                else
                {
                    _batches.Add(new List<Matrix4x4>());
                    addedMatrices = 0;
                }
            }
        }

        private void RenderBatches()
        {
            foreach (var batch in _batches)
            {
                Graphics.DrawMeshInstanced(meshToInstantiate, 0, materialToInstantiate, batch);
            }
        }
        
        private void Update()
        {
            RenderBatches();
        }
    }
}