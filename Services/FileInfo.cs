namespace GcpFileMove.Services
{
    public class FileInfo
    {
        public string UuidFileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public long Size { get; set; }
    }
}
