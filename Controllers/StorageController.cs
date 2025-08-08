using System.IO;
using System.Threading.Tasks;
using GcpFileMove.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GcpFileMove.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StorageController : ControllerBase
    {
        private readonly GcpStorageService _storageService;

        public StorageController(GcpStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFileList()
        {
            var fileList = await _storageService.GetFileListAsync();
            return Ok(fileList);
        }

        [HttpGet("metadata")]
        public async Task<IActionResult> GetFileListWithMetadata()
        {
            var fileList = await _storageService.GetFileListWithMetadataAsync();
            return Ok(fileList);
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file selected.");
            }

            var uuidFileName = await _storageService.UploadFileAsync(file.FileName, file.OpenReadStream());
            return Ok(new { UuidFileName = uuidFileName, OriginalFileName = file.FileName });
        }

        [HttpGet("{uuidFileName}")]
        public async Task<IActionResult> DownloadFile(string uuidFileName)
        {
            var (fileStream, originalFileName) = await _storageService.DownloadFileAsync(uuidFileName);
            return File(fileStream, "application/octet-stream", originalFileName);
        }

        [HttpDelete("{uuidFileName}")]
        public async Task<IActionResult> DeleteFile(string uuidFileName)
        {
            await _storageService.DeleteFileAsync(uuidFileName);
            return Ok();
        }
    }
}
