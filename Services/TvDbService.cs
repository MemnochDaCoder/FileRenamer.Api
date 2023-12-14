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

        public async Task<string> GetToken()
        {
            var apiKey = _configuration["API:ApiKey"];
            var pin = _configuration["API:Pin"];
            var loginUrl = _configuration["API:BaseUrl"] + _configuration["API:LoginUrl"];

            var loginContent = new StringContent(JsonConvert.SerializeObject(new { apiKey, pin }), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(loginUrl, loginContent);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent) ?? throw new ArgumentException("JsonConvert returned null when trying to deserialize the token response.");
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
                    await GetToken();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving token.");
                    return null!;
                }
            }
                
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"https://api4.thetvdb.com/v4/search?query={query}");
            var content = await response.Content.ReadAsStringAsync();
            var returnModel = JsonConvert.DeserializeObject<TvDbResponse>(content) ?? throw new ArgumentNullException("The search result returned no data.");

            return returnModel;
        }

        public async Task<Root> GetEpisodeDetailsAsync(int id, string season, string episode)
        {
            _logger.LogInformation($"Started a episode search: {id}");
            if (token == null)
                await GetToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"https://api4.thetvdb.com/v4/series/{id}/episodes/default?page=1&season={int.Parse(season).ToString()}&episodeNumber={int.Parse(episode).ToString()}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Root>(content) ?? throw new ArgumentNullException("The search result returned no data for episodes.");
        }

        public async Task<MovieDetailModel> GetMovieDetailsAsync(int id)
        {
            if (token == null)
                await GetToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"https://api4.thetvdb.com/v4/movies/{id}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MovieDetailModel>(content) ?? throw new ArgumentNullException("The search result returned no data for movies.");
        }
    }

    public class TokenResponse
    {
        public required string Status { get; set; }
        public required TokenData Data { get; set; }

        public class TokenData
        {
            public required string Token { get; set; }
        }
    }
}
