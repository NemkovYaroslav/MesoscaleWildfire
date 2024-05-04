using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Renderer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private float range = 10.0f;
        
        [SerializeField] private Material material;
        
        [SerializeField] private Mesh mesh;
        
        private int _size;

        private CapsuleCollider[] _colliders;
        
        private ComputeBuffer _transformsBuffer;
        
        private static readonly int Transforms = Shader.PropertyToID("transforms");

        private int _modulesCount;
        
        private TransformAccessArray _transformAccessArray;
        private NativeArray<Vector3> _centers;
        private NativeArray<float> _heights;
        private NativeArray<float> _radii;
        private NativeArray<Matrix4x4> _matrices;

        private RenderParams _renderParams;

        private void Start()
        {
            var modules = GameObject.FindGameObjectsWithTag("Module");
            _modulesCount = modules.Length;
            
            _colliders = new CapsuleCollider[_modulesCount];
            for (var i = 0; i < _modulesCount; i++)
            {
                _colliders[i] = modules[i].GetComponent<CapsuleCollider>();
            }
            
            
            // job
            var transforms = new Transform[_modulesCount];
            for (var i = 0; i < _modulesCount; i++)
            {
                transforms[i] = modules[i].GetComponent<Transform>();
            }
            _transformAccessArray = new TransformAccessArray(transforms);
            
            _centers = new NativeArray<Vector3>(_modulesCount, Allocator.Persistent);
            _heights = new NativeArray<float>(_modulesCount, Allocator.Persistent);
            for (var i = 0; i < _modulesCount; i++)
            {
                _centers[i] = _colliders[i].center;
                _heights[i] = _colliders[i].height;
            }
            
            _matrices = new NativeArray<Matrix4x4>(_modulesCount, Allocator.Persistent);
            
            _size = sizeof(float) * 16;
            
            // render
            _renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * range),
                matProps = new MaterialPropertyBlock()
            };
        }
        
        private void CleanupComputeBuffer()
        {
            if (_transformsBuffer != null)
            {
                _transformsBuffer.Dispose();
            }
            _transformsBuffer = null;
        }

        private void CalculateTransforms()
        {
            _radii = new NativeArray<float>(_modulesCount, Allocator.TempJob);
            for (var i = 0; i < _modulesCount; i++)
            {
                _radii[i] = _colliders[i].radius;
            }
            
            var job = new ModuleRenderJob()
            {
                centers = _centers,
                heights = _heights,
                radii = _radii,
                
                matrices = _matrices
            };
            var handle = job.Schedule(_transformAccessArray);
            handle.Complete();
            
            _radii.Dispose();

            CleanupComputeBuffer();
            
            _transformsBuffer = new ComputeBuffer(_modulesCount, _size);
            _transformsBuffer.SetData(_matrices);
            
            _renderParams.matProps.SetBuffer(Transforms, _transformsBuffer);
            Graphics.RenderMeshPrimitives(_renderParams, mesh, 0, _modulesCount);
        }

        private void Update()
        {
            CalculateTransforms();
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();

            _transformAccessArray.Dispose();
            
            _centers.Dispose();
            _heights.Dispose();
            
            _matrices.Dispose();
        }
    }
}