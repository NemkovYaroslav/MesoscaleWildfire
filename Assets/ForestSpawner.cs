using UnityEngine;
using Random = UnityEngine.Random;

public class ForestSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject resourcePrefab;
    public float distanceBetweenChecks;
    public Vector2 positivePosition;
    public Vector2 negativePosition;

    private void Awake()
    {
        SpawnResources();
    }

    private void SpawnResources()
    {
        for (var x = negativePosition.x; x < positivePosition.x; x += distanceBetweenChecks)
        {
            for (var z = negativePosition.y; z < positivePosition.y; z += distanceBetweenChecks)
            {
                Instantiate(resourcePrefab, new Vector3(x, -6.5f, z), Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0)));
            }
        }
    }
}
