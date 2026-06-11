using UnityEngine;

// Purely cosmetic idle motion for the boss: a gentle bob, sway and turn around
// its authored pose. It is deterministic (driven by Time.time), so every client
// reproduces roughly the same movement without needing a NetworkTransform on the
// boss. Oscillates around the local transform captured at startup, so it does not
// fight the boss's placed position/rotation.
public class BossIdleMotion : MonoBehaviour
{
    [Header("Bob (vertical)")]
    public float bobAmplitude = 0.4f;
    public float bobFrequency = 0.5f;

    [Header("Sway (roll) / Turn (yaw), degrees")]
    public float swayAngle = 5f;
    public float swayFrequency = 0.35f;
    public float turnAngle = 6f;
    public float turnFrequency = 0.25f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private bool captured;

    void OnEnable()
    {
        Capture();
    }

    void Capture()
    {
        if (captured)
        {
            return;
        }

        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        captured = true;
    }

    void Update()
    {
        if (!captured)
        {
            Capture();
        }

        float t = Time.time;

        float bob = Mathf.Sin(t * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.localPosition = baseLocalPosition + Vector3.up * bob;

        float roll = Mathf.Sin(t * swayFrequency * Mathf.PI * 2f) * swayAngle;
        float yaw = Mathf.Sin(t * turnFrequency * Mathf.PI * 2f) * turnAngle;
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, yaw, roll);
    }
}
