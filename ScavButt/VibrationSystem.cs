using System.Collections;
using UnityEngine;
using ScavLib.util;
using static ScavLib.util.PlayerUtil.Thresholds;

namespace ScavButt;

public class VibrationSystem : MonoBehaviour
{
    // amplitude constants — tune after real-device testing
    private const float AMP_HEARTBEAT_BRADY  = 0.40f;
    private const float AMP_HEARTBEAT_TACHY  = 0.60f;
    private const float AMP_HEARTBEAT_ARREST = 0.30f;
    private const float AMP_INTERNAL_BLEED   = 0.30f;
    private const float AMP_PAIN_SHOCK       = 0.50f;
    private const float AMP_HORROR           = 0.45f;
    private const float AMP_FIBRILLATION     = 0.40f;
    private const float AMP_TRAUMA           = 0.20f;

    private const float OPIATE_MAX = 80f;
    private const float TRAUMA_FLOOR = 40f;

    // heartbeat starts vibrating before the clinical thresholds — feel it sooner
    private const float HR_FEEL_LOW  = 70f;  // vibration onset for bradycardia (clinical mild = 60)
    private const float HR_FEEL_HIGH = 95f;  // vibration onset for tachycardia (clinical mild = 110)

    private const float DT = 0.05f; // 20 Hz poll interval

    // global timer used by all sine-based components
    private float _time;

    // impact transient state
    private float _impactAmp;
    private float _impactTime = 999f;
    private float _prevShock;
    private float _prevPain;
    private bool  _prevBoneBroke;
    private bool  _prevDismember;

    // heartbeat state
    private float _beatPhase;
    private float _arrestPeriod  = 0.8f;
    private float _fibJitter;
    private float _fibJitterTimer;

    // horror state
    private float _horrorFreq        = 4f;
    private float _horrorFreqTarget  = 4f;
    private float _horrorFreqTimer;
    private float _horrorNoise;
    private float _horrorNoiseTarget;
    private float _horrorNoiseTimer;

    // fibrillation burst state
    private float _fibBurstFreq;
    private float _fibBurstTimer;

    public void StartPolling() => StartCoroutine(PollLoop());

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(DT);

            if (!ButtplugManager.IsConnected) continue;

