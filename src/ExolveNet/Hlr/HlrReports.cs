using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExolveNet.Hlr;

/// <summary>Что считалось в пакетном отчёте.</summary>
public enum HlrReportType
{
    /// <summary>Значение вне известных — Exolve прислал что-то новое.</summary>
    Unknown = 0,

    /// <summary>Оценка активности (<c>GenerateActivityScoreReport</c>).</summary>
    ActivityScore = 1,

    /// <summary>Удачное время для звонка (<c>GenerateBestCallTimeReport</c>).</summary>
    BestCallTime = 2
}

/// <summary>Состояние пакетного отчёта.</summary>
public enum HlrReportStatus
{
    /// <summary>Значение вне известных — Exolve прислал что-то новое.</summary>
    Unknown = 0,

    /// <summary>Ожидает проверки.</summary>
    Pending = 1,

    /// <summary>Проверка идёт.</summary>
    Processing = 2,

    /// <summary>Отчёт готов.</summary>
    Ready = 3,

    /// <summary>Отчёт готов, но часть номеров с ошибками.</summary>
    ReadyWithErrors = 4,

    /// <summary>Срок хранения истёк — данных больше нет.</summary>
    Expired = 5
}

/// <summary>
/// Читает значение, которое Exolve присылает то числом, то строкой.
/// </summary>
/// <remarks>
/// У <c>GetHLRReport</c> поле <c>status</c> документировано как число, у <c>GetHLRListReport</c> —
/// как строка, при одних и тех же значениях 1…5. Разбирать надо оба варианта, иначе один из
/// методов падает на ровном месте.
/// </remarks>
internal sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int? value = reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt32(out var n) ? n : null,
            JsonTokenType.String => int.TryParse(reader.GetString(), out var n) ? n : null,
            _ => null
        };

        // Неизвестное значение — это Unknown, а не исключение: новый статус у поставщика не повод
        // ронять разбор целого отчёта.
        return value is { } v && Enum.IsDefined(typeof(TEnum), v)
            ? (TEnum)Enum.ToObject(typeof(TEnum), v)
            : default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(Convert.ToInt32(value));
}

/// <summary>
/// Идентификатор поставленного в очередь пакетного отчёта.
/// </summary>
/// <remarks>
/// Отчёт считается асинхронно: здесь только <see cref="FileUuid"/>, с которым потом идут
/// в <c>GetHLRReport</c>.
/// </remarks>
public sealed record HlrReportHandle : ExolveResponse
{
    /// <summary>Идентификатор отчёта.</summary>
    [JsonPropertyName("file_uuid")]
    public string? FileUuid { get; init; }
}

/// <summary>Пакетный отчёт и его состояние.</summary>
public sealed record HlrReport : ExolveResponse
{
    /// <summary>Идентификатор отчёта.</summary>
    [JsonPropertyName("file_uuid")]
    public string? FileUuid { get; init; }

    /// <summary>
    /// Срок, до которого отчёт хранится — 30 дней с момента создания.
    /// </summary>
    /// <remarks>
    /// Поле называется <c>created_at</c>, но по документации это <b>дедлайн хранения</b>, а не
    /// время создания. Название обманчивое, поэтому в SDK оно переименовано; после этой даты
    /// статус становится <see cref="HlrReportStatus.Expired"/>, а данные пропадают.
    /// </remarks>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? RetainedUntil { get; init; }

    /// <summary>Что считалось.</summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(TolerantEnumConverter<HlrReportType>))]
    public HlrReportType Type { get; init; }

    /// <summary>Состояние отчёта.</summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(TolerantEnumConverter<HlrReportStatus>))]
    public HlrReportStatus Status { get; init; }

    /// <summary>Сколько номеров в отчёте.</summary>
    [JsonPropertyName("number_total")]
    public int? NumberTotal { get; init; }

    /// <summary>Результаты проверки в base64, как их отдаёт API.</summary>
    /// <remarks>Декодировать удобнее через <see cref="DecodeResults"/>.</remarks>
    [JsonPropertyName("base64")]
    public string? Base64 { get; init; }

    /// <summary>Отчёт досчитан — забирать результаты можно.</summary>
    public bool IsComplete => Status is HlrReportStatus.Ready or HlrReportStatus.ReadyWithErrors;

    /// <summary>
    /// Декодирует <see cref="Base64"/> в текст.
    /// </summary>
    /// <returns>Содержимое отчёта, или null, если его нет или оно не декодируется.</returns>
    /// <remarks>
    /// Exolve не описывает внутренний формат, поэтому SDK отдаёт текст как есть и не притворяется,
    /// что знает его структуру.
    /// </remarks>
    public string? DecodeResults()
    {
        if (string.IsNullOrWhiteSpace(Base64))
        {
            return null;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Base64!));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>Список пакетных отчётов за период.</summary>
public sealed record HlrReportList : ExolveResponse
{
    /// <summary>Отчёты.</summary>
    [JsonPropertyName("reports")]
    public IReadOnlyList<HlrReport> Reports { get; init; } = Array.Empty<HlrReport>();
}
