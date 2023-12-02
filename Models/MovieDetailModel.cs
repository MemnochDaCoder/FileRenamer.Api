namespace FileRenamer.Api.Models
{
    public class MovieDetailModel
    {
        public string? Status { get; set; }
        public MovieDetailData? Data { get; set; }
    }

    public class MovieDetailData
    {
        public string? Name { get; set; }
        public string? Year { get; set; }
    }
}
