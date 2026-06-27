# ScavButt — Vibration System Design

Handoff document for implementing the additive-synthesis haptic feedback system.
Read `CLAUDE.md` first for project setup, build paths, and BepInEx/ScavLib conventions.

---

## Current state of the codebase

- `Plugin.cs` — BepInEx entry point. Registers the `butt` command, starts
  `WorldLoadedEvent` listener, runs a simple coroutine that polls
  `PlayerUtil.GetAveragePain()` every 100 ms and maps it directly to vibration
  intensity. This polling coroutine is what we will replace with the new system.
- `ButtplugManager.cs` — Manages Intiface Central connection, device list, and
  exposes `Vibrate(double intensity, int durationMs)` and `StopAll()`.
  The new system will call `Vibrate` every tick instead of with a duration.
- `ButtCommand.cs` — In-game console commands (connect, scan, devices, test, etc.).
  No changes needed here.

---

## ScavLib API surface relevant to vibration

There are **no push events for damage, pain, cardiac, or bleeding**. The EventBus
only has world/inventory lifecycle events. Everything must be **polled**.

### Getters we will use

```csharp
// Pain & shock
PlayerUtil.GetAveragePain()        // 0–100, highest limb pain after resilience
PlayerUtil.GetShock()              // 0–100, 100 = forced ragdoll
PlayerUtil.GetPainShock()          // 0–1, ≥0.66 = unconscious

// Cardiac
PlayerUtil.GetHeartRate()          // float, normal ~70 BPM
PlayerUtil.GetFibrillationProgress() // 0–100
PlayerUtil.IsFibrillationRising()  // bool
PlayerUtil.IsFibrillationForced()  // bool
PlayerUtil.IsInCardiacArrest()     // bool (HR < 20)

// Bleeding
PlayerUtil.GetTotalBleedSpeed()    // float per-second sum
PlayerUtil.GetInternalBleeding()   // 0–100, critical at 50
PlayerUtil.GetHemothorax()         // 0–100, thresholds at 40 and 70

// Neurology / horror
PlayerUtil.GetHorrifiedLevel()     // 0–200, scales with enemy size/count
PlayerUtil.GetBrainHealth()        // 0–100, at 0 = death
PlayerUtil.IsBrainDying()          // bool

// Consciousness & drugs
PlayerUtil.GetConsciousness()      // 0–100, <30 = incapacitated, <20 = unconscious
PlayerUtil.HasPainkillers()        // bool
PlayerUtil.GetOpiateHappiness()    // float, high = strong opiates active
PlayerUtil.GetTraumaAmount()       // 0–100, sustained high pain accumulation

// Limbs (for bone-break / dismemberment spike detection)
LimbUtil.HasBrokenBone()           // bool
LimbUtil.HasDismemberment()        // bool
```

### Threshold constants to use (PlayerUtil.Thresholds)

```
Heart rate:   CARDIAC_ARREST=20, BRADYCARDIA_SEVERE=40, BRADYCARDIA_MILD=60,
              TACHYCARDIA_MILD=110, TACHYCARDIA_SEVERE=160, TACHYCARDIA_CRITICAL=200
Bleed speed:  MEDIUM=0.06, HEAVY=0.15, CRITICAL=0.30
Pain:         MILD=10, MODERATE=30, SEVERE=55, AGONY=80
Brain damage: SLIGHT=95, MILD=80, MODERATE=60, SEVERE=30
Consciousness:UNCONSCIOUS=20, INCAPACITATED=30
```

---

## Corrected understanding of game mechanics

These were clarified by the user — do not assume from the API names alone:

- **`IsMindWiped()`** — a drug effect (like a catatonic state from a specific
  substance). Not a damage event. Ignore for vibration purposes.
- **`GetHorrifiedLevel()` (0–200)** — scales with enemy presence (large enemies,
  bosses, large numbers of enemies) and huge visible injuries. It is NOT a
  jumpscare hook. Jumpscares are a separate, undocumented game mechanic not
  exposed by ScavLib.
- **Pain resilience** — `GetAveragePain()` already accounts for resilience, so
  it reflects what the player actually feels, not raw damage.
- **Painkillers** are a dominant mechanic in this game (including fentanyl-class
  opiates). They should meaningfully reduce most vibration effects.

---

## Design philosophy

