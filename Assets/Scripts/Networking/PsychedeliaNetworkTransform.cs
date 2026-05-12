using UnityEngine;
using Unity.Netcode.Components;

public class PsychedeliaNetworkTransform : NetworkTransform
{
    public float serverAuthoritySeconds = 1f;

    private float spawnTime;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        spawnTime = Time.time;
    }

    protected override bool OnIsServerAuthoritative()
    {
        if (!IsSpawned)
        {
            return base.OnIsServerAuthoritative();
        }

        if (Time.time - spawnTime < serverAuthoritySeconds)
        {
            return true;
        }

        return base.OnIsServerAuthoritative();
    }
}
