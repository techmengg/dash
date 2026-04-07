using System.Collections;
using UnityEngine;

public class EnemyAudio : MonoBehaviour
{
    [Header("Enemy Audio Source")]
    public AudioSource enemyAudioSource;

    [Header("Enemy Loop Audio Sources")]
    public AudioSource attackLoopSource;
    public AudioSource rollLoopSource;

    [Header("Enemy Core SFX")]
    public AudioClip spawnSfx;
    [Range(0f, 1f)] public float spawnSfxVolume = 0.9f;
    public AudioClip damageSfx;
    [Range(0f, 1f)] public float damageSfxVolume = 1f;
    public AudioClip deathSfx;
    [Range(0f, 1f)] public float deathSfxVolume = 1f;
    public AudioClip attackSfx;
    [Range(0f, 1f)] public float attackSfxVolume = 0.9f;
    public AudioClip attackLoopSfx;
    [Range(0f, 1f)] public float attackLoopSfxVolume = 0.85f;
    public AudioClip rollLoopSfx;
    [Range(0f, 1f)] public float rollLoopSfxVolume = 1f;
    public AudioClip stunSfx;
    [Range(0f, 1f)] public float stunSfxVolume = 0.9f;

    [Header("Projectile SFX")]
    public AudioClip projectileFireSfx;
    [Range(0f, 1f)] public float projectileFireSfxVolume = 0.9f;
    public AudioClip projectileImpactSfx;
    [Range(0f, 1f)] public float projectileImpactSfxVolume = 1f;

    [Header("Idle Grunts")]
    public bool playRandomGrunts = true;
    public AudioClip[] randomGruntSfx;
    [Range(0f, 1f)] public float randomGruntVolume = 0.75f;
    public float randomGruntMinInterval = 3.5f;
    public float randomGruntMaxInterval = 8f;

    private Coroutine randomGruntCoroutine;

    private void Awake()
    {
        enemyAudioSource = EnsureSource(enemyAudioSource, false);
        attackLoopSource = EnsureSource(attackLoopSource, true);
        rollLoopSource = EnsureSource(rollLoopSource, true);
    }

    private void OnEnable()
    {
        StartRandomGrunts();
    }

    private void OnDisable()
    {
        StopRandomGrunts();
        StopAllLoopingSfx();
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

    public void PlayDamageSfx()
    {
        PlayClip(damageSfx, damageSfxVolume);
    }

    public void PlayDeathSfx()
    {
        PlayClip(deathSfx, deathSfxVolume);
    }

    public void PlayAttackSfx()
    {
        PlayClip(attackSfx, attackSfxVolume);
    }

    public void PlayStunSfx()
    {
        PlayClip(stunSfx, stunSfxVolume);
    }

    public void PlaySpawnSfx()
    {
        PlayClip(spawnSfx, spawnSfxVolume);
    }

    public void PlayProjectileFireSfx()
    {
        if (projectileFireSfx != null)
            PlayClip(projectileFireSfx, projectileFireSfxVolume);
        else
            PlayAttackSfx();
    }

    public void PlayProjectileImpactSfx()
    {
        if (projectileImpactSfx != null)
            PlayClip(projectileImpactSfx, projectileImpactSfxVolume);
        else
            PlayImpactFallback();
    }

    public void SetAttackLoopActive(bool isActive)
    {
        AudioClip loopClip = attackLoopSfx != null ? attackLoopSfx : attackSfx;
        float volume = attackLoopSfx != null ? attackLoopSfxVolume : attackSfxVolume;

        if (isActive)
            StartLoop(attackLoopSource, loopClip, volume);
        else
            StopLoop(attackLoopSource);
    }

    public void SetRollLoopActive(bool isActive)
    {
        AudioClip loopClip = rollLoopSfx != null ? rollLoopSfx : (attackLoopSfx != null ? attackLoopSfx : attackSfx);
        float volume = rollLoopSfx != null ? rollLoopSfxVolume : (attackLoopSfx != null ? attackLoopSfxVolume : attackSfxVolume);

        if (isActive)
            StartLoop(rollLoopSource, loopClip, volume);
        else
            StopLoop(rollLoopSource);
    }

    public void StopAllLoopingSfx()
    {
        StopLoop(attackLoopSource);
        StopLoop(rollLoopSource);
    }

    public void StartRandomGrunts()
    {
        if (!playRandomGrunts)
            return;

        if (randomGruntSfx == null || randomGruntSfx.Length == 0)
            return;

        if (randomGruntCoroutine != null)
            return;

        randomGruntCoroutine = StartCoroutine(RandomGruntRoutine());
    }

    public void StopRandomGrunts()
    {
        if (randomGruntCoroutine == null)
            return;

        StopCoroutine(randomGruntCoroutine);
        randomGruntCoroutine = null;
    }

    private void PlayImpactFallback()
    {
        if (damageSfx != null)
            PlayClip(damageSfx, damageSfxVolume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null || enemyAudioSource == null)
            return;

        enemyAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
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

    private IEnumerator RandomGruntRoutine()
    {
        while (isActiveAndEnabled)
        {
            float minDelay = Mathf.Max(0.2f, randomGruntMinInterval);
            float maxDelay = Mathf.Max(minDelay, randomGruntMaxInterval);
            float wait = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(wait);

            if (!isActiveAndEnabled || enemyAudioSource == null)
                continue;

            if (randomGruntSfx == null || randomGruntSfx.Length == 0)
                continue;

            AudioClip clip = randomGruntSfx[Random.Range(0, randomGruntSfx.Length)];
            if (clip == null)
                continue;

            enemyAudioSource.PlayOneShot(clip, Mathf.Clamp01(randomGruntVolume));
        }

        randomGruntCoroutine = null;
    }
}
