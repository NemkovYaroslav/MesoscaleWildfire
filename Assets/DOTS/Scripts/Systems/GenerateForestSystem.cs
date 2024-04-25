/*
using System.Runtime.InteropServices;
using DOTS.Scripts.ComponentsAndTags;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Scripts.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    [StructLayout(LayoutKind.Auto)]
    public partial struct GenerateForestSystem : ISystem
    {
        private EntityManager _entityManager;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ForestComponent>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;
            
            var forestEntity = SystemAPI.GetSingletonEntity<ForestComponent>();
            var forestAspect = SystemAPI.GetAspect<ForestAspect>(forestEntity);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var halfDimensionX = forestAspect.Dimensions.x * 0.5f;
            var halfDimensionZ = forestAspect.Dimensions.y * 0.5f;
            
            for (var x = -halfDimensionX; x <= halfDimensionX; x += forestAspect.TreeStep)
            {
                for (var z = -halfDimensionZ; z <= halfDimensionZ; z += forestAspect.TreeStep)
                {
                    var newTree = ecb.Instantiate(forestAspect.TreePrefab);
                    
                    var position = new float3(x, 0, z);
                    ecb.SetComponent(
                        newTree,
                        new LocalTransform
                        {
                            Position = position,
                            Rotation = forestAspect.GetRandomRotation(),
                            Scale = forestAspect.Transform.Scale
                        }
                    );
                    
                    ecb.AddComponent<TreeRendererComponent>(newTree);
                }
            }
            
            ecb.Playback(state.EntityManager);
        }
    }
}
*/