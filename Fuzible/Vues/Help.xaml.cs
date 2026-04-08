using FuzibleFramework;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour Help.xaml
    /// </summary>
    /// 

    public partial class Help : Window
    {
        internal static string INTERNAL_DB = "";
        BlockHelp HelpObject = null;

        public bool DocumentationLocked { get; private set; } = false;

        public bool IsOpen { get; internal set; } = false;

        private bool IsLoading = false;

        private bool AdminEdit { get; set; } = false;

        public Help(BlockHelp bHelp)
        {
            

            this.Title = "Documentation";
#if (DEBUG)
            INTERNAL_DB = string.Concat("Data Source=", Path.Combine(@"C:\Users\gtristant\source\repos\Fuzible\Fuzible\Resources", "Help.db"));
#elif (RELEASE)
            INTERNAL_DB = string.Concat("Data Source=", System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "\\Fuzible\\Help.db");
#endif

            InitializeComponent();

            HelpObject = bHelp;

            if (bHelp.IsHTML)
            {
                tbHelp.Visibility = Visibility.Hidden;
                wbHelp.Visibility = Visibility.Visible;
                gbTBHelp.Visibility = Visibility.Hidden;
                gbWBHelp.Visibility = Visibility.Visible;
                FillHTMLHelp(bHelp);
            }
            else
            {
                tbHelp.Visibility = Visibility.Visible;
                wbHelp.Visibility = Visibility.Hidden;
                gbTBHelp.Visibility = Visibility.Visible;
                gbWBHelp.Visibility = Visibility.Hidden;
                FillTextBlock(bHelp);
            }

            AdminEdit = GetAdminInfo();

            if (AdminEdit)
            {
                tbToolBar.Visibility = Visibility.Visible;
                tbHelp.IsReadOnly = false;
                btnSave.Visibility = Visibility.Visible;
            }

            //Activate();
        }

        private void SHSHelp_Closed(object sender, EventArgs e)
        {
            IsOpen = false;
        }

        private void SHSHelp_Loaded(object sender, RoutedEventArgs e)
        {
            IsOpen = true;
        }

        private void Help_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            { ChangeLockedStatus(); }
        }

        public async Task LoadHelpBlock(BlockHelp bHelp)
        {
            this.Title = Languages.Languages.hlp_f3;
            if (!IsLoading)
            {
                if (!DocumentationLocked)
                {
                    if (!bHelp.BlockName.Equals(HelpObject.BlockName))
                    {
                        HelpObject = bHelp;
                        await LoadBlock(tbHelp);
                    };
                }
            }
        }

        private void FillHTMLHelp(BlockHelp bHelp)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(bHelp.BlockContent);
            writer.Flush();
            stream.Position = 0;

            Title = bHelp.BlockName;
            gbWBHelp.Header = bHelp.BlockExemples;
            wbHelp.NavigateToStream(stream);
        }

        private void FillTextBlock(BlockHelp bHelp)
        {
            if (bHelp.BlockContent.Length == 0)
            {
                Task.Factory.StartNew(() => LoadBlock(tbHelp));
            }
            else
            {
                tbHelp.Document.Blocks.Clear();
                //tbHelp.Document.PageWidth = 10000;
                tbHelp.Document.LineHeight = 5;
                tbHelp.Document.FontSize = 11;

                Run rTitle = new(string.Concat("[", bHelp.BlockName, "]"))
                {
                    Foreground = Brushes.DarkRed,
                    FontWeight = FontWeights.Bold
                };
                tbHelp.Document.Blocks.Add(new Paragraph(rTitle));

                Run rContent = new(bHelp.BlockContent)
                {
                    Foreground = Brushes.DarkBlue,
                    FontWeight = FontWeights.Normal
                };
                tbHelp.Document.Blocks.Add(new Paragraph(rContent));

                Run rExemples = new(bHelp.BlockExemples)
                {
                    Foreground = Brushes.DarkGreen,
                    FontStyle = FontStyles.Italic
                };
                tbHelp.Document.Blocks.Add(new Paragraph(rExemples));
            }

        }

        private void TbHelp_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveBlock(tbHelp);
        }

        public void ChangeLockedStatus()
        {
            if (DocumentationLocked)
            {
                tbHelp.BorderThickness = new Thickness(0);
                tbHelp.BorderBrush = Brushes.White;
                DocumentationLocked = false;
            }
            else 
            {
                tbHelp.BorderThickness = new Thickness(3);
                tbHelp.BorderBrush = Brushes.Red;
                DocumentationLocked = true;
            }
        }

        private bool GetAdminInfo()
        {
#if (DEBUG)

            return true;
#elif (RELEASE)
            return false;
#endif
        }

        private async Task LoadBlock(System.Windows.Controls.RichTextBox rtb)
        {
            IsLoading = true;
            string sHelp = ExecuteQuery(true, string.Concat("SELECT liHelp FROM HelpData WHERE liLang = '", HelpObject.Language, "' AND liView = '", HelpObject.View, "' AND liControl = '", HelpObject.BlockName, "';"));

            if (sHelp.Length > 0)
            {
                if (sHelp.StartsWith("[EX]"))
                {
                    await LoadBlockAsync(rtb, true, "Can't Load Help File : " + sHelp);
                }
                else
                {
                    await LoadBlockAsync(rtb, false, sHelp);
                }
            }
            else
            {
                rtb.SetTextRTB(HelpObject.BlockName);
            }
            IsLoading = false;
        }

        private async Task LoadBlockAsync(System.Windows.Controls.RichTextBox rtb, bool bError, string sHelp)
        {
            if (bError)
            {
                await rtb.Dispatcher.InvokeAsync(() =>
                {
                    rtb.AppendText(sHelp);
                });
            }
            else
            {
                try
                {
                    byte[] byteArray = Convert.FromBase64String(sHelp);

                    using (MemoryStream memoryStream = new MemoryStream(byteArray))
                    {
                        await rtb.Dispatcher.InvokeAsync(() =>
                        {
                            TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                            try
                            {
                                textRange.Load(memoryStream, System.Windows.DataFormats.XamlPackage);
                            }
                            catch //ancien format de sauvegarde
                            {
                                textRange.Load(memoryStream, System.Windows.DataFormats.Xaml);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    await rtb.Dispatcher.InvokeAsync(() =>
                    {
                        System.Windows.MessageBox.Show("Unable to load : " + ex.Message);
                    });
                }
            }
        }

        private void SaveBlock(System.Windows.Controls.RichTextBox rtb)
        {
            FlowDocument flowDocument = rtb.Document;
            TextRange textRange = new(flowDocument.ContentStart, flowDocument.ContentEnd);

            using MemoryStream memoryStream = new MemoryStream();
            textRange.Save(memoryStream, DataFormats.XamlPackage);

            byte[] byteArray = memoryStream.ToArray();
            string base64Content = Convert.ToBase64String(byteArray);

            string s = ExecuteQuery(false, string.Concat("INSERT OR REPLACE INTO HelpData(liControl, liHelp, liLang, liView) VALUES('", HelpObject.BlockName, "','", base64Content, "','", HelpObject.Language, "','", HelpObject.View, "');"));
            if (s.Length > 0)
            {
                System.Windows.MessageBox.Show("Unable to Save : " + s);
            }
        }

        private void ChangeFontColor(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.ColorDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                tbHelp.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
            }
        }

        private void ToggleBold(object sender, RoutedEventArgs e)
        {
            var currentWeight = tbHelp.Selection.GetPropertyValue(TextElement.FontWeightProperty);

            if (currentWeight != DependencyProperty.UnsetValue && currentWeight is FontWeight)
            {
                var isBold = ((FontWeight)currentWeight) == FontWeights.Bold;
                tbHelp.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, isBold ? FontWeights.Normal : FontWeights.Bold);
            }
        }

        private void ToggleItalic(object sender, RoutedEventArgs e)
        {
            var currentStyle = tbHelp.Selection.GetPropertyValue(TextElement.FontStyleProperty);

            if (currentStyle != DependencyProperty.UnsetValue && currentStyle is FontStyle)
            {
                var isItalic = ((FontStyle)currentStyle) == FontStyles.Italic;
                tbHelp.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, isItalic ? FontStyles.Normal : FontStyles.Italic);
            }
        }

        private void ToggleUnderline(object sender, RoutedEventArgs e)
        {
            var currentDecorations = tbHelp.Selection.GetPropertyValue(Inline.TextDecorationsProperty);

            if (currentDecorations != DependencyProperty.UnsetValue && currentDecorations is TextDecorationCollection)
            {
                var isUnderlined = ((TextDecorationCollection)currentDecorations).Count > 0;
                tbHelp.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, isUnderlined ? null : TextDecorations.Underline);
            }
        }

        private void ToggleBullets(object sender, RoutedEventArgs e)
        {
            TextMarkerStyle markerStyle = TextMarkerStyle.Disc;

            var currentParagraph = tbHelp.CaretPosition.Paragraph;

            if (currentParagraph != null)
            {
                var listItem = new ListItem(currentParagraph);

                if (listItem != null)
                {
                    // Vérifie si le paragraphe actuel a déjà des puces
                    if (listItem.List == null)
                    {
                        // Si le paragraphe n'est pas déjà dans une liste, créez une nouvelle liste
                        var marker = new List();
                        marker.MarkerStyle = markerStyle;
                        listItem.Blocks.Add(new Paragraph());
                        //listItem.List = marker;
                    }
                    else
                    {
                        // Si le paragraphe est déjà dans une liste, retirez-le de la liste
                        //listItem.List = null;
                    }
                }
            }
        }

        private void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            try
            {
                tbHelp.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, (double)tbHelp.Selection.GetPropertyValue(TextElement.FontSizeProperty) + 1);
            }
            catch { }
        }

        private void DecreaseFontSize(object sender, RoutedEventArgs e)
        {
            try
            {
                tbHelp.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, Math.Max(1, (double)tbHelp.Selection.GetPropertyValue(TextElement.FontSizeProperty) - 1));
            }
            catch { }
        }

        private string ExecuteQuery(bool bSelect, string sQuery)
        {
            string sResult = "";

            try
            {
                SqliteConnection ConnexionSQL;
                ConnexionSQL = new SqliteConnection(INTERNAL_DB);
                ConnexionSQL.Open();
                try
                {
                    SqliteCommand CommandeSQL = new(sQuery, ConnexionSQL);

                    if (bSelect)
                    {
                        try
                        {
                            using (SqliteDataReader reader = CommandeSQL.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    sResult = reader["liHelp"].ToString();
                                }
                                else
                                {
                                    sResult = ""; // No rows found
                                }
                            }
                            //DataSet dsData = new DataSet();
                            //SqliteDataAdapter Adaptateur = new(CommandeSQL);
                            //Adaptateur.Fill(dsData, "");

                            //if (dsData.Tables[0].Rows.Count > 0)
                            //{
                            //    sResult = dsData.Tables[0].Rows[0]["liHelp"].ToString();
                            //}
                            //else { sResult = ""; }
                        }
                        catch (Exception ex) { sResult = ex.Message; }
                    }
                    else
                    {
                        object oR = CommandeSQL.ExecuteScalar();
                        try
                        {
                            sResult = "";
                        }
                        catch (Exception ex) { sResult = "[EX] " + ex.Message; }
                    }
                }
                catch (Exception ex)
                {
                    sResult = "[EX] " + ex.Message;
                }
                ConnexionSQL.Close();
            }
            catch (Exception ex)
            {
                sResult = "[EX] " + ex.Message;
            }

            return sResult;
        }

    }

}
