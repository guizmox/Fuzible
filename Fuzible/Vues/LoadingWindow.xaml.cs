using FuzibleFramework;
using System.Windows;
using System.Windows.Threading;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window
    {
        //public bool IsLoaded { get; private set; } = false;
        private static readonly DispatcherTimer TIMERCONSOLE = new();

        public LoadingWindow()
        {
            InitializeComponent();
            INIProgram.OnLoading += EventsReceiver_Message;
        }

        private void EventsReceiver_Message(object sender, (int, string) e)
        {
            Dispatcher.Invoke(() =>
            {
                lbInfo.Content = e.Item2;
            });

            if (e.Item1 == 3)
            {
                Dispatcher.Invoke(() =>
                {
                    this.Close();
                });
            }
        }
    }
}
