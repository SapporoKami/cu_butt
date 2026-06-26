# cu_butt — Buttplug.io mod for Casualties Unknown

Mod that makes sex toys vibrate on game events (starting with player damage) using the buttplug.io protocol.

## Game & Framework

- **Game:** Casualties Unknown Demo (Scav Prototype)
- **Game path:** `/home/yair/Games/SteamLibrary/steamapps/common/Casualties Unknown Demo`
- **Plugins folder:** `<GamePath>/BepInEx/plugins`
- **Modding framework:** BepInEx 5.x + Harmony + ScavLib API

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

## ScavLib dependency declaration

```csharp
[BepInDependency("com.kanisuko.scavlib", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("com.yourname.yourmod", "YourMod", "1.0.0")]
public class YourPlugin : BaseUnityPlugin { }
```

## Critical ScavLib rule

**Never call PlayerUtil or any player/world method inside `Awake()`.**
The game world isn't initialized yet — always use `WorldLoadedEvent` as the entry point for anything that touches the player.

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

### Pain / damage reads
- `PlayerUtil.GetAveragePain()` — float, average pain across all limbs
- `PlayerUtil.GetShock()` — float
- `PlayerUtil.GetPainShock()` — float
- `PlayerUtil.GetTraumaAmount()` — float
- `PlayerUtil.GetTotalBleedSpeed()` — float

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
