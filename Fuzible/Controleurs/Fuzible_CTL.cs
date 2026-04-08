using FuzibleFramework;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Xml;

namespace Fuzible.Controleurs
{
    public class Fuzible_CTL
    {
        public static event EventHandler<(int, Job, string)> OnJobEvent;

        private readonly PerformanceCounter _PerfCounter = new("Processor", "% Processor Time", "_Total");
        private double _dCpuChargeApp = 0;
        private double _dRamApp = 0;
        private double _dRamGCApp = 0;
        private readonly Process _cPproc = Process.GetCurrentProcess();

        long _lRamKb = 0;

        private BackgroundTask BGTASK;
        private INIProgram INIFile;
        internal MAINParameters MainParams;
        internal LogTools LOG;
        internal Job IMPORTED_JOB
        {
            get; private set;
        }
        private string USERNAME { get; set; } = Environment.UserName.ToUpper();
        internal bool FIRST_START
        {
            get
            {
                return INIFile.FirstStart;
            }
        }
        internal bool OWN_USER
        {
            get
            {
                return INIFile.USER.Equals(GetUsername(), StringComparison.InvariantCultureIgnoreCase);
            }
        }
        public bool SQL_LOG_CONFIGURED
        {
            get
            {
                return INIFile.GlobalParameters.LOG_CONNECTIONSTRING.Trim().Length != 0;
            }
        }

        public List<Job> UserJobsList
        {
            get
            {
                return INIFile.UserJobsList;
            }
        }

        public List<string> JobFamilies
        {
            get
            {
                return INIFile.UserJobsList.Select(j => j.JobFamily.Name).Distinct().ToList();
            }
        }

        public List<CONNString> UserConnectionsList
        {
            get
            {
                return INIFile.Connections.CSList;
            }
        }

        internal Fuzible_CTL(string sUsername, bool bDynamicCheckLicense, bool bReadOnly)
        {
            INIProgram.GetPhysicallyInstalledSystemMemory(out long _lRamKb); //exprimé en Kb

            INIProgram.OnJobEvent += EventsReceiver_Job;

            INIFile = new INIProgram(sUsername, bDynamicCheckLicense, bReadOnly);
            MainParams = INIFile.GlobalParameters;
            LOG = new LogTools(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, true);
            BGTASK = new BackgroundTask(LOG, INIFile);
        }

        public bool SetUserPassword(string sPwd)
        {
            if (OWN_USER)
            {
                try
                {
                    return MainParams.SaveSessionPassword(USERNAME, sPwd);
                }
                catch { return false; }
            }
            else { return false; }
        }

        public Job GetJob(string sJobID)
        {
            return INIFile.GetJob(sJobID);
        }

        public Job GetSubJob(Job INIP, int iJobStep)
        {
            int iCountSubJobs;
            iCountSubJobs = INIFile.GetChildrenCount(INIP.RawJobID).Count + 1;

            if (iJobStep <= iCountSubJobs)
            {
                if (iJobStep == 1)
                {
                    return GetJob(INIP.JobID);
                } //chargement job principal
                else
                {
                    return GetJob(string.Concat("[", INIP.RawJobID, "-", iJobStep.ToString(), "]"));
                } //chargement subjob
            }
            else { return null; }
        }

        internal string GetUsername()
        {
            if (INIFile.GlobalParameters.SECURITY_SHARED_USERS && !INIFile.GlobalParameters.SECURITY_PRINCIPAL_USER.Equals(USERNAME))
            {
                return INIFile.GlobalParameters.SECURITY_PRINCIPAL_USER;
            }
            else { return USERNAME; }
        }

        internal bool LoadQueryQuickHelp(Job INIP)
        {
            bool bExists = false;
            //contrôle qu'on a bien au moins 1 job avec une connexion source correspondant à ce que'on va bricoler
            if (INIP.ConnectionString_Source != null)
            {
                bExists = INIFile.CheckExistingJobWithSourceDriver(INIP.ConnectionString_Source.SConnDriver);
            }
            return bExists;
        }

        internal void ReloadINI()
        {
            INIFile = new INIProgram(USERNAME, true, false);
            //sauvegarde pour l'appli client (des modifs de noms de jobs ont pû être faites
            ServiceApp csParams = new(INIFile);

            bool bOK = csParams.SaveJobToClient(INIFile.UserJobsList, LOG);
            if (!bOK) { throw new Exception(LOG.LogEvents[^1].SMessage); }
        }

        internal void ChangeLanguage(string sLang)
        {
            INIFile.GlobalParameters.ChangeLanguage(sLang, USERNAME);
        }

        internal CONNString GetConnection(string sConnID)
        {
            return INIFile.Connections.GetConnByID(sConnID);
        }

        internal string DeleteJob(string sJobID)
        {
            bool bDeleted = INIFile.DeleteJob(sJobID);
            if (bDeleted)
            {
                ServiceApp csParams = new(INIFile);
                //suppression de la planification
                csParams.DeleteAllPlanifsForJob(sJobID);

                bool bOK = csParams.SaveJobToClient(INIFile.UserJobsList, LOG);
                if (!bOK) { throw new Exception(LOG.LogEvents[^1].SMessage); }

                return Languages.Languages.ma_msg_deletejobok;
            }
            else { return Languages.Languages.ma_msg_deletejobko; }
        }

        internal string RenameJob(string sJobID, string sNewName)
        {
            bool bExists = INIFile.CheckExistingJobName(sNewName);
            if (bExists)
            {
                return sNewName + Languages.Languages.ma_msg_renamejobnameexists;
            }
            else
            {
                INIFile.RenameJob(sJobID, sNewName);
                return Languages.Languages.ma_msg_renamejobok;
            }
        }

        internal string ChangeJobPassword(string sJobID, string sPassword)
        {
            INIFile.ChangeJobPassword(sJobID, sPassword);
            return Languages.Languages.ma_msg_changepwdok;
        }

        internal async static Task<DataTable> StaticGetData(Job INIP, Query sQ, LogTools MyLog, bool bWithDBFromConnection)
        {
            return await LoadQueryData(INIP, sQ, MyLog, bWithDBFromConnection);
        }

        internal async Task<DataTable> GetQueryData(Job INIP, Query sQ, bool bWithDBFromConnection)
        {
            return await LoadQueryData(INIP, sQ, LOG, bWithDBFromConnection);
        }

        private async static Task<DataTable> LoadQueryData(Job INIP, Query sQ, LogTools MyLog, bool bWithDBFromConnection)
        {
            try
            {
                List<DataSet> dsQuery = null;

                if (bWithDBFromConnection)
                {
                    INIP.ConnectionString_Source = sQ.ConnectionSrc; //cas des cross-query
                    INIP.DatabaseName_Source = sQ.ConnectionSrc.SConnDB;
                }

                INIP.OptionalDBName_OnInsert = false;
                INIP.OptionalDynamicParamField_OnInsert = "";
                INIP.OptionalRowID_OnInsert = false;
                INIP.OptionalTimestamp_OnInsert = false;

                Query sQProv = new(INIP, (sQ.CrossJoinQueries.Count > 0 ? sQ.RawQuery : sQ.SQLQuery)); //si il y a des cross-queries, il faut les intégrer

                sQProv.ChangeLimitedResults(sQ.QueryAnalyzer.LimitedResults);
                foreach (Query.QProperty qP in sQ.QueryAnalyzer.QueryProperties)
                {
                    sQProv.QueryAnalyzer.AddQueryProperty(qP.Element, qP.Property, qP.Value, qP.HasToBeShownToUser);
                }

                switch (sQ.ConnectionSrc.SConnDriverSuffix)
                {
                    case "DB":
                        //requête avec des jointures, on joue la requête
                        await Task.Factory.StartNew(() => dsQuery = MThread.GetSourceData(0, INIP, ref sQProv, MyLog, false));
                        break;
                    case "NS":
                        //requête avec des jointures, on joue la requête
                        await Task.Factory.StartNew(() => dsQuery = MThread.GetSourceData(0, INIP, ref sQProv, MyLog, false));
                        break;
                    default:
                        int iFlagRead = INIP.MailFlagRetrievedAsRead; //je ne veux pas marquer les mails comme lus, c'est juste un test !
                        INIP.MailFlagRetrievedAsRead = 0;
                        INIP.RunInSimulationMode = true;

                        await Task.Factory.StartNew(() => dsQuery = MThread.GetSourceData(0, INIP, ref sQProv, MyLog, false));

                        //je les remet comme avant
                        INIP.MailFlagRetrievedAsRead = iFlagRead;
                        INIP.RunInSimulationMode = false;
                        break;
                }

                if (dsQuery.Count > 0)
                {
                    if (dsQuery[0].Tables.Count > 0)
                    {
                        if (sQ.QueryAnalyzer.GetOneTableOnlyFromSource)
                        {
                            return dsQuery[0].Tables[0];
                        }
                        else if (dsQuery[0].Tables.Count >= sQ.QueryAnalyzer.SourceTableToHandle)
                        {
                            return dsQuery[0].Tables[sQ.QueryAnalyzer.SourceTableToHandle];
                        }
                        else { return dsQuery[0].Tables[0]; }
                    }
                    else
                    {
                        return null;
                    }
                }
                else { return null; }
            }
            catch (OperationCanceledException)
            { throw; }
            catch (Exception)
            { throw; }
        }

        internal string AddNewJob(bool bIsSubJob, Job INIPrimaryJob, string sNewJobName)
        {
            //if (bIsSubJob)
            //{
            //    sJobPassword = FITools.EncryptionSystem.AES_Decrypt(INIPrimayJob.JobPassword, INIPrimayJob.RawJobID.ToString());
            //}
            //else
            //{

            //}

            try
            {
                if (sNewJobName.Length > 0 && INIFile.CheckExistingJobName(sNewJobName))
                {
                    return Languages.Languages.ma_msg_createnewjobalreadyexists;
                }
                else
                {
                    //création JobID
                    string sNewJobID = "";
                    if (INIFile.UserJobsList.Count > 0)
                    {
                        if (bIsSubJob)
                        {
                            sNewJobID = string.Concat("[", INIPrimaryJob.RawJobID, "-", (INIFile.GetChildrenCount(INIPrimaryJob.RawJobID).Count + 2).ToString(), "]");
                        }
                        else
                        {
                            sNewJobID = INIFile.GetNewJobID();
                        }
                    }
                    else { sNewJobID = "[1]"; }

                    return sNewJobID;
                }
            }
            catch (Exception ex)
            {
                return Languages.Languages.ma_msg_createnewjoberror + ex.Message;
            }
        }

        internal bool LoadConfigurationScreen(CONNString CS, int iIndex)
        {
            if (OWN_USER)
            {
                return true;
            }
            else { return false; }
        }

        internal bool AddSampleJobs()
        {
            if (OWN_USER)
            {
                Job SampleJobA = INIFile.CreateSampleJob();

                if (SampleJobA != null)
                {
                    return true;
                }
                else { return false; }
            }
            else { return false; }
        }

        internal string GetExempleQuery(Job INIP)
        {
            string sOutputDemo = INIP.LoadQueryTargetExemple();
            string sTableExemple = INIP.LoadQuerySourceExemple(ref LOG);
            return string.Concat(sOutputDemo, ":", "SELECT * FROM ", sTableExemple);
        }


        internal async static Task<string> GetPlanifCalendar(INIProgram INIFile, LogTools MyLog, int iWeeks, bool bRemoveEmptyRows, bool bShowInactives)
        {
            ServiceApp CSParameters = new(INIFile);
            DataSet ds = await CSParameters.GetPlanifCalendar(MyLog, iWeeks, bRemoveEmptyRows, bShowInactives);

            string sResult = "";

            if (ds != null && ds.Tables.Count > 0)
            {
                sResult = MailTools.DatasetToHTML(ds, true, false, false, true);
            }
            return sResult;
        }

