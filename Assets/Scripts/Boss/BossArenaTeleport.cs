using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// Placed in the desert scene (Pustynia). When the host presses the trigger key
// (default P), both players are taken to the boss arena via the networked
// SceneManager, so every connected client follows automatically.
public class BossArenaTeleport : MonoBehaviour
{
    public string arenaSceneName = "Odrealnienie";
    public KeyCode triggerKey = KeyCode.P;

    void Update()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
        {
            return;
        }

        if (!Input.GetKeyDown(triggerKey))
        {
            return;
        }

        TeleportToArena(arenaSceneName);
    }

    // Server-only. Loads the boss arena via the networked SceneManager so every
    // connected client follows. Shared by the manual P-key trigger and by
    // ZabaCounterManager once the frog count reaches its target.
    public static void TeleportToArena(string arenaSceneName)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(arenaSceneName))
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == arenaSceneName)
        {
            return;
        }

        if (networkManager.SceneManager == null)
        {
            Debug.LogWarning("BossArenaTeleport: SceneManager niedostępny (scene management wyłączony?).");
            return;
        }

        networkManager.SceneManager.LoadScene(arenaSceneName, LoadSceneMode.Single);
    }
}
