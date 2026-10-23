# TtyClient.Tests

xUnit test suite for the `TtyClient` project. It covers the TTY protocol stack (Baudot encode/decode, Goertzel tone detection, bit synchronization, FSK synthesis) and the `Audio` classes that don't need a real device (`AudioResampler`, `AudioEndpointInfo`, `WavFileHandler`) with unit and round-trip tests, plus two packaging/regression checks. The classes that wrap live WASAPI devices (`AudioCapture`, `VirtualMicInjector`) have their pure, hardware-independent logic (format validation, virtual-device name matching) extracted into small `internal` methods and tested directly; the rest of those classes, and the WPF layer (`App`, `MainWindow`, the `Views`), have no automated tests and are validated manually (see [`CLAUDE.md`](../CLAUDE.md)).

`TtyClient.csproj` exposes `internal` members to this project via `InternalsVisibleTo`, so tests can call `BaudotTables`, `WavFileHandler`, `AudioCapture.EnsureSupportedFormat`, and `VirtualMicInjector.IsVirtualAudioDeviceName` directly.

## Running the tests

```bash
dotnet test TtyClient.Tests/TtyClient.Tests.csproj
```

With code coverage (uses the `coverlet.collector` package already referenced by this project; no `.runsettings` is checked in, so this is run on demand rather than as part of CI):

