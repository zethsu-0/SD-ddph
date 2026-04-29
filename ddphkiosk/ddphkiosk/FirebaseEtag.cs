using System.Net.Http;

namespace ddphkiosk;

internal static class FirebaseEtag
{
    public static string? GetHeaderValue(HttpResponseMessage response)
    {
        var typedEtag = response.Headers.ETag?.Tag;
        if (!string.IsNullOrWhiteSpace(typedEtag))
        {
            return typedEtag;
        }

        return response.Headers.TryGetValues("ETag", out var values)
            ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            : null;
    }
}
