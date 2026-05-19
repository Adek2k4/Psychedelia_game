using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class ZabaLocal : MonoBehaviour
{
    public float spawnSpinMin = 0.4f;
    public float spawnSpinMax = 1.2f;

    private Rigidbody rb;
    private Collider col;
    private NetworkObject netObj;
    private NetworkTransform netTransform;
    private NetworkRigidbody netRigidbody;
    private bool networkComponentsDisabled;
    private bool hasTouched;
    private bool launched;
    private ulong targetClientId;
    private float launchDelay;
    private float launchSpeed;
    private float despawnAfterLaunch;

    public void CacheComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (col == null)
        {
            col = GetComponent<Collider>();
        }

        if (!networkComponentsDisabled)
        {
            netObj = GetComponent<NetworkObject>();
            netTransform = GetComponent<NetworkTransform>();
            netRigidbody = GetComponent<NetworkRigidbody>();

            if (netTransform != null)
            {
                netTransform.enabled = false;
            }

            if (netRigidbody != null)
            {
                netRigidbody.enabled = false;
            }

            if (netObj != null)
            {
                netObj.enabled = false;
            }

            networkComponentsDisabled = true;
        }

        if (rb == null)
        {
            Debug.LogWarning($"ZabaLocal: Missing Rigidbody on {name}.");
        }

        if (col == null)
        {
            Debug.LogWarning($"ZabaLocal: Missing Collider on {name}.");
        }
    }

    public void Activate(Vector3 position, ulong clientId, float delay, float speed, float despawnAfter)
    {
        CacheComponents();

        StopAllCoroutines();
        hasTouched = false;
        launched = false;
        targetClientId = clientId;
        launchDelay = Mathf.Max(0f, delay);
        launchSpeed = Mathf.Max(0f, speed);
        despawnAfterLaunch = Mathf.Max(0f, despawnAfter);

        transform.position = position;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            float spinSpeed = Random.Range(spawnSpinMin, spawnSpinMax);
            if (spinSpeed > 0f)
            {
                rb.angularVelocity = Random.onUnitSphere * spinSpeed;
            }
        }

        gameObject.SetActive(true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (launched || hasTouched)
        {
            return;
        }

        hasTouched = true;
        StartCoroutine(LaunchAfterDelay());
    }

    void OnTriggerEnter(Collider other)
    {
        if (launched || hasTouched)
        {
            return;
        }

        hasTouched = true;
        StartCoroutine(LaunchAfterDelay());
    }

    IEnumerator LaunchAfterDelay()
    {
        if (launchDelay > 0f)
        {
            yield return new WaitForSeconds(launchDelay);
        }

        if (launched)
        {
            yield break;
        }

        launched = true;
        Vector3 awayDir = Vector3.forward;

        Transform player = GetPlayerTransform(targetClientId);
        if (player != null)
        {
            awayDir = transform.position - player.position;
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 0.0001f)
            {
                Vector2 rand = Random.insideUnitCircle.normalized;
                awayDir = new Vector3(rand.x, 0f, rand.y);
            }
            else
            {
                awayDir.Normalize();
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = awayDir * launchSpeed;
        }

        if (despawnAfterLaunch > 0f)
        {
            yield return new WaitForSeconds(despawnAfterLaunch);
        }

        gameObject.SetActive(false);
    }

    Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client != null && client.PlayerObject != null)
            {
                return client.PlayerObject.transform;
            }
        }

        return null;
    }
}
