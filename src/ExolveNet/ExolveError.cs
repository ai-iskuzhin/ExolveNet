using System.Text.Json.Serialization;

namespace ExolveNet;

/// <summary>Тело ошибки Exolve.</summary>
public sealed record ExolveError
{
    /// <summary>Текст ошибки — например <c>incorrect number format</c>.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Сообщение, если платформа положила его в <c>message</c>, а не в <c>error</c>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Код, если он пришёл.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>Текст ошибки из того поля, в котором он фактически пришёл.</summary>
    public string? Text => string.IsNullOrWhiteSpace(Error) ? Message : Error;
}
