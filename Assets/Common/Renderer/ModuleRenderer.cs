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
        
        public int modulesCount;
        public TransformAccessArray transformsArray;
        public NativeArray<Vector3> centers;
        private NativeArray<float> _heights;
        
        private static readonly int Matrices = Shader.PropertyToID("matrices");
        private ComputeBuffer _renderMatricesBuffer;
        private int _matricesBufferStride;
        
        // TREE HIERARCHY VARIABLES
        public List<Module> orderedModuleList;

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
            
            // fill transforms array
            var transforms = new Transform[modulesCount];
            for (var i = 0; i < modulesCount; i++)
            {
                transforms[i] = orderedModuleList[i].transForm;
            }
            transformsArray = new TransformAccessArray(transforms);
        
            // fill module centers and heights
            centers = new NativeArray<Vector3>(
                modulesCount, 
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            _heights = new NativeArray<float>(
                modulesCount, 
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );
            for (var i = 0; i < modulesCount; i++)
            {
                centers[i] = orderedModuleList[i].capsuleCollider.center;
                _heights[i] = orderedModuleList[i].capsuleCollider.height;
            }

            var wildfireArea = GameObject.FindWithTag("WildfireArea");
            var wildfireAreaPosition = wildfireArea.transform.position;
            var wildfireAreaScale = wildfireArea.transform.lossyScale;
            _renderParams = new RenderParams(renderMaterial)
            {
                worldBounds = new Bounds(wildfireAreaPosition, wildfireAreaScale),
                matProps = new MaterialPropertyBlock()
            };
            
            _matricesBufferStride = sizeof(float) * 16;
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
                radii[i] = orderedModuleList[i].capsuleCollider.radius;
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
                heights = _heights,
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
            _heights.Dispose();
            centers.Dispose();
        }
    }
}