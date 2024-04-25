/*
using DOTS.Scripts.ComponentsAndTags;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DOTS.Scripts.AuthoringAndMono
{
    public class ForestMono : MonoBehaviour
    {
        public float2 forestDimensions;
        
        public int treeStep;
        
        public GameObject treePrefab;

        public uint randomSeed;

        //public Material material;

        //public Mesh mesh;
    }

    public class ForestBaker : Baker<ForestMono>
    {
        public override void Bake(ForestMono authoring)
        {
            var forestEntity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(
                forestEntity,
                new ForestComponent
                {
                    forestDimensions = authoring.forestDimensions,
                    treeStep = authoring.treeStep,
                    treePrefab = GetEntity(authoring.treePrefab, TransformUsageFlags.Dynamic),
                }
            );
            
            AddComponent(
                forestEntity,
                new RandomComponent
                {
                    value = Random.CreateFromIndex(authoring.randomSeed)
                }
            );

            //AddComponentObject(
                //forestEntity, 
                //new CommonRenderDataComponent()
                //{
                    //material = authoring.material,
                    //mesh = authoring.mesh
                //}
            //);
        }
    }
}
*/