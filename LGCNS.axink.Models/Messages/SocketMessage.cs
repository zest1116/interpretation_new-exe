using Newtonsoft.Json;

namespace LGCNS.axink.Models.Messages
{
    public class SocketMessage
    {
        [JsonProperty("type")]
        public required string Type { get; set; }

        [JsonProperty("deviceType")]
        public required string DeviceType { get; set; }
    }
}
