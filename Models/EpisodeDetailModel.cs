#pragma warning disable CS8618
namespace FileRenamer.Api.Models
{
    public class Series
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
    }

    public class Episode
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public string Name { get; set; }
        public string Aired { get; set; }
        public string Image { get; set; }
        public int Number { get; set; }
        public int SeasonNumber { get; set; }
    }

    public class SeriesDetailData
    {
        public Series Series { get; set; }
        public List<Episode> Episodes { get; set; }
    }
}
