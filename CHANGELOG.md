# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.6.0] - 2026-11-24
### Added
- The Call Conversation "send" text box now normalizes input live as it's typed or pasted:
  accents/diacritics are stripped (e.g. `mamá` → `MAMA`, `niño` → `NINO`), letters are
  upper-cased, and characters with no Baudot representation are rejected outright, so what's
  shown always matches exactly what will be transmitted. The Audio Files "text to convert" box
  now gets the same live normalization.

### Changed
- The Call Conversation "Clear" link is now only available while not listening; it's disabled,
  greyed out, and struck through while a call is being listened to, so clearing the transcript
  mid-call isn't possible.

### Fixed
- Fixed the Call Conversation window (including the text input) freezing while an AI
  improved-text request was in flight: a custom Portkey header policy was forcing the HTTP
  call to run synchronously on the UI thread instead of truly asynchronously.
- Fixed a cryptic `Unknown ChatFinishReason value` error shown when a model behind Portkey
  refuses to answer (e.g. Anthropic's `refusal` finish reason, unrecognized by the OpenAI SDK);
  it's now shown as a readable "the model declined to respond" message.
- Fixed a confusing `AI: (error) status 400: ... must contain non-whitespace text` line shown for
  a received message that decoded to nothing but spaces (e.g. a noisy/idle line). The AI call is
  now skipped for such messages; the received characters still show normally, just without an
  `AI:` line.
- Fixed sending a new message cutting off the previous one's FSK audio mid-transmission: "Send"
  is now disabled until the outgoing audio for the current message has finished playing.

## [1.5.0] - 2026-11-18
### Added
- Configurable maximum output tokens for the AI improved text feature, with its own field in
  Settings and a safe fallback when left blank or set to zero/negative.

### Changed
- Refined the default AI system prompt used to clean up decoded TTY messages.

### Fixed
- Fixed the app's window favicon.

## [1.4.0] - 2026-11-16
### Added
- AI improved text: received messages can optionally be sent through an LLM via Portkey to clean
  up typos caused by audio noise or FIGS/LTRS shift mix-ups, shown as an `AI: <improved text>`
  line next to a small badge, with errors surfaced on screen instead of failing silently.
- Configurable AI settings (endpoint URL, API key, provider, model) in the Settings dialog,
  persisted alongside the existing app settings and applied immediately without restarting.
- Per-call transcript logging: every received message, sent message, and AI result/error is
  appended as JSON Lines to `transcription/<call-start-UTC>Z.txt` next to the executable.
- Reset of the FIGS/LTRS shift state after a configurable period of line silence, so the decoder
  recovers between TTY turns without the remote end having to resend LTRS.
- Expanded unit test coverage for the Audio folder (resampling, device info, WAV writing) and for
  the new AI configuration and transcript logging.

### Changed
- Reworked the AI configuration internals and the Settings window layout for clarity.

### Fixed
- Fixed the Settings dialog and its text box behavior.

## [1.3.0] - 2026-10-23
### Changed
- Renamed the application from "TTY Dynamic Client" to "TTY Client".
- Merged the Start listening / Stop controls into a single toggle button.
- Made the received transcript selectable and copyable in both tabs.
- Polished the Call Conversation and Audio Files tab layouts.

### Removed
- Dropped automatic selection of VB-CABLE devices at startup.

## [1.2.0] - 2026-10-22
### Changed
- Reworked the Call Conversation tab layout: relocated the audio device selectors, relabeled
  them, and replaced the default tab chrome with a flat segmented-control style.
- Color-coded the transcript by sender (received in green, sent in blue).

### Fixed
- Fixed a WASAPI handle leak that prevented switching the input device while already listening.

## [1.1.1] - 2026-10-21
### Fixed
- Fixed the release publish command to target `TtyClient.csproj` explicitly, after adding the
  test project caused bare `dotnet publish` to also try publishing the test library.

### Changed
- Moved WAV test fixtures into the test project, next to the tests that use them.

## [1.1.0] - 2026-10-21
### Added
- Split the single-window UI into two independent views: Call Conversation (live capture/send)
  and Audio Files (text-to-WAV / WAV-to-text), each with its own state.
- Automatic selection of VB-CABLE devices at startup, with a one-time warning when missing.
- GitHub Actions workflow to publish a release build on every push to `main`.
- TTY protocol documentation in the README.
- Initial xUnit test suite decoding real recorded TTY call audio.

### Fixed
- Fixed a real decoding bug: the ITA-2 Baudot letters/figures tables had drifted out of
  alignment from index 21 onward, silently mistranslating several characters.

### Changed
- Translated all UI text, code comments, and documentation to English.
- Removed "Skype for Business" branding from the app and docs in favor of generic "softphone"
  wording.
