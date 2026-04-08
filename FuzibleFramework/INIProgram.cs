using Microsoft.Data.Sqlite;
using OfficeOpenXml;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static FuzibleFramework.Query;
using static FuzibleFramework.SQLTools_Enums;

namespace FuzibleFramework
{

    public class INIProgram
    {
        #region "VARIABLES"
        public static Guid InstanceSeed = Guid.NewGuid();

        public static event EventHandler<(int, string)> OnLoading;
        public static event EventHandler<(int, Job, string)> OnJobEvent;

        public readonly string USER = Environment.UserName;
        public static string APP_NAME = "Fuzible.exe";
        public static Version APP_VERSION { get; } = Assembly.GetExecutingAssembly().GetName().Version;
        public static readonly DateTimeFormatInfo SYSTEM_CULTURE_DATE = CultureInfo.GetCultureInfoByIetfLanguageTag(CultureInfo.CurrentCulture.IetfLanguageTag).DateTimeFormat;
        public static readonly NumberFormatInfo SYSTEM_CULTURE_NUMBER = CultureInfo.GetCultureInfoByIetfLanguageTag(CultureInfo.CurrentCulture.IetfLanguageTag).NumberFormat;


        private MAINParameters _GlobalParameters;
        private CONNStrings _Connections;
        private List<Job> _jJobsList = new();
        internal static string INTERNAL_DB = "Data Source=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db;foreign keys=true";
        internal static string PATH_JOBS_XML = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\JOBS\\";

        #endregion

        #region "PROPRIETES"

        public bool IsReadOnly { get; private set; } = false;
        public CONNStrings Connections
        {
            internal set
            {
                _Connections = value;
            }
            get
            {
                return _Connections;
            }
        }
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
        public List<Job> UserJobsList
        {
            get
            {
                return _jJobsList.OrderBy(c => c.ConnectionString_Source == null ? "" : c.ConnectionString_Source.SConnDriverSuffix).ThenBy(c => c.ConnectionString_Target == null ? "" : c.ConnectionString_Target.SConnDriverSuffix).ThenBy(c => c.JobNAME).ToList();
            }
        }

        public bool FirstStart { get; private set; } = false;

        #endregion

        #region "STATIC"

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        public void LoadUserJobs(string sUser, bool bReadOnly)
        {
            try
            {
                _jJobsList.Clear();

                DataSet dsData = new();

                string sQuery = string.Concat("SELECT * FROM job_parameters WHERE \"user\" = '", sUser, "' ORDER BY \"job_id\" ASC;");
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Jobs Parameters");
                }

                if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    for (int iR = 0; iR < dsData.Tables[0].Rows.Count; iR++)
                    {
                        OnLoading?.Invoke(typeof(INIProgram), (1, "Loading Job " + dsData.Tables[0].Rows[iR]["job_id"].ToString()));
                        _jJobsList.Add(new Job(GlobalParameters, dsData.Tables[0].Rows[iR]["job_id"].ToString(), USER, false, dsData.Tables[0].Rows[iR]));
                    }
                }
                else
                {
                    if (!bReadOnly)
                    {
                        OnLoading?.Invoke(typeof(INIProgram), (1, "Creating Sample Jobs"));
                        Job SampleJobA = CreateSampleJob();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            _jJobsList = _jJobsList.OrderBy(o => o.JobNAME).ToList();

        }

        public Job GetJob(string sJobID)
        {
            int iIdxJob = UserJobsList.FindIndex(j => j.JobID.Equals(sJobID));
            if (iIdxJob > -1)
            {
                return UserJobsList[iIdxJob];
            }
            else { return null; }
        }

        #endregion

        #region "PUBLIC VOID"

        public INIProgram(string sUsername, bool bDynamicCheckLicense, bool bReadOnly)
        {
            IsReadOnly = bReadOnly;

            //if (bReadOnly)
            //{
            //    if (!INTERNAL_DB.Contains("Read Only", StringComparison.OrdinalIgnoreCase))
            //    {
            //        if (INTERNAL_DB.EndsWith(";"))
            //        {
            //            INTERNAL_DB = string.Concat(INTERNAL_DB, "Read Only=true");
            //        }
            //        else { INTERNAL_DB = string.Concat(INTERNAL_DB, ";Read Only=true"); }
            //    }
            //}
            //else
            //{
            //    if (INTERNAL_DB.Contains("Read Only=true", StringComparison.OrdinalIgnoreCase))
            //    {
            //        INTERNAL_DB = INTERNAL_DB.Replace("Read Only=true", "Read Only=false", StringComparison.OrdinalIgnoreCase);
            //    }
            //}

            if (!Directory.Exists(PATH_JOBS_XML))
            {
                try
                {
                    Directory.CreateDirectory(PATH_JOBS_XML);
                }
                catch (Exception ex)
                {
                    OnLoading?.Invoke(typeof(INIProgram), (2, "Create Job Path : " + ex.Message));
                }
            }

            string sFileDB = INTERNAL_DB[(INTERNAL_DB.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase) + 12)..];
            sFileDB = sFileDB.Split(Convert.ToChar(";"))[0];

            //création éventuelle de la DB si inexistante
            if (!bReadOnly && INTERNAL_DB.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase) > -1)
            {
                if (!File.Exists(sFileDB) || new FileInfo(sFileDB).Length == 0)
                {
                    OnLoading?.Invoke(typeof(INIProgram), (1, "Creating Internal Database..."));
                    try
                    { SQLQueries.CreateInternalDB(sFileDB); FirstStart = true; }
                    catch (Exception ex)
                    {
                        OnLoading?.Invoke(typeof(INIProgram), (2, "Creating Internal DB : " + ex.Message));
                        throw;
                    }
                }
            }

            if (new FileInfo(sFileDB).Length > 0)
            {

                USER = sUsername.ToUpper();

                OnLoading?.Invoke(typeof(INIProgram), (1, "Updating Internal Database..."));
                //changement de version : création des colonnes manquantes !
                CreateMissingColumns();

                //contrôle pour savoir si on est en mode de sécurité "utilisateurs partagés" 
                if (!bReadOnly) { USER = CheckSharedUsersOption(USER); }

                //chargement des paramètres généraux de l'utilisateur
                try
                {
                    OnLoading?.Invoke(typeof(INIProgram), (1, "Loading Global Settings..."));
                    GlobalParameters = new MAINParameters(USER, bDynamicCheckLicense, bReadOnly);
                }
                catch (Exception ex)
                {
                    OnLoading?.Invoke(typeof(INIProgram), (2, "Loading Global Parameters : " + ex.Message));
                    throw;
                }

                OnLoading?.Invoke(typeof(INIProgram), (1, "Loading Connections..."));
                //chargement des connections de l'utilisateur
                try { Connections = new CONNStrings(GlobalParameters.FUZIBLE_SERVER, USER, bReadOnly); }
                catch (Exception ex)
                {
                    OnLoading?.Invoke(typeof(INIProgram), (2, "Loading Connections : " + ex.Message));
                    throw;
                }

                GlobalParameters.Connections = Connections;

                OnLoading?.Invoke(typeof(INIProgram), (1, "Loading User Jobs..."));
                //chargement des jobs de l'utilisateur
                try { LoadUserJobs(USER, bReadOnly); }
                catch (Exception ex)
                {
                    OnLoading?.Invoke(typeof(INIProgram), (2, "Loading User Jobs : " + ex.Message));
                    throw;
                }
            }
            else { throw new Exception(Languages.Languages.par_dbcorrupted); }

            OnLoading?.Invoke(typeof(INIProgram), (3, "Successfully Loaded"));
        }

