namespace FuzibleFramework
{
    public static class SQLTools_Enums
    {
        public enum API_OPTIONS
        {
            NOTHING = 1,
            GRAPH_DOWNLOAD_FILE_DRIVE = 2,
            LOCKED_BY_TEMPLATE = 3
        }

        public enum AD_USERACCOUNTCONTROL
        {
            SCRIPT = 1,
            ACCOUNTDISABLE = 2,
            HOMEDIR_REQUIRED = 8,
            LOCKOUT = 16,
            PASSWD_NOTREQD = 32,
            PASSWD_CANT_CHANGE = 64,
            ENCRYPTED_TEXT_PWD_ALLOWED = 128,
            TEMP_DUPLICATE_ACCOUNT = 256,
            NORMAL_ACCOUNT = 512,
            DISABLED_ACCOUNT = 514,
            ENABLED_PASSWORD_NOT_REQUIRED = 544,
            DISABLED_PASSWORD_NOT_REQUIRED = 546,
            INTERDOMAIN_TRUST_ACCOUNT = 2048,
            WORKSTATION_TRUST_ACCOUNT = 4096,
            SERVER_TRUST_ACCOUNT = 8192,
            DONT_EXPIRE_PASSWORD = 65536,
            ENABLED_PASSWORD_DOESN_T_EXPIRE = 66048,
            DISABLED_PASSWORD_DOESN_T_EXPIRE = 66050,
            ENABLED_PASSWORD_DOESN_T_EXPIRE_AND_NOT_REQUIRED = 66080,
            DISABLED_PASSWORD_DOESN_T_EXPIRE_AND_NOT_REQUIRED = 66082,
            MNS_LOGON_ACCOUNT = 131072,
            SMARTCARD_REQUIRED = 262144,
            ENABLED_SMARTCARD_REQUIRED = 262656,
            DISABLED_SMARTCARD_REQUIRED = 262658,
            DISABLED_SMARTCARD_REQUIRED_PASSWORD_NOT_REQUIRED = 262690,
            DISABLED_SMARTCARD_REQUIRED_PASSWORD_DOESN_T_EXPIRE = 328194,
            DISABLED_SMARTCARD_REQUIRED_PASSWORD_DOESN_T_EXPIRE_AND_NOT_REQUIRED = 328226,
            TRUSTED_FOR_DELEGATION = 524288,
            DOMAIN_CONTROLLER = 532480,
            NOT_DELEGATED = 1048576,
            USE_DES_KEY_ONLY = 2097152,
            DONT_REQ_PREAUTH = 4194304,
            PASSWORD_EXPIRED = 8388608,
            TRUSTED_TO_AUTH_FOR_DELEGATION = 16777216,
            PARTIAL_SECRETS_ACCOUNT = 67108864
        }

        public enum MAIL_TARGET_FORMAT
        {
            HTML_TABLE = 1,
            ATT_EXCEL_H = 2,
            ATT_EXCEL = 3,
            ATT_CSV_H = 4,
            ATT_CSV = 5
        }

        public enum QUERY_PROPERTIES
        {
            CSV_SEPARATOR = 0,
            CSV_HASHEADER = 1,
            SYNCHRO_PRIMARY_KEY = 2,
            GUESSED_PRIMARY_KEY = 3,
            DATA_TRANSFORMATION = 4,
            DATA_TRANSFORMATION_ERROR = 5,
            CROSSJOIN_LINK = 6,
            FILE_LOCAL_PROCESSED = 7,
            FILE_FTP_PROCESSED = 8,
            WS_SOURCE_POSTWORK = 9,
            ROWS_RETRIEVED = 10,
            ROWS_INSERTED = 11,
            ROWS_DELETED = 12,
            ROWS_UPDATED = 13,
            TARGET_BEHAVIOR_PROCESSED = 14,
            QUERY_RETRY = 15
        }

