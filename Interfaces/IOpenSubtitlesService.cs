using FileRenamer.Api.Models;

namespace FileRenamer.Api.Interfaces
{
    public interface IOpenSubtitlesService
    {
        Task<string> GetToken();
        Task<SubtitleSearchResult> SearchSubtitlesAsync(string name);
        Task DownloadSubtitle(string subtitleId, string newFileName, string filePath);
    }
}
