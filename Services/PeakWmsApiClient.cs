using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StorePitOne.Models;

namespace StorePitOne.Services
{
    public class PeakWmsApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PeakWmsApiClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public class RawApiPage
        {
            public string Url { get; set; } = "";
            public int StatusCode { get; set; }
            public int? TotalRecords { get; set; }
            public string RawJson { get; set; } = "";
        }

        public async Task<List<RawApiPage>> GetStockActionRawPagesAsync(
            string apiKey,
            DateTime fromTime,
            DateTime toTime)
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var pages = new List<RawApiPage>();

            var from = fromTime.ToString("yyyy-MM-ddTHH:mm:ss");
            var to = toTime.ToString("yyyy-MM-ddTHH:mm:ss");

            var lastId = 0;
            const int pageSize = 100;

            while (true)
            {
                var url =
                    $"api/integration/v1/stockAction" +
                    $"?lastId={lastId}" +
                    $"&pageSize={pageSize}" +
                    $"&fromTime={from}" +
                    $"&toTime={to}";

                var response = await client.GetAsync(url);
                var raw = await response.Content.ReadAsStringAsync();

                pages.Add(new RawApiPage
                {
                    Url = url,
                    StatusCode = (int)response.StatusCode,
                    RawJson = raw,
                    TotalRecords = TryGetTotalRecords(raw)
                });

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"StockAction raw sync stoppede med status {(int)response.StatusCode}");
                    break;
                }

                var result = JsonSerializer.Deserialize<PagedResponse<PeakStockActionDto>>(
                    raw,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                var page = result?.Data ?? new List<PeakStockActionDto>();

                if (page.Count == 0)
                {
                    break;
                }

                var highestId = page.Max(x => x.Id);

                if (highestId <= lastId)
                {
                    break;
                }

                lastId = highestId;

                Console.WriteLine($"StockAction raw pagination: hentet side med {page.Count}, lastId {lastId}");

                if (page.Count < pageSize)
                {
                    break;
                }
            }

            return pages;
        }

        public async Task<List<PeakStockActionDto>> GetStockActionsAsync(DateTime fromTime, DateTime toTime)
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            return await GetStockActionsPaged(client, fromTime, toTime);
        }

        public async Task<List<PeakStockActionDto>> GetStockActionsAsync(
            string apiKey,
            DateTime fromTime,
            DateTime toTime)
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            return await GetStockActionsPaged(client, fromTime, toTime);
        }

        private async Task<List<PeakStockActionDto>> GetStockActionsPaged(
            HttpClient client,
            DateTime fromTime,
            DateTime toTime)
        {
            var all = new List<PeakStockActionDto>();

            var from = fromTime.ToString("yyyy-MM-ddTHH:mm:ss");
            var to = toTime.ToString("yyyy-MM-ddTHH:mm:ss");

            var lastId = 0;
            const int pageSize = 100;

            while (true)
            {
                var url =
                    $"api/integration/v1/stockAction" +
                    $"?lastId={lastId}" +
                    $"&pageSize={pageSize}" +
                    $"&fromTime={from}" +
                    $"&toTime={to}";

                var result = await client.GetFromJsonAsync<PagedResponse<PeakStockActionDto>>(url);

                var page = result?.Data ?? new List<PeakStockActionDto>();

                if (page.Count == 0)
                {
                    break;
                }

                all.AddRange(page);

                var highestId = page.Max(x => x.Id);

                if (highestId <= lastId)
                {
                    break;
                }

                lastId = highestId;

                Console.WriteLine($"StockAction pagination: hentet {all.Count} indtil lastId {lastId}");

                if (page.Count < pageSize)
                {
                    break;
                }
            }

            return all;
        }

        public async Task<List<PeakProductDto>> GetProductsAsync()
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            var url =
                $"api/integration/v1/product" +
                $"?lastId=0" +
                $"&pageSize=200";

            var result = await client.GetFromJsonAsync<PagedResponse<PeakProductDto>>(url);

            return result?.Data ?? new List<PeakProductDto>();
        }

        public async Task<List<PeakProductDto>> GetProductsAsync(string apiKey)
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var url =
                $"api/integration/v1/product" +
                $"?lastId=0" +
                $"&pageSize=200";

            var response = await client.GetAsync(url);

            var raw = await response.Content.ReadAsStringAsync();

            Console.WriteLine("----- PRODUCT RAW START -----");
            Console.WriteLine(raw);
            Console.WriteLine("----- PRODUCT RAW END -----");

            var result = JsonSerializer.Deserialize<PagedResponse<PeakProductDto>>(
                raw,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            return result?.Data ?? new List<PeakProductDto>();
        }

        public async Task<List<PeakStockDto>> GetStockAsync(string apiKey)
        {
            var client = _httpClientFactory.CreateClient("PeakWMS");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var url =
                $"api/integration/v1/stock" +
                $"?lastId=0" +
                $"&pageSize=200";

            var result = await client.GetFromJsonAsync<PagedResponse<PeakStockDto>>(url);

            return result?.Data ?? new List<PeakStockDto>();
        }

        private static int? TryGetTotalRecords(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);

                if (doc.RootElement.TryGetProperty("totalRecords", out var totalRecords) &&
                    totalRecords.ValueKind == JsonValueKind.Number &&
                    totalRecords.TryGetInt32(out var value))
                {
                    return value;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}