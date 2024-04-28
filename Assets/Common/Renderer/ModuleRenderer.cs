using UnityEngine;

namespace Common.Renderer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private float range = 5.0f;
        
        [SerializeField] private Material material;
        
        private ComputeBuffer _transformsBuffer;
        
        [SerializeField] private Mesh mesh;

        private Bounds _bounds;

        private int _size;

        //private List<GameObject> _modules;
        
        private static readonly int Transforms = Shader.PropertyToID("transforms");

        private void Setup()
        {
            _bounds = new Bounds(transform.position, Vector3.one * range);

            _size = sizeof(float) * 16;
            
            //_modules = GameObject.FindGameObjectsWithTag("Module").ToList();
        }

        private void CleanupComputeBuffer()
        {
            if (_transformsBuffer != null) 
            {
                _transformsBuffer.Release();
            }
            _transformsBuffer = null;
        }

        private void CalculateTransforms()
        {
            var modules = GameObject.FindGameObjectsWithTag("Module");
            var properties = new Matrix4x4[modules.Length];
            for (var i = 0; i < modules.Length; i++)
            {
                var capsule = modules[i].GetComponent<CapsuleCollider>();

                var capsuleTransform = capsule.transform;
                var position = capsuleTransform.position + capsuleTransform.TransformVector(capsule.center);
                var rotation = capsuleTransform.rotation * Quaternion.LookRotation(Vector3.up);
                var scale = new Vector3(capsule.radius * 2.0f, capsule.height / 2.0f, capsule.radius * 2.0f);

                properties[i] = Matrix4x4.TRS(position, rotation, scale);
            }

            CleanupComputeBuffer();
            
            _transformsBuffer = new ComputeBuffer(modules.Length, _size);
            _transformsBuffer.SetData(properties);
            material.SetBuffer(Transforms, _transformsBuffer);
            
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, _bounds, modules.Length);
        }
        
        private void Start()
        {
            Setup();
        }

        private void Update()
        {
            CalculateTransforms();
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();
        }
    }
}