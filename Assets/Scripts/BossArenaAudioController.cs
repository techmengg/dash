using System.Collections;
using UnityEngine;

/// <summary>
/// Centralized audio controller for boss-area presentation beats.
/// Handles area-title SFX, door-open SFX, ambient loop, boss music loop,
/// and a one-shot epic sting when the player enters the boss arena.
/// </summary>
[DisallowMultipleComponent]
public class BossArenaAudioController : MonoBehaviour
{
    [Header("Auto Start")]
    public bool playAmbientOnStart = true;

    [Header("Audio Sources (auto-created if null)")]
    public AudioSource oneShotSource;
    public AudioSource ambientLoopSource;
    public AudioSource musicLoopSource;

    [Header("Event SFX")]
    public AudioClip titlePopupSfx;
    [Range(0f, 1f)] public float titlePopupSfxVolume = 0.9f;
    public AudioClip doorOpenSfx;
    [Range(0f, 1f)] public float doorOpenSfxVolume = 0.95f;
    public AudioClip bossArenaEntryEpicSfx;
    [Range(0f, 1f)] public float bossArenaEntryEpicSfxVolume = 1f;

    [Header("Ambient")]
    public AudioClip ambientLoopClip;
    [Range(0f, 1f)] public float ambientLoopVolume = 0.4f;
    [Min(0.01f)] public float ambientFadeSeconds = 0.7f;

    [Header("Boss Music")]
    public AudioClip backgroundMusicLoopClip;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.65f;
    [Min(0.01f)] public float musicFadeSeconds = 0.9f;
    public bool fadeOutAmbientOnBossEntry = true;

    private Coroutine ambientFadeCoroutine;
    private Coroutine musicFadeCoroutine;
    private bool hasEnteredBossArena;

    private void Awake()
    {
        oneShotSource = EnsureSource(oneShotSource, false);
        ambientLoopSource = EnsureSource(ambientLoopSource, true);
        musicLoopSource = EnsureSource(musicLoopSource, true);

        ambientLoopSource.playOnAwake = false;
        musicLoopSource.playOnAwake = false;
    }

    private void Start()
    {
        if (playAmbientOnStart)
            StartAmbientLoop();
    }

    public static BossArenaAudioController FindInstance()
    {
        return FindFirstObjectByType<BossArenaAudioController>();
    }

    public bool PlayTitlePopupSfx()
    {
        return PlayOneShot(titlePopupSfx, titlePopupSfxVolume);
    }

    public bool PlayDoorOpenSfx()
    {
        return PlayOneShot(doorOpenSfx, doorOpenSfxVolume);
    }

    public void OnBossArenaEntered()
    {
        if (hasEnteredBossArena)
            return;

        hasEnteredBossArena = true;

        PlayOneShot(bossArenaEntryEpicSfx, bossArenaEntryEpicSfxVolume);

        if (fadeOutAmbientOnBossEntry)
            FadeOutLoop(ambientLoopSource, ambientFadeSeconds, ref ambientFadeCoroutine);

        StartMusicLoop();
    }

    public void StartAmbientLoop()
    {
        FadeInLoop(
            ambientLoopSource,
            ambientLoopClip,
            Mathf.Clamp01(ambientLoopVolume),
            Mathf.Max(0.01f, ambientFadeSeconds),
            ref ambientFadeCoroutine);
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

    /// <summary>
    /// Fades out active boss-area background loops for end screens.
    /// </summary>
    public void FadeOutAllLoopsForEndScreen()
    {
        FadeOutLoop(ambientLoopSource, Mathf.Max(0.01f, ambientFadeSeconds), ref ambientFadeCoroutine);
        FadeOutLoop(musicLoopSource, Mathf.Max(0.01f, musicFadeSeconds), ref musicFadeCoroutine);
    }

    /// <summary>
    /// Called on player respawn to restore pre-boss audio state.
    /// </summary>
    public void ResetForRespawn()
    {
        hasEnteredBossArena = false;

        FadeOutLoop(musicLoopSource, musicFadeSeconds, ref musicFadeCoroutine);

        if (playAmbientOnStart)
            StartAmbientLoop();
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
