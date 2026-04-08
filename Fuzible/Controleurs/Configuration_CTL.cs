using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fuzible.Controleurs
{
    internal class Configuration_CTL
    {
        private readonly string sUsername;
        private readonly INIProgram INIFile;
        private LogTools LOG;
        private readonly ClientApp ClientParameters;

        internal readonly ServiceApp CSParameters;
        internal readonly MAINParameters MainParams;

        public Configuration_CTL(string sUsername)
        {
            this.sUsername = sUsername;
            INIFile = new INIProgram(sUsername, false, false);
            MainParams = INIFile.GlobalParameters;
            LOG = new LogTools(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, true);
            CSParameters = new ServiceApp(INIFile);
            ClientParameters = new ClientApp(false);
        }

        internal static List<string> GetUsers()
        {
            return INIProgram.LoadProgramUsers("");
        }

        internal string CreateStackTable(string sConnString, string sDriver, string sSchema)
        {
            if (sConnString.Length > 0)
            {

                var BDDService = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
                List<string> sListSQLParams = SQLQueries.ParamsForSQLDriver(BDDService, sSchema);

                if (sSchema.Length > 0 && sListSQLParams.Count > 0 && sListSQLParams.Any(s => s.StartsWith("P_DEFAULT_SCHEMA=")))
                {
                    string sToRemove = sListSQLParams.FirstOrDefault(s => s.StartsWith("P_DEFAULT_SCHEMA="));
                    sListSQLParams.Remove(sToRemove);
                    sListSQLParams.Add("P_DEFAULT_SCHEMA=" + sSchema);
                }

                CONNString sConn = new(BDDService, "[0]", "", sConnString, sListSQLParams);

                Job INIP = new(INIFile.GlobalParameters, "[0]", CSParameters.SUsername, false);
                INIP.SetJobPassword("SHS", "", true);
                INIP.ConnectionString_Target = sConn;
                SQLTools SQLConn = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref LOG);

                return CSParameters.CreateServiceAppTables(SQLConn, true);
            }
            else
            {
                return Languages.Languages.mc_msg_mustsetconnection;
            }
        }

        internal string CreateLogTables(string sConnString, string sDriver, string sSchema)
        {
            if (sConnString.Length > 0)
            {
                var BDDLog = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
                List<string> sListSQLParams = SQLQueries.ParamsForSQLDriver(BDDLog, sSchema);
                
                if (sSchema.Length > 0 && sListSQLParams.Count > 0 && sListSQLParams.Any(s => s.StartsWith("P_DEFAULT_SCHEMA=")))
                {
                    string sToRemove = sListSQLParams.FirstOrDefault(s => s.StartsWith("P_DEFAULT_SCHEMA="));
                    sListSQLParams.Remove(sToRemove);
                    sListSQLParams.Add("P_DEFAULT_SCHEMA=" + sSchema);
                }

                CONNString sConn = new(BDDLog, "[0]", "", sConnString, sListSQLParams);

                Job INIP = new(INIFile.GlobalParameters, "[0]", INIFile.USER, false);
                INIP.SetJobPassword("SHS", "", true);
                INIP.ConnectionString_Target = sConn;
                SQLTools SQLConn = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref LOG);

                return ServiceApp.CreateLogTables(SQLConn, true);
            }
            else { return Languages.Languages.mc_msg_mustsetconnection; }
        }

        internal CONNString GetConnection(string sConnID)
        {
            return INIFile.Connections.GetConnByID(sConnID);
        }

        internal void SetClientParams(string sBDD, string sConnString, string sSchema, string sUserJobs)
        {
            ClientParameters.BDDDriverClientApp = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sBDD);
            ClientParameters.SQLConnexionStringClientAppSchema = sSchema;
            ClientParameters.SQLConnexionStringClientApp = sConnString;
            ClientParameters.FuzibleUserJobs = sUserJobs;
            ClientParameters.FuzibleName = INIProgram.APP_NAME;
            ClientParameters.Language = INIFile.GlobalParameters.APP_LANGUAGE;
        }

        internal void SaveMainConfig(List<object> sParameters)
        {
            INIFile.GlobalParameters.SetFromConfig(sParameters);
        }

        internal string SetServiceParams(SQLTools_Enums.BDD sBDDService, int iKeepLog, int iKillIdleJobs, int iParallelJobs, int iFloodInterval, string sUserService, CONNString csService, string sSendJobReport, int iSendJobReportHour, string sSendJobReportAmPm, string sRecipientsJobbReports)
        {
            CSParameters.BDDDriverServiceApp = sBDDService;
            CSParameters.IKeepLogUntil = iKeepLog;
            CSParameters.IPurgeIdleJobsAfterHours = iKillIdleJobs;
            CSParameters.IQteParallelJobs = iParallelJobs;
            CSParameters.FloodingInterval = iFloodInterval;

            CSParameters.SUsername = sUserService;
            CSParameters.SQLConnexionStringServiceApp = csService;

            CSParameters.SendJobReport = sSendJobReport;
            CSParameters.SendJobReport_Hour = iSendJobReportHour;
            CSParameters.SendJobReport_AmPm = sSendJobReportAmPm;
            CSParameters.SendJobReport_Recipients = sRecipientsJobbReports;

            CSParameters.SaveServiceParameters(INIFile.USER);
            ClientParameters.SaveClientINI();

            string sError = "";
            bool bOK = CSParameters.SaveJobToClient(INIFile.UserJobsList, LOG);
            if (!bOK) { sError = LOG.LogEvents[^1].SMessage; }

            return sError;
        }

        internal bool CheckChangeService(SQLTools_Enums.BDD sBDDService, string sUserService, string sConnString, string sConnSchema, int iTest)
        {
            switch (iTest)
            {
                case 0:
                    if (sBDDService != CSParameters.BDDDriverServiceApp || sUserService != CSParameters.SUsername || sConnSchema != CSParameters.BDDDriverDefaultSchema || sConnString != CSParameters.SQLConnexionStringServiceApp.SConnString(null))
                    {
                        return true;
                    }
                    else { return false; }
                case 1:
                    if (sUserService != CSParameters.SUsername && sConnSchema == CSParameters.BDDDriverDefaultSchema && sConnString == CSParameters.SQLConnexionStringServiceApp.SConnString(null))
                    {
                        return true;
                    }
                    else { return false; }
                default:
                    return false;
            }
        }

        internal string MigrateServiceConfiguration(CONNString csService, string sUserService)
        {
            return CSParameters.MigrateServiceData(csService, sUserService);
        }

        internal async static Task<string> CheckGenericConnection(string sDriver, string sConnString)
        {
            var sBDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
            List<string> sConnVars = SQLQueries.ParamsForSQLDriver(sBDD, "");
            CONNString sConn = new(sBDD, "[0]", "", sConnString, sConnVars);
            List<string> sAnswers = await Toolbox.CheckConnection(sConn, new List<string>(), "", Monitoring.TaskCancellationToken);
            return sAnswers[0];
        }

        internal async static Task<string> CheckMailConnection(string sConnString, bool bSSL, string sAuthProtocol)
        {
            List<string> sListParams = new()
            {
                string.Concat("P_MAIL_USE_SSL=", bSSL.ToString()),
                //string.Concat("P_MAIL_PROTOCOL=POP"),
                string.Concat("P_MAIL_AUTH_PROTOCOL=", sAuthProtocol)
            };

            CONNString sConn = new(SQLTools_Enums.BDD.MB_MAIL, "[0]", "", sConnString, sListParams);
            List<string> sAnswers = await Toolbox.CheckConnection(sConn, new List<string>(), "", Monitoring.TaskCancellationToken);
            return sAnswers[0];
        }

        internal int GetDefaultSQLPort(SQLTools_Enums.BDD sDriver)
        {
            switch (sDriver)
            {
                case SQLTools_Enums.BDD.NS_MONGODB:
                   return INIFile.GlobalParameters.DEFAULT_MONGODB_PORT;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    return 1433;
                case SQLTools_Enums.BDD.DB_MYSQL:
                   return INIFile.GlobalParameters.DEFAULT_MYSQL_PORT;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                   return INIFile.GlobalParameters.DEFAULT_POSTGRES_PORT;
                case SQLTools_Enums.BDD.DB_ORACLE:
                   return INIFile.GlobalParameters.DEFAULT_ORACLE_PORT;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                   return INIFile.GlobalParameters.DEFAULT_MSSQL_PORT;
                default:
                    return 1433;
            }
        }

        internal string CreateNewConnection(string sDriver, string sConnName, string sConnString, List<string> sConnParams)
        {
            return INIFile.Connections.AddCONNString((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver), sConnName, sConnString, sConnParams);
        }

        internal string UpdateConnection(string sConnID, string sConnName, string sConnString, string sDriver, List<string> sConnParams)
        {
            return INIFile.Connections.ModifyCONNString(sConnID, sConnName, sConnString, (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver), sConnParams);
        }

        internal List<CONNString> GetConnections()
        {
            return INIFile.Connections.CSList;
        }

        internal List<string> CheckConnStringUsage(string sConnID)
        {
            return INIFile.Connections.CheckConnStringUsage(sConnID);
        }

        internal string DeleteConnection(string sConnID)
        {
            return INIFile.Connections.RemoveCONNString(sConnID);
        }
    }
}
