
using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileRenamer.Api.Services
{
    public class FileRenamingService : IFileRenamingService
    {
        private readonly ITvDbService _tvDbService;
        private readonly ILogger _logger;

        public FileRenamingService(ITvDbService tvDbService, ILogger<FileRenamingService> logger)
        {
            _tvDbService = tvDbService;
            _logger = logger;
        }

        public async Task<List<ProposedChangeModel>> ProposeChangesAsync(RenamingTask task)
        {
            var proposedChanges = new List<ProposedChangeModel>();
            var allowedExtensions = new[] { ".mp4", ".mkv", ".avi" };

            try
            {
                if (!Directory.Exists(task.SourceDirectory) && !Directory.Exists(task.DestinationDirectory))
                {
                    _logger.LogError($"The source: {task.SourceDirectory} or destination: {task.DestinationDirectory} did not exist.");
                    throw new DirectoryNotFoundException($"The source: {task.SourceDirectory} or destination: {task.DestinationDirectory} did not exist.");
                }
                var files = Directory.GetFiles(task.SourceDirectory)
                    .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
                    .ToList();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    if(fileName.Contains("House MD"))
                    {
                        proposedChanges.Add(FormatHouse(fileName, task.SourceDirectory, task.DestinationDirectory));
                        continue;
                    }

                    var formattedFileName = FormatFileName(fileName);

                    if (formattedFileName != null)
                    {

                        var deconstructedFileName = formattedFileName.Split(' ');

                        if (deconstructedFileName.Length == 2)
                        {
                            formattedFileName = deconstructedFileName[0];
                        }

                        var apiData = await _tvDbService.SearchShowsOrMoviesAsync(formattedFileName);

                        if (apiData != null)
                        {
                            if (files.Count == 0)
                            {
                                _logger.LogError("No files were found in the source directory.");
                                throw new ArgumentException("No files were found in the source directory.");
                            }
                            else if (files.Count == 1)
                            {
                                //Movies
                                _logger.LogInformation("Started renaming a movie.");

                                var movieDetail = await _tvDbService.GetMovieDetailsAsync(int.Parse(apiData.Data[0].Id));

                                proposedChanges.Add(new ProposedChangeModel
                                {
                                    OriginalFilePath = task.SourceDirectory,
                                    OriginalFileName = fileName,
                                    ProposedFileName = $"{SanitizeFileName($"{movieDetail?.Data?.Name} ({movieDetail?.Data?.Year})")}",
                                    FileType = deconstructedFileName[deconstructedFileName.Length - 1]
                                });
                            }
                            else
                            {
                                //Shows
                                _logger.LogInformation("Started renaming episode.");

                                var pattern = @"S(\d{2})E(\d{2}) | (\d{1})x(\d{2})";
                                Match match = Regex.Match(deconstructedFileName[1], pattern);

                                string? filePath = null;

                                foreach (var d in deconstructedFileName.Select((value, i) => new { i, value }))
                                {
                                    filePath += d.i != deconstructedFileName.Length ? $" {d.value}" : "";
                                }

                                if (match.Success)
                                {
                                    var season = int.Parse(match.Groups[1].Value);
                                    var episode = int.Parse(match.Groups[2].Value);
                                    var episodeDetail = await _tvDbService.GetEpisodeDetailsAsync(int.Parse(apiData.Data[0].Id), season.ToString(), episode.ToString());
                                    var ss = episodeDetail.Data.Episodes[0].SeasonNumber.ToString().Length == 1 ? "S0" : "S";
                                    var ee = episodeDetail.Data.Episodes[0].Number.ToString().Length == 1 ? "E0" : "E";
                                    var test = $"{episodeDetail.Data.Series.Name} {ss}{episodeDetail.Data.Episodes[0].SeasonNumber}{ee}{episodeDetail.Data.Episodes[0].Number} {episodeDetail.Data.Episodes[0].Name}";

                                    proposedChanges.Add(new ProposedChangeModel
                                    {
                                        OriginalFilePath = task.SourceDirectory,
                                        OriginalFileName = fileName,
                                        ProposedFileName = SanitizeFileName($"{episodeDetail.Data.Series.Name} {ss + episodeDetail.Data.Episodes[0].SeasonNumber}{ee + episodeDetail.Data.Episodes[0].Number} {episodeDetail.Data.Episodes[0].Name}"),
                                        FileType = deconstructedFileName[deconstructedFileName.Length - 1],
                                        Season = season.ToString(),
                                        Episode = episode.ToString()
                                    });
                                }
                            }
                        }
                    }
                }
                return proposedChanges;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proposing changes.");
                return proposedChanges;
            }
        }

        public bool ExecuteRenamingAsync(List<ConfirmedChangeModel> confirmedChanges)
        {
            var allowedExtensions = new[] { ".mp4", ".mkv", ".avi" }; // Defining allowed extensions

            try
            {
                foreach (var change in confirmedChanges)
                {
                    if (allowedExtensions.Contains(Path.GetExtension(change.OriginalFileName))) // Checking file extension before renaming
                    {
                        var oldPath = "";
                        var sanitizedNewFileName = "";
                        var newPath = "";
                        if (!change.OriginalFileName.Contains("House"))
                        {
                            oldPath = Path.Combine(change.OriginalFilePath, change.OriginalFileName);
                            sanitizedNewFileName = SanitizeFileName(change.NewFileName); // Sanitizing the new filename
                            newPath = Path.Combine(change.NewFilePath, sanitizedNewFileName + Path.GetExtension(change.OriginalFileName));
                        }
                        else
                        {
                            oldPath = change.OriginalFilePath;
                            sanitizedNewFileName = SanitizeFileName(change.NewFileName); // Sanitizing the new filename
                            newPath = Path.Combine(change.NewFilePath);
                        }

                        if (File.Exists(newPath))
                        {
                            _logger.LogWarning($"File with the name {sanitizedNewFileName} already exists. Skipping renaming of {change.OriginalFileName}.");
                            continue;
                        }
                        if (File.Exists(oldPath))
                        {
                            File.Move(oldPath, newPath);
                        }
                        else
                        {
                            Console.WriteLine($"File not found: {oldPath}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing renaming.");
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string invalidCharsRemoved = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());

            // You might also want to trim whitespace, if necessary
            return invalidCharsRemoved.Trim();
        }

        private static string FormatFileName(string fileName)
        {
            var parts = fileName.Split('.');

            // Finding the index of the part that is a year (assuming the year is between 1900 and 2099)
            int yearIndex = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (Regex.IsMatch(parts[i], @"^(19|20)\d{2}$"))
                {
                    yearIndex = i - 1;
                    break;
                }
            }

            // If a year was found, keep only the parts before the year and the year itself
            if (yearIndex != -1)
            {
                parts = parts.Take(yearIndex + 1).ToArray();
            }

            // Replacing dots with spaces and joining the parts back together
            if (parts.Length != 2)
            {
                return string.Join(" ", parts);
            }
            else
            {
                return parts[0];
                //var title = parts[0].Split();
                //return $"{title[0]} {title[1]}";
            }
        }

        private static ProposedChangeModel FormatHouse(string fileName, string originalPath, string sourceDirectory)
        {
            var parts = fileName.Split(' ');
            var season = parts[3].Length > 1 ? parts[3] : "0" + parts[3];
            string episodeName = string.Empty;

            if (parts.Length > 7)
            {
                for(int i = 0; i < parts.Length; ++i)
                {
                    if(i > 6)
                    {
                        episodeName += parts[i];
                    }
                }
            }
            return new ProposedChangeModel
            {
                OriginalFilePath = sourceDirectory,
                OriginalFileName = fileName,
                ProposedFileName = $"{parts[0]} {parts[1]} S{season}E{parts[5]} {episodeName}",
                FileType = Path.GetExtension(fileName),
                Season = season.ToString(),
                Episode = episodeName
            };
        }
    }
}
