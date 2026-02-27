using System;

namespace QuadroApp.Security
{
    public static class AppSecurity
    {
        public static string? GetAppPassword()
        {
            return Environment.GetEnvironmentVariable("QUADRO_APP_PASSWORD");
        }

        public static bool Validate(string input)
        {
            var stored = GetAppPassword();

            if (string.IsNullOrWhiteSpace(stored))
                throw new InvalidOperationException("Environment variable QUADRO_APP_PASSWORD is not set.");

            return input == stored;
        }
    }
}
