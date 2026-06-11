using UnityEngine;
using Unity.Netcode;

// Owner-only shotgun. Left-click fires a single large networked ball
// (ShotgunBall). The model + weapon are only active inside the boss arena
// (detected via BossArenaController.Instance). Firing is server-authoritative:
// the client requests a shot, the server spawns and owns the projectile.
public class ShotgunWeapon : NetworkBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public GameObject projectilePrefab;   // ShotgunBall prefab (registered network prefab)
    public GameObject shotgunModel;        // visual under the camera
    public Transform muzzle;               // optional spawn origin

    [Header("Tuning")]
    public float fireCooldown = 0.8f;
    public float projectileSpeed = 35f;
    public float projectileDamage = 12f;
    public float spawnDistance = 1.4f;

    private float lastFireTime = -999f;
    private PlayerDownState downState;

    void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        downState = GetComponent<PlayerDownState>();
    }

    public override void OnNetworkSpawn()
    {
        // Note: not owner-gated. The component stays enabled on every client so
        // the model is shown for all players; only the firing input is owner-only.
        SetModelVisible(false);
    }

    void Update()
    {
        // Model visibility is evaluated on every client so both players can see
        // each other's shotgun while in the arena.
        bool inArena = BossArenaController.Instance != null;
        SetModelVisible(inArena);

        if (!IsOwner || playerCamera == null)
        {
            return;
        }

        if (!inArena)
        {
            return;
        }

        if (downState != null && downState.IsDowned)
        {
            return;
        }

        if (Cursor.visible)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && Time.time - lastFireTime >= fireCooldown)
        {
            lastFireTime = Time.time;

            Vector3 camForward = playerCamera.transform.forward;
            Vector3 spawnPos = (muzzle != null ? muzzle.position : playerCamera.transform.position) + camForward * spawnDistance;
            FireServerRpc(spawnPos, camForward);
        }
    }

    void SetModelVisible(bool visible)
    {
        if (shotgunModel != null && shotgunModel.activeSelf != visible)
        {
            shotgunModel.SetActive(visible);
        }
    }

    [ServerRpc]
    void FireServerRpc(Vector3 spawnPos, Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            return;
        }

        Quaternion rot = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;

        GameObject ball = Instantiate(projectilePrefab, spawnPos, rot);

        ShotgunBall shot = ball.GetComponent<ShotgunBall>();
        if (shot != null)
        {
            shot.InitializeServer(direction, projectileSpeed, projectileDamage);
        }

        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Destroy(ball);
            return;
        }

        netObj.Spawn(true);
    }
}
