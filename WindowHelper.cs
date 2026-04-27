using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Kolsites
{
    internal static class WindowHelper
    {
        public static AppWindow? GetAppWindow(Window window)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            return AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hWnd));
        }

        public static IntPtr GetHwnd(Window window) => WindowNative.GetWindowHandle(window);

        public static double GetDpiScale(Window window)
        {
            var dpi = GetDpiForWindow(GetHwnd(window));
            return dpi / 96.0;
        }

        public static (int width, int height) GetWorkAreaSize(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return (1920, 1080);
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            return (area.WorkArea.Width, area.WorkArea.Height);
        }

        public static (int x, int y, int width, int height) GetWorkArea(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return (0, 0, 1920, 1080);
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            return (area.WorkArea.X, area.WorkArea.Y, area.WorkArea.Width, area.WorkArea.Height);
        }

        /// <summary>הופך חלון לבלי-מסגרת + topmost + ללא ערכים שיכולים לאפשר resize</summary>
        public static void ConfigureBorderlessTopmost(Window window, bool resizable = false)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null) return;

            if (appWindow.Presenter is OverlappedPresenter op)
            {
                op.SetBorderAndTitleBar(false, false);
                op.IsAlwaysOnTop = true;
                op.IsResizable = resizable;
                op.IsMaximizable = false;
                op.IsMinimizable = false;
            }
        }

        public static void Resize(Window window, int width, int height)
        {
            var appWindow = GetAppWindow(window);
            appWindow?.Resize(new SizeInt32(width, height));
        }

        public static void Move(Window window, int x, int y)
        {
            var appWindow = GetAppWindow(window);
            appWindow?.Move(new PointInt32(x, y));
        }

        public static void MoveAndResize(Window window, int x, int y, int width, int height)
        {
            var appWindow = GetAppWindow(window);
            appWindow?.MoveAndResize(new RectInt32(x, y, width, height));
        }

        public static void SetIcon(Window window)
        {
            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    GetAppWindow(window)?.SetIcon(iconPath);
                }
            }
            catch { }
        }

        // הסרת חלון מ-Alt+Tab ומה-taskbar (שימושי לכפתור הצף ולחלון העילי)
        public static void HideFromTaskbar(Window window)
        {
            try
            {
                var appWindow = GetAppWindow(window);
                if (appWindow?.Presenter is OverlappedPresenter op)
                {
                    op.IsMinimizable = false;
                }
                appWindow!.IsShownInSwitchers = false;
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
    }
}
