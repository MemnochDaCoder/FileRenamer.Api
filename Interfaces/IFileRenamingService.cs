using FileRenamer.Api.Models;

namespace FileRenamer.Api.Interfaces
{
    public interface IFileRenamingService
    {
        Task<List<ProposedChangeModel>> ProposeChangesAsync(RenamingTask task);
        bool ExecuteRenamingAsync(List<ConfirmedChangeModel> confirmedChanges);
    }
}
