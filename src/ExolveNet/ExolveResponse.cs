using System.Text.Json.Serialization;

namespace ExolveNet;

/// <summary>Базовый ответ Exolve: метаданные HTTP-обмена рядом с полезной нагрузкой.</summary>
public abstract record ExolveResponse
{
    /// <summary>Метаданные ответа: код, заголовки, при желании сырое тело.</summary>
    [JsonIgnore]
    public ExolveResponseMetadata? Metadata { get; internal set; }
}

/// <summary>Ответ по одному номеру.</summary>
public abstract record ExolveNumberResponse : ExolveResponse
{
    /// <summary>Номер, по которому отвечал API.</summary>
    /// <remarks>
    /// В ответах Exolve это <c>uint64</c> без ведущей <c>+</c> — например <c>79139999999</c>.
    /// Привести к привычному виду помогает <see cref="ExolveNumbers.ToE164"/>.
    /// </remarks>
    [JsonPropertyName("number")]
    public ulong Number { get; init; }
}
