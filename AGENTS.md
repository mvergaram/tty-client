# TTY Client

TTY/TDD software client for Windows 11 that captures softphone call audio and decodes/encodes Baudot FSK tones in real time.

## CLAUDE.md and AGENTS.md

Avoid using/creating CLAUDE.md file. Instead, document on AGENTS.md except those Claude code specific items.

## Stack

- **Language**: C# 13 / .NET 10 (net10.0-windows)
- **UI**: WPF
- **Audio**: NAudio 2.2.1 (WASAPI loopback + WasapiOut)
- **TTY protocol**: US Weitbrecht standard — 45.45 baud, 1400/1800 Hz FSK, Baudot ITA-2
- **AI text improvement**: OpenAI NuGet package (`OpenAI.Chat.ChatClient`) routed through [Portkey](https://portkey.ai/docs/introduction/what-is-portkey.md) as the AI gateway

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
│   ├── GoertzelDetector.cs    # Power @ 1400 Hz vs 1800 Hz, 10ms window oversampled 4x/bit; squelches sub-noise-floor hops and raises LineIdle after a configurable run of silence
│   ├── BitSynchronizer.cs     # FSM: IDLE → START_BIT → DATA_BITS(5) → STOP_BIT
│   ├── BaudotDecoder.cs       # ITA-2 5-bit → char, manages LTRS/FIGS shift
│   ├── BaudotEncoder.cs       # text → List<byte> of ITA-2 codes with shifts
│   └── FSKSynthesizer.cs      # 1400/1800 Hz sine synthesis, 45.45 baud, 16-bit PCM
├── Views/
│   ├── CallConversationView.xaml(.cs)  # Live call interface: input/output devices, start/stop listening, send text via virtual microphone
│   └── AudioFilesView.xaml(.cs)        # Files interface: text → WAV ("Save WAV") and WAV → text ("Import WAV")
├── AI/
│   ├── AiConfig.cs                # Loads ai.conf (ApiKey/Model/SystemPrompt), per-field defaults
│   ├── PortkeyModelReference.cs   # Parses "@provider/model" into the Portkey provider slug + model name
│   ├── PortkeyChatClient.cs       # OpenAI ChatClient wrapped with Portkey's gateway headers (see AI improved text)
│   └── CallTranscriptLogger.cs    # JSON Lines writer for each call's transcription/<timestamp>Z.txt
├── AppSettings.cs              # Persisted user settings (JSON in %AppData%\TtyClient\settings.json) — IdleHopsThreshold, AiImprovedTextEnabled
├── SettingsWindow.xaml(.cs)    # Modal settings dialog opened from MainWindow's gear button
├── MainWindow.xaml             # Shell: header (title + settings gear button) + TabControl hosting the two views (dark Catppuccin theme in App.xaml)
├── MainWindow.xaml.cs          # Opens SettingsWindow; closes the window safely, awaiting CallConversationView.ShutdownAsync()
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

`GoertzelDetector` doesn't trust a MARK/SPACE decision when neither tone's power clears a noise floor — there's no real carrier, just line noise or silence, and feeding that into `BitSynchronizer` risks fabricating a byte that happens to land on the FIGS or LTRS shift code. After enough consecutive silent hops (`LineIdle`, see [Settings](#settings)) it resets `BitSynchronizer`/`BaudotDecoder` back to LTRS instead. This is what lets the decoder recover between TTY turns without the remote end having to resend LTRS — e.g. a message that ends in "..." (FIGS) and is replayed again afterward.

## Encoding pipeline (sending)

```
user text
  → BaudotEncoder → List<byte> of ITA-2 codes with shifts
  → FSKSynthesizer → 16-bit PCM 8kHz (1400/1800 Hz sines at 45.45 baud)
  → VirtualMicInjector → WasapiOut on VB-Cable
  → The softphone transmits the tones as if they were the microphone's voice
```

## AI improved text

`CallConversationView` can send each received (decoded) message through an LLM, via Portkey, to
clean up typos caused by audio noise or FIGS/LTRS shift mix-ups. Enabled by default
(`AppSettings.AiImprovedTextEnabled`, see [Settings](#settings)); when on, a small badge (the
`Assets/ai-favicon.png` icon + "AI improved text") appears next to the "RECEIVED" header.

- **Message boundary**: a dedicated 1-second `DispatcherTimer` in `CallConversationView`, reset on
  every newly received character — independent of `GoertzelDetector.LineIdle`, which serves the
  unrelated purpose of resetting FIGS shift on its own, separately configurable threshold. When the
  timer fires, the buffered text since the last boundary is treated as one completed message.
- **Portkey call**: `AI/PortkeyChatClient.cs` follows Portkey's official C# SDK pattern — the
  `OpenAI` NuGet package's `ChatClient` pointed at `https://api.portkey.ai/v1` with a
  `PipelinePolicy` injecting the `x-portkey-api-key`/`x-portkey-provider` headers instead of a real
  OpenAI key. On success, `AI: <improved text>` is appended after the original message (mauve,
  `#CBA6F7`); on failure or a 15-second timeout, `AI: (unavailable)` is appended instead (red/pink,
  `#F38BA8`) — failures are shown, never silent.
- **`ai.conf`**: a JSON file next to the `.exe` (`AppContext.BaseDirectory`, not `Assembly.Location`
  — the latter is empty for a single-file publish) holding `ApiKey`, `Model` (a combined
  `"@provider/model"` reference, e.g. `"@dsvertex/anthropic.claude-haiku-5-5"`, split by
  `PortkeyModelReference.Parse`), and `SystemPrompt`. Missing file or missing/blank individual
  fields fall back to their own default independently (`AiConfig.DefaultApiKey`/`DefaultModel`/
  `DefaultSystemPrompt`). Loaded once when `CallConversationView` is constructed — editing
  `ai.conf` requires restarting the app to take effect.
- **Call transcript log**: every received message, sent message, and AI result/error for a call is
  appended as one JSON object per line (`AI/CallTranscriptLogger.cs`) to
  `transcription/<call-start-UTC>Z.txt` next to the `.exe`, e.g. `2026-03-05_14-23-05Z.txt`. A
  logging failure (e.g. an unwritable folder) doesn't block "Start listening" — it's swallowed and
  the call proceeds without a transcript file.

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

## Settings

The gear button at the top right of `MainWindow` (opposite the "TTY Client" title) opens
`SettingsWindow`, a modal dialog with two tunable parameters so far:

- **Silent hops to reset FIGS shift** — how many consecutive ~5.5ms Goertzel hops of
  sub-noise-floor energy must pass before the line is considered idle and `LineIdle` resets
  `BitSynchronizer`/`BaudotDecoder` back to LTRS (see [Decoding pipeline](#decoding-pipeline)).
  Default: `GoertzelDetector.DefaultIdleHopsThreshold` (~182 hops ≈ 1 second). The input only
  accepts positive integers; the dialog shows the equivalent time ("~ X sec.") live and
  validates on Save.
- **Improve received text with AI** — toggles `AiImprovedTextEnabled` (see
  [AI improved text](#ai-improved-text)). Default: enabled.

Persisted via `AppSettings` to `%AppData%\TtyClient\settings.json`. The FIGS-reset threshold only
takes effect the next time "Start listening" is pressed (`CallConversationView`) or a WAV is
imported (`AudioFilesView`) — it does not apply live to a capture that's already running, since
`GoertzelDetector`'s threshold is fixed for the lifetime of each instance. The AI toggle, by
contrast, applies immediately (badge visibility and whether new messages are sent to Portkey both
read `App.Settings.AiImprovedTextEnabled` live), even during an ongoing call.

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
