using System.Collections.Generic;
using Common.Wildfire;
using Resources.PathCreator.Core.Runtime.Placer;
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
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        
        
        // TREE HIERARCHY VARIABLES
        public List<Module> orderedModuleList;
        public int modulesCount;
        
        // TRANSFORMS ARRAY (for jobs)
        public TransformAccessArray transformAccessArray;
        
        
        // MODULE RENDER
        private NativeArray<Vector3>   _centersArray;
        private NativeArray<float>     _heightsArray;
        private NativeArray<float>     _radiiArray;
        private NativeArray<Matrix4x4> _matricesArray;
        
        // MODULE MATRICES BUFFER
        private ComputeBuffer _matricesBuffer;
        private int _matricesBufferStride;
        
        
        // LOCAL WILDFIRE AREA MODULE POSITIONS
        private Matrix4x4 _wildfireAreaWorldToLocal;
        private NativeArray<Vector4> _modulePositionsArray;
        
        
        // GRID SHADER
        private ComputeShader _computeShader;
        private ComputeBuffer _modulePositionsBuffer;
        private int _kernelReadData;
        private int _kernelWriteData;
        private static readonly int ModulePositions = Shader.PropertyToID("module_positions");

        private void Start()
        {
            // TREE HIERARCHY
            var trees = GameObject.FindGameObjectsWithTag("Tree");
            orderedModuleList = new List<Module>();
            foreach (var tree in trees)
            {
                var parent = tree.transform;
                foreach (Transform child in parent)
                {
                    var module = child.gameObject.GetComponent<Module>();
                    orderedModuleList.Add(module);
                }
            }
            modulesCount = orderedModuleList.Count;
            
            
            // JOBS SCHEDULE
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = orderedModuleList[i].transForm;
            }
            transformAccessArray = new TransformAccessArray(transforms);
            
            
            // RENDER MODULES
            _centersArray
                = new NativeArray<Vector3>(
                    modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _heightsArray
                = new NativeArray<float>(
                    modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _radiiArray
                = new NativeArray<float>(
                    modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _matricesArray
                = new NativeArray<Matrix4x4>(
                    modulesCount, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _matricesBufferStride = sizeof(float) * 16;
            _matricesBuffer = new ComputeBuffer(modulesCount, _matricesBufferStride);
            
            var wildfireArea = GameObject.FindWithTag("WildfireArea");
            var wildfireAreaTransform = wildfireArea.transform;
            var wildfireAreaPosition = wildfireAreaTransform.position;
            var wildfireAreaScale = wildfireAreaTransform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };

            
            // MODULES POSITIONS
            _wildfireAreaWorldToLocal = wildfireAreaTransform.worldToLocalMatrix;
            
            _modulePositionsArray = new NativeArray<Vector4>(
                modulesCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            
            _modulePositionsBuffer = new ComputeBuffer(modulesCount, sizeof(float) * 4);
            
            _computeShader = wildfireArea.GetComponent<WildfireScript>().computeShader;
            _kernelReadData  = _computeShader.FindKernel("kernel_read_data");
            _kernelWriteData = _computeShader.FindKernel("kernel_write_data");
        }

        private void FillCommonData()
        {
            if (transformAccessArray.length > 0)
            {
                for (var i = 0; i < modulesCount; i++)
                {
                    _centersArray[i] = orderedModuleList[i].capsuleCollider.center;
                    _heightsArray[i] = orderedModuleList[i].capsuleCollider.height;
                    _radiiArray[i]   = orderedModuleList[i].capsuleCollider.radius;
                }
                
                var job = new FillCommonDataJob()
                {
                    centers  = _centersArray,
                    heights  = _heightsArray,
                    radii    = _radiiArray,
                    matrices = _matricesArray,
                
                    wildfireAreaWorldToLocal    = _wildfireAreaWorldToLocal,
                    modulesWildfireAreaPosition = _modulePositionsArray,
                };
                var handle = job.Schedule(transformAccessArray);
                handle.Complete();

                if (_computeShader)
                {
                    _modulePositionsBuffer.SetData(_modulePositionsArray);
                    _computeShader.SetBuffer(_kernelReadData, ModulePositions, _modulePositionsBuffer);
                    _computeShader.SetBuffer(_kernelWriteData, ModulePositions, _modulePositionsBuffer);
                }
            }
        }
        
        private void RenderModules()
        {
            _matricesBuffer.SetData(_matricesArray);
            _renderParams.matProps.SetBuffer(Matrices, _matricesBuffer);
            Graphics.RenderMeshPrimitives(_renderParams, renderMesh, 0, modulesCount);
        }

        private void Update()
        {
            FillCommonData();
            
            RenderModules();
        }
                
        private void CleanupComputeBuffer()
        {
            if (_matricesBuffer != null)
            {
                _matricesBuffer.Release();
            }
            _matricesBuffer = null;
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();
            
            transformAccessArray.Dispose();
            
            _centersArray.Dispose();
            _heightsArray.Dispose();
            _radiiArray.Dispose();
            _matricesArray.Dispose();

            _modulePositionsArray.Dispose();
        }
    }
}