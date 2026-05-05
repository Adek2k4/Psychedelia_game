using UnityEngine;
using Unity.Netcode;

public class PlayerInteractor : NetworkBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 4f;
    public LayerMask interactMask = ~0;
    public KeyCode interactKey = KeyCode.E;
    public float zoomFov = 35f;
    public float zoomSpeed = 10f;
    public bool lookAtTarget = false;
    public float lookAtSpeed = 8f;
    public string promptText = "[E] by uzyc";
    public string waitingText = "Czekanie na drugiego gracza...";
    public string cancelText = "Nacisnij E aby anulowac";
    public Color promptColor = Color.white;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;
    public float vignetteIntensity = 0.45f;
    public float vignetteSpeed = 8f;
    public Color vignetteColor = Color.black;
    public float vignetteInnerRadius = 0.35f;
    public float vignetteFeather = 0.45f;
    public int vignetteTextureSize = 256;

    private PlayerMovement playerMovement;
    private InteractableSync currentTarget;
    private float defaultFov = 60f;
    private bool defaultFovCached = false;
    private bool isWaiting = false;
    private bool hasTarget = false;
    private float currentVignetteIntensity = 0f;
    private Texture2D vignetteTexture;

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        CacheDefaultFov();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        CacheDefaultFov();
    }

    void Update()
    {
        if (!IsOwner || playerCamera == null)
        {
            return;
        }

        CacheDefaultFov();

        if (isWaiting)
        {
            HandleWaiting();
            return;
        }

        if (Cursor.visible)
        {
            hasTarget = false;
            UpdateZoom(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        InteractableSync target = null;
        hasTarget = false;
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            target = hit.collider.GetComponentInParent<InteractableSync>();
            hasTarget = target != null;
        }

        if (target != null && Input.GetKeyDown(interactKey))
        {
            EnterWaiting(target);
            return;
        }

        UpdateZoom(false);
    }

    void HandleWaiting()
    {
        if (Input.GetKeyDown(interactKey))
        {
            ExitWaiting(true);
            return;
        }

        if (currentTarget == null || !currentTarget.IsSpawned)
        {
            ExitWaiting(false);
            return;
        }

        UpdateZoom(true);

        if (lookAtTarget)
        {
            Vector3 toTarget = currentTarget.transform.position - playerCamera.transform.position;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRot, Time.deltaTime * lookAtSpeed);
            }
        }
    }

    void EnterWaiting(InteractableSync target)
    {
        currentTarget = target;
        isWaiting = true;
        hasTarget = false;

        if (playerMovement != null)
        {
            playerMovement.SetInputEnabled(false);
        }

        if (currentTarget != null && currentTarget.IsSpawned)
        {
            currentTarget.SetReadyServerRpc(true);
        }
    }

    void ExitWaiting(bool sendCancel)
    {
        if (sendCancel && currentTarget != null && currentTarget.IsSpawned)
        {
            currentTarget.SetReadyServerRpc(false);
        }

        isWaiting = false;
        currentTarget = null;

        if (playerMovement != null)
        {
            playerMovement.SetInputEnabled(true);
        }

        UpdateZoom(false);
    }

    void CacheDefaultFov()
    {
        if (playerCamera != null && !defaultFovCached)
        {
            defaultFov = playerCamera.fieldOfView;
            defaultFovCached = true;
        }
    }

    void UpdateZoom(bool zoomed)
    {
        if (playerCamera == null || !defaultFovCached)
        {
            return;
        }

        float targetFov = zoomed ? zoomFov : defaultFov;
        if (zoomSpeed <= 0f)
        {
            playerCamera.fieldOfView = targetFov;
            return;
        }

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * zoomSpeed);
        UpdateVignette(zoomed);
    }

    void OnGUI()
    {
        if (!IsOwner)
        {
            return;
        }

        if (Cursor.visible)
        {
            return;
        }

        DrawVignetteOverlay();

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float fontScale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.7f, 1.4f);
        int fontSize = Mathf.RoundToInt(20f * fontScale);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = promptColor;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        if (isWaiting)
        {
            DrawCenteredLabel(centerX, centerY + 60f * fontScale, waitingText, style);
            DrawCenteredLabel(centerX, centerY + 90f * fontScale, cancelText, style);
        }
        else if (hasTarget)
        {
            DrawCenteredLabel(centerX, centerY + 60f * fontScale, promptText, style);
        }
    }

    void DrawCenteredLabel(float centerX, float centerY, string text, GUIStyle style)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Vector2 size = style.CalcSize(new GUIContent(text));
        Rect rect = new Rect(centerX - size.x * 0.5f, centerY - size.y * 0.5f, size.x, size.y);
        GUI.Label(rect, text, style);
    }

    void UpdateVignette(bool zoomed)
    {
        float target = zoomed ? Mathf.Clamp01(vignetteIntensity) : 0f;
        if (vignetteSpeed <= 0f)
        {
            currentVignetteIntensity = target;
        }
        else
        {
            currentVignetteIntensity = Mathf.Lerp(currentVignetteIntensity, target, Time.deltaTime * vignetteSpeed);
        }
    }

    void DrawVignetteOverlay()
    {
        if (currentVignetteIntensity <= 0.001f)
        {
            return;
        }

        EnsureVignetteTexture();
        if (vignetteTexture == null)
        {
            return;
        }

        Color oldColor = GUI.color;
        Color tint = vignetteColor;
        tint.a *= Mathf.Clamp01(currentVignetteIntensity);
        GUI.color = tint;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTexture);
        GUI.color = oldColor;
    }

    void EnsureVignetteTexture()
    {
        int size = Mathf.Clamp(vignetteTextureSize, 32, 512);
        if (vignetteTexture != null && vignetteTexture.width == size)
        {
            return;
        }

        if (vignetteTexture != null)
        {
            Destroy(vignetteTexture);
        }

        vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        vignetteTexture.wrapMode = TextureWrapMode.Clamp;
        vignetteTexture.filterMode = FilterMode.Bilinear;
        vignetteTexture.hideFlags = HideFlags.HideAndDontSave;

        float inner = Mathf.Clamp01(vignetteInnerRadius);
        float feather = Mathf.Max(0.001f, vignetteFeather);
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dist = Vector2.Distance(new Vector2(u, v), center) * 2f;
                float alpha = Mathf.Clamp01((dist - inner) / feather);
                vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        vignetteTexture.Apply(false, true);
    }

    void OnDestroy()
    {
        if (vignetteTexture != null)
        {
            Destroy(vignetteTexture);
        }
    }
}
