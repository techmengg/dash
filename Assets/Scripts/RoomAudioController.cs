using System.Collections;
using UnityEngine;

/// <summary>
/// Centralized audio controller for the Rooms scene.
/// Handles background music loop plus one-shot UI/event SFX.
/// </summary>
[DisallowMultipleComponent]
public class RoomAudioController : MonoBehaviour
{
    [Header("Auto Start")]
    public bool playMusicOnStart = true;

    [Header("Audio Sources (auto-created if null)")]
    public AudioSource oneShotSource;
    public AudioSource musicLoopSource;

    [Header("Background Music")]
    public AudioClip backgroundMusicLoopClip;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.55f;
    [Min(0.01f)] public float musicFadeSeconds = 0.9f;

    [Header("Event SFX")]
    public AudioClip titlePopupSfx;
    [Range(0f, 1f)] public float titlePopupSfxVolume = 0.9f;
    public AudioClip waveTextPopupSfx;
    [Range(0f, 1f)] public float waveTextPopupSfxVolume = 0.9f;
    public AudioClip waveClearedSfx;
    [Range(0f, 1f)] public float waveClearedSfxVolume = 0.95f;
    public AudioClip bossAvailableSfx;
    [Range(0f, 1f)] public float bossAvailableSfxVolume = 1f;

    private Coroutine musicFadeCoroutine;

    private void Awake()
    {
        oneShotSource = EnsureSource(oneShotSource, false);
        musicLoopSource = EnsureSource(musicLoopSource, true);
        musicLoopSource.playOnAwake = false;
    }

    private void Start()
    {
        if (playMusicOnStart)
            StartMusicLoop();
    }

    public static RoomAudioController FindInstance()
    {
        return FindFirstObjectByType<RoomAudioController>();
    }

    public bool PlayTitlePopupSfx()
    {
        return PlayOneShot(titlePopupSfx, titlePopupSfxVolume);
    }

    public bool PlayWaveClearedSfx()
    {
        return PlayOneShot(waveClearedSfx, waveClearedSfxVolume);
    }

    public bool PlayWaveTextPopupSfx()
    {
        // Prefer dedicated wave popup clip; if missing, reuse title popup clip.
        if (waveTextPopupSfx != null)
            return PlayOneShot(waveTextPopupSfx, waveTextPopupSfxVolume);

        return PlayTitlePopupSfx();
    }

    public bool PlayBossAvailableSfx()
    {
        return PlayOneShot(bossAvailableSfx, bossAvailableSfxVolume);
    }

    public void StartMusicLoop()
    {
        FadeInLoop(
            musicLoopSource,
            backgroundMusicLoopClip,
            Mathf.Clamp01(backgroundMusicVolume),
            Mathf.Max(0.01f, musicFadeSeconds),
            ref musicFadeCoroutine);
    }

    public void StopMusicLoop()
    {
        FadeOutLoop(musicLoopSource, Mathf.Max(0.01f, musicFadeSeconds), ref musicFadeCoroutine);
    }

    private AudioSource EnsureSource(AudioSource source, bool shouldLoop)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.loop = shouldLoop;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        return source;
    }

    private bool PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
            return false;

        if (oneShotSource == null)
            oneShotSource = EnsureSource(oneShotSource, false);

        if (oneShotSource == null)
            return false;

        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        return true;
    }

    private void FadeInLoop(
        AudioSource source,
        AudioClip clip,
        float targetVolume,
        float duration,
        ref Coroutine fadeCoroutine)
    {
        if (source == null || clip == null)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeInLoopRoutine(source, clip, targetVolume, duration));
    }

    private void FadeOutLoop(AudioSource source, float duration, ref Coroutine fadeCoroutine)
    {
        if (source == null)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOutLoopRoutine(source, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeInLoopRoutine(AudioSource source, AudioClip clip, float targetVolume, float duration)
    {
        if (source.clip != clip)
        {
            source.clip = clip;
            source.volume = 0f;
            source.Play();
        }
        else if (!source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        float elapsed = 0f;
        float startVolume = source.volume;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private IEnumerator FadeOutLoopRoutine(AudioSource source, float duration)
    {
        if (!source.isPlaying)
        {
            source.volume = 0f;
            yield break;
        }

        float elapsed = 0f;
        float startVolume = source.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
    }
}
