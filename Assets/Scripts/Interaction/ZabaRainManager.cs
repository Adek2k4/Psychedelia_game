using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ZabaRainManager : MonoBehaviour
{
    private static ZabaRainManager instance;

    public static ZabaRainManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ZabaRainManager>(true);
                if (instance == null)
                {
                    GameObject go = new GameObject("ZabaRainManager");
                    instance = go.AddComponent<ZabaRainManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return instance;
        }
    }

    private readonly List<ZabaLocal> pool = new List<ZabaLocal>();
    private Transform poolRoot;
    private string poolRootName = "deszcz";

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartRain(
        Vector2[] offsets,
        ulong[] targetClientIds,
        Transform root,
        string rootName,
        float spawnDelay,
        float spawnHeight,
        float launchDelay,
        float launchSpeed,
        float despawnAfterLaunch)
    {
        if (offsets == null || targetClientIds == null || offsets.Length == 0)
        {
            return;
        }

        if (offsets.Length != targetClientIds.Length)
        {
            Debug.LogWarning("ZabaRainManager: Zaba data mismatch.");
            return;
        }

        SetupPool(root, rootName);
        if (pool.Count == 0)
        {
            Debug.LogWarning("ZabaRainManager: No zaba objects under pool root.");
            return;
        }

        // DEBUG: confirm pool resolved correctly. Remove once fixed.
        Debug.Log($"[ZabaRain] StartRain: pool root={poolRoot.name}, poolCount={pool.Count}, requested={offsets.Length}");

        StartCoroutine(SpawnRoutine(
            offsets,
            targetClientIds,
            spawnDelay,
            spawnHeight,
            launchDelay,
            launchSpeed,
            despawnAfterLaunch
        ));
    }

    void SetupPool(Transform root, string rootName)
    {
        Transform resolvedRoot = root;
        if (resolvedRoot == null)
        {
            resolvedRoot = FindPoolRoot(rootName);
        }

        if (resolvedRoot == null)
        {
            Debug.LogWarning("ZabaRainManager: Pool root not found.");
            return;
        }

        if (poolRoot != resolvedRoot)
        {
            poolRoot = resolvedRoot;
            poolRootName = rootName;
            RebuildPool();
        }
        else if (pool.Count == 0)
        {
            RebuildPool();
        }
    }

    Transform FindPoolRoot(string rootName)
    {
        if (string.IsNullOrWhiteSpace(rootName))
        {
            return null;
        }

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && t.name == rootName)
            {
                return t;
            }
        }

        return null;
    }

    void RebuildPool()
    {
        pool.Clear();
        if (poolRoot == null)
        {
            return;
        }

        if (!poolRoot.gameObject.activeSelf)
        {
            poolRoot.gameObject.SetActive(true);
        }

        int childCount = poolRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = poolRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            ZabaLocal zaba = child.GetComponent<ZabaLocal>();
            if (zaba == null)
            {
                zaba = child.gameObject.AddComponent<ZabaLocal>();
            }

            zaba.CacheComponents();
            pool.Add(zaba);
        }

        if (pool.Count == 0)
        {
            Debug.LogWarning("ZabaRainManager: Pool root has no children.");
        }
    }

    IEnumerator SpawnRoutine(
        Vector2[] offsets,
        ulong[] targetClientIds,
        float spawnDelay,
        float spawnHeight,
        float launchDelay,
        float launchSpeed,
        float despawnAfterLaunch)
    {
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        int count = Mathf.Min(pool.Count, offsets.Length);
        if (offsets.Length > pool.Count)
        {
            Debug.LogWarning("ZabaRainManager: Not enough zaba objects in pool.");
        }

        for (int i = 0; i < count; i++)
        {
            ZabaLocal zaba = pool[i];
            if (zaba == null)
            {
                continue;
            }

            Transform playerTransform = GetPlayerTransform(targetClientIds[i]);
            if (playerTransform == null)
            {
                // DEBUG: this is the most likely silent failure point on clients.
                Debug.LogWarning($"[ZabaRain] No player transform for clientId={targetClientIds[i]}, skipping zaba {i}.");
                continue;
            }

            Vector3 playerPos = playerTransform.position;
            Vector2 offset = offsets[i];
            Vector3 spawnPos = new Vector3(playerPos.x + offset.x, playerPos.y + spawnHeight, playerPos.z + offset.y);

            zaba.Activate(
                spawnPos,
                targetClientIds[i],
                launchDelay,
                launchSpeed,
                despawnAfterLaunch
            );
        }
    }

    Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        if (NetworkManager.Singleton.SpawnManager != null)
        {
            NetworkObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (player != null)
            {
                return player.transform;
            }
        }

        NetworkObject[] objects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj != null && obj.IsPlayerObject && obj.OwnerClientId == clientId)
            {
                return obj.transform;
            }
        }

        return null;
    }
}
