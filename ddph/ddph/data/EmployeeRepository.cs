using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ddph.Data
{
    public class EmployeeRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public async Task<List<EmployeeSummary>> GetEmployeesAsync()
        {
            var employees = await _firebaseClient
                .GetAsync<Dictionary<string, FirebaseEmployeeRecord>>("employees")
                .ConfigureAwait(false);

            if (employees == null)
            {
                return new List<EmployeeSummary>();
            }

            return employees
                .Where(entry => entry.Value != null)
                .Select(entry => new EmployeeSummary(
                    entry.Value!.DisplayName ?? string.Empty,
                    entry.Value.Username ?? entry.Key))
                .OrderBy(employee => employee.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task AddEmployeeAsync(string displayName, string username, string password)
        {
            var normalizedUsername = username.Trim();
            var key = ToEmployeeKey(normalizedUsername);
            var existingEmployee = await _firebaseClient
                .GetAsync<FirebaseEmployeeRecord>($"employees/{key}")
                .ConfigureAwait(false);

            if (existingEmployee != null)
            {
                throw new InvalidOperationException("Employee already exists.");
            }

            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var payload = new Dictionary<string, object?>
            {
                ["displayName"] = displayName.Trim(),
                ["username"] = normalizedUsername,
                ["password"] = password,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };

            await _firebaseClient.PutAsync($"employees/{key}", payload).ConfigureAwait(false);
        }

        public async Task UpdateEmployeeAsync(string originalUsername, string displayName, string username, string? password)
        {
            var originalKey = ToEmployeeKey(originalUsername);
            var employee = await _firebaseClient
                .GetAsync<FirebaseEmployeeRecord>($"employees/{originalKey}")
                .ConfigureAwait(false);

            if (employee == null)
            {
                throw new InvalidOperationException("Employee no longer exists.");
            }

            var normalizedUsername = username.Trim();
            var newKey = ToEmployeeKey(normalizedUsername);
            if (!string.Equals(originalKey, newKey, StringComparison.Ordinal))
            {
                var existingEmployee = await _firebaseClient
                    .GetAsync<FirebaseEmployeeRecord>($"employees/{newKey}")
                    .ConfigureAwait(false);

                if (existingEmployee != null)
                {
                    throw new InvalidOperationException("Employee already exists.");
                }
            }

            var payload = new Dictionary<string, object?>
            {
                ["displayName"] = displayName.Trim(),
                ["username"] = normalizedUsername,
                ["password"] = string.IsNullOrWhiteSpace(password) ? employee.Password : password,
                ["createdAt"] = employee.CreatedAt,
                ["updatedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            await _firebaseClient.PutAsync($"employees/{newKey}", payload).ConfigureAwait(false);
            if (!string.Equals(originalKey, newKey, StringComparison.Ordinal))
            {
                await _firebaseClient.DeleteAsync($"employees/{originalKey}").ConfigureAwait(false);
            }
        }

        public async Task DeleteEmployeeAsync(string username)
        {
            await _firebaseClient.DeleteAsync($"employees/{ToEmployeeKey(username)}").ConfigureAwait(false);
        }

        public async Task<bool> ValidateEmployeeAsync(string username, string password)
        {
            var key = ToEmployeeKey(username);
            var employee = await _firebaseClient
                .GetAsync<FirebaseEmployeeRecord>($"employees/{key}")
                .ConfigureAwait(false);

            return employee != null &&
                string.Equals(employee.Username, username.Trim(), StringComparison.OrdinalIgnoreCase) &&
                employee.Password == password;
        }

        private static string ToEmployeeKey(string username)
        {
            var chars = username
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();

            var key = new string(chars);
            while (key.Contains("--", StringComparison.Ordinal))
            {
                key = key.Replace("--", "-", StringComparison.Ordinal);
            }

            return key.Trim('-');
        }

        private sealed class FirebaseEmployeeRecord
        {
            public string? DisplayName { get; set; }
            public string? Username { get; set; }
            public string? Password { get; set; }
            public string? CreatedAt { get; set; }
        }

        public sealed record EmployeeSummary(string DisplayName, string Username);
    }
}