- **Vibration = punishment, not feedback.** Near-healthy states produce no or
  near-zero vibration. Effects only emerge as the player deteriorates.
- **Consciousness is the global mute.** As the player loses consciousness,
  all effects scale toward zero — except the heartbeat, which remains because
  the game shows a heart monitor screen when unconscious.
- **Drugs dull the signal.** Painkillers reduce pain/shock components; opiates
  reduce almost everything except cardiac.
- **Instant events (bone break, huge hit) spike through everything** without
  cancelling ongoing patterns.

---

## Architecture: Additive Synthesis

The output signal is built by summing independent wave components:

```
output(t) = clamp01( Σ component_i.Evaluate(t) )
```

Each component returns a value in `[0, 1]`. After summing, we apply global
modifiers and clamp to `[0, 1]` before sending to `ButtplugManager.Vibrate()`.

The polling coroutine runs every **50 ms** (20 Hz), which gives enough resolution
for the slowest waveforms (heartbeat at ~1 Hz) while not hammering the USB stack.

### Global modifiers (applied after component sum)

```
consciousnessMult = smoothstep(0, 1, (consciousness - 20) / 40)
                  // 0 when unconscious, 1 when fully conscious
                  // heartbeat component bypasses this entirely

painkillersScale  = HasPainkillers() ? 0.4f : 1.0f
opiateMult        = map(GetOpiateHappiness(), 0, max, 1.0f, 0.15f)
                  // strong opiates reduce nearly all components to ~15%
```

### Soft normalization (prevent saturation)

When the raw sum exceeds 1.0, divide all components proportionally but give
the **impact spike** a priority boost — it should always punch through:

```
if (rawSum > 1.0f)
{
    float excess = rawSum - impactComponent;
    float scale  = (1.0f - impactComponent) / excess;
    // scale all non-impact components by scale, keep impact as-is
}
```

---

## Component definitions

### 1. Heartbeat

The most important component. Always present when HR is abnormal.

```
waveform:   two quick peaks (BUMP-bump), then silence until next beat
            modeled as: A * (gauss(t, μ1, σ1) + 0.6 * gauss(t, μ2, σ2))
            where gauss is a narrow Gaussian bell, μ1 and μ2 are offsets within
            the beat cycle

frequency:  HR / 60 beats per second → beat period = 60 / HR seconds

amplitude:  0.0 when HR is in [60, 110] (normal range, no feedback)
            ramps from 0 → 0.4 as HR drops from 60 → 40 (bradycardia)
            ramps from 0 → 0.6 as HR rises from 110 → 200 (tachycardia)
            chaos mode when IsInCardiacArrest(): random pulse timing, amp ~0.3

fibrillation modifier:
            when GetFibrillationProgress() > 0, add random ±jitter to beat
            timing proportional to FibrillationProgress / 100.
            IsFibrillationForced() = maximum chaos, timing is fully random.

consciousness: EXEMPT — not multiplied by consciousnessMult
drugs:         NOT reduced by painkillers (cardiac is not pain)
```

### 2. Bleeding Throb

Slow rhythmic pulsing tied to blood loss rate.

```
waveform:   sin(2π * freq * t) mapped to [0, 1]
frequency:  lerp(0.4, 1.2, bleedNorm) Hz
            // slow throb at low bleed, faster urgency at critical

amplitude:  bleedNorm = invLerp(BLEED_MEDIUM, BLEED_CRITICAL, GetTotalBleedSpeed())
            0.0 below MEDIUM (0.06/s), full at CRITICAL (0.30/s)
            also add GetInternalBleeding() / 100 * 0.3 on top

consciousness: multiplied by consciousnessMult
drugs:         not reduced by painkillers (bleeding is not pain)
```

### 3. Pain / Shock Wave

The "you are being hurt right now" signal.

```
waveform:   sin(2π * 3.0 * t) at medium frequency

amplitude:  painNorm  = invLerp(PAIN_MODERATE, PAIN_AGONY, GetAveragePain())
            shockNorm = GetShock() / 100
            amp = max(painNorm, shockNorm) * 0.5

consciousness: multiplied by consciousnessMult
drugs:         multiplied by painkillersScale AND opiateMult
               (this is what painkillers are for)
```

### 4. Horror Wave

Erratic, non-rhythmic. Scales with enemy threat presence.

