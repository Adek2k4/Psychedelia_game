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
    [Tooltip("Rooty, pod którymi szukamy materiałów z naszym shaderem.")]
    public Transform[] kaleidoscopeRoots;

    [Tooltip("Nazwa shadera, którego szukamy.")]
    public string kaleidoscopeShaderName = "Custom/KaleidoscopeTextureMorph";

    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private Coroutine despawnRoutine;

    public override void OnNetworkDespawn()
    {
        // Ustaw preset NA INNYCH OBIEKTACH, zanim ten przedmiot zniknie
        ApplyKaleidoscopePresetOnRoots();

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
            readyClients.Add(sender);
        else
            readyClients.Remove(sender);

        if (readyClients.Count >= requiredPlayers)
        {
            if (despawnRoutine == null)
                despawnRoutine = StartCoroutine(DespawnAfterDelay());
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
            NetworkObject.Despawn(true);
        }

        despawnRoutine = null;
    }

    // -------------------------------------------------------
    // Ustawianie presetów na wskazanych rootach
    // -------------------------------------------------------
    void ApplyKaleidoscopePresetOnRoots()
    {
        if (kaleidoscopeRoots == null) return;

        foreach (var root in kaleidoscopeRoots)
        {
            if (root == null) continue;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // TYLKO instancje materiałów – nie dotykamy assetów.[web:101][web:104]
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

            // preset z obrazka
            mat.SetFloat("_AnimationSpeed",         0.63f);
            mat.SetFloat("_Scale",                  1.0f);
            mat.SetFloat("_FractalNormalInfluence", 1.02f);
            mat.SetFloat("_FractalOpacity",         0.218f);
            mat.SetFloat("_BlendMode",              2.0f);   // Lerp

            mat.SetFloat("_WaveTexEnable",  1.0f);
            mat.SetFloat("_WaveNormEnable", 1.0f);

            mat.SetFloat("_WaveStrength", 0.07f);
            mat.SetFloat("_WaveSpeed",    0.25f);
            mat.SetFloat("_WaveFreqMin",  2.4f);
            mat.SetFloat("_WaveFreqMax",  17.9f);
            mat.SetFloat("_WaveAmpMin",   0.034f);
            mat.SetFloat("_WaveAmpMax",   0.095f);

            mat.SetFloat("_ShadowWaveStrength", 0.144f);
            mat.SetFloat("_ShadowWaveSpeed",    0.74f);
            mat.SetFloat("_ShadowWaveFreqMin",  5.3f);
            mat.SetFloat("_ShadowWaveFreqMax",  8.1f);
            mat.SetFloat("_ShadowWaveAmpMin",   0.144f);
            mat.SetFloat("_ShadowWaveAmpMax",   0.152f);
        }
    }
}