# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/).

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
