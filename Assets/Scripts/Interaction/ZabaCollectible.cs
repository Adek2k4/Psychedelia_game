using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ZabaCollectible : NetworkBehaviour
{
    public int countValue = 1;
    public bool disableOnCollect = true;
    public string pickupSoundPath = "Sounds/Frog/pickup";
    public float pickupSoundVolume = 1f;
    public float collectFadeDuration = 0.5f;
    public float collectFloatDistance = 1f;

    private readonly NetworkVariable<bool> isCollected = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private Rigidbody[] cachedRigidbodies;
    private AudioSource[] cachedAudioSources;
    private FadeMaterial[] fadeMaterials;
    private Coroutine collectRoutine;
    private bool collectedVisualApplied;

    private static AudioClip pickupClip;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int DitherEnableId = Shader.PropertyToID("_DitherFadeEnable");

    struct FadeMaterial
    {
        public Material Material;
        public Color BaseColor;
        public int ColorId;
    }

    public bool IsCollected => isCollected.Value;

    void Awake()
    {
        CacheComponents();
    }

    public override void OnNetworkSpawn()
    {
        isCollected.OnValueChanged += HandleCollectedChanged;
        ApplyCollectedState(isCollected.Value, false);
    }

    public override void OnNetworkDespawn()
    {
        isCollected.OnValueChanged -= HandleCollectedChanged;
    }

    void CacheComponents()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        cachedAudioSources = GetComponentsInChildren<AudioSource>(true);
        CacheFadeMaterials();
    }

    void CacheFadeMaterials()
    {
        if (cachedRenderers == null)
        {
            fadeMaterials = null;
            return;
        }

        List<FadeMaterial> materials = new List<FadeMaterial>();
        foreach (var renderer in cachedRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] mats = renderer.materials;
            foreach (var mat in mats)
            {
                if (mat == null)
                {
                    continue;
                }

                int colorId = -1;
                if (mat.HasProperty(BaseColorId))
                {
                    colorId = BaseColorId;
                }
                else if (mat.HasProperty(ColorId))
                {
                    colorId = ColorId;
                }

                if (colorId == -1)
                {
                    continue;
                }

                if (mat.HasProperty(DitherEnableId))
                {
                    mat.SetFloat(DitherEnableId, 1f);
                }

                materials.Add(new FadeMaterial
                {
                    Material = mat,
                    BaseColor = mat.GetColor(colorId),
                    ColorId = colorId
                });
            }
        }

        fadeMaterials = materials.ToArray();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CollectServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isCollected.Value)
        {
            return;
        }

        ZabaCounterManager counter = ZabaCounterManager.FindInScene();
        if (counter == null || !counter.IsActive)
        {
            return;
        }

        isCollected.Value = true;
        counter.AddCountServer(countValue);
    }

    void HandleCollectedChanged(bool previous, bool current)
    {
        ApplyCollectedState(current, true);
    }

    void ApplyCollectedState(bool collected, bool animate)
    {
        if (!disableOnCollect)
        {
            return;
        }

        if (cachedRenderers == null || cachedColliders == null || cachedRigidbodies == null || cachedAudioSources == null || fadeMaterials == null)
        {
            CacheComponents();
        }

        if (!collected)
        {
            return;
        }

        if (collectedVisualApplied)
        {
            return;
        }

        collectedVisualApplied = true;

        if (cachedColliders != null)
        {
            foreach (var collider in cachedColliders)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        if (cachedRigidbodies != null)
        {
            foreach (var body in cachedRigidbodies)
            {
                if (body != null)
                {
                    body.isKinematic = true;
                }
            }
        }

        if (cachedAudioSources != null)
        {
            foreach (var source in cachedAudioSources)
            {
                if (source != null)
                {
                    source.Stop();
                    source.enabled = false;
                }
            }
        }

        if (collectRoutine != null)
        {
            StopCoroutine(collectRoutine);
        }

        if (animate)
        {
            collectRoutine = StartCoroutine(CollectRoutine());
        }
        else
        {
            SetFade(0f);
            SetRenderersEnabled(false);
        }
    }

    IEnumerator CollectRoutine()
    {
        PlayPickupSound();

        Vector3 startPos = transform.position;
        float duration = Mathf.Max(0.01f, collectFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;
            SetFade(alpha);
            transform.position = startPos + Vector3.up * (collectFloatDistance * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFade(0f);
        transform.position = startPos + Vector3.up * collectFloatDistance;
        SetRenderersEnabled(false);
    }

    void PlayPickupSound()
    {
        if (string.IsNullOrWhiteSpace(pickupSoundPath))
        {
            return;
        }

        if (pickupClip == null)
        {
            pickupClip = Resources.Load<AudioClip>(pickupSoundPath);
        }

        if (pickupClip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupSoundVolume);
    }

    void SetFade(float alpha)
    {
        if (fadeMaterials == null)
        {
            return;
        }

        float a = Mathf.Clamp01(alpha);
        foreach (var entry in fadeMaterials)
        {
            if (entry.Material == null)
            {
                continue;
            }

            Color col = entry.BaseColor;
            col.a *= a;
            entry.Material.SetColor(entry.ColorId, col);
        }
    }

    void SetRenderersEnabled(bool enabled)
    {
        if (cachedRenderers == null)
        {
            return;
        }

        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }
}
