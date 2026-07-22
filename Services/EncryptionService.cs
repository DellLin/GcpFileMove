using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GcpFileMove.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(IConfiguration configuration)
        {
            // 從配置文件獲取加密密鑰，或使用預設值
            var keyString = configuration["Encryption:Key"] ?? "MySecretKey12345MySecretKey12345"; // 32 bytes for AES-256
            var ivString = configuration["Encryption:IV"] ?? "MySecretIV123456"; // 16 bytes for AES

            _key = System.Text.Encoding.UTF8.GetBytes(keyString.Substring(0, 32));
            _iv = System.Text.Encoding.UTF8.GetBytes(ivString.Substring(0, 16));
        }

        public async Task<Stream> EncryptStreamAsync(Stream inputStream)
        {
            var outputStream = new MemoryStream();

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                {
                    await inputStream.CopyToAsync(cryptoStream);
                    await cryptoStream.FlushFinalBlockAsync();
                }
            }

            outputStream.Position = 0;
            return outputStream;
        }

        public async Task<Stream> DecryptStreamAsync(Stream inputStream)
        {
            var outputStream = new MemoryStream();

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                {
                    await cryptoStream.CopyToAsync(outputStream);
                }
            }

            outputStream.Position = 0;
            return outputStream;
        }

        // 檔名獨立加密：每次呼叫使用隨機 IV 並附加於密文前，
        // 避免相同檔名產生相同密文（固定 IV 會讓相同檔名可被比對出來）
        public string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string DecryptString(string cipherTextBase64)
        {
            var fullBytes = Convert.FromBase64String(cipherTextBase64);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(fullBytes, iv.Length, fullBytes.Length - iv.Length);

            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
    }
}
