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
    public Vector3[] spawnPositions = new Vector3[]
    {
        new Vector3(19.12333f, 0.63f, 80.12822f),
        new Vector3(7.44f, 0.63f, 80.12822f)
    };
    public float fallbackSpacing = 4f;

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

            networkObject.SpawnAsPlayerObject(clientId, true);
            pendingClients.RemoveAt(i);
        }
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
        if (spawnPoints != null && index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null)
        {
            return spawnPoints[index].position;
        }

        if (spawnPositions != null && index >= 0 && index < spawnPositions.Length)
        {
            return spawnPositions[index];
        }

        return new Vector3(index * fallbackSpacing, 0f, 0f);
    }

    Quaternion GetSpawnRotation(int index)
    {
        if (spawnPoints != null && index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null)
        {
            return spawnPoints[index].rotation;
        }

        return Quaternion.identity;
    }

    int GetClientIndex(ulong clientId)
    {
        int index = 0;
        foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (id == clientId)
            {
                return index;
            }

            index++;
        }

        return 0;
    }
}
