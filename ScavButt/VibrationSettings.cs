using static ScavLib.util.PlayerUtil.Thresholds;

namespace ScavButt;

/// <summary>
/// Centralised tuning knobs and on/off flags for the vibration system.
/// Fields are public statics — wire them to BepInEx config entries or a
/// settings GUI as needed; VibrationSystem reads them every tick.
/// </summary>
public static class VibrationSettings
{
    // ── Global ──────────────────────────────────────────────────────────────
    public static bool  EnableVibration = true;
    /// <summary>Scales the computed waveform before any device limits are applied (0–1).</summary>
    public static float GlobalScale     = 1.0f;
    /// <summary>
    /// Hardware intensity ceiling sent to the device (0–100).
    /// 100 = full device range; 1 = only 0–1 % of device range.
    /// Applied in ButtplugManager.Vibrate() after waveform computation.
    /// Lovense app intensity limits do NOT apply here — buttplug.io talks
    /// directly to firmware over BLE, bypassing the Lovense app entirely.
    /// </summary>
    public static float MaxDeviceIntensity = 100f;

    // ── Heartbeat ────────────────────────────────────────────────────────────
    public static bool  EnableHeartbeat     = true;
    public static float AmpHeartbeatBrady   = 0.40f;
    public static float AmpHeartbeatTachy   = 0.60f;
    public static float AmpHeartbeatArrest  = 0.30f;
    /// <summary>HR below this starts bradycardia feedback (clinical onset ~60 BPM).</summary>
    public static float HrFeelLow           = 60f;
    /// <summary>HR above this starts tachycardia feedback (clinical onset ~110 BPM).</summary>
    public static float HrFeelHigh          = 110f;
    /// <summary>Position of the main (large) heartbeat peak within the [0,1] beat phase.</summary>
    public static float HeartbeatMu1        = 0.10f;
    /// <summary>Position of the secondary (small) heartbeat peak — further from Mu1 = more distinct BUMP-bump gap.</summary>
    public static float HeartbeatMu2        = 0.26f;
    public static float HeartbeatSigma      = 0.03f;
    /// <summary>Relative amplitude of the second peak (0 = no second bump, 1 = equal to first).</summary>
    public static float HeartbeatPeak2Ratio = 0.60f;

    // ── Bleed ────────────────────────────────────────────────────────────────
    public static bool  EnableBleed      = true;
    /// <summary>Flat amplitude added per unit of internal bleeding (0–100 scale).</summary>
    public static float AmpInternalBleed = 0.20f;
    /// <summary>Bleed speed at which vibration begins (default: BLEED_SPEED_CRITICAL — needs serious bleeding).</summary>
    public static float BleedOnset;
    /// <summary>Bleed speed at which vibration reaches BleedMaxAmp (default: 2.5× critical — extreme haemorrhage).</summary>
    public static float BleedCeiling;
    /// <summary>Maximum amplitude bleed can contribute on its own — keeps it from saturating the output alone.</summary>
    public static float BleedMaxAmp  = 0.65f;
    /// <summary>Pulse frequency (Hz) at BleedOnset — slow throb at the low end.</summary>
    public static float BleedFreqMin = 0.15f;
    /// <summary>Pulse frequency (Hz) at BleedCeiling — still slow; keeps bleed from feeling frantic.</summary>
    public static float BleedFreqMax = 0.50f;

    // ── Pain / Shock ─────────────────────────────────────────────────────────
    public static bool  EnablePainShock  = true;
    public static float AmpPainShock     = 0.50f;
    public static float PainBuzzFreqMin  = 5f;
    public static float PainBuzzFreqMax  = 12f;

    // ── Horror ───────────────────────────────────────────────────────────────
    public static bool  EnableHorror    = true;
    public static float AmpHorror       = 0.45f;
    public static float HorrorFreqMin   = 1f;
    public static float HorrorFreqMax   = 8f;

    // ── Fibrillation ─────────────────────────────────────────────────────────
    public static bool  EnableFibrillation = true;
    public static float AmpFibrillation    = 0.40f;
    public static float FibFreqMin         = 8f;
    public static float FibFreqMax         = 15f;

    // ── Impact transient ─────────────────────────────────────────────────────
    public static bool  EnableImpact              = true;
    public static float AmpImpactBoneBreak        = 0.40f;
    public static float AmpImpactDismember        = 0.60f;
    /// <summary>Exponential decay coefficient — higher = shorter transient. Default ≈ 500 ms effective.</summary>
    public static float ImpactDecay               = 8f;
    public static float ImpactFreq                = 8f;
    public static float ImpactShockDeltaThreshold = 5f;
    public static float ImpactPainDeltaThreshold  = 8f;

    // ── Trauma ───────────────────────────────────────────────────────────────
    public static bool  EnableTrauma  = true;
    public static float AmpTrauma     = 0.20f;
    public static float TraumaFreq    = 0.25f;
    /// <summary>Trauma amount below which there is no vibration contribution.</summary>
    public static float TraumaFloor   = 40f;

    // bleed thresholds depend on game constants — compute after static init
    static VibrationSettings()
    {
        BleedOnset   = BLEED_SPEED_CRITICAL;           // need critical bleed to feel anything
        BleedCeiling = BLEED_SPEED_CRITICAL * 2.5f;    // extreme haemorrhage to reach max amp
    }
}
