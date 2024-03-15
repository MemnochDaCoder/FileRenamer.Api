using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FileRenamer.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RenameFilesController : ControllerBase
    {
        private readonly ILogger<RenameFilesController> _logger;
        private readonly IFileRenamingService _fileRenamingService;
        private readonly IOpenSubtitlesService _openSubtitlesService;

        public RenameFilesController(ILogger<RenameFilesController> logger, IFileRenamingService fileRenamingService, IOpenSubtitlesService openSubtitlesService)
        {
            _logger = logger;
            _fileRenamingService = fileRenamingService;
            _openSubtitlesService = openSubtitlesService;
        }

        /// <summary>
        /// Retrieves a list of proposed file name changes for files in the specified source directory.
        /// </summary>
        /// <param name="sourceDirectory">The source directory containing files to be renamed.</param>
        /// <param name="destinationDirectory">The destination directory where renamed files will be moved.</param>
        /// <returns>A list of proposed file name changes.</returns>
        /// <response code="200">Returns the list of proposed file name changes.</response>
        /// <response code="400">If the source or destination directory is null or empty.</response>
        /// <response code="404">If no files are found for renaming.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("GetProposedFileNames")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ProposedChangeModel>>> GetProposedFileNames(string sourceDirectory, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                return BadRequest("Source directory is required.");
            }
            if (string.IsNullOrEmpty(destinationDirectory))
            {
                return BadRequest("Destination directory is required.");
            }

            try
            {
                var proposedChanges = await _fileRenamingService.ProposeChangesAsync(new RenamingTask { SourceDirectory = sourceDirectory, DestinationDirectory = destinationDirectory });
                if (proposedChanges == null || proposedChanges.Count == 0)
                {
                    return NotFound("No files found for renaming.");
                }

                return Ok(proposedChanges);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(500, ex.ToString());
            }
        }

        /// <summary>
        /// Executes the file renaming operation based on the confirmed changes.
        /// </summary>
        /// <param name="confirmedChanges">List of confirmed changes for file renaming.</param>
        /// <returns>An ActionResult indicating the success or failure of the operation.</returns>
        /// <response code="200">If the renaming operation is successful.</response>
        /// <response code="400">If the input is null or invalid.</response>
        /// <response code="500">If an error occurs during the renaming operation.</response>
        [HttpPost("ExecuteRenaming")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ExecuteRenaming([FromBody] List<ConfirmedChangeModel> confirmedChanges)
        {
            if (confirmedChanges == null || !confirmedChanges.Any())
            {
                return BadRequest("Confirmed changes are required.");
            }

            try
            {
                var success = _fileRenamingService.ExecuteRenamingAsync(confirmedChanges);
                if (success)
                {
                    return Ok("Renaming operation completed successfully.");
                }
                else
                {
                    return StatusCode(500, "Renaming operation failed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCode(500, ex.ToString());
            }
        }

        ///// <summary>
        ///// Retrieves subtitle file(s).
        ///// </summary>
        ///// <param name="title">Show/movie title.</param>
        ///// <returns>An ActionResult indicating the success or failure of the operation.</returns>
        ///// <response code="200">If the retrival operation is successful.</response>
        ///// <response code="400">If the input is null or invalid.</response>
        ///// <response code="500">If an error occurs during the retrival operation.</response>
        //[HttpPost("GetSubs")]
        //[ProducesResponseType(StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        //public IActionResult GetSubtitles(string title)
        //{
        //    if(string.IsNullOrEmpty(title))
        //    {
        //        _logger.LogError("Title cannot be null or empty.");
        //        return BadRequest("Title cannot be null or empty.");
        //    }

        //    var result = _openSubtitlesService.SearchSubtitlesAsync(title);

        //    if (result.IsCompletedSuccessfully)
        //        return Ok(result.Result);

        //    return BadRequest(result.Result);
        //}
    }
}
