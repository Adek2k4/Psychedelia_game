using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// Server-authoritative controller for the boss arena ("Odrealnienie").
// Repositions players onto spawn points when the scene loads, tracks the
// down state of both players (all downed -> game over), reacts to the boss
// being defeated (victory), and lets the host restart the fight.
public class BossArenaController : NetworkBehaviour
{
    public enum ArenaState
    {
        Fighting,
        GameOver,
        Victory
    }

    public static BossArenaController Instance { get; private set; }

    [Header("Scene")]
    public string arenaSceneName = "Odrealnienie";

    [Header("Spawn")]
    public Transform[] playerSpawnPoints;   // optional; auto-discovered from NetworkSpawnPoint if empty
    public BossController boss;             // in-scene boss; auto-found if null
    public Transform bossLookTarget;        // players are rotated to face this on spawn

    [Header("Setup")]
    public float setupTimeout = 8f;

    public readonly NetworkVariable<ArenaState> State =
        new NetworkVariable<ArenaState>(ArenaState.Fighting);

    private AudioSource musicSource;

    // Clients already snapped to their spawn point. The retry loop in
    // SetupWhenReady only (re)positions players NOT in this set, so a player
    // that spawned correctly on the first pass isn't repeatedly teleported
    // back to the spawn point while a late-joining teammate is still loading.
    private readonly HashSet<ulong> positionedClients = new HashSet<ulong>();

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (boss == null)
        {
            boss = FindObjectByType<BossController>();
        }

        if (bossLookTarget == null && boss != null)
        {
            bossLookTarget = boss.transform;
        }

        // Background music runs locally on every peer; stops when the fight ends.
        SetupMusic();
        State.OnValueChanged += HandleStateChanged;

        if (IsServer)
        {
            State.Value = ArenaState.Fighting;

            if (boss != null)
            {
                boss.OnBossDefeated += HandleBossDefeated;
            }

            // Reposition once the networked scene load fully completes for all
            // clients (the reliable moment), plus a retry window as a fallback
            // for clients that finish loading slightly late — otherwise a late
            // client keeps its stale (desert) position and floats above the map.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            }

            StartCoroutine(SetupWhenReady());
        }
    }

    void SetupMusic()
    {
        AudioClip clip = BossAudio.Load("boss_music");
        if (clip == null)
        {
            return;
        }

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = 0.4f;
        musicSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.Play();
    }

    void HandleStateChanged(ArenaState previous, ArenaState current)
    {
        if (current != ArenaState.Fighting && musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public override void OnNetworkDespawn()
    {
        State.OnValueChanged -= HandleStateChanged;
        if (musicSource != null)
        {
            musicSource.Stop();
        }

        if (IsServer && boss != null)
        {
            boss.OnBossDefeated -= HandleBossDefeated;
        }

        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
        }
    }

    void HandleLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer)
        {
            return;
        }

        RefreshSpawnPointsIfNeeded();
        PositionAllPlayers();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    static T FindObjectByType<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    IEnumerator SetupWhenReady()
    {
        float elapsed = 0f;
        while (elapsed < setupTimeout)
        {
            if (AllPlayersReady())
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        RefreshSpawnPointsIfNeeded();

        // Re-apply placement a few times over the first couple of seconds so a
        // late-loading client gets snapped to its spawn point even if its player
        // object wasn't ready when the first teleport was sent.
        float[] retryDelays = { 0f, 0.4f, 1f, 2f };
        float start = Time.time;
        int next = 0;
        while (next < retryDelays.Length)
        {
            if (Time.time - start >= retryDelays[next])
            {
                PositionAllPlayers();
                next++;
            }

            yield return null;
        }
    }

    bool AllPlayersReady()
    {
        if (NetworkManager.Singleton == null)
        {
            return false;
        }

        int expected = Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsList.Count);
        int ready = 0;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client != null && client.PlayerObject != null)
            {
                ready++;
            }
        }

        return ready >= expected;
    }

    void PositionAllPlayers()
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        List<ulong> ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        ids.Sort();

        for (int i = 0; i < ids.Count; i++)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(ids[i], out NetworkClient client))
            {
                continue;
            }

            if (client.PlayerObject == null)
            {
                continue;
            }

            if (positionedClients.Contains(ids[i]))
            {
                continue;
            }

            Vector3 pos = GetSpawnPosition(i);
            Quaternion rot = GetSpawnRotation(i, pos);

            PlayerDownState down = client.PlayerObject.GetComponent<PlayerDownState>();
            if (down != null)
            {
                down.ResetServer();
                down.TeleportServer(pos, rot);
            }
            else
            {
                client.PlayerObject.transform.SetPositionAndRotation(pos, rot);
            }

            positionedClients.Add(ids[i]);
        }
    }

    Vector3 GetSpawnPosition(int index)
    {
        if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
        {
            int clamped = Mathf.Clamp(index, 0, playerSpawnPoints.Length - 1);
            if (playerSpawnPoints[clamped] != null)
            {
                return playerSpawnPoints[clamped].position;
            }
        }

        // Fallback: spread players in front of the controller.
        return transform.position + transform.right * (index * 3f - 1.5f);
    }

    Quaternion GetSpawnRotation(int index, Vector3 fromPos)
    {
        if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
        {
            int clamped = Mathf.Clamp(index, 0, playerSpawnPoints.Length - 1);
            if (playerSpawnPoints[clamped] != null)
            {
                return playerSpawnPoints[clamped].rotation;
            }
        }

        if (bossLookTarget != null)
        {
            Vector3 toBoss = bossLookTarget.position - fromPos;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude > 0.001f)
            {
                return Quaternion.LookRotation(toBoss.normalized, Vector3.up);
            }
        }

        return Quaternion.identity;
    }

    void RefreshSpawnPointsIfNeeded()
    {
        bool hasValid = false;
        if (playerSpawnPoints != null)
        {
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                if (playerSpawnPoints[i] != null)
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

        NetworkSpawnPoint[] points = Object.FindObjectsByType<NetworkSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        playerSpawnPoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            playerSpawnPoints[i] = points[i].transform;
        }
    }

    // Called by PlayerDownState on the server whenever a player's down state changes.
    public void NotifyDownStateChangedServer()
    {
        if (!IsServer || State.Value != ArenaState.Fighting)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        int players = 0;
        bool anyAlive = false;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            PlayerDownState down = client.PlayerObject.GetComponent<PlayerDownState>();
            if (down == null)
            {
                continue;
            }

            players++;
            if (!down.IsDowned)
            {
                anyAlive = true;
            }
        }

        if (players > 0 && !anyAlive)
        {
            State.Value = ArenaState.GameOver;
            if (boss != null)
            {
                boss.StopAttacksServer();
            }
        }
    }

    void HandleBossDefeated()
    {
        if (!IsServer)
        {
            return;
        }

        if (State.Value == ArenaState.Fighting)
        {
            State.Value = ArenaState.Victory;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RetryServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only the host (server's own client) may restart.
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
        {
            return;
        }

        if (State.Value == ArenaState.Fighting)
        {
            return;
        }

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
        {
            return;
        }

        // Reloading the arena scene resets the boss and all in-scene state cleanly.
        NetworkManager.Singleton.SceneManager.LoadScene(arenaSceneName, LoadSceneMode.Single);
    }
}
