using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Straight2Trader
{
    public class Scraper
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private const string BaseUrl = "https://uexcorp.space/commodities/info/name/";

        //fetch HTML
        private async Task<HtmlDocument> ScrapeHtmlAsync(string url)
        {
            var htmlDocument = new HtmlDocument();
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                htmlDocument.LoadHtml(response);
            }
            catch
            {
                htmlDocument.LoadHtml("<html></html>"); //to avoid null issues, maybe the page was down
            }
            return htmlDocument;
        }

        //scrape all commodity names from the resources page
        public async Task<Dictionary<string, string>> ScrapeItemsAsync()
        {
            const string url = "https://uexcorp.space/resources/home";
            var htmlDocument = await ScrapeHtmlAsync(url);

            var itemNodes = htmlDocument.DocumentNode.SelectNodes("//table/tbody/tr/td[2]");

            return itemNodes != null
                ? itemNodes
                    .Select(n => n.InnerText.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(name => name, name => name)
                : new Dictionary<string, string>();
        }

        //scrape locations and prices
        public async Task<(string BestLocation, string BestPrice, Dictionary<string, (double Price, int MaxSCU)> AllLocations)> ScrapeSellInfo(string itemName)
        {
            var html = await ScrapeHtmlAsync($"{BaseUrl}{FormatItemName(itemName)}/tab/locations_buying/");
            var rows = html.DocumentNode.SelectNodes("//*[@id='table-sell']/tbody/tr");

            var locations = new Dictionary<string, (double Price, int MaxSCU)>();

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var location = row.SelectSingleNode("./td[1]")?.InnerText.Trim();
                    var priceText = row.SelectSingleNode("./td[11]")?.InnerText.Trim();
                    var maxSCUText = row.SelectSingleNode("./td[16]")?.GetAttributeValue("data-value", "").Trim();

                    if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(priceText))
                        continue;

                    var price = ParsePrice(priceText);
                    var maxSCU = ParseMaxSCU(maxSCUText);

                    if (!locations.ContainsKey(location))
                        locations[location] = (price, maxSCU);
                }
            }

            // Find best location by highest price
            var best = locations.OrderByDescending(x => x.Value.Price).FirstOrDefault();
            string bestLocation = best.Key ?? "No Sell Location Found";
            string bestPrice = best.Key == null ? "N/A" : $"{best.Value.Price:N0}";

            return (bestLocation, bestPrice, locations);
        }


        //some helper methods
        private static List<string> ExtractNodes(HtmlDocument doc, string xpath) =>
            doc.DocumentNode.SelectNodes(xpath)?.Select(n => n.InnerText).ToList() ?? new List<string>();

        private static double ParsePrice(string priceText) =>
            double.TryParse(priceText.Replace(" aUEC", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out double price)
                ? price : 0.0;

        private static int ParseMaxSCU(string scuText)
        {
            if (string.IsNullOrWhiteSpace(scuText)) return int.MaxValue;
            var parts = scuText.Split('-');
            return int.TryParse(parts.Last(), out int maxSCU) ? maxSCU : int.MaxValue;
        }

        private static string FormatItemName(string itemName) =>
            itemName.ToLower().Replace(" ", "-").Replace("'", "");
    }
}
