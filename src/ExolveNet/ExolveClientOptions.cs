namespace ExolveNet;

/// <summary>
/// Настройки клиента МТС Exolve.
/// </summary>
public sealed class ExolveClientOptions
{
    /// <summary>API-ключ приложения. Уходит в заголовок <c>Authorization: Bearer …</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Базовый адрес API. По умолчанию <c>https://api.exolve.ru/</c>.
    /// </summary>
    /// <remarks>
    /// Переопределяется для тестов и прокси. Путь метода (например <c>hlr/v1/GetSimStatus</c>)
    /// дописывается к нему, поэтому адрес обязан заканчиваться слэшем — <see cref="ResolveBaseAddress"/>
    /// добавит его, если забыли.
    /// </remarks>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Версия HLR API: <c>v1</c> (по умолчанию) или <c>v2</c>.
    /// </summary>
    /// <remarks>
    /// У <c>GetActivityScore</c> документированы обе версии; у остальных методов — только
    /// <c>v1</c>. Значение подставляется в путь, поэтому <c>v2</c> на методе, которого в
    /// <c>v2</c> нет, вернёт 404 от Exolve, а не ошибку клиента.
    /// </remarks>
    public string HlrApiVersion { get; set; } = "v1";

    /// <summary>
    /// Сохранять ли сырое тело ответа в <see cref="ExolveResponseMetadata.RawBody"/>.
    /// </summary>
    /// <remarks>
    /// По умолчанию выключено: тело содержит номер телефона, то есть персональные данные, и в
    /// логах ему по умолчанию делать нечего.
    /// </remarks>
    public bool CaptureRawResponseBody { get; set; }

    internal Uri ResolveBaseAddress()
    {
        if (BaseAddress is null)
        {
            return new Uri("https://api.exolve.ru/");
        }

        return BaseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? BaseAddress
            : new Uri(BaseAddress.AbsoluteUri + "/");
    }
}