        internal async static Task<string> CheckSynchroValidity(Job INIP, Query Q, LogTools MyLog, bool bTargetB)
        {
            if (INIP.ConnectionString_Source != null && INIP.ConnectionString_Target != null)
            {
                if (bTargetB) { Q = Q.CreateSwapedTargetQuery(); }

                if (Q.ConnectionTrg.SConnDriverSuffix.Equals("DB"))
                {
                    SQLTools SQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                    bool bTableExists = SQL.CheckForExistingTable(Q.OutputTable, new Query());
                    if (bTableExists)
                    {
                        List<SQLColumn> sListColumnsTarget = SQL.GetFieldsFromTable(Q.OutputTable, new Query());
                        if (sListColumnsTarget.Count > 0)
                        {
                            Job INITarget = INIP.DeepCopy();
                            INITarget.ConnectionString_Source = INITarget.ConnectionString_Target;
                            INITarget.ExportUseDataset = false;
                            INITarget.DatabaseName_Source = INITarget.DatabaseName_Target;
                            INITarget.JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT;

                            Q.QueryAnalyzer.PreBuiltSynchroTargetQuery.RealSynchroTargetColumns = sListColumnsTarget;
                            string sTargetQ = Q.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(true, INIP.SynchroBypassQueryFiltersInTarget, true);
                            sTargetQ = Q.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sTargetQ, INIP, Q, null);

                            Query QTarget = new(INITarget, sTargetQ);
                            QTarget.ChangeLimitedResults(1); //je ne veux pas pomper inutilement des données
                            DataTable dtData = null;

                            try
                            {
                                dtData = await StaticGetData(INITarget, QTarget, MyLog, true);
                            }
                            catch (OperationCanceledException)
                            { return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", sTargetQ, "]"); }
                            catch (Exception)
                            { return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", sTargetQ, "]"); }

                            //DataSet dsData = SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sTargetQ, Q.OutputTable);
                            if (dtData != null)
                            {
                                return string.Concat(Languages.Languages.ma_msg_checksynchrook01, Environment.NewLine, "[", sTargetQ, "]"); //, Environment.NewLine, dtData.Rows.Count.ToString(), Languages.Languages.ma_msg_checksynchrook02);
                            }
                            else { return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", sTargetQ, "]"); }
                        }
                        else { return string.Concat(Languages.Languages.ma_msg_checksynchronofieldintarget, Q.OutputTable, " !"); }
                    }
                    else { return string.Concat(Languages.Languages.ma_msg_checksynchronotableintarget01, Q.OutputTable, Languages.Languages.ma_msg_checksynchronotableintarget02); }
                }
                else { return Languages.Languages.ma_msg_checksynchrounsupported; }
            }
            else { return Languages.Languages.ma_msg_startjobconfigureconn; }
        }

