using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using Newtonsoft.Json;
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

            // Extract the series name and episode details from the query
            var seriesName = Regex.Match(query, @"^(.+?)\s+s(\d{1,2})e(\d{1,2})$", RegexOptions.IgnoreCase).Groups[1].Value;
            var seasonNumber = int.Parse(Regex.Match(query, @"s(\d{1,2})e(\d{1,2})", RegexOptions.IgnoreCase).Groups[1].Value);
            var episodeNumber = int.Parse(Regex.Match(query, @"s(\d{1,2})e(\d{1,2})", RegexOptions.IgnoreCase).Groups[2].Value);

            await EnsureTokenAsync();

            var id = await GetTvdbIdAsync(query);

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

            //var searchUrl = $"{_configuration["TvDb:BaseUrl"]}/search/series?name={Uri.EscapeDataString(seriesName)}";
            //_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            //// Search for the series
            //var searchResponse = await _httpClient.GetAsync(searchUrl);
            //if (!searchResponse.IsSuccessStatusCode)
            //{
            //    _logger.LogError($"Search for series failed with status code: {searchResponse.StatusCode}");
            //    return null!;
            //}

            //var searchContent = await searchResponse.Content.ReadAsStringAsync();
            //var searchResult = JsonConvert.DeserializeObject<TvDbResponse>(searchContent);
            //if (searchResult == null || searchResult.Data == null || !searchResult.Data.Any())
            //{
            //    throw new Exception("Series not found.");
            //}

            //var seriesId = searchResult.Data.First().Id;

            //// Fetch episode information using the series ID
            //var episodeUrl = $"{_configuration["TvDb:BaseUrl"]}/series/{seriesId}/episodes/query?airedSeason={seasonNumber}&airedEpisode={episodeNumber}";
            //var episodeResponse = await _httpClient.GetAsync(episodeUrl);
            //if (!episodeResponse.IsSuccessStatusCode)
            //{
            //    _logger.LogError($"Search for episode failed with status code: {episodeResponse.StatusCode}");
            //    return null!;
            //}

            //var episodeContent = await episodeResponse.Content.ReadAsStringAsync();
            //var episodeResult = JsonConvert.DeserializeObject<TvDbEpisodeResponse>(episodeContent);
            //if (episodeResult == null || episodeResult.Data == null || !episodeResult.Data.Any())
            //{
            //    throw new Exception("Episode not found.");
            //}

            //return episodeResult;
        }

        public async Task<Root> GetEpisodeDetailsAsync(int id, string season, string episode)
        {
            _logger.LogInformation($"Started a episode search: {id}");
            if (token == null)
                await GetTvDbToken();
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

        public (string name, string type) ConstructTitle(string incomingName)
        {
            // Regex pattern to detect season and episode format (e.g., "s01e01")
            var episodePattern = @"\bs(\d{1,2})e(\d{1,2})\b";
            var match = Regex.Match(incomingName, episodePattern, RegexOptions.IgnoreCase);
            var returnValue = ("", "");

            if (match.Success)
            {
                // This is a TV show episode query
                var seasonNumber = match.Groups[1].Value;
                var episodeNumber = match.Groups[2].Value;

                // Assuming the rest of the string before "sXXeXX" is the series name
                var seriesName = incomingName.Substring(0, match.Index).Trim();

                returnValue = ($"{seriesName} S{seasonNumber}E{episodeNumber}", "series");
            }
            else
            {
                // This is a movie query
                // Assuming the movie query is in the format "Movie Name year"
                var yearPattern = @"\b(\d{4})\b";
                var yearMatch = Regex.Match(incomingName, yearPattern);

                if (yearMatch.Success)
                {
                    var year = yearMatch.Value;
                    var movieName = incomingName.Replace(year, "").Trim();

                    returnValue = ($"{movieName} ({year})", "movie");
                }
            }

            // Return the original query if it doesn't match either format
            return returnValue;
        }

        private async Task<(string tvDbId, bool isSeries)> GetTvdbIdAsync(string query)
        {
            _logger.LogInformation($"Started a search for TVDB ID: {query}");
            await EnsureTokenAsync();

            var searchUrl = $"{_configuration["TvDb:BaseUrl"]}/search?query={Uri.EscapeDataString(query)}";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Search for TVDB ID failed with status code: {response.StatusCode}");
                return (null, false);
            }

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<TbDbIdResponse>(content);

            if (searchResult != null && searchResult.Data.Any())
            {
                var tvdbId = searchResult.Data.First().TvdbId;
                var isSeries = searchResult.Data.First().Id;
                return (tvdbId, isSeries.Contains("series"));
            }

            _logger.LogError("No data found in TVDB search response.");
            return (null, false);
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

            if (extendedResponse != null && extendedResponse.Episodes.Any())
            {
                var episode = extendedResponse.Episodes
                    .FirstOrDefault(e => e.SeasonNumber == season && e.Number == episodeNumber);

                if (episode != null)
                {
                    return $"{episode.Name}";
                }
                else
                {
                    _logger.LogWarning($"Episode S{season}E{episodeNumber} not found in series ID {seriesId}.");
                }
            }

            return null!;
        }

        private TvDbResponse MapToResponse(SimplifiedTvDbResponse otherModel)
        {
            if (otherModel?.Data == null)
                return null!;

            return new TvDbResponse
            {
                Data = new List<TvDbData> {
                    new TvDbData() {
                        ObjectID = otherModel.Data.Episodes[0].ObjectId.ToString(),
                        Name = otherModel.Data.Episodes[0].Name,
                        Id = otherModel.Data.Series.Id.ToString(),
                        Type = "series",
                        ImageUrl = otherModel.Data.Series.ImageUrl,
                        Year = otherModel.Data.Series.Year
                    }
                }
            };
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
