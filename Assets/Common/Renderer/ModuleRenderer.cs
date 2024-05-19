using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Common.Renderer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private Material renderMaterial;
        [SerializeField] private Mesh renderMesh;
        
        private RenderParams _renderParams;
        
        private CapsuleCollider[] _colliders;
        
        public int modulesCount;
        public TransformAccessArray transformsArray;
        public NativeArray<Vector3> centers;
        public NativeArray<float> heights;
        
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        private ComputeBuffer _renderMatricesBuffer;
        private int _matricesBufferStride;

        private void Awake()
        {
            var modules = GameObject.FindGameObjectsWithTag("Module");
            modulesCount = modules.Length;
            
            _colliders = new CapsuleCollider[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                _colliders[i] = modules[i].GetComponent<CapsuleCollider>();
            }
            
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = modules[i].transform;
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
                centers[i] = _colliders[i].center;
                heights[i] = _colliders[i].height;
            }
            
            _matricesBufferStride = sizeof(float) * 16;

            var wildfireArea = GameObject.FindWithTag("WildfireArea");
            var wildfireAreaPosition = wildfireArea.transform.position;
            var wildfireAreaScale = wildfireArea.transform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };
        }

        private void RenderModules()
        {
            var radii 
                = new NativeArray<float>(
                    modulesCount, 
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory
                );
            
            for (var i = 0; i < modulesCount; i++)
            {
                radii[i] = _colliders[i].radius;
            }
            
            var matrices 
                = new NativeArray<Matrix4x4>(
                    modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            var job = new ConstructModuleMatricesJob()
            {
                centers = centers,
                heights = heights,
                radii = radii,
                matrices = matrices
            };
            var handle = job.Schedule(transformsArray);
            handle.Complete();
            
            if (_renderMatricesBuffer == null)
            {
                _renderMatricesBuffer = new ComputeBuffer(modulesCount, _matricesBufferStride);
            }
            _renderMatricesBuffer.SetData(matrices);
            
            _renderParams.matProps.SetBuffer(Matrices, _renderMatricesBuffer);
            Graphics.RenderMeshPrimitives(_renderParams, renderMesh, 0, modulesCount);
            
            radii.Dispose();
            matrices.Dispose();
        }

        private void Update()
        {
            RenderModules();
        }
                
        private void CleanupComputeBuffer()
        {
            if (_renderMatricesBuffer != null)
            {
                _renderMatricesBuffer.Release();
            }
            _renderMatricesBuffer = null;
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