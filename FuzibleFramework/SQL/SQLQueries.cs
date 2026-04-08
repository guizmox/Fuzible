using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace FuzibleFramework
{
    public class SQLQueries
    {
        public static string CreateTableClientJobs(SQLTools SQLConnexion, string sTableName, string sSchema, bool bClearLog)
        {
            string sReturn;
            switch (SQLConnexion.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE " + sTableName + " (",
                                                                                                                                "[user_jobs] TEXT(30) NOT NULL,",
                                                                                                                                "[job_id] TEXT(10) NOT NULL, ",
                                                                                                                                "[job_name] TEXT(100) NOT NULL, ",
                                                                                                                                "[job_description] TEXT, ",
                                                                                                                                "[job_params] TEXT(100), ",
                                                                                                                                "[job_queries] TEXT, ",
                                                                                                                                "[job_haschildren] INTEGER NOT NULL, ",
                                                                                                                                "[job_password] TEXT(80) NOT NULL, ",
                                                                                                                                "[job_priority] INTEGER NOT NULL, ",
                                                                                                                                "[application_name] TEXT(100) NOT NULL, ",
                                                                                                                                "[job_category] TEXT(30) NOT NULL, ",
                                                                                                                                "PRIMARY KEY([user_jobs],[job_id]));"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE `", sTableName, "` ( ",
                                                                                                        "`user_jobs` VARCHAR(30) NOT NULL, ",
                                                                                                        "`job_id` VARCHAR(10) NOT NULL, ",
                                                                                                        "`job_name` VARCHAR(100) NOT NULL, ",
                                                                                                        "`job_description` VARCHAR(300) NULL DEFAULT NULL, ",
                                                                                                        "`job_params` VARCHAR(100) NULL DEFAULT NULL, ",
                                                                                                        "`job_queries` TEXT NULL DEFAULT NULL, ",
                                                                                                        "`job_haschildren` INT(11) NOT NULL, ",
                                                                                                        "`job_password` VARCHAR(80) NOT NULL, ",
                                                                                                        "`job_priority` INT(11) NOT NULL, ",
                                                                                                        "`application_name` VARCHAR(100) NOT NULL, ",
                                                                                                        "`job_category` VARCHAR(30) NOT NULL, ",
                                                                                                        "PRIMARY KEY(`user_jobs`,`job_id`) ",
                                                                                                        ") ",
                                                                                                        "COLLATE = 'utf8_general_ci' ",
                                                                                                        "ENGINE = InnoDB;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "]." + sTableName + " ",
                                                                                                    "( ",
                                                                                                        "user_jobs character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "job_id character varying(10) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "job_name character varying(100) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "job_description character varying(300) COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "job_params character varying(100) COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "job_queries character text COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "job_haschildren int NOT NULL, ",
                                                                                                        "job_password character varying(80) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "job_priority int NOT NULL, ",
                                                                                                        "application_name character varying(100) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "job_category character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "CONSTRAINT " + sTableName + "_pkey PRIMARY KEY(user_jobs, job_id) ",
                                                                                                    ") ",
                                                                                                    "WITH( ",
                                                                                                        "OIDS = FALSE ",
                                                                                                    ") ",
                                                                                                    "TABLESPACE pg_default;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "].[", sTableName, "] ( ",
                                                                                                    "[user_jobs][nvarchar](30) NOT NULL, ",
                                                                                                    "[job_id][nvarchar](10) NOT NULL, ",
                                                                                                    "[job_name][nvarchar](100) NOT NULL, ",
                                                                                                    "[job_description][nvarchar](300), ",
                                                                                                    "[job_params][nvarchar](100), ",
                                                                                                    "[job_queries][text], ",
                                                                                                    "[job_haschildren][int] NOT NULL, ",
                                                                                                    "[job_password][nvarchar](80) NOT NULL, ",
                                                                                                    "[job_priority][int] NOT NULL, ",
                                                                                                    "[application_name][nvarchar](100) NOT NULL, ",
                                                                                                    "[job_category][nvarchar](30) NOT NULL, ",
                                                                                                 "CONSTRAINT[PK_", sTableName, "] PRIMARY KEY CLUSTERED ",
                                                                                                "( ",
                                                                                                   "[user_jobs],[job_id] ASC ",
                                                                                                ")WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY] ",
                                                                                                ") ON[PRIMARY]"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE " + sTableName + " (",
                                                                                                                                                    "\"user_jobs\" TEXT NOT NULL,",
                                                                                                                                                    "\"job_id\"    TEXT NOT NULL, ",
                                                                                                                                                    "\"job_name\"  TEXT NOT NULL, ",
                                                                                                                                                    "\"job_description\"   TEXT, ",
                                                                                                                                                    "\"job_params\"    TEXT, ",
                                                                                                                                                    "\"job_queries\"   TEXT, ",
                                                                                                                                                    "\"job_haschildren\"   INTEGER NOT NULL, ",
                                                                                                                                                    "\"job_password\"  TEXT NOT NULL, ",
                                                                                                                                                    "\"job_priority\"  INTEGER NOT NULL, ",
                                                                                                                                                    "\"application_name\"  TEXT NOT NULL, ",
                                                                                                                                                    "\"job_category\"  TEXT NOT NULL, ",
                                                                                                                                                    "PRIMARY KEY(\"user_jobs\",\"job_id\"));"), new Query(), sTableName);
                    break;
            }
            StringBuilder sbLog = new();
            foreach (LogObject LO in SQLConnexion.MyLog.LogEvents)
            { sbLog.AppendLine(LO.ToString_ForFile()); }
            if (SQLConnexion.MyLog.HasNoErrors && SQLConnexion.MyLog.JobWarnings == 0)
            {
                sReturn = "Client Job Table (" + sTableName + ") successfully created !";
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            else
            {
                sReturn = "Errors Detected !" + sbLog.ToString();
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            return sReturn;
        }

        public static string CreateTablePlanifModels(SQLTools SQLConnexion, string sTableName, string sSchema, bool bClearLog)
        {
            string sReturn;
            switch (SQLConnexion.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE ", sTableName, " (",
                                                                                "[id_planifmodel] AUTOINCREMENT NOT NULL, ",
                                                                                "[li_planifmodel] TEXT(200) NOT NULL, ",
                                                                                "[id_job] TEXT(10) NOT NULL, ",
                                                                                "[li_configfile] TEXT(30) NOT NULL, ",
                                                                                "[li_description] TEXT(100) NOT NULL, ",
                                                                                "[li_arguments] TEXT(100), ",
                                                                                "[is_active] INTEGER NOT NULL, ",
                                                                                "PRIMARY KEY([id_planifmodel]));"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE `", sTableName, "` ( ",
                                                                                                        "`id_planifmodel` INT(11) NOT NULL AUTO_INCREMENT, ",
                                                                                                        "`li_planifmodel` VARCHAR(200) NOT NULL, ",
                                                                                                        "`id_job` VARCHAR(10) NOT NULL, ",
                                                                                                        "`li_configfile` VARCHAR(30) NOT NULL, ",
                                                                                                        "`li_description` VARCHAR(100) NOT NULL, ",
                                                                                                        "`li_arguments` VARCHAR(100) NULL DEFAULT NULL, ",
                                                                                                        "`is_active` INT(11) NOT NULL, ",
                                                                                                        "PRIMARY KEY(`id_planifmodel`) ",
                                                                                                        ") ",
                                                                                                        "COLLATE = 'utf8_general_ci' ",
                                                                                                        "ENGINE = InnoDB;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "]." + sTableName + " ",
                                                                                                    "( ",
                                                                                                        "id_planifmodel serial NOT NULL, ",
                                                                                                        "li_planifmodel character varying(200) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "id_job character varying(10) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_configfile character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_description character varying(100) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_arguments character varying(100) COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "is_active int NOT NULL, ",
                                                                                                        "CONSTRAINT " + sTableName + "_pkey PRIMARY KEY(id_planifmodel) ",
                                                                                                    ") ",
                                                                                                    "WITH( ",
                                                                                                        "OIDS = FALSE ",
                                                                                                    ") ",
                                                                                                    "TABLESPACE pg_default;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "].[", sTableName, "] ( ",
                                                                                                    "[id_planifmodel][int] IDENTITY(1, 1) NOT NULL, ",
                                                                                                    "[li_planifmodel][nvarchar](200) NOT NULL, ",
                                                                                                    "[id_job][nvarchar](10) NOT NULL, ",
                                                                                                    "[li_configfile][nvarchar](30) NOT NULL, ",
                                                                                                    "[li_description][nvarchar](100) NOT NULL, ",
                                                                                                    "[li_arguments][nvarchar](100), ",
                                                                                                    "[is_active][int] NOT NULL, ",
                                                                                                 "CONSTRAINT[PK_", sTableName, "] PRIMARY KEY CLUSTERED ",
                                                                                                "( ",
                                                                                                   "[id_planifmodel] ASC ",
                                                                                                ")WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY] ",
                                                                                                ") ON[PRIMARY]"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE ", sTableName, " (",
                                                                                                    "\"id_planifmodel\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ",
                                                                                                    "\"li_planifmodel\" TEXT NOT NULL, ",
                                                                                                    "\"id_job\" TEXT NOT NULL, ",
                                                                                                    "\"li_configfile\" TEXT NOT NULL, ",
                                                                                                    "\"li_description\" TEXT NOT NULL, ",
                                                                                                    "\"li_arguments\" TEXT, ",
                                                                                                    "\"is_active\" INTEGER NOT NULL);"), new Query(), sTableName);
                    break;
            }
            StringBuilder sbLog = new();
            foreach (LogObject LO in SQLConnexion.MyLog.LogEvents)
            { sbLog.AppendLine(LO.ToString_ForFile()); }
            if (SQLConnexion.MyLog.HasNoErrors && SQLConnexion.MyLog.JobWarnings == 0)
            {
                sReturn = "Planification Table (" + sTableName + ") successfully created !";
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            else
            {
                sReturn = "Errors Detected !" + sbLog.ToString();
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            return sReturn;
        }

        public static string CreateTableStackLauncher(SQLTools SQLConnexion, string sTableName, string sSchema, bool bClearLog)
        {
            string sReturn;
            switch (SQLConnexion.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE ", sTableName, " ( ",
                                                                                "[id_stack] AUTOINCREMENT NOT NULL, ",
                                                                                "[dt_stack] DATETIME NOT NULL, ",
                                                                                "[dt_requested_execution] DATETIME, ",
                                                                                "[nb_priority] INTEGER NOT NULL, ",
                                                                                "[li_machine] TEXT(20) NOT NULL, ",
                                                                                "[li_user] TEXT(30) NOT NULL, ",
                                                                                "[li_user_exec] TEXT(30) NOT NULL, ",
                                                                                "[li_apptolaunch] TEXT(100) NOT NULL, ",
                                                                                "[li_configfile] TEXT(30) NOT NULL, ",
                                                                                "[id_job] TEXT(10) NOT NULL, ",
                                                                                "[li_job] TEXT(100) NOT NULL, ",
                                                                                "[li_arguments] TEXT(100) NULL, ",
                                                                                "[li_status] TEXT(20) NOT NULL, ",
                                                                                "[dt_start] DATETIME NULL, ",
                                                                                "[dt_end] DATETIME NULL, ",
                                                                                "[li_message] TEXT NULL);"), new Query(), sTableName);
                    break;

                case SQLTools_Enums.BDD.DB_MYSQL:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE `", sTableName, "` ( ",
                                                                                                        "`id_stack` INT(11) NOT NULL AUTO_INCREMENT, ",
                                                                                                        "`dt_stack` TIMESTAMP NOT NULL, ",
                                                                                                        "`dt_requested_execution` TIMESTAMP NULL, ",
                                                                                                        "`nb_priority` INT(11) NOT NULL, ",
                                                                                                        "`li_machine` VARCHAR(20) NOT NULL, ",
                                                                                                        "`li_user` VARCHAR(30) NOT NULL, ",
                                                                                                        "`li_user_exec` VARCHAR(30) NOT NULL, ",
                                                                                                        "`li_apptolaunch` VARCHAR(100) NOT NULL, ",
                                                                                                        "`li_configfile` VARCHAR(30) NOT NULL, ",
                                                                                                        "`id_job` VARCHAR(10) NOT NULL, ",
                                                                                                        "`li_job` VARCHAR(100) NOT NULL, ",
                                                                                                        "`li_arguments` VARCHAR(100) NULL DEFAULT NULL, ",
                                                                                                        "`li_status` VARCHAR(20) NOT NULL, ",
                                                                                                        "`dt_start` TIMESTAMP NULL DEFAULT NULL, ",
                                                                                                        "`dt_end` TIMESTAMP NULL DEFAULT NULL, ",
                                                                                                        "`li_message` TEXT NULL, ",
                                                                                                        "PRIMARY KEY(`id_stack`) ",
                                                                                                        ") ",
                                                                                                        "COLLATE = 'utf8_general_ci' ",
                                                                                                        "ENGINE = InnoDB;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "]." + sTableName + " ",
                                                                                                    "( ",
                                                                                                        "id_stack serial NOT NULL, ",
                                                                                                        "dt_stack timestamp without time zone NOT NULL, ",
                                                                                                        "dt_requested_execution timestamp without time zone, ",
                                                                                                        "nb_priority integer NOT NULL, ",
                                                                                                        "li_machine character varying(20) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_user character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_user_exec character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_apptolaunch character varying(100) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_configfile character varying(30) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "id_job character varying(20) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_job character varying(100) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "li_arguments character varying(100) COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "li_status character varying(20) COLLATE pg_catalog.\"default\" NOT NULL, ",
                                                                                                        "dt_start timestamp without time zone NULL, ",
                                                                                                        "dt_end timestamp without time zone NULL, ",
                                                                                                         "li_message text COLLATE pg_catalog.\"default\" NULL, ",
                                                                                                        "CONSTRAINT " + sTableName + "_pkey PRIMARY KEY(id_stack) ",
                                                                                                    ") ",
                                                                                                    "WITH( ",
                                                                                                        "OIDS = FALSE ",
                                                                                                    ") ",
                                                                                                    "TABLESPACE pg_default;"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE [", sSchema, "].[", sTableName, "] ( ",
                                                                                                    "[id_stack][int] IDENTITY(1, 1) NOT NULL, ",
                                                                                                    "[dt_stack][datetime] NOT NULL, ",
                                                                                                    "[dt_requested_execution][datetime], ",
                                                                                                    "[nb_priority][int] NOT NULL, ",
                                                                                                    "[li_machine][nvarchar](20) NOT NULL, ",
                                                                                                    "[li_user][nvarchar](30) NOT NULL, ",
                                                                                                    "[li_user_exec][nvarchar](30) NOT NULL, ",
                                                                                                    "[li_apptolaunch][nvarchar](100) NOT NULL, ",
                                                                                                    "[li_configfile][nvarchar](30) NOT NULL, ",
                                                                                                    "[id_job][nvarchar](10) NOT NULL, ",
                                                                                                    "[li_job][nvarchar](100) NOT NULL, ",
                                                                                                    "[li_arguments][nvarchar](100) NULL, ",
                                                                                                    "[li_status][nvarchar](20) NOT NULL, ",
                                                                                                    "[dt_start][datetime] NULL, ",
                                                                                                    "[dt_end][datetime] NULL, ",
                                                                                                    "[li_message][nvarchar(max)] NULL, ",
                                                                                                 "CONSTRAINT[PK_", sTableName, "] PRIMARY KEY CLUSTERED ",
                                                                                                "( ",
                                                                                                   "[id_stack] ASC ",
                                                                                                ")WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY] ",
                                                                                                ") ON[PRIMARY]"), new Query(), sTableName);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    //dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, li_job, li_pwd, li_arguments, li_status, dt_end
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, string.Concat("CREATE TABLE ", sTableName, " ( ",
                                                                                                    "\"id_stack\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ",
                                                                                                    "\"dt_stack\" TEXT NOT NULL, ",
                                                                                                    "\"dt_requested_execution\" TEXT, ",
                                                                                                    "\"nb_priority\" INTEGER NOT NULL, ",
                                                                                                    "\"li_machine\" TEXT NOT NULL, ",
                                                                                                    "\"li_user\" TEXT NOT NULL, ",
                                                                                                    "\"li_user_exec\" TEXT NOT NULL, ",
                                                                                                    "\"li_apptolaunch\" TEXT NOT NULL, ",
                                                                                                    "\"li_configfile\" TEXT NOT NULL, ",
                                                                                                    "\"id_job\" TEXT NOT NULL, ",
                                                                                                    "\"li_job\" TEXT NOT NULL, ",
                                                                                                    "\"li_arguments\" TEXT NULL, ",
                                                                                                    "\"li_status\" TEXT NOT NULL, ",
                                                                                                    "\"dt_start\" TEXT NULL, ",
                                                                                                    "\"dt_end\" TEXT NULL, ",
                                                                                                    "\"li_message\" TEXT NULL);"), new Query(), sTableName);
                    break;
            }
            StringBuilder sbLog = new();
            foreach (LogObject LO in SQLConnexion.MyLog.LogEvents)
            { sbLog.AppendLine(LO.ToString_ForFile()); }
            if (SQLConnexion.MyLog.HasNoErrors && SQLConnexion.MyLog.JobWarnings == 0)
            {
                sReturn = "Stack Table (" + sTableName + ") successfully created !";
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            else
            {
                sReturn = "Errors Detected !" + sbLog.ToString();
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            return sReturn;
        }

        public static string CreateTablesLog(SQLTools SQLConnexion, List<string> sTableName, bool bClearLog)
        {
            string sReturn;

            string sQuery = SQLConnexion.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE_LOG_ENT);
            sQuery = sQuery.Replace("{TABLE_NAME_ENT}", sTableName[0]);
            sQuery = sQuery.Replace("{TABLE_NAME_LIG}", sTableName[1]);
            sQuery = sQuery.Replace("{SCHEMA_NAME}", SQLConnexion.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
            if (sQuery.Length > 0)
            { SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sQuery, new Query(), sTableName[0]); }
            else { SQLConnexion.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, "Can't find SQL pattern in INI file for " + SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE_LOG_ENT.ToString(), SQLTools_Enums.LOG_TYPEINFO.ERR); }

            string sQueryB = SQLConnexion.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE_LOG_LIG);
            sQueryB = sQueryB.Replace("{TABLE_NAME_ENT}", sTableName[0]);
            sQueryB = sQueryB.Replace("{TABLE_NAME_LIG}", sTableName[1]);
            sQueryB = sQueryB.Replace("{SCHEMA_NAME}", SQLConnexion.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA));
            if (sQueryB.Length > 0)
            { SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, sQueryB, new Query(), sTableName[1]); }
            else { SQLConnexion.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.LOG, null, "Can't find SQL pattern in INI file for " + SQLTools_Enums.DRIVER_PARAMS.CREATE_TABLE_LOG_LIG.ToString(), SQLTools_Enums.LOG_TYPEINFO.ERR); }

            StringBuilder sbLog = new();
            foreach (LogObject LO in SQLConnexion.MyLog.LogEvents)
            { sbLog.AppendLine(LO.ToString_ForFile()); }
            if (SQLConnexion.MyLog.HasNoErrors && SQLConnexion.MyLog.JobWarnings == 0)
            {
                sReturn = "Log Tables (" + string.Join(",", sTableName) + ") successfully created !";
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            else
            {
                sReturn = "Errors Detected !" + sbLog.ToString();
                if (bClearLog) { SQLConnexion.MyLog.ClearLogEvents(); }
            }
            return sReturn;
        }

        public static void CreateTablesWebservicesLOG(SQLTools SQLConnexion)
        {
            switch (SQLConnexion.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_MYSQL:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, "", new Query(), "");
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    //TODO
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, "", new Query(), "");
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, "", new Query(), "");
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    SQLConnexion.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND, "", new Query(), "");
                    break;
            }
        }

        public static List<string> ParamsForADDriver()
        {
            List<string> sListQueries = new()
            {
                "P_AD_SEARCH_USER=(&(objectClass=user)(objectCategory=person)([SEARCH_PROPERTY]=[SEARCH_VALUE]))",
                "P_AD_SEARCH_GROUP=(&(objectClass=group)([SEARCH_PROPERTY]=[SEARCH_VALUE]))",
                "P_AD_SEARCH_USERS=(&(objectClass=user)(objectCategory=person))",
                "P_AD_SEARCH_GROUPS=(&(objectClass=group))"
            };

            return sListQueries;
        }

        public static List<string> ParamsForSQLDriver(SQLTools_Enums.BDD bDD, string sBddSchema)
        {
            List<string> sListQueries = new();

            switch (bDD)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + sBddSchema);
                    sListQueries.Add("P_ESCAPE_CHAR=");
                    sListQueries.Add("P_DATE_FORMAT=DATETIME");
                    sListQueries.Add("P_STRING_FORMAT=TEXT");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=ALTER TABLE {TABLE_NAME} ADD CONSTRAINT {CONSTRAINT_NAME} PRIMARY KEY({COLUMNS_LIST});");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE {TABLE_NAME} ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=ALTER TABLE {TABLE_NAME} ALTER COLUMN {COLUMN_NAME} {COLUMN_TYPE} NULL;");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=ALTER TABLE {TABLE_NAME} ALTER COLUMN {COLUMN_NAME} {COLUMN_TYPE} NOT NULL;");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE {TABLE_NAME} ADD {COLUMN_NAME} {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=ALTER TABLE {TABLE_NAME} ALTER COLUMN {COLUMN_NAME} {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=ALTER TABLE {TABLE_NAME} ALTER COLUMN {COLUMN_NAME} DEFAULT '{DEFAULT_VALUE}';");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=ALTER TABLE {TABLE_NAME} ADD CONSTRAINT UK_{COLUMN_NAME} UNIQUE ({COLUMNS_LIST})");
                    sListQueries.Add("P_SHRINK_TABLE=");
                    sListQueries.Add("P_GET_COLUMNS_LIST=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_COLUMN=HANDLED INTERNALLY");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=");
                    sListQueries.Add("P_GET_TABLES=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_TABLES_FILTERED=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_VIEWS=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_PRIMARY_KEY=HANDLED INTERNALLY");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=HANDLED INTERNALLY");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=HANDLED INTERNALLY");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM '{TABLE_NAME}' WHERE dt_event < (Now() - {DAYS_COUNT});");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO '{TABLE_NAME}' (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',Now(),'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}') RETURNING id_job;");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE '{TABLE_NAME}' SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = Now() WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE [{TABLE_NAME_ENT}] ([id_job] AUTOINCREMENT NOT NULL, [li_job] TEXT NOT NULL, [dt_job] DATETIME NOT NULL, [id_status] INT NOT NULL, [li_userlaunch] TEXT NOT NULL, [li_status] TEXT NULL, [dt_end_job] DATETIME NULL, CONSTRAINT [PK_{TABLE_NAME_ENT}] PRIMARY KEY ([id_job]));");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE [{TABLE_NAME_LIG}] ([id_job] INT NOT NULL, [id_ligne] AUTOINCREMENT NOT NULL, [dt_event] DATETIME NOT NULL, [li_origin] TEXT NOT NULL, [li_level] TEXT NOT NULL, [li_classe] TEXT NOT NULL, [li_function] TEXT NOT NULL, li_message TEXT NULL,CONSTRAINT [PK_{TABLE_NAME_LIG}] PRIMARY KEY ([id_ligne]));");
                    sListQueries.Add("P_CREATE_FROM_SELECT=SELECT * INTO [TABLE] FROM [ORIGINAL_TABLE] WHERE 0 = 1;");
                    sListQueries.Add("P_LIMITED_RESULTS=TOP_TOP");
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + sBddSchema);
                    sListQueries.Add("P_ESCAPE_CHAR=");
                    sListQueries.Add("P_DATE_FORMAT=DATE");
                    sListQueries.Add("P_STRING_FORMAT=VARCHAR");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE {TABLE_NAME} ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=");
                    sListQueries.Add("P_CREATE_COLUMN=");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=");
                    sListQueries.Add("P_SHRINK_TABLE=");
                    sListQueries.Add("P_GET_COLUMNS_LIST=");
                    sListQueries.Add("P_GET_COLUMN=");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=");
                    sListQueries.Add("P_GET_TABLES=");
                    sListQueries.Add("P_GET_TABLES_FILTERED=");
                    sListQueries.Add("P_GET_VIEWS=");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=");
                    sListQueries.Add("P_GET_PRIMARY_KEY=");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM {TABLE_NAME};");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM '{TABLE_NAME}' WHERE dt_event < (SYSDATE - {DAYS_COUNT});");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO '{TABLE_NAME}' (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',SYSDATE(),'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}') RETURNING id_job;");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE '{TABLE_NAME}' SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = SYSDATE() WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE `{TABLE_NAME_ENT}` (`id_job` INT NOT NULL AUTO_INCREMENT,`li_job` VARCHAR(80) NULL DEFAULT NULL`dt_job` TIMESTAMP NULL DEFAULT NULL,`id_status` INT(11) NULL DEFAULT NULL,`li_userlaunch` VARCHAR(30) NULL DEFAULT NULL,`li_status` VARCHAR(30) NULL DEFAULT NULL,`dt_end_job` TIMESTAMP NULL DEFAULT NULL, PRIMARY KEY(`id_job`)) COLLATE = 'utf8_general_ci' ENGINE = InnoDB;");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE `{TABLE_NAME_LIG}` (`id_job` INT NULL DEFAULT NULL,`id_ligne` INT(11) NOT NULL AUTO_INCREMENT,`dt_event` TIMESTAMP NULL DEFAULT NULL,`li_origin` VARCHAR(10) NULL DEFAULT NULL,`li_level` VARCHAR(10) NULL DEFAULT NULL,`li_classe` VARCHAR(50) NULL DEFAULT NULL,`li_function` VARCHAR(50) NULL DEFAULT NULL,`li_message` TEXT NULL,PRIMARY KEY(`id_ligne`),INDEX `FK_{TABLE_NAME_LIG}` (`id_job`),CONSTRAINT `FK_{TABLE_NAME_LIG}` FOREIGN KEY(`id_job`) REFERENCES `{TABLE_NAME_ENT}` (`id_job`) ON UPDATE CASCADE ON DELETE CASCADE) COLLATE = 'utf8_general_ci' ENGINE = InnoDB;");
                    sListQueries.Add("P_CREATE_FROM_SELECT=");
                    sListQueries.Add("P_LIMITED_RESULTS=");
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + sBddSchema);
                    sListQueries.Add("P_ESCAPE_CHAR=`");
                    sListQueries.Add("P_DATE_FORMAT=DATETIME");
                    sListQueries.Add("P_STRING_FORMAT=VARCHAR");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=ALTER TABLE `{TABLE_NAME}` ADD CONSTRAINT {CONSTRAINT_NAME} PRIMARY KEY({COLUMNS_LIST});");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE `{TABLE_NAME}` ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=ALTER TABLE `{TABLE_NAME}` MODIFY `{COLUMN_NAME}` {COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=ALTER TABLE `{TABLE_NAME}` MODIFY `{COLUMN_NAME}` {COLUMN_TYPE} NOT NULL;");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE `{TABLE_NAME}` ADD `{COLUMN_NAME}` {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=ALTER TABLE `{TABLE_NAME}` MODIFY `{COLUMN_NAME}` {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=ALTER TABLE `{TABLE_NAME}` MODIFY `{COLUMN_NAME}` DEFAULT '{DEFAULT_VALUE}';");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=ALTER TABLE `{TABLE_NAME}` ADD UNIQUE ({COLUMNS_LIST});");
                    sListQueries.Add("P_SHRINK_TABLE=OPTIMIZE TABLE `{TABLE_NAME}`;");
                    sListQueries.Add("P_GET_COLUMNS_LIST=SELECT column_name, data_type, character_maximum_length, is_nullable, column_key, data_type, concat(coalesce(numeric_precision, ''), ',', coalesce(numeric_scale, '')), column_default, case when COLUMN_KEY = 'MUL' Then 'YES' ELSE 'NO' END as uniqueKey, case when COLUMN_KEY <> '' THEN 'YES' ELSE 'NO' END as IsKey FROM information_schema.columns WHERE table_schema = '{DATABASE_NAME}' AND table_name = '{TABLE_NAME}';");
                    sListQueries.Add("P_GET_COLUMN=SELECT column_name, data_type, character_maximum_length, is_nullable, column_key, data_type, concat(coalesce(numeric_precision, ''), ',', coalesce(numeric_scale, '')), column_default, case when COLUMN_KEY = 'MUL' Then 'YES' ELSE 'NO' END as uniqueKey, case when COLUMN_KEY <> '' THEN 'YES' ELSE 'NO' END as IsKey FROM information_schema.columns WHERE table_schema = '{DATABASE_NAME}' AND table_name = '{TABLE_NAME}' AND column_name = '{COLUMN_NAME}';");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=USE {DATABASE_NAME} SET FOREIGN_KEY_CHECKS=0;");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=USE {DATABASE_NAME} SET FOREIGN_KEY_CHECKS=1;");
                    sListQueries.Add("P_GET_TABLES=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = '{DATABASE_NAME}';");
                    sListQueries.Add("P_GET_TABLES_FILTERED=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = '{DATABASE_NAME}' AND TABLE_NAME LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_VIEWS=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'VIEW' AND TABLE_SCHEMA = '{DATABASE_NAME}';");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'VIEW' AND TABLE_SCHEMA = '{DATABASE_NAME}' AND TABLE_NAME LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_PRIMARY_KEY=SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, COALESCE(CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION), IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{DATABASE_NAME}' AND TABLE_NAME = '{TABLE_NAME}' AND COLUMN_KEY = 'PRI';");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=SELECT DISTINCT col.ORDINAL_POSITION, col.COLUMN_NAME, col.DATA_TYPE, COALESCE(col.CHARACTER_MAXIMUM_LENGTH, col.NUMERIC_PRECISION), col.IS_NULLABLE, tc.CONSTRAINT_NAME, 'NO' as IsIdentity  FROM information_schema.TABLE_CONSTRAINTS tc inner join information_schema.COLUMNS col ON tc.TABLE_SCHEMA = col.TABLE_SCHEMA AND tc.TABLE_NAME = col.TABLE_NAME WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY' AND tc.TABLE_SCHEMA = '{DATABASE_NAME}' AND tc.TABLE_NAME = '{TABLE_NAME}';");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = '{DATABASE_NAME}' AND TABLE_NAME = '{TABLE_NAME}';");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM `{TABLE_NAME}` WHERE DATE_ADD(SYSDATE(), INTERVAL -{DAYS_COUNT} DAY) < dt_event;");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO `{TABLE_NAME}` (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',SYSDATE(),'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}') RETURNING id_job;");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE `{TABLE_NAME}` SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = SYSDATE() WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE `{TABLE_NAME_ENT}` (`id_job` INT(11) NOT NULL AUTO_INCREMENT,`li_job` VARCHAR(80) NULL DEFAULT NULL`dt_job` DATETIME NULL DEFAULT NULL,`id_status` INT(11) NULL DEFAULT NULL,`li_userlaunch` VARCHAR(30) NULL DEFAULT NULL,`li_status` VARCHAR(30) NULL DEFAULT NULL,`dt_end_job` DATETIME NULL DEFAULT NULL, PRIMARY KEY(`id_job`)) COLLATE = 'utf8_general_ci' ENGINE = InnoDB;");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE `{TABLE_NAME_LIG}` (`id_job` INT(11) NULL DEFAULT NULL,`id_ligne` INT(11) NOT NULL AUTO_INCREMENT,`dt_event` DATETIME NULL DEFAULT NULL,`li_origin` VARCHAR(10) NULL DEFAULT NULL,`li_level` VARCHAR(10) NULL DEFAULT NULL,`li_classe` VARCHAR(50) NULL DEFAULT NULL,`li_function` VARCHAR(50) NULL DEFAULT NULL,`li_message` TEXT NULL,PRIMARY KEY(`id_ligne`),INDEX `FK_{TABLE_NAME_LIG}` (`id_job`),CONSTRAINT `FK_{TABLE_NAME_LIG}` FOREIGN KEY(`id_job`) REFERENCES `{TABLE_NAME_ENT}` (`id_job`) ON UPDATE CASCADE ON DELETE CASCADE) COLLATE = 'utf8_general_ci' ENGINE = InnoDB;");
                    sListQueries.Add("P_CREATE_FROM_SELECT=CREATE TABLE [TABLE] AS SELECT * FROM [ORIGINAL_TABLE] WHERE 0 = 1;");
                    sListQueries.Add("P_LIMITED_RESULTS=LIMIT_BOTTOM");
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + sBddSchema);
                    sListQueries.Add("P_ESCAPE_CHAR=\"");
                    sListQueries.Add("P_DATE_FORMAT=TIMESTAMP");
                    sListQueries.Add("P_STRING_FORMAT=VARCHAR2");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=ALTER TABLE {TABLE_NAME} ADD CONSTRAINT {CONSTRAINT_NAME} PRIMARY KEY({COLUMNS_LIST});");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE {TABLE_NAME} ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=ALTER TABLE {TABLE_NAME} MODIFY {COLUMN_NAME} {COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=ALTER TABLE {TABLE_NAME} MODIFY {COLUMN_NAME} {COLUMN_TYPE} NOT NULL;");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE {TABLE_NAME} ADD {COLUMN_NAME} {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=ALTER TABLE {TABLE_NAME} MODIFY {COLUMN_NAME} {COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=ALTER TABLE {TABLE_NAME} MODIFY {COLUMN_NAME} DEFAULT '{DEFAULT_VALUE}';");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=");
                    sListQueries.Add("P_SHRINK_TABLE=ALTER TABLE {TABLE_NAME} SHRINK SPACE;");
                    sListQueries.Add("P_GET_COLUMNS_LIST=SELECT column_name, data_type, data_length, nullable, data_default, data_type, data_precision || ',' || data_scale as NumericPrecision, data_default, 'NO' as UniqueKey, 'NO' as IsKey FROM USER_TAB_COLUMNS WHERE table_name = UPPER('{TABLE_NAME}')");
                    sListQueries.Add("P_GET_COLUMN=SELECT column_name, data_type, data_length, nullable, data_default, data_type, data_precision || ',' || data_scale as NumericPrecision, data_default, 'NO' as UniqueKey, 'NO' as IsKey FROM USER_TAB_COLUMNS WHERE table_name = UPPER('{TABLE_NAME}') AND column_name = '{COLUMN_NAME}'");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=BEGIN FOR c IN (SELECT c.owner, c.table_name, c.constraint_name FROM user_constraints c, user_tables t WHERE c.table_name = t.table_name AND c.status = 'ENABLED' AND NOT (t.iot_type IS NOT NULL AND c.constraint_type = 'P') AND c.table_name = UPPER('{TABLE_NAME}') ORDER BY c.constraint_type DESC) LOOP dbms_utility.exec_ddl_statement('alter table \"' || c.owner || '\".\"' || c.table_name || '\" disable constraint ' || c.constraint_name); END LOOP; END;");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=BEGIN FOR c IN (SELECT c.owner, c.table_name, c.constraint_name FROM user_constraints c, user_tables t WHERE c.table_name = t.table_name AND c.status = 'DISABLED' AND c.table_name = UPPER('{TABLE_NAME}') ORDER BY c.constraint_type) LOOP dbms_utility.exec_ddl_statement('alter table \"' || c.owner || '\".\"' || c.table_name || '\" enable constraint ' || c.constraint_name); END LOOP; END;");
                    sListQueries.Add("P_GET_TABLES=SELECT table_name FROM user_tables");
                    sListQueries.Add("P_GET_TABLES_FILTERED=SELECT table_name FROM user_tables WHERE table_name like '{LIKE_PATTERN}'");
                    sListQueries.Add("P_GET_VIEWS=SELECT view_name FROM sys.all_views");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=SELECT view_name FROM sys.all_views WHERE view_name like '{LIKE_PATTERN}'");
                    sListQueries.Add("P_GET_PRIMARY_KEY=SELECT cols.position, cols.column_name, 'integer', '0', cols.table_name, cons.status, cons.owner FROM all_constraints cons, all_cons_columns cols WHERE cols.table_name = UPPER('{TABLE_NAME}') AND cons.constraint_type = 'P' AND cons.constraint_name = cols.constraint_name AND cons.owner = cols.owner ORDER BY cols.table_name, cols.position");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=SELECT cols.position, cols.column_name, 'integer', '0', cols.table_name, cons.status, cons.owner FROM all_constraints cons, all_cons_columns cols WHERE cols.table_name = UPPER('{TABLE_NAME}') AND cons.constraint_type = 'F' AND cons.constraint_name = cols.constraint_name AND cons.owner = cols.owner ORDER BY cols.table_name, cols.position");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM ALL_OBJECTS WHERE OBJECT_TYPE = 'TABLE' AND OBJECT_NAME = UPPER('{TABLE_NAME}');");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM {TABLE_NAME} WHERE dt_event < (SYSDATE - {DAYS_COUNT});");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO {TABLE_NAME} (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',SYSDATE,'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}'); SELECT LAST_INSERT_ID();");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE {TABLE_NAME} SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = SYSDATE WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=");
                    sListQueries.Add("P_CREATE_FROM_SELECT=CREATE TABLE [TABLE] AS(SELECT * FROM [ORIGINAL_TABLE] WHERE 0 = 0);");
                    sListQueries.Add("P_LIMITED_RESULTS=");
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + (sBddSchema.Length == 0 ? "public" : sBddSchema));
                    sListQueries.Add("P_ESCAPE_CHAR=\"");
                    sListQueries.Add("P_DATE_FORMAT=TIMESTAMP");
                    sListQueries.Add("P_STRING_FORMAT=VARCHAR");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ADD CONSTRAINT {CONSTRAINT_NAME} PRIMARY KEY({COLUMNS_LIST});");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" DROP NOT NULL;");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" SET NOT NULL;");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ADD COLUMN \"{COLUMN_NAME}\" {COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" TYPE {COLUMN_TYPE} USING \"{COLUMN_NAME}\"::{COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" DEFAULT '{DEFAULT_VALUE}';");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ADD UNIQUE ({COLUMNS_LIST});");
                    sListQueries.Add("P_SHRINK_TABLE=VACUUM FULL \"{SCHEMA_NAME}\".\"{TABLE_NAME}\"");
                    sListQueries.Add("P_GET_COLUMNS_LIST=SELECT column_name, data_type, character_maximum_length, is_nullable, is_identity, data_type, concat(coalesce(numeric_precision, ''), ',', coalesce(numeric_scale, '')), column_default, 'NO' as UniqueKey, 'NO' as IsKey FROM information_schema.columns WHERE table_schema = '{SCHEMA_NAME}' AND table_name = '{TABLE_NAME}';");
                    sListQueries.Add("P_GET_COLUMN=SELECT column_name, data_type, character_maximum_length, is_nullable, is_identity, data_type, concat(coalesce(numeric_precision, ''), ',', coalesce(numeric_scale, '')), column_default, 'NO' as UniqueKey, 'NO' as IsKey FROM information_schema.columns WHERE table_schema = '{SCHEMA_NAME}' AND table_name = '{TABLE_NAME}' AND column_name = '{COLUMN_NAME}';");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" DISABLE TRIGGER USER;");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=ALTER TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" ENABLE TRIGGER USER;");
                    sListQueries.Add("P_GET_TABLES=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA NOT IN ('pg_catalog','information_schema');");
                    sListQueries.Add("P_GET_TABLES_FILTERED=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME LIKE '{LIKE_PATTERN}' AND TABLE_SCHEMA NOT IN ('pg_catalog','information_schema');");
                    sListQueries.Add("P_GET_VIEWS=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'VIEW' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}';");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'VIEW' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_PRIMARY_KEY=SELECT pg_attribute.attnum, pg_attribute.attname, format_type(pg_attribute.atttypid, pg_attribute.atttypmod) AS data_type, pg_attribute.attlen, pg_attribute.attnotnull FROM pg_index, pg_class, pg_attribute, pg_namespace WHERE indrelid = pg_class.oid AND nspname = '{SCHEMA_NAME}' AND pg_class.relnamespace = pg_namespace.oid AND pg_attribute.attrelid = pg_class.oid AND pg_attribute.attnum = any(pg_index.indkey) AND indisprimary AND pg_class.relname = '{TABLE_NAME}';");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=SELECT kcu.ordinal_position, kcu.column_name, col.data_type, coalesce(col.character_maximum_length, col.numeric_precision) as colsize, col.is_nullable, ccu.constraint_name, col.is_identity, ccu.table_schema AS foreign_table_schema, ccu.table_name AS foreign_table_name, ccu.column_name AS foreign_column_name FROM information_schema.table_constraints AS tc JOIN information_schema.key_column_usage AS kcu ON tc.constraint_name = kcu.constraint_name JOIN information_schema.constraint_column_usage AS ccu ON ccu.constraint_name = tc.constraint_name JOIN information_schema.columns AS col ON kcu.column_name = col.column_name and ccu.table_catalog = col.table_catalog and ccu.table_schema = col.table_schema and ccu.table_name = col.table_name WHERE constraint_type = 'FOREIGN KEY' AND col.table_schema = '{SCHEMA_NAME}' AND tc.table_name = '{TABLE_NAME}';");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME = '{TABLE_NAME}';");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" WHERE dt_event < CURRENT_DATE - INTERVAL '{DAYS_COUNT} day';");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',CURRENT_TIMESTAMP,'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}') RETURNING id_job;");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE \"{SCHEMA_NAME}\".\"{TABLE_NAME}\" SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = CURRENT_TIMESTAMP WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME_ENT}\" (id_job serial NOT NULL,li_job character varying(80) COLLATE pg_catalog.default NOT NULL,dt_job timestamp without time zone NOT NULL,id_status integer NOT NULL,li_userlaunch character varying(30) COLLATE pg_catalog.default NOT NULL,li_status character varying(30) COLLATE pg_catalog.default,dt_end_job timestamp without time zone NULL,CONSTRAINT {TABLE_NAME_ENT}_pkey PRIMARY KEY(id_job)) WITH(OIDS = FALSE) TABLESPACE pg_default;");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE \"{SCHEMA_NAME}\".\"{TABLE_NAME_LIG}\" (id_job integer NOT NULL,id_ligne serial NOT NULL,dt_event timestamp without time zone NOT NULL,li_origin character varying(10) COLLATE pg_catalog.default NOT NULL,li_level character varying(10) COLLATE pg_catalog.default NOT NULL,li_classe character varying(50) COLLATE pg_catalog.default NOT NULL,li_function character varying(50) COLLATE pg_catalog.default NOT NULL,li_message text COLLATE pg_catalog.default,CONSTRAINT {TABLE_NAME_LIG}_pkey PRIMARY KEY (id_ligne),CONSTRAINT {TABLE_NAME_LIG}_id_job_fkey FOREIGN KEY (id_job) REFERENCES {SCHEMA_NAME}.{TABLE_NAME_ENT} (id_job) MATCH SIMPLE ON UPDATE CASCADE ON DELETE CASCADE) WITH(OIDS = FALSE) TABLESPACE pg_default;");
                    sListQueries.Add("P_CREATE_FROM_SELECT=SELECT * INTO [SCHEMA_NAME].[TABLE] FROM [SCHEMA_NAME].[ORIGINAL_TABLE] WHERE 0 = 1;");
                    sListQueries.Add("P_LIMITED_RESULTS=LIMIT_BOTTOM");
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sListQueries.Add("P_DEFAULT_SCHEMA=" + (sBddSchema.Length == 0 ? "dbo" : sBddSchema));
                    sListQueries.Add("P_ESCAPE_CHAR=\"");
                    sListQueries.Add("P_DATE_FORMAT=DATETIME");
                    sListQueries.Add("P_STRING_FORMAT=NVARCHAR");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ADD CONSTRAINT {CONSTRAINT_NAME} PRIMARY KEY({COLUMNS_LIST});");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" {COLUMN_TYPE};");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" {COLUMN_TYPE} NOT NULL;");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ADD \"{COLUMN_NAME}\" {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ALTER COLUMN \"{COLUMN_NAME}\" {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ADD CONSTRAINT DF_{COLUMN_NAME} DEFAULT '{DEFAULT_VALUE}' FOR \"{COLUMN_NAME}\";");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" ADD CONSTRAINT UK_{COLUMN_NAME} UNIQUE({COLUMNS_LIST});");
                    sListQueries.Add("P_SHRINK_TABLE=DBCC CLEANTABLE([{DATABASE_NAME}],[{TABLE_NAME}]);");
                    sListQueries.Add("P_GET_COLUMNS_LIST=SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, IS_NULLABLE, DATA_TYPE, CONCAT(NUMERIC_PRECISION, '.', NUMERIC_SCALE), COLUMN_DEFAULT, 'NO' as UniqueKey, 'NO' as IsKey FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME = '{TABLE_NAME}';");
                    sListQueries.Add("P_GET_COLUMN=SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, IS_NULLABLE, DATA_TYPE, CONCAT(NUMERIC_PRECISION, '.', NUMERIC_SCALE), COLUMN_DEFAULT, 'NO' as UniqueKey, 'NO' as IsKey FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME = '{TABLE_NAME}' AND COLUMN_NAME = '{COLUMN_NAME}';");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" NOCHECK CONSTRAINT ALL;");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=ALTER TABLE [{SCHEMA_NAME}].\"{TABLE_NAME}\" WITH CHECK CONSTRAINT ALL;");
                    sListQueries.Add("P_GET_TABLES=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_CATALOG = '{DATABASE_NAME}';");
                    sListQueries.Add("P_GET_TABLES_FILTERED=SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_VIEWS=SELECT name FROM sys.views;");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=SELECT name FROM sys.views WHERE name LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_PRIMARY_KEY=SELECT c.column_id, c.name AS column_name, t.name, c.max_length, c.is_nullable, i.name AS index_name, c.is_identity FROM sys.indexes i inner join sys.index_columns ic  ON i.object_id = ic.object_id AND i.index_id = ic.index_id inner join sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id inner join sys.types t on c.system_type_id = t.system_type_id and c.user_type_id = t.user_type_id WHERE i.is_primary_key = 1 and i.object_ID = OBJECT_ID('{DATABASE_NAME}.{SCHEMA_NAME}.{TABLE_NAME}');");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=select c.column_id, t.name as TableWithForeignKey, typ.name, typ.max_length, c.is_nullable, c.name as ColumnName, c.is_nullable from sys.foreign_key_columns as fk inner join sys.tables as t on fk.parent_object_id = t.object_id inner join sys.columns as c on fk.parent_object_id = c.object_id and fk.parent_column_id = c.column_id inner join sys.types as typ on c.system_type_id = typ.system_type_id where fk.referenced_object_id = (select object_id from sys.tables where name = '{TABLE_NAME}');");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = '{DATABASE_NAME}' AND TABLE_SCHEMA = '{SCHEMA_NAME}' AND TABLE_NAME = '{TABLE_NAME}';");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM [{SCHEMA_NAME}].\"{TABLE_NAME}\" WHERE dt_event < DATEADD(day, -{DAYS_COUNT},GETDATE());");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO [{SCHEMA_NAME}].\"{TABLE_NAME}\" (li_job, dt_job, id_status, li_userlaunch, li_status) VALUES ('{JOB_NAME}',GETDATE(),'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}'); SELECT SCOPE_IDENTITY();"); // OUTPUT INSERTED.ID VALUES (id_job);
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE [{SCHEMA_NAME}].\"{TABLE_NAME}\" SET id_status = {JOB_STATUS}, li_status = '{LOG_STATUS}', dt_end_job = GETDATE() WHERE id_job = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE [{SCHEMA_NAME}].[{TABLE_NAME_ENT}] ([id_job][int] IDENTITY(1, 1) NOT NULL,[li_job][nvarchar](80) NOT NULL,[dt_job][datetime] NOT NULL,[id_status][int] NOT NULL,[li_userlaunch][nvarchar](30) NOT NULL,[li_status][nvarchar](30) NULL,[dt_end_job][datetime] NULL,CONSTRAINT[PK_{TABLE_NAME_ENT}] PRIMARY KEY CLUSTERED ([id_job] ASC)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]) ON[PRIMARY];");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE [{SCHEMA_NAME}].[{TABLE_NAME_LIG}] ([id_job][int] NOT NULL,[id_ligne][int] IDENTITY(1, 1) NOT NULL,[dt_event][datetime] NOT NULL,[li_origin][nvarchar](10) NOT NULL,[li_level][nvarchar](10) NOT NULL,[li_classe][nvarchar](50) NOT NULL,[li_function][nvarchar](50) NOT NULL,[li_message][text] NULL,CONSTRAINT[PK_{TABLE_NAME_LIG}] PRIMARY KEY CLUSTERED ([id_ligne] ASC)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY] ; ALTER TABLE [{SCHEMA_NAME}].[{TABLE_NAME_LIG}]  WITH CHECK ADD CONSTRAINT[FK_{TABLE_NAME_LIG}_{TABLE_NAME_ENT}] FOREIGN KEY([id_job]) REFERENCES [{SCHEMA_NAME}].[{TABLE_NAME_ENT}]([id_job]) ON UPDATE CASCADE ON DELETE CASCADE ; ALTER TABLE [{SCHEMA_NAME}].[{TABLE_NAME_LIG}] CHECK CONSTRAINT[FK_{TABLE_NAME_LIG}_{TABLE_NAME_ENT}]");
                    sListQueries.Add("P_CREATE_FROM_SELECT=SELECT * INTO [SCHEMA_NAME].[TABLE] FROM [SCHEMA_NAME].[ORIGINAL_TABLE] WHERE 0 = 1;");
                    sListQueries.Add("P_LIMITED_RESULTS=TOP_TOP");
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sListQueries.Add("P_ESCAPE_CHAR=\"");
                    sListQueries.Add("P_DATE_FORMAT=TEXT");
                    sListQueries.Add("P_STRING_FORMAT=TEXT");
                    sListQueries.Add("P_CREATE_PRIMARY_KEY=");
                    sListQueries.Add("P_CREATE_TABLE=CREATE TABLE \"{TABLE_NAME}\" ({DEFINITION});");
                    sListQueries.Add("P_CHANGE_COLUMN_ALLOW_NULL=");
                    sListQueries.Add("P_CHANGE_COLUMN_DISALLOW_NULL=");
                    sListQueries.Add("P_CREATE_COLUMN=ALTER TABLE \"{TABLE_NAME}\" ADD \"{COLUMN_NAME}\" {COLUMN_TYPE} {NULL_NOT_NULL};");
                    sListQueries.Add("P_CHANGE_COLUMN_TYPE=");
                    sListQueries.Add("P_CHANGE_COLUMN_DEFAULT_VALUE=");
                    sListQueries.Add("P_CHANGE_COLUMN_ADD_UNIQUE=");
                    sListQueries.Add("P_SHRINK_TABLE=VACUUM");
                    sListQueries.Add("P_GET_COLUMNS_LIST=SELECT il.name, il.type, '0', il.\"notnull\", il.\"notnull\", il.type, '0', il.dflt_value, 'NO' as UniqueKey, 'NO' as IsKey FROM sqlite_master AS m, pragma_table_info(m.name) AS il WHERE m.type='table' AND m.name = '{TABLE_NAME}';");
                    sListQueries.Add("P_GET_COLUMN=SELECT il.name, il.type, '0', il.\"notnull\", il.\"notnull\", il.type, '0', il.dflt_value, 'NO' as UniqueKey, 'NO' as IsKey FROM sqlite_master AS m, pragma_table_info(m.name) AS il WHERE m.type='table' AND m.name = '{TABLE_NAME}' AND il.name = '{COLUMN_NAME}';");
                    sListQueries.Add("P_DISABLE_TABLE_CONSTRAINTS=PRAGMA FOREIGN_KEYS=OFF;");
                    sListQueries.Add("P_ENABLE_TABLE_CONSTRAINTS=PRAGMA FOREIGN_KEYS=ON;");
                    sListQueries.Add("P_GET_TABLES=SELECT tbl_name FROM sqlite_master WHERE type = 'table';");
                    sListQueries.Add("P_GET_TABLES_FILTERED=SELECT tbl_name FROM sqlite_master WHERE type = 'table' AND tbl_name LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_VIEWS=SELECT tbl_name FROM sqlite_master WHERE type = 'view';");
                    sListQueries.Add("P_GET_VIEWS_FILTERED=SELECT tbl_name FROM sqlite_master WHERE type = 'view' AND tbl_name LIKE '{LIKE_PATTERN}';");
                    sListQueries.Add("P_GET_PRIMARY_KEY=SELECT il.cid, il.name, il.type, '0', il.\"notnull\", il.name, il.pk FROM sqlite_master AS m, pragma_table_info(m.name) AS il WHERE m.type='table' AND m.name = '{TABLE_NAME}' AND pk = 1;");
                    sListQueries.Add("P_GET_FOREIGN_KEYS=SELECT tbl.cid, tbl.name, il.\"from\" || '_' || il.\"to\" as name_constraint, 0, tbl.\"notnull\", il.seq, il.seq, il.\"table\", il.\"to\" FROM sqlite_master AS m INNER JOIN pragma_foreign_key_list('{TABLE_NAME}') AS il ON il.\"table\" = m.tbl_name INNER JOIN pragma_table_info('{TABLE_NAME}') as tbl ON il.\"from\" = tbl.name WHERE m.type='table';");
                    sListQueries.Add("P_CHECK_TABLE_EXISTENCE=SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND tbl_name = '{TABLE_NAME}';");
                    sListQueries.Add("P_DELETE_LOG_EVENTS=DELETE FROM \"{TABLE_NAME}\" WHERE DATE(\"dt_event\") < DATE('now', '-{DAYS_COUNT} day');");
                    sListQueries.Add("P_INSERT_LOG_EVENT=INSERT INTO \"{TABLE_NAME}\" (\"li_job\", \"dt_job\", \"id_status\", \"li_userlaunch\", \"li_status\") VALUES ('{JOB_NAME}',DATETIME(),'{JOB_STATUS}','{LOG_USER}','{LOG_STATUS}');");
                    sListQueries.Add("P_UPDATE_LOG_EVENT=UPDATE \"{TABLE_NAME}\" SET \"id_status\" = {JOB_STATUS}, \"li_status\" = '{LOG_STATUS}', \"dt_end_job\" = DATETIME('now') WHERE \"id_job\" = {JOB_ID};");
                    sListQueries.Add("P_CREATE_TABLE_LOG_ENT=CREATE TABLE \"{TABLE_NAME_ENT}\" (\"id_job\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,\"li_job\" TEXT NOT NULL,\"dt_job\" TEXT NOT NULL,\"id_status\" INTEGER NOT NULL,\"li_userlaunch\" TEXT NOT NULL,\"li_status\" TEXT NULL,\"dt_end_job\" TEXT NULL);");
                    sListQueries.Add("P_CREATE_TABLE_LOG_LIG=CREATE TABLE \"{TABLE_NAME_LIG}\" (\"id_job\" INTEGER NOT NULL,\"id_ligne\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,\"dt_event\" TEXT NOT NULL,\"li_origin\" TEXT NOT NULL,\"li_level\" TEXT NOT NULL,\"li_classe\" TEXT NOT NULL,\"li_function\" TEXT NOT NULL,\"li_message\" TEXT NULL, FOREIGN KEY(\"id_job\") REFERENCES \"{TABLE_NAME_ENT}\" (\"id_job\") ON DELETE CASCADE ON UPDATE CASCADE)");
                    sListQueries.Add("P_CREATE_FROM_SELECT=CREATE TABLE [TABLE] AS SELECT * FROM [ORIGINAL_TABLE] WHERE 0 = 1;");
                    sListQueries.Add("P_LIMITED_RESULTS=LIMIT_BOTTOM");
                    break;
            }

            return sListQueries;
        }

        internal static string CreateInternalStatsTable()
        {
            return "CREATE TABLE \"job_stats\" (\"id_stat\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, \"user\" TEXT NOT NULL, \"user_exec\" TEXT NOT NULL, \"job_id\" TEXT NOT NULL, \"job_version\" TEXT NOT NULL, \"dt_end_job\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"duration\" INT, \"errors\" INT, \"warnings\" INT, \"queries\" INT, FOREIGN KEY(\"job_id\", \"user\") REFERENCES \"job_parameters\"(\"job_id\", \"user\") ON DELETE CASCADE ON UPDATE CASCADE);";
        }

        internal static void CreateInternalDB(string sFileDB)
        {
            StringBuilder sbCreateTables = new();
            sbCreateTables.AppendLine("CREATE TABLE \"user_parameters\" ( \"user\" TEXT NOT NULL, \"registration_code\" TEXT, \"license_type\" TEXT, \"mail_admin\" INTEGER, \"show_system_alerts\" INTEGER NOT NULL, \"multithreading_cores\" INTEGER NOT NULL, \"multi_target_in_parallel\" INTEGER NOT NULL, \"abort_job_on_errors\" INTEGER NOT NULL, \"connexion_string_sqllog\" TEXT, \"connexion_string_mail\" TEXT, \"keep_log_until\" INTEGER NOT NULL, \"type_driver_sqllog\" TEXT, \"log_mail_dontsend_ifsuccess\" INTEGER NOT NULL, \"allow_special_integers\" INTEGER NOT NULL, \"language\" TEXT NOT NULL, \"sql_max_errors\" INTEGER NOT NULL, \"sql_max_char_queries_debug_mode\" INTEGER NOT NULL, \"sql_commit\" INTEGER NOT NULL, \"sql_decimals\" INTEGER NOT NULL, \"sql_command_timeout\" INTEGER NOT NULL, \"sql_synchro_mode_table\" TEXT NOT NULL, \"sql_shrink_tables\" INTEGER NOT NULL, \"sql_mysql_default_port\" INTEGER NOT NULL, \"sql_postgres_default_port\" INTEGER NOT NULL, \"sql_oracle_default_port\" INTEGER NOT NULL, \"file_processed_dir\" TEXT NOT NULL, \"file_moved_prefix\" INTEGER NOT NULL, \"file_export_dir\" TEXT NOT NULL, \"file_max_rows_analyzer\" INTEGER NOT NULL, \"csv_rows_offset_before_depth_analysis\" INTEGER NOT NULL, \"file_days_to_keep_processed\" INTEGER NOT NULL, \"csv_separators\" TEXT NOT NULL, \"force_csv_invalidrow_integration\" INTEGER NOT NULL, \"csv_source_splitted_rowcount\" INTEGER NOT NULL, \"csv_source_splitted_rowcount_check_distant\" INTEGER NOT NULL, \"csv_header_detection_resemblance_offset\" NUMERIC NOT NULL, \"csv_header_detection_avg_unique_offset\" NUMERIC NOT NULL, \"mail_connection_timeout\" INTEGER NOT NULL, \"mail_max_pj_size\" INTEGER NOT NULL, \"mail_max_strsize_before_pj\" INTEGER NOT NULL, \"ws_connection_timeout\" INTEGER NOT NULL, \"ws_allow_responses_alter_columns\" INTEGER NOT NULL, \"ws_soql_records_only\" INTEGER NOT NULL, \"ws_default_encoding\" TEXT NOT NULL, \"ws_nuxeo_default_encoding\" TEXT NOT NULL, \"enable_query_assistant\" INTEGER NOT NULL, \"query_assistant_maxfilesize\" INTEGER NOT NULL, \"json_parser_replace_special_chars\" INTEGER NOT NULL, \"shell_operations_timeout\" INTEGER NOT NULL, \"shell_operations_ext_timeout\" INTEGER NOT NULL, \"allow_integers_starting_with_0\" INTEGER NOT NULL, \"application_server\" TEXT NOT NULL, \"registration_send_mode\" INTEGER NOT NULL, \"last_state\" TEXT, \"sql_directstream_commit\" INTEGER NOT NULL, \"security_shared_users\" INTEGER NOT NULL, \"security_principal_user\" TEXT NOT NULL, \"multithreading_cores_bigdata\" INTEGER NOT NULL, \"log_debug_functions\" INTEGER NOT NULL, \"user_password\" TEXT, \"json_alternative_processing_mode\" INTEGER NOT NULL, PRIMARY KEY(\"user\") );");
            sbCreateTables.AppendLine("CREATE TABLE \"service_parameters\" ( \"sql_connexion_string\" TEXT NOT NULL, \"sql_driver\" TEXT NOT NULL, \"parallel_jobs\" TEXT NOT NULL, \"flooding_interval\" TEXT NOT NULL, \"keep_log_until\" TEXT NOT NULL, \"kill_running_jobs_after\" TEXT NOT NULL, \"user_jobs\" TEXT NOT NULL, \"send_job_report\" TEXT NOT NULL, \"send_job_report_hour\" TEXT NOT NULL, \"send_job_report_ampm\" TEXT NOT NULL, \"send_job_report_recipients\" TEXT NOT NULL, PRIMARY KEY(\"user_jobs\") ); ");
            sbCreateTables.AppendLine("CREATE TABLE \"user_connstrings\" ( \"user\" TEXT NOT NULL, \"connstring_id\" TEXT NOT NULL, \"connstring_name\" TEXT NOT NULL, \"connstring\" TEXT NOT NULL, \"connstring_driver\" TEXT NOT NULL, PRIMARY KEY(\"user\",\"connstring_id\"), FOREIGN KEY(\"user\") REFERENCES \"user_parameters\"(\"user\") ON DELETE CASCADE ON UPDATE CASCADE ); ");
            sbCreateTables.AppendLine("CREATE TABLE \"user_connstrings_params\" ( \"user\" TEXT NOT NULL, \"connstring_id\" TEXT NOT NULL, \"param_name\" TEXT NOT NULL, \"param_value\" TEXT NOT NULL, PRIMARY KEY(\"user\",\"connstring_id\",\"param_name\"), FOREIGN KEY(\"user\",\"connstring_id\") REFERENCES \"user_connstrings\"(\"user\",\"connstring_id\") ON DELETE CASCADE ON UPDATE CASCADE ); ");
            sbCreateTables.AppendLine("CREATE TABLE \"job_parameters\" ( \"user\" TEXT NOT NULL, \"job_id\" TEXT NOT NULL, \"job_name\" TEXT NOT NULL, \"job_password\" TEXT NOT NULL, \"job_priority\" INTEGER NOT NULL, \"job_description\" TEXT, \"job_creation_date\" TEXT NOT NULL, \"job_modification_date\" TEXT, \"job_lastlaunch_date\" TEXT, \"job_lastlaunch_status\" TEXT, \"job_visibility\" INTEGER NOT NULL, \"subjob_abort_on_errors\" INTEGER NOT NULL, \"postcommands_bypass_on_errors\" INTEGER NOT NULL, \"turbo_mode\" INTEGER NOT NULL, \"source_queries_dynamic_parameters\" TEXT, \"synchro_target_table_behavior\" TEXT NOT NULL, \"synchro_bypass_query_filters_in_target\" INTEGER NOT NULL, \"synchro_store_changes\" INTEGER NOT NULL, \"connexion_string_export\" TEXT, \"connexion_string_import\" TEXT, \"database_source\" TEXT, \"database_target\" TEXT, \"export_import\" TEXT NOT NULL, \"export_use_dataset\" INTEGER NOT NULL, \"in_production\" INTEGER NOT NULL, \"log_level\" TEXT NOT NULL, \"sql_log\" INTEGER NOT NULL, \"threads_export\" INTEGER NOT NULL, \"threads_import\" INTEGER NOT NULL, \"trim_data\" INTEGER NOT NULL, \"drop_table_before_insert\" TEXT NOT NULL, \"check_fields_before_insert\" INTEGER NOT NULL, \"field_analyser_level\" TEXT NOT NULL, \"use_null_import\" INTEGER NOT NULL, \"convert_html_pattern_for_target\" INTEGER NOT NULL, \"allow_alter_column_target\" INTEGER NOT NULL, \"disable_constraints_target\" INTEGER NOT NULL, \"create_sql_primary_key_in_target\" INTEGER NOT NULL, \"import_file_cleanup\" TEXT NOT NULL, \"log_mail_adress\" TEXT, \"hyperfile_arrayfield_transform\" INTEGER NOT NULL, \"hyperfile_arrayfield_transform_separator\" TEXT, \"csv_dont_transform_with_invalid_header\" INTEGER NOT NULL, \"data_transform_rows_columns_add_label_values\" INTEGER NOT NULL, \"source_files_zipped_in\" TEXT, \"append_file_creation\" INTEGER NOT NULL, \"csv_char_separator\" TEXT NOT NULL, \"csv_multiple_files_in_one_pattern\" TEXT NOT NULL, \"csv_separator_end_row\" INTEGER NOT NULL, \"csv_target_add_header\" INTEGER NOT NULL, \"csv_row_offset\" INTEGER NOT NULL, \"xls_sheet_to_read\" INTEGER NOT NULL, \"xls_row_offset\" INTEGER NOT NULL, \"xls_password_source\" TEXT, \"xls_password_target\" TEXT, \"xml_header\" TEXT, \"json_header\" TEXT, \"xml_cdata_tag\" INTEGER NOT NULL, \"xml_remove_tag_for_empty_values\" INTEGER NOT NULL, \"max_rows_in_file\" INTEGER NOT NULL, \"webservice_nuxeo_api_endpoint\" TEXT, \"webservice_call_method\" TEXT NOT NULL, \"webservice_call_method_target\" TEXT NOT NULL, \"webservice_content_type_target\" TEXT NOT NULL, \"webservice_dont_send_empty_values\" INTEGER NOT NULL, \"webservice_request_body_type\" TEXT NOT NULL, \"sql_shrink_tables\" INTEGER NOT NULL, \"sql_add_dbname_column\" INTEGER NOT NULL, \"sql_add_rownum_column\" INTEGER NOT NULL, \"sql_add_dynamicparam_column\" TEXT, \"sql_add_timestamp_column\" INTEGER NOT NULL, \"post_job_sql_command_source\" TEXT, \"post_job_sql_command_target\" TEXT, \"pre_or_post_job_commands_source\" TEXT NOT NULL, \"pre_or_post_job_commands_target\" TEXT NOT NULL, \"ws_http_format_url_in_upper\" INTEGER NOT NULL, \"ws_http_send_columns_offset\" INTEGER NOT NULL, \"ws_save_webresponse_file\" INTEGER NOT NULL, \"ws_tracking_column_webresponse\" TEXT, \"ws_logtable_webresponses\" TEXT, \"ws_success_string\" TEXT, \"mb_get_unread_only\" INTEGER NOT NULL, \"mb_flag_retrieved_as_read\" INTEGER NOT NULL, \"mb_assemble_queries_same_recipient\" INTEGER NOT NULL, \"mb_max_mails_to_get\" INTEGER NOT NULL, \"ad_search_scope\" TEXT NOT NULL, \"remove_id_from_mongodb_query\" INTEGER NOT NULL, \"target_mongo_collection_behavior\" TEXT NOT NULL, \"create_pk_for_mongo_collection\" INTEGER NOT NULL, \"xml_target_row_builder\" TEXT NOT NULL, \"json_target_row_builder\" TEXT NOT NULL, \"mail_target_format\" TEXT NOT NULL, \"xls_target_add_header\" INTEGER NOT NULL, \"delete_source_data_after_insert\" INTEGER NOT NULL, \"ad_target_search_property\" TEXT NOT NULL, \"ad_target_behavior\" INTEGER NOT NULL, \"ad_activate_new_entries\" INTEGER NOT NULL, \"xls_target_with_title\" INTEGER NOT NULL, \"xls_target_style\" TEXT NOT NULL, \"xml_write_mode\" INTEGER NOT NULL, \"auto_sql_table_creation\" INTEGER NOT NULL, \"csv_add_quotes\" INTEGER NOT NULL, \"ws_raw_output\" INTEGER NOT NULL, \"file_raw_output\" INTEGER NOT NULL, \"ws_sql_language\" TEXT NOT NULL, \"sql_directstream_copy\" INTEGER NOT NULL, \"xls_row_write_offset\" INTEGER NOT NULL, \"ws_content_structure\" TEXT NOT NULL, \"xls_write_interpret_formulas\" INTEGER NOT NULL, \"api_get_typedata\" TEXT NOT NULL, \"api_source_postwork\" TEXT NOT NULL, \"subjob_abort_on_nodata\" INTEGER NOT NULL, \"post_job_sql_command_source_conn\" TEXT NOT NULL, \"post_job_sql_command_target_conn\" TEXT NOT NULL, \"webservice_special_http_parameters\" TEXT NOT NULL, \"no_sourcedata_noerror\" INTEGER NOT NULL, PRIMARY KEY(\"user\",\"job_id\"), FOREIGN KEY(\"user\") REFERENCES \"user_parameters\"(\"user\") ON DELETE CASCADE ON UPDATE CASCADE );");
            sbCreateTables.AppendLine("CREATE TABLE \"job_queries\" ( \"user\" TEXT NOT NULL, \"job_id\" TEXT NOT NULL, \"query_id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, \"query\" TEXT NOT NULL, FOREIGN KEY(\"user\",\"job_id\") REFERENCES \"job_parameters\"(\"user\",\"job_id\") ON DELETE CASCADE ON UPDATE CASCADE ); ");
            sbCreateTables.AppendLine("CREATE TABLE \"client_jobs\" ( \"user_jobs\" TEXT NOT NULL, \"job_id\" TEXT NOT NULL, \"job_name\" TEXT NOT NULL, \"job_description\" TEXT, \"job_params\" TEXT, \"job_queries\" TEXT, \"job_haschildren\" INTEGER NOT NULL, \"job_password\" TEXT NOT NULL, \"job_priority\" INTEGER NOT NULL, \"application_name\" TEXT NOT NULL, \"job_category\" TEXT NOT NULL, PRIMARY KEY(\"user_jobs\",\"job_id\") );");
            sbCreateTables.AppendLine("CREATE TABLE \"app_log_ent\" (\"id_job\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,\"li_job\" TEXT NOT NULL,\"dt_job\" TEXT NOT NULL,\"id_status\" INTEGER NOT NULL,\"li_userlaunch\" TEXT NOT NULL,\"li_status\" TEXT NULL,\"dt_end_job\" TEXT NULL); ");
            sbCreateTables.AppendLine("CREATE TABLE \"app_log_lig\" (\"id_job\" INTEGER NOT NULL,\"id_ligne\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,\"dt_event\" TEXT NOT NULL,\"li_origin\" TEXT NOT NULL,\"li_level\" TEXT NOT NULL,\"li_classe\" TEXT NOT NULL,\"li_function\" TEXT NOT NULL,\"li_message\" TEXT NULL, FOREIGN KEY(\"id_job\") REFERENCES \"app_log_ent\" (\"id_job\") ON DELETE CASCADE ON UPDATE CASCADE); ");
            sbCreateTables.AppendLine("CREATE TABLE \"app_planifmodel\" (\"id_planifmodel\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, \"li_planifmodel\" TEXT NOT NULL, \"id_job\" TEXT NOT NULL, \"li_configfile\" TEXT NOT NULL, \"li_description\" TEXT NOT NULL, \"li_arguments\" TEXT, \"is_active\" INTEGER NOT NULL); ");
            sbCreateTables.AppendLine("CREATE TABLE \"app_stacklaunch\" ( \"id_stack\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, \"dt_stack\" TEXT NOT NULL, \"dt_requested_execution\" TEXT, \"nb_priority\" INTEGER NOT NULL, \"li_machine\" TEXT NOT NULL, \"li_user\" TEXT NOT NULL, \"li_user_exec\" TEXT NOT NULL, \"li_apptolaunch\" TEXT NOT NULL, \"li_configfile\" TEXT NOT NULL, \"id_job\" TEXT NOT NULL, \"li_job\" TEXT NOT NULL, \"li_arguments\" TEXT NULL, \"li_status\" TEXT NOT NULL, \"dt_start\" TEXT NULL, \"dt_end\" TEXT NULL, \"li_message\" TEXT NULL); ");

            try
            {

                if (!System.IO.File.Exists(sFileDB))
                {
                    File.WriteAllBytes(sFileDB, new byte[0]);
                }
                
                using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sbCreateTables.ToString(), sqlConn);
                sqlCommand.ExecuteNonQuery();
            }
            catch { throw; }
        }

        internal static string GetQuery_CreateFromSelect(SQLTools_Enums.BDD sConnDriver)
        {
            string sCreate = "";
            switch (sConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sCreate = "SELECT * INTO " + "[TABLE]" + " FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sCreate = "CREATE TABLE " + "[TABLE]" + " AS SELECT * FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    //sCreate = "SELECT * INTO " + "[TABLE]" + " FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sCreate = "CREATE TABLE " + "[TABLE]" + " AS(SELECT * FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 0);";
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sCreate = "SELECT * INTO " + "[TABLE]" + " FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sCreate = "CREATE TABLE " + "[TABLE]" + " AS SELECT * FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sCreate = "SELECT * INTO " + "[TABLE]" + " FROM " + "[ORIGINAL_TABLE]" + " WHERE 0 = 1;";
                    break;
            }
            return sCreate;
        }
    }
}
