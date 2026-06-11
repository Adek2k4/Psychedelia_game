using UnityEngine;
using Unity.Netcode;

// A single large ball projectile fired by the shotgun.
//
// Movement is computed deterministically on EVERY peer from the spawn position,
// a networked direction/speed and the spawn time. This is why it now visibly
// flies on clients: it no longer depends on NetworkTransform replication (which
// left the ball looking like it just blipped at the muzzle before despawning).
// The server additionally resolves the hit on the boss and despawns the ball.
[RequireComponent(typeof(NetworkObject))]
public class ShotgunBall : NetworkBehaviour
{
    public float damage = 12f;
    public float lifetime = 5f;
    public float hitRadius = 0.6f;
    public LayerMask hitMask = ~0;

    // Hit detection is skipped for this long after spawn. Without it, the ball
    // can immediately overlap whatever is right at the muzzle (the shooter's
    // own collider, the gun model, an oversized boss collider, etc.) and get
    // consumed on the very first frame, which looks like the ball "appears for
    // a fraction of a second" and then vanishes.
    public float armingDelay = 0.08f;

    // Direction is taken from the synced spawn rotation (transform.forward), so
    // only the speed needs replicating. Set as the initial value before Spawn().
    private readonly NetworkVariable<float> netSpeed = new NetworkVariable<float>(35f);

    // Without a NetworkTransform, a freshly spawned NetworkObject appears at the
    // PREFAB's default transform on every client other than the server (the
    // instance's transform set by the server right before Spawn() is NOT sent
    // automatically). So the spawn position/rotation must be replicated
    // explicitly; every peer then applies them once on OnNetworkSpawn.
    private readonly NetworkVariable<Vector3> netStartPosition = new NetworkVariable<Vector3>(Vector3.zero);
    private readonly NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(Quaternion.identity);

    private Vector3 startPosition;
    private float startTime;
    private bool started;
    private float life;
    private bool consumed;

    // Called by ShotgunWeapon on the server right before Spawn().
    public void InitializeServer(Vector3 dir, float projectileSpeed, float projectileDamage)
    {
        netSpeed.Value = projectileSpeed;
        damage = projectileDamage;
        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        Quaternion rot = Quaternion.LookRotation(d, Vector3.up);
        transform.rotation = rot;

        netStartPosition.Value = transform.position;
        netRotation.Value = rot;
    }

    public override void OnNetworkSpawn()
    {
        startPosition = netStartPosition.Value;
        transform.SetPositionAndRotation(startPosition, netRotation.Value);
        startTime = Time.time;
        started = true;

        // Shot sound at the muzzle for everyone.
        BossAudio.PlayAt("gun_shot", startPosition, 0.8f);
    }

    Vector3 Direction()
    {
        // Constant after spawn (we never re-rotate the ball); synced via
        // netRotation, so every client agrees on the travel direction.
        return transform.forward;
    }

    void Update()
    {
        if (!started)
        {
            return;
        }

        Vector3 dir = Direction();
        float elapsed = Time.time - startTime;
        Vector3 newPos = startPosition + dir * (netSpeed.Value * elapsed);

        if (IsServer && !consumed)
        {
            Vector3 current = transform.position;
            float step = (newPos - current).magnitude;
            if (step > 0f && elapsed >= armingDelay)
            {
                // Only the boss matters: pass through the shooter, floor, pillars.
                RaycastHit[] hits = Physics.SphereCastAll(current, hitRadius, dir, step, hitMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    BossController boss = hits[i].collider.GetComponentInParent<BossController>();
                    if (boss != null)
                    {
                        Debug.Log("[ShotgunBall] hit BOSS collider: " + hits[i].collider.name + " -> consumed");
                        boss.ApplyDamageServer(damage);
                        Consume();
                        return;
                    }

                    // DEBUG: log anything else the ball hits, so we can see what's
                    // causing it to vanish if it's not the boss. Remove once fixed.
                    Debug.Log("[ShotgunBall] hit non-boss collider: " + hits[i].collider.name
                        + " (layer " + LayerMask.LayerToName(hits[i].collider.gameObject.layer) + ")");
                }
            }

            life += Time.deltaTime;
            if (life >= lifetime)
            {
                Debug.Log("[ShotgunBall] lifetime expired -> consumed");
                Consume();
                return;
            }
        }

        transform.position = newPos;
    }

    void Consume()
    {
        if (consumed)
        {
            return;
        }

        consumed = true;
        if (IsSpawned && NetworkObject != null)
        {
            NetworkObject.Despawn(true);
        }
    }
}
