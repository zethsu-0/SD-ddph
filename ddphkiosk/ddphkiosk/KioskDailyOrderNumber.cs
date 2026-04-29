using System.Globalization;

namespace ddphkiosk;

public readonly record struct KioskDailyOrderNumber(string DateKey, int Number)
{
    public string DisplayNumber => Number.ToString("D3", CultureInfo.InvariantCulture);

    public static KioskDailyOrderNumber FromCurrentCounter(DateTime localDate, int? currentCounter)
    {
        return new KioskDailyOrderNumber(
            GetDateKey(localDate),
            Math.Max(0, currentCounter ?? 0) + 1);
    }

    public static string GetDateKey(DateTime localDate)
    {
        return localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
