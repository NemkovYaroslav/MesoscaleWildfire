using UnityEngine;
using Random = UnityEngine.Random;

namespace WildfireModel.Common
{
    public class ForestFiller : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public GameObject treePrefab;
        public float distanceBetweenTrees;
        public Vector3 positivePosition;
        public Vector3 negativePosition;
        
        private void Awake()
        {
            SpawnResources();
        }
        
        private void SpawnResources()
        {
            for (var x = negativePosition.x; x < positivePosition.x; x += distanceBetweenTrees)
            {
                for (var z = negativePosition.z; z < positivePosition.z; z += distanceBetweenTrees)
                {
                    Instantiate(treePrefab, new Vector3(x, -6.5f, z), Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0)));
                }
            }
        }
    }
}
