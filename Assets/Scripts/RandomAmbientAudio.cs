using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomAmbientAudio : MonoBehaviour
{
    public AudioClip[] clips;
    public Vector2 intervalRange = new Vector2(2f, 10f);
    public float volume = 0.1f;
    public bool playOnStart = true;
    public float spatialBlend = 1f;
    public float minDistance = 5f;
    public float maxDistance = 30f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    private AudioSource audioSource;
    private Coroutine routine;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        Apply3dSettings();
    }

    void OnValidate()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        Apply3dSettings();
    }

    void OnEnable()
    {
        if (playOnStart)
        {
            StartLoop();
        }
    }

    void OnDisable()
    {
        StopLoop();
    }

    public void StartLoop()
    {
        if (routine == null)
        {
            routine = StartCoroutine(PlayLoop());
        }
    }

    public void StopLoop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    IEnumerator PlayLoop()
    {
        while (true)
        {
            float min = Mathf.Max(0f, intervalRange.x);
            float max = Mathf.Max(min, intervalRange.y);
            float delay = Random.Range(min, max);
            yield return new WaitForSeconds(delay);

            if (clips == null || clips.Length == 0)
            {
                continue;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }

    void Apply3dSettings()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
        audioSource.minDistance = Mathf.Max(0.01f, minDistance);
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance, maxDistance);
        audioSource.rolloffMode = rolloffMode;
    }
}
