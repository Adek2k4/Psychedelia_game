using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class InteractableSync : NetworkBehaviour
{
    [Header("Interaction")]
    public int requiredPlayers = 2;
    public float despawnDelay = 1f;

    [Header("Kaleidoscope targets (other objects)")]
    public Transform[] kaleidoscopeRoots;
    public string kaleidoscopeShaderName = "Custom/KaleidoscopeTextureMorph";

    [Header("Terrain materials (Inspector)")]
    public Material terrainMaterialDefault;
    public Material terrainMaterialSmall;
    public string terrainSmallName = "Terrain_0";

    [Header("Zaba rain (local pool)")]
    public Transform zabaPoolRoot;
    public string zabaPoolRootName = "deszcz";
    public int zabaPerPlayer = 5;
    public float zabaSpawnDelay = 60f;
    public float zabaSpawnRadius = 3f;
    public float zabaSpawnHeight = 25f;
    public float zabaLaunchDelay = 1f;
    public float zabaLaunchSpeed = 25f;
    public float zabaDespawnAfterLaunch = 2f;
    public string zabaClockSoundPath = "Sounds/Frog/clock";
    public float zabaClockVolume = 1f;

    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private Coroutine despawnRoutine;
    private bool changesApplied = false;

    public override void OnNetworkDespawn()
    {
        if (!changesApplied)
        {
            ApplyChangesBeforeDespawn();
        }

        readyClients.Clear();
        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (ready)
        {
            readyClients.Add(sender);
        }
        else
        {
            readyClients.Remove(sender);
        }

        if (readyClients.Count >= requiredPlayers)
        {
            if (despawnRoutine == null)
            {
                despawnRoutine = StartCoroutine(DespawnAfterDelay());
            }
        }
        else
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
                despawnRoutine = null;
            }
        }
    }

    IEnumerator DespawnAfterDelay()
    {
        float delay = Mathf.Max(0f, despawnDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (IsSpawned)
        {
            if (IsServer)
            {
                ApplyChangesBeforeDespawn();
                ApplyChangesClientRpc();
                ScheduleZabaRain();
                ZabaCounterManager counter = ZabaCounterManager.FindInScene();
                if (counter != null)
                {
                    counter.ActivateAfterDelayServer(zabaSpawnDelay);
                }
            }

            NetworkObject.Despawn(true);
        }

        despawnRoutine = null;
    }

    void ApplyChangesBeforeDespawn()
    {
        ApplyTerrainMaterials();
        ApplyKaleidoscopePresetOnRoots();
        changesApplied = true;
    }

    [ClientRpc]
    void ApplyChangesClientRpc()
    {
        if (changesApplied)
        {
            return;
        }

        ApplyChangesBeforeDespawn();
    }

    void ApplyKaleidoscopePresetOnRoots()
    {
        if (kaleidoscopeRoots == null) return;

        foreach (var root in kaleidoscopeRoots)
        {
            if (root == null) continue;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Material[] mats = r.materials;
                ApplyToMaterialsArray(mats);
            }
        }
    }

    void ApplyToMaterialsArray(Material[] mats)
    {
        if (mats == null) return;

        foreach (var mat in mats)
        {
            if (mat == null) continue;
            if (mat.shader == null) continue;
            if (mat.shader.name != kaleidoscopeShaderName) continue;

            mat.SetFloat("_FractalOpacity", 0.218f);
            mat.SetFloat("_WaveTexEnable", 1.0f);
            mat.SetFloat("_WaveNormEnable", 1.0f);
            mat.SetFloat("_ShadowWaveSpeed", 0.74f);
        }
    }

    void ApplyTerrainMaterials()
    {
        if (terrainMaterialDefault == null && terrainMaterialSmall == null)
        {
            Debug.LogWarning("InteractableSync: Terrain materials not assigned.");
            return;
        }

        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (terrains == null || terrains.Length == 0)
        {
            return;
        }

        foreach (var terrain in terrains)
        {
            if (terrain == null)
            {
                continue;
            }

            Material target = terrainMaterialDefault;
            if (terrain.name == terrainSmallName && terrainMaterialSmall != null)
            {
                target = terrainMaterialSmall;
            }

            if (target == null)
            {
                Debug.LogWarning($"InteractableSync: Missing terrain material for {terrain.name}.");
                continue;
            }

            terrain.materialType = Terrain.MaterialType.Custom;
            terrain.materialTemplate = target;
        }
    }

    void ScheduleZabaRain()
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        int perPlayer = Mathf.Max(0, zabaPerPlayer);
        if (perPlayer <= 0)
        {
            return;
        }

        List<Vector2> offsets = new List<Vector2>();
        List<ulong> targetIds = new List<ulong>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            for (int i = 0; i < perPlayer; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, zabaSpawnRadius);
                offsets.Add(offset);
                targetIds.Add(client.ClientId);
            }
        }

        if (offsets.Count == 0)
        {
            Debug.LogWarning("InteractableSync: No players found for zaba rain.");
            return;
        }

        PlayClockSoundClientRpc(transform.position, zabaClockVolume);

        StartZabaRainClientRpc(
            offsets.ToArray(),
            targetIds.ToArray(),
            Mathf.Max(0f, zabaSpawnDelay),
            zabaSpawnHeight,
            Mathf.Max(0f, zabaLaunchDelay),
            Mathf.Max(0f, zabaLaunchSpeed),
            Mathf.Max(0f, zabaDespawnAfterLaunch)
        );
    }

    [ClientRpc]
    void StartZabaRainClientRpc(
        Vector2[] spawnOffsets,
        ulong[] targetClientIds,
        float spawnDelay,
        float spawnHeight,
        float launchDelay,
        float launchSpeed,
        float despawnAfterLaunch)
    {
        if (spawnOffsets == null || targetClientIds == null)
        {
            return;
        }

        if (spawnOffsets.Length == 0 || spawnOffsets.Length != targetClientIds.Length)
        {
            Debug.LogWarning("InteractableSync: Zaba spawn data mismatch.");
            return;
        }

        ZabaRainManager.Instance.StartRain(
            spawnOffsets,
            targetClientIds,
            zabaPoolRoot,
            zabaPoolRootName,
            spawnDelay,
            spawnHeight,
            launchDelay,
            launchSpeed,
            despawnAfterLaunch
        );
    }

    [ClientRpc]
    void PlayClockSoundClientRpc(Vector3 position, float volume)
    {
        if (string.IsNullOrWhiteSpace(zabaClockSoundPath))
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(zabaClockSoundPath);
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
