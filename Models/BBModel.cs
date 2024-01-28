using Newtonsoft.Json;

namespace FileRenamer.Api.Models
{
    public class SimplifiedTvDbResponse
    {
        [JsonProperty("data")]
        public SimplifiedData Data { get; set; }
    }

    public class SimplifiedData
    {
        [JsonProperty("series")]
        public SimplifiedSeries Series { get; set; }

        [JsonProperty("episodes")]
        public List<SimplifiedEpisode> Episodes { get; set; }
    }

    public class SimplifiedSeries
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image")]
        public string ImageUrl { get; set; }

        [JsonProperty("objectid")]
        public string ObjectId { get; set; }

        [JsonProperty("recordType")]
        public string Type { get; set; }

        [JsonProperty("year")]
        public string Year { get; set; }
    }

    public class SimplifiedEpisode
    {
        [JsonProperty("id")]
        public int ObjectId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
