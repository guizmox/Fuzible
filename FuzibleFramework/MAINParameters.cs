using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Data.Sqlite;
using System.Data;

namespace FuzibleFramework
{

    public class MAINParameters
    {
        #region "CONSTANTES"

        #endregion

        #region "VARIABLES"
        public CONNStrings Connections { get; internal set; } = null;
        #endregion

        #region "PROPRIETES"

        public string FUZIBLE_SERVER { get; private set; } = "https://github.com/guizmox/Fuzible";

        public bool SHOW_SYSTEM_ALERTS { get; private set; } = true;

        public bool ALLOW_INTEGERS_WITH_SPECIALS { get; private set; } = false;
        public bool ALLOW_INTEGERS_STARTING_WITH_0 { get; private set; } = false;
        public int ABORT_JOB_ON_ERRORS { get; private set; } = 10;
        public int LOG_DAYSTOKEEP { get; private set; } = 8;
        public string LOG_CONNECTIONSTRING { get; private set; } = "Data Source=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db;foreign keys=true";
        public string MAIL_CONNECTIONSTRING { get; private set; } = "";
        public SQLTools_Enums.BDD LOG_BDDDRIVER { get; private set; } = SQLTools_Enums.BDD.DB_SQLITE;
        public string LOG_CONNECTIONSTRINGSCHEMA { get; internal set; } = "";
        public bool LOG_MAIL_DONTSEND_IFSUCCESS { get; private set; } = false;
        public string APP_LANGUAGE { get; private set; } = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName.ToUpper();
        public int MAX_SQL_ERRORS { get; private set; } = 32;
        public int MAX_SQL_QUERY_DEBUG_MODE { get; private set; } = 16384;
        public int SQL_COMMIT { get; private set; } = 128;
        public int SQL_DECIMALS { get; private set; } = 20;
        public int SQL_COMMAND_TIMEOUT { get; private set; } = 360;
        public string SQL_SYNCHRO_MODE_TABLE { get; private set; } = "shs_fuzible_synchro_records";
        public bool SQL_SHRINK_TABLES { get; private set; } = false;
        public int DEFAULT_MONGODB_PORT { get; private set; } = 27017;
        public int DEFAULT_MYSQL_PORT { get; private set; } = 3306;
        public int DEFAULT_POSTGRES_PORT { get; private set; } = 5432;
        public int DEFAULT_ORACLE_PORT { get; private set; } = 1522;
        public int DEFAULT_MSSQL_PORT { get; private set; } = 1433;
        public string FILE_PROCESSED_DIR { get; private set; } = "Processed";
        public bool FILE_ADD_DATETIME_PREFIX { get; private set; } = true;
        public string FILE_EXPORT_DIR { get; private set; } = "Export";
        public string CSV_SEPARATORS { get; private set; } = ";,|\t.";
        public int CSV_MAXLINES_BEFORE_SPLIT { get; private set; } = 500000;
        public string XML_PARENT_NODE { get; private set; } = "<FuzibleXML version=\"[VERSION]\" user=\"[USER]\">";
        public string JSON_PARENT_NODE { get; private set; } = "{\"version\":\"[VERSION]\",\"user\":\"[USER]\",\"datetime\":\"[DATETIME]\",\"namespace\":\"[NAMESPACE]\",\"content\":[";
        public bool JSON_ALTERNATIVE_PROCESSING_MODE { get; private set; } = false;
        public decimal CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET { get; private set; } = 0.50M;
        public decimal CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET { get; private set; } = 0.75M;
        public bool CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT { get; private set; } = false;
        public int FILE_MAX_ROWS_ANALYZER { get; private set; } = 10000;
        public int CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS { get; private set; } = 100;
        public int FILE_DAYS_TO_KEEP_PROCESSED { get; private set; } = 30;

        public int MAIL_MAXPJ_SIZE { get; private set; } = 2048;
        public int MAIL_TIMEOUT { get; private set; } = 30000;
        public int MAIL_MAXCHAR_BEFORE_PJ { get; private set; } = 65536;

        public int WS_TIMEOUT { get; private set; } = 30000;

        public bool WS_ALLOW_RESPONSES_ALTER_COLUMNS { get; private set; } = true;
        public string WS_DEFAULT_ENCODING { get; private set; } = "ISO-8859-1";
        public string WS_NUXEO_DEFAULT_ENCODING { get; private set; } = "UTF-8";

        public List<string> APP_ADMINS = new();

        public int SQL_BULK_INSERT_BATCH_SIZE { get; private set; } = 1000;
        public bool SQL_SHRINK = true;
        public int SQL_HIGH_DATE = 2500;
        public int SQL_LOW_DATE = 1800;
        public string[] DATE_FORMATS = new string[] { "yyyyMMdd", "ddMMyyyy" };
        public bool TURBO_MODE = false;

        public int MULTITHREADING_CORES { get; private set; } = Environment.ProcessorCount;
        public int MULTITHREADING_CORES_BIGDATA { get; private set; } = 2;
        public bool MULTI_TARGET_IN_PARALLEL { get; private set; } = true;
        
        private readonly object _lock = new();
        private List<string> _reservedSqlColumns = new();