```
waveform:   lerp(sin(2π * f * t), noise(t), horrorNorm)
            // transitions from a sine to chaos as horror rises
            where f = random walk between 1–8 Hz
            and noise(t) = per-tick random value smoothed over 100 ms

amplitude:  horrorNorm = GetHorrifiedLevel() / 200   // 0–1
            amp = horrorNorm * 0.45

consciousness: multiplied by consciousnessMult
drugs:         partially reduced by opiateMult (0.5× weight)
```

### 5. Fibrillation Burst

Supplements the heartbeat component when cardiac rhythm is failing.

```
waveform:   high-frequency noise (8–15 Hz random bursts)
frequency:  random, updated every 80–200 ms randomly

amplitude:  fibNorm = GetFibrillationProgress() / 100
            amp = fibNorm * 0.4

consciousness: EXEMPT (cardiac, same as heartbeat)
drugs:         not reduced
```

### 6. Impact Spike (Transient)

One-shot decaying wave triggered by a sudden increase in shock or pain.

```
waveform:   sin(2π * 8 * t) * exp(-8 * timeSinceImpact)
            decays to near-zero in ~0.5 seconds

trigger:    each poll, compute delta:
              shockDelta = GetShock() - prevShock
              painDelta  = GetAveragePain() - prevPain
              boneBroke  = HasBrokenBone() && !prevBoneBroke
              dismember  = HasDismemberment() && !prevDismember
            if shockDelta > 5 OR painDelta > 8 OR boneBroke OR dismember:
              impactAmp = clamp01(max(shockDelta, painDelta) / 50) + (boneBroke ? 0.4 : 0)
              reset timeSinceImpact = 0

amplitude:  impactAmp * exp(-8 * timeSinceImpact)

consciousness: NOT multiplied (you feel a bone break even half-conscious)
drugs:         multiplied by opiateMult only (painkillers reduce it lightly)
priority:      gets preferential treatment in soft normalization — see above
```

### 7. Trauma Grind (Low Priority Background)

Long-term high pain leaves a persistent low hum.

```
waveform:   sin(2π * 0.25 * t)   // very slow wave, almost DC
amplitude:  traumaNorm = invLerp(40, 100, GetTraumaAmount()) * 0.2

consciousness: multiplied by consciousnessMult
drugs:         multiplied by painkillersScale * opiateMult
```

---

## File structure to create

```
ScavButt/
  VibrationSystem.cs    ← new file, the entire synth system lives here
  Plugin.cs             ← replace the old pain coroutine with VibrationSystem
  ButtplugManager.cs    ← no changes needed
  ButtCommand.cs        ← no changes needed
```

### `VibrationSystem.cs` — class outline

