using System.Net;

namespace ExolveNet;

/// <summary>Базовое исключение SDK.</summary>
public abstract class ExolveException : Exception
{
    /// <summary>Создаёт исключение.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Вложенное исключение, если есть.</param>
    protected ExolveException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Запрос не прошёл локальную проверку и в сеть не уходил.</summary>
public sealed class ExolveValidationException : ExolveException
{
    /// <summary>Создаёт исключение валидации.</summary>
    /// <param name="message">Что именно не так.</param>
    public ExolveValidationException(string message) : base(message)
    {
    }
}

/// <summary>Ответ не получен — сеть, таймаут, TLS.</summary>
public sealed class ExolveTransportException : ExolveException
{
    /// <summary>Создаёт транспортное исключение.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Исходная ошибка транспорта.</param>
    public ExolveTransportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exolve ответил статусом вне диапазона 2xx.
/// </summary>
/// <remarks>
/// Отдельный случай — <c>400</c> с текстом <c>enter another number</c>: номер не обслуживается
/// МТС, а часть методов HLR работает только по номерам МТС. Для этого есть
/// <see cref="IsNonMtsNumber"/>, чтобы вызывающему коду не приходилось сравнивать строки.
/// </remarks>
public sealed class ExolveApiException : ExolveException
{
    /// <summary>Создаёт исключение по ответу API.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">Код ответа.</param>
    /// <param name="error">Разобранное тело ошибки, если удалось.</param>
    /// <param name="rawBody">Тело ответа как пришло.</param>
    public ExolveApiException(
        string message, HttpStatusCode statusCode, ExolveError? error, string? rawBody = null)
        : base(message)
    {
        HttpStatusCode = statusCode;
        Error = error;
        RawBody = rawBody;
    }

    /// <summary>Код ответа.</summary>
    public HttpStatusCode HttpStatusCode { get; }

    /// <summary>Тело ошибки, если его удалось разобрать.</summary>
    public ExolveError? Error { get; }

    /// <summary>
    /// Тело ответа как пришло — чтобы форма ошибки, которую модель не знает, оставалась видимой.
    /// </summary>
    /// <remarks>
    /// Именно отсутствие этого поля скрыло реальную форму ошибки: <see cref="Error"/> молча
    /// оказывался пустым, и по признакам вроде <see cref="IsHlrDisabled"/> нельзя было понять, что
    /// они просто не сработали. Номера телефона в теле ошибки нет, поэтому сохраняется целиком.
    /// </remarks>
    public string? RawBody { get; }

    /// <summary>
    /// Номер не принадлежит МТС, а метод работает только по номерам МТС
    /// (<c>400 enter another number</c>).
    /// </summary>
    /// <remarks>
    /// Это ожидаемый исход, а не поломка: по документации <c>GetSimStatus</c>,
    /// <c>GetBestCallTime</c> и <c>GetBestSmsTime</c> данные отдают только по МТС. Вызывающий код
    /// обычно обрабатывает это как «нет данных», а не как ошибку.
    /// </remarks>
    public bool IsNonMtsNumber =>
        HttpStatusCode == HttpStatusCode.BadRequest
        && Error?.Text?.IndexOf("enter another number", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Услуга HLR не включена для приложения (<c>400 hlr is disabled on the application</c>).
    /// </summary>
    public bool IsHlrDisabled =>
        HttpStatusCode == HttpStatusCode.BadRequest
        && Error?.Text?.IndexOf("hlr is disabled", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Тестовый период: проверять можно только свой подтверждённый номер
    /// (<c>400 customer has not signed…</c>).
    /// </summary>
    /// <remarks>
    /// Легко спутать с ограничением по оператору — отсюда отдельный признак.
    /// </remarks>
    public bool IsUnsignedCustomer =>
        HttpStatusCode == HttpStatusCode.BadRequest
        && Error?.Text?.IndexOf("customer has not signed", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// По номеру нет данных (<c>404 no information on this number</c>).
    /// </summary>
    /// <remarks>
    /// <b>Это ответ, а не поломка.</b> Оператор отдал регистр и в нём ничего не нашлось — услуга
    /// выполнена. В личном кабинете такие запросы считаются отдельно («не получен статус»), и у
    /// активности их доля заметная: номер без оценки активности при этом может иметь базовую
    /// справку, так что «нет данных» — это свойство услуги, а не номера.
    /// <para>
    /// Единственный известный случай, когда ответ приходит с кодом <c>404</c>, а не <c>400</c>:
    /// отличать его важно, потому что повторять такой запрос бессмысленно — ответ не изменится,
    /// а запрос будет выполнен снова.
    /// </para>
    /// </remarks>
    public bool IsNoInformation =>
        (HttpStatusCode == HttpStatusCode.NotFound || HttpStatusCode == HttpStatusCode.BadRequest)
        && Error?.Text?.IndexOf("no information on this number", StringComparison.OrdinalIgnoreCase) >= 0;
}

/// <summary>Ответ пришёл, но разобрать его не удалось.</summary>
public sealed class ExolveProtocolException : ExolveException
{
    /// <summary>Создаёт протокольное исключение.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">Код ответа.</param>
    /// <param name="bodyPreview">Фрагмент тела для диагностики (номер скрыт).</param>
    /// <param name="innerException">Исходная ошибка разбора.</param>
    public ExolveProtocolException(
        string message, HttpStatusCode statusCode, string? bodyPreview = null, Exception? innerException = null)
        : base(message, innerException)
    {
        HttpStatusCode = statusCode;
        BodyPreview = bodyPreview;
    }

    /// <summary>Код ответа.</summary>
    public HttpStatusCode HttpStatusCode { get; }

    /// <summary>Фрагмент тела ответа — с замаскированным номером.</summary>
    public string? BodyPreview { get; }
}
