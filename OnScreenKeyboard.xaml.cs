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

        // מכפיל גודל - משפיע על MinWidth/MinHeight/FontSize/Margin של כל המקשים.
        // ברירת המחדל היא 1.0 (גודל רגיל); ערכים גבוהים יותר מגדילים את כל המקלדת.
        private double _scale = 1.0;

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

        /// <summary>קובע את מכפיל הגודל ובונה מחדש את המקלדת. ערך &lt;= 0 נורמל ל-1.0.</summary>
        public void SetScale(double scale)
        {
            if (scale <= 0) scale = 1.0;
            if (Math.Abs(_scale - scale) < 0.001) return;
            _scale = scale;
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
            space.MinWidth = 250 * _scale;
            ApplyKeyScale(space);
            sp.Children.Add(space);

            // Backspace
            var back = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                MinWidth = 80 * _scale,
                Content = new FontIcon { Glyph = "", FontSize = 16 } // BackSpace
            };
            ApplyKeyScale(back);
            back.Click += (_, _) => BackspacePressed?.Invoke();
            sp.Children.Add(back);

            // Enter
            var enter = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                MinWidth = 80 * _scale,
                Content = new FontIcon { Glyph = "", FontSize = 16 } // Enter
            };
            ApplyKeyScale(enter);
            enter.Click += (_, _) => EnterPressed?.Invoke();
            sp.Children.Add(enter);

            return sp;
        }

        private Button MakeKey(string text, string? toolTip = null)
        {
            var btn = new Button
            {
                Style = (Style)Resources["KeyButtonStyle"],
                MinWidth = 58 * _scale,
                Content = text == " " ? "רווח" : text
            };
            ApplyKeyScale(btn);

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
                MinWidth = 78 * _scale,
                MinHeight = 58 * _scale,
                FontSize = 14 * _scale,
                Margin = new Thickness(3 * _scale),
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

        // מחיל גודל סקייל על מקש: גובה, פונט ומרווח. ה-MinWidth נקבע נפרד לפי
        // סוג המקש (סטנדרטי/רחב/רווח) כדי שכל הקנה מידה ייצא יחסי לכל סוג.
        private void ApplyKeyScale(Control btn)
        {
            btn.MinHeight = 58 * _scale;
            btn.FontSize = 22 * _scale;
            btn.Margin = new Thickness(3 * _scale);

            // הגדלת אייקון בתוך הכפתור (כמו על Backspace/Enter) ביחס לסקייל
            if (btn is ContentControl cc && cc.Content is FontIcon icon)
                icon.FontSize = 16 * _scale;
        }
    }
}
