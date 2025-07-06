using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Straight2Trader
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Item> Items { get; set; }
        public double TotalCargoAmount => Items.Sum(item => item.TotalValue);

        private Dictionary<string, string> _itemDictionary;
        private Scraper _scraper;
        private Jokes _jokes = new Jokes();

        public MainWindow()
        {
            InitializeComponent();

            Items = new ObservableCollection<Item>();
            ItemListView.ItemsSource = Items;
            _scraper = new Scraper();

            LoadItems(); //do this so the highly complex UI *cough cough* can be process faster and first while scraping.
        }
        private async void LoadItems() => _itemDictionary = await _scraper.ScrapeItemsAsync();


        //Buttons
        private async void AddOrBestItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Name == "AddItemButton")
                {
                    string item = ItemTextBox.Text;
                    if (!int.TryParse(SCUTextBox.Text, out int scu) || scu <= 0) return;
                    if (string.IsNullOrWhiteSpace(item) || !_itemDictionary.ContainsKey(item)) return;

                    var (bestLocation, bestPriceString, _) = await _scraper.ScrapeSellInfo(item);
                    double.TryParse(bestPriceString.Replace(" aUEC", ""), out double bestPrice);

                    Items.Add(new Item
                    {
                        ItemName = item,
                        SCU = scu.ToString(),
                        SellLocation = bestLocation,
                        BestPrice = bestPrice,
                        TotalValue = scu * bestPrice
                    });

                    ItemTextBox.Clear();
                    SCUTextBox.Clear();
                }
                else if (btn.Name == "BestPriceButton")
                {
                    if (!Items.Any()) return;

                    foreach (var item in Items)
                    {
                        var (bestLocation, bestPriceString, _) = await _scraper.ScrapeSellInfo(item.ItemName);
                        double.TryParse(bestPriceString.Replace(" aUEC", ""), out double bestPrice);

                        item.SellLocation = bestLocation;
                        item.BestPrice = bestPrice;

                        if (double.TryParse(item.SCU, out double parsedSCU))
                        {
                            item.TotalValue = parsedSCU * bestPrice;
                        }
                    }
                }
                ItemListView.Items.Refresh();
                UpdateCargoValue();
            }
        }

        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ItemListView.SelectedItems.Cast<Item>().ToList();
            foreach (var item in selectedItems)
            {
                Items.Remove(item);
            }
            UpdateCargoValue();
        }

        private void OpenLinkButton_Click(object sender, RoutedEventArgs e)
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
                url = $"https://uexcorp.space/commodities/info/name/{formattedItemName}/tab/locations_buying/";
            }

            //opens link if a valid URL was found
            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private async void CombineSCU_Click(object sender, RoutedEventArgs e)
        {
            var combinedItems = new Dictionary<string, (int SCU, string SellLocation, double Price)>();

            foreach (var item in Items)
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

            Items.Clear();
            foreach (var entry in combinedItems)
            {
                string sellLocation = entry.Value.SellLocation;
                double bestPrice = entry.Value.Price;

                if (string.IsNullOrWhiteSpace(sellLocation) || sellLocation == "Unknown")
                {
                    var (bestLocation, priceString, _) = await _scraper.ScrapeSellInfo(entry.Key);
                    sellLocation = bestLocation;

                    if (double.TryParse(priceString.Replace(" aUEC", ""), out double parsedPrice))
                    {
                        bestPrice = parsedPrice;
                    }
                }
                Items.Add(new Item
                {
                    ItemName = entry.Key,
                    SCU = entry.Value.SCU.ToString(),
                    SellLocation = sellLocation,
                    BestPrice = bestPrice,
                    TotalValue = entry.Value.SCU * bestPrice
                });
            }
            ItemListView.Items.Refresh();
            UpdateCargoValue();
        }

        private void ClearCargo_Click(object sender, RoutedEventArgs e)
        {
            Items.Clear();
            UpdateCargoValue();
        }

        private void ReceiptTxt_Click(object sender, RoutedEventArgs e)
        {
            if (!Items.Any()) return;
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
            Directory.CreateDirectory(folderPath);
            string fileName = $"CargoListReceipt_{DateTime.Now:dd-MM-yyyy_HHmmss}.txt";
            string filePath = System.IO.Path.Combine(folderPath, fileName);
            using (var writer = new System.IO.StreamWriter(filePath))
            {
                foreach (var item in Items)
                {
                    writer.WriteLine($"{item.ItemName} - {item.SCU} SCU - {item.SellLocation} - {item.BestPrice:N0} aUEC - Total: {item.FormattedTotalValue}");
                }
                writer.WriteLine($"\n===== Total Cargo Value: {TotalCargoAmount:N0} aUEC ===== \n{_jokes.RandomMessage()}");
            }
        }
        //find best location base on user defined Largest SCU
        private async void OneLocationTakesAll_Click(object sender, RoutedEventArgs e)
        {
            if (!Items.Any()) return;

            int maxUserSCU = int.TryParse(SCUMaxSizeTextBox.Text, out int parsedMaxSCU) ? parsedMaxSCU : int.MaxValue;
            var itemSellData = new Dictionary<string, Dictionary<string, (double Price, int MaxSCU)>>();
            var locationItemCount = new Dictionary<string, int>();

            foreach (var item in Items)
            {
                var (_, _, allLocations) = await _scraper.ScrapeSellInfo(item.ItemName);
                itemSellData[item.ItemName] = allLocations;

            }
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


            foreach (var item in Items)
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
            ItemListView.Items.Refresh();
            UpdateCargoValue();
        }

        //this forces the SCU box and Largest SCU box to use numbers only
        //also holds the 1 - 32 index for the SCU crate sizes
        private void NumberTextBox_InputHandler(object sender, System.Windows.Input.InputEventArgs e)
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

        //for the auto comleption in the text box, also accepts item codes. AUDI will auto complete to Audio Visual Equipment.
        private void HandleAutoComplete(object sender, EventArgs e)
        {
            if (sender == ItemTextBox && e is TextChangedEventArgs)
            {
                string input = ItemTextBox.Text.ToLower();
                if (!string.IsNullOrWhiteSpace(input))
                {
                    var suggestions = _itemDictionary.Keys
                        .Where(k => k.ToLower().Contains(input))
                        .ToList();

                    AutoCompleteListBox.ItemsSource = suggestions;
                    AutoCompleteListBox.Visibility = suggestions.Any() ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    AutoCompleteListBox.Visibility = Visibility.Collapsed;
                }
            }
            else if (sender == AutoCompleteListBox && e is SelectionChangedEventArgs && AutoCompleteListBox.SelectedItem != null)
            {
                string selectedItem = AutoCompleteListBox.SelectedItem.ToString();
                ItemTextBox.TextChanged -= HandleAutoComplete;
                ItemTextBox.Text = string.Empty;
                ItemTextBox.Text = selectedItem;
                ItemTextBox.CaretIndex = ItemTextBox.Text.Length;
                ItemTextBox.Focus();
                ItemTextBox.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                ItemTextBox.TextChanged += HandleAutoComplete;
                AutoCompleteListBox.SelectedItem = null;
                AutoCompleteListBox.Visibility = Visibility.Collapsed;
                /*this is overly complex because if you add an item, then try add the same item again it shits itself.
                 *whats going on here is we remove TextChanged event to avoid infinite loop
                 *then clear and reset the text to force wpf to recognize somthings changed
                 *reattach the event
                 *then reset again to prevent it from getting stuck
                 *
                 *fucking dumb shit took far to long to figure out
                 */
            }
        }

        private void UpdateCargoValue()
        {
            double totalValue = Items.Sum(item => item.TotalValue);
            TotalCargoValue.Content = $"aUEC: {totalValue:N0}";
        }



    }
    public class Item
    {
        public string ItemName { get; set; }
        public string SCU { get; set; }
        public string SellLocation { get; set; }
        public double BestPrice { get; set; }

        private double _totalValue;
        public double TotalValue
        {
            get => _totalValue;
            set => _totalValue = value;
        }
        public string FormattedTotalValue => _totalValue.ToString("N0");
    }
}
