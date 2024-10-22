
using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using System.Diagnostics;
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
                if (!Directory.Exists(task.SourceDirectory) || !Directory.Exists(task.DestinationDirectory))
                {
                    _logger.LogError($"The source: {task.SourceDirectory} or destination: {task.DestinationDirectory} did not exist.");
                    throw new DirectoryNotFoundException($"The source: {task.SourceDirectory} or destination: {task.DestinationDirectory} did not exist.");
                }

                var files = Directory.GetFiles(task.SourceDirectory)
                    .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);

                    // Replace dots with spaces for consistent formatting
                    fileName = fileName.Replace(".", " ");

                    var fileExtension = Path.GetExtension(file);

                    // Use ConstructTitle to parse the filename and determine series or movie
                    var (name, type) = await _tvDbService.ConstructTitle(fileName);

                    // Check if the file is already in the correct 'SxxEyy' format
                    var isFormattedCorrectly = Regex.IsMatch(name, @"s\d{2}e\d{2}", RegexOptions.IgnoreCase);

                    // If the file is a series and already formatted correctly, skip TVDB lookup and add it directly
                    if (type == "series" && isFormattedCorrectly)
                    {
                        proposedChanges.Add(new ProposedChangeModel
                        {
                            OriginalFilePath = file,
                            OriginalFileName = Path.GetFileName(file),
                            ProposedFileName = $"{name}{fileExtension}", // Use the correctly formatted name
                            FileType = type
                        });
                        continue; // Skip further processing and move to the next file
                    }

                    // If the name is not correctly formatted, fetch the TVDB ID for the series
                    if (type == "series")
                    {
                        var (tvDbId, isSeries) = await _tvDbService.GetTvdbIdAsync(name, type);

                        if (isSeries)
                        {
                            // Extract season and episode numbers from 'SxxEyy' format
                            var match = Regex.Match(fileName, @"s(\d{2})e(\d{2})", RegexOptions.IgnoreCase);

                            if (match.Success)
                            {
                                var season = int.Parse(match.Groups[1].Value);
                                var episode = int.Parse(match.Groups[2].Value);

                                // Get episode details from TVDB
                                var seriesDetailData = await _tvDbService.GetEpisodeDetailsAsync(int.Parse(tvDbId.Split('-')[1]), season.ToString(), episode.ToString());

                                if (seriesDetailData != null && seriesDetailData.Data.Episodes != null)
                                {
                                    var episodeDetail = seriesDetailData.Data.Episodes.FirstOrDefault(e => e.SeasonNumber == season && e.Number == episode);

                                    if (episodeDetail != null)
                                    {
                                        var proposedFileName = $"{seriesDetailData.Data.Series.Name} S{season:D2}E{episode:D2} {episodeDetail.Name}{fileExtension}";

                                        proposedChanges.Add(new ProposedChangeModel
                                        {
                                            OriginalFilePath = file,
                                            OriginalFileName = Path.GetFileName(file),
                                            ProposedFileName = proposedFileName,
                                            FileType = type
                                        });
                                    }
                                }
                            }
                        }
                    }
                    else if (type == "movie")
                    {
                        // Handle movies
                        var (tvDbId, _) = await _tvDbService.GetTvdbIdAsync(name, type);
                        var movieDetails = await _tvDbService.GetMovieDetailsAsync(int.Parse(tvDbId));

                        if (movieDetails != null && movieDetails.Data != null)
                        {
                            var proposedFileName = $"{movieDetails.Data.Name} ({movieDetails.Data.Year}){fileExtension}";

                            proposedChanges.Add(new ProposedChangeModel
                            {
                                OriginalFilePath = file,
                                OriginalFileName = file,
                                ProposedFileName = proposedFileName,
                                FileType = type,
                                Season = null,
                                Episode = null
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proposing changes.");
            }

            return proposedChanges;
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
                    change.NewFilePath = change.NewFilePath.Replace("U:\\", "\\\\10.0.0.164\\storage\\");

                    if (allowedExtensions.Contains(Path.GetExtension(change.OriginalFileName))) // Checking file extension before renaming
                    {
                        var oldPath = Path.Combine(change.OriginalFilePath, change.OriginalFileName);
                        var sanitizedNewFileName = SanitizeFileName(change.NewFileName); // Sanitizing the new filename
                        var newPath = Path.Combine(change.NewFilePath, sanitizedNewFileName);

                        if (System.IO.File.Exists(change.NewFilePath))
                        {
                            _logger.LogWarning($"File with the name {sanitizedNewFileName} already exists. Skipping renaming of {change.OriginalFileName}.");
                            continue;
                        }
                        if (System.IO.File.Exists(oldPath) && Directory.Exists($@"{confirmedChanges[0].NewFilePath}\"))
                        {
                            // Move the file
                            System.IO.File.Move(oldPath, newPath);

                            // Check if an existing .srt file is present
                            var oldSrtPath = Path.ChangeExtension(oldPath, ".srt");
                            var newSrtPath = Path.ChangeExtension(newPath, ".srt");

                            if (System.IO.File.Exists(oldSrtPath))
                            {
                                // Rename and move the existing .srt file to match the new .mkv filename
                                System.IO.File.Move(oldSrtPath, newSrtPath);
                                _logger.LogInformation($"Renamed and moved existing .srt file from {oldSrtPath} to {newSrtPath}.");
                            }
                            else if (Path.GetExtension(newPath).ToLower() == ".mkv")
                            {
                                // If no .srt file exists, attempt to extract subtitles
                                newPath = newPath.Replace("\\\\\\\\10.0.0.164", "U:");
                                newSrtPath = newSrtPath.Replace("\\\\\\\\10.0.0.164", "U:");
                                ExtractEnglishSubtitles(newPath, newSrtPath); // Extract subtitles, ignore if none found
                            }
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

                for (int i = 0; i < parts.Length; i++)
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

            foreach (var r in removalList)
            {
                s = s.Replace(r, "");
            }

            return s;
        }

        private static string ExtractFileNameWithPath(string fullPath)
        {
            // Get the file name with extension
            var fileName = Path.GetFileName(fullPath);

            // Find the index of the last backslash
            var lastBackslashIndex = fullPath.LastIndexOf(Path.DirectorySeparatorChar);

            // Extract the part of the path from the last backslash to the end
            var pathSection = fullPath.Substring(lastBackslashIndex + 1);

            return pathSection;
        }

        public void ExtractEnglishSubtitles(string mkvFilePath, string srtFilePath)
        {
            try
            {
                var mkvInfoPath = @"C:\Program Files\MKVToolNix\mkvinfo.exe"; // Full path to mkvinfo
                var mkvExtractPath = @"C:\Program Files\MKVToolNix\mkvextract.exe"; // Full path to mkvextract

                // Use local path (U:) instead of UNC path
                string localMkvFilePath = mkvFilePath.Replace(@"\\10.0.0.164\storage", @"U:");
                string localSrtFilePath = srtFilePath.Replace(@"\\10.0.0.164\storage", @"U:");

                // Step 1: Use mkvinfo to inspect the file and get subtitle track numbers
                var startInfo = new ProcessStartInfo
                {
                    FileName = mkvInfoPath,
                    Arguments = $"\"{localMkvFilePath}\"", // Use local path
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string mkvinfoOutput;
                using (var process = Process.Start(startInfo))
                {
                    mkvinfoOutput = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                }

                // Step 2: Parse mkvinfo output to find English subtitle tracks
                var trackPattern = new Regex(@"\+ Track\s*\|\s*\+ Track number: (\d+).*?Track type: subtitles", RegexOptions.Singleline);
                var languagePattern = new Regex(@"\+ Language: (\w{3})", RegexOptions.Singleline);
                var matches = trackPattern.Matches(mkvinfoOutput);

                var subtitleTracks = new List<(int trackId, bool isForced)>();

                foreach (Match match in matches)
                {
                    int trackNumber = int.Parse(match.Groups[1].Value); // Get the track number
                    bool isForced = !string.IsNullOrEmpty(match.Groups[2].Value); // Check if it's forced
                    subtitleTracks.Add((trackNumber, isForced));
                }

                // Step 3: Extract the identified English subtitle tracks using mkvextract
                foreach (var (trackId, isForced) in subtitleTracks)
                {
                    var subtitleOutputPath = isForced
                        ? Path.ChangeExtension(localMkvFilePath, "forced.srt") // Save forced subtitles as forced.srt
                        : localSrtFilePath; // Save regular subtitles as the same name as the file

                    var extractStartInfo = new ProcessStartInfo
                    {
                        FileName = mkvExtractPath,
                        Arguments = $"tracks \"{localMkvFilePath}\" {trackId - 1}:\"{subtitleOutputPath}\"", // Adjust the trackId for mkvextract
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var extractProcess = Process.Start(extractStartInfo))
                    {
                        extractProcess.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Subtitle extraction failed for {mkvFilePath}: {ex.Message}");
                // Safely continue without stopping the renaming process
            }
        }
    }
}
