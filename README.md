# ScavButtIO

<img width="463" height="457" alt="Screenshot From 2026-06-27 01-49-54" src="https://github.com/user-attachments/assets/deabfce5-d912-4a37-9b6b-80df6c3f09d8" />

BepInEx mod for [Casualties Unknown Demo](https://store.steampowered.com/app/2560010/Casualties_Unknown_Demo/) that makes compatible devices vibrate based on your in-game health state using the [buttplug.io](https://buttplug.io) protocol.

Vibration output is synthesized from seven concurrent wave components driven by live player-state reads: heart rate, bleeding, pain, shock, horror, fibrillation, and cumulative trauma.

---

## What you need

### 1. Casualties Unknown Demo
Available free on Steam.

### 2. BepInEx 5.x
The modding framework the game runs on. If you got the game through Steam it may already be installed — check for a `BepInEx/` folder in your game directory. If not, follow the [BepInEx installation guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html).

### 3. ScavLib API
A modding helper library for Casualties Unknown.

1. Download `ScavLib API.dll` from the [ScavLib releases page](https://github.com/Kanisuko/ScavLib-API-DLL-Repository/releases).
2. Place it in `<GameDir>/BepInEx/plugins/`.

### 4. Intiface Central
The server that talks to your hardware.

1. Download and install [Intiface Central](https://intiface.com/central/).
2. Launch it before starting the game.
3. Connect your device in Intiface Central (it will be discovered automatically once the game tells it to scan).

### 5. A buttplug.io-compatible device
Any device supported by [Buttplug.io](https://iostindex.com/). Vibrating toys, e-stim units, etc.

---

## Installation (pre-built release)

1. Download `ScavButt.dll` from the [latest release](../../releases/latest).
2. Create the folder `<GameDir>/BepInEx/plugins/ScavButt/`.
3. Place `ScavButt.dll` inside it.
4. Launch Intiface Central and make sure your device is connected there.
5. Start the game. Open the in-game console (default: `` ` ``) and run:
   ```
   butt connect
   butt scan
   ```
6. Vibration starts automatically as soon as a world loads.

---

## Usage

Run `butt --help` in the in-game console to see all commands:

```bash
butt <subcommand> [args...]
Subcommands:
  connect     - (Re)connect to Intiface Central
  disconnect  - Disconnect from Intiface Central
  scan        - Start scanning for devices
  stopscan    - Stop scanning for devices
  devices     - List connected devices and their capabilities
  stop        - Stop all devices immediately
  test        - Trigger a test pulse through the wave system (visible in viz, vibrates if connected)
  status      - Show connection state and device count
  viz         - Toggle the wave visualizer overlay  |  butt viz poll — toggle poll-window markers on the graph
  set         - Set a vibration setting  (butt set <key> <value>)
  settings    - List all vibration settings and their current values
```

### Wave visualizer

`butt viz` opens a real-time oscilloscope overlay that shows all seven wave components and the final mixed output. Useful for demos and for tuning settings without needing a device connected.

```bash
butt viz           # toggle the oscilloscope overlay
butt viz poll      # overlay + green markers for each device flush
butt test          # fire a test pulse visible in the viz
```

---

## Configuration

Run `butt set --help` to see all tunable parameters:

```bash
Usage: butt set <key> <value>
 [global]
   enable                  Master on/off (true/false)
   intensity               Hardware intensity cap 0–100 (%); bypasses Lovense app limits
   scale                   Global waveform output scale 0.0–1.0 (pre-hardware)
   poll-hz                 Device command rate 1–60 Hz (real-time flush window; game-time sampling stays at 20 Hz)
   ff-threshold            Time.timeScale above which fast-forward behaviour activates (default 1.5)
   silence-ff              Silence vibrations entirely during fast-forward (true/false); false = dampened output instead
 [effects]
   heartbeat               Heartbeat component on/off
   bleed                   Bleed component on/off
   pain                    Pain/shock component on/off
   horror                  Horror component on/off
   fibril                  Fibrillation component on/off
   impact                  Impact transients on/off
   trauma                  Trauma component on/off
 [heartbeat]
   hr-low                  HR below this triggers bradycardia vibration (BPM)
   hr-high                 HR above this triggers tachycardia vibration (BPM)
   amp-brady               Bradycardia peak amplitude 0–1
   amp-tachy               Tachycardia peak amplitude 0–1
   amp-arrest              Cardiac arrest amplitude 0–1
 [bleed]
   bleed-onset             Bleed speed where vibration starts
   bleed-ceil              Bleed speed where amplitude reaches max
   bleed-max-amp           Max bleed amplitude 0–1 (cap so bleed never saturates alone)
 [amplitudes]
   pain-onset              Raw pain value where vibration starts (0–80; PAIN_MILD=10, PAIN_MODERATE=30)
   amp-pain                Pain/shock peak amplitude 0–1
   amp-horror              Horror peak amplitude 0–1
   amp-fibril              Fibrillation peak amplitude 0–1
   amp-trauma              Trauma peak amplitude 0–1
```

Set a value: `butt set intensity 75` — query the current value: `butt set intensity`. All settings persist across sessions via BepInEx's config file (`BepInEx/config/com.scavbutt.scavbutt.cfg`).

Run `butt settings` to see every setting and its current value at once.

---

## Building from source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version — the project targets .NET Framework 4.8 but the SDK builds it fine)
- Casualties Unknown Demo installed via Steam
- ScavLib API.dll in `<GameDir>/BepInEx/plugins/`

### Steps

1. Clone the repo:
   ```bash
   git clone https://github.com/SapporoKami/cu_butt.git
   cd cu_butt
   ```

2. Copy the game path template:
   ```bash
   cp ScavButt/GamePath.props.example ScavButt/GamePath.props
   ```

3. Edit `ScavButt/GamePath.props` and set `<GamePath>` to your actual install directory:
   ```xml
   <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown Demo</GamePath>
   ```
   On Linux:
   ```xml
   <GamePath>/home/yourname/.steam/steam/steamapps/common/Casualties Unknown Demo</GamePath>
   ```

4. Build:
   ```bash
   dotnet build ScavButt/ScavButt.csproj -c Release
   ```

5. The output is at `ScavButt/bin/Release/ScavButt/ScavButt.dll`. Copy it to `<GameDir>/BepInEx/plugins/ScavButt/ScavButt.dll`.

You can also pass the path directly without creating `GamePath.props`:
```bash
dotnet build ScavButt/ScavButt.csproj -c Release -p:GamePath="C:\...\Casualties Unknown Demo"
```

`GamePath.props` is gitignored and never committed — it stays local to your machine.

---

## How it works

Once connected, the mod polls player state at 20 Hz and synthesizes a vibration intensity from seven additive components:

| Component | Driven by |
|-----------|-----------|
| Heartbeat | Heart rate & beat phase — intensifies during bradycardia, tachycardia, or cardiac arrest |
| Bleeding | Bleed speed & internal bleeding — slow sine wave that speeds up as blood loss worsens |
| Pain/Shock | Pain and shock level — constant buzz that scales from 5 Hz to 12 Hz with severity |
| Horror | Horror level — erratic frequency that random-walks between 1–8 Hz |
| Fibrillation | Ventricular fibrillation progress — high-frequency chaos (8–15 Hz) |
| Impact | Spike detection — short transient on sudden trauma events (bone breaks, dismemberment) |
| Trauma | Cumulative tissue damage — very slow 0.25 Hz background throb |

Components are mixed, soft-normalized so the total never exceeds 1.0, and modulated by consciousness, opiates, and painkillers.

---

## License

MIT — see [LICENSE](LICENSE).
