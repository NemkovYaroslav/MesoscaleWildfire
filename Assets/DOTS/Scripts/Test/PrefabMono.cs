/*
using Unity.Entities;
using UnityEngine;

namespace DOTS.Scripts.Test
{
    public class PrefabMono : MonoBehaviour
    {
        public GameObject prefab;
    }
    
    public class TreeBaker : Baker<PrefabMono>
    {
        public override void Bake(PrefabMono authoring)
        {
            var prefabEntity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(
                prefabEntity,
                new PrefabComponent
                {
                    prefab = GetEntity(authoring.prefab, TransformUsageFlags.None),
                }
            );
        }
    }
    
    public struct PrefabComponent : IComponentData
    {
        public Entity prefab;
    }
}
*/