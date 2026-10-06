using System.Text.Json.Serialization;

namespace ExolveNet;

/// <summary>Базовый ответ Exolve: полезная нагрузка плюс метаданные HTTP-обмена.</summary>
public abstract record ExolveResponse
{
    /// <summary>Номер, по которому отвечал API.</summary>
    /// <remarks>
    /// В ответах Exolve это <c>uint64</c> без ведущей <c>+</c> — например <c>79139999999</c>.
    /// </remarks>
    [JsonPropertyName("number")]
    public ulong Number { get; init; }

    /// <summary>Метаданные ответа: код, заголовки, при желании сырое тело.</summary>
    [JsonIgnore]
    public ExolveResponseMetadata? Metadata { get; internal set; }
}
