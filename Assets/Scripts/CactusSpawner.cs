using System.Collections.Generic;
using UnityEngine;

public class CactusSpawner : MonoBehaviour
{
    [Header("References")]
    public Terrain targetTerrain;
    public GameObject cactusPrefab;
    public GameObject[] cactusPrefabs;
    public Transform container;

    [Header("Area")]
    public bool useTerrainSize = true;
    public Vector2 areaSize = new Vector2(1000f, 1000f);
    public float edgePadding = 2f;

    [Header("Spawn")]
    public int cactusCount = 300;
    public float minSpacing = 2.5f;
    public float maxSlopeAngle = 35f;
    public int maxPlacementAttempts = 15000;
    public bool clearPreviousBeforeSpawn = true;

    [Header("Variation")]
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.3f);
    public Vector2 yRotationRange = new Vector2(0f, 360f);
    public float maxRandomTilt = 6f;
    public Vector2 sinkIntoGroundRange = new Vector2(0.05f, 0.25f);

    [Header("Random")]
    public bool useRandomSeed = true;
    public int randomSeed = 12345;

    private readonly List<Vector2> placedPoints = new List<Vector2>();

    [ContextMenu("Spawn Cacti")]
    public void SpawnCacti()
    {
        ResolveTerrainIfMissing();

        if (targetTerrain == null)
        {
            Debug.LogError("CactusSpawner: Assign Target Terrain or place one active Terrain in the scene.", this);
            return;
        }

        if (!HasAnyPrefab())
        {
            Debug.LogError("CactusSpawner: Assign Cactus Prefab or add prefabs to Cactus Prefabs list.", this);
            return;
        }

        if (container == null)
        {
            GameObject group = new GameObject("SpawnedCacti");
            group.transform.SetParent(transform);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            container = group.transform;
        }

        if (clearPreviousBeforeSpawn)
        {
            ClearSpawnedCacti();
        }

        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.GetPosition();
        Vector3 terrainSize = terrainData.size;

        float spawnSizeX = useTerrainSize ? terrainSize.x : Mathf.Min(areaSize.x, terrainSize.x);
        float spawnSizeZ = useTerrainSize ? terrainSize.z : Mathf.Min(areaSize.y, terrainSize.z);

        float areaStartX = terrainPos.x + (terrainSize.x - spawnSizeX) * 0.5f;
        float areaStartZ = terrainPos.z + (terrainSize.z - spawnSizeZ) * 0.5f;

        if (spawnSizeX <= edgePadding * 2f || spawnSizeZ <= edgePadding * 2f)
        {
            Debug.LogError("CactusSpawner: Area is too small for current edge padding.", this);
            return;
        }

        Random.State oldRandomState = Random.state;
        if (!useRandomSeed)
        {
            Random.InitState(randomSeed);
        }

        placedPoints.Clear();

        int spawned = 0;
        int attempts = 0;
        float minSpacingSqr = minSpacing * minSpacing;

        while (spawned < cactusCount && attempts < maxPlacementAttempts)
        {
            attempts++;

            float x = Random.Range(areaStartX + edgePadding, areaStartX + spawnSizeX - edgePadding);
            float z = Random.Range(areaStartZ + edgePadding, areaStartZ + spawnSizeZ - edgePadding);
            Vector2 candidate = new Vector2(x, z);

            if (!IsFarEnough(candidate, minSpacingSqr))
            {
                continue;
            }

            float normalizedX = Mathf.InverseLerp(terrainPos.x, terrainPos.x + terrainSize.x, x);
            float normalizedZ = Mathf.InverseLerp(terrainPos.z, terrainPos.z + terrainSize.z, z);

            float steepness = terrainData.GetSteepness(normalizedX, normalizedZ);
            if (steepness > maxSlopeAngle)
            {
                continue;
            }

            float baseY = targetTerrain.SampleHeight(new Vector3(x, 0f, z)) + terrainPos.y;
            float sink = Random.Range(sinkIntoGroundRange.x, sinkIntoGroundRange.y);
            float y = baseY - sink;

            float yaw = Random.Range(yRotationRange.x, yRotationRange.y);
            float tiltX = Random.Range(-maxRandomTilt, maxRandomTilt);
            float tiltZ = Random.Range(-maxRandomTilt, maxRandomTilt);
            Quaternion rotation = Quaternion.Euler(-90f + tiltX, yaw, tiltZ);

            GameObject prefabToSpawn = GetRandomPrefab();
            if (prefabToSpawn == null)
            {
                continue;
            }

            GameObject cactus = Instantiate(prefabToSpawn, new Vector3(x, y, z), rotation, container);

            float scale = Random.Range(randomScaleRange.x, randomScaleRange.y);
            cactus.transform.localScale *= scale;

            placedPoints.Add(candidate);
            spawned++;
        }

        if (!useRandomSeed)
        {
            Random.state = oldRandomState;
        }

        if (spawned < cactusCount)
        {
            Debug.LogWarning(
                $"CactusSpawner: Spawned {spawned}/{cactusCount}. Increase area or lower min spacing.",
                this
            );
        }
        else
        {
            Debug.Log($"CactusSpawner: Spawned {spawned} cacti.", this);
        }
    }

    [ContextMenu("Clear Spawned Cacti")]
    public void ClearSpawnedCacti()
    {
        if (container == null)
        {
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private bool IsFarEnough(Vector2 candidate, float minSpacingSqr)
    {
        for (int i = 0; i < placedPoints.Count; i++)
        {
            if ((candidate - placedPoints[i]).sqrMagnitude < minSpacingSqr)
            {
                return false;
            }
        }

        return true;
    }

    private void ResolveTerrainIfMissing()
    {
        if (targetTerrain != null)
        {
            return;
        }

        targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null)
        {
            targetTerrain = FindFirstObjectByType<Terrain>();
        }
    }

    private bool HasAnyPrefab()
    {
        if (cactusPrefabs != null)
        {
            for (int i = 0; i < cactusPrefabs.Length; i++)
            {
                if (cactusPrefabs[i] != null)
                {
                    return true;
                }
            }
        }

        return cactusPrefab != null;
    }

    private GameObject GetRandomPrefab()
    {
        if (cactusPrefabs != null && cactusPrefabs.Length > 0)
        {
            int validCount = 0;
            for (int i = 0; i < cactusPrefabs.Length; i++)
            {
                if (cactusPrefabs[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                int pick = Random.Range(0, validCount);
                for (int i = 0; i < cactusPrefabs.Length; i++)
                {
                    if (cactusPrefabs[i] == null)
                    {
                        continue;
                    }

                    if (pick == 0)
                    {
                        return cactusPrefabs[i];
                    }

                    pick--;
                }
            }
        }

        return cactusPrefab;
    }
}
