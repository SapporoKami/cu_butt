# cu_butt — Buttplug.io mod for Casualties Unknown

Mod that makes sex toys vibrate on game events using the buttplug.io protocol. Vibration output is synthesized from seven concurrent wave components driven by live player-state reads (heart rate, pain, shock, bleeding, trauma, horror, fibrillation).

## Game & Framework

- **Game:** Casualties Unknown Demo (Scav Prototype)
- **Game path:** `/home/yair/Games/SteamLibrary/steamapps/common/Casualties Unknown Demo`
- **Plugins folder:** `<GamePath>/BepInEx/plugins`
- **Modding framework:** BepInEx 5.x + Harmony + ScavLib API

## Source files

| File | Purpose |
|------|---------|
| `ScavButt/Plugin.cs` | BepInEx entry point; lifecycle, event bus wiring |
| `ScavButt/ButtplugManager.cs` | Intiface Central connection loop; `Vibrate()` fan-out |
| `ScavButt/VibrationSystem.cs` | Wave synthesis engine; 20 Hz poll coroutine |
| `ScavButt/ButtCommand.cs` | In-game `butt` command with subcommands |

Plugin GUID: `com.scavbutt.scavbutt` / Name: `ScavButt` / Version: `0.1.0`

## Key paths for .csproj HintPaths

| DLL | Path |
|-----|------|
| `BepInEx.dll` | `<GamePath>/BepInEx/core/BepInEx.dll` |
| `0Harmony.dll` | `<GamePath>/BepInEx/core/0Harmony.dll` |
| `ScavLib API` (assembly name) | `<GamePath>/BepInEx/plugins/ScavLib API.dll` |
| `Assembly-CSharp.dll` | `<GamePath>/<GameData>/Managed/Assembly-CSharp.dll` |
| Unity DLLs | `<GamePath>/<GameData>/Managed/UnityEngine*.dll` |

The ScavLib file on disk is `ScavLib API.dll` (filename has a space) and the assembly name inside is `"ScavLib API"`.
Download from: https://github.com/Kanisuko/ScavLib-API-DLL-Repository/releases

Note: `ScavSetLib.dll` (GUID `com.kanisuko.scavsetlib`) is a different unrelated mod already in plugins — do not confuse with ScavLib API.

## Project settings (from ScavLib as reference)

