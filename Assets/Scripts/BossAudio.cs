using UnityEngine;

public class BossAudio : MonoBehaviour
{
    [Header("Boss One-Shot Audio Source")]
    public AudioSource bossAudioSource;
    public AudioSource bossActionAudioSource;

    [Header("Boss Loop Audio Sources")]
    public AudioSource bossWalkLoopSource;
    public AudioSource bossChewLoopSource;
    public AudioSource bossRollLoopSource;

    [Header("Boss SFX")]
    public AudioClip bossWalkLoopSfx;
    [Range(0f, 1f)] public float bossWalkLoopSfxVolume = 0.85f;
    public AudioClip bossGrabSfx;
    [Range(0f, 1f)] public float bossGrabSfxVolume = 1f;
    public AudioClip bossDamageSfx;
    [Range(0f, 1f)] public float bossDamageSfxVolume = 1f;
    [Min(0f)] public float bossDamageSfxMinInterval = 0.12f;
    public AudioClip bossDeathSfx;
    [Range(0f, 1f)] public float bossDeathSfxVolume = 1f;
    public AudioClip bossChewLoopSfx;
    [Range(0f, 1f)] public float bossChewLoopSfxVolume = 0.9f;
    public AudioClip bossTossSfx;
    [Range(0f, 1f)] public float bossTossSfxVolume = 1f;
    public AudioClip bossGroundSlamSfx;
    [Range(0f, 1f)] public float bossGroundSlamSfxVolume = 1f;
    public AudioClip bossPhase2ExplodeSfx;
    [Range(0f, 1f)] public float bossPhase2ExplodeSfxVolume = 1f;
    public AudioClip bossVictoryScreenSfx;
    [Range(0f, 1f)] public float bossVictoryScreenSfxVolume = 1f;
    public AudioClip bossSummonSfx;
    [Range(0f, 1f)] public float bossSummonSfxVolume = 1f;
    public AudioClip bossRollStartSfx;
    [Range(0f, 1f)] public float bossRollStartSfxVolume = 1f;
    public AudioClip bossRollLoopSfx;
    [Range(0f, 1f)] public float bossRollLoopSfxVolume = 0.9f;

    private float lastBossDamageSfxTime = -999f;

    private void Awake()
    {
        bossAudioSource = EnsureSource(bossAudioSource, false);
        bossActionAudioSource = EnsureSource(bossActionAudioSource, false);
        bossWalkLoopSource = EnsureSource(bossWalkLoopSource, true);
        bossChewLoopSource = EnsureSource(bossChewLoopSource, true);
        bossRollLoopSource = EnsureSource(bossRollLoopSource, true);
    }

    private AudioSource EnsureSource(AudioSource source, bool shouldLoop)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = shouldLoop;
        source.spatialBlend = 0f;
        return source;
    }

    public void PlayBossGrabSfx()
    {
        PlayActionClip(bossGrabSfx, bossGrabSfxVolume);
    }

    public bool TryPlayBossDamageSfx()
    {
        if (bossDamageSfx == null || bossAudioSource == null)
            return false;

        float now = Time.time;
        float minGap = Mathf.Max(Mathf.Max(0f, bossDamageSfxMinInterval), bossDamageSfx.length * 0.9f);
        if (now - lastBossDamageSfxTime < minGap)
            return true;

        lastBossDamageSfxTime = now;
        return PlayClip(bossDamageSfx, bossDamageSfxVolume);
    }

    public bool TryPlayBossDeathSfx()
    {
        return PlayClip(bossDeathSfx, bossDeathSfxVolume);
    }

    public void PlayBossTossSfx()
    {
        PlayActionClip(bossTossSfx, bossTossSfxVolume);
    }

    public void PlayBossGroundSlamSfx()
    {
        PlayActionClip(bossGroundSlamSfx, bossGroundSlamSfxVolume);
    }

    public bool TryPlayBossPhase2ExplodeSfx()
    {
        return PlayActionClip(bossPhase2ExplodeSfx, bossPhase2ExplodeSfxVolume);
    }

    public bool TryPlayBossVictoryScreenSfx()
    {
        return PlayClip(bossVictoryScreenSfx, bossVictoryScreenSfxVolume);
    }

    public bool TryPlayBossSummonSfx()
    {
        return PlayActionClip(bossSummonSfx, bossSummonSfxVolume);
    }

    public bool TryPlayBossRollStartSfx()
    {
        return PlayActionClip(bossRollStartSfx, bossRollStartSfxVolume);
    }

    public bool TrySetBossRollLoopActive(bool isActive)
    {
        if (!isActive)
        {
            StopLoop(bossRollLoopSource);
            return true;
        }

        if (bossRollLoopSource == null || bossRollLoopSfx == null)
        {
            StopLoop(bossRollLoopSource);
            return false;
        }

        StartLoop(bossRollLoopSource, bossRollLoopSfx, bossRollLoopSfxVolume);
        return true;
    }

    public void StopBossActionSfx()
    {
        SetBossChewLoopActive(false);
        TrySetBossRollLoopActive(false);

        if (bossActionAudioSource != null && bossActionAudioSource.isPlaying)
            bossActionAudioSource.Stop();
    }

    public void SetBossWalkLoopActive(bool isActive)
    {
        if (isActive)
            StartLoop(bossWalkLoopSource, bossWalkLoopSfx, bossWalkLoopSfxVolume);
        else
            StopLoop(bossWalkLoopSource);
    }

    public void SetBossChewLoopActive(bool isActive)
    {
        if (isActive)
            StartLoop(bossChewLoopSource, bossChewLoopSfx, bossChewLoopSfxVolume);
        else
            StopLoop(bossChewLoopSource);
    }

    public void StopAllLoopingSfx()
    {
        StopLoop(bossWalkLoopSource);
        StopLoop(bossChewLoopSource);
        StopLoop(bossRollLoopSource);
    }

    private bool PlayClip(AudioClip clip, float volume)
    {
        return PlayClipOnSource(bossAudioSource, clip, volume);
    }

    private bool PlayActionClip(AudioClip clip, float volume)
    {
        return PlayClipOnSource(bossActionAudioSource, clip, volume);
    }

    private bool PlayClipOnSource(AudioSource source, AudioClip clip, float volume)
    {
        if (clip == null || source == null)
            return false;

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
        return true;
    }

    private void StartLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null)
            return;

        if (clip == null)
        {
            StopLoop(source);
            return;
        }

        float clampedVolume = Mathf.Clamp01(volume);

        if (source.isPlaying && source.clip == clip)
        {
            source.volume = clampedVolume;
            return;
        }

        source.clip = clip;
        source.volume = clampedVolume;
        source.loop = true;
        source.Play();
    }

    private void StopLoop(AudioSource source)
    {
        if (source == null)
            return;

        if (source.isPlaying)
            source.Stop();
    }
}
