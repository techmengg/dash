using UnityEngine;

/// <summary>
/// Centralized one-shot SFX controller for death and victory screens.
/// Assign clips here and both screens can trigger them.
/// </summary>
[DisallowMultipleComponent]
public class DeathVictoryScreenAudioController : MonoBehaviour
{
    [Header("One-Shot Audio Source")]
    public AudioSource oneShotSource;

    [Header("Death Screen SFX")]
    public AudioClip deathScreenSfx;
    [Range(0f, 1f)] public float deathScreenSfxVolume = 1f;

    [Header("Victory Screen SFX")]
    public AudioClip victoryScreenSfx;
    [Range(0f, 1f)] public float victoryScreenSfxVolume = 1f;

    [Header("Playback")]
    public bool stopCurrentBeforePlay = false;

    private void Awake()
    {
        oneShotSource = EnsureSource(oneShotSource);
    }

    public static DeathVictoryScreenAudioController FindInstance()
    {
        return FindFirstObjectByType<DeathVictoryScreenAudioController>();
    }

    public void PlayDeathScreenSfx()
    {
        PlayOneShot(deathScreenSfx, deathScreenSfxVolume);
    }

    public void PlayVictoryScreenSfx()
    {
        PlayOneShot(victoryScreenSfx, victoryScreenSfxVolume);
    }

    /// <summary>
    /// Fades out active background loops in the current scene.
    /// </summary>
    public void FadeOutBackgroundMusic()
    {
        RoomAudioController roomAudio = RoomAudioController.FindInstance();
        if (roomAudio != null)
            roomAudio.StopMusicLoop();

        BossArenaAudioController bossAudio = BossArenaAudioController.FindInstance();
        if (bossAudio != null)
            bossAudio.FadeOutAllLoopsForEndScreen();
    }

    private AudioSource EnsureSource(AudioSource source)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || oneShotSource == null)
            return;

        if (stopCurrentBeforePlay && oneShotSource.isPlaying)
            oneShotSource.Stop();

        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
