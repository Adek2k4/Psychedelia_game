using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// The arena boss (the "Human" model). Server-authoritative:
//  - holds health, takes damage from ShotgunBall projectiles,
//  - periodically telegraphs and fires a laser at a random non-downed player,
//  - downs the targeted player on hit (the partner can revive them).
// The laser visual is a runtime LineRenderer driven over ClientRpc.
[RequireComponent(typeof(NetworkObject))]
public class BossController : NetworkBehaviour
{
    [Header("Health")]
    public float maxHealth = 600f;

    [Header("Attack")]
    public float firstAttackDelay = 2f;
    public float attackInterval = 2f;
    public float telegraphTime = 1f;   // window to walk off the locked aim point
    public float fireHoldTime = 0.3f;    // beam stays lit on the locked point before the hit resolves
    public float laserHitRadius = 2.5f;  // how far the player can be from the locked point and still get hit
    public Transform muzzle;             // laser origin; defaults to top of the boss

    [Header("Laser visual")]
    public Color telegraphColor = new Color(1f, 0.2f, 0.6f, 1f);
    public Color fireColor = new Color(0.4f, 1f, 1f, 1f);
    public float beamWidth = 0.25f;

    public readonly NetworkVariable<float> Health = new NetworkVariable<float>(1f);

    public event System.Action OnBossDefeated;

    public bool IsDead => dead;

    private bool attacksActive = true;
    private bool dead;
    private float attackTimer;
    private LineRenderer beam;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Health.Value = Mathf.Max(1f, maxHealth);
            attackTimer = Mathf.Max(0f, firstAttackDelay);
        }
    }

    void Update()
    {
        if (!IsServer || dead || !attacksActive)
        {
            return;
        }

        if (BossArenaController.Instance == null ||
            BossArenaController.Instance.State.Value != BossArenaController.ArenaState.Fighting)
        {
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.5f, attackInterval);
            StartCoroutine(FireLaserRoutine());
        }
    }

    IEnumerator FireLaserRoutine()
    {
        PlayerDownState target = PickTarget();
        if (target == null)
        {
            yield break;
        }

        // Lock the aim point NOW (where the player currently is). The beam will
        // point at this fixed spot for the whole telegraph and shot, so the
        // player has the telegraph window to simply walk off it and dodge. The
        // beam never tracks the player once locked.
        Vector3 lockPoint = AimPoint(target);

        float t = 0f;
        while (t < telegraphTime)
        {
            if (dead || !attacksActive)
            {
                HideLaserClientRpc();
                yield break;
            }

            ShowLaserClientRpc(GetMuzzlePosition(), lockPoint, true);
            t += Time.deltaTime;
            yield return null;
        }

        if (dead || !attacksActive)
        {
            HideLaserClientRpc();
            yield break;
        }

        // Fire at the locked point (still not following the player).
        Vector3 fireOrigin = GetMuzzlePosition();
        ShowLaserClientRpc(fireOrigin, lockPoint, false);
        PlayLaserClientRpc(fireOrigin);

        if (fireHoldTime > 0f)
        {
            yield return new WaitForSeconds(fireHoldTime);
        }

        HideLaserClientRpc();

        if (target == null || target.IsDowned || dead)
        {
            yield break;
        }

        // Hit only if the player is still near where the beam locked on.
        float dist = Vector3.Distance(target.transform.position + Vector3.up, lockPoint);
        if (dist <= Mathf.Max(0.1f, laserHitRadius))
        {
            target.TakeHitServer();
        }
    }

    PlayerDownState PickTarget()
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        List<PlayerDownState> candidates = new List<PlayerDownState>();
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            PlayerDownState down = client.PlayerObject.GetComponent<PlayerDownState>();
            if (down != null && !down.IsDowned)
            {
                candidates.Add(down);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    static Vector3 AimPoint(PlayerDownState target)
    {
        return target.transform.position + Vector3.up;
    }

    // Laser origin: near the boss's eyes (top-front of its bounds), computed
    // from the live renderer bounds so it adapts to scale/orientation instead of
    // relying on a hand-placed muzzle (which ended up at crotch height after the
    // stand-up rotation).
    Vector3 GetMuzzlePosition()
    {
        Bounds b = GetBossBounds();
        float eyeHeight = Mathf.Lerp(b.center.y, b.max.y, 0.7f);
        Vector3 eyes = new Vector3(b.center.x, eyeHeight, b.center.z);
        return eyes + transform.forward * b.extents.z; // push to the front of the head
    }

    Bounds GetBossBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool has = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            // Skip the runtime laser LineRenderer (and any non-mesh renderers) so
            // the beam doesn't blow up the computed bounds / eye position.
            if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))
            {
                continue;
            }

            if (!has)
            {
                b = renderers[i].bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(renderers[i].bounds);
            }
        }

        if (!has)
        {
            return new Bounds(transform.position + Vector3.up * 2.5f, Vector3.one * 2f);
        }

        return b;
    }

    // Called by ShotgunBall on the server when it hits the boss.
    public void ApplyDamageServer(float damage)
    {
        if (!IsServer || dead)
        {
            return;
        }

        Health.Value = Mathf.Max(0f, Health.Value - Mathf.Max(0f, damage));
        PlayHurtClientRpc();
        if (Health.Value <= 0f)
        {
            DieServer();
        }
    }

    [ClientRpc]
    void PlayHurtClientRpc()
    {
        BossAudio.PlayAt("boss_hurt", transform.position, 1f);
    }

    [ClientRpc]
    void PlayLaserClientRpc(Vector3 origin)
    {
        BossAudio.PlayAt("laser", origin, 0.9f);
    }

    void DieServer()
    {
        dead = true;
        attacksActive = false;
        HideLaserClientRpc();
        PlayDeathClientRpc();
        OnBossDefeated?.Invoke();
    }

    public void StopAttacksServer()
    {
        attacksActive = false;
        HideLaserClientRpc();
    }

    public void ResumeAttacksServer()
    {
        if (!dead)
        {
            attacksActive = true;
        }
    }

    [ClientRpc]
    void ShowLaserClientRpc(Vector3 from, Vector3 to, bool telegraph)
    {
        EnsureBeam();
        if (beam == null)
        {
            return;
        }

        Color c = telegraph ? telegraphColor : fireColor;
        float width = telegraph ? beamWidth * 0.5f : beamWidth;
        beam.startColor = c;
        beam.endColor = c;
        beam.startWidth = width;
        beam.endWidth = width;
        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        beam.enabled = true;
    }

    [ClientRpc]
    void HideLaserClientRpc()
    {
        if (beam != null)
        {
            beam.enabled = false;
        }
    }

    [ClientRpc]
    void PlayDeathClientRpc()
    {
        // Minimal death feedback; replace with VFX/animation as desired.
        if (beam != null)
        {
            beam.enabled = false;
        }
    }

    void EnsureBeam()
    {
        if (beam != null)
        {
            return;
        }

        GameObject go = new GameObject("BossLaserBeam");
        go.transform.SetParent(transform, false);
        beam = go.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.numCapVertices = 4;
        beam.textureMode = LineTextureMode.Stretch;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            beam.material = new Material(shader);
        }

        beam.enabled = false;
    }

    void OnDestroy()
    {
        if (beam != null && beam.material != null)
        {
            Destroy(beam.material);
        }
    }
}
