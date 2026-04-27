using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kolsites
{
    public sealed partial class OnScreenKeyboard : UserControl
    {
        public event Action<string>? KeyPressed;
        public event Action? BackspacePressed;
        public event Action? EnterPressed;

        // פריסות:
        // עברית - ע"פ פריסת מקלדת ישראלית סטנדרטית
        private static readonly string[] HebrewRows = new[]
        {
            "קראטוןםפ",
            "שדגכעיחלךף",
            "זסבהנמצתץ"
        };

        private static readonly string[] EnglishLowerRows = new[]
        {
            "qwertyuiop",
            "asdfghjkl",
            "zxcvbnm"
        };

        private static readonly string[] EnglishUpperRows = new[]
        {
            "QWERTYUIOP",
            "ASDFGHJKL",
            "ZXCVBNM"
        };

        private static readonly string[] NumberRows = new[]
        {
            "1234567890",
            "-/:;()$&@\"",
            ".,?!'"
        };

        private enum Layout { Hebrew, EnglishLower, EnglishUpper, Numbers }
        private Layout _currentLayout = Layout.Hebrew;

        public OnScreenKeyboard()
        {
            InitializeComponent();
            FlowDirection = FlowDirection.LeftToRight; // המקלדת עצמה במצב LTR אחיד
            Build();
        }

        public void SetInitialLayout(string layoutCode)
        {
            _currentLayout = layoutCode?.ToLowerInvariant() switch
            {
                "en" => Layout.EnglishLower,
                "num" => Layout.Numbers,
                _ => Layout.Hebrew
            };
            Build();
        }

        private void Build()
        {
            KeyboardRoot.Children.Clear();

            string[] rows = _currentLayout switch
            {
                Layout.Hebrew => HebrewRows,
                Layout.EnglishUpper => EnglishUpperRows,
                Layout.Numbers => NumberRows,
                _ => EnglishLowerRows
            };

            foreach (var row in rows)
                KeyboardRoot.Children.Add(BuildKeyRow(row));

            // שורה תחתונה - מתגי פריסות + רווח + Backspace + Enter
            KeyboardRoot.Children.Add(BuildBottomRow());
        }

        private StackPanel BuildKeyRow(string row)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            foreach (var ch in row)
            {
                sp.Children.Add(MakeKey(ch.ToString()));
            }

            return sp;
        }

        private StackPanel BuildBottomRow()
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // מעבר לפריסות
            sp.Children.Add(MakeLayoutToggle("עב", Layout.Hebrew));
            sp.Children.Add(MakeLayoutToggle(_currentLayout == Layout.EnglishUpper ? "EN ▲" : "EN ▽",
                _currentLayout == Layout.EnglishUpper ? Layout.EnglishLower : Layout.EnglishUpper,
                isActive: _currentLayout == Layout.EnglishLower || _currentLayout == Layout.EnglishUpper));
            sp.Children.Add(MakeLayoutToggle("123", Layout.Numbers));

            // רווח
            var space = MakeKey(" ", "רווח");
            space.Style = (Style)Resources["WideKeyButtonStyle"];
            space.MinWidth = 250;
            sp.Children.Add(space);

            // Backspace
            var back = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                MinWidth = 80,
                Content = new FontIcon { Glyph = "", FontSize = 16 } // BackSpace
            };
            back.Click += (_, _) => BackspacePressed?.Invoke();
            sp.Children.Add(back);

            // Enter
            var enter = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                MinWidth = 80,
                Content = new FontIcon { Glyph = "", FontSize = 16 } // Enter
            };
            enter.Click += (_, _) => EnterPressed?.Invoke();
            sp.Children.Add(enter);

            return sp;
        }

        private Button MakeKey(string text, string? toolTip = null)
        {
            var btn = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                Content = text == " " ? "רווח" : text
            };

            if (toolTip != null)
                ToolTipService.SetToolTip(btn, toolTip);

            btn.Click += (_, _) => KeyPressed?.Invoke(text);
            return btn;
        }

        private Button MakeLayoutToggle(string label, Layout target, bool isActive = false)
        {
            var btn = new Button
            {
                Style = (Style)Resources["LayoutToggleStyle"],
                Content = label
            };
            if (isActive || _currentLayout == target)
            {
                btn.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                btn.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
            }
            btn.Click += (_, _) =>
            {
                _currentLayout = target;
                Build();
            };
            return btn;
        }
    }
}
