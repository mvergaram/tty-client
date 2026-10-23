# TTY Client

TTY/TDD software client for Windows 11 that captures softphone call audio and decodes/encodes Baudot FSK tones in real time.

## Stack

- **Language**: C# 13 / .NET 10 (net10.0-windows)
- **UI**: WPF
- **Audio**: NAudio 2.2.1 (WASAPI loopback + WasapiOut)
- **TTY protocol**: US Weitbrecht standard — 45.45 baud, 1400/1800 Hz FSK, Baudot ITA-2

## Commands

```bash
# Build
dotnet build

# Run
dotnet run

# Publish a standalone executable (a single .exe, no loose native DLLs)
dotnet publish TtyClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting `.exe` ends up at `bin\Release\net10.0-windows\win-x64\publish\TtyClient.exe`.

- **Self-contained** (bundles the .NET 10 runtime): the `.exe` is heavier but runs on any Windows 11 x64 machine with nothing else installed. The framework-dependent alternative produces a lighter `.exe` but requires the .NET 10 Desktop Runtime installed on the target machine.
- **`IncludeNativeLibrariesForSelfExtract=true`**: the native WPF interop DLLs (`PresentationNative_cor3.dll`, `wpfgfx_cor3.dll`, `vcruntime140_cor3.dll`, etc.) get embedded inside the single `.exe` and self-extract to a temp folder on first launch, instead of sitting loose next to the `.exe`.
- **Icon and name**: configured in `TtyClient.csproj` (`ApplicationIcon`, `Product`, `AssemblyTitle`, `Version`) and in `MainWindow.xaml`'s `Icon=`/`Title=`. The icon lives at `Assets\icon.ico` — replace that file and republish to change it.

## Structure

```
tty-client/
├── Audio/
│   ├── AudioCapture.cs        # WASAPI loopback (Render) or real capture (Capture) depending on the chosen device
│   ├── AudioEndpointInfo.cs   # Id/FriendlyName/DataFlow of an endpoint selectable as "Audio input"
│   ├── AudioResampler.cs      # Float32@inputRate → Int16@8kHz, linear
│   ├── VirtualMicInjector.cs  # Plays FSK to VB-Cable or another virtual device (always Render)
│   └── WavFileHandler.cs      # SaveToWav/LoadSamples: PCM 16-bit@8kHz ↔ .wav file (NAudio)
├── TTY/
│   ├── GoertzelDetector.cs    # Power @ 1400 Hz vs 1800 Hz, 20ms blocks (160 samples)
│   ├── BitSynchronizer.cs     # FSM: IDLE → START_BIT → DATA_BITS(5) → STOP_BIT
│   ├── BaudotDecoder.cs       # ITA-2 5-bit → char, manages LTRS/FIGS shift
│   ├── BaudotEncoder.cs       # text → List<byte> of ITA-2 codes with shifts
│   └── FSKSynthesizer.cs      # 1400/1800 Hz sine synthesis, 45.45 baud, 16-bit PCM
├── Views/
│   ├── CallConversationView.xaml(.cs)  # Live call interface: input/output devices, start/stop listening, send text via virtual microphone
│   └── AudioFilesView.xaml(.cs)        # Files interface: text → WAV ("Save WAV") and WAV → text ("Import WAV")
├── MainWindow.xaml             # Shell: header + TabControl hosting the two views (dark Catppuccin theme in App.xaml)
├── MainWindow.xaml.cs          # Closes the window safely, awaiting CallConversationView.ShutdownAsync()
└── TtyClient.csproj
```

Each interface is an independent `UserControl` with its own state (its own `FSKSynthesizer` instance, its own transcript): `CallConversationView` owns the live audio resources (`AudioCapture`, `VirtualMicInjector`) and exposes `ShutdownAsync()` to release them without blocking the UI thread on close; `AudioFilesView` has no dependency on any live audio device. Shared styles (`ActionButton`, `SendButton`, `ComboBox` styles) live in `App.xaml` so both views can reuse them.

## Decoding pipeline

The UI has an "Audio input" selector that lists ALL active endpoints: first the Render ones (for loopback of a softphone call playing through that device), then the Capture ones (real physical microphones/handsets, e.g. an acoustically coupled TTY). `AudioCapture.Start(AudioEndpointInfo?)` picks `WasapiLoopbackCapture` or `WasapiCapture` based on the chosen device's `DataFlow`:

```
Softphone call audio (render device, loopback)  Physical microphone/handset (capture device)
  → WasapiLoopbackCapture                            → WasapiCapture
                    \                                  /
                     → Float32 → Int16 mono @8kHz (AudioResampler)
                     → Goertzel 1400 Hz / 1800 Hz, 160-sample blocks (GoertzelDetector)
                     → MARK/SPACE decision per block → BitSynchronizer (FSM)
                     → ITA-2 5-bit bytes → BaudotDecoder
                     → char → UI (TextBlock)
