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
        
        public CapsuleCollider[] colliders;
        
        public int modulesCount;
        
        private ComputeBuffer _transformsBuffer;
        private int _transformsBufferStride;
        
        private RenderParams _renderParams;
        
        private static readonly int Transforms = Shader.PropertyToID("transforms");
        
        // common fields
        public TransformAccessArray transformsArray; // const
        public NativeArray<Vector3> centers;         // const
        public NativeArray<float> heights;           // const

        private void Awake()
        {
            var modules = GameObject.FindGameObjectsWithTag("Module");

            modulesCount = modules.Length;
            
            colliders = new CapsuleCollider[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                colliders[i] = modules[i].GetComponent<CapsuleCollider>();
            }
        
            // fill transform array for jobs
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = modules[i].GetComponent<Transform>();
            }
            transformsArray = new TransformAccessArray(transforms);
        
            centers = new NativeArray<Vector3>(
                modulesCount, 
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            heights = new NativeArray<float>(
                modulesCount, 
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            for (var i = 0; i < modulesCount; i++)
            {
                centers[i] = colliders[i].center;
                heights[i] = colliders[i].height;
            }
            
            _transformsBufferStride = sizeof(float) * 16;
        
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
                _transformsBuffer.Release();
            }
            _transformsBuffer = null;
        }

        private void RenderModules()
        {
            // input
            var radii = new NativeArray<float>(
                modulesCount, 
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory
            );
            for (var i = 0; i < modulesCount; i++)
            {
                radii[i] = colliders[i].radius;
            }
            
            // output
            var matrices = new NativeArray<Matrix4x4>(
                modulesCount, 
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            
            // fill native array with modules world positions data
            var job = new ModuleRenderJob()
            {
                // unchangeable
                centers = centers,
                heights = heights,
                
                // changeable
                radii = radii,
                
                // result
                matrices = matrices
            };
            var handle = job.Schedule(transformsArray);
            handle.Complete();
            
            CleanupComputeBuffer();
            _transformsBuffer = new ComputeBuffer(modulesCount, _transformsBufferStride);
            _transformsBuffer.SetData(matrices);
            
            // send the modules world positions data to shader
            _renderParams.matProps.SetBuffer(Transforms, _transformsBuffer);
            // should be called every frame
            Graphics.RenderMeshPrimitives(_renderParams, mesh, 0, modulesCount);
            
            radii.Dispose();
            matrices.Dispose();
        }

        private void Update()
        {
            RenderModules();
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();

            transformsArray.Dispose();
            heights.Dispose();
            centers.Dispose();
        }
    }
}