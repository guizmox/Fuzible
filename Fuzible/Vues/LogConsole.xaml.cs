using System.Windows;
using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Media;
using System;
using System.Text;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour LogConsole.xaml
    /// </summary>
    public partial class LogConsole : Window
    {
        public int MAX_MESSAGES = 1000;

        //A window receives this message when the user chooses a command from the Window menu, or when the user chooses the maximize button, minimize button, restore button, or close button.
        public const Int32 WM_SYSCOMMAND = 0x112;

        //Draws a horizontal dividing line.This flag is used only in a drop-down menu, submenu, or shortcut menu.The line cannot be grayed, disabled, or highlighted.
        public const Int32 MF_SEPARATOR = 0x800;

        //Specifies that an ID is a position index into the menu and not a command ID.
        public const Int32 MF_BYPOSITION = 0x400;

        //Specifies that the menu item is a text string.
        public const Int32 MF_STRING = 0x0;

        //Menu Ids for our custom menu items
        public const Int32 _ItemOneMenuId = 1000;
        public const Int32 _ItemTwoMenuID = 1001;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, Int32 wPosition, Int32 wFlags, Int32 wIDNewItem, string lpNewItem);

        public LogConsole()
        {
            InitializeComponent();
            
            this.Hide();
        }

        public void WriteMessage(string message, LogColor color)
        {
            Run run = new(message)
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.Normal,
                FontStyle = FontStyles.Normal
            };
            switch (color)
            {
                case LogColor.green:
                    run.Foreground = Brushes.Green;
                    run.FontWeight = FontWeights.Normal;
                    run.FontStyle = FontStyles.Normal;
                    break;
                case LogColor.red:
                    run.Foreground = Brushes.Red;
                    run.FontWeight = FontWeights.Bold;
                    run.FontStyle = FontStyles.Normal;
                    break;
                case LogColor.orange:
                    run.Foreground = Brushes.Orange;
                    run.FontWeight = FontWeights.DemiBold;
                    run.FontStyle = FontStyles.Normal;
                    break;
                case LogColor.gray:
                    run.Foreground = Brushes.Gray;
                    run.FontWeight = FontWeights.Thin;
                    run.FontStyle = FontStyles.Normal;
                    break;
                case LogColor.darkgray:
                    run.Foreground = Brushes.DarkGray;
                    run.FontWeight = FontWeights.Thin;
                    run.FontStyle = FontStyles.Italic;
                    break;
            }

            if (OutputBlock.Inlines.Count > MAX_MESSAGES)
            {
                try
                {
                    OutputBlock.Inlines.Remove(OutputBlock.Inlines.FirstInline);
                }
                catch { }
            }

            OutputBlock.Inlines.Add(run);

            Scroller.ScrollToEnd();
        }

        public void WriteError(string message, Exception e = null)
        {
            Run run = new(message)
            {
                Foreground = Brushes.Red,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Normal
            };

            if (OutputBlock.Inlines.Count > MAX_MESSAGES)
            {
                try
                {
                    OutputBlock.Inlines.Remove(OutputBlock.Inlines.FirstInline);
                }
                catch { }
            }

            OutputBlock.Inlines.Add(run);

            if (e != null)
            {
                OutputBlock.Inlines.Add(e.Message);
                OutputBlock.Inlines.Add(e.StackTrace);
            }

            Scroller.ScrollToEnd();
        }

        public void ShowConsole()
        {
            this.Show();
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
            base.OnClosing(e);
        }

        private void OnLogLoad(object sender, RoutedEventArgs e)
        {
            IntPtr windowhandle = new WindowInteropHelper(this).Handle;
            HwndSource hwndSource = HwndSource.FromHwnd(windowhandle);

            IntPtr systemMenuHandle = GetSystemMenu(windowhandle, false);

            //Insert our custom menu items
            InsertMenu(systemMenuHandle, 5, MF_BYPOSITION | MF_SEPARATOR, 0, string.Empty); //Add a menu seperator
            InsertMenu(systemMenuHandle, 6, MF_BYPOSITION, _ItemOneMenuId, "Copy to clipboard"); //Add a setting menu item
            InsertMenu(systemMenuHandle, 7, MF_BYPOSITION, _ItemTwoMenuID, "Clear Log"); //add an About menu item

            hwndSource.AddHook(new HwndSourceHook(WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Check if the SystemCommand message has been executed
            if (msg == WM_SYSCOMMAND)
            {
                //check which menu item was clicked
                switch (wParam.ToInt32())
                {
                    case _ItemOneMenuId:
                        CopyToClipboard();
                        handled = true;
                        break;
                    case _ItemTwoMenuID:
                        ClearLog();
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private void CopyToClipboard()
        {
            var sb = new StringBuilder();
            foreach (var inline in OutputBlock.Inlines)
            {
                var run = inline as Run;
                sb.Append(run.Text);
            }
            System.Windows.Forms.Clipboard.SetText(sb.ToString());

            WriteMessage("Content copied to clipboard, press Ctrl+V to paste" + Environment.NewLine, LogColor.green);
        }

        private void ClearLog()
        {
            OutputBlock.Inlines.Clear();
        }

        public string GetLiveLogs()
        {
            var sb = new StringBuilder();
            foreach (var inline in OutputBlock.Inlines)
            {
                var run = inline as Run;
                sb.Append(run.Text);
            }
            return sb.ToString();
        }

    }

    public enum LogColor
    {
        white = 0,
        green = 1,
        red = 2,
        orange = 3,
        gray = 4,
        darkgray = 5
    }
}
