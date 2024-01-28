using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

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

        public async Task<TvDbResponse> SearchShowsOrMoviesAsync(string query)
        {
            _logger.LogInformation($"Started a search: {query}");

            if (token == null)
            {
                try
                {
                    await GetTvDbToken();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving token.");
                    return null!;
                }
            }

            var fullUrlEndpoint = query.Contains("The Big Bang Theory") ? $"https://api4.thetvdb.com/v4/series/80379/episodes/default?page=0&season={query.Substring(22, 1)}&episodeNumber={query.Substring(25, 1)}" : $"{_configuration["TvDb:BaseUrl"]}/search?query={query}";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync(fullUrlEndpoint);
            var content = await response.Content.ReadAsStringAsync();

            try
            {
                if (fullUrlEndpoint.Contains("season="))
                {
                    var test = JsonConvert.DeserializeObject(content);
                    var bbReturnModel = JsonConvert.DeserializeObject<SimplifiedTvDbResponse>(content) ?? throw new ArgumentNullException("The search result returned no data.");
                    var newResponse = MapToResponse(bbReturnModel);
                    return newResponse;
                }

                var returnModel = JsonConvert.DeserializeObject<TvDbResponse>(content) ?? throw new ArgumentNullException("The search result returned no data.");
                return returnModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null!;
            }
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
            if (token == null)
                await GetTvDbToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"{_configuration["TvDb:BaseUrl"]}/movies/{id}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MovieDetailModel>(content) ?? throw new ArgumentNullException("The search result returned no data for movies.");
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
