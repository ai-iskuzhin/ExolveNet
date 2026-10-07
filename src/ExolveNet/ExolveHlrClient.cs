using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ExolveNet.Hlr;

namespace ExolveNet;

/// <summary>
/// Клиент Number Lookup (HLR) API МТС Exolve.
/// </summary>
/// <remarks>
/// Все методы — <c>POST</c> с телом <c>{"number":"7XXXXXXXXXX"}</c> и заголовком
/// <c>Authorization: Bearer …</c>. Номер нормализуется до отправки
/// (<see cref="ExolveNumbers.Normalize"/>): запрос в неверном формате Exolve отклоняет, но
/// тарифицирует.
/// <para>
/// <b>Важно про тарификацию.</b> Запрос платный независимо от содержимого ответа. Если по номеру
/// нет данных (обычно — не было активности около двух лет), API возвращает ошибку, и деньги всё
/// равно списываются. «Неуспешного» HLR-запроса с точки зрения счёта не существует, поэтому
/// перепродавать его «за успех» нельзя.
/// </para>
/// <para>
/// <b>Важно про операторов.</b> Часть методов отдаёт данные только по номерам МТС —
/// <see cref="GetSimStatusAsync"/>, <see cref="GetBestCallTimeAsync"/>,
/// <see cref="GetBestSmsTimeAsync"/>. По чужому номеру это <c>400 enter another number</c>, что
/// в SDK видно как <see cref="ExolveApiException.IsNonMtsNumber"/> — ожидаемый исход, а не сбой.
/// </para>
/// </remarks>
public sealed class ExolveHlrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string UserAgent = BuildUserAgent();

    private readonly HttpClient httpClient;
    private readonly ExolveClientOptions options;

    /// <summary>Создаёт клиент HLR.</summary>
    /// <param name="httpClient">HTTP-клиент; может управляться <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Настройки.</param>
    /// <exception cref="ArgumentNullException">Если аргумент равен null.</exception>
    /// <exception cref="ArgumentException">Если не задан <see cref="ExolveClientOptions.ApiKey"/>.</exception>
    public ExolveHlrClient(HttpClient httpClient, ExolveClientOptions options)
    {
        if (httpClient is null) throw new ArgumentNullException(nameof(httpClient));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("API key must be configured.", nameof(options));
        }

        this.httpClient = httpClient;
        this.options = options;
    }

    /// <summary>Оценка активности номера в сети (0…1). Работает по номерам любых операторов РФ.</summary>
    /// <param name="number">Номер; принимается в любом виде, нормализуется до <c>7XXXXXXXXXX</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Оценка активности, оператор и регион.</returns>
    /// <exception cref="ExolveValidationException">Если номер пуст или не приводится к российскому.</exception>
    /// <exception cref="ExolveApiException">Если Exolve ответил статусом вне 2xx.</exception>
    /// <exception cref="ExolveTransportException">Если ответ не получен.</exception>
    /// <exception cref="ExolveProtocolException">Если ответ не удалось разобрать.</exception>
    public Task<ActivityScoreResult> GetActivityScoreAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<ActivityScoreResult>("GetActivityScore", number, cancellationToken);

    /// <summary>Зарегистрирована ли SIM в сети за последние 24 часа. <b>Только номера МТС.</b></summary>
    /// <param name="number">Номер МТС.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Признак регистрации.</returns>
    /// <exception cref="ExolveApiException">
    /// В том числе <c>400 enter another number</c> для не-МТС — см. <see cref="ExolveApiException.IsNonMtsNumber"/>.
    /// </exception>
    public Task<SimStatusResult> GetSimStatusAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<SimStatusResult>("GetSimStatus", number, cancellationToken);

    /// <summary>Оператор, регион и признак переноса номера (MNP).</summary>
    /// <param name="number">Номер.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения о сети и регионе.</returns>
    public Task<BaseNumberInfoResult> GetBaseNumberInfoAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<BaseNumberInfoResult>("GetBaseNumberInfo", number, cancellationToken);

    /// <summary>Сколько дней прошло с последней активности номера в сети.</summary>
    /// <param name="number">Номер.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число дней с последней активности.</returns>
    public Task<LastActivityResult> GetLastDateActivityAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<LastActivityResult>("GetLastDateActivity", number, cancellationToken);

    /// <summary>Удачное время для звонка (UTC+0). <b>Только номера МТС.</b></summary>
    /// <param name="number">Номер МТС.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Интервал времени.</returns>
    public Task<BestTimeResult> GetBestCallTimeAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<BestTimeResult>("GetBestCallTime", number, cancellationToken);

    /// <summary>Удачное время для SMS (UTC+0). <b>Только номера МТС.</b></summary>
    /// <param name="number">Номер МТС.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Интервал времени.</returns>
    public Task<BestTimeResult> GetBestSmsTimeAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<BestTimeResult>("GetBestSmsTime", number, cancellationToken);

    // ── пакетные отчёты ──────────────────────────────────────────────────────

    /// <summary>Ставит в очередь отчёт по активности для списка номеров (до 50 000).</summary>
    /// <param name="numbers">Номера; нормализуются и кодируются в base64 по правилам Exolve.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор отчёта для <see cref="GetReportAsync"/>.</returns>
    /// <exception cref="ExolveValidationException">Если список пуст, велик или содержит нерабочий номер.</exception>
    public Task<HlrReportHandle> GenerateActivityScoreReportAsync(
        IEnumerable<string> numbers, CancellationToken cancellationToken = default) =>
        PostAsync<HlrReportHandle>(
            "GenerateActivityScoreReport",
            new NumbersRequest(ExolveNumbers.EncodeNumberList(numbers)),
            cancellationToken);

    /// <summary>Ставит в очередь отчёт по удачному времени звонка для списка номеров (до 50 000).</summary>
    /// <param name="numbers">Номера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор отчёта для <see cref="GetReportAsync"/>.</returns>
    public Task<HlrReportHandle> GenerateBestCallTimeReportAsync(
        IEnumerable<string> numbers, CancellationToken cancellationToken = default) =>
        PostAsync<HlrReportHandle>(
            "GenerateBestCallTimeReport",
            new NumbersRequest(ExolveNumbers.EncodeNumberList(numbers)),
            cancellationToken);

    /// <summary>
    /// Ставит в очередь отчёт из уже подготовленного base64.
    /// </summary>
    /// <remarks>
    /// Формат файла Exolve не документирует. <see cref="ExolveNumbers.EncodeNumberList"/>
    /// разделяет номера переводом строки; если выяснится, что нужен другой формат, кодируйте
    /// сами и передавайте сюда.
    /// </remarks>
    /// <param name="type">Какой отчёт считать.</param>
    /// <param name="numbersBase64">Готовый base64 списка номеров.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор отчёта.</returns>
    /// <exception cref="ExolveValidationException">Если base64 пуст или <paramref name="type"/> неизвестен.</exception>
    public Task<HlrReportHandle> GenerateReportAsync(
        HlrReportType type, string numbersBase64, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numbersBase64))
        {
            throw new ExolveValidationException("numbersBase64 is required.");
        }

        var method = type switch
        {
            HlrReportType.ActivityScore => "GenerateActivityScoreReport",
            HlrReportType.BestCallTime => "GenerateBestCallTimeReport",
            _ => throw new ExolveValidationException($"No generator endpoint for report type {type}.")
        };

        return PostAsync<HlrReportHandle>(method, new NumbersRequest(numbersBase64), cancellationToken);
    }

    /// <summary>Забирает пакетный отчёт по его идентификатору.</summary>
    /// <remarks>
    /// Отчёт считается асинхронно: пока он не готов, приходит статус
    /// <see cref="HlrReportStatus.Pending"/> или <see cref="HlrReportStatus.Processing"/> — это
    /// нормальный ответ, а не ошибка. Готовность проверяется через
    /// <see cref="HlrReport.IsComplete"/>.
    /// </remarks>
    /// <param name="fileUuid">Идентификатор, полученный при постановке отчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт и его состояние.</returns>
    /// <exception cref="ExolveValidationException">Если идентификатор пуст.</exception>
    public Task<HlrReport> GetReportAsync(string fileUuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUuid))
        {
            throw new ExolveValidationException("fileUuid is required.");
        }

        return PostAsync<HlrReport>("GetHLRReport", new FileUuidRequest(fileUuid), cancellationToken);
    }

    /// <summary>Перечисляет пакетные отчёты за период.</summary>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    /// <param name="limit">Сколько строк вернуть; null — на усмотрение API.</param>
    /// <param name="offset">С какой строки начинать, считая с нуля.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список отчётов.</returns>
    /// <exception cref="ExolveValidationException">Если <paramref name="from"/> позже <paramref name="to"/>.</exception>
    public Task<HlrReportList> ListReportsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        ulong? limit = null,
        ulong? offset = null,
        CancellationToken cancellationToken = default)
    {
        // Exolve отвечает на это «date_from later than date_to» — проверить дешевле у себя.
        if (from > to)
        {
            throw new ExolveValidationException("from must not be later than to.");
        }

        return PostAsync<HlrReportList>(
            "GetHLRListReport", new ReportListRequest(from, to, limit, offset), cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        string method, string number, CancellationToken cancellationToken)
        where TResponse : ExolveResponse
    {
        var normalized = ExolveNumbers.Normalize(number);
        return await PostAsync<TResponse>(method, new NumberRequest(normalized), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TResponse> PostAsync<TResponse>(
        string method, object requestBody, CancellationToken cancellationToken)
        where TResponse : ExolveResponse
    {
        var path = $"hlr/{options.HlrApiVersion}/{method}";
        var endpoint = new Uri(options.ResolveBaseAddress(), path);
        HttpResponseMessage response;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(requestBody, requestBody.GetType(), options: JsonOptions);

            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ExolveTransportException(
                $"Exolve POST {path} request failed before a response was received.", exception);
        }

        using (response)
        {
#if NETSTANDARD2_0
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (!response.IsSuccessStatusCode)
            {
                throw CreateApiException(path, response, body);
            }

            var result = Deserialize<TResponse>(path, response, body);
            result.Metadata = CreateMetadata(response, body, options.CaptureRawResponseBody);
            return result;
        }
    }

    private static ExolveApiException CreateApiException(string path, HttpResponseMessage response, string body)
    {
        ExolveError? error = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                error = JsonSerializer.Deserialize<ExolveError>(body, JsonOptions);
            }
            catch (JsonException)
            {
                // Тело не по схеме ошибки (404 отдаёт текст) — оставляем error = null.
            }
        }

        var detail = error?.Text is { Length: > 0 } text ? $" {text}." : ".";
        var status = (int)response.StatusCode;

        return new ExolveApiException(
            $"Exolve POST {path} returned HTTP {status} ({response.StatusCode}).{detail}",
            response.StatusCode,
            error,
            body);
    }

    private static TResponse Deserialize<TResponse>(string path, HttpResponseMessage response, string body)
        where TResponse : ExolveResponse
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ExolveProtocolException(
                $"Exolve POST {path} response body was empty. HTTP {(int)response.StatusCode}.",
                response.StatusCode);
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(body, JsonOptions)
                ?? throw new ExolveProtocolException(
                    $"Exolve POST {path} response body deserialized to null.",
                    response.StatusCode,
                    Preview(body));
        }
        catch (JsonException exception)
        {
            var preview = Preview(body);
            throw new ExolveProtocolException(
                $"Exolve POST {path} response body was not valid JSON for the expected model. Preview: {preview}",
                response.StatusCode,
                preview,
                exception);
        }
    }

    private static ExolveResponseMetadata CreateMetadata(
        HttpResponseMessage response, string body, bool captureRawBody)
    {
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(static h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => g.SelectMany(static h => h.Value).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new ExolveResponseMetadata(
            response.StatusCode,
            new ReadOnlyDictionary<string, string[]>(headers),
            captureRawBody ? body : null);
    }

    /// <summary>
    /// Готовит фрагмент тела для текста исключения, скрыв номер.
    /// </summary>
    /// <remarks>
    /// Номер — персональные данные, а текст исключения почти наверняка попадёт в лог.
    /// </remarks>
    private static string Preview(string body)
    {
        var masked = Regex.Replace(
            body,
            "(\"number\"\\s*:\\s*\"?)(\\d{1,4})\\d*(\\d{2})(\"?)",
            "$1$2***$3$4",
            RegexOptions.CultureInvariant);

        const int max = 512;
        return masked.Length <= max ? masked : masked.Substring(0, max);
    }

    private static string BuildUserAgent()
    {
        var version = typeof(ExolveHlrClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ExolveHlrClient).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = version.IndexOf('+');
        if (plus > 0)
        {
            version = version.Substring(0, plus);
        }

        return $"ExolveNet/{version}";
    }

    private sealed record NumberRequest([property: JsonPropertyName("number")] string Number);

    private sealed record NumbersRequest([property: JsonPropertyName("numbers")] string Numbers);

    private sealed record FileUuidRequest([property: JsonPropertyName("file_uuid")] string FileUuid);

    private sealed record ReportListRequest(
        [property: JsonPropertyName("date_from")] DateTimeOffset DateFrom,
        [property: JsonPropertyName("date_to")] DateTimeOffset DateTo,
        [property: JsonPropertyName("limit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ulong? Limit,
        [property: JsonPropertyName("offset"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ulong? Offset);
}
