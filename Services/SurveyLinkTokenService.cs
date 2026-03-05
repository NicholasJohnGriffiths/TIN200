using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TINWeb.Services
{
    public class SurveyLinkTokenService : ISurveyLinkTokenService
    {
        private readonly SurveyLinkSettings _settings;

        public SurveyLinkTokenService(IOptions<SurveyLinkSettings> settings)
        {
            _settings = settings.Value;
        }

        public string GenerateToken(int clientId)
        {
            if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            {
                throw new InvalidOperationException("SurveyLinkSettings:SecretKey is not configured.");
            }

            var expiresUnix = DateTimeOffset.UtcNow.AddHours(_settings.ExpiryHours).ToUnixTimeSeconds();
            var payload = $"{clientId}:{expiresUnix}";
            var signature = ComputeSignature(payload);
            return ToBase64Url($"{payload}:{signature}");
        }

        public bool IsTokenValid(int clientId, string token)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_settings.SecretKey))
            {
                return false;
            }

            string decoded;
            try
            {
                decoded = FromBase64Url(token);
            }
            catch
            {
                return false;
            }

            var parts = decoded.Split(':');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var tokenClientId) || tokenClientId != clientId)
            {
                return false;
            }

            if (!long.TryParse(parts[1], out var expiresUnix))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
            {
                return false;
            }

            var payload = $"{parts[0]}:{parts[1]}";
            var expectedSignature = ComputeSignature(payload);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[2]),
                Encoding.UTF8.GetBytes(expectedSignature));
        }

        private string ComputeSignature(string payload)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(payloadBytes);
            return Convert.ToHexString(hash);
        }

        private static string ToBase64Url(string input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string FromBase64Url(string input)
        {
            var base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
