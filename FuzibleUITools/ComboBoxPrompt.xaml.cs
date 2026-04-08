using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FuzibleUITools
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ComboBoxPrompt : Window
    {
        public string PromptUserData { get; internal set; } = "DEFAULT";

        public ComboBoxPrompt(List<string> sItems, string sTitle, string sCb, string sTb)
        {
            InitializeComponent();
            this.Title = sTitle;
            lbCb.Content = sCb;
            lbTb.Content = sTb;
            foreach (string item in sItems)
            {
                cbJobFamily.Items.Add(item);
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
            if (tbText.Text.Length > 0)
            {
                bool bExists = false;
                //contrôle existence cat
                foreach (var item in cbJobFamily.Items)
                {
                    if (EqualsIgnoreCaseAndAccents(item.ToString(), tbText.Text.Trim()))
                    {
                        PromptUserData = item.ToString();
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    PromptUserData = tbText.Text.Trim();
                }

            }
            else
            {
                PromptUserData = cbJobFamily.SelectedItem.ToString();
            }

            this.Close();
        }

        public static bool EqualsIgnoreCaseAndAccents(string a, string b)
        {
            string Normalize(string s) =>
                new string(s.Normalize(NormalizationForm.FormD)
                            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                            .ToArray())
                .ToLowerInvariant();

            return Normalize(a) == Normalize(b);
        }
    }
}