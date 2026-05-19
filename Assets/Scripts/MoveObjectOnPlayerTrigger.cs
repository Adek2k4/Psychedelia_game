using UnityEngine;

public class MoveObjectOnPlayerTrigger : MonoBehaviour
{
    public bool useLocalPosition = true;
    public float targetY = -5f;
    public float moveSpeed = 1.5f;
    public float activationDelaySeconds = 1f;

    private float startY;
    private float insideTime = 0f;
    private int playerInsideCount = 0;

    void Awake()
    {
        startY = GetY();
    }

    void Update()
    {
        bool active = playerInsideCount > 0;

        if (active)
            insideTime += Time.deltaTime;
        else
            insideTime = 0f;

        float wantedY = (active && insideTime >= activationDelaySeconds) ? targetY : startY;
        float nextY = Mathf.MoveTowards(GetY(), wantedY, moveSpeed * 0.1f * Time.deltaTime);
        SetY(nextY);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            playerInsideCount++;
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInsideCount = Mathf.Max(0, playerInsideCount - 1);
    }

    bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        return other.transform.root.name.Contains("Player");
    }

    float GetY()
    {
        return useLocalPosition ? transform.localPosition.y : transform.position.y;
    }

    void SetY(float y)
    {
        if (useLocalPosition)
        {
            Vector3 pos = transform.localPosition;
            pos.y = y;
            transform.localPosition = pos;
        }
        else
        {
            Vector3 pos = transform.position;
            pos.y = y;
            transform.position = pos;
        }
    }
}