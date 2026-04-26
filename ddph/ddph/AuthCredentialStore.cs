using System;
using System.Collections.Generic;
using System.IO;

namespace ddph
{
    public static class AuthCredentialStore
    {
        private const string CredentialsFileName = "credentials.ini";
        private const string DefaultUsername = "admin";
        private const string DefaultPassword = "admin";

        public static (string Username, string Password) GetAdminCredentials()
        {
            Dictionary<string, string> values;
            try
            {
                values = ReadIniValues();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Unable to read {CredentialsFileName}. Check file permissions.", ex);
            }

            return (
                values.TryGetValue("username", out var username) && !string.IsNullOrWhiteSpace(username) ? username.Trim() : DefaultUsername,
                values.TryGetValue("password", out var password) && !string.IsNullOrWhiteSpace(password) ? password : DefaultPassword);
        }

        private static Dictionary<string, string> ReadIniValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = Path.Combine(AppContext.BaseDirectory, CredentialsFileName);
            if (!File.Exists(path))
            {
                return values;
            }

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("[", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                values[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
            }

            return values;
        }
    }
}