        public enum MAIL_AUTHENTIFICATION_PROTOCOL
        {
            NONE = 1,
            SSL2 = 2,
            SSL3 = 3,
            TLS = 4,
            TLS11 = 5,
            TLS12 = 6,
            TLS13 = 7
        }
        public enum WEBSERVICE_AUTHORIZATION
        {
            NO_AUTH = 0,
            API_AUTH_HEADER = 1,
            API_AUTH_PARAM = 2,
            BEARER = 3,
            BASIC_AUTH = 4,
            OAUTH2 = 5,
            HTTP = 6,
            SALESFORCE_OAUTH,
            OAUTH2DELEGATED
        }

        public enum DRIVER_PARAMS
        {
            DEFAULT_SCHEMA,
            CREATE_PRIMARY_KEY,
            CREATE_TABLE,
            CHANGE_COLUMN_ALLOW_NULL,
            CHANGE_COLUMN_DISALLOW_NULL,
            CREATE_COLUMN,
            CHANGE_COLUMN_TYPE,
            CHANGE_COLUMN_DEFAULT_VALUE,
            CHANGE_COLUMN_ADD_UNIQUE,
            SHRINK_TABLE,
            GET_COLUMNS_LIST,
            GET_COLUMN,
            ENABLE_TABLE_CONSTRAINTS,
            DISABLE_TABLE_CONSTRAINTS,
            GET_TABLES,
            GET_TABLES_FILTERED,
            GET_VIEWS,
            GET_VIEWS_FILTERED,
            GET_PRIMARY_KEY,
            GET_FOREIGN_KEYS,
            CHECK_TABLE_EXISTENCE,
            DELETE_LOG_EVENTS,
            UPDATE_LOG_EVENT,
            INSERT_LOG_EVENT,
            CREATE_TABLE_LOG_ENT,
            CREATE_TABLE_LOG_LIG,
            ESCAPE_CHAR,
            STRING_FORMAT,
            DATE_FORMAT,
            WS_HEADERS,
            WS_AUTH_METHOD,
            WS_AUTH_KEY,
            WS_AUTH_VALUE,
            WS_AUTH_URL,
            WS_QUERY_PARAMS,
            WS_PROXY_URL,
            WS_PROXY_PORT,
            WS_NUXEO_USER,
            WS_NUXEO_PASSWORD,
            FILE_COMEFROM,
            FILE_NETWORK_USERNAME,
            FILE_NETWORK_PASSWORD,
            FILE_PROXY_URL,
            FILE_PROXY_PORT,
            FILE_FTP_USERNAME,
            FILE_FTP_PASSWORD,
            FILE_FTP_PATH,
            FILE_FTP_SSL,
            FILE_SFTP_SSH_KEY_PATH,
            FILE_FTP_PORT,
            MAIL_USE_SSL,
            MAIL_PROXY_PORT,
            MAIL_PROXY_URL,
            MAIL_PROTOCOL,
            MAIL_AUTH_PROTOCOL,
            MONGODB_DOCUMENT,
            AD_SEARCH_GROUP,
            AD_SEARCH_USER,
            AD_SEARCH_USERS,
            AD_SEARCH_GROUPS,
            DRIVER_DATE_LOCALE,
            DRIVER_DECIMALS_LOCALE,
            FILE_FTP_PROXY_USERNAME,
            FILE_FTP_PROXY_PASSWORD,
            FILE_FTP_PROXY_TYPE,
            WS_PROXY_USERNAME,
            WS_PROXY_PASSWORD,
            WS_PROXY_TYPE,
            MAIL_PROXY_TYPE,
            MAIL_PROXY_USERNAME,
            MAIL_PROXY_PASSWORD,
            WS_AUTH_SF_USERTOKEN,
            WS_AUTH_SF_CONSUMERSECRET,
            WS_AUTH_SF_CONSUMERKEY,
            WS_TEMPLATE,
            CREATE_FROM_SELECT,
            AD_USERNAME,
            AD_PASSWORD,
            AD_AUTH,
            LIMITED_RESULTS,
            AD_OU,
            FILE_FTP_IGNORE_TLS_ERRORS
        }

