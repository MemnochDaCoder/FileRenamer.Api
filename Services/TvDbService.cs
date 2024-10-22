using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace FileRenamer.Api.Services
{
    public class TvDbService : ITvDbService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private string token = null!;

        public TvDbService(IHttpClientFactory httpClientFactory, ILogger<TvDbService> logger, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _configuration = config;
        }

        public async Task<string> GetTvDbToken()
        {
            var apiKey = _configuration["TvDb:ApiKey"];
            var pin = _configuration["TvDb:Pin"];
            var loginUrl = _configuration["TvDb:BaseUrl"] + _configuration["TvDb:LoginUrl"];

            var loginContent = new StringContent(JsonConvert.SerializeObject(new { apiKey, pin }), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(loginUrl, loginContent);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TvDbTokenResponse>(responseContent) ?? throw new ArgumentException("JsonConvert returned null when trying to deserialize the token response.");
                token = tokenResponse.Data.Token;
                return token;
            }

            _logger.LogError("Unable to authenticate.");

            throw new Exception("Failed to authenticate.");
        }

        public async Task<TvDbEpisodeResponse> SearchShowsAndFetchEpisodeAsync(string query)
        {
            _logger.LogInformation($"Started a search: {query}");

            var seasonNumber = int.Parse(Regex.Match(query, @"s(\d{1,2})e(\d{1,2})", RegexOptions.IgnoreCase).Groups[1].Value);
            var episodeNumber = int.Parse(Regex.Match(query, @"s(\d{1,2})e(\d{1,2})", RegexOptions.IgnoreCase).Groups[2].Value);

            await EnsureTokenAsync();

            var id = await GetTvdbIdAsync(query, "series");

            if (id.isSeries)
            {
                var episodeName = await GetEpisodeNameAsync(id.tvDbId, seasonNumber, episodeNumber);
                return new TvDbEpisodeResponse
                {
                    Data = new List<EpisodeData>
                    {
                        new EpisodeData
                        {
                            Id = int.Parse(id.tvDbId),
                            AiredEpisodeNumber = episodeNumber,
                            AiredSeason = seasonNumber,
                            EpisodeName = episodeName
                        }
                    }
                };
            }
            else
            {
                throw new Exception("Episode not found.");
            }
        }

        public async Task<Root> GetEpisodeDetailsAsync(int id, string season, string episode)
        {
            _logger.LogInformation($"Started a episode search: {id}");
            await EnsureTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"{_configuration["TvDb:BaseUrl"]}/series/{id}/episodes/default?page=1&season={int.Parse(season)}&episodeNumber={int.Parse(episode)}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Root>(content) ?? throw new ArgumentNullException("The search result returned no data for episodes.");
        }

        public async Task<MovieDetailModel> GetMovieDetailsAsync(int id)
        {
            await EnsureTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"{_configuration["TvDb:BaseUrl"]}/movies/{id}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MovieDetailModel>(content) ?? throw new ArgumentNullException("The search result returned no data for movies.");
        }

        public async Task<(string name, string type)> ConstructTitle(string incomingName)
        {
            // Regex patterns to detect various season and episode formats for TV shows
            var episodePatterns = new List<string>
            {
                @"\bs(\d{1,2})e(\d{1,2})\b",         // S01E01 or s01e01
                @"\bs(\d{1,2})\s?\.?\s?e(\d{1,2})\b", // S01 E01 or S01.E01 or S01  E01
                @"(\d{1,2})x(\d{1,2})",              // 1x01 or 2x12
            };

            // Check for a year first to identify if it's likely a movie
            var yearPattern = @"\b(\d{4})\b";
            var yearMatch = Regex.Match(incomingName, yearPattern);

            // If we find a year and it seems like a movie title, prioritize that
            if (yearMatch.Success)
            {
                // Extract the movie name (everything before the year) and ignore everything after
                var movieName = incomingName.Substring(0, yearMatch.Index).Trim().Replace(".", " ");
                var formattedMovieName = $"{movieName} ({yearMatch.Value})";
                return (formattedMovieName, "movie");
            }

            // If no year is found or we are still considering it a series, proceed with episode pattern matching
            var match = episodePatterns
                .Select(pattern => Regex.Match(incomingName, pattern, RegexOptions.IgnoreCase))
                .FirstOrDefault(m => m.Success);

            if (match != null && match.Success)
            {
                // This is a TV show episode query
                var seasonNumber = match.Groups[1].Value.PadLeft(2, '0'); // Zero-pad to 2 digits if necessary
                var episodeNumber = match.Groups[2].Value.PadLeft(2, '0'); // Zero-pad to 2 digits if necessary

                // Extract the series name (before the season/episode pattern) and episode title (after)
                var seriesName = incomingName.Substring(0, match.Index).Trim();
                var episodeTitle = incomingName.Substring(match.Index + match.Length).Trim(); // Text after the pattern

                var tvDbId = await GetTvdbIdAsync(seriesName, "series");
                var episodeName = await GetEpisodeNameAsync(tvDbId.tvDbId, int.Parse(seasonNumber), int.Parse(episodeNumber));
                var finalName = episodeTitle != episodeName ? episodeName : episodeTitle;

                // Construct the full episode name with the title preserved
                var fullEpisodeName = string.IsNullOrWhiteSpace(episodeTitle)
                    ? $"{seriesName} S{seasonNumber}E{episodeNumber}" // If no episode title is present
                    : $"{seriesName} S{seasonNumber}E{episodeNumber} {finalName}"; // If episode title exists

                return (fullEpisodeName, "series");
            }

            // Fallback if no clear format is found
            return (incomingName.Trim(), "unknown");
        }

        public async Task<(string tvDbId, bool isSeries)> GetTvdbIdAsync(string query, string type)
        {
            _logger.LogInformation($"Started a search for TVDB ID: {query}");
            await EnsureTokenAsync();

            // Use the query as-is since it is already formatted
            if (type == "series")
            {
                var searchUrl = $"{_configuration["TvDb:BaseUrl"]}/search?query={Uri.EscapeDataString(query.Trim())}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Search for TVDB ID failed with status code: {response.StatusCode}");
                    return (null, false)!;
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonConvert.DeserializeObject<TbDbIdResponse>(content);

                if (searchResult != null && searchResult.Data.Any())
                {
                    var tvdbId = searchResult.Data.First().TvdbId ?? searchResult.Data.First().Id;
                    var isSeries = searchResult.Data.First().Id;
                    if (isSeries.Contains("series"))
                    {
                        tvdbId = isSeries.Replace("series-", "");
                    }

                    return (tvdbId, true)!;
                }
                else if (searchResult?.Data.Count == 0)
                {
                    var seriesName = query.Split(' ')[0];
                    var searchSeriesUrl = $"{_configuration["TvDb:BaseUrl"]}/search?query={seriesName}&type=series";
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var seriesNameResponse = await _httpClient.GetAsync(searchSeriesUrl);
                    if (!seriesNameResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Search for TVDB ID failed with status code: {seriesNameResponse.StatusCode}");
                        return (null, false)!;
                    }

                    var seriesNameContent = await seriesNameResponse.Content.ReadAsStringAsync();
                    var seriesNameSearchResult = JsonConvert.DeserializeObject<TbDbIdResponse>(seriesNameContent);
                    var tvDbId = seriesNameSearchResult?.Data.First().Id;

                    if (tvDbId != null)
                        return (tvDbId.Replace("series-", ""), true);
                    else
                        return (null, false);
                }
            }
            else if (type == "movie")
            {
                // For movies, extract the year if it exists in parentheses
                var yearMatch = Regex.Match(query, @"\((\d{4})\)");
                var year = yearMatch.Success ? yearMatch.Groups[1].Value : string.Empty;
                var movieName = yearMatch.Success ? query.Replace(yearMatch.Value, "").Trim() : query.Trim();

                var searchUrl = $"{_configuration["TvDb:BaseUrl"]}/search?query={Uri.EscapeDataString(movieName)}&year={year}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Search for TVDB ID failed with status code: {response.StatusCode}");
                    return (null, false)!;
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonConvert.DeserializeObject<TbDbIdResponse>(content);

                if (searchResult != null && searchResult.Data.Any())
                {
                    var tvdbId = searchResult.Data.First().Id.Replace("movie-", "");
                    return (tvdbId, false)!;
                }
            }

            _logger.LogError("No data found in TVDB search response.");
            return (null, false)!;
        }

        private async Task<string> GetEpisodeNameAsync(string seriesId, int season, int episodeNumber)
        {
            await EnsureTokenAsync();

            var extendedUrl = $"{_configuration["TvDb:BaseUrl"]}/series/{seriesId}/extended?meta=episodes&short=true";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(extendedUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to fetch series details with status code: {response.StatusCode}");
                return null!;
            }

            var content = await response.Content.ReadAsStringAsync();
            var extendedResponse = JsonConvert.DeserializeObject<TvDbExtendedResponse>(content);

            if (extendedResponse?.Data?.Episodes != null && extendedResponse.Data.Episodes.Any())
            {
                var episode = extendedResponse.Data.Episodes
                    .FirstOrDefault(e => e.SeasonNumber == season && e.Number == episodeNumber);

                if (episode != null)
                {
                    return episode.Name;
                }
                else
                {
                    _logger.LogWarning($"Episode S{season}E{episodeNumber} not found in series ID {seriesId}.");
                }
            }

            return null!;
        }

        private async Task EnsureTokenAsync()
        {
            if (string.IsNullOrEmpty(token))
            {
                await GetTvDbToken();
            }
        }
    }

    public class TvDbTokenResponse
    {
        public required string Status { get; set; }
        public required TokenData Data { get; set; }

        public class TokenData
        {
            public required string Token { get; set; }
        }
    }
}
