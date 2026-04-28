using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Kolsites
{
    public sealed partial class BlockedTimeRangeEditor : UserControl
    {
        // אותיות העברית עבור ימי השבוע: 0=ראשון .. 6=שבת
        private static readonly string[] DayLetters = { "א", "ב", "ג", "ד", "ה", "ו", "ש" };
        private static readonly string[] DayNames =
            { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };

        private readonly ToggleButton[] _dayButtons = new ToggleButton[7];
        private bool _suppressEvents;

        public BlockedTimeRange Range { get; private set; } = new();

        /// <summary>נקרא כשהמשתמש לוחץ על כפתור ההסרה - העריכה מבקשת מהמכיל למחוק אותה.</summary>
        public event EventHandler? RemoveRequested;

        public BlockedTimeRangeEditor()
        {
            InitializeComponent();
            BuildDayButtons();
        }

        private void BuildDayButtons()
        {
            for (int i = 0; i < 7; i++)
            {
                int day = i;
                var btn = new ToggleButton
                {
                    Content = DayLetters[i],
                    MinWidth = 36,
                    MinHeight = 36,
                    Padding = new Thickness(4)
                };
                ToolTipService.SetToolTip(btn, DayNames[i]);
                btn.Checked += (_, _) => OnDayToggled(day, true);
                btn.Unchecked += (_, _) => OnDayToggled(day, false);
                _dayButtons[i] = btn;
                DaysPanel.Children.Add(btn);
            }
        }

        private void OnDayToggled(int day, bool isChecked)
        {
            if (_suppressEvents) return;
            if (isChecked)
            {
                if (!Range.Days.Contains(day)) Range.Days.Add(day);
            }
            else
            {
                Range.Days.Remove(day);
            }
            UpdateHint();
        }

        public void Bind(BlockedTimeRange range)
        {
            Range = range;
            _suppressEvents = true;
            try
            {
                NameBox.Text = range.Name ?? "";
                EnabledToggle.IsOn = range.Enabled;
                for (int i = 0; i < 7; i++)
                    _dayButtons[i].IsChecked = range.Days.Contains(i);
                StartPicker.SelectedTime = range.StartTime;
                EndPicker.SelectedTime = range.EndTime;
            }
            finally
            {
                _suppressEvents = false;
            }
            UpdateHint();
        }

        private void UpdateHint()
        {
            // טווח שחוצה חצות - הסבר למשתמש שזה תקין
            if (Range.EndTime <= Range.StartTime && Range.StartTime != TimeSpan.Zero)
            {
                HintText.Text = "הטווח חוצה חצות - יסתיים בבוקר היום שלמחרת.";
                HintText.Visibility = Visibility.Visible;
            }
            else if (Range.Days.Count == 0)
            {
                HintText.Text = "לא נבחרו ימים - הטווח לא יחול על שום יום.";
                HintText.Visibility = Visibility.Visible;
            }
            else
            {
                HintText.Visibility = Visibility.Collapsed;
            }
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            Range.Name = NameBox.Text ?? "";
        }

        private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            Range.Enabled = EnabledToggle.IsOn;
        }

        private void StartPicker_SelectedTimeChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
        {
            if (_suppressEvents) return;
            if (args.NewTime.HasValue) Range.StartTime = args.NewTime.Value;
            UpdateHint();
        }

        private void EndPicker_SelectedTimeChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
        {
            if (_suppressEvents) return;
            if (args.NewTime.HasValue) Range.EndTime = args.NewTime.Value;
            UpdateHint();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
