using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FuzibleUITools
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Prompt : Window
    {
        private int ControlIndex { get; set; } = 0;
        private bool IsPassword { get; set; } = false;
        public string PromptUserData { get; internal set; } = "";
        private bool MustWrite { get; set; } = true;

        public Prompt(string sContentLabel, string sTitle, bool bHideChars, bool bMustWrite, string sDefaultValue = "")
        {
            InitializeComponent();
            IsPassword = bHideChars;
            this.Title = sTitle;
            lbText.Text = sContentLabel;
            MustWrite = bMustWrite;

            if (!bHideChars)
            {
                this.BorderBrush = Brushes.DarkOrange;

                TextBox tbUserData = new()
                {
                    Name = "tbUserData",
                    Text = sDefaultValue,
                    TextWrapping = TextWrapping.Wrap,
                    Width = 240,
                };
                ControlIndex = CvPrompt.Children.Add(tbUserData);
                tbUserData.Focus();
                // <TextBox x:Name="tbUserData" Text="" TextWrapping="Wrap" Width="240" Canvas.Left="25" Canvas.Top="50"/>
            }
            else
            {
                this.BorderBrush = Brushes.Red;

                PasswordBox tbUserData = new()
                {
                    Name = "tbUserData",
                    Width = 240
                };
                ControlIndex = CvPrompt.Children.Add(tbUserData);
                tbUserData.Focus();
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            GetUserInputAndExit();
        }

        private void This_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                GetUserInputAndExit();
            }
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
            }
        }

        private void GetUserInputAndExit()
        {
            if (!IsPassword)
            {
                TextBox tb = (TextBox)CvPrompt.Children[ControlIndex];
                PromptUserData = tb.Text;
            }
            else
            {
                PasswordBox tb = (PasswordBox)CvPrompt.Children[ControlIndex];
                PromptUserData = tb.Password;
            }

            if (PromptUserData.Trim().Length == 0 && MustWrite)
            { MessageBox.Show("Please input some text before leaving."); }
            else { this.Close(); }
        }
    }
}