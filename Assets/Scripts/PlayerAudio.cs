using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("One-Shot Audio Source")]
    public AudioSource dashAudioSource;

    [Header("Loop Audio Sources")]
    public AudioSource movementLoopSource;
    public AudioSource chargeLoopSource;
    public AudioSource dashLoopSource;
    public AudioSource superLoopSource;
    public AudioSource abilityLoopSource;
    public AudioSource chainLoopSource;

    [Header("Dash and Movement SFX")]
    public AudioClip chargeStartSfx;
    [Range(0f, 1f)] public float chargeStartSfxVolume = 0.8f;
    public AudioClip chargeReleaseSfx;
    [Range(0f, 1f)] public float chargeReleaseSfxVolume = 0.9f;
    public AudioClip dashSfx;
    [Range(0f, 1f)] public float dashSfxVolume = 1f;
    public AudioClip walkSfx;
    [Range(0f, 1f)] public float walkSfxVolume = 1f;
    public AudioClip walkLoopSfx;
    [Range(0f, 1f)] public float walkLoopSfxVolume = 1f;
    public AudioClip chargeLoopSfx;
    [Range(0f, 1f)] public float chargeLoopSfxVolume = 0.8f;
    public AudioClip dashLoopSfx;
    [Range(0f, 1f)] public float dashLoopSfxVolume = 1f;

    [Header("Combat SFX")]
    public AudioClip impactSfx;
    [Range(0f, 1f)] public float impactSfxVolume = 1f;
    public AudioClip damageSfx;
    [Range(0f, 1f)] public float damageSfxVolume = 1f;
    public AudioClip deathSfx;
    [Range(0f, 1f)] public float deathSfxVolume = 1f;

    [Header("Super and Ability SFX")]
    public AudioClip abilityActivateSfx;
    [Range(0f, 1f)] public float abilityActivateSfxVolume = 1f;
    public AudioClip abilityReadySfx;
    [Range(0f, 1f)] public float abilityReadySfxVolume = 1f;
    public AudioClip abilityErrorSfx;
    [Range(0f, 1f)] public float abilityErrorSfxVolume = 1f;
    public AudioClip superActivateSfx;
    [Range(0f, 1f)] public float superActivateSfxVolume = 1f;
    public AudioClip superReadySfx;
    [Range(0f, 1f)] public float superReadySfxVolume = 1f;
    public AudioClip superErrorSfx;
    [Range(0f, 1f)] public float superErrorSfxVolume = 1f;
    public AudioClip superEndSfx;
    [Range(0f, 1f)] public float superEndSfxVolume = 1f;
    public AudioClip superActiveSfx;
    [Range(0f, 1f)] public float superActiveSfxVolume = 0.9f;
    public AudioClip superLoopSfx;
    [Range(0f, 1f)] public float superLoopSfxVolume = 0.9f;
    public AudioClip ghostProjectileFireSfx;
    [Range(0f, 1f)] public float ghostProjectileFireSfxVolume = 1f;
    public AudioClip abilityLoopSfx;
    [Range(0f, 1f)] public float abilityLoopSfxVolume = 0.9f;
    public AudioClip trailDetonationSfx;
    [Range(0f, 1f)] public float trailDetonationSfxVolume = 1f;
    public AudioClip cloneSummonSfx;
    [Range(0f, 1f)] public float cloneSummonSfxVolume = 1f;
    public AudioClip cloneDashSfx;
    [Range(0f, 1f)] public float cloneDashSfxVolume = 1f;
    public AudioClip chainStartSfx;
    [Range(0f, 1f)] public float chainStartSfxVolume = 1f;
    public AudioClip chainDashSfx;
    [Range(0f, 1f)] public float chainDashSfxVolume = 1f;
    public AudioClip chainLightningHitSfx;
    [Range(0f, 1f)] public float chainLightningHitSfxVolume = 1f;
    public AudioClip chainLoopSfx;
    [Range(0f, 1f)] public float chainLoopSfxVolume = 0.9f;

    private void Awake()
    {
        dashAudioSource = EnsureSource(dashAudioSource, false);
        movementLoopSource = EnsureSource(movementLoopSource, true);
        chargeLoopSource = EnsureSource(chargeLoopSource, true);
        dashLoopSource = EnsureSource(dashLoopSource, true);
        superLoopSource = EnsureSource(superLoopSource, true);
        abilityLoopSource = EnsureSource(abilityLoopSource, true);
        chainLoopSource = EnsureSource(chainLoopSource, true);
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

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null || dashAudioSource == null)
            return;

        dashAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
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

    public void SetWalkingSfxActive(bool isActive)
    {
        AudioClip loopClip = walkLoopSfx != null ? walkLoopSfx : walkSfx;
        float volume = walkLoopSfx != null ? walkLoopSfxVolume : walkSfxVolume;

        if (isActive)
            StartLoop(movementLoopSource, loopClip, volume);
        else
            StopLoop(movementLoopSource);
    }

    public void SetChargeSfxActive(bool isActive)
    {
        AudioClip loopClip = chargeLoopSfx != null ? chargeLoopSfx : chargeStartSfx;
        float volume = chargeLoopSfx != null ? chargeLoopSfxVolume : chargeStartSfxVolume;

        if (isActive)
            StartLoop(chargeLoopSource, loopClip, volume);
        else
            StopLoop(chargeLoopSource);
    }

    public void SetDashSfxActive(bool isActive)
    {
        AudioClip loopClip = dashLoopSfx != null ? dashLoopSfx : dashSfx;
        float volume = dashLoopSfx != null ? dashLoopSfxVolume : dashSfxVolume;

        if (isActive)
            StartLoop(dashLoopSource, loopClip, volume);
        else
            StopLoop(dashLoopSource);
    }

    public void SetSuperSfxActive(bool isActive)
    {
        AudioClip loopClip = superActiveSfx != null ? superActiveSfx : superLoopSfx;
        float volume = superActiveSfx != null ? superActiveSfxVolume : superLoopSfxVolume;

        if (isActive)
            StartLoop(superLoopSource, loopClip, volume);
        else
            StopLoop(superLoopSource);
    }

    public void SetAbilitySfxActive(bool isActive)
    {
        AudioClip loopClip = abilityLoopSfx;
        float volume = abilityLoopSfxVolume;

        if (isActive)
            StartLoop(abilityLoopSource, loopClip, volume);
        else
            StopLoop(abilityLoopSource);
    }

    public void SetChainSfxActive(bool isActive)
    {
        AudioClip loopClip = chainLoopSfx != null ? chainLoopSfx : chainStartSfx;
        float volume = chainLoopSfx != null ? chainLoopSfxVolume : chainStartSfxVolume;

        if (isActive)
            StartLoop(chainLoopSource, loopClip, volume);
        else
            StopLoop(chainLoopSource);
    }

    public void StopAllLoopingSfx()
    {
        StopLoop(movementLoopSource);
        StopLoop(chargeLoopSource);
        StopLoop(dashLoopSource);
        StopLoop(superLoopSource);
        StopLoop(abilityLoopSource);
        StopLoop(chainLoopSource);
    }

    public void PlayChargeStartSfx()
    {
        PlayClip(chargeStartSfx, chargeStartSfxVolume);
    }

    public void PlayChargeReleaseSfx()
    {
        PlayClip(chargeReleaseSfx, chargeReleaseSfxVolume);
    }

    public void PlayDashSfx()
    {
        PlayClip(dashSfx, dashSfxVolume);
    }

    public void PlayWalkSfx()
    {
        PlayClip(walkSfx, walkSfxVolume);
    }

    public void PlayImpactSfx()
    {
        PlayClip(impactSfx, impactSfxVolume);
    }

    public void PlayAbilityActivateSfx()
    {
        PlayClip(abilityActivateSfx, abilityActivateSfxVolume);
    }

    public void PlayAbilityReadySfx()
    {
        PlayClip(abilityReadySfx, abilityReadySfxVolume);
    }

    public void PlayAbilityErrorSfx()
    {
        PlayClip(abilityErrorSfx, abilityErrorSfxVolume);
    }

    public void PlayDamageSfx()
    {
        PlayClip(damageSfx, damageSfxVolume);
    }

    public void PlaySuperActivateSfx()
    {
        PlayClip(superActivateSfx, superActivateSfxVolume);
    }

    public void PlaySuperReadySfx()
    {
        PlayClip(superReadySfx, superReadySfxVolume);
    }

    public void PlaySuperErrorSfx()
    {
        PlayClip(superErrorSfx, superErrorSfxVolume);
    }

    public void PlaySuperEndSfx()
    {
        PlayClip(superEndSfx, superEndSfxVolume);
    }

    public void PlayGhostProjectileFireSfx()
    {
        PlayClip(ghostProjectileFireSfx, ghostProjectileFireSfxVolume);
    }

    public void PlayTrailDetonationSfx()
    {
        PlayClip(trailDetonationSfx, trailDetonationSfxVolume);
    }

    public void PlayCloneSummonSfx()
    {
        PlayClip(cloneSummonSfx, cloneSummonSfxVolume);
    }

    public void PlayCloneDashSfx()
    {
        PlayClip(cloneDashSfx, cloneDashSfxVolume);
    }

    public void PlayChainStartSfx()
    {
        PlayClip(chainStartSfx, chainStartSfxVolume);
    }

    public void PlayChainDashSfx()
    {
        if (chainDashSfx != null)
            PlayClip(chainDashSfx, chainDashSfxVolume);
        else
            PlayDashSfx();
    }

    public void PlayChainLightningHitSfx()
    {
        if (chainLightningHitSfx != null)
            PlayClip(chainLightningHitSfx, chainLightningHitSfxVolume);
        else
            PlayImpactSfx();
    }

    public void PlayDeathSfx()
    {
        PlayClip(deathSfx, deathSfxVolume);
    }
}
