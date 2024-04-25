/*
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DOTS.Scripts.Test
{
    public partial class PrefabSpawnerSystem : SystemBase
    {
        private Random _random;

        protected override void OnCreate()
        {
            _random = new Random(56);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            if (Input.GetKeyDown(KeyCode.A))
            {
                var entity = SystemAPI.GetSingletonEntity<PrefabComponent>();
                var component = EntityManager.GetComponentData<PrefabComponent>(entity);
                var obj = ecb.Instantiate(component.prefab);
            }
            
            ecb.Playback(EntityManager);
        }
    }
}
*/