using System.Collections.ObjectModel;
using System.Net;

namespace ExolveNet;

/// <summary>
/// Что пришло вместе с ответом: статус, заголовки и — если включено — сырое тело.
/// </summary>
/// <param name="HttpStatusCode">Код ответа.</param>
/// <param name="Headers">Заголовки ответа и его содержимого, без учёта регистра.</param>
/// <param name="RawBody">
/// Сырое тело, если <see cref="ExolveClientOptions.CaptureRawResponseBody"/> включено; иначе null.
/// Содержит номер телефона — обращаться как с персональными данными.
/// </param>
public sealed record ExolveResponseMetadata(
    HttpStatusCode HttpStatusCode,
    ReadOnlyDictionary<string, string[]> Headers,
    string? RawBody);
