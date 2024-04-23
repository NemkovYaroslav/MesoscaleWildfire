using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private float range = 5.0f;
        
        [SerializeField] private Material material;
        
        private ComputeBuffer _transformsBuffer;
        
        [SerializeField] private Mesh mesh;

        private Bounds _bounds;

        private int _size;

        private List<GameObject> _modules;
        
        private static readonly int Transforms = Shader.PropertyToID("transforms");

        private void Setup()
        {
            _bounds = new Bounds(transform.position, Vector3.one * range);

            _size = sizeof(float) * 16;
            
            _modules = GameObject.FindGameObjectsWithTag("Module").ToList();
        }

        private void CalculateTransforms()
        {
            var properties = new Matrix4x4[_modules.Count];
            for (var i = 0; i < _modules.Count; i++)
            {
                var capsule = _modules[i].GetComponent<CapsuleCollider>();

                var capsuleTransform = capsule.transform;
                var position = capsuleTransform.position + capsuleTransform.TransformVector(capsule.center);
                var rotation = capsuleTransform.rotation * Quaternion.LookRotation(Vector3.up);
                var scale = new Vector3(capsule.radius * 2.0f, capsule.height / 2.0f, capsule.radius * 2.0f);

                properties[i] = Matrix4x4.TRS(position, rotation, scale);
            }
            
            _transformsBuffer = new ComputeBuffer(_modules.Count, _size);
            _transformsBuffer.SetData(properties);
            material.SetBuffer(Transforms, _transformsBuffer);
        }
        
        private void Start()
        {
            Setup();
        }

        private void Update()
        {
            CalculateTransforms();
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, _bounds, _modules.Count);
        }
        
        private void OnDestroy() 
        {
            if (_transformsBuffer != null) 
            {
                _transformsBuffer.Release();
            }
            _transformsBuffer = null;
        }
    }
}