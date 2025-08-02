using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Straight2Trader
{
    internal static class Buttons
    {
        public static async Task AddOrBestItem_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Name == "AddItemButton")
                {
                    string item = main.ItemTextBox.Text;
                    if (!int.TryParse(main.SCUTextBox.Text, out int scu) || scu <= 0) return;
                    if (string.IsNullOrWhiteSpace(item) || !main._itemDictionary.ContainsKey(item)) return;

                    var (bestLocation, bestPriceString, _) = await main._scraper.ScrapeSellInfo(item);
                    double bestPrice = ParsePrice(bestPriceString);

                    main.Items.Add(new Item
                    {
                        ItemName = item,
                        SCU = scu.ToString(),
                        SellLocation = bestLocation,
                        BestPrice = bestPrice,
                        TotalValue = scu * bestPrice
                    });

                    main.ItemTextBox.Clear();
                    main.SCUTextBox.Clear();
                }

                //diff button
                else if (btn.Name == "BestPriceButton")
                {
                    if (!main.Items.Any()) return;

                    foreach (var item in main.Items)
                    {
                        var (bestLocation, bestPriceString, _) = await main._scraper.ScrapeSellInfo(item.ItemName);
                        double bestPrice = ParsePrice(bestPriceString);

                        item.SellLocation = bestLocation;
                        item.BestPrice = bestPrice;

                        if (double.TryParse(item.SCU, out double parsedSCU))
                        {
                            item.TotalValue = parsedSCU * bestPrice;
                        }
                    }
                }
                main.ItemListView.Items.Refresh();
                main.UpdateCargoValue();
            }
        }

        public static async Task CombineSCU_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            var combinedItems = new Dictionary<string, (int SCU, string SellLocation, double Price)>();

            foreach (var item in main.Items)
            {
                int scuValue = int.Parse(item.SCU);

                if (combinedItems.ContainsKey(item.ItemName))
                {
                    combinedItems[item.ItemName] = (
                        combinedItems[item.ItemName].SCU + scuValue,
                        string.IsNullOrWhiteSpace(combinedItems[item.ItemName].SellLocation) ? item.SellLocation : combinedItems[item.ItemName].SellLocation,
                        combinedItems[item.ItemName].Price
                    );
                }
                else
                {
                    combinedItems[item.ItemName] = (scuValue, item.SellLocation, item.BestPrice);
                }
            }

            main.Items.Clear();
            foreach (var entry in combinedItems)
            {
                string sellLocation = entry.Value.SellLocation;
                double bestPrice = entry.Value.Price;

                if (string.IsNullOrWhiteSpace(sellLocation) || sellLocation == "Unknown")
                {
                    var (bestLocation, priceString, _) = await main._scraper.ScrapeSellInfo(entry.Key);
                    sellLocation = bestLocation;

                    bestPrice = ParsePrice(priceString);
                }
                main.Items.Add(new Item
                {
                    ItemName = entry.Key,
                    SCU = entry.Value.SCU.ToString(),
                    SellLocation = sellLocation,
                    BestPrice = bestPrice,
                    TotalValue = entry.Value.SCU * bestPrice
                });
            }
            main.ItemListView.Items.Refresh();
            main.UpdateCargoValue();
        }

        //helper for removing aUEC from the price string
        private static double ParsePrice(string priceString)
        {
            double.TryParse(priceString.Replace(" aUEC", "").Trim(), out double price);
            return price;
        }

        public static void RemoveItemButton_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            var selectedItems = main.ItemListView.SelectedItems.Cast<Item>().ToList();
            foreach (var item in selectedItems)
            {
                main.Items.Remove(item);
            }
            main.UpdateCargoValue();
        }

        public static async Task OpenLinkButton_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            string url = null;
            //tag is for the quick links at the bottom
            if (sender is Button button && button.Tag is string buttonUrl)
            {
                url = buttonUrl;
            }
            //for the items, sends directly to that page
            else if (sender is TextBlock textBlock && textBlock.DataContext is Item item)
            {
                string formattedItemName = item.ItemName.ToLower()
                                            .Replace(" ", "-") //way its url is on ucx
                                            .Replace("'", ""); //litterally for e'tam
                url = $"{Scraper.BaseUrl}{formattedItemName}/tab/locations_buying/";
            }
            //opens link if a valid URL was found
            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        public static async Task ReceiptTxt_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            if (!main.Items.Any()) return;
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
            Directory.CreateDirectory(folderPath);
            string fileName = $"CargoListReceipt_{DateTime.Now:dd-MM-yyyy_HHmmss}.txt";
            string filePath = System.IO.Path.Combine(folderPath, fileName);
            using (var writer = new System.IO.StreamWriter(filePath))
            {
                foreach (var item in main.Items)
                {
                    writer.WriteLine($"{item.ItemName} - {item.SCU} SCU - {item.SellLocation} - {item.BestPrice:N0} aUEC - Total: {item.FormattedTotalValue}");
                }
                writer.WriteLine($"\n===== Total Cargo Value: {main.TotalCargoAmount:N0} aUEC ===== \n{main._jokes.RandomMessage()}");
            }
        }

        //find best location base on user defined Largest SCU
        public static async Task OneLocationTakesAll_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            if (!main.Items.Any()) return;

            int maxUserSCU = int.TryParse(main.SCUMaxSizeTextBox.Text, out int parsedMaxSCU) ? parsedMaxSCU : int.MaxValue;
            var itemSellData = new Dictionary<string, Dictionary<string, (double Price, int MaxSCU)>>();
            var locationItemCount = new Dictionary<string, int>();

            //launch all scraper tasks in parallel
            var tasks = main.Items.Select(item =>
                main._scraper.ScrapeSellInfo(item.ItemName)
                    .ContinueWith(t => (item.ItemName, Result: t.Result))
            ).ToList();

            var results = await Task.WhenAll(tasks);

            //populate itemSellData from all results
            foreach (var (itemName, (_, _, allLocations)) in results)
            {
                itemSellData[itemName] = allLocations;
            }

            //check best locations as before
            foreach (var sellData in itemSellData.Values)
            {
                foreach (var kvp in sellData)
                {
                    string location = kvp.Key;
                    int locationMaxSCU = kvp.Value.MaxSCU;

                    if (locationMaxSCU >= maxUserSCU)
                    {
                        if (locationItemCount.TryGetValue(location, out int count))
                            locationItemCount[location] = count + 1;
                        else
                            locationItemCount[location] = 1;
                    }
                }
            }

            if (!locationItemCount.Any()) return;
            string bestLocation = locationItemCount.OrderByDescending(l => l.Value).First().Key;

            //assign results back to items
            foreach (var item in main.Items)
            {
                if (itemSellData.TryGetValue(item.ItemName, out var sellData) &&
                    sellData.TryGetValue(bestLocation, out var locationData) &&
                    double.TryParse(item.SCU, out double parsedSCU))
                {
                    item.SellLocation = bestLocation;
                    item.BestPrice = locationData.Price;
                    item.TotalValue = parsedSCU * item.BestPrice;
                }
            }
            main.ItemListView.Items.Refresh();
            main.UpdateCargoValue();
        }

        //this forces the SCU box and Largest SCU box to use numbers only
        //also holds the 1 - 32 index for the SCU crate sizes
        public static void NumberTextBox_InputHandler(MainWindow main, object sender, System.Windows.Input.InputEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                //numbers only chck
                if (e is System.Windows.Input.TextCompositionEventArgs textEvent)
                {
                    e.Handled = !int.TryParse(textEvent.Text, out _);
                    return;
                }

                int[] scuBoxSizes = { 1, 2, 4, 8, 16, 24, 32 };

                //muse wheel scrolling
                if (e is System.Windows.Input.MouseWheelEventArgs wheelEvent)
                {
                    // Ensure the text box contains a valid number
                    if (!int.TryParse(textBox.Text, out int value))
                        value = 1;

                    if (textBox.Name == "SCUMaxSizeTextBox") //use predefinded box sizes upto 32SCU
                    {
                        int index = Array.IndexOf(scuBoxSizes, value);
                        if (index >= 0)
                        {
                            value = wheelEvent.Delta > 0 && index < scuBoxSizes.Length - 1 ? scuBoxSizes[index + 1] :
                                    wheelEvent.Delta < 0 && index > 0 ? scuBoxSizes[index - 1] : value;
                            //im tired this took too much thought at 1am...
                        }
                    }
                    else //normal numeric input for other text boxes
                    {
                        value = wheelEvent.Delta > 0 ? value + 1 : Math.Max(1, value - 1);
                    }
                    textBox.Text = value.ToString();
                }
            }
        }

        //for the auto comleption in the text box.
        public static void HandleAutoComplete(MainWindow main, object sender, EventArgs e)
        {
            if (sender == main.ItemTextBox && e is TextChangedEventArgs)
            {
                string input = main.ItemTextBox.Text.ToLower();
                if (!string.IsNullOrWhiteSpace(input))
                {
                    var suggestions = main._itemDictionary.Keys
                        .Where(k => k.ToLower().Contains(input))
                        .ToList();

                    main.AutoCompleteListBox.ItemsSource = suggestions;
                    main.AutoCompleteListBox.Visibility = suggestions.Any() ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    main.AutoCompleteListBox.Visibility = Visibility.Collapsed;
                }
            }
            else if (sender == main.AutoCompleteListBox && e is SelectionChangedEventArgs && main.AutoCompleteListBox.SelectedItem != null)
            {
                string selectedItem = main.AutoCompleteListBox.SelectedItem.ToString();

                //useing guard to prevent infiite loops.
                main.ItemTextBox.Text = string.Empty;
                main.ItemTextBox.Text = selectedItem;
                main.ItemTextBox.CaretIndex = main.ItemTextBox.Text.Length;
                main.ItemTextBox.Focus();
                main.ItemTextBox.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                main.AutoCompleteListBox.SelectedItem = null;
                main.AutoCompleteListBox.Visibility = Visibility.Collapsed;
            }
        }

        public static async Task ClearCargo_Click(MainWindow main, object sender, RoutedEventArgs e)
        {
            main.Items.Clear();
            main.UpdateCargoValue();
        }
    }
}
