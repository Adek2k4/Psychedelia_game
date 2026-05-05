using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class InGameMenu : NetworkBehaviour
{
    public string menuSceneName = "Menu";
    public KeyCode toggleMenuKey = KeyCode.Escape;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;
    public float backdropAlpha = 0.35f;
    public float blurStrength = 1.1f;

    private bool showMenu = false;
    private bool showOptions = false;
    private float volume = 1f;
    private PlayerMovement playerMovement;
    private static readonly int BlurStrengthId = Shader.PropertyToID("_PsychedeliaBlurStrength");

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        volume = AudioListener.volume;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        SetMenuVisible(false);
        SetBlurStrength(0f);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleMenuKey))
        {
            SetMenuVisible(!showMenu);
        }
    }

    void OnGUI()
    {
        if (!showMenu)
        {
            return;
        }

        DrawBackdrop();

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float fontScale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.7f, 1.4f);
        int baseFont = Mathf.RoundToInt(18f * fontScale);
        int titleFont = Mathf.RoundToInt(24f * fontScale);

        float areaWidth = Mathf.Clamp(Screen.width * 0.4f, 320f, 600f);
        float areaHeight = Mathf.Clamp(Screen.height * 0.5f, 280f, 520f);
        Rect area = new Rect((Screen.width - areaWidth) * 0.5f, (Screen.height - areaHeight) * 0.5f, areaWidth, areaHeight);

        DrawPanel(area, new Color(0.08f, 0.1f, 0.12f, 0.5f), new Color(1f, 1f, 1f, 0.08f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = titleFont;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 0.95f);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = baseFont;
        labelStyle.normal.textColor = new Color(0.9f, 0.93f, 1f, 0.9f);

        GUIStyle buttonLabelStyle = new GUIStyle(GUI.skin.label);
        buttonLabelStyle.fontSize = baseFont;
        buttonLabelStyle.fontStyle = FontStyle.Bold;
        buttonLabelStyle.alignment = TextAnchor.MiddleCenter;
        buttonLabelStyle.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.95f);

        GUILayout.BeginArea(area);
        GUILayout.Space(6f * fontScale);
        GUILayout.Label("Menu", titleStyle);
        GUILayout.Space(8f * fontScale);

        if (DrawNeonButton("Wznow", buttonLabelStyle, 0f, fontScale))
        {
            SetMenuVisible(false);
        }

        if (DrawNeonButton("Opcje", buttonLabelStyle, 0.12f, fontScale))
        {
            showOptions = !showOptions;
        }

        if (showOptions)
        {
            GUILayout.Space(6f * fontScale);
            GUILayout.Label("Glosnosc", labelStyle);
            float newVolume = GUILayout.HorizontalSlider(volume, 0f, 1f);
            if (!Mathf.Approximately(newVolume, volume))
            {
                volume = newVolume;
                AudioListener.volume = volume;
            }
        }

        GUILayout.Space(8f * fontScale);

        if (DrawNeonButton("Wyjscie do menu glownego", buttonLabelStyle, 0.24f, fontScale))
        {
            ExitToMainMenu();
        }

        GUILayout.EndArea();
    }

    void SetMenuVisible(bool visible)
    {
        showMenu = visible;

        if (playerMovement != null)
        {
            playerMovement.SetInputEnabled(!visible);
        }

        SetBlurStrength(visible ? blurStrength : 0f);
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    void ExitToMainMenu()
    {
        SetMenuVisible(false);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    void OnDisable()
    {
        SetBlurStrength(0f);
    }

    void DrawBackdrop()
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.04f, 0.05f, 0.07f, Mathf.Clamp01(backdropAlpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    void DrawPanel(Rect rect, Color fill, Color border)
    {
        Color oldColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = border;
        DrawOutline(rect, 2f);
        GUI.color = oldColor;
    }

    void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    bool DrawNeonButton(string label, GUIStyle labelStyle, float hueOffset, float fontScale)
    {
        float height = Mathf.Round(36f * fontScale);
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, labelStyle, GUILayout.Height(height));
        float hue = Mathf.Repeat(Time.time * 0.06f + hueOffset, 1f);
        Color accent = Color.HSVToRGB(hue, 0.8f, 1f);
        DrawPanel(rect, new Color(0.05f, 0.06f, 0.08f, 0.7f), new Color(accent.r, accent.g, accent.b, 0.6f));
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.Label(rect, label, labelStyle);
        return clicked;
    }

    void SetBlurStrength(float value)
    {
        Shader.SetGlobalFloat(BlurStrengthId, Mathf.Max(0f, value));
    }
}
