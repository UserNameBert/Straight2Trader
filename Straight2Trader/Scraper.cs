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

        //scrape all commodity names and codes from the resources page
        public async Task<Dictionary<string, string>> ScrapeItemsAsync()
        {
            const string url = "https://uexcorp.space/resources/home";
            var htmlDocument = await ScrapeHtmlAsync(url);

            var itemNodes = htmlDocument.DocumentNode.SelectNodes("//table/tbody/tr/td[2]");
            var codeNodes = htmlDocument.DocumentNode.SelectNodes("//table/tbody/tr/td[3]");

            //we do codes for some items like AUDI *Audio Visual Equipment.

            return itemNodes != null && codeNodes != null && itemNodes.Count == codeNodes.Count
                ? itemNodes.Zip(codeNodes, (item, code) => new { item, code })
                          .ToDictionary(x => x.item.InnerText.Trim(), x => x.code.InnerText.Trim())
                : new Dictionary<string, string>();
        }

        //scrape locations and prices
        public async Task<(string BestLocation, string BestPrice)> ScrapeSpecificItemsInfo(string itemName)
        {
            var htmlDocument = await ScrapeHtmlAsync($"{BaseUrl}{FormatItemName(itemName)}/tab/locations_buying/");
            var locations = ExtractNodes(htmlDocument, "//*[@id='table-sell']/tbody/tr/td[1]");
            var prices = ExtractNodes(htmlDocument, "//*[@id='table-sell']/tbody/tr/td[11]");

            if (locations.Count == 0 || prices.Count == 0 || locations.Count != prices.Count)
                return ("No Sell Location Found", "N/A");

            var result = locations.Zip(prices, (location, priceText) =>
                new { location = location.Trim(), price = ParsePrice(priceText) })
                .OrderByDescending(x => x.price)
                .Select(x => (x.location, $"{x.price:N0}"))
                .FirstOrDefault();

            return result == default ? ("Unknown", "0") : result;
        }

        //i couldnt be bothered to refactor this into the code above, this part came late, sorry im lazy
        public async Task<Dictionary<string, (double Price, int MaxSCU)>> ScrapeAllSellLocationsWithSCULimits(string itemName)
        {
            var htmlDocument = await ScrapeHtmlAsync($"{BaseUrl}{FormatItemName(itemName)}/tab/locations_buying/");
            var rows = htmlDocument.DocumentNode.SelectNodes("//*[@id='table-sell']/tbody/tr");

            return rows?
                .Select(row =>
                {
                    var location = row.SelectSingleNode("./td[1]")?.InnerText.Trim();
                    var priceText = row.SelectSingleNode("./td[11]")?.InnerText.Trim();
                    var maxSCUText = row.SelectSingleNode("./td[16]")?.GetAttributeValue("data-value", "").Trim();

                    if (location != null && !string.IsNullOrEmpty(priceText))
                        return new { location, price = ParsePrice(priceText), maxSCU = ParseMaxSCU(maxSCUText) };
                    return null;
                })
                .Where(x => x != null)
                .ToDictionary(x => x.location, x => (x.price, x.maxSCU)) ?? new Dictionary<string, (double, int)>();
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
