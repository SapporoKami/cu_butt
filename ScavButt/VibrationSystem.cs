using System.Collections;
using UnityEngine;
using ScavLib.util;
using static ScavLib.util.PlayerUtil.Thresholds;
using static ScavLib.util.GameUtil;

namespace ScavButt;

public class VibrationSystem : MonoBehaviour
{
    private const float OPIATE_MAX = 80f;
    private const float DT = 0.05f; // 20 Hz poll interval

    // global timer used by all sine-based components
    private float _time;

    // debug overlay
    public static bool ShowDebug;
    private const int RING_SIZE = 200; // 10 s at 20 Hz
    private readonly float[] _ring = new float[RING_SIZE];
    private int _ringHead;
    private float _dbgHeartbeat, _dbgBleed, _dbgPainShock, _dbgHorror,
                  _dbgFibrillation, _dbgImpact, _dbgTrauma, _dbgTotal;

    // impact transient state
    private float _impactAmp;
    private float _impactTime = 999f;
    private float _prevShock;
    private float _prevPain;
    private bool  _prevBoneBroke;
    private bool  _prevDismember;

    // player-presence tracking for main-menu guard
    private bool _playerWasPresent;

    // heartbeat state
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

    private static VibrationSystem? _instance;

    public void StartPolling()
    {
        _instance = this;
        StartCoroutine(PollLoop());
    }

