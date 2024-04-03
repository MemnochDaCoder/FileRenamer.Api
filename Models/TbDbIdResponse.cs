namespace FileRenamer.Api.Models
{
    public class TbDbIdResponse
    {
        public string Status { get; set; }
        public List<TvDbSearchData> Data { get; set; }
    }

    public class TvDbSearchData
    {
        public string TvdbId { get; set; }
        public string Id { get; set; }
    }

    public class TvDbExtendedResponse
    {
        public List<TvDbEpisode> Episodes { get; set; }
    }

    public class TvDbEpisode
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public string Name { get; set; }
        public string Aired { get; set; }
        public int SeasonNumber { get; set; }
        public int Number { get; set; }
    }

}
