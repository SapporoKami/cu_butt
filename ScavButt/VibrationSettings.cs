using BepInEx.Configuration;

namespace ScavButt;

public static class VibrationSettings
{
    // ── Config entries ───────────────────────────────────────────────────────
    // All null until Initialize() is called from Plugin.Awake().

    // Global
    private static ConfigEntry<bool>  _enableVibration     = null!;
    private static ConfigEntry<float> _globalScale         = null!;
    private static ConfigEntry<float> _maxDeviceIntensity  = null!;
    private static ConfigEntry<float> _pollHz              = null!;
    private static ConfigEntry<float> _ffThreshold         = null!;
    private static ConfigEntry<bool>  _silenceOnFF         = null!;

    // Heartbeat
    private static ConfigEntry<bool>  _enableHeartbeat     = null!;
    private static ConfigEntry<float> _ampBrady            = null!;
    private static ConfigEntry<float> _ampTachy            = null!;
    private static ConfigEntry<float> _ampArrest           = null!;
    private static ConfigEntry<float> _hrFeelLow           = null!;
    private static ConfigEntry<float> _hrFeelHigh          = null!;
    private static ConfigEntry<float> _hbMu1               = null!;
    private static ConfigEntry<float> _hbMu2               = null!;
    private static ConfigEntry<float> _hbSigma             = null!;
    private static ConfigEntry<float> _hbPeak2Ratio        = null!;

    // Bleed
    private static ConfigEntry<bool>  _enableBleed         = null!;
    private static ConfigEntry<float> _ampInternalBleed    = null!;
    private static ConfigEntry<float> _bleedOnset          = null!;
    private static ConfigEntry<float> _bleedCeiling        = null!;
    private static ConfigEntry<float> _bleedMaxAmp         = null!;
    private static ConfigEntry<float> _bleedFreqMin        = null!;
    private static ConfigEntry<float> _bleedFreqMax        = null!;

    // Pain / Shock
    private static ConfigEntry<bool>  _enablePainShock     = null!;
    private static ConfigEntry<float> _ampPainShock        = null!;
    private static ConfigEntry<float> _painOnset           = null!;
    private static ConfigEntry<float> _painBuzzFreqMin     = null!;
    private static ConfigEntry<float> _painBuzzFreqMax     = null!;

    // Horror
    private static ConfigEntry<bool>  _enableHorror        = null!;
    private static ConfigEntry<float> _ampHorror           = null!;
    private static ConfigEntry<float> _horrorFreqMin       = null!;
    private static ConfigEntry<float> _horrorFreqMax       = null!;

    // Fibrillation
    private static ConfigEntry<bool>  _enableFibrillation  = null!;
    private static ConfigEntry<float> _ampFibrillation     = null!;
    private static ConfigEntry<float> _fibFreqMin          = null!;
    private static ConfigEntry<float> _fibFreqMax          = null!;

    // Impact
    private static ConfigEntry<bool>  _enableImpact              = null!;
    private static ConfigEntry<float> _ampImpactBoneBreak        = null!;
    private static ConfigEntry<float> _ampImpactDismember        = null!;
    private static ConfigEntry<float> _impactDecay               = null!;
    private static ConfigEntry<float> _impactFreq                = null!;
    private static ConfigEntry<float> _impactShockDeltaThreshold = null!;
    private static ConfigEntry<float> _impactPainDeltaThreshold  = null!;

    // Trauma
    private static ConfigEntry<bool>  _enableTrauma        = null!;
    private static ConfigEntry<float> _ampTrauma           = null!;
    private static ConfigEntry<float> _traumaFreq          = null!;
    private static ConfigEntry<float> _traumaFloor         = null!;

