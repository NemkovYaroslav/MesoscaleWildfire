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
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material renderMaterial;
        [SerializeField] private Mesh renderMesh;
        
        private RenderParams _renderParams;
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        
        
        // TREE HIERARCHY VARIABLES
        [HideInInspector] public List<Module> orderedModuleList;
        [HideInInspector] public int modulesCount;
        
        // TRANSFORMS ARRAY (for jobs)
        public TransformAccessArray transformAccessArray;
        
        
        // MODULE RENDER
        private NativeArray<Vector3>   _centersArray;
        private NativeArray<float>     _heightsArray;
        private NativeArray<float>     _radiiArray;
        private NativeArray<Matrix4x4> _matricesArray;
        private ComputeBuffer          _matricesBuffer;
        
        
        // BURNED TREE
        private NativeArray<int> _burnedTreeArray;
        private ComputeBuffer     _burnedTreeBuffer;
        
        // LOCAL WILDFIRE AREA MODULE POSITIONS
        private Matrix4x4 _wildfireAreaWorldToLocal;
        public NativeArray<Vector4> modulePositionsArray;
        
        
        // MODULE POSITIONS
        private ComputeBuffer _modulePositionsBuffer;
        private int _kernelReadData;
        private int _kernelWriteData;
        //private int _kernelSimulateFire;
        private static readonly int ModulePositions = Shader.PropertyToID("module_positions");
        private static readonly int BurnedTrees = Shader.PropertyToID("burned_trees");

        private void Start()
        {
            var wildfireArea = GameObject.FindWithTag("WildfireArea");
            
            _kernelReadData     = computeShader.FindKernel("kernel_read_data");
            _kernelWriteData    = computeShader.FindKernel("kernel_write_data");
            
            ///*
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
            //*/

            /*
            orderedModuleList = new List<Module>();
            var modules = GameObject.FindGameObjectsWithTag("Module");
            foreach (var module in modules)
            {
                orderedModuleList.Add(module.GetComponent<Module>());
            }
            modulesCount = orderedModuleList.Count;
            */
            
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
            
            var wildfireAreaTransform      = wildfireArea.transform;
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
                transforms[i] = orderedModuleList[i].transForm;
            }
            transformAccessArray = new TransformAccessArray(transforms);
            
            
            // MODULES POSITIONS
            modulePositionsArray 
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
            _burnedTreeArray
                = new NativeArray<int>(
                    modulesCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory
                );
            _burnedTreeBuffer 
                = new ComputeBuffer(
                    modulesCount, 
                    sizeof(int)
                );
            
            _wildfireAreaWorldToLocal = wildfireAreaTransform.worldToLocalMatrix;
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
                    
                    // check is tree is burned
                    _burnedTreeArray[i] = orderedModuleList[i].isBurned ? 1 : 0;

                    //Debug.Log(_burnedTreeArray[i]);
                }

                var job = new FillMatricesAndPositionsDataJob()
                {
                    centers = _centersArray,
                    heights = _heightsArray,
                    radii = _radiiArray,
                    matrices = _matricesArray,

                    wildfireAreaWorldToLocal = _wildfireAreaWorldToLocal,
                    modulesWildfireAreaPosition = modulePositionsArray,
                };
                var handle = job.Schedule(transformAccessArray);
                handle.Complete();

                _modulePositionsBuffer.SetData(modulePositionsArray);
                computeShader.SetBuffer(_kernelReadData, ModulePositions, _modulePositionsBuffer);
                computeShader.SetBuffer(_kernelWriteData, ModulePositions, _modulePositionsBuffer);
            }
        }
        
        private void RenderModules()
        {
            _matricesBuffer.SetData(_matricesArray);
            _renderParams.matProps.SetBuffer(Matrices, _matricesBuffer);
            
            _burnedTreeBuffer.SetData(_burnedTreeArray);
            _renderParams.matProps.SetBuffer(BurnedTrees, _burnedTreeBuffer);
            
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
        }
        
        private void OnDestroy()
        {
            CleanupComputeBuffer();
            
            transformAccessArray.Dispose();
            
            _centersArray.Dispose();
            _heightsArray.Dispose();
            _radiiArray.Dispose();
            _matricesArray.Dispose();

            modulePositionsArray.Dispose();
        }
    }
}