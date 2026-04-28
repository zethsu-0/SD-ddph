using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ddph.Data
{
    public class FirebaseDatabaseClient
    {
        private const string DatabaseUrl = "https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app";

        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri($"{DatabaseUrl.TrimEnd('/')}/")
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public async Task<T?> GetAsync<T>(string path)
        {
            using var response = await HttpClient.GetAsync(ToFirebasePath(path)).ConfigureAwait(false);
            await EnsureSuccess(response, path).ConfigureAwait(false);

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        public async Task<T?> PostAsync<T>(string path, object payload)
        {
            using var response = await HttpClient.PostAsync(ToFirebasePath(path), CreateJsonContent(payload)).ConfigureAwait(false);
            await EnsureSuccess(response, path).ConfigureAwait(false);

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        public async Task PutAsync(string path, object payload)
        {
            using var response = await HttpClient.PutAsync(ToFirebasePath(path), CreateJsonContent(payload)).ConfigureAwait(false);
            await EnsureSuccess(response, path).ConfigureAwait(false);
        }

        public async Task PatchAsync(string path, object payload)
        {
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), ToFirebasePath(path))
            {
                Content = CreateJsonContent(payload)
            };

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            await EnsureSuccess(response, path).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string path)
        {
            using var response = await HttpClient.DeleteAsync(ToFirebasePath(path)).ConfigureAwait(false);
            await EnsureSuccess(response, path).ConfigureAwait(false);
        }

        private static StringContent CreateJsonContent(object payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static string ToFirebasePath(string path)
        {
            return $"{path.TrimStart('/').TrimEnd('/')}.json";
        }

        private static async Task EnsureSuccess(HttpResponseMessage response, string path)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Firebase request failed for '{path}' with status {(int)response.StatusCode}: {body}");
        }
    }
}