    public static void TriggerTest()
    {
        if (_instance == null) return;
        _instance._impactAmp  = 0.5f;
        _instance._impactTime = 0f;
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(DT);
            // skip all computation when nothing needs it — saves CPU when idle
            if (!ButtplugManager.IsConnected && !ShowDebug)
                continue;

            // skip when no player is actually in a run (main menu, loading screens, etc.)
            // PlayerUtil returns false safe-defaults when no world is loaded
            bool playerPresent = PlayerUtil.IsAlive()
                              || PlayerUtil.IsDying()
                              || PlayerUtil.IsInCardiacArrest();
            if (!playerPresent)
            {
                if (_playerWasPresent && ButtplugManager.IsConnected)
                    ButtplugManager.StopAll(); // silence motors on the frame we lose the player
                _playerWasPresent = false;
                continue;
            }
            _playerWasPresent = true;

            _time += DT;
            float output = ComputeOutput();
            if (ButtplugManager.IsConnected)
                ButtplugManager.Vibrate((double)output);
        }
    }

    private float ComputeOutput()
    {
        if (!VibrationSettings.EnableVibration) return 0f;

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

        float final = Mathf.Clamp01(
            (heartbeat + bleed + painShock + horror + fibrillation + impact + trauma)
            * VibrationSettings.GlobalScale);

        _dbgHeartbeat    = heartbeat;
        _dbgBleed        = bleed;
        _dbgPainShock    = painShock;
        _dbgHorror       = horror;
        _dbgFibrillation = fibrillation;
        _dbgImpact       = impact;
        _dbgTrauma       = trauma;
        _dbgTotal        = final;
        _ring[_ringHead] = final;
        _ringHead        = (_ringHead + 1) % RING_SIZE;
        return final;
    }

    private float HeartbeatComponent()
    {
        if (!VibrationSettings.EnableHeartbeat) return 0f;

        var   body     = GetBody();
        float hr       = PlayerUtil.GetHeartRate();
        bool  arrested = PlayerUtil.IsInCardiacArrest();
        float fibProg  = PlayerUtil.GetFibrillationProgress();

        // Read the game's own beat phase — perfectly synced to the audio/EEG display.
        float beatPhase = body != null ? body.heartProg : 0f;

        float amp;
        if (arrested)
        {
            amp = VibrationSettings.AmpHeartbeatArrest;
        }
        else if (hr < VibrationSettings.HrFeelLow)
        {
            amp = InvLerp(VibrationSettings.HrFeelLow, HEART_RATE_BRADYCARDIA_SEVERE, hr)
                * VibrationSettings.AmpHeartbeatBrady;
        }
        else if (hr > VibrationSettings.HrFeelHigh)
        {
            amp = InvLerp(VibrationSettings.HrFeelHigh, HEART_RATE_TACHYCARDIA_CRITICAL, hr)
                * VibrationSettings.AmpHeartbeatTachy;
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
        float mu1  = VibrationSettings.HeartbeatMu1 + _fibJitter;
        float mu2  = VibrationSettings.HeartbeatMu2 + _fibJitter * 0.5f;
        float sig  = VibrationSettings.HeartbeatSigma;

        float wave = Gauss(beatPhase, mu1, sig)
                   + VibrationSettings.HeartbeatPeak2Ratio * Gauss(beatPhase, mu2, sig);

        return Mathf.Clamp01(wave) * amp;
    }

    private float BleedComponent()
    {
        if (!VibrationSettings.EnableBleed) return 0f;

        float bleedSpeed   = PlayerUtil.GetTotalBleedSpeed();
        float bleedNorm    = InvLerp(VibrationSettings.BleedOnset, VibrationSettings.BleedCeiling, bleedSpeed);
        float internalNorm = PlayerUtil.GetInternalBleeding() / 100f;

        float amp = (bleedNorm + internalNorm * VibrationSettings.AmpInternalBleed)
                  * VibrationSettings.BleedMaxAmp;
        if (amp <= 0f) return 0f;

        float freq = Mathf.Lerp(VibrationSettings.BleedFreqMin, VibrationSettings.BleedFreqMax, bleedNorm);
        float wave = (Mathf.Sin(2f * Mathf.PI * freq * _time) + 1f) * 0.5f;

        return wave * Mathf.Clamp01(amp);
    }

    private float PainShockComponent()
    {
        if (!VibrationSettings.EnablePainShock) return 0f;

        float painNorm  = InvLerp(PAIN_MODERATE, PAIN_AGONY, PlayerUtil.GetAveragePain());
        float shockNorm = PlayerUtil.GetShock() / 100f;
        float amp       = Mathf.Max(painNorm, shockNorm) * VibrationSettings.AmpPainShock;
        if (amp <= 0f) return 0f;

        // constant uncomfortable buzz that never drops to zero;
        // tremor frequency scales with pain so worse pain feels more agitated
        float buzzFreq = Mathf.Lerp(VibrationSettings.PainBuzzFreqMin, VibrationSettings.PainBuzzFreqMax, painNorm);
        float tremor   = Mathf.Sin(2f * Mathf.PI * buzzFreq * _time) * 0.15f;
        return Mathf.Clamp01(amp * (0.85f + tremor));
    }

    private float HorrorComponent()
    {
        if (!VibrationSettings.EnableHorror) return 0f;

        float horrorNorm = PlayerUtil.GetHorrifiedLevel() / 200f;
        float amp        = horrorNorm * VibrationSettings.AmpHorror;
        if (amp <= 0f) return 0f;

        // random frequency walk
        _horrorFreqTimer -= DT;
        if (_horrorFreqTimer <= 0f)
        {
            _horrorFreqTarget = UnityEngine.Random.Range(VibrationSettings.HorrorFreqMin, VibrationSettings.HorrorFreqMax);
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
        if (!VibrationSettings.EnableFibrillation) return 0f;

        float fibNorm = PlayerUtil.GetFibrillationProgress() / 100f;
        float amp     = fibNorm * VibrationSettings.AmpFibrillation;
        if (amp <= 0f) return 0f;

        // random high-frequency bursts
        _fibBurstTimer -= DT;
        if (_fibBurstTimer <= 0f)
        {
            _fibBurstFreq  = UnityEngine.Random.Range(VibrationSettings.FibFreqMin, VibrationSettings.FibFreqMax);
            _fibBurstTimer = UnityEngine.Random.Range(0.08f, 0.20f);
        }

        float wave = (Mathf.Sin(2f * Mathf.PI * _fibBurstFreq * _time) + 1f) * 0.5f;
        return wave * amp;
    }

    private float ImpactComponent()
    {
        if (!VibrationSettings.EnableImpact) return 0f;

        float decay = Mathf.Exp(-VibrationSettings.ImpactDecay * _impactTime);
        float wave  = (Mathf.Sin(2f * Mathf.PI * VibrationSettings.ImpactFreq * _time) + 1f) * 0.5f;
        return wave * _impactAmp * decay;
    }

    private float TraumaComponent()
    {
        if (!VibrationSettings.EnableTrauma) return 0f;

        float traumaNorm = InvLerp(VibrationSettings.TraumaFloor, 100f, PlayerUtil.GetTraumaAmount());
        float amp        = traumaNorm * VibrationSettings.AmpTrauma;
        if (amp <= 0f) return 0f;

        float wave = (Mathf.Sin(2f * Mathf.PI * VibrationSettings.TraumaFreq * _time) + 1f) * 0.5f;
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

        if (shockDelta > VibrationSettings.ImpactShockDeltaThreshold
         || painDelta  > VibrationSettings.ImpactPainDeltaThreshold
         || newBreak || newDismember)
        {
            _impactAmp  = Mathf.Clamp01(Mathf.Max(shockDelta, painDelta) / 50f)
                        + (newBreak     ? VibrationSettings.AmpImpactBoneBreak  : 0f)
                        + (newDismember ? VibrationSettings.AmpImpactDismember  : 0f);
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

    // ------------------------------------------------------------------ debug overlay

    private void OnGUI()
    {
        if (!ShowDebug) return;

        const float PAD     = 14f;
        const float GRAPH_W = 420f;
        const float GRAPH_H = 130f;
        const float ROW_H   = 24f;
        const float TITLE_H = 28f;
        const float LABEL_W = 90f;
        const float NUM_W   = 56f;
        const int   ROWS    = 9;
        float barW = GRAPH_W - LABEL_W - NUM_W;

        float contentH = TITLE_H + 6f + GRAPH_H + 8f + ROWS * (ROW_H + 3f);
        float boxW     = GRAPH_W + PAD * 2f;
        float boxH     = contentH + PAD * 2f;
        float boxX     = Screen.width - boxW - 12f;
        float boxY     = 12f;
        float cx       = boxX + PAD;
        float cy       = boxY + PAD;

        DrawRect(new Rect(boxX, boxY, boxW, boxH), new Color(0f, 0f, 0f, 0.80f));

        GUI.color = Color.white;
        GUI.Label(new Rect(cx, cy, GRAPH_W, TITLE_H), "ScavButt Visualizer");
        cy += TITLE_H + 6f;

        // oscilloscope waveform — scale 200 samples across GRAPH_W
        DrawRect(new Rect(cx, cy, GRAPH_W, GRAPH_H), new Color(0.08f, 0.08f, 0.08f, 1f));
        float slotW = GRAPH_W / RING_SIZE;
        for (int i = 0; i < RING_SIZE; i++)
        {
            float val = _ring[(_ringHead + i) % RING_SIZE];
            float bh  = val * GRAPH_H;
            DrawRect(new Rect(cx + i * slotW, cy + GRAPH_H - bh, slotW, bh), Color.white);
        }
        cy += GRAPH_H + 8f;

        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Heartbeat", _dbgHeartbeat,    new Color(1.0f, 0.25f, 0.25f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Bleed",     _dbgBleed,        new Color(0.85f, 0.0f, 0.1f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Pain",      _dbgPainShock,    new Color(1.0f, 0.55f, 0.0f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Horror",    _dbgHorror,       new Color(0.7f, 0.2f, 1.0f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Fibril",    _dbgFibrillation, new Color(1.0f, 0.3f, 0.85f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Impact",    _dbgImpact,       new Color(1.0f, 0.95f, 0.2f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Trauma",    _dbgTrauma,       new Color(0.55f, 0.55f, 0.55f));
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "TOTAL",     _dbgTotal,        Color.white);

        // hardware intensity cap — static display; the waveform graph above is NOT affected by this
        float hwCapNorm = Mathf.Clamp01(VibrationSettings.MaxDeviceIntensity / 100f);
        DrawRow(ref cy, cx, LABEL_W, barW, NUM_W, ROW_H, "Hw Cap %",  hwCapNorm,        new Color(0.35f, 0.75f, 1.0f));
    }

    private static void DrawRow(ref float cy, float cx, float labelW, float barW, float numW,
                                float rowH, string label, float value, Color color)
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(cx, cy, labelW, rowH), label);
        DrawRect(new Rect(cx + labelW, cy + 3f, barW * value, rowH - 6f), color);
        GUI.Label(new Rect(cx + labelW + barW, cy, numW, rowH), value.ToString("F2"));
        cy += rowH + 3f;
    }

    private static void DrawRect(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
