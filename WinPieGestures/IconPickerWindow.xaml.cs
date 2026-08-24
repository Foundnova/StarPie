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
    using Brush = System.Windows.Media.Brush;
    using Color = System.Windows.Media.Color;
    using Cursors = System.Windows.Input.Cursors;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;

    public partial class IconPickerWindow : Window
    {
        public string? SelectedIconKey { get; private set; }
        private Border? _selectedCard;

        public IconPickerWindow(string? initialKey = null)
        {
            InitializeComponent();
            AppThemeManager.ApplyTheme(this, AppThemeManager.CurrentEffectiveTheme);
            SelectedIconKey = initialKey;
            PopulateIcons();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = $"{I18n.T("IconPickerTitle")} - StarPie";
            if (HeaderTitleText != null) HeaderTitleText.Text = I18n.T("IconPickerHeader");
            if (HeaderSubtitleText != null) HeaderSubtitleText.Text = I18n.T("IconPickerSubtitle");
            if (SearchTextBox != null) SearchTextBox.ToolTip = I18n.T("IconPickerSearchTooltip");
            if (ClearIconButton != null) ClearIconButton.Content = I18n.T("IconPickerClear");
            if (SelectedIconPrefixText != null) SelectedIconPrefixText.Text = I18n.T("IconPickerSelected") + " ";
            if (ConfirmButton != null) ConfirmButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");
            if (string.IsNullOrEmpty(SelectedIconKey) && SelectedIconNameLabel != null)
            {
                SelectedIconNameLabel.Text = I18n.T("IconPickerNone");
            }
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

            var cardBg = (Brush)FindResource("SubtleCardBrush");
            var cardBorder = (Brush)FindResource("InputBorderBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)FindResource("TextSecondaryBrush");

            foreach (var item in items)
            {
                var card = new Border
                {
                    Background = cardBg,
                    BorderBrush = cardBorder,
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
                    Fill = textPrimary,
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
                    Foreground = textSecondary,
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
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
            }

            _selectedCard = card;
            SelectedIconKey = item.Key;
            SelectedIconNameLabel.Text = item.DisplayName;

            card.Background = (Brush)FindResource("NavTabActiveBgBrush");
            card.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
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
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
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
