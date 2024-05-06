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

        public int modulesCount;
        
        private CapsuleCollider[] _colliders;
        
        private ComputeBuffer _transformsBuffer;
        private int _transformsBufferSize;
        
        private RenderParams _renderParams;
        
        private static readonly int Transforms = Shader.PropertyToID("transforms");
        
        // job fields
        public TransformAccessArray transformsArray;
        private NativeArray<Matrix4x4> _matrices;
        public NativeArray<Vector3> centers;
        private NativeArray<float> _heights;

        private void Start()
        {
            var modules = GameObject.FindGameObjectsWithTag("Module");
            modulesCount = modules.Length;
            
            _colliders = new CapsuleCollider[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                _colliders[i] = modules[i].GetComponent<CapsuleCollider>();
            }
            
            // fill transform array for jobs
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = modules[i].GetComponent<Transform>();
            }
            transformsArray = new TransformAccessArray(transforms);
            _matrices = new NativeArray<Matrix4x4>(modulesCount, Allocator.Persistent);
            centers = new NativeArray<Vector3>(modulesCount, Allocator.Persistent);
            _heights = new NativeArray<float>(modulesCount, Allocator.Persistent);
            for (var i = 0; i < modulesCount; i++)
            {
                centers[i] = _colliders[i].center;
                _heights[i] = _colliders[i].height;
            }
            
            _transformsBufferSize = sizeof(float) * 16;
            
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
            // input
            var radii = new NativeArray<float>(modulesCount, Allocator.TempJob);
            for (var i = 0; i < modulesCount; i++)
            {
                radii[i] = _colliders[i].radius;
            }
            
            // job process
            var job = new ModuleRenderJob()
            {
                centers = centers,
                heights = _heights,
                radii = radii,
                
                matrices = _matrices
            };
            var handle = job.Schedule(transformsArray);
            handle.Complete();
            
            radii.Dispose();
            
            CleanupComputeBuffer();
            _transformsBuffer = new ComputeBuffer(modulesCount, _transformsBufferSize);
            _transformsBuffer.SetData(_matrices);
            
            _renderParams.matProps.SetBuffer(Transforms, _transformsBuffer);
            Graphics.RenderMeshPrimitives(_renderParams, mesh, 0, modulesCount);
        }

        private void Update()
        {
            CalculateTransforms();
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();

            transformsArray.Dispose();
            _matrices.Dispose();
            centers.Dispose();
            _heights.Dispose();
        }
    }
}