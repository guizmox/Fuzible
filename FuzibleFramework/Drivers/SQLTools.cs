
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FuzibleFramework
{
    public class SQLTools
    {
        #region "CONSTANTES"

        private int MIN_ROWS_SQLDIRECTSTREAM_FIRSTPASS = 100000;
        private int MIN_ROWS_SQLDIRECTSTREAM_ALLPASS = 1000;
        private DateTime STREAMING_MODE_START_DATE;
        private int STREAMING_MODE_SLEEP_RATE = 1000; //streaming mode : pause toutes les combien de lignes pour rattraper les retards
        private bool STREAMING_MODE_BUSY = false;
        private int STREAMING_MODE_SLEEP_TIME = 0;
        private int DEFAULT_COMMAND_TIMEOUT = 30;

        #endregion

        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose
        {
            get;
        }
        public Job JobParameters;
        public CONNString Connection;
        public LogTools MyLog;

        public string EchappementChar
        {
            get
            {
                return ClassPurpose switch
                {
                    SQLTools_Enums.CLASS_PURPOSE.SRC => Connection.SqlEchappementChar,
                    SQLTools_Enums.CLASS_PURPOSE.TRG => Connection.SqlEchappementChar,
                    SQLTools_Enums.CLASS_PURPOSE.LOG => Connection.SqlEchappementChar,
                    SQLTools_Enums.CLASS_PURPOSE.PRG => Connection.SqlEchappementChar,
                    _ => Connection.SqlEchappementChar,
                };
            }
        }

        #endregion

        #region "PROPRIETES"

        public SQLTools_Enums.CHARACTER_SET CharacterSet { get; set; } = SQLTools_Enums.CHARACTER_SET.utf8;
        public SQLTools_Enums.TYPE_DATA SqlDateTypeCompatibility
        {
            get
            {
                return ClassPurpose switch
                {
                    SQLTools_Enums.CLASS_PURPOSE.SRC => Connection.SqlDateTypeCompatibility,
                    SQLTools_Enums.CLASS_PURPOSE.TRG => Connection.SqlDateTypeCompatibility,
                    SQLTools_Enums.CLASS_PURPOSE.LOG => SQLTools_Enums.TYPE_DATA.DATETIME,
                    SQLTools_Enums.CLASS_PURPOSE.PRG => SQLTools_Enums.TYPE_DATA.DATETIME,
                    _ => SQLTools_Enums.TYPE_DATA.DATETIME,
                };
            }
        }
        public SQLTools_Enums.TYPE_DATA SqlCharTypeCompatibility
        {
            get
            {
                return ClassPurpose switch
                {
                    SQLTools_Enums.CLASS_PURPOSE.SRC => Connection.SqlCharTypeCompatibility,
                    SQLTools_Enums.CLASS_PURPOSE.TRG => Connection.SqlCharTypeCompatibility,
                    SQLTools_Enums.CLASS_PURPOSE.LOG => SQLTools_Enums.TYPE_DATA.VARCHAR,
                    SQLTools_Enums.CLASS_PURPOSE.PRG => SQLTools_Enums.TYPE_DATA.VARCHAR,
                    _ => SQLTools_Enums.TYPE_DATA.VARCHAR,
                };
            }
        }

        private string DatabaseName { get; } = "";

        private string DefaultSchema { get; } = "public";
        public int ConnexionState { get; private set; } = 0;
        public SqlConnection ConnexionSQLServer
        {
            get; private set;
        }
        public MySqlConnection ConnexionMySQL
        {
            get; private set;
        }
        public OdbcConnection ConnexionODBC
        {
            get; private set;
        }
        public OracleConnection ConnexionORACLE
        {
            get; private set;
        }
        public NpgsqlConnection ConnexionPOSTGRE
        {
            get; private set;
        }
        public SqliteConnection ConnexionSqlite
        {
            get; private set;
        }
        public OleDbConnection ConnexionACCESS
        {
            get; private set;
        }

        #endregion

        #region "PUBLIC VOID"

        public SQLTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            JobParameters = INIP;
            MyLog = LogJob;

            switch (JobParameters.SQLDirectStreamPriority)
            {
                case 0:
                    STREAMING_MODE_SLEEP_RATE = 5000; //priorité lecture
                    MIN_ROWS_SQLDIRECTSTREAM_ALLPASS = 5000;
                    break;
                case 1:
                    STREAMING_MODE_SLEEP_RATE = 2500; //priorité écriture
                    MIN_ROWS_SQLDIRECTSTREAM_ALLPASS = 2500;
                    break;
                case 2:
                    STREAMING_MODE_SLEEP_RATE = 1000; //stabilité
                    MIN_ROWS_SQLDIRECTSTREAM_ALLPASS = 1000;
                    break;
            }

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.SRC: //SOURCE
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.SRC;
                    Connection = INIP.ConnectionString_Source;
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    DefaultSchema = ExtractDefaultSchemaFromDBString(Connection.SConnDriver, INIP.ConnectionString_Source.SConnString(INIP.DynParams), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG: //TARGET
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.TRG;
                    Connection = INIP.ConnectionString_Target;
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Target, INIP.DynParams);
                    DefaultSchema = ExtractDefaultSchemaFromDBString(Connection.SConnDriver, INIP.ConnectionString_Target.SConnString(INIP.DynParams), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.LOG: //LOG
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.LOG;
                    List<string> sListParams;
                    sListParams = SQLQueries.ParamsForSQLDriver(JobParameters.GlobalParameters.LOG_BDDDRIVER, JobParameters.GlobalParameters.LOG_CONNECTIONSTRINGSCHEMA);
                    Connection = new CONNString(JobParameters.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, sListParams) { };
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(Toolbox.GetDatabaseNameFromConnectionString(JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, JobParameters.GlobalParameters.LOG_BDDDRIVER), INIP.DynParams);
                    DefaultSchema = ExtractDefaultSchemaFromDBString(Connection.SConnDriver, JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
                    break;
            }

            if (Connection.SConnParams.Count == 0)
            {
                Connection.SConnParams = SQLQueries.ParamsForSQLDriver(Connection.SConnDriver, "");
            }

            if (DatabaseName.Length == 0)
            {
                DatabaseName = Toolbox.GetDatabaseNameFromConnectionString(Connection.SConnString(INIP.DynParams), Connection.SConnDriver);
            }

        }


        [System.Runtime.InteropServices.DllImport("odbccp32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SQLGetInstalledDriversW(char[] lpszBuf, ushort cbufMax, out ushort pcbBufOut);

        public static string[] GetOdbcDriverNames()
        {
            string[] odbcDriverNames = null;
            char[] driverNamesBuffer = new char[ushort.MaxValue];

            bool succeeded = SQLGetInstalledDriversW(driverNamesBuffer, ushort.MaxValue, out ushort size);

            if (succeeded == true)
            {
                char[] driverNames = new char[size - 1];
                Array.Copy(driverNamesBuffer, driverNames, size - 1);
                odbcDriverNames = new string(driverNames).Split('\0');
            }

            return odbcDriverNames;
        }

        public static async Task<List<string>> CheckConnection(CONNString CS, string sDBName, bool bWithDBList, int iCommandTimeout, List<string> sListDynParams)
        {
            DataSet dsData = new();
            List<string> sDB = new();
            string sCState;
            string sC = CS.SConnString(null);


            //if (sC.Contains("Authentication=", StringComparison.OrdinalIgnoreCase))
            //{
            //    SQLServerAzureAuthentication = true;
            //    sC = Regex.Replace(sC, "(.+)(Authentication=.+[^;];?)(.*)", "$1$3", RegexOptions.IgnoreCase);
            //}

            if (sDBName.Length > 0)
            {
                sC = Toolbox.ReplaceDBNameInConnectionString(CS, sDBName, sListDynParams);
            }

            switch (CS.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    try
                    {
                        OleDbConnection ConnexionAccess;
                        ConnexionAccess = new OleDbConnection(sC);

                        await ConnexionAccess.OpenAsync();

                        sCState = ConnexionAccess.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        //if (bWithDBList)
                        //{
                        //    OleDbCommand CommandeAccess = new OleDbCommand("SHOW DATABASES;", ConnexionMySQL) { CommandTimeout = iCommandTimeout };
                        //    MySqlDataAdapter Adaptateur = new MySqlDataAdapter(CommandeSQLMS);
                        //    try
                        //    { Adaptateur.Fill(dsData); }
                        //    catch (Exception ex)
                        //    { sDB.Add("Can't get databases list : " + ex.Message); }
                        //}
                        await ConnexionAccess.CloseAsync();
                    }
                    catch (OleDbException ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_MYSQL:
                    try
                    {
                        MySqlConnection ConnexionMySQL;
                        ConnexionMySQL = new MySqlConnection(sC);
                        await ConnexionMySQL.OpenAsync(Monitoring.TaskCancellationToken);
                        sCState = ConnexionMySQL.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        if (bWithDBList)
                        {
                            MySqlCommand CommandeSQLMS = new("SHOW DATABASES;", ConnexionMySQL) { CommandTimeout = iCommandTimeout };
                            MySqlDataAdapter Adaptateur = new(CommandeSQLMS);
                            try
                            {
                                MySqlDataReader reader = await CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                                DataTable dt = new(sDBName);
                                dt.Load(reader);
                                dsData.Tables.Add(dt);
                            }
                            catch (Exception ex)
                            {
                                sDB.Add(Languages.Languages.sql_connstatus_cantgetdblist + ex.Message);
                            }
                        }
                        await ConnexionMySQL.CloseAsync();
                    }
                    catch (MySqlException ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    try
                    {
                        SqlConnection ConnexionSQLServer;
                        ConnexionSQLServer = new SqlConnection(sC);

                        await ConnexionSQLServer.OpenAsync(Monitoring.TaskCancellationToken);

                        sCState = ConnexionSQLServer.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        if (bWithDBList)
                        {
                            SqlCommand CommandeSQLMS = new("EXEC sp_databases;", ConnexionSQLServer) { CommandTimeout = iCommandTimeout };
                            try
                            {
                                SqlDataReader reader = await CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                                DataTable dt = new(sDBName);
                                dt.Load(reader);
                                dsData.Tables.Add(dt);

                            }
                            catch (Exception ex)
                            {
                                sDB.Add(Languages.Languages.sql_connstatus_cantgetdblist + ex.Message);
                            }
                        }
                        await ConnexionSQLServer.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_SQLITE:
                    try
                    {
                        SqliteConnection ConnexionSqlite;
                        ConnexionSqlite = new SqliteConnection(sC);
                        await ConnexionSqlite.OpenAsync(Monitoring.TaskCancellationToken);
                        sCState = ConnexionSqlite.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        if (bWithDBList)
                        {
                            SqliteCommand CommandeSqlite = new("SELECT \"name\", \"seq\", \"file\" from pragma_database_list;", ConnexionSqlite) { CommandTimeout = iCommandTimeout };
                            try
                            {
                                System.Data.Common.DbDataReader reader = await CommandeSqlite.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                                DataTable dt = new(sDBName);
                                dt.Load(reader);
                                dsData.Tables.Add(dt);
                            }
                            catch (Exception ex)
                            {
                                sDB.Add(Languages.Languages.sql_connstatus_cantgetdblist + ex.Message);
                            }
                        }
                        await ConnexionSqlite.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_ODBC:
                    try
                    {
                        OdbcConnection ConnexionODBC;
                        ConnexionODBC = new OdbcConnection(sC);
                        await ConnexionODBC.OpenAsync(Monitoring.TaskCancellationToken);
                        sCState = ConnexionODBC.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        await ConnexionODBC.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_POSTGRE:
                    try
                    {
                        NpgsqlConnection ConnexionPOSTGRE;
                        ConnexionPOSTGRE = new NpgsqlConnection(sC);
                        await ConnexionPOSTGRE.OpenAsync(Monitoring.TaskCancellationToken);
                        sCState = ConnexionPOSTGRE.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        if (bWithDBList)
                        {
                            NpgsqlCommand CommandeSQLMS = new("SELECT datname FROM pg_database WHERE datistemplate = false;", ConnexionPOSTGRE) { CommandTimeout = iCommandTimeout };
                            try
                            {
                                System.Data.Common.DbDataReader reader = await CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                                DataTable dt = new(sDBName);
                                dt.Load(reader);
                                dsData.Tables.Add(dt);
                            }
                            catch (Exception ex)
                            {
                                sDB.Add(Languages.Languages.sql_connstatus_cantgetdblist + ex.Message);
                            }
                        }
                        await ConnexionPOSTGRE.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;

                case SQLTools_Enums.BDD.DB_ORACLE:
                    try
                    {
                        OracleConnection ConnexionORACLE;
                        ConnexionORACLE = new OracleConnection(sC);
                        await ConnexionORACLE.OpenAsync(Monitoring.TaskCancellationToken);
                        sCState = ConnexionORACLE.State.ToString();
                        sDB.Add(Languages.Languages.sql_connstatus + sCState);
                        if (bWithDBList)
                        {
                            OracleCommand CommandeSQLMS = new("SELECT TABLESPACE_NAME FROM USER_TABLESPACES", ConnexionORACLE) { CommandTimeout = iCommandTimeout };
                            try
                            {
                                System.Data.Common.DbDataReader reader = await CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                                DataTable dt = new(sDBName);
                                dt.Load(reader);
                                dsData.Tables.Add(dt);
                            }
                            catch (Exception ex)
                            {
                                sDB.Add(Languages.Languages.sql_connstatus_cantgetdblist + ex.Message);
                            }
                        }
                        await ConnexionORACLE.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        sDB.Add(Languages.Languages.sql_connstatus + ex.Message);
                    }
                    break;
            }

            if (bWithDBList)
            {
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0) //ajout des DB si demandé
                {
                    if (sDB.Count > 0) { sDB[0] = string.Concat(sDB[0], " (", dsData.Tables[0].Rows.Count.ToString(), " databases)"); }
                    foreach (DataRow dR in dsData.Tables[0].Rows) { sDB.Add(dR[0].ToString()); }
                }
            }

            return sDB;
        }

        public bool QuickConnectionCheck()
        {
            string sC = Connection.SConnString(null);

            bool bChanged = false;
            string sNew = Regex.Replace(sC, "(Timeout=)([0-9]+)", "$1" + "5", RegexOptions.IgnoreCase);
            if (!sNew.Equals(sC)) { bChanged = true; }
            sNew = Regex.Replace(sC, "(Timeout=)([0-9]+)", "$1" + "5", RegexOptions.IgnoreCase);
            if (!sNew.Equals(sC)) { bChanged = true; }
            sNew = Regex.Replace(sC, "(Timeout=)([0-9]+)", "$1" + "5", RegexOptions.IgnoreCase);
            if (!sNew.Equals(sC)) { bChanged = true; }

            if (!bChanged) //non spécifié dans la chaîne
            {
                if (!sC.EndsWith(";")) { sC = sC + ";"; }

                switch (Connection.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sC = string.Concat(sC, "Connection Timeout=5");
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sC = string.Concat(sC, "Connection Timeout=5");
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sC = string.Concat(sC, "Timeout=5");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sC = string.Concat(sC, "Connect Timeout=5");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        break;
                }
            }

            ConnectionState cState = ConnectionState.Closed;

            switch (Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    try
                    {
                        OleDbConnection ConnexionAccess;
                        ConnexionAccess = new OleDbConnection(sC);
                        ConnexionAccess.Open();

                        cState = ConnexionAccess.State;

                        ConnexionAccess.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_MYSQL:
                    try
                    {
                        MySqlConnection ConnexionMySQL;
                        ConnexionMySQL = new MySqlConnection(sC);
                        ConnexionMySQL.Open();

                        cState = ConnexionMySQL.State;

                        ConnexionMySQL.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    try
                    {
                        SqlConnection ConnexionSQLServer;
                        ConnexionSQLServer = new SqlConnection(sC);

                        ConnexionSQLServer.Open();

                        cState = ConnexionSQLServer.State;

                        ConnexionSQLServer.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_SQLITE:
                    try
                    {
                        SqliteConnection ConnexionSqlite;
                        ConnexionSqlite = new SqliteConnection(sC);
                        ConnexionSqlite.Open();

                        cState = ConnexionSqlite.State;

                        ConnexionSqlite.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_ODBC:
                    try
                    {
                        OdbcConnection ConnexionODBC;
                        ConnexionODBC = new OdbcConnection(sC);
                        ConnexionODBC.Open();

                        cState = ConnexionODBC.State;

                        ConnexionODBC.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_POSTGRE:
                    try
                    {
                        NpgsqlConnection ConnexionPOSTGRE;
                        ConnexionPOSTGRE = new NpgsqlConnection(sC);
                        ConnexionPOSTGRE.Open();

                        cState = ConnexionPOSTGRE.State;

                        ConnexionPOSTGRE.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;

                case SQLTools_Enums.BDD.DB_ORACLE:
                    try
                    {
                        OracleConnection ConnexionORACLE;
                        ConnexionORACLE = new OracleConnection(sC);
                        ConnexionORACLE.Open();

                        cState = ConnexionORACLE.State;

                        ConnexionORACLE.Close();
                    }
                    catch
                    {
                        return false;
                    }
                    break;
            }

            if (cState == ConnectionState.Open)
            { return true; }
            else { return false; }
        }

        public bool CheckForExistingTable(string sTableName, Query FuzibleQuery)
        {
            bool bExists = false;

            if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
            {
                Connect(Connection, FuzibleQuery);
                DataTable dtK = ConnexionACCESS.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                Disconnect(Connection, FuzibleQuery);
                if (dtK != null && dtK.Rows.Count > 0)
                {
                    foreach (DataRow dt in dtK.Rows)
                    {
                        if (dt[2].ToString().Equals(sTableName, StringComparison.OrdinalIgnoreCase))
                        {
                            bExists = true; break;
                        }
                    }
                }
            }
            else
            {
                DataSet dsTemp = new();

                string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHECK_TABLE_EXISTENCE);
                sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
                if (sQuery.Length > 0)
                {
                    dsTemp = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, FuzibleQuery, sTableName);
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CHECK_TABLE_EXISTENCE.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                string sResult = BuildStringFromDs(dsTemp);

                if (sResult.Equals(""))
                {
                    bExists = false;
                }
                else if (sResult.Equals("1"))
                {
                    bExists = true;
                }
                else if (sResult.Equals("0"))
                {
                    bExists = false;
                }
                else { bExists = true; } //nombre de lignes renvoyé en cas de SELECT COUNT(*)
            }

            return bExists;
        }

        public List<SQLColumn> GetKeysFromTable(string sTableName, string sTypeKey, Query FuzibleQuery)
        {
            List<SQLColumn> sListKeys = new();
            DataSet dsKeys = new();

            if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
            {
                List<SQLColumn> sqlCols = new();

                //d'abord, liste des colonnes de la table
                Connect(Connection, FuzibleQuery);
                DataTable dtCol = ConnexionACCESS.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, null);
                Disconnect(Connection, FuzibleQuery);
                if (dtCol != null && dtCol.Rows.Count > 0)
                {
                    foreach (DataRow dt in dtCol.Rows)
                    {
                        var oType = (OleDbType)dt[11];
                        SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                        try { sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), oType.ToString().ToUpper().Replace(" ", "_")); }
                        catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dt[0].ToString() + " : " + oType.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        sqlCols.Add(new SQLColumn(Convert.ToInt32(dt[6]), dt[3].ToString(), sType, Toolbox.GetSystemTypeFromListTypes(sType), dt[13].ToString(), !dt[10].ToString().Equals("false", StringComparison.OrdinalIgnoreCase), dt[8].ToString(), false, false));
                    }
                }
                //--------------ensuite, clés
                DataTable dtK = null;

                Connect(Connection, FuzibleQuery);
                if (sTypeKey.Equals("PK"))
                {
                    dtK = ConnexionACCESS.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, null);
                }
                else if (sTypeKey.Equals("FK"))
                {
                    dtK = ConnexionACCESS.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, null);
                }
                Disconnect(Connection, FuzibleQuery);

                if (dtK != null && dtK.Rows.Count > 0)
                {
                    foreach (DataRow dr in dtK.Rows)
                    {
                        if (dr[2].ToString().Equals(sTableName, StringComparison.OrdinalIgnoreCase))
                        {
                            SQLColumn SQLC = sqlCols.First(c => c.ColumnName.Equals(dr[3].ToString(), StringComparison.OrdinalIgnoreCase));
                            if (SQLC != null)
                            {
                                sListKeys.Add(new SQLColumn(Convert.ToInt32(dr[6]), dr[3].ToString(), SQLC.ColumnType, SQLC.ColumnLinqType, SQLC.ColumnSize, SQLC.ColumnAllowsNullValues, SQLC.DefaultValue, true, true));
                            }
                            else { sListKeys.Add(new SQLColumn(Convert.ToInt32(dr[6]), dr[3].ToString(), SQLTools_Enums.TYPE_DATA.TEXT, Type.GetType("System.String"), "0", false, "", true, true)); }
                        }
                    }
                }
                return sListKeys;
            }
            else
            {
                string sQuery = "";
                if (sTypeKey.Equals("PK"))
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_PRIMARY_KEY);
                }
                if (sTypeKey.Equals("FK"))
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_FOREIGN_KEYS);
                }
                sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);

                try
                {
                    if (sQuery.Length > 0)
                    {
                        dsKeys = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, FuzibleQuery, sTableName);
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.GET_PRIMARY_KEY.ToString() + ". Trying autodetection.", SQLTools_Enums.LOG_TYPEINFO.WNG);

                        //TODO deviner la PK
                        if (sTypeKey.Equals("PK"))
                        {
                            DataSet dsTemp;
                            Query sQ = new(JobParameters, "PRIMARYKEY:SELECT * FROM " + sTableName);
                            dsTemp = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQ.SQLQuery, sQ, sTableName);
                            if (dsTemp != null && dsTemp.Tables.Count > 0 && dsTemp.Tables[0].Rows.Count > 0)
                            {
                                SHSOperations SHS = new(JobParameters, sQ, ref MyLog);
                                sListKeys = SHS.GuessPKFromQuery(dsTemp.Tables[0], SQLTools_Enums.TYPE_DATA.VARCHAR, null);
                            }
                            dsTemp.Clear();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                if (dsKeys != null && dsKeys.Tables.Count > 0)
                {
                    //TODO : à compléter selon les besoins d'interprétation
                    foreach (DataRow dr in dsKeys.Tables[0].Rows)
                    {
                        SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                        try { sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), dr[2].ToString().ToUpper().Replace(" ", "_")); }
                        catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dr[0] + " : " + dr[2].ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        Type LinqType = Toolbox.GetSystemTypeFromListTypes(sType);
                        sListKeys.Add(new SQLColumn(Convert.ToInt32(dr[0]), dr[1].ToString(), sType, LinqType, dr[3].ToString(), Toolbox.CastValueToBoolean(dr[3].ToString()), "0", true, true));
                    }
                }
                return sListKeys;
            }
        }

        public void CreatePrimaryKeyFromDt(string sTableName, DataTable dtData, Query FuzibleQuery)
        {

            List<SQLColumn> sListFieldsPK;

            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
            sListFieldsPK = SHS.GuessPKFromQuery(dtData, SqlCharTypeCompatibility, null);

            if (sListFieldsPK.Count > 0) // ajout de la clé primaire sur la table cible
            {
                AddPrimaryKeyToTable(sTableName, sListFieldsPK, FuzibleQuery);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantaddpk + " (" + sTableName + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
            }
        }

        public DataSet CompareDsWithTargetDB(Query FuzibleQuery, string sTargetTable, SQLTools BDDTarget, bool withTargetUpdate, DataTable dtSource)
        {
            DataSet dsCible = new();

            List<SQLColumn> sListFieldsPK = new();

            DataSet dsCompare = new();

            FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetColumns = Toolbox.GetSQLColumnsFromDataTable(dtSource);

            //TODO : ne fonctionne pas avec les SELECT * !!!
            //0 = nom colonne, 1 = alias nom de colonne, 2 = nom table (fichier), 3 = alias table (fichier)

            //on vérifie qu'il y a des données dans la table cible, sinon, aucun intérêt !
            string sQteLignesCible = "";
            bool bTableExists = BDDTarget.CheckForExistingTable(sTargetTable, FuzibleQuery);
            bool bEverythingAdded = !bTableExists;
            if (bTableExists)
            {
                sQteLignesCible = SQLTools.BuildStringFromDs(BDDTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", JobParameters.ConnectionString_Target.SqlEchappementChar, sTargetTable, JobParameters.ConnectionString_Target.SqlEchappementChar, ";"), FuzibleQuery, sTargetTable), 0, 0, 0);
            }

            //si la table cible n'existe pas on la crée complètement avec les données
            if (sQteLignesCible.Equals(""))
            {
                bEverythingAdded = true;
                BDDTarget.InsertDataInBDD(dtSource, sTargetTable, FuzibleQuery);
                sQteLignesCible = SQLTools.BuildStringFromDs(BDDTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", JobParameters.ConnectionString_Target.SqlEchappementChar, sTargetTable, JobParameters.ConnectionString_Target.SqlEchappementChar, ";"), FuzibleQuery, sTargetTable), 0, 0, 0);
                //et on rejoue le COUNT
            }

            try
            {
                if (sQteLignesCible.Length > 0)
                {
                    List<SQLColumn> sListColumnsTarget = BDDTarget.GetFieldsFromTable(sTargetTable, FuzibleQuery);
                    FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.RealSynchroTargetColumns = sListColumnsTarget;
                    FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.SynchroTargetTable = sTargetTable;
                    ///////////////////////////////////////////////////////////////////////////////////////////////////////
                    //METHODE 1 : on essaye de trouver une clé primaire dans la table cible (ne marche que si elle existe !)
                    string sMessageKey = "";
                    bool bApplyPKOnTarget = true;

                    if (sListFieldsPK.Count == 0)
                    {
                        sMessageKey = Languages.Languages.sql_synchro_pktarget + sTargetTable;

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                        sListFieldsPK = BDDTarget.GetKeysFromTable(sTargetTable, "PK", FuzibleQuery);
                        if (sListFieldsPK.Count > 0) { bApplyPKOnTarget = false; }

                        //et on vérifie que la colonne existe bien dans le datatable cible
                        int iMatch = 0;
                        string sMatch = "";
                        foreach (SQLColumn SC in sListFieldsPK)
                        {
                            foreach (DataColumn dc in dtSource.Columns)
                            {
                                if (SC.ColumnName.Equals(dc.ColumnName, StringComparison.InvariantCultureIgnoreCase) || SC.ColumnName.Equals(dc.Caption, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    iMatch++; sMatch = string.Concat(sMatch, dc.ColumnName, ","); break;
                                }
                            }
                        }
                        if (sMatch.Length > 0) { sMatch = sMatch[0..^1]; } //virgule...


                        if (iMatch != sListFieldsPK.Count) //on considère que la colonne de clé primaire n'a pas été trouvée. (on va forcer la création en passant par le datatable source plus loin)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_pkmismatchsourcetarget + (sMatch.Length > 0 ? sMatch : string.Join(",", sListFieldsPK.Select(pk => pk.ColumnName))) + Languages.Languages.sql_synchro_pkmismatchautodetect, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            //cas ou les colonnes de clé primaire de la cible n'existent pas dans la source : on va tenter de chercher la clé dans la source pour l'appliquer à la cible
                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            sListFieldsPK = SHS.GuessPKFromQuery(dtSource, SqlCharTypeCompatibility, sListColumnsTarget);
                            iMatch = 0;
                            sMatch = "";
                            foreach (SQLColumn sCS in sListFieldsPK)
                            {
                                foreach (SQLColumn sCT in sListColumnsTarget)
                                {
                                    if (sCS.ColumnName.Equals(sCT.ColumnName, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        iMatch++; sMatch = string.Concat(sMatch, sCT.ColumnName, ","); break;
                                    }
                                }
                            }
                            if (sMatch.Length > 0) { sMatch = sMatch[0..^1]; } //virgule...

                            if (sListFieldsPK.Count > 0 && iMatch == sListFieldsPK.Count) // on a une clé primaire dans la source, et on a ces colonnes dans la cible
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_pkuniquekeyfound + sMatch + ").", SQLTools_Enums.LOG_TYPEINFO.INF);
                                bApplyPKOnTarget = false;
                            }
                            else
                            {
                                Exception ex = new(Languages.Languages.sql_synchro_pkmismatchsourcetarget + sMatch + ")");
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                                //drop primary key sur cible ?
                                sListFieldsPK = new List<SQLColumn>();
                                bApplyPKOnTarget = false;
                            }
                        }
                    }

                    //METHODE 2 : et si on a pas trouvé dans la cible, on va tenter en loucedé sur la source à partir du dataset
                    if (sListFieldsPK.Count == 0)
                    {
                        sMessageKey = Languages.Languages.sql_synchro_pkfromsourcedt + dtSource.TableName;

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                        DataColumn[] dtCPK = dtSource.PrimaryKey;
                        if (dtCPK.Length != 0)
                        {
                            foreach (DataColumn dtC in dtCPK)
                            {
                                sListFieldsPK.Add(SQLColumn.SQLColumnFromDataColumn(dtC, SqlCharTypeCompatibility));
                            }
                        }
                        bApplyPKOnTarget = true;
                    }

                    //METHODE 3 : toujours rien, on va tâcher de deviner
                    if (sListFieldsPK.Count == 0)
                    {
                        //METHODE 3A : basé sur source dataset
                        sMessageKey = Languages.Languages.sql_synchro_pkfromsourcedt_shs + sTargetTable + ")";

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                        SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                        sListFieldsPK = SHS.GuessPKFromQuery(dtSource, SqlCharTypeCompatibility, sListColumnsTarget);
                        bApplyPKOnTarget = true;
                    }

                    if (sListFieldsPK.Count == 0)
                    {
                        //et si la requête n'a rien donné, on va essayer de deviner quelle est la clé en requêtant la cible "brute"
                        sMessageKey = Languages.Languages.sql_synchro_pkfromtargetdt_shs + sTargetTable + ")";

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                        DataSet dsKeys;

                        string sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(true, JobParameters.SynchroBypassQueryFiltersInTarget, false);
                        sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sTargetQuery, JobParameters, FuzibleQuery, dtSource);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_loadsourcedata_query + sTargetQuery, SQLTools_Enums.LOG_TYPEINFO.DET);

                        dsKeys = BDDTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT,sTargetQuery, FuzibleQuery, sTargetTable);
                        SHSOperations SHSA = new(JobParameters, FuzibleQuery, ref MyLog); //on recherche les champs NUL/NOT NULL car la recherche de PK requiert des champs NOT NULL
                        SHSA.SetDBNullInDataSet(dsKeys.Tables[0]);

                        if (dsKeys.Tables.Count > 0)
                        {
                            List<SQLColumn> sListOfColumnsSourceFromDataset = Toolbox.GetSQLColumnsFromDataTable(dtSource);

                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            sListFieldsPK = SHS.GuessPKFromQuery(dsKeys.Tables[0], SqlCharTypeCompatibility, sListOfColumnsSourceFromDataset);
                        }
                        else
                        {
                            Exception ex = new(Languages.Languages.sql_synchro_errorsintargetquery);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    ///////////////////////////////////////////////////////////////////////////////////////////////////////

                    //Inutile d'aller plus loin sans clé primaire
                    if (sListFieldsPK.Count > 0)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty(sTargetTable, SQLTools_Enums.QUERY_PROPERTIES.SYNCHRO_PRIMARY_KEY, string.Join(",", sListFieldsPK.Select(pk => pk.ColumnName)), true);

                        if (JobParameters.AlterColumnTypeOnInsert)
                        {
                            List<SQLColumn> sListOfColumnsSourceFromDataset = Toolbox.GetSQLColumnsFromDataTable(dtSource);
                            foreach (SQLColumn SQLC in sListOfColumnsSourceFromDataset)
                            {
                                if (!sListColumnsTarget.Any(f => f.ColumnName.Equals(SQLC.ColumnName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                                    var col = SHS.GetListFieldsTypesFromDataset(dtSource, SQLC.ColumnIndex, FuzibleQuery, SQLTools_Enums.CLASS_PURPOSE.SRC);

                                    bool bOK = BDDTarget.CreateColumnInTarget(sTargetTable, col.Count > 0 ? col[0] : SQLC, true, FuzibleQuery);
                                    if (bOK) { sListColumnsTarget.Add(SQLC); }
                                }
                            }
                        }

                        if (bApplyPKOnTarget)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_addpktotarget + sTargetTable + " - " + string.Join(",", Toolbox.GetColumnNamesFromSQLColumnObjects(sListFieldsPK)), SQLTools_Enums.LOG_TYPEINFO.INF);
                            BDDTarget.AddPrimaryKeyToTable(sTargetTable, sListFieldsPK, FuzibleQuery);
                        }

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_loadsourcedata, SQLTools_Enums.LOG_TYPEINFO.INF);
                        //Attention : à ce stade, on va requêter la cible avec les possibles conditions "WHERE" de la source
                        //--------------------------------------------------------------------------------------------------

                        //on requête la cible sur le même périmètre que la source pour procéder à la comparaison
                        string sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(true, JobParameters.SynchroBypassQueryFiltersInTarget, false);
                        sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sTargetQuery, JobParameters, FuzibleQuery, dtSource);

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_loadsourcedata_query + sTargetQuery, SQLTools_Enums.LOG_TYPEINFO.DET);

                        bool bMoreErrorsAfterTargetQuery = false;

                        if (JobParameters.ExportUseDataset)
                        {
                            dsCible = BDDTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sTargetQuery, FuzibleQuery, sTargetTable);
                        }
                        else
                        {
                            var QCible = new Query(JobParameters.DeepCopy(), sTargetQuery);
                            QCible.ChangeLimitedResults(FuzibleQuery.QueryAnalyzer.LimitedResults);

                            int iErrorsBefore = MyLog.JobErrors;
                            dsCible = BDDTarget.GetDataFromDatabase(QCible, bTableExists).Result;
                            if (iErrorsBefore != MyLog.JobErrors) { bMoreErrorsAfterTargetQuery = true; }
                        }

                        if (dsCible != null && dsCible.Tables.Count > 0 && !bMoreErrorsAfterTargetQuery)
                        {
                            Toolbox.AddOptionalColumnsInDatasetFromINI(dsCible.Tables[0], JobParameters, FuzibleQuery, ref MyLog);
                        }

                        //on contrôle que la requête vers la cible n'a pas déconné (une requête qui a déliré renvoie la valeur par défaut du DATASET
                        //de la fonction ExecQuery, soit table 0 (result), colonne 0 (value), ligne 0 = 0
                        // && dsCible.Tables.Count > 0 && dsCible.Tables[0].Rows.Count > 0 && (!dsCible.Tables[0].Rows[0][0].Equals("0"))
                        if (dsCible != null && dsCible.Tables.Count > 0 && !bMoreErrorsAfterTargetQuery)
                        {
                            //---------------------
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_sourcerows + sTargetTable + ") : " + dtSource.Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_targetrows + sTargetTable + ") : " + dsCible.Tables[0].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                            //comparaison source - cible
                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            dsCompare = SHS.CompareDataset(dtSource, dsCible.Tables[0], ref sListFieldsPK, ClassPurpose, FuzibleQuery);

                            if (withTargetUpdate)
                            {
                                int iRowsSource = dtSource.Rows.Count;
                                int iRowsTarget = dsCible.Tables[0].Rows.Count;
                                //on vide la mémoire pour la synchro, inutile de garder de la data
                                dtSource.Clear();
                                dsCible.Tables.Clear();
                                dsCible.Clear();
                                BDDTarget.UpdateTableFromDs(sTargetTable, dsCompare, sListFieldsPK, iRowsSource, iRowsTarget, FuzibleQuery, bEverythingAdded);
                            }
                        }
                        else
                        {
                            Exception ex = new(Languages.Languages.sql_synchro_errorsintargetquery + " (" + sTargetTable + ")");
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_nopkintarget01 + sTargetTable + Languages.Languages.sql_synchro_nopkintarget02, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (dsCible.Tables.Count > 0)
                    {
                        dsCible.Tables[0].TableName = sTargetTable;
                        dsCompare.Merge(dsCible.Tables[0]);
                        dsCible.Clear();
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.sql_synchro_cantcreatetargettable + " (" + sTargetTable + ")");
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            catch (OperationCanceledException)
            { throw; }

            return dsCompare;
        }

        public DataSet ExecQuery(System.Reflection.MethodBase mbCalledFrom, SQLTools_Enums.TYPE_REQUETE eTypeRequete, string sRequete, Query FuzibleQuery, string sDtName, bool withCheckAfterDeleteOrInsert = false)
        {
            if (sDtName.Length == 0) { sDtName = "unknown"; }
            DataSet dsData = new();
            dsData.Tables.Add(sDtName);
            dsData.Tables[0].Columns.Add("VALUE");
            dsData.Tables[0].Rows.Add("0");

            int iQteLignesAvant = 0;
            int iQteLignesApres;
            int iEcartLignes;

            string sRequeteControle = "";


            try
            {
                //si update, delete ou insert, exécution requête de contrôle : pour un insert, compter le nombre de lignes avant, et comparer avec le nombre de lignes après !

                if (withCheckAfterDeleteOrInsert)
                {

                    switch (eTypeRequete)
                    {
                        case SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE:
                            //DELETE FROM table (WHERE)
                            //TRUNCATE TABLE table
                            //string[] sListeInRequeteA = sRequete.Split(Convert.ToChar(" "));
                            string[] sListeInRequeteA = sRequete.Split(Convert.ToChar(" "));
                            int iIndexTableInRequeteA = Array.IndexOf(sListeInRequeteA, "FROM") + 1;
                            if (iIndexTableInRequeteA == -1)
                            { iIndexTableInRequeteA = Array.IndexOf(sListeInRequeteA, "TABLE") + 1; }
                            string sTableInsert = sListeInRequeteA[iIndexTableInRequeteA];
                            sRequeteControle = "SELECT COUNT(*) FROM " + sTableInsert;

                            var tDel = Task.Factory.StartNew(() =>
                            {
                                dsData = RunQuery(System.Reflection.MethodBase.GetCurrentMethod(), Connection.SConnDriver, sRequeteControle, FuzibleQuery, sDtName, SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT).Result;
                            });

                            Monitoring.AddThread(tDel, System.Reflection.MethodBase.GetCurrentMethod());

                            while (!tDel.IsCompleted)
                            { Thread.Sleep(10); }

                            iQteLignesAvant = Convert.ToInt32(BuildStringFromDs(dsData));
                            break;
                        case SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT:
                            //string[] sListeInRequeteB = sRequete.Split(Convert.ToChar(" "));
                            string[] sListeInRequeteB = sRequete.Split(Convert.ToChar(" "));
                            int iIndexTableInRequeteB = Array.IndexOf(sListeInRequeteB, "INTO") + 1;
                            string sTableDelete = sListeInRequeteB[iIndexTableInRequeteB];
                            sRequeteControle = "SELECT COUNT(*) FROM " + sTableDelete;

                            var tIns = Task.Factory.StartNew(() =>
                            {
                                dsData = RunQuery(System.Reflection.MethodBase.GetCurrentMethod(), Connection.SConnDriver, sRequeteControle, FuzibleQuery, sDtName, SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT).Result;
                            });

                            Monitoring.AddThread(tIns, System.Reflection.MethodBase.GetCurrentMethod());

                            while (!tIns.IsCompleted)
                            { Thread.Sleep(10); }
                            iQteLignesAvant = Convert.ToInt32(BuildStringFromDs(dsData));
                            break;
                    }

                }

                var t1 = Task.Factory.StartNew(() =>
                {
                    dsData = RunQuery(mbCalledFrom, Connection.SConnDriver, sRequete, FuzibleQuery, sDtName, eTypeRequete).Result;
                });

                Monitoring.AddThread(t1, System.Reflection.MethodBase.GetCurrentMethod());

                while (!t1.IsCompleted)
                {

                    Thread.Sleep(10);
                }

                //on récupère la valeur de retour
                if (eTypeRequete != SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                {
                    if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0 && dsData.Tables[0].Rows[0][0].ToString().Length >= 1)
                    {
                        string sResult = dsData.Tables[0].Rows[0][0].ToString();
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_query_returnvalue, mbCalledFrom.Name, "-", eTypeRequete.ToString(), ") : ", sResult), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    //exécution requête du compte des lignes pour comparer avant/après
                }

                if ((eTypeRequete == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE | eTypeRequete == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT) & withCheckAfterDeleteOrInsert)
                {
                    var t2 = Task.Factory.StartNew(() =>
                    {
                        dsData = RunQuery(mbCalledFrom, Connection.SConnDriver, sRequeteControle, FuzibleQuery, sDtName, SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT).Result;
                    });

                    Monitoring.AddThread(t2, System.Reflection.MethodBase.GetCurrentMethod());

                    while (!t2.IsCompleted)
                    { Thread.Sleep(10); }

                    iQteLignesApres = Convert.ToInt32(BuildStringFromDs(dsData));

                    //comparaison écart quantité lignes
                    iEcartLignes = iQteLignesAvant - iQteLignesApres;

                    //intégration dans dataset
                    dsData.Tables[0].Rows[0][0] = iEcartLignes.ToString();

                }

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            dsData.CaseSensitive = false;
            return dsData;

        }

        public DataSet ExecStoredProcedure(string sCommand, Query FuzibleQuery, List<string> sParameters = null, string sCommandText = null)
        {
            string sProcedureName = sCommand;
            if (sProcedureName.StartsWith("EXEC ", StringComparison.OrdinalIgnoreCase)) { sProcedureName = sProcedureName[5..]; }
            else if (sProcedureName.StartsWith("EXECUTE ", StringComparison.OrdinalIgnoreCase)) { sProcedureName = sProcedureName[8..]; }
            else if (sProcedureName.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase)) { sProcedureName = sProcedureName[5..]; }
            sProcedureName = sProcedureName.Trim();

            //découpage des paramètres
            if (sParameters == null)
            {
                if (sCommand.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(sProcedureName, ".+[^\\(\\)]\\(.+[^\\(\\)]\\);?"))
                {
                    string sData = sProcedureName[(sProcedureName.IndexOf("(") + 1)..];
                    sProcedureName = sProcedureName[..sProcedureName.IndexOf("(")];
                    sData = sData.EndsWith(";") ? sData[..^1].Trim() : sData.Trim();
                    sData = sData.EndsWith(")") ? sData[..^1].Trim() : sData.Trim();
                    string[] sArgs = sData.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    if (sArgs.Length > 0)
                    {
                        sParameters = new List<string>();
                        foreach (string s in sArgs)
                        {
                            string sArg = s;
                            if (sArg.EndsWith(";")) { sArg = sArg[0..^1]; }
                            sParameters.Add(sArg.Replace("'", "").Trim());
                        }
                    }
                }
                else if (sCommand.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(sProcedureName, ".+[^\\(\\)]\\(\\s*\\);?"))
                {
                    sProcedureName = sProcedureName[..sProcedureName.IndexOf("(")];
                    sProcedureName = sProcedureName.EndsWith(";") ? sProcedureName[..^1].Trim() : sProcedureName.Trim();
                    sProcedureName = sProcedureName.EndsWith(")") ? sProcedureName[..^1].Trim() : sProcedureName.Trim();
                }
                else if (sCommand.StartsWith("EXEC", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(sProcedureName, ".+[^\\s]\\s+.+;?"))
                {
                    string sData = sProcedureName[(sProcedureName.IndexOf(" ") + 1)..];
                    sProcedureName = sProcedureName[..sProcedureName.IndexOf(" ")];
                    string[] sArgs = sData.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    if (sArgs.Length > 0)
                    {
                        sParameters = new List<string>();
                        foreach (string s in sArgs)
                        {
                            string sArg = s;
                            if (sArg.EndsWith(";")) { sArg = sArg[0..^1]; }
                            sParameters.Add(sArg.Replace("'", "").Trim());
                        }
                    }
                }
                sProcedureName = sProcedureName.EndsWith(";") ? sProcedureName[..^1].Trim() : sProcedureName.Trim();
            }

            DataSet dsData = new();
            dsData.Tables.Add("RESULTAT");

            try
            {
                Connect(Connection, FuzibleQuery);

                switch (Connection.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        try
                        {
                            OleDbCommand CommandeAccess = new(sProcedureName, ConnexionACCESS)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeAccess.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                OleDbCommandBuilder.DeriveParameters(CommandeAccess);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeAccess.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeAccess.Parameters[CommandeAccess.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            var readerTask = CommandeAccess.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);
                            CommandeAccess.Dispose();

                        }
                        catch (OleDbException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ACCESSEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;

                    case SQLTools_Enums.BDD.DB_MYSQL:

                        try
                        {
                            MySqlCommand CommandeSQLMS = new(sProcedureName, ConnexionMySQL)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeSQLMS.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                MySqlCommandBuilder.DeriveParameters(CommandeSQLMS);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeSQLMS.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeSQLMS.Parameters[CommandeSQLMS.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            Task<MySqlDataReader> readerTask = CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);

                            CommandeSQLMS.Dispose();

                        }
                        catch (MySqlException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.MYSQLEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;

                    case SQLTools_Enums.BDD.DB_SQLSERVER:

                        try
                        {
                            SqlCommand CommandeSQLSS = new(sProcedureName, ConnexionSQLServer)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = ConnexionSQLServer.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionSQLServer.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeSQLSS.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                SqlCommandBuilder.DeriveParameters(CommandeSQLSS);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeSQLSS.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeSQLSS.Parameters[CommandeSQLSS.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            Task<SqlDataReader> readerTask = CommandeSQLSS.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);

                            CommandeSQLSS.Dispose();

                        }
                        catch (SqlException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.SQLEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;


                    case SQLTools_Enums.BDD.DB_SQLITE:

                        try
                        {
                            SqliteCommand CommandeSqlite = new(sProcedureName, ConnexionSqlite)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeSqlite.CommandText = sCommandText; }

                            //if (sParameters != null)
                            //{
                            //        for (int cptP = 0; cptP <= sParameters.Count - 1; cptP += 1)
                            //        {
                            //            CommandeSqlite.Parameters.AddWithValue(cptP, sParameters[cptP]);
                            //        }
                            //}

                            var readerTask = CommandeSqlite.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);
                            CommandeSqlite.Dispose();

                        }
                        catch (SqliteException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.SQLiteException, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;

                    case SQLTools_Enums.BDD.DB_ODBC:

                        try
                        {
                            OdbcCommand CommandeODBC = new(sProcedureName, ConnexionODBC)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeODBC.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                OdbcCommandBuilder.DeriveParameters(CommandeODBC);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeODBC.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeODBC.Parameters[CommandeODBC.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            var readerTask = CommandeODBC.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);
                            CommandeODBC.Dispose();
                        }
                        catch (OdbcException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ODBCEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;


                    case SQLTools_Enums.BDD.DB_POSTGRE:

                        try
                        {
                            NpgsqlCommand CommandeNpgsql = new(sProcedureName, ConnexionPOSTGRE)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = ConnexionPOSTGRE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionPOSTGRE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeNpgsql.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                NpgsqlCommandBuilder.DeriveParameters(CommandeNpgsql);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeNpgsql.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeNpgsql.Parameters[CommandeNpgsql.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            Task<NpgsqlDataReader> readerTask = CommandeNpgsql.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);

                            CommandeNpgsql.Dispose();
                        }
                        catch (NpgsqlException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.NPGEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;

                    case SQLTools_Enums.BDD.DB_ORACLE:
                        try
                        {
                            OracleCommand CommandeORA = new(sProcedureName, ConnexionORACLE)
                            {
                                CommandType = CommandType.StoredProcedure,
                                CommandTimeout = ConnexionORACLE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionORACLE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (sCommandText != null) { CommandeORA.CommandText = sCommandText; }

                            if (sParameters != null)
                            {
                                OracleCommandBuilder.DeriveParameters(CommandeORA);

                                List<int> iQteParam = new();
                                int iCpt2 = 0;
                                foreach (SqlParameter p in CommandeORA.Parameters)
                                {
                                    if (p.Direction == ParameterDirection.Input)
                                    {
                                        iQteParam.Add(iCpt2);
                                        iCpt2++;
                                    }
                                    else
                                    {
                                        iQteParam.Add(-1);
                                    }
                                }

                                if (sParameters.Count == iQteParam.Count(q => q > -1))
                                {
                                    for (int cptP = 0; cptP <= iQteParam.Count - 1; cptP += 1)
                                    {
                                        if (iQteParam[cptP] > -1)
                                        {
                                            CommandeORA.Parameters[CommandeORA.Parameters[cptP].ParameterName.ToString()].Value = sParameters[iQteParam[cptP]];
                                        }
                                    }
                                }
                            }

                            Task<OracleDataReader> readerTask = CommandeORA.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                            dsData.Tables[0].Load(readerTask.Result);

                            CommandeORA.Dispose();
                        }
                        catch (OracleException ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ORACLEEXCEPTION, System.Reflection.MethodBase.GetCurrentMethod().Name, Connection.SConnDriver.ToString(), sCommandText, Languages.Languages.sql_query_storedproc), SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }

                        break;
                }

                if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0 && dsData.Tables[0].Rows[0][0].ToString().Length > 1)
                {
                    //string sResult = dsData.Tables[0].Rows[0][0].ToString();
                    string sInfo = "col(s):" + dsData.Tables[0].Columns.Count.ToString() + " - row(s):" + dsData.Tables[0].Rows.Count.ToString();
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_query_storedprocreturnvalue, sCommand, ") : ", sInfo), SQLTools_Enums.LOG_TYPEINFO.INF);
                }

                if (!JobParameters.KeepConnexionAfterQuery)
                {
                    Disconnect(Connection, FuzibleQuery);
                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                //FuzibleQuery.QueryErrors += 1;
                if (!JobParameters.KeepConnexionAfterQuery)
                {
                    Disconnect(Connection, FuzibleQuery);
                }
            }

            dsData.CaseSensitive = false;
            return dsData;
        }

        public void DeleteDataInBDD(DataTable dtData, string sTableTarget, List<SQLColumn> sListFieldsWhere, Query FuzibleQuery)
        {
            string sTableName = sTableTarget;
            bool bTableExists = CheckForExistingTable(sTableName, FuzibleQuery);

            if (!bTableExists && !JobParameters.AutoSQLTableCreation)
            {
                Exception ex = new(Languages.Languages.sql_query_unexistingtable + sTableName);
                throw ex;
            }
            else
            {
                PrepareDataForTarget pdFT = CheckCreateAlterTableColumns(bTableExists, dtData, sTableName, FuzibleQuery);
                //CheckForOptionalColumnsAndAddIfNeeded(sTableName, sDBNameSource); //on peut avoir envie de créer les colonnes optionnelles dans la table si elles ne sont pas présentes 

                //création d'une liste qui initie chaque point de départ de chaque thread
                if (pdFT != null)
                {
                    if (pdFT.bStepOK)
                    {
                        int iRows = dtData.Rows.Count;

                        if (iRows > 0)
                        {

                            //exécution des insert en multithread
                            MThread mtClass = new(JobParameters, ClassPurpose, MyLog, dtData.Rows.Count, 0);

                            if (mtClass.QuantityOfThreadsToCompute > 0)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_delete + sTableName + "(" + iRows.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
                                for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                                {
                                    mtClass.INIT_DeleteBDDFromDt(dtData, sTableName, sListFieldsWhere);
                                    //Thread th = new Thread(mtClass.DeleteInBDDFromDatatable);
                                    //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                                    //th.Start(numThread);
                                    // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                                    int numeroThread = numThread;
                                    Task th = new(() => mtClass.DeleteInBDDFromDt(numeroThread, FuzibleQuery));
                                    Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                    th.Start();
                                }

                                if (!JobParameters.TurboMode)
                                {
                                    while (!mtClass.AreAllThreadsFinished)
                                    {
                                        //Monitoring.TaskMonitoring.TaskMonitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                        Thread.Sleep(100);

                                        if (mtClass.HasOperationBeenCancelled)
                                        { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                                    }

                                }
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_delete_nothing + sTableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }

                        JobParameters.IsMultiThreadedImportToBDDFinished = true;
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.sql_query_unabletoanalyze);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dtData.TableName, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
        }

        public void UpdateDataInBDD(DataTable dtData, string sTableTarget, List<SQLColumn> sListFieldsWhere, Query FuzibleQuery)
        {

            string sTableName = sTableTarget;
            bool bTableExists = CheckForExistingTable(sTableName, FuzibleQuery);

            if (!bTableExists && !JobParameters.AutoSQLTableCreation)
            {
                Exception ex = new(Languages.Languages.sql_query_unexistingtable + sTableName);
                throw ex;
            }
            else
            {
                PrepareDataForTarget pdFT = CheckCreateAlterTableColumns(bTableExists, dtData, sTableName, FuzibleQuery);
                //CheckForOptionalColumnsAndAddIfNeeded(sTableName, sDBNameSource); //on peut avoir envie de créer les colonnes optionnelles dans la table si elles ne sont pas présentes 

                //création d'une liste qui initie chaque point de départ de chaque thread

                if (pdFT != null)
                {
                    if (pdFT.bStepOK)
                    {
                        int iRows = dtData.Rows.Count;

                        if (iRows > 0)
                        {
                            List<SQLColumn> sListeColonnesCible = new();
                            sListeColonnesCible = GetFieldsFromTable(sTableName, FuzibleQuery); //récupération des colonnes CIBLE par le schéma et non pas par la détection automatique

                            //exécution des insert en multithread
                            MThread mtClass = new(JobParameters, ClassPurpose, MyLog, dtData.Rows.Count, 0);

                            if (mtClass.QuantityOfThreadsToCompute > 0)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_update + sTableName + "(" + iRows.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
                                for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                                {
                                    mtClass.INIT_UpdateBDDFromDt(dtData, sTableName, sListFieldsWhere, sListeColonnesCible);
                                    //Thread th = new Thread(mtClass.UpdateInBDDFromDatatable);
                                    //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                                    //th.Start(numThread);
                                    // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                                    int numeroThread = numThread;
                                    Task th = new(() => mtClass.UpdateInBDDFromDt(numeroThread, ref FuzibleQuery));
                                    Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                    th.Start();
                                }

                                if (!JobParameters.TurboMode)
                                {
                                    while (!mtClass.AreAllThreadsFinished)
                                    {
                                        //Monitoring.TaskMonitoring.TaskMonitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                        Thread.Sleep(100);

                                        if (mtClass.HasOperationBeenCancelled)
                                        { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                                    }


                                }
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_update_nothing + sTableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }

                        JobParameters.IsMultiThreadedImportToBDDFinished = true;
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.sql_query_unabletoanalyze);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dtData.TableName, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
        }

        public void InsertDataInBDD(DataTable dtData, string sTableTarget, Query FuzibleQuery, int iTableExists = -1)
        {
            //dtSource permet au mode synchro, quand il veut ajouter les lignes manquantes, de comparer la volumétrie de la source et de la cible pour que l'analyseur de champs évalue au mieux les données
            //celui-ci prendra la source ou la cible comme "modèle" selon la quantité de lignes
            string sTableName = sTableTarget;
            bool bTableExists = iTableExists == 1 ? true : false;

            if (iTableExists == -1) { bTableExists = CheckForExistingTable(sTableName, FuzibleQuery); }

            if (!bTableExists && !JobParameters.AutoSQLTableCreation)
            {
                Exception ex = new(Languages.Languages.sql_query_unexistingtable + sTableName);
                throw ex;
            }
            else
            {
                PrepareDataForTarget pdFT = CheckCreateAlterTableColumns(bTableExists, dtData, sTableName, FuzibleQuery);

                if (pdFT != null)
                {
                    if (pdFT.bStepOK)
                    {
                        //création d'une liste qui initie chaque point de départ de chaque thread
                        int iRows = dtData.Rows.Count;

                        if (iRows > 0)
                        {
                            if (JobParameters.SQLTargetBulkCopy)
                            {
                                //exécution des insert en multithread
                                MThread mtClass = new(JobParameters, ClassPurpose, MyLog, iRows, 0);

                                if (mtClass.QuantityOfThreadsToCompute > 0)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_bulkinsert + sTableName + " (" + iRows.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
                                    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                                    {
                                        mtClass.INIT_BulkInsertBDDFromDt(dtData, sTableName, numThread, pdFT.ColumnsTarget, pdFT.sListeNumColonnesToKeepSource);
                                        //Thread th = new Thread(mtClass.InsertInBDDFromDatatable);
                                        //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                                        //th.Start(numThread);
                                        // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                                        int numeroThread = numThread;
                                        Task th = new(() => mtClass.BulkInsertInBDDFromDt(numeroThread, ref FuzibleQuery));
                                        Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                        th.Start();
                                    }

                                    //si on doit attendre la fin de l'opération multithread, on boucle, sinon on passe outre !
                                    if (!JobParameters.TurboMode)
                                    {
                                        while (!mtClass.AreAllThreadsFinished)
                                        {
                                            //Monitoring.TaskMonitoring.TaskMonitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);

                                            if (mtClass.HasOperationBeenCancelled)
                                            { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                                        }
                                    }
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_insert_nothing + sTableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }
                            else
                            {
                                //exécution des insert en multithread
                                MThread mtClass = new(JobParameters, ClassPurpose, MyLog, iRows, 0);

                                if (mtClass.QuantityOfThreadsToCompute > 0)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_insert + sTableName + "(" + iRows.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
                                    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                                    {
                                        mtClass.INIT_InsertBDDFromDt(dtData, sTableName, pdFT.sEntete, pdFT.ColumnsTarget, pdFT.sListeNumColonnesToKeepSource);
                                        //Thread th = new Thread(mtClass.InsertInBDDFromDatatable);
                                        //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                                        //th.Start(numThread);
                                        // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                                        int numeroThread = numThread;
                                        Task th = new(() => mtClass.InsertInBDDFromDt(numeroThread, FuzibleQuery));
                                        Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                        th.Start();
                                    }

                                    //si on doit attendre la fin de l'opération multithread, on boucle, sinon on passe outre !
                                    if (!JobParameters.TurboMode)
                                    {
                                        while (!mtClass.AreAllThreadsFinished)
                                        {
                                            //Monitoring.TaskMonitoring.TaskMonitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);

                                            if (mtClass.HasOperationBeenCancelled)
                                            { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                                        }


                                    }
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_query_insert_nothing + sTableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }
                        }
                        JobParameters.IsMultiThreadedImportToBDDFinished = true;
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.sql_query_insert_exception);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dtData.TableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.sql_query_unabletoanalyze);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dtData.TableName, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
        }

        public void BulkInsertInBDD(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            Task t = null;

            switch (Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ORACLE:
                    t = Task.Factory.StartNew(() => BulkInsertInOracle(iNumThread, dtData, sTableTarget, SQLTargetColumns, sListSourceColumnsToKeep, FuzibleQuery));
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    t = Task.Factory.StartNew(() => t = BulkInsertInSQLServer(iNumThread, dtData, sTableTarget, SQLTargetColumns, sListSourceColumnsToKeep, FuzibleQuery));
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    t = Task.Factory.StartNew(() => t = BulkInsertInMySQL(iNumThread, dtData, sTableTarget, SQLTargetColumns, sListSourceColumnsToKeep, FuzibleQuery));
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    t = Task.Factory.StartNew(() => t = BulkInsertInPostgres(iNumThread, dtData, sTableTarget, SQLTargetColumns, sListSourceColumnsToKeep, FuzibleQuery));
                    break;
                default:
                    throw new Exception("Bulk Copy Not Supporter for Driver " + Connection.SConnDriver.ToString());
            }

            if (t != null)
            {
                Monitoring.AddThread(t, System.Reflection.MethodBase.GetCurrentMethod());

                while (!t.IsCompleted)
                { Thread.Sleep(10); }
            }
        }

        private void BulkInsertInSQLServerNotify(object sender, SqlRowsCopiedEventArgs e, int iDtRows, int iNumThread, string sTable, Query FuzibleQuery)
        {
            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
            int iCopied = int.Parse(e.RowsCopied.ToString());
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("T", iNumThread.ToString("00"), Languages.Languages.mt_insertsql, " ", sTable, " ", iCopied, "/", iDtRows + Toolbox.GetPercent(0, iDtRows, iCopied)), SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        private void BulkInsertInOracleNotify(object sender, OracleRowsCopiedEventArgs e, int iDtRows, ref int iPassage, int iNumThread, string sTable, Query FuzibleQuery)
        {
            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
            iPassage++;
            int iCopied = int.Parse(e.RowsCopied.ToString()) * iPassage;
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("T", iNumThread.ToString("00"), Languages.Languages.mt_insertsql, " ", sTable, " ", iCopied, "/", iDtRows + Toolbox.GetPercent(0, iDtRows, iCopied)), SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        private void BulkInsertInMySqlNotify(object sender, MySqlRowsCopiedEventArgs e, int iDtRows, int iNumThread, string sTable, Query FuzibleQuery)
        {
            int iCopied = int.Parse(e.RowsCopied.ToString());
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("T", iNumThread.ToString("00"), Languages.Languages.mt_insertsql, " ", sTable, " ", iCopied, "/", iDtRows + Toolbox.GetPercent(0, iDtRows, iCopied)), SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        private void BulkInsertInOracle(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            //mapping des colonnes
            List<string[]> sBulkColumns = new();

            foreach (string sCol in sListSourceColumnsToKeep)
            {
                SQLColumn sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(sCol, StringComparison.OrdinalIgnoreCase));
                if (sqlCT != null)
                {
                    DataColumn dtColumn = null;
                    if (!dtData.Columns.Contains(sCol))
                    {
                        foreach (DataColumn dc in dtData.Columns)
                        {
                            if (dc.Caption.Equals(sCol, StringComparison.OrdinalIgnoreCase))
                            {
                                dtColumn = dc;
                                break;
                            }
                        }
                    }
                    else
                    {
                        dtColumn = dtData.Columns[sCol];
                    }
                    if (sqlCT.ColumnLinqType != dtColumn.DataType)
                    {
                        //conversion des données car BulkCopy est très pointilleux sur le format
                        ConvertDataForBulkCopy(iNumThread, dtData, dtColumn, sqlCT, FuzibleQuery);
                    }
                    else if (sqlCT.ColumnLinqType == Type.GetType("System.String") && JobParameters.TrimData)
                    {
                        foreach (DataRow dr in dtData.Rows)
                        {
                            dr[dtColumn] = dr[dtColumn].ToString().Trim();
                        }
                    }
                    //attention il y a un TOUPPER parce qu'il y a un bug dans le driver oracle qui fait qu'il ne reconnait pas les colonnes en minuscule en bulk insert....
                    sBulkColumns.Add(new string[] { dtColumn.ColumnName, sqlCT.ColumnName.ToUpper() });
                }
            }

            Connect(Connection, FuzibleQuery);

            using (OracleBulkCopy bulkCopy = new(ConnexionORACLE, OracleBulkCopyOptions.Default))
            {
                int iPassage = 0;
                bulkCopy.DestinationTableName = sTableTarget;
                bulkCopy.BulkCopyTimeout = ConnexionORACLE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionORACLE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT;
                bulkCopy.BatchSize = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE;
                bulkCopy.NotifyAfter = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE;
                bulkCopy.OracleRowsCopied += new OracleRowsCopiedEventHandler((sender, e) => BulkInsertInOracleNotify(sender, e, dtData.Rows.Count, ref iPassage, iNumThread, sTableTarget, FuzibleQuery));

                foreach (string[] sMap in sBulkColumns)
                {
                    bulkCopy.ColumnMappings.Add(sMap[0], sMap[1]);
                }
                //OracleCommand command = sourceConnection.CreateCommand();
                //command.CommandText = "ALTER SESSION SET NLS_TIMESTAMP_FORMAT = 'DD-MM-YYYY HH24:MI:SS'";
                //command.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);

                try
                {


                    //bulkCopy.SqlRowsCopied += BulkInsertInBDDNotify;
                    if (bulkCopy.ColumnMappings.Count > 0)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_oracle + sTableTarget + " " + dtData.Rows.Count.ToString() + Languages.Languages.sql_bulkcopy_rows, SQLTools_Enums.LOG_TYPEINFO.DET);

                        bulkCopy.WriteToServer(dtData);
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_nocolumns + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }

                    //transaction.Commit();
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_error + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;

                    //try
                    //{
                    //    Disconnect(Connection);
                    //}
                    //catch (Exception ex2)
                    //{
                    //    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_rollbackerror + " (" + ex2.GetType() + " - " + ex2.Message + ")", SQLTools_Enums.LOG_TYPEINFO.ERR);
                    //}
                }
            }
            Disconnect(Connection, FuzibleQuery);
        }

        private async Task BulkInsertInSQLServer(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            //mapping des colonnes
            List<string[]> sBulkColumns = new();

            foreach (string sCol in sListSourceColumnsToKeep)
            {
                SQLColumn sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(sCol, StringComparison.OrdinalIgnoreCase));
                if (sqlCT != null)
                {
                    DataColumn dtColumn = null;
                    if (!dtData.Columns.Contains(sCol))
                    {
                        foreach (DataColumn dc in dtData.Columns)
                        {
                            if (dc.Caption.Equals(sCol, StringComparison.OrdinalIgnoreCase))
                            {
                                dtColumn = dc;
                                break;
                            }
                        }
                    }
                    else
                    {
                        dtColumn = dtData.Columns[sCol];
                    }
                    if (sqlCT.ColumnLinqType != dtColumn.DataType || sqlCT.ColumnLinqType == Type.GetType("System.Guid"))
                    {
                        //conversion des données car BulkCopy est très pointilleux sur le format
                        ConvertDataForBulkCopy(iNumThread, dtData, dtColumn, sqlCT, FuzibleQuery);
                    }
                    else if (sqlCT.ColumnLinqType == Type.GetType("System.String") && JobParameters.TrimData)
                    {
                        foreach (DataRow dr in dtData.Rows)
                        {
                            dr[dtColumn] = dr[dtColumn].ToString().Trim();
                        }
                    }
                    //attention il y a un TOUPPER parce qu'il y a un bug dans le driver oracle qui fait qu'il ne reconnait pas les colonnes en minuscule en bulk insert....
                    sBulkColumns.Add(new string[] { dtColumn.ColumnName, sqlCT.ColumnName });
                }
            }

            Connect(Connection, FuzibleQuery);

            using (SqlTransaction transaction = ConnexionSQLServer.BeginTransaction())
            {
                //GetAppLock()
                using (SqlBulkCopy bulkCopy = new(ConnexionSQLServer, SqlBulkCopyOptions.TableLock, transaction))
                {
                    bulkCopy.DestinationTableName = sTableTarget;
                    bulkCopy.BulkCopyTimeout = ConnexionSQLServer.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionSQLServer.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT;
                    bulkCopy.BatchSize = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE;
                    bulkCopy.NotifyAfter = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE;
                    bulkCopy.SqlRowsCopied += new SqlRowsCopiedEventHandler((sender, e) => BulkInsertInSQLServerNotify(sender, e, dtData.Rows.Count, iNumThread, sTableTarget, FuzibleQuery));
                    SqlCommand command = ConnexionSQLServer.CreateCommand();
                    command.Transaction = transaction;

                    try
                    {
                        foreach (string[] sMap in sBulkColumns)
                        {
                            bulkCopy.ColumnMappings.Add(sMap[0], sMap[1]);
                        }

                        //bulkCopy.SqlRowsCopied += BulkInsertInBDDNotify;
                        if (bulkCopy.ColumnMappings.Count > 0)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_sqlserver + sTableTarget + " " + dtData.Rows.Count.ToString() + Languages.Languages.sql_bulkcopy_rows, SQLTools_Enums.LOG_TYPEINFO.DET);

                            bulkCopy.WriteToServer(dtData);
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_nocolumns + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }

                        await transaction.CommitAsync(Monitoring.TaskCancellationToken);
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_error + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;

                        try
                        {
                            await transaction.RollbackAsync(Monitoring.TaskCancellationToken);
                        }
                        catch (Exception ex2)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_rollbackerror + " (" + ex2.GetType() + " - " + ex2.Message + ")", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                        finally
                        {
                            Disconnect(Connection, FuzibleQuery);
                        }

                    }
                }
            }
            Disconnect(Connection, FuzibleQuery);
        }

        private async Task BulkInsertInPostgres(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            //conversion des colonnes
            for (int iC = 0; iC < dtData.Columns.Count; iC++)
            {
                SQLColumn sqlCT;
                sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(dtData.Columns[iC].ColumnName, StringComparison.OrdinalIgnoreCase));
                if (sqlCT == null)
                {
                    sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(dtData.Columns[iC].Caption, StringComparison.OrdinalIgnoreCase));
                }
                if (sqlCT != null)
                {
                    if (sqlCT.ColumnLinqType != dtData.Columns[iC].DataType)
                    {
                        Toolbox.ConvertColumnType(dtData, dtData.Columns[iC], sqlCT.ColumnLinqType);
                    }
                }
            }


            //COPY importstream_events(aggregateid, sequence, eventtype, event) from STDIN (format binary)
            string sCopyHeader = "COPY " + sTableTarget + "(";

            for (int i = 0; i < sListSourceColumnsToKeep.Count; i++)
            {
                sCopyHeader = string.Concat(sCopyHeader, Connection.SqlEchappementChar, sListSourceColumnsToKeep[i], Connection.SqlEchappementChar, (i == sListSourceColumnsToKeep.Count - 1 ? ") from STDIN (format binary)" : ","));
            }

            Dictionary<Type, NpgsqlTypes.NpgsqlDbType> TypeDict = new();

            //foreach (SQLColumn sqlC in SQLTargetColumns)
            //{
            //    switch (sqlC.ColumnLinqType.ToString())
            //    {
            //        case "System.Int64":
            //            TypeDict.Add(typeof(int), NpgsqlTypes.NpgsqlDbType.Bigint);
            //            break;
            //        case "System.Int32":
            //            TypeDict.Add(typeof(int), NpgsqlTypes.NpgsqlDbType.Integer);
            //            break;
            //        case "System.Int16":
            //            TypeDict.Add(typeof(int), NpgsqlTypes.NpgsqlDbType.Smallint);
            //            break;
            //        case "System.DateTime":
            //            TypeDict.Add(typeof(DateTime), NpgsqlTypes.NpgsqlDbType.Timestamp);
            //            break;
            //        case "System.Boolean":
            //            TypeDict.Add(typeof(bool), NpgsqlTypes.NpgsqlDbType.Boolean);
            //            break;
            //        case "System.Decimal":
            //            TypeDict.Add(typeof(decimal), NpgsqlTypes.NpgsqlDbType.Numeric);
            //            break;
            //        case "System.Double":
            //            TypeDict.Add(typeof(double), NpgsqlTypes.NpgsqlDbType.Numeric);
            //            break;
            //        case "System.Single":
            //            TypeDict.Add(typeof(decimal), NpgsqlTypes.NpgsqlDbType.Numeric);
            //            break;
            //        case "System.String":
            //            TypeDict.Add(typeof(string), NpgsqlTypes.NpgsqlDbType.Varchar);
            //            break;
            //        case "System.Byte":
            //            TypeDict.Add(typeof(byte), NpgsqlTypes.NpgsqlDbType.Bytea);
            //            break;
            //        case "System.Collections.BitArray":
            //            break;
            //    }
            //}
            TypeDict.Add(typeof(Int16), NpgsqlTypes.NpgsqlDbType.Smallint);
            TypeDict.Add(typeof(Int64), NpgsqlTypes.NpgsqlDbType.Bigint);
            TypeDict.Add(typeof(Int32), NpgsqlTypes.NpgsqlDbType.Integer);
            TypeDict.Add(typeof(double), NpgsqlTypes.NpgsqlDbType.Double);
            TypeDict.Add(typeof(decimal), NpgsqlTypes.NpgsqlDbType.Numeric);
            TypeDict.Add(typeof(string), NpgsqlTypes.NpgsqlDbType.Varchar);
            TypeDict.Add(typeof(DateTime), NpgsqlTypes.NpgsqlDbType.Timestamp);
            TypeDict.Add(typeof(char[]), NpgsqlTypes.NpgsqlDbType.Varchar);
            TypeDict.Add(typeof(Guid), NpgsqlTypes.NpgsqlDbType.Uuid);
            TypeDict.Add(typeof(bool), NpgsqlTypes.NpgsqlDbType.Boolean);

            Connect(Connection, FuzibleQuery);

            try
            {
                int nRows = dtData.Rows.Count;
                using (var BulkWrite = ConnexionPOSTGRE.BeginBinaryImport(sCopyHeader))
                {
                    //BulkWrite.Timeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT;
                    for (int idRow = 0; idRow < nRows; idRow++)
                    {
                        await BulkWrite.StartRowAsync(Monitoring.TaskCancellationToken);

                        for (int iC = 0; iC < dtData.Columns.Count; iC++)
                        {
                            if (dtData.Columns[iC].DataType.ToString().Equals("System.String") && JobParameters.TrimData)
                            {
                                dtData.Rows[idRow][iC] = dtData.Rows[idRow][iC].ToString().Trim();
                            }

                            SQLColumn sqlCT;
                            sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(dtData.Columns[iC].ColumnName));
                            if (sqlCT == null)
                            {
                                sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(dtData.Columns[iC].Caption));
                            }
                            if (sqlCT != null)
                            {
                                if (dtData.Rows[idRow].IsNull(dtData.Columns[iC]))
                                {
                                    await BulkWrite.WriteNullAsync(Monitoring.TaskCancellationToken);
                                }
                                else
                                {
                                    if (dtData.Columns[iC].DataType == typeof(string) && string.IsNullOrEmpty(dtData.Rows[idRow].Field<string>(dtData.Columns[iC])))
                                    {
                                        await BulkWrite.WriteNullAsync(Monitoring.TaskCancellationToken);
                                    }
                                    else
                                    {
                                        var typ = TypeDict[sqlCT.ColumnLinqType];
                                        await BulkWrite.WriteAsync(dtData.Rows[idRow][dtData.Columns[iC].Ordinal], typ);
                                    }
                                }
                            }
                        }
                    }
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_postgres + sTableTarget + " " + dtData.Rows.Count.ToString() + Languages.Languages.sql_bulkcopy_rows, SQLTools_Enums.LOG_TYPEINFO.DET);

                    await BulkWrite.CompleteAsync(Monitoring.TaskCancellationToken);
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_error + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            Disconnect(Connection, FuzibleQuery);
        }

        private async Task BulkInsertInMySQL(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            //conversion des colonnes

            List<MySqlBulkCopyColumnMapping> sBulkColumns = new();
            foreach (string sCol in sListSourceColumnsToKeep)
            {
                SQLColumn sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(sCol, StringComparison.OrdinalIgnoreCase));
                if (sqlCT != null)
                {
                    DataColumn dtColumn = null;
                    if (!dtData.Columns.Contains(sCol))
                    {
                        foreach (DataColumn dc in dtData.Columns)
                        {
                            if (dc.Caption.Equals(sCol, StringComparison.OrdinalIgnoreCase))
                            {
                                dtColumn = dc;
                                break;
                            }
                        }
                    }
                    else
                    {
                        dtColumn = dtData.Columns[sCol];
                    }
                    if (sqlCT.ColumnLinqType != dtColumn.DataType)
                    {
                        //conversion des données car BulkCopy est très pointilleux sur le format
                        ConvertDataForBulkCopy(iNumThread, dtData, dtColumn, sqlCT, FuzibleQuery);
                    }
                    else if (sqlCT.ColumnLinqType == Type.GetType("System.String") && JobParameters.TrimData)
                    {
                        foreach (DataRow dr in dtData.Rows)
                        {
                            dr[dtColumn] = dr[dtColumn].ToString().Trim();
                        }
                    }

                    //attention il y a un TOUPPER parce qu'il y a un bug dans le driver oracle qui fait qu'il ne reconnait pas les colonnes en minuscule en bulk insert....
                    sBulkColumns.Add(new MySqlBulkCopyColumnMapping(dtColumn.Ordinal, sqlCT.ColumnName));
                }
            }

            Connect(Connection, FuzibleQuery);

            var bulkCopy = new MySqlBulkCopy(ConnexionMySQL)
            {
                DestinationTableName = sTableTarget,
                BulkCopyTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT,
                //bulkCopy. = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE;
                NotifyAfter = JobParameters.GlobalParameters.SQL_BULK_INSERT_BATCH_SIZE
            };
            bulkCopy.MySqlRowsCopied += new MySqlRowsCopiedEventHandler((sender, e) => BulkInsertInMySqlNotify(sender, e, dtData.Rows.Count, iNumThread, sTableTarget, FuzibleQuery));

            foreach (var sMap in sBulkColumns)
            {
                bulkCopy.ColumnMappings.Add(sMap);
            }
            //OracleCommand command = sourceConnection.CreateCommand();
            //command.CommandText = "ALTER SESSION SET NLS_TIMESTAMP_FORMAT = 'DD-MM-YYYY HH24:MI:SS'";
            //command.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);

            try
            {


                //bulkCopy.SqlRowsCopied += BulkInsertInBDDNotify;
                if (bulkCopy.ColumnMappings.Count > 0)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_mysql + sTableTarget + " " + dtData.Rows.Count.ToString() + Languages.Languages.sql_bulkcopy_rows, SQLTools_Enums.LOG_TYPEINFO.DET);

                    await bulkCopy.WriteToServerAsync(dtData);
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_nocolumns + " (" + sTableTarget + ")", FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }

                //transaction.Commit();
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_error, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;

                //try
                //{
                //    Disconnect(Connection);
                //}
                //catch (Exception ex2)
                //{
                //    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_rollbackerror + " (" + ex2.GetType() + " - " + ex2.Message + ")", SQLTools_Enums.LOG_TYPEINFO.ERR);
                //}
            }

            Disconnect(Connection, FuzibleQuery);
        }

        private void BulkInsertInMySQL_OLD(int iNumThread, DataTable dtData, string sTableTarget, List<SQLColumn> SQLTargetColumns, List<string> sListSourceColumnsToKeep, Query FuzibleQuery)
        {
            //string tempCsvFileSpec = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\" + "mysqldumpforbulkcopy_" + Guid.NewGuid() + ".csv";

            //try
            //{
            //    foreach (string sCol in sListSourceColumnsToKeep)
            //    {
            //        SQLColumn sqlCT = SQLTargetColumns.FirstOrDefault(c => c.ColumnName.Equals(sCol));
            //        if (sqlCT != null)
            //        {
            //            DataColumn dtColumn = null;
            //            if (!dtData.Columns.Contains(sCol))
            //            {
            //                foreach (DataColumn dc in dtData.Columns)
            //                {
            //                    if (dc.Caption.Equals(sCol, StringComparison.OrdinalIgnoreCase))
            //                    {
            //                        dtColumn = dc;
            //                        break;
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                dtColumn = dtData.Columns[sCol];
            //            }

            //            if (sqlCT.ColumnLinqType != dtColumn.DataType)
            //            {
            //                //conversion des données car BulkCopy est très pointilleux sur le format
            //                ConvertDataForBulkCopy(iNumThread, dtData, dtColumn, sqlCT);
            //            }
            //        }
            //    }

            //    //écriture d'un fichier temporaire pour le bulk
            //    using (StreamWriter writer = new StreamWriter(tempCsvFileSpec))
            //    {
            //        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_mysqlcsv + Path.GetFileName(tempCsvFileSpec) + " (" + dtData.Rows.Count.ToString() + Languages.Languages.sql_bulkcopy_rows, SQLTools_Enums.LOG_TYPEINFO.DET);
            //        Rfc4180Writer.WriteDataTable(dtData, writer, false);
            //    }

            //    Connect(Connection);
            //    var msbl = new MySqlBulkLoader(ConnexionMySQL);
            //    msbl.TableName = sTableTarget;
            //    msbl.FileName = tempCsvFileSpec;
            //    msbl.FieldTerminator = ",";
            //    msbl.FieldQuotationCharacter = '"';
            //    msbl.LineTerminator = "\r\n";
            //    msbl.Local = true;
            //    //msbl.Timeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT;
            //    msbl.Load();

            //}
            //catch (Exception ex)
            //{
            //    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_error, SQLTools_Enums.LOG_TYPEINFO.ERR);
            //}
            //finally
            //{
            //    try { System.IO.File.Delete(tempCsvFileSpec); }
            //    catch { }
            //}
        }

        private void ConvertDataForBulkCopy(int iNumThread, DataTable dtData, DataColumn dc, SQLColumn sqlCT, Query FuzibleQuery)
        {
            //dc.ReadOnly = false;
            //exceptions : 
            //on ne convertit pas le type INT
            if (sqlCT.ColumnLinqType.ToString().IndexOf(".Int", StringComparison.OrdinalIgnoreCase) > 0 &&
                dc.DataType.ToString().IndexOf(".Int", StringComparison.OrdinalIgnoreCase) > 0)
            { return; }


            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_convert + dc.ColumnName + " (" + dc.DataType.ToString() + ") => " + sqlCT.ColumnLinqType.ToString(), SQLTools_Enums.LOG_TYPEINFO.DET);

            //si la source possède une colonne GUID, il faut absolument la convertir en string
            //switch (dc.DataType.ToString())
            //{
            //    case "System.Guid":
            //        Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.String"));
            //        break;
            //}

            if (!dc.DataType.ToString().Equals("System.String"))
            {
                if (!dc.DataType.ToString().Equals("System.Byte[]")) //on ne convertit pas le varbinary
                {
                    string sColumn = dc.ColumnName;
                    Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.String"));
                    dc = dtData.Columns[sColumn];
                }
            }

            //conversion de la source (peu importe le type) en colonne forcée
            switch (sqlCT.ColumnLinqType.ToString())
            {
                case "System.Int32":
                    Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.Int32"));
                    break;
                case "System.Int16":
                    Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.Int16"));
                    break;
                case "System.Int64":
                    Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.Int64"));
                    break;
                case "System.Guid":
                    Toolbox.ConvertColumnType(dtData, dc, Type.GetType("System.Guid"));
                    break;
                case "System.Boolean":
                    try
                    {
                        if (dc.MaxLength < 4) //TRUE ET FALSE NE PASSERONT PAS
                        {
                            dc.MaxLength = 5;
                        }

                        foreach (DataRow dr in dtData.Rows)
                        {
                            switch (Connection.SConnDriver)
                            {
                                case SQLTools_Enums.BDD.DB_SQLSERVER:
                                    string sSQLServerBit = Toolbox.SetCleanBoolean(dr[dc].ToString(), false, false);
                                    if (sSQLServerBit.Length == 0)
                                    {
                                        dr[dc] = DBNull.Value;
                                    }
                                    else
                                    {
                                        dr[dc] = sSQLServerBit;
                                    }
                                    break;
                                default:
                                    string sSQLBool = Toolbox.SetCleanBoolean(dr[dc].ToString(), false, false);
                                    if (sSQLBool.Length == 0)
                                    {
                                        dr[dc] = DBNull.Value;
                                    }
                                    else
                                    {
                                        dr[dc] = sSQLBool;
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "T" + (iNumThread + 1).ToString("00") + " -> " + Languages.Languages.sql_bulkcopy_convert_error + " (" + dc.ColumnName + " : " + dc.DataType.ToString() + ")", FuzibleQuery.RetryErrorOrWarning);
                    }
                    //switch (CSTarget.SConnDriver)
                    //{
                    //    case SQLTools_Enums.BDD.DB_SQLSERVER:
                    //        Toolbox.ConvertColumnType(dtData, dc, sqlCT.ColumnLinqType);
                    //        break;
                    //}
                    break;
                case "System.DateTimeOffset":
                    foreach (DataRow dr in dtData.Rows)
                    {
                        switch (Connection.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                string sSQLServerDate = Toolbox.SetCleanDate(dr[dc].ToString(), JobParameters, Connection, false, false, 3);
                                dr[dc] = sSQLServerDate;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                string sMysqlDate = Toolbox.SetCleanDate(dr[dc].ToString(), JobParameters, Connection, false, false, 3);
                                dr[dc] = sMysqlDate;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                //DateTime dt;
                                //DateTime.TryParse(dr[dc].ToString(), out dt);
                                //dr[dc] = dt.ToString();
                                DateTime dt;
                                if (dr[dc].ToString().Length > 0)
                                {
                                    dt = ((DateTimeOffset)dr[dc]).UtcDateTime;
                                    string sOracleDate = dt.ToString("MM/dd/yyyy HH:mm:ss");
                                    dr[dc] = sOracleDate;
                                }
                                break;
                        }
                    }
                    break;
                case "System.DateTime":
                    foreach (DataRow dr in dtData.Rows)
                    {
                        switch (Connection.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                string sSQLServerDate = Toolbox.SetCleanDate(dr[dc].ToString(), JobParameters, Connection, false, false, 2);
                                dr[dc] = sSQLServerDate;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                string sMysqlDate = Toolbox.SetCleanDate(dr[dc].ToString(), JobParameters, Connection, false, false, 2);
                                dr[dc] = sMysqlDate;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                //DateTime dt;
                                //DateTime.TryParse(dr[dc].ToString(), out dt);
                                //dr[dc] = dt.ToString();
                                DateTime dt;
                                DateTime.TryParse(dr[dc].ToString(), out dt);
                                string sOracleDate = dt.ToString("MM/dd/yyyy HH:mm:ss"); //"10/10/2020 00:00:00"
                                                                                         //Toolbox.SetCleanDate(dr[dc].ToString(), JobParameters, CSTarget, false, false, 2);
                                dr[dc] = sOracleDate;
                                break;
                        }
                    }
                    //petit trick : si la BDD cible attend des dates types DD-MM-YYYY, la colonne ne peut pas être convertie en Datetime, on la laisse comme tel
                    switch (Connection.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            Toolbox.ConvertColumnType(dtData, dc, sqlCT.ColumnLinqType);
                            break;
                    }
                    break;

                case "System.Decimal":

                    CultureInfo culture = CultureInfo.CurrentCulture;
                    string decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;

                    foreach (DataRow dr in dtData.Rows)
                    {
                        string sNewDecimal = Toolbox.SetCleanNumber(dr[dc].ToString(), Connection, SQLTools_Enums.TYPE_DATA.DECIMAL, false, false, false);
                        sNewDecimal = sNewDecimal.Replace(decimalSeparator.Equals(",") ? "." : ",", decimalSeparator);

                        switch (Connection.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                //sNewDecimal = Connection.DecimalLocale ? sNewDecimal.Replace(".", ",") : sNewDecimal;
                                break;
                        }

                        if (sNewDecimal.Length == 0)
                        { dr[dc] = DBNull.Value; }
                        else
                        { dr[dc] = sNewDecimal; }
                    }
                    break;
            }
        }

        static void GetAppLock(string name, SqlTransaction tran)
        {
            var cmd = new SqlCommand("sp_getapplock", tran.Connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = tran;
            var pResource = cmd.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255));
            pResource.Value = name;

            var pLockMode = cmd.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.VarChar, 32));
            pLockMode.Value = "Exclusive";

            cmd.ExecuteNonQuery();
        }

        private PrepareDataForTarget CheckCreateAlterTableColumns(bool bTableExists, DataTable dtOut, string sTableName, Query FuzibleQuery)
        {
            bool bDoNotRecreateTable = bTableExists;

            PrepareDataForTarget pdFT = new();

            if (dtOut.Rows.Count > 0)
            {
                //clean des noms de colonne
                foreach (DataColumn dt in dtOut.Columns)
                {
                    dt.Caption = dt.Caption.Replace("\"", "_");
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_anayzingschemas, SQLTools_Enums.LOG_TYPEINFO.DET);

                try
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_gettingcolumnsds, SQLTools_Enums.LOG_TYPEINFO.DET);
                    List<SQLColumn> sListColsSource = Toolbox.GetSQLColumnsFromDataTable(dtOut);

                    if (JobParameters.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING && !FuzibleQuery.ConnectionSrc.SConnDriver.ToString()[..2].Equals("DB"))
                    {
                        //En mode synchro, c'est qu'on a déjà un FieldAnalyzer propre, on ne veut pas réécraser les types
                        //ne s'applique qu'aux sources qui ne sont pas des BDD (CSV, etc...) pour lequelles on a fait une analyse de types manuelle (SHS)
                    }
                    else
                    { 
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_settingfieldsfromds, SQLTools_Enums.LOG_TYPEINFO.DET);
                        FuzibleQuery.QueryAnalyzer.SetFieldsBySQLColumns(dtOut.TableName, sListColsSource);
                    }

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_mergingfieldsfromcq, SQLTools_Enums.LOG_TYPEINFO.DET);
                    List<Query.QField> ListFieldsInQuery = Toolbox.MergeFieldsFromCrossQueries(FuzibleQuery);

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_creatingtable + sTableName, SQLTools_Enums.LOG_TYPEINFO.DET);
                    //en cas de création de table on a besoin de récupérer la définition des champs pour construire l'entête
                    if (!bDoNotRecreateTable || JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP)
                    {
                        bDoNotRecreateTable = false; //en cas de drop
                        SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                        List<SQLColumn> SColumns = SHS.GetListFieldsTypesFromDataset(dtOut, -1, FuzibleQuery, ClassPurpose);
                        FuzibleQuery.QueryAnalyzer.SetFieldsAnalyzer(SColumns);
                        ListFieldsInQuery = FuzibleQuery.QueryAnalyzer.Fields;
                    }

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_gettingcolumnsfromtargettable, SQLTools_Enums.LOG_TYPEINFO.DET);
                    //récupération des colonnes cible (si le nombre de colonnes dans la table cible n'est pas égal à la source, on filtre)
                    if (JobParameters.TargetTableBehavior != SQLTools_Enums.TARGET_TABLE_METHOD.DROP)
                    {
                        pdFT.ColumnsTarget = GetFieldsFromTable(sTableName, FuzibleQuery); //récupération des colonnes cible par le schéma et non pas par l'autodétection

                        foreach (var c in pdFT.ColumnsTarget)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_targetcolumn + c.ColumnName +
                                                                                                                    " (Type:" + c.ColumnType.ToString() +
                                                                                                                    ",Unique:" + c.IsUnique.ToString() +
                                                                                                                    ",Index:" + c.ColumnIndex.ToString() +
                                                                                                                    ",Default:" + c.DefaultValue +
                                                                                                                    ",Size:" + c.ColumnSize + ")", SQLTools_Enums.LOG_TYPEINFO.DET);
                        }

                        if (pdFT.ColumnsTarget.Count == 0) //Cas d'une table inexistante dans la cible : on doit pouvoir la créer donc avoir une liste de colonnes
                        {
                            foreach (DataColumn dtC in dtOut.Columns)
                            {
                                pdFT.ColumnsTarget.Add(SQLColumn.SQLColumnFromDataColumn(dtC, SqlCharTypeCompatibility));
                            }
                        }
                    }
                    else
                    {
                        foreach (DataColumn dtC in dtOut.Columns)
                        {
                            pdFT.ColumnsTarget.Add(SQLColumn.SQLColumnFromDataColumn(dtC, SqlCharTypeCompatibility));
                        }
                    }

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_buildingheader, SQLTools_Enums.LOG_TYPEINFO.DET);

                    //re-parcours pour contrôle
                    List<SQLColumn> ListColumnToCreate = new(); // création d'une liste de colonnes qui n'existeraient pas dans la cible : il faudra les créer

                    foreach (DataColumn c in dtOut.Columns)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_sourcecolumn + c.ColumnName +
                                                                                                                " (Caption:" + c.Caption +
                                                                                                                ",Type:" + c.DataType.ToString() +
                                                                                                                ",Unique:" + c.Unique.ToString() +
                                                                                                                ",Index:" + c.Ordinal.ToString() +
                                                                                                                ",Default:" + c.DefaultValue +
                                                                                                                ",Size:" + c.MaxLength.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.DET);
                    }

                    for (int cptCell = 0; cptCell <= dtOut.Columns.Count - 1; cptCell += 1)
                    {
                        Query.QField qf = ListFieldsInQuery.FirstOrDefault(f => f.Alias.Equals(dtOut.Columns[cptCell].ColumnName, StringComparison.OrdinalIgnoreCase));
                        if (qf == null)
                        {
                            // ce contrôle est probablement idiot mais dans le doute...
                            qf = ListFieldsInQuery.FirstOrDefault(f => f.Name.Equals(dtOut.Columns[cptCell].ColumnName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (qf != null)
                        {
                            if (Toolbox.GetColumnNamesFromSQLColumnObjects(pdFT.ColumnsTarget).Contains(dtOut.Columns[cptCell].ColumnName, StringComparer.OrdinalIgnoreCase))
                            {
                                pdFT.sListeNumColonnesToKeepSource.Add(dtOut.Columns[cptCell].ColumnName); //on sauvegarde la liste des positions des colonnes à conserver
                                if (!bDoNotRecreateTable) { pdFT.sEnteteCreateTable += string.Concat(EchappementChar, dtOut.Columns[cptCell].ColumnName, EchappementChar, qf.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), qf.FieldAnalyzer.ColumnAllowsNullValues ? "" : " NOT NULL", ","); }
                                pdFT.sEntete += string.Concat(EchappementChar, dtOut.Columns[cptCell].ColumnName, EchappementChar, ",");
                            }
                            else if (dtOut.Columns[cptCell].Caption.Length > 0 && Toolbox.GetColumnNamesFromSQLColumnObjects(pdFT.ColumnsTarget).Contains(dtOut.Columns[cptCell].Caption, StringComparer.OrdinalIgnoreCase))
                            {
                                pdFT.sListeNumColonnesToKeepSource.Add(dtOut.Columns[cptCell].Caption); //on sauvegarde la liste des positions des colonnes à conserver
                                if (!bDoNotRecreateTable) { pdFT.sEnteteCreateTable += string.Concat(EchappementChar, dtOut.Columns[cptCell].Caption, EchappementChar, qf.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), qf.FieldAnalyzer.ColumnAllowsNullValues ? "" : " NOT NULL", ","); }
                                pdFT.sEntete += string.Concat(EchappementChar, dtOut.Columns[cptCell].Caption, EchappementChar, ",");
                            }
                            else
                            {
                                if (JobParameters.AlterColumnTypeOnInsert) // on ne prévoit l'ajout de colonne que si on a explicitement demandé à ce que la cible puisse subir une modification de schéma
                                {
                                    ListColumnToCreate.Add(qf.FieldAnalyzer);
                                    pdFT.sListeNumColonnesToKeepSource.Add(dtOut.Columns[cptCell].ColumnName); //on sauvegarde la liste des positions des colonnes à conserver
                                    if (!bDoNotRecreateTable) { pdFT.sEnteteCreateTable += string.Concat(EchappementChar, dtOut.Columns[cptCell].ColumnName, EchappementChar, qf.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), qf.FieldAnalyzer.ColumnAllowsNullValues ? "" : " NOT NULL", ","); }
                                    pdFT.sEntete += string.Concat(EchappementChar, dtOut.Columns[cptCell].ColumnName, EchappementChar, ",");
                                }
                                //en cas de refus de modification de schéma, on veut quand même créer la ou les colonnes optionnelles et insérer les données dedans
                                //car le programme crée ces colonnes optionnelles après cette analyse, donc au premier "jet", on aura bien une nouvelle colonne mais pas de données dedans...
                                else if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(dtOut.Columns[cptCell].ColumnName))
                                {
                                    ListColumnToCreate.Add(qf.FieldAnalyzer);
                                    pdFT.sListeNumColonnesToKeepSource.Add(dtOut.Columns[cptCell].ColumnName); //on sauvegarde la liste des positions des colonnes à conserver
                                    if (!bDoNotRecreateTable) { pdFT.sEnteteCreateTable += string.Concat(EchappementChar, dtOut.Columns[cptCell].ColumnName, EchappementChar, qf.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), qf.FieldAnalyzer.ColumnAllowsNullValues ? "" : " NOT NULL", ","); }
                                    pdFT.sEntete += string.Concat(EchappementChar, dtOut.Columns[cptCell], EchappementChar, ",");
                                }
                            }
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_preparedata_noanalyzerforcolumn, " (", dtOut.Columns[cptCell].ColumnName, ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                    }

                    pdFT.sEntete = pdFT.sEntete[0..^1];
                    //sEnteteCreateTable = sEnteteCreateTable.Substring(0, sEnteteCreateTable.Length - 1);

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_preparedata_targettablebehavior, SQLTools_Enums.LOG_TYPEINFO.DET);

                    //création de la table avec l'entête récemment crée si inexistante
                    if (!bDoNotRecreateTable)
                    {
                        bool bDrop = false;
                        if (JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP && bTableExists) { bDrop = true; }

                        //gestion de l'ajout de clé primaire sur du SQLLite
                        if (JobParameters.CreatePrimaryKeyAfterHavingCreatedATable && Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                        {
                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            List<SQLColumn> sListFieldsPK = SHS.GuessPKFromQuery(dtOut, SqlCharTypeCompatibility, null);
                            if (sListFieldsPK.Count > 0) // ajout de la clé primaire sur la table cible
                            {
                                StringBuilder sbPK = new();
                                sbPK.Append("PRIMARY KEY(");
                                for (int iP = 0; iP < sListFieldsPK.Count; iP++)
                                {
                                    sbPK.Append("\"" + sListFieldsPK[iP].ColumnName + (iP == sListFieldsPK.Count - 1 ? "\") " : "\","));
                                }
                                pdFT.sEnteteCreateTable += sbPK.ToString();
                            }
                        }

                        CreateOutputTable(sTableName, bDrop, pdFT.sEnteteCreateTable, FuzibleQuery);
                        if (CheckForExistingTable(sTableName, FuzibleQuery))
                        {
                            pdFT.bTableJustCreated = true;
                        }
                        else { throw new Exception(Languages.Languages.sql_synchro_cantcreatetargettable); }
                    }

                    //en SQLLite on ne peut pas ajouter de clé primaire, elle est crée en même temps que la table...
                    if (Connection.SConnDriver != SQLTools_Enums.BDD.DB_SQLITE)
                    {//création de clé primaire si disponible
                        if (pdFT.bTableJustCreated && JobParameters.CreatePrimaryKeyAfterHavingCreatedATable)
                        {
                            CreatePrimaryKeyFromDt(sTableName, dtOut, FuzibleQuery);
                        }
                    }

                    pdFT.ColumnsTarget.AddRange(CheckForOptionalColumnsAndAddIfNeeded(sTableName, dtOut, pdFT.ColumnsTarget, FuzibleQuery));

                    if (pdFT.bTableJustCreated)
                    {
                        pdFT.ColumnsTarget = GetFieldsFromTable(sTableName, FuzibleQuery);
                    } //après la création de la table, on l'interroge pour récupérer les bons types de colonnes

                    if (!pdFT.bTableJustCreated) //parfois, on peut avoir demandé un mode "TRUNCATE" sur une table qui n'existait pas : inutile de faire les opérations qui suivent dans ce cas
                    {
                        Int64 iRowsTarget = CountRowsInTargetTable(sTableName, FuzibleQuery);

                        //modification des types de colonnes à l'insert si besoin
                        if (JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING ||
                            JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE ||
                            JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM ||
                            JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME ||
                            JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.TRUNCATE ||
                            JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.FULL_DELETE)
                        {
                            if (JobParameters.AlterColumnTypeOnInsert)
                            {
                                //création des colonnes manquantes si besoin
                                for (int iC = 0; iC < ListColumnToCreate.Count; iC++)
                                {
                                    //analyse de la colonne si ça n'a pas été fait
                                    if (JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP || pdFT.bTableJustCreated)
                                    {
                                    }
                                    else
                                    {
                                        if (JobParameters.AlterColumnTypeOptions > 0 && JobParameters.AlterColumnTypeOptions <= 3)
                                        {
                                            //faire marcher l'analyzer pour déterminer le type au plus précis
                                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                                            FuzibleQuery.QueryAnalyzer.SetFieldsAnalyzer(SHS.GetListFieldsTypesFromDataset(dtOut, ListColumnToCreate[iC].ColumnIndex, FuzibleQuery, ClassPurpose));
                                            //reprendre la colonne (ListColumnToCreate n'a pas l'update du QueryAnalyzer)
                                            foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                                            {
                                                if (qF.FieldAnalyzer.ColumnName.Equals(ListColumnToCreate[iC].ColumnName, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    ListColumnToCreate[iC] = qF.FieldAnalyzer; break;
                                                }
                                            }
                                        }
                                    }

                                    if (JobParameters.AlterColumnTypeOptions > 0 && JobParameters.AlterColumnTypeOptions <= 3)
                                    {
                                        if (iRowsTarget == 0)
                                        {
                                            bool bOK = CreateColumnInTarget(sTableName, ListColumnToCreate[iC], ListColumnToCreate[iC].ColumnAllowsNullValues, FuzibleQuery);
                                            if (bOK) { pdFT.ColumnsTarget.Add(ListColumnToCreate[iC]); }
                                        }
                                        else
                                        {
                                            bool bOK = CreateColumnInTarget(sTableName, ListColumnToCreate[iC], true, FuzibleQuery);
                                            if (bOK) { pdFT.ColumnsTarget.Add(ListColumnToCreate[iC]); }
                                        }
                                    }
                                }
                                //altération des colonnes existantes
                                CompareFieldsFromSourceToTarget(sTableName, ref pdFT.ColumnsTarget, FuzibleQuery, dtOut, iRowsTarget, pdFT.bTableJustCreated);
                            }
                            else
                            {
                                //traitement des colonnes optionnelles
                                //pour celles de type date ou compteur, pas besoin de se préoccuper de leur taille
                                if (JobParameters.OptionalDBName_OnInsert)
                                {
                                    ResizeSQLColumn(sTableName, JobParameters.TargetAddDbName, dtOut, FuzibleQuery);
                                }
                                if (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0)
                                {
                                    string[] sDynamicParam = JobParameters.OptionalDynamicParamField_OnInsert.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var sParam in sDynamicParam)
                                    {
                                        string sColName = "DYNPARAM";
                                        if (sParam.IndexOf("=") > -1)
                                        {
                                            sColName = sParam.Split(Convert.ToChar("="))[0];
                                            ResizeSQLColumn(sTableName, sColName, dtOut, FuzibleQuery);
                                        }
                                    }
                                }
                            }

                            //--!!--attention, cas très spécial des données Boolean qui vont atterir dans une colonne en varchar(1)
                            for (int cpt = 0; cpt < pdFT.ColumnsTarget.Count; cpt += 1)
                            {
                                Query.QField SQLC = null;
                                try
                                {
                                    SQLC = FuzibleQuery.QueryAnalyzer.Fields.First(s => s.FieldAnalyzer.ColumnName.Equals(pdFT.ColumnsTarget[cpt].ColumnName, StringComparison.OrdinalIgnoreCase));
                                }
                                catch { }
                                //on va ici comparer le type de colonnes sources et cible. Si le type est différent, on va mettre à jour le schéma cible pour le faire matcher avec la source
                                if (SQLC != null)
                                {
                                    decimal iDec;
                                    decimal.TryParse(pdFT.ColumnsTarget[cpt].ColumnSize, out iDec);

                                    //problème si colonne cible est NVARCHAR(1) et source est BOOLEAN
                                    if (SQLC.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Boolean")
                                        && pdFT.ColumnsTarget[cpt].ColumnLinqType == Type.GetType("System.String")
                                        && pdFT.ColumnsTarget[cpt].ColumnSize.Length > 0
                                        && iDec < 6)
                                    {
                                        DataColumn dc = null;
                                        foreach (DataColumn c in dtOut.Columns)
                                        {
                                            if (c.ColumnName.Equals(SQLC.Name))
                                            {
                                                dc = c;
                                                break;
                                            }
                                        }
                                        if (dc != null) { Toolbox.ConvertColumnType(dtOut, dc, Type.GetType("System.Byte")); }
                                    }
                                }
                            }
                        }
                    }

                    //on va d'abord mettre d'équerre les colonnes source avec les colonnes cible (au cas ou la création d'une colonne dans la cible aurait merdé par exemple)
                    //List<SQLColumn> sListToRemove = new List<SQLColumn>();
                    //foreach (SQLColumn SQLC in pdFT.ColumnsTarget)
                    //{
                    //    //cbINISection.Items.Cast<ComboBoxItem>().Select(c => (string)c.Tag).ToList()
                    //    if (!pdFT.ColumnsSource.Select(sF => sF.ColumnName.ToUpper()).ToList().Contains(SQLC.ColumnName.ToUpper()))
                    //    { sListToRemove.Add(SQLC); }
                    //}
                    //foreach (SQLColumn SQLC in sListToRemove) { pdFT.ColumnsSource.Remove(SQLC); }

                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                    pdFT.bStepOK = false;
                }
            }

            return pdFT;
        }

        private void ResizeSQLColumn(string sTableName, string sColumnName, DataTable dt, Query FuzibleQuery)
        {
            var colTarget = GetFieldsFromTable(sTableName, FuzibleQuery, sColumnName);
            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
            var colSource = SHS.GetListFieldsTypesFromDataset(dt, dt.Columns[sColumnName].Ordinal, FuzibleQuery, ClassPurpose);

            try
            {
                if (colTarget[0].ColumnLinqType == colSource[0].ColumnLinqType && Convert.ToInt32(colTarget[0].ColumnSize) < Convert.ToInt32(colSource[0].ColumnSize))
                {
                    bool bNullAuthorized = false;
                    string sQuery;
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_TYPE);
                    sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                    sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                    sQuery = sQuery.Replace("{COLUMN_NAME}", sColumnName);
                    sQuery = sQuery.Replace("{COLUMN_TYPE}", colSource[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale));
                    sQuery = sQuery.Replace("{NULL_NOT_NULL}", bNullAuthorized ? "NULL" : "NOT NULL");
                    if (sQuery.Length > 0)
                    {
                        int iErr = MyLog.JobErrors + MyLog.JobWarnings;
                        ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sColumnName);
                        if (iErr == (MyLog.JobErrors + MyLog.JobWarnings))
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_info01, sColumnName, Languages.Languages.sql_changetype_info02, colTarget[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, colSource[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_info01_failed, sColumnName, Languages.Languages.sql_changetype_info02, colTarget[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, colSource[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }
                    }
                }
            }
            catch
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_info01_failed, sColumnName, Languages.Languages.sql_changetype_info02, colTarget[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, colSource[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.WNG);
            }
        }

        internal class PrepareDataForTarget
        {
            public bool bStepOK = true;
            public bool bTableJustCreated = false;

            public string sEnteteCreateTable = "";
            public string sEntete = "";
            public List<SQLColumn> ColumnsTarget = new();
            public List<string> sListeNumColonnesToKeepSource = new();
        }

        public static List<string> BuildListFromDs(DataSet dsData, int numTable = 0, int numCell = 0)
        {
            List<string> lString = new();
            try
            {
                bool bIsBitArray = false;

                if (dsData != null && dsData.Tables.Count > numTable && dsData.Tables[numTable].Columns.Count > numCell)
                {
                    if (dsData.Tables[numTable].Columns[numCell].DataType.Name.Equals("BitArray", StringComparison.OrdinalIgnoreCase))
                    {
                        bIsBitArray = true;
                    }

                    foreach (DataRow dsRow in dsData.Tables[numTable].Rows)
                    {
                        if (bIsBitArray)
                        {
                            var bA = (BitArray)dsRow[numCell];
                            lString.Add(bA.ToBitString());
                        }
                        else
                        {
                            if (!dsRow[numCell].ToString().Trim().Equals(""))
                            {
                                lString.Add(dsRow[numCell].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return lString;

        }

        public static string BuildStringFromDs(DataSet dsData, int numTable = 0, int numRow = 0, int numCell = 0)
        {
            string sStr = " ";

            try
            {
                if (dsData != null && dsData.Tables.Count > numTable && dsData.Tables[numTable].Columns.Count > numCell)
                {
                    if (dsData.Tables[numTable].Rows[numRow][numCell].ToString().Equals(""))
                    {
                        sStr = "";
                    }
                    else
                    {
                        if (dsData.Tables[numTable].Columns[numCell].DataType.Name.Equals("BitArray", StringComparison.OrdinalIgnoreCase))
                        {
                            var bA = (BitArray)dsData.Tables[numTable].Rows[numRow][numCell];
                            sStr = bA.ToBitString();
                        }
                        else { sStr = dsData.Tables[numTable].Rows[numRow][numCell].ToString(); }
                    }
                }

            }
            catch (Exception)
            {
                throw;
            }

            return sStr.Trim();

        }

        public List<string> GetAllTablesFromDatabase(string sDBName, string sFilter, bool bWithViews, CancellationToken ctsToken, Query FuzibleQuery)
        {
            if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
            {
                List<string> sListTables = new();
                try
                {
                    Connect(Connection, FuzibleQuery);
                    DataTable dtAccessT = ConnexionACCESS.GetSchema("Tables");
                    Disconnect(Connection, FuzibleQuery);
                    if (dtAccessT != null && dtAccessT.Rows.Count > 0)
                    {
                        foreach (DataRow dr in dtAccessT.Rows)
                        {
                            if (sFilter.Length > 0) { if (dr[2].ToString().IndexOf(sFilter) > -1) { sListTables.Add(dr[2].ToString()); } }
                            else { sListTables.Add(dr[2].ToString()); }
                        }
                    }

                    if (bWithViews)
                    {
                        Connect(Connection, FuzibleQuery);
                        DataTable dtAccessV = ConnexionACCESS.GetSchema("Views");
                        Disconnect(Connection, FuzibleQuery);
                        if (dtAccessV != null && dtAccessV.Rows.Count > 0)
                        {
                            foreach (DataRow dr in dtAccessV.Rows)
                            {
                                if (sFilter.Length > 0) { if (dr[2].ToString().IndexOf(sFilter) > -1) { sListTables.Add(dr[2].ToString()); } }
                                else { sListTables.Add(dr[2].ToString()); }
                            }
                        }
                    }
                }
                catch { }
                return sListTables;
            }
            else
            {
                DataSet dsData = new();
                //List<string> sListTables = new List<string>();

                string sQuery;
                if (sFilter.Length > 0)
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_TABLES_FILTERED);
                }
                else
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_TABLES);
                }
                sQuery = sQuery.Replace("{DATABASE_NAME}", sDBName);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{LIKE_PATTERN}", sFilter);
                if (sQuery.Length > 0)
                {
                    if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE) //bug driver lors de l'exécution d'une telle requête
                    {

                        JobParameters.ExportUseDataset = false;
                        Query q = new(JobParameters, sQuery);
                        dsData = GetDataFromDatabase(q, false).Result;
                    }
                    else { dsData = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, FuzibleQuery, sDBName, false); }
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.GET_TABLES.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                List<string> sListTables = BuildListFromDs(dsData);

                if (bWithViews)
                {
                    dsData.Clear();
                    if (sFilter.Length > 0)
                    {
                        sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_VIEWS_FILTERED);
                    }
                    else
                    {
                        sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_VIEWS);
                    }
                    sQuery = sQuery.Replace("{DATABASE_NAME}", sDBName);
                    sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                    sQuery = sQuery.Replace("{LIKE_PATTERN}", sFilter);
                    if (sQuery.Length > 0)
                    {
                        dsData = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, FuzibleQuery, sDBName, false);
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.GET_VIEWS.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    sListTables.AddRange(BuildListFromDs(dsData));
                }
                return sListTables;
            }
        }

        public string Connect(CONNString CS, Query FuzibleQuery)
        {
            string sCState = Languages.Languages.sql_connect_notconnected;
            string sC = CS.SConnString(JobParameters.DynParams);

            //if (sC.Contains("Authentication=", StringComparison.OrdinalIgnoreCase))
            //{
            //    SQLServerAzureAuthentication = true;
            //    sC = Regex.Replace(sC, "(.+)(Authentication=.+[^;];?)(.*)", "$1$3", RegexOptions.IgnoreCase);
            //}

            //remplacement de la database de la chaîne de connexion pour matcher avec celle qui a été définie par l'utilisateur
            //switch (ClassPurpose)
            //{
            //    case SQLTools_Enums.CLASS_PURPOSE.SRC:
            //        sC = Toolbox.ReplaceDBNameInConnectionString(CS, JobParameters.DatabaseName_Source, JobParameters.DynParams);
            //        break;
            //    case SQLTools_Enums.CLASS_PURPOSE.TRG:
            //        sC = Toolbox.ReplaceDBNameInConnectionString(CS, JobParameters.DatabaseName_Target, JobParameters.DynParams);
            //        break;
            //    case SQLTools_Enums.CLASS_PURPOSE.LOG:
            //        //on laisse celle qui est mentionnée dans la chaîne
            //        break;
            //    case SQLTools_Enums.CLASS_PURPOSE.PRG:
            //        //idem
            //        break;
            //}

            try
            {
                //connexion fermée : on ouvre par défaut
                if (ConnexionState == 0)
                {
                    switch (CS.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            try
                            {
                                ConnexionACCESS = new OleDbConnection(sC);
                                try
                                {
                                    ConnexionACCESS.Open();
                                    if (DatabaseName.Length > 0)
                                    {
                                        try
                                        {
                                            ConnexionACCESS.ChangeDatabase(DatabaseName);
                                        }
                                        catch (OleDbException ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_switch_db_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        }
                                    }
                                }
                                catch (OleDbException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                }//CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionACCESS.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion MYSQL Ouverte");
                            }
                            catch (OleDbException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            try
                            {
                                if (JobParameters.SQLTargetBulkCopy && sC.IndexOf("AllowLoadLocalInfile=", StringComparison.OrdinalIgnoreCase) == -1)
                                {
                                    sC = sC + (sC.EndsWith(";") ? "AllowLoadLocalInfile=true" : ";AllowLoadLocalInfile=true");
                                }
                                ConnexionMySQL = new MySqlConnection(sC);
                                try
                                {
                                    ConnexionMySQL.Open();
                                    if (DatabaseName.Length > 0)
                                    {
                                        try
                                        {
                                            ConnexionMySQL.ChangeDatabase(DatabaseName);
                                        }
                                        catch (MySqlException ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_switch_db_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        }
                                    }
                                }
                                catch (MySqlException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                }//CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionMySQL.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion MYSQL Ouverte");
                            }
                            catch (MySqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            try
                            {
                                ConnexionSQLServer = new SqlConnection(sC);
                                try
                                {
                                    ConnexionSQLServer.Open();

                                    if (DatabaseName.Length > 0)
                                    {
                                        try
                                        {
                                            ConnexionSQLServer.ChangeDatabase(DatabaseName);
                                        }
                                        catch (SqlException ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_switch_db_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        }
                                    }
                                }
                                catch (SqlException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                } //CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionSQLServer.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion SQL SERVER Ouverte");
                            }
                            catch (SqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_SQLITE:
                            try
                            {
                                ConnexionSqlite = new SqliteConnection(sC);
                                try
                                {
                                    ConnexionSqlite.Open();
                                    //ConnexionSqlite.ChangeDatabase(DatabaseName);
                                }
                                catch (SqliteException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                } //CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionSqlite.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion SQL SERVER Ouverte");
                            }
                            catch (SqliteException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_ODBC:
                            try
                            {
                                ConnexionODBC = new OdbcConnection(sC);
                                try
                                {
                                    ConnexionODBC.Open();
                                    try { if (DatabaseName.Length > 0) { ConnexionODBC.ChangeDatabase(DatabaseName); } }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_connect_cantchangedb, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }
                                catch (OdbcException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                } //CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionODBC.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion ODBC Ouverte");
                            }
                            catch (OdbcException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            try
                            {
                                if (JobParameters.SQLTargetBulkCopy && sC.IndexOf("Include Error Detail=", StringComparison.OrdinalIgnoreCase) == -1)
                                {
                                    sC = sC + (sC.EndsWith(";") ? "Include Error Detail=true" : ";Include Error Detail=true");
                                }

                                ConnexionPOSTGRE = new NpgsqlConnection(sC);
                                try
                                {
                                    ConnexionPOSTGRE.Open();

                                    if (DatabaseName.Length > 0)
                                    {
                                        try
                                        {
                                            ConnexionPOSTGRE.ChangeDatabase(DatabaseName);
                                        }
                                        catch (NpgsqlException ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_switch_db_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        }
                                    }
                                }
                                catch (NpgsqlException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                } // CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionPOSTGRE.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion POSTGRE Ouverte");
                            }
                            catch (NpgsqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_ORACLE:
                            try
                            {
                                ConnexionORACLE = new OracleConnection(sC);
                                sCState = ConnexionORACLE.State.ToString();
                                try
                                {
                                    ConnexionORACLE.Open();

                                    //non autorisé en oracle
                                    //if (DatabaseName.Length > 0) { ConnexionORACLE.ChangeDatabase(DatabaseName); }
                                }
                                catch (OracleException ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                } //CreateDatabase(CS, DatabaseName, true); }
                                sCState = ConnexionORACLE.State.ToString();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion POSTGRE Ouverte");
                            }
                            catch (OracleException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                    }

                    if (JobParameters.KeepConnexionAfterQuery)
                    {
                        ConnexionState = 2;
                        //connexion ouverte pour une durée indéterminée
                    }
                    else
                    {
                        ConnexionState = 1;
                        //connexion ouverte
                    }

                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sCState;

        }

        public void Disconnect(CONNString CS, Query FuzibleQuery)
        {
            Task t = Task.Factory.StartNew(() =>
            {
                try
                {
                    switch (CS.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            try
                            {
                                ConnexionACCESS.Close();
                                ConnexionACCESS.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion MYSQL Fermée");
                            }
                            catch (OleDbException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            try
                            {
                                ConnexionMySQL.Close();
                                ConnexionMySQL.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion MYSQL Fermée");
                            }
                            catch (MySqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            try
                            {
                                ConnexionSQLServer.Close();
                                ConnexionSQLServer.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion SQL SERVER Fermée");
                            }
                            catch (SqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_SQLITE:
                            try
                            {
                                ConnexionSqlite.Close();
                                ConnexionSqlite.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion SQL SERVER Fermée");
                            }
                            catch (SqliteException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_ODBC:
                            try
                            {
                                ConnexionODBC.Close();
                                ConnexionODBC.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion ODBC Fermée");
                            }
                            catch (OdbcException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            try
                            {
                                ConnexionPOSTGRE.Close();
                                ConnexionPOSTGRE.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion POSTGRE Fermée");
                            }
                            catch (NpgsqlException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_ORACLE:
                            try
                            {
                                ConnexionORACLE.Close();
                                ConnexionORACLE.Dispose();
                                //Log_SQLTools.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), "Connexion POSTGRE Fermée");
                            }
                            catch (OracleException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                            break;

                    }

                    ConnexionState = 0;

                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            });

            DateTime dtDis = DateTime.Now;
            bool bNotFinished = true;
            while (bNotFinished)
            {
                Thread.Sleep(100);
                if ((DateTime.Now - dtDis).Seconds > 3)
                {
                    Monitoring.AddThread(t, System.Reflection.MethodBase.GetCurrentMethod());
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_dicreetdisconnect, SQLTools_Enums.LOG_TYPEINFO.DET);
                    break;
                }
                else if ((DateTime.Now - dtDis).Seconds > 60)
                {
                    try
                    {
                        //dans ce cas on arrête tout
                        bNotFinished = false;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_dicreetdisconnect_fatal, SQLTools_Enums.LOG_TYPEINFO.WNG);
                        t.Dispose();
                        t = null;
                    }
                    catch
                    {
                        throw;
                    }
                }
                if (t.Status == TaskStatus.RanToCompletion || t.Status == TaskStatus.Faulted || t.Status == TaskStatus.Canceled)
                { bNotFinished = false; }
            }
        }

        public void AlterTableConstraints(string sTable, bool bDisable, Query FuzibleQuery)
        {
            if (JobParameters.SQLTargetDisableConstraints)
            {
                string sQuery;
                if (bDisable)
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DISABLE_TABLE_CONSTRAINTS);
                }
                else
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.ENABLE_TABLE_CONSTRAINTS);
                }
                sQuery = sQuery.Replace("{TABLE_NAME}", sTable);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
                if (sQuery.Length > 0)
                {
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTable);
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.DISABLE_TABLE_CONSTRAINTS.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
            }
        }

        public void CreateDatabase(CONNString sConnString, string sDBName, bool bWithConnexion, Query FuzibleQuery)
        {
            switch (sConnString.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_MYSQL:
                    string sRa = string.Concat("CREATE DATABASE ", sDBName);
                    string sC = Toolbox.ReplaceDBNameInConnectionString(sConnString, "information_schema", JobParameters.DynParams);
                    ConnexionMySQL = new MySqlConnection(sC);
                    ConnexionMySQL.OpenAsync(Monitoring.TaskCancellationToken);

                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRa, FuzibleQuery, sDBName);
                    if (bWithConnexion)
                    {
                        ConnexionMySQL.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //INCERTAIN
                    string sRb = string.Concat("CREATE DATABASE ", sDBName);
                    string sCb = Toolbox.ReplaceDBNameInConnectionString(sConnString, "information_schema", JobParameters.DynParams);
                    ConnexionODBC = new OdbcConnection(sCb);
                    ConnexionODBC.OpenAsync(Monitoring.TaskCancellationToken);
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRb, FuzibleQuery, sDBName);
                    if (bWithConnexion)
                    {
                        ConnexionODBC.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    //en ORACLE, on ne peut pas créer de DB par la programmation (userspaces)
                    //string sRc = string.Concat("CREATE DATABASE ", sDBName);
                    //sConnexionString = SQLTools_Utils.ReplaceDBNameInConnexionString(sConnexionString, "master");
                    ConnexionORACLE = new OracleConnection(sConnString.SConnString(JobParameters.DynParams));
                    ConnexionORACLE.OpenAsync(Monitoring.TaskCancellationToken);
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRc);
                    if (bWithConnexion)
                    {
                        ConnexionORACLE.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    string sRd = string.Concat("CREATE DATABASE ", sDBName);
                    string sCc = Toolbox.ReplaceDBNameInConnectionString(sConnString, "postgres", JobParameters.DynParams);
                    ConnexionPOSTGRE = new NpgsqlConnection(sCc);
                    ConnexionPOSTGRE.OpenAsync(Monitoring.TaskCancellationToken);
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRd, FuzibleQuery, sDBName);
                    if (bWithConnexion)
                    {
                        ConnexionPOSTGRE.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    string sRe = string.Concat("CREATE DATABASE ", sDBName);
                    string sCd = Toolbox.ReplaceDBNameInConnectionString(sConnString, "master", JobParameters.DynParams);

                    //if (sCd.Contains("Authentication=", StringComparison.OrdinalIgnoreCase))
                    //{
                    //    SQLServerAzureAuthentication = true;
                    //    sCd = Regex.Replace(sCd, "(.+)(Authentication=.+[^;];?)(.*)", "$1$3", RegexOptions.IgnoreCase);
                    //}

                    ConnexionSQLServer = new SqlConnection(sCd);

                    ConnexionSQLServer.OpenAsync(Monitoring.TaskCancellationToken);
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRe, FuzibleQuery, sDBName);
                    if (bWithConnexion)
                    {
                        ConnexionSQLServer.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    string sRf = string.Concat("CREATE DATABASE ", sDBName);
                    string sCe = Toolbox.ReplaceDBNameInConnectionString(sConnString, "main", JobParameters.DynParams);
                    ConnexionSqlite = new SqliteConnection(sCe);
                    ConnexionSqlite.OpenAsync(Monitoring.TaskCancellationToken);
                    //ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("ALTER TABLE ", sTable, bDisable ? " DISABLE KEYS;" : " ENABLE KEYS;"));
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sRf, FuzibleQuery, sDBName);
                    if (bWithConnexion)
                    {
                        ConnexionSqlite.ChangeDatabase(sDBName);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    string sRg = string.Concat("CREATE DATABASE ", sDBName);
                    //search Data Source= dans la chaîne de connection puis remplacer
                    System.Text.RegularExpressions.Match rgSource = Regex.Match(sConnString.SConnString(JobParameters.DynParams), "DATA SOURCE=.[^;]+(;|$)", RegexOptions.IgnoreCase);
                    if (rgSource.Success)
                    {
                        try
                        {
                            string sOldDB = rgSource.Value;
                            sOldDB = sOldDB[(sOldDB.IndexOf("=") + 1)..];
                            if (sOldDB.EndsWith(";")) { sOldDB = sOldDB[0..^1]; }
                            OleDbConnection MyConn = new(sConnString.SConnString(JobParameters.DynParams).Replace(sOldDB, sDBName));
                            MyConn.OpenAsync(Monitoring.TaskCancellationToken);
                            //OleDbCommand Cmd = new OleDbCommand(StrCmd, MyConn); ;
                            //OleDbDataReader ObjReader = Cmd.ExecuteReaderAsync(Monitoring.TaskCancellationToken);
                            //if (ObjReader != null) { }
                            //ObjReader.CloseAsync();
                            MyConn.CloseAsync();
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_createdb_ko + sDBName + ").", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    //au mieux on peut créer un fichier
                    break;
            }

        }

        public void SetIdentityInsert(string sTable, bool bInsertIdentity, Query FuzibleQuery)
        {
            DataSet dsData;
            string sIdentityOnTable;

            switch (Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_MYSQL:
                    //MYSQL accepte l'insertion dans les champs AUTOINCREMENT
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    //POSTGRE accepte l'insertion dans les champs AUTOINCREMENT
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    dsData = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT OBJECTPROPERTY(OBJECT_ID('", sTable, "'), 'TableHasIdentity');"), FuzibleQuery, sTable);
                    sIdentityOnTable = BuildStringFromDs(dsData);

                    if (!sIdentityOnTable.Equals("0"))
                    {
                        ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, string.Concat("SET IDENTITY_INSERT ", sTable, bInsertIdentity ? " ON;" : " OFF;"), FuzibleQuery, sTable);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    //Sqlite accepte l'insertion dans les champs AUTOINCREMENT
                    break;
            }
        }

        public async Task<DataSet> GetDataFromDatabase(Query FuzibleQuery, bool bOutputTableExists)
        {
            //si on est en mode streaming (lecture/écriture parallèle) et en mode drop, il faut absolument
            //que la première passe soit au moins de 100 000 lignes pour créer un schéma convenable
            if (JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP)
            {
                bOutputTableExists = false;
            }

            List<string[]> sAnonymizationFileds = FuzibleQuery.RewriteWithoutAnonymization();

            //le problème du mode direct stream fait qu'on va perdre les properties (inserted/retrieved) car on ne les propage pas dans sQuery
            //et donc le dataset retourné est vide puisque tout a déjà été inséré
            //TODO : risque de problème si la requête subit une transformation ?
            Query FuzibleQueryAsync = JobParameters.SQLDirectStream ? FuzibleQuery : FuzibleQuery.DeepCopy();

            DataSet dsData = new();
            int iQteRows = FuzibleQuery.QueryAnalyzer.LimitedResults;
            if (iQteRows <= 0) { iQteRows = 999999999; }

            if (JobParameters.ExportUseDataset)
            {
                dsData = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, FuzibleQuery.SQLQuery, FuzibleQuery, FuzibleQuery.OutputTable);
            }
            else
            {
                try
                {
                    Connect(Connection, FuzibleQuery);

                    switch (Connection.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            dsData = await GetDataFromMySql(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            dsData = await GetDataFromSQLServer(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            dsData = await GetDataFromSqlite(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            dsData = await GetDataFromODBC(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            dsData = await GetDataFromPostgre(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            dsData = await GetDataFromAccess(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            dsData = await GetDataFromOracle(FuzibleQueryAsync, iQteRows, bOutputTableExists);
                            break;
                    }

                    Disconnect(Connection, FuzibleQuery);
                }
                catch (OperationCanceledException)
                {
                    Disconnect(Connection, FuzibleQuery);
                    throw;
                }
            }

            if (dsData != null && dsData.Tables.Count > 0)
            {
                foreach (string[] sAno in sAnonymizationFileds) //[0] = nom du champ -- [1] = table SQL ou fichier --[2] = type : fichier ou table --[3] index
                {
                    if (dsData.Tables[0].Columns.Contains(sAno[0]))
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_anonymize_column + " : " + sAno[0] + " (" + sAno[2] + "->" + sAno[1] + ")", SQLTools_Enums.LOG_TYPEINFO.INF);

                        bool bRandomFromItSelf = true;

                        List<string> sData = new();
                        switch (sAno[2])
                        {
                            case "TABLE":
                                try
                                {
                                    DataSet dsAno = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT * FROM (" + sAno[1] + ") AS req", FuzibleQuery, "Anonymization", false);
                                    if (dsAno.Tables.Count > 0 && int.TryParse(sAno[3], out int i) && dsAno.Tables[0].Columns.Count > i)
                                    {
                                        sData = SQLTools.BuildListFromDs(dsAno, 0, i);
                                    }
                                    else
                                    {
                                        sData = SQLTools.BuildListFromDs(dsAno);
                                    }
                                    if (sData.Count == 0) { sData = null; }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_anonymize_column_ko + " : " + sAno[0] + " (" + sAno[2] + "->" + sAno[1] + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    sData = null;
                                }
                                break;
                            case "FILE":
                                try
                                {
                                    var JTemp = Job.CreateDummyJob(JobParameters.GlobalParameters, SQLTools_Enums.BDD.FI_CSV, sAno[1], Path.GetFileName(sAno[1]));
                                    Query Q = JTemp.Item2;
                                    FITools fi = new FITools(JTemp.Item1, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                                    DataSet dsAno = fi.GetDataFromFile(SQLTools_Enums.BDD.FI_CSV, ref Q);
                                    if (dsAno.Tables.Count > 0 && int.TryParse(sAno[3], out int i) && dsAno.Tables[0].Columns.Count > i)
                                    {
                                        sData = SQLTools.BuildListFromDs(dsAno, 0, i);
                                    }
                                    else
                                    {
                                        sData = SQLTools.BuildListFromDs(dsAno);
                                    }
                                    if (sData.Count == 0) { sData = null; }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_anonymize_column_ko + " : " + sAno[0] + " (" + sAno[2] + "->" + sAno[1] + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    sData = null;
                                }
                                break;
                            case "RANDOM":
                                bRandomFromItSelf = false;
                                break;
                        }

                        if (sData != null)
                        {
                            try
                            {
                                SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                                List<SQLColumn> sSqlCol = SHS.GetListFieldsTypesFromDataset(dsData.Tables[0], dsData.Tables[0].Columns[sAno[0]].Ordinal, FuzibleQuery, SQLTools_Enums.CLASS_PURPOSE.SRC);

                                Toolbox.RandomData(sData, dsData.Tables[0], sSqlCol, dsData.Tables[0].Columns[sAno[0]], dsData.Tables[0].Columns[sAno[0]].ColumnName, bRandomFromItSelf, "SQLANO", ref MyLog);
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.sql_anonymize_column_ko + " : " + sAno[0] + " (" + sAno[2] + "->" + sAno[1] + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }
                }
            }

            FuzibleQuery = FuzibleQueryAsync;

            if (dsData.Tables.Count > 0) { dsData.Tables[0].Namespace = FuzibleQuery.QueryAnalyzer.Tables[0].Alias; }
            dsData.CaseSensitive = false;
            return dsData;

        }

        public List<SQLColumn> GetFieldsFromTable(string sNomTable, Query FuzibleQuery, string sField = null)
        {
            if (sField != null)
            {
                sField = sField.Replace("'", "''");
            }

            List<SQLColumn> sListeOfColonnes = new();
            List<SQLColumn> sListOfPrimaryKeys;
            List<SQLColumn> sListOfForeignKeys;

            bool bIsKey;

            if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
            {
                sListOfPrimaryKeys = GetKeysFromTable(sNomTable, "PK", FuzibleQuery);
                sListOfForeignKeys = GetKeysFromTable(sNomTable, "FK", FuzibleQuery);

                Connect(Connection, FuzibleQuery);
                DataTable dtK = ConnexionACCESS.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, null);
                Disconnect(Connection, FuzibleQuery);
                if (dtK != null && dtK.Rows.Count > 0)
                {
                    foreach (DataRow dt in dtK.Rows)
                    {
                        bIsKey = false; //important : sert notamment pour empêcher les conversions de types 
                        if (dt[2].ToString().Equals(sNomTable, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (SQLColumn SQLC in sListOfPrimaryKeys)
                            {
                                if (SQLC.ColumnName.Equals(dt[3].ToString()))
                                {
                                    bIsKey = true;
                                }
                            }
                            foreach (SQLColumn SQLC in sListOfForeignKeys)
                            {
                                if (SQLC.ColumnName.Equals(dt[3].ToString()))
                                {
                                    bIsKey = true;
                                }
                            }

                            if (sField != null)
                            {
                                if (dt[3].ToString().Equals(sField, StringComparison.OrdinalIgnoreCase))
                                {
                                    var oType = (OleDbType)dt[11];
                                    SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                                    try { sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), oType.ToString().ToUpper().Replace(" ", "_")); }
                                    catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dt[0].ToString() + " : " + oType.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                    sListeOfColonnes.Add(new SQLColumn(Convert.ToInt32(dt[6]), dt[3].ToString(), sType, Toolbox.GetSystemTypeFromListTypes(sType), dt[13].ToString(), !dt[10].ToString().Equals("false", StringComparison.OrdinalIgnoreCase), dt[8].ToString(), bIsKey, false));
                                    break;
                                }
                            }
                            else
                            {
                                var oType = (OleDbType)dt[11];
                                SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                                try { sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), oType.ToString().ToUpper().Replace(" ", "_")); }
                                catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dt[0].ToString() + " : " + oType.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                sListeOfColonnes.Add(new SQLColumn(Convert.ToInt32(dt[6]), dt[3].ToString(), sType, Toolbox.GetSystemTypeFromListTypes(sType), dt[13].ToString(), !dt[10].ToString().Equals("false", StringComparison.OrdinalIgnoreCase), dt[8].ToString(), bIsKey, false));
                            }
                        }
                    }
                }
            }
            else
            {
                sListOfPrimaryKeys = GetKeysFromTable(sNomTable, "PK", FuzibleQuery);
                sListOfForeignKeys = GetKeysFromTable(sNomTable, "FK", FuzibleQuery);

                DataSet dsTemp;

                string sQuery;
                if (sField == null)
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_COLUMNS_LIST);
                    sQuery = sQuery.Replace("{TABLE_NAME}", sNomTable);
                    sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
                    sQuery = sQuery.Replace("{COLUMN_NAME}", sField);
                    sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                }
                else
                {
                    sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.GET_COLUMN);
                    sQuery = sQuery.Replace("{TABLE_NAME}", sNomTable);
                    sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
                    sQuery = sQuery.Replace("{COLUMN_NAME}", sField);
                    sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                }
                if (sQuery.Length > 0)
                {
                    dsTemp = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, FuzibleQuery, sNomTable);
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.GET_COLUMNS_LIST.ToString() + ". Trying Autodetection.", SQLTools_Enums.LOG_TYPEINFO.WNG);
                    string sRa;
                    if (sField != null)
                    {
                        sRa = "SELECT " + sField + " FROM " + sNomTable;
                    }
                    else { sRa = "SELECT * FROM " + sNomTable; }
                    dsTemp = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sRa, FuzibleQuery, sNomTable);
                    if (dsTemp != null && dsTemp.Tables.Count > 0 && dsTemp.Tables[0].Rows.Count >= 0)
                    {
                        DataColumn[] dcPK = dsTemp.Tables[0].PrimaryKey;
                        for (int iC = 0; iC < dsTemp.Tables[0].Columns.Count; iC++)
                        {
                            string sType = dsTemp.Tables[0].Columns[iC].DataType.ToString();
                            sType = sType[(sType.IndexOf(".") + 1)..];
                            SQLTools_Enums.TYPE_DATA sSQLType = SQLTools_Enums.TYPE_DATA.TEXT;
                            try { sSQLType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sType.ToUpper().Replace(" ", "_")); }
                            catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dsTemp.Tables[0].Columns[iC].ColumnName + " : " + sType.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                            sListeOfColonnes.Add(new SQLColumn(iC, dsTemp.Tables[0].Columns[iC].ColumnName,
                                sSQLType, Toolbox.GetSystemTypeFromListTypes(sSQLType),
                                dsTemp.Tables[0].Columns[iC].MaxLength.ToString(), dsTemp.Tables[0].Columns[iC].AllowDBNull,
                                dsTemp.Tables[0].Columns[iC].DefaultValue.ToString(), dcPK.Contains(dsTemp.Tables[0].Columns[iC]), dsTemp.Tables[0].Columns[iC].Unique));
                        }
                    }
                    dsTemp.Clear();
                    dsTemp = null;
                }

                if (dsTemp != null && dsTemp.Tables.Count > 0)
                {
                    foreach (DataRow dr in dsTemp.Tables[0].Rows)
                    {
                        bIsKey = false;
                        //contrôle le champ pour vérifier si c'est une clé primaire
                        foreach (SQLColumn SQLC in sListOfPrimaryKeys)
                        {
                            if (SQLC.ColumnName.Equals(dr[0].ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                bIsKey = true;
                            }
                        }
                        foreach (SQLColumn SQLC in sListOfForeignKeys)
                        {
                            if (SQLC.ColumnName.Equals(dr[0].ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                bIsKey = true;
                            }
                        }
                        SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                        string sTempType = dr[1].ToString().ToUpper().Replace(" ", "_");
                        if (Regex.IsMatch(sTempType, ".+[^\\(\\)]\\(\\d+\\)"))
                        {
                            sTempType = sTempType[..sTempType.LastIndexOf("(")];
                        }
                        try { sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sTempType); }
                        catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dr[0].ToString() + " : " + dr[1].ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        Type LinqType = Toolbox.GetSystemTypeFromListTypes(sType);
                        sListeOfColonnes.Add(new SQLColumn(0, dr[0].ToString(), sType, LinqType, dr[2].ToString().Length == 0 ? dr[6].ToString() : dr[2].ToString(), Toolbox.CastValueToBoolean(dr[3].ToString()), dr[7].ToString(), bIsKey, bIsKey));
                    }
                }

                if (sListeOfColonnes.Count == 0 && sField == null)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_getcolumns_ko + sNomTable + ").", SQLTools_Enums.LOG_TYPEINFO.INF);
                }
            }

            return sListeOfColonnes;
        }

        public void ShrinkTable(string sTableName, Query FuzibleQuery)
        {

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_shrink_info + sTableName, SQLTools_Enums.LOG_TYPEINFO.INF);

            string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.SHRINK_TABLE);
            sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
            sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
            sQuery = sQuery.Replace("{DATABASE_NAME}", DatabaseName);
            if (sQuery.Length > 0)
            {
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName);
            }
            else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.SHRINK_TABLE.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

        }

        #endregion

        #region "PRIVATE VOID"

        private void UpdateTableFromDs(string sTargetTable, DataSet dsMAJ, List<SQLColumn> sListFieldsNamePK, int iRowsSource, int iRowsTarget, Query FuzibleQuery, bool bEverythingAdded)
        {

            if (dsMAJ.Tables["INSERT"].Rows.Count > 0)
            {
                //si la source a plus de lignes que la cible je fais l'analyse de celle-ci et vice-versa (cas des sources qui contiennent moins de lignes donc analyse des types moins précise
                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("I") > -1 || JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1)
                {
                    int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_toinsert + sTargetTable + ") : " + dsMAJ.Tables["INSERT"].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    InsertDataInBDD(dsMAJ.Tables["INSERT"], sTargetTable, FuzibleQuery);

                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                    if (iErrWarnB > iErrWarnA)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dsMAJ.Tables["INSERT"].Rows.Count.ToString() + @" !", false);
                    }
                    else
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dsMAJ.Tables["INSERT"].Rows.Count.ToString(), false);
                    }
                }
            }

            if (dsMAJ.Tables["UPDATE"].Rows.Count > 0)
            {
                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("U") > -1 || JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1)
                {
                    int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_toupdate + sTargetTable + ") : " + dsMAJ.Tables["UPDATE"].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    UpdateDataInBDD(dsMAJ.Tables["UPDATE"], sTargetTable, sListFieldsNamePK, FuzibleQuery);

                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                    if (iErrWarnB > iErrWarnA)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, @"! " + dsMAJ.Tables["UPDATE"].Rows.Count.ToString() + @" !", false);
                    }
                    else
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, dsMAJ.Tables["UPDATE"].Rows.Count.ToString(), false);
                    }

                    if (JobParameters.SynchroStoreChanges)
                    {
                        SynchroModeStoreUpdateDeleteChanges("U", dsMAJ.Tables["UPDATE_OLDDATA"], sTargetTable, sListFieldsNamePK, FuzibleQuery);
                    }
                }
            }

            if (dsMAJ.Tables["DELETE"].Rows.Count > 0)
            {
                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("D") > -1)
                {
                    int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_todelete + sTargetTable + ") : " + dsMAJ.Tables["DELETE"].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    DeleteDataInBDD(dsMAJ.Tables["DELETE"], sTargetTable, sListFieldsNamePK, FuzibleQuery);

                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                    if (iErrWarnB > iErrWarnA)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, @"! " + dsMAJ.Tables["DELETE"].Rows.Count.ToString() + @" !", false);
                    }
                    else
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, dsMAJ.Tables["DELETE"].Rows.Count.ToString(), false);
                    }

                    if (JobParameters.SynchroStoreChanges)
                    {
                        SynchroModeStoreUpdateDeleteChanges("D", dsMAJ.Tables["DELETE"], sTargetTable, sListFieldsNamePK, FuzibleQuery);
                    }
                }
                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_todelete + sTargetTable + ") : " + dsMAJ.Tables["DELETE"].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_todelete_tag + sTargetTable + ") : " + dsMAJ.Tables["DELETE"].Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                    UpdateDataInBDD(dsMAJ.Tables["DELETE"], sTargetTable, sListFieldsNamePK, FuzibleQuery);

                    if (JobParameters.SynchroStoreChanges)
                    {
                        SynchroModeStoreUpdateDeleteChanges("D", dsMAJ.Tables["DELETE"], sTargetTable, sListFieldsNamePK, FuzibleQuery);
                    }
                }
            }

            UpdateInfosTableSynchroMode(sTargetTable, iRowsSource, bEverythingAdded ? 0 : iRowsTarget, bEverythingAdded ? iRowsSource : dsMAJ.Tables["INSERT"].Rows.Count, dsMAJ.Tables["UPDATE"].Rows.Count, dsMAJ.Tables["DELETE"].Rows.Count, FuzibleQuery);
        }

        private void SynchroModeStoreUpdateDeleteChanges(string sUorD, DataTable dtData, string sTableTarget, List<SQLColumn> sListFieldsWhere, Query FuzibleQuery)
        {
            string sOriginalTable = sTableTarget;
            //TODO : fragile : on pourrait définir la longueur selon le type de BDD
            if (sTableTarget.Length > 56) { sTableTarget = sTableTarget[..56]; }
            sTableTarget = string.Concat(sTableTarget, "_back");

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_synchro_store01, sUorD, " - ", dtData.Rows.Count.ToString(), Languages.Languages.sql_synchro_store02, sTableTarget), SQLTools_Enums.LOG_TYPEINFO.INF);

            //ajout colonne ID ligne dans dataset
            string sRecordIDCol = JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN;
            string sRecordTagCol = JobParameters.GlobalParameters.SQL_SYNCHRO_TAG_COLUMN;
            JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Add(JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN);
            JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Add(JobParameters.GlobalParameters.SQL_SYNCHRO_TAG_COLUMN);

            //pas de support INT64 en Sqlite
            if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
            {
                int sRecordIDValue = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);  //Convert.ToInt32(DateTime.Now.ToString("yyMMddhhmm"));
                dtData.Columns.Add(sRecordIDCol, Type.GetType("System.Int32"));
                dtData.Columns.Add(sRecordTagCol, Type.GetType("System.String"));
                foreach (DataRow dt in dtData.Rows)
                {
                    dt[sRecordIDCol] = sRecordIDValue; dt[sRecordTagCol] = sUorD;
                }
                sListFieldsWhere.Add(new SQLColumn(dtData.Columns[sRecordIDCol].Ordinal, sRecordIDCol, SQLTools_Enums.TYPE_DATA.INT, Type.GetType("System.Int32"), "", false, "", false, false));
            }
            else
            {
                long sRecordIDValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                dtData.Columns.Add(sRecordIDCol, Type.GetType("System.Int64"));
                dtData.Columns.Add(sRecordTagCol, Type.GetType("System.String"));
                foreach (DataRow dt in dtData.Rows)
                {
                    dt[sRecordIDCol] = sRecordIDValue; dt[sRecordTagCol] = sUorD;
                }
                sListFieldsWhere.Add(new SQLColumn(dtData.Columns[sRecordIDCol].Ordinal, sRecordIDCol, SQLTools_Enums.TYPE_DATA.BIGINT, Type.GetType("System.Int64"), "", false, "", false, false));
            }

            //ajout des colonnes optionnelles (sinon problème à la création de table par la suite)
            Query QBackup = new(JobParameters, FuzibleQuery.RawQuery);
            List<Query.QField> qFBackup = new();
            foreach (Query.QField qF in QBackup.QueryAnalyzer.Fields)
            {
                qFBackup.Add(qF);
            }
            foreach (string sCol in JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Distinct())
            {
                qFBackup.Add(new Query.QField(sCol, QBackup.QueryAnalyzer.Tables, qFBackup.Count, QBackup.ConnectionSrc));
            }
            QBackup.QueryAnalyzer.SetFieldsByQFields(qFBackup);

            //créer table si inexistante
            bool bTableExists = CheckForExistingTable(sTableTarget, FuzibleQuery);

            //bool bOldValue = JobParameters.AlterColumnTypeOnInsert;
            //SQLTools_Enums.TARGET_TABLE_METHOD ttm = JobParameters.TargetTableBehavior;
            //JobParameters.TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
            //JobParameters.AlterColumnTypeOnInsert = true;

            if (!bTableExists)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_synchro_store03, sTableTarget), SQLTools_Enums.LOG_TYPEINFO.INF);

                string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_FROM_SELECT);

                if (sQuery.Length > 0) //création de table en mode requête
                {
                    sQuery = sQuery.Replace("[TABLE]", sTableTarget);
                    sQuery = sQuery.Replace("[ORIGINAL_TABLE]", sOriginalTable);
                    sQuery = sQuery.Replace("[SCHEMA_NAME]", DefaultSchema);
                    //ajouter colonnes manquantes
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_CREATE, sQuery, FuzibleQuery, sTableTarget);
                    CreateColumnInTarget(sTableTarget, sListFieldsWhere.Last(), false, FuzibleQuery);
                    CreateColumnInTarget(sTableTarget, new SQLColumn(dtData.Columns[sRecordTagCol].Ordinal, sRecordTagCol, Connection.SqlCharTypeCompatibility, Type.GetType("System.String"), "2", false, "", false, false), false, FuzibleQuery);
                    AddPrimaryKeyToTable(sTableTarget, sListFieldsWhere, FuzibleQuery);
                    InsertDataInBDD(dtData, sTableTarget, QBackup);
                }
                else //création heuristique de table
                {
                    //Exception ex = new Exception(Languages.Languages.sql_synchro_store_noquery);
                    //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sTableTarget, SQLTools_Enums.LOG_TYPEINFO.ERR);
                    InsertDataInBDD(dtData, sTableTarget, QBackup);
                }
            }
            else
            {
                InsertDataInBDD(dtData, sTableTarget, QBackup);
            }
            //retrait du champ ID
            sListFieldsWhere.RemoveAt(sListFieldsWhere.Count - 1);

            //JobParameters.AlterColumnTypeOnInsert = bOldValue;
            //JobParameters.TargetTableBehavior = ttm;
        }

        private void UpdateInfosTableSynchroMode(string sTargetTable, int iRowsSource, int iRowsTarget, int iInsert, int iUpdate, int iDelete, Query FuzibleQuery)
        {
            //inutile d'écrire ce qui était prévu si le fonctionnement du mode synchro n'est pas celui désiré
            if (!JobParameters.SynchroTargetTableBehavior.ToString().Contains('D', StringComparison.CurrentCulture))
            {
                iDelete = 0;
            }
            if (!JobParameters.SynchroTargetTableBehavior.ToString().Contains('U', StringComparison.CurrentCulture))
            {
                iUpdate = 0;
            }
            if (!JobParameters.SynchroTargetTableBehavior.ToString().Contains('I', StringComparison.CurrentCulture))
            {
                iInsert = 0;
            }

            string sFieldIncr = "";

            switch (Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sFieldIncr = "id_ligne AUTOINCREMENT, ";
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sFieldIncr = "";
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sFieldIncr = "id_ligne INT NOT NULL AUTO_INCREMENT, ";
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sFieldIncr = "id_ligne SERIAL PRIMARY KEY, ";
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sFieldIncr = "id_ligne int IDENTITY(1, 1) PRIMARY KEY, ";
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sFieldIncr = "id_ligne INTEGER PRIMARY KEY AUTOINCREMENT, ";
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sFieldIncr = "";
                    break;
            }

            if (!CheckForExistingTable(JobParameters.GlobalParameters.SQL_SYNCHRO_MODE_TABLE, FuzibleQuery))
            {
                CreateOutputTable(JobParameters.GlobalParameters.SQL_SYNCHRO_MODE_TABLE, false, string.Concat(sFieldIncr, "li_table ", SqlCharTypeCompatibility, "(40), dt_maj ", SqlDateTypeCompatibility, ", qte_rows_source int, qte_rows_target int, qte_insert int, qte_update int, qte_delete int,", Connection.SConnDriver == SQLTools_Enums.BDD.DB_MYSQL ? " PRIMARY KEY (id_ligne) " : ""), FuzibleQuery);
                if (!CheckForExistingTable(JobParameters.GlobalParameters.SQL_SYNCHRO_MODE_TABLE, FuzibleQuery))
                {
                    throw new Exception(Languages.Languages.sql_synchro_cantcreatetargettable);
                }
            }

            StringBuilder sbQ = new();
            sbQ.Append(string.Concat("INSERT INTO ", EchappementChar, JobParameters.GlobalParameters.SQL_SYNCHRO_MODE_TABLE, EchappementChar, " (li_table, dt_maj, qte_rows_source, qte_rows_target, qte_insert, qte_update, qte_delete) VALUES "));
            sbQ.Append(string.Concat("(", "'", sTargetTable, "'", ",", Toolbox.SetCleanDate(DateTime.Now.ToString(), JobParameters, Connection, this.JobParameters.UseNull_Target, true, 2), ",", iRowsSource.ToString(), ",", iRowsTarget.ToString(), ",", iInsert.ToString(), ",", iUpdate.ToString(), ",", iDelete.ToString(), ");"));

            //BDDCible.ExecRequete(SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", _sTableInfosStreamingMode, " WHERE li_table = '", sNomTableCible, "';"));
            ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQ.ToString(), FuzibleQuery, JobParameters.GlobalParameters.SQL_SYNCHRO_MODE_TABLE);

            MyLog.AddJobReportRow(sTargetTable, iRowsSource, iRowsTarget, iInsert, iUpdate, iDelete);
        }

        private void AddPrimaryKeyToTable(string sTargetTable, List<SQLColumn> sListFieldsPK, Query FuzibleQuery)
        {
            if (Connection.SConnDriver != SQLTools_Enums.BDD.DB_SQLITE)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_addpkintarget_info + sTargetTable + ")", SQLTools_Enums.LOG_TYPEINFO.INF);

                string sPKey = "";

                for (int cptFPK = 0; cptFPK < sListFieldsPK.Count; cptFPK++)
                {
                    //on interdit le NULL dans chaque champ de clé primaire
                    ChangeNullColumnInTarget(sTargetTable, sListFieldsPK[cptFPK], false, FuzibleQuery);

                    //on construit la chaine des champs PK
                    sPKey = string.Concat(sPKey, EchappementChar, sListFieldsPK[cptFPK].ColumnName, EchappementChar, ",");
                }
                sPKey = sPKey[0..^1];

                string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_PRIMARY_KEY);
                sQuery = sQuery.Replace("{TABLE_NAME}", sTargetTable);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{COLUMNS_LIST}", sPKey);
                sQuery = sQuery.Replace("{CONSTRAINT_NAME}", "PK_" + Toolbox.RemoveSpecialCharacters(sTargetTable, "", true));
                if (sQuery.Length > 0)
                {
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTargetTable);
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CREATE_PRIMARY_KEY.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
            }
        }

        private async Task<DataSet> RunQuery(System.Reflection.MethodBase mbCalledFrom, SQLTools_Enums.BDD whichBDD, string sSQLQuery, Query FuzibleQuery, string sDtName, SQLTools_Enums.TYPE_REQUETE eTypeQuery)
        {
            DataSet dsData = new();
            int iQteErrors = 0;

            if (JobParameters.RunInSimulationMode)
            {
                MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", sSQLQuery));
            }
            //en mode simulation on exécute quand même les SELECT !
            if ((JobParameters.RunInSimulationMode && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND))
                || !JobParameters.RunInSimulationMode)
            {
                try
                {
                    Connect(Connection, FuzibleQuery);

                    switch (whichBDD)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            OleDbCommand CommandeAccess = new(sSQLQuery, ConnexionACCESS)
                            {
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };

                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    OleDbDataAdapter Adaptateur = new(CommandeAccess);
                                    Adaptateur.Fill(dsData, sDtName);
                                    //using (var readerTask = CommandeAccess.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    System.Data.Common.DbDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}

                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeAccess.Dispose();
                                    throw;
                                }
                                catch (OleDbException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ACCESSEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                ////gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    OleDbCommand cmD = new OleDbCommand(Connection.SqlDateFormat, ConnexionACCESS);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}

                                OleDbTransaction dbTransac = ConnexionACCESS.BeginTransaction();
                                CommandeAccess.Transaction = dbTransac;
                                try
                                {
                                    //
                                    CommandeAccess.CommandText = sSQLQuery;

                                    object res = await CommandeAccess.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeAccess.Dispose();
                                    throw;
                                }
                                catch (OleDbException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeAccess.Dispose();
                                        throw;
                                    }
                                    catch (OleDbException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ACCESSEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeAccess.CommandText = sQ;
                                            await CommandeAccess.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeAccess.Dispose();
                                            throw;
                                        }
                                        catch (OleDbException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ACCESSEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.MYSQLEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());


                                }
                            }
                            break;

                        case SQLTools_Enums.BDD.DB_MYSQL:
                            MySqlCommand CommandeSQLMS = new(sSQLQuery, ConnexionMySQL)
                            {
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    MySqlDataAdapter Adaptateur = new(CommandeSQLMS);
                                    Adaptateur.Fill(dsData, sDtName);
                                    //using (var readerTask = CommandeSQLMS.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    MySqlDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLMS.Dispose();
                                    throw;
                                }
                                catch (MySqlException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.MYSQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                //gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    MySqlCommand cmD = new MySqlCommand(Connection.SqlDateFormat, ConnexionMySQL);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}

                                MySqlTransaction dbTransac = ConnexionMySQL.BeginTransaction();
                                CommandeSQLMS.Transaction = dbTransac;
                                try
                                {
                                    CommandeSQLMS.CommandText = sSQLQuery;

                                    object res = await CommandeSQLMS.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLMS.Dispose();
                                    throw;
                                }
                                catch (MySqlException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeSQLMS.Dispose();
                                        throw;
                                    }
                                    catch (MySqlException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.MYSQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeSQLMS.CommandText = sQ;
                                            await CommandeSQLMS.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeSQLMS.Dispose();
                                            throw;
                                        }
                                        catch (MySqlException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.MYSQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.MYSQLEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());


                                }
                            }
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            SqlCommand CommandeSQLSS = new(sSQLQuery, ConnexionSQLServer)
                            {
                                CommandTimeout = ConnexionSQLServer.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionSQLServer.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    SqlDataAdapter Adaptateur = new(CommandeSQLSS);
                                    Adaptateur.Fill(dsData, sDtName);

                                    //    SqlDataReader reader;
                                    //using (var readerTask = CommandeSQLSS.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dt.TableNewRow += delegate (object sender, DataTableNewRowEventArgs e)
                                    //    { 
                                    //        if (cancellationToken.IsCancellationRequested)
                                    //        { throw new OperationCanceledException("Operation Cancelled"); } 
                                    //    };
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLSS.Dispose();
                                    throw;
                                }
                                catch (SqlException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.SQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    SqlCommand cmD = new SqlCommand(Connection.SqlDateFormat, ConnexionSQLServer);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}

                                SqlTransaction dbTransac = ConnexionSQLServer.BeginTransaction();
                                CommandeSQLSS.Transaction = dbTransac;
                                try
                                {
                                    CommandeSQLSS.CommandText = sSQLQuery;

                                    object res = await CommandeSQLSS.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    //CommandeSQLSS.
                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLSS.Dispose();
                                    throw;
                                }
                                catch (Exception)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                        //CommandeSQLSS.CommandText = PrepareSQLTransaction(false);
                                        //CommandeSQLSS.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeSQLSS.Dispose();
                                        throw;
                                    }
                                    catch (SqlException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.SQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeSQLSS.CommandText = sQ;
                                            await CommandeSQLSS.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeSQLSS.Dispose();
                                            throw;
                                        }
                                        catch (SqlException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.SQLEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.SQLEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());
                                }
                            }

                            break;

                        case SQLTools_Enums.BDD.DB_SQLITE:
                            SqliteCommand CommandeSqlite = new(sSQLQuery, ConnexionSqlite)
                            {
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    SqliteDataAdapter Adaptateur = new(CommandeSqlite);
                                    await Adaptateur.FillAsync(dsData, sDtName);
                                    //using (var readerTask = CommandeSqlite.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    System.Data.Common.DbDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSqlite.Dispose();
                                    throw;
                                }
                                catch (SqliteException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.SQLiteException, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                //gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    SqliteCommand cmD = new SqliteCommand(Connection.SqlDateFormat, ConnexionSqlite);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}
                                if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE)
                                {
                                    SqliteCommand cmD = new("PRAGMA journal_mode = MEMORY;", ConnexionSqlite);
                                    await cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                }

                                SqliteTransaction dbTransac = ConnexionSqlite.BeginTransaction();
                                CommandeSqlite.Transaction = dbTransac;
                                try
                                {
                                    CommandeSqlite.CommandText = string.Concat(sSQLQuery.Trim().EndsWith(";") ? sSQLQuery : (sSQLQuery.Trim() + ";"), " SELECT last_insert_rowid();");

                                    object res = await CommandeSqlite.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "0" : res.ToString();
                                        //string sResult = res == null ? ConnexionSqlite.LastInsertRowId.ToString().Equals("0") ? "" : ConnexionSqlite.LastInsertRowId.ToString() : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSqlite.Dispose();
                                    throw;
                                }
                                catch (SqliteException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                        //CommandeSQLSS.CommandText = PrepareSQLTransaction(false);
                                        //CommandeSQLSS.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeSqlite.Dispose();
                                        throw;
                                    }
                                    catch (SqliteException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.SQLiteException, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeSqlite.CommandText = sQ;
                                            await CommandeSqlite.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeSqlite.Dispose();
                                            throw;
                                        }
                                        catch (SqliteException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.SQLiteException, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.SQLEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());
                                }
                            }

                            break;

                        case SQLTools_Enums.BDD.DB_ODBC:

                            OdbcCommand CommandeSQLHF = new(sSQLQuery, ConnexionODBC)
                            //CommandeSQLHF.CommandType = CommandType.Text;
                            {
                                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };

                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    OdbcDataAdapter Adaptateur = new(CommandeSQLHF);
                                    Adaptateur.Fill(dsData, sDtName);
                                    //using (var readerTask = CommandeSQLHF.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    System.Data.Common.DbDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLHF.Dispose();
                                    throw;
                                }
                                catch (OdbcException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ODBCEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                //gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    OdbcCommand cmD = new OdbcCommand(Connection.SqlDateFormat, ConnexionODBC);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}

                                OdbcTransaction dbTransac = ConnexionODBC.BeginTransaction();
                                CommandeSQLHF.Transaction = dbTransac;
                                try
                                {
                                    CommandeSQLHF.CommandText = sSQLQuery;

                                    object res = await CommandeSQLHF.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeSQLHF.Dispose();
                                    throw;
                                }
                                catch (OdbcException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                        //CommandeSQLHF.CommandText = PrepareSQLTransaction(false);
                                        //CommandeSQLHF.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeSQLHF.Dispose();
                                        throw;
                                    }
                                    catch (OdbcException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ODBCEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeSQLHF.CommandText = sQ;
                                            await CommandeSQLHF.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeSQLHF.Dispose();
                                            throw;
                                        }
                                        catch (OdbcException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ODBCEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.ODBCEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());
                                }
                            }

                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            NpgsqlCommand CommandePOSTGRE = new(sSQLQuery, ConnexionPOSTGRE)
                            {
                                CommandTimeout = ConnexionPOSTGRE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionPOSTGRE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    NpgsqlDataAdapter Adaptateur = new(CommandePOSTGRE);
                                    Adaptateur.Fill(dsData, sDtName);
                                    //using (var readerTask = CommandePOSTGRE.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    System.Data.Common.DbDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandePOSTGRE.Dispose();
                                    throw;
                                }
                                catch (NpgsqlException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.NPGEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                //gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    NpgsqlCommand cmD = new NpgsqlCommand(Connection.SqlDateFormat, ConnexionPOSTGRE);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}

                                NpgsqlTransaction dbTransac = ConnexionPOSTGRE.BeginTransaction();
                                CommandePOSTGRE.Transaction = dbTransac;
                                try
                                {
                                    CommandePOSTGRE.CommandText = sSQLQuery;

                                    object res = await CommandePOSTGRE.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandePOSTGRE.Dispose();
                                    throw;
                                }
                                catch (NpgsqlException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                        //CommandePOSTGRE.CommandText = PrepareSQLTransaction(false);
                                        //CommandePOSTGRE.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandePOSTGRE.Dispose();
                                        throw;
                                    }
                                    catch (NpgsqlException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.NPGEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandePOSTGRE.CommandText = sQ;
                                            await CommandePOSTGRE.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandePOSTGRE.Dispose();
                                            throw;
                                        }
                                        catch (NpgsqlException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.NPGEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.NPGEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());
                                }
                            }

                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:

                            //patch à cause des noms de tables : 
                            sSQLQuery = sSQLQuery.Replace("\"" + sDtName + "\"", sDtName);

                            if (eTypeQuery != SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                if (Regex.IsMatch(sSQLQuery, "^(INSERT |DELETE |UPDATE )", RegexOptions.IgnoreCase))
                                {
                                    sSQLQuery = string.Concat("BEGIN ", Environment.NewLine, sSQLQuery);
                                    sSQLQuery = string.Concat(sSQLQuery, Environment.NewLine, " END;");
                                }
                                else
                                {
                                    if (sSQLQuery.EndsWith(";") && !sSQLQuery.StartsWith("BEGIN ", StringComparison.OrdinalIgnoreCase))
                                    {
                                        sSQLQuery = sSQLQuery[0..^1];
                                    }
                                }

                                //gestion des dates : forcer le modèle européen
                                //if (Connection.SqlDateFormat.Length > 0 && (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE || eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE))
                                //{
                                //    OracleCommand cmD = new OracleCommand(Connection.SqlDateFormat, ConnexionORACLE);
                                //    cmD.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                //}
                            }
                            else { if (sSQLQuery.EndsWith(";")) { sSQLQuery = sSQLQuery[0..^1]; } }

                            OracleCommand CommandeORACLE = new(sSQLQuery, ConnexionORACLE)
                            {
                                CommandTimeout = ConnexionORACLE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionORACLE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
                            };
                            if (eTypeQuery == SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT)
                            {
                                try
                                {
                                    OracleDataAdapter Adaptateur = new(CommandeORACLE);
                                    Adaptateur.Fill(dsData, sDtName);
                                    //using (var readerTask = CommandeORACLE.ExecuteReaderAsync(Monitoring.TaskCancellationToken))
                                    //{
                                    //    System.Data.Common.DbDataReader reader = readerTask.Result;
                                    //    DataTable dt = new DataTable(sDtName);
                                    //    dt.Load(reader);
                                    //    dsData.Tables.Add(dt);
                                    //}
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeORACLE.Dispose();
                                    throw;
                                }
                                catch (OracleException ex)
                                {
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, EXCTools.GetDetailledException(ex, EXCTools.EXCEPTION_TYPE.ORACLEEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "", 1,Parameters.ClassLaunchedByWhichThread);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sSQLQuery);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                OracleTransaction dbTransac = ConnexionORACLE.BeginTransaction();
                                CommandeORACLE.Transaction = dbTransac;
                                try
                                {
                                    CommandeORACLE.CommandText = sSQLQuery;

                                    object res = await CommandeORACLE.ExecuteScalarAsync(Monitoring.TaskCancellationToken);

                                    dsData.Tables.Add(sDtName);
                                    dsData.Tables[0].Columns.Add("VALUE");
                                    DataRow dr = dsData.Tables[0].NewRow();
                                    {
                                        string sResult = res == null ? "" : res.ToString();
                                        if (res != null && res.GetType() == typeof(System.Collections.BitArray))
                                        {
                                            var bA = (BitArray)res;
                                            sResult = bA.ToBitString();
                                        }
                                        dr[0] = sResult;
                                    }
                                    dsData.Tables[0].Rows.Add(dr);

                                    await dbTransac.CommitAsync(Monitoring.TaskCancellationToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    CommandeORACLE.Dispose();
                                    throw;
                                }
                                catch (OracleException)
                                {
                                    //MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    List<string> sListeRequetes = Toolbox.ExtractItemsFromStringBuilderQuery(eTypeQuery, sSQLQuery);

                                    try
                                    {
                                        if (sListeRequetes.Count > 0)
                                        {
                                            await dbTransac.RollbackAsync(Monitoring.TaskCancellationToken);
                                            await dbTransac.DisposeAsync();
                                        }
                                        //CommandeORACLE.CommandText = PrepareSQLTransaction(false);
                                        //CommandeORACLE.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        CommandeORACLE.Dispose();
                                        throw;
                                    }
                                    catch (OracleException exB)
                                    {
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ORACLEEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sSQLQuery, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    }

                                    foreach (string sQ in sListeRequetes)
                                    {
                                        try
                                        {
                                            CommandeORACLE.CommandText = sSQLQuery;
                                            await CommandeORACLE.ExecuteNonQueryAsync(Monitoring.TaskCancellationToken);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            CommandeORACLE.Dispose();
                                            throw;
                                        }
                                        catch (OracleException exB)
                                        {
                                            iQteErrors++;
                                            if (iQteErrors > JobParameters.GlobalParameters.MAX_SQL_ERRORS)
                                            {
                                                Exception exC = new(string.Concat(Languages.Languages.sql_runquery_toomanyerrors, JobParameters.GlobalParameters.MAX_SQL_ERRORS.ToString(), Languages.Languages.sql_runquery_cancellingtransaction));
                                                MyLog.LogMessage(mbCalledFrom, ClassPurpose, exC, sDtName, FuzibleQuery.RetryErrorOrWarning);
                                                FuzibleQuery.QueryErrors += 1;
                                                break;
                                            }
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, sDtName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            MyLog.LogMessage(mbCalledFrom, ClassPurpose, exB, EXCTools.GetDetailledException(exB, EXCTools.EXCEPTION_TYPE.ORACLEEXCEPTION, mbCalledFrom.Name, whichBDD.ToString(), sQ, eTypeQuery.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                                            //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), exB, "", 1,Parameters.ClassLaunchedByWhichThread);
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQ);
                                        }
                                    }

                                    //Log_SQLTools.LogMessage(mbCalledFrom, ExceptionTools.GetDetailledException(ex, ExceptionTools.TYPE_EXCEPTION.ORACLEEXCEPTION), true, 1);
                                    //Log_SQLTools.LogData(System.Reflection.MethodBase.GetCurrentMethod(), ex, "");
                                    //Log_SQLTools.WriteQueriesErrorsIntoFile(sbRequete.ToString());
                                }
                            }
                            break;
                    }

                    if (!JobParameters.KeepConnexionAfterQuery)
                    {
                        Disconnect(Connection, FuzibleQuery);
                    }

                }
                catch (OperationCanceledException)
                {
                    Disconnect(Connection, FuzibleQuery);
                    throw;
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(mbCalledFrom, ClassPurpose, ex, sDtName, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                    if (!JobParameters.KeepConnexionAfterQuery)
                    {
                        Disconnect(Connection, FuzibleQuery);
                    }
                }
            }

            return dsData;

        }

        private void CreateOutputTable(string sTableName, bool bDrop, string sHeaderCreateTable, Query FuzibleQuery)
        {
            sHeaderCreateTable = sHeaderCreateTable[0..^1]; //effacement des derniers ",                                                                                            //}

            //nettoyage du nom de la table finale (au cas ou elle aurait un format moisi genre 0001_XXX ou le nom de mes fichiers splittés
            sTableName = Toolbox.CleanTableName(sTableName);

            //bug driver oracle en bulk insert. noms en upper obligatoires
            if (JobParameters.SQLTargetBulkCopy && Connection.SConnDriver == SQLTools_Enums.BDD.DB_ORACLE)
            {
                sHeaderCreateTable = sHeaderCreateTable.ToUpper();
            }

            if (bDrop)
            {
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DROP, "DROP TABLE " + EchappementChar + sTableName + EchappementChar + ";", FuzibleQuery, sTableName);
            }

            string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE);
            sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
            sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
            sQuery = sQuery.Replace("{DEFINITION}", sHeaderCreateTable);
            if (sQuery.Length > 0)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_createtable + sTableName, SQLTools_Enums.LOG_TYPEINFO.INF);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_CREATE, sQuery, FuzibleQuery, sTableName);
            }
            else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }

        }

        private List<SQLColumn> CheckForOptionalColumnsAndAddIfNeeded(string sTableName, DataTable dtS, List<SQLColumn> sListColumsTarget, Query FuzibleQuery)
        {

            List<SQLColumn> SQLOptionalColumnsAdded = new();

            //on peut avoir envie de créer les colonnes optionnelles alors que la table existe déjà
            if (JobParameters.TargetTableBehavior != SQLTools_Enums.TARGET_TABLE_METHOD.DROP)
            {
                if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains("IDX_COL") && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains("IDX_COL")))
                {
                    SQLColumn SQLC = new(0, "IDX_COL", SQLTools_Enums.TYPE_DATA.INT, System.Type.GetType("System.Int32"), "", false, "0", false, false, "Fuzible Internal Column");
                    bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                    if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                }

                if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(JobParameters.TargetAddDbName) && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains(JobParameters.TargetAddDbName)))
                {
                    SQLColumn SQLC = new(0, JobParameters.TargetAddDbName, SqlCharTypeCompatibility, System.Type.GetType("System.String"), (dtS.Namespace.Length + 10).ToString(), false, dtS.Namespace, false, false, "Fuzible Internal Column");
                    bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                    if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                }

                if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(JobParameters.TargetAddRowNum) && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains(JobParameters.TargetAddRowNum)))
                {
                    SQLColumn SQLC = new(0, JobParameters.TargetAddRowNum, SQLTools_Enums.TYPE_DATA.INT, System.Type.GetType("System.Int32"), "", false, "0", false, false, "Fuzible Internal Column");
                    bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                    if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                }

                if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(JobParameters.TargetAddDtLoad) && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains(JobParameters.TargetAddDtLoad)))
                {
                    SQLColumn SQLC = new(0, JobParameters.TargetAddDtLoad, SqlDateTypeCompatibility, System.Type.GetType("System.DateTime"), "", false, DateTime.Now.ToString(), false, false, "Fuzible Internal Column");
                    bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                    if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                }

                if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains("SYNCHRO_TAG") && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains("SYNCHRO_TAG")))
                {
                    SQLColumn SQLC = new(0, "SYNCHRO_TAG", SqlCharTypeCompatibility, System.Type.GetType("System.String"), "2", true, "", false, false, "Fuzible Internal Column");
                    bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                    if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                }

                if (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0)
                {
                    string sDynamicParam = JobParameters.OptionalDynamicParamField_OnInsert;
                    string sColName = "DYNPARAM";
                    if (sDynamicParam.IndexOf("=") > -1)
                    {
                        sColName = sDynamicParam.Split(Convert.ToChar("="))[0];
                        sDynamicParam = sDynamicParam.Split(Convert.ToChar("="))[1];
                    }
                    if (!sDynamicParam.Contains('{', StringComparison.CurrentCulture))
                    {
                        sDynamicParam = string.Concat("{", sDynamicParam, "}");
                    }
                    sDynamicParam = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sDynamicParam, JobParameters.DynParams);
                    if (JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(sColName) && (!sListColumsTarget.Select(sC => sC.ColumnName).Contains(sColName)))
                    {
                        SQLColumn SQLC = new(0, sColName, SqlCharTypeCompatibility, System.Type.GetType("System.String"), (sDynamicParam.Length + 10).ToString(), false, sDynamicParam, false, false, "Fuzible Custom Column");
                        bool bOK = CreateColumnInTarget(sTableName, SQLC, true, FuzibleQuery);
                        if (bOK) { SQLOptionalColumnsAdded.Add(SQLC); }
                    }
                }
            }

            return SQLOptionalColumnsAdded;
        }

        public Int64 CountRowsInTargetTable(string sTableName, Query FuzibleQuery)
        {
            string sResult;
            Int64 iCountRows = 1; //pourquoi 1 ?????????????????
            DataSet dsCheck = new();

            switch (Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_MYSQL:
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //INCERTAIN
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    dsCheck = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM ", EchappementChar, sTableName, EchappementChar, ";"), FuzibleQuery, sTableName);
                    break;
            }

            sResult = BuildStringFromDs(dsCheck);

            if (sResult.Length > 0)
            {
                iCountRows = Int64.Parse(sResult);
            }

            return iCountRows;
        }

        private void ChangeNullColumnInTarget(string sTableName, SQLColumn SQLC, bool bNullAuthorized, Query FuzibleQuery)
        {
            if (Connection.SConnDriver != SQLTools_Enums.BDD.DB_SQLITE)
            {
                List<SQLColumn> sSQLCol = GetFieldsFromTable(sTableName, FuzibleQuery, SQLC.ColumnName);

                if (sSQLCol.Count > 0 && sSQLCol[0].ColumnAllowsNullValues != bNullAuthorized)
                {
                    string sQuery;
                    if (bNullAuthorized)
                    {
                        sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_ALLOW_NULL);
                    }
                    else
                    {
                        sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_DISALLOW_NULL);
                    }
                    sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                    sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                    sQuery = sQuery.Replace("{COLUMN_NAME}", sSQLCol[0].ColumnName);
                    sQuery = sQuery.Replace("{COLUMN_TYPE}", sSQLCol[0].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale));
                    if (sQuery.Length > 0)
                    {
                        ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sSQLCol[0].ColumnName);
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_DISALLOW_NULL.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
            }
        }

        private bool CreateColumnInTarget(string sTableName, SQLColumn sSQLCol, bool bNullAuthorized, Query FuzibleQuery)
        {
            //contrôle d'existence de la colonne
            List<SQLColumn> SQLC;

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_createcolumn_info01 + sSQLCol.ColumnName + " > " + sSQLCol.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale) + Languages.Languages.sql_createcolumn_info02 + sTableName, SQLTools_Enums.LOG_TYPEINFO.INF);

            string sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_COLUMN);
            sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
            sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
            sQuery = sQuery.Replace("{COLUMN_NAME}", sSQLCol.ColumnName);
            sQuery = sQuery.Replace("{COLUMN_TYPE}", sSQLCol.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale));
            sQuery = sQuery.Replace("{NULL_NOT_NULL}", bNullAuthorized ? "NULL" : "NOT NULL");
            if (sQuery.Length > 0)
            {
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sSQLCol.ColumnName);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CREATE_COLUMN.ToString() + Languages.Languages.sql_createcolumn_tryingdroprecreate, SQLTools_Enums.LOG_TYPEINFO.WNG);
                //tentative de création en passant par un 'CREATE TABLE x AS SELECT'
                string sQuery1 = string.Concat("CREATE TABLE ", Connection.SqlEchappementChar, sTableName, "_tmpfuz", Connection.SqlEchappementChar, " AS SELECT *, '' AS ", Connection.SqlEchappementChar, sSQLCol.ColumnName, Connection.SqlEchappementChar, " FROM ", Connection.SqlEchappementChar, sTableName, Connection.SqlEchappementChar);
                string sQuery2 = "DROP TABLE " + Connection.SqlEchappementChar + sTableName + Connection.SqlEchappementChar;
                string sQuery3 = string.Concat("ALTER TABLE ", Connection.SqlEchappementChar, sTableName, "_tmpfuz", Connection.SqlEchappementChar, " RENAME TO ", Connection.SqlEchappementChar, sTableName, Connection.SqlEchappementChar);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery1, FuzibleQuery, sTableName + ":" + sSQLCol.ColumnName);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery2, FuzibleQuery, sTableName + ":" + sSQLCol.ColumnName);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery3, FuzibleQuery, sTableName + ":" + sSQLCol.ColumnName);
            }

            //recheck colonne
            SQLC = GetFieldsFromTable(sTableName, FuzibleQuery, sSQLCol.ColumnName);

            if (SQLC.Count == 0)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_createcolumn_ko + sTableName + " -> " + sSQLCol.ColumnName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                return false;
            }
            else
            {
                return true;
            }

        }

        private bool ChangeColumnTypeInTarget(string sTableName, List<SQLColumn> sListColumns, SQLColumn sSQLColT, Query.QField sSQLColS, Int64 iRowsInTarget, Query FuzibleQuery)
        {
            int iErr = MyLog.JobErrors;
            int iWng = MyLog.JobWarnings;

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_info01, sSQLColS.FieldAnalyzer.ColumnName, Languages.Languages.sql_changetype_info02, sSQLColT.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, sSQLColS.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.INF);

            //sSQLColT.ColumnAllowsNullValues : subtilité : c'est la table cible qui pilote la notion de NULL / NOT NULL
            bool bNullAuthorized;
            if (iRowsInTarget == 0)
            {
                bNullAuthorized = sSQLColS.FieldAnalyzer.ColumnAllowsNullValues;
            }
            else
            {
                //si la cible n'autorise pas les NULL mais qu'il y a des NULL dans la source, il faut passer la colonne à NULL
                if (sSQLColS.FieldAnalyzer.ColumnAllowsNullValues && (!sSQLColT.ColumnAllowsNullValues))
                {
                    bNullAuthorized = true;
                }
                else { bNullAuthorized = sSQLColT.ColumnAllowsNullValues; }
            }

            string sQuery;
            sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_TYPE);
            sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
            sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
            sQuery = sQuery.Replace("{COLUMN_NAME}", sSQLColS.FieldAnalyzer.ColumnName);
            sQuery = sQuery.Replace("{COLUMN_TYPE}", sSQLColS.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale));
            sQuery = sQuery.Replace("{NULL_NOT_NULL}", bNullAuthorized ? "NULL" : "NOT NULL");
            if (sQuery.Length > 0)
            {
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);

            }
            else
            {
                //si le pattern n'existe pas, tentative de recréation de la table
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_TYPE.ToString() + ". Trying a table re-creation instead.", SQLTools_Enums.LOG_TYPEINFO.WNG);

                StringBuilder sbFields = new();
                foreach (SQLColumn sCol in sListColumns)
                {
                    if (sCol.ColumnName.Equals(sSQLColS.FieldAnalyzer.ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        sbFields.Append("CAST(" + Connection.SqlEchappementChar + sCol.ColumnName + Connection.SqlEchappementChar + " AS " + sSQLColS.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale) + ") AS " + Connection.SqlEchappementChar + sCol.ColumnName + Connection.SqlEchappementChar + ",");
                    }
                    else { sbFields.Append(Connection.SqlEchappementChar + sCol.ColumnName + Connection.SqlEchappementChar + ","); }
                }

                string sQuery1 = string.Concat("CREATE TABLE ", Connection.SqlEchappementChar, sTableName, "_tmpfuz", Connection.SqlEchappementChar, " AS SELECT " + sbFields.ToString()[0..^1] + " FROM ", Connection.SqlEchappementChar, sTableName, Connection.SqlEchappementChar);
                string sQuery2 = "DROP TABLE " + Connection.SqlEchappementChar + sTableName + Connection.SqlEchappementChar;
                string sQuery3 = string.Concat("ALTER TABLE ", Connection.SqlEchappementChar, sTableName, "_tmpfuz", Connection.SqlEchappementChar, " RENAME TO ", Connection.SqlEchappementChar, sTableName, Connection.SqlEchappementChar);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery1, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery2, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);
                ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery3, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);


            }

            if (sSQLColS.FieldAnalyzer.DefaultValue.Length > 0)
            {
                sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_DEFAULT_VALUE);
                sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{COLUMN_NAME}", sSQLColS.FieldAnalyzer.ColumnName);
                sQuery = sQuery.Replace("{DEFAULT_VALUE}", sSQLColS.FieldAnalyzer.DefaultValue.ToString());
                if (sQuery.Length > 0)
                {
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_DEFAULT_VALUE.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
            }

            if (sSQLColS.FieldAnalyzer.IsUnique)
            {
                sQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_DEFAULT_VALUE);
                sQuery = sQuery.Replace("{TABLE_NAME}", sTableName);
                sQuery = sQuery.Replace("{SCHEMA_NAME}", DefaultSchema);
                sQuery = sQuery.Replace("{COLUMNS_LIST}", EchappementChar + sSQLColS.FieldAnalyzer.ColumnName + EchappementChar);
                if (sQuery.Length > 0)
                {
                    ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND, sQuery, FuzibleQuery, sTableName + ":" + sSQLColS.FieldAnalyzer.ColumnName);
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_cantfindinipatternfor + SQLTools_Enums.DRIVER_PARAMS.CHANGE_COLUMN_ADD_UNIQUE.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG); }
            }

            //savoir si ça s'est bien passé
            if (iErr == MyLog.JobErrors && iWng == MyLog.JobWarnings)
            { return true; }
            else { return false; }
        }

        private void CompareFieldsFromSourceToTarget(string sTableName, ref List<SQLColumn> sListFieldsTarget, Query FuzibleQuery, DataTable dtData, Int64 iRowsInTarget, bool bTableJustCreated)
        {

            decimal iS;
            decimal iT;
            bool bS;
            bool bT;
            int iRowsInSource = dtData.Rows.Count;

            try
            {
                //problème : quand on utilise les données issues d'un fichier, ws... par défaut le champ est string car on ne fait pas marcher le SHS analyzer pour gagner du temps
                //3 cas de figure : 
                //1. on a droppé la table : le SHS a déjà tourné
                //2. la table a été crée à l'instant : le SHS a déjà tourné
                //3. dans tous les autres cas, le SHS n'a pas tourné
                if (JobParameters.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP || bTableJustCreated)
                {
                }
                else
                {
                    SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                    FuzibleQuery.QueryAnalyzer.SetFieldsAnalyzer(SHS.GetListFieldsTypesFromDataset(dtData, -1, FuzibleQuery, ClassPurpose));
                }

                for (int cpt = 0; cpt < sListFieldsTarget.Count; cpt += 1)
                {
                    Query.QField SQLC = null;
                    try
                    {
                        string sColTarget = sListFieldsTarget[cpt].ColumnName;
                        SQLC = FuzibleQuery.QueryAnalyzer.Fields.First(s => s.FieldAnalyzer.ColumnName.Equals(sColTarget, StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }
                    //on va ici comparer le type de colonnes sources et cible. Si le type est différent, on va mettre à jour le schéma cible pour le faire matcher avec la source
                    if (SQLC != null)
                    {
                        iS = 0;
                        iT = 0;
                        bS = false;
                        bT = false;

                        //altération des NULL
                        if (SQLC.FieldAnalyzer.ColumnAllowsNullValues) // on passe la cible en ALLOW NULL si la source possède du NULL dans un champ en particulier
                        {
                            ChangeNullColumnInTarget(sTableName, SQLC.FieldAnalyzer, SQLC.FieldAnalyzer.ColumnAllowsNullValues, FuzibleQuery);
                        }

                        //même colonne, type différent ! changement de type
                        if (SQLC.FieldAnalyzer.ColumnLinqType != sListFieldsTarget[cpt].ColumnLinqType)
                        {
                            if (!sListFieldsTarget[cpt].IsKey)
                            {
                                //il faut interdire certaines conversions (varchar -> bit, varchar -> int, datetime -> bit)
                                int iCanAlterSafely = CheckForbiddenColumnConversion(SQLC, sListFieldsTarget[cpt], iRowsInTarget, JobParameters.AlterColumnTypeOptions, sTableName);

                                switch (iCanAlterSafely)
                                {
                                    case -1: //ne DOIT pas convertir
                                        break;

                                    case 0: //ne PEUT pas convertir
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_aborting, SQLC.FieldAnalyzer.ColumnName, Languages.Languages.sql_changetype_info02, sListFieldsTarget[cpt].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, SQLC.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        break;

                                    case 1: //peut essayer de convertir au préalable les données de la colonne par sureté, puis passer en varchar (à défaut de mieux)

                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_risky, SQLC.FieldAnalyzer.ColumnName, Languages.Languages.sql_changetype_info02, sListFieldsTarget[cpt].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, SQLC.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName, Languages.Languages.sql_changetype_cast), SQLTools_Enums.LOG_TYPEINFO.INF);

                                        //attention ; le cast ne marche pas en "ACCESS" : il faut faire un CDATE, CNUMBER...
                                        if (Connection.SConnDriver != SQLTools_Enums.BDD.DB_ACCESS)
                                        {
                                            SQLTools_Enums.TYPE_DATA sTypeConvert = Toolbox.ConvertSQLDataType(SQLC, Connection);
                                            string sQuery = string.Concat("UPDATE ", Connection.SqlEchappementChar, sTableName, Connection.SqlEchappementChar,
                                                                          " SET ", Connection.SqlEchappementChar, sListFieldsTarget[cpt].ColumnName, Connection.SqlEchappementChar,
                                                                          " = ", "CAST(", Connection.SqlEchappementChar, sListFieldsTarget[cpt].ColumnName, Connection.SqlEchappementChar, " AS ", sTypeConvert.ToString(), ");");
                                            DataSet dsData = ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sQuery, FuzibleQuery, sTableName + ":" + sListFieldsTarget[cpt].ColumnName);

                                            if (dsData.Tables.Count == 0 && sListFieldsTarget[cpt].ColumnLinqType != Type.GetType("System.String"))
                                            {
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_cast_ko, SQLC.FieldAnalyzer.ColumnName, Languages.Languages.sql_changetype_info02, sListFieldsTarget[cpt].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, SQLC.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                                SQLC.FieldAnalyzer.ColumnType = sListFieldsTarget[cpt].ColumnType;
                                                SQLC.FieldAnalyzer.ColumnLinqType = sListFieldsTarget[cpt].ColumnLinqType;
                                                Toolbox.ConvertColumnType(dtData, dtData.Columns[SQLC.Name], sListFieldsTarget[cpt].ColumnLinqType);
                                            }

                                            if (dsData.Tables.Count == 0 && sListFieldsTarget[cpt].ColumnLinqType == Type.GetType("System.String"))
                                            {
                                                //on compte la longueur max qu'on doit attribuer au varchar
                                                int iMaxLengthColumn = dtData.AsEnumerable().Select(row => row[SQLC.Name]).OfType<string>().Max(val => val.Length) * 2;
                                                if (iMaxLengthColumn > 4000)
                                                {
                                                    sListFieldsTarget[cpt].ColumnLinqType = Type.GetType("System.String");
                                                    sListFieldsTarget[cpt].ColumnType = SQLTools_Enums.TYPE_DATA.TEXT;
                                                    SQLC.FieldAnalyzer.ColumnType = SQLTools_Enums.TYPE_DATA.TEXT;
                                                    SQLC.FieldAnalyzer.ColumnLinqType = Type.GetType("System.String");
                                                }
                                                else
                                                {
                                                    //si la conversion n'est pas possible, on convertit malgré tout en varchar ! on récupère au préalable la taille max de la colonne
                                                    sListFieldsTarget[cpt].ColumnLinqType = Type.GetType("System.String");
                                                    sListFieldsTarget[cpt].ColumnType = Connection.SqlCharTypeCompatibility;
                                                    SQLC.FieldAnalyzer.ColumnType = Connection.SqlCharTypeCompatibility;
                                                    SQLC.FieldAnalyzer.ColumnLinqType = Type.GetType("System.String");
                                                }
                                                SQLC.FieldAnalyzer.ColumnSize = iMaxLengthColumn.ToString();
                                                bS = decimal.TryParse(SQLC.FieldAnalyzer.ColumnSize, NumberStyles.Any, CultureInfo.InvariantCulture, out iS);
                                                bT = decimal.TryParse(sListFieldsTarget[cpt].ColumnSize, NumberStyles.Any, CultureInfo.InvariantCulture, out iT);
                                                if (iS > iT)
                                                {
                                                    if (!sListFieldsTarget[cpt].IsKey)
                                                    {
                                                        bool b1 = ChangeColumnTypeInTarget(sTableName, sListFieldsTarget, sListFieldsTarget[cpt], SQLC, iRowsInTarget, FuzibleQuery);
                                                        if (b1)
                                                        {
                                                            sListFieldsTarget[cpt].ColumnType = SQLC.FieldAnalyzer.ColumnType;
                                                            sListFieldsTarget[cpt].ColumnSize = SQLC.FieldAnalyzer.ColumnSize;
                                                        }
                                                    } //on ne touche pas les colonnes avec contraintes !
                                                }
                                            }

                                            if (dsData.Tables.Count > 0)
                                            {
                                                bool b2 = ChangeColumnTypeInTarget(sTableName, sListFieldsTarget, sListFieldsTarget[cpt], SQLC, iRowsInTarget, FuzibleQuery);
                                                if (b2)
                                                {
                                                    sListFieldsTarget[cpt].ColumnType = SQLC.FieldAnalyzer.ColumnType;
                                                    sListFieldsTarget[cpt].ColumnSize = SQLC.FieldAnalyzer.ColumnSize;
                                                }
                                            }
                                        }
                                        else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_changetype_cast_ko, SQLC.FieldAnalyzer.ColumnName, Languages.Languages.sql_changetype_info02, sListFieldsTarget[cpt].BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info03, SQLC.FieldAnalyzer.BuildTypeForTableCreation(Connection.SConnDriver, Connection.DecimalLocale), Languages.Languages.sql_changetype_info04, sTableName), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                        break;

                                    case 2: //on peut y aller sans problème
                                        bool b3 = ChangeColumnTypeInTarget(sTableName, sListFieldsTarget, sListFieldsTarget[cpt], SQLC, iRowsInTarget, FuzibleQuery);
                                        if (b3)
                                        {
                                            sListFieldsTarget[cpt].ColumnType = SQLC.FieldAnalyzer.ColumnType;
                                            sListFieldsTarget[cpt].ColumnSize = SQLC.FieldAnalyzer.ColumnSize;
                                        }
                                        break;
                                }
                            } //on ne touche pas les colonnes avec contraintes !
                        }
                        else //test de la longueur du type
                        {
                            int iCanAlterSafely = CheckForbiddenColumnConversion(SQLC, sListFieldsTarget[cpt], iRowsInTarget, JobParameters.AlterColumnTypeOptions, sTableName);

                            if (iCanAlterSafely > 1)
                            {
                                if (SQLC.FieldAnalyzer.ColumnType != SQLTools_Enums.TYPE_DATA.TEXT)
                                {
                                    bS = decimal.TryParse(SQLC.FieldAnalyzer.ColumnSize, NumberStyles.Any, CultureInfo.InvariantCulture, out iS);
                                    bT = decimal.TryParse(sListFieldsTarget[cpt].ColumnSize, NumberStyles.Any, CultureInfo.InvariantCulture, out iT);
                                    if (iS > iT)
                                    {
                                        if (!sListFieldsTarget[cpt].IsKey)
                                        {
                                            bool b4 = ChangeColumnTypeInTarget(sTableName, sListFieldsTarget, sListFieldsTarget[cpt], SQLC, iRowsInTarget, FuzibleQuery);
                                            if (b4)
                                            {
                                                sListFieldsTarget[cpt].ColumnType = SQLC.FieldAnalyzer.ColumnType;
                                                sListFieldsTarget[cpt].ColumnSize = SQLC.FieldAnalyzer.ColumnSize;
                                            }
                                        } //on ne touche pas les colonnes avec contraintes !
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        private Type RetrieveTargetFieldData(CONNString CST, SQLColumn SQLT, Query.QField SQLS, string sTableName)
        {
            if (SQLT.IsKey) //on ne touche pas à une colonne de type clé
            {
                return null;
            }
            else
            {
                Job TempParams = JobParameters.DeepCopy();
                TempParams.ConnectionString_Source = CST;
                TempParams.DatabaseName_Source = JobParameters.DatabaseName_Target;
                string sQuery = string.Concat("SELECT ", CST.SqlEchappementChar, SQLT.ColumnName, CST.SqlEchappementChar, " FROM ", CST.SqlEchappementChar, sTableName, CST.SqlEchappementChar);
                Query QTest = new Query(TempParams, sQuery);
                var dsData = MThread.GetSourceData(0, TempParams, ref QTest, MyLog, false);

                if (dsData != null && dsData.Count > 0 && dsData[0].Tables.Count > 0 && dsData[0].Tables[0].Rows.Count > 0)
                {
                    //si il y a des données qui correspondent aux données entrantes même si le schéma est différent, on change
                    SHSOperations SHS = new(JobParameters, QTest, ref MyLog);
                    List<SQLColumn> sColumns = SHS.GetListFieldsTypesFromDataset(dsData[0].Tables[0], 0, QTest, SQLTools_Enums.CLASS_PURPOSE.SRC);
                    if (sColumns.Count > 0) { return sColumns[0].ColumnLinqType; }
                    else { return null; }
                }
                else if (dsData != null && dsData.Count > 0 && dsData[0].Tables.Count > 0 && dsData[0].Tables[0].Rows.Count == 0)
                {
                    //si il n'y a pas de données dans la table cible on peut donc changer le type sans pression
                    return SQLS.FieldAnalyzer.ColumnLinqType;
                }
                else
                {
                    return null;
                }
            }
        }

        public int CheckForbiddenColumnConversion(Query.QField ColumnSource, SQLColumn ColumnTarget, Int64 iRowsTarget, int iConvertOptions, string sTableName)
        {
            int iConvertOK = 2;

            if (iConvertOptions == 3 || iConvertOptions == 4) //ne convertit que le même type
            {
                if (ColumnSource.FieldAnalyzer.ColumnLinqType == ColumnTarget.ColumnLinqType)
                {
                    iConvertOK = 2;
                }
                else if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int32") && ColumnTarget.ColumnLinqType == Type.GetType("System.Int64")) //type INT16 => INT32
                {
                    iConvertOK = 2;
                }
                else if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int16") && ColumnTarget.ColumnLinqType == Type.GetType("System.Int32")) //type INT16 => INT32
                {
                    iConvertOK = 2;
                }
                else if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int16") && ColumnTarget.ColumnLinqType == Type.GetType("System.Int64")) //type INT16 => INT32
                {
                    iConvertOK = 2;
                }
                else { iConvertOK = -1; }
            }
            else if (iConvertOptions == 1 || iConvertOptions == 5)
            {
                //il y a ce que ramène la définition de la colonne et le type de données réel. 
                //A ce titre, on peut faire l'analyse réelle du champ
                //est-ce qu'on contrôle le champ en faisant une requête SELECT *
                Type tData = RetrieveTargetFieldData(JobParameters.ConnectionString_Target, ColumnTarget, ColumnSource, sTableName);

                if (tData != null && tData == ColumnSource.FieldAnalyzer.ColumnLinqType)
                {
                    //ça signifie que le type de colonne du schéma n'est pas terrible et qu'on peut donc le changer proprement
                    return 2;
                }

                //je refuse de convertir du BIT
                if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Boolean"))
                {
                    iConvertOK = 0;
                }

                if (iRowsTarget > 0) //certaines conversions sont interdites si la cible contient des lignes
                {
                    //integer > decimal interdit
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int32") && ColumnTarget.ColumnLinqType == Type.GetType("System.Decimal"))
                    {
                        iConvertOK = 1;
                    }
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int64") && ColumnTarget.ColumnLinqType == Type.GetType("System.Decimal"))
                    {
                        iConvertOK = 1;
                    }
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int16") && ColumnTarget.ColumnLinqType == Type.GetType("System.Decimal"))
                    {
                        iConvertOK = 1;
                    }
                    //decimal > varchar interdit
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Decimal") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }
                    //int > varchar interdit
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int32") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int64") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int16") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }
                    //varchar > datetime
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.DateTime") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }

                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.DateTimeOffset") && ColumnTarget.ColumnLinqType == Type.GetType("System.String"))
                    {
                        iConvertOK = 1;
                    }

                    //bitArray
                    if (ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Collections.BitArray"))
                    {
                        iConvertOK = 0;
                    }
                    //int32->int64 interdit en Sqlite (pas de support INT64)
                    if (Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE && ColumnSource.FieldAnalyzer.ColumnLinqType == Type.GetType("System.Int64") && ColumnTarget.ColumnLinqType == Type.GetType("System.Int32"))
                    {
                        iConvertOK = 0;
                    }
                }
            }
            else { iConvertOK = -1; }

            return iConvertOK;
        }

        private async Task<DataSet> GetDataFromMySql(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            MySqlCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionMySQL);

            try
            {
                MySqlDataReader reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;
                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            { throw; }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromSQLServer(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            SqlCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionSQLServer)
            {
                CommandTimeout = ConnexionSQLServer.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionSQLServer.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                SqlDataReader reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation))
                { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromSqlite(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            SqliteCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionSqlite)
            {
                CommandTimeout =  JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                var reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromODBC(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            OdbcCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionODBC)
            {
                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                var reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromPostgre(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            NpgsqlCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionPOSTGRE)
            {
                CommandTimeout = ConnexionPOSTGRE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionPOSTGRE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                NpgsqlDataReader reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromAccess(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            OleDbCommand Commande = new(FuzibleQuery.SQLQuery, ConnexionACCESS)
            {
                CommandTimeout = JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                var reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);

                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private async Task<DataSet> GetDataFromOracle(Query FuzibleQuery, int iQteRows, bool bOutputTableExists)
        {
            DataSet dsData = new();

            string sFichierFinal = FuzibleQuery.OutputTable;
            int iQteFields;
            List<int> iColAvoid = new();

            string sSQLQuery = FuzibleQuery.SQLQuery;
            if (sSQLQuery.EndsWith(";")) { sSQLQuery = sSQLQuery[0..^1]; }

            OracleCommand Commande = new(sSQLQuery, ConnexionORACLE)
            {
                CommandTimeout = ConnexionORACLE.CommandTimeout != DEFAULT_COMMAND_TIMEOUT ? ConnexionORACLE.CommandTimeout : JobParameters.GlobalParameters.SQL_COMMAND_TIMEOUT
            };

            try
            {
                var reader = await Commande.ExecuteReaderAsync(Monitoring.TaskCancellationToken);
                iQteFields = reader.FieldCount;
                object oValue;

                try
                {
                    dsData.Tables.Add(CreateDataTableFromSchema(reader.GetSchemaTable(), bOutputTableExists, FuzibleQuery, ref iColAvoid));
                }
                catch
                {
                    dsData.Tables.Add(FuzibleQuery.OutputTable);

                    for (int cptField = 0; cptField < iQteFields; cptField += 1)
                    {
                        if (!dsData.Tables[0].Columns.Contains(reader.GetName(cptField).ToString()))
                        {
                            try { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString(), reader.GetFieldType(cptField)); }
                            catch { dsData.Tables[0].Columns.Add(reader.GetName(cptField).ToString()); }
                        }
                        else
                        {
                            iColAvoid.Add(cptField);
                        }
                    }
                }

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_compmode_writing, SQLTools_Enums.LOG_TYPEINFO.INF);

                bool bLigneOK = true;
                Exception exValue;
                DataRow dtRow;
                int cptQ = 0;
                var cols = Toolbox.GetSQLColumnsFromDataTable(dsData.Tables[0]);

                int iInsert = 0;
                var tOperation = new List<SQLStreamingData>();
                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                {
                    tOperation.Add(null);
                }

                DateTime dtStart = DateTime.Now;
                DateTime dtBatchStreamingMode = DateTime.Now;

                bool bFirstPass = false;

                System.Timers.Timer timer = new System.Timers.Timer(JobParameters.GlobalParameters.SQL_DIRECTSTREAM_COMMIT * 1000);
                if (JobParameters.SQLDirectStream)
                {
                    timer.Elapsed += (sender, e) => OnTimerStreamingMode(FuzibleQuery, bOutputTableExists, cptQ, tOperation, dsData, ref iInsert);
                    timer.AutoReset = true;
                    timer.Enabled = true;
                }

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    cptQ++;

                    if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                    {
                        bFirstPass = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.sql_compmode_parsing01, cptQ.ToString(), Languages.Languages.sql_compmode_parsing02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                    {
                        bFirstPass = false;
                    }

                    if (cptQ > iQteRows)
                    { break; }

                    bLigneOK = true;
                    exValue = null;

                    // Initialisation avec la bonne taille
                    List<object> sListRow = new List<object>(new object[iQteFields - iColAvoid.Count]);
                    int iIndex = -1;

                    for (int cptCol = 0; cptCol < iQteFields; cptCol++)
                    {
                        if (!iColAvoid.Contains(cptCol))
                        {
                            oValue = DBNull.Value;
                            try
                            {
                                //Type tC = Type.GetType("System.String");
                                //try { tC = reader[cptCol].GetType(); }
                                //catch { tC = Type.GetType("System.String"); }

                                try
                                {
                                    oValue = reader[cptCol];

                                    if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType)
                                    {
                                        try
                                        {
                                            var col = cols.FirstOrDefault(c => c.ColumnIndex.Equals(cptCol));
                                            Type tC = col.ColumnLinqType;
                                            oValue = Toolbox.ConvertValue(oValue, tC, col);
                                            if (oValue.ToString().Length == 0)
                                            { oValue = DBNull.Value; }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                                $"(IDX={cptCol}, CTC={cols.Count})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex,
                                        $"(IDX={cptCol})", SQLTools_Enums.LOG_TYPEINFO.DBG);
                                    oValue = DBNull.Value;
                                    bLigneOK = false;
                                }

                                //if (JobParameters.TrimData && oValue is string)
                                //    oValue = oValue.ToString().Trim();
                                //else if (tC.FullName.Equals("System.DBNull"))
                                //    oValue = DBNull.Value;

                                //// Accès sécurisé à l'index correct
                                iIndex++;
                                sListRow[iIndex] = oValue;
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, $"(DR:{reader})", SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }
                    }

                    if (!bLigneOK)
                    {
                        StringBuilder sbError = new();
                        sbError.Append(string.Concat("[", sFichierFinal, "] "));
                        for (int cptC = 0; cptC < iQteFields; cptC += 1)
                        {
                            try
                            {
                                sbError.Append(string.Concat(reader.GetValue(cptC).ToString().Trim(), ";"));
                            }
                            catch (Exception)
                            {
                                sbError.Append(string.Concat("ERR(", reader.GetName(cptC), ");"));
                            }
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, exValue, sbError.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    //rajout de valeurs vides si il en manquait
                    if (sListRow.Count < iQteFields - iColAvoid.Count)
                    {
                        int iQToAdd = iQteFields - sListRow.Count;
                        for (int iCpt = 0; iCpt < iQToAdd; iCpt++)
                        {
                            sListRow.Add(DBNull.Value);
                        }
                    }

                    dtRow = dsData.Tables[0].NewRow();
                    try
                    {
                        dtRow.ItemArray = sListRow.ToArray();
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (JobParameters.SQLDirectStream)
                    {
                        if (cptQ % STREAMING_MODE_SLEEP_RATE == 0)
                        {
                            double dMsBatch = (DateTime.Now - dtBatchStreamingMode).TotalMilliseconds;
                            dtBatchStreamingMode = DateTime.Now;
                            CalculateSleepTime(tOperation, dMsBatch, false);
                            if (STREAMING_MODE_SLEEP_TIME > 0)
                            { Thread.Sleep(STREAMING_MODE_SLEEP_TIME); }
                        }
                    }

                    lock (dsData)
                    {
                        if (dsData.Tables[0].Rows.Count == 0) //gestion du timing pour ralentir le process de lecture (et éviter la saturation RAM)
                        { STREAMING_MODE_START_DATE = DateTime.Now; }

                        dsData.Tables[0].Rows.Add(dtRow);
                    }
                }

                timer.Stop();
                timer.Enabled = false;

                while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }

                //les lignes finales restantes
                if (JobParameters.IsRunning && JobParameters.SQLDirectStream && dsData.Tables[0].Rows.Count > 0)
                {
                    MThread.RepSyncTask_DirectSQLStream(1, iInsert == 0 ? 0 : 2, cptQ, dsData, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog);
                    if (iInsert == 0) //dans le cas ou il n'y aurait eu qu'une passe, cette section de code ne sera pas exécutée dans RepSyncTask_DirectSQLStream
                    { MyLog.AddJobReportRow(FuzibleQuery.OutputTable, cptQ, 0, cptQ, 0, 0); }
                }
            }
            catch (OperationCanceledException)
            {
                await Commande.DisposeAsync();
                throw;
            }
            catch (Exception ex)
            {
                dsData.Clear();
                dsData.Dispose();
                dsData = new DataSet();
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            await Commande.DisposeAsync();

            return dsData;
        }

        private void CalculateSleepTime(List<SQLStreamingData> tOperation, double dReadBatchMs, bool bFinished)
        {
            SQLStreamingData sData = null;
            lock (tOperation)
            {
                sData = bFinished ? tOperation.FirstOrDefault(t => t != null && t.Operation.IsCompleted) : tOperation.FirstOrDefault(t => t != null && !t.Operation.IsCompleted);
            }

            if (sData != null) //aller chercher des stats de durée d'exécution pour ajuster le temps de lecture
            {
                int iSleepTime = sData.CalculateSleepTimePer100Rows(JobParameters.SQLDirectStreamPriority, STREAMING_MODE_SLEEP_RATE, dReadBatchMs);

                if (iSleepTime > 0)
                {
                    STREAMING_MODE_SLEEP_TIME = iSleepTime;
                }
                else
                {
                    STREAMING_MODE_SLEEP_TIME = 0;
                    //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_target_directstreamcopy_changereadspeed, " ", STREAMING_MODE_SLEEP_TIME.ToString(), " (RT:", sData.ReadTime.ToString(), " / WT:", sData.WriteTime.ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                }
            }
            else { STREAMING_MODE_SLEEP_TIME = 0; }
        }

        private void OnTimerStreamingMode(Query FuzibleQuery, bool bOutputTableExists, int cptQ, List<SQLStreamingData> tOperation, DataSet dsData, ref int iInsert)
        {
            SQLStreamingData sData = null;

            if (JobParameters.SQLDirectStream && JobParameters.IsRunning)
            {
                if (cptQ >= (bOutputTableExists ? 0 : MIN_ROWS_SQLDIRECTSTREAM_FIRSTPASS) && !STREAMING_MODE_BUSY) //première itération avec un seuil minimum pour pouvoir créer une table proprement si besoin
                {
                    int iSlot = Toolbox.GetAvailableTaskSlot(tOperation, iInsert);
                    lock (tOperation) { sData = tOperation.FirstOrDefault(t => t != null && t.Operation.IsCompleted); }

                    int iRows = dsData.Tables[0].Rows.Count;

                    if (iRows >= MIN_ROWS_SQLDIRECTSTREAM_ALLPASS && iSlot > -1)
                    {
                        STREAMING_MODE_BUSY = true;

                        //if (sData != null)
                        //{
                        //    CalculateSleepTime(tOperation, true);
                        //    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_target_directstreamcopy_synchroinfo, " R:", sData.QteRows, " / ST:", STREAMING_MODE_SLEEP_TIME.ToString(), " / RT:", sData.ReadTime.ToString(), " / WT:", sData.WriteTime.ToString()), SQLTools_Enums.LOG_TYPEINFO.DET);
                        //}

                        DataSet dsCopy = null;
                        int iFixedCpt = cptQ;
                        int iFixedInsert = iInsert;

                        lock (dsData)
                        {
                            dsCopy = dsData.Copy();
                            dsData.Tables[0].Rows.Clear();
                        }

                        double iLoadTime = (DateTime.Now - STREAMING_MODE_START_DATE).TotalMilliseconds;
                        SQLStreamingData op = new SQLStreamingData(iLoadTime, iRows, new Task(() => MThread.RepSyncTask_DirectSQLStream(iSlot, iFixedInsert == 0 ? 0 : 1, iFixedCpt, dsCopy, JobParameters.DeepCopy(), FuzibleQuery, ref MyLog)));
                        tOperation[iSlot] = op;

                        tOperation[iSlot].StartOperation();

                        iInsert++;

                        STREAMING_MODE_BUSY = false;
                    }
                }

                if (STREAMING_MODE_SLEEP_TIME >= 0 && sData == null)
                {
                    lock (tOperation) { sData = tOperation.FirstOrDefault(t => t != null && !t.Operation.IsCompleted); }
                    if (sData != null)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_target_directstreamcopy_changereadspeed.Replace(" / 1000", " / " + STREAMING_MODE_SLEEP_RATE.ToString()), " ", STREAMING_MODE_SLEEP_TIME.ToString(), " (R:", sData.QteRows, " / RT:", Math.Round(sData.ReadTime, 2).ToString(), " / ARS:", Math.Round(sData.ProjectedReadTime, 2).ToString(), " / WT:", Math.Round(sData.WriteTime, 2).ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                }
            }
        }

        private DataTable CreateDataTableFromSchema(DataTable dtSchema, bool bOutputTableExists, Query Q, ref List<int> iColAvoid)
        {
            List<SQLColumn> TargetColumns = null;
            if (JobParameters.IsRunning && JobParameters.ConnectionString_Target.SConnDriverSuffix.Equals("DB") && JobParameters.SQLTrustTargetColumnType & bOutputTableExists)
            {
                SQLTools sql = new SQLTools(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                TargetColumns = sql.GetFieldsFromTable(Q.OutputTable, Q);
            }

            //insertion des données de driver : 
            List<SQLColumn> sDriverColumns = new();
            DataTable dtData = new(Q.OutputTable);
            StringBuilder sbBug = new();
            int iCptCol = -1;

            foreach (DataRow dr in dtSchema.Rows)
            {
                bool bAvoid = false;
                iCptCol++;
                try
                {
                    if (!dtData.Columns.Contains((string)dr["ColumnName"]))
                    {
                        //(sType.IndexOf("SINGLE") > -1) { TypeField = Type.GetType("System.Double"); }
                        Type tpColumn = dr["DataType"] == DBNull.Value ? Type.GetType("System.String") : (Type)dr["DataType"];
                        if (tpColumn.Equals("System.Single")) { tpColumn = Type.GetType("System.Double"); }

                        if (JobParameters.IsRunning && JobParameters.SQLTrustTargetColumnType && TargetColumns.FirstOrDefault(c => c.ColumnName.Equals((string)dr["ColumnName"], StringComparison.OrdinalIgnoreCase)) != null)
                        {
                            tpColumn = TargetColumns.FirstOrDefault(c => c.ColumnName.Equals((string)dr["ColumnName"], StringComparison.OrdinalIgnoreCase)).ColumnLinqType;
                        }

                        dtData.Columns.Add((string)dr["ColumnName"], tpColumn);

                        if (Q.ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_ODBC) //trop de risques d'erreurs avec les drivers ODBC exotiques
                        {
                            if (Q.ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_SQLITE) //SQLite peut reporter du NOT NULL sur une colonne qui en a
                            {
                                if (dr.Table.Columns.Contains("AllowDBNull") && dr["AllowDBNull"] != DBNull.Value) { dtData.Columns[dtData.Columns.Count - 1].AllowDBNull = (bool)dr["AllowDBNull"]; }
                            }

                            if (dr.Table.Columns.Contains("ColumnSize") && dr["ColumnSize"] != DBNull.Value && dtData.Columns[dtData.Columns.Count - 1].DataType.Equals(Type.GetType("System.String")))  //taille s'applique uniquement sur string
                            {
                                int iLen = (int)dr["ColumnSize"];

                                if (iLen > 0) { dtData.Columns[dtData.Columns.Count - 1].MaxLength = iLen; }
                                else
                                {
                                    if (Q.ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_SQLITE) //Sqlite n'a pas de tailles définies
                                    {
                                        sbBug.Append(string.Concat(dtData.Columns[dtData.Columns.Count - 1].ColumnName, " : ", Languages.Languages.sql_column_wrong_string_size, ","));
                                    }
                                }
                            }

                            //if (dr.Table.Columns.Contains("IsReadOnly") && dr["IsReadOnly"] != DBNull.Value) { dtData.Columns[dtData.Columns.Count - 1].ReadOnly = (bool)dr["IsReadOnly"]; }
                            if (dr.Table.Columns.Contains("IsUnique") && dr["IsUnique"] != DBNull.Value) { dtData.Columns[dtData.Columns.Count - 1].Unique = (bool)dr["IsUnique"]; }
                        }
                    }
                    else { iColAvoid.Add(iCptCol); bAvoid = true; }
                }
                catch (Exception ex)
                {
                    dtData.Columns.Add((string)dr["ColumnName"]);
                    sbBug.Append(Languages.Languages.sql_wrongcolumnschema);
                    foreach (DataColumn dc in dtSchema.Columns)
                    {
                        sbBug.Append(string.Concat(dc.ColumnName, "=", dr[dc.ColumnName].ToString(), ","));
                    }
                    sbBug.Append(string.Concat(" (", ex.Message, ")"));
                }

                if (!bAvoid)
                {
                    StringBuilder sbCaption = new();
                    foreach (DataColumn dc in dtSchema.Columns)
                    {
                        sbCaption.AppendLine(string.Concat("\t", dc.ColumnName, "=", dr[dc.ColumnName].ToString(), ","));
                    }
                    dtData.Columns[dtData.Columns.Count - 1].ExtendedProperties.Add("Driver Data", sbCaption.ToString());

                    SQLTools_Enums.TYPE_DATA sType = SQLTools_Enums.TYPE_DATA.TEXT;
                    try
                    {
                        sType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), dtData.Columns[dtData.Columns.Count - 1].DataType.Name.ToUpper().Replace(" ", "_").Replace("[", "XXX").Replace("]", "ZZZ"));
                    }
                    catch { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_column_unknown_type + " (" + dtData.Columns[dtData.Columns.Count - 1].ColumnName + " : " + dtData.Columns[dtData.Columns.Count - 1].DataType.Name + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    sDriverColumns.Add(new SQLColumn(dtData.Columns[dtData.Columns.Count - 1].Ordinal, dtData.Columns[dtData.Columns.Count - 1].ColumnName, sType, Type.GetType(dtData.Columns[dtData.Columns.Count - 1].DataType.FullName),
                        dtData.Columns[dtData.Columns.Count - 1].MaxLength.ToString(), dtData.Columns[dtData.Columns.Count - 1].AllowDBNull,
                        dtData.Columns[dtData.Columns.Count - 1].DefaultValue.ToString(), dtData.Columns[dtData.Columns.Count - 1].Unique,
                        dtData.Columns[dtData.Columns.Count - 1].Unique, dtData.Columns[dtData.Columns.Count - 1].ExtendedProperties.Contains("Driver Data") ? dtData.Columns[dtData.Columns.Count - 1].ExtendedProperties["Driver Data"].ToString() : ""));
                }
            }

            if (sbBug.Length > 0)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sbBug.ToString(), SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            Q.QueryAnalyzer.SetFieldsBySQLColumns(Q.OutputTable, sDriverColumns);

            return dtData;
        }

        #endregion

        #region "STATIC"

        public static DataTable Join2Dt(DataTable dtA, DataTable dtB, string sJoinField)
        {
            var dt = new DataTable();
            var joinTable = from t1 in dtA.AsEnumerable()
                            join t2 in dtB.AsEnumerable()
                                on t1[sJoinField] equals t2[sJoinField]
                            select new
                            {
                                t1,
                                t2
                            };

            foreach (DataColumn col in dtA.Columns)
            {
                dt.Columns.Add(col.ColumnName, typeof(string));
            }

            dt.Columns.Remove(sJoinField);

            foreach (DataColumn col in dtB.Columns)
            {
                dt.Columns.Add(col.ColumnName, typeof(string));
            }

            foreach (var row in joinTable)
            {
                DataRow newRow = dt.NewRow();
                newRow.ItemArray = row.t1.ItemArray.Union(row.t2.ItemArray).ToArray();
                dt.Rows.Add(newRow);
            }
            dt.CaseSensitive = false;
            return dt;
        }

        public static List<string[]> ScanForDBInstancesInNetwork(SQLTools_Enums.BDD sBDD, int iPort)
        {
            DataTable dtConn = new();
            List<string[]> sListConn = new();

            switch (sBDD)
            {
                //             case SQLTools_Enums.BDD.DB_SQLSERVER:
                //                 try
                //                 {
                //                     //TODO !!!
                //                     System.Data.Sql.SqlDataSourceEnumerator instance =
                //System.Data.Sql.SqlDataSourceEnumerator.Instance
                //                     dtConn = SqlDataSourceEnumerator.Instance.GetDataSources();
                //                     //ServerName: COMPUTER1
                //                     //InstanceName   : SQLEXPRESS
                //                     //IsClustered    : No
                //                     //Version        : 9.00.4035.00
                //                     foreach (DataRow dr in dtConn.Rows)
                //                     {
                //                         string sConnString = string.Concat("SERVER=", dr[0], ";DATABASE=;User ID=;Password=;Trusted_Connection=True;Connection Timeout=60;Integrated Security=true;");
                //                         sListConn.Add(new string[5] { dr[0].ToString(), dr[1].ToString(), dr[2].ToString(), dr[3].ToString(), sConnString });
                //                     }
                //                 }
                //                 catch (Exception ex)
                //                 { throw ex; }
                //                 break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    //scan filesystem for .db ou .accdb files
                    foreach (string filePath in Directory.GetDirectories("C:\\"))
                    {
                        try
                        {
                            if (!filePath.ToLower().Contains("program files", StringComparison.CurrentCulture) || !filePath.ToLower().Contains("windows", StringComparison.CurrentCulture))
                            {
                                try
                                {
                                    foreach (string sF in Directory.GetFiles(filePath, "*.*", SearchOption.AllDirectories))
                                    {
                                        if (sF.EndsWith(".db") || sF.EndsWith(".accdb"))
                                        {
                                            sListConn.Add(new string[5] { sF, "Access DB : " + sF, "", "", string.Concat("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + sF) });
                                        }
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { break; }
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    string[] sDrivers = SQLTools.GetOdbcDriverNames();
                    foreach (string s in sDrivers)
                    {
                        if (s.StartsWith("Microsoft") && s.IndexOf("Excel") > 0)
                        {
                            sListConn.Add(new string[5] { s, "Excel ODBC : " + s, "", "32 bits", string.Concat("Driver={", s, "};Dbq=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\;Extensions=asc,csv,tab,txt;ReadOnly=0;") });
                        }
                        else if (s.StartsWith("Microsoft") && s.IndexOf("Access") > 0)
                        {
                            sListConn.Add(new string[5] { s, "Access ODBC : " + s, "", "32 bits", string.Concat("Driver={", s, "};Dbq=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\;ReadOnly=0;") });
                        }
                        else { sListConn.Add(new string[5] { s, "ODBC : " + s, "", "32 bits", string.Concat("Driver={", s, "};") }); }
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    try
                    {
                        string[] sFiles = Directory.GetFiles(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "*.db", SearchOption.AllDirectories);
                        string[] sFilesB = Directory.GetFiles(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "*.Sqlite3", SearchOption.AllDirectories);
                        foreach (string s in sFiles)
                        {
                            sListConn.Add(new string[5] { Path.GetFileName(s), "Sqlite : " + Path.GetFileName(s), "", "Local", string.Concat("Data Source=", s, ";") });
                        }
                        foreach (string s in sFilesB)
                        {
                            sListConn.Add(new string[5] { Path.GetFileName(s), "Sqlite : " + Path.GetFileName(s), "", "Local", string.Concat("Data Source=", s, ";") });
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    break;
                default:
                    try
                    {
                        List<string> sListIP = new();
                        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                        foreach (IPAddress ip in host.AddressList)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                sListIP.Add(ip.ToString()); break;
                            }
                        }
                        //scan subnetwork
                        for (int iPP = 0; iPP <= 255; iPP++)
                        {
                            string sIp = sListIP[0][..sListIP[0].LastIndexOf(".")];
                            sIp = string.Concat(sIp, ".", iPP.ToString());
                            using TcpClient Scan = new();
                            try
                            {
                                IAsyncResult result = Scan.BeginConnect(sIp, iPort, null, null);
                                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
                                if (success)
                                {
                                    string sDns = "";
                                    try { IPHostEntry IP = Dns.GetHostEntry(sIp); sDns = IP.HostName; }
                                    catch (SocketException) { sDns = sIp; }

                                    switch (sBDD)
                                    {
                                        case SQLTools_Enums.BDD.DB_MYSQL:
                                            string sConnString1 = string.Concat("server=", sDns, ";Port=", iPort.ToString(), ";uid=;pwd=;DATABASE=;Convert Zero Datetime=True;SslMode=none;");
                                            sListConn.Add(new string[5] { sDns, "MySQL : " + sDns, "", sIp, sConnString1 });
                                            break;
                                        case SQLTools_Enums.BDD.DB_POSTGRE:
                                            string sConnString2 = string.Concat("Server=", sDns, ";Port=", iPort.ToString(), ";Database=;Userid=;Password=;Ssl Mode=Require;Trust Server Certificate=true;");
                                            sListConn.Add(new string[5] { sDns, "Postgres : " + sDns, "", sIp, sConnString2 });
                                            break;
                                        case SQLTools_Enums.BDD.DB_ORACLE:
                                            string sConnString3 = string.Concat("Data Source=", sDns, ";User Id=myUsername;Password=myPassword;Integrated Security=no;");
                                            sListConn.Add(new string[5] { sDns, "Oracle : " + sDns, "", sIp, sConnString3 });
                                            break;
                                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                                            string sConnString4 = string.Concat("SERVER=", sDns, ";DATABASE=;User ID=;Password=;Trusted_Connection=True;Connection Timeout=60;Integrated Security=true;");
                                            sListConn.Add(new string[5] { sDns, "Sql Server : " + sDns, "", sIp, sConnString4 });
                                            break;
                                    }
                                }
                            }
                            catch
                            {
                                //port fermé
                            }
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    break;
            }
            return sListConn;
        }

        private string ExtractDefaultSchemaFromDBString(SQLTools_Enums.BDD bdd, string sConnString, string sSchemaFromParams)
        {
            switch (bdd)
            {
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    if (sSchemaFromParams.Length > 0)
                    {
                        return sSchemaFromParams;
                    }
                    else
                    {
                        return "dbo";
                    }
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    string sPath = "public";
                    //recherche Seartch_Path
                    if (sConnString.IndexOf("Search_Path=", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        sPath = sConnString[sConnString.IndexOf("Search_Path=", StringComparison.OrdinalIgnoreCase)..];
                        if (sPath.IndexOf(";") > 0) { sPath = sPath[..sPath.IndexOf(";")]; }

                        try
                        {
                            sPath = sPath[(sPath.IndexOf("=") + 1)..];
                            if (sPath.Length == 0)
                            {
                                sPath = "public";
                            }
                        }
                        catch { sPath = "public"; }
                    }
                    return sPath;
                default:
                    return "";
            }        
        }

        #endregion
    }

    public class SqliteDataAdapter
    {
        private SqliteCommand _cmd = null;

        public SqliteDataAdapter(SqliteCommand SqliteCmd)
        {
            _cmd = SqliteCmd;
        }

        public void Fill(DataSet dsData, string sDtName)
        {
            dsData.Tables.Clear();
            dsData.Clear();
            if (sDtName.Length > 0) { dsData.DataSetName = sDtName; }

            DataTable dt = new(sDtName);

            try
            {
                var reader = _cmd.ExecuteReader();
                var dtSchema = reader.GetSchemaTable();
                var dtColumnSchema = reader.GetColumnSchema();

                List<string> sListColumnsSchema = new();

                foreach (var col in dtColumnSchema)
                {
                    DataColumn newcol = new(col.ColumnName, col.DataType);
                    if (col.IsAutoIncrement != null)
                    { newcol.AutoIncrement = col.IsAutoIncrement.Value; }

                    //if (col.IsReadOnly != null)
                    //{ newcol.ReadOnly = col.IsReadOnly.Value; }

                    if (col.IsUnique != null)
                    { newcol.Unique = col.IsUnique.Value; }

                    int iLoop = 0;
                    while (dt.Columns.Contains(newcol.ColumnName))
                    {
                        iLoop++;
                        newcol.ColumnName = string.Concat(newcol.ColumnName, "@", iLoop);
                    }
                    dt.Columns.Add(newcol);

                }

                //foreach (string sCol in sListColumnsSchema)
                //{
                //    var col = dtColumnSchema.First(c => c.ColumnName.Equals(sCol));

                //    if (col.ColumnOrdinal != null)
                //    { dt.Columns[col.ColumnName].SetOrdinal(col.ColumnOrdinal.Value); }
                //}

                while (reader.Read())
                {
                    DataRow dr = dt.NewRow();
                    foreach (DataColumn col in dt.Columns)
                    {
                        string sCol = col.ColumnName.Split('@')[0];
                        try
                        {
                            dr[col.ColumnName] = reader.GetValue(sCol);
                        }
                        catch
                        { }
                    }
                    dt.Rows.Add(dr);
                }

                dsData.Tables.Add(dt);
            }
            catch { throw; }

        }

        public async Task FillAsync(DataSet dsData, string sDtName)
        {
            dsData.Tables.Clear();
            dsData.Clear();
            if (sDtName.Length > 0) { dsData.DataSetName = sDtName; }

            DataTable dt = new(sDtName);

            try
            {
                var reader = _cmd.ExecuteReader();
                var dtSchema = reader.GetSchemaTable();
                var dtColumnSchema = reader.GetColumnSchema();

                List<string> sListColumnsSchema = new();

                foreach (var col in dtColumnSchema)
                {
                    DataColumn newcol = new(col.ColumnName, col.DataType);
                    if (col.IsAutoIncrement != null)
                    { newcol.AutoIncrement = col.IsAutoIncrement.Value; }

                    //if (col.IsReadOnly != null)
                    //{ newcol.ReadOnly = col.IsReadOnly.Value; }

                    if (col.IsUnique != null)
                    { newcol.Unique = col.IsUnique.Value; }

                    int iLoop = 0;
                    while (dt.Columns.Contains(newcol.ColumnName))
                    {
                        iLoop++;
                        newcol.ColumnName = string.Concat(newcol.ColumnName, "@", iLoop);
                    }
                    dt.Columns.Add(newcol);

                }

                //foreach (string sCol in sListColumnsSchema)
                //{
                //    var col = dtColumnSchema.First(c => c.ColumnName.Equals(sCol));

                //    if (col.ColumnOrdinal != null)
                //    { dt.Columns[col.ColumnName].SetOrdinal(col.ColumnOrdinal.Value); }
                //}

                while (await reader.ReadAsync(Monitoring.TaskCancellationToken))
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    DataRow dr = dt.NewRow();
                    foreach (DataColumn col in dt.Columns)
                    {
                        string sCol = col.ColumnName.Split('@')[0];
                        try
                        {
                            dr[col.ColumnName] = reader.GetValue(sCol);
                        }
                        catch
                        { }
                    }
                    dt.Rows.Add(dr);
                }

                dsData.Tables.Add(dt);
            }
            catch (OperationCanceledException)
            { throw; }
            catch (Exception) { throw; }

        }
    }
}