        private static string CheckSharedUsersOption(string sUser)
        {
            DataSet dsData = new();

            string sQuery = string.Concat("SELECT \"security_principal_user\", \"user\" FROM \"user_parameters\" WHERE \"security_shared_users\" = 1 AND \"user\" != '" + sUser + "'");
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Shared Users");
            }

            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                string sUserTemp = dsData.Tables[0].Rows[0]["security_principal_user"].ToString();
                if (sUserTemp.Length > 0)
                {
                    sUser = FITools.EncryptionSystem.AES_Decrypt(sUserTemp, dsData.Tables[0].Rows[0]["user"].ToString());
                }
            }

            return sUser;
        }

        public static string GetUserLanguage(string sUser)
        {
            DataSet dsData = new();

            string sQuery = "";
            sQuery = "SELECT COUNT(*) as tbexists FROM sqlite_master WHERE type='table' AND name='user_parameters';";
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Table Exists");
            }

            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0 && dsData.Tables[0].Rows[0]["tbexists"].ToString().Equals("1"))
            {
                dsData = new DataSet();
                sQuery = string.Concat("SELECT \"language\", \"user\"",
                                        " FROM \"user_parameters\"",
                                        " WHERE \"user\" = '", sUser, "'",
                                        " UNION",
                                        " SELECT \"language\", \"user\"",
                                        " FROM \"user_parameters\"",
                                        " WHERE \"user\" != '", sUser, "'",
                                        " AND \"security_shared_users\" = 1;");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Shared Users");
                }

                if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    return dsData.Tables[0].Rows[0]["language"].ToString().ToUpper();
                }
                else { return CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToUpper(); }
            }
            else { return CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToUpper(); }
        }

        public bool ExecuteJob(Job INIP, LogTools MyLogProgram, List<string> sListDynParams, string sDelegateUser, List<Query> QueriesTemp = null)
        {
            OnJobEvent?.Invoke(typeof(INIProgram), (0, INIP, ""));

            INIP.IsRunning = true;

            Monitoring.StartStopProgram(MyLogProgram);

            List<Job> jSubJobs = GetChildrenCount(INIP.RawJobID);
            MyLogProgram.StartJobLog(INIP, sDelegateUser, sListDynParams, jSubJobs.Count);

            int iStartJob = INIP.SubJobID;
            int iCountSubJobs = 1;
            if (INIP.SubJobID > 1)
            {
                iCountSubJobs = INIP.SubJobID;
            }
            else { iCountSubJobs = jSubJobs.Count + 1; }

            int iQueries = 0;
            int iLoop = 0;
            int iErrors = 0;
            int iWarnings = 0;
            DateTime dtStartJob = DateTime.Now;
            DateTime dtJob = DateTime.Now;

            bool Continue = true;

            MThread mtClass = null;

            for (int iJob = iStartJob; iJob <= iCountSubJobs; iJob++)
            {
                iLoop++;

                try
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    iErrors = MyLogProgram.JobErrors;
                    iWarnings = MyLogProgram.JobWarnings;
                    dtJob = DateTime.Now;

                    Job JobRun;
                    if (INIP.RawJobID == 0) { JobRun = INIP; }
                    else if (iJob > 1) { JobRun = jSubJobs[iJob - 2]; }
                    else { JobRun = INIP; }

                    JobRun.RunInSimulationMode = INIP.RunInSimulationMode;

                    if (!Continue && JobRun.AbortSubJobExecutionIfNoData)
                    {
                        switch (JobRun.NoSourceDataNoError)
                        {
                            case 0:
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_cancelsubjobnodata, SQLTools_Enums.LOG_TYPEINFO.INF);
                                break;
                            case 1:
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_cancelsubjobnodata, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                break;
                            case 2:
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_cancelsubjobnodata, SQLTools_Enums.LOG_TYPEINFO.ERR);
                                break;
                        }
                        break;
                    }

                    if ((!MyLogProgram.HasNoErrors) && INIP.AbortSubJobExecutionIfErrors)
                    {
                        MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_startjobaborting, SQLTools_Enums.LOG_TYPEINFO.WNG);
                        break;
                    }

                    //information en cas de subjob
                    if (iJob > 1) { MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.par_startsubjob, JobRun.JobNAME, " (", iJob.ToString(), "/", iCountSubJobs.ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.INF); }

                    JobRun.DynParams = sListDynParams;

                    //exécution des pré-traitements si demandé
                    List<string> sListNewParams = ExecutePrePostJobOperations(JobRun, MyLogProgram, true, sListDynParams, INIP.JobQueries);

                    int iMaxIter = 1;

                    if (sListDynParams.SequenceEqual(sListNewParams) && sListDynParams.Any(p => p.StartsWith("%"))) //JobRun.DynParams_LoopThroughRows
                    {
                        if (JobRun.DynParams_LoopThroughRows)
                        {
                            iMaxIter = 0;

                            switch (JobRun.NoSourceDataNoError)
                            {
                                case 0:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.ma_msg_dynparams_nodataforloop, " (", string.Join(",", JobRun.DynParams), ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
                                    break;
                                case 1:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.ma_msg_dynparams_nodataforloop, " (", string.Join(",", JobRun.DynParams), ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    break;
                                case 2:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.ma_msg_dynparams_nodataforloop, " (", string.Join(",", JobRun.DynParams), ")"), SQLTools_Enums.LOG_TYPEINFO.ERR);
                                    break;
                            }
                        }
                    }
                    else if (sListNewParams.Count > 0)
                    {
                        JobRun.DynParams = sListNewParams;
                    }

                    List<string> sListDynParamsPreserved = new();
                    foreach (string s in JobRun.DynParams)
                    { sListDynParamsPreserved.Add(s); }
                    //si on doit boucler sur les résultats des paramètres dynamiques
                    //problème si on a invoqué à la fois des dynparams issues de la source et de la cible
                    //0 = quantité à boucler, 1 = dynparam à réécrire à chaque boucle
                    List<int[]> iDynParamLoop = new();

                    if (JobRun.DynParams_LoopThroughRows && iMaxIter > 0)
                    {
                        for (int iD = 0; iD < JobRun.DynParams.Count; iD++)
                        {
                            iDynParamLoop.Add(new int[] { 1, iD });

                            string[] sSplit = Regex.Split(JobRun.DynParams[iD], SHSRegex.REGEX_SPLIT_DYNPARAMS);
                            if (iDynParamLoop[iD][0] < sSplit.Length)
                            {
                                iDynParamLoop[iD][0] = sSplit.Length;
                                iDynParamLoop[iD][1] = iD;
                            }
                        }
                        if (iDynParamLoop.Count > 0)
                        {
                            iMaxIter = iDynParamLoop.Max(i => i[0]);
                            //Languages.Languages.ma_msg_dynparams_loopquantity_01, " '", sListDynParams[iDynParamLoop.Last()[1]], "'. ", 
                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.ma_msg_dynparams_loopquantity_02, iMaxIter.ToString(), " ", Languages.Languages.ma_msg_dynparams_loopquantity_03), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                    }

                    //flag pour identifier, dans un job itératif, qu'on est déjà passé par la case "truncate"
                    bool bTargetBehaviorAlreadyProcessed = false;
                    string sTargetProcessed = "";

                    //on va lancer autant de fois le job qu'il y a d'iwtérations dans le paramètre dynamique
                    for (int iPD = 0; iPD < iMaxIter; iPD++)
                    {
                        JobRun = JobRun.DeepCopy();

                        //changement du comportement en cas. C'est un gros parti pris !!!
                        //problème : si on est sur un job en mode TRUNCATE et que la première itération n'a pas fonctionné
                        //il ne jouera jamais le TRUNCATE ! et donc les prochaines itérations vont potentiellement ne plus fonctionner
                        if (iPD > 0 && mtClass != null)
                        {
                            //on va chercher dans l'itération précédente si 
                            //si il y en avait une qui est associée au job, a déjà été réalisée
                            if (!bTargetBehaviorAlreadyProcessed)
                            {
                                foreach (var q in mtClass.GetFinalQueries)
                                {
                                    bTargetBehaviorAlreadyProcessed = (q.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == QUERY_PROPERTIES.TARGET_BEHAVIOR_PROCESSED) ? Convert.ToBoolean(q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.TARGET_BEHAVIOR_PROCESSED).Value) : false);
                                    if (bTargetBehaviorAlreadyProcessed)
                                    {
                                        sTargetProcessed = q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.TARGET_BEHAVIOR_PROCESSED).Element;
                                        break;
                                    }
                                }
                            }

                            if (bTargetBehaviorAlreadyProcessed)
                            {
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.ma_msg_dynparams_loop_targetbehavior + sTargetProcessed, SQLTools_Enums.LOG_TYPEINFO.DET);
                                //log qu'il n'y a pas de truncate à faire ?
                                JobRun.AppendFileCreation = true;
                                JobRun.TargetMongoCollectionBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
                                JobRun.TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
                            }
                        }

                        List<Query> sListQueries = new();

                        if (JobRun.DynParams_LoopThroughRows)
                        {
                            StringBuilder sbParamToValue = new();
                            //réécriture du paramètre dynamique 
                            foreach (int[] iPar in iDynParamLoop)
                            {
                                string[] sSplit = Regex.Split(sListDynParamsPreserved[iPar[1]], SHSRegex.REGEX_SPLIT_DYNPARAMS);
                                JobRun.DynParams[iPar[1]] = sSplit.Length > iPD ? sSplit[iPD] : sSplit[0];
                                sbParamToValue.Append("{" + sListDynParams[iPar[1]] + " => " + JobRun.DynParams[iPar[1]] + "} ");
                            }
                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat("--- [", iPD.ToString() + "/", iMaxIter.ToString(), "] ", Languages.Languages.ma_msg_dynparams_loopquantity_04, sbParamToValue.ToString(), " ---"), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }

                        if (JobRun.RawJobID == 0) //job temporaire, non sauvegardé
                        {
                            JobRun.JobQueries = QueriesTemp;
                        }
                        else { JobRun.LoadJobQueries(true); }

                        //copie des requêtes du Job
                        foreach (Query Q in JobRun.JobQueries)
                        {
                            if (Q.IsEnabled) { sListQueries.Add(new Query(JobRun, Q.RawQuery)); }
                        }

                        int iQteQueries = sListQueries.Count;
                        List<Query> qListToRemove = new();
                        bool bCSplittedInParts = false;

                        for (int iQ = 0; iQ < iQteQueries; iQ++)
                        {
                            JobRun = JobRun.DeepCopy();

                            if (sListQueries[iQ].ConnectionSrc.SConnDriverSuffix.Equals("FI")) //gestion des requêtes fichiers de type select * from *.*
                            {
                                List<Query> sNewQueries = Job.GetFilesQueriesFromSingleQuery(sListQueries[iQ], JobRun, false, ref MyLogProgram, ref bCSplittedInParts);
                                if (sNewQueries.Count == 0)
                                {
                                    qListToRemove.Add(sListQueries[iQ]);

                                    switch (JobRun.NoSourceDataNoError)
                                    {
                                        case 0:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                                            break;
                                        case 1:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            break;
                                        case 2:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                                            break;
                                    }
                                }
                                else
                                {
                                    qListToRemove.Add(sListQueries[iQ]);
                                    sListQueries.AddRange(sNewQueries);
                                }
                            }
                            else if (sListQueries[iQ].ConnectionSrc.SConnDriverSuffix.Equals("DB")) //gestion des requêtes database de type select * from *.*
                            {
                                List<Query> sNewQueries = Job.GetSQLTablesFromSingleQuery(sListQueries[iQ], JobRun, ref MyLogProgram);
                                if (sNewQueries.Count == 0)
                                {
                                    qListToRemove.Add(sListQueries[iQ]);

                                    switch (JobRun.NoSourceDataNoError)
                                    {
                                        case 0:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                                            break;
                                        case 1:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            break;
                                        case 2:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                                            break;
                                    }
                                }
                                else
                                {
                                    qListToRemove.Add(sListQueries[iQ]);
                                    sListQueries.AddRange(sNewQueries);
                                }
                            }
                            else if (sListQueries[iQ].ConnectionSrc.SConnDriverSuffix.Equals("NS")) //gestion des requêtes database de type select * from *.*
                            {
                                List<Query> sNewQueries = Job.GetSQLTablesFromSingleQuery(sListQueries[iQ], JobRun, ref MyLogProgram);
                                if (sNewQueries.Count == 0)
                                {
                                    qListToRemove.Add(sListQueries[iQ]);

                                    switch (JobRun.NoSourceDataNoError)
                                    {
                                        case 0:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                                            break;
                                        case 1:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            break;
                                        case 2:
                                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + sListQueries[iQ].QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                                            break;
                                    }
                                }
                                else
                                {
                                    qListToRemove.Add(sListQueries[iQ]);
                                    sListQueries.AddRange(sNewQueries);
                                }
                            }
                            else if (sListQueries[iQ].ConnectionSrc.SConnDriverSuffix.Equals("WS")) //gestion des requêtes database de type select * from *.*
                            {
                                List<Query> sNewQueries = Job.GetAPIFilesFromSingleQuery(sListQueries[iQ], JobRun, ref MyLogProgram);
                                if (sNewQueries.Count > 0)
                                {
                                    qListToRemove.Add(sListQueries[iQ]);
                                    sListQueries.AddRange(sNewQueries);
                                }
                            }
                        }
                        foreach (Query Q in qListToRemove) { sListQueries.Remove(Q); }

                        if (sListQueries.Count > 0)
                        {
                            iQueries += sListQueries.Count; //compteur général de la quantité de requêtes initiées par le job

                            JobRun.IsRunning = true;

                            SQLTools_Enums.REPSYNC_INSERT_METHOD rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY;
                            if (sListQueries.Count > 1)
                            {
                                string sMerge = sListQueries[0].RawOutput;
                                bool bMerge = true;

                                switch (JobRun.ConnectionString_Target.SConnDriverSuffix)
                                {
                                    case "MB":
                                        if (JobRun.MailAssembleQueriesSameRecipient)
                                        {
                                            rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.ALL_IN_ONE;
                                        }
                                        break;
                                    case "WS":
                                        break;
                                    case "FI":
                                        if (!bCSplittedInParts)
                                        {
                                            if (JobRun.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.FI_XLS)
                                            {
                                                List<string> sListTables = new();
                                                foreach (Query sQ in sListQueries)
                                                {
                                                    sListTables.Add(sQ.QueryAnalyzer.Tables[0].Alias);
                                                }
                                                if (sListTables.Count == sListTables.Distinct().Count())
                                                {
                                                    rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.ALL_IN_ONE;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                foreach (Query sQ in sListQueries)
                                                {
                                                    if (!sQ.RawOutput.Equals(sMerge))
                                                    {
                                                        bMerge = false; break;
                                                    }
                                                }
                                                if (sListQueries.Count > 1 && bMerge)
                                                {
                                                    rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.MERGE;
                                                }
                                            }
                                        }
                                        break;
                                    case "DB":
                                        if (!bCSplittedInParts)
                                        {
                                            foreach (Query sQ in sListQueries)
                                            {
                                                if (!sQ.RawOutput.Equals(sMerge))
                                                {
                                                    bMerge = false; break;
                                                }
                                            }
                                            if (sListQueries.Count > 1 && bMerge)
                                            {
                                                rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.MERGE;
                                            }
                                        }
                                        break;
                                    case "NS":
                                        if (!bCSplittedInParts)
                                        {
                                            foreach (Query sQ in sListQueries)
                                            {
                                                if (!sQ.RawOutput.Equals(sMerge))
                                                {
                                                    bMerge = false; break;
                                                }
                                            }
                                            if (sListQueries.Count > 1 && bMerge)
                                            {
                                                rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.MERGE;
                                            }
                                        }
                                        break;
                                    case "AD":
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (iLoop > 1) //permet d'inscrire dans le job report HTML le nom du sous-job pour bien séparer les lignes du rapport
                            {
                                MyLogProgram.AddJobReportRowSubJob(JobRun.JobNAME);
                            }

                            //exécution des insert en multithread
                            mtClass = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLogProgram, sListQueries.Count, 0);

                            if (mtClass.QuantityOfThreadsToCompute > 0)
                            {
                                for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                                {
                                    int numeroThread = numThread;
                                    System.Threading.Tasks.Task th = new(() => mtClass.RepSyncTask(numeroThread, sListQueries, rsInsertMethod, false));
                                    Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                    th.Start();
                                }

                                while (!mtClass.AreAllThreadsFinished)
                                {
                                    Thread.Sleep(100);
                                }
                                if (mtClass.HasOperationBeenCancelled)
                                {
                                    throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")");
                                }
                                if (!MThread.DataHasBeenRetrieved)
                                {
                                    Continue = false;
                                }
                            }
                            else
                            {
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_startjobcantstartmultithread, SQLTools_Enums.LOG_TYPEINFO.ERR);
                            }
                            bool bJobOK = MyLogProgram.HasNoErrors;

                            //gestion des post-traitement dans la SOURCE (job SQL + lancement autre job)
                            if (bJobOK || (!bJobOK && !JobRun.BypassPostJobExecutionIfErrors))
                            {
                                var newParams = ExecutePrePostJobOperations(JobRun, MyLogProgram, false, sListDynParams, sListQueries);
                                if (newParams.Count > 0)
                                {
                                    JobRun.DynParams = newParams;
                                }
                            }


                            //échec Job ?
                            if (!bJobOK && JobRun.BypassPostJobExecutionIfErrors && JobRun.PrePostJob_CommandSource.Length > 0 && JobRun.PreOrPostCommand_Source == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS)
                            {
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_startjobcantrunpostcmd, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }

                            //log synthèse
                            StringBuilder sbQuerySummary = new();
                            foreach (var q in mtClass.GetFinalQueries)
                            {
                                string sRetrieved = (q.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == QUERY_PROPERTIES.ROWS_RETRIEVED) ? Languages.Languages.par_jobsummary_rowsretrieved + q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.ROWS_RETRIEVED).Value : Languages.Languages.par_jobsummary_rowsretrieved + "0");
                                string sInserted = (q.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == QUERY_PROPERTIES.ROWS_INSERTED) ? " / " + Languages.Languages.par_jobsummary_rowsinserted + q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.ROWS_INSERTED).Value : "");
                                string sUpdated = (q.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == QUERY_PROPERTIES.ROWS_UPDATED) ? " / " + Languages.Languages.par_jobsummary_rowsupdated + q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.ROWS_UPDATED).Value : "");
                                string sDeleted = (q.QueryAnalyzer.QueryProperties.Any(qP => qP.Property == QUERY_PROPERTIES.ROWS_DELETED) ? " / " + Languages.Languages.par_jobsummary_rowsdeleted + q.QueryAnalyzer.QueryProperties.FirstOrDefault(qP => qP.Property == QUERY_PROPERTIES.ROWS_DELETED).Value : "");
                                sbQuerySummary.Append(string.Concat(" -> ", q.OutputTable, " : ", sRetrieved, sInserted, sUpdated, sDeleted));
                            }
                            //astuce du "{}" pour identifier l'information de synthèse dans les logs (app_stacklaunch) issus des exécutions planifiées
                            string iter = Regex.Replace(iMaxIter.ToString(), @"\d", "0");
                            string sSummary = string.Concat("{ ", JobRun.JobNAME, " #", (iPD + 1).ToString(iter), " : ", Languages.Languages.par_jobsummary, sbQuerySummary.ToString().Trim(), " }");

                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, sSummary, SQLTools_Enums.LOG_TYPEINFO.INF);

                            JobRun.IsRunning = false;
                        }
                        else
                        {
                            switch (JobRun.NoSourceDataNoError)
                            {
                                case 0:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_startjobnoqueries, SQLTools_Enums.LOG_TYPEINFO.INF);
                                    break;
                                case 1:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_startjobnoqueries, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    break;
                                case 2:
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_startjobnoqueries, SQLTools_Enums.LOG_TYPEINFO.ERR);
                                    break;
                            }
                        }
                    }

                    //information de dernier lancement de job
                    iErrors = MyLogProgram.JobErrors - iErrors;
                    iWarnings = MyLogProgram.JobWarnings - iWarnings;
                    TimeSpan ts = DateTime.Now - dtJob;
                    string sSt = string.Concat(Languages.Languages.par_startjobreport01, string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds), Languages.Languages.par_startjobreport02, iErrors.ToString(), Languages.Languages.par_startjobreport03, iWarnings.ToString());

                    UpdateJobExecution(JobRun, sListDynParams, DateTime.Now, sSt, IsReadOnly);
                }
                catch (OperationCanceledException ex)
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, Languages.Languages.shs_operation_cancelled, SQLTools_Enums.LOG_TYPEINFO.ERR);
                    Task.Factory.StartNew(() => OnJobEvent.Invoke(typeof(INIProgram), (1, INIP, Languages.Languages.shs_operation_cancelled)));
                    return false;
                }
                catch (Exception ex)
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, Languages.Languages.par_startjobkounhandled, SQLTools_Enums.LOG_TYPEINFO.ERR);
                    Task.Factory.StartNew(() => OnJobEvent.Invoke(typeof(INIProgram), (2, INIP, Languages.Languages.par_startjobkounhandled)));
                    return false;
                }
            }

            string sStatus = MyLogProgram.EndJobLog(INIP, sDelegateUser);

            INIP.IsRunning = false;
            INIP.SaveJobStats((DateTime.Now - dtStartJob).TotalSeconds, MyLogProgram.JobWarnings, MyLogProgram.JobErrors, iQueries);

            Task.Factory.StartNew(() => OnJobEvent.Invoke(typeof(INIProgram), (3, INIP, sStatus)));

            Monitoring.StartStopProgram(MyLogProgram);

            return true;
        }

        public static void RunSingleQueryFromTab(Job INIP, LogTools MyLogSingleQ)
        {
            INIP.IsRunning = true;

            Monitoring.StartStopProgram(MyLogSingleQ);

            List<Query> sListQueries = new() { INIP.JobQueries[0] };
            Query Q = INIP.JobQueries[0];

            bool bCSplittedInParts = false;

            if (Q.ConnectionSrc.SConnDriverSuffix.Equals("FI")) //gestion des requêtes fichiers de type select * from *.*
            {
                List<Query> sNewQueries = Job.GetFilesQueriesFromSingleQuery(Q, INIP, false, ref MyLogSingleQ, ref bCSplittedInParts);
                if (sNewQueries.Count > 0)
                {
                    sListQueries.AddRange(sNewQueries);
                    sListQueries.Remove(Q);
                }
                else
                {
                    sListQueries.Remove(Q);

                    switch (INIP.NoSourceDataNoError)
                    {
                        case 0:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case 1:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        case 2:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                            break;
                    }
                }
            }
            else if (Q.ConnectionSrc.SConnDriverSuffix.Equals("DB")) //gestion des requêtes database de type select * from *.*
            {
                List<Query> sNewQueries = Job.GetSQLTablesFromSingleQuery(Q, INIP, ref MyLogSingleQ);
                if (sNewQueries.Count > 0)
                {
                    sListQueries.AddRange(sNewQueries);
                    sListQueries.Remove(Q);
                }
                else
                {
                    sListQueries.Remove(Q);

                    switch (INIP.NoSourceDataNoError)
                    {
                        case 0:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case 1:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        case 2:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                            break;
                    }
                }
            }
            else if (Q.ConnectionSrc.SConnDriverSuffix.Equals("NS")) //gestion des requêtes database de type select * from *.*
            {
                List<Query> sNewQueries = Job.GetSQLTablesFromSingleQuery(Q, INIP, ref MyLogSingleQ);
                if (sNewQueries.Count > 0)
                {
                    sListQueries.AddRange(sNewQueries);
                    sListQueries.Remove(Q);
                }
                else
                {
                    sListQueries.Remove(Q);

                    switch (INIP.NoSourceDataNoError)
                    {
                        case 0:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case 1:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        case 2:
                            MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_nofileortablewithpattern + Q.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.LOG_TYPEINFO.ERR);
                            break;
                    }
                }
            }

            if (sListQueries.Count > 0)
            {
                SQLTools_Enums.REPSYNC_INSERT_METHOD rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY;
                //astuce qui permet, en mode mail, de sauvegarder les différents dataset de requêtes en mémoire pour faire des mails groupés
                //idem quand on est en mode multi-requêtes et qu'on veut forcer le target name
                if (sListQueries.Count > 1)
                {
                    if (INIP.ConnectionString_Target.SConnDriverSuffix.Equals("MB") && INIP.MailAssembleQueriesSameRecipient)
                    {
                        rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.ALL_IN_ONE;
                    }

                    if (!bCSplittedInParts)
                    {
                        string sMerge = sListQueries[0].OutputTable;
                        bool bMerge = true;
                        foreach (Query sQ in sListQueries) { if (!sQ.OutputTable.Equals(sMerge)) { bMerge = false; break; } }
                        if (bMerge) { rsInsertMethod = SQLTools_Enums.REPSYNC_INSERT_METHOD.MERGE; }
                    }
                }

                //exécution des insert en multithread
                MThread mtClass = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLogSingleQ, sListQueries.Count, 0);

                if (mtClass.QuantityOfThreadsToCompute > 0)
                {
                    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                    {
                        int numeroThread = numThread;
                        System.Threading.Tasks.Task th = new(() => mtClass.RepSyncTask(numeroThread, sListQueries, rsInsertMethod, false));
                        Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                        th.Start();
                    }

                    while (!mtClass.AreAllThreadsFinished)
                    {

                        //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(100);
                    }
                    if (mtClass.HasOperationBeenCancelled)
                    {
                        throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")");
                    }
                }
                else
                {
                    MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_singlequerymultithreaderror, SQLTools_Enums.LOG_TYPEINFO.ERR);
                }

                //StringBuilder sbLog = new StringBuilder();
                //foreach (LogObject LO in MyLogSingleQ.LogEvents)
                //{ sStatus.Add(LO);  }
            }
            else
            {
                MyLogSingleQ.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_singlequerynoquery, SQLTools_Enums.LOG_TYPEINFO.ERR);
            }

            Monitoring.StartStopProgram(MyLogSingleQ);

            INIP.IsRunning = false;
        }

        public static List<string> ExecutePrePostJobOperations(Job JobRun, LogTools MyLogProgram, bool bPreJob, List<string> sListDynParams, List<Query> sListQueries)
        {
            CONNString CSSource = JobRun.PrePostJob_CommandSourceConnection.Length > 0 ?
                JobRun.GlobalParameters.Connections.GetConnByID(JobRun.PrePostJob_CommandSourceConnection) :
                JobRun.ConnectionString_Source;
            CONNString CSTarget = JobRun.PrePostJob_CommandTargetConnection.Length > 0 ?
                JobRun.GlobalParameters.Connections.GetConnByID(JobRun.PrePostJob_CommandTargetConnection) :
                JobRun.ConnectionString_Target;

            Job JobCommand = JobRun.DeepCopy();
            JobCommand.ConnectionString_Source = CSSource;
            JobCommand.ConnectionString_Target = CSTarget;

            List<string> sListFinalParams = new();

            SQLTools_Enums.PRE_POST_JOB_COMMANDS ppPrePost = bPreJob ? SQLTools_Enums.PRE_POST_JOB_COMMANDS.PRE_JOB_COMMANDS : SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS;

            //pour les API, j'ai positionné des URl de post-commandes dans les Query Properties
            if (ppPrePost == SQLTools_Enums.PRE_POST_JOB_COMMANDS.POST_JOB_COMMANDS && CSSource.SConnDriver.ToString()[..2] == "WS" && JobCommand.WebserviceSourcePostWork != "NOTHING")
            {
                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost04, SQLTools_Enums.LOG_TYPEINFO.INF);

                foreach (Query Q in sListQueries)
                {
                    List<Query.QProperty> qProp = null;
                    try { qProp = Q.QueryAnalyzer.QueryProperties.Where(qP => qP.Property == SQLTools_Enums.QUERY_PROPERTIES.WS_SOURCE_POSTWORK).ToList(); } catch { }
                    if (qProp != null && qProp.Count > 0)
                    {
                        foreach (Query.QProperty q in qProp)
                        {
                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executeprepost01 + "[" + JobCommand.WebserviceSourcePostWork + "] - " + q.Element + " -> " + q.Value, SQLTools_Enums.LOG_TYPEINFO.INF);

                            var WSPostWork = new WSTools(JobCommand, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram); //exécution sur la source
                            string sData = WSPostWork.ExecutePostWorkOperation(q, new Query());
                            MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executepreport_resultfromws + sData, SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                    }
                }
            }

            if (JobCommand.PrePostJob_CommandSource.Length > 0 && JobCommand.PreOrPostCommand_Source == ppPrePost)
            {
                foreach (string sP in sListDynParams) { sListFinalParams.Add(sP); }

                if (CSSource.SConnDriver.ToString()[..2] == "DB")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost04, SQLTools_Enums.LOG_TYPEINFO.INF);

                    string[] sListPrePostCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandSource.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                    SQLTools SQLSource = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);

                    int iC = 0;
                    foreach (string sCommand in sListPrePostCommands)
                    {
                        iC++;
                        if (!JobCommand.RunInSimulationMode)
                        {
                            SQLTools_Enums.TYPE_REQUETE scType = SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND;
                            string sC = string.Concat(sCommand.Trim(), ";");
                            if (sC.StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT; }
                            else if (sC.StartsWith("UPDATE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE; }
                            else if (sC.StartsWith("DELETE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE; }
                            else if (sC.StartsWith("DROP", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_DROP; }
                            else if (sC.StartsWith("CREATE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_CREATE; }
                            else if (sC.StartsWith("CALL", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE; }
                            else if (sC.StartsWith("EXEC", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE; }
                            DataSet dsReturn = null;
                            if (scType == SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE)
                            {
                                dsReturn = SQLSource.ExecStoredProcedure(sC, new Query(), null, null);
                            }
                            else
                            {
                                dsReturn = SQLSource.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), scType, sC, new Query(), ppPrePost.ToString());
                            }

                            //produit une liste des paramètres dynamiques avec leur valeur définitive, que ce soit une liste ou une seule valeur
                            List<Tuple<string, string>> sValues = Toolbox.CreateRelationBetweenDataAndDynParam(dsReturn, !JobCommand.DynParams_LoopThroughRows, true, iC, sListFinalParams, ",", "'");
                            //on peut avoir par exemple
                            //%CS1 = '1','2','3','4','5'
                            //%CT1[1] = 1{;}2{;}3 

                            //MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_sqlval + string.Join(";", sValues), SQLTools_Enums.LOG_TYPEINFO.INF);

                            if (sValues.Count > 0)
                            {
                                for (int iP = 0; iP < sListFinalParams.Count; iP++)
                                {
                                    Tuple<string, string> tData = sValues.FirstOrDefault(d => d.Item1.Equals(sListDynParams[iP]));
                                    if (tData != null)
                                    {
                                        //remplacement du paramètre invoqué par les valeurs (Connection Source + numéro de la commande)
                                        MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.par_executepreport_replacejobparam01, sListFinalParams[iP], Languages.Languages.par_executepreport_replacejobparam02, tData.Item2.Length > 50 ? (tData.Item2[..50] + "...") : tData.Item2), SQLTools_Enums.LOG_TYPEINFO.DET);
                                        sListFinalParams[iP] = tData.Item2;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_sqlsource, sCommand));
                        }
                    }
                }
                if (CSSource.SConnDriver.ToString()[..2] == "FI")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost04, SQLTools_Enums.LOG_TYPEINFO.INF);

                    FITools FILESource = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);

                    int iC = 0;
                    string[] sCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandSource.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sCommand in sCommands)
                    {
                        if (!JobCommand.RunInSimulationMode)
                        {
                            //TODO : ne gère pas les multi-commandes séparées par ;
                            iC++;
                            string sValue = FILESource.ExecuteProcess(sCommand, false);
                            string[] sValues = sValue.Split("|", StringSplitOptions.RemoveEmptyEntries);
                            if (sValues.Length > 0)
                            {
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_fileval + sValues.Last(), SQLTools_Enums.LOG_TYPEINFO.INF);
                            }

                            for (int iP = 0; iP < sListFinalParams.Count; iP++)
                            {
                                if (sListFinalParams[iP].Equals(string.Concat("%CS", iC.ToString())))
                                {
                                    sListFinalParams[iP] = sValue;
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_filesource, sCommand.Replace("\\r", " ").Replace("\\n", " ")));
                        }
                    }
                }
                if (CSSource.SConnDriver.ToString()[..2] == "NS")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost04, SQLTools_Enums.LOG_TYPEINFO.INF);

                    NOSQLTools NOSQL = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLogProgram);

                    int iC = 0;
                    string[] sCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandSource.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sCommand in sCommands)
                    {
                        if (!JobCommand.RunInSimulationMode)
                        {
                            iC++;
                            DataSet dsReturn = NOSQL.ExecuteCommand(sCommand);
                            //produit une liste des paramètres dynamiques avec leur valeur définitive, que ce soit une liste ou une seule valeur
                            List<Tuple<string, string>> sValues = Toolbox.CreateRelationBetweenDataAndDynParam(dsReturn, !JobCommand.DynParams_LoopThroughRows, true, iC, sListFinalParams, ",", "'");
                            //on peut avoir par exemple
                            //%CS1 = '1','2','3','4','5'
                            //%CT1[1] = 1{;}2{;}3 

                            //MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_sqlval + string.Join(";", sValues), SQLTools_Enums.LOG_TYPEINFO.INF);

                            if (sValues.Count > 0)
                            {
                                for (int iP = 0; iP < sListFinalParams.Count; iP++)
                                {
                                    Tuple<string, string> tData = sValues.FirstOrDefault(d => d.Item1.Equals(sListDynParams[iP]));
                                    if (tData != null)
                                    {
                                        //remplacement du paramètre invoqué par les valeurs (Connection Source + numéro de la commande)
                                        MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.par_executepreport_replacejobparam01, sListFinalParams[iP], Languages.Languages.par_executepreport_replacejobparam02, tData.Item2.Length > 50 ? (tData.Item2[..50] + "...") : tData.Item2), SQLTools_Enums.LOG_TYPEINFO.DET);
                                        sListFinalParams[iP] = tData.Item2;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_mongosource, JobCommand.PrePostJob_CommandSource.Replace("\\r", " ").Replace("\\n", " ")));
                        }
                    }
                }
            }

            if (JobCommand.PrePostJob_CommandTarget.Length > 0 && JobCommand.PreOrPostCommand_Target == ppPrePost)
            {
                foreach (string sP in sListDynParams) { sListFinalParams.Add(sP); }

                if (CSTarget.SConnDriver.ToString()[..2] == "DB")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost05, SQLTools_Enums.LOG_TYPEINFO.INF);
                    string[] sListPrePostCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandTarget.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                    SQLTools SQLTarget = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLogProgram);

                    int iC = 0;
                    foreach (string sCommand in sListPrePostCommands)
                    {
                        iC++;
                        if (!JobCommand.RunInSimulationMode)
                        {
                            SQLTools_Enums.TYPE_REQUETE scType = SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND;
                            string sC = string.Concat(sCommand.Trim(), ";");
                            if (sC.StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT; }
                            else if (sC.StartsWith("UPDATE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE; }
                            else if (sC.StartsWith("DELETE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE; }
                            else if (sC.StartsWith("DROP", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_DROP; }
                            else if (sC.StartsWith("CREATE", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.MAKE_CREATE; }
                            else if (sC.StartsWith("CALL", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE; }
                            else if (sC.StartsWith("EXEC", StringComparison.InvariantCultureIgnoreCase)) { scType = SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE; }
                            DataSet dsReturn = null;
                            if (scType == SQLTools_Enums.TYPE_REQUETE.STORED_PROCEDURE)
                            {
                                dsReturn = SQLTarget.ExecStoredProcedure(sC, new Query(), null, null);
                            }
                            else
                            {
                                dsReturn = SQLTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), scType, sC, new Query(), ppPrePost.ToString());
                            }
                            List<Tuple<string, string>> sValues = Toolbox.CreateRelationBetweenDataAndDynParam(dsReturn, !JobCommand.DynParams_LoopThroughRows, false, iC, sListFinalParams, ",", "'");

                            //MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_sqlval + string.Join(";", sValues), SQLTools_Enums.LOG_TYPEINFO.INF);

                            if (sValues.Count > 0)
                            {
                                for (int iP = 0; iP < sListFinalParams.Count; iP++)
                                {
                                    Tuple<string, string> tData = sValues.FirstOrDefault(d => d.Item1.Equals(sListDynParams[iP]));
                                    if (tData != null)
                                    {
                                        //remplacement du paramètre invoqué par les valeurs (Connection Source + numéro de la commande)
                                        sListFinalParams[iP] = tData.Item2;
                                        MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.par_executepreport_replacejobparam01, sListFinalParams[iP], Languages.Languages.par_executepreport_replacejobparam02, tData.Item2.Length > 50 ? (tData.Item2[..50] + "...") : tData.Item2), SQLTools_Enums.LOG_TYPEINFO.INF);
                                    }
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_sqltarget, sCommand));
                        }
                    }
                }
                if (CSTarget.SConnDriver.ToString()[..2] == "FI")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost05, SQLTools_Enums.LOG_TYPEINFO.INF);

                    FITools FILETarget = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLogProgram);

                    int iC = 0;

                    string[] sCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandTarget.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sCommand in sCommands)
                    {
                        if (!JobCommand.RunInSimulationMode)
                        {
                            iC++;
                            string sValue = FILETarget.ExecuteProcess(sCommand, false);
                            string[] sValues = sValue.Split("|", StringSplitOptions.RemoveEmptyEntries);

                            if (sValues.Length > 0)
                            {
                                MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_fileval + sValues.Last(), SQLTools_Enums.LOG_TYPEINFO.INF);
                            }

                            for (int iP = 0; iP < sListFinalParams.Count; iP++)
                            {
                                if (sListFinalParams[iP].Equals(string.Concat("%CT", iC.ToString())))
                                {
                                    sListFinalParams[iP] = sValue;
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_filetarget, sCommand.Replace("\\r", " ").Replace("\\n", " ")));
                        }
                    }
                }
                if (CSTarget.SConnDriver.ToString()[..2] == "NS")
                {
                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.par_executeprepost01 + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executeprepost05, SQLTools_Enums.LOG_TYPEINFO.INF);

                    NOSQLTools NOSQL = new(JobCommand, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLogProgram);

                    int iC = 0;

                    string[] sCommands = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobCommand.PrePostJob_CommandSource.Replace("\\r", " ").Replace("\\n", " "), JobCommand.DynParams).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sCommand in sCommands)
                    {
                        if (!JobCommand.RunInSimulationMode)
                        {
                            iC++;
                            DataSet dsReturn = NOSQL.ExecuteCommand(sCommand);
                            //produit une liste des paramètres dynamiques avec leur valeur définitive, que ce soit une liste ou une seule valeur
                            List<Tuple<string, string>> sValues = Toolbox.CreateRelationBetweenDataAndDynParam(dsReturn, !JobCommand.DynParams_LoopThroughRows, true, iC, sListFinalParams, ",", "'");
                            //on peut avoir par exemple
                            //%CS1 = '1','2','3','4','5'
                            //%CT1[1] = 1{;}2{;}3 

                            //MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_executeprepost_sqlval + string.Join(";", sValues), SQLTools_Enums.LOG_TYPEINFO.INF);

                            if (sValues.Count > 0)
                            {
                                for (int iP = 0; iP < sListFinalParams.Count; iP++)
                                {
                                    Tuple<string, string> tData = sValues.FirstOrDefault(d => d.Item1.Equals(sListDynParams[iP]));
                                    if (tData != null)
                                    {
                                        //remplacement du paramètre invoqué par les valeurs (Connection Source + numéro de la commande)
                                        MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.par_executepreport_replacejobparam01, sListFinalParams[iP], Languages.Languages.par_executepreport_replacejobparam02, tData.Item2.Length > 50 ? (tData.Item2[..50] + "...") : tData.Item2), SQLTools_Enums.LOG_TYPEINFO.DET);
                                        sListFinalParams[iP] = tData.Item2;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MyLogProgram.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.par_executepreport_simuwillexec + (bPreJob ? Languages.Languages.par_executepre : Languages.Languages.par_executepost) + Languages.Languages.par_executepreport_mongotarget, sCommand.Replace("\\r", " ").Replace("\\n", " ")));
                        }
                    }
                }
            }
            return sListFinalParams;
        }

        public bool CheckExistingJobWithSourceDriver(SQLTools_Enums.BDD sConnDriver)
        {
            try
            {
                //DB_STATE = dr["last_state"].ToString();
                DataSet dsData = new();
                string sQuery = string.Concat("select COUNT(*) as qTeJobs ",
                                                "from job_parameters jp ",
                                                "inner join user_connstrings cs ON jp.connexion_string_export = cs.connstring_id AND jp.user = cs.user ",
                                                "where cs.user = '" + USER + "' ",
                                                "and substr(cs.connstring_driver, 1, 2) = substr('" + sConnDriver.ToString() + "', 1, 2) " +
                                                "and jp.job_id NOT IN ('[1]','[2]');");
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "DBState");
                }
                string sR = SQLTools.BuildStringFromDs(dsData);

                //return true;
                if (sR.Equals("0")) { return false; } else { return true; }
            }
            catch { return false; }
        }

        public bool RemoveFamily(string sFamily)
        {
            StringBuilder sbQuery = new StringBuilder();
            sbQuery.Append("UPDATE job_parameters SET job_family = 'DEFAULT' ");
            sbQuery.Append(" WHERE \"user\" = '" + USER + "' AND \"job_family\" = '" + sFamily + "';");

            try
            {
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                return true;
            }
            catch
            {
                return false;
            }

        }

        public bool SaveJob(Job INIP)
        {
            bool bOK = true;

            INIP.JobModificationDate = DateTime.Now;

            if (INIP.Job_IsSubJob)
            {
                INIP.SetJobPassword(GetJob(INIP.ParentJobID).JobPassword, "", true);
            }

            try
            {
                //test existence job
                bool bCreate = true;
                DataSet dsData = new();
                string sQuery = string.Concat("SELECT COUNT(*) FROM job_parameters WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + INIP.JobID + "';");
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Queries");
                }
                string sExists = SQLTools.BuildStringFromDs(dsData);
                if (sExists.Equals("1")) { bCreate = false; }

                StringBuilder sbQuery = new();


                if (bCreate)
                {
                    sbQuery.Append("INSERT INTO job_parameters (\"user\",\"job_id\",\"job_name\",\"job_family\",\"job_password\",\"job_priority\",\"job_description\",\"job_creation_date\",\"job_modification_date\",\"job_lastlaunch_date\",\"job_lastlaunch_status\",\"job_visibility\",\"subjob_abort_on_errors\",\"postcommands_bypass_on_errors\",\"turbo_mode\",\"source_queries_dynamic_parameters\",\"synchro_target_table_behavior\",\"synchro_bypass_query_filters_in_target\",\"synchro_store_changes\",\"connexion_string_export\",\"connexion_string_import\",\"database_source\",\"database_target\",\"export_import\",\"export_use_dataset\",\"in_production\",\"log_level\",\"sql_log\",\"threads_export\",\"threads_import\",\"trim_data\",\"drop_table_before_insert\",\"check_fields_before_insert\",\"field_analyser_level\",\"use_null_import\",\"convert_html_pattern_for_target\",\"allow_alter_column_target\",\"disable_constraints_target\",\"sql_target_bulk_copy\",\"create_sql_primary_key_in_target\",\"import_file_cleanup\",\"log_mail_adress\",\"hyperfile_arrayfield_transform\",\"hyperfile_arrayfield_transform_separator\",\"csv_dont_transform_with_invalid_header\",\"data_transform_rows_columns_add_label_values\", \"data_transform_cross_queries\", \"source_files_zipped_in\",\"append_file_creation\",\"csv_char_separator\",\"csv_encoding_target\",\"csv_multiple_files_in_one_pattern\",\"csv_separator_end_row\",\"csv_target_add_header\",\"csv_row_offset\",\"xls_sheet_to_read\",\"xls_row_offset\",\"xls_password_source\",\"xls_password_target\",\"xml_header\",\"json_header\",\"xml_cdata_tag\",\"xml_remove_tag_for_empty_values\",\"max_rows_in_file\",\"webservice_nuxeo_api_endpoint\",\"webservice_call_method\",\"webservice_call_method_target\",\"webservice_content_type_target\",\"webservice_dont_send_empty_values\", \"webservice_request_body_type\", \"sql_shrink_tables\",\"sql_add_dbname_column\",\"sql_add_rownum_column\",\"sql_add_dynamicparam_column\",\"sql_add_timestamp_column\", \"target_add_dbname\", \"target_add_dtload\", \"target_add_rownum\", \"post_job_sql_command_source\",\"post_job_sql_command_target\",\"pre_or_post_job_commands_source\",\"pre_or_post_job_commands_target\",\"ws_http_format_url_in_upper\",\"ws_http_send_columns_offset\",\"ws_save_webresponse_file\",\"ws_tracking_column_webresponse\",\"ws_logtable_webresponses\",\"ws_success_string\",\"mb_get_unread_only\",\"mb_flag_retrieved_as_read\",\"mb_assemble_queries_same_recipient\", \"mb_max_mails_to_get\", \"mb_target_html_file_template\", \"mb_target_html_dataset_keyword\", \"ad_search_scope\", \"remove_id_from_mongodb_query\", \"create_pk_for_mongo_collection\",\"target_mongo_collection_behavior\", \"xml_target_row_builder\", \"json_target_row_builder\", \"mail_target_format\", \"xls_target_add_header\", \"delete_source_data_after_insert\", \"prepost_commands_loopjobthroughresults\", \"ad_target_search_property\", \"ad_target_behavior\", \"ad_activate_new_entries\", \"xls_target_with_title\", \"xls_target_style\", \"xml_write_mode\", \"auto_sql_table_creation\", \"csv_add_quotes\", \"ws_raw_output\", \"file_raw_output\", \"ws_sql_language\", \"sql_directstream_copy\", \"xls_row_write_offset\", \"ws_content_structure\", \"xls_write_interpret_formulas\", \"api_get_typedata\", \"api_source_postwork\", \"subjob_abort_on_nodata\", \"post_job_sql_command_source_conn\", \"post_job_sql_command_target_conn\", \"webservice_special_http_parameters\", \"no_sourcedata_noerror\", \"allow_alter_column_target_onlysafe\", \"sql_directstream_priority\", \"sql_trust_target_columns\", \"query_retries\", \"job_version\") VALUES ('" + USER + "','" + INIP.JobID + "',");
                }
                else
                {
                    sbQuery.Append("UPDATE job_parameters SET ");
                }
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_name\"=", "'", INIP.JobNAME.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_family\"=", "'", INIP.JobFamily.Name.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_password\"=", "'", INIP.JobPassword.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_priority\"=", "'", INIP.JobPriority.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_description\"=", "'", INIP.JobDescription.Trim().Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_creation_date\"=", "'", INIP.JobCreationDate.ToString(CultureInfo.CurrentCulture), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_modification_date\"=", "'", INIP.JobModificationDate.ToString(CultureInfo.CurrentCulture), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_lastlaunch_date\"=", "'", INIP.JobLastLaunchDate.ToString(CultureInfo.CurrentCulture), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_lastlaunch_status\"=", "'", INIP.JobLastLaunchStatus.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_visibility\"=", "'", INIP.IsJobVisibleInClientApp ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"subjob_abort_on_errors\"=", "'", INIP.AbortSubJobExecutionIfErrors ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"postcommands_bypass_on_errors\"=", "'", INIP.BypassPostJobExecutionIfErrors ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"turbo_mode\"=", "'", INIP.TurboMode ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"source_queries_dynamic_parameters\"=", "'", INIP.DynParams.Count > 0 ? string.Join(";", INIP.DynParams.ToArray()).Replace("'", "''") : "", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"synchro_target_table_behavior\"=", "'", INIP.SynchroTargetTableBehavior.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"synchro_bypass_query_filters_in_target\"=", "'", INIP.SynchroBypassQueryFiltersInTarget ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"synchro_store_changes\"=", "'", INIP.SynchroStoreChanges ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"connexion_string_export\"=", "'", INIP.ConnectionString_Source, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"connexion_string_import\"=", "'", INIP.ConnectionString_Target, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"database_source\"=", "'", INIP.DatabaseName_Source.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"database_target\"=", "'", INIP.DatabaseName_Target.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"export_import\"=", "'", INIP.JobMethod.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"export_use_dataset\"=", "'", INIP.ExportUseDataset ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"in_production\"=", "'", "1", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"log_level\"=", "'", INIP.LogLevel.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_log\"=", "'", INIP.HasSqlLog ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"threads_export\"=", "'", INIP.Threads_Source.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"threads_import\"=", "'", INIP.Threads_Target.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"trim_data\"=", "'", INIP.TrimData ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"drop_table_before_insert\"=", "'", INIP.TargetTableBehavior.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"check_fields_before_insert\"=", "'", INIP.CheckFieldsBeforeInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"field_analyser_level\"=", "'", INIP.FieldAnalyzerLevel.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"use_null_import\"=", "'", INIP.UseNull_Target ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"convert_html_pattern_for_target\"=", "'", INIP.ConvertHTMLPatternsForTarget ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"allow_alter_column_target\"=", "'", INIP.AlterColumnTypeOnInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"disable_constraints_target\"=", "'", INIP.SQLTargetDisableConstraints ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_target_bulk_copy\"=", "'", INIP.SQLTargetBulkCopy ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"create_sql_primary_key_in_target\"=", "'", INIP.CreatePrimaryKeyAfterHavingCreatedATable ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"import_file_cleanup\"=", "'", INIP.FileCleanup_Import.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"log_mail_adress\"=", "'", INIP.LogMailAdress.Count == 0 ? "" : string.Join(";", INIP.LogMailAdress.ToArray()), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"hyperfile_arrayfield_transform\"=", "'", INIP.HyperFileArrayFieldTransformation.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"hyperfile_arrayfield_transform_separator\"=", "'", INIP.DataTransformSeparatorOrLabel.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_dont_transform_with_invalid_header\"=", "'", INIP.DataTransformDontTransformIfVariableArraySizes ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"data_transform_rows_columns_add_label_values\"=", "'", INIP.DataTransformRowsToColumnsAddLabelToValues ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"data_transform_cross_queries\"=", "'", INIP.DataTransformAlsoCrossQueries ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"source_files_zipped_in\"=", "'", INIP.FileSourceZippedIn.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"append_file_creation\"=", "'", INIP.AppendFileCreation ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_char_separator\"=", "'", INIP.CSVCharSeparator_Target, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_encoding_target\"=", "'", INIP.CSVEncoding_Target, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_multiple_files_in_one_pattern\"=", "'", INIP.CSVMultipleFilesInOnePattern.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_separator_end_row\"=", "'", INIP.CSVCharSeparator_EndRow ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_target_add_header\"=", "'", INIP.CSVAddHeader ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_row_offset\"=", "'", INIP.CSVRowOffset, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_sheet_to_read\"=", "'", INIP.XLSSheetToRead, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_row_offset\"=", "'", INIP.XLSRowOffset.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_password_source\"=", "'", FITools.EncryptionSystem.AES_Encrypt(INIP.XLSPasswordSource.ToString(), SHSConstantes.PROGRAM_PWD), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_password_target\"=", "'", FITools.EncryptionSystem.AES_Encrypt(INIP.XLSPasswordTarget.ToString(), SHSConstantes.PROGRAM_PWD), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xml_header\"=", "'", INIP.XMLHeader.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"json_header\"=", "'", INIP.JSONHeader.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xml_cdata_tag\"=", "'", INIP.XMLAddCDataTag ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xml_remove_tag_for_empty_values\"=", "'", INIP.XMLRemoveTagForEmptyValues ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"max_rows_in_file\"=", "'", INIP.MaxRowsInAFile.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_nuxeo_api_endpoint\"=", "'", INIP.WebserviceNuxeo_EndpointSource.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_call_method\"=", "'", INIP.WebServiceCallMethod.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_call_method_target\"=", "'", INIP.WebServiceCallMethod_Target.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_content_type_target\"=", "'", INIP.WebServiceContentType_Target.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_dont_send_empty_values\"=", "'", INIP.WebserviceHTTP_DontSendEmptyValues ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_request_body_type\"=", "'", INIP.WebServiceRequestBodyType.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_shrink_tables\"=", "'", INIP.ShrinkSQLTables ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_add_dbname_column\"=", "'", INIP.OptionalDBName_OnInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_add_rownum_column\"=", "'", INIP.OptionalRowID_OnInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_add_dynamicparam_column\"=", "'", INIP.OptionalDynamicParamField_OnInsert.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_add_timestamp_column\"=", "'", INIP.OptionalTimestamp_OnInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"target_add_dbname\"=", "'", INIP.TargetAddDbName.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"target_add_dtload\"=", "'", INIP.TargetAddDtLoad.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"target_add_rownum\"=", "'", INIP.TargetAddRowNum.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"post_job_sql_command_source\"=", "'", INIP.PrePostJob_CommandSource.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"post_job_sql_command_target\"=", "'", INIP.PrePostJob_CommandTarget.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"pre_or_post_job_commands_source\"=", "'", INIP.PreOrPostCommand_Source, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"pre_or_post_job_commands_target\"=", "'", INIP.PreOrPostCommand_Target, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_http_format_url_in_upper\"=", "'", INIP.WebserviceHTTP_FormatURLInUpper ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_http_send_columns_offset\"=", "'", INIP.WebserviceHTTP_SendColumnsOffset.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_save_webresponse_file\"=", "'", INIP.WebServiceSaveResponseFile ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_tracking_column_webresponse\"=", "'", INIP.WebServiceTrackingColumnInResponses.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_logtable_webresponses\"=", "'", INIP.WebserviceLogTableResponses.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_success_string\"=", "'", INIP.WebServiceSuccessString.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_get_unread_only\"=", "'", INIP.MailGetUnreadOnly ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_flag_retrieved_as_read\"=", "'", INIP.MailFlagRetrievedAsRead.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_assemble_queries_same_recipient\"=", "'", INIP.MailAssembleQueriesSameRecipient ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_max_mails_to_get\"=", "'", INIP.MaxMailsToGet.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_target_html_file_template\"=", "'", INIP.MailTargetHTMLFileTemplate.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mb_target_html_dataset_keyword\"=", "'", INIP.MailTargetHTMLDataSetKeyword.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ad_search_scope\"=", "'", INIP.ADSearchScope.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"remove_id_from_mongodb_query\" = ", "'", INIP.RemoveIDFromMongoDBQuery ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"create_pk_for_mongo_collection\" = ", "'", INIP.CreatePKForMongoCollection ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"target_mongo_collection_behavior\" = ", "'", INIP.TargetMongoCollectionBehavior.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xml_target_row_builder\"=", "'", INIP.XMLTargetRowBuilder, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"json_target_row_builder\"=", "'", INIP.JSONTargetRowBuilder, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mail_target_format\"=", "'", INIP.MailTargetFormat.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_target_add_header\"=", "'", INIP.XLSAddHeader ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"delete_source_data_after_insert\"=", "'", INIP.SourceTableDeleteAfterInsert ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"prepost_commands_loopjobthroughresults\"=", "'", INIP.DynParams_LoopThroughRows_Source && INIP.DynParams_LoopThroughRows_Target ? "3" : INIP.DynParams_LoopThroughRows_Source ? "1" : INIP.DynParams_LoopThroughRows_Target ? "2" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ad_target_search_property\"=", "'", INIP.ADSearchProperty, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ad_target_behavior\"=", "'", INIP.ADTargetBehavior.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ad_activate_new_entries\"=", "'", INIP.ADActivateEntry ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_target_with_title\"=", "'", INIP.XLSWithTitle ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_target_style\"=", "'", INIP.XLSStyle, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xml_write_mode\"=", "'", INIP.XMLWriteMode.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"auto_sql_table_creation\"=", "'", INIP.AutoSQLTableCreation ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_add_quotes\"=", "'", INIP.CSVAddQuotes ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_raw_output\"=", "'", INIP.WebserviceRawOutput ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_raw_output\"=", "'", INIP.FileRawOutput ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_sql_language\"=", "'", INIP.WebserviceSQLLanguage.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_directstream_copy\"=", "'", INIP.SQLDirectStream ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_row_write_offset\"=", "'", INIP.XLSRowWriteOffset.ToString(), "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_content_structure\"=", "'", INIP.WebserviceContentStructure.ToString().Replace("'", "''"), "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"xls_write_interpret_formulas\"=", "'", INIP.XLSInterpretFormulas ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"api_get_typedata\"=", "'", INIP.WebserviceTypeData.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"api_source_postwork\"=", "'", INIP.WebserviceSourcePostWork.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"subjob_abort_on_nodata\"=", "'", INIP.AbortSubJobExecutionIfNoData ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"post_job_sql_command_source_conn\"=", "'", INIP.PrePostJob_CommandSourceConnection, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"post_job_sql_command_target_conn\"=", "'", INIP.PrePostJob_CommandTargetConnection, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"webservice_special_http_parameters\"=", "'", INIP.WebserviceSpecialHttpParameters, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"no_sourcedata_noerror\"=", "'", INIP.NoSourceDataNoError, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"allow_alter_column_target_onlysafe\"=", "'", INIP.AlterColumnTypeOptions, "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_directstream_priority\"=", "'", INIP.SQLDirectStreamPriority.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_trust_target_columns\"=", "'", INIP.SQLTrustTargetColumnType ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"query_retries\"=", "'", INIP.QueryRetries.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"job_version\"=", "'", INIP.JobVersionWithUser, "'"));

                if (bCreate) { sbQuery.Append(");"); }
                else { sbQuery.Append(" WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + INIP.JobID + "';"); }

                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }

                dsData.Clear();
                dsData = new DataSet();
                sbQuery.Clear();
                sbQuery.Append(string.Concat("SELECT * FROM job_parameters WHERE \"user\" = '", USER, "' AND \"job_id\" = '" + INIP.JobID + "';"));

                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Jobs Parameters");
                }

                //rafraichissement du Datarow (utile si on exporte le job en XML juste après. Unique usage
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count == 1)
                {
                    INIP.JobAsDataRow = dsData.Tables[0].Rows[0];
                }

                if (bCreate)
                {
                    _jJobsList.Add(INIP);
                }
                else
                {
                    int iIdx = -1;
                    for (int iJ = 0; iJ < _jJobsList.Count; iJ++)
                    {
                        if (_jJobsList[iJ].JobID.Equals(INIP.JobID)) { iIdx = iJ; break; }
                    }
                    if (iIdx > -1) { _jJobsList[iIdx] = INIP; }
                }

                if (Directory.Exists(PATH_JOBS_XML))
                {
                    string sJobDataAsXML = INIP.ExportJobAsXML(INIP.USER, INIP.JobNAME, false, false);
                    File.WriteAllText(Path.Combine(PATH_JOBS_XML, string.Concat("JOB_", INIP.USER, "_", INIP.JobID, ".XML")), sJobDataAsXML, Encoding.UTF8);
                }

            }
            catch (Exception)
            {
                throw;
            }

            return bOK;
        }

        public void SaveJobQueries(Job INIP, List<Query> sQueries)
        {
            List<string> sRawQueries = new();
            foreach (Query q in sQueries)
            {
                sRawQueries.Add(q.RawQuery); //.Replace("'", "''") remplacement suite chiffrage
            }

            try
            {
                DataSet dsData = new();

                StringBuilder sbQuery = new();

                using SqliteConnection sqlConn = new(INTERNAL_DB);
                sqlConn.Open();

                using SqliteCommand sqlCommand = new("DELETE FROM job_queries WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + INIP.JobID + "';", sqlConn);
                sqlCommand.ExecuteNonQuery();
                sqlConn.Close();

                for (int i = 0; i < sRawQueries.Count; i++)
                {
                    sbQuery.Append("INSERT INTO job_queries (\"user\", \"job_id\", \"query\") VALUES (");
                    sbQuery.Append(string.Concat("'", USER, "',"));
                    sbQuery.Append(string.Concat("'", INIP.JobID, "',"));
                    sbQuery.Append(string.Concat("'", FITools.EncryptionSystem.AES_Encrypt(sRawQueries[i], SHSConstantes.PROGRAM_PWD), "');"));

                    if ((i + 1) % 16 == 0 || i == sRawQueries.Count - 1)
                    {
                        using SqliteCommand sqlCommandB = new(sbQuery.ToString(), sqlConn);
                        sqlConn.Open();
                        sqlCommandB.ExecuteNonQuery();
                        sqlConn.Close();
                        sbQuery.Clear();
                    }
                }
            }
            catch (Exception) { throw; }

            string sCompare1 = string.Join(Environment.NewLine, sRawQueries).Trim();
            string sCompare2 = INIP.GetQueriesAsString().Trim();

            if (!sCompare1.Equals(sCompare2))
            {
                INIP.IncrementVersion(false, true);
                SaveVersion(INIP);
            }

            if (Directory.Exists(PATH_JOBS_XML))
            {
                INIP.JobQueries = sQueries;
                string sJobDataAsXML = INIP.ExportJobAsXML(INIP.USER, INIP.JobNAME, false, false);
                File.WriteAllText(Path.Combine(PATH_JOBS_XML, string.Concat("JOB_", INIP.USER, "_", INIP.JobID, ".XML")), sJobDataAsXML, Encoding.UTF8);
            }

            INIP.LoadJobQueries(true);
        }

        public string GetJobVersion(string sUser, string sJobID)
        {
            try
            {
                //DB_STATE = dr["last_state"].ToString();
                DataSet dsData = new();
                string sQuery = string.Concat("select \"job_version\" FROM \"job_parameters\" WHERE \"user\" = '", USER, "' AND \"job_id\" = '", sJobID, "';");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "JobVersion");
                }
                return SQLTools.BuildStringFromDs(dsData, 0, 0, 0);
            }
            catch { return ""; }
        }

        public List<Job> GetChildrenCount(int iRawJobID)
        {
            List<Job> sJobs = new();
            foreach (Job J in _jJobsList)
            {
                if (J.JobID.StartsWith(string.Concat("[", iRawJobID.ToString(), "-")))
                {
                    sJobs.Add(J);
                }
            }
            return sJobs.OrderBy(j => j.SubJobID).ToList();
        }

        public string GetNewJobID()
        {
            //rechercher d'abord un trou dans les séquences
            var iJobs = _jJobsList.Select(j => j.RawJobID).Distinct().ToList();
            iJobs.Sort();

            for (int iJob = 1; iJob < iJobs.Count; iJob++)
            {
                if (iJob != iJobs[iJob - 1])
                {
                    return string.Concat("[", iJob.ToString(), "]");
                }
            }

            int iMaxID = _jJobsList.Count > 0 ? _jJobsList.Max(INIP => INIP.RawJobID) : 0;
            return string.Concat("[", (iMaxID + 1).ToString(), "]");
        }

        public void ChangeJobPassword(string sJobID, string sNewPassword)
        {
            //arrive décrypté
            string sEncryptedPwd = FITools.EncryptionSystem.AES_Encrypt(sNewPassword, sJobID[1..sJobID.IndexOf("]")]);

            string sQuery = "UPDATE job_parameters SET \"job_password\" = '" + sEncryptedPwd + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobID + "';";
            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
            _jJobsList.First(j => j.JobID.Equals(sJobID)).SetJobPassword(sEncryptedPwd, sJobID, true);
        }

        public bool RenameJob(string sJobID, string sNewName)
        {
            string sQuery = "UPDATE job_parameters SET \"job_name\" = '" + sNewName.Replace("'", "''") + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobID + "';";
            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
            _jJobsList.First(j => j.JobID.Equals(sJobID)).JobNAME = sNewName;

            return true;
        }

        public bool CheckExistingJobName(string sNewJobName)
        {
            bool bExists;
            DataSet dsData = new();

            try
            {
                //check si nom existe déjà
                string sQueryCount = "SELECT COUNT(*) FROM job_parameters WHERE \"user\" = '" + USER + "' AND \"job_name\" = '" + sNewJobName.Replace("'", "''") + "';";
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQueryCount, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Existing Name");
                }

                string sCount = SQLTools.BuildStringFromDs(dsData);
                if (sCount.Equals("0"))
                {
                    bExists = false;
                }
                else { bExists = true; }
            }
            catch (Exception) { throw; }

            return bExists;
        }

        public bool DeleteJob(string sJobID)
        {
            bool bOK = false;

            DataSet dsData = new();
            string sQuery = "";
            try
            {
                string sRawID = sJobID;
                if (sJobID.IndexOf("-") > 0)
                {
                    sRawID = sJobID[..(sJobID.IndexOf("-") + 1)]; // on récupère [10- 
                    sQuery = "SELECT job_id FROM job_parameters WHERE \"user\" = '" + USER + "' AND (\"job_id\" LIKE '" + sRawID + "%' OR \"job_id\" = '" + sRawID[0..^1] + "]') ORDER BY LENGTH(\"job_id\") ASC, \"job_id\" ASC;";
                }
                else
                {
                    sQuery = "SELECT job_id FROM job_parameters WHERE \"user\" = '" + USER + "' AND (\"job_id\" LIKE '" + sRawID[0..^1] + "-%' OR \"job_id\" = '" + sRawID[0..^1] + "]') ORDER BY LENGTH(\"job_id\") ASC, \"job_id\" ASC;";
                }

                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Jobs");
                }

                List<string> sSubs = SQLTools.BuildListFromDs(dsData);

                if (sSubs.Count > 1)
                {
                    //position du sub dans la liste
                    int iPos = sSubs.IndexOf(sJobID);

                    //le premier subjob devient principal, les autres sont décalés d'un cran
                    StringBuilder sbQuery = new();
                    sbQuery.AppendLine("DELETE FROM job_parameters WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobID + "';");
                    for (int iR = iPos + 1; iR < sSubs.Count; iR++)
                    {
                        sbQuery.AppendLine("UPDATE job_parameters SET \"job_id\" = '" + sSubs[iR - 1] +
                                                                    "' WHERE \"user\" = '" + USER +
                                                                    "' AND \"job_id\" = '" + sSubs[iR] + "';");
                    }
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }
                    bOK = true;
                    LoadUserJobs(USER, false);
                }
                else //suppression simple
                {
                    sQuery = "DELETE FROM job_parameters WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobID + "';";
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }
                    bOK = true;
                    Job jD = _jJobsList.First(j => j.JobID.Equals(sJobID));
                    _jJobsList.Remove(jD);
                }

                if (Directory.Exists(PATH_JOBS_XML))
                {
                    File.Delete(Path.Combine(PATH_JOBS_XML, string.Concat("JOB_", sJobID, ".XML")));
                }

            }
            catch (Exception) { throw; }

            return bOK;
        }

        public string SwapSubJob(string sSubJobID, bool bGoUp)
        {
            int iRawJobID = Convert.ToInt32(sSubJobID[1..sSubJobID.IndexOf("-")]);

            //contrôler si il y a des sub-jobs à décaler
            List<Job> sSubs = GetChildrenCount(iRawJobID);

            //position du subjob
            int iPos = sSubs.IndexOf(GetJob(sSubJobID));

            string sNewID = sSubJobID;

            if (bGoUp) //on envoie un subjob [1-3] vers [1-4]
            {
                if (iPos < sSubs.Count - 1)
                {
                    string sJobJumpA = string.Concat("[", iRawJobID.ToString(), "-", (sSubs.Count + 2).ToString(), "]");

                    string sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sJobJumpA + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sSubJobID + "';";
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    //envoyer le job suivant sur la position plus basse
                    sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sSubJobID + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sSubs[iPos + 1].JobID + "';";
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sSubs[iPos + 1].JobID + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobJumpA + "';";

                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    sNewID = sSubs[iPos + 1].JobID;
                }
            }
            else //on envoie un subjob [1-3] vers [1-2]
            {
                if (iPos > 0)
                {
                    string sJobJumpA = string.Concat("[", iRawJobID.ToString(), "-", (sSubs.Count + 2).ToString(), "]");

                    string sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sJobJumpA + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sSubJobID + "';";
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    //envoyer le job suivant sur la position plus basse
                    sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sSubJobID + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sSubs[iPos - 1].JobID + "';";
                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sSubs[iPos - 1].JobID + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sJobJumpA + "';";

                    using (SqliteConnection sqlConn = new(INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    sNewID = sSubs[iPos - 1].JobID;
                }
            }

            LoadUserJobs(USER, false);

            return sNewID;
        }

        public string SetSubJobAsMainJob(string sSubJobID)
        {
            int iRawJobID = Convert.ToInt32(sSubJobID[1..sSubJobID.IndexOf("-")]);
            //trouver nouvel ID dispo
            string sNewID = GetNewJobID();
            string sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sNewID + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sSubJobID + "';";
            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                sqlCommand.ExecuteNonQuery();
            }

            //contrôler si il y a des sub-jobs à décaler
            List<Job> sSubs = GetChildrenCount(iRawJobID);

            //position du subjob
            int iPos = sSubs.IndexOf(GetJob(sSubJobID));

            //on tente de retirer par exemple le subjob [1-2] d'un job qui a aussi un [1-3], [1-4]...
            if (iPos < sSubs.Count - 1)
            {
                StringBuilder sbQuery = new();
                for (int iR = iPos + 1; iR < sSubs.Count; iR++)
                {
                    sbQuery.AppendLine("UPDATE job_parameters SET \"job_id\" = '" + sSubs[iR - 1].JobID +
                                                                "' WHERE \"user\" = '" + USER +
                                                                "' AND \"job_id\" = '" + sSubs[iR].JobID + "';");
                }
                using SqliteConnection sqlConn = new(INTERNAL_DB);
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                sqlCommand.ExecuteNonQuery();
            }

            LoadUserJobs(USER, false);

            return sNewID;
        }

        public string SetJobAsSubJob(string sMainJobID, string sSecondJob)
        {
            //le mainjob passe dans les steps du second job
            Job Main = GetJob(sSecondJob);
            List<Job> sSubs = GetChildrenCount(Main.RawJobID);
            string sNewId;
            //si il y a des subs, on le place en dernier
            if (sSubs.Count > 0)
            {
                int iNewId = sSubs.Last().SubJobID + 1;
                sNewId = string.Concat("[", Main.RawJobID, "-", iNewId.ToString(), "]");
                //update
            }
            else
            {
                sNewId = string.Concat("[", Main.RawJobID, "-2]");
            }

            string sQuery = "UPDATE job_parameters SET \"job_id\" = '" + sNewId + "', \"job_family\" = '" + Main.JobFamily.Name + "' WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + sMainJobID + "';";
            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                sqlCommand.ExecuteNonQuery();
            }

            LoadUserJobs(USER, false);

            return sNewId;
        }

        public static bool RandomlyChangeSampleXLS(string sPath)
        {
            bool bOK = true;

            Random rNd = new();
            Random rNdDelete = new();
            Random rNdModify = new();

            List<int> iModified = new();
            List<int> iDeleted = new();

            FileInfo fiExcel = new(sPath + "SAMPLE.XLSX");
            using (ExcelPackage pck = new(fiExcel))
            {
                string sData = "XXX";
                ExcelWorksheet ws1 = pck.Workbook.Worksheets[1];
                int iRC = 1;
                while (sData != null)
                {
                    iRC++;
                    sData = (string)ws1.Cells["A" + iRC].Value;
                }
                sData = (string)ws1.Cells["A" + (iRC - 1)].Value;
                int iNewStart = Convert.ToInt32(sData) + 1;

                for (int i = iRC; i < iRC + 101; i++)
                {
                    ws1.Cells["A" + i].Value = iNewStart.ToString();
                    ws1.Cells["B" + i].Value = Toolbox.RandomString(rNd, 16, true).PadRight(30);
                    ws1.Cells["C" + i].Value = rNd.Next(-1000000, 1000000).ToString();
                    ws1.Cells["D" + i].Value = Math.Round(iNewStart / 10.0).ToString();
                    iNewStart++;
                }
                pck.Save();
            }
            using (ExcelPackage pck = new(fiExcel))
            {
                ExcelWorksheet ws1 = pck.Workbook.Worksheets[1];

                for (int i = 0; i < 50; i++)
                {
                    iModified.Add(rNdModify.Next(1000, 9000));
                    iDeleted.Add(rNdDelete.Next(1000, 9000));
                }

                for (int i = 0; i < iModified.Count; i++)
                {
                    ws1.Cells["B" + iModified[i]].Value = Toolbox.RandomString(rNd, 16, true).PadRight(30);
                    ws1.Cells["C" + iModified[i]].Value = rNd.Next(-1000000, 1000000).ToString();
                }

                for (int i = 0; i < iDeleted.Count; i++)
                {
                    ws1.DeleteRow(iDeleted[i], 1, true);
                }

                pck.Save();
            }

            return bOK;

        }

        public static bool CreateSampleXLS(string sPath)
        {
            try
            {
                Random rNd = new();

                FileInfo fiExcel = new(sPath + "SAMPLE.XLSX");
                using (ExcelPackage pck = new(fiExcel))
                {
                    ExcelWorksheet ws1 = pck.Workbook.Worksheets.Add("EmptySheet");
                    ExcelWorksheet ws2 = pck.Workbook.Worksheets.Add("SampleSheet");

                    //boucle pour insérer des données factices
                    DataTable dtXLS = new("SampleExcel");
                    dtXLS.Columns.Add("sample code");
                    dtXLS.Columns.Add("sample untrimmed name");
                    dtXLS.Columns.Add("random number");
                    dtXLS.Columns.Add("sample group");
                    for (int i = 0; i < 10000; i++)
                    {
                        DataRow drXLS = dtXLS.NewRow();
                        drXLS[0] = i;
                        drXLS[1] = Toolbox.RandomString(rNd, 16, true).PadRight(30);
                        drXLS[2] = rNd.Next(-1000000, 1000000);
                        drXLS[3] = Math.Round(i / 10.0);
                        dtXLS.Rows.Add(drXLS);
                    }

                    ws2.Cells["A2"].LoadFromDataTable(dtXLS, true);
                    pck.Save();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Job CreateBenchmarkingJob(string sPath, int iQteRows)
        {
            if (!Directory.Exists(sPath))
            {
                try { Directory.CreateDirectory(sPath); }
                catch { }
            }

            if (!File.Exists(sPath + "BENCHMARK.CSV"))
            {
                using StreamWriter sw = new(sPath + "BENCHMARK.CSV", true, Encoding.UTF8, 65536);
                StringBuilder sbHeader = new();
                for (int i = 1; i <= 10; i++)
                {
                    sbHeader.Append("id_bench_" + i.ToString("00") + ";");
                    sbHeader.Append("li_bench_" + i.ToString("00") + ";");
                    sbHeader.Append("nb_bench_" + i.ToString("00") + ";");
                    sbHeader.Append("dt_bench_" + i.ToString("00") + ";");
                }
                sw.WriteLine(sbHeader.ToString()[..^1]);

                Random rNd = new();

                for (int iCpt = 1; iCpt <= iQteRows; iCpt++)
                {
                    sbHeader.Clear();
                    for (int i = 1; i <= 10; i++)
                    {
                        sbHeader.Append(iCpt.ToString() + ";");
                        sbHeader.Append(Toolbox.RandomString(rNd, 16, true) + ";");
                        sbHeader.Append((rNd.Next(-1000000, 1000000) / 0.16).ToString() + ";");
                        sbHeader.Append(DateTime.Now.AddDays(rNd.Next(-100000, 100000)).ToString() + ";");
                    }

                    sw.WriteLine(sbHeader.ToString()[0..^1]);
                }
            }

            Job jBench = new(this.GlobalParameters, "[0]", USER, false, null)
            {
                ConnectionString_Source = Connections.GetConnByID("[1]"),
                ConnectionString_Target = Connections.GetConnByID("[1]"),
                JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT,
                CSVRowOffset = 0,
                MaxRowsInAFile = iQteRows + 1
            };
            jBench.GlobalParameters.ConfigureForBenchmarking(iQteRows);

            Query qBench = new(jBench, "SELECT * FROM BENCHMARK.CSV");
            jBench.JobQueries.Add(qBench);

            return jBench;
        }

        public static bool CreateSampleCSV(string sPath)
        {
            if (!Directory.Exists(sPath))
            {
                try { Directory.CreateDirectory(sPath); }
                catch { }
            }

            bool bOK = true;
            StringBuilder sbCSVSample = new();
            StringBuilder sbCSVSample2 = new();
            StringBuilder sbCSVSample3 = new();

            sbCSVSample.AppendLine("id_sample;li_sample;dt_random_date;nb_random_number;li_random_string;id_group;id_ssgroup");

            Random rNd = new();

            for (int iCpt = 1; iCpt <= 100; iCpt++)
            {
                char[] cId = iCpt.ToString().ToCharArray();
                string sLiSample = "";
                foreach (char c in cId)
                {
                    switch (c.ToString())
                    {
                        case "0":
                            sLiSample = string.Concat(sLiSample, "zero", "-");
                            break;
                        case "1":
                            sLiSample = string.Concat(sLiSample, "one", "-");
                            break;
                        case "2":
                            sLiSample = string.Concat(sLiSample, "two", "-");
                            break;
                        case "3":
                            sLiSample = string.Concat(sLiSample, "three", "-");
                            break;
                        case "4":
                            sLiSample = string.Concat(sLiSample, "four", "-");
                            break;
                        case "5":
                            sLiSample = string.Concat(sLiSample, "five", "-");
                            break;
                        case "6":
                            sLiSample = string.Concat(sLiSample, "six", "-");
                            break;
                        case "7":
                            sLiSample = string.Concat(sLiSample, "seven", "-");
                            break;
                        case "8":
                            sLiSample = string.Concat(sLiSample, "eight", "-");
                            break;
                        case "9":
                            sLiSample = string.Concat(sLiSample, "nine", "-");
                            break;
                    }
                }
                sLiSample = sLiSample[0..^1];

                double dR = rNd.Next(100, 1000) / 16.0;

                string sRandomStr = Toolbox.RandomString(rNd, 16, true);

                string sRandomDate = DateTime.Now.AddDays(rNd.Next(-100, 100)).ToString();

                //groupe de 10
                string sGroup = (iCpt % 10).ToString("000");

                //groupe de 5;
                string ssGroup = (iCpt % 5).ToString("000");

                sbCSVSample.AppendLine(string.Concat(iCpt.ToString(), ";", sLiSample, ";", sRandomDate, ";", dR.ToString(), ";", sRandomStr, ";", sGroup, ";", ssGroup));
            }
            StreamWriter sw = new(sPath + "SAMPLE.CSV", false, Encoding.UTF8);
            sw.Write(sbCSVSample.ToString());
            sw.Close();

            sbCSVSample2.AppendLine("id_sample_join;li_sample_join");
            for (int iCpt = 1; iCpt <= 100; iCpt += 5)
            {
                string sRandomStr = Toolbox.RandomString(rNd, 16, true);
                sbCSVSample2.AppendLine(string.Concat(iCpt.ToString(), ";", sRandomStr));
            }
            StreamWriter sw2 = new(sPath + "SAMPLE_JOIN.CSV", false, Encoding.UTF8);
            sw2.Write(sbCSVSample2.ToString());
            sw2.Close();

            sbCSVSample3.AppendLine("<?xml version='1.0'?>");
            sbCSVSample3.AppendLine("<SAMPLE_FILE>");
            for (int iCpt = 1; iCpt <= 100; iCpt += 5)
            {
                string sRandomStr = Toolbox.RandomString(rNd, 16, true);
                sbCSVSample3.AppendLine("<row>");
                sbCSVSample3.AppendLine(string.Concat("<id_sample_join>", iCpt.ToString(), "</id_sample_join>", "<li_sample_join>", sRandomStr, "</li_sample_join>"));
                sbCSVSample3.AppendLine("</row>");
            }
            sbCSVSample3.AppendLine("</SAMPLE_FILE>");
            StreamWriter sw3 = new(sPath + "SAMPLE_CROSSJOIN.XML", false, Encoding.UTF8);
            sw3.Write(sbCSVSample3.ToString());
            sw3.Close();

            bool bXLS = CreateSampleXLS(sPath);
            if (!bXLS) { bOK = false; }

            return bOK;
        }

        public Job CreateSampleJob()
        {
            string sJobName = "CSV -> XLS : This is a sample Job";

            string[] sSampleQueries = new string[] {
            "SAMPLE_OUTPUT_SELECTALL.XLSX:SELECT * FROM SAMPLE.CSV",
            "SAMPLE_OUTPUT_SELECTBYFIELD.XLSX:SELECT id_sample AS FirstColumn, li_sample AS SecondColumn FROM SAMPLE.CSV",
            "SAMPLE_OUTPUT_DISTINCT.XLSX:SELECT DISTINCT id_ssgroup AS DistinctSSGroup FROM SAMPLE.CSV",
            "SAMPLE_OUTPUT_WHERE_01.XLSX:SELECT * FROM SAMPLE.CSV WHERE id_sample > 50",
            "SAMPLE_OUTPUT_WHERE_02.XLSX:SELECT * FROM SAMPLE.CSV WHERE id_ssgroup = '001' AND id_sample > 75",
            "SAMPLE_OUTPUT_WHERE_03.XLSX:SELECT * FROM SAMPLE.CSV WHERE SUBSTRING(id_ssgroup, 3, 1) = '1'",
            "SAMPLE_OUTPUT_ORDERBY.XLSX:SELECT id_sample, li_sample, dt_random_date FROM SAMPLE.CSV ORDER BY dt_random_date DESC",
            "SAMPLE_OUTPUT_FUNCTION_01.XLSX:SELECT id_sample, CONCAT(li_sample, '-', li_random_string) as Concat FROM SAMPLE.CSV",
            "SAMPLE_OUTPUT_FUNCTION_02.XLSX:SELECT id_sample, SUBSTRING(id_group, 3,1) as SubstringGroup, LPAD(id_group, 8, 'X') as LpadGroup, UPPER(li_sample) as UpperSample FROM SAMPLE.CSV",
            "SAMPLE_OUTPUT_CASEWHEN.XLSX:SELECT id_sample, CASE id_sample WHEN 1 THEN 'Hello' WHEN 2 THEN 'World' ELSE 'Nothing' END as CaseWhen FROM SAMPLE.CSV WHERE id_sample <= 10",
            "SAMPLE_OUTPUT_GROUPBY_01.XLSX:SELECT SUM(nb_random_number) AS RandomSum, id_group as Group FROM SAMPLE.CSV GROUP BY id_group",
            "SAMPLE_OUTPUT_GROUPBY_02.XLSX:SELECT SUM(nb_random_number) AS RandomSum, MAX(id_sample) AS IdMax, id_group as Group, id_ssgroup as SubGroup FROM SAMPLE.CSV GROUP BY id_group, id_ssgroup",
            "SAMPLE_OUTPUT_GROUPBY_03.XLSX:SELECT SUM(nb_random_number) AS RandomSum, SUBSTRING(id_group, 3, 1) as Group FROM SAMPLE.CSV GROUP BY SUBSTRING(id_group, 3, 1)",
            "SAMPLE_OUTPUT_LEFTJOIN.XLSX:SELECT fileA.id_sample AS IdInitial, fileB.id_sample_join AS IdJoin, fileB.li_sample_join as LiJoin, ISNULL(fileB.li_sample_join, 'Nothing') as IsNullField FROM SAMPLE.CSV AS fileA LEFT JOIN SAMPLE_JOIN.CSV AS fileB ON fileA.id_sample = fileB.id_sample_join",
            "SAMPLE_OUTPUT_INNERJOIN.XLSX:SELECT fileA.id_sample AS IdInitial, fileB.id_sample_join AS IdJoin, fileB.li_sample_join as LiJoin FROM SAMPLE.CSV AS fileA INNER JOIN SAMPLE_JOIN.CSV AS fileB ON fileA.id_sample = fileB.id_sample_join",
            "SAMPLE_INNERJOIN_SUBQUERY.XLSX:SELECT fileA.id_sample AS IdInitial, fileB.id_sample_join AS IdJoin, fileB.li_sample_join as LiJoin FROM SAMPLE.CSV AS fileA INNER JOIN (SELECT * FROM SAMPLE_JOIN.CSV) AS fileB ON fileA.id_sample = fileB.id_sample_join",
            "SAMPLE_OUTPUT_CROSSQUERY.XLSX:SELECT id_sample as JoinField, li_sample FROM SAMPLE.CSV [--[3]] SELECT id_sample_join as JoinField, li_sample_join FROM SAMPLE_CROSSJOIN.XML WHERE id_sample_join = 1",
            "SAMPLE_CROSSQUERY_WHERE.XLSX:SELECT id_sample as JoinField, li_sample FROM SAMPLE.CSV [<>[3]WHERE li_sample_join IS NOT NULL] SELECT id_sample_join as JoinField, li_sample_join FROM SAMPLE_CROSSJOIN.XML WHERE id_sample_join = 1",
            "[2]SAMPLE_OUTPUT_TARGET_01.XLSX[3]SAMPLE_OUTPUT_TARGET_02.XLSX:SELECT * FROM SAMPLE.CSV AS MYTAB",
            "SAMPLE_OUTPUT_DYNAMICPARAM.XLSX:SELECT id_sample, CONCAT(li_sample, '/', {?1}, '/', li_random_string) as DynamicConcat FROM SAMPLE.CSV WHERE dt_random_date < '{?2}'",
            "{?3}.XLSX:SELECT * from SAMPLE.CSV",
            "[1]NEWPATH\\*_OUT.CSV:SELECT * FROM *.CSV",
            "SAMPLE_OUTPUT_READFROMOTHERPATH.XLSX:SELECT * FROM NEWPATH\\SAMPLE_OUT.CSV" };

            Job FirstJob;

            string sJobID = GetNewJobID();
            CreateSampleCSV(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\");

            FirstJob = new Job(GlobalParameters, sJobID, USER, false);

            FirstJob.SetJobPassword("", sJobID, false);
            FirstJob.JobNAME = sJobName;
            FirstJob.JobDescription = Languages.Languages.par_samplejobinfo;
            FirstJob.ConnectionString_Source = Connections.CSList.Count > 0 ? Connections.GetConnByID("[1]") : null; //dans les conn par défaut, la 1 est CSV dans répertoire FILES
            FirstJob.ConnectionString_Target = Connections.CSList.Count > 1 ? Connections.GetConnByID("[2]") : null; //la 2 est XLS
            FirstJob.DynParams = new List<string> { "SampleDynamicParam;%DD/%MM/%YYYY;SAMPLE_OUTPUT_DYNPARAM" };
            FirstJob.OptionalDynamicParamField_OnInsert = "DynColumn={?1}";

            List<Query> sListQueries = new();

            foreach (string sQ in sSampleQueries)
            {
                sListQueries.Add(new Query(FirstJob, sQ));
            }

            bool bOK = SaveJob(FirstJob);

            if (bOK) { SaveJobQueries(FirstJob, sListQueries); }

            return bOK ? FirstJob : null;

        }

        public List<string[]> GetAllJobsQueries()
        {
            List<string[]> sListQueries = new();
            try
            {
                DataSet dsData = new();

                string sQuery = string.Concat("SELECT \"job_id\", \"query\" FROM job_queries WHERE \"user\" = '", USER, "' ORDER BY \"job_id\" ASC;");
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Queries");
                }
                if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    string sJob = dsData.Tables[0].Rows[0][0].ToString();
                    StringBuilder sbQueries = new();
                    foreach (DataRow dr in dsData.Tables[0].Rows)
                    {
                        string sQueryDecrypted = dr["query"].ToString();

                        if (!sQueryDecrypted.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                        {
                            try { sQueryDecrypted = FITools.EncryptionSystem.AES_Decrypt(sQueryDecrypted, SHSConstantes.PROGRAM_PWD); }
                            catch { }

                            if (dr["query"].ToString().Length > 0 && sQueryDecrypted.Length == 0) //la requête n'était pas chiffrée
                            {
                                sQueryDecrypted = dr["query"].ToString();
                            }
                        }

                        if (dr["job_id"].ToString().Equals(sJob))
                        {
                            sbQueries.AppendLine(sQueryDecrypted);
                        }
                        else
                        {
                            sListQueries.Add(new string[] { sJob, sbQueries.ToString() });
                            sbQueries.Clear();
                            sbQueries.AppendLine(sQueryDecrypted);
                            sJob = dr["job_id"].ToString();
                        }
                    }
                    sListQueries.Add(new string[] { sJob, sbQueries.ToString() });
                }
            }
            catch (Exception) { throw; }

            return sListQueries;

        }

        public Job ImportExternalJob(INIProgram ExternalUserData, Job ExtJobToProcess, bool bUseNameAsMatchingPattern)
        {
            DataSet dsData = new();
            //récupération de toutes les connexions du job externe
            string sQuery = "select distinct ucs.\"connstring_id\" as connstring_source, uct.\"connstring_id\" as connstring_target, ucsppc.\"connstring_id\" as connstring_source_prepostcommand, uctppc.\"connstring_id\" as connstring_target_prepostcommand, ucq.\"connstring_id\" as connstring_in_queries, ucdp.\"connstring_id\" as connstring_in_dynparam" +
                            " from job_parameters uj" +
                            " inner join job_queries jq on jq.\"job_id\" = uj.\"job_id\" AND uj.\"user\" = jq.\"user\"" +
                            " left join user_connstrings ucs ON ucs.\"connstring_id\" = uj.\"connexion_string_export\" AND ucs.\"user\" = uj.\"user\"" +
                            " left join user_connstrings uct ON uct.\"connstring_id\" = uj.\"connexion_string_import\" AND uct.\"user\" = uj.\"user\"" +
                            " left join user_connstrings ucsppc ON ucsppc.\"connstring_id\" = uj.\"post_job_sql_command_source_conn\" AND ucsppc.\"user\" = uj.\"user\"" +
                            " left join user_connstrings uctppc ON uctppc.\"connstring_id\" = uj.\"post_job_sql_command_target_conn\" AND uctppc.\"user\" = uj.\"user\"" +
                            " left join user_connstrings ucq ON jq.\"query\" like('%' || ucq.\"connstring_id\" || '%') AND ucq.\"user\" = uj.\"user\"" +
                            " left join user_connstrings ucdp ON uj.\"source_queries_dynamic_parameters\" like('%' || ucdp.\"connstring_id\" || '%') AND ucdp.\"user\" = uj.\"user\"" +
                            " where uj.job_id = '" + ExtJobToProcess.JobID + "' and uj.user = '" + ExtJobToProcess.USER + "';";

            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Job Connections");
            }
            //tester que les connexions récupérées n'existent pas dans l'utilisateur actuel
            var sConnSource = SQLTools.BuildListFromDs(dsData, 0, 0).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var sConnTarget = SQLTools.BuildListFromDs(dsData, 0, 1).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var sConnSourcePrePostCommand = new List<string>();
            try
            {
                sConnSourcePrePostCommand = SQLTools.BuildListFromDs(dsData, 0, 2).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            }
            catch { }
            var sConnTargetPrePostCommand = new List<string>();
            try
            {
                sConnTargetPrePostCommand = SQLTools.BuildListFromDs(dsData, 0, 3).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            }
            catch { }

            var sConnQueries = SQLTools.BuildListFromDs(dsData, 0, 4).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var sConnDynParam = SQLTools.BuildListFromDs(dsData, 0, 5).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

            //ajout des contrôles (existence connexion...
            CONNString csS = ExternalUserData.Connections.GetConnByID(sConnSource[0]);
            CONNString csT = ExternalUserData.Connections.GetConnByID(sConnTarget[0]);
            CONNString csSPPC = ExternalUserData.Connections.GetConnByID(sConnSourcePrePostCommand.Count > 0 ? sConnSourcePrePostCommand[0] : "");
            CONNString csTPPC = ExternalUserData.Connections.GetConnByID(sConnTargetPrePostCommand.Count > 0 ? sConnTargetPrePostCommand[0] : "");
            List<CONNString> csQ = new();
            List<CONNString> csDP = new();

            foreach (string sC in sConnQueries) { if (ExternalUserData.Connections.GetConnByID(sC) != null) { csQ.Add(ExternalUserData.Connections.GetConnByID(sC)); } }
            foreach (string sC in sConnDynParam) { if (ExternalUserData.Connections.GetConnByID(sC) != null) { csDP.Add(ExternalUserData.Connections.GetConnByID(sC)); } }

            CONNString NewCSS = null;
            CONNString NewCST = null;
            CONNString NewCSSPPC = null;
            CONNString NewCSTPPC = null;
            List<CONNString> NewCSQ = new();
            List<CONNString> NewCSDP = new();

            //on cherche si une chaîne de connexion identique existe pour notre utilisateur local
            if (csS != null)
            {
                if (bUseNameAsMatchingPattern)
                {
                    if (Connections.CSList.Any(c => c.SConnName.Equals(csS.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csS.SConnDriver))
                    {
                        NewCSS = Connections.CSList.First(c => c.SConnName.Equals(csS.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csS.SConnDriver);
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csS.SConnDriver, string.Concat(csS.SConnName, " (imported)"), csS.SConnString(null), csS.SConnParams);
                        NewCSS = Connections.GetConnByID(sID);
                    }
                }
                else
                {
                    if (Connections.CSList.Any(c => c.SConnString(null).Equals(csS.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csS.SConnDriver))
                    {
                        NewCSS = Connections.CSList.First(c => c.SConnString(null).Equals(csS.SConnString(null), StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csS.SConnDriver, string.Concat(csS.SConnName, " (imported)"), csS.SConnString(null), csS.SConnParams);
                        NewCSS = Connections.GetConnByID(sID);
                    }
                }
            }

            if (csT != null)
            {
                if (bUseNameAsMatchingPattern)
                {
                    if (Connections.CSList.Any(c => c.SConnName.Equals(csT.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csT.SConnDriver))
                    {
                        NewCST = Connections.CSList.First(c => c.SConnName.Equals(csT.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csT.SConnDriver);
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csT.SConnDriver, string.Concat(csT.SConnName, " (imported)"), csT.SConnString(null), csT.SConnParams);
                        NewCST = Connections.GetConnByID(sID);
                    }
                }
                else
                {
                    if (Connections.CSList.Any(c => c.SConnString(null).Equals(csT.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csT.SConnDriver))
                    {
                        NewCST = Connections.CSList.First(c => c.SConnString(null).Equals(csT.SConnString(null), StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csT.SConnDriver, string.Concat(csT.SConnName, " (imported)"), csT.SConnString(null), csT.SConnParams);
                        NewCST = Connections.GetConnByID(sID);
                    }
                }
            }

            if (csSPPC != null)
            {
                if (bUseNameAsMatchingPattern)
                {
                    if (Connections.CSList.Any(c => c.SConnName.Equals(csSPPC.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csSPPC.SConnDriver))
                    {
                        NewCSSPPC = Connections.CSList.First(c => c.SConnName.Equals(csSPPC.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csSPPC.SConnDriver);
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csSPPC.SConnDriver, string.Concat(csSPPC.SConnName, " (imported)"), csSPPC.SConnString(null), csSPPC.SConnParams);
                        NewCSSPPC = Connections.GetConnByID(sID);
                    }
                }
                else
                {
                    if (Connections.CSList.Any(c => c.SConnString(null).Equals(csSPPC.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csSPPC.SConnDriver))
                    {
                        NewCSSPPC = Connections.CSList.First(c => c.SConnString(null).Equals(csSPPC.SConnString(null), StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csSPPC.SConnDriver, string.Concat(csSPPC.SConnName, " (imported)"), csSPPC.SConnString(null), csSPPC.SConnParams);
                        NewCSSPPC = Connections.GetConnByID(sID);
                    }
                }
            }

            if (csTPPC != null)
            {
                if (bUseNameAsMatchingPattern)
                {
                    if (Connections.CSList.Any(c => c.SConnName.Equals(csTPPC.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csTPPC.SConnDriver))
                    {
                        NewCSTPPC = Connections.CSList.First(c => c.SConnName.Equals(csTPPC.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csTPPC.SConnDriver);
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csTPPC.SConnDriver, string.Concat(csTPPC.SConnName, " (imported)"), csTPPC.SConnString(null), csTPPC.SConnParams);
                        NewCSTPPC = Connections.GetConnByID(sID);
                    }
                }
                else
                {
                    if (Connections.CSList.Any(c => c.SConnString(null).Equals(csTPPC.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == csTPPC.SConnDriver))
                    {
                        NewCSTPPC = Connections.CSList.First(c => c.SConnString(null).Equals(csTPPC.SConnString(null), StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        string sID = Connections.AddCONNString(csTPPC.SConnDriver, string.Concat(csTPPC.SConnName, " (imported)"), csTPPC.SConnString(null), csTPPC.SConnParams);
                        NewCSTPPC = Connections.GetConnByID(sID);
                    }
                }
            }

            if (csQ != null && csQ.Count > 0)
            {
                foreach (CONNString cs in csQ)
                {
                    if (bUseNameAsMatchingPattern)
                    {
                        if (Connections.CSList.Any(c => c.SConnName.Equals(cs.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver))
                        {
                            NewCSQ.Add(Connections.CSList.First(c => c.SConnName.Equals(cs.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver));
                        }
                        else
                        {
                            string sID = Connections.AddCONNString(cs.SConnDriver, string.Concat(cs.SConnName, " (imported)"), cs.SConnString(null), cs.SConnParams);
                            NewCSQ.Add(Connections.GetConnByID(sID));
                        }
                    }
                    else
                    {
                        if (Connections.CSList.Any(c => c.SConnString(null).Equals(cs.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver))
                        {
                            NewCSQ.Add(Connections.CSList.First(c => c.SConnString(null).Equals(cs.SConnString(null), StringComparison.OrdinalIgnoreCase)));
                        }
                        else
                        {
                            string sID = Connections.AddCONNString(cs.SConnDriver, string.Concat(cs.SConnName, " (imported)"), cs.SConnString(null), cs.SConnParams);
                            NewCSQ.Add(Connections.GetConnByID(sID));
                        }
                    }
                }
            }

            if (csDP != null && csDP.Count > 0)
            {
                foreach (CONNString cs in csDP)
                {
                    if (bUseNameAsMatchingPattern)
                    {
                        if (Connections.CSList.Any(c => c.SConnName.Equals(cs.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver))
                        {
                            NewCSDP.Add(Connections.CSList.First(c => c.SConnName.Equals(cs.SConnName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver));
                        }
                        else
                        {
                            string sID = Connections.AddCONNString(cs.SConnDriver, string.Concat(cs.SConnName, " (imported)"), cs.SConnString(null), cs.SConnParams);
                            NewCSDP.Add(Connections.GetConnByID(sID));
                        }
                    }
                    else
                    {
                        if (Connections.CSList.Any(c => c.SConnString(null).Equals(cs.SConnString(null), StringComparison.OrdinalIgnoreCase) && c.SConnDriver == cs.SConnDriver))
                        {
                            NewCSDP.Add(Connections.CSList.First(c => c.SConnString(null).Equals(cs.SConnString(null), StringComparison.OrdinalIgnoreCase)));
                        }
                        else
                        {
                            string sID = Connections.AddCONNString(cs.SConnDriver, string.Concat(cs.SConnName, " (imported)"), cs.SConnString(null), cs.SConnParams);
                            NewCSDP.Add(Connections.GetConnByID(sID));
                        }
                    }
                }
            }

            //on a maintenant une liste de conversion entre les connexions du job externe et celles du job interne
            //on charge le job externe
            dsData.Tables.Clear();
            dsData = new DataSet();
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new("SELECT * FROM job_parameters WHERE \"user\" = '" + ExtJobToProcess.USER + "' AND \"job_id\" = '" + ExtJobToProcess.JobID + "';", sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "External Job");
            }

            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                Job JNew = new(GlobalParameters, "[0]", USER, false, dsData.Tables[0].Rows[0]);

                //remplacement des chaînes de connexion par les nouvelles
                if (NewCSS != null) { JNew.ConnectionString_Source = NewCSS; }
                if (NewCST != null) { JNew.ConnectionString_Target = NewCST; }
                if (NewCSSPPC != null) { JNew.PrePostJob_CommandSourceConnection = NewCSSPPC.SConnID; }
                if (NewCSTPPC != null) { JNew.PrePostJob_CommandTargetConnection = NewCSTPPC.SConnID; }

                JNew.JobQueries.Clear();

                //remplacement des connexions dans les queries (cross-queries et dual target)
                List<Query> NewQueries = new();
                foreach (Query Q in ExtJobToProcess.JobQueries)
                {
                    string sNewQuery = Q.RawQuery;
                    for (int iC = 0; iC < NewCSQ.Count; iC++)
                    {
                        sNewQuery = sNewQuery.Replace(csQ[iC].SConnID, NewCSQ[iC].SConnID);
                    }
                    NewQueries.Add(new Query(JNew, sNewQuery));
                }
                JNew.JobQueries = NewQueries;

                //remplacement des connexions dans les paramètres dynamiques
                List<string> NewDynParams = new();
                foreach (string s in ExtJobToProcess.DynParams)
                {
                    string sNewParam = s;
                    for (int iC = 0; iC < NewCSDP.Count; iC++)
                    {
                        sNewParam = sNewParam.Replace(csDP[iC].SConnID, NewCSDP[iC].SConnID);
                    }
                    NewDynParams.Add(sNewParam);
                }
                JNew.DynParams = NewDynParams;
                JNew.IsJobVisibleInClientApp = false;
                JNew.HasSqlLog = false;

                return JNew;
            }
            else { return null; }
        }

        public Job ImportExternalJobXML(DataSet dsJob, string sPwd, bool bUseNameToFindConnection, bool bEncrypted, ref List<string> sErrorsImport)
        {
            Job NewJob = null;
            //démultiplexage d'un job importé en XML

            //récupération des colonnes en comparant ce qui est attendu dans un job
            DataSet dsData = new();
            string sQuery = "select * from job_parameters limit 1;";

            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Job Parameters");
            }
            if (dsData != null && dsData.Tables.Count > 0)
            {
                List<string> sListQueries = new();

                //attribution d'un nouvel ID
                string sNewID = GetNewJobID();
                //on va remplacer les valeurs de chaque colonne par le contenu dans le fichier XML
                DataRow drJob = dsData.Tables[0].NewRow();

                foreach (DataColumn dc in dsData.Tables[0].Columns)
                {
                    if (dsJob.Tables["JobParams"].Columns.Contains(dc.ColumnName))
                    {
                        string sValue = "";
                        if (!dc.ColumnName.Equals("job_id"))
                        {
                            if (bEncrypted)
                            {
                                sValue = FITools.EncryptionSystem.AES_Decrypt(dsJob.Tables["JobParams"].Rows[0][dc.ColumnName].ToString(), sPwd);
                            }
                            else
                            {
                                sValue = WebUtility.HtmlDecode(dsJob.Tables["JobParams"].Rows[0][dc.ColumnName].ToString());
                            }
                        }
                        else
                        { sValue = dsJob.Tables["JobParams"].Rows[0][dc.ColumnName].ToString(); }

                        drJob[dc.ColumnName] = sValue;
                    }
                    else { drJob[dc.ColumnName] = DBNull.Value; }
                }

                drJob["job_id"] = sNewID;
                drJob["job_password"] = FITools.EncryptionSystem.AES_Encrypt(sPwd, sNewID);
                drJob["user"] = USER;
                drJob["job_name"] = string.Concat(drJob["job_name"], " (imported)");

                //gestion des correspondances de connexion : faire l'import de ce qui se trouve dans le job
                int iQteConnToAdd = 4;
                string sAdd = "0";
                try { sAdd = dsJob.Tables["AdditionnalConnections"].Rows[0]["Count"].ToString(); } catch { }
                iQteConnToAdd += Convert.ToInt32(sAdd);
                List<string> sListNewConn = new();

                int iQteParams = Convert.ToInt32(dsJob.Tables["JobDynamicParams"].Rows[0]["Count"].ToString());
                int iQteQuery = Convert.ToInt32(dsJob.Tables["JobQueries"].Rows[0]["Count"].ToString());
                for (int iQ = 0; iQ < iQteQuery; iQ++)
                {
                    if (bEncrypted)
                    {
                        sListQueries.Add(FITools.EncryptionSystem.AES_Decrypt(dsJob.Tables["Query" + (iQ + 1)].Rows[0]["String"].ToString(), sPwd));
                    }
                    else
                    {
                        sListQueries.Add(WebUtility.HtmlDecode(dsJob.Tables["Query" + (iQ + 1)].Rows[0]["String"].ToString()));
                    }
                }

                //ajout des nouvelles connexions
                for (int iC = 0; iC < iQteConnToAdd; iC++)
                {
                    string sTablename = iC switch
                    {
                        0 => "ConnJobSource",
                        1 => "ConnJobTarget",
                        2 => "ConnJobPrePostCommandSource",
                        3 => "ConnJobPrePostCommandTarget",
                        _ => "Conn" + (iC - 3).ToString(),
                    };

                    if (dsJob.Tables.Contains(sTablename))
                    {
                        //ConnJobSource, ConnJobTarget, AdditionnalConnections (Count : Conn1, Conn2...)
                        string sOldID = dsJob.Tables[sTablename].Rows[0]["ID"].ToString();
                        var eBDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), dsJob.Tables[sTablename].Rows[0]["Driver"].ToString());
                        string sConn = dsJob.Tables[sTablename].Rows[0]["String"].ToString();
                        string sName = string.Concat(dsJob.Tables[sTablename].Rows[0]["Name"].ToString());
                        int iParamCount = Convert.ToInt32(dsJob.Tables[sTablename].Rows[0]["ParamCount"].ToString());
                        List<string> sParams = new();
                        for (int iP = 0; iP < iParamCount; iP++)
                        {
                            sParams.Add(dsJob.Tables[sTablename].Rows[0]["Param" + (iP + 1).ToString()].ToString());
                        }

                        //decryption des paramètres : chaîne + params avec l'ID de job
                        sConn = FITools.EncryptionSystem.AES_Decrypt(sConn, sPwd);
                        for (int iP = 0; iP < sParams.Count; iP++)
                        {
                            sParams[iP] = FITools.EncryptionSystem.AES_Decrypt(sParams[iP], sPwd);
                        }

                        //recherche de connexion existante
                        string sNewConnId = "";
                        if (bUseNameToFindConnection)
                        {
                            if (Connections.CSList.Any(c => c.SConnName.Equals(sName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == eBDD))
                            {
                                sNewConnId = Connections.CSList.First(c => c.SConnName.Equals(sName, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == eBDD).SConnID;
                            }
                            else
                            {
                                sNewConnId = Connections.AddCONNString(eBDD, sName + " (imported)", sConn, sParams);
                                sListNewConn.Add(sNewConnId);
                            }
                        }
                        else
                        {
                            if (Connections.CSList.Any(c => c.SConnString(null).Equals(sConn, StringComparison.OrdinalIgnoreCase) && c.SConnDriver == eBDD))
                            {
                                sNewConnId = Connections.CSList.First(c => c.SConnString(null).Equals(sConn, StringComparison.OrdinalIgnoreCase)).SConnID;
                            }
                            else
                            {
                                sNewConnId = Connections.AddCONNString(eBDD, sName + " (imported)", sConn, sParams);
                                sListNewConn.Add(sNewConnId);
                            }
                        }

                        //parcours du job pour remplacement
                        switch (iC)
                        {
                            case 0:
                                drJob["connexion_string_export"] = sNewConnId;
                                break;
                            case 1:
                                drJob["connexion_string_import"] = sNewConnId;
                                break;
                            case 2:
                                drJob["post_job_sql_command_source_conn"] = sNewConnId;
                                break;
                            case 3:
                                drJob["post_job_sql_command_target_conn"] = sNewConnId;
                                break;
                        }

                        //recherche d'une connexion dans les paramètres dynamiques
                        string sPars = drJob["source_queries_dynamic_parameters"].ToString();
                        string[] sPSplit = sPars.Split(Convert.ToChar(";"));
                        for (int iP = 0; iP < iQteParams; iP++)
                        {
                            if (dsJob.Tables["DynamicParam" + (iP + 1)].Columns.Contains("Conn"))
                            {
                                if (sPSplit[iP].Equals(sOldID))
                                {
                                    sPSplit[iP] = sNewConnId;
                                }
                            }
                        }
                        drJob["source_queries_dynamic_parameters"] = string.Join(";", sPSplit);

                        //recherche d'une connexion dans les requêtes
                        for (int iQ = 0; iQ < iQteQuery; iQ++)
                        {
                            if (dsJob.Tables["Query" + (iQ + 1)].Columns.Contains("ConnTargetA")) //requête avec target différent
                            {
                                if (dsJob.Tables["Query" + (iQ + 1)].Rows[0]["ConnTargetA"].Equals(sOldID))
                                {
                                    Match mcTable = Regex.Match(sListQueries[iQ], SHSRegex.REGEX_STARTQUERY);
                                    if (Regex.Match(mcTable.Value, "^\\[\\d+\\]").Success)
                                    {
                                        string sTable = mcTable.Value[..(mcTable.Value.IndexOf("]") + 1)].Replace(sOldID, sNewConnId);
                                        string sNewQuery = sListQueries[iQ].Replace(mcTable.Value[..(mcTable.Value.IndexOf("]") + 1)], sTable);
                                        sListQueries[iQ] = sNewQuery;
                                    }
                                }
                            }
                            if (dsJob.Tables["Query" + (iQ + 1)].Columns.Contains("ConnTargetB")) //multi-target
                            {
                                if (dsJob.Tables["Query" + (iQ + 1)].Rows[0]["ConnTargetB"].Equals(sOldID))
                                {
                                    Match mcTable = Regex.Match(sListQueries[iQ], SHSRegex.REGEX_STARTQUERY);
                                    string sTable = mcTable.Value[..(mcTable.Value.IndexOf(":") + 1)].Replace(sOldID, sNewConnId);
                                    string sNewQuery = sListQueries[iQ].Replace(mcTable.Value[..(mcTable.Value.IndexOf(":") + 1)], sTable);
                                    sListQueries[iQ] = sNewQuery;
                                }
                            }
                            for (int iCC = 0; iCC < dsJob.Tables["Query" + (iQ + 1)].Columns.Count; iCC++)
                            {
                                if (dsJob.Tables["Query" + (iQ + 1)].Columns.Contains("ConnCrossJoinQuery" + (iCC + 1).ToString()))
                                {
                                    if (dsJob.Tables["Query" + (iQ + 1)].Rows[0]["ConnCrossJoinQuery" + (iCC + 1).ToString()].Equals(sOldID))
                                    {
                                        MatchCollection mcCross = Regex.Matches(sListQueries[iQ], SHSRegex.REGEX_CROSSJOINSCRIPT_B);
                                        foreach (Match mc in mcCross)
                                        {
                                            string sCC = mc.Value.Replace(sOldID, sNewConnId);
                                            string sNewQuery = sListQueries[iQ].Replace(mc.Value, sCC);
                                            sListQueries[iQ] = sNewQuery;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                try
                {
                    string sNewJobID = GetNewJobID();
                    NewJob = new Job(GlobalParameters, sNewJobID, USER, false, drJob);
                    sErrorsImport = NewJob.LoadErrors;
                    NewJob.JobQueries = Job.ExtractQueriesFromString(NewJob, string.Join(Environment.NewLine, sListQueries));
                }
                catch (Exception)
                {
                    //en cas de merdouille, on vire toutes les connexions crées
                    foreach (string sC in sListNewConn)
                    {
                        Connections.RemoveCONNString(sC);
                    }
                    throw;
                }
            }

            return NewJob;
        }

        public static bool CheckUserExists(string sUser)
        {
            bool bExists;
            DataSet dsData = new();

            try
            {
                //check si nom existe déjà
                string sQueryCount = "SELECT COUNT(*) FROM user_parameters WHERE \"user\" = '" + sUser + "';";
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQueryCount, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Existing User");
                }

                string sCount = SQLTools.BuildStringFromDs(dsData);
                if (sCount.Equals("0"))
                {
                    bExists = false;
                }
                else { bExists = true; }
            }
            catch (Exception) { throw; }

            return bExists;
        }

        public static string GetUsersAndJobs()
        {
            StringBuilder sbUsersJobs = new();
            sbUsersJobs.AppendLine("LICENSE DETAILS : ");
            sbUsersJobs.AppendLine(Environment.NewLine);

            string sQuery = string.Concat("SELECT sp.user, COUNT(DISTINCT(cs.connstring_id)) as nb_connection, COUNT(DISTINCT(jq.query_id)) as nb_queries, COUNT(DISTINCT(jp.job_id)) as nb_jobs " +
                                          "FROM \"user_parameters\" as sp " +
                                          "INNER JOIN \"user_connstrings\" as cs ON sp.user = cs.user " +
                                          "INNER JOIN \"job_queries\" as jq ON sp.user = jq.user " +
                                          "INNER JOIN \"job_parameters\" as jp ON sp.user = jp.user " +
                                          "GROUP BY sp.user;");
            DataSet dsData = new();

            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Jobs");
            }

            foreach (DataRow dr in dsData.Tables[0].Rows)
            {
                sbUsersJobs.AppendLine(string.Concat("-> ", dr[0].ToString(), " - Jobs : ", dr[3].ToString(), ", Queries : ", dr[2].ToString(), ", Connections : ", dr[1].ToString()));
            }

            return sbUsersJobs.ToString();
        }

        public static int GetFullJobsList()
        {
            string sQuery = string.Concat("SELECT COUNT(*) as nb_jobs FROM \"job_parameters\";");
            DataSet dsData = new();

            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Jobs");
            }
            return Convert.ToInt32(SQLTools.BuildStringFromDs(dsData));
        }

        public static int GetFullConnList()
        {
            string sQuery = string.Concat("SELECT COUNT(*) as nb_connections FROM \"user_connstrings\";");
            DataSet dsData = new();

            using (SqliteConnection sqlConn = new(INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Connections");
            }
            return Convert.ToInt32(SQLTools.BuildStringFromDs(dsData));
        }

        public static List<string> LoadProgramUsers(string sWithMine)
        {
            List<string> sUsers = new();

            try
            {
                DataSet dsData = new();

                string sQuery = string.Concat("SELECT \"user\" FROM user_parameters", sWithMine.Length == 0 ? ";" : (" WHERE \"user\" != '" + sWithMine + "';"));
                using (SqliteConnection sqlConn = new(INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Job Queries");
                }
                sUsers = SQLTools.BuildListFromDs(dsData);
            }
            catch (Exception ex)
            {
                sUsers.Add(Languages.Languages.par_loadusersko + ex.Message);
            }

            return sUsers;
        }

        internal class DownloadFile
        {
            private volatile bool _completed;
            public string DownloadStatus = "";

            public void Download(string address, string location)
            {
                WebClient client = new();
                Uri Uri = new(address);

                _completed = false;

                client.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);

                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgress);
                client.DownloadFileAsync(Uri, location);

            }

            public bool DownloadCompleted
            {
                get
                {
                    return _completed;
                }
            }

            private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
            {
                // Displays the operation identifier, and the transfer progress.
                //DownloadStatus = string.Concat("{0}    downloaded {1} of {2} bytes. {3} % complete...",
                //    (string)e.UserState,
                //    e.BytesReceived,
                //    e.TotalBytesToReceive,
                //    e.ProgressPercentage);
                DownloadStatus = string.Concat(Languages.Languages.msg_downloadingfile, e.ProgressPercentage.ToString(), " %");
            }

            private void Completed(object sender, AsyncCompletedEventArgs e)
            {
                if (e.Cancelled == true)
                {
                    LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Download has been canceled.", SQLTools_Enums.LOG_TYPEINFO.WNG, true);
                }
                else
                {
                    LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Download completed.", SQLTools_Enums.LOG_TYPEINFO.INF, true);
                }

                _completed = true;
            }
        }


        #endregion

        #region "PRIVATE VOID"

        private static void CreateMissingColumns()
        {
            //check column 15/03/2021
            DataSet dsDataJob = new();
            string sQuery = "SELECT * FROM 'job_parameters' LIMIT 1;";
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsDataJob, "JobMissingColumn");
            }

            //check column 02/04/2021
            DataSet dsDataUser = new();
            string sQuery2 = "SELECT * FROM 'user_parameters' LIMIT 1;";
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery2, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsDataUser, "UserMissingColumn");
            }

            //check column 02/04/2021
            DataSet dsDataService = new();
            string sQuery3 = "SELECT * FROM 'service_parameters' LIMIT 1;";
            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery3, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsDataService, "ServiceMissingColumn");
            }

            if (dsDataService != null && dsDataService.Tables.Count > 0)
            {
                if (!dsDataService.Tables[0].Columns.Contains("send_job_report"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN send_job_report TEXT NOT NULL DEFAULT 'NONE';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataService.Tables[0].Columns.Contains("send_job_report_hour"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN send_job_report_hour TEXT NOT NULL DEFAULT '8';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataService.Tables[0].Columns.Contains("send_job_report_ampm"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN send_job_report_ampm TEXT NOT NULL DEFAULT 'AM';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataService.Tables[0].Columns.Contains("send_job_report_recipients"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN send_job_report_recipients TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataService.Tables[0].Columns.Contains("kill_running_jobs_after"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN kill_running_jobs_after TEXT NOT NULL DEFAULT '8';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataService.Tables[0].Columns.Contains("sql_connexion_string_schema"))
                {
                    string sQCreate = "ALTER TABLE service_parameters ADD COLUMN sql_connexion_string_schema TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
            }

            if (dsDataJob != null && dsDataJob.Tables.Count > 0)
            {
                if (!dsDataJob.Tables[0].Columns.Contains("job_family"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN job_family TEXT NOT NULL DEFAULT 'DEFAULT';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("job_version"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN job_version TEXT NOT NULL DEFAULT 'V1.0.0';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("allow_alter_column_target_onlysafe"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN allow_alter_column_target_onlysafe INTEGER NOT NULL DEFAULT 1;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("no_sourcedata_noerror"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN no_sourcedata_noerror INTEGER NOT NULL DEFAULT 1;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("webservice_special_http_parameters"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN webservice_special_http_parameters TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("post_job_sql_command_source_conn"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN post_job_sql_command_source_conn TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("post_job_sql_command_target_conn"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN post_job_sql_command_target_conn TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("xls_write_interpret_formulas"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN xls_write_interpret_formulas INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("ws_content_structure"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN ws_content_structure TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("xls_row_write_offset"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN xls_row_write_offset INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("mb_target_html_file_template"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN mb_target_html_file_template TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("mb_target_html_dataset_keyword"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN mb_target_html_dataset_keyword TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("prepost_commands_loopjobthroughresults"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN prepost_commands_loopjobthroughresults INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("data_transform_cross_queries"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN data_transform_cross_queries INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("target_add_dbname"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN target_add_dbname TEXT NOT NULL DEFAULT 'DBNAME';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("target_add_dtload"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN target_add_dtload TEXT NOT NULL DEFAULT 'DTLOAD';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("target_add_rownum"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN target_add_rownum TEXT NOT NULL DEFAULT 'ROWNUM';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }

                if (!dsDataJob.Tables[0].Columns.Contains("auto_sql_table_creation"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN auto_sql_table_creation INTEGER NOT NULL DEFAULT 1;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("csv_add_quotes"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN csv_add_quotes INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("ws_raw_output"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN ws_raw_output INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("file_raw_output"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN file_raw_output INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("ws_sql_language"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN ws_sql_language TEXT NOT NULL DEFAULT 'FUZIBLE_SQL';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("sql_directstream_copy"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN sql_directstream_copy INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("sql_trust_target_columns"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN sql_trust_target_columns INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("sql_directstream_priority"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN sql_directstream_priority INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("sql_target_bulk_copy"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN sql_target_bulk_copy INTEGER NOT NULL DEFAULT 0;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("json_header"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN json_header TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("api_get_typedata"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN api_get_typedata TEXT NOT NULL DEFAULT 'NOTHING';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("api_source_postwork"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN api_source_postwork TEXT NOT NULL DEFAULT 'NOTHING';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("subjob_abort_on_nodata"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN subjob_abort_on_nodata INTEGER NOT NULL DEFAULT '1';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("query_retries"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN query_retries INTEGER NOT NULL DEFAULT '0';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataJob.Tables[0].Columns.Contains("csv_encoding_target"))
                {
                    string sQCreate = "ALTER TABLE job_parameters ADD COLUMN csv_encoding_target TEXT NOT NULL DEFAULT 'UTF8';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("show_live_help_start"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN show_live_help_start INTEGER NOT NULL DEFAULT '1';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("ws_soql_records_only"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN ws_soql_records_only INTEGER NOT NULL DEFAULT '1';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("sql_directstream_commit"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN sql_directstream_commit INTEGER NOT NULL DEFAULT '1000';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("security_shared_users"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN security_shared_users INTEGER NOT NULL DEFAULT '0';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("security_principal_user"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN security_principal_user TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("multithreading_cores_bigdata"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN multithreading_cores_bigdata INTEGER NOT NULL DEFAULT '2';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("log_debug_functions"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN log_debug_functions INTEGER NOT NULL DEFAULT '0';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("sql_bulk_rate"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN sql_bulk_rate INTEGER NOT NULL DEFAULT '1000';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("user_password"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN user_password TEXT;";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("json_alternative_processing_mode"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN json_alternative_processing_mode INTEGER NOT NULL DEFAULT '0';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                if (!dsDataUser.Tables[0].Columns.Contains("connexion_string_sqllog_schema"))
                {
                    string sQCreate = "ALTER TABLE user_parameters ADD COLUMN connexion_string_sqllog_schema TEXT NOT NULL DEFAULT '';";
                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQCreate, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        private void UpdateJobExecution(Job INIP, List<string> sListDynParams, DateTime dtEnd, string sStatus, bool bIsReadOnly)
        {
            if (!INIP.JobID.Equals("[0]")) //job temporaire, on ne met rien à jour puisqu'il n'y a rien à mettre à jour
            {
                StringBuilder sbQuery = new();
                INIP.DynParams = sListDynParams;
                INIP.JobLastLaunchDate = DateTime.Now;
                INIP.JobLastLaunchStatus = sStatus;

                string sReadWriteDB = INTERNAL_DB;
                if (bIsReadOnly)
                {
                    //sReadWriteDB = INTERNAL_DB.Replace("Read Only=true", "");
                    sStatus = string.Concat(sStatus, " - USER : ", Environment.UserName.ToUpper());
                }

                sbQuery.Append("UPDATE job_parameters SET ");
                //sbQuery.Append(string.Concat("\"source_queries_dynamic_parameters\"=", "'", (sListDynParams.Count > 0 ? string.Join(";", sListDynParams.ToArray()).Replace("'", "''") : ""), "',"));
                sbQuery.Append(string.Concat("\"job_lastlaunch_date\"=", "'", dtEnd.ToString(CultureInfo.CurrentCulture), "',"));
                sbQuery.Append(string.Concat("\"job_lastlaunch_status\"=", "'", sStatus.Replace("'", "''"), "'"));
                sbQuery.Append(" WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + INIP.JobID + "';");

                using SqliteConnection sqlConn = new(sReadWriteDB);
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
        }

        private void SaveVersion(Job INIP)
        {
            if (!INIP.JobID.Equals("[0]")) //job temporaire, on ne met rien à jour puisqu'il n'y a rien à mettre à jour
            {
                try
                {
                    StringBuilder sbQuery = new StringBuilder();
                    sbQuery.Append("UPDATE \"job_parameters\" SET \"job_version\" = '" + INIP.JobVersionWithUser + "'");
                    sbQuery.Append(" WHERE \"user\" = '" + USER + "' AND \"job_id\" = '" + INIP.JobID + "';");
                    using SqliteConnection sqlConn = new(INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                catch
                {
                    throw;
                }
            }
        }
    }

    #endregion
}
