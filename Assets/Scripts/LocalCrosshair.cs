using UnityEngine;
using Unity.Netcode;

public class LocalCrosshair : NetworkBehaviour
{
    public int size = 6;
    public Color color = Color.white;
    public bool hideWhenCursorVisible = true;

    void OnGUI()
    {
        if (!IsOwner)
        {
            return;
        }

        if (hideWhenCursorVisible && Cursor.visible)
        {
            return;
        }

        int drawSize = Mathf.Max(2, size);
        float x = (Screen.width - drawSize) * 0.5f;
        float y = (Screen.height - drawSize) * 0.5f;

        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, drawSize, drawSize), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}
