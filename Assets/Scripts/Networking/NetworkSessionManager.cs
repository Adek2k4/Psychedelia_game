using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class NetworkSessionManager : MonoBehaviour
{
    public int maxPlayers = 2;
    public string gameSceneName = "Pustynia";
    public GameObject hostPlayerPrefab;
    public GameObject clientPlayerPrefab;
    public Transform[] spawnPoints;

    private readonly List<ulong> pendingClients = new List<ulong>();
    private bool registeredApprovalCallback = false;
    private bool registeredClientConnected = false;
    private bool registeredSceneLoaded = false;

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("Brak NetworkManager w scenie.");
            return;
        }

        NetworkSessionManager[] managers = FindObjectsByType<NetworkSessionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers != null && managers.Length > 1)
        {
            string sceneName = gameObject.scene.name;
            Debug.LogWarning($"Znaleziono {managers.Length} NetworkSessionManager w instancji. Scene: {sceneName}");
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                {
                    Debug.LogWarning($"- {managers[i].name} (scene: {managers[i].gameObject.scene.name})");
                }
            }
        }

        if (NetworkManager.Singleton.ConnectionApprovalCallback == null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = HandleApproval;
            registeredApprovalCallback = true;
        }
        else
        {
            Debug.LogWarning("ConnectionApprovalCallback jest juz ustawiony. Sprawdz, czy NetworkSessionManager nie jest zdublowany.");
        }

        if (!registeredClientConnected)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            registeredClientConnected = true;
        }

        if (!registeredSceneLoaded)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            registeredSceneLoaded = true;
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        RegisterNetworkPrefabs();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (registeredApprovalCallback && NetworkManager.Singleton.ConnectionApprovalCallback == HandleApproval)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
        }

        if (registeredClientConnected)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }

        if (registeredSceneLoaded)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    void HandleApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int connectedPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;
        bool allow = connectedPlayers < maxPlayers;

        response.Approved = allow;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = allow ? string.Empty : "Server full";
    }

    void HandleClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!pendingClients.Contains(clientId))
        {
            pendingClients.Add(clientId);
        }

        TrySpawnPendingClients();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (scene.name != gameSceneName)
        {
            return;
        }

        TrySpawnPendingClients();
    }

    void RefreshSpawnPointsIfNeeded()
    {
        bool hasValid = false;
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    hasValid = true;
                    break;
                }
            }
        }

        if (hasValid)
        {
            return;
        }

        NetworkSpawnPoint[] points = FindObjectsByType<NetworkSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (points == null || points.Length == 0)
        {
            return;
        }

        System.Array.Sort(points, (a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
        });

        spawnPoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            spawnPoints[i] = points[i].transform;
        }
    }

    void TrySpawnPendingClients()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != gameSceneName)
        {
            return;
        }

        if (pendingClients.Count == 0)
        {
            return;
        }

        RefreshSpawnPointsIfNeeded();
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Brak spawn pointow (NetworkSpawnPoint). Nie mozna zespawnowac graczy.");
            return;
        }

        for (int i = pendingClients.Count - 1; i >= 0; i--)
        {
            ulong clientId = pendingClients[i];
            NetworkClient client;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out client))
            {
                pendingClients.RemoveAt(i);
                continue;
            }

            if (client.PlayerObject != null)
            {
                pendingClients.RemoveAt(i);
                continue;
            }

            int index = GetClientIndex(clientId);
            Vector3 spawnPosition = GetSpawnPosition(index);
            Quaternion spawnRotation = GetSpawnRotation(index);

            Debug.Log($"Spawn client {clientId} at index {index}: {spawnPosition}");

            GameObject prefab = GetPlayerPrefabForClient(clientId);
            if (prefab == null)
            {
                Debug.LogWarning("Brak prefabu gracza dla klienta/hosta.");
                pendingClients.RemoveAt(i);
                continue;
            }

            GameObject playerInstance = Instantiate(prefab, spawnPosition, spawnRotation);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogWarning("Player Prefab nie ma NetworkObject.");
                Destroy(playerInstance);
                pendingClients.RemoveAt(i);
                continue;
            }

            // destroyWithScene=false: players must survive networked scene
            // transitions (e.g. Pustynia -> Odrealnienie via host pressing P).
            // With true, the player objects (and their cameras) are destroyed
            // when the scene unloads, leaving the new scene with no camera.
            networkObject.SpawnAsPlayerObject(clientId, false);
            Debug.Log($"Actual position for client {clientId}: {playerInstance.transform.position}");
            StartCoroutine(LogSpawnAfterDelay(playerInstance, clientId));
            pendingClients.RemoveAt(i);
        }
    }

    System.Collections.IEnumerator LogSpawnAfterDelay(GameObject playerInstance, ulong clientId)
    {
        yield return new WaitForSeconds(1f);

        if (playerInstance == null)
        {
            Debug.LogWarning($"Client {clientId} player object destroyed before delayed log.");
            yield break;
        }

        Transform child = playerInstance.transform.childCount > 0 ? playerInstance.transform.GetChild(0) : null;
        Vector3 childLocalPos = child != null ? child.localPosition : Vector3.zero;
        Debug.Log($"Delayed position for client {clientId}: {playerInstance.transform.position} | child local: {childLocalPos}");
    }

    void RegisterNetworkPrefabs()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        TryAddNetworkPrefab(hostPlayerPrefab);

        if (clientPlayerPrefab != hostPlayerPrefab)
        {
            TryAddNetworkPrefab(clientPlayerPrefab);
        }
    }

    void TryAddNetworkPrefab(GameObject prefab)
    {
        if (prefab == null || NetworkManager.Singleton == null)
        {
            return;
        }

        if (NetworkManager.Singleton.NetworkConfig != null &&
            NetworkManager.Singleton.NetworkConfig.Prefabs != null &&
            NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(prefab))
        {
            return;
        }

        try
        {
            NetworkManager.Singleton.AddNetworkPrefab(prefab);
        }
        catch (System.Exception)
        {
            Debug.LogWarning("Nie mozna dodac NetworkPrefab (prawdopodobnie juz dodany lub brak NetworkObject).");
        }
    }

    GameObject GetPlayerPrefabForClient(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId)
        {
            if (hostPlayerPrefab != null)
            {
                return hostPlayerPrefab;
            }
        }
        else
        {
            if (clientPlayerPrefab != null)
            {
                return clientPlayerPrefab;
            }
        }

        return NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
    }

    Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return Vector3.zero;
        }

        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        return spawnPoints[clamped].position;
    }

    Quaternion GetSpawnRotation(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return Quaternion.identity;
        }

        int clamped = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        return spawnPoints[clamped].rotation;
    }

    int GetClientIndex(ulong clientId)
    {
        List<ulong> ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        ids.Sort();
        int index = ids.IndexOf(clientId);
        return index < 0 ? 0 : index;
    }
}
