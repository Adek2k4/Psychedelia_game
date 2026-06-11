using UnityEngine;

// Small helper for the boss-arena one-shot sounds in Resources/Sounds/Boss.
// Clips are loaded by name (without extension), e.g. BossAudio.PlayAt("laser", pos).
public static class BossAudio
{
    const string Folder = "Sounds/Boss/";

    public static AudioClip Load(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return null;
        }

        return Resources.Load<AudioClip>(Folder + clipName);
    }

    public static void PlayAt(string clipName, Vector3 position, float volume = 1f)
    {
        AudioClip clip = Load(clipName);
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