        public enum MONGODB_DOCUMENT
        {
            STANDARDBSON = 1,
            FUZIBLEBSON = 2,
            STRINGBSON = 3
        }

        public enum CROSSJOIN_TYPES
        {
            UNKNOWN = 0,
            LEFT = 1,
            INNER = 2,
            RIGHT = 3,
            OUTER = 4
        }

        public enum REPSYNC_INSERT_METHOD
        {
            BY_QUERY = 1,
            ALL_IN_ONE = 2,
            MERGE = 3
        }

        public enum MAIL_GET_PROTOCOL
        {
            POP = 1,
            IMAP = 2
        }

        public enum JOB_STACK_STATUS
        {
            REQUESTED = 1,
            CANCELLED = 2,
            RUNNING = 3,
            FINISHED = 4,
            FINISHED_WITH_ERRORS = 5,
            KILLED = 6,
            FAILED = 7
        }

        public enum CLASS_PURPOSE
        {
            SRC = 1,
            TRG = 2,
            LOG = 3,
            PRG = 4
        }

        public enum JOB_PURPOSE
        {
            EXPORT_IMPORT = 3,
            STREAMING = 6
        }

        public enum WEBSERVICE_CONTENT : int
        {
            JSON = 1,
            XML = 2,
            HTTP_PARAMS = 3,
            TXT = 4,
            BINARY = 5
        }

        public enum WEBSERVICE_METHOD : int
        {
            POST = 1,
            GET = 2,
            PUT = 3,
            DELETE = 4,
            PATCH = 5
        }

        public enum WEBSERVICE_SQL : int
        {
            FUZIBLE_SQL = 1,
            SOQL = 2,
            NXQL = 3,
            GRAPHQL = 4,
            OQL = 5
        }

        public enum WEBSERVICE_TEMPLATE : int
        {
            DEFAULT = 1,
            GLPI = 2,
            SALESFORCE = 3,
            SALESFORCE_SOQL = 4,
            CEGID = 5,
            YOUTUBE_V3 = 6,
            NUXEO = 7,
            MICROSOFT_GRAPH = 8,
            FACEBOOK_GRAPH = 9,
            ITOP = 10,
            MICROSOFT_ONEDRIVE = 11
        }

        public enum WEBSERVICE_REQUEST_BODY_TYPE : int
        {
            RAW_JSON = 1,
            RAW_XML = 2,
            FORM_DATA = 3
        }

        public enum CHARACTER_SET : int
        {
            utf8 = 1,
            unicode = 2,
            utf7 = 3
        }

        public enum TYPE_DATA : int
        {
            UNKNOWN = 0,
            CHAR = 1,
            INT = 2,
            DECIMAL = 3,
            DATE = 4,
            BOOL = 5,
            FLOAT = 6,
            TEXT = 7,
            BIT = 8,
            DATETIME = 9,
            DOUBLE = 10,
            NUMERIC = 11,
            REAL = 12,
            UNSIGNED = 13,
            NUMBER = 14,
            SMALLINT = 15,
            INTEGER = 16,
            BIGINT = 17,
            TIMESTAMP = 18,
            BOOLEAN = 19,
            VARCHAR = 20,
            NVARCHAR = 21,
            MONEY = 22,
            BIN = 23,
            BITARRAY = 24,
            STRING = 25,
            CHARACTER_VARYING = 26,
            TIMESTAMP_WITHOUT_TIME_ZONE = 27,
            TIMESTAMP_WITH_TIME_ZONE = 28,
            SERIAL = 29,
            BIG_SERIAL = 30,
            DOUBLE_PRECISION = 31,
            JSON = 32,
            XML = 33,
            TIME = 34,
            TIME_WITH_TIME_ZONE = 35,
            TIME_WITHOUT_TIME_ZONE = 36,
            DATETIME2 = 37,
            VARCHAR2 = 38,
            BLOB = 39,
            TINYINT = 40,
            WCHAR = 41,
            VARWCHAR = 42,
            VARNUMERIC = 43,
            VARBINARY = 44,
            BINARY = 45,
            CURRENCY = 46,
            DBTIME = 47,
            DBDATE = 48,
            DBTIMESTAMP = 49,
            GUID = 50,
            LONGVARCHAR = 51,
            LONGVARBINARY = 52,
            LONGVARWCHAR = 53,
            INT16 = 54,
            INT32 = 55,
            INT64 = 56,
            NCHAR = 57,
            LONG = 58,
            RAW = 59,
            IMAGE = 60,
            SMALLDATETIME = 61,
            SINGLE = 62,
            OBJECT = 63,
            INTERVAL = 64,
            DURATION = 65,
            PICTURE = 66,
            CLOB = 67,
            UUID = 68,
            BYTE = 69,
            UNIQUEIDENTIFIER = 70,
            BYTEXXXZZZ = 71,
            BYTEA = 72,
            DATETIMEOFFSET = 73
        }

