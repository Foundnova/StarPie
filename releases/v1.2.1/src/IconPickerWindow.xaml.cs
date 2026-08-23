using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WinPieGestures
{
    public partial class IconPickerWindow : Window
    {
        public string? SelectedIconKey { get; private set; }
        private Border? _selectedCard;

        public IconPickerWindow(string? initialKey = null)
        {
            InitializeComponent();
            SelectedIconKey = initialKey;
            PopulateIcons();
        }

        private void PopulateIcons(string filter = "")
        {
            IconsWrapPanel.Children.Clear();
            _selectedCard = null;

            var items = IconHelper.VectorIconList;
            if (!string.IsNullOrEmpty(filter))
            {
                items = items.Where(i => 
                    i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) || 
                    i.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    i.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            foreach (var item in items)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(4),
                    Padding = new Thickness(6),
                    Cursor = Cursors.Hand,
                    Tag = item
                };

                var sp = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var path = new Path
                {
                    Data = Geometry.Parse(item.SvgData),
                    Fill = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var tb = new TextBlock
                {
                    Text = item.Key,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 72,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                sp.Children.Add(path);
                sp.Children.Add(tb);
                card.Child = sp;

                // Selection check
                if (string.Equals(SelectedIconKey, item.Key, StringComparison.OrdinalIgnoreCase))
                {
                    SelectCard(card, item);
                }

                card.MouseLeftButtonDown += (s, e) =>
                {
                    SelectCard(card, item);
                    if (e.ClickCount == 2)
                    {
                        Confirm_Click(this, new RoutedEventArgs());
                    }
                };

                IconsWrapPanel.Children.Add(card);
            }
        }

        private void SelectCard(Border card, VectorIconItem item)
        {
            if (_selectedCard != null)
            {
                _selectedCard.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                _selectedCard.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            }

            _selectedCard = card;
            SelectedIconKey = item.Key;
            SelectedIconNameLabel.Text = item.DisplayName;

            card.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateIcons(SearchTextBox.Text.Trim());
        }

        private void ClearIcon_Click(object sender, RoutedEventArgs e)
        {
            SelectedIconKey = "";
            SelectedIconNameLabel.Text = "(无图标)";
            if (_selectedCard != null)
            {
                _selectedCard.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                _selectedCard.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                _selectedCard = null;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
