# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.2] - 2026-10-07

### Added

- **`ExolveApiException.IsNoInformation`** — «по номеру нет данных», единственный известный ответ
  с кодом `404` вместо `400`. Это **ответ, а не поломка**: регистр опрошен, в нём ничего не
  нашлось, услуга выполнена. Без признака такой ответ попадал в общую ветку ошибок и выглядел
  как сбой — а вызывающий код, приняв его за сбой, повторяет запрос, который гарантированно
  вернёт то же самое.

  У активности доля таких ответов заметная, и номер без оценки активности при этом может иметь
  базовую справку: «нет данных» — свойство услуги по этому номеру, а не самого номера.

## [0.2.1] - 2026-10-07

A third bug of the same family as 0.2.0 — the undocumented part of the wire, found by sending a
two-number report and getting one number back.

### Fixed

- **The batch file needs a header line; without it the first number is silently dropped.** Exolve
  parses the base64 file as CSV and skips the first row. `EncodeNumberList` joined the numbers with
  a newline and nothing else, so the first number of every list went in as the header and was never
  checked: the report came back `Ready`, just one number short, with `number_total` one below what
  was sent. A list of a single number was rejected outright with «at least one number is required»
  — its only line had become the header. The file now opens with a `Number` row.

  The vendor documents the field as «формат файла — base64, формат номера — 7ХХХХХХХХХХ» and says
  nothing about a header, so the previous form was a reasonable reading. It was also untested: no
  unit test covered `EncodeNumberList` at all, which is why 73 green tests said nothing about it.
  Covered now, including the single-number case.

### Note for callers

`number_total` in a report is **how many numbers Exolve parsed, not how many it answered.** A row
that came back `No information on this number` is counted in it. Anyone billing per answer should
count the rows whose `Result` is non-empty instead.

## [0.2.0] - 2026-10-07

Two bugs that every unit test in 0.1.0 passed over, because the tests encoded the vendor's
documentation rather than the wire. Both were found by running the SDK against `api.exolve.ru`.

### Fixed

- **The error body is a nested object, not a string.** Exolve answers
  `{"error":{"message":"…"}}`; the documentation shows a flat `{"error":"…"}`. Only the flat form
  was modelled, so `ExolveError.Text` was always null — and with it `IsNonMtsNumber`,
  `IsHlrDisabled` and `IsUnsignedCustomer` never fired. Those flags exist so callers never match
  on error strings, and against the real service all three were dead. Both shapes now parse, plus
  a top-level `message`.
- **`network_code` is `RU25002`, not five bare digits.** ISO country letters, then MCC, then MNC.
  Splitting by the documented positions yielded `RU2` / `50`, so a MegaFon number resolved as
  `Other` and `GetBaseNumberInfo` disagreed with `GetActivityScore` about the operator of the same
  number. Digits are extracted before splitting, so both the real and the documented form work.

### Added

- `ExolveApiException.RawBody` — the response body, kept. Discarding it is what hid the error-shape
  bug: the body was parsed, found wanting and thrown away, leaving an empty `Error` and no way to
  tell the flags had simply not matched.
- `BaseNumberInfoResult.Country` — the ISO letters from `network_code`.
- `MobileOperator.Yota` (MNC 11) and `MobileOperator.TinkoffMobile` (MNC 62), verified against the
  live API. Note that T-Mobile is an MVNO on MTS's network and is still refused by `GetSimStatus`,
  so the vendor's "is this MTS" test goes by operator of record, not by host network.

### Changed

- Documentation no longer carries detail specific to one consumer of the SDK.

### Confirmed against the live API

- `GetActivityScore` and `GetBaseNumberInfo` answer for **any** Russian operator; only
  `GetSimStatus` is MTS-only. The restriction is per method, not per platform.
- Only `200` responses are billed. Rejections — wrong operator, service disabled, malformed
  number — are not. (The "no data for this number" case is still untested.)

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
