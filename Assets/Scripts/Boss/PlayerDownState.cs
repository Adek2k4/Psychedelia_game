using UnityEngine;
using Unity.Netcode;

// Per-player networked "downed" state for the boss fight.
//  - The boss downs a player via DownServer().
//  - A downed player can't move or shoot; the partner walks up and holds E
//    to revive them (ReviveServerRpc). No bleed-out: a downed player stays
//    down until revived or until both are down (game over, handled by the
//    BossArenaController).
// Also provides a server->owner teleport used by the controller to place
// players on spawn points after the networked scene load.
public class PlayerDownState : NetworkBehaviour
{
    [Header("Combat")]
    public int maxHits = 2; // hits before going down (player survives the first hit)

    [Header("Revive")]
    public float reviveDistance = 2.5f;
    public float reviveDuration = 3f;
    public KeyCode reviveKey = KeyCode.E;

    [Header("UI")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;
    public string downedText = "Jesteś powalony — czekaj na pomoc";
    public string revivePromptText = "[E] podnieś gracza";
    public Color downedTint = new Color(0.5f, 0f, 0.1f, 0.35f);

    public readonly NetworkVariable<bool> Downed = new NetworkVariable<bool>(false);
    public bool IsDowned => Downed.Value;

    private PlayerMovement movement;
    private ShotgunWeapon weapon;
    private CharacterController characterController;
    private Camera playerCamera;

    private int hitsTaken;
    private float hitFlashEndTime = -999f;
    private bool wasArenaEnded;
    private float reviveProgress;
    private PlayerDownState reviveTarget;
    private static PlayerDownState[] cachedPlayers;
    private static float cacheTime = -999f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        weapon = GetComponent<ShotgunWeapon>();
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        Downed.OnValueChanged += HandleDownedChanged;
        ApplyDownedLocal(Downed.Value);
    }

    public override void OnNetworkDespawn()
    {
        Downed.OnValueChanged -= HandleDownedChanged;
    }

    // ---- Server API ----

    // Boss laser hit. The player survives the first hit(s) and only goes down on
    // the maxHits-th hit. A non-downing hit flashes the screen for the owner.
    public void TakeHitServer()
    {
        if (!IsServer || Downed.Value)
        {
            return;
        }

        hitsTaken++;
        if (hitsTaken >= Mathf.Max(1, maxHits))
        {
            DownServer();
        }
        else
        {
            HitFlashClientRpc(OwnerOnly());
            PlayHurtSoundClientRpc();
        }
    }

    [ClientRpc]
    void PlayHurtSoundClientRpc()
    {
        BossAudio.PlayAt("player_hurt", transform.position, 1f);
    }

    public void DownServer()
    {
        if (!IsServer || Downed.Value)
        {
            return;
        }

        Downed.Value = true;
        HitFlashClientRpc(OwnerOnly());
        PlayHurtSoundClientRpc();
        if (BossArenaController.Instance != null)
        {
            BossArenaController.Instance.NotifyDownStateChangedServer();
        }
    }

    public void ResetServer()
    {
        if (!IsServer)
        {
            return;
        }

        hitsTaken = 0;
        Downed.Value = false;
    }

