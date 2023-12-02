namespace FileRenamer.Api.Models
{
    public class RenamingTask
    {
        public required string SourceDirectory { get; set; }
        public required string DestinationDirectory { get; set; }
    }
}
