using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Common.Wildfire
{
    public class ForestSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject prefabToSpawn; 
        [SerializeField] private int numToSpawn = 1000; 
        [SerializeField] private GameObject spawnZone;
        
        private void Awake()
        {
            PerformSpawning();
        }

        private void PerformSpawning()
        {
            var spawnBounds = spawnZone.GetComponent<MeshRenderer>().bounds;
            var spawnRect = new Rect(spawnBounds.min.x, spawnBounds.min.z, spawnBounds.size.x, spawnBounds.size.z);

            for (var index = 0; index < numToSpawn; ++index)
            {
                var spawnPos 
                    = new Vector3(
                        Random.Range(spawnRect.xMin, spawnRect.xMax),
                        0f,
                        Random.Range(spawnRect.yMin, spawnRect.yMax)
                    );
            
                var newGo = GameObject.Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            }
        
        }
    }
}