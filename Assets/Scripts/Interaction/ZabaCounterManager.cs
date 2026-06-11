using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ZabaCounterManager : NetworkBehaviour
{
    public int targetCount = 10;
    public bool resetOnActivate = true;

    [Header("Boss arena")]
    public string arenaSceneName = "Odrealnienie";
    public string imageResourcePath = "Images/zaba";
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;
    public float imageSize = 160f;
    public float imageCounterSpacing = 20f;
    public float counterFontSize = 56f;
    public float counterWaveAmplitude = 8f;
    public float counterWaveSpeed = 6f;
    public float counterWaveOffset = 0.6f;
    public float counterHueSpeed = 0.2f;
    public float counterHueOffset = 0.12f;
    public float counterSaturation = 1f;
    public float counterValue = 1f;

    private readonly NetworkVariable<int> currentCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Texture2D zabaImage;

    public bool IsActive => isActive.Value;

    public static ZabaCounterManager FindInScene()
    {
        return FindObjectOfType<ZabaCounterManager>(true);
    }

    public void ActivateAfterDelayServer(float delay)
    {
        if (!IsServer)
        {
            return;
        }

        StartCoroutine(ActivateAfterDelayRoutine(delay));
    }

    IEnumerator ActivateAfterDelayRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!IsServer)
        {
            yield break;
        }

        if (resetOnActivate)
        {
            currentCount.Value = 0;
        }

        isActive.Value = true;
    }

    public void AddCountServer(int value)
    {
        if (!IsServer)
        {
            return;
        }

        if (!isActive.Value)
        {
            return;
        }

        int next = currentCount.Value + Mathf.Max(0, value);
        bool wasComplete = currentCount.Value >= targetCount;
        currentCount.Value = Mathf.Min(next, Mathf.Max(0, targetCount));

        if (!wasComplete && currentCount.Value >= targetCount && targetCount > 0)
        {
            BossArenaTeleport.TeleportToArena(arenaSceneName);
        }
    }

    void OnGUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        if (!isActive.Value)
        {
            return;
        }

        if (zabaImage == null && !string.IsNullOrWhiteSpace(imageResourcePath))
        {
            zabaImage = Resources.Load<Texture2D>(imageResourcePath);
        }

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float scale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.6f, 1.6f);

        float safeX = Screen.safeArea.x + 20f * scale;
        float safeY = Screen.safeArea.y + 20f * scale;
        float imgSize = Mathf.Max(1f, imageSize * scale);

        if (zabaImage != null)
        {
            Rect imgRect = new Rect(safeX, safeY, imgSize, imgSize);
            GUI.DrawTexture(imgRect, zabaImage, ScaleMode.ScaleToFit, true);
        }

        string counter = $"{currentCount.Value}/{targetCount}";
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(counterFontSize * scale);
        style.alignment = TextAnchor.MiddleLeft;

        float textX = safeX + imgSize + imageCounterSpacing * scale;
        float textY = safeY + imgSize * 0.5f - style.lineHeight * 0.5f;
        DrawWavyText(counter, new Vector2(textX, textY), style, scale);
    }

    void DrawWavyText(string text, Vector2 startPos, GUIStyle style, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        float time = Time.time;
        float x = startPos.x;
        float baseY = startPos.y;
        Color oldColor = GUI.color;
        float letterSpacing = Mathf.Max(0f, 1.5f * scale);
        float slashExtra = Mathf.Max(0f, 1.0f * scale);

        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            Vector2 size = style.CalcSize(new GUIContent(ch));
            float wave = Mathf.Sin(time * counterWaveSpeed + i * counterWaveOffset) * counterWaveAmplitude * scale;
            float hue = Mathf.Repeat(time * counterHueSpeed + i * counterHueOffset, 1f);
            Color col = Color.HSVToRGB(hue, counterSaturation, counterValue);
            float extra = ch == "/" ? slashExtra : 0f;

            GUI.color = col;
            GUI.Label(new Rect(x, baseY + wave, size.x + extra, style.lineHeight), ch, style);
            x += size.x + letterSpacing + extra;
        }

        GUI.color = oldColor;
    }
}
