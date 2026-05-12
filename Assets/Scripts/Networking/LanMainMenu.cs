using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LanMainMenu : MonoBehaviour
{
    public string gameSceneName = "Pustynia";
    public int maxPlayers = 2;
    public string address = "127.0.0.1";
    public string listenAddress = "0.0.0.0";
    public int port = 7777;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;
    public float titleWaveSpeed = 2.4f;
    public float titleColorSpeed = 0.12f;
    public float accentColorSpeed = 0.06f;

    private bool showMenu = true;
    private string portText;

    void Awake()
    {
        portText = port.ToString();
    }

    void OnGUI()
    {
        if (!showMenu)
        {
            return;
        }

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float fontScale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.7f, 1.4f);
        int baseFont = Mathf.RoundToInt(18f * fontScale);
        int titleFont = Mathf.RoundToInt(68f * fontScale);
        int subtitleFont = Mathf.RoundToInt(20f * fontScale);

        float titleY = Mathf.Clamp(Screen.height * 0.08f, 32f, 200f);
        DrawKaleidoscopeTitle("PSYCHEDELIA", new Vector2(Screen.width * 0.5f, titleY), titleFont);

        float areaWidth = Mathf.Clamp(Screen.width * 0.54f, 420f, 720f);
        float areaHeight = Mathf.Clamp(Screen.height * 0.58f, 360f, 640f);
        float areaX = (Screen.width - areaWidth) * 0.5f;
        float areaY = Mathf.Clamp(Screen.height * 0.22f, 150f, 300f);
        Rect area = new Rect(areaX, areaY, areaWidth, areaHeight);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = baseFont;
        labelStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 0.9f);

        GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = subtitleFont;
        subtitleStyle.fontStyle = FontStyle.Bold;
        subtitleStyle.alignment = TextAnchor.MiddleCenter;
        subtitleStyle.normal.textColor = new Color(0.9f, 0.93f, 1f, 0.9f);

        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = baseFont;
        textFieldStyle.padding = new RectOffset(8, 8, 6, 6);
        textFieldStyle.normal.background = null;
        textFieldStyle.focused.background = null;
        textFieldStyle.hover.background = null;
        textFieldStyle.active.background = null;
        textFieldStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 0.95f);

        GUIStyle buttonLabelStyle = new GUIStyle(GUI.skin.label);
        buttonLabelStyle.fontSize = baseFont;
        buttonLabelStyle.fontStyle = FontStyle.Bold;
        buttonLabelStyle.alignment = TextAnchor.MiddleCenter;
        buttonLabelStyle.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.95f);

        GUILayout.BeginArea(area);
        GUILayout.Space(10f * fontScale);
        GUILayout.Label("LAN Co-op", subtitleStyle);
        GUILayout.Space(10f * fontScale);

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            GUILayout.Label("Brak NetworkManager w scenie.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("Status: " + GetStatusLabel(networkManager), labelStyle);
        GUILayout.Space(8f * fontScale);

        GUILayout.Label("Adres serwera", labelStyle);
        address = DrawInputField(address, textFieldStyle, fontScale);
        GUILayout.Label("Adres nasluchu (host)", labelStyle);
        listenAddress = DrawInputField(listenAddress, textFieldStyle, fontScale);
        GUILayout.Label("Port", labelStyle);
        portText = DrawInputField(portText, textFieldStyle, fontScale);

        GUILayout.Space(10f * fontScale);

        bool isRunning = networkManager.IsClient || networkManager.IsServer;
        if (!isRunning)
        {
            GUILayout.Label("Max graczy: " + maxPlayers, labelStyle);
            if (DrawNeonButton("Play (Host)", buttonLabelStyle, 0f, fontScale))
            {
                StartHost(networkManager);
            }
            if (DrawNeonButton("Dolacz (Client)", buttonLabelStyle, 0.12f, fontScale))
            {
                StartClient(networkManager);
            }
            if (DrawNeonButton("Wyjscie", buttonLabelStyle, 0.24f, fontScale))
            {
                QuitGame();
            }
        }
        else
        {
            if (DrawNeonButton("Stop", buttonLabelStyle, 0.24f, fontScale))
            {
                networkManager.Shutdown();
            }
        }

        GUILayout.EndArea();
    }

    void StartHost(NetworkManager networkManager)
    {
        if (!TryApplyNetworkConfig(networkManager))
        {
            return;
        }

        if (networkManager.StartHost())
        {
            showMenu = false;
            if (!string.IsNullOrWhiteSpace(gameSceneName) && SceneManager.GetActiveScene().name != gameSceneName)
            {
                networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        }
    }

    void StartClient(NetworkManager networkManager)
    {
        if (!TryApplyNetworkConfig(networkManager))
        {
            return;
        }

        if (networkManager.StartClient())
        {
            showMenu = false;
        }
    }

    bool TryApplyNetworkConfig(NetworkManager networkManager)
    {
        if (!ushort.TryParse(portText, out ushort parsedPort))
        {
            Debug.LogWarning("Nieprawidlowy port.");
            return false;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogWarning("Brak UnityTransport na obiekcie NetworkManager.");
            return false;
        }

        transport.SetConnectionData(address, parsedPort, listenAddress);
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = true;
        return true;
    }

    string GetStatusLabel(NetworkManager networkManager)
    {
        if (networkManager.IsHost)
        {
            return "Host";
        }

        if (networkManager.IsServer)
        {
            return "Server";
        }

        if (networkManager.IsClient)
        {
            return "Client";
        }

        return "Offline";
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    void DrawKaleidoscopeTitle(string text, Vector2 center, float fontSize)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(fontSize);
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.UpperLeft;
        style.clipping = TextClipping.Overflow;

        float spacing = fontSize * 0.08f;
        float lineHeight = style.CalcSize(new GUIContent("Mg")).y;
        float rectHeight = Mathf.Max(lineHeight * 1.3f, fontSize * 1.2f);
        float yTop = Mathf.Max(center.y, Screen.safeArea.y + 4f);
        float totalWidth = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            totalWidth += style.CalcSize(new GUIContent(ch)).x;
            if (i < text.Length - 1)
            {
                totalWidth += spacing;
            }
        }

        float startX = center.x - totalWidth * 0.5f;
        float time = Time.time;

        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            float chWidth = style.CalcSize(new GUIContent(ch)).x;
            float hue = Mathf.Repeat(time * titleColorSpeed + i * 0.09f, 1f);
            Color color = Color.HSVToRGB(hue, 0.85f, 1f);
            style.normal.textColor = color;

            Rect charRect = new Rect(startX, yTop, chWidth, rectHeight);
            GUI.Label(charRect, ch, style);
            startX += chWidth + spacing;
        }
    }

    float ClampTitleCenterY(float centerY, float height)
    {
        float safeTop = Screen.safeArea.y;
        return Mathf.Max(centerY, safeTop + height * 0.6f + 4f);
    }

    string DrawInputField(string value, GUIStyle textFieldStyle, float fontScale)
    {
        float height = Mathf.Round(34f * fontScale);
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, textFieldStyle, GUILayout.Height(height));
        DrawFieldBackground(rect, new Color(0.05f, 0.06f, 0.07f, 0.4f), new Color(1f, 1f, 1f, 0.08f));
        return GUI.TextField(rect, value, textFieldStyle);
    }

    bool DrawNeonButton(string label, GUIStyle labelStyle, float hueOffset, float fontScale)
    {
        float height = Mathf.Round(40f * fontScale);
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, labelStyle, GUILayout.Height(height));
        float hue = Mathf.Repeat(Time.time * accentColorSpeed + hueOffset, 1f);
        Color accent = Color.HSVToRGB(hue, 0.8f, 1f);
        DrawFieldBackground(rect, new Color(0.05f, 0.06f, 0.08f, 0.65f), new Color(accent.r, accent.g, accent.b, 0.65f));
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.Label(rect, label, labelStyle);
        return clicked;
    }

    void DrawFieldBackground(Rect rect, Color fill, Color border)
    {
        Color oldColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = border;
        DrawOutline(rect, 2f);
        GUI.color = oldColor;
    }
}