    // ── Initialize ───────────────────────────────────────────────────────────
    public static void Initialize(ConfigFile cfg)
    {
        _enableVibration    = cfg.Bind("Global", "EnableVibration",    true,  "Master on/off");
        _globalScale        = cfg.Bind("Global", "GlobalScale",        1.0f,  "Global waveform output scale 0.0–1.0 (pre-hardware)");
        _maxDeviceIntensity = cfg.Bind("Global", "MaxDeviceIntensity", 100f,  "Hardware intensity ceiling sent to the device (0–100%)");
        _pollHz             = cfg.Bind("Global", "PollHz",             20f,   "Device command rate 1–60 Hz");
        _ffThreshold        = cfg.Bind("Global", "FastForwardThreshold", 1.5f,"Time.timeScale above which fast-forward behaviour activates");
        _silenceOnFF        = cfg.Bind("Global", "SilenceOnFastForward", false,"Silence vibrations entirely during fast-forward when true");

        _enableHeartbeat    = cfg.Bind("Heartbeat", "Enable",          true,  "Heartbeat component on/off");
        _ampBrady           = cfg.Bind("Heartbeat", "AmpBrady",        0.40f, "Bradycardia peak amplitude 0–1");
        _ampTachy           = cfg.Bind("Heartbeat", "AmpTachy",        0.60f, "Tachycardia peak amplitude 0–1");
        _ampArrest          = cfg.Bind("Heartbeat", "AmpArrest",       0.30f, "Cardiac arrest amplitude 0–1");
        _hrFeelLow          = cfg.Bind("Heartbeat", "HrFeelLow",       60f,   "HR below this triggers bradycardia vibration (BPM)");
        _hrFeelHigh         = cfg.Bind("Heartbeat", "HrFeelHigh",      110f,  "HR above this triggers tachycardia vibration (BPM)");
        _hbMu1              = cfg.Bind("Heartbeat", "Mu1",             0.10f, "Main peak position within [0,1] beat phase");
        _hbMu2              = cfg.Bind("Heartbeat", "Mu2",             0.26f, "Secondary peak position within [0,1] beat phase");
        _hbSigma            = cfg.Bind("Heartbeat", "Sigma",           0.03f, "Peak width (Gaussian sigma)");
        _hbPeak2Ratio       = cfg.Bind("Heartbeat", "Peak2Ratio",      0.60f, "Relative amplitude of second heartbeat peak 0–1");

        _enableBleed        = cfg.Bind("Bleed", "Enable",              true,  "Bleed component on/off");
        _ampInternalBleed   = cfg.Bind("Bleed", "AmpInternalBleed",    0.20f, "Flat amplitude added per unit of internal bleeding");
        _bleedOnset         = cfg.Bind("Bleed", "BleedOnset",          0.06f, "Bleed speed where vibration starts");
        _bleedCeiling       = cfg.Bind("Bleed", "BleedCeiling",        1.5f,  "Bleed speed where amplitude reaches max");
        _bleedMaxAmp        = cfg.Bind("Bleed", "BleedMaxAmp",         0.65f, "Max bleed amplitude 0–1");
        _bleedFreqMin       = cfg.Bind("Bleed", "FreqMin",             0.15f, "Pulse frequency at BleedOnset (Hz)");
        _bleedFreqMax       = cfg.Bind("Bleed", "FreqMax",             0.50f, "Pulse frequency at BleedCeiling (Hz)");

        _enablePainShock    = cfg.Bind("PainShock", "Enable",          true,  "Pain/shock component on/off");
        _ampPainShock       = cfg.Bind("PainShock", "AmpPainShock",    0.50f, "Pain/shock peak amplitude 0–1");
        _painOnset          = cfg.Bind("PainShock", "PainOnset",       25f,   "Raw pain value where vibration begins (0–80)");
        _painBuzzFreqMin    = cfg.Bind("PainShock", "BuzzFreqMin",     5f,    "Buzz frequency at moderate pain (Hz)");
        _painBuzzFreqMax    = cfg.Bind("PainShock", "BuzzFreqMax",     12f,   "Buzz frequency at agony (Hz)");

        _enableHorror       = cfg.Bind("Horror", "Enable",             true,  "Horror component on/off");
        _ampHorror          = cfg.Bind("Horror", "AmpHorror",          0.45f, "Horror peak amplitude 0–1");
        _horrorFreqMin      = cfg.Bind("Horror", "FreqMin",            1f,    "Horror carrier min frequency (Hz)");
        _horrorFreqMax      = cfg.Bind("Horror", "FreqMax",            8f,    "Horror carrier max frequency (Hz)");

        _enableFibrillation = cfg.Bind("Fibrillation", "Enable",       true,  "Fibrillation component on/off");
        _ampFibrillation    = cfg.Bind("Fibrillation", "AmpFibrillation", 0.40f, "Fibrillation peak amplitude 0–1");
        _fibFreqMin         = cfg.Bind("Fibrillation", "FreqMin",      8f,    "Fibrillation carrier min frequency (Hz)");
        _fibFreqMax         = cfg.Bind("Fibrillation", "FreqMax",      15f,   "Fibrillation carrier max frequency (Hz)");

        _enableImpact              = cfg.Bind("Impact", "Enable",             true,  "Impact transients on/off");
        _ampImpactBoneBreak        = cfg.Bind("Impact", "AmpBoneBreak",       0.40f, "Impact amplitude for bone break");
        _ampImpactDismember        = cfg.Bind("Impact", "AmpDismember",       0.60f, "Impact amplitude for dismemberment");
        _impactDecay               = cfg.Bind("Impact", "Decay",              8f,    "Exponential decay coefficient (higher = shorter transient)");
        _impactFreq                = cfg.Bind("Impact", "Freq",               8f,    "Impact sine frequency (Hz)");
        _impactShockDeltaThreshold = cfg.Bind("Impact", "ShockDeltaThreshold", 5f,  "Shock delta that triggers an impact event");
        _impactPainDeltaThreshold  = cfg.Bind("Impact", "PainDeltaThreshold",  8f,  "Pain delta that triggers an impact event");

        _enableTrauma       = cfg.Bind("Trauma", "Enable",             true,  "Trauma component on/off");
        _ampTrauma          = cfg.Bind("Trauma", "AmpTrauma",          0.20f, "Trauma peak amplitude 0–1");
        _traumaFreq         = cfg.Bind("Trauma", "Freq",               0.25f, "Trauma pulse frequency (Hz)");
        _traumaFloor        = cfg.Bind("Trauma", "Floor",              40f,   "Trauma amount below which there is no vibration");
    }

