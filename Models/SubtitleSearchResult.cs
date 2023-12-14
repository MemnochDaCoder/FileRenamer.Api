namespace FileRenamer.Api.Models
{
    public class SubtitleSearchResult
    {
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public string[]? Data { get; set; }
    }
}