            _time += DT;
            ButtplugManager.Vibrate((double)ComputeOutput());
        }
    }

    private float ComputeOutput()
    {
        DetectSpikes();

        float heartbeat    = HeartbeatComponent();
        float bleed        = BleedComponent();
        float painShock    = PainShockComponent();
        float horror       = HorrorComponent();
        float fibrillation = FibrillationComponent();
        float impact       = ImpactComponent();
        float trauma       = TraumaComponent();

        float consciousness = PlayerUtil.GetConsciousness();
        float cMult = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((consciousness - CONSCIOUSNESS_UNCONSCIOUS) / 40f));

        float opiateRaw  = PlayerUtil.GetOpiateHappiness();
        float opiate     = Mathf.Clamp01(opiateRaw / OPIATE_MAX);
        float opiateMult = Mathf.Lerp(1f, 0.15f, opiate);
        float pillsMult  = PlayerUtil.HasPainkillers() ? 0.4f : 1.0f;

        // heartbeat and fibrillation are exempt from consciousness and drug scaling
        bleed        *= cMult;
        painShock    *= cMult * pillsMult * opiateMult;
        horror       *= cMult * Mathf.Lerp(1f, 0.5f, opiate);
        trauma       *= cMult * pillsMult * opiateMult;
        impact       *= opiateMult; // conscious-independent; painkillers skipped (only opiates)

        float rawSum = heartbeat + bleed + painShock + horror
                     + fibrillation + impact + trauma;

        // soft normalization: scale non-impact components so impact punches through
        if (rawSum > 1.0f)
        {
            float nonImpact = rawSum - impact;
            if (nonImpact > 0f)
            {
                float scale = Mathf.Clamp01((1.0f - impact) / nonImpact);
                heartbeat    *= scale;
                bleed        *= scale;
                painShock    *= scale;
                horror       *= scale;
                fibrillation *= scale;
                trauma       *= scale;
            }
        }

        return Mathf.Clamp01(heartbeat + bleed + painShock + horror
                            + fibrillation + impact + trauma);
    }

    private float HeartbeatComponent()
    {
        float hr        = PlayerUtil.GetHeartRate();
        bool  arrested  = PlayerUtil.IsInCardiacArrest();
        float fibProg   = PlayerUtil.GetFibrillationProgress();
        bool  fibForced = PlayerUtil.IsFibrillationForced();

        // always advance beat phase to stay synchronized
        float period = (arrested || fibForced)
            ? _arrestPeriod
            : 60f / Mathf.Max(hr, 1f);

        _beatPhase += (1f / period) * DT;
        if (_beatPhase >= 1f)
        {
            _beatPhase -= 1f;
            if (arrested || fibForced)
                _arrestPeriod = UnityEngine.Random.Range(0.3f, 2.5f);
        }

        float amp;
        if (arrested)
        {
            amp = AMP_HEARTBEAT_ARREST;
        }
        else if (hr < HR_FEEL_LOW)
        {
            amp = InvLerp(HR_FEEL_LOW, HEART_RATE_BRADYCARDIA_SEVERE, hr)
                * AMP_HEARTBEAT_BRADY;
        }
        else if (hr > HR_FEEL_HIGH)
        {
            amp = InvLerp(HR_FEEL_HIGH, HEART_RATE_TACHYCARDIA_CRITICAL, hr)
                * AMP_HEARTBEAT_TACHY;
        }
        else
        {
            return 0f; // normal range — no haptic feedback
        }

        // fibrillation jitter shifts the peak positions
        float fibNorm = fibProg / 100f;
        if (fibNorm > 0f)
        {
            _fibJitterTimer -= DT;
            if (_fibJitterTimer <= 0f)
            {
                _fibJitter      = UnityEngine.Random.Range(-1f, 1f) * fibNorm * 0.12f;
                _fibJitterTimer = UnityEngine.Random.Range(0.05f, 0.15f);
            }
        }
        else
        {
            _fibJitter = 0f;
        }

        // BUMP-bump two-peak Gaussian waveform within the beat cycle
        float mu1    = 0.10f + _fibJitter;
        float mu2    = 0.20f + _fibJitter * 0.5f;
        const float sigma = 0.03f;

        float wave = Gauss(_beatPhase, mu1, sigma)
                   + 0.6f * Gauss(_beatPhase, mu2, sigma);

        return Mathf.Clamp01(wave) * amp;
    }

    private float BleedComponent()
    {
        float bleedSpeed   = PlayerUtil.GetTotalBleedSpeed();
        float bleedNorm    = InvLerp(BLEED_SPEED_MEDIUM, BLEED_SPEED_CRITICAL, bleedSpeed);
        float internalNorm = PlayerUtil.GetInternalBleeding() / 100f;

        float amp = bleedNorm + internalNorm * AMP_INTERNAL_BLEED;
        if (amp <= 0f) return 0f;

        float freq = Mathf.Lerp(0.4f, 1.2f, bleedNorm);
        float wave = (Mathf.Sin(2f * Mathf.PI * freq * _time) + 1f) * 0.5f;

        return wave * Mathf.Clamp01(amp);
    }

    private float PainShockComponent()
    {
        float painNorm  = InvLerp(PAIN_MODERATE, PAIN_AGONY, PlayerUtil.GetAveragePain());
        float shockNorm = PlayerUtil.GetShock() / 100f;
        float amp       = Mathf.Max(painNorm, shockNorm) * AMP_PAIN_SHOCK;
        if (amp <= 0f) return 0f;

        // constant uncomfortable buzz that never drops to zero;
        // tremor frequency scales with pain so worse pain feels more agitated
        float buzzFreq = Mathf.Lerp(5f, 12f, painNorm);
        float tremor   = Mathf.Sin(2f * Mathf.PI * buzzFreq * _time) * 0.15f;
        return Mathf.Clamp01(amp * (0.85f + tremor));
    }

    private float HorrorComponent()
    {
        float horrorNorm = PlayerUtil.GetHorrifiedLevel() / 200f;
        float amp        = horrorNorm * AMP_HORROR;
        if (amp <= 0f) return 0f;

        // random frequency walk between 1–8 Hz
        _horrorFreqTimer -= DT;
        if (_horrorFreqTimer <= 0f)
        {
            _horrorFreqTarget = UnityEngine.Random.Range(1f, 8f);
            _horrorFreqTimer  = UnityEngine.Random.Range(0.1f, 0.5f);
        }
        _horrorFreq = Mathf.Lerp(_horrorFreq, _horrorFreqTarget, 0.15f);

        // per-tick noise smoothed over ~100 ms
        _horrorNoiseTimer -= DT;
        if (_horrorNoiseTimer <= 0f)
        {
            _horrorNoiseTarget = UnityEngine.Random.Range(0f, 1f);
            _horrorNoiseTimer  = 0.1f;
        }
        _horrorNoise = Mathf.Lerp(_horrorNoise, _horrorNoiseTarget, 0.5f);

        float sinWave = (Mathf.Sin(2f * Mathf.PI * _horrorFreq * _time) + 1f) * 0.5f;
        float wave    = Mathf.Lerp(sinWave, _horrorNoise, horrorNorm);

        return wave * amp;
    }

    private float FibrillationComponent()
    {
        float fibNorm = PlayerUtil.GetFibrillationProgress() / 100f;
        float amp     = fibNorm * AMP_FIBRILLATION;
        if (amp <= 0f) return 0f;

        // random high-frequency bursts, updated every 80–200 ms
        _fibBurstTimer -= DT;
        if (_fibBurstTimer <= 0f)
        {
            _fibBurstFreq  = UnityEngine.Random.Range(8f, 15f);
            _fibBurstTimer = UnityEngine.Random.Range(0.08f, 0.20f);
        }

        float wave = (Mathf.Sin(2f * Mathf.PI * _fibBurstFreq * _time) + 1f) * 0.5f;
        return wave * amp;
    }

    private float ImpactComponent()
    {
        float decay = Mathf.Exp(-8f * _impactTime);
        float wave  = (Mathf.Sin(2f * Mathf.PI * 8f * _time) + 1f) * 0.5f;
        return wave * _impactAmp * decay;
    }

    private float TraumaComponent()
    {
        float traumaNorm = InvLerp(TRAUMA_FLOOR, 100f, PlayerUtil.GetTraumaAmount());
        float amp        = traumaNorm * AMP_TRAUMA;
        if (amp <= 0f) return 0f;

        float wave = (Mathf.Sin(2f * Mathf.PI * 0.25f * _time) + 1f) * 0.5f;
        return wave * amp;
    }

    private void DetectSpikes()
    {
        float shock     = PlayerUtil.GetShock();
        float pain      = PlayerUtil.GetAveragePain();
        bool  boneBroke = LimbUtil.HasBrokenBone();
        bool  dismember = LimbUtil.HasDismemberment();

        float shockDelta   = shock - _prevShock;
        float painDelta    = pain  - _prevPain;
        bool  newBreak     = boneBroke && !_prevBoneBroke;
        bool  newDismember = dismember && !_prevDismember;

        if (shockDelta > 5f || painDelta > 8f || newBreak || newDismember)
        {
            _impactAmp  = Mathf.Clamp01(Mathf.Max(shockDelta, painDelta) / 50f)
                        + (newBreak     ? 0.4f : 0f)
                        + (newDismember ? 0.6f : 0f);
            _impactAmp  = Mathf.Clamp01(_impactAmp);
            _impactTime = 0f;
        }
        else
        {
            _impactTime += DT;
        }

        _prevShock     = shock;
        _prevPain      = pain;
        _prevBoneBroke = boneBroke;
        _prevDismember = dismember;
    }

    // wrap-aware Gaussian bell curve for heartbeat waveform
    private static float Gauss(float x, float mu, float sigma)
    {
        float d = x - mu;
        if (d >  0.5f) d -= 1f;
        if (d < -0.5f) d += 1f;
        return Mathf.Exp(-0.5f * (d / sigma) * (d / sigma));
    }

    private static float InvLerp(float a, float b, float v)
        => Mathf.Clamp01((v - a) / (b - a));
}
