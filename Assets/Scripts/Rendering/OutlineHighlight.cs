using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OutlineHighlight : MonoBehaviour
{
    public Color color = Color.white;
    public float thickness = 0.02f;
    public bool highlightOnStart = false;

    private Material outlineMaterial;
    private List<Renderer> renderers = new List<Renderer>();
    private bool highlighted = false;

    void Awake()
    {
        CacheRenderers();
        EnsureMaterial();
        SetHighlighted(highlightOnStart);
    }

    void OnEnable()
    {
        if (highlighted)
        {
            ApplyOutline(true);
        }
    }

    void OnDisable()
    {
        ApplyOutline(false);
    }

    void OnValidate()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.SetColor("_Color", color);
            outlineMaterial.SetFloat("_Thickness", thickness);
        }
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
        {
            return;
        }

        highlighted = value;
        ApplyOutline(value);
    }

    void CacheRenderers()
    {
        renderers.Clear();
        GetComponentsInChildren(true, renderers);
    }

    void EnsureMaterial()
    {
        if (outlineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/Psychedelia/Outline");
        if (shader == null)
        {
            Debug.LogWarning("Outline shader not found. Ensure Hidden/Psychedelia/Outline exists.");
            return;
        }

        outlineMaterial = new Material(shader);
        outlineMaterial.hideFlags = HideFlags.HideAndDontSave;
        outlineMaterial.SetColor("_Color", color);
        outlineMaterial.SetFloat("_Thickness", thickness);
    }

    void ApplyOutline(bool enable)
    {
        EnsureMaterial();
        if (outlineMaterial == null)
        {
            return;
        }

        if (renderers.Count == 0)
        {
            CacheRenderers();
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            List<Material> materials = new List<Material>(renderer.sharedMaterials);
            bool hasOutline = materials.Contains(outlineMaterial);
            if (enable && !hasOutline)
            {
                materials.Add(outlineMaterial);
                renderer.sharedMaterials = materials.ToArray();
            }
            else if (!enable && hasOutline)
            {
                materials.Remove(outlineMaterial);
                renderer.sharedMaterials = materials.ToArray();
            }
        }
    }
}
