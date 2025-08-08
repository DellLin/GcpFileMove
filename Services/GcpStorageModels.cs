using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GcpFileMove.Services
{
    public class GcpStorageListResponse
    {
        [JsonPropertyName("items")]
        public List<GcpStorageItem> Items { get; set; } = new List<GcpStorageItem>();
    }

    public class GcpStorageItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public string Size { get; set; } = "0";

        [JsonPropertyName("timeCreated")]
        public string TimeCreated { get; set; } = string.Empty;
    }
}
