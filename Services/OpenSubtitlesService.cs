using Newtonsoft.Json;
using System.Text;
using FileRenamer.Api.Interfaces;
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
            var loginUrl = _configuration["OpenSubs:LoginEndpoint"];

            var loginData = new
            {
                username = _configuration["OpenSubs:UserName"],
                password = _configuration["OpenSubs:Password"]
            };

            var loginContent = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("Api-Key", apiKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("filerenamer.api v1.0");

            var response = await _httpClient.PostAsync(loginUrl, loginContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Authentication failed with status code {response.StatusCode}: {errorContent}");
                throw new Exception($"Failed to authenticate. Response: {errorContent}");
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<SubtitleTokenResponse>(responseContent);
                if (tokenResponse?.Status != "200")
                {
                    _logger.LogError("There was an error getting the token.");
                    throw new Exception("There was an issue retrieving the token.");
                }

                token = tokenResponse?.Data?.Token!;
                return token;
            }

            _logger.LogError("Unable to authenticate.");
            throw new Exception("Failed to authenticate.");
        }

        public async Task<SubtitleSearchResult> SearchSubtitlesAsync(string title)
        {
            if (token == null)
                await GetToken();

            if(title == null)
            {
                return null!;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"https://api.opensubtitles.com/api/v1/subtitles?language=en&query={title}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error getting the subtitles: {response.StatusCode}");
                throw new Exception($"Error getting the subtitles: {response.StatusCode}");
            }

            // Parse the response
            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<SubtitleSearchResult>(content);

            // Filter results where from_trusted is true and sort by download count
            var filteredResults = searchResult?.data?
                .Where(d => d.attributes != null && d.attributes.from_trusted)
                .OrderByDescending(d => d?.attributes?.download_count)
                .ToList();

            // If no trusted results, sort all results by download count
            if (filteredResults != null && !filteredResults.Any())
            {
                filteredResults = searchResult?.data?
                    .OrderByDescending(d => d.attributes != null ? d.attributes.download_count : 0)
                    .ToList();
            }

            // Take the first result or null if no results
            var firstResult = filteredResults?.FirstOrDefault();

            // Return a new SubtitleSearchResult with only the first result
            return new SubtitleSearchResult
            {
                total_pages = firstResult != null ? 1 : 0,
                total_count = firstResult != null ? 1 : 0,
                per_page = 1,
                page = 1,
                data = firstResult != null ? new List<Datum> { firstResult } : new List<Datum>()
            };
        }

        public async Task DownloadSubtitle(string subtitleId, string newFileName, string filePath)
        {
            var downloadUrl = "https://api.opensubtitles.com/api/v1/download";
            var requestData = new { file_id = subtitleId };

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("filerenamer.api v1.0");

            var response = await httpClient.PostAsync(downloadUrl, new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var downloadResponse = JsonConvert.DeserializeObject<DownloadResponse>(content);

                if (!string.IsNullOrEmpty(downloadResponse?.link))
                {
                    await DownloadFile(downloadResponse.link, newFileName, filePath);
                }
            }
        }

        private async Task DownloadFile(string fileUrl, string newFileName, string newFilePath)
        {
            using var httpClient = new HttpClient();
            var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
            await System.IO.File.WriteAllBytesAsync($"{newFilePath}/{newFileName}.srt", fileBytes);
        }

        public class DownloadResponse
        {
            public string? link { get; set; }
            public string? file_name { get; set; }
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
