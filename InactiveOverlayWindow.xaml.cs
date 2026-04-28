using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Kolsites
{
    /// <summary>
    /// חלון מודאלי שמוצג כשמשתמש מנסה לפתוח את הקיוסק בזמן חסום.
    /// סוגר את עצמו אוטומטית כשהטווח החסום מסתיים.
    /// </summary>
    public sealed partial class InactiveOverlayWindow : Window
    {
        private readonly DateTime _endsAt;
        private readonly BlockedTimeRange _range;
        private readonly DispatcherQueueTimer _countdownTimer;

        public InactiveOverlayWindow(BlockedTimeChecker.BlockResult block, AppTheme theme)
        {
            InitializeComponent();
            _range = block.Range;
            _endsAt = block.EndsAt;

            Title = "Kolsites";
            RootGrid.FlowDirection = FlowDirection.RightToLeft;
            if (RootGrid is FrameworkElement fe)
                fe.RequestedTheme = ThemeHelper.ToElementTheme(theme);

            ConfigureWindow();

            ResumeAtText.Text = $"התוכנה תשוב לפעול בשעה {_endsAt:HH:mm}";
            UpdateCountdown();

            var dispatcher = DispatcherQueue.GetForCurrentThread();
            _countdownTimer = dispatcher.CreateTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += (_, _) => UpdateCountdown();
            _countdownTimer.Start();

            Closed += (_, _) => _countdownTimer.Stop();
        }

        private void ConfigureWindow()
        {
            WindowHelper.SetIcon(this);
            WindowHelper.ConfigureBorderlessTopmost(this, resizable: false);
            WindowHelper.HideFromTaskbar(this);

            // גודל קבוע במרכז המסך
            int w = 520, h = 360;
            var (waX, waY, waW, waH) = WindowHelper.GetWorkArea(this);
            var scale = WindowHelper.GetDpiScale(this);
            int width = (int)(w * scale);
            int height = (int)(h * scale);
            WindowHelper.MoveAndResize(this,
                waX + (waW - width) / 2,
                waY + (waH - height) / 2,
                width, height);
        }

        private void UpdateCountdown()
        {
            var remaining = _endsAt - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                Close();
                return;
            }

            // הצגה קריאה: "נותרו עוד X דקות" או "נותרו עוד שעה X דקות" וכד'.
            string text;
            int totalMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
            if (remaining.TotalMinutes < 1)
            {
                int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
                text = seconds == 1 ? "נותרה שנייה" : $"נותרו {seconds} שניות";
            }
            else if (totalMinutes < 60)
            {
                text = totalMinutes == 1 ? "נותרה דקה" : $"נותרו עוד {totalMinutes} דקות";
            }
            else
            {
                int hours = totalMinutes / 60;
                int mins = totalMinutes % 60;
                string hourPart = hours == 1 ? "שעה" : $"{hours} שעות";
                if (mins == 0)
                    text = $"נותרו עוד {hourPart}";
                else
                    text = $"נותרו עוד {hourPart} ו-{mins} דקות";
            }
            CountdownText.Text = text;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
