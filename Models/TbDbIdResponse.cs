#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace FileRenamer.Api.Models
{
    // Response model for TVDB ID search
    public class TbDbIdResponse
    {
        public string Status { get; set; }
        public List<TvDbSearchData> Data { get; set; }
    }

    // Search data containing TVDB IDs
    public class TvDbSearchData
    {
        public string TvdbId { get; set; }
        public string Id { get; set; }
    }

    // Extended response model containing detailed TV show data
    public class TvDbExtendedResponse
    {
        public TvDbExtendedData Data { get; set; } // Matches the JSON structure correctly.
    }

    // Extended data containing series info and a list of episodes
    public class TvDbExtendedData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<TvDbEpisode> Episodes { get; set; }
    }

    // Model representing an individual episode
    public class TvDbEpisode
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public string Name { get; set; }
        public string Aired { get; set; } // Consider changing to DateTime if the value is always a date.
        public int SeasonNumber { get; set; }
        public int Number { get; set; }
    }
}
