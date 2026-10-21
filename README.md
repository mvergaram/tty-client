# TTY Dynamic Client

TTY/TDD software client for Windows 11 that captures softphone call audio and decodes/encodes Baudot FSK tones in real time.

## TTY Protocol

This app implements the **US Weitbrecht TTY/TDD standard**, the protocol used by TTY devices over the telephone network in the United States:

- **Modulation**: audio-frequency FSK (Frequency-Shift Keying)
- **MARK frequency**: 1400 Hz (binary 1 / idle tone)
- **SPACE frequency**: 1800 Hz (binary 0)
- **Baud rate**: 45.45 baud (~22 ms per bit)
- **Character encoding**: Baudot code, ITA-2 variant — 5 data bits per character, with LTRS/FIGS shift codes to switch between letters and digits/punctuation
- **Frame structure**: 1 START bit (SPACE) + 5 DATA bits (LSB first) + 1.5 STOP bits (MARK)
- **Sample rate**: 8000 Hz internally, matching standard telephony audio

Decoding measures tone power at 1400 Hz and 1800 Hz with the Goertzel algorithm over short, overlapping windows, then reconstructs each character by locking onto its own start-bit edge instead of free-running a block counter — this keeps bit sampling correctly aligned even though the gap between characters (stop bits, a human typist's pauses) isn't a fixed length.

TTY tones only survive a call path that doesn't transcode or compress the audio, which is why this only works over a real PSTN call with G.711 (see [Installation and setup](#installation-and-setup) below) — calls that stay within the same softphone platform often use a proprietary codec and strip the tones out.

See [`CLAUDE.md`](CLAUDE.md) for the fixed-parameters table and the full decode/encode pipeline diagrams.

## Requirements

- Windows 11
- .NET 10 Desktop Runtime (or build/publish self-contained, see below)
- **[VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/)** — free virtual audio driver, **required** to send the synthesized TTY tones to your softphone

### Why is VB-CABLE needed?

Most softphones don't allow injecting arbitrary audio directly: they only transmit whatever they receive from the device configured as the **microphone**. VB-CABLE solves this by creating a pair of virtual audio devices in Windows:

> Simple & Easy! All audio streams sent to VB-CABLE inputs (Playback Device) will be forwarded to VB-CABLE outputs (Recording Device).

In other words: everything this app plays to **"CABLE Input"** (which Windows sees as a playback device) automatically shows up on **"CABLE Output"** (which Windows sees as a recording device). This lets "CABLE Output" be used as if it were a real microphone.

Concrete flow in this app:

1. The user types a message and the app encodes it as FSK tones (Baudot ITA-2).
2. The app plays those tones to **CABLE Input** (the "Audio output" device in the UI).
3. VB-CABLE forwards that signal to **CABLE Output**.
4. In your softphone, configured with **CABLE Output** as its microphone, the tones are transmitted as if they were the user's voice.

### Installation and setup

1. Download and install VB-CABLE from <https://vb-audio.com/Cable/>.
2. In your softphone's audio settings → **Microphone**: select `CABLE Output (VB-Audio Virtual Cable)`.
3. In the app, in the **"Audio output"** selector, choose `CABLE Input (VB-Audio Virtual Cable)`.
4. The call must connect to a **real PSTN number** with the gateway/SIP trunk set to **G.711 without transcoding** — calls that stay within the same softphone platform often use a proprietary codec and don't preserve the FSK tones.

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
