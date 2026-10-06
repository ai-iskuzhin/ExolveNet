using System.Globalization;

namespace ExolveNet;

/// <summary>
/// Приводит номер к формату, который принимает Exolve, и отвергает всё остальное до запроса.
/// </summary>
/// <remarks>
/// Exolve ждёт <c>7XXXXXXXXXX</c> — одиннадцать цифр, начиная с семёрки, без <c>+</c>.
/// Нормализация здесь нужна потому, что вызывающий код обычно хранит номера в E.164
/// (<c>+7…</c>), а запрос с плюсом Exolve отклоняет как <c>incorrect number format</c> — то есть
/// за него уже списаны деньги, а ответа нет.
/// </remarks>
public static class ExolveNumbers
{
    /// <summary>
    /// Нормализует номер к <c>7XXXXXXXXXX</c>.
    /// </summary>
    /// <param name="number">Номер в любом из привычных видов: <c>+7…</c>, <c>8…</c>, с пробелами и дефисами.</param>
    /// <returns>Одиннадцать цифр, начиная с <c>7</c>.</returns>
    /// <exception cref="ExolveValidationException">Если номер пуст или не приводится к российскому мобильному.</exception>
    public static string Normalize(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ExolveValidationException("number is required.");
        }

        var digits = new char[number!.Length];
        var n = 0;
        foreach (var c in number)
        {
            if (c >= '0' && c <= '9')
            {
                digits[n++] = c;
            }
        }

        var raw = new string(digits, 0, n);

        // Российские номера набирают и через 8, и через +7 — у Exolve формат один.
        if (raw.Length == 11 && raw[0] == '8')
        {
            raw = "7" + raw.Substring(1);
        }

        if (raw.Length != 11 || raw[0] != '7')
        {
            throw new ExolveValidationException(
                $"number must be a Russian number in 7XXXXXXXXXX form; got {raw.Length} digit(s).");
        }

        return raw;
    }

    /// <summary>Возвращает номер из ответа в виде E.164 (<c>+7…</c>).</summary>
    /// <param name="number">Номер, как его вернул API.</param>
    public static string ToE164(ulong number) =>
        "+" + number.ToString(CultureInfo.InvariantCulture);
}
