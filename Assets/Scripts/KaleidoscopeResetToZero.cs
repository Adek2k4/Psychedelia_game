using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class KaleidoscopeResetToZero : MonoBehaviour
{
    [Tooltip("Nazwa shadera, którego szukamy.")]
    public string shaderName = "Custom/KaleidoscopeTextureMorph";

    [Tooltip("Czy przeszukiwać dzieci (rekurencyjnie).")]
    public bool includeChildren = true;

#if UNITY_EDITOR
    [ContextMenu("Apply ZERO preset to all Kaleidoscope materials")]
    public void ApplyZeroPresetContextMenu()
    {
        ApplyZeroPreset();
    }
#endif

    public void ApplyZeroPreset()
    {
        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // sharedMaterials -> modyfikujemy assety na dysku[web:101][web:104]
            var mats = r.sharedMaterials;
            if (mats == null) continue;

            foreach (var mat in mats)
            {
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != shaderName) continue;

                // WARTOŚCI Z OBRAZKA (zero preset)
                mat.SetFloat("_AnimationSpeed",         0.0f);
                mat.SetFloat("_Scale",                  0.1f);
                mat.SetFloat("_FractalNormalInfluence", 0.0f);
                mat.SetFloat("_FractalOpacity",         0.0f);
                mat.SetFloat("_BlendMode",              2.0f); // Lerp

                mat.SetFloat("_WaveTexEnable",  1.0f);
                mat.SetFloat("_WaveNormEnable", 1.0f);

                mat.SetFloat("_WaveStrength", 0.0f);
                mat.SetFloat("_WaveSpeed",    1.0f);
                mat.SetFloat("_WaveFreqMin",  1.0f);
                mat.SetFloat("_WaveFreqMax",  1.0f);
                mat.SetFloat("_WaveAmpMin",   0.0f);
                mat.SetFloat("_WaveAmpMax",   0.0f);

                mat.SetFloat("_ShadowWaveStrength", 0.0f);
                mat.SetFloat("_ShadowWaveSpeed",    1.0f);
                mat.SetFloat("_ShadowWaveFreqMin",  0.1f);
                mat.SetFloat("_ShadowWaveFreqMax",  0.1f);
                mat.SetFloat("_ShadowWaveAmpMin",   0.0f);
                mat.SetFloat("_ShadowWaveAmpMax",   0.0f);

#if UNITY_EDITOR
                EditorUtility.SetDirty(mat);
#endif
            }
        }

#if UNITY_EDITOR
        AssetDatabase.SaveAssets();
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(KaleidoscopeResetToZero))]
public class KaleidoscopeResetToZeroEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var script = (KaleidoscopeResetToZero)target;
        if (GUILayout.Button("Apply ZERO preset to all Kaleidoscope materials"))
        {
            script.ApplyZeroPreset();
        }
    }
}
#endif