using UnityEngine;
using Random = UnityEngine.Random;

namespace WildfireModel.Wildfire
{
    public class ForestSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public GameObject resourcePrefab;
        public float distanceBetweenChecks;
        public Vector3 positivePosition;
        public Vector3 negativePosition;
        
        private void Awake()
        {
            SpawnResources();
        }

        private void SpawnResources()
        {
            for (var x = negativePosition.x; x < positivePosition.x; x += distanceBetweenChecks)
            {
                for (var z = negativePosition.z; z < positivePosition.z; z += distanceBetweenChecks)
                {
                    Instantiate(resourcePrefab, new Vector3(x, -6.5f, z), Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0)));
                }
            }
        }
    }
}