```bash
dotnet test TtyClient.Tests/TtyClient.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

This writes a Cobertura XML report under `TestResults/<guid>/coverage.cobertura.xml`.

## Test classes

### `BaudotDecoderTests.cs`
Target: `TTY/BaudotDecoder.cs` (`BaudotDecoder`), using `TTY/BaudotTables.cs` constants as inputs.

- `OnByte_LtrsAndFigsShift_SwitchStateWithoutEmittingCharacter` — shift codes (LTRS, FIGS) change internal state but emit no character.
- `OnByte_SameCodeDecodesDifferently_DependingOnShift` — code `1` decodes to `'E'` under LTRS and `'3'` under FIGS.
- `OnByte_ControlCharacters_DecodeIdenticallyInBothShifts` — line feed / space / carriage return decode the same regardless of shift state.
- `OnByte_UnassignedFigsCode_EmitsNothing` (codes 0, 9, 11, 13) — FIGS codes with no assigned character are silently dropped.
- `OnByte_BlankCode_EmitsNothingInEitherShift` — code `0` (blank) emits nothing under LTRS.
- `Reset_ReturnsToLettersShift` — `Reset()` after a FIGS shift returns decoding to LTRS.
- `OnByte_CodeOutOfRange_ThrowsIndexOutOfRangeException` (codes 32, 255) — codes outside the 5-bit range (0–31) throw.

### `BaudotEncoderTests.cs`
Target: `TTY/BaudotEncoder.cs` (`BaudotEncoder`, static), asserting against `BaudotTables` constants.

- `Encode_PrependsAndAppendsLtrs_ForEmptyString` — `""` encodes to just the open/close `[LTRS, LTRS]` wrapper.
- `Encode_SimpleLetters_ProducesLtrsPrefixAndCodes` — `"SOS"` encodes to the correct per-letter codes with no redundant shifts.
- `Encode_Lowercase_MatchesUppercase` — `"ABC"` and `"abc"` produce identical output.
- `Encode_LetterDigitLetterDigit_MinimizesShifts` — `"A1B2"` emits a shift exactly on each letter/digit transition, not per character.
- `Encode_RepeatedFigures_DoesNotRepeatShift` — `"12"` emits a single FIGS shift, not one per digit.
- `Encode_ControlCharacters_DoNotForceFigsShift` — `" \r\n"` stays in LTRS throughout.
- `Encode_UnsupportedCharacters_AreSilentlyDropped` — `"A@B"` encodes the same as `"AB"`.
- `Encode_OnlyUnsupportedCharacters_ProducesJustLtrsWrapper` — `"@@@"` encodes the same as `""`.
- `Encode_Null_ThrowsNullReferenceException` — `null` input throws (documents an unguarded null path).

### `BitSynchronizerTests.cs`
Target: `TTY/BitSynchronizer.cs` (`BitSynchronizer`).

Helper: `BuildFrameTicks(code, cleanStop)` generates the quarter-bit oversampled tick sequence for one async frame (1 start + 5 data LSB-first + 1.5 stop), optionally with a corrupted stop bit.

- `OnBit_FullValidFrame_EmitsCorrectByte` (codes 0, 1, 5, 17, 31) — a clean tick sequence round-trips to the exact byte.
- `OnBit_GlitchNotSustainedAtStartBitCenter_IsIgnored` — a false start-bit edge that doesn't hold through its center is discarded; a following valid frame still decodes correctly.
- `OnBit_CorruptStopBit_StillEmitsCharacter` (codes 3, 30) — a bad stop bit doesn't prevent that character's data bits from being emitted.
- `OnBit_BackToBackFramesWithoutIdleGap_BothDecodeCorrectly` — two frames with no idle gap between them both decode in order.
- `Reset_MidFrame_DiscardsPartialFrameWithoutEmitting` — `Reset()` mid-frame discards partial state; a subsequent full frame decodes cleanly.

### `FSKSynthesizerTests.cs`
Target: `TTY/FSKSynthesizer.cs` (`FSKSynthesizer`), exercised together with `GoertzelDetector`, `BitSynchronizer`, `BaudotDecoder`/`BaudotEncoder` as a full encode→audio→decode pipeline.

- `Synthesize_EmptyList_ProducesOnly200msMarkIdle` — an empty code list produces exactly 200 ms of MARK-only idle tone.
- `Synthesize_SingleCharacter_StartBitIsSpaceAndStopBitIsMark` — the synthesized start bit decodes as SPACE and the 1.5-bit stop period as MARK.
- `Synthesize_RoundTrip_DecodesBackToOriginalText` (`"SOS"`, `"HELLO WORLD"`, `"TEST 123"`, `""`) — end-to-end: encode → synthesize → Goertzel → bit sync → decode reproduces the original (uppercased) text.
- `Synthesize_CodeWithBitsAboveFive_IsTruncatedToLowFiveBits` — bits above the 5-bit Baudot width are masked off before synthesis.
- `Synthesize_Null_ThrowsNullReferenceException` — `null` input throws (unguarded null path).

### `GoertzelDetectorTests.cs`
Target: `TTY/GoertzelDetector.cs` (`GoertzelDetector`).

Helper: `GenerateTone(freqHz, count, amplitude)` synthesizes a pure sine wave for feeding the detector.

- `ProcessSamples_PureMarkTone_DetectsMark` — a pure 1400 Hz tone decodes as MARK only.
- `ProcessSamples_PureSpaceTone_DetectsSpace` — a pure 1800 Hz tone decodes as SPACE only.
- `ProcessSamples_Silence_ResolvesToSpace` — an all-zero buffer resolves to SPACE (documents the no-signal tie-break).
- `ProcessSamples_PartialWindowAtStreamStart_EmitsNoBitUntilWindowFills` — no detection is emitted until the analysis window has enough samples; validates the exact `AnalysisWindow`/hop boundary math.
- `ProcessSamples_StreamedInSmallChunks_MatchesSingleCallResult` — feeding samples one at a time vs. in one call produces identical detections (buffering is chunk-boundary safe).
- `ProcessSamples_CountGreaterThanArrayLength_ThrowsIndexOutOfRangeException` — an out-of-bounds `count` throws.

### `AudioResamplerTests.cs`
Target: `Audio/AudioResampler.cs` (`AudioResampler`), also exercised together with `GoertzelDetector` for a realistic end-to-end case.

- `Resample_MonoAtNativeRate_ScalesFloatToInt16Range` — at a 1:1 ratio (8000 Hz input), known float samples scale to the expected `short`s with hand-computed values.
- `Resample_StereoInput_DownmixesChannelsByAveraging` — interleaved L/R samples are averaged into mono before resampling.
- `Resample_FractionalRatio_LinearlyInterpolatesBetweenSamples` — a 12000→8000 Hz ratio (1.5) produces hand-computed linearly-interpolated values, proving the `frac` blend math.
- `Resample_AmplitudeAboveUnity_ClampsToInt16Range` — float samples beyond ±1.0 clamp to `short.MaxValue`/`MinValue` instead of wrapping.
- `Resample_EmptyInput_ReturnsEmptyArray` — a zero-length buffer returns an empty array.
- `Resample_StateCarriesAcrossCalls_StreamingMatchesSingleCall` — splitting one buffer into two `Resample` calls (at a frame-aligned boundary) produces the same output as one call with the full buffer, proving the carried-over fractional position state is correct.
- `Resample_RealisticLoopbackRate_PreservesToneForGoertzelDetection` — a 1400 Hz tone at a typical 48 kHz stereo WASAPI loopback format, resampled down to 8 kHz mono, is still detected as MARK by `GoertzelDetector`.

### `AudioEndpointInfoTests.cs`
Target: `Audio/AudioEndpointInfo.cs` (`AudioEndpointInfo`).

- `IsLoopback_RenderFlow_ReturnsTrue` / `IsLoopback_CaptureFlow_ReturnsFalse` — the loopback flag follows `DataFlow.Render` vs. `DataFlow.Capture`.
- `DisplayName_RenderFlow_AppendsOutputLoopbackSuffix` / `DisplayName_CaptureFlow_AppendsMicrophoneSuffix` — the UI-facing label gets the correct `"— Output (loopback)"` / `"— Microphone"` suffix.

### `WavFileHandlerTests.cs`
Target: `Audio/WavFileHandler.cs` (`WavFileHandler`, internal), both methods — complements `WavDecodingTests.cs` below, which only exercises `LoadSamples`.

- `SaveToWav_ThenLoadSamples_RoundTripsThroughFullDecodePipeline` — synthesized TTY audio (`BaudotEncoder` → `FSKSynthesizer`) is saved to a temp `.wav` file, then loaded and decoded (`GoertzelDetector` → `BitSynchronizer` → `BaudotDecoder`) back to the original text — validates the exact pipeline behind `AudioFilesView`'s "Save WAV"/"Import WAV".
- `SaveToWav_WritesFixedTtyWaveFormat` — the written file's format (8000 Hz, 16-bit, mono) matches the fixed `TtyFormat`, verified independently via `NAudio.Wave.WaveFileReader`.

### `AudioCaptureFormatValidationTests.cs`
Target: `Audio/AudioCapture.cs`'s `EnsureSupportedFormat` (bumped from `private` to `internal` to make this pure validation logic testable without a real capture device).

- `EnsureSupportedFormat_IeeeFloat32_DoesNotThrow` / `EnsureSupportedFormat_Extensible32Bit_DoesNotThrow` — both accepted 32-bit float encodings pass.
- `EnsureSupportedFormat_Pcm16Bit_ThrowsNotSupportedExceptionNamingDevice` — a 16-bit PCM format throws, and the message names the device and explains the fix.

### `VirtualMicInjectorDeviceFilterTests.cs`
Target: `Audio/VirtualMicInjector.cs`'s `IsVirtualAudioDeviceName` (extracted from the inline filter in `GetVirtualDevices()` into a testable `internal static` predicate, same behavior).

- `IsVirtualAudioDeviceName_VirtualDeviceNames_ReturnsTrue` (CABLE Input/Output, case-insensitive, "Virtual", "VB-Audio") — real VB-CABLE and generic virtual-device names match.
- `IsVirtualAudioDeviceName_RealHardwareDeviceNames_ReturnsFalse` (Realtek, Speakers, Headset Microphone) — ordinary hardware device names don't.

### `WavDecodingTests.cs`
Target: `Audio/WavFileHandler.cs` (`WavFileHandler`, internal), integrated with `GoertzelDetector` → `BitSynchronizer` → `BaudotDecoder` against **real captured PSTN audio fixtures** under `Audios/`.

Helpers: `AudioFiles()` (enumerates `Audios/*.wav`, pairing each file with its filename-derived expected text), `LevenshteinDistance` (fuzzy string comparison), `MaxAllowedEditDistance = 5` (tolerance for real-world noise/sync artifacts).

- `DecodesWavFileToExpectedText` — for each real recording (`PLEASE HOLD.wav`, `PLEASE CONTINUE TO HOLD.wav`, `THANKS FOR CALLING MEMBER ENGLISH RT.PLEASE TYPE GA TO CONTINUE. GA.wav`), the decoded text is within edit distance 5 of the expected transcript. This is a regression test against real audio, not an exact-match unit test.

### `PublishOutputTests.cs`
Not a test of app logic — a build/packaging sanity check on `TtyClient.csproj` itself.

- `BuildOutputDoesNotContainTestAudioAssets` — runs `dotnet build TtyClient.csproj -c Release` into a temp folder and asserts no `*.wav` file (e.g. the test fixtures) leaks into the shipped output.

## Code coverage per app class

Measured with `dotnet test --collect:"XPlat Code Coverage"` (72/72 tests passing at time of writing). Percentages are a point-in-time snapshot — regenerate them with the command above to check current numbers.

| Class | Line | Branch | Covered by |
|---|---|---|---|
| `TTY.BaudotEncoder` | 100% | 100% | `BaudotEncoderTests` |
| `TTY.BaudotTables` | 100% | 100% | indirectly, via `BaudotDecoderTests` / `BaudotEncoderTests` |
| `TTY.FSKSynthesizer` | 100% | 100% | `FSKSynthesizerTests` |
| `Audio.AudioEndpointInfo` | 100% | 100% | `AudioEndpointInfoTests` |
| `Audio.AudioResampler` | 100% | 78.57% | `AudioResamplerTests` |
| `Audio.WavFileHandler` | 100% | 75% | `WavFileHandlerTests`, `WavDecodingTests` |
| `TTY.BitSynchronizer` | 100% | 93.75% | `BitSynchronizerTests` |
| `TTY.GoertzelDetector` | 100% | 93.75% | `GoertzelDetectorTests` |
| `TTY.BaudotDecoder` | 100% | 90% | `BaudotDecoderTests` |
| `Audio.AudioCapture` | 14.28% | 16.66% | `AudioCaptureFormatValidationTests` (only `EnsureSupportedFormat`; `GetInputDevices`/`Start`/`Stop`/`OnDataAvailable` need a real device) |
| `Audio.VirtualMicInjector` | 7.89% | 20% | `VirtualMicInjectorDeviceFilterTests` (only `IsVirtualAudioDeviceName`; `Play`/`Stop`/`Dispose` need a real device) |
| `Views.AudioFilesView` | 0% | 0% | none — WPF view |
| `Views.CallConversationView` | 0% | 0% | none — WPF view |
| `App`, `MainWindow` | 0% | 0% | none — WPF shell |

The TTY protocol core (Baudot encode/decode, bit framing, tone detection, FSK synthesis) and the hardware-independent `Audio` classes (`AudioResampler`, `AudioEndpointInfo`, `WavFileHandler`) are fully covered at the line level, with the remaining branch gaps being single defensive/edge branches (e.g. `BaudotDecoder`'s FIGS-unassigned-code paths, `BitSynchronizer`'s corrupted-stop-bit branch, `WavFileHandler`'s untaken stereo-to-mono branch since all fixtures are mono). `AudioCapture` and `VirtualMicInjector` only reach their extracted pure-logic methods (`EnsureSupportedFormat`, `IsVirtualAudioDeviceName`) — the rest of those classes, and the UI classes, require live WASAPI devices or interactive WPF rendering and are covered by manual testing instead (see [`CLAUDE.md`](../CLAUDE.md)'s known limitations and the installation/setup steps in [`README.md`](../README.md)).