```

Both paths require float32 input format (`AudioCapture.EnsureSupportedFormat`); if the device provides another format, a clear error is raised instead of decoding garbage.

## Encoding pipeline (sending)

```
user text
  → BaudotEncoder → List<byte> of ITA-2 codes with shifts
  → FSKSynthesizer → 16-bit PCM 8kHz (1400/1800 Hz sines at 45.45 baud)
  → VirtualMicInjector → WasapiOut on VB-Cable
  → The softphone transmits the tones as if they were the microphone's voice
```

## Fixed TTY parameters

| Constant | Value | Where |
|---|---|---|
| MARK frequency | 1400 Hz | GoertzelDetector, FSKSynthesizer |
| SPACE frequency | 1800 Hz | GoertzelDetector, FSKSynthesizer |
| Baud rate | 45.45 baud | FSKSynthesizer (SamplesPerBit = 176 @ 8kHz) |
| Internal sample rate | 8000 Hz | GoertzelDetector, FSKSynthesizer |
| Hop between decisions | 44 samples | GoertzelDetector (1/4 bit, `OversampleFactor` = 4) |
| Analysis window | 80 samples | GoertzelDetector (10ms, overlapping; multiple of 40 → 1400/1800 Hz land on exact bins) |
| Frame structure | 1 START + 5 DATA + 1.5 STOP | BitSynchronizer, FSKSynthesizer |

## Environment prerequisites

1. **VB-Audio Virtual Cable** installed (free driver): <https://vb-audio.com/Cable/>
2. In your softphone's audio settings → **Microphone**: select `CABLE Input (VB-Audio Virtual Cable)`
3. The call must connect to a **real PSTN number** — calls that stay within the same softphone platform often use a proprietary codec, which does not preserve the FSK tones
4. The gateway/SIP trunk must be configured with **G.711 without transcoding**

## Known limitations

- TTY audio **only works over PSTN** (G.711 required). Calls that stay within the same softphone platform are not supported (they typically use a proprietary codec instead of G.711).
- The 80-sample `AnalysisWindow` in `GoertzelDetector` is a measured trade-off between noise margin and tolerance to clock drift: longer bin-aligned windows (120, 160) decode better in noise but start failing at ±3% baud error from the transmitter, which a real gateway can produce. If changed, it must remain a multiple of 40 samples or both filters end up mistuned.
- VB-Cable is a kernel driver; it may require IT approval in corporate environments.

## Maintenance of related materials

Whenever a change modifies the app's UI (views in `Views/`, styles in `App.xaml`, etc.), ask the user
before finishing the task whether the following should also be updated:
- Screenshots in `README.md` (if any exist and show the part of the UI that changed).
- Screenshots in `presentation/screenshots/` and, if relevant, the related content in the generator
  script `presentation/_build/generar_presentacion.py` (the `shot_call`/`shot_files` slides in the PPTX
  and the images embedded in the presenter guide DOCX).

Do not update these materials automatically without confirming first — they are external deliverables
(demos, presenter guides) that don't always need to stay in sync with every minor visual change.

## TODO

- [x] ~~Investigate why the app hangs (becomes unresponsive) if it's listening and the window is closed (X).~~ Fixed: `MainWindow.Window_Closing` cancels the close, awaits `CallConversationView.ShutdownAsync()` (which does `_capture.Dispose()`/`_injector.Dispose()` inside a `Task.Run`, off the UI thread), and only then closes the window.
