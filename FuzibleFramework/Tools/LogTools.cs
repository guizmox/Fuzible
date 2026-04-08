using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FuzibleFramework
{


    public class LogTools
    {

        #region "VARIABLES"

        private int _lograte = 0;
        private DateTime _lastlog = DateTime.Now.AddDays(-1);

        private bool _logDbAlive = false;

        private int _iQteDaysKeepSQLLog = 8;
        private readonly FileWriter fiWriterLog = new();
        private readonly FileWriter fiWriterDbg = new();
        private readonly FileWriter fiWriterQueries = new();
        private readonly FileWriter fiWriterDebugMode = new();
        private string _sPathLog = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\LOG\\";
        private readonly string _sUserApp = Environment.UserName.ToUpper();
        private readonly MethodBase _miMethod;
        private readonly List<LogObject> _loListFullLog = new();

        public static event EventHandler<LogObject> OnNewLogEvent;
        public static event EventHandler<LogObject> OnNewStepByStepEvent;

        private string DATE_FORMAT = "";
        private int PENDING_USER_ACTION { set; get; } = 0;
        public bool WaitUserAction { get { return PENDING_USER_ACTION == 1 ? true : false; } }

        //public static event EventHandler<string> OnTooMuchErrors;
        //private bool bAlreadySentTooMuchErrors = false;

        public enum AVAILABLE_FIELDS_IN_LOG : int
        {
            DATE_LOG = 1,
            ERREUR = 2,
            APPNAME = 3,
            NULL = 4,
            IS_ERREUR = 5,
            USER = 6
        }
        public enum LOG_LEVEL : int
        {
            ERRORS_ONLY = 1,
            ERRORS_MESSAGES = 2,
            ERRORS_MESSAGES_DETAIL = 3
        }
        public enum JOB_STATUS
        {
            RUNNING = 1,
            FINISHED_WITHOUT_ERRORS = 2,
            FINISHED_WITH_ERRORS = 3
        }
        public enum DEBUG_MODE
        {
            QUERY = 1,
            FUNCNAME = 2,
            INFORMATION = 3,
            UIEVENT = 4
        }

        #endregion

        #region "PROPRIETES"

        private DateTime DateStartJob = DateTime.Now;
        private DateTime DateEndJob = DateTime.Now;

        public string LOGFilename { get { return fiWriterLog.Filepath; } }
        public string DEBUGFilename { get { return fiWriterDbg.Filepath; } }
        public string QUERIESFilename { get { return fiWriterQueries.Filepath; } }
        public LOG_LEVEL LogLevel { get; set; } = LOG_LEVEL.ERRORS_ONLY;
        public List<string> LogTables { get; } = new List<string> { "app_log_ent", "app_log_lig" };
        public string PathToLog
        {
            get { return _sPathLog; }
            set
            {
                if (Directory.Exists(value))
                {
                    _sPathLog = value;
                }
                else
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(_sPathLog);
                        _sPathLog = value;
                    }
                    catch (Exception ex)
                    {
                        _sPathLog = Directory.GetCurrentDirectory() + "\\LOG\\";
                        LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                    }
                }
            }
        }
        public string AppName { get; } = "";
        public bool HasNoErrors
        {
            get
            {
                if (JobErrors > 0)
                { return false; }
                else
                { return true; }
            }
        }
        public int LogRate { get { return _lograte; } }
        public SQLTools SQLLog { get; set; }
        public int SQLNumJob { get; private set; } = 0;
        public bool IsLogInSQL { get; private set; } = false;
        public int QuantityOfDaysToKeepSQLLog
        {
            get { return _iQteDaysKeepSQLLog; }
            set
            {
                if (value > 0 && value < 60)
                {
                    _iQteDaysKeepSQLLog = value;
                }
            }
        }
        public List<LogObject> LogEvents
        { get { List<LogObject> LEvents = _loListFullLog; return LEvents; } }
        public int JobErrors { get; private set; } = 0;
        public string JobName { get; private set; } = "";
        public int AbortJobWhenErrorsExceed { get; private set; } = 10;
        public int JobWarnings { get; private set; } = 0;
        public string JobRunningTime
        {
            get
            {
                //DateTime dtD = DateTime.Now;
                //DateTime dtE = DateTime.Now;
                //if (LogEvents.Count > 0)
                //{
                //    dtD = LogEvents[0].DtEvent;
                //    dtE = LogEvents[^1].DtEvent;
                //}
                TimeSpan ts = DateEndJob - DateStartJob;
                return string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
            }
        }
        public DataTable DtJobReport { get; private set; } = new DataTable(Languages.Languages.log_rowreport);
        public bool DebugFunctions
        {
            get;
            private set;
        } = false;

        private bool RaiseMessages = false;

        public bool StepByStepMode { get; private set; } = false;
        private Job StepByStepModeCurrentJob = null;
        private Query DummyQuery = new Query();

        #endregion

        #region "PUBLIC VOID"

        public LogTools(string sAppName, System.Reflection.MethodBase miMethod, Job INIP, bool bRaiseEvent)
        {
            //Console.OutputEncoding = Encoding.UTF8;
            //Console.InputEncoding = Encoding.UTF8;

            if (!Directory.Exists(PathToLog))
            {
                Directory.CreateDirectory(PathToLog);
            }

            RaiseMessages = bRaiseEvent;

            string sJobID = "";
            if (INIP != null)
            {
                sJobID = string.Concat("_", INIP.JobID);
                DebugFunctions = INIP.GlobalParameters.LOG_DEBUG_FUNCTIONS;
            }

            AppName = sAppName;

            _miMethod = miMethod;

            fiWriterLog.Filepath = string.Concat(PathToLog, _sUserApp, sJobID, "_Log_", System.DateTime.Now.ToString("yyyyMMddHHmmss"), ".txt");
            fiWriterDbg.Filepath = string.Concat(PathToLog, _sUserApp, sJobID, "_Dbg_", System.DateTime.Now.ToString("yyyyMMddHHmmss"), ".txt");
            fiWriterQueries.Filepath = string.Concat(PathToLog, _sUserApp, sJobID, "_Queries_", System.DateTime.Now.ToString("yyyyMMddHHmmss"), ".txt");
            fiWriterDebugMode.Filepath = string.Concat(PathToLog, "APPDEBUG_", _sUserApp.ToUpper(), sJobID, "_", System.DateTime.Now.ToString("yyyyMMdd"), ".txt");

            DtJobReport.Columns.Add(Languages.Languages.log_rowreport01, Type.GetType("System.String"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport02, Type.GetType("System.Int32"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport03, Type.GetType("System.Int32"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport04, Type.GetType("System.Int32"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport05, Type.GetType("System.Int32"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport06, Type.GetType("System.Int32"));
            DtJobReport.Columns.Add(Languages.Languages.log_rowreport07, Type.GetType("System.DateTime"));
            DtJobReport.Namespace = Languages.Languages.log_rowreport08;

            //INIP peut arriver à NULL si c'est le programme principal qui logue (donc à ce stade, pas au niveau JOB mais au niveau PROGRAMME)
            if (INIP != null)
            {
                LogLevel = INIP.LogLevel;
                _iQteDaysKeepSQLLog = INIP.GlobalParameters.LOG_DAYSTOKEEP;
                AbortJobWhenErrorsExceed = INIP.GlobalParameters.ABORT_JOB_ON_ERRORS;
                JobName = INIP.JobNAME;
                //InitSQLLog(INIP.HasSqlLog, new SQLTools(INIP, SQLTools_Enums.CLASS_PURPOSE.LOG, this))
            }
            else
            {
                LogLevel = LOG_LEVEL.ERRORS_ONLY;
                PrepareSQLLog(false, null);
            }

            //on met un minuteur sur le framework
            if (_miMethod != null)
            {

                //TODO
                //InitTimer();
                //LogData(_miMethod, null, "Initialisation", 2);
            }

        }

        public void StartJobLog(Job INIP, string sDelegateUser, List<string> sListDynParams, int iSubJobs)
        {
            DateStartJob = DateTime.Now;

            iSubJobs = iSubJobs == 0 ? 1 : iSubJobs;

            LogLevel = INIP.LogLevel;
            ClearLogEvents();
            JobWarnings = 0;
            JobErrors = 0;

            if (INIP.HasSqlLog)
            {
                LogTools llog = this;
                var _sqlLog = new SQLTools(INIP, SQLTools_Enums.CLASS_PURPOSE.LOG, ref llog);

                _logDbAlive = _sqlLog.QuickConnectionCheck();

                if (_logDbAlive)
                {
                    PrepareSQLLog(INIP.HasSqlLog, _sqlLog);
                    StartStopAndCleanLogInSQL(false, INIP, sDelegateUser.Length > 0 ? sDelegateUser : Environment.UserName.ToUpper(), LogTools.JOB_STATUS.RUNNING);
                }
                else
                {
                    LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_logsqlcantreach, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }
            }

            string sContext = Toolbox.GetAppContext();
            LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, sContext, SQLTools_Enums.LOG_TYPEINFO.DET);

            LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(DateTime.Now.ToString("dd/MM/yyyy"), Languages.Languages.log_jobstarted, INIP.JobNAME, " - ", INIP.JobVersion, " (1/", iSubJobs.ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
            
            if (INIP.DynParams.Count > 0)
            { LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.log_jobstarteddynparams, string.Join(";", sListDynParams)), SQLTools_Enums.LOG_TYPEINFO.INF); }

        }

        public void AddJobReportRetry(string sData)
        {
            lock (DtJobReport)
            {
                if (DtJobReport != null && DtJobReport.Columns.Count > 0)
                {
                    DataRow dr = DtJobReport.NewRow();
                    dr[Languages.Languages.log_rowreport01] = string.Concat("<p style=\"color:darkorange;font-style:italic;font-weight:bold;\">[", sData, "]</p>");
                    dr[Languages.Languages.log_rowreport02] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport03] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport04] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport05] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport06] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport07] = DateTime.Now;
                    DtJobReport.Rows.Add(dr);
                }
            }
        }

        public void AddJobReportRowSubJob(string sSubJob)
        {
            lock (DtJobReport)
            {
                if (DtJobReport != null && DtJobReport.Columns.Count > 0)
                {
                    DataRow dr = DtJobReport.NewRow();
                    dr[Languages.Languages.log_rowreport01] = string.Concat("<p style=\"color:darkblue;font-style:italic;font-weight:bold;\">[", sSubJob, "]</p>");
                    dr[Languages.Languages.log_rowreport02] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport03] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport04] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport05] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport06] = DBNull.Value;
                    dr[Languages.Languages.log_rowreport07] = DateTime.Now;
                    DtJobReport.Rows.Add(dr);
                }
            }
        }

        public void AddJobReportRow(string sTable, int iSource, int iTarget, int iInserterd, int iUpdated, int iDeleted)
        {
            lock (DtJobReport)
            {
                if (DtJobReport != null && DtJobReport.Columns.Count > 0)
                {
                    DataRow dr = DtJobReport.NewRow();
                    dr[Languages.Languages.log_rowreport01] = sTable;
                    dr[Languages.Languages.log_rowreport02] = iSource;
                    dr[Languages.Languages.log_rowreport03] = iTarget;
                    dr[Languages.Languages.log_rowreport04] = iInserterd;
                    dr[Languages.Languages.log_rowreport05] = iUpdated;
                    dr[Languages.Languages.log_rowreport06] = iDeleted;
                    dr[Languages.Languages.log_rowreport07] = DateTime.Now;
                    DtJobReport.Rows.Add(dr);
                }
            }
        }

        public string EndJobLog(Job INIP, string sDelegateUser)
        {
            DateEndJob = DateTime.Now;

            string sStatus = string.Concat(Languages.Languages.log_jobended01, JobRunningTime, " - ", Languages.Languages.log_jobended02, JobErrors.ToString(), " - ", Languages.Languages.log_jobended03, JobWarnings.ToString());

            //Log de sortie
            LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, sStatus, SQLTools_Enums.LOG_TYPEINFO.INF);
            LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(DateTime.Now.ToString("dd/MM/yyyy"), Languages.Languages.log_jobfinished, HasNoErrors ? Languages.Languages.log_jobfinished_noerrors : Languages.Languages.log_jobfinished_errors, ") : ", INIP.JobNAME, " - ", INIP.JobVersion), SQLTools_Enums.LOG_TYPEINFO.INF);

            if (SQLLog != null)
            {
                _logDbAlive = SQLLog.QuickConnectionCheck();

                if (_logDbAlive)
                {
                    LogDataInSql();
                }
                else
                {
                    LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_logsqlcantreach, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }
            }

            if (INIP.LogMailAdress.Count > 0)
            {
                LogTools llog = this;
                MailTools mLog = new(INIP, SQLTools_Enums.CLASS_PURPOSE.LOG, ref llog);
                LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.msg_send_report_endjob + string.Join(",", INIP.LogMailAdress), SQLTools_Enums.LOG_TYPEINFO.INF);
                mLog.SendMailLog();
            }

            if (SQLLog != null)
            {
                if (_logDbAlive)
                {
                    StartStopAndCleanLogInSQL(true, INIP, sDelegateUser.Length > 0 ? sDelegateUser : Environment.UserName.ToUpper(), HasNoErrors ? LogTools.JOB_STATUS.FINISHED_WITHOUT_ERRORS : LogTools.JOB_STATUS.FINISHED_WITH_ERRORS);
                }
            }

            return sStatus;
        }

        public string CreateJobHTMLSummary(string sJobName)
        {
            StringBuilder sbLog = new();
            sbLog.AppendLine(string.Concat("<h3>", sJobName, "</h3>"));

            if (HasNoErrors)
            { sbLog.AppendLine(string.Concat("<b><p><font color=", @"""green"">", Languages.Languages.mb_log_success, JobRunningTime, ").</font></p></b>")); }
            else
            { sbLog.AppendLine(string.Concat("<b><p><font color=", @"""red"">", Languages.Languages.mb_log_errors01, JobRunningTime, "). " + Languages.Languages.mb_log_errors02 + "</font></p></b>")); }

            sbLog.AppendLine(string.Concat("<p><font color=", @"""orange""", "   >", Languages.Languages.mb_log_warnings, JobWarnings.ToString(), "</font></p>"));
            sbLog.AppendLine(string.Concat("<p><font color=", @"""red""", "   >", Languages.Languages.mb_log_errors, JobErrors.ToString(), "</font></p>"));
            sbLog.AppendLine(MailTools.DatatableToHTML(DtJobReport, true, true, false, true));
            return sbLog.ToString();
        }

        public void ClearLogEvents()
        {
            //vidage de la RAM
            lock (LogEvents) { LogEvents.Clear(); }
            JobErrors = 0;
            JobWarnings = 0;
            lock (DtJobReport) { DtJobReport.Clear(); }
        }

        public int StartStopAndCleanLogInSQL(bool bFinished, Job INIP, string sUser, JOB_STATUS JobStatus)
        {
            int iNumJob = 0;
            string sJobName = string.Concat(INIP.JobID, " ", INIP.JobNAME.Replace("'", " ").Replace("\"", " "), " - ", INIP.JobVersion);
            object Js = Convert.ChangeType(JobStatus, JobStatus.GetTypeCode());

            if (SQLLog != null)
            {
                lock (SQLLog)
                {
                    if (IsLogInSQL)
                    {
                        DataSet dsTemp = new();

                        //contrôle existence modèle de données LOG
                        //entêtes
                        bool bExistsENT = SQLLog.CheckForExistingTable(LogTables[0], DummyQuery);
                        //lignes
                        bool bExistsLIG = SQLLog.CheckForExistingTable(LogTables[1], DummyQuery);

                        if (bExistsENT && bExistsLIG)
                        {
                            InitGetLocaleLog(); //initialisation de la locale datetime du LOG

                            //puis on insère ou update l'entête
                            if (!bFinished)
                            {
                                string sQuery = SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DELETE_LOG_EVENTS);
                                sQuery = sQuery.Replace("{TABLE_NAME}", LogTables[1]);
                                sQuery = sQuery.Replace("{SCHEMA_NAME}", SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                                sQuery = sQuery.Replace("{DAYS_COUNT}", _iQteDaysKeepSQLLog.ToString());
                                if (sQuery.Length > 0)
                                { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, sQuery, DummyQuery, LogTables[1]); }
                                else { LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, Languages.Languages.log_sqlcantfindpattern + SQLTools_Enums.DRIVER_PARAMS.DELETE_LOG_EVENTS.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                                sQuery = SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.INSERT_LOG_EVENT);
                                sQuery = sQuery.Replace("{TABLE_NAME}", LogTables[0]);
                                sQuery = sQuery.Replace("{SCHEMA_NAME}", SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                                sQuery = sQuery.Replace("{JOB_NAME}", sJobName);
                                sQuery = sQuery.Replace("{JOB_STATUS}", Js.ToString());
                                sQuery = sQuery.Replace("{LOG_USER}", sUser);
                                sQuery = sQuery.Replace("{LOG_STATUS}", JobStatus.ToString());
                                if (sQuery.Length > 0)
                                { dsTemp = SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sQuery, DummyQuery, LogTables[0]); }
                                else { LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, Languages.Languages.log_sqlcantfindpattern + SQLTools_Enums.DRIVER_PARAMS.INSERT_LOG_EVENT.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                                try
                                { SQLNumJob = Convert.ToInt32(SQLTools.BuildStringFromDs(dsTemp)); }
                                catch (Exception)
                                { SQLNumJob = -1; }
                            }
                            else
                            {
                                string sQuery = SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.UPDATE_LOG_EVENT);
                                sQuery = sQuery.Replace("{TABLE_NAME}", LogTables[0]);
                                sQuery = sQuery.Replace("{SCHEMA_NAME}", SQLLog.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                                sQuery = sQuery.Replace("{JOB_NAME}", sJobName);
                                sQuery = sQuery.Replace("{JOB_STATUS}", Js.ToString());
                                sQuery = sQuery.Replace("{LOG_USER}", sUser);
                                sQuery = sQuery.Replace("{LOG_STATUS}", JobStatus.ToString());
                                sQuery = sQuery.Replace("{JOB_ID}", SQLNumJob.ToString());
                                if (sQuery.Length > 0)
                                { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sQuery, DummyQuery, LogTables[0]); }
                                else { LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, Languages.Languages.log_sqlcantfindpattern + SQLTools_Enums.DRIVER_PARAMS.UPDATE_LOG_EVENT.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                            }
                        }
                        else
                        {
                            LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, Languages.Languages.log_sqlnotables, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            IsLogInSQL = false;
                        }

                        iNumJob = SQLNumJob;
                    }
                }
                //nettoyage du Log après X jours
                if (bFinished)
                {
                    PurgeLog(IsLogInSQL);
                }
            }
            return iNumJob;

        }

        public void LogDataInSql()
        {
            if (IsLogInSQL && SQLLog != null)
            {
                StringBuilder sbQuery = new();
                int iCount = 0;
                lock (LogEvents)
                {
                    List<LogObject> LEvents = LogEvents;
                    foreach (LogObject LE in LEvents)
                    {
                        string sDate = ProcessDateTime(LE.DtEvent, SQLLog.Connection.SConnDriver);
                        iCount++;
                        sbQuery.AppendLine(string.Concat("INSERT INTO ", LogTables[1], " (id_job, li_origin, dt_event, li_level, li_classe, li_function, li_message) ",
                                                         "VALUES (", SQLNumJob.ToString(), ", '", LE.Cpurpose, "', ", sDate, ", '", LE.TLevel.ToString(), "','", LE.MiMethod.DeclaringType.Name, "','", LE.SFunction, "','", LE.SMessage.Replace("'", "''"), "');"));
                        if (iCount % 200 == 0) // pour éviter de faire trop d'insert à chaque fois
                        {
                            lock (SQLLog) { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQuery.ToString(), DummyQuery, LogTables[1]); }
                            sbQuery.Length = 0;
                        }
                    }
                }

                if (sbQuery.Length > 0)
                { lock (SQLLog) { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQuery.ToString(), DummyQuery, LogTables[1]); } }

                sbQuery.Length = 0;

            }
        }

        private string ProcessDateTime(DateTime dtEvent, SQLTools_Enums.BDD sBDD)
        {
            string sDate = "";

            if (DATE_FORMAT.Length == 0)
            {
                sDate = Toolbox.SetCleanDate(dtEvent.ToString(), SQLLog.JobParameters, SQLLog.Connection, true, true, 2);
            }
            else
            {
                sDate = dtEvent.ToString(DATE_FORMAT);
            }

            switch (sBDD)
            {
                case SQLTools_Enums.BDD.DB_SQLITE:
                    return string.Concat("DATETIME('", sDate, "')");
                default:
                    return string.Concat("CAST('", sDate, "' AS DATETIME)");
            }
        }

        public string GetSQLSimpleLogReport(string sUser, string sJobID, string sDateFormat, DateTime dtSearch)
        {
            //string sJobIDReplaced = sJobID.Replace("[", "-").Replace("]", "+");

            string sResult = "";
            //passage dates au format sans secondes
            string sDtSearch = dtSearch.ToString(sDateFormat);
            string sDtSearchAdd1Min = dtSearch.AddMinutes(5).ToString(sDateFormat);
            string sQuery = "";
            if (SQLLog.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
            {
                //sQuery = "SELECT dt_job, li_status, dt_end_job" +
                //            " FROM " + LogTables[0] +
                //            " WHERE li_userlaunch = '" + sUser + "'" +
                //            " AND REPLACE(REPLACE(li_job, '[', '-'), ']', '+') LIKE '" + sJobID + "%'" +
                //            " AND (DATETIME(dt_job) >= DATETIME('" + sDtSearch + "') AND DATETIME(dt_job) < DATETIME('" + sDtSearchAdd1Min + "'))";
                sQuery = "SELECT dt_start as dt_job, li_status, dt_end as dt_end_job FROM app_stacklaunch" +
                            " WHERE li_user = '" + sUser + "'" +
                            " AND id_job = '" + sJobID + "'" +
                            " AND (DATETIME(dt_start) >= DATETIME('" + sDtSearch + "') AND DATETIME(dt_start) < DATETIME('" + sDtSearchAdd1Min + "'))";
            }
            else
            {
                //sQuery = "SELECT dt_job, li_status, dt_end_job" +
                //                " FROM " + LogTables[0] +
                //                " WHERE li_userlaunch = '" + sUser + "'" +
                //                " AND REPLACE(REPLACE(li_job, '[', '-'), ']', '+') LIKE '" + sJobID + "%'" +
                //                " AND (dt_job >= CAST('" + sDtSearch + "' AS DATETIME) AND dt_job < CAST('" + sDtSearchAdd1Min + "' AS DATETIME))";
                sQuery = "SELECT dt_start as dt_job, li_status, dt_end as dt_end_job FROM app_stacklaunch" +
                                " WHERE li_user = '" + sUser + "'" +
                                " AND id_job = '" + sJobID + "'" +
                                " AND (dt_start >= CAST('" + sDtSearch + "' AS DATETIME) AND dt_start < CAST('" + sDtSearchAdd1Min + "' AS DATETIME))";
            }

            DataSet dsData = SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, DummyQuery, LogTables[1]);
            
            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                if (dsData.Tables[0].Rows[0]["dt_end_job"].Equals(System.DBNull.Value))
                {
                    sResult = dsData.Tables[0].Rows[0]["li_status"].ToString().ToLower().Replace("_", " ");
                }
                else
                {
                    string sStatus = dsData.Tables[0].Rows[0]["li_status"].ToString().ToLower().Replace("_", " ");
                    sStatus = string.Concat(sStatus[..1].ToUpper() + sStatus[1..]);
                    var dtStart = Convert.ToDateTime(dsData.Tables[0].Rows[0]["dt_job"]);
                    var dtEnd = Convert.ToDateTime(dsData.Tables[0].Rows[0]["dt_end_job"]);
                    string sDuree = (dtEnd - dtStart).TotalSeconds > 60 ? Math.Round((dtEnd - dtStart).TotalMinutes, 1).ToString() + " min." : Math.Round((dtEnd - dtStart).TotalSeconds, 1).ToString() + " sec.";
                    sResult = string.Concat(sStatus, Languages.Languages.log_sqlrunningtime, sDuree);
                }
            }
            else { sResult = ""; }
            return sResult;
        }

        public void InitGetLocaleLog()
        {
            if (SQLLog != null)
            {
                string s = "";
                if (SQLLog.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                {
                    s = SQLTools.BuildStringFromDs(SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT DATETIME('1900-12-31 00:00:00');", null, "date"));
                }
                else
                {
                    s = SQLTools.BuildStringFromDs(SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT CAST('1900-12-31 00:00:00' AS DATETIME);", null, "date"));
                }

                if (s.Length < 8) //si la requête a crashé
                {
                    DATE_FORMAT = "yyyy-dd-MM HH:mm:ss";
                }
                else
                {
                    DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
                }
            }
            else
            {
                DATE_FORMAT = "";
            }
        }

        public void PrepareSQLLog(bool bUseSQLLog, SQLTools SQLLog)
        {

            if (bUseSQLLog)
            {
                if (SQLLog != null)
                {
                    this.SQLLog = SQLLog;
                    IsLogInSQL = true;
                }
                else
                { IsLogInSQL = false; }
            }
            else
            {
                this.SQLLog = null;
                IsLogInSQL = false;
            }

        }

        public void WriteQueriesErrorsIntoFile(System.Reflection.MethodBase miFunction, SQLTools_Enums.CLASS_PURPOSE stSourceOrTarget, string sQuery)
        {
            try
            {
                fiWriterQueries.WriteQueryToFile(sQuery.Replace("\r\n", string.Empty));
            }
            catch (Exception ex2)
            {
                Console.WriteLine(Toolbox.RemoveDiacritics(ex2.Message));
                _loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, SQLTools_Enums.LOG_TYPEINFO.ERR, ex2.Message));
            }
        }

        public void WriteDebugMode(string sJobID, System.Reflection.MethodBase miFunction, LogTools.DEBUG_MODE dbgMode, string sQuery)
        {
            try
            {
                //StringBuilder sbParam = new StringBuilder();
                //var parameters = miFunction.GetParameters();
                //foreach (ParameterInfo parameter in parameters)
                //{ sbParam.Append(parameter.Name); }
                fiWriterDebugMode.WriteQueryToFile(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";", sJobID, ";", miFunction.DeclaringType.Name, ";", dbgMode.ToString(), ";", sQuery.Replace("\r\n", string.Empty)));
            }
            catch (Exception ex2)
            { Console.WriteLine(Toolbox.RemoveDiacritics(ex2.Message)); }
        }

        public static void StaticMessage(System.Reflection.MethodBase miFunction, SQLTools_Enums.CLASS_PURPOSE stSourceOrTarget, Exception ex, string sMsg, SQLTools_Enums.LOG_TYPEINFO LTypeInfo, bool bRaiseEvent)
        {
            string sMessage;

            if (ex != null)
            {
                sMessage = string.Concat(ex.Message.Replace(Environment.NewLine, " "), ex.StackTrace != null ? " - StackTrace : " + ex.StackTrace.Replace(Environment.NewLine, " ") : "", " (", sMsg, ")");
            }
            else
            {
                sMessage = sMsg;
            }

            var log = new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage);


            if (bRaiseEvent) { RaiseEvent(log); }
        }

        public void LogMessage(System.Reflection.MethodBase miFunction, SQLTools_Enums.CLASS_PURPOSE stSourceOrTarget, Exception ex, string sMsg, SQLTools_Enums.LOG_TYPEINFO LTypeInfo)
        {
            if (LTypeInfo != SQLTools_Enums.LOG_TYPEINFO.INF)
            {
                if (DateTime.Now.Hour == _lastlog.Hour && DateTime.Now.Minute == _lastlog.Minute && DateTime.Now.Second == _lastlog.Second)
                { _lograte += 1; }
                else
                { _lograte = 0; }

                _lastlog = DateTime.Now;
            }
            else
            {
                if (_lograte > 10) { _lograte = 10; }
            }

            //LEVEL : 1 = erreur, 2 = info, 3 = debug
            //string sDate = System.DateTime.Now.ToString();
            string sLevel;
            string sMessage;

            if (ex != null)
            { sMessage = string.Concat(ex.Message.Replace(Environment.NewLine, " "), ex.StackTrace != null ? (Environment.NewLine + Environment.NewLine + "StackTrace : " + ex.StackTrace) : Environment.NewLine + Environment.NewLine, Environment.NewLine, Environment.NewLine, sMsg); }
            else
            { sMessage = sMsg; }

            switch (LTypeInfo)
            {
                case SQLTools_Enums.LOG_TYPEINFO.ERR:

                    sLevel = "ERR";
                    fiWriterLog.WriteToFile(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage));
                    JobErrors++;
                    lock (_loListFullLog) { _loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage)); }
                    Console.WriteLine("[" + sLevel + "] " + Toolbox.RemoveDiacritics(sMessage));
                    break;

                case SQLTools_Enums.LOG_TYPEINFO.WNG:

                    sLevel = "WNG";
                    fiWriterLog.WriteToFile(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage));
                    JobWarnings++;
                    lock (_loListFullLog) { _loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage)); }
                    Console.WriteLine("[" + sLevel + "] " + Toolbox.RemoveDiacritics(sMessage));
                    break;

                case SQLTools_Enums.LOG_TYPEINFO.INF:

                    if (LogLevel == LOG_LEVEL.ERRORS_MESSAGES || LogLevel == LOG_LEVEL.ERRORS_MESSAGES_DETAIL)
                    {
                        sLevel = "INF";
                        fiWriterLog.WriteToFile(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage));
                        lock (_loListFullLog) { _loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage)); }
                        Console.WriteLine("[" + sLevel + "] " + Toolbox.RemoveDiacritics(sMessage));
                    }
                    break;

                case SQLTools_Enums.LOG_TYPEINFO.DET:

                    if (LogLevel == LOG_LEVEL.ERRORS_MESSAGES_DETAIL)
                    {
                        fiWriterLog.WriteToFile(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage));
                        lock (_loListFullLog) { _loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage)); }
                    }
                    break;

                case SQLTools_Enums.LOG_TYPEINFO.DBG:
                    //sMessage = ExceptionTools.GetDetailledException(ex, ExceptionTools.EXCEPTION_TYPE.EXCEPTION, sMessage);
                    fiWriterDbg.WriteToFile(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage));
                    //_loListFullLog.Add(new LogObject(DateTime.Now, miFunction, stSourceOrTarget, sLevel, sMessage));
                    break;
            }

            var log = new LogObject(DateTime.Now, miFunction, stSourceOrTarget, LTypeInfo, sMessage);


            if (RaiseMessages)
            {
                RaiseEvent(log);

                //if (JobErrors > AbortJobWhenErrorsExceed)
                //{
                //    var err = new LogObject(DateTime.Now, miFunction, SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.ERR, Languages.Languages.ma_msg_toomucherrorsduringjob01 + JobErrors.ToString() + Languages.Languages.ma_msg_toomucherrorsduringjob02);
                //    lock (_loListFullLog)
                //    { _loListFullLog.Add(err); }
                //    //bAlreadySentTooMuchErrors = true;
                //    Monitoring.CancelJob();
                //    //OnTooMuchErrors?.Invoke(typeof(LogTools), JobName);
                //}
            }

            //important, il faut que ce soit en deuxième position et laisser le RaiseEvent en premier
            if (StepByStepMode)
            {
                PENDING_USER_ACTION = 1;
                if (LTypeInfo == SQLTools_Enums.LOG_TYPEINFO.INF || LTypeInfo == SQLTools_Enums.LOG_TYPEINFO.WNG || LTypeInfo == SQLTools_Enums.LOG_TYPEINFO.ERR)
                {
                    OnNewStepByStepEvent?.Invoke(typeof(LogTools), log);

                    while (PENDING_USER_ACTION == 1)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private static void RaiseEvent(LogObject log)
        {
            Task.Run(() => OnNewLogEvent?.Invoke(typeof(LogTools), log));
        }

        public void ActivateStepByStepForJob(Job currentJob)
        {
            StepByStepMode = true;
            StepByStepModeCurrentJob = currentJob;
        }

        public void ContinueStepByStepJobExecution(int iStatus)
        {
            if (iStatus == -1) //continuer jusqu'à la fin
            {
                StepByStepMode = false;
                OnNewStepByStepEvent = null;
                PENDING_USER_ACTION = 0;
            }
            else if (iStatus == 2) //annulation
            {
                PENDING_USER_ACTION = iStatus;

                StepByStepMode = false;
                OnNewStepByStepEvent = null;
                Monitoring.CancelJob();
                throw new OperationCanceledException();
            }
            else
            {
                PENDING_USER_ACTION = iStatus;
            }
        }

        public class LogObjectEventArgs : EventArgs
        {
            public LogObject Data
            {
                get; set;
            }
            public LogObjectEventArgs(LogObject data)
            {
                Data = data;
            }
        }


        public class FileWriter
        {
            public string Filepath { get; set; }

            private static readonly object locker = new();

            public void WriteStringToFile(string sData, bool bAppend)
            {
                lock (locker)
                {
                    using FileStream file = new(Filepath, bAppend ? FileMode.Append : FileMode.Truncate, FileAccess.Write, FileShare.Read);
                    using StreamWriter writer = new(file, Encoding.UTF8);
                    writer.WriteLine(sData);
                }

            }

            public void WriteToFile(LogObject LO)
            {
                lock (locker)
                {
                    using FileStream file = new(Filepath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using StreamWriter writer = new(file, Encoding.Unicode);
                    //if (new FileInfo(Filepath).Length == 0)
                    //{ writer.WriteLine("DATE;LEVEL;FUNCTION;MESSAGE"); }

                    writer.WriteLine(LO.ToString_ForFile());
                }

            }

            internal void WriteQueryToFile(string sQuery)
            {
                lock (locker)
                {
                    using FileStream file = new(Filepath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using StreamWriter writer = new(file, Encoding.Unicode);
                    //if (new FileInfo(Filepath).Length == 0)
                    //{ writer.WriteLine("DATE;LEVEL;FUNCTION;MESSAGE"); }
                    writer.WriteLine(sQuery);
                }
            }
        }

        #endregion

        #region "PRIVATE VOID"

        private void PurgeLog(bool bWithSQLPurge)
        {
            DateTime dtKeep = DateTime.Now;
            dtKeep = dtKeep.AddDays(-QuantityOfDaysToKeepSQLLog);
            int iCompare;

            string[] sLogFiles = Directory.GetFiles(PathToLog);

            foreach (string sLF in sLogFiles)
            {
                iCompare = DateTime.Compare(File.GetCreationTime(sLF), dtKeep);
                if (iCompare < 0)
                {
                    try
                    { File.Delete(sLF); }
                    catch (Exception)
                    { } // Laisse tomber
                }
            }

            if (bWithSQLPurge)
            {
                List<string> sEnteteTableLog = new()
                {
                    LogTables[0]
                };
                switch (SQLLog.Connection.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        foreach (string sTableLog in sEnteteTableLog)
                        { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sTableLog, " WHERE DATE_ADD(SYSDATE(), INTERVAL -", QuantityOfDaysToKeepSQLLog.ToString(), " DAY) < dt_job;"), DummyQuery, sTableLog); }
                        break;

                    case SQLTools_Enums.BDD.DB_ODBC:
                        //TODO
                        break;

                    case SQLTools_Enums.BDD.DB_ORACLE:
                        foreach (string sTableLog in sEnteteTableLog)
                        { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sTableLog, " WHERE dt_job < (SYSDATE - ", QuantityOfDaysToKeepSQLLog.ToString(), ");"), DummyQuery, sTableLog); }
                        break;

                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        foreach (string sTableLog in sEnteteTableLog)
                        { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sTableLog, " WHERE dt_job < CURRENT_DATE - INTERVAL '", QuantityOfDaysToKeepSQLLog.ToString(), " day';"), DummyQuery, sTableLog); }
                        break;

                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        foreach (string sTableLog in sEnteteTableLog)
                        { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sTableLog, " WHERE dt_job < DATEADD(day, -", QuantityOfDaysToKeepSQLLog.ToString(), ",GETDATE());"), DummyQuery, sTableLog); }
                        break;

                    case SQLTools_Enums.BDD.DB_SQLITE:
                        foreach (string sTableLog in sEnteteTableLog)
                        { SQLLog.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sTableLog, " WHERE DATE(\"dt_job\") < date('now', '-" + QuantityOfDaysToKeepSQLLog.ToString(), " day');"), DummyQuery, sTableLog); }
                        break;
                }
            }
        }

        public string GetRowsReportAsString()
        {
            StringBuilder sbReport = new();
            if (DtJobReport != null && DtJobReport.Rows.Count > 0)
            {
                foreach (DataColumn dc in DtJobReport.Columns)
                { sbReport.AppendLine(string.Concat("-> ", dc.ColumnName, " : ", DtJobReport.Rows[0][dc].ToString())); }
            }
            return sbReport.ToString();
        }

        public string GetFullLOGAsString()
        {
            LogObject[] lObj = LogEvents.ToArray();
            StringBuilder sbLog = new();
            foreach (LogObject lo in lObj)
            { sbLog.AppendLine(lo.ToString_ForUI()); }
            return sbLog.ToString();
        }

        public DataTable GetFullLogAsDataTable()
        {
            DataTable dtLog = new("LOG");
            dtLog.Columns.Add("Date");
            dtLog.Columns.Add("Level");
            dtLog.Columns.Add("Method");
            dtLog.Columns.Add("Function");
            dtLog.Columns.Add("Message");
            LogObject[] lObj = LogEvents.ToArray();
            StringBuilder sbLog = new();
            foreach (LogObject lo in lObj)
            {
                DataRow dr = dtLog.NewRow();
                dr["Date"] = lo.DtEvent;
                dr["Level"] = lo.TLevel;
                dr["Method"] = lo.MiMethod;
                dr["Function"] = lo.SFunction;
                dr["Message"] = lo.SMessage;
                dtLog.Rows.Add(dr);
            }
            return dtLog;
        }

        #endregion

    }

    public class LogObject
    {
        public LogObject(DateTime dtEvent, System.Reflection.MethodBase miMethod, SQLTools_Enums.CLASS_PURPOSE Cpurpose, SQLTools_Enums.LOG_TYPEINFO LTLevel, string sMessage)
        {
            DtEvent = dtEvent;
            MiMethod = miMethod;
            this.Cpurpose = Cpurpose;
            TLevel = LTLevel;
            SMessage = sMessage;
        }

        public override string ToString()
        {
            return string.Concat("[", DtEvent.ToString("HH:mm:ss"), "]", "[", TLevel.ToString(), "]", "[", Cpurpose.ToString(), "]", "[", MiMethod.DeclaringType.Name, ".", MiMethod.Name, "] ", SMessage);
        }

        public string ToLightString()
        {
            return string.Concat("[", DtEvent.ToString("HH:mm:ss"), " - ", TLevel.ToString(), "] : ", SMessage);
        }

        public string ToString_ForFile()
        {
            return string.Concat(DtEvent.ToString("HH:mm:ss"), ";", TLevel.ToString(), ";", Cpurpose.ToString(), ";", MiMethod.DeclaringType.Name, ".", MiMethod.Name, ";", SMessage.Replace(Environment.NewLine, " "));
        }

        public string ToString_ForUI()
        {
            return string.Concat(DtEvent.ToString("HH:mm:ss"), ";", TLevel.ToString(), ";", Cpurpose.ToString(), ";", MiMethod.DeclaringType.Name, ".", MiMethod.Name, ";", SMessage);
        }

        public DateTime DtEvent { get; }
        public System.Reflection.MethodBase MiMethod { get; }
        public SQLTools_Enums.CLASS_PURPOSE Cpurpose { get; }
        public SQLTools_Enums.LOG_TYPEINFO TLevel { get; }
        public string SFunction { get; }
        public string SMessage { get; }
    }

}
