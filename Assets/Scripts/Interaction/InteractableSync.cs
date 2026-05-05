using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class InteractableSync : NetworkBehaviour
{
    public int requiredPlayers = 2;
    public float despawnDelay = 1f;

    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private Coroutine despawnRoutine;

    public override void OnNetworkDespawn()
    {
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
        {
            readyClients.Add(sender);
        }
        else
        {
            readyClients.Remove(sender);
        }

        if (readyClients.Count >= requiredPlayers)
        {
            if (despawnRoutine == null)
            {
                despawnRoutine = StartCoroutine(DespawnAfterDelay());
            }
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
}
