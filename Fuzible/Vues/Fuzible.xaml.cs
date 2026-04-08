using Fuzible.Controleurs;
using FuzibleFramework;
using FuzibleUITools;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Fuzible
{
    public partial class UI : Window
    {
        #region "VARIABLES"

        //public INIProgram FuzibleController;
        private Fuzible_CTL FuzibleController;
        public ProgramHelp AppHelp; //attention : initialisé dans la procédure de démarrage
        public Help QueryHelp;
        private bool WindowFocused = false;

        //private CancellationTokenSource _ctsExecuteTaskCancel = new CancellationTokenSource();
        private static readonly string BYPASS_PWD = string.Concat(SHSConstantes.PROGRAM_PWD, Environment.UserName.ToLower());
        private string USERNAME;

        private int TUTORIAL_STEP = -1;
        private int TUTORIAL_MODE = 0;
        private static Query TEMP_QUERY = null;

        private static string DELEGATE_USERNAME { set; get; } = "";

        private static readonly DispatcherTimer TIMERCONSOLE = new();
        private static readonly DispatcherTimer TIMERINTELLISENSE = new();
        private static readonly IntellisenseData INTELLISENSE = new();
        private int INTELLISENSE_REFRESH = 0;

        private LogConsole console;
        private CancellationTokenSource cancelTokenBGTasks = new();

        private int JobStatus = 5;

        #endregion

        public UI(string _delegateUser, Fuzible_CTL _controller)
        {
            FuzibleController = _controller;

            PasswordControl(false);

            AppDomain.CurrentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(AppDomain_UnhandledException);
            //AppDomain.CurrentDomain.FirstChanceException += App_FirstChanceException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; //Example 4 

            InitializeComponent();

            MiShowLiveHelpStart.IsChecked = FuzibleController.MainParams.SHOW_LIVE_HELP_START;

            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.90;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double windowWidth = ActualWidth;
            double windowHeight = Height;

            Left = screenWidth * (1.0 / 3.0);  // 1/3 de la largeur depuis la gauche
            Top = (screenHeight - windowHeight) / 2;  // Centré verticalement

            KeyDown += HandleTutorialKeyPress;
            IsEnabled = false;
            //chargement de l'interface
            DataObject.AddPastingHandler(tbQueries, new DataObjectPastingEventHandler(TextBoxPasting));
            Closed += new EventHandler(MainWindow_Closed);

            tbConsole.Document.PageWidth = 795;

            USERNAME = FuzibleController.GetUsername();
            //mode utilisateurs partagés
            DELEGATE_USERNAME = _delegateUser;

            LaunchTimer();

            try
            {
                AppHelp = new ProgramHelp(FuzibleController.MainParams.APP_LANGUAGE);
            }
            catch (Exception ex)
            {
                FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, Languages.Languages.ma_msg_xmlhelpnotfound, SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            if (FuzibleController.FIRST_START)
            {
                MessageBox.Show(Languages.Languages.ma_msg_switchlanguage);

                //premier tutorial forcé.
                Help xamlHelp1 = new(AppHelp.GetHelpBlock("TUTORIAL 01"));
                xamlHelp1.ShowDialog();
                TUTORIAL_MODE = 1;

                MessageBox.Show(Languages.Languages.ma_msg_tutorialinfo01 +
                                Environment.NewLine +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo02 +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo03 +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo04);

                ClearFields();
                cbINISection.SelectedIndex = -1;

                TUTORIAL_STEP = 0;
                BorderBrush = System.Windows.Media.Brushes.Red;
                BorderThickness = new Thickness(2);
                //fin tutorial
            }

            //chargement console + intellisense + events

            console = new LogConsole();
            //console.ShowConsole();

            if (FuzibleController.OWN_USER)
            {
                if (FuzibleController.MainParams.ENABLE_QUERY_ASSISTANT)
                {
                    INTELLISENSE.Enable();
                }
            }

            LogTools.OnNewLogEvent += EventsReceiver_NewLog;
            BackgroundTask.OnFinishedTask += EventReceiver_BackgroundTaskFinished;
            BackgroundTask.OnRunningTask += EventReceiver_BackgroundTaskRunning;
            BackgroundTask.OnStartedTask += EventReceiver_BackgroundTaskStarted;
            BackgroundTask.OnCancelledTask += EventReceiver_BackgroundTaskCancelled;
            IntellisenseData.OnRunningIntellisense += EventReceiver_Intellisense;
            Fuzible_CTL.OnJobEvent += EventsReceiver_Job;
            RichTB.OnColorizingField += EventsReceiver_ColorizingFields;
            //LogConsole

            FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(INIProgram.APP_NAME, ", Version ", INIProgram.APP_VERSION.ToString()), SQLTools_Enums.LOG_TYPEINFO.DET);

            this.ShowDialog();
        }

        #region "EVENEMENTS"

        private void Fuzible_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            { Program.LiveHelp.ChangeLockedStatus(); }

            if (e.Key == Key.F8)
            {
                if (FuzibleController.LOG.StepByStepMode && FuzibleController.LOG.WaitUserAction)
                {
                    FuzibleController.LOG.ContinueStepByStepJobExecution(0);
                    btnExecuteStepByStep.IsEnabled = false;
                    btnExecuteTask.IsEnabled = false;
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachMouseDownEventToAllControls(this.gdUI);

            if (FuzibleController.MainParams.SHOW_LIVE_HELP_START)
            {
                Program.LiveHelp = new Help(new BlockHelp("Fuzible", "MiLiveHelp", "", ""));
                Program.LiveHelp.Show();
                Program.LiveHelp.ChangeLockedStatus();
            }
        }

        private void AttachMouseDownEventToAllControls(DependencyObject parent)
        {
            foreach (var child in GetVisualChildren(parent))
            {
                if (child is UIElement uiElement)
                {
                    switch (uiElement)
                    {
                        case System.Windows.Controls.TextBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        //case System.Windows.Controls.Label:
                        //    uiElement.MouseMove += UIElement_MouseDown;
                        //    break;
                        case System.Windows.Controls.Button:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.RichTextBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.ComboBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.CheckBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.Slider:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.MenuItem:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                    }

                    // Récursivement, attacher des gestionnaires d'événements pour les éléments enfants
                    AttachMouseDownEventToAllControls(uiElement);
                }
            }
        }

        private static IEnumerable<DependencyObject> GetVisualChildren(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                yield return VisualTreeHelper.GetChild(parent, i);
            }
        }

        private async void UIElement_MouseDown(object sender, MouseEventArgs e)
        {
            if (Program.LiveHelp != null && Program.LiveHelp.IsOpen)
            {
                if (!WindowFocused)
                {
                    Keyboard.Focus(this);
                    WindowFocused = true;
                }

                string sControlName = "";
                switch (sender)
                {
                    case System.Windows.Controls.TextBox:
                        sControlName = ((TextBox)sender).Name;
                        break;
                    //case System.Windows.Controls.Label:
                    //    sControlName = ((Label)sender).Name;
                    //    break;
                    case System.Windows.Controls.Button:
                        sControlName = ((Button)sender).Name;
                        break;
                    case System.Windows.Controls.RichTextBox:
                        sControlName = ((RichTextBox)sender).Name;
                        break;
                    case System.Windows.Controls.ComboBox:
                        sControlName = ((ComboBox)sender).Name;
                        break;
                    case System.Windows.Controls.CheckBox:
                        sControlName = ((CheckBox)sender).Name;
                        break;
                    case System.Windows.Controls.Slider:
                        sControlName = ((Slider)sender).Name;
                        break;
                    case System.Windows.Controls.ListBox:
                        sControlName = ((ListBox)sender).Name;
                        break;
                    case System.Windows.Controls.MenuItem:
                        sControlName = ((MenuItem)sender).Name;
                        break;

                }

                if (sControlName.Length > 0)
                {
                    await Program.LiveHelp.LoadHelpBlock(new BlockHelp("Fuzible", sControlName, "", ""));
                }
            }
        }

        private void Event_ConfigurationClosed(object sender, EventArgs e)
        {
            //on recharge la liste des connections parce que potentiellement, elle a changé (la liste se modifie dans les paramètres généraux)
            try { FuzibleController.ReloadINI(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            LoadUIListConnections(((Configuration)sender).JobDbSource, ((Configuration)sender).JobDbTarget);
        }

        private void EventsReceiver_Job(object sender, (int, Job, string) e)
        {
            JobStatus = e.Item1;

            //0=départ, 1=cancel, 2=exception, 3=fini correctement
            switch (e.Item1)
            {
                case 0:
                    Dispatcher.Invoke(() => { tbConsole.Document.Blocks.Clear(); tbConsole.Document.PageWidth = 795; });
                    break;
                case 1:
                    ReleaseJob();
                    break;
                case 2:

                    ReleaseJob();

                    break;
                case 3:
                    Dispatcher.Invoke(() =>
                    {
                        lbJobVersion.Content = "Job Version : " + e.Item2.JobVersionWithUser;

                        string sLog = FuzibleController.LOG.CreateJobHTMLSummary(System.Net.WebUtility.HtmlEncode(e.Item2.JobNAME + " - " + e.Item2.JobVersion));

                        Help xamlHelp = new(new BlockHelp("Fuzible", string.Concat(Languages.Languages.ma_msg_jobstatus, e.Item3), sLog, e.Item2.JobLastLaunchStatus, true));
                        xamlHelp.ShowDialog();
                    });
                    ReleaseJob();
                    break;
            }
        }

        private void EventsReceiver_NewLog(object sender, LogObject log)
        {
            if (FuzibleController.LOG.LogRate > 10) // pour éviter que l'UI se fige à cause d'un rate trop élevé
            {
            }
            else
            {
                string sMessage = string.Concat("[", log.TLevel, "] ", log.SMessage);
                LogColor cColor = LogColor.white;
                Brush lColor = Brushes.Orange;
                switch (log.TLevel)
                {
                    case SQLTools_Enums.LOG_TYPEINFO.INF:
                        cColor = LogColor.white;
                        //lColor = Brushes.Black;
                        break;
                    case SQLTools_Enums.LOG_TYPEINFO.ERR:
                        cColor = LogColor.red;
                        //lColor = Brushes.Red;
                        break;
                    case SQLTools_Enums.LOG_TYPEINFO.WNG:
                        cColor = LogColor.orange;
                        //lColor = Brushes.Orange;
                        break;
                    case SQLTools_Enums.LOG_TYPEINFO.DBG:
                        cColor = LogColor.darkgray;
                        //lColor = Brushes.DarkGray;
                        break;
                    case SQLTools_Enums.LOG_TYPEINFO.DET:
                        cColor = LogColor.gray;
                        //lColor = Brushes.Gray;
                        break;
                }

                Dispatcher.Invoke(() =>
                {
                    var lLevel = (LogTools.LOG_LEVEL)Enum.Parse(typeof(LogTools.LOG_LEVEL), cbNiveauLog.SelectedValue.ToString());
                    bool bDoNotShow = false;

                    switch (lLevel)
                    {
                        case LogTools.LOG_LEVEL.ERRORS_ONLY:
                            if (log.TLevel == SQLTools_Enums.LOG_TYPEINFO.DET) { bDoNotShow = true; }
                            if (log.TLevel == SQLTools_Enums.LOG_TYPEINFO.DBG) { bDoNotShow = true; }
                            if (log.TLevel == SQLTools_Enums.LOG_TYPEINFO.WNG) { bDoNotShow = true; }
                            break;
                        case LogTools.LOG_LEVEL.ERRORS_MESSAGES:
                            if (log.TLevel == SQLTools_Enums.LOG_TYPEINFO.DET) { bDoNotShow = true; }
                            if (log.TLevel == SQLTools_Enums.LOG_TYPEINFO.DBG) { bDoNotShow = true; }
                            break;
                        case LogTools.LOG_LEVEL.ERRORS_MESSAGES_DETAIL:
                            break;
                    }

                    if (!bDoNotShow)
                    {
                        if (!cbLogView.IsChecked.Value)
                        {
                            TextRange tr = new TextRange(tbConsole.Document.ContentEnd, tbConsole.Document.ContentEnd);
                            tr.Text = string.Concat(Environment.NewLine, log.ToString());
                            AdjustRichTextBoxWidth(tbConsole, log.ToString());
                            switch (log.TLevel)
                            {
                                case SQLTools_Enums.LOG_TYPEINFO.WNG:
                                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Orange);
                                    break;
                                case SQLTools_Enums.LOG_TYPEINFO.ERR:
                                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);
                                    break;
                                case SQLTools_Enums.LOG_TYPEINFO.INF:
                                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.White);
                                    break;
                                default:
                                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Gray);
                                    break;

                            }

                            //ConsoleWrite(Languages.Languages.ma_msg_logviewerlog, true);
                            //autoscroll
                            if (scConsole.VerticalOffset == scConsole.ScrollableHeight)
                            {
                                scConsole.ScrollToEnd();
                            }
                        }
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    lbPendingTask.Foreground = lColor;
                    lbPendingTask.Content = lbPendingTask.Content = log.SMessage;
                    console.WriteMessage($"{DateTime.Now.ToString("HH:mm:ss")} - {sMessage}{Environment.NewLine}", cColor);
                });
            }
        }

        private void EventsReceiver_StepByStepLog(object sender, LogObject log)
        {
            string sMessage = string.Concat("[", log.TLevel, "] ", log.SMessage);
            LogColor cColor = LogColor.white;
            Brush lColor = Brushes.Orange;
            switch (log.TLevel)
            {
                case SQLTools_Enums.LOG_TYPEINFO.INF:
                    cColor = LogColor.white;
                    //lColor = Brushes.Black;
                    break;
                case SQLTools_Enums.LOG_TYPEINFO.ERR:
                    cColor = LogColor.red;
                    //lColor = Brushes.Red;
                    break;
                case SQLTools_Enums.LOG_TYPEINFO.WNG:
                    cColor = LogColor.orange;
                    //lColor = Brushes.Orange;
                    break;
                case SQLTools_Enums.LOG_TYPEINFO.DBG:
                    cColor = LogColor.darkgray;
                    //lColor = Brushes.DarkGray;
                    break;
                case SQLTools_Enums.LOG_TYPEINFO.DET:
                    cColor = LogColor.gray;
                    //lColor = Brushes.Gray;
                    break;
            }

            Dispatcher.Invoke(() =>
            {
                tcMenu.SelectedIndex = 4;
                btnExecuteStepByStep.IsEnabled = true;
                btnExecuteTask.IsEnabled = true;
                tbExecuteStepByStep.Text = Languages.Languages.ma_jobconf_btn_startjobstepbystep_continue;
                tbExecuteTask.Text = Languages.Languages.ma_jobconf_btn_startjobstepbystep_toend;
            });
        }

        private void EventReceiver_Intellisense(object sender, string e)
        {
            if (e.Length > 0)
            {
                Dispatcher.Invoke(() => { lbQueryLoading.Content = e; });
            }
        }

        private void EventsReceiver_ColorizingFields(object sender, string e)
        {
            Dispatcher.Invoke(() => { lbQueryLoading.Content = e; });
        }

        private void EventReceiver_BackgroundTaskRunning(object sender, LogObject e)
        {
            if (e != null)
            {
                FuzibleController.LOG.LogMessage(e.MiMethod, e.Cpurpose, null, e.SMessage, e.TLevel);
            }
            //else
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        lbPendingTask.Content = "";
            //    });
            //}
        }

        private void EventReceiver_BackgroundTaskStarted(object sender, BackgroundTask.TaskType sTask)
        {
            switch (sTask)
            {
                default:
                    Dispatcher.Invoke(() => { tbConsole.Document.Blocks.Clear(); tbConsole.Document.PageWidth = 795; tcMenu.SelectedIndex = 4; });
                    break;
            }
        }

        private void EventReceiver_BackgroundTaskCancelled(object sender, OperationCanceledException e)
        {
            ReleaseJob();
        }

        private void EventReceiver_BackgroundTaskFinished(object sender, (BackgroundTask.TaskType, object) e)
        {
            //Dispatcher.Invoke(() => { lbPendingTask.Content = Languages.Languages.ma_msg_nobgtaskrunning; });

            switch (e.Item1)
            {
                case BackgroundTask.TaskType.LOAD_PLANIF_CALENDAR:

                    string sCalendar = (string)e.Item2;
                    Dispatcher.Invoke(() =>
                    {
                        if (sCalendar.Length > 0)
                        {
                            try
                            {
                                if (!Directory.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\HELP\\"))
                                {
                                    Directory.CreateDirectory(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\HELP\\");
                                }

                                string sFile = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "\\HELP\\", "CALENDAR_", "", USERNAME, ".HTML");

                                StreamWriter sw = new(sFile, false);
                                sw.Write(sCalendar);
                                sw.Close();
                                ProcessStartInfo startInfo = new(sFile) { UseShellExecute = true };
                                Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Languages.Languages.ma_msg_planifcalendarunabletogenerate + ex.Message);
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_planifcalendar_nothingtoshow); }
                    });

                    break;

                case BackgroundTask.TaskType.BENCHMARK:
                    try { File.Delete(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\FILES\\BENCHMARK.CSV"); } catch { }
                    break;

                case BackgroundTask.TaskType.EXECUTE_SINGLE_QUERY:
                    Dispatcher.Invoke(() =>
                    {
                        tcMenu.SelectedIndex = 4;
                    });
                    break;

                case BackgroundTask.TaskType.GET_COLUMN_MAPPING:
                    string sMapping = (string)e.Item2;
                    var hlpBlock = new BlockHelp("Fuzible", "Mapping Information", sMapping, "", true);
                    Dispatcher.Invoke(() =>
                    {
                        Help h = new(hlpBlock);
                        h.Show();
                    });
                    break;

                case BackgroundTask.TaskType.CHECK_CROSSQUERY:
                    string sJoin = (string)e.Item2;
                    MessageBox.Show(sJoin);
                    break;

                case BackgroundTask.TaskType.CHECK_SYNCHRO_VALIDITY:
                    string sStatus = (string)e.Item2;
                    MessageBox.Show(sStatus);
                    break;

                default:
                    break;
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, e.Exception, "", SQLTools_Enums.LOG_TYPEINFO.WNG);
            //string sEx = Languages.Languages.ma_msg_jobkilled;

            //StreamWriter sw = new StreamWriter("ERROR.TXT", true, Encoding.UTF8);
            //sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";ERR;UnobservedTask_Exception;", (e.Exception.Message + " / " + e.Observed.ToString()), " (", Toolbox.GetAppContext(), ")"));

            //try
            //{
            //    if (JobStatus > 0)
            //    {
            //        MessageBox.Show(Languages.Languages.ma_msg_canceljob);
            //        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            //        ReleaseJob();
            //        Mouse.OverrideCursor = null;
            //    }
            //}
            //catch (Exception ex)
            //{ sEx = ex.Message; }

            //sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";INF;Job_Status;", sEx));
            //sw.Close();
            //MessageBox.Show(string.Concat("The Application Crashed :", Environment.NewLine, (e.Exception.Message + " / " + e.Observed.ToString()), Environment.NewLine, Toolbox.GetAppContext()));
            ////System.Environment.Exit(1);
        }

        private void AppDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            string sEx = Languages.Languages.ma_msg_jobkilled;

            StreamWriter sw = new("ERROR.TXT", true, Encoding.UTF8);
            sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";ERR;Unhandled_Exception;", (e.ExceptionObject as Exception).Message, " (", Toolbox.GetAppContext(), ")"));

            try
            {
                if (JobStatus == 0)
                {
                    MessageBox.Show(Languages.Languages.ma_msg_canceljob);
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    Monitoring.CancelJob();
                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            { sEx = ex.Message; }

            sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";INF;Job_Status;", sEx));
            sw.Close();
            MessageBox.Show(string.Concat("The Application Crashed !", Environment.NewLine, Environment.NewLine, (e.ExceptionObject as Exception).Message, Environment.NewLine, Toolbox.GetAppContext()));
            //System.Environment.Exit(1);
        }

        private void App_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            string sEx = Languages.Languages.ma_msg_jobkilled;

            StreamWriter sw = new("ERROR.TXT", true, Encoding.UTF8);
            sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";ERR;FirstChance_Exception;", e.Exception.Message, " (", Toolbox.GetAppContext(), ")"));

            try
            {
                if (JobStatus == 0)
                {
                    MessageBox.Show(Languages.Languages.ma_msg_canceljob);
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    Monitoring.CancelJob();
                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            { sEx = ex.Message; }

            sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";INF;Job_Status;", sEx));
            sw.Close();
            MessageBox.Show(string.Concat("The Application Crashed :", Environment.NewLine, e.Exception.Message, Environment.NewLine, Toolbox.GetAppContext()));
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (JobStatus == 0)
            {
                MessageBox.Show(Languages.Languages.ma_msg_canceljob);
                FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_cancellingsubthreads + Monitoring.GetQteThreads.ToString() + ")...", SQLTools_Enums.LOG_TYPEINFO.INF);
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                Monitoring.CancelJob();

                Exception ex = new(Languages.Languages.ma_msg_jobcancelled);
                FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, "", SQLTools_Enums.LOG_TYPEINFO.ERR);

                Mouse.OverrideCursor = null;
            }

            System.Environment.Exit(0);
        }

        private void HandleTutorialKeyPress(object sender, KeyEventArgs e)
        {
            if (TUTORIAL_STEP > -1)
            {
                switch (e.Key)
                {
                    case Key.F2:
                        if (TUTORIAL_STEP > 1)
                        {
                            TUTORIAL_STEP--;
                            TutorialMoveStep(TUTORIAL_STEP, TUTORIAL_MODE);
                        }
                        break;
                    case Key.F1:
                        if (TUTORIAL_STEP < 42)
                        {
                            TUTORIAL_STEP++;
                            TutorialMoveStep(TUTORIAL_STEP, TUTORIAL_MODE);
                        }
                        break;
                    case Key.Escape:
                        TutorialReinitCanvas();
                        ClearFields();
                        MessageBox.Show(Languages.Languages.ma_msg_tutorialmodeexited);
                        break;
                }
            }
        }

        private void TutorialMoveStep(int iTutoStep, int iTutoMode)
        {
            switch (iTutoStep)
            {
                case 1:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        default:
                            TutorialInitStep(Array.Empty<string>(), "", cvUIButtons);
                            TutorialInitStep(new string[] { "tbJobDescription", "lbJobDescription" }, Languages.Languages.ma_msg_infotutorial01_00, cvJobConfiguration);
                            break;
                    }
                    break;
                case 2:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "cbModeExtraction", "lbModeExtraction" }, Languages.Languages.ma_msg_infotutorial01_01, cvJobConfiguration);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "cbModeExtraction", "lbModeExtraction" }, Languages.Languages.ma_msg_infotutorial02_01, cvJobConfiguration);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "cbModeExtraction", "lbModeExtraction" }, Languages.Languages.ma_msg_infotutorial03_01, cvJobConfiguration);
                            break;
                    }
                    break;
                case 3:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        case 1:
                            cbModeExtraction.SelectedIndex = 0;
                            TutorialInitStep(new string[] { "lbQueryVariableParameters", "tbQueryVariableParameters" }, Languages.Languages.ma_msg_infotutorial01_02, cvJobConfiguration);
                            break;
                        case 2:
                            cbModeExtraction.SelectedIndex = 0;
                            TutorialInitStep(new string[] { "lbQueryVariableParameters", "tbQueryVariableParameters" }, Languages.Languages.ma_msg_infotutorial02_02, cvJobConfiguration);
                            break;
                        case 3:
                            cbModeExtraction.SelectedIndex = 1;
                            TutorialInitStep(new string[] { "cbSQLSynchroTargetBehavior" }, Languages.Languages.ma_msg_infotutorial03_02, cvJobConfiguration);
                            break;
                    }
                    break;
                case 4:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "lbQueryVariableParameters", "tbQueryVariableParameters" }, Languages.Languages.ma_msg_infotutorial01_03, cvJobConfiguration);
                            RichTB.SetTextRTB(tbQueryVariableParameters, "50;%YYYY-%MM-%DD");
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "lbQueryVariableParameters", "tbQueryVariableParameters" }, Languages.Languages.ma_msg_infotutorial02_03, cvJobConfiguration);
                            RichTB.SetTextRTB(tbQueryVariableParameters, "helloWorld;[2];%SC1;%USER"); //programmation d'un double target ainsi que d'une commande pré-job alimentant le paramètre
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "ckSynchroStoreChanges" }, Languages.Languages.ma_msg_infotutorial03_03, cvJobConfiguration);
                            break;
                    }
                    break;
                case 5:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        default:
                            TutorialInitStep(new string[] { "lbNiveauLog", "cbNiveauLog" }, Languages.Languages.ma_msg_infotutorial01_04, cvJobConfiguration);
                            var tc0 = (TabItem)tcMenu.Items[0];
                            tc0.IsEnabled = true;
                            var tc1 = (TabItem)tcMenu.Items[1];
                            tc1.IsEnabled = false;
                            break;
                    }
                    break;
                case 6:
                    tcMenu.SelectedIndex = 0;
                    switch (iTutoMode)
                    {
                        default:
                            TutorialInitStep(Array.Empty<string>(), "", cvJobConfiguration);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial01_05, cvUI);
                            var tc00 = (TabItem)tcMenu.Items[0];
                            tc00.IsEnabled = false;
                            var tc11 = (TabItem)tcMenu.Items[1];
                            tc11.IsEnabled = true;
                            break;
                    }
                    break;
                case 7:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "cbConnSource", "lbSourcesList" }, Languages.Languages.ma_msg_infotutorial01_06, cvSource);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "cbConnSource", "lbSourcesList" }, Languages.Languages.ma_msg_infotutorial02_06, cvSource);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "cbConnSource", "lbSourcesList" }, Languages.Languages.ma_msg_infotutorial03_06, cvSource);
                            break;
                    }
                    break;
                case 8:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "btnTestBDDExport" }, Languages.Languages.ma_msg_infotutorial01_07, cvSource);
                            cbConnSource.SelectedValue = "[1]";
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "btnTestBDDExport" }, Languages.Languages.ma_msg_infotutorial02_07, cvSource);
                            cbConnSource.SelectedValue = "[5]";
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "btnTestBDDExport" }, Languages.Languages.ma_msg_infotutorial03_07, cvSource);
                            cbConnSource.SelectedValue = "[2]";
                            break;
                    }
                    break;
                case 9:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        case 1:
                            CanvasFileSource.Effect = null;
                            CanvasFileSource.IsEnabled = true;
                            TutorialInitStep(new string[] { "lbCSVImport", "cbCSVImport" }, Languages.Languages.ma_msg_infotutorial01_08, CanvasFileSource);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "cbBDDExport" }, Languages.Languages.ma_msg_infotutorial02_08, cvSource);
                            break;
                        case 3:
                            CanvasFileSource.Effect = null;
                            CanvasFileSource.IsEnabled = true;
                            TutorialInitStep(new string[] { "lbCSVImport", "cbCSVImport" }, Languages.Languages.ma_msg_infotutorial03_08, CanvasFileSource);
                            break;
                    }
                    break;
                case 10:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Source", "tbPostJobCommandSource" }, Languages.Languages.ma_msg_infotutorial01_09, cvSource);
                            cbCSVImport.SelectedIndex = 0;
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Source", "tbPostJobCommandSource" }, Languages.Languages.ma_msg_infotutorial02_09, cvSource);
                            break;
                        case 3:
                            cbCSVImport.SelectedIndex = 0;
                            CanvasFileSourceXLS.Effect = null;
                            CanvasFileSourceXLS.IsEnabled = true;
                            TutorialInitStep(new string[] { "tbXSLSheetToRead", "lbXSLSheetToRead" }, Languages.Languages.ma_msg_infotutorial03_09, CanvasFileSourceXLS);
                            break;
                    }
                    break;
                case 11:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Source", "tbPostJobCommandSource" }, Languages.Languages.ma_msg_infotutorial01_10, cvSource);
                            RichTB.SetTextRTB(tbPostJobCommandSource, "DATE /T");
                            cbPrePostJobCommands_Source.SelectedIndex = 0;
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Source", "tbPostJobCommandSource" }, Languages.Languages.ma_msg_infotutorial02_10, cvSource);
                            RichTB.SetTextRTB(tbPostJobCommandSource, "SELECT COUNT(*) FROM user_parameters;");
                            cbPrePostJobCommands_Source.SelectedIndex = 0;
                            break;
                        case 3:
                            CanvasFileSourceXLS.Effect = null;
                            CanvasFileSourceXLS.IsEnabled = true;
                            TutorialInitStep(new string[] { "tbXLSRowOffset", "lbXLSRowOffset" }, Languages.Languages.ma_msg_infotutorial03_10, CanvasFileSourceXLS);
                            break;
                    }
                    var tc000 = (TabItem)tcMenu.Items[1];
                    tc000.IsEnabled = true;
                    var tc111 = (TabItem)tcMenu.Items[2];
                    tc111.IsEnabled = false;
                    break;
                case 12:
                    tcMenu.SelectedIndex = 1;
                    switch (iTutoMode)
                    {
                        default:
                            TutorialInitStep(Array.Empty<string>(), "", cvSource);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial01_11, cvUI);
                            var tc0000 = (TabItem)tcMenu.Items[1];
                            tc0000.IsEnabled = false;
                            var tc1111 = (TabItem)tcMenu.Items[2];
                            tc1111.IsEnabled = true;
                            tbXSLSheetToRead.Text = "2";
                            tbXLSRowOffset.Text = "1";
                            break;
                    }
                    break;
                case 13:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "lbTargetList", "cbConnTarget" }, Languages.Languages.ma_msg_infotutorial01_12, cvTarget);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "lbTargetList", "cbConnTarget" }, Languages.Languages.ma_msg_infotutorial02_12, cvTarget);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "lbTargetList", "cbConnTarget" }, Languages.Languages.ma_msg_infotutorial03_12, cvTarget);
                            break;
                    }
                    break;
                case 14:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "btnTestBDDImport" }, Languages.Languages.ma_msg_infotutorial01_13, cvTarget);
                            cbConnTarget.SelectedValue = "[5]";
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "btnTestBDDImport" }, Languages.Languages.ma_msg_infotutorial02_13, cvTarget);
                            cbConnTarget.SelectedValue = "[1]";
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "btnTestBDDImport" }, Languages.Languages.ma_msg_infotutorial03_13, cvTarget);
                            cbConnTarget.SelectedValue = "[5]";
                            break;
                    }
                    break;
                case 15:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "cbBDDImport", "lbImportDatabse" }, Languages.Languages.ma_msg_infotutorial01_14, cvTarget);
                            break;
                        case 2:
                            //colonne additionnelle 1
                            CanvasOptionalColumnsTarget.Effect = null;
                            CanvasOptionalColumnsTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddSqlRowsColumn" }, Languages.Languages.ma_msg_infotutorial02_14, CanvasOptionalColumnsTarget);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "cbBDDImport", "lbImportDatabse" }, Languages.Languages.ma_msg_infotutorial03_14, cvTarget);
                            break;
                    }
                    break;
                case 16:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //colonne additionnelle 1
                            CanvasOptionalColumnsTarget.Effect = null;
                            CanvasOptionalColumnsTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddSqlRowsColumn" }, Languages.Languages.ma_msg_infotutorial01_15, CanvasOptionalColumnsTarget);
                            break;
                        case 2:
                            //colonne additionnelle 2
                            TutorialInitStep(new string[] { "ckAddSqlTimestampColumn" }, Languages.Languages.ma_msg_infotutorial02_15, CanvasOptionalColumnsTarget);
                            break;
                        case 3:
                            //colonne additionnelle 1
                            CanvasOptionalColumnsTarget.Effect = null;
                            CanvasOptionalColumnsTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddSqlRowsColumn" }, Languages.Languages.ma_msg_infotutorial03_15, CanvasOptionalColumnsTarget);
                            break;
                    }
                    break;
                case 17:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //colonne additionnelle 2
                            TutorialInitStep(new string[] { "ckAddSqlTimestampColumn" }, Languages.Languages.ma_msg_infotutorial01_16, CanvasOptionalColumnsTarget);
                            break;
                        case 2:
                            //colonne additionnelle 3
                            TutorialInitStep(new string[] { "ckAddSqlDBNameColumn" }, Languages.Languages.ma_msg_infotutorial02_16, CanvasOptionalColumnsTarget);
                            break;
                        case 3:
                            //colonne additionnelle 2
                            TutorialInitStep(new string[] { "ckAddSqlTimestampColumn" }, Languages.Languages.ma_msg_infotutorial03_16, CanvasOptionalColumnsTarget);
                            break;
                    }
                    break;
                case 18:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //colonne additionnelle 3
                            TutorialInitStep(new string[] { "ckAddSqlDBNameColumn" }, Languages.Languages.ma_msg_infotutorial01_17, CanvasOptionalColumnsTarget);
                            break;
                        case 2:
                            //dynamic field magic sql command 1 (helloWorld;[3];%CS1)
                            TutorialInitStep(new string[] { "lbDynamicParamToAddInTable", "tbDynamicParamToAddInTable" }, Languages.Languages.ma_msg_infotutorial02_17, CanvasOptionalColumnsTarget);
                            break;
                        case 3:
                            //colonne additionnelle 3
                            TutorialInitStep(Array.Empty<string>(), "", cvTarget);
                            CanvasOptionalColumnsTarget.Effect = null;
                            CanvasOptionalColumnsTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddSqlDBNameColumn" }, Languages.Languages.ma_msg_infotutorial03_17, CanvasOptionalColumnsTarget);
                            break;
                    }
                    break;
                case 19:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //colonne additionnelle paramétrée #01
                            TutorialInitStep(new string[] { "lbDynamicParamToAddInTable", "tbDynamicParamToAddInTable" }, Languages.Languages.ma_msg_infotutorial01_18, CanvasOptionalColumnsTarget);
                            break;
                        case 2:
                            TutorialInitStep(Array.Empty<string>(), "", CanvasFileTarget);
                            //dynamic field magic sql command 2
                            TutorialInitStep(new string[] { "lbDynamicParamToAddInTable", "tbDynamicParamToAddInTable" }, Languages.Languages.ma_msg_infotutorial02_18, CanvasOptionalColumnsTarget);
                            tbDynamicParamToAddInTable.Text = "COLUMN_WITH_SIMPLE_DATA={?1};COLUMN_WITH_SOURCE_PRECOMMAND_DATA={?3}";
                            break;
                        case 3:
                            //Trim Data
                            TutorialInitStep(new string[] { "ckTrimData" }, Languages.Languages.ma_msg_infotutorial03_18, cvTarget);
                            break;
                    }
                    break;
                case 20:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //colonne additionnelle paramétrée #02
                            TutorialInitStep(Array.Empty<string>(), "", cvTarget);
                            CanvasOptionalColumnsTarget.Effect = null;
                            CanvasOptionalColumnsTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "lbDynamicParamToAddInTable", "tbDynamicParamToAddInTable" }, Languages.Languages.ma_msg_infotutorial01_19, CanvasOptionalColumnsTarget);
                            tbDynamicParamToAddInTable.Text = "CUSTOM_COLUMN={?2}";
                            break;
                        case 2:
                            //quantité de lignes/fichier
                            TutorialInitStep(Array.Empty<string>(), "", CanvasOptionalColumnsTarget);
                            CanvasFileTarget.Effect = null;
                            CanvasFileTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "tbCSVRowsPerFile", "lbCSVRowsPerFile" }, Languages.Languages.ma_msg_infotutorial02_19, CanvasFileTarget);
                            break;
                        case 3:
                            //change type    
                            TutorialInitStep(Array.Empty<string>(), "", cvTarget);
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAllowSchemaAlterationTarget" }, Languages.Languages.ma_msg_infotutorial03_19, CanvasSQLTarget);
                            break;
                    }
                    break;
                case 21:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbParallelInsertion", "tbQteThreadsImport" }, Languages.Languages.ma_msg_infotutorial01_20, cvTarget);
                            break;
                        case 2:
                            //append existing row 
                            TutorialInitStep(Array.Empty<string>(), "", CanvasFileTargetCSV);
                            CanvasFileTarget.Effect = null;
                            CanvasFileTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAppendFileCreation" }, Languages.Languages.ma_msg_infotutorial02_20, CanvasFileTarget);
                            break;
                        case 3:
                            //alter constraints
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckDisableConstraintsTarget" }, Languages.Languages.ma_msg_infotutorial03_20, CanvasSQLTarget);
                            break;
                    }
                    break;
                case 22:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(Array.Empty<string>(), "", cvTarget);
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "lbDropImport", "cbDropImport" }, Languages.Languages.ma_msg_infotutorial01_21, CanvasSQLTarget);
                            break;
                        case 2:
                            //Séparateur CSV
                            TutorialInitStep(Array.Empty<string>(), "", CanvasFileTarget);
                            CanvasFileTargetCSV.Effect = null;
                            CanvasFileTargetCSV.IsEnabled = true;
                            TutorialInitStep(new string[] { "tbCSVSeparator", "lbCSVSeparator" }, Languages.Languages.ma_msg_infotutorial02_21, CanvasFileTargetCSV);
                            break;
                        case 3:
                            //primary key
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddPkAfterCreateTable" }, Languages.Languages.ma_msg_infotutorial03_21, CanvasSQLTarget);
                            break;
                    }
                    break;
                case 23:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddPkAfterCreateTable" }, Languages.Languages.ma_msg_infotutorial01_22, CanvasSQLTarget);
                            break;
                        case 2:
                            //Add Header Row
                            CanvasFileTargetCSV.Effect = null;
                            CanvasFileTargetCSV.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckAddHeaderCSV" }, Languages.Languages.ma_msg_infotutorial02_22, CanvasFileTargetCSV);
                            break;
                        case 3:
                            //Set Null
                            CanvasSQLTarget.Effect = null;
                            CanvasSQLTarget.IsEnabled = true;
                            TutorialInitStep(new string[] { "ckBlankNull" }, Languages.Languages.ma_msg_infotutorial03_22, CanvasSQLTarget);
                            break;
                    }
                    break;
                case 24:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //pre-command sql 01
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial01_23, cvTarget);
                            break;
                        case 2:
                            //pre-command sql 01
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial02_23, cvTarget);
                            break;
                        case 3:
                            //pre-command sql 01
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial03_23, cvTarget);
                            break;
                    }
                    break;
                case 25:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        case 1:
                            //pre-command sql 02
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial01_24, cvTarget);
                            RichTB.SetTextRTB(tbPostJobCommandTarget, "SELECT DATE('now');");
                            break;
                        case 2:
                            //pre-command cmd 02
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial02_24, cvTarget);
                            RichTB.SetTextRTB(tbPostJobCommandTarget, "DATE /T");
                            break;
                        case 3:
                            //pre-command sql 02
                            TutorialInitStep(new string[] { "cbPrePostJobCommands_Target", "tbPostJobCommandTarget" }, Languages.Languages.ma_msg_infotutorial03_24, cvTarget);
                            RichTB.SetTextRTB(tbPostJobCommandTarget, "SELECT DATE('now');");
                            break;
                    }
                    var tc00000 = (TabItem)tcMenu.Items[2];
                    tc00000.IsEnabled = true;
                    var tc11111 = (TabItem)tcMenu.Items[3];
                    tc11111.IsEnabled = false;
                    cbPrePostJobCommands_Target.SelectedIndex = 1;
                    break;
                case 26:
                    tcMenu.SelectedIndex = 2;
                    switch (iTutoMode)
                    {
                        default:
                            TutorialInitStep(Array.Empty<string>(), "", cvTarget);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial01_25, cvUI);
                            var tc000000 = (TabItem)tcMenu.Items[2];
                            tc000000.IsEnabled = false;
                            var tc111111 = (TabItem)tcMenu.Items[3];
                            tc111111.IsEnabled = true;
                            break;
                    }
                    break;
                case 27:
                    tcMenu.SelectedIndex = 3;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26, cvQueries);
                            break;
                    }
                    break;
                case 28:
                    tcMenu.SelectedIndex = 3;
                    tbQueries.Focus();

                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26b, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26b, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26b, cvQueries);
                            break;
                    }
                    break;
                case 29:
                    if (RichTB.GetTextRTB(tbQueries).Length == 0)
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_infotutorial_mustwritesomething); TUTORIAL_STEP--;
                    }
                    else
                    {
                        tbQueries.Focus();
                        switch (iTutoMode)
                        {
                            case 1:
                                TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26c, cvQueries);
                                break;
                            case 2:
                                TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26c, cvQueries);
                                break;
                            case 3:
                                TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26c, cvQueries);
                                break;
                        }
                    }
                    break;
                case 30:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26d, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26d, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26d, cvQueries);
                            break;
                    }
                    break;
                case 31:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26e, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26e, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26e, cvQueries);
                            break;
                    }
                    break;
                case 32:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26f, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26f, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26f, cvQueries);
                            break;
                    }
                    break;
                case 33:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26g, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26g, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26g, cvQueries);
                            break;
                    }
                    break;
                case 34:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26h, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26h, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26h, cvQueries);
                            break;
                    }
                    break;
                case 35:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26i, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26i, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26i, cvQueries);
                            break;
                    }
                    break;
                case 36:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26j, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26j, cvQueries);
                            break;
                        case 3:
                            //move to log viewer
                            TutorialInitStep(Array.Empty<string>(), "", cvQueries);
                            TutorialInitStep(Array.Empty<string>(), "", cvUIButtons);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial03_26j, cvUI);
                            var tc0000000a = (TabItem)tcMenu.Items[3];
                            tc0000000a.IsEnabled = false;
                            var tc1111111a = (TabItem)tcMenu.Items[4];
                            tc1111111a.IsEnabled = true;
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            break;
                    }
                    break;
                case 37:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26k, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26k, cvQueries);
                            break;
                        case 3:
                            //launch
                            tcMenu.SelectedIndex = 4;
                            TutorialInitStep(new string[] { "btnExecuteTask" }, Languages.Languages.ma_msg_infotutorial03_26k, cvUIButtons);
                            btnSaveINI.BorderThickness = new Thickness(0);
                            btnSaveINI.BorderBrush = Brushes.DimGray;
                            btnExecuteTask.BorderThickness = new Thickness(3);
                            btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                    }
                    break;
                case 38:
                    tbQueries.Focus();
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial01_26l, cvQueries);
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial02_26l, cvQueries);
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "LbQueryTooltip", "lbQueryAutoComplete", "tbQueries" }, Languages.Languages.ma_msg_infotutorial03_26l, cvQueries);
                            break;
                    }
                    break;
                case 39:
                    switch (iTutoMode)
                    {
                        case 1:
                            tcMenu.SelectedIndex = 3;
                            TutorialInitStep(Array.Empty<string>(), "", cvQueries);
                            TutorialInitStep(Array.Empty<string>(), "", cvUIButtons);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial01_29, cvUI);
                            var tc0000000a = (TabItem)tcMenu.Items[3];
                            tc0000000a.IsEnabled = false;
                            var tc1111111a = (TabItem)tcMenu.Items[4];
                            tc1111111a.IsEnabled = true;
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            break;
                        case 2:
                            tcMenu.SelectedIndex = 3;
                            TutorialInitStep(Array.Empty<string>(), "", cvQueries);
                            TutorialInitStep(Array.Empty<string>(), "", cvUIButtons);
                            TutorialInitStep(new string[] { "tcMenu" }, Languages.Languages.ma_msg_infotutorial02_29, cvUI);
                            var tc0000000c = (TabItem)tcMenu.Items[3];
                            tc0000000c.IsEnabled = false;
                            var tc1111111c = (TabItem)tcMenu.Items[4];
                            tc1111111c.IsEnabled = true;
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            break;
                        case 3:
                            //change
                            INIProgram.RandomlyChangeSampleXLS(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\");
                            //launch again
                            TutorialInitStep(new string[] { "btnExecuteTask" }, Languages.Languages.ma_msg_infotutorial03_29, cvUIButtons);
                            btnSaveINI.BorderThickness = new Thickness(0);
                            btnSaveINI.BorderBrush = Brushes.DimGray;
                            btnExecuteTask.BorderThickness = new Thickness(3);
                            btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                    }
                    break;
                case 40:
                    tcMenu.SelectedIndex = 4;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "btnExecuteTask" }, Languages.Languages.ma_msg_infotutorial01_30, cvUIButtons);
                            btnSaveINI.BorderThickness = new Thickness(0);
                            btnSaveINI.BorderBrush = Brushes.DimGray;
                            btnExecuteTask.BorderThickness = new Thickness(3);
                            btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "btnExecuteTask" }, Languages.Languages.ma_msg_infotutorial02_30, cvUIButtons);
                            btnSaveINI.BorderThickness = new Thickness(0);
                            btnSaveINI.BorderBrush = Brushes.DimGray;
                            btnExecuteTask.BorderThickness = new Thickness(3);
                            btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                        case 3:
                            //information
                            TutorialInitStep(new string[] { "btnExecuteTask" }, Languages.Languages.ma_msg_infotutorial03_30, cvUIButtons);
                            btnSaveINI.BorderThickness = new Thickness(0);
                            btnSaveINI.BorderBrush = Brushes.DimGray;
                            btnExecuteTask.BorderThickness = new Thickness(3);
                            btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                    }
                    break;
                case 41:
                    tcMenu.SelectedIndex = 4;
                    switch (iTutoMode)
                    {
                        case 1:
                            TutorialInitStep(new string[] { "btnSaveINI" }, Languages.Languages.ma_msg_infotutorial01_31, cvUIButtons);
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            btnSaveINI.BorderThickness = new Thickness(3);
                            btnSaveINI.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                        case 2:
                            TutorialInitStep(new string[] { "btnSaveINI" }, Languages.Languages.ma_msg_infotutorial02_31, cvUIButtons);
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            btnSaveINI.BorderThickness = new Thickness(3);
                            btnSaveINI.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                        case 3:
                            TutorialInitStep(new string[] { "btnSaveINI" }, Languages.Languages.ma_msg_infotutorial03_31, cvUIButtons);
                            btnExecuteTask.BorderThickness = new Thickness(0);
                            btnExecuteTask.BorderBrush = Brushes.DimGray;
                            btnSaveINI.BorderThickness = new Thickness(3);
                            btnSaveINI.BorderBrush = System.Windows.Media.Brushes.Red;
                            break;
                    }
                    break;
                case 42:
                    switch (iTutoMode)
                    {
                        default:
                            MessageBox.Show(Languages.Languages.ma_msg_infotutorial01_32);
                            TutorialReinitCanvas();
                            break;
                    }
                    break;
            }
        }

        private void RemoveTutorialFromCanvas()
        {
            if (cvTutorial.Children.Contains(bdTutorial)) { cvTutorial.Children.Remove(bdTutorial); }
            if (cvUIButtons.Children.Contains(bdTutorial)) { cvUIButtons.Children.Remove(bdTutorial); }
            if (cvUI.Children.Contains(bdTutorial)) { cvUI.Children.Remove(bdTutorial); }
            if (cvJobConfiguration.Children.Contains(bdTutorial)) { cvJobConfiguration.Children.Remove(bdTutorial); }
            if (CanvasFileSource.Children.Contains(bdTutorial)) { CanvasFileSource.Children.Remove(bdTutorial); }
            if (CanvasFileSourceCSV.Children.Contains(bdTutorial)) { CanvasFileSourceCSV.Children.Remove(bdTutorial); }
            if (CanvasFileSourceXLS.Children.Contains(bdTutorial)) { CanvasFileSourceXLS.Children.Remove(bdTutorial); }
            if (CanvasFileSourceXML.Children.Contains(bdTutorial)) { CanvasFileSourceXML.Children.Remove(bdTutorial); }
            if (CanvasSQLSource.Children.Contains(bdTutorial)) { CanvasSQLSource.Children.Remove(bdTutorial); }
            if (cvTarget.Children.Contains(bdTutorial)) { cvTarget.Children.Remove(bdTutorial); }
            if (cvSource.Children.Contains(bdTutorial)) { cvSource.Children.Remove(bdTutorial); }

            if (CanvasOptionalColumnsTarget.Children.Contains(bdTutorial)) { CanvasOptionalColumnsTarget.Children.Remove(bdTutorial); }
            if (CanvasFileTarget.Children.Contains(bdTutorial)) { CanvasFileTarget.Children.Remove(bdTutorial); }
            if (CanvasFileTargetCSV.Children.Contains(bdTutorial)) { CanvasFileTargetCSV.Children.Remove(bdTutorial); }
            if (CanvasFileTargetXLS.Children.Contains(bdTutorial)) { CanvasFileTargetXLS.Children.Remove(bdTutorial); }
            if (CanvasFileTargetXML.Children.Contains(bdTutorial)) { CanvasFileTargetXML.Children.Remove(bdTutorial); }
            if (CanvasSQLTarget.Children.Contains(bdTutorial)) { CanvasSQLTarget.Children.Remove(bdTutorial); }
            if (cvQueries.Children.Contains(bdTutorial)) { cvQueries.Children.Remove(bdTutorial); }
            if (cvQueriesSandbox.Children.Contains(bdTutorial)) { cvQueriesSandbox.Children.Remove(bdTutorial); }
            if (cvLogViewer.Children.Contains(bdTutorial)) { cvLogViewer.Children.Remove(bdTutorial); }
        }

        private void TutorialReinitCanvas()
        {
            BorderBrush = System.Windows.Media.Brushes.White;
            BorderThickness = new Thickness(0);

            lbTutorial.Visibility = Visibility.Hidden;
            bdTutorial.Visibility = Visibility.Hidden;
            cvJobSelection.Visibility = Visibility.Visible;

            btnExecuteTask.BorderThickness = new Thickness(0);
            btnExecuteTask.BorderBrush = Brushes.DimGray;

            btnSaveINI.BorderThickness = new Thickness(0);
            btnSaveINI.BorderBrush = Brushes.DimGray;

            RemoveTutorialFromCanvas();

            foreach (UIElement ui in cvUI.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvUIButtons.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvJobConfiguration.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvSource.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileSource.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileSourceCSV.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileSourceXLS.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileSourceXML.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasSQLSource.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvTarget.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasOptionalColumnsTarget.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileTarget.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileTargetCSV.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileTargetXLS.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasFileTargetXML.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in CanvasSQLTarget.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvQueries.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvQueriesSandbox.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }
            foreach (UIElement ui in cvLogViewer.Children)
            {
                ui.Effect = null;
                ui.IsEnabled = true;
            }

            foreach (TabItem tI in tcMenu.Items)
            {
                tI.IsEnabled = true;
            }

            TUTORIAL_MODE = 0;
            TUTORIAL_STEP = -1;

            //ClearFields();
        }

        private void TutorialInitStep(string[] sTutoControls, string sTooltip, Canvas cv)
        {
            RemoveTutorialFromCanvas();

            //affichage uniquement de l'onglet en cours (pour éviter que l'user aille n'importe ou)
            foreach (TabItem tI in tcMenu.Items)
            {
                if (!tI.IsSelected) { tI.IsEnabled = false; }
                else { tI.IsEnabled = true; }
            }

            BlurEffect objBlur = new()
            {
                Radius = 6
            };

            foreach (UIElement ui in cv.Children)
            {
                Type gUI = ui.GetType();
                switch (gUI.Name)
                {
                    case "Slider":
                        var sl = (Slider)ui;
                        if (!sTutoControls.Contains(sl.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = sl.Width + 2; bdTutorial.Height = sl.Height + 2; }
                        break;
                    case "TabControl":
                        var tc = (TabControl)ui;
                        if (!sTutoControls.Contains(tc.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = tc.Width + 2; bdTutorial.Height = tc.Height + 2; }
                        break;
                    case "ComboBox":
                        var cb = (ComboBox)ui;
                        if (!sTutoControls.Contains(cb.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = cb.Width + 2; bdTutorial.Height = cb.Height + 2; }
                        break;
                    case "ListBox":
                        var lbx = (ListBox)ui;
                        if (!sTutoControls.Contains(lbx.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = lbx.Width + 2; bdTutorial.Height = lbx.Height + 2; }
                        break;
                    case "TextBox":
                        var tb = (TextBox)ui;
                        if (!sTutoControls.Contains(tb.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = tb.Width + 2; bdTutorial.Height = tb.Height + 2; }
                        break;
                    case "RichTextBox":
                        var rtb = (RichTextBox)ui;
                        if (!sTutoControls.Contains(rtb.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = rtb.Width + 2; bdTutorial.Height = rtb.Height + 2; }
                        break;
                    case "Label":
                        var lb = (Label)ui;
                        if (!sTutoControls.Contains(lb.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { ui.Effect = null; ui.IsEnabled = true; }
                        break;
                    case "CheckBox":
                        var cbb = (CheckBox)ui;
                        if (!sTutoControls.Contains(cbb.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = cbb.Width + 2; bdTutorial.Height = cbb.Height + 2; }
                        break;
                    case "Button":
                        var btn = (Button)ui;
                        if (!sTutoControls.Contains(btn.Name))
                        {
                            ui.Effect = objBlur;
                            ui.IsEnabled = false;
                        }
                        else { SetTutorialLabelPosition(ui, cv, sTooltip); bdTutorial.Width = btn.Width + 2; bdTutorial.Height = btn.Height + 2; }
                        break;
                    default:
                        ui.Effect = objBlur;
                        ui.IsEnabled = false;
                        break;
                }
            }
            if (sTooltip.Length > 0)
            {
                lbTutorial.Visibility = Visibility.Visible;
                bdTutorial.Visibility = Visibility.Visible;
                cvJobSelection.Visibility = Visibility.Hidden;
                cv.Children.Add(bdTutorial);
                lbTutorial.Text = sTooltip;
            }
            else
            {
                lbTutorial.Visibility = Visibility.Hidden;
                bdTutorial.Visibility = Visibility.Hidden;
                cvJobSelection.Visibility = Visibility.Visible;
            }
        }

        private void SetTutorialLabelPosition(UIElement ui, Canvas cv, string sTooltip)
        {
            ui.Effect = null;
            ui.IsEnabled = true;
            System.Windows.Point pCP = ui.TransformToAncestor(cv).Transform(new System.Windows.Point(0, 0));
            bdTutorial.Margin = new Thickness(pCP.X - 1, pCP.Y - 1, 0, 0);
        }

        private async void TcMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Equals(sender, e.OriginalSource))
            {
                if (Program.LiveHelp != null)
                {
                    Canvas contentPresenter = tcMenu.SelectedContent as Canvas;
                    if (contentPresenter != null) { AttachMouseDownEventToAllControls(contentPresenter); }
                }

                var tcM = (TabControl)e.OriginalSource;

                if (tcM.SelectedIndex == 3 && TUTORIAL_MODE == 0)
                {
                    Job INIP = InitJobParameters("[0]");
                    bool bExists = FuzibleController.LoadQueryQuickHelp(INIP);
                    if (!bExists && INIP.ConnectionString_Source != null) //faire popper l'aide
                    {
                        if (QueryHelp == null)
                        {
                            QueryHelp = new Help(AppHelp.GetHelpBlock("QUERY " + INIP.ConnectionString_Source.SConnDriver.ToString()[0..2]));
                            QueryHelp.Show();
                        }
                        else
                        {
                            if (QueryHelp.IsOpen)
                            {
                                await QueryHelp.LoadHelpBlock(AppHelp.GetHelpBlock("QUERY " + INIP.ConnectionString_Source.SConnDriver.ToString()[0..2]));
                            }
                            else
                            {
                                QueryHelp = null;
                                QueryHelp = new Help(AppHelp.GetHelpBlock("QUERY " + INIP.ConnectionString_Source.SConnDriver.ToString()[0..2]));
                                QueryHelp.Show();
                            }
                        }
                    }
                }
            }
        }

        private void MnuFileMenuConfig_Click(object sender, RoutedEventArgs e)
        {
            int iMenu = 0;
            var mI = (MenuItem)sender;
            switch (mI.Name)
            {
                case "MiLog":
                    iMenu = 1;
                    break;
                case "MiSql":
                    iMenu = 3;
                    break;
                case "MiConnections":
                    iMenu = 0;
                    break;
                case "MiFile":
                    iMenu = 4;
                    break;
                case "MiWs":
                    iMenu = 6;
                    break;
                case "MiMail":
                    iMenu = 5;
                    break;
                case "MiDataAnalyzer":
                    iMenu = 2;
                    break;
                case "MiServiceApp":
                    iMenu = 7;
                    break;
                default:
                    break;
            }

            if (JobStatus == 0)
            {
                MessageBox.Show(Languages.Languages.ma_msg_canteditwhilejobrunning);
            }
            else
            {
                bool bLoad = FuzibleController.LoadConfigurationScreen(null, iMenu);

                if (bLoad)
                {
                    FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_userpwdask, Languages.Languages.ma_msg_userpwd, true, true);
                    fP.ShowDialog();
                    string sPwd = fP.PromptUserData;
                    if (sPwd.Equals(FuzibleController.MainParams.USER_PASSWORD))
                    {
                        string sDBT = tbDatabaseTarget.Text;
                        string sDBS = tbDatabaseSource.Text;

                        Configuration xamlMainConfig = new(USERNAME, null, iMenu, sDBS, sDBT);

                        try //ça peut planter si on a saisi un mauvais mot de passe
                        {
                            xamlMainConfig.Show();
                            xamlMainConfig.Closed += Event_ConfigurationClosed;
                        }
                        catch { }
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_userpwdwrong);
                    }
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme);
                }
            }
        }

        private void MnuFileMenuTools_Click(object sender, RoutedEventArgs e)
        {
            if (JobStatus > 0)
            {
                var mI = (MenuItem)sender;

                switch (mI.Name)
                {
                    case "MiReorganize":
                        JobsOrganize JO = new(USERNAME);
                        JO.ShowDialog();

                        try
                        {
                            FuzibleController.ReloadINI();
                            //chargement des jobs
                            LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_unabletoloaduserspace + ex.Message);
                            System.Environment.Exit(1);
                        }
                        break;
                    case "MiImport":

                        if (JobStatus > 0)
                        {
                            if (FuzibleController.OWN_USER)
                            {
                                cbINISection.SelectedIndex = -1;

                                ImportJob xamlImportJob = new(USERNAME);
                                xamlImportJob.ShowDialog();

                                if (xamlImportJob.IS_IMPORT_OK)
                                {
                                    Job NewJob = null;
                                    try
                                    {
                                        NewJob = xamlImportJob.NEWJOB;
                                        //FuzibleController.ReloadINI();
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(Languages.Languages.ma_msg_unableprocessextjob + ex.Message);
                                    }

                                    if (NewJob != null)
                                    {
                                        ClearFields();
                                        LoadUIListConnections("", "");
                                        //attention : comme on a ajouté de nouvelles connections, on doit les faire matcher avec le job qui vient d'être importé (il a un autre ID de connectoin -> pas le même fichier)
                                        //Job INIPNew = xamlImportJob.NEWJOB;
                                        LoadUI(NewJob, true);

                                        MessageBox.Show(Languages.Languages.ma_msg_successfullyimported);
                                    }
                                }
                                else
                                {
                                    FuzibleController = new Fuzible_CTL(USERNAME, true, false); //si il y a eu une erreur, l'user est en lecture seule, ce qui est dommage...
                                }
                            }
                            else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_canteditwhilejobrunning);
                        }

                        break;
                    case "MiExportXML":
                        if (cbINISection.SelectedIndex >= 0)
                        {
                            if (FuzibleController.OWN_USER)
                            {
                                string sCurrentJobID = cbINISection.SelectedValue.ToString();
                                Job JExport = FuzibleController.GetJob(sCurrentJobID);

                                string sXML = JExport.ExportJobAsXML(USERNAME, JExport.JobDescription, true, true);
                                SaveFileDialog sFD = new()
                                {
                                    Filter = "(*.xml) | *.xml",
                                    DefaultExt = "xml",
                                    FileName = JExport.JobNAME,
                                    Title = "Export Fuzible Job"
                                };
                                if (sFD.ShowDialog() == true)
                                {
                                    File.WriteAllText(sFD.FileName, sXML);
                                    MessageBox.Show(Languages.Languages.ma_msg_jobexported);
                                }
                            }
                            else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
                        break;
                    case "MiImportXML":
                        if (FuzibleController.OWN_USER)
                        {
                            cbINISection.SelectedIndex = -1;

                            OpenFileDialog lFD = new()
                            {
                                Filter = "(*.xml) | *.xml",
                                DefaultExt = "xml",
                                Title = "Import Fuzible Job"
                            };
                            bool? result = lFD.ShowDialog();

                            if (lFD.FileName.Length > 0)
                            {
                                Job NewJob = null;
                                try
                                {
                                    try
                                    {
                                        FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputpwd, Languages.Languages.ma_msg_inputpwd, true, true);
                                        fP.ShowDialog();
                                        string sJobPassword = fP.PromptUserData;

                                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_matchconnectionsbyname, Languages.Languages.mc_msg_matchconnectionsbyname_header, MessageBoxButton.YesNo);
                                        if (msgR.ToString().ToUpper().Equals("YES"))
                                        {
                                            string sStatus = FuzibleController.ImportXMLJob(lFD.FileName, sJobPassword, true);
                                            MessageBox.Show(sStatus);
                                        }
                                        else
                                        {
                                            string sStatus = FuzibleController.ImportXMLJob(lFD.FileName, sJobPassword, false);
                                            MessageBox.Show(sStatus);
                                        }
                                        NewJob = FuzibleController.IMPORTED_JOB;
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(Languages.Languages.ma_msg_importjobunknowxml01 + ex.Message + ")" + Environment.NewLine + Languages.Languages.ma_msg_importjobunknowxml02);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_importjobunabletoprocess + ex.Message);
                                }

                                if (NewJob != null)
                                {
                                    ClearFields();
                                    LoadUIListConnections("", "");
                                    //attention : comme on a ajouté de nouvelles connections, on doit les faire matcher avec le job qui vient d'être importé (il a un autre ID de connectoin -> pas le même fichier)
                                    //Job INIPNew = xamlImportJob.NEWJOB;
                                    LoadUI(NewJob, true);

                                    MessageBox.Show(Languages.Languages.ma_msg_importjobsuccess + NewJob.JobNAME);
                                }
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
                        break;
                    case "MiPlanif":
                        if (FuzibleController.IsServiceAppConfigured())
                        {
                            DataSet dsCalendar;
                            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_planifcalendarinfo01 + Environment.NewLine + Languages.Languages.ma_msg_planifcalendarinfo02, Languages.Languages.ma_msg_planifcalendarinfo03, MessageBoxButton.YesNo);
                            if (msgR.ToString().ToUpper().Equals("YES"))
                            {
                                MessageBoxResult msgRb = MessageBox.Show(Languages.Languages.ma_msg_planifcalendarinfo04, Languages.Languages.ma_msg_planifcalendarinfo05, MessageBoxButton.YesNo);
                                if (msgRb.ToString().ToUpper().Equals("YES"))
                                {
                                    FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.LOAD_PLANIF_CALENDAR, new bool[] { true, true }, cancelTokenBGTasks.Token);
                                }
                                else
                                {
                                    FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.LOAD_PLANIF_CALENDAR, new bool[] { true, false }, cancelTokenBGTasks.Token);
                                }
                            }
                            else
                            {
                                MessageBoxResult msgRb = MessageBox.Show(Languages.Languages.ma_msg_planifcalendarinfo06, Languages.Languages.ma_msg_planifcalendarinfo07, MessageBoxButton.YesNo);
                                if (msgRb.ToString().ToUpper().Equals("YES"))
                                {
                                    FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.LOAD_PLANIF_CALENDAR, new bool[] { false, true }, cancelTokenBGTasks.Token);
                                }
                                else
                                {
                                    FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.LOAD_PLANIF_CALENDAR, new bool[] { false, false }, cancelTokenBGTasks.Token);
                                }
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_planifcalendar_servicenotconfigured); }
                        break;
                    case "MiLoadUserspace":

                        if (JobStatus > 0)
                        {
                            bool bOK = true;

                            FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputusername, Languages.Languages.ma_msg_inputusername, false, true);
                            fP.ShowDialog();
                            string sUserExt = fP.PromptUserData.Trim();

                            List<string> sUsers = INIProgram.LoadProgramUsers(USERNAME);
                            if (sUserExt.Equals(USERNAME))
                            {
                                bOK = false;
                                MessageBox.Show(Languages.Languages.ma_msg_userspacealreadyinuse);
                            }
                            else if (sUsers.Count == 0)
                            {
                                bOK = false;
                                MessageBox.Show(Languages.Languages.ma_msg_userspacenomoreusers);
                            }
                            else if (sUsers.IndexOf(sUserExt) == -1)
                            {
                                bOK = false;
                                MessageBox.Show(Languages.Languages.ma_msg_userspacenotfound + sUserExt + ")");
                            }

                            if (bOK)
                            {
                                try
                                {
                                    //saisie du mot de passe
                                    FuzibleController = new Fuzible_CTL(sUserExt, false, true);
                                    USERNAME = sUserExt;
                                    MessageBox.Show(string.Concat(Languages.Languages.ma_msg_userspacedata01, sUserExt, Languages.Languages.ma_msg_userspacedata02));
                                    
                                    ClearFields();
                                    LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_userspacecantload + ex.Message);
                                }
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning); }


                        break;
                    default:
                        break;
                }


            }
            else
            {
                MessageBox.Show(Languages.Languages.ma_msg_canteditwhilejobrunning);
            }

        }

        private void MnuFileMenuFile_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mI = null;
            mI = (MenuItem)sender;


            switch (mI.Name)
            {
                case "CancelBackgroundTask":
                    cancelTokenBGTasks.Cancel();
                    cancelTokenBGTasks = new CancellationTokenSource();
                    break;
                case "ChangeUserPassword":
                    if (FuzibleController.OWN_USER)
                    {
                        ChangePassword();
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
                    break;
                case "MiSampleJob":
                    bool bOK = FuzibleController.AddSampleJobs();
                    if (bOK)
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_samplejobok);
                        LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_samplejobko);
                    }
                    break;
                case "MiExit":
                    if (JobStatus == 0)
                    {
                        Exception ex = new(Languages.Languages.ma_msg_canteditwhilejobrunning);
                        Monitoring.CancelJob();
                        FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, "", SQLTools_Enums.LOG_TYPEINFO.ERR);
                    }
                    System.Environment.Exit(1);
                    break;
                case "MiShowConsole":
                    console.ShowConsole();
                    break;
                case "MiDebugTask":
                    if (1 == 0)
                    {
                        //Help xamlHelp1 = new(new BlockHelp("Live Help", "", ""));
                        //xamlHelp1.Show();
                    }
                    break;
                case "MiBenchmarking":
                    if (JobStatus > 0)
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_benchmarkinginfo02, Languages.Languages.ma_msg_benchmarkinginfo01, MessageBoxButton.YesNo);
                        if (msgR.ToString().ToUpper().Equals("YES"))
                        {
                            bool bCanRun = FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.BENCHMARK, null, cancelTokenBGTasks.Token);
                            if (bCanRun)
                            {

                            }
                            else { MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning); }
                        }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning); }

                    break;
                case "MiLangEN":
                    if (FuzibleController.OWN_USER)
                    {
                        if (JobStatus > 0)
                        {
                            var mILang = (MenuItem)sender;
                            try
                            {
                                FuzibleController.ChangeLanguage("EN");
                                MessageBox.Show(Languages.Languages.ma_msg_changelanguagerestart);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Languages.Languages.ma_msg_cantchangelanguage + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning);
                        }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
                    break;
                case "MiLangFR":
                    if (FuzibleController.OWN_USER)
                    {
                        if (JobStatus > 0)
                        {
                            var mILang = (MenuItem)sender;
                            try
                            {
                                FuzibleController.ChangeLanguage("FR");
                                MessageBox.Show(Languages.Languages.ma_msg_changelanguagerestart);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Languages.Languages.ma_msg_cantchangelanguage + ex.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning);
                        }
                    }
                    break;
            }
        }

        private void MnuFileMenuHelp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox)
            {
                CheckBox cI = ((CheckBox)sender);
                switch (cI.Name)
                {
                    case "MiShowLiveHelpStart":
                        bool bOK = cI.IsChecked.Value ? FuzibleController.MainParams.SetLiveHelpAtStart(true) : FuzibleController.MainParams.SetLiveHelpAtStart(false);
                        if (!bOK)
                        {
                            MessageBox.Show("Error while saving Main Params");
                        }
                        else
                        {
                            if (!cI.IsChecked.Value)
                            {
                                if (Program.LiveHelp != null && Program.LiveHelp.IsOpen)
                                {
                                    Program.LiveHelp.Close();
                                }
                            }
                            else
                            {
                                if (Program.LiveHelp == null || !Program.LiveHelp.IsOpen)
                                {
                                    Program.LiveHelp = new Help(new BlockHelp("Fuzible", "MiLiveHelp", "", ""));
                                    Program.LiveHelp.Show();
                                }
                            }
                        }
                        break;
                }
            }
            else
            {
                var mI = (MenuItem)sender;

                switch (mI.Name)
                {
                    case "MiLiveHelp":
                        if (Program.LiveHelp == null || !Program.LiveHelp.IsOpen)
                        {
                            Program.LiveHelp = new Help(new BlockHelp("Fuzible", "MiLiveHelp", "", ""));
                            Program.LiveHelp.Show();
                        }
                        break;
                    case "MiVisitUs":
                        ProcessStartInfo psi = new()
                        {
                            FileName = FuzibleController.MainParams.FUZIBLE_SERVER,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        break;
                    case "MiDBBrowser":
                        ProcessStartInfo psi2 = new()
                        {
                            FileName = "https://sqlitebrowser.org",
                            UseShellExecute = true
                        };
                        Process.Start(psi2);
                        break;
                    case "MiAboutUs":
                        StringBuilder sbAbout = new();
                        sbAbout.AppendLine(string.Concat("Version : ", INIProgram.APP_VERSION));
                        sbAbout.Append(Environment.NewLine);
                        sbAbout.AppendLine(Languages.Languages.ma_msg_credits01);
                        //sbAbout.Append(Environment.NewLine);
                        sbAbout.AppendLine(FuzibleController.MainParams.FUZIBLE_SERVER);
                        sbAbout.Append(Environment.NewLine);
                        sbAbout.AppendLine(Languages.Languages.ma_msg_credits02);
                        sbAbout.AppendLine(Toolbox.GetProgramDependencies());
                        MessageBox.Show(sbAbout.ToString());
                        break;
                    case "MiContactUs":
                        string mailto = string.Format("mailto:{0}?Subject={1}&Body={2}", SHSConstantes.DEV_MAIL, "Fuzible Assistance", "");
                        mailto = Uri.EscapeDataString(mailto);
                        ProcessStartInfo psi3 = new()
                        {
                            FileName = mailto,
                            UseShellExecute = true
                        };
                        Process.Start(psi3);
                        break;
                }
            }
        }

        private void MnuFileMenuTutorial_Click(object sender, RoutedEventArgs e)
        {
            var mITuto = (MenuItem)sender;

            if (JobStatus > 0)
            {
                switch (mITuto.Name)
                {
                    case "miReplicationFileToSQL":
                        Help xamlHelp1 = new(AppHelp.GetHelpBlock("TUTORIAL 01"));
                        xamlHelp1.ShowDialog();
                        TUTORIAL_MODE = 1;
                        break;
                    case "miReplicationSQLToFile":
                        Help xamlHelp2 = new(AppHelp.GetHelpBlock("TUTORIAL 02"));
                        xamlHelp2.ShowDialog();
                        TUTORIAL_MODE = 2;
                        break;
                    case "miSynchroFileToSQL":
                        Help xamlHelp3 = new(AppHelp.GetHelpBlock("TUTORIAL 03"));
                        xamlHelp3.ShowDialog();
                        TUTORIAL_MODE = 3;
                        break;
                }

                MessageBox.Show(Languages.Languages.ma_msg_tutorialinfo01 +
                                Environment.NewLine +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo02 +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo03 +
                                Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo04);

                ClearFields();
                cbINISection.SelectedIndex = -1;

                TUTORIAL_STEP = 0;
                BorderBrush = System.Windows.Media.Brushes.Red;
                BorderThickness = new Thickness(2);
            }
            else
            {
                MessageBox.Show(Languages.Languages.ma_msg_tutorialcantlaunch);
            }
        }

        private void CbJobFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string sFamily = "";
            if (e.AddedItems.Count > 0)
            {
                sFamily = e.AddedItems[0].ToString();

                LoadUIListSections(sFamily);
            }
        }

        private void CbJobFamily_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock)
            {
                var item = (TextBlock)e.OriginalSource;
                if (item != null)
                {
                    MessageBoxResult msgR = MessageBox.Show(string.Concat(Languages.Languages.mc_msg_removefamily01, " [", item.Text, "]", Environment.NewLine, Languages.Languages.mc_msg_removefamily02), Languages.Languages.mc_msg_removefamily01, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        MessageBox.Show(FuzibleController.RemoveFamily(item.Text));
                        LoadUIListSections("");
                    }
                }
            }
        }

        private void CbINISection_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var cbINI = (ComboBox)sender;
            string sText = cbINI.Text;
            if (sText.Length == 0)
            {
                foreach (ComboBoxItem cbI in cbINISection.Items)
                {
                    if (!cbI.Tag.ToString().Contains('-', StringComparison.CurrentCulture))
                    {
                        cbI.Visibility = Visibility.Visible;
                    }
                    else { cbI.Visibility = Visibility.Collapsed; }
                }
                cbINISection.IsDropDownOpen = false;
            }
            else
            {
                foreach (ComboBoxItem cbI in cbINISection.Items)
                {
                    if (cbI.Content.ToString().IndexOf(sText, StringComparison.OrdinalIgnoreCase) > -1 && !cbI.Tag.ToString().Contains('-', StringComparison.CurrentCulture)) //gestion des subjobs
                    {
                        cbI.Visibility = Visibility.Visible;
                    }
                    else { cbI.Visibility = Visibility.Collapsed; }
                }
                cbINISection.IsDropDownOpen = true;
                var tbI = (TextBox)e.OriginalSource; //éviter que le IsDropDownOpen ne sélectionne le texte
                tbI.Select(tbI.Text.Length, 0);
            }
        }

        private void CbINISection_DropDownOpened(object sender, EventArgs e)
        {
            foreach (ComboBoxItem cbI in cbINISection.Items)
            {
                if (!cbI.Tag.ToString().Contains('-', StringComparison.CurrentCulture))
                {
                    cbI.Visibility = Visibility.Visible;
                }
                else { cbI.Visibility = Visibility.Collapsed; }
            }
        }

        private void CbINISection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbINISection.SelectedIndex > -1)
            {
                var cbI = (ComboBoxItem)e.AddedItems[0];
                string sCurrentJobID = cbI.Tag.ToString();
                Job INIP = FuzibleController.GetJob(sCurrentJobID);

                if (INIP != null)
                {
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                    bool bOk = true;
                    if (!FuzibleController.OWN_USER)
                    {
                        if (!FuzibleController.GetJob(sCurrentJobID).Job_IsSubJob) //on ne demande pas le MDP d'un subjob
                        {
                            bOk = false;
                            string sJobP = INIP.JobPassword;
                            string sJobID = INIP.RawJobID.ToString();
                            string sJobPassword = PromptForPassword();
                            if (sJobPassword.Equals(FITools.EncryptionSystem.AES_Decrypt(sJobP, sJobID))) { bOk = true; }
                        }
                    }

                    if (bOk)
                    {
                        btnExecuteTask.IsEnabled = true;
                        ClearComboBoxConnection();
                        ClearFields();
                        LoadUI(INIP, false);
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_wrongpwd); }

                    Mouse.OverrideCursor = null;
                }
            }
        }

        private void CbBDDImport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBDDImport.SelectedIndex > -1 && cbBDDImport.Items.Count > 0 && tbDatabaseTarget.Visibility == Visibility.Visible)
            {
                tbDatabaseTarget.Text = cbBDDImport.SelectedValue.ToString();
            }

        }

        private void CbBDDExport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBDDExport.SelectedIndex > -1 && cbBDDExport.Items.Count > 0 && tbDatabaseSource.Visibility == Visibility.Visible)
            {
                tbDatabaseSource.Text = cbBDDExport.SelectedValue.ToString();

                AskIntellisenseRefresh(true);
            }
        }

        private void CbWSSQLLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbWSSQLLanguage.SelectedIndex > 0)
            {
                AskIntellisenseRefresh(true);
            }
        }

        private void CbConnSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbConnSource.SelectedIndex > -1)
            {
                //ComboBoxItem cbI = (ComboBoxItem)cbConnSource.Items[cbConnSource.SelectedIndex];
                cbBDDExport.Items.Clear();
                tbDatabaseSource.Text = "";
                var cbI = (ComboBoxItem)e.AddedItems[0];
                CONNString CS = FuzibleController.GetConnection(cbI.Tag.ToString());
                cbDriverSource.SelectedValue = CS.SConnDriver.ToString();

                if (INTELLISENSE.Connection == null)
                {
                    AskIntellisenseRefresh(true);
                }
                else
                {
                    if (!CS.SConnID.Equals(INTELLISENSE.Connection.SConnID))
                    {
                        AskIntellisenseRefresh(true);
                    }
                }
            }
        }

        private void CbConnTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbConnTarget.SelectedIndex > -1)
            {
                cbBDDImport.Items.Clear();
                tbDatabaseTarget.Text = "";
                var cbI = (ComboBoxItem)e.AddedItems[0];
                CONNString CS = FuzibleController.GetConnection(cbI.Tag.ToString());
                cbDriverTarget.SelectedValue = CS.SConnDriver.ToString();
            }
        }

        private void BtnJobStats_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex > -1)
            {
                if (FuzibleController.OWN_USER)
                {
                    string sCurrentJobID = cbINISection.SelectedValue.ToString();
                    Job INIP = FuzibleController.GetJob(sCurrentJobID);
                    JobStats stats = INIP.LoadJobStats();

                    if (AppHelp != null && stats.HasStats)
                    {
                        Help xamlHelp = new(new BlockHelp("Fuzible", Languages.Languages.ma_msg_jobstats, stats.ToString(), ""));
                        xamlHelp.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_nojobstats);
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_unregistered); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
        }

        private void BtnJobFamily_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex > -1)
            {
                if (FuzibleController.OWN_USER)
                {
                    // Charger les données à la volée
                    List<string> items = FuzibleController.JobFamilies;
                    ComboBoxPrompt cbP = new ComboBoxPrompt(items, Languages.Languages.ma_msg_setjobfamily, Languages.Languages.ma_msg_setjobfamily_cb, Languages.Languages.ma_msg_setjobfamily_tb);
                    cbP.ShowDialog();
                    string sFamily = cbP.PromptUserData;

                    try
                    {
                        string sCurrentJobID = cbINISection.SelectedValue.ToString();
                        Job INIP = FuzibleController.GetJob(sCurrentJobID);
                        INIP.JobFamily = new JobFamily { Name = sFamily };
                        SaveJob(INIP, true);
                        cbJobFamily.SelectedItem = sFamily;
                    }
                    catch
                    {

                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_unregistered); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
        }

        private void BtnOrchestration_Click(object sender, RoutedEventArgs e)
        {
            if (FuzibleController.OWN_USER)
            {
                if (cbINISection.SelectedIndex >= 0)
                {
                    //contrôler que c'est le bon USER qui va définir l'orchestration du job
                    //TODO !!
                    if (FuzibleController.IsServiceAppConfigured())
                    {
                        string sCurrentJobID = cbINISection.SelectedValue.ToString();
                        if (!FuzibleController.GetJob(sCurrentJobID).Job_IsSubJob)
                        {
                            JobOrchestration xamlOrchestration = new(USERNAME, sCurrentJobID);
                            xamlOrchestration.Show();
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_planifcalendar_cantplanifsubjob); }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_planifcalendar_servicenotconfigured); }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
        }

        private void BtnChooseHTMLTemplate_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog lFD = new()
            {
                Filter = "(*.html) | *.html",
                DefaultExt = "html",
                Title = "Html File"
            };
            lFD.ShowDialog();

            if (lFD.FileName.Length > 0)
            {
                try
                {
                    string html = File.ReadAllText(lFD.FileName);

                    if (!Toolbox.IsStringHTML(html))
                    {
                        MessageBox.Show(Languages.Languages.ma_target_btn_loadhtml_fail);
                    }
                    else
                    {
                        //test du chargement du HTML
                        MessageBox.Show(Languages.Languages.ma_target_btn_loadhtml_ok);
                        tbTargetMailHTMLTemplate.Text = lFD.FileName;
                    }
                }
                catch (Exception ex)
                { MessageBox.Show(ex.Message); }
            }
        }

        private async void BtnTestBDDImport_Click(object sender, RoutedEventArgs e)
        {
            if (cbConnTarget.SelectedIndex > -1)
            {
                CONNString sConn = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());

                List<string> sDynVars;
                string[] sVars = RichTB.GetTextRTB(tbQueryVariableParameters).Trim().Split(Convert.ToChar(";"));
                sDynVars = sVars.ToList();

                List<string> sAnswers = await Toolbox.CheckConnection(sConn, sDynVars, tbDatabaseTarget.Text.Trim(), Monitoring.TaskCancellationToken);

                MessageBox.Show(sAnswers[0]);
                sAnswers.RemoveAt(0);

                ClearComboBoxConnection("cbBDDImport");
                foreach (string sF in sAnswers)
                {
                    ComboBoxItem cbNewItem = new() { Tag = sF, Content = sF };
                    cbBDDImport.Items.Add(cbNewItem);
                }

                if (sAnswers.Count > 0 && sConn.SConnDriverSuffix.Equals("FI") && !sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Contains("FTP", StringComparison.CurrentCulture))
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_showcontentpath01, Languages.Languages.ma_msg_showcontentpath02, MessageBoxButton.YesNo);

                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        ProcessStartInfo startInfo = new(sConn.SConnString(sDynVars)) { UseShellExecute = true };
                        Process.Start(startInfo);
                    }
                }
            }
        }

        private async void BtnTestBDDExport_Click(object sender, RoutedEventArgs e)
        {
            if (cbConnSource.SelectedIndex > -1)
            {
                CONNString sConn = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());

                List<string> sDynVars;
                string[] sVars = RichTB.GetTextRTB(tbQueryVariableParameters).Trim().Split(Convert.ToChar(";"));
                sDynVars = sVars.ToList();

                List<string> sAnswers = await Toolbox.CheckConnection(sConn, sDynVars, tbDatabaseSource.Text.Trim(), Monitoring.TaskCancellationToken);

                MessageBox.Show(sAnswers[0]);
                sAnswers.RemoveAt(0);

                ClearComboBoxConnection("cbBDDExport");
                foreach (string sF in sAnswers)
                {
                    if (sF.Length > 0)
                    {
                        ComboBoxItem cbNewItem = new() { Tag = sF, Content = sF };
                        cbBDDExport.Items.Add(cbNewItem);
                    }
                }

                //ouvrir le répertoire si mode fichier
                if (sAnswers.Count > 0 && sConn.SConnDriverSuffix.Equals("FI") && !sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Contains("FTP", StringComparison.CurrentCulture))
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_showcontentpath01, Languages.Languages.ma_msg_showcontentpath02, MessageBoxButton.YesNo);

                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        ProcessStartInfo startInfo = new(sConn.SConnString(sDynVars)) { UseShellExecute = true };
                        Process.Start(startInfo);
                    }
                }
            }
        }

        private void BtnEditBDDExport_Click(object sender, RoutedEventArgs e)
        {
            if (cbDriverSource.SelectedIndex > -1)
            {
                CONNString sConn = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());

                string sDBT = tbDatabaseTarget.Text;
                string sDBS = tbDatabaseSource.Text;

                FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_userpwdask, Languages.Languages.ma_msg_userpwd, true, true);
                fP.ShowDialog();
                string sPwd = fP.PromptUserData;
                if (sPwd.Equals(FuzibleController.MainParams.USER_PASSWORD))
                {
                    Configuration xamlMainConfig = new(USERNAME, sConn, 0, sDBS, sDBT);
                    xamlMainConfig.Show();
                    xamlMainConfig.Closed += Event_ConfigurationClosed;
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_userpwdwrong);
                }
            }
        }

        private void BtnEditBDDImport_Click(object sender, RoutedEventArgs e)
        {
            if (cbDriverTarget.SelectedIndex > -1)
            {
                CONNString sConn = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());
                string sDBT = tbDatabaseTarget.Text;
                string sDBS = tbDatabaseSource.Text;

                FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_userpwdask, Languages.Languages.ma_msg_userpwd, true, true);
                fP.ShowDialog();
                string sPwd = fP.PromptUserData;
                if (sPwd.Equals(FuzibleController.MainParams.USER_PASSWORD))
                {
                    Configuration xamlMainConfig = new(USERNAME, sConn, 0, sDBS, sDBT);

                    xamlMainConfig.Show();
                    xamlMainConfig.Closed += Event_ConfigurationClosed;
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_userpwdwrong);
                }
            }
        }

        private void BtnDeleteJob_Click(object sender, RoutedEventArgs e)
        {

            if (cbINISection.SelectedIndex >= 0)
            {
                if (FuzibleController.OWN_USER)
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_deletejobsure01, Languages.Languages.ma_msg_deletejobsure02, MessageBoxButton.OKCancel);

                    if (msgR.ToString().ToUpper().Equals("OK"))
                    {
                        string sJobID = cbINISection.SelectedValue.ToString();

                        try
                        {
                            string sStatus = FuzibleController.DeleteJob(sJobID);
                            MessageBox.Show(sStatus);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }

                        LbJobStep.Items.Clear();
                        LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
                        cbINISection.SelectedIndex = -1;
                        ClearFields();
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
        }

        private void BtnRenameJob_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0)
            {
                if (FuzibleController.OWN_USER)
                {
                    //string sCbIndex = cbINISection.SelectedValue.ToString();
                    string sCurrentJobID = cbINISection.SelectedValue.ToString();

                    FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_renamejobsure01, Languages.Languages.ma_msg_renamejobsure02, false, true, FuzibleController.GetJob(sCurrentJobID).JobNAME);
                    fP.ShowDialog();
                    string sNomJob = fP.PromptUserData.Trim();
                    sNomJob = sNomJob.Replace("[", "(").Replace("]", ")"); // caractères réservés

                    string sNewJobName = sNomJob.Trim();

                    if (sNomJob.Length > 0 && cbINISection.SelectedIndex >= 0)
                    {

                        string sStatus = FuzibleController.RenameJob(sCurrentJobID, sNewJobName);

                        MessageBox.Show(sStatus);
                        string sCbIndex = cbINISection.SelectedValue.ToString();
                        LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");

                        if (!sCbIndex.IsNullOrEmpty())
                        {
                            cbINISection.SelectedValue = sCbIndex;
                        }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_renamejobnotvalid); }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
        }

        private void BtnChangePwd_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedValue != null)
            {
                if (FuzibleController.OWN_USER)
                {
                    string sCurrentJobID = cbINISection.SelectedValue.ToString();

                    FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_changepwd01, Languages.Languages.ma_msg_changepwd02, true, true);
                    fP.ShowDialog();
                    string sOldPassword = fP.PromptUserData;

                    if (sOldPassword.Equals(BYPASS_PWD))
                    {
                        sOldPassword = FITools.EncryptionSystem.AES_Decrypt(FuzibleController.GetJob(sCurrentJobID).JobPassword, FuzibleController.GetJob(sCurrentJobID).JobID[1..FuzibleController.GetJob(sCurrentJobID).JobID.IndexOf("]")]);
                    }

                    if (sOldPassword.Equals(FITools.EncryptionSystem.AES_Decrypt(FuzibleController.GetJob(sCurrentJobID).JobPassword, FuzibleController.GetJob(sCurrentJobID).JobID[1..FuzibleController.GetJob(sCurrentJobID).JobID.IndexOf("]")])))
                    {
                        //------------
                        FuzibleUITools.Prompt fP2 = new(Languages.Languages.ma_msg_changepwd03, Languages.Languages.ma_msg_changepwd04, true, true);
                        fP2.ShowDialog();
                        string sNewPassword = fP2.PromptUserData;

                        if (sNewPassword.Length > 0)
                        {
                            string sStatus = FuzibleController.ChangeJobPassword(sCurrentJobID, sNewPassword);
                            MessageBox.Show(sStatus);

                            string sCbIndex = cbINISection.SelectedValue.ToString();
                            LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");

                            if (!sCbIndex.IsNullOrEmpty())
                            {
                                cbINISection.SelectedValue = sCbIndex;
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_changepwdinvalid); }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_wrongpwd); }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_nojobselected); }
        }

        private void BtnCreerNouveau_Click(object sender, RoutedEventArgs e)
        {
            CreateNewJob();
        }

        private void TbNextStepJob_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0)
            {
                if (LbJobStep.SelectedIndex < LbJobStep.Items.Count - 1)
                {
                    LbJobStep.SelectedIndex += 1;
                }
                else
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_addstep01, Languages.Languages.ma_msg_addstep02, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        string sCurrentJobID = cbINISection.SelectedValue.ToString();
                        sCurrentJobID = string.Concat("[", FuzibleController.GetJob(sCurrentJobID).RawJobID.ToString(), "]");
                        ClearFields();

                        FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputnewjob01, Languages.Languages.ma_msg_inputnewjob02, false, true, Languages.Languages.ma_msg_inputnewjobdefault);
                        fP.ShowDialog();
                        string sNewJobName = fP.PromptUserData.Trim();

                        string sStatus = FuzibleController.AddNewJob(true, FuzibleController.GetJob(sCurrentJobID), sNewJobName);

                        if (Regex.IsMatch(sStatus, @"\[\d+-\d+\]"))
                        {
                            Job INIP = InitJobParameters(sStatus);
                            INIP.JobNAME = sNewJobName;

                            SaveJob(INIP, false);

                            Job SubINIP = FuzibleController.GetJob(sStatus);

                            if (SubINIP != null)
                            {
                                ComboBoxItem cbI = new()
                                {
                                    Content = string.Concat(SubINIP.SubJobID, " > ", SubINIP.JobNAME),
                                    Tag = SubINIP.JobID,
                                };
                                LbJobStep.Items.Add(cbI);
                                LbJobStep.SelectedIndex += 1;
                            }
                            else { MessageBox.Show(Languages.Languages.ma_jobconf_btn_createnewsubjob_ko); }
                        }
                        else { MessageBox.Show(sStatus); }
                    }
                }
            }
        }

        private void BtnSaveINI_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex > -1)
            {
                if (FuzibleController.OWN_USER)
                {
                    try
                    {
                        string sCurrentJobID = cbINISection.SelectedValue.ToString();
                        Job JobCopy = FuzibleController.GetJob(sCurrentJobID).DeepCopy();
                        Job CurrentJob = InitJobParameters(sCurrentJobID);

                        string sVersion = FuzibleController.GetJobVersionFromDB(CurrentJob);
                        if (!sVersion.Equals(CurrentJob.JobVersion))
                        {
                            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_jobconflict, "Conflict", MessageBoxButton.YesNo);
                            if (msgR.ToString().ToUpper().Equals("YES"))
                            {
                                CreateNewJob();
                            }
                            else
                            {
                                FuzibleController.ReloadINI();
                                LoadUI(FuzibleController.GetJob(CurrentJob.JobID), false);
                            }
                        }
                        else
                        {
                            if (!FuzibleController.JobsAreEqual(CurrentJob, JobCopy))
                            {
                                CurrentJob.IncrementVersion(true, false);
                            }

                            SaveJob(CurrentJob, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_savejobunable + ex.Message);
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
            }
            else
            {
                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_savejobunindexed01, Languages.Languages.ma_msg_savejobunindexed02, MessageBoxButton.YesNo);
                if (msgR.ToString().ToUpper().Equals("YES"))
                {
                    CreateNewJob();
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_savejobcancel);
                }

            }
        }

        private void BtnExecuteStepByStep_Click(object sender, RoutedEventArgs e)
        {
            if (FuzibleController.LOG.WaitUserAction)
            {
                FuzibleController.LOG.ContinueStepByStepJobExecution(0);
                btnExecuteStepByStep.IsEnabled = false;
                btnExecuteTask.IsEnabled = false;
            }
            else
            {
                ExecuteJob(true);
            }
        }

        private void BtnExecuteTask_Click(object sender, RoutedEventArgs e)
        {
            if (FuzibleController.LOG.WaitUserAction)
            {
                FuzibleController.LOG.ContinueStepByStepJobExecution(-1);
                btnExecuteStepByStep.IsEnabled = false;
                btnExecuteTask.IsEnabled = false;
            }
            else
            {
                ExecuteJob(false);
            }
        }

        private void BtnStopTask_Click(object sender, RoutedEventArgs e)
        {
            if (FuzibleController.LOG.WaitUserAction)
            {
                try
                {
                    FuzibleController.LOG.ContinueStepByStepJobExecution(2);
                }
                catch (OperationCanceledException)
                {
                    FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_cancellingsubthreads + Monitoring.GetQteThreads.ToString() + ")...", SQLTools_Enums.LOG_TYPEINFO.INF);
                    Monitoring.CancelJob();
                }
            }
            else
            {
                if (JobStatus == 0 || !FuzibleController.IsBackgroundTaskCompleted())
                {
                    MessageBox.Show(Languages.Languages.ma_msg_canceljob);

                    FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_cancellingsubthreads + Monitoring.GetQteThreads.ToString() + ")...", SQLTools_Enums.LOG_TYPEINFO.INF);

                    Monitoring.CancelJob();
                }

                //Exception ex = new Exception(Languages.Languages.ma_msg_jobcancelled);
                //FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, "", SQLTools_Enums.LOG_TYPEINFO.ERR);
            }
        }

        private void BtnDefineWsContentStructure_Click(object sender, RoutedEventArgs e)
        {
            FuzibleUITools.TextContent fP = new(Languages.Languages.ma_msg_editwscontent_info, Languages.Languages.ma_msg_editwscontent_title, lbWsJsonStructure.Content.ToString());
            fP.ShowDialog();
            string sData = fP.PromptUserData.Trim();

            lbWsJsonStructure.Content = sData;
        }

        private void BtnVoirRequetes_Click(object sender, RoutedEventArgs e)
        {
            Job JobSandbox = InitJobParameters("[0]");

            try
            {
                StringBuilder sbRequetes = new();

                string[] sVariables = RichTB.GetTextRTB(tbQueryVariableParameters).Trim().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries); //.Split(Convert.ToChar(";"));

                List<string> sListVariables = new();
                foreach (string sV in sVariables)
                {
                    sListVariables.Add(sV);
                }
                JobSandbox.DynParams = sListVariables;

                if (sListVariables.Count > 0)
                {
                    List<string> sListToShow = new() { };

                    if (JobSandbox.ConnectionString_Source != null)
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes01, JobSandbox.ConnectionString_Source.SConnString(sListVariables)));
                    }

                    if (JobSandbox.ConnectionString_Source != null && JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("DB"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes02, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbDatabaseTarget.Text.ToString(), sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Source != null && JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("NS"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes03, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbDatabaseTarget.Text.ToString(), sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Source != null && JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("FI"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes04, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbFilesZippedIn.Text, sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Source != null && (JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("DB") || JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("FI") || JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("NS")))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes05, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(RichTB.GetTextRTB(tbPostJobCommandSource), sListVariables)));
                    }

                    if (JobSandbox.ConnectionString_Target != null)
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes06, JobSandbox.ConnectionString_Target.SConnString(sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Target != null && JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("DB"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes07, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbDatabaseSource.Text.ToString(), sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Target != null && JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("NS"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes08, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbDatabaseSource.Text.ToString(), sListVariables)));
                    }
                    if (JobSandbox.ConnectionString_Target != null && (JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("DB") || JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("FI") || JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("NS")))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes09, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(RichTB.GetTextRTB(tbPostJobCommandTarget), sListVariables)));
                    }
                    sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes10, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbWSReponsesTrackingColumn.Text, sListVariables)));
                    if (JobSandbox.ConnectionString_Target != null && JobSandbox.ConnectionString_Target.SConnDriverSuffix.Equals("WS"))
                    {
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes11, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbWSReponsesTrackingColumn.Text, sListVariables)));
                        sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes12, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbWSSuccessString.Text, sListVariables)));
                    }
                    sListToShow.Add(string.Concat(Languages.Languages.ma_msg_voirrequetes13, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(tbMailAdress.Text, sListVariables)));

                    if (cbConnTarget.SelectedIndex > -1 && cbConnTarget.SelectedValue.ToString()[..2].Equals("FI"))
                    {
                        string sModifiedWorkingDirectory = "";
                        string sNomFichierFinal = "";
                        string sFilename = "";
                        string sReplacedFile = "";
                        List<string> sPathFileExt = new();
                        List<string> sListReplacement = new();

                        sListToShow.Add(Languages.Languages.ma_msg_voirrequetes14);

                        string[] sLines = RichTB.GetTextRTB(tbQueries).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string sQ in sLines)
                        {
                            sFilename = new Query(JobSandbox, sQ).OutputTable;
                            sModifiedWorkingDirectory = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobSandbox.ConnectionString_Target.SConnString(sListVariables), JobSandbox.DynParams, sFilename);
                            sNomFichierFinal = string.Concat(sModifiedWorkingDirectory, sFilename);
                            sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sNomFichierFinal);
                            Query sQQ = new Query(JobSandbox, sQ);
                            sListReplacement = FITools.NamingPatternGenerator(JobSandbox, sPathFileExt, JobSandbox.ConnectionString_Target.SConnDriver.ToString()[(JobSandbox.ConnectionString_Target.SConnDriver.ToString().IndexOf("_") + 1)..], 0, 1, null, sQQ.QueryAnalyzer.Tables[0].Name, ref FuzibleController.LOG, sQQ);
                            sReplacedFile = string.Concat(sListReplacement[0], sListReplacement[3], sListReplacement[1]);
                            sListToShow.Add(sReplacedFile);
                        }
                    }
                    StringBuilder sbHelp = new();
                    sbHelp.Append(string.Join(Environment.NewLine, sListToShow.ToArray()));

                    if (AppHelp != null)
                    {
                        Help xamlHelp = new(new BlockHelp("Fuzible", Languages.Languages.ma_msg_voirrequetesdynparams, sbHelp.ToString(), ""));
                        xamlHelp.ShowDialog();
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_voirrequetesnodynparam); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Languages.Languages.ma_msg_voirrequetes_unableloadfile + ex.Message);
            }
        }

        private void btnQueryFontPlus_Click(object sender, RoutedEventArgs e)
        {
            if (tbQueries.FontSize < 20)
            {
                tbQueries.FontSize++;
                tbQueriesSandbox.FontSize++;
            }
        }

        private void btnQueryFontMoins_Click(object sender, RoutedEventArgs e)
        {
            if (tbQueries.FontSize > 6)
            {
                tbQueries.FontSize--;
                tbQueriesSandbox.FontSize--;
            }
        }

        private void CkPreJobCommandSource_LoopThroughResult_Click(object sender, RoutedEventArgs e)
        {
            CheckBox ck = (CheckBox)sender;
            if (ck.IsChecked.Value)
            {
                Help help = new(AppHelp.GetHelpBlock("PRE_COMMANDS_LOOP_RESULTS"));
                help.Show();
            }
            else
            {
                Help help = new(AppHelp.GetHelpBlock("PRE_COMMANDS_DONT_LOOP_RESULTS"));
                help.Show();
            }
        }

        private void CkAddPkAfterCreateTable_Click(object sender, RoutedEventArgs e)
        {
            var ckB = (CheckBox)sender;
            if (ckB.IsChecked.Value)
            {
                MessageBox.Show(Languages.Languages.ma_msg_createpkwarning01 + Environment.NewLine + Languages.Languages.ma_msg_createpkwarning02);
            }
        }

        private void CkAllowSchemaAlterationTarget_Click(object sender, RoutedEventArgs e)
        {
            if (cbConnTarget.SelectedIndex > -1)
            {
                var ckB = (CheckBox)sender;
                if (ckB.IsChecked.Value)
                {
                    cbSqlAlterOptions.Visibility = Visibility.Visible;

                    CONNString CSt = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());
                    if (CSt.SConnDriverSuffix.Equals("DB") && cbDriverTarget.SelectedIndex > -1 && cbDriverTarget.SelectedValue.Equals(CSt.SConnDriver.ToString()))
                    {
                        if (CSt.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_TYPE).Length == 0)
                        {
                            StringBuilder sbCareful = new();
                            sbCareful.AppendLine(Languages.Languages.ma_msg_alterschemawarning01);
                            sbCareful.AppendLine(Languages.Languages.ma_msg_alterschemawarning02);
                            sbCareful.AppendLine(Languages.Languages.ma_msg_alterschemawarning03);
                            sbCareful.AppendLine(Languages.Languages.ma_msg_alterschemawarning04);
                            sbCareful.AppendLine(Languages.Languages.ma_msg_alterschemawarning05);
                            MessageBox.Show(sbCareful.ToString());
                        }
                    }
                }
                else { cbSqlAlterOptions.Visibility = Visibility.Hidden; }
            }
        }

        private void CkSqlLog_Checked(object sender, RoutedEventArgs e)
        {
            if (!FuzibleController.SQL_LOG_CONFIGURED)
            {
                MessageBox.Show(Languages.Languages.ma_msg_sqllognoconn);
                ckSqlLog.IsChecked = false;
            }
        }

        private void BtnOpenLogPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessStartInfo startInfo = new(FuzibleController.LOG.PathToLog) { UseShellExecute = true };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (AppHelp != null)
            {
                Help xamlHelp;

                switch (btn.Name)
                {
                    case "btnHelpForAPIDataToRetrieve":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("API DATA HELPER"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForHTMLTemplate":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("MAIL HTML TEMPLATE"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForSHSAnalyzer":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("SHS DATA ANALYZER"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForPivotTransform":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("DATA TRANSFORMATION"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForParallelQueries":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("PARALLEL QUERIES"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForMultipleFilesAtOnce":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("RULES FOR MULTIPLES FILES AT ONCE"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForPrePostCommandsSource":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("PRE AND POST JOB COMMANDS"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForParallelInsert":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("PARALLEL INSERT"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForMultipleFilesTargetPattern":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("RULES FOR MULTIPLE FILES OUTPUT"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForXMlRowBuilder":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("XML ROW BUILDER"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForJSONRowBuilder":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("JSON ROW BUILDER"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForPrePostCommandsTarget":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("PRE AND POST JOB COMMANDS"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForQueries":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("SOURCE QUERIES"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForDynamicQueryParameters":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("BASIC SCRIPT LANGUAGE"));
                        xamlHelp.ShowDialog();
                        break;
                    case "btnHelpForCommandLine":
                        xamlHelp = new Help(AppHelp.GetHelpBlock("COMMAND LINE"));
                        xamlHelp.ShowDialog();
                        break;
                }
            }
        }

        private void BtnLoadDemoQuery_Click(object sender, RoutedEventArgs e)
        {
            Job INIP = InitJobParameters("[0]");
            if (INIP.ConnectionString_Source != null && INIP.ConnectionString_Target != null)
            {
                string sQuery = FuzibleController.GetExempleQuery(INIP);
                if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                ColorizeRichTextBox(false, true);
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_startjobconfigureconn); }
        }

        private void BtnZipFileBrowser_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new()
            {
                Filter = "(*.ZIP) | *.ZIP"
            };
            if (cbConnSource.SelectedIndex > 1)
            {
                CONNString CS = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());
                if (CS != null && CS.SConnDriverSuffix.Equals("FI"))
                {
                    string sStr = CS.SConnString(RichTB.GetTextRTB(tbQueryVariableParameters).Split(Convert.ToChar(";")).ToList());
                    if (sStr.IndexOf("=") > 0)
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_zipfileonsftpwarning);
                    }
                    else
                    {
                        fileDialog.InitialDirectory = sStr;
                        fileDialog.ShowDialog();
                        if (fileDialog.FileName.Length > 0)
                        {
                            string sFile = fileDialog.FileName[(fileDialog.FileName.LastIndexOf("\\") + 1)..];
                            tbFilesZippedIn.Text = sFile;
                        }
                    }
                }

            }
        }

        private void BtnExportLog_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(FuzibleController.LOG.GetFullLOGAsString());
            MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard);
        }

        private void TbXSLSheetToRead_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (cbConnSource.SelectedIndex > -1)
            {
                CONNString CS = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());

                if (CS != null && CS.SConnDriver == SQLTools_Enums.BDD.FI_XLS && INTELLISENSE.Connection != null)
                {
                    AskIntellisenseRefresh(true);
                }
            }
        }

        private void TbQueries_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            LbQueryTooltip.Items.Clear();
            LbQueryTooltip.Visibility = Visibility.Hidden;
        }

        private void TbQueryVariableParameters_KeyUp(object sender, KeyEventArgs e)
        {
            //positionner le combobox des commandes dynamiques sur le bon pattern
            string[] sParams = RichTB.GetTextRTB(tbQueryVariableParameters).Split(Convert.ToChar(";"), StringSplitOptions.RemoveEmptyEntries);
            var cb0 = (ComboBoxItem)cbListDynamicCommands.Items[0];
            cb0.Content = "";
            int iIndex = -1;
            bool bFound = false;
            if (sParams.Length > 0)
            {
                foreach (ComboBoxItem cbI in cbListDynamicCommands.Items)
                {
                    iIndex++;
                    if (cbI.Content.ToString().StartsWith(sParams.Last(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (!cbI.Content.ToString().StartsWith("%"))
                        {
                            string sData = cbI.Content.ToString();
                            sData = sData.Split(Convert.ToChar("["))[0].Trim();
                            string sScript = sData.Split(Convert.ToChar(":"))[0].Trim();
                            string sExemple = "%YYYYMM<1M";
                            if (sExemple.Length > 0) { sExemple = string.Concat(" [", sExemple, "]"); }
                            cbI.Content = string.Concat(sData, sExemple);
                        }
                        else
                        {
                            string sData = cbI.Content.ToString();
                            sData = sData.Split(Convert.ToChar("["))[0].Trim();
                            string sScript = sData.Split(Convert.ToChar(":"))[0].Trim();
                            string sExemple = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage("{?1}", new List<string> { sScript });
                            if (sExemple.Length > 0) { sExemple = string.Concat(" [", sExemple, "]"); }
                            cbI.Content = string.Concat(sData, sExemple);
                        }
                        cbListDynamicCommands.SelectedIndex = iIndex;
                        bFound = true;
                        break;
                    }
                }
                if (!bFound)
                {
                    cbListDynamicCommands.SelectedIndex = 0;
                    var cbI = (ComboBoxItem)cbListDynamicCommands.Items[0];
                    cbI.Content = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage("{?1}", new List<string> { sParams.Last() });
                }
            }
            else { cbListDynamicCommands.SelectedIndex = -1; }

            TextRange tR = new(tbQueryVariableParameters.Document.ContentStart, tbQueryVariableParameters.Document.ContentEnd);
            tR.ClearAllProperties();
            RichTB.SetColorsRTB(tbQueryVariableParameters, new List<RTBColorizer> { new RTBColorizer(";", new SolidColorBrush(Colors.Red), FontWeights.UltraBold, FontStyles.Normal) }, true);
        }

        private void TbQueriesSandbox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                if (cbConnSandbox.SelectedIndex > -1)
                {
                    Query sQ;

                    CONNString CSsb = FuzibleController.GetConnection(cbConnSandbox.SelectedValue.ToString());

                    Job JobSandbox = InitJobParameters("[0]");
                    JobSandbox.ConnectionString_Source = CSsb;
                    JobSandbox.DatabaseName_Source = tbDatabaseSandbox.Text.Length > 0 ? tbDatabaseSandbox.Text : CSsb.SConnDB;
                    JobSandbox.ConnectionString_Target = null;
                    JobSandbox.JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT;

                    string sRawQuery = RichTB.GetTextRTB(tbQueriesSandbox).Trim();
                    sQ = new Query(JobSandbox, sRawQuery);

                    if (sQ != null && sQ.QueryAnalyzer != null)
                    {
                        ShowSourceDataset(JobSandbox, sQ, null, false, false);
                    }
                    else if (sQ != null && sQ.QueryAnalyzer == null && JobSandbox.ConnectionString_Source.SConnDriverSuffix.Equals("DB"))
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_abnormalquerysandbox);

                        ShowSourceDataset(JobSandbox, null, sRawQuery, false, false);
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_badquery);
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_querysandboxmustchooseconn); }
            }
        }

        private void TbQueries_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Job JobSandbox = InitJobParameters("[0]");
            Query sQ = FindQueryFromRichTextBox(sender, JobSandbox);

            LbQueryTooltip.Items.Clear();
            LbQueryTooltip.Visibility = Visibility.Hidden;

            ContextMenu cm = new()
            {
                Style = Resources["FlatContextMenu"] as Style
            };//make a context menu instance

            if (sQ != null)
            {
                if (TEMP_QUERY == null)
                {
                    LoadQueryMenu(cm, sQ, JobSandbox);
                }
                else if (TEMP_QUERY.RawQuery.Length != sQ.RawQuery.Length || !TEMP_QUERY.RawQuery.Equals(sQ.RawQuery))
                {
                    LoadQueryMenu(cm, sQ, JobSandbox);
                }
                else { LoadQueryMenu(cm, TEMP_QUERY, JobSandbox); }
            }

            ((RichTextBox)sender).ContextMenu = cm;//add the context menu to the sender

            ColorizeRichTextBox(false, false);
        }

        private void TbPreviousStepJob_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex >= 0 && LbJobStep.SelectedIndex > 0)
            {
                LbJobStep.SelectedIndex -= 1;
            }
        }

        private void TbJobParameterizedLaunch_DoubleClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(tbJobParameterizedLaunch.Text);
            MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard);
        }

        private void LbJobStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbJobStep.SelectedValue != null)
            {
                if (cbINISection.SelectedIndex > -1)
                {
                    var cb = (ComboBox)e.Source;
                    var cbI = (ComboBoxItem)cb.SelectedItem;
                    string sCurrentJobID = cbI.Tag.ToString();
                    cbINISection.SelectedValue = sCurrentJobID;
                }
            }
        }

        private void LbQueryTooltip_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var lbI = (ListBoxItem)LbQueryTooltip.SelectedItem;
                string sTextIntellisense = lbI.Content.ToString();
                InsertIntellisense(sTextIntellisense);
            }

            if (e.Key == Key.Escape)
            {
                LbQueryTooltip.Items.Clear();
                LbQueryTooltip.Visibility = Visibility.Hidden;
                tbQueries.Focus();
            }
        }

        private void LbQueryTooltip_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var lbI = (ListBoxItem)LbQueryTooltip.SelectedItem;
            string sTextIntellisense = lbI.Content.ToString().Trim();
            InsertIntellisense(sTextIntellisense);
            LbQueryTooltip.Items.Clear();
            LbQueryTooltip.Visibility = Visibility.Hidden;
            tbQueries.Focus();
        }

        private void OnControlDoubleClick(object sender, MouseButtonEventArgs e)
        {
        }

        private void InsertIntellisense(string sTextIntellisense)
        {
            try
            {
                sTextIntellisense = sTextIntellisense.Trim();

                //on enlève la partie exemple (commence toujours par (ex:)
                if (sTextIntellisense.IndexOf(" [", StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    sTextIntellisense = sTextIntellisense[..sTextIntellisense.IndexOf(" [", StringComparison.InvariantCultureIgnoreCase)];
                }
                if (sTextIntellisense.IndexOf("(ex : ", StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    sTextIntellisense = sTextIntellisense[..sTextIntellisense.IndexOf("(ex : ", StringComparison.InvariantCultureIgnoreCase)];
                }

                string sTextQuery = tbQueries.CaretPosition.GetTextInRun(LogicalDirection.Backward);
                if (sTextQuery.EndsWith("\t")) { tbQueries.CaretPosition.DeleteTextInRun(-1); }
                sTextQuery = sTextQuery.Trim();
                //recherche dans la requête le bout de texte qui a provoqué le déclenchement de l'intellisense et le coix d'un élément
                for (int iP = sTextQuery.Length - 1; iP > 0; iP--)
                {
                    string sSearch = sTextQuery[iP..];
                    if (sTextIntellisense.StartsWith(sSearch, StringComparison.InvariantCultureIgnoreCase))
                    {
                        tbQueries.CaretPosition.DeleteTextInRun(-sSearch.Length); break;
                    }
                }

                if (sTextIntellisense.Length > 0)
                {
                    string sTemp = tbQueries.CaretPosition.GetTextInRun(LogicalDirection.Backward);
                    if (sTemp.EndsWith(" ") || sTemp.EndsWith("."))
                    {
                        tbQueries.CaretPosition.InsertTextInRun(sTextIntellisense);
                        tbQueries.CaretPosition = tbQueries.CaretPosition.GetPositionAtOffset(sTextIntellisense.Length);
                    }
                    else
                    {
                        tbQueries.CaretPosition.InsertTextInRun(" " + sTextIntellisense);
                        tbQueries.CaretPosition = tbQueries.CaretPosition.GetPositionAtOffset(sTextIntellisense.Length + 1);
                    }

                }
                LbQueryTooltip.Items.Clear();
                LbQueryTooltip.Visibility = Visibility.Hidden;
                tbQueries.Focus();
            }
            catch { }
        }

        private void RichTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            //if (TUTORIAL_STEP > -1)
            //{
            //    HandleTutorialKeyPress(sender, e);
            //}
            //else
            //{
            if (FuzibleController.MainParams.ENABLE_QUERY_ASSISTANT)
            {
                if (e.Key == Key.Enter)
                {
                    ColorizeRichTextBox(true, true);
                    LbQueryTooltip.Items.Clear();
                    LbQueryTooltip.Visibility = Visibility.Hidden;
                }
                else
                {
                    if (e.Key == Key.Tab && LbQueryTooltip.Items.Count > 0) //donner la main sur intellisense
                    {
                        LbQueryTooltip.SelectedIndex = 0;
                        LbQueryTooltip.Focus();
                    }
                    else
                    {
                        Job JobSandbox = InitJobParameters("[0]");
                        CONNString CSs = FindConnectionFromQueryInRichTextBox(sender, JobSandbox);
                        CONNString CSt = JobSandbox.ConnectionString_Target;

                        Query sQ = null;

                        if (CSs != null && CSt != null)
                        {
                            if (e.Key == Key.F5)
                            {
                                sQ = FindQueryFromRichTextBox(sender, JobSandbox);
                                if (sQ != null)
                                {
                                    ShowSourceDataset(JobSandbox, sQ, null, false, true);
                                }
                            }

                            if (FuzibleController.OWN_USER)
                            {
                                var box = (RichTextBox)sender;
                                string sText = box.CaretPosition.GetTextInRun(LogicalDirection.Backward);
                                string sRawText = sText;
                                sText = sText.Trim();
                                sText = Regex.Replace(sText, SHSRegex.REGEX_WHITESPACES, " ");
                                List<string> sListToShow = new();

                                if (!INTELLISENSE.IsLoading && INTELLISENSE.Connection != null)
                                {
                                    if (INTELLISENSE.Connection.SConnID.Equals(CSs.SConnID)) //on ne fait pas d'intellisense sur une cross-join (trop dur à gérer)
                                    {
                                        if (sRawText.Length > 0)
                                        {
                                            foreach (string sSQL in INTELLISENSE.Patterns)
                                            {
                                                string sLastWord = sText;
                                                if (sLastWord.IndexOf(" ") > -1) { sLastWord = sLastWord[(sLastWord.IndexOf(" ") + 1)..]; }
                                                if (sLastWord.IndexOf(":") > -1) { sLastWord = sLastWord[(sLastWord.LastIndexOf(":") + 1)..]; }

                                                if (sSQL.StartsWith(sLastWord, StringComparison.InvariantCultureIgnoreCase) && sSQL.Split("[")[0].Trim().Length > sLastWord.Length)
                                                {
                                                    sListToShow.Add(string.Concat("{PT}", sSQL));
                                                }
                                            }
                                            if (sText.EndsWith("FROM", StringComparison.InvariantCultureIgnoreCase) || sText.Trim().EndsWith("JOIN", StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                foreach (string sT in INTELLISENSE.Tables)
                                                {
                                                    sListToShow.Add(string.Concat("{TT}", sT));
                                                }
                                            }
                                            else //je recherche la dernière itération du FROM ou JOIN
                                            {
                                                foreach (string sT in INTELLISENSE.Tables)
                                                {
                                                    for (int i = 1; i < sT.Length; i++)
                                                    {
                                                        if (sText.EndsWith(string.Concat("FROM ", sT[..i]), StringComparison.InvariantCultureIgnoreCase) || sText.Trim().EndsWith(string.Concat("JOIN ", sT[..i]), StringComparison.InvariantCultureIgnoreCase))
                                                        {
                                                            sListToShow.Add(string.Concat("{TT}", sT)); break;
                                                        }
                                                    }
                                                }
                                            }

                                            foreach (IntellisenseData.TablesAndFieldsInQuery sFields in INTELLISENSE.TablesInQuery)
                                            {
                                                foreach (string sAlias in sFields.Aliases)
                                                {
                                                    if (sText.EndsWith(string.Concat(sAlias, "."), StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        foreach (string sF in sFields.Fields)
                                                        {
                                                            sListToShow.Add(string.Concat("{FF}", sF));
                                                        }
                                                    }
                                                    else if (sText.EndsWith(sAlias, StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        foreach (string sF in sFields.Fields)
                                                        {
                                                            sListToShow.Add(string.Concat("{FP}", sAlias, ".", sF));
                                                        }
                                                    }
                                                    else
                                                    {
                                                        foreach (string sF in sFields.Fields)
                                                        {
                                                            for (int i = 0; i < sF.Length; i++)
                                                            {
                                                                if (sText.EndsWith(string.Concat(",", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat(", ", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("(", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("SELECT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("DISTINCT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("GROUP BY", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("ORDER BY", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("WHERE", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                                    || sText.EndsWith(string.Concat("AND", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase))
                                                                {
                                                                    if (INTELLISENSE.TablesInQuery.Count > 1)
                                                                    {
                                                                        sListToShow.Add(string.Concat("{FP}", sAlias, ".", sF)); break;
                                                                    }
                                                                    else
                                                                    {
                                                                        sListToShow.Add(string.Concat("{FF}", sF)); break;
                                                                    }
                                                                }
                                                                else if (sText.EndsWith(string.Concat(sAlias, ".", sF[..i]), StringComparison.InvariantCultureIgnoreCase))
                                                                {
                                                                    sListToShow.Add(string.Concat("{FF}", sF)); break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            foreach (string sF in IntellisenseData.TransfoPatterns(INTELLISENSE.Connection.SConnDriverSuffix))
                                            {
                                                for (int i = 0; i < sF.Length; i++)
                                                {

                                                    if (sText.EndsWith(string.Concat("SELECT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("DISTINCT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat(", ", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat(",", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("GROUP BY", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("WHERE", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("AND", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("ORDER BY", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        sListToShow.Add(string.Concat("{FC}", sF));
                                                    }
                                                }
                                            }

                                            foreach (string sF in IntellisenseData.AftSelectFunc(INTELLISENSE.Connection.SConnDriverSuffix)) //tout ce qui est après SELECT (DISTINCT, TOP
                                            {
                                                //doit finir par table + as ou juste table
                                                for (int i = 0; i < sF.Length; i++)
                                                {
                                                    if (sText.EndsWith(string.Concat("SELECT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase)
                                                       || sText.EndsWith(string.Concat("DISTINCT", i > 0 ? " " : "", sF[..i]), StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        sListToShow.Add(string.Concat("{AS}", sF));
                                                    }
                                                }
                                            }

                                            //on recherche vite fait si on a avant un pattern output:SELECT blabla FROM blabla
                                            //en redescendant le curseur
                                            bool[] bHasQuery = QuickQuerySearch(box);

                                            if (bHasQuery[0])
                                            {
                                                foreach (string sF in IntellisenseData.JoinPatterns) //INNER JOIN...
                                                {
                                                    //doit finir par table + as ou juste table
                                                    for (int i = 0; i < sF.Length; i++)
                                                    {

                                                        if (sRawText.EndsWith(" " + sF[..i], StringComparison.InvariantCultureIgnoreCase))
                                                        {
                                                            sListToShow.Add(string.Concat("{JP}", sF));
                                                        }
                                                    }
                                                }

                                                foreach (string sF in IntellisenseData.FuncPatterns) //LIMIT, ORDER, WHERE, UNION
                                                {
                                                    for (int i = 0; i < sF.Length; i++)
                                                    {

                                                        if (sRawText.EndsWith(" " + sF[..i], StringComparison.InvariantCultureIgnoreCase))
                                                        {
                                                            sListToShow.Add(string.Concat("{MP}", sF));
                                                        }
                                                    }
                                                }
                                            }
                                            if (bHasQuery[1])
                                            {
                                                foreach (string sF in IntellisenseData.AggPatterns) //GROUP BY...
                                                {
                                                    for (int i = 0; i < sF.Length; i++)
                                                    {

                                                        if (sRawText.EndsWith(" " + sF[..i], StringComparison.InvariantCultureIgnoreCase))
                                                        {
                                                            sListToShow.Add(string.Concat("{AP}", sF));
                                                        }
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }

                                TextPointer tp = tbQueries.Selection.End;

                                LbQueryTooltip.Items.Clear();
                                LbQueryTooltip.Visibility = Visibility.Hidden;

                                if (tp != null && sListToShow.Count > 0)
                                {
                                    string sType = "";
                                    string sFromType = "";
                                    switch (INTELLISENSE.Connection.SConnDriverSuffix)
                                    {
                                        case "AD":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_adobj;
                                            break;
                                        case "DB":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_tables;
                                            break;
                                        case "FI":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_files;
                                            break;
                                        case "WS":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_wsobj;
                                            break;
                                        case "MB":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_mailbox;
                                            break;
                                        case "NS":
                                            sFromType = Languages.Languages.ma_msg_queryassistant_coll;
                                            break;
                                    }

                                    for (int i = 0; i < sListToShow.Count; i++)
                                    {
                                        if (!sType.Equals(sListToShow[i][0..4]))
                                        {
                                            sType = sListToShow[i][0..4];
                                            //changement, on ajoute un menu intermédiaire
                                            if (sType.Equals("{PT}")) { } //SELECT ET FROM
                                            else if (sType.Equals("{TT}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = sFromType, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //TABLES
                                            else if (sType.Equals("{FC}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_sqlfunctions, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //SQL
                                            else if (sType.Equals("{AS}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_sqlafterselect, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //APRES SELECT
                                            else if (sType.Equals("{JP}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_sqljoin, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //JOIN
                                            else if (sType.Equals("{MP}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_sqlstd, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //MISC
                                            else if (sType.Equals("{AP}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_sqlagg, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //AGG
                                            else if (sType.Equals("{FP}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_fieldsalias, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //FIELDS
                                            else if (sType.Equals("{FF}")) { LbQueryTooltip.Items.Add(new ListBoxItem() { Foreground = Brushes.Black, Content = Languages.Languages.ma_msg_queryassistant_fieldsnoalias, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, IsEnabled = false }); } //FIELDS
                                        }
                                        //sType = sListToShow[i][0..4];
                                        Brush bR = Brushes.Black;
                                        if (sType.Equals("{TT}")) { bR = Brushes.DarkGreen; } //TABLES
                                        else if (sType.Equals("{FC}")) { bR = Brushes.DarkCyan; } //SQL
                                        else if (sType.Equals("{FP}")) { bR = Brushes.DarkSlateBlue; } //FIELDS
                                        else if (sType.Equals("{FF}")) { bR = Brushes.DarkBlue; } //FIELDS

                                        LbQueryTooltip.Items.Add(new ListBoxItem() { Name = "i_" + i.ToString(), Foreground = bR, Content = "  " + sListToShow[i][4..] });
                                    }

                                    Point pM3 = tbQueries.CaretPosition.GetCharacterRect(LogicalDirection.Forward).Location;

                                    double dPointX = pM3.X + 30;
                                    double dPointY = pM3.Y + 60;

                                    LbQueryTooltip.Margin = new Thickness(dPointX, dPointY, 0, 0);

                                    //LbQueryTooltip.Margin = new Thickness(tbQueries.Width - 200, tbQueries.Height - 200, 0, 0);
                                    LbQueryTooltip.Visibility = Visibility.Visible;
                                    LbQueryTooltip.Background = System.Windows.Media.Brushes.White;
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_queryassistantmusthaveconn);
                        }
                    }
                }
                //}
            }
        }

        private static bool[] QuickQuerySearch(RichTextBox box)
        {
            bool[] bQueryExists = new bool[] { false, false };

            TextPointer pointerA = box.CaretPosition;
            TextPointer pointerB = box.Document.ContentStart;
            TextRange tRInit = new(pointerA, pointerB);
            string sQ = tRInit.Text.Trim();

            if (Regex.Match(sQ, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
            {
                sQ = sQ[(sQ.LastIndexOf(":") + 1)..];
                if (Regex.IsMatch(sQ, SHSRegex.REGEX_BASIC_QUERY, RegexOptions.IgnoreCase))
                {
                    bQueryExists[0] = true;
                }
                if (Regex.IsMatch(sQ, SHSRegex.REGEX_BASIC_QUERY_AGG, RegexOptions.IgnoreCase))
                {
                    bQueryExists[1] = true;
                }

            }

            return bQueryExists;

        }

        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                string lPastingText = e.DataObject.GetData(DataFormats.Text) as string;
                //tbQueries.Document.ContentEnd.InsertTextInRun(lPastingText);
                tbQueries.Selection.Text = "";
                tbQueries.CaretPosition.InsertTextInRun(lPastingText);
                e.CancelCommand();
                ColorizeRichTextBox(false, true);
            }
            catch { }
        }

        private void AdjustRichTextBoxWidth(RichTextBox rtb, string sText)
        {
            if ((sText.Length * 5.5) > rtb.Document.PageWidth)
            {
                rtb.Document.PageWidth = sText.Length * 5.5;
            }
            rtb.ScrollToEnd();
        }

        #endregion


        #region "METHODES"

        private void ExecuteJob(bool bStepbystep)
        {
            if (cbConnSource.SelectedIndex > -1 && cbConnTarget.SelectedIndex > -1)
            {
                if (CheckFieldsForErrors())
                {
                    Job CurrentJob;
                    if (cbINISection.SelectedIndex > -1)
                    {
                        string sJobID = cbINISection.SelectedValue.ToString();
                        CurrentJob = FuzibleController.GetJob(sJobID);
                        List<string> sListManualDynParams = Toolbox.FromStringToList(RichTB.GetTextRTB(tbQueryVariableParameters));

                        if (FuzibleController.OWN_USER) //on ne sauvegarde pas un INI externe
                        {
                            Job JobCopy = CurrentJob.DeepCopy();
                            CurrentJob = InitJobParameters(sJobID);

                            //contrôle de concurrence avant sauvegarde
                            string sVersion = FuzibleController.GetJobVersionFromDB(CurrentJob);
                            if (!sVersion.Equals(CurrentJob.JobVersion))
                            {
                                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_jobconflict, "Conflict", MessageBoxButton.YesNo);
                                if (msgR.ToString().ToUpper().Equals("YES"))
                                {
                                    CreateNewJob();
                                }
                                else
                                {
                                    FuzibleController.ReloadINI();
                                    LoadUI(FuzibleController.GetJob(CurrentJob.JobID), false);
                                }
                            }
                            else
                            {
                                if (!FuzibleController.JobsAreEqual(CurrentJob, JobCopy))
                                {
                                    CurrentJob.IncrementVersion(true, false);
                                }

                                SaveJob(CurrentJob, true);
                            }
                        }
                        else
                        {
                            if (!sListManualDynParams.SequenceEqual(CurrentJob.DynParams))
                            {
                                //en cas de chargement d'un userspace externe, on ne peut pas sauvegarder les données, donc si on a changé les
                                //paramètres dynamiques, on est coincé. J'autorise donc ici le changement des dynparams   
                                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_startjobdynparam01, Languages.Languages.ma_msg_startjobdynparam02, MessageBoxButton.YesNo);
                                if (msgR.ToString().ToUpper().Equals("NO"))
                                {
                                    sListManualDynParams = CurrentJob.DynParams;
                                }
                            }
                        }

                        if (CurrentJob.Job_IsSubJob)
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_startjobslave);
                        }
                        FuzibleController.LOG = new LogTools(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), CurrentJob, true);


                        if (bStepbystep)
                        {
                            FuzibleController.LOG.ActivateStepByStepForJob(CurrentJob);
                            //s'abonner à l'évènement LOG
                            LogTools.OnNewStepByStepEvent += EventsReceiver_StepByStepLog;
                        }


                        CurrentJob.DynParams = sListManualDynParams;
                        bool bRun = FuzibleController.INIT_StartJob(CurrentJob, RichTB.GetTextRTB(tbQueries), ckSimulationMode.IsChecked.Value, DELEGATE_USERNAME);
                        if (!bRun)
                        {
                            MessageBox.Show(string.Concat(Languages.Languages.ma_msg_backgroundtaskrunning));
                        }
                        else
                        {
                            if (bStepbystep)
                            {
                                btnExecuteStepByStep.BorderThickness = new Thickness(1);
                                btnExecuteStepByStep.BorderBrush = System.Windows.Media.Brushes.Red;
                                btnExecuteTask.BorderThickness = new Thickness(1);
                                btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                                btnStopTask.BorderThickness = new Thickness(1);
                                btnStopTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            }
                            btnExecuteStepByStep.IsEnabled = false;
                            btnExecuteTask.IsEnabled = false;
                            //btnStopTask.IsEnabled = true;
                        }
                    }
                    else
                    {
                        string sJobID = "[0]";
                        MessageBox.Show(Languages.Languages.ma_msg_startjobsave);
                        CurrentJob = InitJobParameters(sJobID);

                        bool bRun = FuzibleController.INIT_StartJob(CurrentJob, RichTB.GetTextRTB(tbQueries), ckSimulationMode.IsChecked.Value, DELEGATE_USERNAME);
                        if (!bRun)
                        {
                            MessageBox.Show(string.Concat(Languages.Languages.ma_msg_backgroundtaskrunning));
                        }
                        else
                        {
                            if (bStepbystep)
                            {
                                btnExecuteStepByStep.BorderThickness = new Thickness(1);
                                btnExecuteStepByStep.BorderBrush = System.Windows.Media.Brushes.Red;
                                btnExecuteTask.BorderThickness = new Thickness(1);
                                btnExecuteTask.BorderBrush = System.Windows.Media.Brushes.Red;
                                btnStopTask.BorderThickness = new Thickness(1);
                                btnStopTask.BorderBrush = System.Windows.Media.Brushes.Red;
                            }
                            btnExecuteStepByStep.IsEnabled = false;
                            btnExecuteTask.IsEnabled = false;
                            //btnStopTask.IsEnabled = true;
                        }
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_startjobinputerrors); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_startjobconfigureconn); }
        }

        private void AskIntellisenseRefresh(bool bChangeConnection)
        {
            if (INTELLISENSE_REFRESH == 0 || (INTELLISENSE_REFRESH == 1 && bChangeConnection)) //priorité au changement de connection
            {

                INTELLISENSE_REFRESH = bChangeConnection ? 2 : 1;
            }
        }

        private void IntellisenseInit()
        {
            int iRefresh = INTELLISENSE_REFRESH;
            bool bBusy = false;

            if (iRefresh > 0)
            {
                INTELLISENSE_REFRESH = -1; //pour empêcher le rechargement alors que c'est pas fini

                Job JobSandbox = InitJobParameters("[0]");
                string sText = RichTB.GetTextRTB(tbQueries);
                sText = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sText, JobSandbox.DynParams);

                try
                {
                    if (iRefresh == 2)
                    {
                        Dispatcher.Invoke(() => { lbQueryLoading.Content = Languages.Languages.ma_msg_intellisense_newconnection; });
                        bBusy = INTELLISENSE.ChangeConnection(JobSandbox, sText);
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { lbQueryLoading.Content = Languages.Languages.ma_msg_intellisense_refreshqueries; });
                        INTELLISENSE.UpdateQueryData(JobSandbox, sText);
                    }
                }
                catch (OperationCanceledException)
                {
                    FuzibleController.LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_intellisense_cancelled, SQLTools_Enums.LOG_TYPEINFO.INF);
                }

                if (bBusy)
                {
                    INTELLISENSE_REFRESH = iRefresh;
                }
                else
                {
                    INTELLISENSE_REFRESH = 0;
                    Dispatcher.Invoke(() => { lbQueryLoading.Content = Languages.Languages.ma_msg_backgroundtaskpending; });
                }
            }
        }

        internal void PasswordControl(bool bChange)
        {
            if (FuzibleController.MainParams.USER_PASSWORD == null || bChange)
            {
                string sPwd = "";
                bool NotComplexEnough = true;
                bool NotMatch = true;
                while (NotComplexEnough)
                {
                    //création du mot de passe obligatoire !
                    FuzibleUITools.Prompt fP = new(bChange ? Languages.Languages.ma_msg_userpwdinfochange : Languages.Languages.ma_msg_userpwdinfo, Languages.Languages.ma_msg_userpwd, true, true);
                    fP.ShowDialog();
                    sPwd = fP.PromptUserData;
                    if (sPwd.Length >= 8 && Regex.Match(sPwd, "[0-9]+").Success && Regex.Match(sPwd, "[A-z]+").Success)
                    {
                        NotComplexEnough = false;
                    }
                    else
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_userpwdaddcomplexity, Languages.Languages.ma_msg_userpwdaddcomplexity, MessageBoxButton.OKCancel);
                        if (msgR.ToString().ToUpper().Equals("CANCEL"))
                        {
                            System.Environment.Exit(1);
                        }
                    }
                }
                while (NotMatch)
                {
                    FuzibleUITools.Prompt fPConf = new(Languages.Languages.ma_msg_userpwdinfo2, Languages.Languages.ma_msg_userpwd, true, true);
                    fPConf.ShowDialog();
                    string sPwdConf = fPConf.PromptUserData;

                    if (sPwd.Equals(sPwdConf))
                    {
                        NotMatch = false;
                    }
                    else
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_userpwdconfnotmatch, Languages.Languages.ma_msg_userpwdconfnotmatch, MessageBoxButton.OKCancel);
                        if (msgR.ToString().ToUpper().Equals("CANCEL"))
                        {
                            System.Environment.Exit(1);
                        }
                    }
                }

                bool bOK = FuzibleController.SetUserPassword(sPwd);
                if (bOK) { MessageBox.Show(Languages.Languages.ma_msg_userpwdset); }
                else { MessageBox.Show(Languages.Languages.ma_msg_userpwdnotset); }
            }
            else
            {
                FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_userpwdask, Languages.Languages.ma_msg_userpwd, true, true);
                fP.ShowDialog();
                string sPwd = fP.PromptUserData;
                if (sPwd.Equals(FuzibleController.MainParams.USER_PASSWORD))
                {
                    //rien à faire, c'est OK
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_userpwdwrong);
                    System.Environment.Exit(1);
                }
            }
        }

        internal void ChangePassword()
        {
            bool StillWrong = true;
            while (StillWrong)
            {
                FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_userpwdask, Languages.Languages.ma_msg_userpwd, true, true);
                fP.ShowDialog();
                string sPwd = fP.PromptUserData;

                if (sPwd.Equals(FuzibleController.MainParams.USER_PASSWORD))
                {
                    StillWrong = false;
                }
                else
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_userpwdwrongnoexit, Languages.Languages.ma_msg_userpwdwrong, MessageBoxButton.OKCancel);
                    if (msgR.ToString().ToUpper().Equals("CANCEL"))
                    {
                        System.Environment.Exit(1);
                    }
                }
            }
            PasswordControl(true);
        }

        private List<MenuItem> GetMenuItemsFromQuery(Job INIP, Query sQ, bool bIsSubQuery)
        {
            List<MenuItem> cmMenu = new();

            if (bIsSubQuery)
            {
                //supprimer les subqueries de la requête
                //rechercher prochaine itération de subquery et couper la requête à partir de là.
                string sQuery = sQ.RawQuery;
                Match mcSQ = Regex.Match(sQuery, SHSRegex.REGEX_CROSSJOINSCRIPT, RegexOptions.IgnoreCase);
                if (mcSQ.Success)
                {
                    sQuery = sQuery[..mcSQ.Index].TrimEnd();
                    sQ = new Query(INIP, sQuery, sQ.ConnectionSrc);
                }

                MenuItem item = new()
                {
                    Header = Languages.Languages.ma_msg_querymenu_shs01,
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.DarkOrange
                };//make a menuitem instance
                item.Click += (s, eArgs) =>
                {
                    ShowSourceDataset(INIP, sQ, null, true, true);
                };
                cmMenu.Add(item);//add the item to the context menu=
            }

            MenuItem cmSub4 = new()
            {
                Header = Languages.Languages.ma_msg_querymenu_shs02,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };//make a menuitem instance
            foreach (Query.QField qF in sQ.QueryAnalyzer.Fields)
            {
                MenuItem itemSM = new()
                {
                    Header = string.Concat("> ", qF.Alias.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                MenuItem itemSSM = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs03, qF.IsSubQuery ? Languages.Languages.ma_msg_querymenu_shs04 : qF.Raw.Replace("_", "__"), Environment.NewLine,
                                            Languages.Languages.ma_msg_querymenu_shs05, qF.Name.Replace("_", "__"), Environment.NewLine,
                                              qF.Table.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs06 + qF.Table.Replace("_", "__") + Environment.NewLine : "",
                                              qF.TableAlias != qF.Table ? Languages.Languages.ma_msg_querymenu_shs07 + qF.TableAlias.Replace("_", "__") + Environment.NewLine : "",
                                              qF.Transformation.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs08 + qF.Transformation.Replace("_", "__") + Environment.NewLine : "",
                                              qF.Functions.Count > 0 ? Languages.Languages.ma_msg_querymenu_shs09 + string.Join(Environment.NewLine + "\t\t", qF.TransformationDetails).Replace("_", "__") + Environment.NewLine : "",
                                              qF.IsSubQuery ? Languages.Languages.ma_msg_querymenu_shs10 + qF.IsSubQuery.ToString() + Environment.NewLine : "",
                                              Languages.Languages.ma_msg_querymenu_shs11, qF.Index.ToString()),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };

                if (qF.IsSubQuery)
                {
                    List<MenuItem> sSubList = GetMenuItemsFromQuery(INIP, new Query(INIP, qF.Name), true);
                    foreach (MenuItem mI in sSubList)
                    {
                        itemSSM.Items.Add(mI);
                    }
                }

                itemSSM.Click += (s, eArgs) => { Clipboard.SetText(qF.Raw); MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard); };
                itemSM.Items.Add(itemSSM);
                cmSub4.Items.Add(itemSM);//add the item to the context menu
            }
            cmMenu.Add(cmSub4);

            MenuItem cmSub5 = new()
            {
                Header = Languages.Languages.ma_msg_querymenu_shs12,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };//make a menuitem instance
            foreach (Query.QTable qF in sQ.QueryAnalyzer.Tables)
            {
                MenuItem itemSM = new()
                {
                    Header = string.Concat("> ", qF.Alias.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                if ((sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) && (qF.Name.IndexOf("*") > -1 || qF.Name.IndexOf("%") > -1))
                {
                    itemSM.Foreground = System.Windows.Media.Brushes.OrangeRed; itemSM.FontStyle = FontStyles.Italic;
                }
                MenuItem itemSSM = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs13, qF.IsSubQuery ? Languages.Languages.ma_msg_querymenu_shs14 : qF.Name.Replace("_", "__"), Environment.NewLine,
                                            qF.LinkType.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs15 + qF.LinkType + Environment.NewLine : "",
                                            Query.QAnalyzer.GetTablesJoinLinkAsString(qF).Length > 0 ? Languages.Languages.ma_msg_querymenu_shs16 + Query.QAnalyzer.GetTablesJoinLinkAsString(qF) + Environment.NewLine : "",
                                            qF.IsSubQuery ? Languages.Languages.ma_msg_querymenu_shs17 + qF.IsSubQuery.ToString() : ""),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                if ((sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) && (qF.Name.IndexOf("*") > -1 || qF.Name.IndexOf("%") > -1) && !qF.IsSubQuery)
                {
                    itemSSM.Header = itemSSM.Header.ToString().Trim() + Environment.NewLine + Languages.Languages.ma_msg_querymenu_shs18;
                }

                if (qF.IsSubQuery)
                {
                    List<MenuItem> sSubList = GetMenuItemsFromQuery(INIP, new Query(INIP, qF.Name), true);
                    foreach (MenuItem mI in sSubList)
                    {
                        itemSSM.Items.Add(mI);
                    }
                }

                itemSSM.Click += (s, eArgs) => { Clipboard.SetText(qF.Name); MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard); };
                itemSM.Items.Add(itemSSM);
                cmSub5.Items.Add(itemSM);//add the item to the context menu
            }
            cmMenu.Add(cmSub5);

            MenuItem cmSub6 = new()
            {
                Header = Languages.Languages.ma_msg_querymenu_shs19,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };//make a menuitem instance
            foreach (Query.QWhere qF in sQ.QueryAnalyzer.Where)
            {
                MenuItem itemSM = new()
                {
                    Header = string.Concat("> ", qF.Where.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                MenuItem itemSSM = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs20, qF.FieldAlias.Replace("_", "__"), Environment.NewLine,
                                            Languages.Languages.ma_msg_querymenu_shs21, qF.IsAliasField.ToString(), Environment.NewLine,
                                            Languages.Languages.ma_msg_querymenu_shs22, qF.WhereSign, Environment.NewLine,
                                            Languages.Languages.ma_msg_querymenu_shs23, qF.WhereCompare.Replace("_", "__"), Environment.NewLine,
                                            Languages.Languages.ma_msg_querymenu_shs23b, qF.ReversedCondition, Environment.NewLine,
                                            qF.IsSubQuery ? Languages.Languages.ma_msg_querymenu_shs24 + qF.IsSubQuery.ToString() : "", Environment.NewLine,
                                            qF.IsSubQueryComparedTo ? Languages.Languages.ma_msg_querymenu_shs25 + qF.IsSubQueryComparedTo.ToString() : "", Environment.NewLine,
                                            qF.TableField.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs26 + qF.TableField.Replace("_", "__") + Environment.NewLine : "",
                                            qF.TableWhereCompare.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs27 + qF.TableWhereCompare.Replace("_", "__") + Environment.NewLine : "",
                                            qF.SubConditions.Count > 0 ? Languages.Languages.ma_msg_querymenu_shs27b + qF.GetSubConditions().Replace("_", "__") + Environment.NewLine : ""),

                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                itemSSM.Click += (s, eArgs) => { Clipboard.SetText(qF.Where); MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard); };
                itemSM.Items.Add(itemSSM);
                cmSub6.Items.Add(itemSM);//add the item to the context menu
            }
            cmMenu.Add(cmSub6);

            MenuItem cmSub7 = new()
            {
                Header = Languages.Languages.ma_msg_querymenu_shs28,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };//make a menuitem instance
            foreach (Query.QGroupByOrderBy qF in sQ.QueryAnalyzer.GroupBy)
            {
                MenuItem itemSM = new()
                {
                    Header = string.Concat("> ", qF.Field.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                MenuItem itemSSM = new()
                {
                    Header = string.Concat(qF.Alias.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs29 + qF.Alias.Replace("_", "__") + Environment.NewLine : "",
                                           qF.Alias.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs30 + qF.IsAliasField.ToString() + Environment.NewLine : "",
                                           qF.From.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs31 + qF.From.Replace("_", "__") + Environment.NewLine : "",
                                           Languages.Languages.ma_msg_querymenu_shs32, qF.RawField.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                itemSSM.Click += (s, eArgs) => { Clipboard.SetText(qF.RawField); MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard); };
                itemSM.Items.Add(itemSSM);
                cmSub7.Items.Add(itemSM);//add the item to the context menu
            }
            cmMenu.Add(cmSub7);

            MenuItem cmSub8 = new()
            {
                Header = Languages.Languages.ma_msg_querymenu_shs33,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };//make a menuitem instance
            foreach (Query.QGroupByOrderBy qF in sQ.QueryAnalyzer.OrderBy)
            {
                MenuItem itemSM = new()
                {
                    Header = string.Concat("> ", qF.Field.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                MenuItem itemSSM = new()
                {
                    Header = string.Concat(qF.Alias.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs29 + qF.Alias.Replace("_", "__") + Environment.NewLine : "",
                                           qF.AscDesc.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs34 + qF.AscDesc + Environment.NewLine : "",
                                           qF.Alias.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs30 + qF.IsAliasField.ToString() + Environment.NewLine : "",
                                           qF.From.Length > 0 ? Languages.Languages.ma_msg_querymenu_shs31 + qF.From.Replace("_", "__") + Environment.NewLine : "",
                                           Languages.Languages.ma_msg_querymenu_shs32, qF.RawField.Replace("_", "__")),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                itemSSM.Click += (s, eArgs) => { Clipboard.SetText(qF.RawField); MessageBox.Show(Languages.Languages.ma_msg_copiedinclipboard); };
                itemSM.Items.Add(itemSSM);
                cmSub8.Items.Add(itemSM);//add the item to the context menu
            }
            cmMenu.Add(cmSub8);

            if (sQ.QueryAnalyzer.UnionQueries.Count > 0)
            {
                MenuItem cmSub9 = new()
                {
                    Header = Languages.Languages.ma_msg_querymenu_shs35,
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                foreach (string sUnion in sQ.QueryAnalyzer.UnionQueries)
                {
                    MenuItem itemSM = new()
                    {
                        Header = string.Concat("> ", sUnion),
                        FontSize = 9,
                        Foreground = System.Windows.Media.Brushes.DarkBlue
                    };//make a menuitem instance
                    cmSub9.Items.Add(itemSM);//add the item to the context menu
                }
                cmMenu.Add(cmSub9);
            }

            if (!bIsSubQuery)
            {
                MenuItem cmSub11 = new()
                {
                    Header = Languages.Languages.ma_msg_querymenu_shs36,
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkGreen
                };//make a menuitem instance
                MenuItem itemSM1 = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs37, (sQ.QueryAnalyzer.SourceTableToHandle + 1).ToString()),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM1);
                MenuItem itemSM2 = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs38, sQ.QueryAnalyzer.GetOneTableOnlyFromSource.ToString()),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM2);
                MenuItem itemSM3 = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs39, sQ.QueryAnalyzer.IsDistinct.ToString()),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM3);
                MenuItem itemSM4 = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs40, sQ.QueryAnalyzer.LimitedResults > 0 ? sQ.QueryAnalyzer.LimitedResults.ToString() : "No"),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM4);
                MenuItem itemSM4b = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs40b, sQ.QueryAnalyzer.OffSetStart, " / ", sQ.QueryAnalyzer.FetchCount),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM4b);
                MenuItem itemSM5 = new()
                {
                    Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs41, sQ.QueryAnalyzer.SubQueriesCount.ToString()),
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };//make a menuitem instance
                cmSub11.Items.Add(itemSM5);
                cmMenu.Add(cmSub11);
            }

            if (sQ.QueryAnalyzer.Errors.Count > 0)
            {
                MenuItem cmSub10 = new()
                {
                    Header = Languages.Languages.ma_msg_querymenu_shs42,
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.Red
                };//make a menuitem instance
                foreach (Query.QError qE in sQ.QueryAnalyzer.Errors)
                {
                    MenuItem itemSM = new()
                    {
                        Header = string.Concat("> ", qE.ErrorType.ToString() + " : ", qE.ErrorPattern.Replace("_", "__")),
                        FontSize = 9,
                        Foreground = qE.ErrorType.Equals(Query.QError.ErrorLevel.WARNING) ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.DarkRed
                    };//make a menuitem instance
                    MenuItem itemSSM = new()
                    {
                        Header = string.Concat(Languages.Languages.ma_msg_querymenu_shs43, qE.ErrorDescription),
                        FontSize = 9,
                        Foreground = qE.ErrorType.Equals(Query.QError.ErrorLevel.WARNING) ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.DarkRed
                    };//make a menuitem instance
                    itemSM.Items.Add(itemSSM);//add the item to the context menu
                    cmSub10.Items.Add(itemSM);//add the item to the context menu
                }
                cmMenu.Add(cmSub10);
            }

            return cmMenu;
        }

        private void CheckCrossQueryValidity(Job INIP, Query qCrossQuery, Query qMainQuery)
        {
            if (qCrossQuery.CrossQueryForceJoinFields.Count > 0)
            {
                MessageBox.Show(string.Concat(Languages.Languages.ma_msg_querymenu26, string.Join(",", qCrossQuery.CrossQueryForceJoinFields), ")", Environment.NewLine, Languages.Languages.ma_msg_querymenu27));
            }
            else
            {
                MessageBox.Show(string.Concat(Languages.Languages.ma_msg_querymenu28, qCrossQuery.ConnectionSrc.SConnName, Languages.Languages.ma_msg_querymenu29, Environment.NewLine, Languages.Languages.ma_msg_querymenu30));
            }

            try
            {
                Tuple<Job, List<Query>> data = new(INIP, new List<Query> { qMainQuery, qCrossQuery });
                FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.CHECK_CROSSQUERY, data, cancelTokenBGTasks.Token);
            }
            catch (OperationCanceledException)
            { }
        }

        private void AddCrossConnectionJoin(Job INIP, Query sQ)
        {
            //Requêtes multi-bases : utilisation d'un link inter-select : 
            //Select* from table as a [->[25]] select* from fichier as b
            //par détection de la clé primaire sur table a donc déduction de la clé étrangère par association de nom
            //Code : left ->
            //Code : inner <>
            //Ex : [->[25]] ou[<>[25]]

            //step 1 : afficher un écran de sélection des connections
            if ((sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) && !sQ.QueryAnalyzer.Tables[0].IsSubQuery && (sQ.QueryAnalyzer.Tables[0].Name.IndexOf("*") > -1 || sQ.QueryAnalyzer.Tables[0].Name.IndexOf("%") > -1))
            {
                MessageBox.Show(Languages.Languages.ma_msg_crossqueryunable);
            }
            else
            {
                CrossQuery xamlCrossQuery = new(USERNAME, sQ, INIP.ConnectionString_Source, false, "Cross Queries");
                xamlCrossQuery.ShowDialog();
                string sJoin = string.Concat(" ", xamlCrossQuery.OUTPUT_SCRIPT, " ");

                if (xamlCrossQuery.OUTPUT_SCRIPT.Length > 0)
                {
                    StringBuilder sbQ = new();

                    string s = tbQueries.CaretPosition.GetTextInRun(LogicalDirection.Forward);

                    if (!s.Trim().StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase))
                    {
                        TextPointer start = tbQueries.Document.ContentStart;

                        while (start != null && start.CompareTo(tbQueries.Document.ContentEnd) < 0)
                        {
                            //if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                            //{
                            sbQ.Append(start.GetTextInRun(LogicalDirection.Forward));

                            int match_start = sbQ.ToString().RemoveWhitespace().IndexOf(sQ.RawQuery.RemoveWhitespace());
                            if (match_start >= 0 && start.Paragraph != null)
                            {
                                start = start.GetNextContextPosition(LogicalDirection.Forward);
                                start.Paragraph.Inlines.Add(sJoin);
                                break;
                            }
                            //}
                            start = start.GetNextContextPosition(LogicalDirection.Forward);
                        }
                    }
                    else
                    {
                        tbQueries.CaretPosition.InsertTextInRun(sJoin);
                    }
                }
            }
        }

        private string GetHeaderFromQuery(Job INIP, Query sQ, string sTable)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            object[] sData = new object[] { "", new List<string>() };

            try { sData = Task.Run(() => FuzibleController.BuildQuery(INIP, sQ, sTable)).Result; }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //intégration au Qassistant
            if (INTELLISENSE != null && !INTELLISENSE.IsLoading)
            {
                if (INTELLISENSE.TablesInQuery.Count == 0 || !INTELLISENSE.TablesInQuery.Any(t => t.Table.Equals(sTable)))
                {
                    Query.QTable qT = sQ.QueryAnalyzer.Tables.First(q => q.Name.Equals(sTable));
                    INTELLISENSE.TablesInQuery.Add(new IntellisenseData.TablesAndFieldsInQuery(qT.Name, new List<string> { qT.Alias }, (List<string>)sData[1]));
                }
                else
                {
                    foreach (IntellisenseData.TablesAndFieldsInQuery sT in INTELLISENSE.TablesInQuery)
                    {
                        if (sT.Table.Equals(sQ.OutputTable) && sT.Fields.Count == 0)
                        {
                            sT.Fields = (List<string>)sData[1]; break;
                        }
                    }
                }
            }

            Mouse.OverrideCursor = null;

            return (string)sData[0];
        }

        private void AddMultiTargetQuery(Job INIP, Query sQ)
        {
            //step 1 : afficher un écran de sélection des connections

            if (FuzibleController.GetMultiTargetScriptsFromString(sQ.RawQuery, INIP.DynParams).Count <= 2)  //limitation technique à 2 targets / query
            {
                CrossQuery xamlCrossQuery = new(USERNAME, sQ, INIP.ConnectionString_Target, true, "Multi-Target");
                xamlCrossQuery.ShowDialog();
                string sOutput = xamlCrossQuery.OUTPUT_SCRIPT;

                if (sOutput.Length > 0)
                {

                    StringBuilder sbQ = new();
                    TextPointer start = tbQueries.Document.ContentStart;

                    while (start != null && start.CompareTo(tbQueries.Document.ContentEnd) < 0)
                    {
                        //if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                        //{
                        sbQ.Append(start.GetTextInRun(LogicalDirection.Forward));

                        int match_start = sbQ.ToString().IndexOf(sQ.RawOutput + ":SELECT", StringComparison.InvariantCultureIgnoreCase);

                        //on essaye de trouver la requête d'origine pour signaler au curseur quand il doit arrêter de scanner le texte
                        //match_start = Regex.Match(sbQ.ToString(), sQ.RawOutput + ":SELECT", RegexOptions.IgnoreCase).Index;

                        if (match_start >= 0 && start.Paragraph != null)
                        {
                            //start = start.GetNextContextPosition(LogicalDirection.Forward);
                            sOutput = string.Concat(sOutput, ":");
                            start.Paragraph.Inlines.Add(sOutput);
                            Inline iN = start.Paragraph.Inlines.LastInline;
                            start.Paragraph.Inlines.Remove(start.Paragraph.Inlines.LastInline);
                            start.Paragraph.Inlines.Remove(start.Paragraph.Inlines.FirstInline);
                            start.Paragraph.Inlines.InsertBefore(start.Paragraph.Inlines.FirstInline, iN);

                            break;
                        }
                        //}
                        start = start.GetNextContextPosition(LogicalDirection.Forward);
                    }
                }
            }
            else
            {
                MessageBox.Show(Languages.Languages.ma_msg_multitargetlimit2);
            }
        }

        private void InsertTextInQueriesRTB(Query sQ, string sWordToAdd, string sInsertAfter)
        {
            StringBuilder sbQ = new();
            TextPointer start = tbQueries.Document.ContentStart;

            while (start != null && start.CompareTo(tbQueries.Document.ContentEnd) < 0)
            {
                sbQ.Append(start.GetTextInRun(LogicalDirection.Forward));
                string sFrom = sQ.RawQuery[..(sQ.RawQuery.IndexOf(sInsertAfter) + sInsertAfter.Length)];
                int match_start = sbQ.ToString().RemoveWhitespace().IndexOf(sFrom.RemoveWhitespace());
                if (match_start >= 0 && start.Paragraph != null)
                {
                    start = start.GetNextContextPosition(LogicalDirection.Forward);
                    for (int iI = 0; iI < start.Paragraph.Inlines.Count; iI++)
                    {
                        if (start.Paragraph.Inlines.ElementAt(iI).ContentStart.GetTextInRun(LogicalDirection.Forward).Equals(sQ.QueryAnalyzer.Tables[0].Name))
                        {
                            start.Paragraph.Inlines.InsertAfter(start.Paragraph.Inlines.ElementAt(iI), new Run(sWordToAdd));
                            break;
                        }
                    }
                    break;
                }
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        private void AddPasswordToMBQuery(Query sQ)
        {
            FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputaddpwdtomailquery01 + sQ.QueryAnalyzer.Tables[0].Name + "\" :", Languages.Languages.ma_msg_inputaddpwdtomailquery02, true, true);
            fP.ShowDialog();
            string sPassword = fP.PromptUserData.Trim();

            if (sPassword.Length > 0)
            {
                sPassword = string.Concat("[", sPassword, "]");
                InsertTextInQueriesRTB(sQ, sPassword, sQ.QueryAnalyzer.Tables[0].Name);
            }
        }

        private void AddBodyToWSQuery(Job INIP, Query sQ)
        {
            string sBody = "";
            switch (INIP.WebServiceRequestBodyType)
            {
                case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.FORM_DATA:
                    FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputaddwsformdatatoquery01, Languages.Languages.ma_msg_inputaddwsformdatatoquery02, false, true);
                    fP.ShowDialog();
                    sBody = fP.PromptUserData.Trim();
                    break;
                case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.RAW_JSON:
                    FuzibleUITools.Prompt fP2 = new(Languages.Languages.ma_msg_inputaddwsjsontoquery01, Languages.Languages.ma_msg_inputaddwsjsontoquery02, false, true);
                    fP2.ShowDialog();
                    sBody = fP2.PromptUserData.Trim();
                    break;
                case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.RAW_XML:
                    FuzibleUITools.Prompt fP3 = new(Languages.Languages.ma_msg_inputaddwsxmltoquery01, Languages.Languages.ma_msg_inputaddwsxmltoquery02, false, true);
                    fP3.ShowDialog();
                    sBody = fP3.PromptUserData.Trim();
                    break;
            }

            if (sBody.Length > 0)
            {
                sBody = string.Concat("[", sBody, "]");
                InsertTextInQueriesRTB(sQ, sBody, sQ.QueryAnalyzer.Tables[0].Name);
            }
        }

        private void QueryBuilder(Job INIP, bool bIsFromScratch)
        {
            string sTableToQuery = "";
            string sOutputTable = "";
            string sQuery = "SELECT ";
            string sFToQuery;
            string[] sFieldsToQuery;

            if (INIP.ConnectionString_Source != null && INIP.ConnectionString_Target != null)
            {

                switch (INIP.ConnectionString_Source.SConnDriver.ToString()[..2])
                {
                    case "DB":
                        FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputcreatequery01, Languages.Languages.ma_msg_inputcreatequery02, false, true);
                        fP.ShowDialog();
                        sTableToQuery = fP.PromptUserData;
                        break;
                    case "NS":
                        FuzibleUITools.Prompt fPb = new(Languages.Languages.ma_msg_inputcreatequery03, Languages.Languages.ma_msg_inputcreatequery04, false, true);
                        fPb.ShowDialog();
                        sTableToQuery = fPb.PromptUserData;
                        break;
                    case "FI":
                        FuzibleUITools.Prompt fP2 = new(Languages.Languages.ma_msg_inputcreatequery05, Languages.Languages.ma_msg_inputcreatequery06, false, true);
                        fP2.ShowDialog();
                        sTableToQuery = fP2.PromptUserData;
                        string sPathSource = INIP.ConnectionString_Source.SConnString(INIP.DynParams);
                        if (File.Exists(sPathSource + "\\" + sTableToQuery))
                        {
                            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery07, Languages.Languages.ma_msg_inputcreatequery07, MessageBoxButton.YesNo);

                            if (msgR.ToString().ToUpper().Equals("YES"))
                            {
                                sQuery = string.Concat(sQuery, "*", " FROM ", sTableToQuery);
                            }
                            else
                            {
                                FuzibleUITools.Prompt fP3 = new(Languages.Languages.ma_msg_inputcreatequery08, Languages.Languages.ma_msg_inputcreatequery09, false, true);
                                fP3.ShowDialog();
                                sFToQuery = fP3.PromptUserData;
                                sFieldsToQuery = sFToQuery.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                                sQuery = string.Concat(sQuery, string.Join(",", sFieldsToQuery), " FROM ", sTableToQuery);
                            }
                        }
                        else
                        {
                            sTableToQuery = ""; MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery10);
                        }
                        break;
                    case "WS":
                        FuzibleUITools.Prompt fP4 = new(Languages.Languages.ma_msg_inputcreatequery11, Languages.Languages.ma_msg_inputcreatequery12, false, true);
                        fP4.ShowDialog();
                        sTableToQuery = fP4.PromptUserData;
                        MessageBoxResult msgRc = MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery07, Languages.Languages.ma_msg_inputcreatequery07, MessageBoxButton.YesNo);

                        if (msgRc.ToString().ToUpper().Equals("YES"))
                        {
                            sQuery = string.Concat(sQuery, "*", " FROM ", sTableToQuery);
                        }
                        else
                        {
                            FuzibleUITools.Prompt fP5 = new(Languages.Languages.ma_msg_inputcreatequery08, Languages.Languages.ma_msg_inputcreatequery09, false, true);
                            fP5.ShowDialog();
                            sFToQuery = fP5.PromptUserData;
                            sFieldsToQuery = sFToQuery.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                            sQuery = string.Concat(sQuery, string.Join(",", sFieldsToQuery), " FROM ", sTableToQuery);
                        }
                        break;
                    case "MB":
                        FuzibleUITools.Prompt fP6 = new(Languages.Languages.ma_msg_inputcreatequery13, Languages.Languages.ma_msg_inputcreatequery14, false, true);
                        fP6.ShowDialog();
                        sTableToQuery = fP6.PromptUserData;
                        MessageBoxResult msgRb = MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery07, Languages.Languages.ma_msg_inputcreatequery07, MessageBoxButton.YesNo);

                        if (msgRb.ToString().ToUpper().Equals("YES"))
                        {
                            sQuery = string.Concat(sQuery, string.Join(",", MailTools.MAIL_FIELDS.ToArray()), " FROM ", sTableToQuery);
                        }
                        else
                        {
                            FuzibleUITools.Prompt fP7 = new(Languages.Languages.ma_msg_inputcreatequery08, Languages.Languages.ma_msg_inputcreatequery09, false, true);
                            fP7.ShowDialog();
                            sFToQuery = fP7.PromptUserData;
                            sFieldsToQuery = sFToQuery.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string sF in sFieldsToQuery)
                            {
                                if (MailTools.MAIL_FIELDS.Contains(sF.Trim().ToUpper())) { sQuery = string.Concat(sQuery, sF.Trim().ToUpper(), ","); }
                            }
                            sQuery = sQuery[0..^1];
                            sQuery = string.Concat(sQuery, " FROM ", sTableToQuery);
                        }
                        break;
                    case "AD":
                        FuzibleUITools.Prompt fP8 = new(Languages.Languages.ma_msg_inputcreatequery15, Languages.Languages.ma_msg_inputcreatequery16, false, true);
                        fP8.ShowDialog();
                        sTableToQuery = fP8.PromptUserData;
                        MessageBoxResult msgRb2 = MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery07, Languages.Languages.ma_msg_inputcreatequery07, MessageBoxButton.YesNo);
                        if (msgRb2.ToString().ToUpper().Equals("YES"))
                        {
                            sQuery = string.Concat(sQuery, string.Join(",", MailTools.MAIL_FIELDS.ToArray()), " FROM ", sTableToQuery);
                        }
                        else
                        {
                            FuzibleUITools.Prompt fP9 = new(Languages.Languages.ma_msg_inputcreatequery08, Languages.Languages.ma_msg_inputcreatequery09, false, true);
                            fP9.ShowDialog();
                            sFToQuery = fP9.PromptUserData;
                            sFieldsToQuery = sFToQuery.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string sF in sFieldsToQuery)
                            {
                                sQuery = string.Concat(sQuery, sF.Trim().ToUpper(), ",");
                            }
                            sQuery = sQuery[0..^1];
                            sQuery = string.Concat(sQuery, " FROM ", sTableToQuery);
                        }
                        break;
                }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery17); }

            if (sTableToQuery.Length > 0)
            {
                switch (INIP.ConnectionString_Target.SConnDriver.ToString()[..2])
                {
                    case "DB":
                        FuzibleUITools.Prompt fP10 = new(Languages.Languages.ma_msg_inputcreatequery18, Languages.Languages.ma_msg_inputcreatequery19, false, true);
                        fP10.ShowDialog();
                        sOutputTable = fP10.PromptUserData;
                        break;
                    case "NS":
                        FuzibleUITools.Prompt fP10b = new(Languages.Languages.ma_msg_inputcreatequery20, Languages.Languages.ma_msg_inputcreatequery21, false, true);
                        fP10b.ShowDialog();
                        sOutputTable = fP10b.PromptUserData;
                        break;
                    case "FI":
                        FuzibleUITools.Prompt fP11 = new(Languages.Languages.ma_msg_inputcreatequery22, Languages.Languages.ma_msg_inputcreatequery23, false, true);
                        fP11.ShowDialog();
                        sOutputTable = fP11.PromptUserData;
                        break;
                    case "WS":
                        FuzibleUITools.Prompt fP12 = new(Languages.Languages.ma_msg_inputcreatequery24, Languages.Languages.ma_msg_inputcreatequery25, false, true);
                        fP12.ShowDialog();
                        sOutputTable = fP12.PromptUserData;
                        break;
                    case "MB":
                        FuzibleUITools.Prompt fP13 = new(Languages.Languages.ma_msg_inputcreatequery26, Languages.Languages.ma_msg_inputcreatequery26, false, true);
                        fP13.ShowDialog();
                        sOutputTable = fP13.PromptUserData;
                        break;
                }

                if (sOutputTable.Length > 0)
                {
                    sQuery = string.Concat(sOutputTable, ":", sQuery);

                    if (sQuery.Length > 16 && sQuery.IndexOf("SELECT", StringComparison.InvariantCultureIgnoreCase) > 0 && sQuery.IndexOf("FROM", StringComparison.InvariantCultureIgnoreCase) > 0)
                    {
                        MessageBoxResult msgRd = MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery27, Languages.Languages.ma_msg_inputcreatequery28, MessageBoxButton.YesNo);

                        if (msgRd.ToString().ToUpper().Equals("YES"))
                        {
                            Query Q = new(INIP, sQuery);
                            ShowSourceDataset(INIP, Q, null, false, true);
                        }

                        //bool bOK = SaveConfigINIFile(INIP.JobNAME, INIP.JobID, INIP.JobPassword);
                        //if (!bOK) { MessageBox.Show("Unable to save Job."); }

                        if (bIsFromScratch)
                        {
                            RichTB.SetTextRTB(tbQueries, string.Concat(RichTB.GetTextRTB(tbQueries), Environment.NewLine, sQuery));
                        }
                        else
                        {
                            tbQueries.CaretPosition.InsertLineBreak(); tbQueries.CaretPosition.InsertTextInRun(sQuery); tbQueries.CaretPosition.InsertLineBreak();
                        }
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_inputcreatequery29);
                    }
                }
            }
        }

        private void LoadQueryMenu(ContextMenu cm, Query sQ, Job JobSandbox)
        {
            cm.Items.Clear();
            cm.KeyDown += (s, eArgs) => { if (eArgs.Key == Key.F3) { Program.LiveHelp.ChangeLockedStatus(); } };

            try
            {
                if (JobSandbox.ConnectionString_Source != null && JobSandbox.ConnectionString_Target != null)
                {
                    if (sQ != null)
                    {
                        string sHeader = sQ.ConnectionTrg.SConnName.Replace("_", "__");
                        if (sQ.HasMultiTarget)
                        {
                            Query Q = sQ.CreateSwapedTargetQuery();
                            sHeader = string.Concat(sHeader, " [A] + ", Q.ConnectionTrg.SConnName.Replace("_", "__"), " [B]");
                        }

                        MenuItem item3 = new()
                        {
                            Name = "miQueryOutput",
                            Header = string.Concat(sHeader, " -> ", sQ.OutputTable.Replace("_", "__")),
                            Foreground = System.Windows.Media.Brushes.Black,
                            Background = System.Windows.Media.Brushes.DimGray,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold
                        };//make a menuitem instance
                        item3.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(item3);//add the item to the context menu

                        MenuItem itemQA = new()
                        {
                            Name = "miQueryAnalyzer",
                            Header = Languages.Languages.ma_msg_querymenu01,
                            Foreground = System.Windows.Media.Brushes.DarkRed,
                            FontSize = 12,
                            FontWeight = FontWeights.Light
                        };//make a menuitem instance
                        itemQA.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(itemQA);//add the item to the context menu                       

                        MenuItem itemS = new()
                        {
                            Name = "miQuerySourceInfo",
                            Header = Languages.Languages.ma_msg_querymenu02,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        itemS.MouseMove += UIElement_MouseDown;

                        MenuItem itemS1 = new()
                        {
                            Header = LoadQuerySourceInfos(JobSandbox, sQ, true).Replace("_", "__"),
                            FontSize = 9,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        itemS.Items.Add(itemS1);//add the item to the context menu

                        cm.Items.Add(itemS);//add the item to the context menu

                        if ((sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) && !sQ.QueryAnalyzer.Tables[0].IsSubQuery && (sQ.QueryAnalyzer.Tables[0].Name.IndexOf("*") > -1 || sQ.QueryAnalyzer.Tables[0].Name.IndexOf("%") > -1))
                        {
                            MenuItem lbl = new()
                            {
                                Header = sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") ? Languages.Languages.ma_msg_querymenu03 : Languages.Languages.ma_msg_querymenu04,
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.OrangeRed
                            };//make a menuitem instance
                            lbl.Click += (s, eArgs) => { MessageBox.Show(Languages.Languages.ma_msg_querymenu05); };
                            cm.Items.Add(lbl);//add the item to the context menu
                        }

                        if (sQ.QueryAnalyzer.SourceTableToHandle > 0)
                        {
                            MenuItem lbl = new()
                            {
                                Header = Languages.Languages.ma_msg_querymenu06 + (sQ.QueryAnalyzer.SourceTableToHandle + 1).ToString() + Languages.Languages.ma_msg_querymenu07,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.OrangeRed
                            };//make a menuitem instance
                            lbl.Click += (s, eArgs) => { MessageBox.Show(Languages.Languages.ma_msg_querymenu08 + (sQ.QueryAnalyzer.SourceTableToHandle + 1).ToString()); };
                            cm.Items.Add(lbl);//add the item to the context menu
                        }

                        MenuItem itemT = new()
                        {
                            Name = "miQueryTargetInfo",
                            Header = Languages.Languages.ma_msg_querymenu09,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        itemT.MouseMove += UIElement_MouseDown;

                        MenuItem itemT1 = new()
                        {
                            Header = LoadQueryTargetInfos(JobSandbox, sQ, true).Replace("_", "__"),
                            FontSize = 9,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        itemT.Items.Add(itemT1);//add the item to the context menu

                        cm.Items.Add(itemT);//add the item to the context menu

                        if (sQ.ConnectionTrg.SConnDriverSuffix.Equals("FI"))
                        {
                            if ((sQ.QueryAnalyzer.OutputTable.IndexOf("[") > -1 && sQ.QueryAnalyzer.OutputTable.IndexOf("]") > -1) || (sQ.QueryAnalyzer.OutputTable.IndexOf("{") > -1 && sQ.QueryAnalyzer.OutputTable.IndexOf("}") > -1))
                            {
                                MatchCollection mcOutA = Regex.Matches(sQ.QueryAnalyzer.OutputTable, SHSRegex.REGEX_FILE_OUTPUT_PATTERN);
                                MatchCollection mcOutB = Regex.Matches(sQ.QueryAnalyzer.OutputTable, "{[\\d\\w\\s_%()\\-'\"|&+=!?]+}");
                                string sPatterns = "";
                                if (mcOutA.Count > 0 || mcOutB.Count > 0)
                                {
                                    foreach (Match mc in mcOutA) { sPatterns = string.Concat(sPatterns, mc.Value); }
                                    foreach (Match mc in mcOutB) { sPatterns = string.Concat(sPatterns, mc.Value); }
                                    MenuItem lbl = new()
                                    {
                                        Header = Languages.Languages.ma_msg_querymenu10 + sPatterns.Replace("_", "__") + Languages.Languages.ma_msg_querymenu11,
                                        FontSize = 9,
                                        Foreground = System.Windows.Media.Brushes.OrangeRed
                                    };//make a menuitem instance
                                    lbl.Click += (s, eArgs) => { MessageBox.Show(Languages.Languages.ma_msg_querymenu12); };
                                    cm.Items.Add(lbl);//add the item to the context menu
                                }
                            }
                        }

                        if ((sQ.ConnectionSrc.SConnDriverSuffix.Equals("NS") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") || sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) && !sQ.QueryAnalyzer.Tables[0].IsSubQuery)
                        {
                            if (sQ.QueryAnalyzer.Tables[0].Name.IndexOf("*") > -1 || sQ.QueryAnalyzer.Tables[0].Name.IndexOf("%") > -1)
                            {
                                //contrôle de script de l'output pour avertir si aucun pattern
                                if (!sQ.OutputTable.Contains('*', StringComparison.CurrentCulture))
                                {
                                    MenuItem lbl = new()
                                    {
                                        Header = sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") ? Languages.Languages.ma_msg_querymenu13 : Languages.Languages.ma_msg_querymenu14,
                                        FontSize = 9,
                                        Foreground = System.Windows.Media.Brushes.Red
                                    };//make a menuitem instance
                                    lbl.Click += (s, eArgs) => { MessageBox.Show(Languages.Languages.ma_msg_querymenu15); };
                                    cm.Items.Add(lbl);//add the item to the context menu
                                }
                                else
                                {
                                    MenuItem lbl = new()
                                    {
                                        Header = sQ.ConnectionSrc.SConnDriverSuffix.Equals("FI") ? Languages.Languages.ma_msg_querymenu16 : Languages.Languages.ma_msg_querymenu17,
                                        FontSize = 9,
                                        Foreground = System.Windows.Media.Brushes.OrangeRed
                                    };//make a menuitem instance
                                    lbl.Click += (s, eArgs) => { MessageBox.Show(Languages.Languages.ma_msg_querymenu18); };
                                    cm.Items.Add(lbl);//add the item to the context menu
                                }
                            }
                        }

                        if (sQ.HasMultiTarget)
                        {
                            Query Q = sQ.CreateSwapedTargetQuery();
                            if (Q != null)
                            {
                                MenuItem itemT2 = new()
                                {
                                    Name = "miQueryMultiTarget",
                                    Header = Languages.Languages.ma_msg_querymenu19,
                                    FontSize = 10,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue
                                };//make a menuitem instance
                                itemT2.MouseMove += UIElement_MouseDown;

                                MenuItem itemT2b = new()
                                {
                                    Header = LoadQueryTargetInfos(JobSandbox, Q, false).Replace("_", "__"),
                                    FontSize = 9,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue
                                };//make a menuitem instance
                                itemT2.Items.Add(itemT2b);//add the item to the context menu

                                cm.Items.Add(itemT2);//add the item to the context menu
                            }
                        }

                        MenuItem item1 = new()
                        {
                            Name = "miQueryDetails",
                            Header = Languages.Languages.ma_msg_querymenu21,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        item1.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(item1);//add the item to the context menu

                        List<MenuItem> mListQuery = GetMenuItemsFromQuery(JobSandbox, sQ, false);

                        foreach (MenuItem mI in mListQuery)
                        {
                            item1.Items.Add(mI);
                        }

                        foreach (Query.QTable qT in sQ.QueryAnalyzer.Tables)
                        {
                            //recherche de subqueries
                            if (Regex.IsMatch(qT.Name, "\\(\\s*SELECT\\s+", RegexOptions.IgnoreCase) && qT.Name.EndsWith(")"))
                            {
                                string sSq = qT.Name[1..];
                                sSq = sSq[0..^1];
                                Query SubQuery = new(JobSandbox, string.Concat("SUBQUERY_", qT.Alias, ":", sSq));
                                MenuItem mItem = new()
                                {
                                    Name = "miQuerySubQueries",
                                    Header = Languages.Languages.ma_msg_querymenu22 + qT.Alias,
                                    FontSize = 9,
                                    Foreground = System.Windows.Media.Brushes.RoyalBlue
                                };//make a menuitem instance
                                mItem.MouseMove += UIElement_MouseDown;

                                cm.Items.Add(mItem);//add the item to the context menu
                                List<MenuItem> mIs = new();
                                mIs = GetMenuItemsFromQuery(JobSandbox, SubQuery, true);
                                foreach (MenuItem mI in mIs)
                                {
                                    mItem.Items.Add(mI);
                                }
                            }
                        }

                        for (int iU = 0; iU < sQ.QueryAnalyzer.UnionQueries.Count; iU++)
                        {
                            MenuItem mItem = new()
                            {
                                Name = "miQueryUnionQueries",
                                Header = Languages.Languages.ma_msg_querymenu23 + (iU + 1).ToString(),
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.RoyalBlue
                            };//make a menuitem instance
                            mItem.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(mItem);//add the item to the context menu
                            List<MenuItem> mIs = new();
                            Query sQUnion = new(JobSandbox, sQ.QueryAnalyzer.UnionQueries[iU]);
                            mIs = GetMenuItemsFromQuery(JobSandbox, sQUnion, true);
                            foreach (MenuItem mI in mIs)
                            {
                                mItem.Items.Add(mI);
                            }
                        }

                        foreach (Query qCJ in sQ.CrossJoinQueries)
                        {
                            MenuItem mItem = new()
                            {
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.RoyalBlue
                            };//make a menuitem instance
                            cm.Items.Add(mItem);//add the item to the context menu

                            //infos cross-query
                            MenuItem mItemCQa = new()
                            {
                                Name = "miQueryCrossQBehavior",
                                Header = Languages.Languages.ma_msg_querymenu24,
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkRed
                            };
                            mItemCQa.MouseMove += UIElement_MouseDown;

                            mItem.Items.Add(mItemCQa);

                            //infos cross-query
                            MenuItem mItemCQc = new()
                            {
                                Name = "miQueryCrossQLink",
                                Header = Languages.Languages.ma_msg_querymenu25,
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkCyan
                            };
                            mItemCQc.Click += (s, eArgs) =>
                            {
                                CheckCrossQueryValidity(JobSandbox, qCJ, sQ);
                            };
                            mItemCQc.MouseMove += UIElement_MouseDown;

                            mItem.Items.Add(mItemCQc);

                            MenuItem mItemCQb = new()
                            {
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkGreen
                            };

                            List<MenuItem> mIs = new();

                            mItem.Header = Languages.Languages.ma_msg_querymenu31 + qCJ.ConnectionSrc.SConnName + ")".ToString();
                            mIs = GetMenuItemsFromQuery(JobSandbox, qCJ, true);
                            mItemCQb.Header = LoadCrossQueryInfos(qCJ, sQ).Replace("_", "__");

                            mItemCQa.Items.Add(mItemCQb); //ajout infos cross-query
                            foreach (MenuItem mI in mIs)
                            {
                                mItem.Items.Add(mI);
                            }
                        }

                        if (sQ.ConnectionTrg.SConnDriverSuffix.Equals("DB")) //TODO :si y'a un double target, c'est pourri
                        {
                            MenuItem item9f = new()
                            {
                                Name = "miQueryMapping",
                                Header = Languages.Languages.ma_msg_querymenu35b,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkOrange
                            };//make a menuitem instance
                            item9f.Click += (s, eArgs) =>
                            {
                                MessageBox.Show(Languages.Languages.ma_msg_dsmappinginfo);

                                JobSandbox.JobQueries = new List<Query> { sQ };

                                bool bCanRun = FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.GET_COLUMN_MAPPING, JobSandbox, cancelTokenBGTasks.Token);
                                if (!bCanRun) { MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning); }
                            };
                            item9f.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9f);//add the item to the context menu
                        }

                        Separator sp1 = new()
                        {
                            Background = System.Windows.Media.Brushes.Black
                        };
                        cm.Items.Add(sp1);

                        if (JobSandbox.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE || JobSandbox.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM)
                        {
                            MenuItem item9 = new()
                            {
                                Name = "miQueryPartialDelete",
                                Header = Languages.Languages.ma_msg_querymenu32,
                                Foreground = System.Windows.Media.Brushes.DarkRed,
                                FontSize = 12,
                                FontWeight = FontWeights.Light
                            };//make a menuitem instance
                            item9.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9);//add the item to the context menu

                            MenuItem item9a = new()
                            {
                                Name = "miQuerySynchroTransco",
                                Header = Languages.Languages.ma_msg_querymenu33,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9a.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9a);//add the item to the context menu

                            string sPartialDelete = string.Concat("DELETE FROM ", sQ.OutputTable, Environment.NewLine);
                            if (JobSandbox.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE)
                            {
                                sPartialDelete = string.Concat(sPartialDelete, Toolbox.CleanSQLConditionsForTarget_WithSourceQuery(sQ.ConnectionTrg.SqlEchappementChar, sQ, null).Trim());
                            }
                            else if (JobSandbox.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM)
                            {
                                sPartialDelete = string.Concat(sPartialDelete, Toolbox.CleanSQLConditionsForTarget_WithDynamicParams(sQ.ConnectionTrg.SqlEchappementChar, JobSandbox.OptionalDynamicParamField_OnInsert, JobSandbox.DynParams).Trim());
                            }
                            else if (JobSandbox.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME)
                            {
                                string sValue = sQ.ConnectionSrc.SConnDriverSuffix switch
                                {
                                    "DB" => JobSandbox.DatabaseName_Source,
                                    "FI" => sQ.QueryAnalyzer.Tables[0].Name,
                                    "MB" => sQ.QueryAnalyzer.Tables[0].Name,
                                    "AD" => sQ.QueryAnalyzer.Tables[0].Name,
                                    "WS" => sQ.QueryAnalyzer.Tables[0].Name,
                                    _ => "?",
                                };
                                sPartialDelete = string.Concat(sPartialDelete, Toolbox.CleanSQLConditionsForTarget_WithDbName(sQ.ConnectionTrg.SqlEchappementChar, JobSandbox.TargetAddDbName, sValue)).Trim();
                            }

                            MenuItem item9c = new()
                            {
                                Header = sPartialDelete.Replace("_", "__"),
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9a.Items.Add(item9c);//add the item to the context menu

                            Separator sp5 = new()
                            {
                                Background = System.Windows.Media.Brushes.Black
                            };
                            cm.Items.Add(sp5);
                        }

                        if (JobSandbox.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                        {
                            MenuItem item9 = new()
                            {
                                Name = "miQuerySynchroQuery",
                                Header = Languages.Languages.ma_msg_querymenu34,
                                Foreground = System.Windows.Media.Brushes.DarkRed,
                                FontSize = 12,
                                FontWeight = FontWeights.Light
                            };//make a menuitem instance
                            item9.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9);//add the item to the context menu

                            MenuItem item9a = new()
                            {
                                Name = "miQuerySynchroTranscoTarget",
                                Header = Languages.Languages.ma_msg_querymenu35,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9a.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9a);//add the item to the context menu

                            string sSyncQuery = string.Concat(Languages.Languages.ma_msg_querymenu36, Environment.NewLine, sQ.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(false, JobSandbox.SynchroBypassQueryFiltersInTarget, true));
                            sSyncQuery = sQ.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sSyncQuery, JobSandbox, sQ, null);

                            if (sSyncQuery.IndexOf(" FROM ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQuery = sSyncQuery.Insert(sSyncQuery.IndexOf(" FROM ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }
                            if (sSyncQuery.IndexOf(" WHERE ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQuery = sSyncQuery.Insert(sSyncQuery.IndexOf(" WHERE ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }
                            //if (sSyncQuery.IndexOf(" AND ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQuery = sSyncQuery.Insert(sSyncQuery.IndexOf(" AND ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }

                            MenuItem item9c = new()
                            {
                                Header = sSyncQuery.Replace("_", "__"),
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9a.Items.Add(item9c);//add the item to the context menu

                            string sSyncQueryB = sQ.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(true, JobSandbox.SynchroBypassQueryFiltersInTarget, true);
                            sSyncQueryB = sQ.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sSyncQueryB, JobSandbox, sQ, null);

                            Separator spx = new();
                            item9a.Items.Add(spx);

                            if (sSyncQueryB.Length > 0)
                            {
                                sSyncQueryB = string.Concat(Languages.Languages.ma_msg_querymenu37, Environment.NewLine, sSyncQueryB);
                                if (sSyncQueryB.IndexOf(" FROM ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQueryB = sSyncQueryB.Insert(sSyncQueryB.IndexOf(" FROM ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }
                                if (sSyncQueryB.IndexOf(" WHERE ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQueryB = sSyncQueryB.Insert(sSyncQueryB.IndexOf(" WHERE ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }
                                if (sSyncQueryB.IndexOf(" AND ", StringComparison.InvariantCultureIgnoreCase) > 0) { sSyncQueryB = sSyncQueryB.Insert(sSyncQueryB.IndexOf(" AND ", StringComparison.InvariantCultureIgnoreCase) + 1, Environment.NewLine); }
                            }
                            else
                            {
                                sSyncQueryB = string.Concat(Languages.Languages.ma_msg_querymenu38, Environment.NewLine, Languages.Languages.ma_msg_querymenu39);
                            }

                            MenuItem item9e = new()
                            {
                                Header = sSyncQueryB.Replace("_", "__"),
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9a.Items.Add(item9e);//add the item to the context menu

                            MenuItem item9b = new()
                            {
                                Name = "miQuerySynchroValidity",
                                Header = Languages.Languages.ma_msg_querymenu40,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item9b.Click += (s, eArgs) =>
                            {
                                Tuple<Job, Query, bool> data = new(JobSandbox, sQ, false);
                                FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.CHECK_SYNCHRO_VALIDITY, data, cancelTokenBGTasks.Token);
                            };
                            item9b.MouseMove += UIElement_MouseDown;

                            cm.Items.Add(item9b);//add the item to the context menu

                            if (sQ.HasMultiTarget)
                            {
                                MenuItem item9d = new()
                                {
                                    Header = Languages.Languages.ma_msg_querymenu41,
                                    FontSize = 10,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue
                                };//make a menuitem instance
                                item9d.Click += (s, eArgs) =>
                                {
                                    Tuple<Job, Query, bool> data = new(JobSandbox, sQ, true);
                                    FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.CHECK_SYNCHRO_VALIDITY, data, cancelTokenBGTasks.Token);
                                };
                                cm.Items.Add(item9d);//add the item to the context menu
                            }

                            Separator sp5 = new()
                            {
                                Background = System.Windows.Media.Brushes.Black
                            };
                            cm.Items.Add(sp5);
                        }

                        MenuItem itemEQ = new()
                        {
                            Name = "miQueryExecute",
                            Header = Languages.Languages.ma_msg_querymenu42,
                            Foreground = System.Windows.Media.Brushes.DarkRed,
                            FontSize = 12,
                            FontWeight = FontWeights.Light
                        };//make a menuitem instance
                        itemEQ.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(itemEQ);//add the item to the context menu

                        //show source data
                        MenuItem item = new()
                        {
                            Name = "miQuerySourceData",
                            Header = Languages.Languages.ma_msg_querymenu20,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkOrange
                        };//make a menuitem instance
                        item.Click += (s, eArgs) =>
                        {
                            ShowSourceDataset(JobSandbox, sQ, null, false, true);
                        };
                        item.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(item);//add the item to the context menu

                        //run single query
                        MenuItem item6 = new()
                        {
                            Name = "miQuerySingleQ",
                            Header = Languages.Languages.ma_msg_querymenu43,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        item6.Click += (s, eArgs) =>
                        {
                            if (CheckFieldsForErrors())
                            {
                                if (JobSandbox.ConnectionString_Source != null && JobSandbox.ConnectionString_Target != null)
                                {
                                    //RunSingleQueryFromTab(JobSandbox, sQ, ckSimulationMode.IsChecked.Value); 
                                    JobSandbox.RunInSimulationMode = ckSimulationMode.IsChecked.Value;
                                    JobSandbox.JobQueries = new List<Query> { sQ };

                                    bool bCanRun = FuzibleController.CreateBackgroundTask(USERNAME, BackgroundTask.TaskType.EXECUTE_SINGLE_QUERY, JobSandbox, cancelTokenBGTasks.Token);
                                    if (!bCanRun)
                                    { MessageBox.Show(Languages.Languages.ma_msg_jobalreadyrunning); }
                                }
                                else { MessageBox.Show(Languages.Languages.ma_msg_startjobconfigureconn); }
                            }
                            else { MessageBox.Show(Languages.Languages.ma_msg_singlequeryinputerror); }

                        };
                        item6.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(item6);//add the item to the context menu

                        Separator sp2 = new()
                        {
                            Background = System.Windows.Media.Brushes.Black
                        };
                        cm.Items.Add(sp2);

                        MenuItem itemQS = new()
                        {
                            Name = "miQueryScripting",
                            Header = Languages.Languages.ma_msg_querymenu44,
                            Foreground = System.Windows.Media.Brushes.DarkRed,
                            FontSize = 12,
                            FontWeight = FontWeights.Light
                        };//make a menuitem instance
                        itemQS.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(itemQS);//add the item to the context menu

                        MenuItem cmSub3 = new()
                        {
                            Name = "miQueryGetHeader",
                            Header = Languages.Languages.ma_msg_querymenu45,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        cmSub3.MouseMove += UIElement_MouseDown;

                        string sFields = "";
                        foreach (Query.QTable qTable in sQ.QueryAnalyzer.Tables)
                        {
                            MenuItem itemSM = new()
                            {
                                Header = qTable.Alias.Replace("_", "__"),
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue,
                                Background = System.Windows.Media.Brushes.White
                            };//make a menuitem instance
                            itemSM.Click += (s, eArgs) =>
                            {
                                sFields = string.Concat(sFields, GetHeaderFromQuery(JobSandbox, sQ, qTable.Name));
                                if (sFields.Length > 0)
                                {
                                    if (sFields.EndsWith(",")) { sFields = sFields[0..^1]; }

                                    MessageBox.Show(string.Concat(Languages.Languages.ma_msg_querymenu46, sFields, ")"));
                                    Clipboard.SetText(sFields);
                                    //ajout des données dans l'intellisense
                                    if (!INTELLISENSE.IsLoading && INTELLISENSE.Connection != null)
                                    {
                                        string sTable = Regex.Replace(qTable.Name, @"([\|\{\}\$\?\^\!\[\]\(\)])", "\\$1");
                                        foreach (IntellisenseData.TablesAndFieldsInQuery sT in INTELLISENSE.TablesInQuery)
                                        {
                                            if (sT.Fields.Count == 1 && sT.NoDynamicFields) { sT.Fields.Clear(); } //jo le bricolo
                                            if (sT.Fields.Count == 0 && (sT.Aliases.Contains(qTable.Alias) || sT.Table.Equals(sTable)))
                                            {
                                                sT.Fields = sFields.Split(Convert.ToChar(",")).ToList();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_querymenu47);
                                }
                            };
                            cmSub3.Items.Add(itemSM);//add the item to the context menu
                        }
                        cm.Items.Add(cmSub3);

                        if (JobSandbox.DynParams.Count > 0)
                        {
                            MenuItem cmSub1 = new()
                            {
                                Name = "miQueryDynParams",
                                Header = Languages.Languages.ma_msg_querymenu48,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };
                            cmSub1.MouseMove += UIElement_MouseDown;

                            int iDynParam = 0;
                            foreach (string sSP in JobSandbox.DynParams)
                            {
                                iDynParam++;
                                string sParam = iDynParam.ToString();
                                MenuItem itemSM = new()
                                {
                                    Header = string.Concat(Languages.Languages.ma_msg_querymenu49, sParam, Languages.Languages.ma_msg_querymenu50, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sSP, JobSandbox.DynParams).Replace("_", "__"), ")"),
                                    FontSize = 10,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue,
                                    Background = System.Windows.Media.Brushes.White
                                };//make a menuitem instance
                                itemSM.Click += (s, eArgs) => { tbQueries.CaretPosition.InsertTextInRun(string.Concat("{?", sParam, "}")); ColorizeRichTextBox(false, true); };
                                cmSub1.Items.Add(itemSM);//add the item to the context menu
                            }

                            cm.Items.Add(cmSub1);
                        }
                        else
                        {
                            MenuItem cmSub1 = new()
                            {
                                Name = "miQueryAddDynParam",
                                Header = Languages.Languages.ma_msg_querymenu51,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };
                            cmSub1.MouseMove += UIElement_MouseDown;

                            cmSub1.Click += (s, eArgs) =>
                            {
                                FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_querymenu52, Languages.Languages.ma_msg_querymenu53, false, true);
                                fP.ShowDialog();
                                string sParam = fP.PromptUserData.Trim();
                                tbQueries.CaretPosition.InsertTextInRun("{?1}");
                                RichTB.SetTextRTB(tbQueryVariableParameters, sParam);
                                MessageBox.Show(Languages.Languages.ma_msg_querymenu54);
                                ColorizeRichTextBox(false, true);
                            };
                            cm.Items.Add(cmSub1);
                        }

                        MenuItem cmSub2 = new()
                        {
                            Name = "miQueryDBTransfo",
                            Header = Languages.Languages.ma_msg_querymenu55,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };
                        cmSub2.MouseMove += UIElement_MouseDown;

                        foreach (string sFP in IntellisenseData.TransfoPatterns(sQ.ConnectionSrc.SConnDriverSuffix))
                        {
                            MenuItem itemSM = new()
                            {
                                Header = sFP.Replace("_", "__"),
                                FontSize = 9,
                                Foreground = System.Windows.Media.Brushes.DarkBlue,
                                Background = System.Windows.Media.Brushes.White
                            };//make a menuitem instance
                            itemSM.Click += (s, eArgs) => { tbQueries.CaretPosition.InsertTextInRun(sFP.IndexOf("(ex :") > 0 ? sFP[..sFP.IndexOf("(ex :")] : sFP); ColorizeRichTextBox(true, true); };
                            cmSub2.Items.Add(itemSM);//add the item to the context menu
                        }

                        cm.Items.Add(cmSub2);

                        MenuItem item12 = new()
                        {
                            Name = "miQueryBasicBuilder",
                            Header = Languages.Languages.ma_msg_querymenu56,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        item12.MouseMove += UIElement_MouseDown;

                        item12.Click += (s, eArgs) => { QueryBuilder(JobSandbox, false); ColorizeRichTextBox(false, true); };
                        cm.Items.Add(item12);//add the item to the context menu

                        Separator sp4 = new()
                        {
                            Background = System.Windows.Media.Brushes.Black
                        };
                        cm.Items.Add(sp4);

                        MenuItem itemAO = new()
                        {
                            Name = "miQueryAdvanced",
                            Header = Languages.Languages.ma_msg_querymenu57,
                            Foreground = System.Windows.Media.Brushes.DarkRed,
                            FontSize = 12,
                            FontWeight = FontWeights.Light
                        };//make a menuitem instance
                        itemAO.MouseMove += UIElement_MouseDown;

                        cm.Items.Add(itemAO);//add the item to the context menu

                        if (sQ.ConnectionSrc.SConnDriverSuffix.Equals("WS"))
                        {
                            MenuItem item7 = new()
                            {
                                Name = "miQueryAdvancedBodyContent",
                                Header = Languages.Languages.ma_msg_querymenu58,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };//make a menuitem instance
                            item7.MouseMove += UIElement_MouseDown;

                            item7.Click += (s, eArgs) => { AddBodyToWSQuery(JobSandbox, sQ); };
                            cm.Items.Add(item7);//add the item to the context menu
                        }

                        if (sQ.ConnectionSrc.SConnDriverSuffix.Equals("MB"))
                        {
                            if (!sQ.ConnectionSrc.SConnString(JobSandbox.DynParams).Contains(sQ.QueryAnalyzer.Tables[0].Name))
                            {
                                MenuItem item8 = new()
                                {
                                    Name = "miQueryAdvancedSpecifyPwd",
                                    Header = Languages.Languages.ma_msg_querymenu59 + sQ.QueryAnalyzer.Tables[0].Name + Languages.Languages.ma_msg_querymenu60,
                                    FontSize = 10,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue
                                };//make a menuitem instance
                                item8.MouseMove += UIElement_MouseDown;

                                item8.Click += (s, eArgs) => { AddPasswordToMBQuery(sQ); };
                                cm.Items.Add(item8);//add the item to the context menu
                            }
                        }

                        MenuItem item5 = new()
                        {
                            Name = "miQueryAdvancedCrossQ",
                            Header = Languages.Languages.ma_msg_querymenu61,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        item5.MouseMove += UIElement_MouseDown;

                        item5.Click += (s, eArgs) => { AddCrossConnectionJoin(JobSandbox, sQ); ColorizeRichTextBox(true, true); };
                        cm.Items.Add(item5);//add the item to the context menu

                        MenuItem item4 = new()
                        {
                            Name = "miQueryAdvancedDualT",
                            Header = Languages.Languages.ma_msg_querymenu62,
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.DarkBlue
                        };//make a menuitem instance
                        item4.MouseMove += UIElement_MouseDown;

                        item4.Click += (s, eArgs) => { AddMultiTargetQuery(JobSandbox, sQ); ColorizeRichTextBox(true, true); };
                        cm.Items.Add(item4);//add the item to the context menu      

                        //pattern multi-fichiers
                        if (sQ.ConnectionTrg.SConnDriverSuffix.Equals("FI"))
                        {
                            MenuItem cmSub4 = new()
                            {
                                Name = "miQueryFileOutPattern",
                                Header = Languages.Languages.ma_msg_querymenu63,
                                FontSize = 10,
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };
                            cmSub4.MouseMove += UIElement_MouseDown;

                            List<string> sListPattern = new()
                            {
                                Languages.Languages.ma_msg_querymenu64,
                                Languages.Languages.ma_msg_querymenu65,
                                Languages.Languages.ma_msg_querymenu66,
                                Languages.Languages.ma_msg_querymenu67
                            };
                            for (int iO = 0; iO < 4; iO++)
                            {
                                MenuItem itemSM = new()
                                {
                                    Header = sListPattern[iO].Replace("_", "__"),
                                    FontSize = 9,
                                    Foreground = System.Windows.Media.Brushes.DarkBlue,
                                    Background = System.Windows.Media.Brushes.White
                                };//make a menuitem instance
                                itemSM.Click += (s, eArgs) =>
                                {
                                    try
                                    {
                                        string sPattern = itemSM.Header.ToString()[..(itemSM.Header.ToString().IndexOf("]") + 1)];
                                        tbQueries.CaretPosition.InsertTextInRun(sPattern);
                                        ColorizeRichTextBox(true, true);
                                    }
                                    catch (Exception ex) { MessageBox.Show(ex.Message); }

                                };
                                cmSub4.Items.Add(itemSM);//add the item to the context menu
                            }
                            cm.Items.Add(cmSub4);
                        }
                    }
                    else
                    {
                        MenuItem item = new()
                        {
                            Name = "miQueryCreateNewQ",
                            Header = Languages.Languages.ma_msg_querymenu68,
                            Foreground = System.Windows.Media.Brushes.Black,
                            Background = System.Windows.Media.Brushes.DimGray,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold
                        };//make a menuitem instance
                        item.MouseMove += UIElement_MouseDown;

                        item.Click += (s, eArgs) => { QueryBuilder(JobSandbox, true); ColorizeRichTextBox(false, true); };
                        cm.Items.Add(item);//add the item to the context menu
                    }
                }
                else { MessageBox.Show(Languages.Languages.ma_msg_querysandboxmustchooseconn); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Languages.Languages.ma_msg_querymenu69 + ex.Message + ")");
            }
        }

        private static string LoadQuerySourceInfos(Job INIP, Query Q, bool bFirstSource)
        {
            StringBuilder sbEx = new();

            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source01 + Q.ConnectionSrc.SConnDriverFriendlyName);
            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source02 + Q.ConnectionSrc.SConnName);

            StringBuilder sbFileInfo = new();

            if (Q.ConnectionTrg.SConnDriverSuffix.Equals("FI") || Q.ConnectionTrg.SConnDriverSuffix.Equals("DB") || Q.ConnectionTrg.SConnDriverSuffix.Equals("NS"))
            {
                string sTable = Q.QueryAnalyzer.Tables[0].Name;
                if (!Q.QueryAnalyzer.Tables[0].IsSubQuery)
                {
                    bool bSQL = Q.QueryAnalyzer.Tables[0].Name.IndexOf("%") > -1;
                    bool bFILE = Q.QueryAnalyzer.Tables[0].Name.IndexOf("*") > -1;
                    if (bSQL || bFILE)
                    {
                        if (bFILE) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source03); }
                        if (bSQL) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source04); }

                        string sResult = "";
                        if (bFILE) { sResult = sTable.Replace("*", Languages.Languages.ma_msg_querymenu_source05); }
                        ;
                        if (bSQL) { sResult = sTable.Replace("%", Languages.Languages.ma_msg_querymenu_source05); }

                        if (bSQL) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source06 + sResult); }
                        if (bFILE) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source07 + sResult); }
                        sbEx.AppendLine("");
                    }
                }
            }

            if (Q.ConnectionSrc.SConnDriverSuffix.Equals("FI"))
            {
                if (Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                {
                    FTPTools.FTPConnectionVariables FTPVars = new(Q.ConnectionSrc, INIP.DynParams);
                    FTPVars.RewritePath(Q.QueryAnalyzer.Tables[0].Name);

                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source08 + FTPVars.FTPURL);
                    sbFileInfo.AppendLine("PORT : " + FTPVars.FTPPort);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source09 + FTPVars.FTPRemotePath);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source10 + FTPVars.Is_SFTP.ToString());
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source11 + FTPVars.FTPUsername + "," + FTPVars.FTPPassword);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source12 + FTPVars.SFTPUseAuthentificationByKeyFile.ToString());
                    if (FTPVars.SFTPSSHKeyFile.Length > 0) { sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source13 + FTPVars.SFTPSSHKeyFile); }
                    sbFileInfo.AppendLine("PROXY TYPE : " + FTPVars.FTPProxyType.ToString());
                    if (FTPVars.FTPProxyUsername.Length > 0) { sbFileInfo.AppendLine("PROXY USER : " + FTPVars.FTPProxyUsername); }
                    if (FTPVars.FTPProxyPassword.Length > 0) { sbFileInfo.AppendLine("PROXY PWD : " + FTPVars.FTPProxyPassword); }
                    if (FTPVars.FTPProxyURL.Length > 0) { sbFileInfo.AppendLine("PROXY URL : " + FTPVars.FTPProxyURL); }
                    if (FTPVars.FTPProxyPort > 0) { sbFileInfo.AppendLine("PROXY PORT : " + FTPVars.FTPProxyPort.ToString()); }
                }
                else
                {
                    foreach (Query.QTable qT in Q.QueryAnalyzer.Tables)
                    {
                        if (qT.Name.LastIndexOf("\\") > 0)
                        {
                            string sFile = qT.Name;
                            if (sFile.StartsWith("\\")) { sFile = sFile[1..]; }
                            string sDir = string.Concat(Q.ConnectionSrc.SConnString(INIP.DynParams), sFile[..sFile.LastIndexOf("\\")]);
                            sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source14 + sDir);
                        }
                        else if (qT.Name.LastIndexOf("/") > 0)
                        {
                            string sFile = qT.Name;
                            if (sFile.StartsWith("/")) { sFile = sFile[1..]; }
                            string sDir = string.Concat(Q.ConnectionSrc.SConnString(INIP.DynParams), sFile[..sFile.LastIndexOf("/")]);
                            sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source14 + sDir);
                        }
                        else { sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source14 + Q.ConnectionSrc.SConnString(INIP.DynParams)); }
                    }
                }
                string sFiles = "";
                foreach (Query.QTable qT in Q.QueryAnalyzer.Tables)
                {
                    sFiles = string.Concat(sFiles, qT.Name[(qT.Name.LastIndexOf("\\") + 1)..], ",");
                }
                sFiles = sFiles[0..^1];
                sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source15 + sFiles);

                if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                {
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source16 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                }
                if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                {
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_source17 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                }
            }

            switch (Q.ConnectionSrc.SConnDriver)
            {
                case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                    string[] sObjects = { "users", "groups" };
                    if (sObjects.Contains(Q.QueryAnalyzer.Tables[0].Name))
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source18 + ADTools.GetADQuery(Q.ConnectionSrc, Q.QueryAnalyzer.Tables[0].Name));
                    }
                    else { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source19); }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source20 + INIP.ADSearchScope.ToString());
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source21);
                    break;
                case SQLTools_Enums.BDD.FI_CSV:
                    sbEx.Append(sbFileInfo);
                    break;
                case SQLTools_Enums.BDD.FI_XLS:
                    sbEx.Append(sbFileInfo);
                    break;
                case SQLTools_Enums.BDD.FI_JSON:
                    sbEx.Append(sbFileInfo);
                    break;
                case SQLTools_Enums.BDD.FI_XML:
                    sbEx.Append(sbFileInfo);
                    break;
                case SQLTools_Enums.BDD.MB_MAIL:
                    MailTools.MAILConnectionVariables MCvars = new(Q.ConnectionSrc, 10000, 1, INIP.DynParams);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source22 + MCvars.Host_Send);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source23 + MCvars.Host_Receive);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source24 + MCvars.SENDPort.ToString());
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source25 + MCvars.RECEIVEPort.ToString());
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source26 + Q.QueryAnalyzer.Tables[0].Name);
                    if (!MailTools.CheckMailValid(Q.QueryAnalyzer.Tables[0].Name))
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source27);
                    }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source28 + MCvars.SenderAddress);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source29 + MCvars.SenderPassword);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source30 + MCvars.IsSSL.ToString());
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source31 + MCvars.MailGetProtocol.ToString());
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source32 + MCvars.AuthentificationProtocol.ToString());
                    sbEx.AppendLine("PROXY TYPE : " + MCvars.ProxyType.ToString());
                    if (MCvars.ProxyUsername.Length > 0) { sbEx.AppendLine("PROXY USER : " + MCvars.ProxyUsername); }
                    if (MCvars.ProxyPassword.Length > 0) { sbEx.AppendLine("PROXY PWD : " + MCvars.ProxyPassword); }
                    if (MCvars.ProxyURL.Length > 0) { sbEx.AppendLine("PROXY URL : " + MCvars.ProxyURL); }
                    if (MCvars.ProxyPort > 0) { sbEx.AppendLine("PROXY PORT : " + MCvars.ProxyPort.ToString()); }
                    if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source33 + string.Join(",", MailTools.MAIL_FIELDS));
                    }
                    else
                    {
                        string sUnknownF = "";
                        foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                        {
                            if (!MailTools.MAIL_FIELDS.Contains(qF.Name)) { sUnknownF = string.Concat(sUnknownF, qF.Name, ","); }
                        }
                        if (sUnknownF.Length > 0)
                        {
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source34 + sUnknownF[0..^1]);
                        }
                    }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source35);
                    break;
                case SQLTools_Enums.BDD.WS_REST:
                    WSTools.WebserviceConnectionVariables wSV2 = new(Q.ConnectionSrc, 0, INIP.DynParams);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source36 + wSV2.WebServiceURL);
                    string sSubURL = Q.QueryAnalyzer.Tables[0].Name.Split(Convert.ToChar("["))[0];
                    string sFullURL = string.Concat(wSV2.WebServiceURL, sSubURL.IndexOf("=") > -1 ? ((wSV2.WebServiceURL.IndexOf("?") > 0 ? "&" : "?") + sSubURL) : sSubURL);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source37 + sFullURL);
                    var sAuth = (SQLTools_Enums.WEBSERVICE_AUTHORIZATION)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_AUTHORIZATION), Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD));
                    string sURLAuth = Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Split(Convert.ToChar("["))[0];
                    string sURLAuthBody = sURLAuth.Length < Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Length ? Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Split(Convert.ToChar("["))[1] : "";
                    if (sURLAuthBody.Length > 0 && sURLAuthBody.EndsWith("]")) { sURLAuthBody = sURLAuthBody[0..^1]; }
                    string sKeyAuth = Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY);
                    string sValueAuth = Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE);
                    string sParamAuth = Q.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source38 + sAuth.ToString());
                    switch (sAuth)
                    {
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_HEADER:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source39 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source40 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source41 + sKeyAuth + ":" + Languages.Languages.ma_msg_querymenu_source42); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source43 + sKeyAuth + ":" + sValueAuth); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_PARAM:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source44 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source45 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source46 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", Languages.Languages.ma_msg_querymenu_source42)); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source47 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", sValueAuth)); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BASIC_AUTH:
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source47 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", sValueAuth)); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BEARER:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source39 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source40 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source41 + sKeyAuth + ":Bearer " + Languages.Languages.ma_msg_querymenu_source42); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source43 + sKeyAuth + ":Bearer " + sValueAuth); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.HTTP:
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source48 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sParamAuth));
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2:
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2DELEGATED:
                            break;
                    }
                    if (wSV2.WebServiceHeader.Count > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source49 + string.Join(";", wSV2.WebServiceHeader)); }
                    foreach (Query.QTable qT in Q.QueryAnalyzer.Tables)
                    {
                        if (qT.Name.IndexOf("[") > -1)
                        {
                            string sWhere = qT.Name[(qT.Name.IndexOf("[") + 1)..];
                            if (sWhere.EndsWith("]"))
                            {
                                sWhere = sWhere[0..^1];
                                sWhere = Toolbox.SetCleanJsonPattern(sWhere);
                                sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source50 + sWhere);
                            }
                            else { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source51); }
                        }
                    }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source52 + INIP.WebServiceCallMethod.ToString());
                    sbEx.AppendLine("PROXY TYPE : " + wSV2.WebServiceProxyType.ToString());
                    if (wSV2.WebServiceProxyUsername.Length > 0) { sbEx.AppendLine("PROXY USER : " + wSV2.WebServiceProxyUsername); }
                    if (wSV2.WebServiceProxyPassword.Length > 0) { sbEx.AppendLine("PROXY PWD : " + wSV2.WebServiceProxyPassword); }
                    if (wSV2.WebServiceProxyURL.Length > 0) { sbEx.AppendLine("PROXY URL : " + wSV2.WebServiceProxyURL); }
                    if (wSV2.WebServiceProxyPort > 0) { sbEx.AppendLine("PROXY PORT : " + wSV2.WebServiceProxyPort.ToString()); }
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source54 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.NS_MONGODB:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source53 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstSource ? INIP.DatabaseName_Source : Q.ConnectionSrc.SConnDB, INIP.DynParams));
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source56 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandSource.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_source57 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandSource, INIP.DynParams));
                    }

                    break;
            }

            return sbEx.ToString().Trim();
        }

        private static string LoadCrossQueryInfos(Query Q, Query Qlast)
        {
            StringBuilder sbQ = new();

            //crossquery
            sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq01, Q.ConnectionSrc.SConnName));
            sbQ.AppendLine(Languages.Languages.ma_msg_querymenu_crossq02);
            //sbQ.Append(LoadQuerySourceInfos(Q, false));
            sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq03, Q.CrossJoinType.ToString()));

            List<string> sFieldsJoin = new(); //recherche des champs de jointure

            if (Qlast != null)
            {
                foreach (Query.QField qFl in Qlast.QueryAnalyzer.Fields)
                {
                    foreach (Query.QField qFn in Q.QueryAnalyzer.Fields)
                    {
                        if (qFl.Alias.Equals(qFn.Alias, StringComparison.InvariantCultureIgnoreCase))
                        {
                            sFieldsJoin.Add(qFn.Alias); break;
                        }
                    }
                }
            }

            if (Qlast != null && Q.CrossQueryForceJoinFields.Count > 0)
            {
                sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq04, string.Join(",", Q.CrossQueryForceJoinFields)));
            }
            else
            {
                if (sFieldsJoin.Count == 0)
                {
                    sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq05));
                }
                else { sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq06, string.Join(",", sFieldsJoin))); }
            }
            if (Q.CrossJoinQueryWhere.Length > 0) { sbQ.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_crossq07, Q.CrossJoinQueryWhere.ToString())); }

            return sbQ.ToString().Trim();
        }

        private string LoadQueryTargetInfos(Job INIP, Query Q, bool bFirstTarget)
        {
            StringBuilder sbEx = new();

            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target01 + Q.ConnectionTrg.SConnDriverFriendlyName);
            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target02 + Q.ConnectionTrg.SConnName);

            StringBuilder sbFileInfo = new();
            if (Q.ConnectionTrg.SConnDriverSuffix.Equals("FI"))
            {
                if (Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                {
                    FTPTools.FTPConnectionVariables FTPVars = new(Q.ConnectionTrg, INIP.DynParams);
                    FTPVars.RewritePath(Q.QueryAnalyzer.Tables[0].Name);

                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target03 + FTPVars.FTPURL);
                    sbFileInfo.AppendLine("PORT : " + FTPVars.FTPPort);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target04 + FTPVars.FTPRemotePath);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target05 + FTPVars.Is_SFTP.ToString());
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target06 + FTPVars.FTPUsername + "," + FTPVars.FTPPassword);
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target07 + FTPVars.SFTPUseAuthentificationByKeyFile.ToString());
                    if (FTPVars.SFTPSSHKeyFile.Length > 0) { sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target08 + FTPVars.SFTPSSHKeyFile); }
                    sbFileInfo.AppendLine("PROXY TYPE : " + FTPVars.FTPProxyType.ToString());
                    if (FTPVars.FTPProxyUsername.Length > 0) { sbFileInfo.AppendLine("PROXY USER : " + FTPVars.FTPProxyUsername); }
                    if (FTPVars.FTPProxyPassword.Length > 0) { sbFileInfo.AppendLine("PROXY PWD : " + FTPVars.FTPProxyPassword); }
                    if (FTPVars.FTPProxyURL.Length > 0) { sbFileInfo.AppendLine("PROXY URL : " + FTPVars.FTPProxyURL); }
                    if (FTPVars.FTPProxyPort > 0) { sbFileInfo.AppendLine("PROXY PORT : " + FTPVars.FTPProxyPort.ToString()); }
                }
                else
                {
                    if (Q.OutputTable.LastIndexOf("\\") > 0)
                    {
                        string sFile = Q.OutputTable;
                        if (sFile.StartsWith("\\")) { sFile = sFile[1..]; }
                        string sDir = string.Concat(Q.ConnectionTrg.SConnString(INIP.DynParams), sFile[..sFile.LastIndexOf("\\")]);
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target09 + sDir);
                    }
                    else { sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target09 + Q.ConnectionTrg.SConnString(INIP.DynParams)); }
                }

                if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                {
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target10 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                }
                if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                {
                    sbFileInfo.AppendLine(Languages.Languages.ma_msg_querymenu_target11 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                }

            }

            if (Q.ConnectionTrg.SConnDriverSuffix.Equals("FI"))
            {
                string sFileOutput = Q.OutputTable[(Q.OutputTable.LastIndexOf("\\") + 1)..];
                MatchCollection mcOutA = Regex.Matches(sFileOutput, SHSRegex.REGEX_FILE_OUTPUT_PATTERN);
                if (mcOutA.Count > 0)
                {
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target12);
                    foreach (Match mc in mcOutA)
                    {
                        if (mc.Value.Equals("[ROWCOUNT]")) { sbEx.AppendLine("\t" + mc.Value + Languages.Languages.ma_msg_querymenu_target13); }
                        else if (mc.Value.Equals("[FILECOUNT]")) { sbEx.AppendLine("\t" + mc.Value + Languages.Languages.ma_msg_querymenu_target14); }
                        else if (mc.Value.Equals("[QUERYALIAS]")) { sbEx.AppendLine("\t" + mc.Value + Languages.Languages.ma_msg_querymenu_target15 + Q.QueryAnalyzer.Tables[0].Alias); }
                        else { sbEx.AppendLine("\t" + mc.Value + Languages.Languages.ma_msg_querymenu_target16); }
                    }
                    sbEx.AppendLine("");
                }
            }

            switch (Q.ConnectionTrg.SConnDriver)
            {
                case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                    string sValueAD = Languages.Languages.ma_msg_querymenu_target20b;
                    if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                    {
                        sValueAD = Languages.Languages.ma_msg_querymenu_target20;
                    }
                    else
                    {
                        foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                        {
                            if (qF.Alias.Equals(INIP.ADSearchProperty, StringComparison.OrdinalIgnoreCase)) { sValueAD = Languages.Languages.ma_msg_querymenu_target20c + qF.Alias; break; }
                        }
                    }

                    if (Q.OutputTable.StartsWith("user", StringComparison.OrdinalIgnoreCase))
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target17 + Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_USER).Replace("[SEARCH_PROPERTY]", INIP.ADSearchProperty).Replace("[SEARCH_VALUE]", "[" + sValueAD + "]"));
                    }
                    else
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target17 + Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_GROUP).Replace("[SEARCH_PROPERTY]", INIP.ADSearchProperty).Replace("[SEARCH_VALUE]", "[" + sValueAD + "]"));
                    }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target18 + Q.OutputTable);
                    break;
                case SQLTools_Enums.BDD.FI_CSV:
                    sbEx.Append(sbFileInfo);
                    string sOut = Q.OutputTable[(Q.OutputTable.LastIndexOf("\\") + 1)..];
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target19 + sOut);
                    if (INIP.CSVAddHeader)
                    {
                        if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                        {
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target20);
                        }
                        else
                        {
                            string sH = "";
                            foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                            {
                                sH = string.Concat(sH, qF.Alias, INIP.CSVCharSeparator_Target);
                            }
                            if (!INIP.CSVCharSeparator_EndRow) { sH = sH[0..^1]; }
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target21 + sH);
                        }
                    }
                    break;
                case SQLTools_Enums.BDD.FI_XLS:
                    sbEx.Append(sbFileInfo);
                    string sOutB = Q.OutputTable[(Q.OutputTable.LastIndexOf("\\") + 1)..];
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target19 + sOutB);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target22 + Q.QueryAnalyzer.Tables[0].Alias);

                    string sObjet = INIP.JobDescription.Length == 0 ? INIP.JobNAME : INIP.JobDescription;
                    sObjet = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sObjet, INIP.DynParams);

                    if (INIP.XLSWithTitle) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target22b + sObjet); }
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target22c + INIP.XLSStyle);
                    }
                    if (INIP.XLSAddHeader)
                    {
                        if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                        {
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target20);
                        }
                        else
                        {
                            string sH = "";
                            foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                            {
                                sH = string.Concat(sH, qF.Alias, INIP.CSVCharSeparator_Target);
                            }
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target21 + sH);
                        }
                    }
                    break;
                case SQLTools_Enums.BDD.FI_JSON:
                    sbEx.Append(sbFileInfo);
                    string sOutC = Q.OutputTable[(Q.OutputTable.LastIndexOf("\\") + 1)..];
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target19 + sOutC);
                    break;
                case SQLTools_Enums.BDD.FI_XML:
                    sbEx.Append(sbFileInfo);
                    string sOutD = Q.OutputTable[(Q.OutputTable.LastIndexOf("\\") + 1)..];
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target19 + sOutD);
                    string sXML = "";
                    if (INIP.XMLWriteMode == 0)
                    {
                        if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                        {
                            sXML = Languages.Languages.ma_msg_querymenu_target23;
                        }
                        else
                        {
                            sXML = "<" + INIP.XMLTargetRowBuilder + ">";
                            foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                            {
                                sXML = string.Concat(sXML, "<" + qF.Alias, ">" + (INIP.XMLAddCDataTag ? "<![CDATA[value]]>" : "value") + "</" + qF.Alias + ">");
                            }
                            sXML = string.Concat(sXML, "</" + INIP.XMLTargetRowBuilder + ">");
                        }
                    }
                    else
                    {
                        if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                        {
                            sXML = Languages.Languages.ma_msg_querymenu_target23;
                        }
                        else
                        {
                            sXML = "<" + Q.QueryAnalyzer.Tables[0].Alias;
                            foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                            {
                                sXML = string.Concat(sXML, " " + qF.Alias, "=\"[value]\"");
                            }
                            sXML = string.Concat(sXML, " />");
                        }
                    }

                    string sXMLh;

                    string sMainBody = "";
                    if (INIP.XMLWriteMode == 0)
                    {
                        sMainBody = Q.QueryAnalyzer.Tables[0].Alias.Replace("\\\"\"", "\""); //cas des alias "cii_agents ref=\""C2AGT_CODE\"""
                        if (sMainBody.StartsWith("\"") && sMainBody.EndsWith("\"")) { sMainBody = sMainBody[1..^1]; }
                        else if (sMainBody.StartsWith("`") && sMainBody.EndsWith("`")) { sMainBody = sMainBody[1..^1]; }
                    }
                    else
                    {
                        sMainBody = INIP.XMLTargetRowBuilder;
                    }

                    if (INIP.XMLHeader.StartsWith("<?") && INIP.XMLHeader.EndsWith("?>")) { sXMLh = INIP.XMLHeader; }
                    else if (INIP.XMLHeader.StartsWith("<") && INIP.XMLHeader.EndsWith(">")) { sXMLh = string.Concat("<?", INIP.XMLHeader[1..^1], "?>"); }
                    else { sXMLh = string.Concat("<?", INIP.XMLHeader, "?>"); }

                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target24 + sXMLh
                                                                + "<" + sMainBody + ">"
                                                                + sXML
                                                                + "</" + sMainBody.Split(" ")[0].Split("=")[0] + ">");
                    break;
                case SQLTools_Enums.BDD.MB_MAIL:
                    string sObjetB = INIP.JobDescription.Length == 0 ? INIP.JobNAME : INIP.JobDescription;
                    sObjet = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sObjetB, INIP.DynParams);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target25 + sObjetB);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target26 + Q.OutputTable);
                    string[] sRecipients = Q.OutputTable.Split(Convert.ToChar(","));
                    string sInvalid = "";
                    foreach (string sMail in sRecipients)
                    {
                        if (!MailTools.CheckMailValid(sMail))
                        {
                            sInvalid = string.Concat(sInvalid, sMail, ",");
                        }
                    }
                    if (sInvalid.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target27 + sInvalid[0..^1]); }
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target28 + Q.QueryAnalyzer.Tables[0].Alias.Replace("_", " "));
                    break;
                case SQLTools_Enums.BDD.WS_REST:
                    string webRequest = Languages.Languages.ma_msg_querymenu_target29;
                    WSTools.WebserviceConnectionVariables wSV2 = new(Q.ConnectionTrg, 0, INIP.DynParams);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target30 + wSV2.WebServiceURL);
                    string sFullURL = string.Concat(wSV2.WebServiceURL, Q.OutputTable.IndexOf("=") > -1 ? ((wSV2.WebServiceURL.IndexOf("?") > 0 ? "&" : "?") + Q.OutputTable) : Q.OutputTable);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target31 + sFullURL);
                    var sAuth = (SQLTools_Enums.WEBSERVICE_AUTHORIZATION)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_AUTHORIZATION), Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD));
                    string sURLAuth = Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Split(Convert.ToChar("["))[0];
                    string sURLAuthBody = sURLAuth.Length < Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Length ? Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Split(Convert.ToChar("["))[1] : "";
                    if (sURLAuthBody.Length > 0 && sURLAuthBody.EndsWith("]")) { sURLAuthBody = sURLAuthBody[0..^1]; }
                    string sKeyAuth = Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY);
                    string sValueAuth = Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE);
                    string sParamAuth = Q.ConnectionTrg.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS);
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target32 + sAuth.ToString());
                    switch (sAuth)
                    {
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_HEADER:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target33 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target34 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target36 + sKeyAuth + ":" + Languages.Languages.ma_msg_querymenu_target35); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target36 + sKeyAuth + ":" + sValueAuth); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_PARAM:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target33 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target34 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target37 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", "DYNAMIC_TOKEN")); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target37 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", sValueAuth)); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BASIC_AUTH:
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target37 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sKeyAuth, "=", sValueAuth)); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BEARER:
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target33 + sURLAuth); }
                            if (sURLAuthBody.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target34 + sURLAuthBody); }
                            if (sURLAuth.Length > 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target36 + sKeyAuth + ":Bearer " + Languages.Languages.ma_msg_querymenu_target35); }
                            if (sURLAuth.Length == 0) { sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target36 + sKeyAuth + ":Bearer " + sValueAuth); }
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.HTTP:
                            sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target38 + string.Concat(sFullURL, !sFullURL.Contains('?', StringComparison.CurrentCulture) ? "?" : "&", sParamAuth));
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2:
                            break;
                        case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2DELEGATED:
                            break;
                    }
                    if (Q.QueryAnalyzer.Fields[0].Name.Equals("*"))
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target39 + webRequest);
                    }
                    else
                    {
                        DataTable dtTemp = new(); //création d'un résultat factice
                        foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                        {
                            dtTemp.Columns.Add(qF.Alias, Type.GetType("System.String"));
                        }
                        DataRow dr = dtTemp.NewRow();
                        foreach (Query.QField qF in Q.QueryAnalyzer.Fields)
                        {
                            dr[qF.Alias] = "value";
                        }
                        dtTemp.Rows.Add(dr);
                        List<Tuple<string, object>> sListWSQueries = WSTools.CreateRESTQueriesFromDataset(INIP, ref FuzibleController.LOG, dtTemp, Q.QueryAnalyzer.Tables[0].Alias);
                        if (sListWSQueries.Count > 0)
                        {
                            switch (INIP.WebServiceContentType_Target)
                            {
                                case SQLTools_Enums.WEBSERVICE_CONTENT.JSON:
                                    sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target40, sListWSQueries[0].Item1));
                                    break;
                                case SQLTools_Enums.WEBSERVICE_CONTENT.XML:
                                    sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target41, sListWSQueries[0].Item1));
                                    break;
                                case SQLTools_Enums.WEBSERVICE_CONTENT.TXT:
                                    sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target42, sListWSQueries[0].Item1));
                                    break;
                                case SQLTools_Enums.WEBSERVICE_CONTENT.HTTP_PARAMS:
                                    sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target43, sListWSQueries[0].Item1));
                                    break;
                                case SQLTools_Enums.WEBSERVICE_CONTENT.BINARY:
                                    sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target42b, sListWSQueries[0].Item1));
                                    break;
                            }
                        }
                        dtTemp.Clear();
                    }
                    if (INIP.WebServiceSaveResponseFile)
                    {
                        string sLogTable;
                        if (INIP.WebserviceLogTableResponses.Length > 0)
                        {
                            string sL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.WebserviceLogTableResponses, INIP.DynParams);
                            sLogTable = string.Concat("log_", Toolbox.RemoveSpecialCharacters(sL, "_", false).ToLower());
                        }
                        else { sLogTable = string.Concat("log_", Toolbox.RemoveSpecialCharacters(Q.OutputTable, "_", false).ToLower()); }

                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target44, sLogTable));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target45));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target46, Q.QueryAnalyzer.Tables[0].Alias));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target47, webRequest));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target48));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target49));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target50));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target51));
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target52));
                        if (INIP.WebServiceTrackingColumnInResponses.Length > 0)
                        {
                            sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target53, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.WebServiceTrackingColumnInResponses, INIP.DynParams)));
                        }
                        sbEx.AppendLine(string.Concat(Languages.Languages.ma_msg_querymenu_target54));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget || INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target56 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target58 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target60 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
                case SQLTools_Enums.BDD.NS_MONGODB:
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target55 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(bFirstTarget && INIP.ConnectionString_Target.SConnID == Q.ConnectionTrg.SConnID ? INIP.DatabaseName_Target : Q.ConnectionTrg.SConnDB, INIP.DynParams));
                    sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target57 + Q.OutputTable);
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target59 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    if (INIP.PreOrPostCommand_Target == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && INIP.PrePostJob_CommandTarget.Length > 0)
                    {
                        sbEx.AppendLine(Languages.Languages.ma_msg_querymenu_target61 + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.PrePostJob_CommandTarget, INIP.DynParams));
                    }
                    break;
            }

            //sbEx.Append(Environment.NewLine);
            //sbEx.AppendLine(string.Concat("******************************** USAGES ********************************"));
            //sbEx.AppendLine("> INPUT ALIAS : Used for optional DBNAME column : Source = File : source filename / Also used when Target = Webservice : Will fill the additionnal 'WS_QUERYTARGETNAME' column in each webservice answer");
            //sbEx.AppendLine("\t[Side note about the DBNAME column : it will be filled like this : Source = SQL -> source database / Source = MAIL -> sender address / Source = WEBSERVICE -> Webservice URL]");
            //sbEx.AppendLine("> INPUT ALIAS : Target = XML File : The main XML tag will be the Input Alias / Target = EXCEL : The Sheet name will be the Input Alias / Target = MAIL : The Sheet Name will be the input alias");
            //sbEx.AppendLine("> TARGET : Used exclusively as the Target Output (SQL -> Table / FILE -> Filename / WEBSERVICE -> Table or file to receive HTTP answers / MAIL -> Recipient address)");

            return sbEx.ToString().Trim();
        }

        private void ShowSourceDataset(Job INIP, Query sBuiltQuery, string sRawQuery, bool bIsSubQuery, bool bAnalyzeData)
        {
            if (JobStatus > 0 && FuzibleController.IsBackgroundTaskCompleted())
            {
                if (sRawQuery != null)
                {
                    if (INIP.ConnectionString_Source != null)
                    {
                        ShowSource sS = new(INIP, null, sRawQuery, bAnalyzeData);
                        sS.Show();
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_queryassistantmusthaveconn);
                    }
                }
                else if (sBuiltQuery != null)
                {
                    if (INIP.ConnectionString_Source != null)
                    {
                        if (!sBuiltQuery.QueryAnalyzer.Errors.Any(qE => qE.ErrorType.Equals(Query.QError.ErrorLevel.ERROR)))
                        {
                            //si on essaie de charger une cross-query, la connection source est différente de celle du job
                            if (bIsSubQuery)
                            {
                                INIP.ConnectionString_Source = sBuiltQuery.ConnectionSrc;
                                INIP.DatabaseName_Source = sBuiltQuery.ConnectionSrc.SConnDB;
                            }
                            if (sBuiltQuery.ConnectionTrg != null)
                            {
                                INIP.ConnectionString_Target = sBuiltQuery.ConnectionTrg;
                            }
                            if (bIsSubQuery)
                            {
                                INIP.HyperFileArrayFieldTransformation = 0;
                                INIP.OptionalDBName_OnInsert = false;
                                INIP.OptionalDynamicParamField_OnInsert = "";
                                INIP.OptionalRowID_OnInsert = false;
                                INIP.OptionalTimestamp_OnInsert = false;
                                INIP.JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT;

                            } //pas de synchro à l'analyse d'une subquery, union, ou cross-query

                            List<string> sParamsDyn = INIP.HasDynParamsWithResultFromPrePostJob;
                            if (sParamsDyn.Count == 0)
                            {
                                ShowSource sS = new(INIP, sBuiltQuery, null, bAnalyzeData);
                                sS.Show();
                                sBuiltQuery = sS.SourceQuery;
                            }
                            else
                            {
                                List<string> sPUsed = new();
                                foreach (string sP in sParamsDyn)
                                {
                                    if (sBuiltQuery.RawQuery.IndexOf(sP) > -1)
                                    {
                                        sPUsed.Add(sP);
                                    }
                                }
                                if (sPUsed.Count == 0)
                                {
                                    ShowSource sS = new(INIP, sBuiltQuery, null, bAnalyzeData);
                                    sS.Show();
                                    sBuiltQuery = sS.SourceQuery;
                                }
                                else
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_showsourcewarningdynparams + string.Join(",", sPUsed) + ")");
                                }
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.ma_msg_showsourceerrorsinquery); }
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_queryassistantmusthaveconn);
                    }
                    TEMP_QUERY = sBuiltQuery;
                }
                else
                {
                    MessageBox.Show(Languages.Languages.ma_msg_queryassistantmusthaveconn);
                }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_canteditwhilejobrunning); }
        }

        private Query FindQueryFromRichTextBox(object sender, Job INIP)
        {
            var box = (RichTextBox)sender;
            Query sQ = null;

            //on récupère le contenu de toute la ligne
            if (RichTB.GetTextRTB(box).Length > 0 && box.CaretPosition.Paragraph != null && box.CaretPosition.Paragraph.Inlines.Count > 0)
            {
                string sSelected = box.Selection.Text;

                if (sSelected.Trim().Length > 0)
                {
                    if (Regex.Match(sSelected, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                    {
                        sQ = new Query(INIP, sSelected);
                    }
                    else if (Regex.Match(string.Concat("OUTPUT:" + sSelected), SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                    {
                        sQ = new Query(INIP, string.Concat("OUTPUT:", sSelected));
                    }
                    else
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_findqueryerror03);
                    }
                }
                else
                {
                    string sTextRun = "";

                    TextPointer caretPosition = box.CaretPosition;

                    // Si la position du curseur est au début du texte, retournez une chaîne vide
                    if (caretPosition == null || caretPosition.GetOffsetToPosition(box.Document.ContentEnd) == 0)
                    {
                        sTextRun = "";
                    }

                    TextPointer start = box.Document.ContentStart;
                    TextPointer end = box.CaretPosition;

                    TextRange textRange = new(start, end);
                    sTextRun = textRange.Text.Trim();

                    //TextPointer pointerA = box.CaretPosition.GetLineStartPosition(0);
                    //bool bNewLine = false;
                    //while (pointerA != null)
                    //{
                    //    if (pointerA.IsAtLineStartPosition)
                    //    {
                    //        if (!bNewLine) { sTextRun = string.Concat(Environment.NewLine, sTextRun); }
                    //        bNewLine = true;
                    //    }
                    //    else
                    //    {
                    //        string s = pointerA.GetTextInRun(LogicalDirection.Backward);
                    //        if (s.Length > 0)
                    //        {
                    //            sTextRun = string.Concat(s, sTextRun);
                    //            bNewLine = false;
                    //        }
                    //    }
                    //    pointerA = pointerA.GetNextContextPosition(LogicalDirection.Backward);
                    //}
                    //sTextRun = sTextRun.Trim();

                    //string sQueries = RichTB.GetTextRTB((RichTextBox)sender);

                    try
                    {
                        string sQuery = GetCurrentSqlQuery((RichTextBox)sender);

                        if (Regex.Match(sQuery, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                        {
                            sQ = new Query(INIP, sQuery);
                        }
                        else
                        {
                            MessageBox.Show(Languages.Languages.ma_msg_findqueryerror03);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Languages.Languages.ma_msg_findqueryerror01 + ex.Message);
                    }

                    //if (Regex.Match(sQueries, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                    //{
                    //    if (Regex.Match(sQueries, "((\\)\\s*)||(\\s+))FROM((\\(\\s*)||(\\s+))", RegexOptions.IgnoreCase).Success)
                    //    {
                    //        try
                    //        {
                    //            string sStartQ = "";
                    //            string sEndQuery = sQueries[sTextRun.Length..].Trim();
                    //            string sActualLine = sQueries[sTextRun.Length..].Trim();
                    //            if (sActualLine.IndexOf(Environment.NewLine) > 0)
                    //            {
                    //                sActualLine = sActualLine.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    //                sEndQuery = sEndQuery[sEndQuery.IndexOf(Environment.NewLine)..];
                    //            }
                    //            else { sEndQuery = ""; } //il n'y a aucune nouvelle ligne à la fin

                    //            //si la ligne actuelle le contient pas le pattern "OUTPUT:SELECT", alors on en déduit qu'on est en plein milieu d'une requête
                    //            //on recherche donc l'itération la plus tardive de "OUTPUT:SELECT" dans sTextRun
                    //            Match mcActual = Regex.Match(sActualLine, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
                    //            if (!mcActual.Success) // la ligne actuelle est en plein milieu d'une requête
                    //            {
                    //                MatchCollection mcListQ = Regex.Matches(sTextRun, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
                    //                sStartQ = sTextRun[mcListQ[^1].Index..];
                    //            }
                    //            if (mcActual.Index > 0) //la ligne actuelle est en plein milieu d'une requête et contient aussi la suivante
                    //            {
                    //                sActualLine = sActualLine.Substring(0, mcActual.Index);
                    //            }

                    //            //on recherche maintenant la fin de la requête : on regarde les lignes qui suivent sActualLine pour trouver le prochain pattern "OUTPUT:SELECT"
                    //            Match mcEnd = Regex.Match(sEndQuery, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
                    //            if (mcEnd.Success)
                    //            {
                    //                sEndQuery = sEndQuery.Substring(0, mcEnd.Index);
                    //            }

                    //            string sDefQuery = string.Concat(sStartQ, " ", sActualLine, " ", sEndQuery);
                    //            sQ = new Query(INIP, sDefQuery);
                    //        }
                    //        catch (Exception ex) { MessageBox.Show(Languages.Languages.ma_msg_findqueryerror01 + ex.Message); }
                    //    }
                    //    else { MessageBox.Show(Languages.Languages.ma_msg_findqueryerror02); }
                    //}
                    //else
                    //{
                    //    MessageBox.Show(Languages.Languages.ma_msg_findqueryerror03);
                    //}
                }
            }
            else
            {
                if (RichTB.GetTextRTB(box).Length == 0)
                {
                    if (INIP.ConnectionString_Source != null && INIP.ConnectionString_Target != null)
                    {
                        string sQuery = "";
                        switch (INIP.ConnectionString_Source.SConnDriverSuffix)
                        {
                            case "DB":
                                MessageBoxResult msgRA = MessageBox.Show(Languages.Languages.ma_msg_findqueryfullreplication01, Languages.Languages.ma_msg_findqueryfullreplication02, MessageBoxButton.YesNo);
                                if (msgRA.ToString().ToUpper().Equals("YES"))
                                {
                                    switch (INIP.ConnectionString_Target.SConnDriverSuffix)
                                    {
                                        case "FI":
                                            sQuery = "*." + INIP.ConnectionString_Target.SConnDriver.ToString()[3..] + ":SELECT * FROM %";
                                            break;
                                        case "DB":
                                            sQuery = "*:SELECT * FROM %";
                                            break;
                                        case "NS":
                                            sQuery = "*:SELECT * FROM %";
                                            break;
                                        case "AD":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                        case "MB":
                                            sQuery = "AnyMail@AnyProvider.com:SELECT * FROM %";
                                            break;
                                        case "WS":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                    }
                                    if (sQuery.Length > 0)
                                    {
                                        if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                        tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_findqueryexemple);
                                    sQuery = FuzibleController.GetExempleQuery(INIP);
                                    if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                    tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                }
                                break;
                            case "FI":
                                MessageBoxResult msgRB = MessageBox.Show(Languages.Languages.ma_msg_findqueryfullpathreplication01, Languages.Languages.ma_msg_findqueryfullpathreplication02, MessageBoxButton.YesNo);
                                if (msgRB.ToString().ToUpper().Equals("YES"))
                                {
                                    switch (INIP.ConnectionString_Target.SConnDriverSuffix)
                                    {
                                        case "DB":
                                            sQuery = "*:SELECT * FROM *." + INIP.ConnectionString_Source.SConnDriver.ToString()[3..];
                                            break;
                                        case "NS":
                                            sQuery = "*:SELECT * FROM *." + INIP.ConnectionString_Source.SConnDriver.ToString()[3..];
                                            break;
                                        case "FI":
                                            sQuery = "*." + INIP.ConnectionString_Target.SConnDriver.ToString()[3..] + ":SELECT * FROM *." + INIP.ConnectionString_Source.SConnDriver.ToString()[3..];
                                            break;
                                        case "AD":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                        case "MB":
                                            sQuery = "AnyMail@AnyProvider.com:SELECT * FROM *." + INIP.ConnectionString_Source.SConnDriver.ToString()[3..];
                                            break;
                                        case "WS":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                    }
                                    if (sQuery.Length > 0)
                                    {
                                        if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                        tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_findqueryexemple);
                                    sQuery = FuzibleController.GetExempleQuery(INIP);
                                    if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                    tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                }
                                break;
                            case "NS":
                                MessageBoxResult msgRC = MessageBox.Show(Languages.Languages.ma_msg_findqueryfullreplication01, Languages.Languages.ma_msg_findqueryfullreplication02, MessageBoxButton.YesNo);
                                if (msgRC.ToString().ToUpper().Equals("YES"))
                                {
                                    switch (INIP.ConnectionString_Target.SConnDriverSuffix)
                                    {
                                        case "FI":
                                            sQuery = "*." + INIP.ConnectionString_Target.SConnDriver.ToString()[3..] + ":SELECT * FROM %";
                                            break;
                                        case "DB":
                                            sQuery = "*:SELECT * FROM %";
                                            break;
                                        case "NS":
                                            sQuery = "*:SELECT * FROM %";
                                            break;
                                        case "AD":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                        case "MB":
                                            sQuery = "AnyMail@AnyProvider.com:SELECT * FROM %";
                                            break;
                                        case "WS":
                                            MessageBox.Show(Languages.Languages.ma_msg_findquerynotsupported);
                                            break;
                                    }
                                    if (sQuery.Length > 0)
                                    {
                                        if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                        tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(Languages.Languages.ma_msg_findqueryexemple);
                                    sQuery = FuzibleController.GetExempleQuery(INIP);
                                    if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                    tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                }
                                break;
                            default:
                                MessageBox.Show(Languages.Languages.ma_msg_findqueryexemple);
                                sQuery = FuzibleController.GetExempleQuery(INIP);
                                if (RichTB.GetTextRTB(tbQueries).Length > 0) { tbQueries.Document.ContentEnd.InsertLineBreak(); }
                                tbQueries.Document.ContentEnd.InsertTextInRun(sQuery);
                                break;
                        }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_startjobconfigureconn); }
                }
            }

            return sQ;
        }

        private string GetCurrentSqlQuery(RichTextBox rtb)
        {
            // Obtenez le TextPointer de la position actuelle du curseur
            TextPointer caretPosition = rtb.CaretPosition;

            // Obtenez le TextPointer du début de la requête SQL actuelle
            TextPointer queryStart = FindQueryStart(caretPosition, rtb);

            // Obtenez le TextPointer de la fin de la requête SQL actuelle
            TextPointer queryEnd = FindQueryEnd(caretPosition, rtb);

            // Créez un TextRange couvrant la requête SQL actuelle
            TextRange queryTextRange = new(queryStart, queryEnd);

            //une seule ligne de requête
            if (queryTextRange.Text.Trim().Length == 0)
            {
                TextPointer nextLineStart = queryStart.GetLineStartPosition(1);

                // Si la ligne actuelle n'est pas la dernière ligne, utilisez le TextPointer de début de la ligne suivante
                TextPointer lineEnd = (nextLineStart != null) ? nextLineStart.GetInsertionPosition(LogicalDirection.Backward) : queryStart.DocumentEnd;

                // Créez un TextRange couvrant le texte entre le début de la ligne actuelle et la fin de cette ligne
                TextRange textRange = new(queryStart, lineEnd);

                // Obtenez le texte du TextRange
                return textRange.Text.Trim();
            }
            else
            {
                return queryTextRange.Text.Trim();
            }
        }

        private TextPointer FindQueryStart(TextPointer position, RichTextBox rtb)
        {
            // Recherchez le début de la requête SQL en remontant depuis la position actuelle
            while (position != null && position.GetPointerContext(LogicalDirection.Backward) != TextPointerContext.None)
            {
                position = position.GetNextContextPosition(LogicalDirection.Backward);

                bool isAtLineStart = IsTextPointerAtLineStart(position);

                if (isAtLineStart) //on voit si c'est la première ligne de la requête
                {
                    string temptext = GetLineTextAtPosition(position);

                    if (Regex.Match(temptext, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                    {
                        // Trouvé le début de la requête SQL
                        return position;
                    }
                }

                // Obtenez le TextPointer du début de la ligne actuelle
                TextPointer lineStart = position.GetLineStartPosition(0);

                // Obtenez le TextPointer du début de la ligne suivante
                TextPointer nextLineStart = position.GetLineStartPosition(1);



                // Créez un TextRange couvrant la ligne actuelle
                TextRange lineTextRange = new(lineStart, nextLineStart ?? position);

                // Obtenez le texte de la ligne actuelle
                string lineText = lineTextRange.Text;

                if (Regex.Match(lineText, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                {
                    // Trouvé le début de la requête SQL
                    return lineStart;
                }
            }

            // Si le début de la requête SQL n'est pas trouvé, retournez le début du document
            return rtb.Document.ContentStart;
        }

        private TextPointer FindQueryEnd(TextPointer position, RichTextBox rtb)
        {
            position = position.GetLineStartPosition(1);

            // Recherchez la fin de la requête SQL en descendant depuis la position actuelle
            while (position != null && position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.None)
            {
                position = position.GetNextContextPosition(LogicalDirection.Forward);

                // Obtenez le TextPointer du début de la ligne actuelle
                TextPointer lineStart = position.GetLineStartPosition(0);

                // Obtenez le TextPointer du début de la ligne suivante
                TextPointer nextLineStart = position.GetLineStartPosition(1);
                TextPointer nextPosition = position.GetNextInsertionPosition(LogicalDirection.Forward);

                TextRange lineTextRange = null;

                if (nextLineStart == null && nextPosition == null)
                {
                    lineTextRange = new(lineStart, position);
                }
                // Créez un TextRange couvrant la ligne actuelle
                else { lineTextRange = new(lineStart, nextLineStart ?? nextPosition); }

                // Obtenez le texte de la ligne actuelle
                string lineText = lineTextRange.Text;

                if (Regex.Match(lineText, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase).Success)
                {
                    // Trouvé le prochain "OUTPUT:SELECT", la requête SQL se termine ici
                    return lineStart;
                }
            }

            // Si le prochain "OUTPUT:SELECT" n'est pas trouvé, retournez la fin du document
            return rtb.Document.ContentEnd;
        }

        private string GetLineTextAtPosition(TextPointer position)
        {
            if (position == null)
                return string.Empty;

            // Obtenez le TextPointer du début de la ligne actuelle
            TextPointer lineStart = position.GetLineStartPosition(0);

            // Obtenez le TextPointer du début de la ligne suivante
            TextPointer nextLineStart = position.GetLineStartPosition(1);

            // Si la ligne actuelle n'est pas la dernière ligne, utilisez le TextPointer de début de la ligne suivante comme point de fin
            TextPointer lineEnd = nextLineStart ?? position.DocumentEnd;

            // Créez un TextRange couvrant la ligne actuelle
            TextRange lineTextRange = new(lineStart, lineEnd);

            // Obtenez le texte du TextRange
            string lineText = lineTextRange.Text.Trim();

            return lineText;
        }

        private bool IsTextPointerAtLineStart(TextPointer textPointer)
        {
            // Obtenez le TextPointer du début de la ligne actuelle
            TextPointer lineStart = textPointer.GetLineStartPosition(0);

            // Comparez les deux pointeurs pour vérifier si le TextPointer est en tout début de la ligne
            return textPointer.CompareTo(lineStart) == 0;
        }

        private CONNString FindConnectionFromQueryInRichTextBox(object sender, Job INIP)
        {
            //Query sQ = FindQueryFromRichTextBox(sender, INIP);
            CONNString CSs = INIP.ConnectionString_Source;

            var rtb = (RichTextBox)sender;
            TextRange txtRange = new(rtb.Document.ContentStart, rtb.CaretPosition);
            MatchCollection mc = Regex.Matches(txtRange.Text, SHSRegex.REGEX_CROSSJOINSCRIPT, RegexOptions.IgnoreCase);
            if (mc.Count > 0)
            {
                //on prend la dernière itération et on va regarder si le curseur est bien sur une cross-query et non pas une requête simple (on aura alors dans l'itération un XXX:SELECT)
                string sLastText = txtRange.Text[mc[^1].Index..];
                if (!Regex.IsMatch(sLastText, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase)) //à priori on est sur une requête simple, donc associée à la connexion du job
                {
                    string sScriptCrossJoin;
                    sScriptCrossJoin = mc[^1].Value;
                    string sConn = "";
                    if (sScriptCrossJoin.Length > 0) { sConn = Regex.Match(sScriptCrossJoin, "\\[[0-9]+\\]").Value; }
                    if (sConn.Length > 0)
                    {
                        CSs = FuzibleController.GetConnection(sConn);
                    }
                }
            }

            return CSs;
        }

        private void CreateNewJob()
        {
            if (FuzibleController.OWN_USER)
            {
                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ma_msg_newjob01, Languages.Languages.ma_msg_newjob02, MessageBoxButton.YesNo);
                if (msgR.ToString().ToUpper().Equals("YES"))
                {
                    FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputnewjob01, Languages.Languages.ma_msg_inputnewjob02, false, true, Languages.Languages.ma_msg_inputnewjobdefault);
                    fP.ShowDialog();
                    string sNewJobName = fP.PromptUserData.Trim();
                    sNewJobName = sNewJobName.Replace("[", "(").Replace("]", ")"); // caractères réservés
                    sNewJobName = sNewJobName.Trim();

                    //vidage du richtextbox des requêtes :
                    //tbQueries.Document.Blocks.Clear();

                    if (sNewJobName.Length > 0)
                    {
                        string sStatus = FuzibleController.AddNewJob(false, null, sNewJobName);

                        if (Regex.IsMatch(sStatus, @"\[\d+\]"))
                        {
                            Job INIP = InitJobParameters(sStatus);
                            string sJobPassword = "";

                            while (sJobPassword.Length == 0)
                            {
                                sJobPassword = PromptForPassword();
                            }
                            INIP.SetJobPassword(sJobPassword, sStatus, false);
                            INIP.JobNAME = sNewJobName;
                            //sauvegarde de la nouvelle section dans le fichier INI
                            SaveJob(INIP, false);

                            cbINISection.SelectedValue = INIP.JobID;
                        }
                        else { MessageBox.Show(sStatus); }
                    }
                    else { MessageBox.Show(Languages.Languages.ma_msg_createnewjobnotvalidname); }
                }
                else
                {
                    ClearFields();
                    MessageBox.Show(Languages.Languages.ma_msg_newjobnowconfigure);
                    cbINISection.SelectedIndex = -1;
                }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_canonlymodifyme); }
        }

        private void SaveJob(Job INIP, bool bSilent)
        {
            string sStatus = "";

            if (CheckFieldsForErrors())
            {
                if (INIP.JobPassword.Length == 0)
                {
                    MessageBox.Show(Languages.Languages.ma_msg_mustsetpwd);
                    string sJobPassword = PromptForPassword();
                    INIP.SetJobPassword(sJobPassword, INIP.JobID, false);
                }

                try
                {
                    sStatus = FuzibleController.SaveConfigINIFile(INIP, RichTB.GetTextRTB(tbQueries));
                }
                catch (Exception ex)
                { MessageBox.Show(ex.Message); }
            }
            else { MessageBox.Show(Languages.Languages.ma_msg_cantsaveinputerror); }

            if (!bSilent) { MessageBox.Show(sStatus); }

            //on remet à plat la liste des jobs (au cas ou la connexion du job a changé, il faut réinitialiser les séparateurs)
            string sJobIndex = cbINISection.SelectedIndex == -1 ? INIP.JobID : cbINISection.SelectedValue.ToString();
            LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
            cbINISection.SelectedValue = sJobIndex;
            //pardon pour les 2 lignes qui suivent...
            tbDatabaseSource.Text = INIP.DatabaseName_Source;
            tbDatabaseTarget.Text = INIP.DatabaseName_Target;
        }

        private static string PromptForPassword()
        {
            FuzibleUITools.Prompt fP = new(Languages.Languages.ma_msg_inputpwd, Languages.Languages.ma_msg_inputpwd, true, true);
            fP.ShowDialog();
            string sPassword = fP.PromptUserData.Trim();

            return sPassword;
        }

        private void JobController(object sender, EventArgs e)
        {
            if (cbINISection.Items.Count == 0 && FuzibleController.UserJobsList.Count > 0)
            {
                LoadUIListSections(cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "");
            }

            //refresh des éléments UI en fonction des paramétrages
            SmartUI();

            //intellisense des requêtes
            AskIntellisenseRefresh(false);

            if (cbLogView.IsChecked.Value)
            {
                Dispatcher.Invoke(() =>
                {
                    //lbConsoleExport.Content = Monitoring.GetThreadsCPUConsumption;
                    tbConsole.Document.PageWidth = 795;
                    tbConsole.Document.Blocks.Clear();
                    TextRange tr = new TextRange(tbConsole.Document.ContentEnd, tbConsole.Document.ContentEnd);
                    tr.Text = Monitoring.InformationsRunningThreads;
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);
                    AdjustRichTextBoxWidth(tbConsole, tr.Text);
                });
            }

            if (Monitoring.TaskCancellationToken.IsCancellationRequested)
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                IsEnabled = false;
            }

            if (TUTORIAL_STEP == 0)
            {
                lbTutorial.Visibility = Visibility.Visible;
                bdTutorial.Visibility = Visibility.Visible;
                cvJobSelection.Visibility = Visibility.Hidden;
                if (cvUIButtons.Children.Contains(bdTutorial)) { cvUIButtons.Children.Add(bdTutorial); }
                lbTutorial.Text = Languages.Languages.ma_msg_tutorialinfo02 +
                                    Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo03 +
                                    Environment.NewLine + Languages.Languages.ma_msg_tutorialinfo04;
                foreach (UIElement ui in cvUI.Children)
                {
                    ui.IsEnabled = false;
                }
            }
            if (TUTORIAL_STEP > 0)
            {
                foreach (UIElement ui in cvUI.Children)
                {
                    ui.IsEnabled = true;
                }
            }
        }

        private void LaunchTimer()
        {
            TIMERCONSOLE.Tick += (sender, e) => JobController(sender, e);
            //tTimerConsole.Tick += new EventHandler(JobController);
            TimeSpan tsSpan = new(0, 0, 1);
            TIMERCONSOLE.Interval = tsSpan;
            TIMERCONSOLE.Start();

            TIMERINTELLISENSE.Tick += (sender, e) => IntellisenseInit();
            //tTimerConsole.Tick += new EventHandler(JobController);
            TimeSpan tsSpanI = new(0, 0, 5);
            TIMERINTELLISENSE.Interval = tsSpanI;
            TIMERINTELLISENSE.Start();
        }

        private void ReleaseJob()
        {
            //tExecuteTask.Abort();
            //_ctsExecuteTaskCancel.Cancel();

            Dispatcher?.Invoke(() =>
            {
                btnExecuteTask.IsEnabled = false;
                btnExecuteStepByStep.IsEnabled = false;
                btnStopTask.IsEnabled = false;
            });
            //if (TUTORIAL_MODE != 0) //en mode tuto, perte de focus pour appuyer sur "F1"
            //{
            //    lbTutorial.Text = string.Concat(Languages.Languages.ma_msg_jobstatus, FuzibleController.EXECUTETASK.Status.ToString());
            //}
            Monitoring.KillAllRunningThreads();

            Dispatcher?.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
                IsEnabled = true;
                btnExecuteTask.IsEnabled = true;
                btnExecuteStepByStep.IsEnabled = true;
                btnStopTask.IsEnabled = true;
                btnExecuteStepByStep.BorderThickness = new Thickness(1);
                btnExecuteStepByStep.BorderBrush = Brushes.DimGray;
                tbExecuteStepByStep.Text = Languages.Languages.ma_jobconf_btn_startjobstepbystep;
                btnStopTask.BorderThickness = new Thickness(1);
                btnStopTask.BorderBrush = Brushes.DimGray;
                btnExecuteTask.BorderThickness = new Thickness(1);
                btnExecuteTask.BorderBrush = Brushes.DimGray;
                tbExecuteTask.Text = Languages.Languages.ma_jobconf_btn_startjob;
                Title = Languages.Languages.ma_app_title;
                IsEnabled = true;
            });
        }

        private void ClearFields()
        {
            lbJobVersion.Content = "";

            var tbMenuS = (TabItem)tcMenu.Items[1];
            tbMenuS.Header = Languages.Languages.ma_mnu_job_source;
            var tbMenuT = (TabItem)tcMenu.Items[2];
            tbMenuT.Header = Languages.Languages.ma_mnu_job_target;

            cbNoSourceDataNoError.SelectedIndex = 1;
            cbQueryRetry.SelectedIndex = 0;

            cbINISection.IsEnabled = true;
            btnCreerNouveau.IsEnabled = true;
            btnChangePwd.IsEnabled = true;
            ckJobVisibility.IsEnabled = true;
            tbQueryVariableParameters.IsEnabled = true;
            cbNiveauLog.IsEnabled = true;
            ckSqlLog.IsEnabled = true;
            tbMailAdress.IsEnabled = true;

            ckSqlLog.IsChecked = false;
            cbNiveauLog.SelectedIndex = 1;
            cbConnSource.SelectedIndex = -1;
            cbConnTarget.SelectedIndex = -1;
            cbConnSourcePrePostCommand.SelectedIndex = -1;
            cbConnTargetPrePostCommand.SelectedIndex = -1;
            tbDatabaseSource.Text = "";
            tbDatabaseTarget.Text = "";

            tbJobDescription.Text = "";
            RichTB.SetTextRTB(tbQueryVariableParameters, "");
            tbMailAdress.Text = "";
            tbJobParameterizedLaunch.Text = "";

            ckDataReader.IsChecked = true;
            ckSQLDirectStream.IsChecked = false;
            ckSQLTrustTargetColumns.IsChecked = false;
            cbSQLDirectStreamPriority.SelectedIndex = 0;
            ckDeleteSourceSQLAfterInser.IsChecked = false;
            tbQteThreadsExport.Value = 1;
            tbQteThreadsImport.Value = 1;
            cbSQLSynchroTargetBehavior.SelectedIndex = 0;
            ckJobVisibility.IsChecked = false;
            ckSubJobAbortIfErrors.IsChecked = true;
            ckSubJobAbortIfNoData.IsChecked = true;
            ckBypassPostJobOnErrors.IsChecked = true;
            ckBlankNull.IsChecked = true;
            ckFieldAnalyzer.IsChecked = true;
            ckAllowSchemaAlterationTarget.IsChecked = false;
            cbSqlAlterOptions.SelectedIndex = 0;
            cbSqlAlterOptions.Visibility = Visibility.Hidden;
            ckTrimData.IsChecked = true;
            cbDataPivotMethod.SelectedIndex = 0;
            tbHyperfileSeparator.Text = "_";
            ckDontTransformCSVWithInvalidHeader.IsChecked = true;
            ckTransformCrossQueries.IsChecked = false;
            ckTransformAddLabelValues.IsChecked = false;
            ckAddPkAfterCreateTable.IsChecked = true;
            ckDisableConstraintsTarget.IsChecked = false;
            ckSQLBulkCopyTarget.IsChecked = false;
            ckSynchroStoreChanges.IsChecked = false;
            ckConvertHTMLEntitis.IsChecked = false;
            ckAutoTableCreation.IsChecked = true;

            cbCSVImport.SelectedIndex = 0;
            cbDropImport.SelectedIndex = 0;
            cbFieldAnalyzerLevel.SelectedIndex = 1;
            cbModeExtraction.SelectedIndex = 0;

            tbFilesZippedIn.Text = "";
            tbXSLSheetToRead.Text = "1";
            tbXLSRowOffset.Text = "0";
            tbXLSRowWriteOffset.Text = "0";
            ckXMLRemoveTagForEmptyValues.IsChecked = false;
            tbXMLHeader.Text = "xml version='1.0'";
            tbJSONHeader.Text = "";
            tbXMLRowBuilder.Text = "Row";
            tbXLSPasswordSource.Text = "";
            tbXLSPasswordTarget.Text = "";
            ckAddHeaderXLS.IsChecked = true;
            cbXMLWriteMode.SelectedIndex = 0;
            ckInterpretFormulaXLS.IsChecked = false;

            tbTargetAddDbName.Text = "DBNAME";
            tbTargetAddDtLoad.Text = "DTLOAD";
            tbTargetAddRownum.Text = "ROWNUM";

            tbJSONRowBuilder.Text = "[JOBNAME]";

            ckAppendFileCreation.IsChecked = false;
            ckAddHeaderCSV.IsChecked = true;
            tbCSVSeparator.Text = ";";
            cbCSVEncoding.SelectedIndex = 0;
            tbCSVSplitPattern.Text = "^";
            tbCSVRowOffset.Text = "0";
            tbCSVRowsPerFile.Text = "9999999";
            ckAddQuotesCSV.IsChecked = false;

            cbWSCallMethod.SelectedIndex = 0;
            cbWSCallBodyType.SelectedIndex = 0;
            cbWSSQLLanguage.SelectedIndex = 0;
            ckWSDontSendEmptyValues.IsChecked = true;
            ckWebserviceRawOutput.IsChecked = false;
            ckFileRawOutput.IsChecked = false;
            lbTargetMailHTMLTemplate.Content = "";
            cbWSTypeData.SelectedIndex = 0;
            cbWSSourcePostWork.SelectedIndex = 0;

            ckAddSqlDBNameColumn.IsChecked = false;
            ckAddSqlRowsColumn.IsChecked = false;
            ckAddSqlTimestampColumn.IsChecked = false;
            tbDynamicParamToAddInTable.Text = "";

            RichTB.SetTextRTB(tbPostJobCommandSource, "");
            RichTB.SetTextRTB(tbPostJobCommandTarget, "");
            cbPrePostJobCommands_Source.SelectedIndex = 0;
            cbPrePostJobCommands_Target.SelectedIndex = 0;
            ckPreJobCommandSource_LoopThroughResult.IsChecked = false;
            ckPreJobCommandTarget_LoopThroughResult.IsChecked = false;
            ckPreJobCommandSource_LoopThroughResult.Visibility = Visibility.Hidden;
            ckPreJobCommandTarget_LoopThroughResult.Visibility = Visibility.Hidden;

            ckWSFormatURLInUpper.IsChecked = false;
            ckWSSaveResponseFile.IsChecked = true;
            tbWSSuccessString.Text = "<OK>";
            tbWSReponsesTrackingColumn.Text = "";
            tbWSResponsesLogTable.Text = "API_Log_{%YYYY%MM%DD}";
            tbWSColumnsSendOffset.Text = "0";
            tbWSSpecialHttpParams.Text = "";
            tbWSNuxeoEndPoint.Text = "";
            lbWsJsonStructure.Content = "";

            ckMailGetUnreadOnly.IsChecked = true;
            cbMailPostWork.SelectedIndex = 0;
            tbMailMaxToRead.Text = "100";
            ckMailAssembleQueriesRecipient.IsChecked = true;
            cbMailTargetDataFormat.SelectedIndex = 0;
            tbTargetMailHTMLTemplate.Text = "";
            tbTargetMailHTMLDataKeyWord.Text = "";

            cbADSearchScope.SelectedIndex = 0;

            cbDropCollectionMongoDB.SelectedIndex = 0;
            ckAddPkAfterCreateMongoCollection.IsChecked = false;
            ckRemoveIDFromMongoDBQuery.IsChecked = false;

            cbListDynamicCommands.SelectedIndex = 0;
            var cbI = (ComboBoxItem)cbListDynamicCommands.Items[0];
            cbI.Content = "";
            //INIQ.PopulateQueriesFromFuzibleController(INIP.SQLVariablesInSourceQueries, false);
            tbQueries.Document.Blocks.Clear();

            tbLog.Foreground = System.Windows.Media.Brushes.Black;
            tbLog.FontWeight = FontWeights.Normal;

            ColorizeRichTextBox(true, true); //colorisation des requêtes
        }

        private bool CheckFieldsForErrors()
        {
            bool bOK = true;
            int iValue = 0;

            if (tbMailAdress.Text.Length > 0)
            {
                string[] sAdresses = tbMailAdress.Text.Split(Convert.ToChar(";"));
                bool bM = true;
                foreach (string sM in sAdresses) { if (!Toolbox.CheckEmailValid(sM)) { bM = false; tbMailAdress.Background = System.Windows.Media.Brushes.IndianRed; break; } }
                if (bM) { tbMailAdress.Background = System.Windows.Media.Brushes.White; }
                else { bOK = false; }
            }
            else { tbMailAdress.Background = System.Windows.Media.Brushes.White; }

            if (tbHyperfileSeparator.Text.Length == 0) { bOK = false; cbDataPivotMethod.SelectedIndex = 2; tbHyperfileSeparator.Background = System.Windows.Media.Brushes.IndianRed; }
            else { tbHyperfileSeparator.Background = System.Windows.Media.Brushes.White; }

            _ = int.TryParse(tbCSVRowOffset.Text, out iValue);
            if (iValue < 0 || iValue > 100000) { bOK = false; tbCSVRowOffset.Background = System.Windows.Media.Brushes.IndianRed; } else { tbCSVRowOffset.Background = System.Windows.Media.Brushes.White; }

            _ = int.TryParse(tbXLSRowOffset.Text, out iValue);
            if (iValue < 0 || iValue > 100000) { bOK = false; tbXLSRowOffset.Background = System.Windows.Media.Brushes.IndianRed; } else { tbXLSRowOffset.Background = System.Windows.Media.Brushes.White; }

            _ = int.TryParse(tbXLSRowWriteOffset.Text, out iValue);
            if (iValue < 0 || iValue > 9999) { bOK = false; tbXLSRowWriteOffset.Background = System.Windows.Media.Brushes.IndianRed; } else { tbXLSRowWriteOffset.Background = System.Windows.Media.Brushes.White; }

            _ = int.TryParse(tbXSLSheetToRead.Text, out iValue);
            if (iValue < 0 || iValue > 100) { bOK = false; tbXSLSheetToRead.Background = System.Windows.Media.Brushes.IndianRed; } else { tbXSLSheetToRead.Background = System.Windows.Media.Brushes.White; }

            if (tbCSVSplitPattern.Text.Length < 1 || tbCSVSplitPattern.Text.Length > 100) { bOK = false; tbCSVSplitPattern.Background = System.Windows.Media.Brushes.IndianRed; } else { tbCSVSplitPattern.Background = System.Windows.Media.Brushes.White; }

            _ = int.TryParse(tbCSVRowsPerFile.Text, out iValue);
            if (iValue < 1 || iValue > 100000000) { bOK = false; tbCSVRowsPerFile.Background = System.Windows.Media.Brushes.IndianRed; } else { tbCSVRowsPerFile.Background = System.Windows.Media.Brushes.White; }

            if (tbCSVSeparator.Text.Length < 1 || tbCSVSeparator.Text.Length > 10) { bOK = false; tbCSVSeparator.Background = System.Windows.Media.Brushes.IndianRed; } else { tbCSVSeparator.Background = System.Windows.Media.Brushes.White; }

            if (tbXMLHeader.Text.Length < 10 || tbXMLHeader.Text.Length > 100) { bOK = false; tbXMLHeader.Background = System.Windows.Media.Brushes.IndianRed; } else { tbXMLHeader.Background = System.Windows.Media.Brushes.White; }

            if (tbXMLRowBuilder.Text.IndexOf(">") > -1 || tbXMLRowBuilder.Text.IndexOf("<") > -1) { bOK = false; tbXMLRowBuilder.Background = System.Windows.Media.Brushes.IndianRed; } else { tbXMLRowBuilder.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddDbName.Text.Length == 0 && ckAddSqlDBNameColumn.IsChecked.Value) { bOK = false; tbTargetAddDbName.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddDbName.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddDtLoad.Text.Length == 0 && ckAddSqlTimestampColumn.IsChecked.Value) { bOK = false; tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddRownum.Text.Length == 0 && ckAddSqlRowsColumn.IsChecked.Value) { bOK = false; tbTargetAddRownum.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddRownum.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddDbName.Text.Length > 0 && ckAddSqlDBNameColumn.IsChecked.Value && !Regex.IsMatch(tbTargetAddDbName.Text, "^[a-zA-Z0-9_]+$")) { bOK = false; tbTargetAddDbName.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddDbName.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddDtLoad.Text.Length > 0 && ckAddSqlTimestampColumn.IsChecked.Value && !Regex.IsMatch(tbTargetAddDtLoad.Text, "^[a-zA-Z0-9_]+$")) { bOK = false; tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddRownum.Text.Length > 0 && ckAddSqlRowsColumn.IsChecked.Value && !Regex.IsMatch(tbTargetAddRownum.Text, "^[a-zA-Z0-9_]+$")) { bOK = false; tbTargetAddRownum.Background = System.Windows.Media.Brushes.IndianRed; } else { tbTargetAddRownum.Background = System.Windows.Media.Brushes.White; }

            if (tbTargetAddRownum.Text == tbTargetAddDtLoad.Text || tbTargetAddRownum.Text == tbTargetAddDbName.Text || tbTargetAddDbName.Text == tbTargetAddDtLoad.Text)
            {
                bOK = false;
                tbTargetAddRownum.Background = System.Windows.Media.Brushes.IndianRed;
                tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.IndianRed;
                tbTargetAddDbName.Background = System.Windows.Media.Brushes.IndianRed;
            }
            else
            {
                tbTargetAddRownum.Background = System.Windows.Media.Brushes.White;
                tbTargetAddDtLoad.Background = System.Windows.Media.Brushes.White;
                tbTargetAddDbName.Background = System.Windows.Media.Brushes.White;
            }

            return bOK;
        }

        private void ClearComboBoxConnection(string sComposant = null)
        {
            if (sComposant == null)
            {
                cbBDDExport.Items.Clear();
                ComboBoxItem cbNewItemB = new() { Tag = "", Content = "" }; ;
                cbBDDExport.Items.Add(cbNewItemB);

                cbBDDImport.Items.Clear();
                ComboBoxItem cbNewItemA = new() { Tag = "", Content = "" }; ;
                cbBDDImport.Items.Add(cbNewItemA);
            }
            else
            {
                switch (sComposant)
                {
                    case "cbBDDImport":
                        cbBDDImport.Items.Clear();
                        ComboBoxItem cbNewItemA = new() { Tag = "", Content = "" }; ;
                        cbBDDImport.Items.Add(cbNewItemA);
                        break;
                    case "cbBDDExport":
                        cbBDDExport.Items.Clear();
                        ComboBoxItem cbNewItemB = new() { Tag = "", Content = "" }; ;
                        cbBDDExport.Items.Add(cbNewItemB);
                        break;
                }
            }

        }

        private void SmartUI()
        {
            Title = string.Concat(Languages.Languages.ma_app_title, FuzibleController.GetInfosChargeApp());

            IsEnabled = true;

            if (RichTB.GetTextRTB(tbQueries).Length == 0)
            {
                btnExecuteTask.IsEnabled = false;
                btnExecuteStepByStep.IsEnabled = false;
            }

            if (Monitoring.GetQteThreads == 0)
            {
                btnExecuteTask.IsEnabled = true;
                btnExecuteStepByStep.IsEnabled = true;
                lbPendingTask.Content = "";
            }

            //quand il y a des requêtes avec des multi-target, on doit permettre de tout paramétrer en target
            List<string> sListDynParams;
            sListDynParams = RichTB.GetTextRTB(tbQueryVariableParameters).Split(Convert.ToChar(";")).ToList();

            if (FuzibleController.OWN_USER) // on met en évidence le côté "readonly" des fichiers INI externes
            {
                CanvasBorderC.Foreground = System.Windows.Media.Brushes.Black;
            }
            else { CanvasBorderC.Foreground = System.Windows.Media.Brushes.Red; }
            CanvasBorderC.Header = string.Concat(Languages.Languages.ma_jobconf_gb_jobselection, " [", USERNAME, "]");

            if (!FuzibleController.SQL_LOG_CONFIGURED)
            {
                ckSqlLog.Visibility = Visibility.Hidden;
            }
            else { ckSqlLog.Visibility = Visibility.Visible; }

            if (sListDynParams.Count > 0)
            {
                tbJobParameterizedLaunch.Text = string.Concat(INIProgram.APP_NAME, " \"", USERNAME, "\"", " \"", lbJobID.Content, "\"", " \"", lbJobPassword.Content, "\"", " \"", string.Join(";", sListDynParams), "\"");
            }
            else
            {
                tbJobParameterizedLaunch.Text = string.Concat(INIProgram.APP_NAME, " \"", USERNAME, "\"", " \"", lbJobID.Content, "\"", " \"", lbJobPassword.Content, "\"");
            }

            switch (cbModeExtraction.SelectedValue.ToString())
            {
                case "EXPORT_IMPORT":
                    cbSQLSynchroTargetBehavior.Visibility = Visibility.Hidden;

                    foreach (ComboBoxItem bcI in cbConnTarget.Items)
                    {
                        bcI.Visibility = Visibility.Visible;
                    }
                    break;
                case "STREAMING":
                    cbSQLSynchroTargetBehavior.Visibility = Visibility.Visible;

                    foreach (ComboBoxItem bcI in cbConnTarget.Items)
                    {
                        CONNString CS = FuzibleController.GetConnection(bcI.Tag.ToString());
                        if (CS != null && CS.SConnDriverSuffix.Equals("WS"))
                        {
                            bcI.Visibility = Visibility.Hidden;
                        }
                        if (CS != null && CS.SConnDriverSuffix.Equals("MB"))
                        {
                            bcI.Visibility = Visibility.Hidden;
                        }
                    }
                    break;
            }

            switch (cbModeExtraction.SelectedValue.ToString())
            {
                case "EXPORT_IMPORT":
                    cbADTargetBehavior.IsEnabled = true;
                    cbConnSource.IsEnabled = true;
                    cbDropImport.IsEnabled = true;
                    cbDropCollectionMongoDB.IsEnabled = true;
                    ckSynchroStoreChanges.Visibility = Visibility.Hidden;
                    ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Hidden;
                    LbJobType.Text = Languages.Languages.ma_jobconf_lbl_helpjobtype_replication;
                    break;
                case "STREAMING":
                    cbADTargetBehavior.SelectedValue = 0;
                    cbADTargetBehavior.IsEnabled = false;
                    cbConnSource.IsEnabled = true;
                    cbDropImport.SelectedValue = "NOTHING";
                    cbDropImport.IsEnabled = false;
                    cbDropCollectionMongoDB.IsEnabled = false;
                    cbDropCollectionMongoDB.SelectedValue = "NOTHING";
                    ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Visible;
                    LbJobType.Text = Languages.Languages.ma_jobconf_lbl_helpjobtype_synchro;

                    ckSynchroStoreChanges.Visibility = Visibility.Visible;
                    if (cbConnTarget.SelectedIndex > -1)
                    {
                        if (FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString()).SConnDriverSuffix.Equals("AD")) { ckSynchroStoreChanges.Visibility = Visibility.Hidden; } else { ckSynchroStoreChanges.Visibility = Visibility.Visible; }
                    }
                    break;
            }

            //UI dédiée au choix optionnel de la connexion des pre/post-commandes
            CONNString csPrePostCommandSource = null;
            try
            {
                if (cbConnSourcePrePostCommand.SelectedIndex > -1)
                {
                    csPrePostCommandSource = FuzibleController.GetConnection(cbConnSourcePrePostCommand.SelectedValue.ToString());
                }
                else if (cbConnSource.SelectedIndex > -1)
                {
                    csPrePostCommandSource = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());
                }
            }
            catch { }

            if (csPrePostCommandSource != null)
            {
                switch (csPrePostCommandSource.SConnDriverSuffix)
                {
                    case "WS":
                        CanvasBorderN.Header = Languages.Languages.ma_source_gb_prepostcmd;
                        tbPostJobCommandSource.IsEnabled = false;
                        cbPrePostJobCommands_Source.IsEnabled = false;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Hidden;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Visible;
                        tbPostJobCommandSource.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Hidden;
                        break;

                    case "NS":
                        CanvasBorderN.Header = Languages.Languages.ma_source_gb_prepostcmd_mongo;
                        tbPostJobCommandSource.IsEnabled = true;
                        cbPrePostJobCommands_Source.IsEnabled = true;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Visible;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Hidden;
                        tbPostJobCommandSource.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Visible;
                        break;

                    case "FI":
                        CanvasBorderN.Header = Languages.Languages.ma_source_gb_prepostcmd_file;
                        tbPostJobCommandSource.IsEnabled = true;
                        cbPrePostJobCommands_Source.IsEnabled = true;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Visible;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Hidden;
                        tbPostJobCommandSource.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Visible;
                        break;

                    case "DB":
                        CanvasBorderN.Header = Languages.Languages.ma_target_gb_prepostcmd_sql;
                        tbPostJobCommandSource.IsEnabled = true;
                        cbPrePostJobCommands_Source.IsEnabled = true;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Visible;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Hidden;
                        tbPostJobCommandSource.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Visible;
                        break;

                    case "MB":
                        CanvasBorderN.Header = Languages.Languages.ma_source_gb_prepostcmd;
                        tbPostJobCommandSource.IsEnabled = true;
                        cbPrePostJobCommands_Source.IsEnabled = true;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Hidden;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Hidden;
                        tbPostJobCommandSource.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Hidden;
                        break;

                    case "AD":
                        CanvasBorderN.Header = Languages.Languages.ma_source_gb_prepostcmd;
                        tbPostJobCommandSource.IsEnabled = false;
                        cbPrePostJobCommands_Source.IsEnabled = false;
                        cbPrePostJobCommands_Source.Visibility = Visibility.Hidden;
                        CanvasWebservicePrePostOperation.Visibility = Visibility.Hidden;
                        tbPostJobCommandSource.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsSource.Visibility = Visibility.Hidden;
                        break;
                }
            }

            //UI pour la connexion source
            if (cbConnSource.SelectedIndex > -1)
            {
                CONNString csS = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());
                var tbMenu = (TabItem)tcMenu.Items[1];
                tbMenu.Header = string.Concat(Languages.Languages.ma_mnu_job_source, " : ", FuzibleController.GetSourceTargetLabel(cbConnSource.SelectedValue.ToString()));

                string sSQL = csS.SConnSQLLangage;

                //gestion des languages SQL pourris des webservices (salesforce...)
                if (csS.SConnDriverSuffix.Equals("WS") && cbWSSQLLanguage.SelectedIndex > 0)
                {
                    sSQL = cbWSSQLLanguage.SelectedValue.ToString();
                }

                if (csS.SConnDriverSuffix.Equals("DB") ||
                    csS.SConnDriverSuffix.Equals("AD") ||
                    csS.SConnDriverSuffix.Equals("MB") ||
                    csS.SConnDriverSuffix.Equals("NS"))
                { ckFileRawOutput.IsChecked = false; ckWebserviceRawOutput.IsChecked = false; }

                tiJobQueries.Header = string.Concat(tiJobQueries.Header.ToString().Split("(")[0].Trim(), " (", sSQL, ")");
                txtQueryBackground.Text = string.Concat(sSQL, Environment.NewLine, Environment.NewLine, Languages.Languages.ma_queries_lbl_rightclick);

                switch (csS.SConnDriverSuffix)
                {
                    case "WS":

                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection;
                        lbExportDatabse.Content = "";
                        lbExportDatabse.Visibility = Visibility.Hidden;

                        tbDatabaseSource.Visibility = Visibility.Hidden;
                        cbBDDExport.Visibility = Visibility.Hidden;
                        ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Visible;

                        if (csS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE).Equals("ITOP"))
                        {
                            cbWSSQLLanguage.SelectedValue = "OQL";
                            cbWSSQLLanguage.IsEnabled = true;
                            CanvasWebserviceNUXEOSource.Visibility = Visibility.Hidden;
                            CanvasWebserviceRESTSource.Visibility = Visibility.Visible;
                            ckSynchroBypassFiltersInTargetQuery.IsChecked = true;
                            ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Hidden;
                        }
                        else if (csS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE).Equals("SALESFORCE_SOQL"))
                        {
                            cbWSSQLLanguage.SelectedValue = "SOQL";
                            cbWSSQLLanguage.IsEnabled = false;
                            CanvasWebserviceNUXEOSource.Visibility = Visibility.Hidden;
                            CanvasWebserviceRESTSource.Visibility = Visibility.Visible;
                            ckSynchroBypassFiltersInTargetQuery.IsChecked = true;
                            ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Hidden;
                        }
                        else if (csS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE).Equals("NUXEO"))
                        {
                            cbWSSQLLanguage.SelectedValue = "NXQL";
                            cbWSSQLLanguage.IsEnabled = false;
                            CanvasWebserviceNUXEOSource.Visibility = Visibility.Visible;
                            CanvasWebserviceRESTSource.Visibility = Visibility.Hidden;
                            ckSynchroBypassFiltersInTargetQuery.IsChecked = true;
                            ckSynchroBypassFiltersInTargetQuery.Visibility = Visibility.Hidden;
                        }
                        else if (csS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE).Equals("SALESFORCE"))
                        {
                            cbWSSQLLanguage.SelectedValue = "FUZIBLE_SQL";
                            cbWSSQLLanguage.IsEnabled = false;
                            CanvasWebserviceNUXEOSource.Visibility = Visibility.Hidden;
                            CanvasWebserviceRESTSource.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            cbWSSQLLanguage.SelectedValue = "FUZIBLE_SQL";
                            cbWSSQLLanguage.IsEnabled = true;
                            CanvasWebserviceNUXEOSource.Visibility = Visibility.Hidden;
                            CanvasWebserviceRESTSource.Visibility = Visibility.Visible;
                        }


                        if (cbWSTypeData.SelectedValue.ToString().Contains("_FILE_"))
                        {
                            ckWebserviceRawOutput.IsChecked = true;
                        }

                        if (csS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                        {
                            cbWSTypeData.SelectedValue = "LOCKED_BY_TEMPLATE";
                            cbWSCallMethod.SelectedValue = "GET";
                            cbWSCallBodyType.SelectedValue = "RAW_JSON";
                            //ckWebserviceRawOutput.IsChecked = false;
                        }
                        break;

                    case "NS":
                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection_sql;
                        lbExportDatabse.Content = Languages.Languages.ma_source_lbl_databases;
                        lbExportDatabse.Visibility = Visibility.Visible;
                        tbDatabaseSource.Visibility = Visibility.Visible;
                        cbBDDExport.Visibility = Visibility.Visible;
                        break;

                    case "FI":
                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection_file;
                        lbExportDatabse.Content = Languages.Languages.ma_source_lbl_databases_file;
                        lbExportDatabse.Visibility = Visibility.Hidden;
                        tbDatabaseSource.Visibility = Visibility.Hidden;
                        cbBDDExport.Visibility = Visibility.Hidden;
                        break;

                    case "DB":
                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection_sql;
                        lbExportDatabse.Content = Languages.Languages.ma_source_lbl_databases;
                        lbExportDatabse.Visibility = Visibility.Visible;
                        tbDatabaseSource.Visibility = Visibility.Visible;
                        cbBDDExport.Visibility = Visibility.Visible;
                        break;

                    case "MB":
                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection_mb;
                        lbExportDatabse.Content = "";
                        lbExportDatabse.Visibility = Visibility.Hidden;
                        tbDatabaseSource.Visibility = Visibility.Hidden;
                        cbBDDExport.Visibility = Visibility.Hidden;
                        break;

                    case "AD":
                        btnTestBDDExport.Content = Languages.Languages.ma_source_btn_tryconnection_ad;
                        lbExportDatabse.Content = "";
                        lbExportDatabse.Visibility = Visibility.Hidden;
                        tbDatabaseSource.Visibility = Visibility.Hidden;
                        cbBDDExport.Visibility = Visibility.Hidden;
                        break;
                }
            }


            //UI dédiée au choix optionnel de la connexion des pre/post-commandes
            CONNString csPrePostCommandTarget = null;
            try
            {
                if (cbConnTargetPrePostCommand.SelectedIndex > -1)
                {
                    csPrePostCommandTarget = FuzibleController.GetConnection(cbConnTargetPrePostCommand.SelectedValue.ToString());
                }
                else if (cbConnTarget.SelectedIndex > -1)
                {
                    csPrePostCommandTarget = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());
                }
            }
            catch { }

            if (csPrePostCommandTarget != null)
            {
                switch (csPrePostCommandTarget.SConnDriverSuffix)
                {
                    case "WS":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcommands;
                        tbPostJobCommandTarget.IsEnabled = false;
                        cbPrePostJobCommands_Target.IsEnabled = false;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Hidden;
                        tbPostJobCommandTarget.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Hidden;
                        break;

                    case "NS":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcmd_mongo;
                        tbPostJobCommandTarget.IsEnabled = true;
                        cbPrePostJobCommands_Target.IsEnabled = true;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Visible;
                        tbPostJobCommandTarget.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Visible;
                        break;

                    case "FI":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcmd_file;
                        tbPostJobCommandTarget.IsEnabled = true;
                        cbPrePostJobCommands_Target.IsEnabled = true;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Visible;
                        tbPostJobCommandTarget.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Visible;
                        break;

                    case "DB":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcmd_sql;
                        tbPostJobCommandTarget.IsEnabled = true;
                        cbPrePostJobCommands_Target.IsEnabled = true;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Visible;
                        tbPostJobCommandTarget.Visibility = Visibility.Visible;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Visible;
                        break;

                    case "MB":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcommands;
                        tbPostJobCommandTarget.IsEnabled = true;
                        cbPrePostJobCommands_Target.IsEnabled = true;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Hidden;
                        tbPostJobCommandTarget.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Hidden;
                        break;

                    case "AD":
                        CanvasBorderM.Header = Languages.Languages.ma_target_gb_prepostcommands;
                        tbPostJobCommandTarget.IsEnabled = false;
                        cbPrePostJobCommands_Target.IsEnabled = false;
                        cbPrePostJobCommands_Target.Visibility = Visibility.Hidden;
                        tbPostJobCommandTarget.Visibility = Visibility.Hidden;
                        btnHelpForPrePostCommandsTarget.Visibility = Visibility.Hidden;
                        break;
                }
            }

            //UI dédié à la connexion cible
            if (cbConnTarget.SelectedIndex > -1)
            {
                var tbMenu = (TabItem)tcMenu.Items[2];
                tbMenu.Header = string.Concat(Languages.Languages.ma_mnu_job_target, " : ", FuzibleController.GetSourceTargetLabel(cbConnTarget.SelectedValue.ToString()));

                CONNString CSt = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());
                switch (CSt.SConnDriverSuffix)
                {
                    case "WS":
                        //CanvasWebserviceTarget.Visibility = Visibility.Visible;
                        //CanvasFileTarget.Visibility = Visibility.Hidden;
                        //CanvasMailTarget.Visibility = Visibility.Hidden;
                        //CanvasSQLTarget.Visibility = Visibility.Hidden;
                        //CanvasADTarget.Visibility = Visibility.Hidden;
                        //CanvasNOSQLTarget.Visibility = Visibility.Hidden;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_ws;
                        lbImportDatabse.Content = "";
                        lbImportDatabse.Visibility = Visibility.Hidden;

                        tbDatabaseTarget.Visibility = Visibility.Hidden;
                        cbBDDImport.Visibility = Visibility.Hidden;

                        LbParallelInsertion.Visibility = Visibility.Hidden;
                        tbQteThreadsImport.Visibility = Visibility.Hidden;
                        btnHelpForParallelInsert.Visibility = Visibility.Hidden;
                        //cbBDDImport.IsEnabled = false;
                        //cbBDDImport.Items.Clear();
                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_ws;

                        if (ckWSSaveResponseFile.IsChecked.Value)
                        {
                            LbWSLogTable.Visibility = Visibility.Visible;
                            LbWSLogTrackingColumns.Visibility = Visibility.Visible;
                            tbWSReponsesTrackingColumn.Visibility = Visibility.Visible;
                            tbWSResponsesLogTable.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            LbWSLogTable.Visibility = Visibility.Hidden;
                            LbWSLogTrackingColumns.Visibility = Visibility.Hidden;
                            tbWSReponsesTrackingColumn.Visibility = Visibility.Hidden;
                            tbWSResponsesLogTable.Visibility = Visibility.Hidden;
                        }

                        if (CSt.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                        {
                            ckFileRawOutput.IsChecked = true;
                            ckWebserviceRawOutput.IsChecked = true;
                            tbWSColumnsSendOffset.Text = "0";
                            cbWSCallMethodTarget.SelectedValue = "PUT";
                            cbWSContentTypeTarget.SelectedValue = "BINARY";
                            lbWsJsonStructure.Content = "[RAWDATA_BINARY]";
                            cbWSCallMethodTarget.IsEnabled = false;
                            cbWSContentTypeTarget.IsEnabled = false;
                            btnDefineWsContentStructure.IsEnabled = false;
                        }
                        else
                        {
                            cbWSCallMethodTarget.IsEnabled = true;
                            cbWSContentTypeTarget.IsEnabled = true;
                            btnDefineWsContentStructure.IsEnabled = true;
                        }

                        if (cbWSContentTypeTarget.SelectedValue != null)
                        {
                            if (cbWSContentTypeTarget.SelectedValue.ToString().Equals("HTTP_PARAMS"))
                            {
                                CanvasWebserviceHttpParamsSettings.Visibility = Visibility.Visible;
                                CanvasWebserviceHttpParamsSpecial.Visibility = Visibility.Hidden;
                            }
                            if (cbWSContentTypeTarget.SelectedValue.ToString().Equals("BINARY"))
                            {
                                CanvasWebserviceHttpParamsSettings.Visibility = Visibility.Hidden;
                                CanvasWebserviceHttpParamsSpecial.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                CanvasWebserviceHttpParamsSettings.Visibility = Visibility.Hidden;
                                CanvasWebserviceHttpParamsSpecial.Visibility = Visibility.Hidden;
                            }


                        }

                        break;

                    case "FI":
                        //CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        //CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        //CanvasFileTarget.Visibility = Visibility.Visible;
                        //CanvasMailTarget.Visibility = Visibility.Hidden;
                        //CanvasSQLTarget.Visibility = Visibility.Hidden;
                        //CanvasADTarget.Visibility = Visibility.Hidden;
                        //CanvasNOSQLTarget.Visibility = Visibility.Hidden;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_file;
                        lbImportDatabse.Content = Languages.Languages.ma_target_lbl_databases_file;
                        lbImportDatabse.Visibility = Visibility.Hidden;

                        tbDatabaseTarget.Visibility = Visibility.Hidden;
                        cbBDDImport.Visibility = Visibility.Hidden;

                        LbParallelInsertion.Visibility = Visibility.Hidden;
                        tbQteThreadsImport.Visibility = Visibility.Hidden;
                        btnHelpForParallelInsert.Visibility = Visibility.Hidden;

                        //cbBDDImport.IsEnabled = false;
                        //cbBDDImport.Items.Clear();
                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_file;
                        break;

                    case "DB":
                        //CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        //CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        //CanvasFileTarget.Visibility = Visibility.Hidden;
                        //CanvasMailTarget.Visibility = Visibility.Hidden;
                        //CanvasSQLTarget.Visibility = Visibility.Visible;
                        //CanvasADTarget.Visibility = Visibility.Hidden;
                        //CanvasNOSQLTarget.Visibility = Visibility.Hidden;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_sql;
                        lbImportDatabse.Content = Languages.Languages.ma_target_lbl_databases;
                        lbImportDatabse.Visibility = Visibility.Visible;

                        tbDatabaseTarget.Visibility = Visibility.Visible;
                        cbBDDImport.Visibility = Visibility.Visible;

                        LbParallelInsertion.Visibility = Visibility.Visible;
                        tbQteThreadsImport.Visibility = Visibility.Visible;
                        btnHelpForParallelInsert.Visibility = Visibility.Visible;

                        //cbBDDImport.IsEnabled = true;
                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_sql;
                        break;

                    case "NS":
                        //CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        //CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        //CanvasFileTarget.Visibility = Visibility.Hidden;
                        //CanvasMailTarget.Visibility = Visibility.Hidden;
                        //CanvasSQLTarget.Visibility = Visibility.Hidden;
                        //CanvasADTarget.Visibility = Visibility.Hidden;
                        //CanvasNOSQLTarget.Visibility = Visibility.Visible;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_sql;
                        lbImportDatabse.Content = Languages.Languages.ma_target_lbl_databases;
                        lbImportDatabse.Visibility = Visibility.Visible;

                        tbDatabaseTarget.Visibility = Visibility.Visible;
                        cbBDDImport.Visibility = Visibility.Visible;

                        LbParallelInsertion.Visibility = Visibility.Visible;
                        tbQteThreadsImport.Visibility = Visibility.Visible;
                        btnHelpForParallelInsert.Visibility = Visibility.Visible;

                        //cbBDDImport.IsEnabled = true;
                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_mongo;
                        break;

                    case "MB":
                        //CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        //CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        //CanvasFileTarget.Visibility = Visibility.Hidden;
                        //CanvasMailTarget.Visibility = Visibility.Visible;
                        //CanvasSQLTarget.Visibility = Visibility.Hidden;
                        //CanvasADTarget.Visibility = Visibility.Hidden;
                        //CanvasNOSQLTarget.Visibility = Visibility.Hidden;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_mb;
                        lbImportDatabse.Content = "";
                        lbImportDatabse.Visibility = Visibility.Hidden;

                        tbDatabaseTarget.Visibility = Visibility.Hidden;
                        cbBDDImport.Visibility = Visibility.Hidden;

                        LbParallelInsertion.Visibility = Visibility.Hidden;
                        tbQteThreadsImport.Visibility = Visibility.Hidden;
                        btnHelpForParallelInsert.Visibility = Visibility.Hidden;

                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_mb;
                        break;

                    case "AD":
                        //CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        //CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        //CanvasFileTarget.Visibility = Visibility.Hidden;
                        //CanvasMailTarget.Visibility = Visibility.Hidden;
                        //CanvasSQLTarget.Visibility = Visibility.Hidden;
                        //CanvasADTarget.Visibility = Visibility.Visible;
                        //CanvasNOSQLTarget.Visibility = Visibility.Hidden;

                        btnTestBDDImport.Content = Languages.Languages.ma_target_btn_tryconnection_ad;
                        lbImportDatabse.Content = "";
                        lbImportDatabse.Visibility = Visibility.Hidden;

                        tbDatabaseTarget.Visibility = Visibility.Hidden;
                        cbBDDImport.Visibility = Visibility.Hidden;

                        LbParallelInsertion.Visibility = Visibility.Hidden;
                        tbQteThreadsImport.Visibility = Visibility.Hidden;
                        btnHelpForParallelInsert.Visibility = Visibility.Hidden;

                        lbQueryAutoComplete.Content = Languages.Languages.ma_queries_lbl_howtoquery_ad;
                        break;
                }
            }

            if (cbWSSQLLanguage.SelectedIndex == 0) //fuzible SQL
            {
                lbWSCallBodyType.Visibility = Visibility.Visible;
                cbWSCallBodyType.Visibility = Visibility.Visible;
                lbWSCallMethod.Visibility = Visibility.Visible;
                cbWSCallMethod.Visibility = Visibility.Visible;
            }
            else
            {
                lbWSCallBodyType.Visibility = Visibility.Hidden;
                cbWSCallBodyType.Visibility = Visibility.Hidden;
                lbWSCallMethod.Visibility = Visibility.Hidden;
                cbWSCallMethod.Visibility = Visibility.Hidden;
            }

            switch (cbXMLWriteMode.SelectedIndex)
            {
                case 0:
                    ckAddCDataXML.Visibility = Visibility.Visible;
                    break;
                case 1:
                    ckAddCDataXML.Visibility = Visibility.Hidden;
                    ckAddCDataXML.IsChecked = false;
                    break;
            }

            switch (cbDataPivotMethod.SelectedValue.ToString())
            {
                case "1":
                    tbHyperfileSeparator.Visibility = Visibility.Visible;
                    ckDontTransformCSVWithInvalidHeader.Visibility = Visibility.Visible;
                    ckTransformAddLabelValues.Visibility = Visibility.Hidden;
                    ckTransformCrossQueries.Visibility = Visibility.Visible;
                    break;
                case "2":
                    tbHyperfileSeparator.Visibility = Visibility.Visible;
                    ckDontTransformCSVWithInvalidHeader.Visibility = Visibility.Visible;
                    ckTransformAddLabelValues.Visibility = Visibility.Hidden;
                    ckTransformCrossQueries.Visibility = Visibility.Visible;
                    break;
                case "3":
                    tbHyperfileSeparator.Visibility = Visibility.Hidden;
                    ckDontTransformCSVWithInvalidHeader.Visibility = Visibility.Hidden;
                    ckTransformAddLabelValues.Visibility = Visibility.Visible;
                    ckTransformCrossQueries.Visibility = Visibility.Visible;
                    break;
                case "0":
                    tbHyperfileSeparator.Visibility = Visibility.Hidden;
                    ckDontTransformCSVWithInvalidHeader.Visibility = Visibility.Hidden;
                    ckTransformAddLabelValues.Visibility = Visibility.Hidden;
                    ckTransformCrossQueries.Visibility = Visibility.Hidden;
                    break;
            }

            if (!ckFieldAnalyzer.IsChecked.Value)
            {
                ckAddPkAfterCreateTable.IsEnabled = false;
                ckAddPkAfterCreateTable.IsChecked = false;
                ckAllowSchemaAlterationTarget.IsEnabled = false;
                ckAllowSchemaAlterationTarget.IsChecked = false;
                cbSqlAlterOptions.Visibility = Visibility.Hidden;
            }
            else
            {
                ckAddPkAfterCreateTable.IsEnabled = true;
                ckAllowSchemaAlterationTarget.IsEnabled = true;
            }

            //colorisation de l'onglet LOG si la job a des erreurs
            if (!FuzibleController.LOG.HasNoErrors)
            {
                var tbLog = (TabItem)tcMenu.Items[4];
                tbLog.Foreground = DateTime.Now.Second % 2 == 0 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Black;
                tbLog.FontWeight = FontWeights.Bold;
            }
            else
            {
                var tbLog = (TabItem)tcMenu.Items[4];
                tbLog.Foreground = System.Windows.Media.Brushes.Black;
                tbLog.FontWeight = FontWeights.Normal;
            }


            //en SQLite, ou certaines connexions ODBC on ne peut pas avoir de modification des contraintes
            if (cbConnTarget.SelectedIndex > -1)
            {
                CONNString CSt = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString());
                if (CSt.SConnDriverSuffix.Equals("DB") && cbDriverTarget.SelectedIndex > -1 && cbDriverTarget.SelectedValue.Equals(CSt.SConnDriver.ToString()))
                {
                    if (CSt.GetParam(SQLTools_Enums.DRIVER_PARAMS.DISABLE_TABLE_CONSTRAINTS).Length == 0)
                    {
                        ckDisableConstraintsTarget.IsChecked = false;
                        ckDisableConstraintsTarget.IsEnabled = false;
                        ckDisableConstraintsTarget.FontStyle = FontStyles.Italic;
                    }
                    else
                    {
                        ckDisableConstraintsTarget.IsEnabled = true;
                        ckDisableConstraintsTarget.FontStyle = FontStyles.Normal;
                    }
                }
                if (CSt.SConnDriverSuffix.Equals("DB"))
                {
                    if (ckDataReader.IsChecked.Value)
                    {
                        ckSQLTrustTargetColumns.Visibility = Visibility.Visible; ckSQLTrustTargetColumns.IsEnabled = true;
                    }
                    else
                    {
                        ckSQLTrustTargetColumns.Visibility = Visibility.Hidden; ckSQLTrustTargetColumns.IsEnabled = false;
                    }
                }
            }

            if (cbINISection.SelectedIndex > -1)
            {
                List<Job> ListJob = new();
                if (FuzibleController.GetJob(cbINISection.SelectedValue.ToString()) != null) { ListJob = FuzibleController.GetJobSteps(cbINISection.SelectedValue.ToString()); }

                if (ListJob.Count > 0)
                {
                    ckSubJobAbortIfErrors.Visibility = Visibility.Visible;
                    ckSubJobAbortIfNoData.Visibility = Visibility.Visible;
                    TbPreviousStepJob.Visibility = Visibility.Visible;
                    LbJobStep.Visibility = Visibility.Visible;
                    TbNextStepJob.Content = Languages.Languages.ma_jobconf_btn_nextstep;
                }
                else
                {
                    ckSubJobAbortIfErrors.Visibility = Visibility.Hidden;
                    ckSubJobAbortIfNoData.Visibility = Visibility.Hidden;
                    TbPreviousStepJob.Visibility = Visibility.Hidden;
                    LbJobStep.Visibility = Visibility.Hidden;
                    TbNextStepJob.Content = Languages.Languages.ma_jobconf_btn_nextstep_newstep;
                }
            }
            else
            {
                ckSubJobAbortIfErrors.Visibility = Visibility.Hidden;
                ckSubJobAbortIfNoData.Visibility = Visibility.Hidden;
                TbPreviousStepJob.Visibility = Visibility.Hidden;
                LbJobStep.Visibility = Visibility.Hidden;
                TbNextStepJob.Content = Languages.Languages.ma_jobconf_btn_nextstep_newstep;
            }

            if (ckDataReader.IsChecked.Value)
            {
                ckSQLDirectStream.Visibility = Visibility.Visible; ckSQLDirectStream.IsEnabled = true;
                cbSQLDirectStreamPriority.Visibility = Visibility.Visible; cbSQLDirectStreamPriority.IsEnabled = true;

            }
            else
            {
                ckSQLDirectStream.Visibility = Visibility.Hidden; ckSQLDirectStream.IsEnabled = false; ckSQLDirectStream.IsChecked = false;
                cbSQLDirectStreamPriority.Visibility = Visibility.Hidden; cbSQLDirectStreamPriority.IsEnabled = false;
            }

            //impossible de gérer le mode DELETE quand on ne traite que par petits bouts de flux
            if (cbModeExtraction.SelectedValue.ToString().Equals("STREAMING"))
            {
                switch (cbSQLSynchroTargetBehavior.SelectedValue.ToString())
                {
                    case "DI":
                        ckSQLDirectStream.IsEnabled = false;
                        ckSQLDirectStream.IsChecked = false;
                        ckSQLDirectStream.FontStyle = FontStyles.Italic;
                        cbSQLDirectStreamPriority.IsEnabled = false;
                        break;
                    case "DUI":
                        ckSQLDirectStream.IsEnabled = false;
                        ckSQLDirectStream.IsChecked = false;
                        ckSQLDirectStream.FontStyle = FontStyles.Italic;
                        cbSQLDirectStreamPriority.IsEnabled = false;
                        break;
                    case "TAG":
                        ckSQLDirectStream.IsEnabled = false;
                        ckSQLDirectStream.IsChecked = false;
                        ckSQLDirectStream.FontStyle = FontStyles.Italic;
                        cbSQLDirectStreamPriority.IsEnabled = false;
                        break;
                    default:
                        ckSQLDirectStream.FontStyle = FontStyles.Normal;
                        break;
                }
            }

            //gestion des pré-post commandes et de l'option loop through rows
            if (RichTB.GetTextRTB(tbPostJobCommandSource).Length > 0 && cbPrePostJobCommands_Source.SelectedValue.ToString().Equals("PRE_JOB_COMMANDS"))
            {
                string[] sListPrePostCommands = RichTB.GetTextRTB(tbPostJobCommandSource).Replace("\\r", " ").Replace("\\n", " ").Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                if (sListPrePostCommands.Length == 1 && ckPreJobCommandTarget_LoopThroughResult.IsChecked == false)
                {
                    ckPreJobCommandSource_LoopThroughResult.Visibility = Visibility.Visible;
                    //ckPreJobCommandTarget_LoopThroughResult.Visibility = Visibility.Hidden;
                }
                else
                {
                    ckPreJobCommandSource_LoopThroughResult.Visibility = Visibility.Hidden;
                    ckPreJobCommandSource_LoopThroughResult.IsChecked = false;
                }
            }
            else
            {
                ckPreJobCommandSource_LoopThroughResult.Visibility = Visibility.Hidden;
                ckPreJobCommandSource_LoopThroughResult.IsChecked = false;
            }

            if (RichTB.GetTextRTB(tbPostJobCommandTarget).Length > 0 && cbPrePostJobCommands_Target.SelectedValue.ToString().Equals("PRE_JOB_COMMANDS"))
            {
                string[] sListPrePostCommands = RichTB.GetTextRTB(tbPostJobCommandTarget).Replace("\\r", " ").Replace("\\n", " ").Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                if (sListPrePostCommands.Length == 1 && ckPreJobCommandSource_LoopThroughResult.IsChecked == false)
                {
                    ckPreJobCommandTarget_LoopThroughResult.Visibility = Visibility.Visible;
                    //ckPreJobCommandSource_LoopThroughResult.Visibility = Visibility.Hidden;
                }
                else
                {
                    ckPreJobCommandTarget_LoopThroughResult.Visibility = Visibility.Hidden;
                    ckPreJobCommandTarget_LoopThroughResult.IsChecked = false;
                }
            }
            else
            {
                ckPreJobCommandTarget_LoopThroughResult.Visibility = Visibility.Hidden;
                ckPreJobCommandTarget_LoopThroughResult.IsChecked = false;
            }

            if (cbMailTargetDataFormat.SelectedValue != null && cbMailTargetDataFormat.SelectedValue.ToString().Equals("HTML_TABLE"))
            {
                tbTargetMailHTMLDataKeyWord.Visibility = Visibility.Visible;
                tbTargetMailHTMLTemplate.Visibility = Visibility.Visible;
                btnChooseHTMLTemplate.Visibility = Visibility.Visible;
                lbTargetMailHTMLDataKeyWord.Visibility = Visibility.Visible;
                lbTargetMailHTMLTemplate.Visibility = Visibility.Visible;
                btnHelpForHTMLTemplate.Visibility = Visibility.Visible;
            }
            else
            {
                tbTargetMailHTMLDataKeyWord.Visibility = Visibility.Hidden;
                tbTargetMailHTMLTemplate.Visibility = Visibility.Hidden;
                btnChooseHTMLTemplate.Visibility = Visibility.Hidden;
                lbTargetMailHTMLDataKeyWord.Visibility = Visibility.Hidden;
                lbTargetMailHTMLTemplate.Visibility = Visibility.Hidden;
                tbTargetMailHTMLDataKeyWord.Text = "";
                tbTargetMailHTMLTemplate.Text = "";
                btnHelpForHTMLTemplate.Visibility = Visibility.Hidden;
            }

            //en mode raw file -> file, on fait simplement une copie de fichier... pas d'option
            ckTrimData.Visibility = Visibility.Visible;
            CanvasOptionalColumnsTarget.Visibility = Visibility.Visible;
            lbRawOutput.Visibility = Visibility.Hidden;
            lbMailTargetDataFormat.Visibility = Visibility.Visible;
            cbMailTargetDataFormat.Visibility = Visibility.Visible;

            if (cbDriverSource.SelectedIndex > -1)
            {
                switch (cbDriverSource.SelectedValue.ToString()[..2])
                {
                    case "WS":
                        CanvasWebserviceSource.Visibility = Visibility.Visible;
                        CanvasFileSource.Visibility = Visibility.Hidden;
                        CanvasMailSource.Visibility = Visibility.Hidden;
                        CanvasSQLSource.Visibility = Visibility.Hidden;
                        CanvasADSource.Visibility = Visibility.Hidden;
                        CanvasNOSQLSource.Visibility = Visibility.Hidden;
                        break;

                    case "FI":
                        CanvasWebserviceSource.Visibility = Visibility.Hidden;
                        CanvasFileSource.Visibility = Visibility.Visible;
                        CanvasMailSource.Visibility = Visibility.Hidden;
                        CanvasSQLSource.Visibility = Visibility.Hidden;
                        CanvasADSource.Visibility = Visibility.Hidden;
                        CanvasNOSQLSource.Visibility = Visibility.Hidden;
                        break;

                    case "DB":
                        CanvasWebserviceSource.Visibility = Visibility.Hidden;
                        CanvasFileSource.Visibility = Visibility.Hidden;
                        CanvasMailSource.Visibility = Visibility.Hidden;
                        CanvasSQLSource.Visibility = Visibility.Visible;
                        CanvasADSource.Visibility = Visibility.Hidden;
                        CanvasNOSQLSource.Visibility = Visibility.Hidden;
                        break;

                    case "NS":
                        CanvasWebserviceSource.Visibility = Visibility.Hidden;
                        CanvasFileSource.Visibility = Visibility.Hidden;
                        CanvasMailSource.Visibility = Visibility.Hidden;
                        CanvasSQLSource.Visibility = Visibility.Hidden;
                        CanvasADSource.Visibility = Visibility.Hidden;
                        CanvasNOSQLSource.Visibility = Visibility.Visible;
                        break;

                    case "MB":
                        CanvasWebserviceSource.Visibility = Visibility.Hidden;
                        CanvasFileSource.Visibility = Visibility.Hidden;
                        CanvasMailSource.Visibility = Visibility.Visible;
                        CanvasSQLSource.Visibility = Visibility.Hidden;
                        CanvasADSource.Visibility = Visibility.Hidden;
                        CanvasNOSQLSource.Visibility = Visibility.Hidden;
                        break;

                    case "AD":
                        CanvasWebserviceSource.Visibility = Visibility.Hidden;
                        CanvasFileSource.Visibility = Visibility.Hidden;
                        CanvasMailSource.Visibility = Visibility.Hidden;
                        CanvasSQLSource.Visibility = Visibility.Hidden;
                        CanvasADSource.Visibility = Visibility.Visible;
                        CanvasNOSQLSource.Visibility = Visibility.Hidden;
                        break;

                }

                switch (cbDriverSource.SelectedValue.ToString())
                {
                    case "FI_CSV":
                        CanvasFileSourceCSV.Visibility = Visibility.Visible;
                        CanvasFileSourceXLS.Visibility = Visibility.Hidden;
                        CanvasFileSourceXML.Visibility = Visibility.Hidden;
                        CanvasFileSourceJSON.Visibility = Visibility.Hidden;
                        break;
                    case "FI_XLS":
                        CanvasFileSourceCSV.Visibility = Visibility.Hidden;
                        CanvasFileSourceXLS.Visibility = Visibility.Visible;
                        CanvasFileSourceXML.Visibility = Visibility.Hidden;
                        CanvasFileSourceJSON.Visibility = Visibility.Hidden;
                        break;
                    case "FI_JSON":
                        CanvasFileSourceJSON.Visibility = Visibility.Visible;
                        CanvasFileSourceCSV.Visibility = Visibility.Hidden;
                        CanvasFileSourceXLS.Visibility = Visibility.Hidden;
                        CanvasFileSourceXML.Visibility = Visibility.Hidden;
                        break;
                    case "FI_XML":
                        CanvasFileSourceCSV.Visibility = Visibility.Hidden;
                        CanvasFileSourceXLS.Visibility = Visibility.Hidden;
                        CanvasFileSourceXML.Visibility = Visibility.Visible;
                        CanvasFileSourceJSON.Visibility = Visibility.Hidden;
                        break;
                    case "WS_REST":
                        CanvasWebserviceRESTSource.Visibility = Visibility.Visible;
                        //CanvasWebserviceNUXEOSource.Visibility = Visibility.Visible;
                        break;
                    default:
                        CanvasFileSourceCSV.Visibility = Visibility.Hidden;
                        CanvasFileSourceXLS.Visibility = Visibility.Hidden;
                        CanvasFileSourceXML.Visibility = Visibility.Hidden;
                        CanvasFileSourceJSON.Visibility = Visibility.Hidden;
                        break;
                }

                if (ckFileRawOutput.IsChecked.Value)
                {
                    CanvasFileSourceCSV.Visibility = Visibility.Hidden;
                    CanvasFileSourceJSON.Visibility = Visibility.Hidden;
                    CanvasFileSourceXLS.Visibility = Visibility.Hidden;
                    CanvasFileSourceXML.Visibility = Visibility.Hidden;
                }
            }

            if (cbDriverTarget.SelectedIndex > -1)
            {
                switch (cbDriverTarget.SelectedValue.ToString()[..2])
                {
                    case "WS":
                        CanvasWebserviceTarget.Visibility = Visibility.Visible;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        CanvasMailTarget.Visibility = Visibility.Hidden;
                        CanvasSQLTarget.Visibility = Visibility.Hidden;
                        CanvasADTarget.Visibility = Visibility.Hidden;
                        CanvasNOSQLTarget.Visibility = Visibility.Hidden;
                        break;

                    case "FI":
                        CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Visible;
                        CanvasMailTarget.Visibility = Visibility.Hidden;
                        CanvasSQLTarget.Visibility = Visibility.Hidden;
                        CanvasADTarget.Visibility = Visibility.Hidden;
                        CanvasNOSQLTarget.Visibility = Visibility.Hidden;
                        break;

                    case "DB":
                        CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        CanvasMailTarget.Visibility = Visibility.Hidden;
                        CanvasSQLTarget.Visibility = Visibility.Visible;
                        CanvasADTarget.Visibility = Visibility.Hidden;
                        CanvasNOSQLTarget.Visibility = Visibility.Hidden;
                        break;

                    case "NS":
                        CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        CanvasMailTarget.Visibility = Visibility.Hidden;
                        CanvasSQLTarget.Visibility = Visibility.Hidden;
                        CanvasADTarget.Visibility = Visibility.Hidden;
                        CanvasNOSQLTarget.Visibility = Visibility.Visible;
                        break;

                    case "MB":
                        CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        CanvasMailTarget.Visibility = Visibility.Visible;
                        CanvasSQLTarget.Visibility = Visibility.Hidden;
                        CanvasADTarget.Visibility = Visibility.Hidden;
                        CanvasNOSQLTarget.Visibility = Visibility.Hidden;
                        break;

                    case "AD":
                        CanvasWebserviceTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        CanvasMailTarget.Visibility = Visibility.Hidden;
                        CanvasSQLTarget.Visibility = Visibility.Hidden;
                        CanvasADTarget.Visibility = Visibility.Visible;
                        CanvasNOSQLTarget.Visibility = Visibility.Hidden;
                        break;

                }

                switch (cbDriverTarget.SelectedValue.ToString())
                {
                    case "FI_CSV":
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Visible;
                        CanvasFileTargetCSV.Visibility = Visibility.Visible;
                        CanvasFileTargetJSON.Visibility = Visibility.Hidden;
                        CanvasFileTargetXML.Visibility = Visibility.Hidden;
                        CanvasFileTargetXLS.Visibility = Visibility.Hidden;
                        break;
                    case "FI_XLS":
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Visible;
                        CanvasFileTargetCSV.Visibility = Visibility.Hidden;
                        CanvasFileTargetJSON.Visibility = Visibility.Hidden;
                        CanvasFileTargetXML.Visibility = Visibility.Hidden;
                        CanvasFileTargetXLS.Visibility = Visibility.Visible;
                        break;
                    case "FI_JSON":
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Visible;
                        CanvasFileTargetCSV.Visibility = Visibility.Hidden;
                        CanvasFileTargetJSON.Visibility = Visibility.Visible;
                        CanvasFileTargetXML.Visibility = Visibility.Hidden;
                        CanvasFileTargetXLS.Visibility = Visibility.Hidden;
                        break;
                    case "FI_XML":
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Visible;
                        CanvasFileTargetCSV.Visibility = Visibility.Hidden;
                        CanvasFileTargetJSON.Visibility = Visibility.Hidden;
                        CanvasFileTargetXML.Visibility = Visibility.Visible;
                        CanvasFileTargetXLS.Visibility = Visibility.Hidden;
                        break;
                    case "WS_REST":
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Visible;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Visible;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        break;
                    default:
                        CanvasWebserviceRESTTarget.Visibility = Visibility.Hidden;
                        CanvasWebserviceNUXEOTarget.Visibility = Visibility.Hidden;
                        CanvasFileTarget.Visibility = Visibility.Hidden;
                        break;
                }
            }

            if (cbConnSource.SelectedIndex > -1 && cbDriverTarget.SelectedIndex > -1)
            {
                CONNString CSs = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString());
                if (CSs.SConnDriverSuffix.Equals("FI") && cbDriverTarget.SelectedValue.ToString().StartsWith("FI") && ckFileRawOutput.IsChecked.Value)
                {
                    CanvasFileTarget.Visibility = Visibility.Hidden;
                    ckTrimData.Visibility = Visibility.Hidden;
                    CanvasOptionalColumnsTarget.Visibility = Visibility.Hidden;
                    lbRawOutput.Visibility = Visibility.Visible;
                }
                if (CSs.SConnDriverSuffix.Equals("WS") && cbDriverTarget.SelectedValue.ToString().StartsWith("FI") && ckWebserviceRawOutput.IsChecked.Value)
                {
                    CanvasFileTarget.Visibility = Visibility.Hidden;
                    ckTrimData.Visibility = Visibility.Hidden;
                    CanvasOptionalColumnsTarget.Visibility = Visibility.Hidden;
                    lbRawOutput.Visibility = Visibility.Visible;
                }

                if (CSs.SConnDriverSuffix.Equals("FI") && cbDriverTarget.SelectedValue.ToString().StartsWith("MB") && ckFileRawOutput.IsChecked.Value)
                {
                    lbMailTargetDataFormat.Visibility = Visibility.Hidden;
                    cbMailTargetDataFormat.Visibility = Visibility.Hidden;
                    CanvasOptionalColumnsTarget.Visibility = Visibility.Hidden;
                    //lbRawOutput.Visibility = Visibility.Visible;
                }
                if (CSs.SConnDriverSuffix.Equals("WS") && cbDriverTarget.SelectedValue.ToString().StartsWith("MB") && ckWebserviceRawOutput.IsChecked.Value)
                {
                    lbMailTargetDataFormat.Visibility = Visibility.Hidden;
                    cbMailTargetDataFormat.Visibility = Visibility.Hidden;
                    CanvasOptionalColumnsTarget.Visibility = Visibility.Hidden;
                    //lbRawOutput.Visibility = Visibility.Visible;
                }
            }

            if (ckAppendFileCreation.IsChecked.Value)
            {
                lbXLSAppendDetail.Visibility = Visibility.Visible;
            }
            else
            {
                lbXLSAppendDetail.Visibility = Visibility.Hidden;
            }
        }

        private void BuildWSPostActionItems(string sTemplate)
        {
            switch (sTemplate)
            {
                case "MICROSOFT_ONEDRIVE":

                    bool bRebuild = true;
                    if (cbWSSourcePostWork.Items.Count == 3)
                    {
                        ComboBoxItem cb1 = (ComboBoxItem)cbWSSourcePostWork.Items[1];
                        ComboBoxItem cb2 = (ComboBoxItem)cbWSSourcePostWork.Items[2];
                        if (cb1.Tag.ToString().Equals("DELETE") && cb2.Tag.ToString().Equals("RENAME"))
                        { bRebuild = false; }
                    }
                    else if (cbWSSourcePostWork.Items.Count != 1 && cbWSSourcePostWork.Items.Count != 3)
                    {
                        int iMax = cbWSSourcePostWork.Items.Count;
                        for (int i = 1; i < iMax; i++)
                        {
                            cbWSSourcePostWork.Items.Remove(cbWSSourcePostWork.Items[1]);
                        }
                    }

                    if (bRebuild)
                    {
                        ComboBoxItem cbNewItemS2 = new()
                        {
                            Tag = "DELETE",
                            Content = Languages.Languages.ma_source_ws_postprocess_02
                        };
                        cbWSSourcePostWork.Items.Add(cbNewItemS2);

                        ComboBoxItem cbNewItemS3 = new()
                        {
                            Tag = "RENAME",
                            Content = Languages.Languages.ma_source_ws_postprocess_03
                        };
                        cbWSSourcePostWork.Items.Add(cbNewItemS3);
                    }

                    break;
                default:
                    if (cbWSSourcePostWork.Items.Count > 1)
                    {
                        int iMax = cbWSSourcePostWork.Items.Count;
                        for (int i = 1; i < iMax; i++)
                        {
                            cbWSSourcePostWork.Items.Remove(cbWSSourcePostWork.Items[1]);
                        }
                    }
                    break;
            }
        }

        private void LoadUI(Job INIP, bool bIsExternalJob)
        {
            if (!INIP.Job_IsSubJob) //on ne recharge la CB que si on est sur le job principal
            {
                LbJobStep.Items.Clear();
                for (int iStep = 1; iStep <= FuzibleController.GetJobSteps(INIP.JobID).Count + 1; iStep++)
                {
                    Job INISubJob = FuzibleController.GetSubJob(INIP, iStep);
                    if (INISubJob != null)
                    {
                        ComboBoxItem cbI = new()
                        {
                            Content = string.Concat(INISubJob.SubJobID, " > ", INISubJob.JobNAME),
                            Tag = INISubJob.JobID,
                        };
                        LbJobStep.Items.Add(cbI);
                    }
                }
                LbJobStep.SelectedIndex = 0;
            }

            if (INIP.Job_IsSubJob)
            {
                if (LbJobStep.Items.Cast<ComboBoxItem>().Select(c => (string)c.Tag).ToList().IndexOf(INIP.JobID) == -1) //le subjob vient d'être crée, il n'existe donc pas dans la liste
                {
                    ComboBoxItem cbI = new()
                    {
                        Content = string.Concat(INIP.SubJobID, " > ", INIP.JobNAME),
                        Tag = INIP.JobID,
                    };
                    LbJobStep.Items.Add(cbI);
                    LbJobStep.SelectedIndex = LbJobStep.Items.Count - 1;
                }
            }

            //LbJobStep.Content = string.Concat("STEP ", INIP.SubJobID.ToString(), "/", INIP.JobChildrenQuantity.ToString());
            lbJobID.Content = INIP.JobID;
            lbJobPassword.Content = INIP.JobPassword;

            tbJobDescription.Text = INIP.JobDescription;

            lbJobVersion.Content = "Job Version : " + INIP.JobVersionWithUser;
            //***affichage des données du job principal pour les sub
            if (bIsExternalJob)
            {
                ckSqlLog.IsChecked = INIP.HasSqlLog;
                if (INIP.LogMailAdress.Count > 0) { tbMailAdress.Text = string.Join(";", INIP.LogMailAdress.ToArray()); } else { tbMailAdress.Text = ""; }
                RichTB.SetTextRTB(tbQueryVariableParameters, string.Join(";", INIP.DynParams.ToArray()));
                RichTB.SetColorsRTB(tbQueryVariableParameters, new List<RTBColorizer> { new RTBColorizer(";", new SolidColorBrush(Colors.Red), FontWeights.Bold, FontStyles.Normal) }, true);
                cbNiveauLog.SelectedValue = INIP.LogLevel.ToString();
            }
            else
            {
                ckSqlLog.IsChecked = FuzibleController.GetJob(INIP.ParentJobID).HasSqlLog;
                if (FuzibleController.GetJob(INIP.ParentJobID).LogMailAdress.Count > 0) { tbMailAdress.Text = string.Join(";", FuzibleController.GetJob(INIP.ParentJobID).LogMailAdress.ToArray()); } else { tbMailAdress.Text = ""; }
                RichTB.SetTextRTB(tbQueryVariableParameters, string.Join(";", FuzibleController.GetJob(INIP.ParentJobID).DynParams.ToArray()));
                RichTB.SetColorsRTB(tbQueryVariableParameters, new List<RTBColorizer> { new RTBColorizer(";", new SolidColorBrush(Colors.Red), FontWeights.Bold, FontStyles.Normal) }, true);
                cbNiveauLog.SelectedValue = FuzibleController.GetJob(INIP.ParentJobID).LogLevel.ToString();
                //***affichage des données du job principal pour les sub
            }

            cbNoSourceDataNoError.SelectedIndex = INIP.NoSourceDataNoError;
            cbQueryRetry.SelectedIndex = INIP.QueryRetries;

            if (INIP.Job_IsSubJob)
            {
                //cbINISection.IsEnabled = false;
                btnCreerNouveau.IsEnabled = false;
                btnChangePwd.IsEnabled = false;
                btnJobFamily.IsEnabled = false;
                ckJobVisibility.IsEnabled = false;
                ckJobVisibility.IsChecked = false;
                tbQueryVariableParameters.IsEnabled = false;
                cbNiveauLog.IsEnabled = false;
                ckSqlLog.IsEnabled = false;
                tbMailAdress.IsEnabled = false;
            }
            else
            {
                cbINISection.IsEnabled = true;
                btnCreerNouveau.IsEnabled = true;
                btnChangePwd.IsEnabled = true;
                btnJobFamily.IsEnabled = true;
                ckJobVisibility.IsEnabled = true;
                ckJobVisibility.IsChecked = INIP.IsJobVisibleInClientApp;
                tbQueryVariableParameters.IsEnabled = true;
                cbNiveauLog.IsEnabled = true;
                ckSqlLog.IsEnabled = true;
                tbMailAdress.IsEnabled = true;
            }

            if (INIP.ConnectionString_Source != null) { cbDriverSource.SelectedValue = INIP.ConnectionString_Source.SConnDriver.ToString(); cbConnSource.SelectedValue = INIP.ConnectionString_Source.ToString(); } else { cbConnSource.SelectedIndex = -1; }
            if (INIP.ConnectionString_Source != null && cbConnSandbox.SelectedIndex == -1) { cbConnSandbox.SelectedValue = INIP.ConnectionString_Source.ToString(); }
            ;
            if (INIP.ConnectionString_Target != null) { cbDriverTarget.SelectedValue = INIP.ConnectionString_Target.SConnDriver.ToString(); cbConnTarget.SelectedValue = INIP.ConnectionString_Target.ToString(); } else { cbConnTarget.SelectedIndex = -1; }

            if (INIP.DatabaseName_Source.Length > 0)
            {
                tbDatabaseSource.Text = INIP.DatabaseName_Source;
            }
            else { tbDatabaseSource.Text = INIP.ConnectionString_Source != null ? INIP.ConnectionString_Source.SConnDB : ""; }

            if (INIP.DatabaseName_Source.Length > 0)
            {
                if (tbDatabaseSandbox.Text.Length == 0) { tbDatabaseSandbox.Text = INIP.DatabaseName_Source; }
            }
            else { if (tbDatabaseSandbox.Text.Length == 0) { tbDatabaseSandbox.Text = INIP.ConnectionString_Source != null ? INIP.ConnectionString_Source.SConnDB : ""; } }

            if (INIP.DatabaseName_Target.Length > 0)
            {
                tbDatabaseTarget.Text = INIP.DatabaseName_Target;
            }
            else { tbDatabaseTarget.Text = INIP.ConnectionString_Target != null ? INIP.ConnectionString_Target.SConnDB : ""; }

            ckDataReader.IsChecked = !INIP.ExportUseDataset;
            ckSQLDirectStream.IsChecked = INIP.SQLDirectStream;
            ckSQLTrustTargetColumns.IsChecked = INIP.SQLTrustTargetColumnType;
            cbSQLDirectStreamPriority.SelectedValue = INIP.SQLDirectStreamPriority;

            tbQteThreadsExport.Value = INIP.Threads_Source;
            tbQteThreadsImport.Value = INIP.Threads_Target;

            cbSQLSynchroTargetBehavior.SelectedValue = INIP.SynchroTargetTableBehavior.ToString();

            ckJobVisibility.IsChecked = INIP.IsJobVisibleInClientApp;
            ckSubJobAbortIfErrors.IsChecked = INIP.AbortSubJobExecutionIfErrors;
            ckSubJobAbortIfNoData.IsChecked = INIP.AbortSubJobExecutionIfNoData;
            ckBypassPostJobOnErrors.IsChecked = INIP.BypassPostJobExecutionIfErrors;
            ckBlankNull.IsChecked = INIP.UseNull_Target;
            ckFieldAnalyzer.IsChecked = INIP.CheckFieldsBeforeInsert;
            ckAllowSchemaAlterationTarget.IsChecked = INIP.AlterColumnTypeOnInsert;
            cbSqlAlterOptions.SelectedIndex = INIP.AlterColumnTypeOptions - 1;

            if (ckAllowSchemaAlterationTarget.IsChecked.Value)
            { cbSqlAlterOptions.Visibility = Visibility.Visible; }

            ckTrimData.IsChecked = INIP.TrimData;
            cbDataPivotMethod.SelectedValue = INIP.HyperFileArrayFieldTransformation;
            tbHyperfileSeparator.Text = INIP.DataTransformSeparatorOrLabel;
            ckDontTransformCSVWithInvalidHeader.IsChecked = INIP.DataTransformDontTransformIfVariableArraySizes;
            ckTransformAddLabelValues.IsChecked = INIP.DataTransformRowsToColumnsAddLabelToValues;
            ckTransformCrossQueries.IsChecked = INIP.DataTransformAlsoCrossQueries;
            ckAddPkAfterCreateTable.IsChecked = INIP.CreatePrimaryKeyAfterHavingCreatedATable;
            ckDisableConstraintsTarget.IsChecked = INIP.SQLTargetDisableConstraints;
            ckSQLBulkCopyTarget.IsChecked = INIP.SQLTargetBulkCopy;
            ckSynchroStoreChanges.IsChecked = INIP.SynchroStoreChanges;
            ckConvertHTMLEntitis.IsChecked = INIP.ConvertHTMLPatternsForTarget;
            ckSynchroBypassFiltersInTargetQuery.IsChecked = INIP.SynchroBypassQueryFiltersInTarget;
            ckDeleteSourceSQLAfterInser.IsChecked = INIP.SourceTableDeleteAfterInsert;
            ckAutoTableCreation.IsChecked = INIP.AutoSQLTableCreation;
            ckAddQuotesCSV.IsChecked = INIP.CSVAddQuotes;

            cbCSVImport.SelectedValue = INIP.FileCleanup_Import.ToString();
            cbDropImport.SelectedValue = INIP.TargetTableBehavior.ToString();
            cbFieldAnalyzerLevel.SelectedValue = INIP.FieldAnalyzerLevel.ToString();
            cbModeExtraction.SelectedValue = INIP.JobMethod.ToString();

            tbFilesZippedIn.Text = INIP.FileSourceZippedIn;
            tbXSLSheetToRead.Text = INIP.XLSSheetToRead.ToString();
            tbXLSRowOffset.Text = INIP.XLSRowOffset.ToString();
            tbXLSRowWriteOffset.Text = INIP.XLSRowWriteOffset.ToString();
            ckAddHeaderXLS.IsChecked = INIP.XLSAddHeader;
            ckXMLRemoveTagForEmptyValues.IsChecked = INIP.XMLRemoveTagForEmptyValues;
            tbXMLHeader.Text = INIP.XMLHeader;
            tbJSONHeader.Text = INIP.JSONHeader;
            tbXMLRowBuilder.Text = INIP.XMLTargetRowBuilder;
            cbXMLWriteMode.SelectedIndex = INIP.XMLWriteMode;
            ckInterpretFormulaXLS.IsChecked = INIP.XLSInterpretFormulas;
            tbXLSPasswordSource.Text = INIP.XLSPasswordSource;
            tbXLSPasswordTarget.Text = INIP.XLSPasswordTarget;
            tbJSONRowBuilder.Text = INIP.JSONTargetRowBuilder;
            cbXLSStyle.SelectedValue = INIP.XLSStyle;
            ckAddTitleXLS.IsChecked = INIP.XLSWithTitle;

            ckAppendFileCreation.IsChecked = INIP.AppendFileCreation;
            ckAddHeaderCSV.IsChecked = INIP.CSVAddHeader;
            tbCSVSeparator.Text = INIP.CSVCharSeparator_Target;
            cbCSVEncoding.SelectedValue = INIP.CSVEncoding_Target;
            tbCSVSplitPattern.Text = INIP.CSVMultipleFilesInOnePattern;
            tbCSVRowOffset.Text = INIP.CSVRowOffset.ToString();
            tbCSVRowsPerFile.Text = INIP.MaxRowsInAFile.ToString();

            tbTargetAddRownum.Text = INIP.TargetAddRowNum;
            tbTargetAddDbName.Text = INIP.TargetAddDbName;
            tbTargetAddDtLoad.Text = INIP.TargetAddDtLoad;

            cbWSCallMethod.SelectedValue = INIP.WebServiceCallMethod.ToString();
            cbWSCallMethodTarget.SelectedValue = INIP.WebServiceCallMethod_Target.ToString();
            cbWSContentTypeTarget.SelectedValue = INIP.WebServiceContentType_Target.ToString();
            cbWSCallBodyType.SelectedValue = INIP.WebServiceRequestBodyType.ToString();
            cbWSSQLLanguage.SelectedValue = INIP.WebserviceSQLLanguage.ToString();
            ckWSDontSendEmptyValues.IsChecked = INIP.WebserviceHTTP_DontSendEmptyValues;
            tbWSNuxeoEndPoint.Text = INIP.WebserviceNuxeo_EndpointSource;
            ckWebserviceRawOutput.IsChecked = INIP.WebserviceRawOutput;
            ckFileRawOutput.IsChecked = INIP.FileRawOutput;
            cbWSTypeData.SelectedValue = INIP.WebserviceTypeData.ToString();

            if (INIP.PrePostJob_CommandSourceConnection.Length > 0 || INIP.ConnectionString_Source != null)
            {
                CONNString CS = INIP.PrePostJob_CommandSourceConnection.Length > 0 ? INIP.GlobalParameters.Connections.GetConnByID(INIP.PrePostJob_CommandSourceConnection) : INIP.ConnectionString_Source;
                BuildWSPostActionItems(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE));
            }

            cbWSSourcePostWork.SelectedValue = INIP.WebserviceSourcePostWork;

            ckAddSqlDBNameColumn.IsChecked = INIP.OptionalDBName_OnInsert;
            ckAddSqlRowsColumn.IsChecked = INIP.OptionalRowID_OnInsert;
            ckAddSqlTimestampColumn.IsChecked = INIP.OptionalTimestamp_OnInsert;
            tbDynamicParamToAddInTable.Text = INIP.OptionalDynamicParamField_OnInsert;

            RichTB.SetTextRTB(tbPostJobCommandSource, INIP.PrePostJob_CommandSource);
            RichTB.SetTextRTB(tbPostJobCommandTarget, INIP.PrePostJob_CommandTarget);
            cbPrePostJobCommands_Source.SelectedValue = INIP.PreOrPostCommand_Source.ToString();
            cbPrePostJobCommands_Target.SelectedValue = INIP.PreOrPostCommand_Target.ToString();

            ckPreJobCommandSource_LoopThroughResult.IsChecked = INIP.DynParams_LoopThroughRows_Source;
            ckPreJobCommandTarget_LoopThroughResult.IsChecked = INIP.DynParams_LoopThroughRows_Target;

            if (INIP.PrePostJob_CommandSourceConnection.Length > 0)
            { cbConnSourcePrePostCommand.SelectedValue = INIP.PrePostJob_CommandSourceConnection; }
            else { cbConnSourcePrePostCommand.SelectedIndex = -1; }

            if (INIP.PrePostJob_CommandTargetConnection.Length > 0)
            { cbConnTargetPrePostCommand.SelectedValue = INIP.PrePostJob_CommandTargetConnection; }
            else { cbConnTargetPrePostCommand.SelectedIndex = -1; }

            ckWSFormatURLInUpper.IsChecked = INIP.WebserviceHTTP_FormatURLInUpper;
            ckWSSaveResponseFile.IsChecked = INIP.WebServiceSaveResponseFile;
            tbWSSuccessString.Text = INIP.WebServiceSuccessString;
            tbWSReponsesTrackingColumn.Text = INIP.WebServiceTrackingColumnInResponses;
            tbWSColumnsSendOffset.Text = INIP.WebserviceHTTP_SendColumnsOffset.ToString();
            tbWSSpecialHttpParams.Text = INIP.WebserviceSpecialHttpParameters;
            tbWSResponsesLogTable.Text = INIP.WebserviceLogTableResponses;
            lbWsJsonStructure.Content = INIP.WebserviceContentStructure;

            ckMailGetUnreadOnly.IsChecked = INIP.MailGetUnreadOnly;
            cbMailPostWork.SelectedValue = INIP.MailFlagRetrievedAsRead;
            tbMailMaxToRead.Text = INIP.MaxMailsToGet.ToString();
            ckMailAssembleQueriesRecipient.IsChecked = INIP.MailAssembleQueriesSameRecipient;
            cbMailTargetDataFormat.SelectedValue = INIP.MailTargetFormat;
            tbTargetMailHTMLTemplate.Text = INIP.MailTargetHTMLFileTemplate;
            tbTargetMailHTMLDataKeyWord.Text = INIP.MailTargetHTMLDataSetKeyword;

            cbADSearchScope.SelectedValue = INIP.ADSearchScope.ToString();
            cbADTargetBehavior.SelectedValue = INIP.ADTargetBehavior.ToString();
            tbADTargetProperty.Text = INIP.ADSearchProperty;
            ckADActivateNewEntries.IsChecked = INIP.ADActivateEntry;

            ckRemoveIDFromMongoDBQuery.IsChecked = INIP.RemoveIDFromMongoDBQuery;
            ckAddPkAfterCreateMongoCollection.IsChecked = INIP.CreatePKForMongoCollection;
            cbDropCollectionMongoDB.SelectedValue = INIP.TargetMongoCollectionBehavior.ToString();
            //INIQ.PopulateQueriesFromFuzibleController(INIP.SQLVariablesInSourceQueries, false);
            tbQueries.Document.Blocks.Clear();

            if (!bIsExternalJob) { INIP.LoadJobQueries(true); }
            string sQueries = INIP.GetQueriesAsString();
            tbQueries.AppendText(sQueries);
            SetMaxWidthRichTextBox(tbQueries, sQueries);

            ColorizeRichTextBox(true, true); //colorisation des requêtes

        }

        private static void SetMaxWidthRichTextBox(RichTextBox rtb, string sQueries)
        {
            int iWMax = 0;
            string[] sQ = sQueries.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (string s in sQ)
            {
                if (s.Length > iWMax) { iWMax = s.Length; }
            }

            if (iWMax < 2000) { iWMax = 2000; }

            rtb.Document.PageWidth = iWMax * 10;
            rtb.Document.LineHeight = 5;
        }

        private void LoadUIListSections(string sFamily)
        {
            //cbINISection.SelectedIndex = -1;
            cbINISection.Items.Clear();

            string sOldConnSuffixS = "";
            string sOldConnSuffixT = "";
            string scSPrefixName;
            string scTPrefixName;

            int iC = 0;

            if (sFamily.Length == 0)
            {
                cbJobFamily.Items.Clear();
                foreach (string FAM in FuzibleController.JobFamilies)
                {
                    cbJobFamily.Items.Add(FAM);
                }
            }

            foreach (Job INIP in FuzibleController.UserJobsList)
            {
                if ((sFamily.Length > 0 && INIP.JobFamily.Name.Equals(sFamily)) || sFamily.Length == 0)
                {
                    CONNString csS = INIP.ConnectionString_Source;
                    CONNString csT = INIP.ConnectionString_Target;
                    scSPrefixName = csS == null ? Languages.Languages.ma_msg_unknown : csS.SConnDriverSuffixFriendlyName;
                    scTPrefixName = csT == null ? Languages.Languages.ma_msg_unknown : csT.SConnDriverSuffixFriendlyName;

                    iC++;

                    if (!scSPrefixName.Equals(sOldConnSuffixS) || iC == 1)
                    {
                        ComboBoxItem cbNewItemS1 = new()
                        {
                            Tag = string.Concat("[SEP_", scSPrefixName, "]"),
                            Content = string.Concat("[", scSPrefixName, " -> ", scTPrefixName, "]"),
                            Foreground = System.Windows.Media.Brushes.Red,
                            FontStyle = FontStyles.Italic,
                            FontWeight = FontWeights.Bold,
                            FontSize = 10,
                            IsEnabled = false
                        };
                        cbINISection.Items.Add(cbNewItemS1);
                    }
                    if (scSPrefixName.Equals(sOldConnSuffixS) && !scTPrefixName.Equals(sOldConnSuffixT) && iC > 1)
                    {
                        ComboBoxItem cbNewItemS2 = new()
                        {
                            Tag = string.Concat("[SEP_", scSPrefixName, "]"),
                            Content = string.Concat("[", scSPrefixName, " -> ", scTPrefixName, "]"),
                            Foreground = System.Windows.Media.Brushes.Red,
                            FontStyle = FontStyles.Italic,
                            FontWeight = FontWeights.Bold,
                            FontSize = 10,
                            IsEnabled = false
                        };
                        cbINISection.Items.Add(cbNewItemS2);
                    }

                    ComboBoxItem cbNewItem = new()
                    {
                        Tag = INIP.JobID,
                        Content = string.Concat("   ", INIP.JobID, " ", INIP.JobNAME),
                        ToolTip = string.Concat(Languages.Languages.ma_jobconf_lbl_jobdescription, INIP.JobDescription),
                        FontWeight = FuzibleController.GetJobColor(INIP.RawJobID),
                        Visibility = INIP.Job_IsSubJob ? Visibility.Collapsed : Visibility.Visible,
                        Foreground = INIP.Job_IsSubJob ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.DarkBlue

                    };
                    cbINISection.Items.Add(cbNewItem);

                    sOldConnSuffixS = scSPrefixName;
                    sOldConnSuffixT = scTPrefixName;
                }
            }

            LoadUIListConnections("", "");

        }

        private void LoadUIListConnections(string sDBSource, string sDBTarget)
        {
            string sActualPositionS = "";
            string sActualPositionT = "";
            string sActualPositionSB = "";
            string sActualPositionSPrepost = "";
            string sActualPositionTPrepost = "";
            if (cbConnSource.SelectedIndex > -1) { sActualPositionS = (string)cbConnSource.SelectedValue; }
            if (cbConnTarget.SelectedIndex > -1) { sActualPositionT = (string)cbConnTarget.SelectedValue; }
            if (cbConnSandbox.SelectedIndex > -1) { sActualPositionSB = (string)cbConnSandbox.SelectedValue; }
            if (cbConnSourcePrePostCommand.SelectedIndex > -1) { sActualPositionSPrepost = (string)cbConnSourcePrePostCommand.SelectedValue; }
            if (cbConnTargetPrePostCommand.SelectedIndex > -1) { sActualPositionTPrepost = (string)cbConnTargetPrePostCommand.SelectedValue; }

            cbConnSource.Items.Clear();
            cbConnTarget.Items.Clear();
            cbConnSandbox.Items.Clear();
            cbConnSourcePrePostCommand.Items.Clear();
            cbConnTargetPrePostCommand.Items.Clear();

            SQLTools_Enums.BDD sBDD = SQLTools_Enums.BDD.FI_FILE;
            int iC = 0;

            foreach (CONNString CS in FuzibleController.UserConnectionsList)
            {
                iC++;

                if (CS.SConnDriverSuffix.StartsWith("DB"))
                {
                    ComboBoxItem cbNewItemSPC = new()
                    {
                        Tag = CS.SConnID,
                        Content = string.Concat(CS.SConnID, " -> ", CS.SConnName),
                        FontSize = 9,
                        //ToolTip = CS.SConnString(null),
                        Foreground = CS.SConnString(null).IndexOf("{") > 0 ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Black
                    };
                    cbConnSourcePrePostCommand.Items.Add(cbNewItemSPC);

                    ComboBoxItem cbNewItemTPC = new()
                    {
                        Tag = CS.SConnID,
                        Content = string.Concat(CS.SConnID, " -> ", CS.SConnName),
                        FontSize = 9,
                        //ToolTip = CS.SConnString(null),
                        Foreground = CS.SConnString(null).IndexOf("{") > 0 ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Black
                    };
                    cbConnTargetPrePostCommand.Items.Add(cbNewItemTPC);
                }

                if (!CS.SConnDriver.Equals(sBDD) || iC == 1)
                {
                    ComboBoxItem cbNewItemS1 = new()
                    {
                        Tag = string.Concat("[SEP_", CS.SConnDriverFriendlyName, "]"),
                        Content = string.Concat("[", CS.SConnDriverFriendlyName, "]"),
                        Foreground = System.Windows.Media.Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbConnSource.Items.Add(cbNewItemS1);

                    ComboBoxItem cbNewItemT1 = new()
                    {
                        Tag = string.Concat("[SEP_", CS.SConnDriverFriendlyName, "]"),
                        Content = string.Concat("[", CS.SConnDriverFriendlyName, "]"),
                        Foreground = System.Windows.Media.Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbConnTarget.Items.Add(cbNewItemT1);

                    ComboBoxItem cbNewItemSB1 = new()
                    {
                        Tag = string.Concat("[SEP_", CS.SConnDriverFriendlyName, "]"),
                        Content = string.Concat("[", CS.SConnDriverFriendlyName, "]"),
                        Foreground = System.Windows.Media.Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbConnSandbox.Items.Add(cbNewItemSB1);
                }

                ComboBoxItem cbNewItemS2 = new()
                {
                    Tag = CS.SConnID,
                    Content = string.Concat("   ", CS.SConnID, " -> ", CS.SConnName),
                    //ToolTip = CS.SConnString(null),
                    Foreground = CS.SConnString(null).IndexOf("{") > 0 ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Black
                };
                cbConnSource.Items.Add(cbNewItemS2);

                ComboBoxItem cbNewItemT2 = new()
                {
                    Tag = CS.SConnID,
                    Content = string.Concat("   ", CS.SConnID, " -> ", CS.SConnName),
                    //ToolTip = CS.SConnString(null),
                    Foreground = CS.SConnString(null).IndexOf("{") > 0 ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Black
                };
                cbConnTarget.Items.Add(cbNewItemT2);

                ComboBoxItem cbNewItemSB2 = new()
                {
                    Tag = CS.SConnID,
                    Content = string.Concat("   ", CS.SConnID, " -> ", CS.SConnName),
                    //ToolTip = CS.SConnString(null),
                    Foreground = CS.SConnString(null).IndexOf("{") > 0 ? System.Windows.Media.Brushes.DarkBlue : System.Windows.Media.Brushes.Black
                };
                cbConnSandbox.Items.Add(cbNewItemSB2);

                sBDD = CS.SConnDriver;
            }

            cbConnSource.SelectedValue = sActualPositionS;
            cbConnTarget.SelectedValue = sActualPositionT;
            cbConnSandbox.SelectedValue = sActualPositionSB;
            cbConnSourcePrePostCommand.SelectedValue = sActualPositionSPrepost;
            cbConnTargetPrePostCommand.SelectedValue = sActualPositionTPrepost;

            if (sDBSource.Length > 0) { tbDatabaseSource.Text = sDBSource; }
            if (sDBTarget.Length > 0) { tbDatabaseTarget.Text = sDBTarget; }

        }

        private void ColorizeRichTextBox(bool bSearchErrors, bool bResetProperties)
        {
            if (FuzibleController.MainParams.ENABLE_QUERY_ASSISTANT)
            {
                string sQueries = RichTB.GetTextRTB(tbQueries);

                List<RTBColorizer> sListPatterns = new();

                //colorisation des cross-join
                MatchCollection mcCJQueries = Regex.Matches(sQueries, SHSRegex.REGEX_CROSSJOINSCRIPT_B);
                foreach (Match mcCJ in mcCJQueries)
                {
                    sListPatterns.Add(new RTBColorizer(mcCJ.Value, new SolidColorBrush(Colors.DarkGoldenrod), FontWeights.Bold, FontStyles.Normal));
                }

                //Colorisation Language de script
                List<string> sListGTSQL = new()
                {
                    "FROM\\s+",
                    "GROUP\\s+BY\\s+",
                    "SELECT\\*\\s+",
                    "SELECT\\s+",
                    "ORDER\\s+BY\\s+",
                    "LEFT\\s+OUTER\\s+JOIN\\s+",
                    "RIGHT\\s+OUTER\\s+JOIN\\s+",
                    "INNER\\s+JOIN\\s+",
                    "LEFT\\s+JOIN\\s+",
                    "RIGHT\\s+JOIN\\s+",
                    "OUTER\\s+JOIN\\s+",
                    "OUTER\\s+APPLY\\s+",
                    "CROSS\\s+APPLY\\s+",
                    "WHERE\\s+",
                    "AND\\s+",
                    "DISTINCT\\s+",
                    "\\s+NOT\\s+IN\\s+",
                    "\\s+IN\\s+",
                    "\\s+NOT\\s+",
                    "LIKE\\s+",
                    "\\s+UNION\\s+"
                };

                foreach (string s in sListGTSQL)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.Purple), FontWeights.DemiBold, FontStyles.Normal));
                }

                List<string> SListGTSQLFunctions = new()
                {
                    "CASE\\s+",
                    "ELSE\\s+",
                    "WHEN\\s+",
                    "THEN\\s+",
                    "END\\s+",
                    "\\s+ON\\s+",
                    "SUBSTRING\\(",
                    "CONCAT\\(",
                    "ISNULL\\(",
                    "CONVERT\\(",
                    "LPAD\\(",
                    "RPAD\\(",
                    "RTRIM\\(",
                    "LTRIM\\(",
                    "TRIM\\(",
                    "CHARINDEX\\(",
                    "LENGTH\\(",
                    "UPPER\\(",
                    "LOWER\\(",
                    "REPLACE\\(",
                    "COALESCE\\(",
                    "ANONYMIZE\\(",
                    "SUM\\(",
                    "AVG\\(",
                    "MIN\\(",
                    "MAX\\(",
                    "STRING_AGG\\("
                };

                foreach (string s in SListGTSQLFunctions)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.MediumPurple), FontWeights.DemiBold, FontStyles.Normal));
                }


                List<string> sListExtendedSQL = new() { "OFFSET\\s+", "FETCH\\s+FIRST\\s+", "FETCH\\s+NEXT\\s+", "ROWS\\s+ONLY", "HAVING\\s+", "TOP\\s+", "PERCENT\\s+", "LIMIT\\s+", "UNION\\s+", "TABLE\\s+\\d\\s+", "\\d\\s+ONLY\\s+" };

                foreach (string s in sListExtendedSQL)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.DarkKhaki), FontWeights.DemiBold, FontStyles.Normal));
                }

                List<string> sListAs = new() { "\\s+AS\\s+" };

                foreach (string s in sListAs)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.Brown), FontWeights.Bold, FontStyles.Oblique));
                }

                //Colorisation des parenthèses
                //List<string> sListParenthesis = new List<string> { "\\)", "\\(" };
                //Ext.SetColorsRTB(tbQueries, sListParenthesis, new SolidColorBrush(Colors.ForestGreen), FontWeights.DemiBold, false);

                //colorisation du "{?1}"

                //colorisation du "RESULTAT:REQUETE"
                List<string> sListTargetInQueries = new();
                string[] tbQueriesRows = sQueries.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string sQ in tbQueriesRows)
                {
                    MatchCollection mC = Regex.Matches(sQ, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
                    foreach (Match mR in mC)
                    {
                        sListTargetInQueries.Add(sQ[..(mR.Value.IndexOf(":") + 1)]);
                    }
                }

                foreach (string s in sListTargetInQueries)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.Green), FontWeights.Bold, FontStyles.Normal));
                }

                List<string> sListScriptToColorize = Toolbox.ScriptLanguage.GetListScriptZonesFromString(RichTB.GetTextRTB(tbQueries));

                foreach (string s in sListScriptToColorize)
                {
                    sListPatterns.Add(new RTBColorizer(s, new SolidColorBrush(Colors.Blue), FontWeights.DemiBold, FontStyles.Italic));
                }

                if (bSearchErrors)
                {
                    try
                    {
                        //analyse des erreurs de syntaxe
                        Job INIP = InitJobParameters("[0]");
                        List<Query> sListQueries = Job.ExtractQueriesFromString(INIP, sQueries);
                        foreach (Query Q in sListQueries)
                        {
                            if (Q.QueryAnalyzer != null)
                            {
                                foreach (Query.QError qE in Q.QueryAnalyzer.Errors)
                                {
                                    sListPatterns.Add(new RTBColorizer(Toolbox.RemoveRegexFromString(qE.ErrorPattern), qE.ErrorType == Query.QError.ErrorLevel.ERROR ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.DarkOrange), FontWeights.UltraBold, FontStyles.Oblique));
                                }

                                foreach (Query qCJ in Q.CrossJoinQueries)
                                {
                                    foreach (Query.QError qE in qCJ.QueryAnalyzer.Errors)
                                    {
                                        sListPatterns.Add(new RTBColorizer(Toolbox.RemoveRegexFromString(qE.ErrorPattern), qE.ErrorType == Query.QError.ErrorLevel.ERROR ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.DarkOrange), FontWeights.UltraBold, FontStyles.Oblique));
                                    }
                                }
                            }
                        }
                    }
                    catch { } //rien à faire si la requête est merdique, on ne colorise pas
                }

                RichTB.SetColorsRTB(tbQueries, sListPatterns, bResetProperties);
                RichTB.SetSQLComments(tbQueries);

            }
        }

        private Job InitJobParameters(string sCurrentJobID)
        {
            Job INIP = FuzibleController.GetJob(sCurrentJobID);

            if (INIP == null)
            {
                INIP = new Job(FuzibleController.MainParams, sCurrentJobID, USERNAME, false)
                {
                    JobCreationDate = DateTime.Now
                };
            }
            else
            {
                string sPassword = FuzibleController.GetJob(sCurrentJobID).JobPassword;
                string sJobName = FuzibleController.GetJob(sCurrentJobID).JobNAME;
                INIP.JobNAME = sJobName;
                INIP.SetJobPassword(sPassword, sCurrentJobID, true);
            }

            INIP.JobFamily = new JobFamily { Name = cbJobFamily.SelectedValue != null ? cbJobFamily.SelectedValue.ToString() : "DEFAULT" };
            INIP.JobDescription = tbJobDescription.Text.Trim();
            INIP.JobPriority = 1;
            INIP.AbortSubJobExecutionIfErrors = ckSubJobAbortIfErrors.IsChecked.Value;
            INIP.AbortSubJobExecutionIfNoData = ckSubJobAbortIfNoData.IsChecked.Value;
            INIP.NoSourceDataNoError = cbNoSourceDataNoError.SelectedIndex;
            INIP.QueryRetries = cbQueryRetry.SelectedIndex;
            INIP.BypassPostJobExecutionIfErrors = ckBypassPostJobOnErrors.IsChecked.Value;
            INIP.IsJobVisibleInClientApp = ckJobVisibility.IsChecked.Value;
            if (cbConnSource.SelectedIndex > -1) { INIP.ConnectionString_Source = FuzibleController.GetConnection(cbConnSource.SelectedValue.ToString()); }
            if (cbConnTarget.SelectedIndex > -1) { INIP.ConnectionString_Target = FuzibleController.GetConnection(cbConnTarget.SelectedValue.ToString()); }
            INIP.DatabaseName_Source = tbDatabaseSource.Text;
            INIP.DatabaseName_Target = tbDatabaseTarget.Text;
            INIP.JobMethod = (SQLTools_Enums.JOB_PURPOSE)Enum.Parse(typeof(SQLTools_Enums.JOB_PURPOSE), cbModeExtraction.SelectedValue.ToString());
            INIP.ExportUseDataset = !ckDataReader.IsChecked.Value;
            INIP.SQLDirectStream = ckSQLDirectStream.IsChecked.Value;
            INIP.SQLTrustTargetColumnType = ckSQLTrustTargetColumns.IsChecked.Value;
            INIP.SQLDirectStreamPriority = Convert.ToInt32(cbSQLDirectStreamPriority.SelectedValue.ToString());
            INIP.SourceTableDeleteAfterInsert = ckDeleteSourceSQLAfterInser.IsChecked.Value;
            INIP.LogLevel = (LogTools.LOG_LEVEL)Enum.Parse(typeof(LogTools.LOG_LEVEL), cbNiveauLog.SelectedValue.ToString());
            INIP.HasSqlLog = ckSqlLog.IsChecked.Value;
            INIP.Threads_Source = Convert.ToInt32(tbQteThreadsExport.Value);
            INIP.Threads_Target = Convert.ToInt32(tbQteThreadsImport.Value);
            INIP.TrimData = ckTrimData.IsChecked.Value;
            INIP.ConvertHTMLPatternsForTarget = ckConvertHTMLEntitis.IsChecked.Value;
            INIP.AlterColumnTypeOnInsert = ckAllowSchemaAlterationTarget.IsChecked.Value;
            INIP.AlterColumnTypeOptions = cbSqlAlterOptions.SelectedIndex + 1;
            INIP.TargetTableBehavior = (SQLTools_Enums.TARGET_TABLE_METHOD)Enum.Parse(typeof(SQLTools_Enums.TARGET_TABLE_METHOD), cbDropImport.SelectedValue.ToString());
            INIP.CheckFieldsBeforeInsert = ckFieldAnalyzer.IsChecked.Value;
            INIP.SQLTargetDisableConstraints = ckDisableConstraintsTarget.IsChecked.Value;
            INIP.SQLTargetBulkCopy = ckSQLBulkCopyTarget.IsChecked.Value;
            INIP.FieldAnalyzerLevel = (SQLTools_Enums.PRECISION_COLUMN_ANALYZER)Enum.Parse(typeof(SQLTools_Enums.PRECISION_COLUMN_ANALYZER), cbFieldAnalyzerLevel.SelectedValue.ToString());
            INIP.UseNull_Target = ckBlankNull.IsChecked.Value;
            INIP.DynParams = Toolbox.FromStringToList(RichTB.GetTextRTB(tbQueryVariableParameters));
            INIP.SynchroTargetTableBehavior = (SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR)Enum.Parse(typeof(SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR), cbSQLSynchroTargetBehavior.SelectedValue.ToString());
            INIP.SynchroBypassQueryFiltersInTarget = ckSynchroBypassFiltersInTargetQuery.IsChecked.Value;
            INIP.SynchroStoreChanges = ckSynchroStoreChanges.IsChecked.Value;
            INIP.FileSourceZippedIn = tbFilesZippedIn.Text.Trim();
            INIP.FileCleanup_Import = (SQLTools_Enums.CSV_CLEANUP_METHOD)Enum.Parse(typeof(SQLTools_Enums.CSV_CLEANUP_METHOD), cbCSVImport.SelectedValue.ToString());
            if (tbMailAdress.Text.Trim().Equals(""))
            {
                INIP.LogMailAdress = new List<string>();
            }
            else
            {
                INIP.LogMailAdress = new List<string>(Regex.Split(tbMailAdress.Text.Trim().Replace(" ", ""), ";"));
            }
            INIP.HyperFileArrayFieldTransformation = Convert.ToInt16(cbDataPivotMethod.SelectedValue.ToString());
            INIP.DataTransformDontTransformIfVariableArraySizes = ckDontTransformCSVWithInvalidHeader.IsChecked.Value;
            INIP.DataTransformSeparatorOrLabel = tbHyperfileSeparator.Text.Trim();
            INIP.DataTransformAlsoCrossQueries = ckTransformCrossQueries.IsChecked.Value;
            INIP.DataTransformRowsToColumnsAddLabelToValues = ckTransformAddLabelValues.IsChecked.Value;
            INIP.CreatePrimaryKeyAfterHavingCreatedATable = ckAddPkAfterCreateTable.IsChecked.Value;
            INIP.AppendFileCreation = ckAppendFileCreation.IsChecked.Value;
            INIP.CSVCharSeparator_Target = tbCSVSeparator.Text;
            INIP.CSVEncoding_Target = cbCSVEncoding.SelectedValue.ToString();
            INIP.CSVAddHeader = ckAddHeaderCSV.IsChecked.Value;
            INIP.CSVMultipleFilesInOnePattern = tbCSVSplitPattern.Text;
            try { INIP.CSVRowOffset = Convert.ToInt16(tbCSVRowOffset.Text); } catch { }
            try { INIP.MaxRowsInAFile = Convert.ToInt32(tbCSVRowsPerFile.Text); } catch { }
            try { INIP.XLSSheetToRead = Convert.ToInt16(tbXSLSheetToRead.Text); } catch { }
            try { INIP.XLSRowOffset = Convert.ToInt16(tbXLSRowOffset.Text); } catch { }
            try { INIP.XLSRowWriteOffset = Convert.ToInt16(tbXLSRowWriteOffset.Text); } catch { }
            INIP.CSVAddQuotes = ckAddQuotesCSV.IsChecked.Value;
            INIP.XLSPasswordSource = tbXLSPasswordSource.Text.Trim();
            INIP.XLSPasswordTarget = tbXLSPasswordTarget.Text.Trim();
            INIP.XLSAddHeader = ckAddHeaderXLS.IsChecked.Value;
            INIP.XLSWithTitle = ckAddTitleXLS.IsChecked.Value;
            INIP.XLSStyle = cbXLSStyle.SelectedValue.ToString();
            INIP.XMLAddCDataTag = ckAddCDataXML.IsChecked.Value;
            INIP.TargetAddRowNum = tbTargetAddRownum.Text;
            INIP.TargetAddDbName = tbTargetAddDbName.Text;
            INIP.TargetAddDtLoad = tbTargetAddDtLoad.Text;
            INIP.XMLRemoveTagForEmptyValues = ckXMLRemoveTagForEmptyValues.IsChecked.Value;
            INIP.XMLHeader = tbXMLHeader.Text.Trim();
            INIP.JSONHeader = tbJSONHeader.Text.Trim();
            INIP.XMLTargetRowBuilder = tbXMLRowBuilder.Text.Trim();
            INIP.XLSInterpretFormulas = ckInterpretFormulaXLS.IsChecked.Value;
            INIP.XMLWriteMode = cbXMLWriteMode.SelectedIndex;
            INIP.JSONTargetRowBuilder = tbJSONRowBuilder.Text.Trim();
            INIP.WebserviceNuxeo_EndpointSource = tbWSNuxeoEndPoint.Text.Trim();
            INIP.WebServiceCallMethod = (SQLTools_Enums.WEBSERVICE_METHOD)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_METHOD), cbWSCallMethod.SelectedValue.ToString());
            INIP.WebServiceContentType_Target = (SQLTools_Enums.WEBSERVICE_CONTENT)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_CONTENT), cbWSContentTypeTarget.SelectedValue.ToString());
            INIP.WebServiceCallMethod_Target = (SQLTools_Enums.WEBSERVICE_METHOD)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_METHOD), cbWSCallMethodTarget.SelectedValue.ToString());
            INIP.WebServiceRequestBodyType = (SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE), cbWSCallBodyType.SelectedValue.ToString());
            INIP.WebserviceSQLLanguage = (SQLTools_Enums.WEBSERVICE_SQL)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_SQL), cbWSSQLLanguage.SelectedValue.ToString());
            INIP.WebserviceRawOutput = ckWebserviceRawOutput.IsChecked.Value;
            INIP.FileRawOutput = ckFileRawOutput.IsChecked.Value;
            INIP.OptionalDBName_OnInsert = ckAddSqlDBNameColumn.IsChecked.Value;
            INIP.OptionalRowID_OnInsert = ckAddSqlRowsColumn.IsChecked.Value;
            INIP.OptionalTimestamp_OnInsert = ckAddSqlTimestampColumn.IsChecked.Value;
            INIP.OptionalDynamicParamField_OnInsert = tbDynamicParamToAddInTable.Text.Trim();
            INIP.PrePostJob_CommandSource = RichTB.GetTextRTB(tbPostJobCommandSource).Trim();
            INIP.PrePostJob_CommandTarget = RichTB.GetTextRTB(tbPostJobCommandTarget).Trim();
            INIP.PreOrPostCommand_Source = (SQLTools_Enums.PRE_POST_JOB_COMMANDS)Enum.Parse(typeof(SQLTools_Enums.PRE_POST_JOB_COMMANDS), cbPrePostJobCommands_Source.SelectedValue.ToString());
            INIP.PreOrPostCommand_Target = (SQLTools_Enums.PRE_POST_JOB_COMMANDS)Enum.Parse(typeof(SQLTools_Enums.PRE_POST_JOB_COMMANDS), cbPrePostJobCommands_Target.SelectedValue.ToString());
            INIP.PrePostJob_CommandSourceConnection = cbConnSourcePrePostCommand.SelectedIndex > -1 ? cbConnSourcePrePostCommand.SelectedValue.ToString() : "";
            INIP.PrePostJob_CommandTargetConnection = cbConnTargetPrePostCommand.SelectedIndex > -1 ? cbConnTargetPrePostCommand.SelectedValue.ToString() : "";
            INIP.DynParams_LoopThroughRows_Source = ckPreJobCommandSource_LoopThroughResult.IsChecked.Value;
            INIP.DynParams_LoopThroughRows_Target = ckPreJobCommandTarget_LoopThroughResult.IsChecked.Value;
            INIP.WebserviceHTTP_FormatURLInUpper = ckWSFormatURLInUpper.IsChecked.Value;
            INIP.WebserviceSpecialHttpParameters = tbWSSpecialHttpParams.Text.Trim();
            try { INIP.WebserviceHTTP_SendColumnsOffset = Convert.ToInt16(tbWSColumnsSendOffset.Text); } catch { }
            INIP.WebServiceSaveResponseFile = ckWSSaveResponseFile.IsChecked.Value;
            INIP.WebserviceContentStructure = lbWsJsonStructure.Content.ToString().Trim();
            INIP.WebServiceSuccessString = tbWSSuccessString.Text.Trim();
            INIP.WebserviceHTTP_DontSendEmptyValues = ckWSDontSendEmptyValues.IsChecked.Value;
            INIP.WebServiceTrackingColumnInResponses = tbWSReponsesTrackingColumn.Text.Trim();
            INIP.WebserviceLogTableResponses = tbWSResponsesLogTable.Text.Trim();
            INIP.WebserviceSourcePostWork = cbWSSourcePostWork.SelectedValue == null ? "NOTHING" : cbWSSourcePostWork.SelectedValue.ToString();
            INIP.WebserviceTypeData = (SQLTools_Enums.API_OPTIONS)Enum.Parse(typeof(SQLTools_Enums.API_OPTIONS), cbWSTypeData.SelectedValue.ToString());
            try { INIP.MailFlagRetrievedAsRead = Convert.ToInt32(cbMailPostWork.SelectedValue.ToString()); } catch { }
            try { INIP.MaxMailsToGet = Convert.ToInt32(tbMailMaxToRead.Text.Trim().ToString()); } catch { INIP.MaxMailsToGet = 100; }
            ;
            INIP.MailGetUnreadOnly = ckMailGetUnreadOnly.IsChecked.Value;
            INIP.MailAssembleQueriesSameRecipient = ckMailAssembleQueriesRecipient.IsChecked.Value;
            INIP.MailTargetFormat = (SQLTools_Enums.MAIL_TARGET_FORMAT)Enum.Parse(typeof(SQLTools_Enums.MAIL_TARGET_FORMAT), cbMailTargetDataFormat.SelectedValue.ToString());
            INIP.MailTargetHTMLFileTemplate = tbTargetMailHTMLTemplate.Text.Trim();
            INIP.MailTargetHTMLDataSetKeyword = tbTargetMailHTMLDataKeyWord.Text.Trim();
            INIP.ADSearchScope = (SQLTools_Enums.AD_SEARCH_SCOPE)Enum.Parse(typeof(SQLTools_Enums.AD_SEARCH_SCOPE), cbADSearchScope.SelectedValue.ToString());
            INIP.ADTargetBehavior = Convert.ToInt32(cbADTargetBehavior.SelectedValue);
            INIP.ADSearchProperty = tbADTargetProperty.Text.Trim();
            INIP.ADActivateEntry = ckADActivateNewEntries.IsChecked.Value;
            INIP.RemoveIDFromMongoDBQuery = ckRemoveIDFromMongoDBQuery.IsChecked.Value;
            INIP.CreatePKForMongoCollection = ckAddPkAfterCreateMongoCollection.IsChecked.Value;
            INIP.TargetMongoCollectionBehavior = (SQLTools_Enums.TARGET_TABLE_METHOD)Enum.Parse(typeof(SQLTools_Enums.TARGET_TABLE_METHOD), cbDropCollectionMongoDB.SelectedValue.ToString());
            INIP.AutoSQLTableCreation = ckAutoTableCreation.IsChecked.Value;

            return INIP;
        }

        private delegate void ParametrizedMethodInvoker5(string arg, bool bClear);

        private delegate void ParametrizedMethodInvoker6(string arg);

        #endregion

        private void Fuzible_Initialized(object sender, EventArgs e)
        {

        }

    }


    internal static class RichTB
    {
        public static event EventHandler<string> OnColorizingField;

        internal static void SetColorsRTB(this RichTextBox richTextBox, List<RTBColorizer> qCPatterns, bool bResetproperties)
        {
            OnColorizingField?.Invoke(typeof(RichTB), Languages.Languages.ma_msg_queryloading06);

            double dFontSize = richTextBox.FontSize; // (double)new FontSizeConverter().ConvertFrom("10pt");

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
                                    {
                                        startIndex++;
                                    }
                                    else { bFound = true; }
                                }

                                if (tR != null)
                                {
                                    trRanges.Add(new RangeAndStyle(tR, Pattern));
                                }
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

            OnColorizingField?.Invoke(typeof(RichTB), Languages.Languages.ma_msg_backgroundtaskpending);
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

        internal class RangeAndStyle
        {
            internal TextRange Range
            {
                get; set;
            }
            internal RTBColorizer Style
            {
                get; set;
            }

            internal RangeAndStyle(TextRange tr, RTBColorizer style)
            {
                Range = tr;
                Style = style;
            }
        }

        internal static void SetSQLComments(RichTextBox tbQueries)
        {
            TextPointer pointerStart = tbQueries.CaretPosition.DocumentStart;
            TextPointer tpStartA = null;
            TextPointer tpStopA;
            TextPointer tpStartB = null;
            TextPointer tpStopB = null;
            bool bIsInCommentA = false;
            bool bIsInCommentB = false;

            while (pointerStart != null)
            {
                TextPointerContext tC = pointerStart.GetPointerContext(LogicalDirection.Forward);

                string textRun = "";
                if (tC == TextPointerContext.Text) { textRun = pointerStart.GetTextInRun(LogicalDirection.Forward); }

                if (bIsInCommentA)
                {
                    if (tC == TextPointerContext.None || pointerStart.IsAtLineStartPosition)
                    {
                        bIsInCommentA = false;
                        tpStopA = pointerStart.GetPositionAtOffset(0);
                        TextRange tR = new(tpStartA, tpStopA);

                        tR.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.DarkGray);
                        tR.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                        tR.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                        tR.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Tahoma"));
                        tR.ApplyPropertyValue(TextElement.FontSizeProperty, (double)new FontSizeConverter().ConvertFrom("10pt"));
                    }
                }
                if (bIsInCommentB && tpStopB != null)
                {
                    bIsInCommentB = false;
                    TextRange tR = new(tpStartB, tpStopB);

                    tR.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.DarkGray);
                    tR.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                    tR.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                    tR.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Tahoma"));
                    tR.ApplyPropertyValue(TextElement.FontSizeProperty, (double)new FontSizeConverter().ConvertFrom("10pt"));
                }

                if (textRun.StartsWith("--"))
                {
                    bIsInCommentA = true; tpStartA = pointerStart.GetPositionAtOffset(0);
                }
                if (textRun.StartsWith("/*"))
                {
                    bIsInCommentB = true; tpStartB = pointerStart.GetPositionAtOffset(0);
                }
                if (textRun.StartsWith("*/"))
                {
                    tpStopB = pointerStart.GetPositionAtOffset(2);
                }

                pointerStart = pointerStart.GetNextContextPosition(LogicalDirection.Forward);

            }
        }
    }
    internal class RTBColorizer
    {
        public string SData { get; internal set; } = "";
        public SolidColorBrush SColor { get; internal set; } = new SolidColorBrush(Colors.Black);
        public FontWeight SSize { get; internal set; } = FontWeights.DemiBold;

        public FontStyle SStyle { get; internal set; } = FontStyles.Normal;

        public RTBColorizer(string _sData, SolidColorBrush _sColor, FontWeight _sSize, FontStyle _sStyle)
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