        internal async static Task<string> ViewCrossQueryInfo(Job INIP, LogTools MyLog, Query qCrossQuery, Query qMainQuery)
        {
            try
            {
                StringBuilder sbPrimaryKey = new();
                string sErrors = "";

                List<DataTable> dtResultsMain = new();
                DataTable dtTemp = await Fuzible_CTL.StaticGetData(INIP, qMainQuery, MyLog, true);
                if (dtTemp != null)
                {
                    dtResultsMain.Add(dtTemp);
                    sbPrimaryKey.AppendLine(Languages.Languages.ma_msg_checkccvalidityinfo01 + qMainQuery.ConnectionSrc.SConnName + " - " + dtTemp.Rows.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo02 + dtTemp.Columns.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo03);
                }
                else { sErrors = string.Concat(sErrors, Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityerrorinfo01, qMainQuery.ConnectionSrc.SConnName, Languages.Languages.ma_msg_checkccvalidityerrorinfo02); }

                int iCQIndex = qMainQuery.CrossJoinQueries.IndexOf(qCrossQuery);

                for (int iQ = 0; iQ < iCQIndex; iQ++)
                {
                    dtTemp = await Fuzible_CTL.StaticGetData(INIP, qMainQuery.CrossJoinQueries[iQ], MyLog, true);
                    if (dtTemp != null)
                    {
                        dtResultsMain.Add(dtTemp);
                        sbPrimaryKey.AppendLine(Languages.Languages.ma_msg_checkccvalidityinfo01 + qMainQuery.CrossJoinQueries[iQ].ConnectionSrc.SConnName + " - " + dtTemp.Rows.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo02 + dtTemp.Columns.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo03);
                    }
                    else { sErrors = string.Concat(sErrors, Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityerrorinfo01, qMainQuery.CrossJoinQueries[iQ].ConnectionSrc.SConnName, Languages.Languages.ma_msg_checkccvalidityerrorinfo02); }
                }

                dtTemp = await Fuzible_CTL.StaticGetData(INIP, qCrossQuery, MyLog, true);
                if (dtTemp != null)
                {
                    sbPrimaryKey.AppendLine(Languages.Languages.ma_msg_checkccvalidityinfo01 + qCrossQuery.ConnectionSrc.SConnName + " - " + dtTemp.Rows.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo02 + dtTemp.Columns.Count.ToString() + Languages.Languages.ma_msg_checkccvalidityinfo03);
                }
                if (dtTemp == null) { sErrors = string.Concat(sErrors, Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityerrorinfo01, qCrossQuery.ConnectionSrc.SConnName, Languages.Languages.ma_msg_checkccvalidityerrorinfo02); }

                sErrors = sErrors.Trim();
                sbPrimaryKey.AppendLine();
                bool bContainsKey = false;

                if (dtTemp != null && dtTemp.Columns.Count > 0)
                {
                    if (qCrossQuery.CrossQueryForceJoinFields.Count > 0)
                    {
                        for (int iE = 0; iE < dtResultsMain.Count; iE++)
                        {
                            var s1 = dtResultsMain[iE].Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList();
                            var s2 = dtTemp.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList();

                            foreach (string sF in qCrossQuery.CrossQueryForceJoinFields)
                            {
                                if (s1.Any(s => s.Equals(sF, StringComparison.OrdinalIgnoreCase)) && s2.Any(s => s.Equals(sF, StringComparison.OrdinalIgnoreCase)))
                                {
                                    bContainsKey = true;
                                    string sMessage = string.Concat(Languages.Languages.ma_msg_checkccvalidityinfo04 + qCrossQuery.ConnectionSrc.SConnName + Languages.Languages.ma_msg_checkccvalidityinfo05, Languages.Languages.ma_msg_checkccvalidityinfo06 + qMainQuery.CrossJoinQueries[iE].ConnectionSrc.SConnName + "]", Languages.Languages.ma_msg_checkccvalidityinfo08, string.Join(",", qCrossQuery.CrossQueryForceJoinFields), Languages.Languages.ma_msg_checkccvalidityinfo09);
                                    sbPrimaryKey.AppendLine(sMessage);
                                    break;
                                }
                                else
                                {
                                    bContainsKey = false;
                                }
                            }
                            if (bContainsKey) { break; }
                        }
                    }
                    else
                    {
                        var s2 = dtTemp.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList();
                        //comparaison de l'entête
                        for (int iE = 0; iE < dtResultsMain.Count; iE++)
                        {
                            var s1 = dtResultsMain[iE].Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList();

                            foreach (string sF in s2)
                            {
                                if (s1.Any(s => s.Equals(sF, StringComparison.OrdinalIgnoreCase)))
                                {
                                    bContainsKey = true;
                                    string sMessage = string.Concat(Languages.Languages.ma_msg_checkccvalidityinfo04 + qCrossQuery.ConnectionSrc.SConnName + Languages.Languages.ma_msg_checkccvalidityinfo05, iE == 0 ? (Languages.Languages.ma_msg_checkccvalidityinfo06 + qMainQuery.ConnectionSrc.SConnName + "]") : (Languages.Languages.ma_msg_checkccvalidityinfo07 + qMainQuery.CrossJoinQueries[iE].ConnectionSrc.SConnName + "]"), Languages.Languages.ma_msg_checkccvalidityinfo08, sF, Languages.Languages.ma_msg_checkccvalidityinfo09);
                                    sbPrimaryKey.AppendLine(sMessage);
                                    break;
                                }
                            }
                            if (bContainsKey) { break; }
                        }
                    }

                    //test condition
                    if (qCrossQuery.CrossJoinQueryWhere.Length > 0)
                    {
                        string sWhereCondition = qCrossQuery.CrossJoinQueryWhere;
                        if (sWhereCondition.StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase)) { sWhereCondition = sWhereCondition[5..].Trim(); }

                        //construction d'une bête datatable contenant toutes les colonnes pour tester le filtre
                        DataTable dtTest = new("WHERE");
                        foreach (DataColumn dc in dtTemp.Columns)
                        {
                            dtTest.Columns.Add(dc.ColumnName, dc.DataType);
                        }

                        foreach (DataTable dt in dtResultsMain)
                        {
                            foreach (DataColumn dc in dt.Columns)
                            {
                                if (!dtTest.Columns.Contains(dc.ColumnName)) { dtTest.Columns.Add(dc.ColumnName, dc.DataType); }
                            }
                        }
                        DataRow dr = dtTest.NewRow();
                        dtTest.Rows.Add(dr);
                        try
                        {
                            dtTest.Select(sWhereCondition);
                            sbPrimaryKey.AppendLine(string.Concat(Environment.NewLine, Languages.Languages.ma_msg_checkccvaliditywherevalid01, sWhereCondition, Languages.Languages.ma_msg_checkccvaliditywherevalid02));
                        }
                        catch (Exception ex)
                        {
                            sbPrimaryKey.AppendLine(string.Concat(Environment.NewLine, Languages.Languages.ma_msg_checkccvaliditywhereinvalid01, sWhereCondition, Languages.Languages.ma_msg_checkccvaliditywhereinvalid02, ex.Message));
                        }
                    }
                }

                if (sErrors.Length > 0)
                {
                    sbPrimaryKey.AppendLine(string.Concat(Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityerrordetected, Environment.NewLine, sErrors, Environment.NewLine));
                }

                if (!bContainsKey)
                {
                    string sF1 = "";
                    foreach (DataTable dt in dtResultsMain)
                    {
                        sF1 = string.Concat(sF1, string.Join(",", dt.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList()));
                    }
                    sbPrimaryKey.AppendLine(string.Concat(Languages.Languages.ma_msg_checkccvalidityresume01,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume02, dtTemp != null ? string.Join(",", dtTemp.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList()) : "null",
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume03, sF1,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume04,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume05,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume06,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume07,
                        Environment.NewLine, Languages.Languages.ma_msg_checkccvalidityresume08));
                }

                dtTemp = null;
                dtResultsMain = null;

                return sbPrimaryKey.ToString();

            }
            catch (OperationCanceledException)
            {
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        internal async static Task<string> DataShowColumnMappings(Job INIP, LogTools MyLog, bool bTargetB)
        {
            Query Q = INIP.JobQueries[0];

            //LogTools MyLogSingleQ = new LogTools("SINGLEQUERY", System.Reflection.MethodBase.GetCurrentMethod(), INIP, true);
            //LogTools.OnNewLogEvent += (s, eArgs) =>
            //{ BackgroundTask.SListTaskInfo.Add(eArgs); };

            bool bHasNonMatchedColumns = false;
            DataTable dtHtmlTable = new("Html");
            DataTable dtMapping = new("Mapping");

            dtHtmlTable.Namespace = Q.RawOutput + " : " + Environment.NewLine + Languages.Languages.ma_msg_mappingsourcetarget;
            dtHtmlTable.Columns.Add(Languages.Languages.ma_msg_mappingsource);
            dtHtmlTable.Columns.Add(Languages.Languages.ma_msg_mappinglink);
            dtHtmlTable.Columns.Add(Languages.Languages.ma_msg_mappingtarget);

            dtMapping.Namespace = Languages.Languages.ma_msg_mappingsourcetarget;
            dtMapping.Columns.Add(Languages.Languages.ma_msg_mappingsource);
            dtMapping.Columns.Add(Languages.Languages.ma_msg_mappinglink);
            dtMapping.Columns.Add(Languages.Languages.ma_msg_mappingtarget);

            if (INIP.ConnectionString_Source != null && INIP.ConnectionString_Target != null)
            {
                if (bTargetB) { Q = Q.CreateSwapedTargetQuery(); }

                if (Q.ConnectionTrg.SConnDriverSuffix.Equals("DB"))
                {
                    SQLTools SQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                    bool bTableExists = SQL.CheckForExistingTable(Q.OutputTable, new Query());
                    if (bTableExists)
                    {
                        string sHtmlStyleKO = "<p style=\"color:OrangeRed\";>";

                        List<SQLColumn> sListColumnsTarget = SQL.GetFieldsFromTable(Q.OutputTable, new Query());
                        if (sListColumnsTarget.Count > 0)
                        {
                            Job INISource = INIP.DeepCopy();
                            INISource.ExportUseDataset = false;
                            INISource.JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT;

                            Q.ChangeLimitedResults(3); //je ne veux pas pomper inutilement des données. Note, je prends au moins 3 lignes car si il y a un data transform, il faut au moins 1 ligne de données

                            DataTable dtData = null;

                            try
                            {
                                dtData = await StaticGetData(INISource, Q, MyLog, false);
                            }
                            catch (OperationCanceledException)
                            { return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", Q.SQLQuery, "]"); }
                            catch (Exception)
                            { return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", Q.SQLQuery, "]"); }

                            //DataSet dsData = SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sTargetQ, Q.OutputTable);
                            if (dtData != null)
                            {
                                List<string> sExistSource = new();

                                foreach (DataColumn dc in dtData.Columns)
                                {
                                    DataRow drA = dtHtmlTable.NewRow();
                                    DataRow drB = dtMapping.NewRow();

                                    string sColSource = dc.ColumnName;
                                    string sColTarget = "";

                                    var sqlcol = sListColumnsTarget.FirstOrDefault(c => c.ColumnName.Equals(sColSource, StringComparison.OrdinalIgnoreCase));
                                    sColTarget = sqlcol != null ? sqlcol.ColumnName : sColTarget;

                                    if (sqlcol == null)
                                    {
                                        drA[0] = sHtmlStyleKO + sColSource + "</p>";
                                        drA[1] = "<b>KO</b>";
                                        drA[2] = "";
                                        drB[0] = sColSource;
                                        drB[1] = "<b>KO</b>";
                                        drB[2] = "";
                                        bHasNonMatchedColumns = true;
                                    }
                                    else
                                    {
                                        drA[0] = sColSource;
                                        drA[1] = "OK";
                                        drA[2] = sColTarget;
                                        drB[0] = sColSource;
                                        drB[1] = "OK";
                                        drB[2] = sColTarget;
                                    }

                                    dtHtmlTable.Rows.Add(drA);
                                    dtMapping.Rows.Add(drB);
                                    sExistSource.Add(sColSource);
                                }
                                foreach (SQLColumn sTargetCol in sListColumnsTarget)
                                {
                                    if (!sExistSource.Any(e => e.Equals(sTargetCol.ColumnName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        DataRow drA = dtHtmlTable.NewRow();
                                        DataRow drB = dtMapping.NewRow();
                                        drA[0] = "";
                                        drA[1] = "<b>KO</b>";
                                        drA[2] = sHtmlStyleKO + sTargetCol.ColumnName + "</p>";
                                        drB[0] = "";
                                        drB[1] = "<b>KO</b>";
                                        drB[2] = sTargetCol.ColumnName;
                                        bHasNonMatchedColumns = true;

                                        dtHtmlTable.Rows.Add(drA);
                                        dtMapping.Rows.Add(drB);
                                    }
                                }


                                StringBuilder sbHtml = new();
                                sbHtml.Append("<p style=\"font-weight: italic; color: darkslateblue\";>" + System.Web.HttpUtility.HtmlEncode(Languages.Languages.ma_msg_mappingalias).Replace("\r\n", "<br />") + "</p>");
                                sbHtml.Append("</br>" + MailTools.DatatableToHTML(dtHtmlTable, true, true, false, true));

                                if (bHasNonMatchedColumns)
                                {
                                    string sNewQuery = SmartColumnsMapping(dtMapping, Q, INIP);
                                    sNewQuery = System.Web.HttpUtility.HtmlEncode(sNewQuery);
                                    sNewQuery = sNewQuery.Replace("\r\n", "<br />");
                                    sbHtml.Append("</br><p style=\"font-weight: bold; color: darkslateblue\";>" + System.Web.HttpUtility.HtmlEncode(Languages.Languages.ma_msg_mappingsuggestedquery).Replace("\r\n", "<br />") + "</p></br>" + sNewQuery);
                                }

                                return sbHtml.ToString();
                            }
                            else
                            {
                                return string.Concat(Languages.Languages.ma_msg_checksynchroko, Environment.NewLine, "[", Q.SQLQuery, "]");
                            }
                        }
                        else
                        {
                            return string.Concat(Languages.Languages.ma_msg_checksynchronofieldintarget, Q.OutputTable, " !");
                        }
                    }
                    else { return string.Concat(Languages.Languages.ma_msg_checksynchronotableintarget01, Q.OutputTable, Languages.Languages.ma_msg_checksynchronotableintarget02); }
                }
                else { return Languages.Languages.ma_msg_checksynchrounsupported; }
            }
            else { return Languages.Languages.ma_msg_startjobconfigureconn; }

        }

        internal static string SmartColumnsMapping(DataTable dtMapping, Query Q, Job INIP)
        {
            //dtmapping contient une représentation des colonnes source et cible
            List<string> sColSource = new();
            List<string> sColTarget = new();

            string sQuery = Q.RawQuery;
            string sQueryWithoutOutput = Query.RemoveTableCibleFromQuery(sQuery);
            string sOutput = Q.RawOutput;

            StringBuilder sbFields = new();

            foreach (DataRow dr in dtMapping.Rows)
            {
                if (dr[0].ToString().Length > 0) { sColSource.Add(dr[0].ToString()); }
                if (dr[2].ToString().Length > 0) { sColTarget.Add(dr[2].ToString()); }
            }

            bool bStop = false;
            while (!bStop)
            {
                int[] iCloseString = new int[3] { 0, 0, 1000 };

                //on cherche la correspondance la plus probable entre un champ source et un champ cible
                for (int iT = 0; iT < sColTarget.Count; iT++)
                {
                    for (int iS = 0; iS < sColSource.Count; iS++)
                    {
                        int iLevenshein = Toolbox.LevenshteinDistance.Compute(sColSource[iS], sColTarget[iT]);
                        if (iCloseString[2] > iLevenshein)
                        { iCloseString = new int[] { iS, iT, iLevenshein }; }
                    }
                }

                sbFields.Append(Q.ConnectionSrc.SqlEchappementChar + sColSource[iCloseString[0]] + Q.ConnectionSrc.SqlEchappementChar +
                                " AS " + Q.ConnectionSrc.SqlEchappementChar + sColTarget[iCloseString[1]] + Q.ConnectionSrc.SqlEchappementChar + ", ");

                sColSource.RemoveAt(iCloseString[0]);
                sColTarget.RemoveAt(iCloseString[1]);

                if (sColTarget.Count == 0 || sColSource.Count == 0) { bStop = true; }
            }

            sQuery = string.Concat(sOutput + ":SELECT " + sbFields.ToString()[..(sbFields.Length - 2)] + Environment.NewLine + " FROM (" + sQueryWithoutOutput + ") AS " + Q.ConnectionSrc.SqlEchappementChar + Q.OutputTable + Q.ConnectionSrc.SqlEchappementChar);

            return sQuery;
        }

        #region "PRIVATE VOID"

        internal string GetSourceTargetLabel(string sConnID)
        {
            CONNString CS = GetConnection(sConnID);
            string sSourceTarget = "";

            switch (CS.SConnDriver)
            {
                case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                    sSourceTarget = "Active Directory";
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sSourceTarget = "MS Access Database";
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sSourceTarget = "MySQL/MariaDB Database";
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sSourceTarget = "Odbc Database";
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sSourceTarget = "Oracle Database";
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sSourceTarget = "Postgres Database";
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sSourceTarget = "SQL Server Database";
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sSourceTarget = "SQLite Database";
                    break;
                case SQLTools_Enums.BDD.NS_MONGODB:
                    sSourceTarget = "MongoDB Database";
                    break;
                case SQLTools_Enums.BDD.FI_CSV:
                    sSourceTarget = "CSV File";
                    break;
                case SQLTools_Enums.BDD.FI_FILE:
                    sSourceTarget = "Unknown File";
                    break;
                case SQLTools_Enums.BDD.FI_XLS:
                    sSourceTarget = "Excel File";
                    break;
                case SQLTools_Enums.BDD.FI_XML:
                    sSourceTarget = "XML File";
                    break;
                case SQLTools_Enums.BDD.FI_JSON:
                    sSourceTarget = "JSON File";
                    break;
                case SQLTools_Enums.BDD.MB_MAIL:
                    sSourceTarget = "Mailbox";
                    break;
                case SQLTools_Enums.BDD.WS_REST:
                    sSourceTarget = "API REST";
                    break;
            }
            return sSourceTarget;
        }

        internal string GetInfosChargeApp()
        {
            _dCpuChargeApp = Math.Round(_PerfCounter.NextValue(), 1);
            _dRamApp = Math.Round(_cPproc.PrivateMemorySize64 / 1000000.0); //conversion en Mb
            _dRamGCApp = Math.Round(GC.GetTotalMemory(false) / 1000000.0); //convertion en Mb

            if (INIFile.GlobalParameters.SHOW_SYSTEM_ALERTS)
            {
                if (_dCpuChargeApp == 100 && DateTime.Now.Second % 5 == 0) //message tt les 5 secondes
                {
                    LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_highcpu + _dCpuChargeApp.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                var dRam = (_PerfCounter.RawValue / 1000000.0);
                var systemRamInMb = _lRamKb / 2024;

                if (_dRamGCApp > (dRam - 100) && DateTime.Now.Second % 5 == 0)
                {
                    LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_highram + _dRamGCApp.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                //pas tout le temps
                if (DateTime.Now.Second % 5 == 0)
                {
                    int iPercentUsedRam = Toolbox.GetPercentUsageRam();
                    if (iPercentUsedRam > 95) //si il ne reste que 500mb c'est pas terrible
                    {
                        LOG.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_lowram + iPercentUsedRam.ToString() + " %", SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
            }

            return string.Concat(Languages.Languages.ma_msg_runninghreads, Monitoring.GetQteThreads.ToString(), " / ", "CPU : ", _dCpuChargeApp.ToString(), " % / ", "RAM : ", _dRamApp.ToString(), " Mb / ", _dRamGCApp.ToString(), " Mb");

        }

        internal string RemoveFamily(string sFamily)
        {
            bool bOK = INIFile.RemoveFamily(sFamily);
            if (bOK)
            {
                return Languages.Languages.ma_msg_removefamilyok;
            }
            else
            {
                return Languages.Languages.ma_msg_removefamilyko;
            }
        }

        internal string SaveConfigINIFile(Job INIP, string sQueries)
        {
            bool bOK = false;

            //Contrôle de l'existence de la section dans le fichier INI

            bOK = INIFile.SaveJob(INIP);
            if (bOK)
            {
                //sauvegarde des paramètres communs aux sub-jobs associés
                foreach (Job J in INIFile.GetChildrenCount(INIP.RawJobID))
                {
                    //maj dynparams (partagés entre chaque subjob
                    J.JobFamily = INIP.JobFamily;
                    J.DynParams = INIP.DynParams;
                    J.LogMailAdress = INIP.LogMailAdress;
                    J.HasSqlLog = INIP.HasSqlLog;
                    J.LogLevel = INIP.LogLevel;
                    INIFile.SaveJob(J);
                    //GT 20/08/2024 : pourquoi sauvegarder les requêtes des autres jobs ??
                    //SaveConfigQueries(J, J.LoadJobQueries(false));
                }

                bOK = SaveConfigQueries(INIP, sQueries);
                if (!bOK) { return Languages.Languages.ma_msg_cantsavepbqueries; }
                else
                {
                    ServiceApp csParams = new(INIFile);

                    bOK = csParams.SaveJobToClient(INIFile.UserJobsList, LOG);
                    if (!bOK) { throw new Exception(LOG.LogEvents[^1].SMessage); }

                    return Languages.Languages.ma_msg_createnewjobok;
                }
            }
            else { return Languages.Languages.ma_msg_createnewjobko; }
        }

        private static bool CheckValidQueriesInTab(string sQueries)
        {
            bool bOK = true;
            bool bAtLeastOneQuery = false;
            string[] tbLines = sQueries.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            //test d'intégrité : on vérifie que chaque requête du job commence bien par "table:select"
            foreach (string sQ in tbLines)
            {
                if (Regex.IsMatch(sQ, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase))
                {
                    bAtLeastOneQuery = true;
                }
                if ((!bAtLeastOneQuery) && sQ.Trim().Length > 0 && sQ.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    bOK = false;
                    break;
                }
            }
            return bOK;
        }

        private bool SaveConfigQueries(Job INIP, string sQueries)
        {
            bool bOK = CheckValidQueriesInTab(sQueries);

            if (bOK)
            {
                List<Query> sListQueries = Job.ExtractQueriesFromString(INIP, sQueries);
                INIFile.SaveJobQueries(INIP, sListQueries);
            }

            return bOK;
        }

        internal bool INIT_StartJob(Job INIP, string sQueries, bool bSimulation, string sDelegate)
        {
            INIP.RunInSimulationMode = bSimulation;

            Task t = Task.Factory.StartNew(() => INIFile.ExecuteJob(INIP.DeepCopy(), LOG, INIP.DynParams, sDelegate, INIP.RawJobID == 0 ? Job.ExtractQueriesFromString(INIP, sQueries) : null));
            Monitoring.AddThread(t, System.Reflection.MethodBase.GetCurrentMethod());

            return true;
        }

        private void EventsReceiver_Job(object sender, (int, Job, string) e)
        {
            Task.Factory.StartNew(() => OnJobEvent.Invoke(typeof(INIProgram), (e.Item1, e.Item2, e.Item3)));
        }

        internal object[] BuildQuery(Job INIP, Query sQ, string sTable)
        {
            try
            {
                StringBuilder sbFields = new();
                List<string> sDefListFields = new();

                INIP.OptionalDBName_OnInsert = false;
                INIP.OptionalDynamicParamField_OnInsert = "";
                INIP.OptionalRowID_OnInsert = false;
                INIP.OptionalTimestamp_OnInsert = false;

                Match mcOut = Regex.Match(sQ.RawQuery, ".+\\s*:\\s*SELECT", RegexOptions.IgnoreCase);
                Match mcOnly = Regex.Match(sQ.RawQuery, ".+\\s*:\\s*SELECT\\s+TABLE\\s+\\d+", RegexOptions.IgnoreCase);

                string sQTemp = "";
                if (mcOnly.Success)
                {
                    sQTemp = mcOnly.Value + " ONLY ";
                }
                else 
                {
                    sQTemp = mcOut.Success ? (mcOut.Value + " ") : (sQ.OutputTable + ":SELECT ");
                }

                sQTemp = string.Concat(sQTemp, "* FROM ", sTable);

                Query sQProv = new(INIP, sQTemp);
                sQProv.ChangeLimitedResults(1000);

                switch (sQ.ConnectionSrc.SConnDriverSuffix)
                {
                    case "DB":
                        SQLTools SQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref LOG);
                        List<SQLColumn> sListFields = new();

                        if (sTable.StartsWith("("))
                        {
                            MThread.GetSourceData(0, INIP, ref sQProv, LOG, false);
                            foreach (Query.QField qF in sQProv.QueryAnalyzer.Fields)
                            {
                                if (!qF.Name.Equals("*")) { sbFields.Append(string.Concat(qF.Alias + ",")); sDefListFields.Add(qF.Alias); }
                            }
                        }
                        else
                        {
                            sListFields = SQL.GetFieldsFromTable(sTable, new Query());
                            foreach (SQLColumn sF in sListFields)
                            {
                                if (!sF.Equals("*"))
                                {
                                    sbFields.Append(string.Concat(SQL.EchappementChar, sF.ColumnName, SQL.EchappementChar, ","));
                                    sDefListFields.Add(string.Concat(SQL.EchappementChar, sF.ColumnName, SQL.EchappementChar));
                                }
                            }
                        }
                        break;
                    case "NS":
                        NOSQLTools NOSQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref LOG);
                        List<SQLColumn> sListFieldsNOSQL = new();

                        if (sTable.StartsWith("("))
                        {
                            MThread.GetSourceData(0, INIP, ref sQProv, LOG, false);
                            foreach (Query.QField qF in sQProv.QueryAnalyzer.Fields)
                            {
                                if (!qF.Name.Equals("*")) { sbFields.Append(string.Concat(qF.Alias + ",")); sDefListFields.Add(qF.Alias); }
                            }
                        }
                        else
                        {
                            sListFields = NOSQL.GetFieldsFromCollection(sTable);
                            foreach (SQLColumn sF in sListFields)
                            {
                                if (!sF.Equals("*"))
                                {
                                    sbFields.Append(string.Concat(sF.ColumnName + ","));
                                    sDefListFields.Add(sF.ColumnName);
                                }
                            }
                        }
                        break;
                    default:
                        //j'initialise certains paramètres que je ne veux pas appliquer
                        int iFlagRead = INIP.MailFlagRetrievedAsRead; //je ne veux pas marquer les mails comme lus, c'est juste un test !
                        INIP.MailFlagRetrievedAsRead = 0;
                        INIP.RunInSimulationMode = true;

                        MThread.GetSourceData(0, INIP, ref sQProv, LOG, false);

                        if (sQProv.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == SQLTools_Enums.QUERY_PROPERTIES.CSV_HASHEADER))
                        {
                            string sHasHeader = sQProv.QueryAnalyzer.QueryProperties.First(qP => qP.Property == SQLTools_Enums.QUERY_PROPERTIES.CSV_HASHEADER).Value;
                            if (sHasHeader.Equals("False", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new Exception(Languages.Languages.ma_msg_getheaderfromqueryerror);
                            }
                        }

                        foreach (Query.QField qF in sQProv.QueryAnalyzer.Fields)
                        {
                            if (!qF.Name.Equals("*"))
                            {
                                sbFields.Append(string.Concat("\"", qF.Alias, "\"", ","));
                                sDefListFields.Add(string.Concat("\"", qF.Alias, "\""));
                            }
                        }

                        //je les remet comme avant
                        INIP.MailFlagRetrievedAsRead = iFlagRead;
                        INIP.RunInSimulationMode = false;
                        break;
                }
                return new object[] { sbFields.ToString(), sDefListFields };
            }
            catch (OperationCanceledException)
            { throw; }
            catch (Exception)
            { throw; }
        }

        public List<string> GetMultiTargetScriptsFromString(string sQ, List<string> sListDynParams)
        {
            List<string> sListMT = new();

            if (sQ.Length > 0 && sQ.IndexOf(":") > -1)
            {
                sQ = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sQ, sListDynParams);

                string sTarget = sQ[..sQ.IndexOf(":")];

                string[] sSplit = sTarget.Split(Convert.ToChar("]"));

                foreach (string sT in sSplit)
                {
                    if (sT.IndexOf("[") > -1)
                    {
                        string sConn = string.Concat(sT[sT.IndexOf("[")..], "]");

                        if (INIFile.Connections != null)
                        {
                            if (INIFile.Connections.GetIndexSection(sConn) > -1)
                            {
                                sListMT.Add(sConn);
                            }
                        }
                    }
                }
            }

            return sListMT;
        }

        internal string ImportXMLJob(string sFilename, string sJobPassword, bool bUseNameToFindConnection)
        {
            DataSet dsJob = new();
            try
            {
                string sData = File.ReadAllText(sFilename, Encoding.UTF8);
                XmlReader xml = XmlReader.Create(new StringReader(sData));
                dsJob.ReadXml(xml, XmlReadMode.Auto);
            }
            catch (Exception ex) { return Languages.Languages.ma_msg_importjobunable + ex.Message; }

            string sPwd = dsJob.Tables["JobParams"].Rows[0]["job_password"].ToString();

            string sId = dsJob.Tables["JobParams"].Rows[0]["job_id"].ToString();

            sId = sId[1..^1];

            if (Regex.IsMatch(sId, @"\d+-\d+")) //les MDP sont encodés avec uniquement la racine du job
            {
                sId = sId.Split('-')[0];
            }

            //le password est doublement chiffré
            sPwd = FITools.EncryptionSystem.AES_Decrypt(sPwd, sId);

            //contrôle de version pour alerte si version différente
            string sFuzibleVersion = dsJob.Tables["Job"].Rows[0]["FuzibleVersion"].ToString();
            string sExportVersion = dsJob.Tables["Job"].Rows[0]["ExportVersion"].ToString();

            if (!sExportVersion.Equals(Job.EXPORT_XML_VERSION))
            {
                return string.Concat(Languages.Languages.ma_msg_importjob01
                                , Environment.NewLine, Languages.Languages.ma_msg_importjob02, sExportVersion
                                , Environment.NewLine, Languages.Languages.ma_msg_importjob03, Job.EXPORT_XML_VERSION);
            }

            if (sJobPassword.Equals(FITools.EncryptionSystem.AES_Decrypt(sPwd, sId)))
            {
                try
                {
                    bool bEncrypt = true;
                    try { bEncrypt = bool.Parse(dsJob.Tables["Job"].Rows[0]["Encrypted"].ToString()); } catch { }

                    List<string> sErrorsImport = new();
                    IMPORTED_JOB = INIFile.ImportExternalJobXML(dsJob, sJobPassword, bUseNameToFindConnection, bEncrypt, ref sErrorsImport);

                    string sErrors = "";
                    if (sErrorsImport.Count > 0)
                    {
                        sErrors = Environment.NewLine + Environment.NewLine + Languages.Languages.ma_msg_importjoberrors + Environment.NewLine + string.Join(Environment.NewLine + " - ", sErrorsImport);
                    }

                    return Languages.Languages.ma_msg_importjobwarning + sErrors;
                }
                catch (Exception ex) { return Languages.Languages.ma_msg_importjobunable + ex.Message; }
            }
            else
            {
                return Languages.Languages.ma_msg_importjobwrongpwd;
            }
        }

        internal bool IsServiceAppConfigured()
        {
            ServiceApp CSParameters = new(INIFile);
            return CSParameters.SQLConnexionServiceApp != null;
        }

        internal bool JobsAreEqual(Job job1, Job job2)
        {
            if (job1.JobDescription != job2.JobDescription) return false;
            if (job1.JobPriority != job2.JobPriority ) return false; 
            if (job1.AbortSubJobExecutionIfErrors != job2.AbortSubJobExecutionIfErrors ) return false; 
            if (job1.AbortSubJobExecutionIfNoData != job2.AbortSubJobExecutionIfNoData ) return false; 
            if (job1.NoSourceDataNoError != job2.NoSourceDataNoError ) return false; 
            if (job1.QueryRetries != job2.QueryRetries ) return false; 
            if (job1.BypassPostJobExecutionIfErrors != job2.BypassPostJobExecutionIfErrors ) return false; 
            if (job1.IsJobVisibleInClientApp != job2.IsJobVisibleInClientApp ) return false; 
            if (job1.ConnectionString_Source.SConnID != job2.ConnectionString_Source.SConnID) return false; 
            if (job1.ConnectionString_Target.SConnID != job2.ConnectionString_Target.SConnID) return false; 
            if (job1.DatabaseName_Source != job2.DatabaseName_Source ) return false; 
            if (job1.DatabaseName_Target != job2.DatabaseName_Target ) return false; 
            if (job1.JobMethod != job2.JobMethod ) return false; 
            if (job1.ExportUseDataset != job2.ExportUseDataset ) return false; 
            if (job1.SQLDirectStream != job2.SQLDirectStream ) return false; 
            if (job1.SQLTrustTargetColumnType != job2.SQLTrustTargetColumnType ) return false; 
            if (job1.SQLDirectStreamPriority != job2.SQLDirectStreamPriority ) return false; 
            if (job1.SourceTableDeleteAfterInsert != job2.SourceTableDeleteAfterInsert ) return false; 
            if (job1.LogLevel != job2.LogLevel ) return false; 
            if (job1.HasSqlLog != job2.HasSqlLog ) return false; 
            if (job1.Threads_Source != job2.Threads_Source ) return false; 
            if (job1.Threads_Target != job2.Threads_Target ) return false; 
            if (job1.TrimData != job2.TrimData ) return false; 
            if (job1.ConvertHTMLPatternsForTarget != job2.ConvertHTMLPatternsForTarget ) return false; 
            if (job1.AlterColumnTypeOnInsert != job2.AlterColumnTypeOnInsert ) return false; 
            if (job1.AlterColumnTypeOptions != job2.AlterColumnTypeOptions ) return false; 
            if (job1.TargetTableBehavior != job2.TargetTableBehavior ) return false; 
            if (job1.CheckFieldsBeforeInsert != job2.CheckFieldsBeforeInsert ) return false; 
            if (job1.SQLTargetDisableConstraints != job2.SQLTargetDisableConstraints ) return false; 
            if (job1.SQLTargetBulkCopy != job2.SQLTargetBulkCopy ) return false; 
            if (job1.FieldAnalyzerLevel != job2.FieldAnalyzerLevel ) return false; 
            if (job1.UseNull_Target != job2.UseNull_Target ) return false; 
            if (!job1.DynParams.SequenceEqual(job2.DynParams) ) return false; 
            if (job1.SynchroTargetTableBehavior != job2.SynchroTargetTableBehavior ) return false; 
            if (job1.SynchroBypassQueryFiltersInTarget != job2.SynchroBypassQueryFiltersInTarget ) return false; 
            if (job1.SynchroStoreChanges != job2.SynchroStoreChanges ) return false; 
            if (job1.FileSourceZippedIn != job2.FileSourceZippedIn ) return false; 
            if (job1.FileCleanup_Import != job2.FileCleanup_Import ) return false; 
            if (!job1.LogMailAdress.SequenceEqual(job2.LogMailAdress) ) return false; 
            if (job1.HyperFileArrayFieldTransformation != job2.HyperFileArrayFieldTransformation ) return false;
            if (job1.DataTransformDontTransformIfVariableArraySizes != job2.DataTransformDontTransformIfVariableArraySizes) return false; 
            if (job1.DataTransformSeparatorOrLabel != job2.DataTransformSeparatorOrLabel ) return false; 
            if (job1.DataTransformAlsoCrossQueries != job2.DataTransformAlsoCrossQueries ) return false; 
            if (job1.DataTransformRowsToColumnsAddLabelToValues != job2.DataTransformRowsToColumnsAddLabelToValues ) return false; 
            if (job1.CreatePrimaryKeyAfterHavingCreatedATable != job2.CreatePrimaryKeyAfterHavingCreatedATable ) return false; 
            if (job1.AppendFileCreation != job2.AppendFileCreation ) return false; 
            if (job1.CSVCharSeparator_Target != job2.CSVCharSeparator_Target ) return false; 
            if (job1.CSVEncoding_Target != job2.CSVEncoding_Target ) return false;
            if (job1.CSVAddHeader != job2.CSVAddHeader ) return false; 
            if (job1.CSVMultipleFilesInOnePattern != job2.CSVMultipleFilesInOnePattern ) return false; 
            if (job1.CSVRowOffset != job2.CSVRowOffset ) return false; 
            if (job1.MaxRowsInAFile != job2.MaxRowsInAFile ) return false; 
            if (job1.XLSSheetToRead != job2.XLSSheetToRead ) return false; 
            if (job1.XLSRowOffset != job2.XLSRowOffset ) return false; 
            if (job1.XLSRowWriteOffset != job2.XLSRowWriteOffset ) return false; 
            if (job1.CSVAddQuotes != job2.CSVAddQuotes ) return false; 
            if (job1.XLSPasswordSource != job2.XLSPasswordSource ) return false; 
            if (job1.XLSPasswordTarget != job2.XLSPasswordTarget ) return false; 
            if (job1.XLSAddHeader != job2.XLSAddHeader ) return false; 
            if (job1.XLSWithTitle != job2.XLSWithTitle ) return false; 
            if (job1.XLSStyle != job2.XLSStyle ) return false; 
            if (job1.XMLAddCDataTag != job2.XMLAddCDataTag ) return false; 
            if (job1.TargetAddRowNum != job2.TargetAddRowNum ) return false; 
            if (job1.TargetAddDbName != job2.TargetAddDbName ) return false; 
            if (job1.TargetAddDtLoad != job2.TargetAddDtLoad ) return false; 
            if (job1.XMLRemoveTagForEmptyValues != job2.XMLRemoveTagForEmptyValues ) return false; 
            if (job1.XMLHeader != job2.XMLHeader ) return false; 
            if (job1.JSONHeader != job2.JSONHeader ) return false; 
            if (job1.XMLTargetRowBuilder != job2.XMLTargetRowBuilder ) return false; 
            if (job1.XLSInterpretFormulas != job2.XLSInterpretFormulas ) return false; 
            if (job1.XMLWriteMode != job2.XMLWriteMode ) return false; 
            if (job1.JSONTargetRowBuilder != job2.JSONTargetRowBuilder ) return false; 
            if (job1.WebserviceNuxeo_EndpointSource != job2.WebserviceNuxeo_EndpointSource ) return false; 
            if (job1.WebServiceCallMethod != job2.WebServiceCallMethod ) return false; 
            if (job1.WebServiceContentType_Target != job2.WebServiceContentType_Target ) return false; 
            if (job1.WebServiceCallMethod_Target != job2.WebServiceCallMethod_Target ) return false; 
            if (job1.WebServiceRequestBodyType != job2.WebServiceRequestBodyType ) return false; 
            if (job1.WebserviceSQLLanguage != job2.WebserviceSQLLanguage ) return false; 
            if (job1.WebserviceRawOutput != job2.WebserviceRawOutput ) return false; 
            if (job1.FileRawOutput != job2.FileRawOutput ) return false; 
            if (job1.OptionalDBName_OnInsert != job2.OptionalDBName_OnInsert ) return false; 
            if (job1.OptionalRowID_OnInsert != job2.OptionalRowID_OnInsert ) return false; 
            if (job1.OptionalTimestamp_OnInsert != job2.OptionalTimestamp_OnInsert ) return false; 
            if (job1.OptionalDynamicParamField_OnInsert != job2.OptionalDynamicParamField_OnInsert ) return false; 
            if (job1.PrePostJob_CommandSource != job2.PrePostJob_CommandSource ) return false; 
            if (job1.PrePostJob_CommandTarget != job2.PrePostJob_CommandTarget ) return false; 
            if (job1.PreOrPostCommand_Source != job2.PreOrPostCommand_Source ) return false; 
            if (job1.PreOrPostCommand_Target != job2.PreOrPostCommand_Target ) return false; 
            if (job1.PrePostJob_CommandSourceConnection != job2.PrePostJob_CommandSourceConnection ) return false; 
            if (job1.PrePostJob_CommandTargetConnection != job2.PrePostJob_CommandTargetConnection ) return false; 
            if (job1.DynParams_LoopThroughRows_Source != job2.DynParams_LoopThroughRows_Source ) return false; 
            if (job1.DynParams_LoopThroughRows_Target != job2.DynParams_LoopThroughRows_Target ) return false; 
            if (job1.WebserviceHTTP_FormatURLInUpper != job2.WebserviceHTTP_FormatURLInUpper ) return false; 
            if (job1.WebserviceSpecialHttpParameters != job2.WebserviceSpecialHttpParameters ) return false; 
            if (job1.WebserviceHTTP_SendColumnsOffset != job2.WebserviceHTTP_SendColumnsOffset ) return false; 
            if (job1.WebServiceSaveResponseFile != job2.WebServiceSaveResponseFile ) return false; 
            if (job1.WebserviceContentStructure != job2.WebserviceContentStructure ) return false; 
            if (job1.WebServiceSuccessString != job2.WebServiceSuccessString ) return false; 
            if (job1.WebserviceHTTP_DontSendEmptyValues != job2.WebserviceHTTP_DontSendEmptyValues ) return false; 
            if (job1.WebServiceTrackingColumnInResponses != job2.WebServiceTrackingColumnInResponses ) return false; 
            if (job1.WebserviceLogTableResponses != job2.WebserviceLogTableResponses ) return false; 
            if (job1.WebserviceSourcePostWork != job2.WebserviceSourcePostWork ) return false; 
            if (job1.WebserviceTypeData != job2.WebserviceTypeData ) return false; 
            if (job1.MailFlagRetrievedAsRead != job2.MailFlagRetrievedAsRead ) return false; 
            if (job1.MaxMailsToGet != job2.MaxMailsToGet ) return false; 
            if (job1.MailGetUnreadOnly != job2.MailGetUnreadOnly ) return false; 
            if (job1.MailAssembleQueriesSameRecipient != job2.MailAssembleQueriesSameRecipient ) return false; 
            if (job1.MailTargetFormat != job2.MailTargetFormat ) return false; 
            if (job1.MailTargetHTMLFileTemplate != job2.MailTargetHTMLFileTemplate ) return false; 
            if (job1.MailTargetHTMLDataSetKeyword != job2.MailTargetHTMLDataSetKeyword ) return false; 
            if (job1.ADSearchScope != job2.ADSearchScope ) return false; 
            if (job1.ADTargetBehavior != job2.ADTargetBehavior ) return false; 
            if (job1.ADSearchProperty != job2.ADSearchProperty ) return false; 
            if (job1.ADActivateEntry != job2.ADActivateEntry ) return false; 
            if (job1.RemoveIDFromMongoDBQuery != job2.RemoveIDFromMongoDBQuery ) return false; 
            if (job1.CreatePKForMongoCollection != job2.CreatePKForMongoCollection ) return false; 
            if (job1.TargetMongoCollectionBehavior != job2.TargetMongoCollectionBehavior ) return false; 
            if (job1.AutoSQLTableCreation != job2.AutoSQLTableCreation ) return false;

            return true;
        }

        internal List<Job> GetJobSteps(string sJobID)
        {
            Job INIP = GetJob(sJobID);
            return INIP == null ? new List<Job>() : INIFile.GetChildrenCount(INIP.RawJobID);
        }

        internal string GetJobVersionFromDB(Job INIP)
        {
            if (INIP != null)
            {
                string sVersion = INIFile.GetJobVersion(INIP.USER, INIP.JobID);
                return sVersion.Split(".").Length > 3 ? sVersion[0..sVersion.LastIndexOf(".")] : sVersion;
            }
            else
            {
                return "V1.0.0";
            }
        }

        internal FontWeight GetJobColor(int iRawID)
        {
            return INIFile.GetChildrenCount(iRawID).Count + 1 == 1 ? FontWeights.Normal : FontWeights.Bold;
        }

        internal bool CreateBackgroundTask(string sUserName, BackgroundTask.TaskType BgAction, object oData, CancellationToken ctsToken)
        {
            return BGTASK.CreateBGTask(BgAction, oData, ctsToken);
        }

        internal bool IsBackgroundTaskCompleted()
        {
            return BGTASK.STask == null || BGTASK.STask.IsCompleted;
        }



        #endregion

    }

    internal class IntellisenseData
    {
        private static Task INTELLISENSE_TASK;

        private string Queries = "";
        private Job JobSandbox;
        public CONNString Connection = null;
        public List<string> Patterns = new();
        public List<string> Tables = new();
        public List<TablesAndFieldsInQuery> TablesInQuery = new();
        public bool Lock = false;
        public bool IsLoading = false;
        private string Status
        {
            set
            {
                OnRunningIntellisense?.Invoke(typeof(IntellisenseData), value);
            }
        }
        public CancellationTokenSource ctsToken = new();

        public static event EventHandler<string> OnRunningIntellisense;

        private bool IsActivated = false;

        public IntellisenseData()
        {

        }

        public static List<string> AftSelectFunc(string sDriver)
        {
            switch (sDriver)
            {
                case "DB":
                    return new List<string>() {  " AS (ex : SELECT id_client AS myClient [...])",
                                                "* (ex : SELECT * FROM [...])",
                                                "DISTINCT (ex : SELECT DISTINCT * FROM [...])"};
                default:
                    return new List<string>() {  " AS (ex : SELECT id_client AS myClient [...])",
                                                "* (ex : SELECT * FROM [...])",
                                                "DISTINCT (ex : SELECT DISTINCT * FROM [...])",
                                                "TOP (ex : SELECT TOP 100 * FROM)",
                                                 "TABLE x (ex : SELECT TABLE 1 * FROM [...])",
                                                "TABLE x ONLY (ex : SELECT TABLE 1 ONLY * FROM [...])"};

            }
        }

        public static List<string> AggPatterns = new() { "GROUP BY (ex : SELECT id_client, li_client, SUM(nb_amount) FROM [...] GROUP BY id_client, li_client)" };

        public static List<string> FuncPatterns = new()
        {
            "LIMIT (ex : SELECT * FROM myTable LIMIT 100)",
            "ORDER BY (ex : SELECT * FROM [...] ORDER BY field ASC",
            "WHERE (ex : SELECT * FROM [...] WHERE field = 'value')",
            "AND (ex : SELECT * FROM [...] WHERE field = 'value' AND field2 = 0)",
            "UNION (ex : SELECT field FROM table1 UNION SELECT field FROM table2)"
        };

        public static List<string> JoinPatterns = new()
        {
            "INNER JOIN",
            "LEFT JOIN",
            "RIGHT JOIN",
            "JOIN",
            "OUTER JOIN"
        };

        public static List<string> TransfoPatterns(string sDriver)
        {
            switch (sDriver)
            {
                case "DB":
                    return new List<string> { "ANONYMIZE(field, 'CSV_FileWithValuesToUse', ColumnIndex_Base0)", "ANONYMIZE(field, 'SourceDB_TableName', ColumnIndex_Base0)" };
                default:
                    return new List<string> {  "ANONYMIZE(field, 'CSV_FileWithValuesToUse', ColumnIndex_Base0)",
            "ANONYMIZE(field, 'RANDOM')",
            "ANONYMIZE(field)",
            "CASE field WHEN 'value1' THEN 'value2' (...) ELSE 'value3' END",
            "COALESCE(field, 'replacementValue')",
            "CHARINDEX(field, 'expToFind')",
            "LENGTH(field)",
            "CONCAT(field1, field2,...)",
            "CONVERT(field, SQL type)",
            "AVG(field)",
            "SUM(field)",
            "MIN(field)",
            "MAX(field)",
            "COUNT(field)",
            "ISNULL(field, 'replacementValue')",
            "LPAD(field, paddedLength, 'padString')",
            "LTRIM(field)",
            "LOWER(field)",
            "RPAD(field, paddedLength, 'padString')",
            "RTRIM(field)",
            "REPLACE(field, 'ValueToReplace','ReplacementValue')",
            "SUBSTRING(field, startIndex, length)",
            "TRIM(field)",
            "UPPER(field)"};
            }
        }

        public bool ChangeConnection(Job job, string sQueries)
        {
            if (INTELLISENSE_TASK == null || INTELLISENSE_TASK.IsCompleted)
            {
                if (IsActivated)
                {
                    ctsToken.Cancel();

                    //while (IsLoading) { Thread.Sleep(100); }

                    ctsToken = new CancellationTokenSource();

                    Patterns.Clear();
                    Tables.Clear();
                    TablesInQuery.Clear();
                    Lock = false;

                    JobSandbox = job;
                    Queries = sQueries;

                    try
                    {
                        INTELLISENSE_TASK = Task.Factory.StartNew(() => SmartQueries());

                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                return false;
            }
            else { return true; }
        }

        internal bool UpdateQueryData(Job job, string sQueries)
        {
            if (INTELLISENSE_TASK == null || INTELLISENSE_TASK.IsCompleted)
            {
                if (IsActivated && !IsLoading && !sQueries.Equals(Queries))
                {
                    JobSandbox = job;
                    Queries = sQueries;

                    try
                    {
                        INTELLISENSE_TASK = Task.Factory.StartNew(() => SmartQueries());
                    }
                    catch
                    {
                        throw;
                    }
                }
                return false;
            }
            else { return true; }
        }

        private static List<string> CollectTablesFromRichTextBox(string sQueries, string sReplaceChar)
        {
            List<string> sListTables = new();

            string sRegexFromAs = "(FROM|JOIN)\\s+[\"`\\[]?.+[^\\(\\)][\"`\\]]?((\\s+AS\\s+)([\"`\\[]?[\\w\\d]+[\"`\\]]?)(\\s*))?";

            MatchCollection mcTables = Regex.Matches(sQueries, sRegexFromAs, RegexOptions.IgnoreCase);
            foreach (Match mc in mcTables)
            {
                if (mc.Value.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    string sT = mc.Value[(mc.Value.IndexOf(mc.Value.IndexOf("FROM ", StringComparison.OrdinalIgnoreCase) > -1 ? "FROM " : "JOIN ", StringComparison.OrdinalIgnoreCase) + 4)..];
                    sT = sT[..sT.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase)];
                    //fort risque d'erreur avec les subqueries, donc protection
                    if ((Regex.Matches(sT, "\\(").Count == Regex.Matches(sT, "\\)").Count) && (Regex.Matches(sT, "\\[").Count == Regex.Matches(sT, "\\]").Count))
                    {
                        sT = Regex.Replace(sT, @"([\|\{\}\$\?\^\!\[\]\(\)])", sReplaceChar + "$1");
                        sListTables.Add(sT.Trim());
                    }
                }
            }

            return sListTables;
        }

        private void SmartQueries()
        {
            IsLoading = true;

            LogTools MyLogProgram = new("Intellisense", System.Reflection.MethodBase.GetCurrentMethod(), null, false);

            var OldConn = Connection;
            Connection = JobSandbox.ConnectionString_Source;

            if (Connection != null && OldConn != Connection)
            {
                Status = string.Concat(Languages.Languages.ma_msg_intellisense_setconnection, "[", Connection.SConnDriverFriendlyName, "] ", Connection.SConnName);
            }

            try
            {

                //ctsToken.Token.ThrowIfCancellationRequested();

                JobSandbox.RunInSimulationMode = true;

                if (!Lock)
                {
                    List<string> sListSQL = new();
                    List<string> sListTables = new();

                    if (JobSandbox.ConnectionString_Source != null)
                    {
                        switch (JobSandbox.ConnectionString_Source.SConnDriverSuffix)
                        {
                            case "NS":
                                Status = Languages.Languages.ma_msg_intellisense_status01;
                                NOSQLTools NOSQL = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                sListTables = NOSQL.GetAllCollectionsFromDatabase(JobSandbox.DatabaseName_Source, "", ctsToken.Token);
                                if (sListTables.Count == 0)
                                {
                                    sListTables = CollectTablesFromRichTextBox(Queries, "");
                                    if (sListTables.Count == 0) { sListTables.Add(Languages.Languages.ma_msg_intellisense_sql_cantgettables + JobSandbox.DatabaseName_Source); }
                                }
                                sListTables.Add("%table_pattern% [ex : (multiple tables) SELECT * FROM %cli% will query all tables with 'cli' pattern (client, ent_cli...)]");
                                break;
                            case "DB":
                                Status = Languages.Languages.ma_msg_intellisense_status02;
                                SQLTools SQL = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                sListTables = SQL.GetAllTablesFromDatabase(JobSandbox.DatabaseName_Source, "", true, ctsToken.Token, new Query());
                                if (sListTables.Count == 0)
                                {
                                    sListTables = CollectTablesFromRichTextBox(Queries, "");
                                    if (sListTables.Count == 0) { sListTables.Add(Languages.Languages.ma_msg_intellisense_sql_cantgettables + JobSandbox.DatabaseName_Source); }
                                }
                                sListTables.Add("%table_pattern% [ex : (multiple tables) SELECT * FROM %cli% will query all tables with 'cli' pattern (client, ent_cli...)]");
                                break;
                            case "FI":
                                //on ajoute la liste des fichiers du répertoire dans la liste d'aide à l'autocomplétion
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM myfile.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT myField FROM myfile.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT myField AS field FROM myfile.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));

                                sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 1 * FROM myfile.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));
                                sListSQL.Add(string.Concat("FROM [ex : (single file) SELECT * FROM myfile.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));
                                sListSQL.Add(string.Concat("FROM [ex : (multiple files) SELECT * FROM *.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));
                                sListSQL.Add(string.Concat("FROM [ex : (multiple files) SELECT * FROM something*something*.", JobSandbox.ConnectionString_Source.SConnDriver.ToString().Split(Convert.ToChar("_"))[1].ToLower(), "]"));

                                Status = Languages.Languages.ma_msg_intellisense_status03;
                                FITools FILE = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                List<string> sFiles = FILE.GetAllFilesFromPath("." + JobSandbox.ConnectionString_Source.SConnDriver.ToString()[3..], true);
                                if (JobSandbox.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_CSV) { sFiles.AddRange(FILE.GetAllFilesFromPath("." + "TXT", true)); }
                                if (JobSandbox.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_JSON) { sFiles.AddRange(FILE.GetAllFilesFromPath("." + "JS", true)); }
                                if (sFiles.Count > 0)
                                {
                                    foreach (string sF in sFiles) { sListTables.Add(sF); }
                                }
                                else { sListTables.Add(Languages.Languages.ma_msg_intellisense_sql_cantgetfiles + JobSandbox.ConnectionString_Source.SConnString(JobSandbox.DynParams)); }
                                break;
                            case "WS":
                                Status = Languages.Languages.ma_msg_intellisense_status03b;
                                //******************************************************************************
                                //penser à CHANGER LoadQuerySourceExemple (Classe Parameters/Job) en cas d'ajout
                                //******************************************************************************
                                switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                                {
                                    case "GLPI":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM User/?range=0-5000]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT Ticket.name FROM Ticket/?order=desc&range=0-100&is_deleted=0]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT Ticket.name as TicketName FROM Ticket/?order=desc&range=0-100&is_deleted=0]"));
                                        sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM User/?range=0-5000]"));
                                        break;
                                    case "SALESFORCE_SOQL":
                                        if (JobSandbox.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL)
                                        {
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM sobjects/Account/listviews/00BD0000006j0vrMAA"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM /query/?q=SELECT+SObjectType+FROM+ObjectPermissions"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 2 ONLY id as MyId, url as MyURL from sobjects/Account/listviews"));
                                            sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM sobjects/Account/listviews]"));
                                        }
                                        else
                                        {
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT Id, Name FROM Account WHERE Name = 'Sandy']"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT Count() FROM Account]"));
                                            sListSQL.Add(string.Concat("FROM [ex : SELECT Id, Name FROM Account]"));
                                        }
                                        break;
                                    case "MICROSOFT_GRAPH":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM users"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM users?$count=true&$select=givenName,surname"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 2 ONLY givenName as theName, surname as Alias FROM users"));
                                        //sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM sobjects/Account/listviews]"));
                                        break;
                                    case "FACEBOOK_GRAPH":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM MyFacebookUserID/posts"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM MyFacebookUserID/posts?fields=id,message"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 2 ONLY id as PostID, message as PostData FROM MyFacebookUserID/posts?fields=id,message"));
                                        //sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM sobjects/Account/listviews]"));
                                        break;
                                    case "SALESFORCE":
                                        if (JobSandbox.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL)
                                        {
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM sobjects/Account/listviews/00BD0000006j0vrMAA"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM /query/?q=SELECT+SObjectType+FROM+ObjectPermissions"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 2 ONLY id as MyId, url as MyURL from sobjects/Account/listviews"));
                                            sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM sobjects/Account/listviews]"));
                                        }
                                        else
                                        {
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT Id, Name FROM Account WHERE Name = 'Sandy']"));
                                            sListSQL.Add(string.Concat("SELECT [ex : SELECT Count() FROM Account]"));
                                            sListSQL.Add(string.Concat("FROM [ex : SELECT Id, Name FROM Account]"));
                                        }
                                        break;
                                    case "YOUTUBE_V3":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM subscriptions?channelId=anyChannelID&maxResults=5]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM subscriptions?channelId=anyChannelID&maxResults=5[Optional Body Content : JSON, XML, Form-data]]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 3 ONLY id as MyChannelID FROM subscriptions?channelId=anyChannelID&maxResults=5]"));
                                        sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM subscriptions?channelId=anyChannelID&maxResults=5]"));
                                        break;
                                    case "NUXEO":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM Document WHERE ct:type != 'folder']"));
                                        sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM Document WHERE ct:type != 'folder' AND ecm:isProxy = 0]"));
                                        break;
                                    case "OQL":
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT email FROM Person]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM Person WHERE email LIKE '%.com']"));
                                        break;
                                    default:
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM MyWebserviceObject[Optional Body Content : JSON, XML, Form-data]]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT myField FROM MyWebserviceObject[Optional Body Content : JSON, XML, Form-data]]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT myField AS field FROM MyWebserviceObject[Optional Body Content : JSON, XML, Form-data]]"));
                                        sListSQL.Add(string.Concat("SELECT [ex : SELECT TABLE 1 * FROM MyWebserviceObject[Optional Body Content : JSON, XML, Form-data]]"));
                                        sListSQL.Add(string.Concat("FROM [ex : SELECT * FROM MyWebserviceObject[Optional Body Content : JSON, XML, Form-data]]"));
                                        break;
                                }

                                //tentative d'ajout des tables
                                switch (JobSandbox.WebserviceSQLLanguage)
                                {
                                    case SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL:
                                        sListTables = CollectTablesFromRichTextBox(Queries, "\\");
                                        break;
                                    case SQLTools_Enums.WEBSERVICE_SQL.SOQL:
                                        WSTools WS = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                        sListTables = WS.GetSOQLObjectsList(ctsToken.Token).Result;
                                        break;
                                    case SQLTools_Enums.WEBSERVICE_SQL.NXQL:
                                        //WSTools WS = new WSTools(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                        //sListTables = WS.GetSOQLObjectsList();
                                        break;
                                    case SQLTools_Enums.WEBSERVICE_SQL.GRAPHQL:
                                        //WSTools WS = new WSTools(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                        //sListTables = WS.GetSOQLObjectsList();
                                        break;
                                }
                                break;
                            case "AD":
                                foreach (string sA in ADTools.AD_OBJECTS)
                                {
                                    sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM ", sA, "]"));
                                    sListSQL.Add(string.Concat("SELECT [ex : SELECT samAccountName FROM ", sA, "]"));
                                    sListSQL.Add(string.Concat("SELECT [ex : SELECT samAccountName AS id_ppl FROM ", sA, "]"));
                                    sListTables.Add(sA);
                                }
                                break;
                            case "MB":
                                MailTools.MAILConnectionVariables mbVars = new(JobSandbox.ConnectionString_Source, 0, 0, JobSandbox.DynParams);
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM ", mbVars.SenderAddress, "]"));
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT SENDER FROM ", mbVars.SenderAddress, "]"));
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT SENDER as thesender FROM ", mbVars.SenderAddress, "]"));
                                sListSQL.Add(string.Concat("SELECT [ex : SELECT * FROM ", mbVars.SenderAddress, "[", mbVars.SenderPassword, "]", "]"));
                                sListTables.Add(mbVars.SenderAddress);
                                break;
                            default:
                                break;
                        }
                    }

                    if (JobSandbox.ConnectionString_Target != null)
                    {
                        switch (JobSandbox.ConnectionString_Target.SConnDriverSuffix)
                        {
                            case "DB":
                                //autocomplétion pour les requêtes de synchro
                                sListSQL.Add("SELECT [ex : (single table) SELECT mySourceField AS myTargetField FROM myTable]");
                                sListSQL.Add("SELECT [ex : (multiple tables) SELECT * FROM %something%]");
                                sListSQL.Add("AS [ex : SELECT mySourceField AS myTargetField FROM ...]");
                                sListSQL.Add("WHERE [ex : SELECT mySourceField AS myTargetField FROM [...] WHERE mySourceField = 1]");
                                sListSQL.Add("GROUP BY [UNSUPPORTED IN TARGET - WILL BE BYPASSED]");
                                sListSQL.Add("ORDER BY [UNSUPPORTED IN TARGET - WILL BE BYPASSED]");
                                break;
                        }
                    }

                    Patterns = sListSQL;
                    Tables = sListTables;
                    Lock = true;
                }
                else
                {
                    if (JobSandbox.ConnectionString_Source != null)
                    {
                        //récupération de la liste des champs des tables présentes dans la requête saisie
                        foreach (string sTable in Tables.ToList()) //tolist pour faire une copie
                        {
                            try
                            {
                                ctsToken.Token.ThrowIfCancellationRequested();

                                string sRegexFromAs = "(FROM\\s+[\"`\\[]?" + Toolbox.RemoveRegexFromString(sTable) + "[\"`\\]]?((\\s+AS\\s+)([\"`\\[]?[\\w\\d]+[\"`\\]]?)(\\s*))?)";
                                string sRegexJoinAs = "(JOIN\\s+[\"`\\[]?" + Toolbox.RemoveRegexFromString(sTable) + "[\"`\\]]?((\\s+AS\\s+)([\"`\\[]?[\\w\\d]+[\"`\\]]?)(\\s*))?)";

                                MatchCollection mcTables = Regex.Matches(Queries, string.Concat(sRegexFromAs, "|", sRegexJoinAs), RegexOptions.IgnoreCase);
                                if (mcTables.Count > 0)
                                {
                                    List<bool> bAs = new(); //default = false
                                    List<string> sAliases = new(); //default = sTable

                                    foreach (Match mc in mcTables)
                                    {

                                        if (mc.Value.IndexOf(" as ", StringComparison.InvariantCultureIgnoreCase) > -1)
                                        {
                                            string sA = mc.Value[(mc.Value.IndexOf(" as ", StringComparison.InvariantCultureIgnoreCase) + 4)..].Trim();
                                            if (!sAliases.Contains(sA))
                                            {
                                                bAs.Add(true);
                                                sAliases.Add(sA);
                                            }
                                        }
                                        else { if (!sAliases.Contains(sTable)) { bAs.Add(false); sAliases.Add(sTable); } }
                                    }

                                    //ajout des champs dispo 
                                    if (!TablesInQuery.Any(f => f.Table.Equals(sTable)))
                                    {
                                        TablesInQuery.Add(new IntellisenseData.TablesAndFieldsInQuery(sTable, sAliases, new List<string>()));
                                    }
                                    else
                                    {
                                        TablesInQuery.First(f => f.Table.Equals(sTable)).SetAliases(sAliases);
                                    }

                                    //for (int iA = 0; iA < sAliases.Count; iA++)
                                    //{

                                    //    TablesInQuery.First(f => f.Table.Equals(sTable)).RemoveAliases();
                                    //    ////ajout de l'alias si il a été saisi dans la requête
                                    //    //else if (TablesInQuery.Any(f => f.Table.Equals(sTable)) && TablesInQuery.First(f => f.Table.Equals(sTable)).Aliases.Contains(TablesInQuery.First(f => f.Table.Equals(sTable)).Table) && bAs[iA])
                                    //    //{
                                    //    //    TablesInQuery.First(f => f.Table.Equals(sTable)).AddAlias(sAliases[iA]);
                                    //    //}
                                    //    //else if (TablesInQuery.Any(f => f.Table.Equals(sTable)) && !TablesInQuery.First(f => f.Table.Equals(sTable)).Aliases.Contains(sAliases[iA]))
                                    //    //{
                                    //    //    TablesInQuery.First(f => f.Table.Equals(sTable)).ModifyAlias(sAlias);
                                    //    //}
                                    //}

                                    //champs
                                    if (TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Count == 0)
                                    {
                                        switch (JobSandbox.ConnectionString_Source.SConnDriverSuffix)
                                        {
                                            case "WS":
                                                Status = Languages.Languages.ma_msg_intellisense_status04 + sTable + ")...";
                                                //tentative d'ajout des tables
                                                switch (JobSandbox.WebserviceSQLLanguage)
                                                {
                                                    case SQLTools_Enums.WEBSERVICE_SQL.SOQL:
                                                        WSTools WS = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                                        List<string> sListFields = WS.GetSOQLAttributesList(sTable).Result;
                                                        if (sListFields.Count == 0) { TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status05 + sTable + ") : " + Languages.Languages.ma_msg_intellisense_status06); }
                                                        foreach (string sF in sListFields)
                                                        {
                                                            TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(sF);
                                                        }
                                                        break;
                                                    default:
                                                        TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status05 + sTable + ") : " + Languages.Languages.ma_msg_intellisense_status06);
                                                        TablesInQuery.First(f => f.Table.Equals(sTable)).NoDynamicFields = true;
                                                        break;
                                                }
                                                break;
                                            case "MB":
                                                Status = Languages.Languages.ma_msg_intellisense_status07 + sTable + ")...";
                                                foreach (string sF in MailTools.MAIL_FIELDS)
                                                {
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(sF);
                                                }
                                                break;
                                            case "AD":
                                                Status = Languages.Languages.ma_msg_intellisense_status08 + sTable + ")...";
                                                if (sTable.Equals("users", StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    foreach (string sF in ADTools.AD_FIELDS_USERS)
                                                    {
                                                        TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(sF);
                                                    }
                                                }
                                                else if (sTable.Equals("groups", StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    foreach (string sF in ADTools.AD_FIELDS_GROUPS)
                                                    {
                                                        TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(sF);
                                                    }
                                                }
                                                break;
                                            case "DB":
                                                //récupération de la liste des champs des tables présentes dans la requête saisie
                                                Status = Languages.Languages.ma_msg_intellisense_status09 + sTable + ")...";
                                                SQLTools SQL = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                                List<SQLColumn> SQLColumns = SQL.GetFieldsFromTable(sTable, new Query());
                                                if (SQLColumns.Count == 0)
                                                {
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status10 + sTable + ")");
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).NoDynamicFields = true;
                                                }
                                                foreach (SQLColumn SCol in SQLColumns)
                                                {
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(SCol.ColumnName);
                                                }
                                                break;
                                            case "NS":
                                                //récupération de la liste des champs des tables présentes dans la requête saisie
                                                Status = Languages.Languages.ma_msg_intellisense_status11 + sTable + ")...";
                                                NOSQLTools NOSQL = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                                List<SQLColumn> NOSQLColumns = NOSQL.GetFieldsFromCollection(sTable);
                                                if (NOSQLColumns.Count == 0)
                                                {
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status10 + sTable + ")");
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).NoDynamicFields = true;
                                                }
                                                foreach (SQLColumn SCol in NOSQLColumns)
                                                {
                                                    TablesInQuery.First(f => f.Table.Equals(sTable)).Fields.Add(SCol.ColumnName);
                                                }
                                                break;
                                            case "FI":
                                                //récupération de la liste des champs des fichiers présents dans la requête saisie
                                                FITools FI = new(JobSandbox, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);
                                                if (!FI.IsWorkingDirectoryAnFTPURL && !FI.IsWorkingDirectoryAnSFTPURL)
                                                {
                                                    Status = Languages.Languages.ma_msg_intellisense_status12 + sTable + ")...";
                                                    string sPath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobSandbox.ConnectionString_Source.SConnString(JobSandbox.DynParams), JobSandbox.DynParams); //a ce stade on a on un chemin, ou une adresse FTP
                                                    if (File.Exists(sPath + sTable))
                                                    {
                                                        FileInfo f = new(sPath + sTable);
                                                        long s1 = f.Length;

                                                        bool bGoFile = true;

                                                        TablesAndFieldsInQuery tfQ = TablesInQuery.First(t => t.Table.Equals(sTable));
                                                        if (tfQ == null || (tfQ.Fields.Count == 0 && !tfQ.NoDynamicFields))
                                                        {

                                                            //on ne traite pas les XML et JSON trop gros
                                                            if (f.Length >= JobSandbox.GlobalParameters.QASSISTANT_MAXFILESIZEANALYZE
                                                                && (JobSandbox.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_JSON
                                                                    || JobSandbox.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_XML))
                                                            {
                                                                bGoFile = false;
                                                            }

                                                            if (bGoFile) //environ 50 mo
                                                            {
                                                                try
                                                                {
                                                                    //gestion de select table x * FROM
                                                                    string sOnly = "TOP " + JobSandbox.GlobalParameters.QASSISTANT_MAXFILEROWSANALYZE;
                                                                    //int iTable = 0;
                                                                    //Match mcTableSearch = Regex.Match(Queries, "(SELECT TABLE \\d )[ONLY]?.+(FROM " + sTable + ")", RegexOptions.IgnoreCase);
                                                                    //if (mcTableSearch.Success)
                                                                    //{ 
                                                                    //    sOnly = Regex.Match(mcTableSearch.Value, SHSRegex.REGEX_QUERY_TABLE, RegexOptions.IgnoreCase).Value;
                                                                    //    iTable = Convert.ToInt32(Regex.Match(mcTableSearch.Value, SHSRegex.REGEX_QUERY_TABLE, RegexOptions.IgnoreCase).Groups[2].Value) - 1;
                                                                    //    if (iTable < 0) { iTable = 0; }
                                                                    //}  
                                                                    JobSandbox.GlobalParameters.SetAnalyzerParams(5000, JobSandbox.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT); //on va éviter de surcharger le CPU
                                                                    Query sQ = new(JobSandbox, "TEST:SELECT " + sOnly + " * FROM " + "\"" + sTable + "\"");

                                                                    //excel : tester toutes les sheets
                                                                    DataSet dsData = FI.GetDataFromFile(JobSandbox.ConnectionString_Source.SConnDriver, ref sQ);

                                                                    if (dsData == null)
                                                                    {
                                                                        TablesInQuery.First(x => x.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status10 + sTable + ")");
                                                                        TablesInQuery.First(x => x.Table.Equals(sTable)).NoDynamicFields = true;
                                                                    }
                                                                    else if (dsData.Tables.Count == 0)
                                                                    {
                                                                        TablesInQuery.First(x => x.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status10 + sTable + ")");
                                                                        TablesInQuery.First(x => x.Table.Equals(sTable)).NoDynamicFields = true;
                                                                    }
                                                                    else
                                                                    {
                                                                        for (int iTable = 0; iTable < dsData.Tables.Count; iTable++)
                                                                        {
                                                                            foreach (DataColumn SCol in dsData.Tables[iTable].Columns)
                                                                            {
                                                                                TablesInQuery.First(x => x.Table.Equals(sTable)).Fields.Add(SCol.ColumnName + (dsData.Tables.Count > 1 ? " [TABLE " + (iTable + 1).ToString() + " : " + Languages.Languages.ma_msg_queryassistant_multidata + ":SELECT TABLE " + (iTable + 1).ToString() + "...']" : ""));
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                catch (OperationCanceledException)
                                                                {
                                                                    throw;
                                                                }
                                                                catch (Exception)
                                                                {
                                                                    Status = Languages.Languages.ma_msg_intellisense_status13 + sTable + "...";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                TablesInQuery.First(fx => fx.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status14 + sTable + ") : " + Languages.Languages.ma_msg_intellisense_status15);
                                                                TablesInQuery.First(fx => fx.Table.Equals(sTable)).NoDynamicFields = true;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        TablesInQuery.First(fy => fy.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status16 + sTable + Languages.Languages.ma_msg_intellisense_status17);
                                                        TablesInQuery.First(fy => fy.Table.Equals(sTable)).NoDynamicFields = true;
                                                    }
                                                }
                                                else
                                                {
                                                    TablesInQuery.First(fz => fz.Table.Equals(sTable)).Fields.Add(Languages.Languages.ma_msg_intellisense_status18 + sTable + Languages.Languages.ma_msg_intellisense_status19);
                                                    TablesInQuery.First(fz => fz.Table.Equals(sTable)).NoDynamicFields = true;
                                                }
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    if (TablesInQuery.Any(f => f.Table.Equals(sTable)))
                                    {
                                        TablesInQuery.Remove(TablesInQuery.First(f => f.Table.Equals(sTable)));
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                IsLoading = false;
                                throw;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                IsLoading = false;
                throw;
            }

            IsLoading = false;
            Status = Languages.Languages.ma_msg_backgroundtaskpending;
        }

        internal void Enable()
        {
            IsActivated = true;
        }

        internal class TablesAndFieldsInQuery
        {
            private List<string> _sAliases = new();
            public string Table { get; set; } = "";
            public List<string> Aliases
            {
                get
                {
                    return _sAliases;
                }
            }
            public List<string> Fields { get; set; } = new List<string>();
            public bool NoDynamicFields { get; internal set; } = false;

            internal void SetAliases(List<string> sAliases)
            {
                _sAliases = sAliases;
            }

            public TablesAndFieldsInQuery(string sTable, List<string> sAliases, List<string> sFields)
            {
                _sAliases = sAliases;
                Table = sTable;
                Fields = sFields;
            }
        }

    }

    internal class BackgroundTask
    {
        public static event EventHandler<(TaskType, object)> OnFinishedTask;
        public static event EventHandler<LogObject> OnRunningTask;
        public static event EventHandler<TaskType> OnStartedTask;
        public static event EventHandler<OperationCanceledException> OnCancelledTask;

        private LogTools MyLog;

        public enum TaskType
        {
            BENCHMARK = 3,
            EXECUTE_SINGLE_QUERY = 4,
            GET_COLUMN_MAPPING = 5,
            CHECK_CROSSQUERY = 6,
            CHECK_SYNCHRO_VALIDITY = 7,
            LOAD_PLANIF_CALENDAR = 9
        }

        private readonly INIProgram INIFile;
        internal Task STask
        {
            get; set;
        }
        private TaskType STaskType
        {
            get; set;
        }

        private object TaskData;

        private System.Timers.Timer TaskTimer;

        private static List<LogObject> SListTaskInfo = new();

        internal BackgroundTask(LogTools LOG, INIProgram _pini)
        {
            MyLog = LOG;
            INIFile = _pini;

            TaskTimer = new System.Timers.Timer
            {
                Interval = 500
            };
            TaskTimer.Elapsed += OnTimedEvent;
        }

        internal bool CreateBGTask(TaskType sTaskType, object Data, CancellationToken ctsToken)
        {
            if (STask != null)
            { return false; }

            STaskType = sTaskType;

            SListTaskInfo.Clear();

            OnStartedTask?.Invoke(typeof(BackgroundTask), STaskType);
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_startbgtask + " : " + STaskType.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

            TaskTimer.Start();

            switch (sTaskType)
            {
                case TaskType.LOAD_PLANIF_CALENDAR:
                    bool[] bParam = (bool[])Data;
                    STask = Fuzible_CTL.GetPlanifCalendar(INIFile, MyLog, 1, bParam[0], bParam[1]);
                    break;

                case TaskType.BENCHMARK:

                    STask = Task.Factory.StartNew(() => Benchmarking(ctsToken));
                    break;
                case TaskType.EXECUTE_SINGLE_QUERY:
                    STask = Task.Factory.StartNew(() => INIProgram.RunSingleQueryFromTab(((Job)Data).DeepCopy(), MyLog));
                    break;
                case TaskType.GET_COLUMN_MAPPING:
                    STask = Fuzible_CTL.DataShowColumnMappings((Job)Data, MyLog, false);
                    break;
                case TaskType.CHECK_CROSSQUERY:
                    Tuple<Job, List<Query>> dataSyncInfo = (Tuple<Job, List<Query>>)Data;
                    STask = Fuzible_CTL.ViewCrossQueryInfo(dataSyncInfo.Item1, MyLog, dataSyncInfo.Item2[0], dataSyncInfo.Item2[1]);
                    break;
                case TaskType.CHECK_SYNCHRO_VALIDITY:
                    Tuple<Job, Query, bool> dataSyncCheck = (Tuple<Job, Query, bool>)Data;
                    STask = Fuzible_CTL.CheckSynchroValidity(dataSyncCheck.Item1, dataSyncCheck.Item2, MyLog, dataSyncCheck.Item3);
                    break;
            }
            Monitoring.AddThread(STask, System.Reflection.MethodBase.GetCurrentMethod());

            return true;
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (STask != null)
            {
                if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                {
                    TaskTimer.Stop();
                    STask = null;

                    //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_backgroundtaskended + " : " + STaskType.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    OnCancelledTask?.Invoke(typeof(BackgroundTask), new OperationCanceledException());
                }
                else if (STask.IsCompleted)
                {
                    if (STaskType == TaskType.GET_COLUMN_MAPPING || STaskType == TaskType.LOAD_PLANIF_CALENDAR || STaskType == TaskType.CHECK_CROSSQUERY || STaskType == TaskType.CHECK_SYNCHRO_VALIDITY)
                    {
                        Task<string> t = (Task<string>)STask;
                        TaskData = t.Result;
                    }

                    TaskTimer.Stop();
                    STask = null;


                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_backgroundtaskended + " : " + STaskType.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    OnFinishedTask?.Invoke(typeof(BackgroundTask), (STaskType, TaskData));
                }
                else
                {
                    OnRunningTask?.Invoke(typeof(BackgroundTask), SListTaskInfo.Count > 0 ? SListTaskInfo.Last() : null);
                }
            }
        }

        private void Benchmarking(CancellationToken ctsToken)
        {
            int iQteRows = 1000000;
            SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, "1/5 Creating CSV file..."));
            Job jBench = INIFile.CreateBenchmarkingJob(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\", iQteRows);

            LogTools MyLogBench = new("BENCHMARK", System.Reflection.MethodBase.GetCurrentMethod(), jBench, true);

            List<double> dSecPerTest = new();

            //test single core
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    if (i == 0) { jBench.GlobalParameters.SetMultiCore(1); SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, "2/5 Single-core - Loading Data...")); }
                    if (i == 1) { jBench.GlobalParameters.SetMultiCore(Environment.ProcessorCount); SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, "4/5 Multi-core - Loading Data...")); }

                    DateTime dtStart1 = DateTime.Now;
                    Query qBench = jBench.JobQueries[0];
                    List<DataSet> dsData = MThread.GetSourceData(0, jBench, ref qBench, MyLogBench, false);
                    dSecPerTest.Add((DateTime.Now - dtStart1).TotalMilliseconds);

                    if (dsData != null && dsData.Count > 0 && dsData[0].Tables.Count > 0)
                    {
                        if (i == 0) { SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, "3/5 Single-core - Analyzing Data...")); }
                        if (i == 1) { SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, "5/5 Multi-core - Analyzing Data...")); }
                        DateTime dtStart2 = DateTime.Now;
                        SHSOperations shsBench = new(jBench, qBench, ref MyLogBench);
                        shsBench.GetListFieldsTypesFromDataset(dsData[0].Tables[0], -1, qBench, SQLTools_Enums.CLASS_PURPOSE.SRC);
                        dSecPerTest.Add((DateTime.Now - dtStart2).TotalMilliseconds);
                        dsData.Clear();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    else { break; }
                }

                //formatage de la réponse :
                StringBuilder sbBench = new();
                sbBench.AppendLine("Benchmark Result :");
                sbBench.AppendLine(string.Concat("Single-Core - Loading Data in RAM : ", Math.Round(dSecPerTest[0], 0).ToString(), " ms."));
                sbBench.AppendLine(string.Concat("Single-Core - Analyzing Data : ", Math.Round(dSecPerTest[1], 0).ToString(), " ms."));
                sbBench.AppendLine(string.Concat("-> Single-Core Result : ", Math.Round(1000000 - (dSecPerTest[0] + dSecPerTest[1]), 0).ToString()));
                sbBench.AppendLine(string.Concat("Multi-Core - Loading Data in RAM : ", Math.Round(dSecPerTest[2], 0).ToString(), " ms."));
                sbBench.AppendLine(string.Concat("Multi-Core - Analyzing Data : ", Math.Round(dSecPerTest[3], 0).ToString(), " ms."));
                sbBench.AppendLine(string.Concat("-> Multi-Core Result : ", Math.Round(1000000 - (dSecPerTest[2] + dSecPerTest[3]), 0).ToString()));
                SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, sbBench.ToString()));
            }
            catch (OperationCanceledException ex)
            {
                StringBuilder sbBench = new();
                sbBench.AppendLine("Benchmark Result :");
                sbBench.AppendLine(ex.Message);
                SListTaskInfo.Add(new LogObject(DateTime.Now, System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, SQLTools_Enums.LOG_TYPEINFO.INF, sbBench.ToString()));
            }
        }

    }
}
