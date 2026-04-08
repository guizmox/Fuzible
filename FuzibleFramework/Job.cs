using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static FuzibleFramework.LogTools;

namespace FuzibleFramework
{
    public class JobFamily
    {
        public string Name { get; set; }
        public JobFamily() { }
    }

    public class JobStats
    {
        public Job INIP { get; set; }
        public int MaxDuration { get; set; } = 0;
        public int MinDuration { get; set; } = 99999999;
        public int AvgDuration { get; set; } = 0;
        public int Iterations { get; set; } = 0;
        public int AvgErrors { get; set; } = 0;
        public int AvgWarnings { get; set; } = 0;
        public bool HasStats { get; set; } = false;
        public readonly List<string> UserExec = new List<string>();
        public readonly List<string> VersionExec = new List<string>();

        public JobStats(Job JobParams)
        {
            INIP = JobParams;
        }

        public override string ToString()
        {
            string sAvgDuration = AvgDuration >= 60
                ? $"{(AvgDuration / 60)} min. {(AvgDuration % 60)} sec."
                : $"{AvgDuration} sec.";

            string sMaxDuration = MaxDuration >= 60
                ? $"{(MaxDuration / 60)} min. {(MaxDuration % 60)} sec."
                : $"{MaxDuration} sec.";

            string sMinDuration = MinDuration >= 60
                ? $"{(MinDuration / 60)} min. {(MinDuration % 60)} sec."
                : $"{MinDuration} sec.";

            StringBuilder sbStats = new();

            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_creationdate, INIP.JobCreationDate.ToString()));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_modificationdate, INIP.JobModificationDate.ToString()));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_lastlaunchdate, INIP.JobLastLaunchDate.ToString()));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_lastlaunchstatus, INIP.JobLastLaunchStatus));
            sbStats.AppendLine(Environment.NewLine);
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_iterations, Iterations));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_avgduration, sAvgDuration));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_maxduration, sMaxDuration));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_minduration, sMinDuration));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_avgwarnings, AvgWarnings));
            sbStats.AppendLine(string.Concat(Languages.Languages.ma_msg_stats_avgerrors, AvgErrors));
            sbStats.AppendLine(Environment.NewLine);
            sbStats.AppendLine(Languages.Languages.ma_msg_stats_userexec);
            foreach (string s in UserExec)
            {
                sbStats.AppendLine(string.Concat("\t - ", s));
            }

            sbStats.AppendLine(Environment.NewLine);
            sbStats.AppendLine(Languages.Languages.ma_msg_stats_versionexec);
            foreach (string s in VersionExec)
            {
                sbStats.AppendLine(s);
            }

            return sbStats.ToString();
        }

        internal void AddUserExec(string sUser, int iIterations)
        {
            UserExec.Add(string.Concat(sUser, " -> ", iIterations, " iteration(s)"));
        }

        internal void AddStatByVersion(string sVersion, int iQueries, int iIterations, int iAvgErrors, int iAvgWarnings, int iAvgDuration, int iMinDuration, int iMaxDuration)
        {
            StringBuilder sbStats = new();

            string sAvgDuration = iAvgDuration >= 60
                ? $"{(iAvgDuration / 60)} min. {(iAvgDuration % 60)} sec."
                : $"{iAvgDuration} sec.";

            string sMaxDuration = iMaxDuration >= 60
                ? $"{(iMaxDuration / 60)} min. {(iMaxDuration % 60)} sec."
                : $"{iMaxDuration} sec.";

            string sMinDuration = iMinDuration >= 60
                ? $"{(iMinDuration / 60)} min. {(iMinDuration % 60)} sec."
                : $"{iMinDuration} sec.";

            sbStats.AppendLine(string.Concat("- VERSION ", sVersion, " (", iQueries, " ", Languages.Languages.ma_msg_stats_queries, ")"));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_iterations, iIterations));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_avgduration, sAvgDuration));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_maxduration, sMaxDuration));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_minduration, sMinDuration));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_avgwarnings, iAvgWarnings));
            sbStats.AppendLine(string.Concat("\t", Languages.Languages.ma_msg_stats_avgerrors, iAvgErrors));
            VersionExec.Add(sbStats.ToString());
        }
    }

    public class Job
    {
        #region "VARIABLES"

        private MAINParameters _GlobalParameters;

        private readonly string _sJobID = "";

        private string _sJobVersion = "V1.0.0." + Environment.UserName;
        private string _sDatabaseSource = "";
        private string _sDatabaseTarget = "";
        private string _sCSVMultipleFilesInOnePattern = "^";
        private string _sPrePostCommandInSource = "";
        private string _sPrePostCommandInTarget = "";
        private bool _bHasSQLLog = false;

        #endregion

        #region "PROPRIETES"

        public MAINParameters GlobalParameters
        {
            internal set
            {
                _GlobalParameters = value;
            }
            get
            {
                return _GlobalParameters;
            }
        }

        public static readonly string EXPORT_XML_VERSION = "1.0";

        private List<string> _sListDisallowedSQLColumns = new() { "SYNCHRO_TAG", "ROWID", "IDX_COL", "DYNPARAM" };

        public List<string> DISALLOWED_SQL_COLUMNS
        {
            get
            {

                if (!_sListDisallowedSQLColumns.Contains(TargetAddDbName) && OptionalDBName_OnInsert) { _sListDisallowedSQLColumns.Add(TargetAddDbName); }
                if (!_sListDisallowedSQLColumns.Contains(TargetAddDtLoad) && OptionalTimestamp_OnInsert) { _sListDisallowedSQLColumns.Add(TargetAddDtLoad); }
                if (!_sListDisallowedSQLColumns.Contains(TargetAddRowNum) && OptionalRowID_OnInsert) { _sListDisallowedSQLColumns.Add(TargetAddRowNum); }
                return _sListDisallowedSQLColumns;
            }
        }

        public string USER
        {
            get; internal set;
        }

        public string JobVersion { get { return _sJobVersion.Split('.').Length > 3 ? _sJobVersion[0.._sJobVersion.LastIndexOf(".")] : _sJobVersion; } set { _sJobVersion = value; } }

        public string JobVersionWithUser { get { return _sJobVersion.Split('.').Length > 3 ? _sJobVersion : _sJobVersion + "." + Environment.UserName; } }

        public List<Query> JobQueries { get; set; } = new List<Query>();

        public JobFamily JobFamily { get; set; } = new JobFamily { Name = "DEFAULT" };

        public string JobNAME { get; set; } = "";

        public string JobDescription { get; set; } = "";

        public DateTime JobCreationDate { get; set; } = DateTime.Now;

        public DateTime JobModificationDate { get; set; } = DateTime.Now;

        public DateTime JobLastLaunchDate { get; set; } = DateTime.Now;

        public string JobLastLaunchStatus { get; set; } = "Unknown Status";

        public bool Job_IsSubJob
        {
            get
            {
                if (_sJobID.IndexOf("-") > -1) { return true; } else { return false; };
            }
        }
        public int RawJobID
        {
            get
            {
                return Convert.ToInt16(_sJobID.Split(Convert.ToChar("-"))[0].Replace("[", "").Replace("]", ""));
            }
        }
        public int SubJobID
        {
            get
            {
                if (_sJobID.IndexOf("-") > -1) { return Convert.ToInt16(_sJobID.Split(Convert.ToChar("-"))[1].Replace("[", "").Replace("]", "")); } else { return 1; }
            }
        }
        public string ParentJobID
        {
            get
            {
                if (_sJobID.IndexOf("-") > -1) { return _sJobID[.._sJobID.IndexOf("-")] + "]"; } else { return _sJobID; }
            }
        }
        public bool AlterColumnTypeOnInsert { get; set; } = false;
        public int AlterColumnTypeOptions { get; set; } = 4;
        public bool CreatePrimaryKeyAfterHavingCreatedATable { get; set; } = false;
        public bool ShrinkSQLTables { get; set; } = true;
        public bool OptionalRowID_OnInsert { get; set; } = false;
        public bool OptionalRowID_OnInsert_ForceRecount { get; set; } = false;
        public bool OptionalDBName_OnInsert { get; set; } = false;
        public string OptionalDynamicParamField_OnInsert { get; set; } = "";
        public bool OptionalTimestamp_OnInsert { get; set; } = false;
        public bool TurboMode { get; set; } = false;
        public bool SynchroBypassQueryFiltersInTarget { get; set; } = false;
        public bool AbortSubJobExecutionIfErrors { get; set; } = true;
        public bool AbortSubJobExecutionIfNoData { get; set; } = true;
        public bool BypassPostJobExecutionIfErrors { get; set; } = true;
        public bool IsJobVisibleInClientApp { get; set; } = false;
        public CONNString ConnectionString_Source
        {
            get; set;
        }
        public CONNString ConnectionString_Target
        {
            get; set;
        }
        public SQLTools_Enums.JOB_PURPOSE JobMethod { get; set; } = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT;
        public bool SynchroStoreChanges { get; set; } = false;
        public bool ExportUseDataset { get; set; } = true;
        public string DatabaseName_Source
        {
            get
            {
                return _sDatabaseSource.Length > 0 ? _sDatabaseSource : ConnectionString_Source == null ? "" : Toolbox.GetDatabaseNameFromConnectionString(ConnectionString_Source.SConnString(DynParams), ConnectionString_Source.SConnDriver);
            }
            set
            {
                _sDatabaseSource = value;
            }
        }
        public string DatabaseName_Target
        {
            get
            {
                return _sDatabaseTarget.Length > 0 ? _sDatabaseTarget.Trim() : ConnectionString_Target == null ? "" : Toolbox.GetDatabaseNameFromConnectionString(ConnectionString_Target.SConnString(DynParams), ConnectionString_Target.SConnDriver);
            }
            set
            {
                _sDatabaseTarget = value;
            }
        }
        public LogTools.LOG_LEVEL LogLevel { get; set; } = LogTools.LOG_LEVEL.ERRORS_MESSAGES;
        public bool HasSqlLog
        {
            get
            {
                return _bHasSQLLog;
            }
            set
            {
                _bHasSQLLog = GlobalParameters.LOG_CONNECTIONSTRING.Trim().Length > 0 && value;
            }
        }
        public int JobPriority { get; set; } = 1;
        public int Threads_Source { get; set; } = 1;
        public int Threads_Target { get; set; } = 1;
        public bool TrimData { get; set; } = true;
        public SQLTools_Enums.TARGET_TABLE_METHOD TargetTableBehavior { get; set; } = SQLTools_Enums.TARGET_TABLE_METHOD.TRUNCATE;
        public SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR SynchroTargetTableBehavior { get; set; } = SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR.DUI;
        public bool CheckFieldsBeforeInsert { get; set; } = true;
        public SQLTools_Enums.PRECISION_COLUMN_ANALYZER FieldAnalyzerLevel { get; set; } = SQLTools_Enums.PRECISION_COLUMN_ANALYZER.HIGH_PRECISION;
        public bool UseNull_Target { get; set; } = true;
        public bool ConvertHTMLPatternsForTarget { get; set; } = false;
        public SQLTools_Enums.CSV_CLEANUP_METHOD FileCleanup_Import { get; set; } = SQLTools_Enums.CSV_CLEANUP_METHOD.RIEN;
        public List<string> LogMailAdress { get; set; } = new List<string>();
        public List<string> DynParams { get; set; } = new List<string>();
        public bool DynParams_LoopThroughRows { get { if (DynParams_LoopThroughRows_Source || DynParams_LoopThroughRows_Target) { return true; } else { return false; } } }
        public bool DynParams_LoopThroughRows_Source
        {
            get;
            set;
        } = false;
        public bool DynParams_LoopThroughRows_Target
        {
            get;
            set;
        } = false;

        public List<string> HasDynParamsWithResultFromPrePostJob
        {
            get
            {
                List<string> sParams = new();
                if (DynParams != null)
                {
                    for (int iP = 0; iP < DynParams.Count; iP++)
                    {
                        if (DynParams[iP].StartsWith("%CS") || DynParams[iP].StartsWith("%CT"))
                        {
                            sParams.Add(string.Concat("{?", (iP + 1).ToString(), "}"));
                        }
                    }
                }
                return sParams;
            }
        }
        public bool SourceTableDeleteAfterInsert { get; set; } = false;
        public bool SQLTargetBulkCopy { get; set; } = false;
        public bool SQLTargetDisableConstraints { get; set; } = false;
        public int HyperFileArrayFieldTransformation { get; set; } = 0;
        public bool RemoveIDFromMongoDBQuery { get; set; } = false;
        public bool CreatePKForMongoCollection { get; set; } = false;
        public SQLTools_Enums.TARGET_TABLE_METHOD TargetMongoCollectionBehavior { get; set; } = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
        public string DataTransformSeparatorOrLabel { get; set; } = "_";
        public bool DataTransformDontTransformIfVariableArraySizes { get; set; } = false;
        public bool DataTransformRowsToColumnsAddLabelToValues { get; set; } = false;
        public bool DataTransformAlsoCrossQueries { get; set; } = false;
        public bool AppendFileCreation { get; set; } = false;
        public string CSVCharSeparator_Target { get; set; } = ";";
        public string CSVEncoding_Target { get; set; } = "UTF8";
        public bool CSVAddQuotes { get; set; } = false;
        public int CSVRowOffset { get; set; } = 0;
        public bool CSVCharSeparator_EndRow { get; set; } = false;
        public bool CSVAddHeader { get; set; } = true;
        public bool XLSAddHeader { get; set; } = true;
        public bool XLSWithTitle { get; set; } = false;
        public string XLSStyle { get; set; } = "None";
        public bool XLSInterpretFormulas { get; set; } = false;
        public string TargetAddRowNum { get; set; } = "ROWNUM";
        public string TargetAddDbName { get; set; } = "DBNAME";
        public string TargetAddDtLoad { get; set; } = "DTLOAD";
        public string CSVMultipleFilesInOnePattern
        {
            get
            {
                return _sCSVMultipleFilesInOnePattern.Length == 0 ? "^" : _sCSVMultipleFilesInOnePattern;
            }
            set
            {
                _sCSVMultipleFilesInOnePattern = value.Length == 0 ? "^" : value;
            }
        }
        public int XLSSheetToRead { get; set; } = 1;
        public int XLSRowOffset { get; set; } = 0;
        public int XLSRowWriteOffset { get; set; } = 0;
        public string XMLHeader { get; set; } = "xml version='1.0'";
        public string JSONHeader { get; set; } = "";
        public bool XMLAddCDataTag { get; set; } = false;
        public bool XMLRemoveTagForEmptyValues { get; set; } = false;
        public string XMLTargetRowBuilder { get; set; } = "Row";
        public int XMLWriteMode { get; set; } = 0; //0=tag, 1=attributes
        public string JSONTargetRowBuilder { get; set; } = "[JOBNAME]";
        public string XLSPasswordSource { get; set; } = "";
        public string XLSPasswordTarget { get; set; } = "";
        public int MaxRowsInAFile { get; set; } = 1000000;
        public string FileSourceZippedIn { get; set; } = "";
        public string PrePostJob_CommandSource
        {
            get
            {
                return _sPrePostCommandInSource.Replace("\n", "\\n").Replace("\r", "\\r");
            }
            set
            {
                _sPrePostCommandInSource = value.Replace("\\n", "\n").Replace("\\r", "\r");
            }
        }
        public string PrePostJob_CommandTarget
        {
            get
            {
                return _sPrePostCommandInTarget.Replace("\n", "\\n").Replace("\r", "\\r");
            }
            set
            {
                _sPrePostCommandInTarget = value.Replace("\\n", "\n").Replace("\\r", "\r");
            }
        }
        public string PrePostJob_CommandSourceConnection { get; set; } = "";
        public string PrePostJob_CommandTargetConnection { get; set; } = "";
        public SQLTools_Enums.PRE_POST_JOB_COMMANDS PreOrPostCommand_Source { get; set; } = SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS;
        public SQLTools_Enums.PRE_POST_JOB_COMMANDS PreOrPostCommand_Target { get; set; } = SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS;
        public SQLTools_Enums.WEBSERVICE_METHOD WebServiceCallMethod { get; set; } = SQLTools_Enums.WEBSERVICE_METHOD.POST;
        public bool WebServiceSaveResponseFile { get; set; } = true;
        public string WebserviceContentStructure { get; set; } = "";
        public SQLTools_Enums.WEBSERVICE_METHOD WebServiceCallMethod_Target { get; set; } = SQLTools_Enums.WEBSERVICE_METHOD.POST;
        public SQLTools_Enums.WEBSERVICE_CONTENT WebServiceContentType_Target { get; set; } = SQLTools_Enums.WEBSERVICE_CONTENT.JSON;
        public SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE WebServiceRequestBodyType { get; set; } = SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.RAW_JSON;
        public SQLTools_Enums.WEBSERVICE_SQL WebserviceSQLLanguage { get; set; } = SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL;
        public string WebServiceSuccessString { get; set; } = "";
        public string WebServiceTrackingColumnInResponses { get; set; } = "";
        public string WebserviceLogTableResponses { get; set; } = "";
        public bool WebserviceHTTP_FormatURLInUpper { get; set; } = false;
        public int WebserviceHTTP_SendColumnsOffset { get; set; } = 0;
        public bool WebserviceHTTP_DontSendEmptyValues { get; set; } = true;
        public string WebserviceNuxeo_EndpointSource { get; set; } = "";
        public bool WebserviceRawOutput { get; set; } = false;
        public string WebserviceSpecialHttpParameters { get; set; } = "";
        public SQLTools_Enums.API_OPTIONS WebserviceTypeData { get; set; } = SQLTools_Enums.API_OPTIONS.NOTHING;
        public string WebserviceSourcePostWork { get; set; } = "NOTHING";

        public bool FileRawOutput { get; set; } = false;

        public string MailTargetHTMLFileTemplate { get; set; } = "";
        public string MailTargetHTMLDataSetKeyword { get; set; } = "";
        public int MailFlagRetrievedAsRead { get; set; } = 0;
        public bool MailGetUnreadOnly { get; set; } = true;
        public int MaxMailsToGet { get; set; } = 100;
        public bool MailAssembleQueriesSameRecipient
        {
            get; set;
        }
        public SQLTools_Enums.MAIL_TARGET_FORMAT MailTargetFormat { get; set; } = SQLTools_Enums.MAIL_TARGET_FORMAT.HTML_TABLE;
        public SQLTools_Enums.AD_SEARCH_SCOPE ADSearchScope { get; set; } = SQLTools_Enums.AD_SEARCH_SCOPE.AD_SUBTREE;
        public string ADSearchProperty { get; set; } = "name";
        public int ADTargetBehavior { get; set; } = 0; //0 = nothing : 1 = drop
        public bool ADActivateEntry { get; set; } = true;

        protected bool _bMultiThreadImportIntoBDDFinished = false;
        public bool KeepConnexionAfterQuery { get; set; } = false;
        public bool OnThreadPerField_OnCheck { get; set; } = false;
        public bool IsMultiThreadedImportToBDDFinished
        {
            get
            {
                return _bMultiThreadImportIntoBDDFinished;
            }
            set
            {
                _bMultiThreadImportIntoBDDFinished = value;
            }
        }
        public string JobPassword { get; private set; } = "";
        public static string[] RecoverPassword(string sEncryptedPassword, int iTestsMax)
        {
            string sDResult = "";
            string sIdJob = "";

            //on tente de récupérer l'ID du job qui a permis de chiffrer le MDP initial
            for (int iCpt = 0; iCpt < iTestsMax; iCpt++)
            {
                sDResult = FITools.EncryptionSystem.AES_Decrypt(sEncryptedPassword, iCpt.ToString());
                if (sDResult.Length > 0) { sIdJob = string.Concat("[", iCpt.ToString(), "]"); break; }
            }
            //essai avec subjobs
            if (sDResult.Length == 0)
            {
                for (int iCpt = 1; iCpt <= iTestsMax; iCpt++)
                {
                    for (int iCptB = 1; iCptB <= 10; iCptB++)
                    {
                        sDResult = FITools.EncryptionSystem.AES_Decrypt(sEncryptedPassword, string.Concat(iCpt.ToString(), "-", iCptB.ToString()));
                        if (sDResult.Length > 0) { sIdJob = string.Concat("[", iCpt.ToString(), "-", iCptB.ToString(), "]"); break; }
                    }
                    if (sDResult.Length > 0) { break; }
                }
            }

            return new string[2] { sIdJob, sDResult };
        }
        public string JobID
        {
            get
            {
                if (!_sJobID.StartsWith("["))
                {
                    return string.Concat("[", _sJobID, "]");
                }
                else { return _sJobID; }

            }
        }
        public bool RunInSimulationMode { get; set; } = false;
        public string JobCategory
        {
            get
            {
                string sCat;
                if (ConnectionString_Source != null) { sCat = ConnectionString_Source.SConnDriverSuffixFriendlyName; } else { sCat = "Unknown"; }
                sCat = string.Concat(sCat, " -> ");
                if (ConnectionString_Target != null) { sCat = string.Concat(sCat, ConnectionString_Target.SConnDriverSuffixFriendlyName); } else { sCat = string.Concat(sCat, "Unknown"); }
                return sCat;
            }
        }

        public bool AutoSQLTableCreation { get; set; } = true;
        public bool SQLDirectStream { get; set; } = false;
        public int SQLDirectStreamPriority { get; set; } = 0;
        public bool IsRunning { get; set; } = false; //variable temp pour savoir si le job tourne
        public int NoSourceDataNoError { get; set; } = 1; //0=rien, 1=avert, 2=err
        public bool SQLTrustTargetColumnType { get; set; } = false;

        internal DataRow JobAsDataRow = null;

        public List<string> LoadErrors = new();

        public int QueryRetries { get; set; } = 0;

        #endregion

        #region "PUBLIC VOID"

        public Job(MAINParameters INIFile, string sJobID, string sUsername, bool bLoadQueries, DataRow drJobParams = null)
        {
            USER = sUsername;
            _sJobID = sJobID;
            GlobalParameters = INIFile.DeepCopy();

            if (drJobParams != null) //je ne charge aucune requête pour les job "factices" (crées de toutes pièces par l'application service ou client)
            {
                JobAsDataRow = drJobParams;
                LoadErrors = LoadJobParameters(drJobParams);
                if (bLoadQueries) { LoadJobQueries(true); } //new JobQueries(this, string.Concat(sPath, "QUERIES_JOBS_", sUsername, ".INI"));
            }
        }

        public Job DeepCopy()
        {
            return (Job)this.MemberwiseClone();
        }

        public void SetJobPassword(string sPassword, string sKey, bool bAlreadyEncrypted)
        {
            if (bAlreadyEncrypted) { JobPassword = sPassword; }
            else
            {
                sKey = sKey.IndexOf("-") > 0 ? (sKey[..sKey.IndexOf("-")] + "]") : sKey; //les subjobs doivent avoir le même MDP que le job principal
                sKey = sKey.Replace("[", "").Replace("]", "");
                JobPassword = FITools.EncryptionSystem.AES_Encrypt(sPassword, sKey);
            }
        }

        public void AskForPassword(string sApp, string sUser, List<string> sListAdmin)
        {
            string[] sPwd = RecoverPassword(JobPassword, 100);
            LogTools MailLog = new(sApp, System.Reflection.MethodBase.GetCurrentMethod(), this, false);
            MailTools MTMail = new(this, SQLTools_Enums.CLASS_PURPOSE.PRG, ref MailLog);
            StringBuilder sbAskForPW = new();
            sbAskForPW.AppendLine("[" + sApp + "]");
            sbAskForPW.AppendLine(Environment.NewLine);
            sbAskForPW.AppendLine("<br><br>" + Languages.Languages.par_askpw01);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw02 + sUser);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw03 + Monitoring.GetLocalIPAddress());
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw04 + JobID);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw05 + JobNAME);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw06 + JobPassword);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw07 + sPwd[1]);
            sbAskForPW.AppendLine("<br>" + Languages.Languages.par_askpw08 + sPwd[0]);
            sbAskForPW.AppendLine("<br><br>");
            string sObjet = "[" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + "]" + Languages.Languages.par_askpw09;

            MTMail.SendMail(sObjet, sbAskForPW.ToString(), sListAdmin, null, null, true);
        }

        public static List<Query> GetFilesQueriesFromSingleQuery(Query sQ, Job INIP, bool bShowSourceOnly, ref LogTools MyLog, ref bool bCSplittedInParts)
        {
            string sFilter = sQ.QueryAnalyzer.Tables[0].Name;
            string sExt = "";
            sFilter = sFilter.RemoveWhitespace();

            bool bRecursive = false;
            if (sFilter.StartsWith("."))
            {
                bRecursive = true;
                sFilter = sFilter[1..];
            }

            sFilter = sFilter.Replace(".", "[.]");
            sFilter = sFilter.Replace("*", ".*");
            //gestion du subdir (genre \\MYFILES\\file*)
            if (sFilter.LastIndexOf("\\") > 0)
            {
                sExt = sFilter[..(sFilter.LastIndexOf("\\") + 1)];
                sFilter = sFilter[(sFilter.LastIndexOf("\\") + 1)..];
            }

            List<Query> sListQueries = new();
            List<string[]> sListFilesToImport = new();
            string sPathToImport = sQ.ConnectionSrc.SConnString(INIP.DynParams);
            if (!sPathToImport.EndsWith("\\")) { sPathToImport = string.Concat(sPathToImport, "\\"); } //connection c:\test (oubli du \)
            sPathToImport = string.Concat(sPathToImport, sExt.StartsWith("\\") ? sExt[1..] : sExt);

            if (sQ.QueryAnalyzer.Tables[0].Name.Contains('*') && !sQ.QueryAnalyzer.Tables[0].IsSubQuery) //démultiplication des requêtes
            {
                if (sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                {
                    if (sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("SFTP") > -1)
                    {
                        try { sListFilesToImport = FTPTools.GetListOfFileFromFTPOrSFTPPath(sQ.ConnectionSrc, sFilter, INIP.DynParams, bRecursive); }
                        catch (Exception) { throw; }
                    }
                    else
                    {
                        try
                        {
                            sListFilesToImport = FTPTools.GetListOfFileFromFTPOrSFTPPath(sQ.ConnectionSrc, sFilter, INIP.DynParams, bRecursive);
                        }
                        catch (Exception) { throw; }
                    }
                }
                else
                {
                    FITools.AddNetworkPathToCache(INIP.ConnectionString_Source, sQ.ConnectionSrc.SConnString(INIP.DynParams), ref MyLog);
                    if (Directory.Exists(sPathToImport))
                    {
                        string[] sFiles = bRecursive ? Directory.GetFiles(sPathToImport, "", SearchOption.AllDirectories) : Directory.GetFiles(sPathToImport, "");
                        foreach (string sFile in sFiles)
                        {
                            if (sFilter.Length > 0)
                            {
                                string sF = Path.GetFileName(sFile);
                                if (Regex.IsMatch(sF, "^" + sFilter, RegexOptions.IgnoreCase))
                                {
                                    string sAddPath = sFile.Replace(sPathToImport, "");
                                    if (sAddPath.IndexOf("\\") == -1) { sAddPath = ""; }
                                    else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("\\") + 1)]; }
                                    sListFilesToImport.Add(new string[] { sFile, sAddPath });
                                }
                            }
                            else
                            {
                                string sAddPath = sFile.Replace(sPathToImport, "");
                                if (sAddPath.IndexOf("\\") == -1) { sAddPath = ""; }
                                else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("\\") + 1)]; }
                                sListFilesToImport.Add(new string[] { sFile, sAddPath });
                            }
                        }
                    }
                }
                sListQueries = GetQueriesFromMultipleFiles(INIP, sQ, sListFilesToImport, sExt);
            }
            else
            {
                sListQueries.Add(sQ);
                if (sQ.QueryAnalyzer.Tables[0].IsSubQuery)
                {
                    Query qSub = new(INIP, sQ.QueryAnalyzer.Tables[0].Name, sQ.ConnectionSrc);
                    foreach (Query.QTable qT in qSub.QueryAnalyzer.Tables)
                    {
                        string sSubPath = qT.Name.IndexOf("\\") > 0 ? qT.Name[0..(qT.Name.IndexOf("\\") + 1)] : "";

                        sListFilesToImport.Add(new string[] { qT.Name, sSubPath });
                    }
                }
                else
                {
                    string sSubPath = sQ.QueryAnalyzer.Tables[0].Name.IndexOf("\\") > 0 ? sQ.QueryAnalyzer.Tables[0].Name[0..(sQ.QueryAnalyzer.Tables[0].Name.IndexOf("\\") + 1)] : "";
                    sListFilesToImport.Add(new string[] { sQ.QueryAnalyzer.Tables[0].Name, sSubPath });
                }
            }
            //Toolbox.RemoveSpecialCharacters(Path.GetFileNameWithoutExtension(sFile)) : Toolbox.RemoveSpecialCharacters(sForceTargetName)

            //ici on va compter le nombre de lignes dans chaque fichier CSV pour le découper en petits bouts
            //if (INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_CSV)
            //{
            //    List<string> sFilesToRemove = new List<string>();
            //    List<List<string>> sListFilesToAdd = new List<List<string>>();
            //    for (int iF = 0; iF < sListFilesToImport.Count; iF++)
            //    {
            //        bool bIsFTP = false;
            //        if (sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1) { bIsFTP = true; }

            //        List<string> sAddFiles = new List<string>();
            //        if (!bShowSourceOnly) { sAddFiles = CheckAndSplitCSV(INIP, sQ, ref MyLog, sPathToImport + Path.GetFileName(sListFilesToImport[iF]), bIsFTP); }

            //        if (sAddFiles.Count > 0) //le fichier était trop gros : il a été coupé en petits morceaux
            //        {
            //            bCSplittedInParts = true;
            //            sFilesToRemove.Add(sListFilesToImport[iF]);
            //            sListFilesToAdd.Add(sAddFiles);
            //        }
            //    }
            //    int i = -1;
            //    foreach (string sF in sFilesToRemove)
            //    {
            //        i++;
            //        int iIdx = sListFilesToImport.IndexOf(sF);
            //        sListFilesToImport.RemoveAt(iIdx);
            //        sListFilesToImport.InsertRange(iIdx, sListFilesToAdd[i]);
            //    }
            //    if (bCSplittedInParts) { sListQueries = GetQueriesFromMultipleFiles(INIP, sQ, sListFilesToImport, sExt); }
            //}

            return sListQueries;
        }

        public static List<Query> GetSQLTablesFromSingleQuery(Query sQ, Job INIP, ref LogTools MyLog)
        {
            string sFilter = sQ.QueryAnalyzer.Tables[0].Name;

            List<Query> sListQueries = new();


            string sT = sQ.QueryAnalyzer.Tables[0].Name;

            //int iMatch = Regex.Match(sT, SHSRegex.REGEX_SQL_FIELD).Index;
            bool bSubQ = Regex.Match(sT, SHSRegex.REGEX_BASIC_SUBQUERY).Success;
            //if ((iMatch != 0 || (iMatch == 0 && sT.Length == 1)) && sT.Length <= 60) //démultiplication des requêtes
            if (!bSubQ && sT.IndexOf("%") > -1) //pas de sous-requête mais un "%" qui indique un like
            {
                if (!sT.Contains("FROM ", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<string> sListeTables;
                    if (INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.NS_MONGODB)
                    {
                        NOSQLTools NOSQLAccess = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        //sListeTables = NOSQLAccess.GetAllCollectionsFromDatabase(sQ.ConnectionSrc.SConnDB, sFilter);
                        sListeTables = NOSQLAccess.GetAllCollectionsFromDatabase(INIP.DatabaseName_Source.Length == 0 ? sQ.ConnectionSrc.SConnDB : INIP.DatabaseName_Source, sFilter, Monitoring.TaskCancellationToken);
                    }
                    else
                    {
                        SQLTools SQLAccess = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        //sListeTables = SQLAccess.GetAllTablesFromDatabase(sQ.ConnectionSrc.SConnDB, sFilter, false);
                        sListeTables = SQLAccess.GetAllTablesFromDatabase(INIP.DatabaseName_Source.Length == 0 ? sQ.ConnectionSrc.SConnDB : INIP.DatabaseName_Source, sFilter, false, Monitoring.TaskCancellationToken, sQ);
                    }

                    for (int iF = 0; iF < sListeTables.Count; iF++)
                    {
                        string sFQuery = sQ.QueryAnalyzer.RawQuery;
                        string sFQuery_Start = sFQuery[..sQ.QueryAnalyzer.FromStartsAt];
                        string sFQuery_From = sFQuery[sQ.QueryAnalyzer.FromStartsAt..];
                        string sTable = sQ.QueryAnalyzer.Tables[0].Name;
                        int iPosTable = sFQuery_From.IndexOf(sTable);
                        int iLengthTable = sTable.Length;
                        sFQuery_From = sFQuery_From.Remove(iPosTable, iLengthTable);
                        sFQuery_From = sFQuery_From.Insert(iPosTable, sQ.ConnectionSrc.SqlEchappementChar + sListeTables[iF] + sQ.ConnectionSrc.SqlEchappementChar);

                        string sOutputTable = sQ.OutputTable.Replace("*", sListeTables[iF]);
                        if (sQ.OutputTable.Contains('*'))
                        {
                            if (sQ.HasMultiTarget)
                            {
                                sOutputTable = sQ.ConnectionTrg.SConnID + sOutputTable;
                            }
                        }

                        //subtilité des target "forcés" dont le "table name" n'avait pas le nom de la connexion
                        if (sQ.ConnectionTrg != INIP.ConnectionString_Target && !sQ.HasMultiTarget) { sOutputTable = string.Concat(sQ.ConnectionTrg, sOutputTable); }
                        if (sQ.ConnectionSrc != INIP.ConnectionString_Source) { sFQuery_Start = string.Concat(sQ.ConnectionSrc, sFQuery_Start); }

                        if (sQ.HasMultiTarget) //reconstruction du second target si besoin
                        {
                            Query Q = sQ.CreateSwapedTargetQuery();
                            string sOutputTable2 = Q.OutputTable.Replace("*", sListeTables[iF]);
                            if (Q.OutputTable.Contains('*'))
                            {
                                sOutputTable2 = Q.ConnectionTrg.SConnID + sOutputTable2;
                            }
                            sOutputTable = string.Concat(sOutputTable, sOutputTable2);
                        }

                        string sCrossQ = "";
                        if (sQ.CrossJoinQueries.Count > 0)
                        {
                            foreach (var q in sQ.CrossJoinQueries)
                            {
                                sCrossQ += string.Concat(Environment.NewLine, q.CrossQueryRawScript, " ", q.RawQuery);
                            }
                        }

                        sListQueries.Add(new Query(INIP, string.Concat(sOutputTable, ":", sFQuery_Start, sFQuery_From, sCrossQ)));
                    }
                }
                else { sListQueries.Add(sQ); }
            }
            else { sListQueries.Add(sQ); }

            return sListQueries;
        }

        public static List<Query> GetAPIFilesFromSingleQuery(Query sQ, Job INIP, ref LogTools MyLog)
        {
            string sFilter = sQ.QueryAnalyzer.Tables[0].Name;
            List<Query> sListQueries = new();
            string sT = sQ.QueryAnalyzer.Tables[0].Name;
            List<string> sListeTables = new();

            if (INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.WS_REST)
            {
                if (INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                {
                    sListeTables = WSTools.GetFileListFromAPIPattern(INIP, MyLog, sQ.ConnectionSrc, sFilter, sQ).Result;
                }
            }

            for (int iF = 0; iF < sListeTables.Count; iF++)
            {
                string sFQuery = sQ.QueryAnalyzer.RawQuery;
                string sFQuery_Start = sFQuery[..sQ.QueryAnalyzer.FromStartsAt];
                string sFQuery_From = sFQuery[sQ.QueryAnalyzer.FromStartsAt..];
                string sTable = sQ.QueryAnalyzer.Tables[0].Name;
                int iPosTable = sFQuery_From.IndexOf(sTable);
                int iLengthTable = sTable.Length;
                sFQuery_From = sFQuery_From.Remove(iPosTable, iLengthTable);
                sFQuery_From = sFQuery_From.Insert(iPosTable, sQ.ConnectionSrc.SqlEchappementChar + sListeTables[iF] + sQ.ConnectionSrc.SqlEchappementChar);

                string sOutputTable = sQ.OutputTable.Replace("*", sListeTables[iF]);
                if (sQ.OutputTable.Contains('*'))
                {
                    if (sQ.HasMultiTarget)
                    {
                        sOutputTable = sQ.ConnectionTrg.SConnID + sOutputTable;
                    }
                }

                //subtilité des target "forcés" dont le "table name" n'avait pas le nom de la connexion
                if (sQ.ConnectionTrg != INIP.ConnectionString_Target && !sQ.HasMultiTarget) { sOutputTable = string.Concat(sQ.ConnectionTrg, sOutputTable); }
                if (sQ.ConnectionSrc != INIP.ConnectionString_Source) { sFQuery_Start = string.Concat(sQ.ConnectionSrc, sFQuery_Start); }

                if (sQ.HasMultiTarget) //reconstruction du second target si besoin
                {
                    Query Q = sQ.CreateSwapedTargetQuery();
                    string sOutputTable2 = Q.OutputTable.Replace("*", sListeTables[iF]);
                    if (Q.OutputTable.Contains('*'))
                    {
                        sOutputTable2 = Q.ConnectionTrg.SConnID + sOutputTable2;
                    }
                    sOutputTable = string.Concat(sOutputTable, sOutputTable2);
                }

                string sCrossQ = "";
                if (sQ.CrossJoinQueries.Count > 0)
                {
                    foreach (var q in sQ.CrossJoinQueries)
                    {
                        sCrossQ += string.Concat(Environment.NewLine, q.CrossQueryRawScript, " ", q.RawQuery);
                    }
                }

                sListQueries.Add(new Query(INIP, string.Concat(sOutputTable, ":", sFQuery_Start, sFQuery_From, sCrossQ)));
            }

            return sListQueries;
        }

        public string GetQueriesAsString()
        {
            StringBuilder sbQueries = new();

            foreach (Query Q in JobQueries)
            {
                sbQueries.AppendLine(Q.RawQuery);
            }

            return sbQueries.ToString();
        }

        public JobStats LoadJobStats()
        {
            JobStats stats = new JobStats(this);
            StringBuilder sbQueries = new();

            try
            {
                CheckStatsTable();
                DataSet dsData = new();
                string sQuery = string.Concat("SELECT MAX(duration) as MaxDuration, MIN(duration) as MinDuration, AVG(duration) as AvgDuration, COUNT(id_stat) as Iterations, AVG(warnings) as AvgWarning, AVG(errors) as AvgErrors FROM job_stats WHERE \"user\" = '", USER, "' AND \"job_id\" = '", JobID, "' GROUP BY \"user\", \"job_id\";");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Stats");
                }

                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    stats.HasStats = true;
                    try { stats.MaxDuration = Convert.ToInt32(dsData.Tables[0].Rows[0]["MaxDuration"]); } catch { }
                    try { stats.MinDuration = Convert.ToInt32(dsData.Tables[0].Rows[0]["MinDuration"]); } catch { }
                    try { stats.AvgDuration = Convert.ToInt32(dsData.Tables[0].Rows[0]["AvgDuration"]); } catch { }
                    try { stats.Iterations = Convert.ToInt32(dsData.Tables[0].Rows[0]["Iterations"]); } catch { }
                    try { stats.AvgWarnings = Convert.ToInt32(dsData.Tables[0].Rows[0]["AvgWarnings"]); } catch { }
                    try { stats.AvgErrors = Convert.ToInt32(dsData.Tables[0].Rows[0]["AvgErrors"]); } catch { }
                }

                dsData = new();
                sQuery = string.Concat("SELECT COUNT(id_stat) as Iterations, \"user_exec\" as UsrExec FROM job_stats WHERE \"user\" = '", USER, "' AND \"job_id\" = '", JobID, "' GROUP BY \"user\", \"job_id\", \"user_exec\";");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Stats");
                }
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in dsData.Tables[0].Rows)
                    {
                        try { stats.AddUserExec(row["UsrExec"].ToString(), Convert.ToInt32(row["Iterations"])); } catch { }
                    }
                }

                dsData = new();
                sQuery = string.Concat("SELECT MAX(duration) as MaxDuration, MIN(duration) as MinDuration, AVG(duration) as AvgDuration, COUNT(id_stat) as Iterations, AVG(warnings) as AvgWarning, AVG(errors) as AvgErrors, AVG(queries) as AvgQueries, \"job_version\" as Version FROM job_stats WHERE \"user\" = '", USER, "' AND \"job_id\" = '", JobID, "' GROUP BY \"user\", \"job_id\", \"job_version\";");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Stats");
                }
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in dsData.Tables[0].Rows)
                    {
                        int iMaxDuration = 0;
                        int iMinDuration = 0;
                        int iAvgDuration = 0;
                        int iAvgWarnings = 0;
                        int iAvgErrors = 0;
                        int iIterations = 0;
                        int iAvgQueries = 0;
                        try { iMaxDuration = Convert.ToInt32(row["MaxDuration"]); } catch { }
                        try { iMinDuration = Convert.ToInt32(row["MinDuration"]); } catch { }
                        try { iAvgDuration = Convert.ToInt32(row["AvgDuration"]); } catch { }
                        try { iIterations = Convert.ToInt32(row["Iterations"]); } catch { }
                        try { iAvgWarnings = Convert.ToInt32(row["AvgWarnings"]); } catch { }
                        try { iAvgErrors = Convert.ToInt32(row["AvgErrors"]); } catch { }
                        try { iAvgQueries = Convert.ToInt32(row["AvgQueries"]); } catch { }
                        try { stats.AddStatByVersion(row["Version"].ToString(), iAvgQueries, iIterations, iAvgErrors, iAvgWarnings, iAvgDuration, iMinDuration, iMaxDuration); } catch { }
                    }
                }
            }
            catch (Exception) { throw; }

            return stats;
        }

        public bool SaveJobStats(double duration, int warnings, int errors, int queries)
        {
            try
            {
                CheckStatsTable();
                string sQuery = string.Concat("INSERT INTO job_stats (\"job_id\", \"user\", \"user_exec\", \"job_version\", \"duration\", \"warnings\", \"errors\", \"queries\") VALUES ('", JobID, "', '", USER, "', '", Environment.UserName, "', '", JobVersion, "', ", Convert.ToInt32(duration), ", ", warnings, ", ", errors, ", ", queries, ");");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    using (SqliteCommand sqlCreateCmd = new(sQuery, sqlConn))
                    {
                        sqlConn.Open();
                        sqlCreateCmd.ExecuteNonQuery();
                        sqlConn.Close();
                    }
                }
                return true;
            }
            catch
            { throw; }
        }

        private void CheckStatsTable()
        {
            try
            {
                DataSet dsData = new();

                //contrôle d'existence de la table des stats
                string sQuery = string.Concat("PRAGMA table_info(\"job_stats\");");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Stats");
                }
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count == 0)
                {
                    //la table doit être crée
                    sQuery = SQLQueries.CreateInternalStatsTable();
                    using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                    {
                        using (SqliteCommand sqlCreateCmd = new(sQuery, sqlConn))
                        {
                            sqlConn.Open();
                            sqlCreateCmd.ExecuteNonQuery();
                            sqlConn.Close();
                        }
                    }
                }
            }
            catch { throw; }
        }

        public string LoadJobQueries(bool bProcessAnalyzer)
        {
            StringBuilder sbQueries = new();

            List<Query> sListQueriesJob = new();

            try
            {
                DataSet dsData = new();

                string sQuery = string.Concat("SELECT \"query\" FROM job_queries WHERE \"user\" = '", USER, "' AND \"job_id\" = '", JobID, "';");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Queries");
                }
                List<string> sQueries = SQLTools.BuildListFromDs(dsData);
                foreach (string sQ in sQueries)
                {
                    string sQueryDecrypt = sQ;
                    if (!sQueryDecrypt.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                    {
                        try { sQueryDecrypt = FITools.EncryptionSystem.AES_Decrypt(sQueryDecrypt, SHSConstantes.PROGRAM_PWD); }
                        catch { }

                        if (sQ.Length > 0 && sQueryDecrypt.Length == 0) //la requête n'était pas chiffrée
                        {
                            sQueryDecrypt = sQ;
                        }
                    }

                    if (bProcessAnalyzer) { sListQueriesJob.Add(new Query(this, sQueryDecrypt)); }
                    sbQueries.AppendLine(sQueryDecrypt);
                }
            }
            catch (Exception) { throw; }

            JobQueries = sListQueriesJob;

            return sbQueries.ToString();
        }

        public static List<Query> ExtractQueriesFromString(Job INIP, string sQueries)
        {
            List<Query> sListQueriesJob = new();

            MatchCollection mC = Regex.Matches(sQueries, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
            for (int iM = 0; iM < mC.Count; iM++)
            {
                int iLength = iM == mC.Count - 1 ? sQueries.Length - mC[iM].Index : mC[iM + 1].Index - mC[iM].Index - 1;
                string sQuery = sQueries.Substring(mC[iM].Index, iLength).Trim();
                Query Q = new(INIP, sQuery);
                sListQueriesJob.Add(Q);
            }

            if (mC.Count == 0 && sQueries.Length > 0)
            {
                string sQuery = string.Concat("UNKNOWN_OUTPUT:", sQueries);
                Query Q = new(INIP, sQuery);
                sListQueriesJob.Add(Q);
            }

            return sListQueriesJob;
        }

        public string ExportJobAsXML(string sUser, string sDescription, bool bWithLastUseData, bool bEncrypt)
        {
            string sPassword = FITools.EncryptionSystem.AES_Decrypt(JobPassword, RawJobID.ToString());

            sDescription = System.Security.SecurityElement.Escape(sDescription);

            StringBuilder sbJob = new();
            sbJob.AppendLine("<?xml version='1.0'?>");

            sbJob.AppendLine("<Job>");
            sbJob.AppendLine("\t<ExportVersion>" + EXPORT_XML_VERSION + "</ExportVersion>");
            sbJob.AppendLine("\t<FuzibleVersion>" + INIProgram.APP_VERSION.ToString() + "</FuzibleVersion>");
            sbJob.AppendLine("\t<User>" + sUser + "</User>");
            sbJob.AppendLine("\t<CreationDate>" + DateTime.Now.ToString("dd/MM/yyyy") + "</CreationDate>");
            sbJob.AppendLine("\t<Description>" + sDescription + "</Description>");
            sbJob.AppendLine("\t<Encrypted>" + bEncrypt.ToString() + "</Encrypted>");

            sbJob.AppendLine("\t<JobParams>");
            if (JobAsDataRow != null)
            {
                int iC = 0;
                foreach (DataColumn dC in JobAsDataRow.Table.Columns)
                {
                    if (!bWithLastUseData && dC.ColumnName.Equals("job_modification_date")) { }
                    else if (!bWithLastUseData && dC.ColumnName.Equals("job_lastlaunch_date")) { }
                    else if (!bWithLastUseData && dC.ColumnName.Equals("job_lastlaunch_status")) { }
                    else if (!bWithLastUseData && dC.ColumnName.Equals("job_version")) { }
                    else
                    {
                        string sValue = "";
                        if (dC.ColumnName.Equals("job_id"))
                        {
                            sValue = JobAsDataRow[iC].ToString();
                        }
                        else if (dC.ColumnName.Equals("job_password"))
                        {
                            sValue = FITools.EncryptionSystem.AES_Encrypt(JobAsDataRow[iC].ToString(), RawJobID.ToString());
                        }
                        else
                        {
                            sValue = (bEncrypt ? FITools.EncryptionSystem.AES_Encrypt(JobAsDataRow[iC].ToString(), sPassword) : System.Security.SecurityElement.Escape(JobAsDataRow[iC].ToString()));
                        }

                        //sbJob.AppendLine("\t\t<" + dC.ColumnName + ">" + Toolbox.ReplaceXMLSpecials(JobAsDataRow[iC].ToString(), true) + "</" + dC.ColumnName + ">");
                        sbJob.AppendLine("\t\t<" + dC.ColumnName + ">" + sValue + "</" + dC.ColumnName + ">");
                    }
                    iC++;
                }
            }

            //récupération de toutes les connexions utilisées dans le job (source, target, queries, dyn params)
            List<string> sConnToAdd = new()
            {
                ConnectionString_Source != null ? ConnectionString_Source.SConnID : "",
                ConnectionString_Target != null ? ConnectionString_Target.SConnID : ""
            };

            sConnToAdd.RemoveAll(s => string.IsNullOrWhiteSpace(s));

            sbJob.AppendLine("\t</JobParams>");

            if (ConnectionString_Source != null) { sbJob.AppendLine(ConnectionString_Source.ExportConnStringAsXML("JobSource", sPassword)); }
            if (ConnectionString_Target != null) { sbJob.AppendLine(ConnectionString_Target.ExportConnStringAsXML("JobTarget", sPassword)); }

            CONNString csPrePostCommandSource = GlobalParameters.Connections.GetConnByID(PrePostJob_CommandSourceConnection);
            if (csPrePostCommandSource == null) { csPrePostCommandSource = ConnectionString_Source; }

            if (csPrePostCommandSource != null)
            { sbJob.AppendLine(csPrePostCommandSource.ExportConnStringAsXML("JobPrePostCommandSource", sPassword)); }

            CONNString csPrePostCommandTarget = GlobalParameters.Connections.GetConnByID(PrePostJob_CommandTargetConnection);
            if (csPrePostCommandTarget == null) { csPrePostCommandTarget = ConnectionString_Target; }

            if (csPrePostCommandTarget != null)
            { sbJob.AppendLine(csPrePostCommandTarget.ExportConnStringAsXML("JobPrePostCommandTarget", sPassword)); }

            sbJob.AppendLine("\t<JobDynamicParams>");
            sbJob.AppendLine("\t\t<Count>" + DynParams.Count.ToString() + "</Count>");

            //contrôle des dynparams
            int iDP = 0;
            foreach (string sP in DynParams)
            {
                iDP++;

                sbJob.AppendLine("\t\t<DynamicParam" + iDP.ToString() + ">");

                sbJob.AppendLine("\t\t\t<Value>" + (bEncrypt ? FITools.EncryptionSystem.AES_Encrypt(sP, sPassword) : System.Security.SecurityElement.Escape(sP)) + "</Value>");

                if (Regex.IsMatch(sP, "^\\[\\d+\\]$"))
                {
                    if (GlobalParameters.Connections.GetConnByConnString(sP) != null)
                    {
                        CONNString CSDP = GlobalParameters.Connections.GetConnByConnString(sP);
                        if (!sConnToAdd.Contains(CSDP.SConnID))
                        {
                            sConnToAdd.Add(CSDP.SConnID);
                            sbJob.AppendLine("\t\t\t<Conn>" + CSDP.SConnID + "</Conn>");
                        }
                    }
                }
                sbJob.AppendLine("\t\t</DynamicParam" + iDP.ToString() + ">");
            }
            sbJob.AppendLine("\t</JobDynamicParams>");

            sbJob.AppendLine("\t<JobQueries>");
            sbJob.AppendLine("\t\t<Count>" + JobQueries.Count.ToString() + "</Count>");

            StringBuilder sbAddConn = new();
            sbAddConn.AppendLine("\t<AdditionnalConnections>");

            for (int iQ = 0; iQ < JobQueries.Count; iQ++)
            {               
                sbJob.AppendLine("\t\t<Query" + (iQ + 1).ToString() + ">");
                sbJob.AppendLine("\t\t\t<String>" + (bEncrypt ? FITools.EncryptionSystem.AES_Encrypt(JobQueries[iQ].RawQuery, sPassword) : System.Security.SecurityElement.Escape(JobQueries[iQ].RawQuery)) + "</String>");

                if (!ConnectionString_Source.SConnID.Equals(JobQueries[iQ].ConnectionSrc.SConnID))
                {
                    if (!sConnToAdd.Contains(JobQueries[iQ].ConnectionSrc.SConnID))
                    {
                        sConnToAdd.Add(JobQueries[iQ].ConnectionSrc.SConnID);
                    }
                    sbJob.AppendLine("\t\t\t<ConnSource>" + JobQueries[iQ].ConnectionSrc.SConnID + "</ConnSource>");
                }

                if (!ConnectionString_Target.SConnID.Equals(JobQueries[iQ].ConnectionTrg.SConnID))
                {
                    if (!sConnToAdd.Contains(JobQueries[iQ].ConnectionTrg.SConnID))
                    {
                        sConnToAdd.Add(JobQueries[iQ].ConnectionTrg.SConnID);
                    }
                }
                sbJob.AppendLine("\t\t\t<ConnTargetA>" + JobQueries[iQ].ConnectionTrg.SConnID + "</ConnTargetA>");

                //contrôle des multi-target
                if (JobQueries[iQ].HasMultiTarget)
                {
                    Query Q = JobQueries[iQ].CreateSwapedTargetQuery();
                    if (!ConnectionString_Target.SConnID.Equals(Q.ConnectionTrg.SConnID))
                    {
                        if (!sConnToAdd.Contains(Q.ConnectionTrg.SConnID))
                        {
                            sConnToAdd.Add(Q.ConnectionTrg.SConnID);
                        }
                        sbJob.AppendLine("\t\t\t<ConnTargetB>" + Q.ConnectionTrg.SConnID + "</ConnTargetB>");
                    }
                }

                //contrôle des cross-join
                int iQQ = 0;
                foreach (Query Q in JobQueries[iQ].CrossJoinQueries)
                {
                    iQQ++;
                    if (!sConnToAdd.Contains(Q.ConnectionSrc.SConnID))
                    {
                        sConnToAdd.Add(Q.ConnectionSrc.SConnID);
                    }
                    sbJob.AppendLine("\t\t\t<ConnCrossJoinQuery" + iQQ.ToString() + ">" + Q.ConnectionSrc.SConnID + "</ConnCrossJoinQuery" + iQQ.ToString() + ">");
                }

                sbJob.AppendLine("\t\t</Query" + (iQ + 1).ToString() + ">");
            }

            sbAddConn.AppendLine("\t\t<Count>" + (sConnToAdd.Count - 2).ToString() + "</Count>");

            for (int iAC = 2; iAC < sConnToAdd.Count; iAC++)
            {
                sbAddConn.AppendLine(GlobalParameters.Connections.GetConnByID(sConnToAdd[iAC]).ExportConnStringAsXML((iAC - 1).ToString(), sPassword));
            }

            sbAddConn.AppendLine("\t</AdditionnalConnections>");

            sbJob.AppendLine("\t</JobQueries>");

            sbJob.AppendLine(sbAddConn.ToString().TrimEnd());

            sbJob.AppendLine("</Job>");

            return sbJob.ToString();
        }

        public string LoadQueryTargetExemple()
        {
            string sOutputDemo = "ReplaceBy_Output";
            if (ConnectionString_Target != null)
            {
                switch (ConnectionString_Target.SConnDriverSuffix)
                {
                    case "DB":
                        sOutputDemo = "ReplaceBy_AnySQLTable";
                        break;
                    case "NS":
                        sOutputDemo = "ReplaceBy_AnySQLCollection";
                        break;
                    case "FI":
                        sOutputDemo = "ReplaceBy_AnyFile." + ConnectionString_Target.SConnDriver.ToString()[3..];
                        break;
                    case "WS":
                        sOutputDemo = "ReplaceBy_AnyWebserviceURL";
                        break;
                    case "MB":
                        sOutputDemo = "ReplaceBy_AnyMail@AnyProvider.com";
                        break;
                    case "AD":
                        sOutputDemo = "groups";
                        break;
                }
                if ((FileRawOutput || WebserviceRawOutput) && ConnectionString_Target.SConnDriverSuffix.Equals("FI"))
                { sOutputDemo = "[RAWFILE_NAME]"; }

                if (ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                {
                    sOutputDemo = "ReplaceBy_AnyFileName";
                }
            }
            return sOutputDemo;
        }

        public string LoadQuerySourceExemple(ref LogTools MyLogProgram)
        {
            string sTable = "MyTable";
            if (ConnectionString_Source != null)
            {
                switch (ConnectionString_Source.SConnDriverSuffix)
                {
                    case "DB":
                        SQLTools SQL = new(this, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                        List<string> sListTables = SQL.GetAllTablesFromDatabase(this.DatabaseName_Source, "", false, Monitoring.TaskCancellationToken, new Query());
                        sTable = sListTables.Count > 0 ? sListTables[0] : "ReplaceBy_AnySQLTable";
                        break;
                    case "NS":
                        NOSQLTools NOSQL = new(this, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                        List<string> sListCollections = NOSQL.GetAllCollectionsFromDatabase(this.DatabaseName_Source, "", Monitoring.TaskCancellationToken);
                        sTable = sListCollections.Count > 0 ? sListCollections[0] : "ReplaceBy_AnySQLCollection";
                        break;
                    case "FI":
                        FITools FILE = new(this, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                        List<string> sListFiles = FILE.GetAllFilesFromPath("." + this.ConnectionString_Source.SConnDriver.ToString()[3..], false);
                        sTable = sListFiles.Count > 0 ? sListFiles[0] : "ReplaceBy_AnyFilename." + ConnectionString_Source.SConnDriver.ToString()[3..]; ;
                        break;
                    case "WS":
                        sTable = ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) switch
                        {
                            "YOUTUBE_V3" => "subscriptions?channelId=anyChannelID&maxResults=5",
                            "SALESFORCE" => "sobjects/Account/listviews",
                            "SALESFORCE_SOQL" => "Account",
                            "GLPI" => "User/?range=0-5000",
                            _ => "ReplaceBy_AnyWebserviceURL",
                        };
                        if (ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_GRAPH" && WebserviceTypeData == SQLTools_Enums.API_OPTIONS.GRAPH_DOWNLOAD_FILE_DRIVE)
                        {
                            sTable = "drives/{drive_id}/items/{item_id}/children";
                        }
                        if (ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                        {
                            sTable = "ReplaceByAnyFileWhichIsOnTheDrive";
                        }
                        break;
                    case "MB":
                        MailTools MT = new(this, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                        string sMail = MT.MAILVariables.SenderAddress;
                        try
                        {
                            string sProvider = MT.MAILVariables.SenderAddress.Split(Convert.ToChar("@"))[1];
                            sTable = "AnyMail@" + sProvider + "[AssociatedPassword]";
                        }
                        catch { sTable = sMail; }
                        break;
                    case "AD":
                        sTable = "groups";
                        break;
                }
            }
            return sTable;
        }

        #endregion

        #region "PRIVATE VOID"

        private static List<Query> GetQueriesFromMultipleFiles(Job INIP, Query sQ, List<string[]> sListFilesToImport, string sExt)
        {
            List<Query> sListQueries = new();

            for (int iF = 0; iF < sListFilesToImport.Count; iF++)
            {
                string sFilename = sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1 ? sListFilesToImport[iF][0] : Path.GetFileName(sListFilesToImport[iF][0]);

                string sFQuery = sQ.QueryAnalyzer.RawQuery;
                string sFQuery_Start = sFQuery[..sQ.QueryAnalyzer.FromStartsAt];
                string sFQuery_From = sFQuery[sQ.QueryAnalyzer.FromStartsAt..];
                string sTable = sQ.QueryAnalyzer.Tables[0].Name;
                int iPosTable = sFQuery_From.IndexOf(sTable);
                int iLengthTable = sTable.Length;
                sFQuery_From = sFQuery_From.Remove(iPosTable, iLengthTable);
                sFQuery_From = sFQuery_From.Insert(iPosTable, sExt + sFilename);
                //sFQuery_From = string.Concat(sExt, sFQuery_From);

                string sOutputTable = "";
                if (sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                {
                    sOutputTable = sQ.OutputTable.Equals("*") ? sFilename : sQ.OutputTable.Replace("*", sFilename);
                }
                else
                {
                    if (sQ.OutputTable.Equals("*"))
                    { sOutputTable = sListFilesToImport[iF][1] + sFilename; }
                    else
                    {
                        //exemple du *_OUT.CSV:SELECT * FROM *.CSV
                        //pour éviter d'avoir SAMPLE.CSV_OUT.CSV mais SAMPLE_OUT.CSV
                        string sOutputExt = Path.GetExtension(sQ.OutputTable);
                        if (sOutputExt.Length > 0)
                        {
                            sOutputTable = sQ.OutputTable.Replace("*", sListFilesToImport[iF][1] + sFilename[0..sFilename.LastIndexOf(".")]);
                        }
                        else
                        {
                            string sInputExt = Path.GetExtension(sFilename);
                            //sOutputTable = sQ.OutputTable.Replace("*", sListFilesToImport[iF][1] + sFilename[0..sFilename.LastIndexOf(".")]) + sInputExt;
                            //GT août 2024 : je ne sais pas pourquoi il y avait le sInputExt : 
                            sOutputTable = sQ.OutputTable.Replace("*", sListFilesToImport[iF][1] + sFilename[0..sFilename.LastIndexOf(".")]);
                        }
                    }
                }

                //subtilité des target "forcés" dont le "table name" n'avait pas le nom de la connexion
                if (sQ.ConnectionTrg != INIP.ConnectionString_Target) { sOutputTable = string.Concat(sQ.ConnectionTrg, sOutputTable); }
                if (sQ.ConnectionSrc != INIP.ConnectionString_Source) { sFQuery_Start = string.Concat(sQ.ConnectionSrc, sFQuery_Start); }

                if (sQ.HasMultiTarget) //reconstruction du second target si besoin
                {
                    Query Q = sQ.CreateSwapedTargetQuery();

                    string sOutputTable2 = "";
                    if (sQ.ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                    {
                        sOutputTable2 = sQ.OutputTable.Equals("*") ? sFilename : sQ.OutputTable.Replace("*", sFilename);
                    }
                    else
                    {
                        sOutputTable2 = Q.OutputTable.Equals("*") ? sListFilesToImport[iF][1] + sFilename : Q.OutputTable.Replace("*", sListFilesToImport[iF][1] + sFilename);
                    }

                    sOutputTable = string.Concat(sOutputTable, Q.ConnectionTrg, sOutputTable2);
                }

                string sCrossQ = "";
                if (sQ.CrossJoinQueries.Count > 0)
                {
                    foreach (var q in sQ.CrossJoinQueries)
                    {
                        sCrossQ += string.Concat(Environment.NewLine, q.CrossQueryRawScript, " ", q.RawQuery);
                    }
                }

                sListQueries.Add(new Query(INIP, string.Concat(sOutputTable, ":", sFQuery_Start, sFQuery_From, sCrossQ)));
            }
            return sListQueries;
        }

        //private static List<string> CheckAndSplitCSV(Job INIP, Query sQ, ref LogTools MyLog, string sFile, bool bIsFTP)
        //{
        //    //List<string> sListFiles = new List<string>();
        //    //int iLineCount = 0;

        //    //if (bIsFTP)
        //    //{
        //    //    if (INIP.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT)
        //    //    {
        //    //        //download préalable pour compter les lignes
        //    //        FTPTools FTP = new FTPTools(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
        //    //        sFile = FTP.GetFileFromFTPOrSFTPPath(sFile);
        //    //    }
        //    //}
        //    //if (sFile.Length > 0 && File.Exists(sFile))
        //    //{
        //    //    using (var reader = File.OpenText(sFile))
        //    //    {
        //    //        while (reader.ReadLine() != null)
        //    //        { iLineCount++; }
        //    //    }
        //    //    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Path.GetFileName(sFile) + " : " + iLineCount.ToString() + Languages.Languages.par_splitcsv01, SQLTools_Enums.LOG_TYPEINFO.DET);

        //    //    //si nombre de lignes > nombre toléré (RAM) on découpe en plusieurs fichiers
        //    //    if (iLineCount > INIP.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT)
        //    //    {
        //    //        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_splitcsv02 + Path.GetFileName(sFile) + " (" + iLineCount.ToString() + Languages.Languages.par_splitcsv01 + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
        //    //        string[] sFileInRam;
        //    //        List<string> sListFileInRam = new List<string>();

        //    //        var fs = new FileStream(sFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        //    //        StreamReader sr = new StreamReader(fs);

        //    //        int iR = -1;
        //    //        while (iR < (10000 > iLineCount ? iLineCount : 10000))
        //    //        {
        //    //            iR++;
        //    //            sListFileInRam.Add(sr.ReadLine());
        //    //        }
        //    //        sr.Close();
        //    //        sFileInRam = sListFileInRam.ToArray();
        //    //        sListFileInRam.Clear();
        //    //        string sCSVCharSeparator = Toolbox.CSVCharSeparator(sFileInRam, INIP.GlobalParameters.FILE_MAX_ROWS_ANALYZER, INIP.GlobalParameters.CSV_SEPARATORS);
        //    //        sQ.QueryAnalyzer.AddQueryProperty(Path.GetFileName(sFile), SQLTools_Enums.QUERY_PROPERTIES.CSV_SEPARATOR, sCSVCharSeparator, true);

        //    //        SHSOperations SHS = new SHSOperations(INIP, sQ, ref MyLog);
        //    //        bool bHeader = SHS.IsFirstRowHeader(sFileInRam, sCSVCharSeparator);
        //    //        sQ.QueryAnalyzer.AddQueryProperty(Path.GetFileName(sFile), SQLTools_Enums.QUERY_PROPERTIES.CSV_HASHEADER, bHeader.ToString(), true);

        //    //        string sHeader = bHeader ? sFileInRam[0] : "";

        //    //        int iQteSplit = (iLineCount / INIP.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT);
        //    //        string sPath = sFile.Substring(0, sFile.LastIndexOf("\\") + 1);
        //    //        for (int i = 0; i <= iQteSplit; i++)
        //    //        {
        //    //            string sF = sFile[(sFile.LastIndexOf("\\") + 1)..];
        //    //            sF = string.Concat((i + 1).ToString("0000"), "_", sF);
        //    //            sListFiles.Add(sF);
        //    //        }

        //    //        var fsB = new FileStream(sFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        //    //        StreamReader srB = new StreamReader(fsB);

        //    //        StreamWriter sw = null;
        //    //        int iRB = -1;
        //    //        int iNextFile = -1;
        //    //        while (!srB.EndOfStream)
        //    //        {
        //    //            iRB++;
        //    //            if (iRB % INIP.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT == 0)
        //    //            {
        //    //                if (sw != null) { sw.Close(); }
        //    //                iNextFile++;

        //    //                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_splitcsv05 + sListFiles[iNextFile], SQLTools_Enums.LOG_TYPEINFO.DET);

        //    //                sw = new StreamWriter(sPath + sListFiles[iNextFile], false, srB.CurrentEncoding);
        //    //                if (bHeader && iRB > 0) { sw.WriteLine(sHeader); } //irb > 0 parce que sinon il écrit le header en double sur le 1er fichier
        //    //            }
        //    //            sw.WriteLine(srB.ReadLine());
        //    //        }
        //    //        srB.Close();
        //    //        if (sw != null) { sw.Close(); }

        //    //        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Path.GetFileName(sFile) + Languages.Languages.par_splitcsv03 + iQteSplit.ToString() + Languages.Languages.par_splitcsv04, SQLTools_Enums.LOG_TYPEINFO.INF);
        //    //    }
        //    //}
        //    //return sListFiles;
        //}

        private List<string> LoadJobParameters(DataRow dr)
        {
            List<string> sErrors = new();

            try
            {
                try { JobNAME = dr["job_name"].ToString(); } catch { sErrors.Add("job_name"); }
                try { JobFamily = new JobFamily { Name = dr["job_family"].ToString() }; } catch { sErrors.Add("job_family"); }
                try { SetJobPassword(dr["job_password"].ToString(), "", true); } catch { sErrors.Add("job_password"); }
                try { JobPriority = Convert.ToInt16(dr["job_priority"].ToString()); } catch { sErrors.Add("job_priority"); }
                try { JobDescription = dr["job_description"].ToString(); } catch { sErrors.Add("job_description"); }

                DateTime dt;
                DateTime.TryParse(dr["job_creation_date"].ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
                JobCreationDate = dt;
                DateTime.TryParse(dr["job_modification_date"].ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
                JobModificationDate = dt;
                DateTime.TryParse(dr["job_lastlaunch_date"].ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
                JobLastLaunchDate = dt;

                try { JobLastLaunchStatus = dr["job_lastlaunch_status"].ToString(); } catch { sErrors.Add("job_lastlaunch_status"); }
                try { IsJobVisibleInClientApp = Convert.ToBoolean(dr["job_visibility"]); } catch { sErrors.Add("job_visibility"); }
                try { AbortSubJobExecutionIfErrors = Convert.ToBoolean(dr["subjob_abort_on_errors"]); } catch { sErrors.Add("subjob_abort_on_errors"); }
                try { AbortSubJobExecutionIfNoData = Convert.ToBoolean(Convert.ToInt32(dr["subjob_abort_on_nodata"])); } catch { sErrors.Add("subjob_abort_on_nodata"); }
                try { BypassPostJobExecutionIfErrors = Convert.ToBoolean(dr["postcommands_bypass_on_errors"]); } catch { sErrors.Add("postcommands_bypass_on_errors"); }
                try { TurboMode = Convert.ToBoolean(dr["turbo_mode"]); } catch { sErrors.Add("turbo_mode"); }
                try { DynParams = Toolbox.FromStringToList(dr["source_queries_dynamic_parameters"].ToString()); } catch { sErrors.Add("source_queries_dynamic_parameters"); }
                try { SynchroTargetTableBehavior = (SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR)Enum.Parse(typeof(SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR), dr["synchro_target_table_behavior"].ToString()); } catch { sErrors.Add("synchro_target_table_behavior"); }
                try { SynchroStoreChanges = Convert.ToBoolean(dr["synchro_store_changes"]); } catch { sErrors.Add("synchro_store_changes"); }
                try { SynchroBypassQueryFiltersInTarget = Convert.ToBoolean(dr["synchro_bypass_query_filters_in_target"]); } catch { sErrors.Add("synchro_bypass_query_filters_in_target"); }
                try { ConnectionString_Source = GlobalParameters.Connections.GetConnByID(dr["connexion_string_export"].ToString()); } catch { sErrors.Add("connexion_string_export"); }
                try { ConnectionString_Target = GlobalParameters.Connections.GetConnByID(dr["connexion_string_import"].ToString()); } catch { sErrors.Add("connexion_string_import"); }
                try { DatabaseName_Source = dr["database_source"].ToString(); } catch { sErrors.Add("database_source"); }
                try { DatabaseName_Target = dr["database_target"].ToString(); } catch { sErrors.Add("database_target"); }
                try { JobMethod = (SQLTools_Enums.JOB_PURPOSE)Enum.Parse(typeof(SQLTools_Enums.JOB_PURPOSE), dr["export_import"].ToString()); } catch { sErrors.Add("export_import"); }
                try { ExportUseDataset = Convert.ToBoolean(dr["export_use_dataset"]); } catch { sErrors.Add("export_use_dataset"); }
                try { SQLDirectStream = Convert.ToBoolean(dr["sql_directstream_copy"]); } catch { sErrors.Add("sql_directstream_copy"); }
                try { SQLTrustTargetColumnType = Convert.ToBoolean(dr["sql_trust_target_columns"]); } catch { sErrors.Add("sql_trust_target_columns"); }
                try { SQLDirectStreamPriority = Convert.ToInt32(dr["sql_directstream_priority"].ToString()); } catch { sErrors.Add("sql_directstream_priority"); }
                try { LogLevel = (LogTools.LOG_LEVEL)Enum.Parse(typeof(LogTools.LOG_LEVEL), dr["log_level"].ToString()); } catch { sErrors.Add("log_level"); }
                try { HasSqlLog = Convert.ToBoolean(dr["sql_log"]); } catch { sErrors.Add("sql_log"); }
                try { Threads_Source = Convert.ToInt32(dr["threads_export"].ToString()); } catch { sErrors.Add("threads_export"); }
                try { Threads_Target = Convert.ToInt32(dr["threads_import"].ToString()); } catch { sErrors.Add("threads_import"); }
                try { SourceTableDeleteAfterInsert = Convert.ToBoolean(dr["delete_source_data_after_insert"]); } catch { sErrors.Add("delete_source_data_after_insert"); }

                //0 = rien, 1 = source, 2 = target, 3 = source+target
                int iLoop = -1;
                try { iLoop = Convert.ToInt32(dr["prepost_commands_loopjobthroughresults"]); } catch { sErrors.Add("prepost_commands_loopjobthroughresults"); }
                DynParams_LoopThroughRows_Source = iLoop != 0 && (iLoop == 1 || iLoop != 2);
                DynParams_LoopThroughRows_Target = iLoop != 0 && iLoop != 1 && (iLoop == 2 || true);

                try { TrimData = Convert.ToBoolean(dr["trim_data"]); } catch { sErrors.Add("trim_data"); }
                try { TargetTableBehavior = (SQLTools_Enums.TARGET_TABLE_METHOD)Enum.Parse(typeof(SQLTools_Enums.TARGET_TABLE_METHOD), dr["drop_table_before_insert"].ToString()); } catch { sErrors.Add("drop_table_before_insert"); }
                try { CheckFieldsBeforeInsert = Convert.ToBoolean(dr["check_fields_before_insert"]); } catch { sErrors.Add("check_fields_before_insert"); }
                try { FieldAnalyzerLevel = (SQLTools_Enums.PRECISION_COLUMN_ANALYZER)Enum.Parse(typeof(SQLTools_Enums.PRECISION_COLUMN_ANALYZER), dr["field_analyser_level"].ToString()); } catch { sErrors.Add("field_analyser_level"); }
                try { UseNull_Target = Convert.ToBoolean(dr["use_null_import"]); } catch { sErrors.Add("use_null_import"); }
                try { ConvertHTMLPatternsForTarget = Convert.ToBoolean(dr["convert_html_pattern_for_target"]); } catch { sErrors.Add("convert_html_pattern_for_target"); }
                try { AlterColumnTypeOnInsert = Convert.ToBoolean(dr["allow_alter_column_target"]); } catch { sErrors.Add("allow_alter_column_target"); }
                try { AlterColumnTypeOptions = Convert.ToInt32(dr["allow_alter_column_target_onlysafe"]); } catch { sErrors.Add("allow_alter_column_target_onlysafe"); }
                try { SQLTargetDisableConstraints = Convert.ToBoolean(dr["disable_constraints_target"]); } catch { sErrors.Add("disable_constraints_target"); }
                try { SQLTargetBulkCopy = Convert.ToBoolean(dr["sql_target_bulk_copy"]); } catch { sErrors.Add("sql_target_bulk_copy"); }
                try { CreatePrimaryKeyAfterHavingCreatedATable = Convert.ToBoolean(dr["create_sql_primary_key_in_target"]); } catch { sErrors.Add("create_sql_primary_key_in_target"); }
                try { FileCleanup_Import = (SQLTools_Enums.CSV_CLEANUP_METHOD)Enum.Parse(typeof(SQLTools_Enums.CSV_CLEANUP_METHOD), dr["import_file_cleanup"].ToString()); } catch { sErrors.Add("import_file_cleanup"); }
                try { LogMailAdress = dr["log_mail_adress"].ToString().Length == 0 ? new List<string>() : new List<string>(dr["log_mail_adress"].ToString().Split(Convert.ToChar(";"))); } catch { sErrors.Add("log_mail_adress"); }
                try { HyperFileArrayFieldTransformation = Convert.ToInt16(dr["hyperfile_arrayfield_transform"].ToString()); } catch { sErrors.Add("hyperfile_arrayfield_transform"); }
                try { DataTransformSeparatorOrLabel = dr["hyperfile_arrayfield_transform_separator"].ToString(); } catch { sErrors.Add("hyperfile_arrayfield_transform_separator"); }
                try { DataTransformDontTransformIfVariableArraySizes = Convert.ToBoolean(dr["csv_dont_transform_with_invalid_header"]); } catch { sErrors.Add("csv_dont_transform_with_invalid_header"); }
                try { DataTransformRowsToColumnsAddLabelToValues = Convert.ToBoolean(dr["data_transform_rows_columns_add_label_values"]); } catch { sErrors.Add("data_transform_rows_columns_add_label_values"); }
                try { DataTransformAlsoCrossQueries = Convert.ToBoolean(dr["data_transform_cross_queries"]); } catch { sErrors.Add("data_transform_cross_queries"); }
                try { FileSourceZippedIn = dr["source_files_zipped_in"].ToString(); } catch { sErrors.Add("source_files_zipped_in"); }
                try { AppendFileCreation = Convert.ToBoolean(dr["append_file_creation"]); } catch { sErrors.Add("append_file_creation"); }
                try { CSVCharSeparator_Target = dr["csv_char_separator"].ToString(); } catch { sErrors.Add("csv_char_separator"); }
                try { CSVEncoding_Target = dr["csv_encoding_target"].ToString(); } catch { sErrors.Add("csv_encoding_target"); }
                try { CSVMultipleFilesInOnePattern = dr["csv_multiple_files_in_one_pattern"].ToString(); } catch { sErrors.Add("csv_multiple_files_in_one_pattern"); }
                try { CSVCharSeparator_EndRow = Convert.ToBoolean(dr["csv_separator_end_row"]); } catch { sErrors.Add("csv_separator_end_row"); }
                try { CSVAddHeader = Convert.ToBoolean(dr["csv_target_add_header"]); } catch { sErrors.Add("csv_target_add_header"); }
                try { CSVRowOffset = Convert.ToInt16(dr["csv_row_offset"].ToString()); } catch { sErrors.Add("csv_row_offset"); }
                try { CSVAddQuotes = Convert.ToBoolean(dr["csv_add_quotes"]); } catch { sErrors.Add("csv_add_quotes"); }
                try { XLSSheetToRead = Convert.ToInt16(dr["xls_sheet_to_read"].ToString()); } catch { sErrors.Add("xls_sheet_to_read"); }
                try { XLSRowOffset = Convert.ToInt16(dr["xls_row_offset"].ToString()); } catch { sErrors.Add("xls_row_offset"); }
                try { XLSRowWriteOffset = Convert.ToInt16(dr["xls_row_write_offset"].ToString()); } catch { sErrors.Add("xls_row_write_offset"); }
                try { XLSPasswordSource = FITools.EncryptionSystem.AES_Decrypt(dr["xls_password_source"].ToString(), SHSConstantes.PROGRAM_PWD); } catch { sErrors.Add("xls_password_source"); }
                try { XLSPasswordTarget = FITools.EncryptionSystem.AES_Decrypt(dr["xls_password_target"].ToString(), SHSConstantes.PROGRAM_PWD); } catch { sErrors.Add("xls_password_target"); }
                try { XLSAddHeader = Convert.ToBoolean(dr["xls_target_add_header"]); } catch { sErrors.Add("xls_target_add_header"); }
                try { XLSWithTitle = Convert.ToBoolean(dr["xls_target_with_title"]); } catch { sErrors.Add("xls_target_with_title"); }
                try { XLSInterpretFormulas = Convert.ToBoolean(dr["xls_write_interpret_formulas"]); } catch { sErrors.Add("xls_write_interpret_formulas"); }
                try { XLSStyle = dr["xls_target_style"].ToString(); } catch { sErrors.Add("xls_target_style"); }
                try { MaxRowsInAFile = Convert.ToInt32(dr["max_rows_in_file"].ToString()); } catch { sErrors.Add("max_rows_in_file"); }
                try { XMLAddCDataTag = Convert.ToBoolean(dr["xml_cdata_tag"]); } catch { sErrors.Add("xml_cdata_tag"); }
                try { XMLRemoveTagForEmptyValues = Convert.ToBoolean(dr["xml_remove_tag_for_empty_values"]); } catch { sErrors.Add("xml_remove_tag_for_empty_values"); }
                try { XMLHeader = dr["xml_header"].ToString(); } catch { sErrors.Add("xml_header"); }
                try { JSONHeader = dr["json_header"].ToString(); } catch { sErrors.Add("json_header"); }
                try { XMLTargetRowBuilder = dr["xml_target_row_builder"].ToString(); } catch { sErrors.Add("xml_target_row_builder"); }
                try { XMLWriteMode = Convert.ToInt32(dr["xml_write_mode"].ToString()); } catch { sErrors.Add("xml_write_mode"); }
                try { JSONTargetRowBuilder = dr["json_target_row_builder"].ToString(); } catch { sErrors.Add("json_target_row_builder"); }
                try { WebserviceNuxeo_EndpointSource = dr["webservice_nuxeo_api_endpoint"].ToString(); } catch { sErrors.Add("webservice_nuxeo_api_endpoint"); }
                try { WebServiceCallMethod = (SQLTools_Enums.WEBSERVICE_METHOD)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_METHOD), dr["webservice_call_method"].ToString()); } catch { sErrors.Add("webservice_call_method"); }
                try { WebServiceCallMethod_Target = (SQLTools_Enums.WEBSERVICE_METHOD)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_METHOD), dr["webservice_call_method_target"].ToString()); } catch { sErrors.Add("webservice_call_method_target"); }
                try { WebServiceContentType_Target = (SQLTools_Enums.WEBSERVICE_CONTENT)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_CONTENT), dr["webservice_content_type_target"].ToString()); } catch { sErrors.Add("webservice_content_type_target"); }
                try { WebServiceRequestBodyType = (SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE), dr["webservice_request_body_type"].ToString()); } catch { sErrors.Add("webservice_request_body_type"); }
                try { WebserviceHTTP_DontSendEmptyValues = Convert.ToBoolean(dr["webservice_dont_send_empty_values"]); } catch { sErrors.Add("webservice_dont_send_empty_values"); }
                try { WebserviceTypeData = (SQLTools_Enums.API_OPTIONS)Enum.Parse(typeof(SQLTools_Enums.API_OPTIONS), dr["api_get_typedata"].ToString()); } catch { sErrors.Add("api_get_typedata"); }
                try { WebserviceSourcePostWork = dr["api_source_postwork"].ToString(); } catch { sErrors.Add("api_source_postwork"); }
                try { WebserviceRawOutput = Convert.ToBoolean(dr["ws_raw_output"]); } catch { sErrors.Add("ws_raw_output"); }
                try { FileRawOutput = Convert.ToBoolean(dr["file_raw_output"]); } catch { sErrors.Add("file_raw_output"); }
                try { OptionalDBName_OnInsert = Convert.ToBoolean(dr["sql_add_dbname_column"]); } catch { sErrors.Add("sql_add_dbname_column"); }
                try { OptionalRowID_OnInsert = Convert.ToBoolean(dr["sql_add_rownum_column"]); } catch { sErrors.Add("sql_add_rownum_column"); }
                try { OptionalTimestamp_OnInsert = Convert.ToBoolean(dr["sql_add_timestamp_column"]); } catch { sErrors.Add("sql_add_timestamp_column"); }
                try { OptionalDynamicParamField_OnInsert = dr["sql_add_dynamicparam_column"].ToString(); } catch { sErrors.Add("sql_add_dynamicparam_column"); }
                try { TargetAddRowNum = dr["target_add_rownum"].ToString(); } catch { sErrors.Add("target_add_rownum"); }
                try { TargetAddDbName = dr["target_add_dbname"].ToString(); } catch { sErrors.Add("target_add_dbname"); }
                try { TargetAddDtLoad = dr["target_add_dtload"].ToString(); } catch { sErrors.Add("target_add_dtload"); }
                try { ShrinkSQLTables = Convert.ToBoolean(dr["sql_shrink_tables"]); } catch { sErrors.Add("sql_shrink_tables"); }
                try { PrePostJob_CommandSource = dr["post_job_sql_command_source"].ToString(); } catch { sErrors.Add("post_job_sql_command_source"); }
                try { PrePostJob_CommandTarget = dr["post_job_sql_command_target"].ToString(); } catch { sErrors.Add("post_job_sql_command_target"); }
                try { PrePostJob_CommandSourceConnection = dr["post_job_sql_command_source_conn"].ToString(); } catch { sErrors.Add("post_job_sql_command_source"); }
                try { PrePostJob_CommandTargetConnection = dr["post_job_sql_command_target_conn"].ToString(); } catch { sErrors.Add("post_job_sql_command_target"); }
                try { PreOrPostCommand_Source = (SQLTools_Enums.PRE_POST_JOB_COMMANDS)Enum.Parse(typeof(SQLTools_Enums.PRE_POST_JOB_COMMANDS), dr["pre_or_post_job_commands_source"].ToString()); } catch { sErrors.Add("pre_or_post_job_commands_source"); }
                try { PreOrPostCommand_Target = (SQLTools_Enums.PRE_POST_JOB_COMMANDS)Enum.Parse(typeof(SQLTools_Enums.PRE_POST_JOB_COMMANDS), dr["pre_or_post_job_commands_target"].ToString()); } catch { sErrors.Add("pre_or_post_job_commands_target"); }
                try { WebserviceHTTP_FormatURLInUpper = Convert.ToBoolean(dr["ws_http_format_url_in_upper"]); } catch { sErrors.Add("ws_http_format_url_in_upper"); }
                try { WebserviceHTTP_SendColumnsOffset = Convert.ToInt16(dr["ws_http_send_columns_offset"].ToString()); } catch { sErrors.Add("ws_http_send_columns_offset"); }
                try { WebServiceSaveResponseFile = Convert.ToBoolean(dr["ws_save_webresponse_file"]); } catch { sErrors.Add("ws_save_webresponse_file"); }
                try { WebserviceContentStructure = dr["ws_content_structure"].ToString(); } catch { sErrors.Add("ws_content_structure"); }
                try { WebServiceSuccessString = dr["ws_success_string"].ToString(); } catch { sErrors.Add("ws_success_string"); }
                try { WebserviceLogTableResponses = dr["ws_logtable_webresponses"].ToString(); } catch { sErrors.Add("ws_logtable_webresponses"); }
                try { WebserviceSpecialHttpParameters = dr["webservice_special_http_parameters"].ToString(); } catch { sErrors.Add("webservice_special_http_parameters"); }
                try { WebServiceTrackingColumnInResponses = dr["ws_tracking_column_webresponse"].ToString(); } catch { sErrors.Add("ws_tracking_column_webresponse"); }
                try { MailGetUnreadOnly = Convert.ToBoolean(dr["mb_get_unread_only"]); } catch { sErrors.Add("mb_get_unread_only"); }
                try { MailFlagRetrievedAsRead = Convert.ToInt16(dr["mb_flag_retrieved_as_read"]); } catch { sErrors.Add("mb_flag_retrieved_as_read"); }
                try { MailAssembleQueriesSameRecipient = Convert.ToBoolean(dr["mb_assemble_queries_same_recipient"]); } catch { sErrors.Add("mb_assemble_queries_same_recipient"); }
                try { MailTargetFormat = (SQLTools_Enums.MAIL_TARGET_FORMAT)Enum.Parse(typeof(SQLTools_Enums.MAIL_TARGET_FORMAT), dr["mail_target_format"].ToString()); } catch { sErrors.Add("mail_target_format"); }
                try { MailTargetHTMLFileTemplate = dr["mb_target_html_file_template"].ToString(); } catch { sErrors.Add("mb_target_html_file_template"); }
                try { MailTargetHTMLDataSetKeyword = dr["mb_target_html_dataset_keyword"].ToString(); } catch { sErrors.Add("mb_target_html_dataset_keyword"); }
                try { MaxMailsToGet = Convert.ToInt32(dr["mb_max_mails_to_get"].ToString()); } catch { sErrors.Add("mb_max_mails_to_get"); }
                try { ADSearchScope = (SQLTools_Enums.AD_SEARCH_SCOPE)Enum.Parse(typeof(SQLTools_Enums.AD_SEARCH_SCOPE), dr["ad_search_scope"].ToString()); } catch { sErrors.Add("ad_search_scope"); }
                try { ADSearchProperty = dr["ad_target_search_property"].ToString(); } catch { sErrors.Add("ad_target_search_property"); }
                try { ADTargetBehavior = Convert.ToInt32(dr["ad_target_behavior"].ToString()); } catch { sErrors.Add("ad_target_behavior"); }
                try { ADActivateEntry = Convert.ToBoolean(dr["ad_activate_new_entries"]); } catch { sErrors.Add("ad_activate_new_entries"); }
                try { RemoveIDFromMongoDBQuery = Convert.ToBoolean(dr["remove_id_from_mongodb_query"]); } catch { sErrors.Add("remove_id_from_mongodb_query"); }
                try { CreatePKForMongoCollection = Convert.ToBoolean(dr["create_pk_for_mongo_collection"]); } catch { sErrors.Add("create_pk_for_mongo_collection"); }
                try { TargetMongoCollectionBehavior = (SQLTools_Enums.TARGET_TABLE_METHOD)Enum.Parse(typeof(SQLTools_Enums.TARGET_TABLE_METHOD), dr["target_mongo_collection_behavior"].ToString()); } catch { sErrors.Add("target_mongo_collection_behavior"); }
                try { AutoSQLTableCreation = Convert.ToBoolean(dr["auto_sql_table_creation"]); } catch { sErrors.Add("auto_sql_table_creation"); }
                try { WebserviceSQLLanguage = (SQLTools_Enums.WEBSERVICE_SQL)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_SQL), dr["ws_sql_language"].ToString()); } catch { sErrors.Add("ws_sql_language"); }
                try { NoSourceDataNoError = Convert.ToInt32(dr["no_sourcedata_noerror"]); } catch { sErrors.Add("no_sourcedata_noerror"); }
                try { QueryRetries = Convert.ToInt32(dr["query_retries"]); } catch { sErrors.Add("query_retries"); }
                try { JobVersion = dr["job_version"].ToString(); } catch { sErrors.Add("job_version"); }
            }
            catch (Exception) { throw; }

            return sErrors;
        }

        internal static Tuple<Job, Query> CreateDummyJob(MAINParameters INI, SQLTools_Enums.BDD sDriver, string sConnection, string sObject)
        {
            switch (sDriver) 
            {
                case SQLTools_Enums.BDD.FI_CSV:
                    Job JTemp = new Job(INI, "[0]", "fuzible", false, null);
                    JTemp.ConnectionString_Source = new CONNString(sDriver, "0", "CSVTemp", Path.GetDirectoryName(sConnection));
                    Query QTemp = new Query(JTemp, "SELECT * FROM " + sObject + " AS req");
                    return new Tuple<Job, Query> ( JTemp, QTemp );
                default:
                    return null;
            }
        }

        public void IncrementVersion(bool bParams, bool bQueries)
        {
            //Forme = V1.0.0 (itération, params, queries)
            string sVersion = JobVersion;
            string[] splitVersion = sVersion.Split('.');
            if (bParams)
            {
                int i = int.Parse(splitVersion[1]);
                i++;
                splitVersion[1] = i.ToString(); 
            }
            if (bQueries)
            {
                int i = int.Parse(splitVersion[2]);
                i++;
                splitVersion[2] = i.ToString();
            }
            sVersion = string.Concat(splitVersion[0], ".", splitVersion[1], ".", splitVersion[2], ".", Environment.UserName);
            JobVersion = sVersion;

        }

        #endregion
    }
}
