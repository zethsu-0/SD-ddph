using ddphkiosk;
using System.Net.Http;

var firstDay = new DateTime(2026, 4, 29);
var nextDay = firstDay.AddDays(1);

var oneHundredFirst = KioskDailyOrderNumber.FromCurrentCounter(firstDay, 100);
AssertEqual(101, oneHundredFirst.Number, "daily order number should keep counting past 100");
AssertEqual("101", oneHundredFirst.DisplayNumber, "display should stay short and readable");

var firstOrderToday = KioskDailyOrderNumber.FromCurrentCounter(firstDay, null);
var firstOrderTomorrow = KioskDailyOrderNumber.FromCurrentCounter(nextDay, null);

AssertEqual(1, firstOrderToday.Number, "first order today should start at 1");
AssertEqual(1, firstOrderTomorrow.Number, "first order tomorrow should reset to 1");
AssertNotEqual(firstOrderToday.DateKey, firstOrderTomorrow.DateKey, "different days should use different counters");

using var response = new HttpResponseMessage();
response.Headers.TryAddWithoutValidation("ETag", "null_etag");
AssertEqual("null_etag", FirebaseEtag.GetHeaderValue(response), "Firebase raw etag should be accepted");

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
    }
}

static void AssertNotEqual<T>(T left, T right, string message)
{
    if (EqualityComparer<T>.Default.Equals(left, right))
    {
        throw new InvalidOperationException($"{message}. Both were {left}.");
    }
}
