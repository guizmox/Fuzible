using System;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace FuzibleUITools
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TextContent : Window
    {
        private int ControlIndex { get; set; } = 0;
        private bool IsPassword { get; set; } = false;
        public string PromptUserData { get; internal set; } = "";
        private bool MustWrite { get; set; } = true;

        public TextContent(string sContentLabel, string sTitle, string sDefaultValue = "", bool bMustWrite = false)
        {
            InitializeComponent();
            this.Title = sTitle;
            lbInfo.Text = sContentLabel;
            MustWrite = bMustWrite;

            if (sDefaultValue.Length > 0)
            {
                lbText.Document.Blocks.Clear();
                lbText.Document.Blocks.Add(new Paragraph(new Run(sDefaultValue.Trim())));
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
        }

        private void GetUserInputAndExit()
        {
            StringBuilder sbData = new();

            TextRange textRange = new(lbText.Document.ContentStart, lbText.Document.ContentEnd);
            string[] rtbLines = textRange.Text.Split(Environment.NewLine);
            foreach (var line in rtbLines)
            {
                sbData.AppendLine(line.Trim());
            }

            PromptUserData = sbData.ToString();
            //PromptUserData = new TextRange(lbText.Document.ContentStart, lbText.Document.ContentEnd).Text.Trim();

            if (PromptUserData.Trim().Length == 0 && MustWrite)
            { MessageBox.Show("Please input some text before leaving."); }
            else { this.Close(); }
        }

    }
}