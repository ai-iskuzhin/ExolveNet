# ExolveNet

Неофициальный .NET SDK для платформы **МТС Exolve**. В текущей версии закрыт **Number Lookup
(HLR) API** — проверка номера: активность, статус SIM, оператор и регион, давность активности,
удачное время для контакта.

Не аффилирован с МТС Exolve и АО «МТТ».

[![NuGet](https://img.shields.io/nuget/v/ExolveNet.svg)](https://www.nuget.org/packages/ExolveNet/)

## Установка

```bash
dotnet add package ExolveNet
```

Таргеты: `netstandard2.0`, `net8.0`, `net10.0`.

## Поддерживаемые методы

| Метод Exolve | API клиента | HTTP | Только МТС? |
| --- | --- | --- | --- |
| Оценка активности | `GetActivityScoreAsync` | `POST hlr/{v}/GetActivityScore` | нет |
| Статус SIM | `GetSimStatusAsync` | `POST hlr/v1/GetSimStatus` | **да** |
| Оператор и регион | `GetBaseNumberInfoAsync` | `POST hlr/v1/GetBaseNumberInfo` | не указано |
| Давность активности | `GetLastDateActivityAsync` | `POST hlr/v1/GetLastDateActivity` | не указано |
| Время для звонка | `GetBestCallTimeAsync` | `POST hlr/v1/GetBestCallTime` | **да** |
| Время для SMS | `GetBestSmsTimeAsync` | `POST hlr/v1/GetBestSmsTime` | **да** |

Пакетные отчёты (`GenerateActivityScoreReport`, `GetHLRReport` и остальные — до 50 000 номеров за
раз) пока не реализованы.

Полное описание полей — в [docs/api-hlr.md](docs/api-hlr.md).

## Быстрый старт

```csharp
using ExolveNet;

var client = new ExolveHlrClient(
    httpClient,                                  // можно из IHttpClientFactory
    new ExolveClientOptions { ApiKey = "<API-ключ приложения>" });

var score = await client.GetActivityScoreAsync("+7 913 999-99-99");

Console.WriteLine(score.Score);     // 0.85 — от 0 (нет активности) до 1
Console.WriteLine(score.OwnerId);   // оператор
Console.WriteLine(score.Number);    // 79139999999
```

Номер принимается в любом привычном виде — `+7…`, `8…`, с пробелами и дефисами — и приводится к
`7XXXXXXXXXX` до отправки. Это не косметика: запрос в неверном формате Exolve отклоняет, **но
тарифицирует**, поэтому единственный бесплатный отказ — локальный.

### Какой оператор обслуживает номер

```csharp
var info = await client.GetBaseNumberInfoAsync("+79139999999");

Console.WriteLine(info.OwnerId);   // Tele2
Console.WriteLine(info.Mnc);       // 20
Console.WriteLine(info.IsPorted);  // true — номер переносили
```

По префиксу оператора определить нельзя: номер мог быть перенесён (MNP), и тогда «мтс-овский»
префикс принадлежит другому оператору. `GetBaseNumberInfo` — единственный надёжный ответ.

### Методы «только для МТС»

```csharp
try
{
    var sim = await client.GetSimStatusAsync(number);
    Console.WriteLine(sim.IsRegistered);
}
catch (ExolveApiException ex) when (ex.IsNonMtsNumber)
{
    // 400 enter another number — номер не МТС. Ожидаемый исход, а не сбой.
}
```

## Тарификация — важное

**Платный запрос — любой запрос, независимо от ответа.** Если по номеру нет данных (обычно не
было активности около двух лет), API возвращает ошибку, и деньги всё равно списываются.
«Неуспешного» HLR-запроса с точки зрения счёта не существует — перепродавать его «за успех»
нельзя.

## Обработка ошибок

| Исключение | Когда |
| --- | --- |
| `ExolveValidationException` | номер не прошёл локальную проверку — в сеть ничего не ушло |
| `ExolveApiException` | Exolve ответил вне диапазона 2xx |
| `ExolveTransportException` | ответа нет: сеть, таймаут, TLS |
| `ExolveProtocolException` | ответ есть, но не разбирается |

У `ExolveApiException` есть признаки для частых случаев, чтобы не сравнивать строки в своём коде:
`IsNonMtsNumber`, `IsHlrDisabled`, `IsUnsignedCustomer` (тестовый период — можно проверять только
свой подтверждённый номер; легко спутать с ограничением по оператору).

## Персональные данные

Номер телефона — персональные данные, а текст исключения почти наверняка попадёт в лог. Поэтому:

- в `ExolveProtocolException.BodyPreview` номер замаскирован;
- сырое тело ответа в `Metadata.RawBody` **не сохраняется**, пока не включишь
  `CaptureRawResponseBody`.

## Лицензия

MIT.