    // ── Properties ───────────────────────────────────────────────────────────
    // Setters write through to ConfigEntry, which auto-saves the .cfg file.

    // Global
    public static bool  EnableVibration      { get => _enableVibration.Value;    set => _enableVibration.Value    = value; }
    public static float GlobalScale          { get => _globalScale.Value;         set => _globalScale.Value         = value; }
    public static float MaxDeviceIntensity   { get => _maxDeviceIntensity.Value;  set => _maxDeviceIntensity.Value  = value; }
    public static float PollHz               { get => _pollHz.Value;              set => _pollHz.Value              = value; }
    public static float FastForwardThreshold { get => _ffThreshold.Value;         set => _ffThreshold.Value         = value; }
    public static bool  SilenceOnFastForward { get => _silenceOnFF.Value;         set => _silenceOnFF.Value         = value; }

    // Heartbeat
    public static bool  EnableHeartbeat     { get => _enableHeartbeat.Value;  set => _enableHeartbeat.Value  = value; }
    public static float AmpHeartbeatBrady   { get => _ampBrady.Value;         set => _ampBrady.Value         = value; }
    public static float AmpHeartbeatTachy   { get => _ampTachy.Value;         set => _ampTachy.Value         = value; }
    public static float AmpHeartbeatArrest  { get => _ampArrest.Value;        set => _ampArrest.Value        = value; }
    public static float HrFeelLow           { get => _hrFeelLow.Value;        set => _hrFeelLow.Value        = value; }
    public static float HrFeelHigh          { get => _hrFeelHigh.Value;       set => _hrFeelHigh.Value       = value; }
    public static float HeartbeatMu1        { get => _hbMu1.Value;            set => _hbMu1.Value            = value; }
    public static float HeartbeatMu2        { get => _hbMu2.Value;            set => _hbMu2.Value            = value; }
    public static float HeartbeatSigma      { get => _hbSigma.Value;          set => _hbSigma.Value          = value; }
    public static float HeartbeatPeak2Ratio { get => _hbPeak2Ratio.Value;     set => _hbPeak2Ratio.Value     = value; }

