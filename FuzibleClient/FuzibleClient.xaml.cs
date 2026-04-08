using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FuzibleClient
{
    public partial class MainWindow : Window
    {
        readonly ClientApp ClientParameters = null;
        readonly List<ClientJob> ListOfJobs = new();

        public MainWindow()
        {
            try
            {
                ClientParameters = new ClientApp(true);
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ClientParameters.Language);

                InitializeComponent();

                if (ClientParameters.LoadingError.Length == 0)
                {
                    ListOfJobs = ClientParameters.ListOfJobs;
                    LoadUIListSections(ListOfJobs);
                    lbAboutApp.Content = string.Concat("GT/Build : ", Monitoring.GetBuildDate(Assembly.GetExecutingAssembly()), " - " + Languages.Languages.fc_msg_connectedto + " [", ClientParameters.BDDDriverClientApp.ToString().Replace("_", "__"), "]");
                }
                else { lbAboutApp.Content = ClientParameters.LoadingError.Trim(); }

                dtRequestedExecution.SelectedDate = DateTime.Now;
                this.Height = (System.Windows.SystemParameters.PrimaryScreenHeight * 0.4);
            }
            catch (Exception ex)
            { lbAboutApp.Content = "Unable to Start : " + ex.Message + Environment.NewLine + "(DB:" + ClientParameters != null ? ClientParameters.BDDDriverClientApp.ToString() : "null" + " / USER:" + ClientParameters != null ? ClientParameters.FuzibleUserJobs : "null" + ")"; }

        }

        private void BtnCheckStatus_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0)
            {
                ClientJob cJ = ListOfJobs.First(j => j.JobKey.Equals(cbINISection.SelectedValue.ToString()));
                string sMessage = ClientParameters.CheckJobStatus_SQL(cJ);
                if (sMessage.Length == 0 && ClientParameters.LoadingError.Length > 0)
                { MessageBox.Show(ClientParameters.LoadingError); }
                else { TextBlockWrite(sMessage, true); }
            }
            else
            {
                MessageBox.Show(Languages.Languages.fc_msg_nojobselected);
            }
        }

        private void CbINISection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbINISection.SelectedValue != null)
            {
                string sJobKey = cbINISection.SelectedValue.ToString();
                bool bOk = LoadSelectedJob(sJobKey);
                if (bOk)
                {
                    RichTB.SetTextRTB(tbQueryVariableParameters, ListOfJobs.First(j => j.JobKey.Equals(sJobKey)).JobDynParams);
                    RichTB.SetColorsRTB(tbQueryVariableParameters, new List<RTBColorizer> { new RTBColorizer(";", new SolidColorBrush(Colors.Red), FontWeights.UltraBold, FontStyles.Normal) }, true);
                    btnExecuteTask.IsEnabled = true;
                    btnVoirRequetes.IsEnabled = true;
                    btnCheckStatus.IsEnabled = true;
                }
                else
                {
                    cbINISection.SelectedIndex = -1;
                    btnExecuteTask.IsEnabled = false;
                    btnVoirRequetes.IsEnabled = false;
                    btnCheckStatus.IsEnabled = false;
                }
            }
        }

        private void BtnVoirRequetes_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0)
            {
                try
                {
                    string sJobKey = cbINISection.SelectedValue.ToString();
                    string sQueries = ListOfJobs.First(j => j.JobKey.Equals(sJobKey)).JobQueries;
                    List<string> sDynParams = Toolbox.FromStringToList(RichTB.GetTextRTB(tbQueryVariableParameters));
                    sQueries = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sQueries, sDynParams);
                    MessageBox.Show(sQueries);
                }
                catch (Exception ex)
                { MessageBox.Show(Languages.Languages.fc_msg_unabletoloadjobqueries + ex.Message); }
            }
            else
            {
                MessageBox.Show(Languages.Languages.fc_msg_nojobselected);
            }
        }

        private void BtnExecuteTask_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0)
            {
                ClientJob cJ = ListOfJobs.First(j => j.JobKey.Equals(cbINISection.SelectedValue.ToString()));
                btnExecuteTask.IsEnabled = false;
                tbConsole.Text = "";
                btnExecuteTask.IsEnabled = true;
                try
                {
                    string sMessage = RunJob_SQL(cJ, RichTB.GetTextRTB(tbQueryVariableParameters), cJ.JobPriority, dtRequestedExecution.SelectedDate == null ? DateTime.Now : dtRequestedExecution.SelectedDate.Value, tbRequestedExecutionHour.Text.Trim().Length > 0 ? Convert.ToInt32(tbRequestedExecutionHour.Text.Trim()) : -1);
                    MessageBox.Show(sMessage);
                }
                catch (Exception ex)
                { MessageBox.Show(Languages.Languages.fc_msg_cantrequest + ex.Message); }
            }
            else
            {
                MessageBox.Show(Languages.Languages.fc_msg_nojobselected);
            }
        }

        private void TbQueryVariableParameters_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            TextRange tR = new(tbQueryVariableParameters.Document.ContentStart, tbQueryVariableParameters.Document.ContentEnd);
            tR.ClearAllProperties();
            RichTB.SetColorsRTB(tbQueryVariableParameters, new List<RTBColorizer> { new RTBColorizer(";", new SolidColorBrush(Colors.Red), FontWeights.UltraBold, FontStyles.Normal) }, true);
        }

        private bool LoadSelectedJob(string sSelectedJobID)
        {
            ClientJob cJ = ListOfJobs.First(j => j.JobKey.Equals(cbINISection.SelectedValue.ToString()));

            bool bOK = false;

            try
            {
                string sJobPassword = PromptForPassword().Trim();

                string sPathApp = Path.GetFileName(cJ.ApplicationPathAndFile);
                if (sPathApp.Equals(ClientParameters.FuzibleName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string sJobIDPwd = cJ.JobID[1..cJ.JobID.IndexOf("]")];
                        sJobPassword = FITools.EncryptionSystem.AES_Encrypt(sJobPassword, sJobIDPwd);
                    }
                    catch { bOK = false; }
                }

                if (cJ.JobPassword.Equals(sJobPassword))
                { bOK = true; }
                else
                {
                    MessageBox.Show(Languages.Languages.fc_wrongpwd01, Languages.Languages.fc_wrongpwd02);
                    bOK = false;
                }
            }
            catch (Exception ex)
            { TextBlockWrite(Languages.Languages.fc_msg_unabletoloadjob + ex.Message + ")", false); }

            return bOK;
        }

        private static string PromptForPassword()
        {
            FuzibleUITools.Prompt fP = new(Languages.Languages.fc_input_pwd, Languages.Languages.fc_input_pwd, true, true);
            fP.ShowDialog();
            string sPassword = fP.PromptUserData;
            return sPassword;
        }

        private void LoadUIListSections(List<ClientJob> sListOfJobs)
        {
            cbINISection.Items.Clear();
            string sApplication = "";
            string sCategory = "";
            foreach (ClientJob cJ in sListOfJobs)
            {
                if (!sCategory.Equals(cJ.JobCategory))
                {
                    ComboBoxItem cbNewItemS1 = new()
                    {
                        Tag = string.Concat("[SEP_", cJ.JobCategory.ToUpper(), "]"),
                        Content = string.Concat("[" + cJ.JobCategory.ToUpper(), "]"),
                        Foreground = Brushes.Red,
                        FontWeight = FontWeights.UltraBold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbINISection.Items.Add(cbNewItemS1);
                }

                if (!sApplication.Equals(cJ.AppAndUser) || !sCategory.Equals(cJ.JobCategory))
                {
                    ComboBoxItem cbNewItemS1 = new()
                    {
                        Tag = string.Concat("[SEP_", cJ.AppAndUser, "]"),
                        Content = string.Concat("   ", cJ.AppAndUser),
                        Foreground = Brushes.DarkBlue,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbINISection.Items.Add(cbNewItemS1);
                }

                ComboBoxItem cbNewItem = new()
                {
                    Tag = string.Concat(cJ.JobKey),
                    Content = string.Concat("      ", cJ.JobName),
                    ToolTip = string.Concat(Languages.Languages.fc_msg_jobdescription, cJ.JobDescription),
                    FontWeight = cJ.ChildrenCount == 0 ? FontWeights.Normal : FontWeights.Bold,
                };
                cbINISection.Items.Add(cbNewItem);

                sApplication = cJ.AppAndUser;
                sCategory = cJ.JobCategory;
            }
        }

        private string RunJob_SQL(ClientJob cJob, string sDynamicParams, int iJobPriority, DateTime dtRequestedLaunch, int iHourRequestedLaunch)
        {
            string sResult;

            DateTime dtFormatedRequestedDate = Convert.ToDateTime(dtRequestedLaunch.ToShortDateString());
            if (iHourRequestedLaunch > -1)
            { dtFormatedRequestedDate = dtFormatedRequestedDate.AddHours(iHourRequestedLaunch); }

            bool bAlreadyRunning = ClientParameters.CheckRunningJob(cJob);

            if (bAlreadyRunning)
            {
                sResult = Languages.Languages.fc_msg_jobalreadyinvoked;
            }
            else
            {
                int iFlood = ClientParameters.CheckFloodingInterval(cJob, sDynamicParams);
                //contrôle d'intervalle pour ne pas flooder les demandes
                if (iFlood > 0)
                {
                    sResult = Languages.Languages.fc_msg_floodcontrol01 + iFlood.ToString() + Languages.Languages.fc_msg_floodcontrol02;
                }
                else
                {
                    sResult = ClientParameters.InsertJobStack(cJob, sDynamicParams, iJobPriority.ToString(), iHourRequestedLaunch, dtFormatedRequestedDate);
                }
            }

            return sResult;
        }

        delegate void ParametrizedMethodInvoker5(string arg, bool bClear);
        void TextBlockWrite(string arg, bool bClear)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Dispatcher.Invoke(new ParametrizedMethodInvoker5(TextBlockWrite), arg);
                return;
            }
            if (bClear) { tbConsole.Inlines.Clear(); }
            tbConsole.Inlines.Add(arg + Environment.NewLine);
        }

    }

    internal static class RichTB
    {
        internal class RangeAndStyle
        {
            public TextRange Range { get; set; }
            public RTBColorizer Style { get; set; }

            public RangeAndStyle(TextRange tr, RTBColorizer style)
            {
                Range = tr;
                Style = style;
            }
        }

        internal static void SetColorsRTB(this RichTextBox richTextBox, List<RTBColorizer> qCPatterns, bool bResetproperties)
        {
            double dFontSize = (double)new FontSizeConverter().ConvertFrom("10pt");

            if (bResetproperties)
            {
                //RAZ RichTextBox
                TextPointer pointerA = richTextBox.Document.ContentStart;
                TextPointer pointerB = richTextBox.Document.ContentEnd;
                TextRange tRInit = new(pointerA, pointerB);
                tRInit.ClearAllProperties();
            }

            //tRInit.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Black));
            //tRInit.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            //tRInit.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Tahoma"));
            //tRInit.ApplyPropertyValue(TextElement.FontSizeProperty, dFontSize);

            TextPointer pointerStart = richTextBox.CaretPosition.DocumentStart;
            List<RangeAndStyle> trRanges = new();

            while (pointerStart != null)
            {
                if (pointerStart.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = pointerStart.GetTextInRun(LogicalDirection.Forward);

                    foreach (RTBColorizer Pattern in qCPatterns)
                    {
                        try
                        {
                            MatchCollection matches = Regex.Matches(textRun, Pattern.SData, RegexOptions.IgnoreCase);
                            foreach (Match match in matches)
                            {
                                TextRange tR = null;
                                bool bFound = false;
                                int startIndex = match.Index;
                                int length = match.Length;

                                while (!bFound)
                                {
                                    TextPointer start = pointerStart.GetPositionAtOffset(startIndex, LogicalDirection.Forward);
                                    TextPointer end = start.GetPositionAtOffset(length, LogicalDirection.Forward);
                                    if (end == null) { break; }
                                    tR = new TextRange(start, end);
                                    if (!tR.Text.Equals(match.Value))
                                    { startIndex++; }
                                    else { bFound = true; }
                                }

                                if (tR != null)
                                { trRanges.Add(new RangeAndStyle(tR, Pattern)); }
                            }
                        }
                        catch { } //erreur inconnue
                    }
                }

                pointerStart = pointerStart.GetNextContextPosition(LogicalDirection.Forward);

            }

            //application des styles

            richTextBox.SetValue(Paragraph.LineHeightProperty, 2.0);
            richTextBox.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Black));
            richTextBox.SetValue(TextElement.FontWeightProperty, FontWeights.Normal);
            richTextBox.SetValue(TextElement.FontFamilyProperty, new FontFamily("Tahoma"));
            richTextBox.SetValue(TextElement.FontSizeProperty, dFontSize);

            foreach (RangeAndStyle tR in trRanges)
            {
                tR.Range.ApplyPropertyValue(TextElement.ForegroundProperty, tR.Style.SColor);
                tR.Range.ApplyPropertyValue(TextElement.FontWeightProperty, tR.Style.SSize);
                tR.Range.ApplyPropertyValue(TextElement.FontStyleProperty, tR.Style.SStyle);
                tR.Range.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Tahoma"));
                tR.Range.ApplyPropertyValue(TextElement.FontSizeProperty, dFontSize);
            }
        }

        internal static void SetTextRTB(this RichTextBox richTextBox, string text)
        {
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph(new Run(text.Trim())));
        }

        internal static string GetTextRTB(this RichTextBox richTextBox)
        {
            return new TextRange(richTextBox.Document.ContentStart,
                richTextBox.Document.ContentEnd).Text.Trim();
        }

    }
    internal class RTBColorizer
    {
        internal string SData { get; set; } = "";
        internal SolidColorBrush SColor { get; set; } = new SolidColorBrush(Colors.Black);
        internal FontWeight SSize { get; set; } = FontWeights.DemiBold;

        internal FontStyle SStyle { get; set; } = FontStyles.Normal;

        internal RTBColorizer(string _sData, SolidColorBrush _sColor, FontWeight _sSize, FontStyle _sStyle)
        {
            _sData = _sData.Replace("?", "\\?");
            _sData = _sData.Replace("[", "\\[");
            _sData = _sData.Replace("]", "\\]");
            _sData = Regex.Replace(_sData, "{\\d+}", "{}"); //interdit : {1}
            SColor = _sColor;
            SData = _sData;
            SSize = _sSize;
            SStyle = _sStyle;
        }

    }

}

