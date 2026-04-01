using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Krzak : MonoBehaviour
{
    public float kickForce = 8f;
    public float liftForce = 1.2f;
    public float spinTorque = 4f;
    public float minKickInterval = 0.1f;
    public float referencePlayerSpeed = 6f;
    public float maxKickMultiplier = 2f;
    public float stationaryThreshold = 0.1f;
    public float movementDamping = 1.1f;
    public float spinDamping = 1.5f;
    public float stopEpsilon = 0.03f;

    private Rigidbody rb;
    private float lastKickTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb.linearVelocity.sqrMagnitude > 0f)
        {
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, movementDamping * Time.fixedDeltaTime);
            if (rb.linearVelocity.sqrMagnitude < stopEpsilon * stopEpsilon)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        if (rb.angularVelocity.sqrMagnitude > 0f)
        {
            rb.angularVelocity = Vector3.MoveTowards(rb.angularVelocity, Vector3.zero, spinDamping * Time.fixedDeltaTime);
            if (rb.angularVelocity.sqrMagnitude < stopEpsilon * stopEpsilon)
            {
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        TryKick(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryKick(other);
    }

    void TryKick(Collider other)
    {
        if (Time.time - lastKickTime < minKickInterval)
        {
            return;
        }

        Vector3 moveDirection = GetPlayerMoveDirection(other);
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        KickFromWorldDirection(moveDirection);
    }

    public void KickFromWorldDirection(Vector3 worldDirection)
    {
        if (Time.time - lastKickTime < minKickInterval)
        {
            return;
        }

        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        float playerSpeed = flatDirection.magnitude;
        if (playerSpeed < stationaryThreshold)
        {
            return;
        }

        lastKickTime = Time.time;

        float speedNormalized = referencePlayerSpeed > 0f ? playerSpeed / referencePlayerSpeed : 1f;
        float kickMultiplier = Mathf.Clamp(speedNormalized, 0f, maxKickMultiplier);

        Vector3 kickDirection = (flatDirection.normalized + Vector3.up * liftForce).normalized;
        rb.AddForce(kickDirection * (kickForce * kickMultiplier), ForceMode.Impulse);

        Vector3 spinAxis = Vector3.Cross(Vector3.up, flatDirection.normalized);
        if (spinAxis.sqrMagnitude > 0.0001f)
        {
            rb.AddTorque(spinAxis.normalized * (spinTorque * kickMultiplier), ForceMode.Impulse);
        }
    }

    public void StopMovement()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    Vector3 GetPlayerMoveDirection(Collider other)
    {
        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb != null && otherRb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 velocity = otherRb.linearVelocity;
            velocity.y = 0f;
            return velocity;
        }

        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller != null)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                return velocity;
            }
        }

        return Vector3.zero;
    }
}
