using Newtonsoft.Json;

namespace LGCNS.axink.Models.Messages
{
    /// <summary>
    /// STT Stream전송 초기메세지
    /// </summary>
    public class InitMessage
    {
        [JsonProperty("type")]
        public required string Type { get; set; }

        [JsonProperty("accessToken")]
        public required string AccessToken { get; set; }

        [JsonProperty("roomId")]
        public required int RoomId { get; set; }

        [JsonProperty("sourceLanguage")]
        public required string SourceLanguage { get; set; }

        [JsonProperty("targetLanguage")]
        public required string TargetLanguage { get; set; }

        [JsonProperty("platform")]
        public required string Platform { get; set; }

        [JsonProperty("roomType")]
        public required string RoomType { get; set; }

        [JsonProperty("audioInfo")]
        public required AudioInfo AudioInfo { get; set; }
    }

    public class AudioInfo
    {
        [JsonProperty("audioFormat")]
        public required string AudioFormat { get; set; }

        [JsonProperty("sampleRate")]
        public required int SampleRate { get; set; }

        [JsonProperty("numChannels")]
        public required int NumChannels { get; set; }
    }
}