    ClientRpcParams OwnerOnly()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
    }

    [ClientRpc]
    void HitFlashClientRpc(ClientRpcParams rpcParams = default)
    {
        hitFlashEndTime = Time.time + 0.3f;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReviveServerRpc()
    {
        if (!Downed.Value)
        {
            return;
        }

        hitsTaken = 0;
        Downed.Value = false;
        if (BossArenaController.Instance != null)
        {
            BossArenaController.Instance.NotifyDownStateChangedServer();
        }
    }

    public void TeleportServer(Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
        {
            return;
        }

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        TeleportClientRpc(position, rotation, rpcParams);
    }

    [ClientRpc]
    void TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        bool hadController = characterController != null && characterController.enabled;
        if (hadController)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (hadController)
        {
            characterController.enabled = true;
        }
    }

    // ---- Local state application ----

    void HandleDownedChanged(bool previous, bool current)
    {
        ApplyDownedLocal(current);
    }

    void ApplyDownedLocal(bool downed)
    {
        if (IsOwner && movement != null)
        {
            movement.SetInputEnabled(!downed);
        }

        if (!downed)
        {
            reviveProgress = 0f;
            reviveTarget = null;
        }
    }

    void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        // On game over / victory, stop controlling the player and free the mouse
        // so the host can actually click "Spróbuj jeszcze raz". Restore control
        // when the fight resumes (after a retry).
        BossArenaController arena = BossArenaController.Instance;
        bool ended = arena != null && arena.State.Value != BossArenaController.ArenaState.Fighting;
        if (ended)
        {
            if (movement != null)
            {
                movement.SetInputEnabled(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            reviveProgress = 0f;
            reviveTarget = null;
            wasArenaEnded = true;
            return;
        }

        if (wasArenaEnded)
        {
            wasArenaEnded = false;
            if (!Downed.Value && movement != null)
            {
                movement.SetInputEnabled(true);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Downed.Value)
        {
            reviveProgress = 0f;
            reviveTarget = null;
            return;
        }

        PlayerDownState target = FindDownedTeammate();
        if (target == null)
        {
            reviveProgress = 0f;
            reviveTarget = null;
            return;
        }

        if (Input.GetKey(reviveKey) && !Cursor.visible)
        {
            if (reviveTarget != target)
            {
                reviveTarget = target;
                reviveProgress = 0f;
            }

            reviveProgress += Time.deltaTime;
            if (reviveProgress >= Mathf.Max(0.1f, reviveDuration))
            {
                target.ReviveServerRpc();
                reviveProgress = 0f;
                reviveTarget = null;
            }
        }
        else
        {
            reviveTarget = target;
            reviveProgress = 0f;
        }
    }

    PlayerDownState FindDownedTeammate()
    {
        if (Time.time - cacheTime > 0.5f || cachedPlayers == null)
        {
            cachedPlayers = Object.FindObjectsByType<PlayerDownState>(FindObjectsSortMode.None);
            cacheTime = Time.time;
        }

        PlayerDownState best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            PlayerDownState other = cachedPlayers[i];
            if (other == null || other == this || !other.IsDowned)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist <= reviveDistance && dist < bestDist)
            {
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    void OnGUI()
    {
        if (!IsOwner)
        {
            return;
        }

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float fontScale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.7f, 1.4f);
        int fontSize = Mathf.RoundToInt(22f * fontScale);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.95f);
        style.clipping = TextClipping.Overflow;

        if (Downed.Value)
        {
            Color old = GUI.color;
            GUI.color = downedTint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            DrawCenteredLabel(Screen.width * 0.5f, Screen.height * 0.5f, downedText, style);
            return;
        }

        // Brief red flash when hit but not yet downed.
        if (Time.time < hitFlashEndTime)
        {
            Color old = GUI.color;
            float a = Mathf.Clamp01((hitFlashEndTime - Time.time) / 0.3f) * 0.4f;
            GUI.color = new Color(0.8f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        if (reviveTarget != null)
        {
            string text = revivePromptText;
            if (reviveProgress > 0f)
            {
                int percent = Mathf.RoundToInt(Mathf.Clamp01(reviveProgress / Mathf.Max(0.1f, reviveDuration)) * 100f);
                text = revivePromptText + "  " + percent + "%";
            }

            DrawCenteredLabel(Screen.width * 0.5f, Screen.height * 0.5f + 80f * fontScale, text, style);
        }
    }

    void DrawCenteredLabel(float centerX, float centerY, string text, GUIStyle style)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Vector2 size = style.CalcSize(new GUIContent(text));
        float height = Mathf.Max(size.y * 1.6f, style.fontSize * 1.4f);
        Rect rect = new Rect(centerX - size.x * 0.5f, centerY - height * 0.5f, size.x, height);
        GUI.Label(rect, text, style);
    }
}
