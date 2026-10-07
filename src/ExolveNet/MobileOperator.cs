namespace ExolveNet;

/// <summary>Оператор, обслуживающий номер.</summary>
public enum MobileOperator
{
    /// <summary>Определить не удалось — ни кода сети, ни узнаваемого названия.</summary>
    Unknown = 0,

    /// <summary>МТС.</summary>
    Mts = 1,

    /// <summary>МегаФон.</summary>
    Megafon = 2,

    /// <summary>Билайн.</summary>
    Beeline = 3,

    /// <summary>Tele2.</summary>
    Tele2 = 4,

    /// <summary>Yota — MVNO на сети МегаФона.</summary>
    Yota = 5,

    /// <summary>Т-Мобайл (Тинькофф Мобайл) — MVNO на сети МТС.</summary>
    TinkoffMobile = 6,

    /// <summary>Оператор определён, но это не один из четырёх федеральных.</summary>
    Other = 99
}

/// <summary>
/// Определяет оператора по коду сети или по названию из ответа.
/// </summary>
/// <remarks>
/// Код сети надёжнее названия: <c>owner_id</c> — свободная строка, которую поставщик может
/// написать как угодно, а MNC стандартизован.
/// <para>
/// Таблица намеренно скупая — четыре федеральных оператора плюс два MVNO, чьи MNC проверены на
/// живом API. Для остальных возвращается
/// <see cref="MobileOperator.Other"/>: врать точным названием там, где нет уверенности, хуже, чем
/// честно сказать «не из известных».
/// <para>
/// MVNO важны отдельно: Т-Мобайл работает на сети МТС, но Exolve всё равно считает такой номер
/// не-мтс-овским — <c>GetSimStatus</c> отвечает на него <c>enter another number</c>. То есть
/// проверка «это МТС» у Exolve идёт по оператору записи, а не по сети, на которой номер работает.
/// </para>
/// </para>
/// </remarks>
public static class ExolveOperators
{
    /// <summary>Код страны (MCC) для России.</summary>
    public const string RussiaMcc = "250";

    /// <summary>
    /// Определяет оператора.
    /// </summary>
    /// <param name="mnc">Код оператора из <c>network_code</c>, две цифры.</param>
    /// <param name="ownerId">Название из <c>owner_id</c> — запасной вариант, если MNC нет.</param>
    /// <returns>Оператор, либо <see cref="MobileOperator.Unknown"/>, если определить не по чему.</returns>
    public static MobileOperator Resolve(string? mnc, string? ownerId = null)
    {
        switch (mnc)
        {
            case "01": return MobileOperator.Mts;
            case "02": return MobileOperator.Megafon;
            case "99": return MobileOperator.Beeline;
            case "20": return MobileOperator.Tele2;
            // Проверено на живом API.
            case "11": return MobileOperator.Yota;
            case "62": return MobileOperator.TinkoffMobile;
        }

        if (!string.IsNullOrWhiteSpace(mnc))
        {
            // Код есть и он валиден, просто не из четвёрки — это Other, а не «неизвестно».
            return MobileOperator.Other;
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return MobileOperator.Unknown;
        }

        var name = ownerId!;
        if (Has(name, "mts") || Has(name, "мтс")) return MobileOperator.Mts;
        if (Has(name, "megafon") || Has(name, "мегафон")) return MobileOperator.Megafon;
        if (Has(name, "beeline") || Has(name, "билайн")) return MobileOperator.Beeline;
        if (Has(name, "tele2") || Has(name, "теле2")) return MobileOperator.Tele2;
        if (Has(name, "yota") || Has(name, "йота")) return MobileOperator.Yota;
        if (Has(name, "tinkoff") || Has(name, "т-мобайл") || Has(name, "тинькофф")) return MobileOperator.TinkoffMobile;

        return MobileOperator.Other;
    }

    private static bool Has(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}
