using Newtonsoft.Json;
using System.Text;
using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Utils;
using FileRenamer.Api.Models;
using System.Net.Http.Headers;

namespace FileRenamer.Api.Services
{
    public class OpenSubtitlesService : IOpenSubtitlesService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private string token = null!;

        public OpenSubtitlesService(IHttpClientFactory httpClientFactory, ILogger<IOpenSubtitlesService> logger, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _configuration = config;
        }

        public async Task<string> GetToken()
        {
            var apiKey = _configuration["OpenSubs:Key"];
            var userName = _configuration["OpenSubs:UserName"];
            var password = _configuration["OpenSubs:Password"];
            var loginUrl = _configuration["OpenSubs:LoginEndpoint"];

            var loginContent = new StringContent(JsonConvert.SerializeObject(new { userName, password}), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(loginUrl, loginContent);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<SubtitleTokenResponse>(responseContent) ?? throw new ArgumentException("JsonConvert returned null when trying to deserialize the token response.");

                if(tokenResponse.Status != "200")
                {
                    _logger.LogError("There was an error getting the token.");
                    throw new Exception("There was an issue retrieving the token.");
                }

                token = tokenResponse.Data.Token;
                return token;
            }

            _logger.LogError("Unable to authenticate.");

            throw new Exception("Failed to authenticate.");
        }

        public async Task<SubtitleSearchResult> SearchSubtitlesAsync(string title)
        {
            if (token == null)
                await GetToken();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"https://api.opensubtitles.com/api/v1/subtitles?language=english&moviehash={HashFile(title)}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error getting the subtitles: {response.StatusCode}");
                throw new Exception($"Error getting the subtitles: {response.StatusCode}");
            }

            // Parse the response
            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<SubtitleSearchResult>(content);

            return searchResult!;
        }

        private string HashFile(string name)
        {
            return HashingHelper.ComputeHashInLowerCase(name, HashingClassAlgorithms.MD5);
        }
    }

    public class SubtitleTokenResponse
    {
        public required string Status { get; set; }
        public required TokenData Data { get; set; }

        public class TokenData
        {
            public required string Token { get; set; }
            public string? Status { get; set; }
        }
    }
}