        public List<string> RESERVED_SQL_COLUMNS
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_reservedSqlColumns); // Retourne une copie pour éviter les conflits
                }
            }
        }

        public void AddReservedColumn(string column)
        {
            lock (_lock)
            {
                _reservedSqlColumns.Add(column);
            }
        }

        public void SetReservedColumns(List<string> newColumns)
        {
            lock (_lock)
            {
                _reservedSqlColumns = new List<string>(newColumns); // Remplacement sécurisé
            }
        }

        public string SQL_SYNCHRO_STORE_COLUMN = "RECORD_ID";
        public string SQL_SYNCHRO_TAG_COLUMN = "RECORD_TAG";

        public int SHELL_OPERATIONS_Fuzible_TIMEOUT { get; private set; } = 120;
        public int SHELL_OPERATIONS_EXT_TIMEOUT { get; private set; } = 480;
        public bool ENABLE_QUERY_ASSISTANT { get; private set; } = true;
        public bool JSON_PARSER_REPLACE_SPECIAL_CHARS { get; private set; } = true;
        public string MONGODB_INDEXNAME { get; private set; } = "SHSFUZIBLE_UNIQUE_";
        public CultureInfo Culture
        {
            get
            {
                return new CultureInfo(APP_LANGUAGE.ToLower() + "-" + APP_LANGUAGE.ToUpper());
            }
        }

        public int QASSISTANT_MAXFILESIZEANALYZE { get; private set; } = 50485760;
        public int QASSISTANT_MAXFILEROWSANALYZE { get; private set; } = 5000;
        public bool CSV_FORCE_INTEGRATION_WRONG_LENGTH { get; private set; } = true;
        public bool WS_SOQL_GETRECORDSONLY { get; private set; } = true;
        public int SQL_DIRECTSTREAM_COMMIT { get; private set; } = 10000;
        private string USER
        {
            get;
        }
        public bool SECURITY_SHARED_USERS { get; private set; } = false;
        public bool LOG_DEBUG_FUNCTIONS { get; private set; } = false;
        public string SECURITY_PRINCIPAL_USER { get; private set; } = "";
        public string USER_PASSWORD { get; private set; }
        public bool SHOW_LIVE_HELP_START { get; private set; } = true;

        #endregion

        #region "PUBLIC VOID"

        internal MAINParameters(string sUser, bool bDynamicCheckLicense, bool bReadOnly)
        {
            USER = sUser;

            LoadMainUserParameters(sUser, bReadOnly);
        }

        public MAINParameters(string sUsername)
        {
            //utilisé uniquement pour l'enregistrement du programme donc aucun contrôle de cohérence de la clé
            USER = sUsername;
            //LoadRegKey(false, sUsername);
            LoadMainUserParameters(USER, true);
        }

        public bool SaveSessionPassword(string sUser, string sPwd)
        {
            USER_PASSWORD = sPwd;
            try
            {
                SaveMainUserParameters(sUser, false);
                return true;
            }
           catch { return false; }
        }

        public bool SetLiveHelpAtStart(bool bYes)
        {
            SHOW_LIVE_HELP_START = bYes;
            try
            {
                SaveMainUserParameters(USER, false);
                return true;
            }
            catch { return false; }
        }

        public void SetFromConfig(List<object> sParameters)
        {
            APP_ADMINS = (List<string>)sParameters[0];
            MULTITHREADING_CORES = (int)sParameters[1];
            LOG_BDDDRIVER = (SQLTools_Enums.BDD)sParameters[2];
            LOG_CONNECTIONSTRINGSCHEMA = (string)sParameters[51];
            LOG_CONNECTIONSTRING = (string)sParameters[3];

            MAIL_CONNECTIONSTRING = (string)sParameters[4];
            LOG_DAYSTOKEEP = (int)sParameters[5];
            ABORT_JOB_ON_ERRORS = (int)sParameters[6];
            LOG_MAIL_DONTSEND_IFSUCCESS = (bool)sParameters[7];
            ALLOW_INTEGERS_WITH_SPECIALS = (bool)sParameters[8];
            ALLOW_INTEGERS_STARTING_WITH_0 = (bool)sParameters[9];
            MAX_SQL_ERRORS = (int)sParameters[10];
            MAX_SQL_QUERY_DEBUG_MODE = (int)sParameters[11];
            SQL_COMMAND_TIMEOUT = (int)sParameters[12];
            SQL_COMMIT = (int)sParameters[13];
            SQL_DIRECTSTREAM_COMMIT = (int)sParameters[14];
            SQL_DECIMALS = (int)sParameters[15];
            SQL_SYNCHRO_MODE_TABLE = (string)sParameters[16];
            FILE_EXPORT_DIR = (string)sParameters[17];
            FILE_PROCESSED_DIR = (string)sParameters[18];
            FILE_ADD_DATETIME_PREFIX = (bool)sParameters[19];
            FILE_MAX_ROWS_ANALYZER = (int)sParameters[20];
            CSV_FORCE_INTEGRATION_WRONG_LENGTH = (bool)sParameters[21];
            CSV_MAXLINES_BEFORE_SPLIT = (int)sParameters[22];
            CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT = (bool)sParameters[23];
            CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET = (decimal)sParameters[24];
            CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET = (decimal)sParameters[25];
            CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS = (int)sParameters[26];
            FILE_DAYS_TO_KEEP_PROCESSED = (int)sParameters[27];
            CSV_SEPARATORS = (string)sParameters[28];
            MAIL_MAXPJ_SIZE = (int)sParameters[29];
            MAIL_TIMEOUT = (int)sParameters[30];
            MAIL_MAXCHAR_BEFORE_PJ = (int)sParameters[31];
            WS_TIMEOUT = (int)sParameters[32];
            WS_ALLOW_RESPONSES_ALTER_COLUMNS = (bool)sParameters[33];
            WS_SOQL_GETRECORDSONLY = (bool)sParameters[34];
            WS_DEFAULT_ENCODING = (string)sParameters[35];
            WS_NUXEO_DEFAULT_ENCODING = (string)sParameters[36];
            MULTI_TARGET_IN_PARALLEL = (bool)sParameters[37];
            SQL_SHRINK_TABLES = (bool)sParameters[38];
            SHOW_SYSTEM_ALERTS = (bool)sParameters[39];

            JSON_PARSER_REPLACE_SPECIAL_CHARS = (bool)sParameters[40];
            ENABLE_QUERY_ASSISTANT = (bool)sParameters[41];
            QASSISTANT_MAXFILESIZEANALYZE = (int)sParameters[42];
            SHELL_OPERATIONS_Fuzible_TIMEOUT = (int)sParameters[43];
            SHELL_OPERATIONS_EXT_TIMEOUT = (int)sParameters[44];

            SECURITY_SHARED_USERS = (bool)sParameters[45];
            SECURITY_PRINCIPAL_USER = (string)sParameters[46];

            MULTITHREADING_CORES_BIGDATA = (int)sParameters[47];

            LOG_DEBUG_FUNCTIONS = (bool)sParameters[48];

            SQL_BULK_INSERT_BATCH_SIZE = (int)sParameters[49];

            JSON_ALTERNATIVE_PROCESSING_MODE = (bool)sParameters[50];

            SaveMainUserParameters(USER, false);
        }

        public void SetMultiCore(int iThreads)
        {
            MULTITHREADING_CORES = iThreads;
        }

        public void SetAnalyzerParams(int iFileMaxRowsAnalyzer, int iCSVMaxlinesBeforeSplit)
        {
            FILE_MAX_ROWS_ANALYZER = iFileMaxRowsAnalyzer;
            CSV_MAXLINES_BEFORE_SPLIT = iCSVMaxlinesBeforeSplit;
        }

        public void ChangeLanguage(string sLang, string sUser)
        {
            //EN, FR, ...
            try
            {
                //contrôle de cohérence entre le type de license demandé et la quantité d'utilisateurs enregistrés en BDD

                StringBuilder sbQuery = new();

                sbQuery.Append("UPDATE user_parameters SET \"language\" = ");
                sbQuery.Append("'" + sLang + "'");
                sbQuery.Append(" WHERE \"user\" = '" + sUser + "';");

                using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
            catch (Exception)
            {
                throw;
            }
        }

        internal void SaveMainUserParameters(string sUser, bool bCreate)
        {
            try
            {
                StringBuilder sbQuery = new();

                if (bCreate)
                {
                    sbQuery.Append("INSERT INTO user_parameters (\"user\",\"mail_admin\",\"show_system_alerts\",\"multithreading_cores\",\"multi_target_in_parallel\",\"abort_job_on_errors\",\"connexion_string_sqllog\",\"connexion_string_sqllog_schema\",\"connexion_string_mail\",\"keep_log_until\",\"type_driver_sqllog\",\"log_mail_dontsend_ifsuccess\",\"allow_special_integers\",\"language\",\"sql_max_errors\",\"sql_max_char_queries_debug_mode\",\"sql_bulk_rate\",\"sql_commit\",\"sql_decimals\",\"sql_command_timeout\",\"sql_synchro_mode_table\",\"sql_shrink_tables\",\"sql_mysql_default_port\",\"sql_postgres_default_port\",\"sql_oracle_default_port\",\"keep_log_until\",\"file_processed_dir\",\"file_moved_prefix\",\"file_export_dir\",\"file_max_rows_analyzer\",\"csv_rows_offset_before_depth_analysis\",\"file_days_to_keep_processed\",\"csv_separators\",\"csv_source_splitted_rowcount\",\"csv_source_splitted_rowcount_check_distant\",\"csv_header_detection_resemblance_offset\",\"csv_header_detection_avg_unique_offset\",\"mail_connection_timeout\",\"mail_max_pj_size\",\"mail_max_strsize_before_pj\",\"ws_connection_timeout\",\"ws_allow_responses_alter_columns\", \"ws_soql_records_only\", \"ws_default_encoding\",\"ws_nuxeo_default_encoding\",\"enable_query_assistant\",\"json_parser_replace_special_chars\",\"shell_operations_timeout\",\"shell_operations_ext_timeout\", \"allow_integers_starting_with_0\", \"application_server\", \"registration_send_mode\", \"query_assistant_maxfilesize\", \"force_csv_invalidrow_integration\", \"sql_directstream_commit\", \"security_shared_users\", \"security_principal_user\", \"multithreading_cores_bigdata\", \"log_debug_functions\", \"user_password\", \"json_alternative_processing_mode\", \"show_live_help_start\") VALUES ('" + sUser + "',");
                }
                else
                {
                    sbQuery.Append("UPDATE user_parameters SET ");
                }
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mail_admin\"=", "'", FITools.EncryptionSystem.AES_Encrypt(string.Join(";", APP_ADMINS), SHSConstantes.PROGRAM_PWD + sUser.ToLower()), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"show_system_alerts\"=", "'", SHOW_SYSTEM_ALERTS ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"multithreading_cores\"=", "'", MULTITHREADING_CORES > Environment.ProcessorCount ? Environment.ProcessorCount.ToString() : MULTITHREADING_CORES.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"multi_target_in_parallel\"=", "'", MULTI_TARGET_IN_PARALLEL ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"abort_job_on_errors\"=", "'", ABORT_JOB_ON_ERRORS.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"connexion_string_sqllog\"=", "'", FITools.EncryptionSystem.AES_Encrypt(LOG_CONNECTIONSTRING, SHSConstantes.PROGRAM_PWD), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"connexion_string_sqllog_schema\"=", "'", FITools.EncryptionSystem.AES_Encrypt(LOG_CONNECTIONSTRINGSCHEMA, SHSConstantes.PROGRAM_PWD), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"connexion_string_mail\"=", "'", FITools.EncryptionSystem.AES_Encrypt(MAIL_CONNECTIONSTRING, SHSConstantes.PROGRAM_PWD), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"keep_log_until\"=", "'", LOG_DAYSTOKEEP.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"type_driver_sqllog\"=", "'", LOG_BDDDRIVER.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"log_mail_dontsend_ifsuccess\"=", "'", LOG_MAIL_DONTSEND_IFSUCCESS ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"allow_special_integers\"=", "'", ALLOW_INTEGERS_WITH_SPECIALS ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"language\"=", "'", APP_LANGUAGE.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_max_errors\"=", "'", MAX_SQL_ERRORS.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_max_char_queries_debug_mode\"=", "'", MAX_SQL_QUERY_DEBUG_MODE.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_bulk_rate\"=", "'", SQL_BULK_INSERT_BATCH_SIZE.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_commit\"=", "'", SQL_COMMIT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_decimals\"=", "'", SQL_DECIMALS.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_command_timeout\"=", "'", SQL_COMMAND_TIMEOUT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_synchro_mode_table\"=", "'", Toolbox.RemoveSpecialCharacters(SQL_SYNCHRO_MODE_TABLE, "_", false), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_shrink_tables\"=", "'", SQL_SHRINK_TABLES ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_mysql_default_port\"=", "'", DEFAULT_MYSQL_PORT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_postgres_default_port\"=", "'", DEFAULT_POSTGRES_PORT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_oracle_default_port\"=", "'", DEFAULT_ORACLE_PORT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"keep_log_until\"=", "'", LOG_DAYSTOKEEP.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_processed_dir\"=", "'", Toolbox.RemoveSpecialCharacters(FILE_PROCESSED_DIR, "_", false), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_moved_prefix\"=", "'", FILE_ADD_DATETIME_PREFIX ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_export_dir\"=", "'", Toolbox.RemoveSpecialCharacters(FILE_EXPORT_DIR, "_", false), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_max_rows_analyzer\"=", "'", FILE_MAX_ROWS_ANALYZER.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_rows_offset_before_depth_analysis\"=", "'", CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"file_days_to_keep_processed\"=", "'", FILE_DAYS_TO_KEEP_PROCESSED.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_separators\"=", "'", CSV_SEPARATORS.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\b", "\\b").Replace("\v", "\\v").Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_source_splitted_rowcount\"=", "'", CSV_MAXLINES_BEFORE_SPLIT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_source_splitted_rowcount_check_distant\"=", "'", CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_header_detection_resemblance_offset\"=", "'", CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET.ToString().Replace(",", "."), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"csv_header_detection_avg_unique_offset\"=", "'", CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET.ToString().Replace(",", "."), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mail_connection_timeout\"=", "'", MAIL_TIMEOUT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mail_max_pj_size\"=", "'", MAIL_MAXPJ_SIZE.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"mail_max_strsize_before_pj\"=", "'", MAIL_MAXCHAR_BEFORE_PJ.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_connection_timeout\"=", "'", WS_TIMEOUT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_allow_responses_alter_columns\"=", "'", WS_ALLOW_RESPONSES_ALTER_COLUMNS ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_soql_records_only\"=", "'", WS_SOQL_GETRECORDSONLY ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_default_encoding\"=", "'", WS_DEFAULT_ENCODING.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"ws_nuxeo_default_encoding\"=", "'", WS_NUXEO_DEFAULT_ENCODING.Replace("'", "''"), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"enable_query_assistant\"=", "'", ENABLE_QUERY_ASSISTANT ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"json_parser_replace_special_chars\"=", "'", JSON_PARSER_REPLACE_SPECIAL_CHARS ? "1" : "0", "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"shell_operations_timeout\"=", "'", SHELL_OPERATIONS_Fuzible_TIMEOUT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"shell_operations_ext_timeout\"=", "'", SHELL_OPERATIONS_EXT_TIMEOUT.ToString(), "',"));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"allow_integers_starting_with_0\"=", "'", ALLOW_INTEGERS_STARTING_WITH_0 ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"application_server\"=", "'", FUZIBLE_SERVER, "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"registration_send_mode\"=", "'0', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"query_assistant_maxfilesize\"=", "'", QASSISTANT_MAXFILESIZEANALYZE.ToString(), "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"force_csv_invalidrow_integration\"=", "'", CSV_FORCE_INTEGRATION_WRONG_LENGTH ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"sql_directstream_commit\"=", "'", SQL_DIRECTSTREAM_COMMIT.ToString(), "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"security_shared_users\"=", "'", SECURITY_SHARED_USERS ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"security_principal_user\"=", "'", SECURITY_SHARED_USERS && SECURITY_PRINCIPAL_USER.Length > 0 ? FITools.EncryptionSystem.AES_Encrypt(SECURITY_PRINCIPAL_USER.ToString(), sUser) : "", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"multithreading_cores_bigdata\"=", "'", MULTITHREADING_CORES_BIGDATA.ToString(), "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"log_debug_functions\"=", "'", LOG_DEBUG_FUNCTIONS ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"user_password\"=", USER_PASSWORD != null && USER_PASSWORD.Length > 0 ? ("'" + FITools.EncryptionSystem.AES_Encrypt(USER_PASSWORD.ToString(), sUser) + "', ") : "NULL, "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"json_alternative_processing_mode\"=", "'", JSON_ALTERNATIVE_PROCESSING_MODE ? "1" : "0", "', "));
                sbQuery.Append(string.Concat(bCreate ? "" : "\"show_live_help_start\"=", "'", SHOW_LIVE_HELP_START ? "1" : "0", "'"));
  
                if (bCreate) { sbQuery.Append(");"); }
                else { sbQuery.Append(" WHERE \"user\" = '" + sUser + "';"); }

                using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
            catch (Exception)
            {
                throw;
            }
        }

        internal void LoadMainUserParameters(string sUser, bool bReadOnly)
        {
            DataSet dsData = new();
            try
            {
                string sQuery = string.Concat("SELECT * FROM \"user_parameters\" WHERE \"user\" = '", sUser, "'");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Main Parameters");
                }
                if (dsData != null && dsData.Tables.Count > 0)
                {
                    if (dsData.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow dr in dsData.Tables[0].Rows)
                        {
                            string sAdminMail = dr["mail_admin"].ToString();
                            if (sAdminMail.Length > 1)
                            {
                                sAdminMail = FITools.EncryptionSystem.AES_Decrypt(sAdminMail, SHSConstantes.PROGRAM_PWD + sUser.ToLower());
                                APP_ADMINS = sAdminMail.Split(Convert.ToChar(";")).ToList();
                            }
                            SHOW_SYSTEM_ALERTS = Convert.ToBoolean(dr["show_system_alerts"]);
                            int iCoresUser = Convert.ToInt32(dr["multithreading_cores"].ToString());
                            if (iCoresUser > Environment.ProcessorCount) { iCoresUser = Environment.ProcessorCount; } //en cas de modif de la machine
                            MULTITHREADING_CORES = iCoresUser;
                            MULTITHREADING_CORES_BIGDATA = Convert.ToInt32(dr["multithreading_cores_bigdata"].ToString());
                            MULTI_TARGET_IN_PARALLEL = Convert.ToBoolean(dr["multi_target_in_parallel"]);
                            ABORT_JOB_ON_ERRORS = Convert.ToInt32(dr["abort_job_on_errors"].ToString());
                            LOG_DAYSTOKEEP = Convert.ToInt32(dr["keep_log_until"].ToString());
                            LOG_CONNECTIONSTRING = FITools.EncryptionSystem.AES_Decrypt(dr["connexion_string_sqllog"].ToString(), SHSConstantes.PROGRAM_PWD);
                            LOG_CONNECTIONSTRINGSCHEMA = FITools.EncryptionSystem.AES_Decrypt(dr["connexion_string_sqllog_schema"].ToString(), SHSConstantes.PROGRAM_PWD);
                            MAIL_CONNECTIONSTRING = FITools.EncryptionSystem.AES_Decrypt(dr["connexion_string_mail"].ToString(), SHSConstantes.PROGRAM_PWD);
                            LOG_BDDDRIVER = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), dr["type_driver_sqllog"].ToString());
                            LOG_MAIL_DONTSEND_IFSUCCESS = Convert.ToBoolean(dr["log_mail_dontsend_ifsuccess"]);
                            ALLOW_INTEGERS_WITH_SPECIALS = Convert.ToBoolean(dr["allow_special_integers"]);
                            APP_LANGUAGE = dr["language"].ToString().ToUpper();
                            MAX_SQL_ERRORS = Convert.ToInt32(dr["sql_max_errors"].ToString());
                            MAX_SQL_QUERY_DEBUG_MODE = Convert.ToInt32(dr["sql_max_char_queries_debug_mode"].ToString());
                            SQL_COMMIT = Convert.ToInt32(dr["sql_commit"].ToString());
                            SQL_BULK_INSERT_BATCH_SIZE = Convert.ToInt32(dr["sql_bulk_rate"].ToString());
                            SQL_DIRECTSTREAM_COMMIT = Convert.ToInt32(dr["sql_directstream_commit"].ToString());
                            SQL_DECIMALS = Convert.ToInt32(dr["sql_decimals"].ToString());
                            SQL_COMMAND_TIMEOUT = Convert.ToInt32(dr["sql_command_timeout"].ToString());
                            SQL_SYNCHRO_MODE_TABLE = dr["sql_synchro_mode_table"].ToString();
                            SQL_SHRINK_TABLES = Convert.ToBoolean(dr["sql_shrink_tables"]);
                            DEFAULT_MYSQL_PORT = Convert.ToInt32(dr["sql_mysql_default_port"].ToString());
                            DEFAULT_POSTGRES_PORT = Convert.ToInt32(dr["sql_postgres_default_port"].ToString());
                            DEFAULT_ORACLE_PORT = Convert.ToInt32(dr["sql_oracle_default_port"].ToString());
                            LOG_DAYSTOKEEP = Convert.ToInt32(dr["keep_log_until"].ToString());
                            FILE_PROCESSED_DIR = dr["file_processed_dir"].ToString();
                            FILE_ADD_DATETIME_PREFIX = Convert.ToBoolean(dr["file_moved_prefix"]);
                            FILE_EXPORT_DIR = dr["file_export_dir"].ToString();
                            FILE_MAX_ROWS_ANALYZER = Convert.ToInt32(dr["file_max_rows_analyzer"].ToString());
                            CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS = Convert.ToInt32(dr["csv_rows_offset_before_depth_analysis"].ToString());
                            FILE_DAYS_TO_KEEP_PROCESSED = Convert.ToInt32(dr["file_days_to_keep_processed"].ToString());
                            CSV_MAXLINES_BEFORE_SPLIT = Convert.ToInt32(dr["csv_source_splitted_rowcount"].ToString());
                            CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT = Convert.ToBoolean(dr["csv_source_splitted_rowcount_check_distant"]);
                            decimal.TryParse(dr["csv_header_detection_resemblance_offset"].ToString(), out decimal d);
                            CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET = d;
                            decimal.TryParse(dr["csv_header_detection_avg_unique_offset"].ToString(), out d);
                            CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET = d;
                            CSV_SEPARATORS = dr["csv_separators"].ToString();
                            CSV_FORCE_INTEGRATION_WRONG_LENGTH = Convert.ToBoolean(dr["force_csv_invalidrow_integration"]);
                            MAIL_TIMEOUT = Convert.ToInt32(dr["mail_connection_timeout"].ToString());
                            MAIL_MAXPJ_SIZE = Convert.ToInt32(dr["mail_max_pj_size"].ToString());
                            MAIL_MAXCHAR_BEFORE_PJ = Convert.ToInt32(dr["mail_max_strsize_before_pj"].ToString());
                            WS_TIMEOUT = Convert.ToInt32(dr["ws_connection_timeout"].ToString());
                            WS_ALLOW_RESPONSES_ALTER_COLUMNS = Convert.ToBoolean(dr["ws_allow_responses_alter_columns"]);
                            WS_SOQL_GETRECORDSONLY = Convert.ToBoolean(dr["ws_soql_records_only"]);
                            WS_DEFAULT_ENCODING = dr["ws_default_encoding"].ToString();
                            WS_NUXEO_DEFAULT_ENCODING = dr["ws_nuxeo_default_encoding"].ToString();
                            ENABLE_QUERY_ASSISTANT = Convert.ToBoolean(dr["enable_query_assistant"]);
                            QASSISTANT_MAXFILESIZEANALYZE = Convert.ToInt32(dr["query_assistant_maxfilesize"]);
                            JSON_PARSER_REPLACE_SPECIAL_CHARS = Convert.ToBoolean(dr["json_parser_replace_special_chars"]);
                            SHELL_OPERATIONS_Fuzible_TIMEOUT = Convert.ToInt32(dr["shell_operations_timeout"].ToString());
                            SHELL_OPERATIONS_EXT_TIMEOUT = Convert.ToInt32(dr["shell_operations_ext_timeout"].ToString());
                            ALLOW_INTEGERS_STARTING_WITH_0 = Convert.ToBoolean(dr["allow_integers_starting_with_0"]);
                            FUZIBLE_SERVER = "https://github.com/guizmox/Fuzible"; //removed as of april 26 : dr["application_server"].ToString();
                            //REGISTRATION_SEND_MODE = Convert.ToInt32(dr["registration_send_mode"].ToString());
                            SECURITY_SHARED_USERS = Convert.ToBoolean(dr["security_shared_users"]);
                            LOG_DEBUG_FUNCTIONS = Convert.ToBoolean(dr["log_debug_functions"]);
                            JSON_ALTERNATIVE_PROCESSING_MODE = Convert.ToBoolean(dr["json_alternative_processing_mode"]);
                            SHOW_LIVE_HELP_START = Convert.ToBoolean(dr["show_live_help_start"]);

                            string sUserTmp = dr["security_principal_user"].ToString();
                            if (sUserTmp.Length > 0 && SECURITY_SHARED_USERS)
                            {
                                SECURITY_PRINCIPAL_USER = FITools.EncryptionSystem.AES_Decrypt(sUserTmp, sUser);
                            }
                            USER_PASSWORD = dr["user_password"] == DBNull.Value ? null : FITools.EncryptionSystem.AES_Decrypt(dr["user_password"].ToString(), sUser);
                        }
                    }
                    else //si les valeurs n'existent pas, on crée
                    {
                        if (!bReadOnly) { SaveMainUserParameters(sUser, true); }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static void INIMigrationToSQLLite(string INTERNAL_DB, string sUser, string sPath)
        {
            string sDir = sPath;
            string sQuery = "";
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    switch (i)
                    {
                        case 0:
                            LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing User Config", SQLTools_Enums.LOG_TYPEINFO.DET, true);
                            sQuery = BuildQueryMainParameters(sDir, sUser);
                            break;
                        case 1:
                            LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing User Connections", SQLTools_Enums.LOG_TYPEINFO.DET, true);
                            sQuery = BuildQueryUserConnStrings(sDir, sUser);
                            break;
                        case 2:
                            LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing User Jobs", SQLTools_Enums.LOG_TYPEINFO.DET, true);
                            sQuery = BuildQueryJobParameters(sDir, sUser);
                            break;
                        case 3:
                            LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing User Jobs Queries", SQLTools_Enums.LOG_TYPEINFO.DET, true);
                            sQuery = BuildQueryJobQueries(sDir, sUser);
                            break;
                    }
                    using SqliteConnection sqlConn = new(INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                using (StreamWriter sw = File.CreateText(sDir + "\\ERR_LOG.TXT"))
                {
                    sw.Write("ERROR : " + ex.Message + Environment.NewLine + sQuery.Trim());
                }
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null,ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG, true);

                Console.ReadKey();
            }
            LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processed", SQLTools_Enums.LOG_TYPEINFO.DET, true);
            Console.ReadKey();
        }

        private static string BuildQueryJobQueries(string sDir, string sUser)
        {
            string[] sMainFiles = Directory.GetFiles(sDir + "\\INI\\", "QUERIES_JOBS_" + sUser + ".*");

            StringBuilder sbQuery = new();

            foreach (string sFile in sMainFiles)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing " + Path.GetFileName(sFile), SQLTools_Enums.LOG_TYPEINFO.DET, true);

                string[] sContent = File.ReadAllLines(sFile);

                List<string> sListQueries = new();

                int iR = 0;
                string sJobID = "";
                foreach (string sC in sContent)
                {
                    iR++;
                    if (Regex.IsMatch(sC, "^\\[[\\d\\-]+\\]$") || sC.Length == 0 || iR == sContent.Length)
                    {
                        if (sListQueries.Count > 0)
                        {
                            for (int i = 0; i < sListQueries.Count; i++)
                            {
                                string sEncryptedQuery = sListQueries[i];
                                try { sEncryptedQuery = FITools.EncryptionSystem.AES_Encrypt(sEncryptedQuery, SHSConstantes.PROGRAM_PWD); }
                                catch { }
                                sbQuery.AppendLine();
                                sbQuery.Append("INSERT INTO job_queries (\"user\",\"job_id\",\"query\") VALUES (");
                                sbQuery.Append("'" + sUser + "','" + sJobID + "','" + sListQueries[i] + "'");
                                sbQuery.Append(");");
                            }
                            sListQueries.Clear();
                        }
                        sJobID = sC;
                    } //job ID                  
                    else
                    {
                        //recherche pattern table
                        bool bOK = false;
                        Match mcQ = Regex.Match(sC, ".+:\\s*SELECT.*", RegexOptions.IgnoreCase);
                        if (mcQ.Success && mcQ.Index == 0)
                        {
                            bOK = true;
                        }
                        else
                        {
                            mcQ = Regex.Match(sC, ".+:\\s*$", RegexOptions.IgnoreCase);
                            if (mcQ.Success && mcQ.Index == 0)
                            {
                                bOK = true;
                            }
                        }
                        if (bOK)
                        {
                            sListQueries.Add(sC); //.Replace("'", "''")
                        }
                        else { sListQueries[^1] = string.Concat(sListQueries[^1], Environment.NewLine, sC); } //.Replace("'", "''")
                    }
                }
            }
            return sbQuery.ToString();
        }

        private static string BuildQueryUserConnStrings(string sDir, string sUser)
        {
            string[] sMainFiles = Directory.GetFiles(sDir + "\\INI\\", "CONNSTRINGS_" + sUser + ".*");

            StringBuilder sbQueryA = new();
            StringBuilder sbQueryB = new();

            foreach (string sFile in sMainFiles)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing " + Path.GetFileName(sFile), SQLTools_Enums.LOG_TYPEINFO.DET, true);

                string[] sContent = File.ReadAllLines(sFile);
                List<string> sListParamA = new();
                List<string> sListValuesParamA = new();
                List<string> sListParamB = new();
                List<string> sListValuesParamB = new();
                string sConnID = "";

                int iR = 0;
                foreach (string sC in sContent)
                {
                    iR++;
                    if (Regex.IsMatch(sC, "^\\[[\\d\\-]+\\]$") || sC.Length == 0 || iR == sContent.Length)
                    {
                        if (iR > 0)
                        {
                            if (sListValuesParamA.Count > 0)
                            {
                                sbQueryA.AppendLine();
                                sbQueryA.Append("INSERT INTO \"user_connstrings\" (\"user\",\"connstring_id\",\"connstring_name\",\"connstring\",\"connstring_driver\") VALUES ('" + sUser + "','" + sConnID + "',");

                                for (int i = 0; i < sListValuesParamA.Count; i++)
                                {
                                    if (i > 0) { sbQueryA.Append(','); }
                                    sbQueryA.Append("'" + sListValuesParamA[i] + "'");
                                }
                                for (int i = 0; i < sListValuesParamB.Count; i++)
                                {
                                    sbQueryB.AppendLine();
                                    sbQueryB.Append("INSERT INTO \"user_connstrings_params\" (\"user\",\"connstring_id\",\"param_name\",\"param_value\") VALUES ('" + sUser + "','" + sConnID + "',");
                                    sbQueryB.Append("'" + sListParamB[i] + "','" + sListValuesParamB[i] + "'");
                                    sbQueryB.Append(");");
                                }
                                sbQueryA.Append(");");

                                sListValuesParamA.Clear();
                                sListValuesParamB.Clear();
                                sListParamA.Clear();
                                sListParamB.Clear();
                            }
                        }

                        sConnID = sC;

                    }
                    else
                    {
                        if (sC.StartsWith("P_"))
                        {
                            sListParamB.Add(sC.Split(Convert.ToChar("="))[0]);
                            sListValuesParamB.Add(sC[(sC.IndexOf("=") + 1)..].Replace("'", "''"));
                        }
                        else
                        {
                            sListParamA.Add(sC.Split(Convert.ToChar("="))[0].ToLower());
                            sListValuesParamA.Add(sC[(sC.IndexOf("=") + 1)..].Replace("'", "''"));
                        }
                    }
                }
            }

            sbQueryA.AppendLine(sbQueryB.ToString());

            return sbQueryA.ToString();
        }

        private static string BuildQueryJobParameters(string sDir, string sUser)
        {
            string[] sMainFiles = Directory.GetFiles(sDir + "\\INI\\", "JOBS_" + sUser + ".*");

            StringBuilder sbQuery = new();

            foreach (string sFile in sMainFiles)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing " + Path.GetFileName(sFile), SQLTools_Enums.LOG_TYPEINFO.DET, true);

                string[] sContent = File.ReadAllLines(sFile);
                List<string> sListParam = new();
                List<string> sListValuesParam = new();

                int iR = 0;
                string sJobID = "";
                foreach (string sC in sContent)
                {
                    iR++;

                    if (Regex.IsMatch(sC, "^\\[[\\d\\-]+\\]$") || sC.Length == 0 || iR == sContent.Length)
                    {
                        if (iR > 0)
                        {
                            if (sListParam.Count > 0)
                            {
                                sbQuery.AppendLine();
                                sbQuery.Append("INSERT INTO job_parameters (\"user\",\"job_id\",");
                                for (int i = 0; i < sListParam.Count; i++)
                                {
                                    if (i > 0) { sbQuery.Append(','); }
                                    sbQuery.Append("\"" + sListParam[i] + "\"");
                                }
                                sbQuery.Append(") VALUES (");
                                sbQuery.Append("'" + sUser + "','" + sJobID + "',");
                                for (int i = 0; i < sListValuesParam.Count; i++)
                                {
                                    if (i > 0) { sbQuery.Append(','); }
                                    sbQuery.Append(sListValuesParam[i].Length == 0 ? "null" : ("'" + sListValuesParam[i].Trim().Replace("\\r\\n", " ") + "'"));
                                }
                                sbQuery.Append(");");
                                sListParam.Clear();
                                sListValuesParam.Clear();
                            }
                        }
                        sJobID = sC;
                    } //job ID                  
                    else
                    {
                        sListParam.Add(sC.Split(Convert.ToChar("="))[0].ToLower());
                        sListValuesParam.Add(sC[(sC.IndexOf("=") + 1)..].Replace("'", "''"));
                    }
                }
            }
            return sbQuery.ToString();
        }

        private static string BuildQueryMainParameters(string sDir, string sUser)
        {
            string[] sMainFiles = Directory.GetFiles(sDir + "\\INI\\", "CONFIG_" + sUser + ".*");

            StringBuilder sbQuery = new();

            foreach (string sFile in sMainFiles)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, "Processing " + Path.GetFileName(sFile), SQLTools_Enums.LOG_TYPEINFO.DET, true);

                string[] sContent = File.ReadAllLines(sFile);
                sbQuery.AppendLine();
                sbQuery.Append("INSERT INTO user_parameters (\"user\",");
                int iR = 0;
                foreach (string sC in sContent)
                {
                    iR++;

                    if (sC.Length > 0) //fin de fichier
                    {
                        if (iR > 1) { sbQuery.Append(','); }
                        sbQuery.Append("\"" + sC.Split(Convert.ToChar("="))[0].ToLower() + "\"");
                    }
                }
                sbQuery.Append(") VALUES ('" + sUser + "',");
                iR = 0;
                foreach (string sC in sContent)
                {
                    iR++;
                    if (sC.Length > 0)
                    {
                        if (iR > 1) { sbQuery.Append(','); }
                        sbQuery.Append(sC[(sC.IndexOf("=") + 1)..].Length == 0 ? "null" : ("'" + sC[(sC.IndexOf("=") + 1)..].Replace("'", "''").Trim().Replace("\\r\\n", " ") + "'"));
                    }
                }
                sbQuery.Append(");");
            }
            return sbQuery.ToString();
        }

        internal MAINParameters DeepCopy()
        {
            return (MAINParameters)this.MemberwiseClone();
        }

        internal void ConfigureForBenchmarking(int iQteRows)
        {
            CSV_MAXLINES_BEFORE_SPLIT = iQteRows + 1;
            MULTITHREADING_CORES_BIGDATA = 1;
        }

        #endregion

        #region "PRIVATE VOID"

        #endregion

    }

}
