namespace FileRenamer.Api.Models
{
    public class ProposedChangeModel
    {
        public required string OriginalFilePath { get; set; } // Full path of the original file
        public required string OriginalFileName { get; set; } // Original name of the file
        public required string ProposedFileName { get; set; } // Proposed new name of the file
        public required string FileType { get; set; } // Type of the file (Movie/TV Show)
        public string? Season { get; set; }
        public string? Episode { get; set; }
    }
}
