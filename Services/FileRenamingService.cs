
using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace FileRenamer.Api.Services
{
    public class FileRenamingService : IFileRenamingService
    {
        private readonly ITvDbService _tvDbService;
        private readonly IOpenSubtitlesService _openSubtitlesService;
        private readonly ILogger _logger;

        public FileRenamingService(ITvDbService tvDbService, IOpenSubtitlesService openSubtitlesService, ILogger<FileRenamingService> logger)
        {
            _tvDbService = tvDbService;
            _openSubtitlesService = openSubtitlesService;
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

                    var formattedFileName = FormatFileName(fileName);

                    if (formattedFileName != null)
                    {
                        var deconstructedFileName = formattedFileName.Split(' ');

                        if (deconstructedFileName.Length == 2)
                        {
                            formattedFileName = deconstructedFileName[0];
                        }

                        var apiData = await _tvDbService.SearchShowsAndFetchEpisodeAsync(formattedFileName);

                        if (apiData != null && apiData.Data != null && apiData.Data.Count > 0)
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

                                var pattern = @"S(\d{2})E(\d{2})|s(\d{2})e(\d{2})|(\d{1})x(\d{2})";
                                Match match = Regex.Match(deconstructedFileName[1], pattern);

                                string? filePath = null;

                                foreach (var d in deconstructedFileName.Select((value, i) => new { i, value }))
                                {
                                    filePath += d.i != deconstructedFileName.Length ? $" {d.value}" : "";
                                    if (!match.Success)
                                    {
                                        match = Regex.Match(d.value, pattern);
                                    }
                                }

                                if (match.Success)
                                {
                                    var s = FindAndParseTwoIntegers(match.Groups);
                                    var season = s.firstInt;
                                    var episode = s.secondInt;
                                    var episodeDetail = await _tvDbService.GetEpisodeDetailsAsync(int.Parse(apiData.Data[0].Id), season.ToString(), episode.ToString());
                                    var ss = episodeDetail.Data.Episodes[0].SeasonNumber.ToString().Length == 1 ? "S0" : "S";
                                    var ee = episodeDetail.Data.Episodes[0].Number.ToString().Length == 1 ? "E0" : "E";
                                    var proposedFileName = SanitizeFileName($"{episodeDetail.Data.Series.Name} {ss + episodeDetail.Data.Episodes[0].SeasonNumber}{ee + episodeDetail.Data.Episodes[0].Number} {episodeDetail.Data.Episodes[0].Name}");

                                    proposedChanges.Add(new ProposedChangeModel
                                    {
                                        OriginalFilePath = task.SourceDirectory,
                                        OriginalFileName = fileName,
                                        ProposedFileName = proposedFileName,
                                        FileType = deconstructedFileName[deconstructedFileName.Length - 1],
                                        Season = season.ToString(),
                                        Episode = episode.ToString()
                                    });
                                }
                                else
                                {
                                    _logger.LogError("No match was found using the regex for season and episode.");
                                    throw new Exception("No match was found using the regex for season and episode.");
                                }
                            }
                        }
                        else
                        {
                            var split = formattedFileName.Split(' ');
                            formattedFileName = "";

                            for(int i = 0; i < split.Length; i++)
                            {
                                if (split[i] != "mp4" && split[i] != "avi" && split[i] != "mkv")
                                {
                                        formattedFileName += split[i] + " ";
                                }
                            }

                            formattedFileName = formattedFileName.Trim();

                            proposedChanges.Add(new ProposedChangeModel
                            {
                                OriginalFilePath = task.SourceDirectory,
                                OriginalFileName = fileName,
                                ProposedFileName = formattedFileName,
                                FileType = Path.GetExtension(fileName).TrimStart('.')
                            });
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
            for (int i = 0; i < confirmedChanges.Count - 1; i++)
            {
                if (confirmedChanges.Count <= i) { continue; }
                confirmedChanges[i + 1].NewFilePath = $@"{confirmedChanges[0].NewFilePath}";
                confirmedChanges[i + 1].OriginalFilePath = confirmedChanges[0].OriginalFilePath;
            }

            try
            {
                foreach (var change in confirmedChanges)
                {
                    if (allowedExtensions.Contains(Path.GetExtension(change.OriginalFileName))) // Checking file extension before renaming
                    {
                        var oldPath = Path.Combine(change.OriginalFilePath, change.OriginalFileName);
                        var sanitizedNewFileName = SanitizeFileName(change.NewFileName); // Sanitizing the new filename
                        var newPath = Path.Combine(change.NewFilePath, sanitizedNewFileName + Path.GetExtension(change.OriginalFileName));

                        if (System.IO.File.Exists(change.NewFilePath))
                        {
                            _logger.LogWarning($"File with the name {sanitizedNewFileName} already exists. Skipping renaming of {change.OriginalFileName}.");
                            continue;
                        }
                        if (System.IO.File.Exists(oldPath) && Directory.Exists($@"{confirmedChanges[0].NewFilePath}\"))
                        {
                            System.IO.File.Move(oldPath, newPath);
                            //var subtitleSearchResult = _openSubtitlesService.SearchSubtitlesAsync(sanitizedNewFileName);
                            //if (subtitleSearchResult != null)
                            //{
                            //    _openSubtitlesService.DownloadSubtitle(subtitleSearchResult.Result.data[0].id, sanitizedNewFileName, newPath);
                            //}
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
            // Split the filename into parts
            var parts = fileName.Split('.');
            if (parts.Length < 3)
            {
                parts = fileName.Split(' ');

                for(int i = 0; i < parts.Length; i++)
                {
                    parts[i] = RemoveExtensions(parts[i]);
                }
            }
            // Use a StringBuilder for efficient string manipulation
            var formattedName = new StringBuilder();

            foreach (var part in parts)
            {
                // Check for a year in parentheses (e.g., 2023) for movies
                if (Regex.IsMatch(part, @"^(19|20)\d{2}$"))
                {
                    formattedName.Append($" ({part})");
                    break; // Stop processing after the year for movies
                }

                // Check for resolution or quality indicators and break the loop if found
                if (IsNonTitlePart(part))
                {
                    break;
                }

                // Append other parts of the title, replacing dots with spaces
                formattedName.Append(part.Replace(".", " ") + " ");
            }

            // Trim and return the formatted name
            return formattedName.ToString().Replace("  ", " ").Trim();
        }

        private static bool IsNonTitlePart(string part)
        {
            var nonTitleParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WEBRip", "x264", "AAC", "YTS", "MX", "1080p", "720p", "mp4", "avi", "mkv"
                // Add more non-title parts as needed
            };

            return nonTitleParts.Contains(part) || Regex.IsMatch(part, @"\d{3,4}p", RegexOptions.IgnoreCase);
        }

        private (int? firstInt, int? secondInt) FindAndParseTwoIntegers(GroupCollection groups)
        {
            int? firstInt = null;
            int? secondInt = null;

            // Assuming groups[0] is the entire match, start from groups[1]
            for (int i = 1; i < groups.Count; i++)
            {
                if (int.TryParse(groups[i].Value, out int result))
                {
                    if (firstInt == null)
                    {
                        firstInt = result;
                    }
                    else
                    {
                        secondInt = result;
                        break; // Exit after finding the second integer
                    }
                }
            }

            return (firstInt, secondInt);
        }

        private static string RemoveExtensions(string s)
        {
            var removalList = new string[] { ".mp4", ".avi", ".mkv" };

            foreach(var r in removalList)
            {
                s = s.Replace(r, "");
            }

            return s;
        }
    }
}
