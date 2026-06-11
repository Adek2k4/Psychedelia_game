using UnityEngine;
using Unity.Netcode;

// In-scene HUD for the boss arena. Draws the boss health bar while fighting,
// and the game-over / victory overlay. On game over (or victory) the host gets
// a "Spróbuj jeszcze raz" button that restarts the fight; other clients are
// told to wait for the host.
public class BossArenaHud : MonoBehaviour
{
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float uiScale = 1f;

    public string gameOverText = "GAME OVER";
    public string victoryText = "Wygraliście!";
    public string retryText = "Spróbuj jeszcze raz";
    public string waitForHostText = "Czekaj, aż host zrestartuje...";
    public string bossLabel = "BOSS";

    void OnGUI()
    {
        BossArenaController controller = BossArenaController.Instance;
        if (controller == null || !controller.IsSpawned)
        {
            return;
        }

        float screenScale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
        float fontScale = Mathf.Clamp(screenScale * Mathf.Max(0.75f, uiScale), 0.7f, 1.4f);

        BossArenaController.ArenaState state = controller.State.Value;

        if (state == BossArenaController.ArenaState.Fighting)
        {
            DrawBossHealth(controller, fontScale);
            return;
        }

        DrawEndOverlay(controller, state, fontScale);
    }

    void DrawBossHealth(BossArenaController controller, float fontScale)
    {
        if (controller.boss == null || controller.boss.IsDead)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, controller.boss.maxHealth);
        float fraction = Mathf.Clamp01(controller.boss.Health.Value / maxHealth);

        float barWidth = Mathf.Clamp(Screen.width * 0.4f, 280f, 700f);
        float barHeight = Mathf.Round(22f * fontScale);
        float x = (Screen.width - barWidth) * 0.5f;
        float y = Mathf.Clamp(Screen.height * 0.06f, 24f, 120f);

        Color old = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.7f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, barWidth + 4f, barHeight + 4f), Texture2D.whiteTexture);

        float hue = Mathf.Lerp(0f, 0.33f, fraction);
        GUI.color = Color.HSVToRGB(hue, 0.85f, 1f);
        GUI.DrawTexture(new Rect(x, y, barWidth * fraction, barHeight), Texture2D.whiteTexture);
        GUI.color = old;

        GUIStyle label = new GUIStyle(GUI.skin.label);
        label.fontSize = Mathf.RoundToInt(16f * fontScale);
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.95f);
        GUI.Label(new Rect(x, y - 24f * fontScale, barWidth, 24f * fontScale), bossLabel, label);
    }

    void DrawEndOverlay(BossArenaController controller, BossArenaController.ArenaState state, float fontScale)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.03f, 0.02f, 0.05f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        bool victory = state == BossArenaController.ArenaState.Victory;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = Mathf.RoundToInt(64f * fontScale);
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = victory
            ? new Color(0.6f, 1f, 0.8f, 0.97f)
            : new Color(1f, 0.4f, 0.5f, 0.97f);
        title.clipping = TextClipping.Overflow;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.42f;
        string headline = victory ? victoryText : gameOverText;
        Vector2 size = title.CalcSize(new GUIContent(headline));
        GUI.Label(new Rect(centerX - size.x * 0.5f, centerY - size.y * 0.5f, size.x, size.y), headline, title);

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        float buttonWidth = Mathf.Clamp(Screen.width * 0.3f, 260f, 480f);
        float buttonHeight = Mathf.Round(54f * fontScale);
        float buttonY = centerY + 80f * fontScale;
        Rect buttonRect = new Rect(centerX - buttonWidth * 0.5f, buttonY, buttonWidth, buttonHeight);

        if (isHost)
        {
            if (DrawNeonButton(buttonRect, retryText, fontScale))
            {
                controller.RetryServerRpc();
            }
        }
        else
        {
            GUIStyle wait = new GUIStyle(GUI.skin.label);
            wait.fontSize = Mathf.RoundToInt(22f * fontScale);
            wait.alignment = TextAnchor.MiddleCenter;
            wait.normal.textColor = new Color(0.9f, 0.93f, 1f, 0.9f);
            wait.clipping = TextClipping.Overflow;
            GUI.Label(buttonRect, waitForHostText, wait);
        }
    }

    bool DrawNeonButton(Rect rect, string label, float fontScale)
    {
        float hue = Mathf.Repeat(Time.time * 0.08f, 1f);
        Color accent = Color.HSVToRGB(hue, 0.8f, 1f);

        Color old = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(accent.r, accent.g, accent.b, 0.7f);
        DrawOutline(rect, 2f);
        GUI.color = old;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = Mathf.RoundToInt(24f * fontScale);
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.97f);

        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        GUI.Label(rect, label, labelStyle);
        return clicked;
    }

    void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }
}
