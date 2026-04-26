using System;
using System.IO;

namespace ddph
{
    public static class AuthSessionStore
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DreamDoughPH",
            "remember-login");

        public static bool IsRemembered()
        {
            return File.Exists(SessionFilePath);
        }

        public static string CurrentUsername { get; private set; } = "admin";
        public static bool IsAdmin => string.Equals(CurrentUsername, "admin", StringComparison.OrdinalIgnoreCase);

        public static string GetRememberedUsername()
        {
            if (!File.Exists(SessionFilePath))
            {
                return CurrentUsername;
            }

            var username = File.ReadAllText(SessionFilePath).Trim();
            CurrentUsername = string.IsNullOrWhiteSpace(username) ? "admin" : username;
            return CurrentUsername;
        }

        public static void SignIn(string username)
        {
            CurrentUsername = string.IsNullOrWhiteSpace(username) ? "admin" : username.Trim();
        }

        public static void Remember(string username)
        {
            SignIn(username);
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFilePath)!);
            File.WriteAllText(SessionFilePath, CurrentUsername);
        }

        public static void Forget()
        {
            CurrentUsername = "admin";
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
            }
        }
    }
}
