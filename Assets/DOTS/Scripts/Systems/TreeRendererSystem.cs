/*
using DOTS.Scripts.ComponentsAndTags;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Scripts.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(GenerateForestSystem))] 
    public partial class TreeRendererSystem : SystemBase
    {
        private Material _material;
        private Mesh _mesh;
        private Bounds _bounds;

        private bool _isInitialize;
        
        protected override void OnCreate()
        {
            RequireForUpdate<ForestComponent>();
        }
        
        protected override void OnUpdate()
        {
            var forestEntity = SystemAPI.GetSingletonEntity<ForestComponent>();
            
            if (!_isInitialize)
            {
                var render = EntityManager.GetComponentData<CommonRenderDataComponent>(forestEntity);
                _material = render.material;
                _mesh = render.mesh;
                _bounds = new Bounds(Vector3.zero, Vector3.one * 5);
                
                _isInitialize = true;
            }
            
            

            //Graphics.DrawMeshInstancedProcedural(_mesh, 0, _material, _bounds, 1);
        }
    }
}
*/