    // Bleed
    public static bool  EnableBleed      { get => _enableBleed.Value;       set => _enableBleed.Value       = value; }
    public static float AmpInternalBleed { get => _ampInternalBleed.Value;  set => _ampInternalBleed.Value  = value; }
    public static float BleedOnset       { get => _bleedOnset.Value;        set => _bleedOnset.Value        = value; }
    public static float BleedCeiling     { get => _bleedCeiling.Value;      set => _bleedCeiling.Value      = value; }
    public static float BleedMaxAmp      { get => _bleedMaxAmp.Value;       set => _bleedMaxAmp.Value       = value; }
    public static float BleedFreqMin     { get => _bleedFreqMin.Value;      set => _bleedFreqMin.Value      = value; }
    public static float BleedFreqMax     { get => _bleedFreqMax.Value;      set => _bleedFreqMax.Value      = value; }

    // Pain / Shock
    public static bool  EnablePainShock  { get => _enablePainShock.Value;   set => _enablePainShock.Value   = value; }
    public static float AmpPainShock     { get => _ampPainShock.Value;      set => _ampPainShock.Value      = value; }
    public static float PainOnset        { get => _painOnset.Value;         set => _painOnset.Value         = value; }
    public static float PainBuzzFreqMin  { get => _painBuzzFreqMin.Value;   set => _painBuzzFreqMin.Value   = value; }
    public static float PainBuzzFreqMax  { get => _painBuzzFreqMax.Value;   set => _painBuzzFreqMax.Value   = value; }

    // Horror
    public static bool  EnableHorror    { get => _enableHorror.Value;    set => _enableHorror.Value    = value; }
    public static float AmpHorror       { get => _ampHorror.Value;       set => _ampHorror.Value       = value; }
    public static float HorrorFreqMin   { get => _horrorFreqMin.Value;   set => _horrorFreqMin.Value   = value; }
    public static float HorrorFreqMax   { get => _horrorFreqMax.Value;   set => _horrorFreqMax.Value   = value; }

    // Fibrillation
    public static bool  EnableFibrillation { get => _enableFibrillation.Value; set => _enableFibrillation.Value = value; }
    public static float AmpFibrillation    { get => _ampFibrillation.Value;    set => _ampFibrillation.Value    = value; }
    public static float FibFreqMin         { get => _fibFreqMin.Value;         set => _fibFreqMin.Value         = value; }
    public static float FibFreqMax         { get => _fibFreqMax.Value;         set => _fibFreqMax.Value         = value; }

    // Impact
    public static bool  EnableImpact              { get => _enableImpact.Value;              set => _enableImpact.Value              = value; }
    public static float AmpImpactBoneBreak        { get => _ampImpactBoneBreak.Value;        set => _ampImpactBoneBreak.Value        = value; }
    public static float AmpImpactDismember        { get => _ampImpactDismember.Value;        set => _ampImpactDismember.Value        = value; }
    public static float ImpactDecay               { get => _impactDecay.Value;               set => _impactDecay.Value               = value; }
    public static float ImpactFreq                { get => _impactFreq.Value;                set => _impactFreq.Value                = value; }
    public static float ImpactShockDeltaThreshold { get => _impactShockDeltaThreshold.Value; set => _impactShockDeltaThreshold.Value = value; }
    public static float ImpactPainDeltaThreshold  { get => _impactPainDeltaThreshold.Value;  set => _impactPainDeltaThreshold.Value  = value; }

    // Trauma
    public static bool  EnableTrauma  { get => _enableTrauma.Value;  set => _enableTrauma.Value  = value; }
    public static float AmpTrauma     { get => _ampTrauma.Value;     set => _ampTrauma.Value     = value; }
    public static float TraumaFreq    { get => _traumaFreq.Value;    set => _traumaFreq.Value    = value; }
    public static float TraumaFloor   { get => _traumaFloor.Value;   set => _traumaFloor.Value   = value; }
}
