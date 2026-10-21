# TTY Dynamic Client — Skype for Business

TTY/TDD software client for Windows 11 that captures Skype for Business call audio and decodes/encodes Baudot FSK tones in real time.

## Requirements

- Windows 11
- .NET 10 Desktop Runtime (or build/publish self-contained, see below)
- **[VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/)** — free virtual audio driver, **required** to send the synthesized TTY tones to Skype for Business

### Why is VB-CABLE needed?

Skype for Business doesn't allow injecting arbitrary audio directly: it only transmits whatever it receives from the device configured as the **microphone**. VB-CABLE solves this by creating a pair of virtual audio devices in Windows:

> Simple & Easy! All audio streams sent to VB-CABLE inputs (Playback Device) will be forwarded to VB-CABLE outputs (Recording Device).

In other words: everything this app plays to **"CABLE Input"** (which Windows sees as a playback device) automatically shows up on **"CABLE Output"** (which Windows sees as a recording device). This lets "CABLE Output" be used as if it were a real microphone.

Concrete flow in this app:

1. The user types a message and the app encodes it as FSK tones (Baudot ITA-2).
2. The app plays those tones to **CABLE Input** (the "Audio output" device in the UI).
3. VB-CABLE forwards that signal to **CABLE Output**.
4. In Skype for Business, configured with **CABLE Output** as its microphone, the tones are transmitted as if they were the user's voice.

### Installation and setup

1. Download and install VB-CABLE from <https://vb-audio.com/Cable/>.
2. In Skype for Business → Audio settings → **Microphone**: select `CABLE Output (VB-Audio Virtual Cable)`.
3. In the app, in the **"Audio output"** selector, choose `CABLE Input (VB-Audio Virtual Cable)`.
4. The call must connect to a **real PSTN number** with the gateway/SIP trunk set to **G.711 without transcoding** — SfB-to-SfB calls use Silk/RTAudio and don't preserve the FSK tones.

> VB-CABLE is a kernel driver; it may require IT approval in corporate environments.

## Build and run

```bash
dotnet build
dotnet run
```

## Publish a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting `.exe` ends up at `bin\Release\net10.0-windows\win-x64\publish\TtyClient.exe`.

More details on architecture, the audio pipeline, and TTY parameters in [`CLAUDE.md`](CLAUDE.md).
