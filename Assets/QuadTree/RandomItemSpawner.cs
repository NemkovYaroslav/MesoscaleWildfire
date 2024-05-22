using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuadTree
{
    public class RandomItemSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject prefabToSpawn;
        [SerializeField] private int        numToSpawn = 1000;
        [SerializeField] private GameObject spawnZone;

        [SerializeField] private UnityEvent<Rect>             on2DBoundsCalculated = new();
        [SerializeField] private UnityEvent<GameObject>       onItemSpawned        = new();
        [SerializeField] private UnityEvent<List<GameObject>> onAllItemsSpawned    = new();
        
        private void Start()
        {
            PerformSpawning();
        }

        private void PerformSpawning()
        {
            var spawnBounds = spawnZone.GetComponent<MeshRenderer>().bounds;

            var items = new List<GameObject>(numToSpawn);
            
            var spawnRect = new Rect(spawnBounds.min.x, spawnBounds.min.z, spawnBounds.size.x, spawnBounds.size.z);

            on2DBoundsCalculated.Invoke(spawnRect);

            for (var index = 0; index < numToSpawn; ++index)
            {
                var spawnPos
                    = new Vector3(
                        Random.Range(spawnRect.xMin, spawnRect.xMax),
                        -6.5f,
                        Random.Range(spawnRect.yMin, spawnRect.yMax)
                    );
                
                var newGo = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

                items.Add(newGo);
                onItemSpawned.Invoke(newGo);
            }

            onAllItemsSpawned.Invoke(items);
        }
    }
}