```csharp
using System.Collections;
using UnityEngine;
using ScavLib.util;
using ScavLib.event_bus;

namespace ScavButt
{
    public class VibrationSystem : MonoBehaviour
    {
        // ---- component state ----
        private float _time;
        private float _impactAmp;
        private float _impactTime;
        private float _prevShock;
        private float _prevPain;
        private bool  _prevBoneBroke;
        private bool  _prevDismember;

        // heartbeat phase tracker
        private float _beatPhase;       // 0–1, fraction through current beat cycle
        private float _fibJitter;       // current timing jitter amount

        // horror noise state
        private float _horrorNoise;
        private float _horrorNoiseTarget;
        private float _horrorNoiseTimer;

        public void StartPolling() => StartCoroutine(PollLoop());

        private IEnumerator PollLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.05f);  // 20 Hz

                if (!ButtplugManager.IsConnected) continue;

                _time += 0.05f;

                float output = ComputeOutput();
                ButtplugManager.Vibrate((double)output);
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

            // global modifiers
            float consciousness = PlayerUtil.GetConsciousness();
            float cMult = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((consciousness - 20f) / 40f));

            float opiate      = Mathf.Clamp01(PlayerUtil.GetOpiateHappiness() / 80f);
            float opiateMult  = Mathf.Lerp(1f, 0.15f, opiate);
            float pillsMult   = PlayerUtil.HasPainkillers() ? 0.4f : 1.0f;

            // apply modifiers per component
            bleed        *= cMult;
            painShock    *= cMult * pillsMult * opiateMult;
            horror       *= cMult * Mathf.Lerp(1f, 0.5f, opiate);
            trauma       *= cMult * pillsMult * opiateMult;
            impact       *= opiateMult;          // conscious-independent, pills-lite
            // heartbeat and fibrillation: no consciousness or drug scaling

            float rawSum = heartbeat + bleed + painShock + horror
                         + fibrillation + impact + trauma;

            // soft normalization: protect impact spike
            if (rawSum > 1.0f)
            {
                float nonImpact = rawSum - impact;
                if (nonImpact > 0f)
                {
                    float scale = Mathf.Clamp01((1.0f - impact) / nonImpact);
                    bleed *= scale; painShock *= scale; horror *= scale;
                    fibrillation *= scale; heartbeat *= scale; trauma *= scale;
                }
            }

            return Mathf.Clamp01(heartbeat + bleed + painShock + horror
                                + fibrillation + impact + trauma);
        }

        // ---- component implementations (stubs, flesh out each) ----

        private float HeartbeatComponent() { /* ... */ return 0f; }
        private float BleedComponent()     { /* ... */ return 0f; }
        private float PainShockComponent() { /* ... */ return 0f; }
        private float HorrorComponent()    { /* ... */ return 0f; }
        private float FibrillationComponent() { /* ... */ return 0f; }
        private float ImpactComponent()    { /* ... */ return 0f; }
        private float TraumaComponent()    { /* ... */ return 0f; }

        private void DetectSpikes()
        {
            float shock     = PlayerUtil.GetShock();
            float pain      = PlayerUtil.GetAveragePain();
            bool  boneBroke = LimbUtil.HasBrokenBone();
            bool  dismember = LimbUtil.HasDismemberment();

            float shockDelta = shock - _prevShock;
            float painDelta  = pain  - _prevPain;
            bool  newBreak   = boneBroke && !_prevBoneBroke;
            bool  newDismember = dismember && !_prevDismember;

            if (shockDelta > 5f || painDelta > 8f || newBreak || newDismember)
            {
                _impactAmp  = Mathf.Clamp01(Mathf.Max(shockDelta, painDelta) / 50f)
                            + (newBreak ? 0.4f : 0f)
                            + (newDismember ? 0.6f : 0f);
                _impactAmp  = Mathf.Clamp01(_impactAmp);
                _impactTime = 0f;
            }
            else
            {
                _impactTime += 0.05f;
            }

            _prevShock     = shock;
            _prevPain      = pain;
            _prevBoneBroke = boneBroke;
            _prevDismember = dismember;
        }

        private static float InvLerp(float a, float b, float v)
            => Mathf.Clamp01((v - a) / (b - a));
    }
}
```

### Changes to `Plugin.cs`

1. Remove the existing pain-polling coroutine.
2. In `WorldLoadedEvent` handler, do:
   ```csharp
   _vibrationSystem = gameObject.AddComponent<VibrationSystem>();
   _vibrationSystem.StartPolling();
   ```
3. In `WorldUnloadingEvent` (or equivalent teardown), call
   `ButtplugManager.StopAll()` and destroy the component.

---

## Implementation order

1. **Create `VibrationSystem.cs`** with the class skeleton above and all seven
   component stubs returning 0.
2. **Wire it into `Plugin.cs`** replacing the old coroutine.
3. **Implement `HeartbeatComponent()`** first — this is the signature effect and
   easiest to feel/test. Get the beat timing right using `_beatPhase` advancing
   at `(HR / 60) * dt` per tick.
4. **Implement `BleedComponent()`** and `ImpactComponent()` — these are the two
   most game-relevant effects.
5. **Implement remaining components** in order: `PainShockComponent`,
   `HorrorComponent`, `FibrillationComponent`, `TraumaComponent`.
6. **Tune thresholds and amplitudes** with a real device. All amplitude
   constants should be named consts at the top of the class so they're easy
   to tweak.

---

## Open questions / things to verify in game

- Does `GetHorrifiedLevel()` actually spike on large enemy encounters?
  Need to test in-game. If the ramp is too slow, switch to delta-detection.
- What is the actual range of `GetOpiateHappiness()`? The raw write allows
  unclamped values so the normalization divisor (80f above) is a guess.
- `GetTotalBleedSpeed()` — confirm units are per-second (not per-frame).
  The threshold constants (0.06, 0.15, 0.30) imply per-second.
- No jumpscare event is exposed by ScavLib. If the user wants to revisit this,
  it would require Harmony-patching the game's jumpscare trigger class
  directly from `Assembly-CSharp.dll`.
