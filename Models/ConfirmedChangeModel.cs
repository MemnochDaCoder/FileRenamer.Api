namespace FileRenamer.Api.Models
{
    public class ConfirmedChangeModel
    {
        public required string OriginalFilePath { get; set; } // Full path of the original file
        public required string NewFilePath { get; set; } // New path where the file will be moved after renaming
        public required string OriginalFileName { get; set; } // Original name of the file
        public required string NewFileName { get; set; } // New name of the file confirmed for renaming
    }
}
