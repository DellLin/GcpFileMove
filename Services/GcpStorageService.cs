using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GcpFileMove.Services
{
    public class GcpStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly GcpAuthService _authService;
        private readonly EncryptionService _encryptionService;

        // 簡單的記憶體快取，實際應用中可考慮使用 IMemoryCache
        private readonly Dictionary<string, string> _fileNameCache = new Dictionary<string, string>();

        public GcpStorageService(IConfiguration configuration, HttpClient httpClient, GcpAuthService authService, EncryptionService encryptionService)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _authService = authService;
            _encryptionService = encryptionService;
        }

        public async Task<string> GetFileListAsync()
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];
            var url = $"https://storage.googleapis.com/storage/v1/b/{bucketName}/o";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<List<FileInfo>> GetFileListWithMetadataAsync()
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];
            var url = $"https://storage.googleapis.com/storage/v1/b/{bucketName}/o";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var storageResponse = JsonSerializer.Deserialize<GcpStorageListResponse>(jsonResponse);

            var fileInfoList = new List<FileInfo>();

            if (storageResponse?.Items != null)
            {
                foreach (var item in storageResponse.Items)
                {
                    try
                    {
                        // 嘗試下載並解析每個檔案的元數據
                        var originalFileName = await GetOriginalFileNameAsync(item.Name);

                        fileInfoList.Add(new FileInfo
                        {
                            UuidFileName = item.Name,
                            OriginalFileName = originalFileName,
                            Size = long.TryParse(item.Size, out var size) ? size : 0,
                            UploadDate = DateTime.TryParse(item.TimeCreated, out var date) ? date : DateTime.MinValue
                        });
                    }
                    catch (Exception)
                    {
                        // 如果無法解析某個檔案，使用 UUID 作為顯示名稱
                        fileInfoList.Add(new FileInfo
                        {
                            UuidFileName = item.Name,
                            OriginalFileName = $"Unknown ({item.Name})",
                            Size = long.TryParse(item.Size, out var size) ? size : 0,
                            UploadDate = DateTime.TryParse(item.TimeCreated, out var date) ? date : DateTime.MinValue
                        });
                    }
                }
            }

            return fileInfoList;
        }

        private async Task<string> GetOriginalFileNameAsync(string uuidFileName)
        {
            // 檢查快取
            if (_fileNameCache.TryGetValue(uuidFileName, out var cachedName))
            {
                return cachedName;
            }

            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];
            var url = $"https://storage.googleapis.com/storage/v1/b/{bucketName}/o/{uuidFileName}?alt=media";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var encryptedStream = await response.Content.ReadAsStreamAsync();
            var decryptedStream = await _encryptionService.DecryptStreamAsync(encryptedStream);

            // 只讀取檔案名稱部分，不需要整個檔案內容
            var lengthBuffer = new byte[4];
            await decryptedStream.ReadAsync(lengthBuffer, 0, 4);
            var fileNameLength = BitConverter.ToInt32(lengthBuffer, 0);

            var fileNameBuffer = new byte[fileNameLength];
            await decryptedStream.ReadAsync(fileNameBuffer, 0, fileNameLength);
            var originalFileName = Encoding.UTF8.GetString(fileNameBuffer);

            // 加入快取
            _fileNameCache[uuidFileName] = originalFileName;

            // 清理資源
            decryptedStream.Dispose();
            encryptedStream.Dispose();

            return originalFileName;
        }

        public async Task<string> UploadFileAsync(string fileName, Stream stream)
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];

            // 生成UUID作為檔案名稱
            var uuidFileName = Guid.NewGuid().ToString();
            var url = $"https://storage.googleapis.com/upload/storage/v1/b/{bucketName}/o?uploadType=media&name={uuidFileName}";

            // 創建包含原始檔案名稱的檔案流
            var fileStreamWithMetadata = await CreateStreamWithMetadata(fileName, stream);

            // 加密檔案流
            var encryptedStream = await _encryptionService.EncryptStreamAsync(fileStreamWithMetadata);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var content = new StreamContent(encryptedStream);
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            // 上傳成功後，將檔案名稱加入快取
            _fileNameCache[uuidFileName] = fileName;

            // 清理流
            encryptedStream.Dispose();
            fileStreamWithMetadata.Dispose();

            return uuidFileName; // 返回UUID檔案名稱
        }
        public async Task<(Stream fileStream, string originalFileName)> DownloadFileAsync(string uuidFileName)
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];
            var url = $"https://storage.googleapis.com/storage/v1/b/{bucketName}/o/{uuidFileName}?alt=media";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var encryptedStream = await response.Content.ReadAsStreamAsync();

            // 解密檔案流
            var decryptedStream = await _encryptionService.DecryptStreamAsync(encryptedStream);

            // 從解密的流中提取原始檔案名稱和檔案內容
            var (fileStream, originalFileName) = await ExtractFileStreamAndMetadata(decryptedStream);

            return (fileStream, originalFileName);
        }

        private async Task<Stream> CreateStreamWithMetadata(string fileName, Stream fileStream)
        {
            var memoryStream = new MemoryStream();

            // 將檔案名稱編碼為UTF-8
            var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
            var fileNameLength = BitConverter.GetBytes(fileNameBytes.Length);

            // 寫入檔案名稱長度（4字節）
            await memoryStream.WriteAsync(fileNameLength, 0, 4);

            // 寫入檔案名稱
            await memoryStream.WriteAsync(fileNameBytes, 0, fileNameBytes.Length);

            // 寫入實際檔案內容
            await fileStream.CopyToAsync(memoryStream);

            memoryStream.Position = 0;
            return memoryStream;
        }

        private async Task<(Stream fileStream, string originalFileName)> ExtractFileStreamAndMetadata(Stream streamWithMetadata)
        {
            // 讀取檔案名稱長度（4字節）
            var lengthBuffer = new byte[4];
            await streamWithMetadata.ReadAsync(lengthBuffer, 0, 4);
            var fileNameLength = BitConverter.ToInt32(lengthBuffer, 0);

            // 讀取檔案名稱
            var fileNameBuffer = new byte[fileNameLength];
            await streamWithMetadata.ReadAsync(fileNameBuffer, 0, fileNameLength);
            var originalFileName = Encoding.UTF8.GetString(fileNameBuffer);

            // 剩餘的內容就是實際的檔案內容
            var fileStream = new MemoryStream();
            await streamWithMetadata.CopyToAsync(fileStream);
            fileStream.Position = 0;

            return (fileStream, originalFileName);
        }

        public async Task DeleteFileAsync(string uuidFileName)
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            var bucketName = _configuration["Gcp:BucketName"];
            var url = $"https://storage.googleapis.com/storage/v1/b/{bucketName}/o/{uuidFileName}";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }
}
