using System;
using System.Text;
using System.Text.Json;

namespace AquaMap.Helpers
{
    /// <summary>
    /// Reads the role from the stored JWT to show or hide admin-only actions.
    /// UI only: the signature is not checked here, the API enforces the rule.
    /// </summary>
    public static class JwtRole
    {
        // JwtSecurityTokenHandler writes ClaimTypes.Role as the short name "role" (see the API's TokenService).
        private const string RoleClaim = "role";
        private const string AdministratorRole = "Administrator";

        public static bool IsAdministrator(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            try
            {
                using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
                if (!payload.RootElement.TryGetProperty(RoleClaim, out var role)) return false;

                return role.ValueKind switch
                {
                    JsonValueKind.String => role.GetString() == AdministratorRole,
                    JsonValueKind.Array => HasAdministrator(role),
                    _ => false
                };
            }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
            {
                return false;
            }
        }

        private static bool HasAdministrator(JsonElement roles)
        {
            foreach (var role in roles.EnumerateArray())
                if (role.ValueKind == JsonValueKind.String && role.GetString() == AdministratorRole)
                    return true;
            return false;
        }

        private static string DecodeBase64Url(string segment)
        {
            var base64 = segment.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
    }
}
