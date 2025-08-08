using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GcpFileMove.Services
{
    public class GcpAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public GcpAuthService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            var keyFilePath = _configuration["Gcp:ServiceAccountKey"];
            var keyFileContent = await File.ReadAllTextAsync(keyFilePath!);
            var keyData = JsonSerializer.Deserialize<JsonElement>(keyFileContent);

            var privateKey = keyData.GetProperty("private_key").GetString();
            var clientEmail = keyData.GetProperty("client_email").GetString();

            var header = new { alg = "RS256", typ = "JWT" };
            var payload = new
            {
                iss = clientEmail,
                scope = "https://www.googleapis.com/auth/devstorage.full_control",
                aud = "https://oauth2.googleapis.com/token",
                exp = new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds(),
                iat = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()
            };

            var base64Header = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
            var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));

            var signingInput = $"{base64Header}.{base64Payload}";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            var signature = rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var base64Signature = Convert.ToBase64String(signature);

            var jwt = $"{signingInput}.{base64Signature}";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", jwt)
            });

            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return tokenData.GetProperty("access_token").GetString() ?? string.Empty;
        }
    }
}