        public enum TYPE_REQUETE : int
        {
            MAKE_SELECT = 1,
            MAKE_INSERT = 2,
            MAKE_DELETE = 3,
            MAKE_UPDATE = 4,
            MAKE_DROP = 5,
            MAKE_CREATE = 6,
            MAKE_SYSTEM_COMMAND = 7,
            STORED_PROCEDURE = 8,
            MAKE_ALTER_COMMAND = 9
        }

        public enum BDD : int
        {
            DB_MYSQL = 1,
            DB_SQLSERVER = 2,
            DB_ODBC = 3,
            DB_POSTGRE = 4,
            DB_ORACLE = 5,
            DB_SQLITE = 6,
            DB_ACCESS = 7,
            NS_MONGODB = 8,
            FI_CSV = 9,
            FI_XML = 10,
            FI_XLS = 11,
            FI_FILE = 12,
            FI_JSON = 13,
            WS_REST = 14,
            MB_MAIL = 16,
            AD_ACTIVEDIRECTORY = 17,
        }

        public enum PRECISION_COLUMN_ANALYZER : int
        {
            VERY_LOW_PRECISION = 100,
            LOW_PRECISION = 50,
            NORMAL_PRECISION = 10,
            HIGH_PRECISION = 1
        }

        public enum TARGET_TABLE_METHOD : int
        {
            TRUNCATE = 1,
            DROP = 2,
            NOTHING = 3,
            PARTIAL_DELETE = 4,
            FULL_DELETE = 5,
            PARTIAL_DELETE_COL_DYNPARAM = 6,
            PARTIAL_DELETE_COL_DBNAME = 7
        }

        public enum SYNCHRO_TARGET_TABLE_BEHAVIOR : int
        {
            DUI = 1,
            UI = 2,
            U = 3,
            I = 4,
            DI = 5,
            TAG = 6,
            D = 7
        }

        public enum CSV_CLEANUP_METHOD : int
        {
            RIEN = 0,
            SUPPRIMER = 1,
            ZIPPER = 2,
            DEPLACER = 3
        }

        public enum SQL_JOIN : int
        {
            INNER_JOIN = 0,
            LEFT_JOIN = 1,
            RIGHT_JOIN = 2,
            OUTER_JOIN = 3
        }

        public enum LOG_TYPEINFO : int
        {
            INF = 1,
            WNG = 2,
            ERR = 3,
            DBG = 4,
            DET = 5
        }

        public enum PRE_POST_JOB_COMMANDS : int
        {
            PRE_JOB_COMMANDS = 1,
            POST_JOB_COMMANDS = 2
        }

        public enum AD_SEARCH_SCOPE : int
        {
            AD_BASE = 1,
            AD_ONELEVEL = 2,
            AD_SUBTREE = 3
        }
    }
}
