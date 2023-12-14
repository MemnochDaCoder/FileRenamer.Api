using FileRenamer.Api.Models;

namespace FileRenamer.Api.Interfaces
{
    public interface IOpenSubtitlesService
    {
        Task<string> GetToken();
        Task<SubtitleSearchResult> SearchSubtitlesAsync(string name);
    }
}
