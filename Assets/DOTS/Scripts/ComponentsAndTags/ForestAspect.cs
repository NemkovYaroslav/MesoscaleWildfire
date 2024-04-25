/*
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Scripts.ComponentsAndTags
{
    public readonly partial struct ForestAspect : IAspect
    {
        public readonly Entity entity;
        
        
        private readonly RefRO<LocalTransform> _transform; 
        public LocalTransform Transform => _transform.ValueRO;
        
        
        private readonly RefRO<ForestComponent> _forestComponent;
        public float2 Dimensions => _forestComponent.ValueRO.forestDimensions;
        public int TreeStep => _forestComponent.ValueRO.treeStep;
        public Entity TreePrefab => _forestComponent.ValueRO.treePrefab;
        
        private readonly RefRW<RandomComponent> _forestRandom;
        
        public quaternion GetRandomRotation() => quaternion.RotateY(_forestRandom.ValueRW.value.NextFloat(-0.5f, 0.5f));
    }
}
*/