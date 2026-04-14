using System.Collections.Generic;
using UnityEngine;

public class SceneObjectDesertPlacer : MonoBehaviour
{
    [Header("References")]
    public Terrain targetTerrain;
    public Transform sourceParent;
    public List<Transform> sceneObjects = new List<Transform>();
    public Transform spawnedContainer;

    [Header("Collection")]
    public bool autoCollectFromParentOnDistribute = true;
    public bool includeInactiveChildren = true;

    [Header("Area")]
    public bool useTerrainSize = true;
    public Vector2 areaSize = new Vector2(1000f, 1000f);
    public float edgePadding = 2f;

    [Header("Placement")]
    public int objectsToSpawn = 200;
    public float minSpacing = 2f;
    public float maxSlopeAngle = 35f;
    public int maxPlacementAttempts = 20000;

    [Header("Rotation")]
    public bool randomizeYaw = true;
    public Vector2 yRotationRange = new Vector2(0f, 360f);
    public float maxRandomTilt = 6f;

    [Header("Grounding")]
    public float yOffset = 1f;
    public bool clearPreviousBeforeSpawn = true;

    [Header("Random")]
    public bool useRandomSeed = true;
    public int randomSeed = 12345;

    private readonly List<Vector2> placedPoints = new List<Vector2>();

    [ContextMenu("Spawn Scene Object Clones")]
    public void SpawnSceneObjectClones()
    {
        ResolveTerrainIfMissing();
        if (targetTerrain == null)
        {
            Debug.LogError("SceneObjectDesertPlacer: No Terrain found/assigned.", this);
            return;
        }

        if (autoCollectFromParentOnDistribute)
        {
            CollectObjectsFromParent();
        }

        if (spawnedContainer == null)
        {
            GameObject containerObject = new GameObject("SpawnedSceneObjects");
            containerObject.transform.SetParent(transform);
            containerObject.transform.localPosition = Vector3.zero;
            containerObject.transform.localRotation = Quaternion.identity;
            spawnedContainer = containerObject.transform;
        }

        if (clearPreviousBeforeSpawn)
        {
            ClearSpawnedClones();
        }

        List<Transform> validObjects = GetValidObjects();
        if (validObjects.Count == 0)
        {
            Debug.LogWarning("SceneObjectDesertPlacer: No source scene objects to clone.", this);
            return;
        }

        if (objectsToSpawn <= 0)
        {
            Debug.LogWarning("SceneObjectDesertPlacer: Objects To Spawn must be greater than 0.", this);
            return;
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
            Debug.LogError("SceneObjectDesertPlacer: Area too small for current edge padding.", this);
            return;
        }

        Random.State oldRandomState = Random.state;
        if (!useRandomSeed)
        {
            Random.InitState(randomSeed);
        }

        placedPoints.Clear();
        float minSpacingSqr = minSpacing * minSpacing;

        int placedCount = 0;
        int attempts = 0;

        while (placedCount < objectsToSpawn && attempts < maxPlacementAttempts)
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

            Transform source = validObjects[Random.Range(0, validObjects.Count)];

            float baseY = targetTerrain.SampleHeight(new Vector3(x, 0f, z)) + terrainPos.y;
            float y = baseY + yOffset;

            float yaw = randomizeYaw ? Random.Range(yRotationRange.x, yRotationRange.y) : source.eulerAngles.y;
            float tiltX = Random.Range(-maxRandomTilt, maxRandomTilt);
            float tiltZ = Random.Range(-maxRandomTilt, maxRandomTilt);

            Vector3 spawnPos = new Vector3(x, y, z);
            Quaternion spawnRot = Quaternion.Euler(-90f + tiltX, yaw, tiltZ);

            Transform clone = Instantiate(source, spawnPos, spawnRot, spawnedContainer);
            clone.name = source.name;

            placedPoints.Add(candidate);
            placedCount++;
        }

        if (!useRandomSeed)
        {
            Random.state = oldRandomState;
        }

        if (placedCount < objectsToSpawn)
        {
            Debug.LogWarning($"SceneObjectDesertPlacer: Spawned {placedCount}/{objectsToSpawn} clones.", this);
        }
        else
        {
            Debug.Log($"SceneObjectDesertPlacer: Spawned {placedCount} clones.", this);
        }
    }

    [ContextMenu("Clear Spawned Clones")]
    public void ClearSpawnedClones()
    {
        if (spawnedContainer == null)
        {
            return;
        }

        for (int i = spawnedContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = spawnedContainer.GetChild(i);
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

    [ContextMenu("Collect Objects From Parent")]
    public void CollectObjectsFromParent()
    {
        if (sourceParent == null)
        {
            return;
        }

        sceneObjects.Clear();

        for (int i = 0; i < sourceParent.childCount; i++)
        {
            Transform child = sourceParent.GetChild(i);
            if (!includeInactiveChildren && !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            sceneObjects.Add(child);
        }
    }

    private List<Transform> GetValidObjects()
    {
        List<Transform> valid = new List<Transform>(sceneObjects.Count);

        for (int i = 0; i < sceneObjects.Count; i++)
        {
            Transform t = sceneObjects[i];
            if (t == null)
            {
                continue;
            }

            if (t == transform || (sourceParent != null && t == sourceParent))
            {
                continue;
            }

            valid.Add(t);
        }

        return valid;
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
}