- Target: `.NET Framework 4.8` (`<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>`)
- LangVersion: `latest`
- All game/BepInEx DLL references: `<Private>False</Private>` (don't copy to output)
- Buttplug NuGet packages: copy to output (they must ship with the mod)

## Plugin lifecycle

```
Awake()
  ├── register 'butt' command via CommandRegistry
  ├── ButtplugManager.Initialize()   ← registers event handlers; does NOT auto-connect
  └── EventBus.Register(this)

WorldLoadedEvent
  └── AddComponent<VibrationSystem>().StartPolling()   ← coroutine starts here

WorldUnloadingEvent
  ├── ButtplugManager.StopAll()
  └── Destroy(_vibrationSystem)

OnDestroy
  ├── EventBus.Unregister(this)
  └── ButtplugManager.StopConnectionLoop()
```

**Critical:** never call PlayerUtil or any world method in `Awake()` — always wait for `WorldLoadedEvent`.

## ButtplugManager

Targets Intiface Central at `ws://127.0.0.1:12345`. Connection is manual — run `butt connect` to start. Once connected, runs an async reconnect loop (2 s retry) and auto-starts device scanning.

- `ButtplugManager.Vibrate(double intensity, int durationMs = 0)` — fans out to all connected devices that have vibrate attributes. `durationMs = 0` leaves the motor running (used by the 20 Hz poll). `durationMs > 0` holds then stops (used by `butt test`).
- `ButtplugManager.StopAll()` — stops all motors immediately.
- `ButtplugManager.Connect()` / `Disconnect()` — exposed for the `butt` command.

## Wave synthesis vibration system

`VibrationSystem` is a `MonoBehaviour` added to the plugin's `gameObject` on world load. It runs a coroutine at **20 Hz** (`DT = 0.05 s`) that calls `ComputeOutput()` and passes the result to `ButtplugManager.Vibrate()`.

### ComputeOutput — seven additive components

```
output = heartbeat + bleed + painShock + horror + fibrillation + impact + trauma
```

Each component returns a value in `[0, 1]`. They are mixed, modulated, soft-normalized, and clamped to `[0, 1]` before sending to the device.

#### 1. HeartbeatComponent
BUMP-bump cardiac waveform using two wrap-aware Gaussian peaks within the game's own `[0, 1]` beat phase.

- **Synced to game audio/EEG**: reads `body.heartProg` (public field on `Body`) directly — no self-timed phase. The game drives the same variable that triggers the heartbeat sound and EEG display.
- Two peaks: `mu1 = 0.10`, `mu2 = 0.20`, `sigma = 0.03`. The second peak is 60% amplitude.
- Fibrillation progress (`0–100`) jitters the peak positions by up to ±0.12 in `[0, 1]`, updated every `50–150 ms`.
- Amplitude is **zero in the normal HR range** (`HR_FEEL_LOW = 70` … `HR_FEEL_HIGH = 95`):
  - Below 70 → scales up to `AMP_HEARTBEAT_BRADY = 0.40` at `HEART_RATE_BRADYCARDIA_SEVERE`
  - Above 95 → scales up to `AMP_HEARTBEAT_TACHY = 0.60` at `HEART_RATE_TACHYCARDIA_CRITICAL`
  - Cardiac arrest → fixed `AMP_HEARTBEAT_ARREST = 0.30`
- **Exempt from all modulation** (consciousness, drugs, painkillers).

#### 2. BleedComponent
Slow sine wave expressing bleeding urgency.

- Frequency: `0.4–1.2 Hz` interpolated by bleed speed between `BLEED_SPEED_MEDIUM` and `BLEED_SPEED_CRITICAL`.
- Internal bleeding adds a flat `AMP_INTERNAL_BLEED = 0.30` amplitude contribution.
- Modulated by consciousness.

#### 3. PainShockComponent
Constant buzz that never drops to zero when active (intentionally uncomfortable).

- Amplitude: `max(painNorm, shockNorm) * AMP_PAIN_SHOCK (0.50)`.
- Carrier: `amp * (0.85 + 0.15 * sin(buzzFreq * t))` — a 15% tremor on top of a DC floor.
- Buzz frequency scales from `5 Hz` (moderate pain) to `12 Hz` (agony).
- Modulated by consciousness, opiates, and painkillers.

#### 4. HorrorComponent
Erratic vibration for psychological dread.

- Amplitude: `horrorLevel / 200 * AMP_HORROR (0.45)`.
- Carrier frequency random-walks between `1–8 Hz`, updated every `100–500 ms`, interpolated each tick.
- At high horror levels, blends the sine wave with smoothed random noise (noise contribution = `horrorNorm`).
- Modulated by consciousness and opiates (at 50% opiate strength).

#### 5. FibrillationComponent
High-frequency chaos for ventricular fibrillation.

- Amplitude: `fibrillationProgress / 100 * AMP_FIBRILLATION (0.40)`.
- Sine wave at a random frequency `8–15 Hz`, re-randomized every `80–200 ms`.
- **Exempt from all modulation** (same as heartbeat).

#### 6. ImpactComponent
Short transient for sudden trauma events.

- Triggered by `DetectSpikes()` which runs each tick before the components.
- Triggers when: shock delta > 5, pain delta > 8, new bone break (+0.40 amplitude), or new dismemberment (+0.60 amplitude).
- Wave: 8 Hz sine, decaying with `exp(-8 * timeSinceTrigger)` — ~500 ms effective duration.
- Amplitude is clamped from the delta magnitude (shock/pain delta ÷ 50).
- Only modulated by opiates (not consciousness or painkillers) — pain impacts register even when unconscious.

#### 7. TraumaComponent
Slow background throb for cumulative tissue damage.

- Amplitude: `InvLerp(TRAUMA_FLOOR=40, 100, traumaAmount) * AMP_TRAUMA (0.20)`.
- 0.25 Hz sine wave — very slow pulse.
- Modulated by consciousness, opiates, and painkillers.

### Modulation stack

```
bleed        *= cMult                                   // consciousness
painShock    *= cMult * pillsMult * opiateMult
horror       *= cMult * Lerp(1.0, 0.5, opiate)
trauma       *= cMult * pillsMult * opiateMult
impact       *= opiateMult                              // opiates only; consciousness-independent
// heartbeat and fibrillation: no modulation
```

- `cMult` — `SmoothStep(0, 1, (consciousness - CONSCIOUSNESS_UNCONSCIOUS) / 40)`, fades to 0 as consciousness drops.
- `opiateMult` — `Lerp(1.0, 0.15, opiateHappiness / 80)`, approaches 0.15 at full opiate dose.
- `pillsMult` — `0.4` when `PlayerUtil.HasPainkillers()`, else `1.0`.

### Soft normalization

If the raw sum exceeds 1.0, all non-impact components are uniformly scaled down so impact always gets its full amplitude:

```csharp
scale = Clamp01((1.0 - impact) / nonImpact)
// applied to heartbeat, bleed, painShock, horror, fibrillation, trauma
```

### Amplitude constants (tuning reference)

| Constant | Value | Component |
|----------|-------|-----------|
| `AMP_HEARTBEAT_BRADY` | 0.40 | Bradycardia |
| `AMP_HEARTBEAT_TACHY` | 0.60 | Tachycardia |
| `AMP_HEARTBEAT_ARREST` | 0.30 | Cardiac arrest |
| `AMP_INTERNAL_BLEED` | 0.30 | Internal bleeding |
| `AMP_PAIN_SHOCK` | 0.50 | Pain / shock buzz |
| `AMP_HORROR` | 0.45 | Horror |
| `AMP_FIBRILLATION` | 0.40 | Fibrillation |
| `AMP_TRAUMA` | 0.20 | Trauma |

## `butt` command subcommands

| Command | Action |
|---------|--------|
| `butt status` | Show connection state and device count |
| `butt connect` | (Re)connect to Intiface Central |
| `butt disconnect` | Disconnect from Intiface Central |
| `butt scan` | Start device scan |
| `butt stopscan` | Stop device scan |
| `butt devices` | List connected devices and their capabilities |
| `butt stop` | Stop all devices immediately |
| `butt test` | Brief test vibration (0.01 intensity, 300 ms) |

## ScavLib dependency declaration

```csharp
[BepInDependency("com.kanisuko.scavlib", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("com.scavbutt.scavbutt", "ScavButt", "0.1.0")]
public class Plugin : BaseUnityPlugin { }
```

## ScavLib namespaces

- `ScavLib.util` — PlayerUtil, LimbUtil, SkillUtil, ItemUtil
- `ScavLib.event_bus` — EventBus, Subscribe attribute, world/item events
- `ScavLib.mods` — IModLifecycle, ModLifecycleBase, ModRegistry
- `ScavLib.config` — ConfigManager
- `ScavLib.command` — CommandRegistry

## PlayerUtil — damage-relevant API

All methods return safe defaults when no world is loaded (never throw NullReferenceException).

### State queries
- `PlayerUtil.IsAlive()` — bool
- `PlayerUtil.IsConscious()` — bool
- `PlayerUtil.IsDying()` — bool
- `PlayerUtil.IsInCardiacArrest()` — bool
- `PlayerUtil.IsFibrillationForced()` — bool
- `PlayerUtil.HasPainkillers()` — bool

### Pain / damage reads
- `PlayerUtil.GetAveragePain()` — float
- `PlayerUtil.GetShock()` — float
- `PlayerUtil.GetPainShock()` — float
- `PlayerUtil.GetTraumaAmount()` — float
- `PlayerUtil.GetTotalBleedSpeed()` — float
- `PlayerUtil.GetInternalBleeding()` — float (0–100)
- `PlayerUtil.GetConsciousness()` — float
- `PlayerUtil.GetHeartRate()` — float (BPM)
- `PlayerUtil.GetFibrillationProgress()` — float (0–100)
- `PlayerUtil.GetHorrifiedLevel()` — float (0–200)
- `PlayerUtil.GetOpiateHappiness()` — float (0–~80)

### LimbUtil
- `LimbUtil.HasBrokenBone()` — bool (any limb)
- `LimbUtil.HasDismemberment()` — bool (any limb)

### Threshold constants
`PlayerUtil.Thresholds` has named constants for every moodle threshold including 4 pain levels, blood pressure levels, heart rate levels, etc. Use these instead of magic numbers.

## Wiki references

- Getting Started: https://github.com/Kanisuko/ScavLib-API-DLL-Repository/wiki/Getting-Started
- Wiki home: https://github.com/Kanisuko/ScavLib-API-DLL-Repository/wiki
- PlayerUtil: https://github.com/Kanisuko/ScavLib-API-DLL-Repository/wiki/PlayerUtil
- CommandRegistry: https://github.com/Kanisuko/ScavLib-API-DLL-Repository/wiki/CommandRegistry

## CommandRegistry pattern

Commands inherit `BaseCommand` from `ScavLib.command`, require `Name`, `Description`, and `Execute(string[] args)`.
Register in `Awake()`: `CommandRegistry.TryRegister(new MyCommand(), PluginName, out var error)`.
Output to console via `GameUtil.Log("message")`.
`args[0]` is the command name itself; real args start at `args[1]`.
Use prefixed names to avoid conflicts (our command is `butt`).

## Reference projects (in /references/)

- `CultOfTheButtplug/` — example buttplug mod for a different Unity game (Cult of the Lamb). Shows plugin structure, ButtplugClient connection loop, and Harmony damage patch pattern.
- `ScavLib-API/` — source of ScavLib itself. Use for understanding available APIs and as the canonical .csproj setup reference.
