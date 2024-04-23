using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private float range = 5.0f;
        
        [SerializeField] private Material material;
        
        private ComputeBuffer _meshPropertiesBuffer;
        
        [SerializeField] private Mesh mesh;

        private Bounds _bounds;

        [SerializeField] private Transform treeTransform;

        private int _modulesCount;
        private static readonly int Properties = Shader.PropertyToID("properties");

        private struct MeshProperties
        {
            public Matrix4x4 mat;

            public static int Size()
            {
                return sizeof(float) * 4 * 4;
            }
        }

        private void Setup()
        {
            _bounds = new Bounds(transform.position, Vector3.one * range);
            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            var capsules = treeTransform.GetComponentsInChildren<CapsuleCollider>();

            _modulesCount = capsules.Length;
            
            var properties = new MeshProperties[capsules.Length];
            for (var i = 0; i < _modulesCount; i++)
            {
                var position = capsules[i].transform.position + capsules[i].transform.TransformVector(capsules[i].center);

                var rotation = capsules[i].transform.rotation * Quaternion.LookRotation(Vector3.up);
                
                var scale = new Vector3(capsules[i].radius * 2.0f, capsules[i].height / 2.0f, capsules[i].radius * 2.0f);

                var props = new MeshProperties
                {
                    mat = Matrix4x4.TRS(position, rotation, scale)
                };
                properties[i] = props;
            }
            
            _meshPropertiesBuffer = new ComputeBuffer(_modulesCount, MeshProperties.Size());
            _meshPropertiesBuffer.SetData(properties);
            material.SetBuffer(Properties, _meshPropertiesBuffer);
        }
        
        private void Start()
        {
            Setup();
        }

        private void Update()
        {
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, _bounds, _modulesCount);
        }
        
        private void OnDisable() {
            if (_meshPropertiesBuffer != null) 
            {
                _meshPropertiesBuffer.Release();
            }
            _meshPropertiesBuffer = null;
        }
    }
}