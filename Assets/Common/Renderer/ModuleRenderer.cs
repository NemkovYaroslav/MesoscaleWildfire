using System.Collections.Generic;
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
        private ComputeBuffer _renderMatricesBuffer;
        private int _matricesBufferStride;
        
        
        // LOCAL WILDFIRE AREA MODULE POSITIONS
        private Matrix4x4 _wildfireAreaWorldToLocal;
        public NativeArray<Vector4> modulePositionsArray;

        
        private void Start()
        {
            // INITIALIZE TREE HIERARCHY DATA
            // used to sort modules to tree hierarchy order
            orderedModuleList = new List<Module>();
            var trees = GameObject.FindGameObjectsWithTag("Tree");
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
            _renderMatricesBuffer = new ComputeBuffer(modulesCount, _matricesBufferStride);
            
            // used for schedule jobs
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = orderedModuleList[i].transForm;
            }
            transformAccessArray = new TransformAccessArray(transforms);

            var wildfireArea = GameObject.FindWithTag("WildfireArea");
            var wildfireAreaTransform = wildfireArea.transform;
            var wildfireAreaPosition = wildfireAreaTransform.position;
            var wildfireAreaScale = wildfireAreaTransform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };

            _wildfireAreaWorldToLocal = wildfireAreaTransform.worldToLocalMatrix;
            
            modulePositionsArray = new NativeArray<Vector4>(
                modulesCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
        }

        private void RenderModules()
        {
            if (transformAccessArray.isCreated)
            {
                // fill module location params
                for (var i = 0; i < modulesCount; i++)
                {
                    _centersArray[i] = orderedModuleList[i].capsuleCollider.center;
                    _heightsArray[i] = orderedModuleList[i].capsuleCollider.height;
                    _radiiArray[i]   = orderedModuleList[i].capsuleCollider.radius;
                }
            
                // fill common data about modules
                var job = new FillCommonDataJob()
                {
                    centers  = _centersArray,
                    heights  = _heightsArray,
                    radii    = _radiiArray,
                    matrices = _matricesArray,
                
                    wildfireAreaWorldToLocal    = _wildfireAreaWorldToLocal,
                    modulesWildfireAreaPosition = modulePositionsArray,
                };
                var handle = job.Schedule(transformAccessArray);
                handle.Complete();
            
                _renderMatricesBuffer.SetData(_matricesArray);
                _renderParams.matProps.SetBuffer(Matrices, _renderMatricesBuffer);
                Graphics.RenderMeshPrimitives(_renderParams, renderMesh, 0, modulesCount);
            }
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
            
            transformAccessArray.Dispose();
            
            _centersArray.Dispose();
            _heightsArray.Dispose();
            _radiiArray.Dispose();
            _matricesArray.Dispose();

            modulePositionsArray.Dispose();
        }
    }
}