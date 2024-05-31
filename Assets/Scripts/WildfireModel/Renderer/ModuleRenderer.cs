using System.Collections.Generic;
using TreeModel.Runtime.Placer;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace WildfireModel.Renderer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [Header("Module Render Settings")]
        [SerializeField] private Material renderMaterial;
        [SerializeField] private Mesh     renderMesh;

        private Wildfire.Wildfire _wildfire;
        
        private RenderParams _renderParams;
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        
        
        // TREE HIERARCHY VARIABLES
        [HideInInspector] public List<Module> orderedModuleList;
        [HideInInspector] public int modulesCount;
        
        
        // TRANSFORMS ARRAY (for jobs)
        private TransformAccessArray _transformAccessArray;
        
        
        // MODULE RENDER
        private NativeArray<Vector3>   _centersArray;
        private NativeArray<float>     _heightsArray;
        private NativeArray<float>     _radiiArray;
        private NativeArray<Matrix4x4> _matricesArray;
        private ComputeBuffer          _matricesBuffer;
        
        
        // BURNED TREE
        private NativeArray<float> _isolatedTreeArray;
        private ComputeBuffer      _isolatedTreeBuffer;
        
        
        // LOCAL WILDFIRE AREA MODULE POSITIONS
        private Matrix4x4            _wildfireAreaWorldToLocal;
        private NativeArray<Vector4> _modulePositionsArray;
        
        
        // MODULE POSITIONS
        private ComputeBuffer _modulePositionsBuffer;
        private int _kernelReadData;
        private int _kernelWriteData;
        private static readonly int ModulePositions = Shader.PropertyToID("module_positions");
        private static readonly int IsolatedTrees   = Shader.PropertyToID("burning_trees");

        private void Start()
        {
            _wildfire = GameObject.FindWithTag("WildfireArea").GetComponent<Wildfire.Wildfire>();
            
            _kernelReadData  = _wildfire.GridComputeShader.FindKernel("kernel_read_data");
            _kernelWriteData = _wildfire.GridComputeShader.FindKernel("kernel_write_data");
            
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
            
            _matricesBuffer 
                = new ComputeBuffer(
                    modulesCount, 
                    sizeof(float) * 16
                );
            
            var wildfireAreaTransform       = _wildfire.transform;
            var wildfireAreaPosition = wildfireAreaTransform.position;
            var wildfireAreaScale    = wildfireAreaTransform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };

            
            // JOBS SCHEDULE
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = orderedModuleList[i].cachedTransform;
            }
            _transformAccessArray = new TransformAccessArray(transforms);
            
            
            // MODULES POSITIONS
            _modulePositionsArray 
                = new NativeArray<Vector4>(
                    modulesCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _modulePositionsBuffer 
                = new ComputeBuffer(
                    modulesCount, 
                    sizeof(float) * 4
                );
            
            
            // BURNED TREES
            _isolatedTreeArray
                = new NativeArray<float>(
                    modulesCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _isolatedTreeBuffer 
                = new ComputeBuffer(
                    modulesCount, 
                    sizeof(float)
                );
            
            _wildfireAreaWorldToLocal = wildfireAreaTransform.worldToLocalMatrix;
        }

        private void FillCommonData()
        {
            if (_transformAccessArray.length > 0)
            {
                for (var i = 0; i < modulesCount; i++)
                {
                    _centersArray[i] = orderedModuleList[i].cachedCapsuleCollider.center;
                    _heightsArray[i] = orderedModuleList[i].cachedCapsuleCollider.height;
                    _radiiArray[i]   = orderedModuleList[i].cachedCapsuleCollider.radius;
                    
                    // check if tree is start burning
                    _isolatedTreeArray[i] = orderedModuleList[i].isIsolatedByCoal ? 1.0f : 0.0f;
                }

                var job = new FillMatricesAndPositionsDataJob()
                {
                    centers = _centersArray,
                    heights = _heightsArray,
                    radii = _radiiArray,
                    matrices = _matricesArray,

                    wildfireAreaWorldToLocal = _wildfireAreaWorldToLocal,
                    modulesWildfireAreaPosition = _modulePositionsArray,
                };
                var handle = job.Schedule(_transformAccessArray);
                handle.Complete();

                _modulePositionsBuffer.SetData(_modulePositionsArray);
                _wildfire.GridComputeShader.SetBuffer(_kernelReadData, ModulePositions, _modulePositionsBuffer);
                _wildfire.GridComputeShader.SetBuffer(_kernelWriteData, ModulePositions, _modulePositionsBuffer);
            }
        }
        
        private void RenderModules()
        {
            _matricesBuffer.SetData(_matricesArray);
            _renderParams.matProps.SetBuffer(Matrices, _matricesBuffer);
            
            _isolatedTreeBuffer.SetData(_isolatedTreeArray);
            _renderParams.matProps.SetBuffer(IsolatedTrees, _isolatedTreeBuffer);
            
            Graphics.RenderMeshPrimitives(_renderParams, renderMesh, 0, modulesCount);
        }

        private void Update()
        {
            Profiler.BeginSample("FillMatricesAndRender");
            FillCommonData();
            RenderModules();
            Profiler.EndSample();
        }
                
        private void CleanupComputeBuffer()
        {
            if (_matricesBuffer != null)
            {
                _matricesBuffer.Release();
            }
            _matricesBuffer = null;
                
            if (_modulePositionsBuffer != null)
            {
                _modulePositionsBuffer.Release();
            }
            _modulePositionsBuffer = null;
            
            if (_isolatedTreeBuffer != null)
            {
                _isolatedTreeBuffer.Release();
            }
            _isolatedTreeBuffer = null; 
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();
            
            _transformAccessArray.Dispose();
            
            _centersArray.Dispose();
            _heightsArray.Dispose();
            _radiiArray.Dispose();
            _matricesArray.Dispose();

            _isolatedTreeArray.Dispose();

            _modulePositionsArray.Dispose();
        }
    }
}