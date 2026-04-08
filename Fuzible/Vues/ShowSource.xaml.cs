using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour ShowSource.xaml
    /// </summary>
    public partial class ShowSource : Window
    {
        private static readonly DispatcherTimer TIMERCONSOLE = new();
        private bool DATA_LOADED = false;
        private bool DATA_LOADING = false;
        private int MAX_PREVIEW_ROWS = 500;
        private bool AnalyzeData = false;
        private readonly Job INIP;
        public Query SourceQuery;
        public string RawQuery = null;
        private readonly Query InitialSourceQuery;
        private readonly List<Query> AutomodeQueries = new();
        private LogTools MyLog;
        private List<DataSet> dsSourceAndSynchroData = new();

        private void MainWindow_Closing(object sender, EventArgs e)
        {
            TIMERCONSOLE.Stop();

            InterruptLoading();
        }

        private async void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (!TIMERCONSOLE.IsEnabled)
            {
                if (e.Key == Key.F5)
                {
                    MyLog.ClearLogEvents(); // on veut éviter de chopper les anciens LOG

                    try
                    {
                        await INIT_GetData();
                    }
                    catch (OperationCanceledException)
                    {
                        dsSourceAndSynchroData = null;
                        MessageBox.Show(Languages.Languages.ss_msg_cancelled);
                        DATA_LOADED = true;
                        DATA_LOADING = false;
                        Mouse.OverrideCursor = null;
                        this.IsEnabled = true;
                    }
                }
            }
            else
            {
                MessageBox.Show(Languages.Languages.ss_msg_alreadyloading);
            }
        }

        private void BtnCancelLoad_Click(object sender, RoutedEventArgs e)
        {
            InterruptLoading();

            EnableDisableButton(btnLoadData, true);
            ButtonWrite(Languages.Languages.ss_btn_clicktoload, btnLoadData);
        }

        private void BtnCopyAsCSV_Click(object sender, RoutedEventArgs e)
        {
            //dsSourceAndSynchroData
            //Dataset 1, table 1 : source
            //Dataset 2, tables 1,2,3,4 = insert, update, delete, target
            int iMenu = tcMenu.SelectedIndex;
            bool bOK = false;
            string sFile = "";
            switch (iMenu)
            {
                case 0:
                    sFile = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", INIP.USER, "_", INIP.JobID, "_SOURCEEXPORT.CSV");
                    if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 0 && dsSourceAndSynchroData[0].Tables.Count > 0)
                    {
                        bOK = CopyDataGridToCSV(dsSourceAndSynchroData[0].Tables[0], sFile);
                    }
                    break;
                case 1:
                    sFile = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", INIP.USER, "_", INIP.JobID, "_TARGETEXPORT.CSV");
                    if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 1 && dsSourceAndSynchroData[1].Tables.Count > 3)
                    {
                        bOK = CopyDataGridToCSV(dsSourceAndSynchroData[1].Tables[3], sFile);
                    }
                    break;
                case 2:
                    sFile = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", INIP.USER, "_", INIP.JobID, "_SYNCHROINSERTEXPORT.CSV");
                    if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 1 && dsSourceAndSynchroData[1].Tables.Count > 0)
                    {
                        bOK = CopyDataGridToCSV(dsSourceAndSynchroData[1].Tables[0], sFile);
                    }
                    break;
                case 3:
                    sFile = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", INIP.USER, "_", INIP.JobID, "_SYNCHROUPDATEEXPORT.CSV");
                    if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 1 && dsSourceAndSynchroData[1].Tables.Count > 1)
                    {
                        bOK = CopyDataGridToCSV(dsSourceAndSynchroData[1].Tables[1], sFile);
                    }
                    break;
                case 4:
                    sFile = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", INIP.USER, "_", INIP.JobID, "_SYNCHRODELETEEXPORT.CSV");
                    if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 1 && dsSourceAndSynchroData[1].Tables.Count > 2)
                    {
                        bOK = CopyDataGridToCSV(dsSourceAndSynchroData[1].Tables[2], sFile);
                    }
                    break;
            }
            if (bOK)
            {
                ProcessStartInfo startInfo = new(sFile) { UseShellExecute = true };
                Process.Start(startInfo);
            }
            else { MessageBox.Show(Languages.Languages.ss_msg_unabletoexportdata); }
        }

        private void LbStatsSource_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (AutomodeQueries.Count > 0)
            {
                ContextMenu cm = new()
                {
                    MaxHeight = 400,
                    MaxWidth = 500
                };
                int iQ = -1;

                foreach (Query Q in AutomodeQueries)
                {
                    iQ++;
                    MenuItem item3 = new()
                    {
                        Header = string.Concat("[", iQ.ToString(), "] : ", Q.QueryAnalyzer.Tables[0].Name.Replace("_", "__"), " -> ", Q.OutputTable.Replace("_", "__")),
                        Foreground = Brushes.Black,
                        Background = Brushes.White,
                        FontSize = 10,
                    };
                    item3.Click += (s, eArgs) => { SourceQuery = Q; };
                    cm.Items.Add(item3);//add the item to the context menu
                }
                 ((Label)sender).ContextMenu = cm;//add the context menu to the sender
            }
        }

        private void DataGrid_CopyingRowClipboardContent(object sender, DataGridRowClipboardEventArgs e)
        {
            var dg = (DataGrid)sender;
            DataGridClipboardCellContent currentCell = e.ClipboardRowContent[dg.CurrentCell.Column.DisplayIndex];
            e.ClipboardRowContent.Clear();
            e.ClipboardRowContent.Add(currentCell);
        }

        public ShowSource(Job JobParameters, Query sQ, string sRawQuery, bool bWithDataAnalyzer)
        {
            InitializeComponent();

            INIP = JobParameters;
            MyLog = new LogTools("ShowSource", System.Reflection.MethodBase.GetCurrentMethod(), INIP, true)
            {
                LogLevel = LogTools.LOG_LEVEL.ERRORS_ONLY
            };

            AnalyzeData = bWithDataAnalyzer;

            if (sRawQuery != null)
            {
                SourceQuery = null;
                InitialSourceQuery = null;
                RawQuery = sRawQuery;
            }
            else if (sQ != null)
            {
                if (sQ.ConnectionSrc != null)
                {
                    if (sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI"))
                    {
                        bool bCSVSplittedInParts = false;
                        AutomodeQueries = Job.GetFilesQueriesFromSingleQuery(sQ, INIP, true, ref MyLog, ref bCSVSplittedInParts);
                        if (AutomodeQueries.Count > 1)
                        {
                            MessageBox.Show(Languages.Languages.ss_msg_multiplequeriesdetected);
                            lbStatsSource.BorderBrush = Brushes.Red;
                            lbStatsSource.BorderThickness = new Thickness(1, 1, 1, 1);
                        }
                        else if (AutomodeQueries.Count == 1)
                        {
                            sQ = AutomodeQueries[0];
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ss_msg_nomatchingquery);
                            btnLoadData.IsEnabled = false;
                        }
                    }
                    else if (sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB"))
                    {
                        AutomodeQueries = Job.GetSQLTablesFromSingleQuery(sQ, INIP, ref MyLog);
                        if (AutomodeQueries.Count > 1)
                        {
                            MessageBox.Show(Languages.Languages.ss_msg_multiplequeriesdetected);
                            lbStatsSource.BorderBrush = Brushes.Red;
                            lbStatsSource.BorderThickness = new Thickness(1, 1, 1, 1);
                        }
                        else if (AutomodeQueries.Count == 1)
                        {
                            sQ = AutomodeQueries[0];
                        }
                        else
                        {
                            if (sQ.ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_ODBC) // en ODBC on ne peut pas nécessairement récupérer les requêtes automatiquement (pas de pattern)
                            {
                                MessageBox.Show(Languages.Languages.ss_msg_nomatchingquery);
                                btnLoadData.IsEnabled = false;
                            }
                        }
                    }
                    else if (sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS"))
                    {
                        AutomodeQueries = Job.GetSQLTablesFromSingleQuery(sQ, INIP, ref MyLog);
                        if (AutomodeQueries.Count > 1)
                        {
                            MessageBox.Show(Languages.Languages.ss_msg_multiplequeriesdetected);
                            lbStatsSource.BorderBrush = Brushes.Red;
                            lbStatsSource.BorderThickness = new Thickness(1, 1, 1, 1);
                        }
                        else if (AutomodeQueries.Count == 1)
                        {
                            sQ = AutomodeQueries[0];
                        }
                        else
                        {
                            if (sQ.ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_ODBC) // en ODBC on ne peut pas nécessairement récupérer les requêtes automatiquement (pas de pattern)
                            {
                                MessageBox.Show(Languages.Languages.ss_msg_nomatchingquery);
                                btnLoadData.IsEnabled = false;
                            }
                        }
                    }
                }
                SourceQuery = sQ;
                InitialSourceQuery = new Query(JobParameters, sQ.RawQuery);
            }

            if (INIP.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
            {
                var tbMenuT = (TabItem)tcMenu.Items[1]; tbMenuT.Visibility = Visibility.Hidden;
                var tbMenuI = (TabItem)tcMenu.Items[2]; tbMenuI.Visibility = Visibility.Hidden;
                var tbMenuU = (TabItem)tcMenu.Items[3]; tbMenuU.Visibility = Visibility.Hidden;
                var tbMenuD = (TabItem)tcMenu.Items[4]; tbMenuD.Visibility = Visibility.Hidden;
            }

            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.9;
        }

        private void DsDataSource_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ContextMenu cm = new()
            {
                MaxHeight = 400,
                MaxWidth = 500
            };
            int iQ = -1;

            if (dsSourceAndSynchroData != null && dsSourceAndSynchroData.Count > 0 && dsSourceAndSynchroData[0].Tables.Count > 1)
            {
                foreach (DataTable dt in dsSourceAndSynchroData[0].Tables)
                {
                    iQ++;
                    MenuItem item3 = new()
                    {
                        Header = string.Concat("[", dt.TableName.Replace("_", "__"), "] : ", dt.Rows.Count.ToString(), Languages.Languages.ss_msg_rows),
                        Tag = iQ.ToString(),
                        Foreground = Brushes.Black,
                        Background = Brushes.White,
                        FontSize = 10,
                    };
                    item3.Click += (s, eArgs) => { LoadSourceDataTab(dt.TableName); };
                    cm.Items.Add(item3);//add the item to the context menu
                }
            } ((DataGrid)sender).ContextMenu = cm;//add the context menu to the sender
        }

        private void LoadSourceDataTab(string sTable)
        {
            AnalyzeAndShowData(dsSourceAndSynchroData[0].Tables[sTable], lbStatsSource, DsDataSource);
        }

        private async void BtnLoadData_Click(object sender, RoutedEventArgs e)
        {
            if (!TIMERCONSOLE.IsEnabled)
            {
                MyLog.ClearLogEvents(); // on veut éviter de chopper les anciens LOG

                try
                {
                    await INIT_GetData();
                }
                catch (OperationCanceledException)
                {
                    dsSourceAndSynchroData = null;
                    MessageBox.Show(Languages.Languages.ss_msg_cancelled);
                    DATA_LOADED = true;
                    DATA_LOADING = false;
                    Mouse.OverrideCursor = null;
                    this.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show(Languages.Languages.ss_msg_alreadyloading);
            }
        }

        private static bool CopyDataGridToCSV(DataTable dtData, string sFile)
        {
            bool bOK = true;
            try
            {
                StreamWriter fw = null;

                try { fw = new StreamWriter(sFile, false, Encoding.UTF8); }
                catch
                {
                    Directory.CreateDirectory(sFile[..(sFile.LastIndexOf("\\") + 1)]);
                    fw = new StreamWriter(sFile, false, Encoding.UTF8);
                }

                string[] sRow = new string[dtData.Columns.Count];
                //ajout entête
                for (int iC = 0; iC < dtData.Columns.Count; iC++)
                {
                    sRow[iC] = dtData.Columns[iC].ColumnName;
                }
                fw.WriteLine(string.Join(";", sRow));

                //ajout lignes
                for (int iR = 0; iR < dtData.Rows.Count; iR++)
                {
                    for (int iC = 0; iC < dtData.Columns.Count; iC++)
                    {
                        sRow[iC] = dtData.Rows[iR][iC].ToString().Replace(";", ".,");
                    }
                    fw.WriteLine(string.Join(";", sRow));
                }
                fw.Close();
                fw.Dispose();
            }
            catch { bOK = false; }

            return bOK;
        }

        private async Task INIT_GetData()
        {
            if (SourceQuery == null && RawQuery != null)
            {
                LaunchTimer();

                await ExecuteSQL(RawQuery, new Query());
            }
            else
            {
                int iMaxRows = 1000;
                try { iMaxRows = Convert.ToInt32(tbMaxRowsPreview.Text.Trim()); } catch { }
                if (iMaxRows < 1) { iMaxRows = 1; }
                MAX_PREVIEW_ROWS = iMaxRows;

                //1 dataset : les données source (à priori, 1 table)
                //2 dataset : les données de synchro (à priori, 4 tables : INSERT, UPDATE, DELETE, données cible)
                if (dsSourceAndSynchroData != null)
                {
                    foreach (DataSet ds in dsSourceAndSynchroData) { ds.Clear(); }
                }

                dsSourceAndSynchroData = new List<DataSet>();

                AnalyzeData = ckDataAnalyzer.IsChecked.Value;

                bool bWithSynchroData = true;
                if (INIP.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ss_msg_retrievesynchro01, Languages.Languages.ss_msg_retrievesynchro02, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES")) { bWithSynchroData = true; } else { bWithSynchroData = false; }
                }

                try
                {
                    SourceQuery = AutomodeQueries.Count > 0 ? new Query(INIP, SourceQuery.RawQuery) : new Query(INIP, InitialSourceQuery.RawQuery);
                    if (SourceQuery.CrossJoinQueries.Count > 0) { MessageBox.Show(Languages.Languages.ss_msg_crossjoininfo); }

                    btnLoadData.Content = Languages.Languages.ss_msg_loadingsource;
                    LaunchTimer();

                    await GetData(bWithSynchroData, iMaxRows);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    btnLoadData.Content = Languages.Languages.ss_msg_datacantbeloaded;
                    Mouse.OverrideCursor = null;
                    MessageBox.Show(Languages.Languages.ss_msg_unabletoretrieve + ex.Message);
                }
            }
        }

        private async Task GetData(bool bWithSynchroData, int iQteRows)
        {
            DATA_LOADING = true;

            EnableDisableButton(btnLoadData, false);
            try
            {
                //j'initialise certains paramètres que je ne veux pas appliquer
                int iFlagRead = INIP.MailFlagRetrievedAsRead; //je ne veux pas marquer les mails comme lus, c'est juste un test !
                INIP.MailFlagRetrievedAsRead = 0;
                INIP.RunInSimulationMode = true;

                //en mode streaming si on change la quantité de lignes récupérées, on foire potentiellement l'analyse
                bool bUseDataReader = false;
                if (INIP.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT || !bWithSynchroData) { bUseDataReader = true; }

                if (bUseDataReader)
                {
                    SourceQuery.ChangeLimitedResults(iQteRows);
                    INIP.ExportUseDataset = false;
                } //pour aller plus vite

                await Task.Factory.StartNew(() => dsSourceAndSynchroData = MThread.GetSourceData(0, INIP, ref SourceQuery, MyLog, bWithSynchroData));

                //je les remet comme avant
                INIP.MailFlagRetrievedAsRead = iFlagRead;
                INIP.RunInSimulationMode = false;

                int iDataSet = 0;

                foreach (DataSet dsData in dsSourceAndSynchroData)
                {
                    if (dsData.Tables.Count > 0)
                    {
                        if (iDataSet == 0) //source Data
                        {

                            ButtonWrite(Languages.Languages.ss_msg_analyzingsourcedata, btnLoadData);
                            Toolbox.InitDsDtNames(dsData, INIP, SourceQuery);
                            AnalyzeAndShowData(dsData.Tables[0], lbStatsSource, DsDataSource);

                            if (dsData.Tables.Count > 1) //un webservice peut retourner plusieurs tables
                            {
                                MessageBox.Show(Languages.Languages.ss_msg_additionaldatafound);
                            }
                        }

                        if (iDataSet == 1)
                        {
                            for (int iT = 0; iT < dsData.Tables.Count; iT++)
                            {
                                switch (iT)
                                {
                                    case 0:
                                        //INSERT
                                        ButtonWrite(Languages.Languages.ss_msg_analyzinginsertdata, btnLoadData);

                                        AnalyzeAndShowData(dsData.Tables[iT], lbStatsSInsert, DsDataSInsert);

                                        break;
                                    case 1:
                                        //UPDATE
                                        ButtonWrite(Languages.Languages.ss_msg_analyzingupdatedata, btnLoadData);

                                        AnalyzeAndShowData(dsData.Tables[iT], lbStatsSUpdate, DsDataSUpdate);
                                        break;
                                    case 2:
                                        //DELETE
                                        ButtonWrite(Languages.Languages.ss_msg_analyzingdeletedata, btnLoadData);

                                        AnalyzeAndShowData(dsData.Tables[iT], lbStatsSDelete, DsDataSDelete);
                                        break;
                                    case 3:
                                        //UPDATE OLD DATA
                                        //ButtonWrite(Languages.Languages.ss_msg_analyzingtargetdata, btnLoadData);
                                        //AnalyzeAndShowData(dsData.Tables[iT], lbStatsTarget, DsDataTarget);
                                        break;
                                    case 4:
                                        //TARGET DATA
                                        ButtonWrite(Languages.Languages.ss_msg_analyzingtargetdata, btnLoadData);

                                        AnalyzeAndShowData(dsData.Tables[iT], lbStatsTarget, DsDataTarget);
                                        break;
                                }
                            }
                        }
                    }
                    iDataSet++;
                }

                DATA_LOADED = true;
            }
            catch (OperationCanceledException)
            {
                DATA_LOADING = false;
                throw;
            }
            catch (Exception)
            {
                DATA_LOADING = false;
                throw;
            }
            EnableDisableButton(btnLoadData, true);

            DATA_LOADING = false;
        }

        private async Task ExecuteSQL(string sSQLCode, Query sQuery)
        {
            DATA_LOADING = true;

            EnableDisableButton(btnLoadData, false);

            try
            {
                LogTools SQLLog = new("SQLLog", System.Reflection.MethodBase.GetCurrentMethod(), INIP, false);
                SQLTools SQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref SQLLog);

                //dumb mais pas le choix
                if (sSQLCode.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    DataSet dsData = SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sSQLCode, sQuery, "SQLReadRawCode", false);
                    DataGridWrite(DsDataSource, SQLLog.GetFullLogAsDataTable().DefaultView);
                }
                else
                {
                    DataSet dsData = SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sSQLCode, sQuery, "SQLRawCommandCode", false);
                    DataGridWrite(DsDataSource, SQLLog.GetFullLogAsDataTable().DefaultView);
                }

                DATA_LOADED = true;
            }
            catch (OperationCanceledException)
            {
                DATA_LOADING = false;
                throw;
            }

            EnableDisableButton(btnLoadData, true);

            DATA_LOADING = false;
        }

        private void AnalyzeAndShowData(DataTable dtData, Label lblToShow, DataGrid dgToShow)
        {
            //Toolbox.AddOptionalColumnsInDatasetFromINI(dtData, INIP, ref INIP.GlobalParameters.RESERVED_SQL_COLUMNS);
            try
            {
                if (AnalyzeData)
                {
                    if (dtData.Rows.Count > (10000 * INIP.GlobalParameters.MULTITHREADING_CORES)) //joue-t-on l'analyseur si les données sont volumineuses ?
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ss_msg_hugedatabypassshs01, Languages.Languages.ss_msg_hugedatabypassshs02, MessageBoxButton.YesNo);
                        if (msgR.ToString().ToUpper().Equals("NO"))
                        {
                            SHSOperations SHS = new(INIP, SourceQuery, ref MyLog);
                            SourceQuery.QueryAnalyzer.SetFieldsAnalyzer(SHS.GetListFieldsTypesFromDataset(dtData, -1, SourceQuery, SQLTools_Enums.CLASS_PURPOSE.SRC));
                        }
                    }
                    else
                    {
                        SHSOperations SHS = new(INIP, SourceQuery, ref MyLog);
                        SourceQuery.QueryAnalyzer.SetFieldsAnalyzer(SHS.GetListFieldsTypesFromDataset(dtData, -1, SourceQuery, SQLTools_Enums.CLASS_PURPOSE.SRC));
                    }

                    if (dtData.Rows.Count > (10000 * INIP.GlobalParameters.MULTITHREADING_CORES)) //joue-t-on l'analyseur si les données sont volumineuses ?
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ss_msg_hugedatabypasspk01, Languages.Languages.ss_msg_hugedatabypasspk02, MessageBoxButton.YesNo);
                        if (msgR.ToString().ToUpper().Equals("NO"))
                        {
                            SHSOperations SHS = new(INIP, SourceQuery, ref MyLog);
                            SHS.GuessPKFromQuery(dtData, SourceQuery.ConnectionSrc.SqlCharTypeCompatibility, null);
                        }
                    }
                    else
                    {
                        SHSOperations SHS = new(INIP, SourceQuery, ref MyLog);
                        SHS.GuessPKFromQuery(dtData, SourceQuery.ConnectionSrc.SqlCharTypeCompatibility, null);
                    }
                }

                int iFieldsCount = dtData.Columns.Count;
                int iInitialRowCount;

                if (dtData.Rows.Count > MAX_PREVIEW_ROWS)
                {
                    dtData.AcceptChanges();
                    iInitialRowCount = dtData.Rows.Count;
                    int iR = dtData.Rows.Count - MAX_PREVIEW_ROWS;
                    for (int iC = 0; iC < iR; iC++)
                    {
                        dtData.Rows.RemoveAt(MAX_PREVIEW_ROWS);
                    }
                }
                else
                {
                    iInitialRowCount = dtData.Rows.Count;
                }

                StringBuilder sbData = new();
                sbData.AppendLine(Languages.Languages.ss_msg_infodata_01 + dtData.TableName.Replace("_", "__"));
                sbData.AppendLine(Languages.Languages.ss_msg_infodata_02 + dtData.Namespace.Replace("_", "__"));
                sbData.AppendLine(Languages.Languages.ss_msg_infodata_03 + iInitialRowCount.ToString());
                sbData.AppendLine(Languages.Languages.ss_msg_infodata_04 + iFieldsCount.ToString());
                LabelWrite(sbData.ToString(), lblToShow);

                if (SourceQuery.QueryAnalyzer != null)
                {
                    sbData.Clear();
                    sbData.AppendLine(Languages.Languages.ss_msg_infodata_05);
                    foreach (Query.QProperty qP in SourceQuery.QueryAnalyzer.QueryProperties)
                    {
                        if (qP.HasToBeShownToUser)
                        {
                            sbData.AppendLine(string.Concat("   [", qP.Element.Replace("_", "__"), "] ", qP.Property.ToString().Replace("_", "__"), " : ", qP.Value.Replace("_", "__")));
                        }
                    }
                    LabelWrite(sbData.ToString(), lbPropertiesSource);
                }

                DataGridWrite(dgToShow, dtData.DefaultView);
            }
            catch (OperationCanceledException ex)
            {
                LabelWrite(ex.Message, lbPropertiesSource);
            }
        }

        private delegate void ParametrizedMethodInvoker4(string arg, Label lb);

        private delegate void ParametrizedMethodInvoker5(string arg, Button lb);

        private delegate void ParametrizedMethodInvoker6(DataGrid dG, DataView dV);

        private delegate void ParametrizedMethodInvoker7(int iIndex);

        private delegate void ParametrizedMethodInvoker8(Button bBtn, bool bEnable);

        private delegate void ParametrizedMethodInvoker9(TextBlock tb, string sArg);

        private void ButtonWrite(string arg, Button lb)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.BeginInvoke(new ParametrizedMethodInvoker5(ButtonWrite), arg, lb);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            lb.Content = arg;
        }

        private void LabelWrite(string arg, Label lb)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.Invoke(new ParametrizedMethodInvoker4(LabelWrite), arg, lb);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            lb.Content = arg;
        }

        private void DataGridWrite(DataGrid dG, DataView dV)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.Invoke(new ParametrizedMethodInvoker6(DataGridWrite), dG, dV);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            //pour une raison qui m'échappe, les noms de colonnes avec des "." n'affichent pas leurs données... qui sont pourtant présentes dans la datatable
            //dG.Items.Clear();
            dG.Columns.Clear();

            //foreach (DataColumn dc in dV.Table.Columns)
            //{
            //    dc.Caption = dc.ColumnName;
            //    dc.ColumnName = Toolbox.RemoveSpecialCharacters(dc.ColumnName, (dc.Ordinal.ToString() + "_"), true);
            //}

            foreach (DataColumn dc in dV.Table.Columns)
            {
                var gridColumn = new DataGridTextColumn()
                {
                    Header = dc.Caption,
                    Binding = new Binding(string.Concat("[", dc.ColumnName, "]")),
                    SortMemberPath = dc.ColumnName
                };
                dG.Columns.Add(gridColumn);
            }


            StringBuilder sbData = new();

            for (int iC = 0; iC < dG.Columns.Count; iC++)
            {
                if (SourceQuery != null && SourceQuery.QueryAnalyzer.Fields.Count > iC)
                {
                    if (SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer != null)
                    {
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition01 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnName);
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition02 + (SourceQuery.QueryAnalyzer.Fields.Count > iC ? SourceQuery.QueryAnalyzer.Fields[iC].Raw : ""));
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition03 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnType + (SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnSize.Length == 0 ? "" : "(" + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnSize + ")"));
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition04 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnLinqType.ToString().Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition05 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.ColumnAllowsNullValues.ToString());
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition06 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.IsUnique.ToString());
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition07 + SourceQuery.QueryAnalyzer.Fields[iC].FieldAnalyzer.DriverData.ToString());
                    }
                    else
                    {
                        sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition01);
                    }
                }
                else
                {
                    sbData.AppendLine(Languages.Languages.ss_msg_fielddefinition01 + dV.Table.Columns[iC].ColumnName);
                }

                var style = new Style();
                foreach (Setter sT in dG.Columns[iC].HeaderStyle.Setters)
                {
                    style.Setters.Add(sT);
                }
                style.Setters.Add(new Setter(ToolTipService.ToolTipProperty, sbData.ToString()));
                //style.Setters.Add(new Setter(BackgroundProperty, Brushes.LightSteelBlue));
                //style.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
                //style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(5, 5, 5, 5)));
                //style.Setters.Add(new Setter(DataGrid.ColumnHeaderHeightProperty, 20));
                dG.Columns[iC].HeaderStyle = style;

                dG.Columns[iC].Header = dG.Columns[iC].Header.ToString().Replace("_", "__");
                sbData.Clear();
            }

            try { dG.ItemsSource = dV; } catch (Exception ex) { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "Can't Load Data. Unknown Reason.", SQLTools_Enums.LOG_TYPEINFO.ERR); }
        }

        private void ChangeMenuIndex(int idx)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.Invoke(new ParametrizedMethodInvoker7(ChangeMenuIndex), idx);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            tcMenu.SelectedIndex = idx;
        }

        private void EnableDisableButton(Button bBtn, bool bEnable)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.Invoke(new ParametrizedMethodInvoker8(EnableDisableButton), bBtn, bEnable);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            bBtn.IsEnabled = bEnable;
        }

        private void TextBlockWrite(TextBlock tb, string sArg)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Task.Factory.StartNew(() =>
                {
                    Dispatcher?.Invoke(new ParametrizedMethodInvoker9(TextBlockWrite), tb, sArg);
                });

                return;
            }
            //tbConsole.Text = string.Concat(tbConsole.Text, Environment.NewLine, arg);
            tb.Text = sArg;
        }

        private void JobController(object sender, EventArgs e)
        {
            if (DATA_LOADED)
            {
                DATA_LOADED = false;
                StringBuilder sbLog = new();
                foreach (LogObject LO in MyLog.LogEvents)
                {
                    sbLog.AppendLine(LO.ToString_ForUI());
                }
                if (!MyLog.HasNoErrors)
                {
                    Help xamlHelp = new(new BlockHelp("ShowSource", Languages.Languages.ss_msg_datanotloadederror01, sbLog.ToString(), ""));
                    xamlHelp.ShowDialog();
                    ButtonWrite(Languages.Languages.ss_msg_datanotloadederror02, btnLoadData);
                }
                else
                {
                    if (MyLog.LogEvents.Count > 0)
                    {
                        Help xamlHelp = new(new BlockHelp("ShowSource", Languages.Languages.ss_msg_dataloadedwarnings, sbLog.ToString(), ""));
                        xamlHelp.ShowDialog();
                    }
                    ButtonWrite(Languages.Languages.ss_msg_dataloaded, btnLoadData);
                }

                sbLog.Clear();
                ChangeMenuIndex(0);
                TIMERCONSOLE.IsEnabled = false;
            }
        }

        private void LaunchTimer()
        {
            TIMERCONSOLE.Tick += (sender, e) => JobController(sender, e);
            //tTimerConsole.Tick += new EventHandler(JobController);
            TimeSpan tsSpan = new(0, 0, 1);
            TIMERCONSOLE.Interval = tsSpan;
            TIMERCONSOLE.Start();
        }

        private void InterruptLoading()
        {
            if (DATA_LOADING)
            {
                Monitoring.CancelJob();
                MessageBox.Show(Languages.Languages.ma_msg_canceljob);
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                this.IsEnabled = false;
                int iWait = 0;
                
                while (DATA_LOADING)
                {
                    iWait++;
                    Thread.Sleep(1000);
                    if (iWait > 10)
                    {
                        DATA_LOADED = false;
                        DATA_LOADING = false;
                        break;
                    } //10sec
                }

                Mouse.OverrideCursor = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

    }
}
