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
    }
}
