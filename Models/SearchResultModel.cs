#pragma warning disable CS8618
using Newtonsoft.Json;

namespace FileRenamer.Api.Models
{
    public class SearchResultModel
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string ImageUrl { get; set; }
        public string Year { get; set; }
    }

    public class TvDbData
    {
        [JsonProperty("objectID")]
        public string ObjectID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tvdb_id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("year")]
        public string Year { get; set; }
    }

    public class TvDbResponse
    {
        [JsonProperty("data")]
        public List<TvDbData> Data { get; set; }
    }
}
