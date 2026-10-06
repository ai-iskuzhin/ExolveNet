# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-10-06

### Added

- Initial release of `ExolveNet`: an unofficial .NET SDK for the MTS Exolve platform.
- `ExolveHlrClient` covering the six single-number Number Lookup (HLR) methods:
  `GetActivityScoreAsync`, `GetSimStatusAsync`, `GetBaseNumberInfoAsync`,
  `GetLastDateActivityAsync`, `GetBestCallTimeAsync`, `GetBestSmsTimeAsync`.
- Bearer-key authentication, configurable base address and HLR API version (`v1` / `v2`).
- Typed results that parse the wire quirks rather than passing them on: the activity score and
  the day count arrive as strings, and the best-contact window as `"17:00:00,21:00:00"` — exposed
  as `double?`, `int?` and a `TimeSpan?` pair, with the raw value kept alongside so an
  unparseable score reads as "unknown" instead of silently becoming zero.
- Number normalization before every call (`+7…`, `8…`, spaces and dashes → `7XXXXXXXXXX`).
  Exolve rejects a malformed number **and still bills for it**, so the only free rejection is a
  local one.
- `ExolveValidationException` / `ExolveApiException` / `ExolveTransportException` /
  `ExolveProtocolException`, with `IsNonMtsNumber`, `IsHlrDisabled` and `IsUnsignedCustomer` on
  the API exception so callers do not match on error strings.
- Personal-data handling: the phone number is masked in `BodyPreview`, and the raw response body
  is only retained when `CaptureRawResponseBody` is set.
- Multi-targeting for `netstandard2.0`, `net8.0` and `net10.0`.

- The four batch report methods, up to 50 000 numbers per report:
  `GenerateActivityScoreReportAsync`, `GenerateBestCallTimeReportAsync`, `GetReportAsync`,
  `ListReportsAsync`. The number list goes over the wire as a **base64 file** rather than a JSON
  array — the SDK normalizes each number and encodes the list, and refuses an empty list, a list
  over the limit, or a single bad number locally, *before* a billable request. `GenerateReportAsync`
  takes a prepared base64 payload for the case where Exolve's undocumented file format turns out
  not to be newline-separated.
- `HlrReportStatus` / `HlrReportType` parse whether the field arrives as a number
  (`GetHLRReport`) or a string (`GetHLRListReport`) — the two endpoints document it differently
  for the same values — and an unrecognised value becomes `Unknown` instead of failing the parse.
- `HlrReport.RetainedUntil` renames the wire's `created_at`, which is documented as the 30-day
  **retention deadline**, not a creation time.
- `MobileOperator` enum with `BaseNumberInfoResult.Operator` and `ActivityScoreResult.Operator`,
  resolved from the network code (MNC) and falling back to the `owner_id` text. The table covers
  the four federal operators only — anything else resolves to `Other`, because naming an operator
  we are not sure of is worse than saying it is not one of those four. This is the field that
  answers "may this number go to an MTS-only method", which a prefix table cannot: a ported number
  (`mnp`) carries the wrong prefix.
- Package icon.

### Not included yet

- PassCall and FlashCall (authorization by voice / by call). Their specification lives on
  `wiki.exolve.ru`, which serves a navigation shell rather than the API reference, so the field
  names could not be verified — and guessing them in an SDK is worse than omitting them.
