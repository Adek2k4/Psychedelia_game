using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class AutoMaterialTilingParent : MonoBehaviour
{
    public Vector2 tilingPerUnit = Vector2.one;
    public bool useXZ = true;
    public bool includeInactive = true;

    private Renderer parentRenderer;
    private Renderer[] renderers;

    private readonly Dictionary<Renderer, Material[]> runtimeMaterials = new Dictionary<Renderer, Material[]>();

    void OnEnable()
    {
        Rebuild();
        ApplyTiling();
    }

    void OnValidate()
    {
        Rebuild();
        ApplyTiling();
    }

    void OnTransformChildrenChanged()
    {
        Rebuild();
        ApplyTiling();
    }

    void Update()
    {
        if (parentRenderer == null || renderers == null || renderers.Length == 0)
            Rebuild();

        ApplyTiling();
    }

    void Rebuild()
    {
        parentRenderer = GetComponent<Renderer>();
        renderers = GetComponentsInChildren<Renderer>(includeInactive);

        if (parentRenderer == null)
            return;

        Material parentMaterial = parentRenderer.material;
        if (parentMaterial == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (r == parentRenderer)
                continue;

            if (runtimeMaterials.ContainsKey(r))
                continue;

            Material[] sourceSlots = r.sharedMaterials;
            int slotCount = (sourceSlots != null && sourceSlots.Length > 0) ? sourceSlots.Length : 1;

            Material[] newMaterials = new Material[slotCount];

            for (int m = 0; m < slotCount; m++)
            {
                Material copy = new Material(parentMaterial);
                copy.name = parentMaterial.name + " (" + r.name + ")";
                newMaterials[m] = copy;
            }

            r.materials = newMaterials;
            runtimeMaterials[r] = newMaterials;
        }
    }

    void ApplyTiling()
    {
        if (renderers == null || renderers.Length == 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            Material[] mats = r.materials;
            if (mats == null || mats.Length == 0)
                continue;

            Vector3 s = r.transform.lossyScale;

            Vector2 tiling = useXZ
                ? new Vector2(s.x * tilingPerUnit.x, s.z * tilingPerUnit.y)
                : new Vector2(s.x * tilingPerUnit.x, s.y * tilingPerUnit.y);

            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                    mats[m].mainTextureScale = tiling;
            }
        }
    }

    void OnDisable()
    {
        foreach (var pair in runtimeMaterials)
        {
            Material[] mats = pair.Value;
            if (mats == null)
                continue;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(mats[i]);
                else
                    DestroyImmediate(mats[i]);
            }
        }

        runtimeMaterials.Clear();
    }
}