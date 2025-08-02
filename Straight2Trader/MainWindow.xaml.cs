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

        internal Dictionary<string, string> _itemDictionary;
        internal Scraper _scraper;
        internal readonly Jokes _jokes = new Jokes();

        public MainWindow()
        {
            InitializeComponent();

            Items = new ObservableCollection<Item>();
            ItemListView.ItemsSource = Items;
            _scraper = new Scraper();

            ItemTextBox.TextChanged += (s, e) => Buttons.HandleAutoComplete(this, s, e);
            AutoCompleteListBox.SelectionChanged += (s, e) => Buttons.HandleAutoComplete(this, s, e);

            LoadItems();
        }


        private async void LoadItems() => _itemDictionary = await _scraper.ScrapeItemsAsync();

        //Buttons.cs
        private async void AddOrBestItem_Click(object s, RoutedEventArgs e) => Buttons.AddOrBestItem_Click(this, s, e);
        private void RemoveItemButton_Click(object s, RoutedEventArgs e) => Buttons.RemoveItemButton_Click(this, s, e);
        private async void OpenLinkButton_Click(object s, RoutedEventArgs e) => Buttons.OpenLinkButton_Click(this, s, e);
        private async void CombineSCU_Click(object s, RoutedEventArgs e) => Buttons.CombineSCU_Click(this, s, e);
        private async void ReceiptTxt_Click(object s, RoutedEventArgs e) => Buttons.ReceiptTxt_Click(this, s, e);
        private async void OneLocationTakesAll_Click(object s, RoutedEventArgs e) => Buttons.OneLocationTakesAll_Click(this, s, e);
        private void NumberTextBox_InputHandler(object s, System.Windows.Input.InputEventArgs e) => Buttons.NumberTextBox_InputHandler(this, s, e);
        private void HandleAutoComplete(object s, EventArgs e) => Buttons.HandleAutoComplete(this, s, e);
        private async void ClearCargo_Click(object s, RoutedEventArgs e) => Buttons.ClearCargo_Click(this, s, e);

        //staying in MainWindow.cs because it updates the UI directly. gets called from Buttons.cs many times after item changes.
        internal async void UpdateCargoValue() => TotalCargoValue.Content = $"aUEC: {Items.Sum(item => item.TotalValue):N0}";
    }
    public class Item
    {
        public string ItemName { get; set; }
        public string SCU { get; set; }
        public string SellLocation { get; set; }
        public double BestPrice { get; set; }
        public double TotalValue { get; set; }
        public string FormattedTotalValue => TotalValue.ToString("N0");
    }
}
