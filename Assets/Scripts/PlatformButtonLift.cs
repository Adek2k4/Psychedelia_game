using UnityEngine;

public class PlatformButtonLift : MonoBehaviour
{
    public Transform button;
    public Collider zoneCollider;
    public Transform lift;
    public bool useLocalPositions = true;
    public float buttonPressedY = -0.539f;
    public float liftUpY = 0.39f;
    public float buttonMoveSpeed = 1.5f;
    public float liftMoveSpeed = 1.5f;
    public float activationDelaySeconds = 1f;
    public float liftDelaySeconds = 1f;
    public LayerMask playerMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    private float buttonStartY;
    private float liftStartY;
    private float pressedTime = 0f;
    private Collider buttonCollider;
    private float zoneActiveTime = 0f;
    private float liftProgress = 0f;

    void Awake()
    {
        if (button == null)
        {
            Transform found = transform.Find("przycisk");
            if (found != null)
            {
                button = found;
            }
        }

        if (lift == null)
        {
            Transform found = transform.Find("winda");
            if (found != null)
            {
                lift = found;
            }
        }

        if (button != null)
        {
            buttonStartY = GetY(button);
            buttonCollider = button.GetComponent<Collider>();
            if (buttonCollider == null)
            {
                buttonCollider = button.GetComponentInChildren<Collider>(true);
            }
        }

        if (zoneCollider == null)
        {
            Transform foundZone = transform.Find("strefa");
            if (foundZone != null)
            {
                zoneCollider = foundZone.GetComponent<Collider>();
                if (zoneCollider == null)
                {
                    zoneCollider = foundZone.GetComponentInChildren<Collider>(true);
                }
            }
        }

        if (lift != null)
        {
            liftStartY = GetY(lift);
        }
    }

    void Update()
    {
        if (button == null || lift == null)
        {
            return;
        }

        bool zoneActive = IsSomethingInZone();
        if (zoneActive)
        {
            zoneActiveTime += Time.deltaTime;
        }
        else
        {
            zoneActiveTime = 0f;
        }

        bool pressed = zoneActive && zoneActiveTime >= activationDelaySeconds;

        float targetButtonY = pressed ? buttonPressedY : buttonStartY;
        float currentButtonY = GetY(button);
        float buttonSpeed = buttonMoveSpeed * 0.1f;
        float nextButtonY = Mathf.MoveTowards(currentButtonY, targetButtonY, buttonSpeed * Time.deltaTime);
        SetY(button, nextButtonY);

        bool buttonAtTarget = Mathf.Abs(nextButtonY - buttonPressedY) <= 0.001f;
        if (pressed && buttonAtTarget)
        {
            pressedTime += Time.deltaTime;
        }
        else
        {
            pressedTime = 0f;
        }

        float liftDistance = Mathf.Abs(liftUpY - liftStartY);
        float liftSpeed = liftMoveSpeed * 0.25f;
        float targetProgress = (pressed && pressedTime >= liftDelaySeconds) ? 1f : 0f;
        if (liftDistance > 0.0001f)
        {
            float progressSpeed = (liftSpeed / liftDistance) * Time.deltaTime;
            liftProgress = Mathf.MoveTowards(liftProgress, targetProgress, progressSpeed);
        }
        else
        {
            liftProgress = targetProgress;
        }

        float eased = Mathf.SmoothStep(0f, 1f, liftProgress);
        float nextLiftY = Mathf.Lerp(liftStartY, liftUpY, eased);
        SetY(lift, nextLiftY);
    }

    bool IsSomethingInZone()
    {
        if (zoneCollider == null)
        {
            return false;
        }

        Bounds bounds = zoneCollider.bounds;
        Vector3 halfExtents = bounds.extents;
        if (halfExtents.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Collider[] hits = Physics.OverlapBox(bounds.center, halfExtents, zoneCollider.transform.rotation, playerMask, triggerInteraction);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            if (hits[i] == zoneCollider)
            {
                continue;
            }

            if (buttonCollider != null && hits[i] == buttonCollider)
            {
                continue;
            }

            if (button != null && hits[i].transform.IsChildOf(button))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    float GetY(Transform target)
    {
        return useLocalPositions ? target.localPosition.y : target.position.y;
    }

    void SetY(Transform target, float y)
    {
        if (useLocalPositions)
        {
            Vector3 pos = target.localPosition;
            pos.y = y;
            target.localPosition = pos;
        }
        else
        {
            Vector3 pos = target.position;
            pos.y = y;
            target.position = pos;
        }
    }
}
