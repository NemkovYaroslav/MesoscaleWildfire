using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using WildfireModel.Wildfire;

namespace WildfireModel.Renderer
{
    public class ModuleRenderer : MonoBehaviour
    {
        [Header("Module Render Settings")]
        [SerializeField] private Material renderMaterial;
        [SerializeField] private Mesh renderMesh;

        private Wildfire.Wildfire _wildfire;
        
        private RenderParams _renderParams;
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        
        
        // TREE HIERARCHY VARIABLES
        [HideInInspector] public List<Module> orderedModuleList;
        //[HideInInspector] public int modulesCount;
        
        
        // TRANSFORMS ARRAY (for jobs)
        public TransformAccessArray transformAccessArray;
        
        
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
        private Matrix4x4 _wildfireAreaWorldToLocal;
        
        
        // MODULE POSITIONS
        private NativeArray<Vector4> _modulePositionsArray;
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
            
            // JOBS SCHEDULE
            var transforms = new Transform[orderedModuleList.Count];
            for (var i = 0; i < orderedModuleList.Count; i++)
            {
                transforms[i] = orderedModuleList[i].cachedTransform;
            }
            transformAccessArray = new TransformAccessArray(transforms);
            
            //modulesCount = orderedModuleList.Count;
            
            
            // RENDER MODULES
            _centersArray
                = new NativeArray<Vector3>(
                    transformAccessArray.length, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _heightsArray
                = new NativeArray<float>(
                    transformAccessArray.length, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _radiiArray
                = new NativeArray<float>(
                    transformAccessArray.length, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _matricesArray
                = new NativeArray<Matrix4x4>(
                    transformAccessArray.length, 
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            
            _matricesBuffer 
                = new ComputeBuffer(
                    transformAccessArray.length, 
                    sizeof(float) * 16
                );
            
            var wildfireAreaTransform = _wildfire.transform;
            var wildfireAreaPosition = wildfireAreaTransform.position;
            var wildfireAreaScale = wildfireAreaTransform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };
            _wildfireAreaWorldToLocal = wildfireAreaTransform.worldToLocalMatrix;
            
            
            // BURNED TREES
            _isolatedTreeArray
                = new NativeArray<float>(
                    transformAccessArray.length,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _isolatedTreeBuffer 
                = new ComputeBuffer(
                    transformAccessArray.length, 
                    sizeof(float)
                );
            
            
            // MODULES POSITIONS BUFFER
            _modulePositionsArray 
                = new NativeArray<Vector4>(
                    transformAccessArray.length,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _modulePositionsBuffer 
                = new ComputeBuffer(
                    transformAccessArray.length, 
                    sizeof(float) * 4
                );
        }

        private void FillCommonData()
        {
            if (transformAccessArray.length > 0)
            {
                for (var i = 0; i < transformAccessArray.length; i++)
                {
                    if (orderedModuleList[i])
                    {
                        _centersArray[i] = orderedModuleList[i].cachedCapsuleCollider.center;
                        _heightsArray[i] = orderedModuleList[i].cachedCapsuleCollider.height;
                        _radiiArray[i]   = orderedModuleList[i].cachedCapsuleCollider.radius;
                    
                        // check if tree is start burning
                        _isolatedTreeArray[i] = orderedModuleList[i].isIsolatedByCoal ? 1.0f : 0.0f;
                    }
                    else
                    {
                        _centersArray[i] = new Vector3(0,0,0);
                        _heightsArray[i] = 0;
                        _radiiArray[i]   = 0;
                    
                        // check if tree is start burning
                        _isolatedTreeArray[i] = 0;
                    }
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
                var handle = job.Schedule(transformAccessArray);
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
            
            Graphics.RenderMeshPrimitives(_renderParams, renderMesh, 0, transformAccessArray.length);
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
            
            transformAccessArray.Dispose();
            
            _centersArray.Dispose();
            _heightsArray.Dispose();
            _radiiArray.Dispose();
            _matricesArray.Dispose();
            _isolatedTreeArray.Dispose();
            
            _modulePositionsArray.Dispose();
        }
    }
}