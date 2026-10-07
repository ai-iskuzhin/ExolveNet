using System.Globalization;
using System.Text.Json.Serialization;

namespace ExolveNet.Hlr;

/// <summary>
/// Оценка активности номера в сети — <c>GetActivityScore</c>.
/// </summary>
/// <remarks>
/// Применение по документации: вычистить неактивные номера из базы перед рассылкой или обзвоном.
/// Владение номером это не доказывает, поэтому к подтверждению номера метод отношения не имеет.
/// </remarks>
public sealed record ActivityScoreResult : ExolveNumberResponse
{
    /// <summary>
    /// Оценка от <c>"0"</c> до <c>"1"</c> строкой, как её отдаёт API.
    /// </summary>
    /// <remarks>
    /// Хранится как пришло: приводить к числу — задача <see cref="Score"/>, и при неожидаемом
    /// формате лучше отдать сырое значение, чем молча подставить ноль.
    /// </remarks>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>Идентификатор оператора, обслуживающего номер.</summary>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; init; }

    /// <summary>Код региона, в котором куплена SIM.</summary>
    [JsonPropertyName("region_code")]
    public uint? RegionCode { get; init; }

    /// <summary>
    /// Оператор, определённый по <see cref="OwnerId"/>.
    /// </summary>
    /// <remarks>
    /// Кода сети этот метод не возвращает, поэтому определение идёт по названию — оно менее
    /// надёжно. Там, где оператор важен, лучше спрашивать <c>GetBaseNumberInfo</c>.
    /// </remarks>
    public MobileOperator Operator => ExolveOperators.Resolve(mnc: null, OwnerId);

    /// <summary>
    /// <see cref="Result"/> числом: 0 — минимальная активность, 1 — максимальная; null, если
    /// значение не разобралось.
    /// </summary>
    public double? Score =>
        double.TryParse(Result, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}

/// <summary>
/// Зарегистрирована ли SIM в сети — <c>GetSimStatus</c>.
/// </summary>
/// <remarks>
/// <b>Только номера МТС.</b> По чужому номеру Exolve отвечает <c>400 enter another number</c>,
/// что в SDK видно как <see cref="ExolveApiException.IsNonMtsNumber"/>.
/// </remarks>
public sealed record SimStatusResult : ExolveNumberResponse
{
    /// <summary>
    /// <see langword="true"/>, если SIM регистрировалась в сети за последние 24 часа.
    /// </summary>
    [JsonPropertyName("is_registered")]
    public bool IsRegistered { get; init; }
}

/// <summary>
/// Оператор, регион и признак переноса — <c>GetBaseNumberInfo</c>.
/// </summary>
/// <remarks>
/// Единственный способ узнать реального оператора: по префиксу этого сделать нельзя, потому что
/// номер мог быть перенесён (<see cref="IsPorted"/>) — тогда «мтс-овский» префикс принадлежит
/// другому оператору и наоборот.
/// </remarks>
public sealed record BaseNumberInfoResult : ExolveNumberResponse
{
    /// <summary>Идентификатор оператора, обслуживающего номер.</summary>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; init; }

    /// <summary>Код региона, в котором куплена SIM.</summary>
    [JsonPropertyName("region_code")]
    public uint? RegionCode { get; init; }

    /// <summary>
    /// Код сети, как его отдаёт API, — например <c>RU25002</c>.
    /// </summary>
    /// <remarks>
    /// Документация описывает его как «три цифры кода страны (MCC) плюс две цифры кода оператора
    /// (MNC)», но на деле перед цифрами идут ещё буквы страны: <c>RU</c> + <c>250</c> + <c>02</c>.
    /// Разбирать по позициям, как написано в документации, нельзя — получится <c>RU2</c> и
    /// <c>50</c>. Готовые части — в <see cref="Country"/>, <see cref="Mcc"/> и <see cref="Mnc"/>.
    /// </remarks>
    [JsonPropertyName("network_code")]
    public string? NetworkCode { get; init; }

    /// <summary>Номер переносился между операторами (MNP).</summary>
    [JsonPropertyName("mnp")]
    public bool IsPorted { get; init; }

    /// <summary>Буквенный код страны из <see cref="NetworkCode"/> (<c>RU</c>), или null.</summary>
    public string? Country
    {
        get
        {
            if (string.IsNullOrEmpty(NetworkCode)) return null;
            var letters = new string(NetworkCode!.TakeWhile(char.IsLetter).ToArray());
            return letters.Length > 0 ? letters : null;
        }
    }

    /// <summary>Код страны MCC из <see cref="NetworkCode"/> (<c>250</c> для России), или null.</summary>
    public string? Mcc => Digits is { Length: >= 3 } d ? d.Substring(0, 3) : null;

    /// <summary>Код оператора MNC из <see cref="NetworkCode"/>, или null.</summary>
    public string? Mnc => Digits is { Length: >= 5 } d ? d.Substring(3, 2) : null;

    /// <summary>
    /// Только цифры из <see cref="NetworkCode"/> — чтобы разбор не зависел от того, есть ли перед
    /// ними буквы страны.
    /// </summary>
    private string? Digits =>
        NetworkCode is null ? null : new string(NetworkCode.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Оператор, определённый по <see cref="Mnc"/>, а при его отсутствии — по
    /// <see cref="OwnerId"/>.
    /// </summary>
    /// <remarks>
    /// Именно это значение отвечает на вопрос «пускать ли номер в метод, который работает только
    /// по МТС»: с учётом <see cref="IsPorted"/> по префиксу номера такой вывод сделать нельзя.
    /// </remarks>
    public MobileOperator Operator => ExolveOperators.Resolve(Mnc, OwnerId);
}

/// <summary>
/// Сколько дней прошло с последней активности в сети — <c>GetLastDateActivity</c>.
/// </summary>
public sealed record LastActivityResult : ExolveNumberResponse
{
    /// <summary>Число дней строкой, как его отдаёт API.</summary>
    [JsonPropertyName("days_since_last_activity")]
    public string? DaysSinceLastActivity { get; init; }

    /// <summary><see cref="DaysSinceLastActivity"/> числом, или null, если значение не разобралось.</summary>
    public int? Days =>
        int.TryParse(DaysSinceLastActivity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}

/// <summary>
/// Удачное время для контакта — <c>GetBestCallTime</c> и <c>GetBestSmsTime</c>.
/// </summary>
/// <remarks>
/// <b>Только номера МТС</b> (в документации — «пока»). Интервал приходит в UTC+0 строкой вида
/// <c>17:00:00,21:00:00</c>; разобранные границы — в <see cref="From"/> и <see cref="To"/>.
/// </remarks>
public sealed record BestTimeResult : ExolveNumberResponse
{
    /// <summary>Интервал строкой, как его отдаёт API, например <c>17:00:00,21:00:00</c> (UTC+0).</summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>Начало интервала в UTC, или null, если строка не разобралась.</summary>
    public TimeSpan? From => Part(0);

    /// <summary>Конец интервала в UTC, или null, если строка не разобралась.</summary>
    public TimeSpan? To => Part(1);

    private TimeSpan? Part(int index)
    {
        if (string.IsNullOrWhiteSpace(Result))
        {
            return null;
        }

        var parts = Result!.Split(',');
        return parts.Length == 2
            && TimeSpan.TryParse(parts[index].Trim(), CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
    }
}
