using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Security.Principal;
using MySqlConnector;

namespace FuzibleFramework
{

    public class ClientApp
    {
        private static readonly string LEGACY_INI_FILE_CLIENT_APP = string.Concat(Directory.GetCurrentDirectory(), "\\INI\\", "CLIENTAPP.INI");
        private static readonly string INI_FILE_CLIENT_APP = string.Concat(Directory.GetCurrentDirectory(), "\\CLIENTAPP.INI");
        private static readonly string INI_FILE_CLIENT_APP_ALT = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\CLIENTAPP.INI");
        private static readonly string INI_PASSWORD = SHSConstantes.PROGRAM_PWD;

        public string LoadingError { get; private set; } = "";
        public List<ClientJob> ListOfJobs { get; set; } = new List<ClientJob>();
        public SQLTools_Enums.BDD BDDDriverClientApp { get; set; } = SQLTools_Enums.BDD.DB_SQLITE;
        public string SQLConnexionStringClientApp
        {
            get; set;
        }
        public string SQLConnexionStringClientAppSchema
        {
            get; set;
        }
        public string FuzibleUserJobs
        {
            get; set;
        }
        public string FuzibleName
        {
            get; set;
        }
        public string Language { get; set; } = "EN";
        public int FloodingDelay { get; set; } = 15;

        private readonly LogTools.FileWriter fiClient = new();
        private readonly LogTools.FileWriter fiClientB = new(); // pour l'écriture dans le répertoire commun

        public ClientApp(bool bLoadJobs)
        {
            if (File.Exists(LEGACY_INI_FILE_CLIENT_APP))
            {
                fiClient.Filepath = LEGACY_INI_FILE_CLIENT_APP;
                LoadINIClientApp(LEGACY_INI_FILE_CLIENT_APP, bLoadJobs);
            }
            else if (File.Exists(INI_FILE_CLIENT_APP))
            {
                fiClient.Filepath = INI_FILE_CLIENT_APP;
                LoadINIClientApp(INI_FILE_CLIENT_APP, bLoadJobs);
            }
            else if (File.Exists(INI_FILE_CLIENT_APP_ALT))
            {
                fiClient.Filepath = INI_FILE_CLIENT_APP_ALT;
                LoadINIClientApp(INI_FILE_CLIENT_APP_ALT, bLoadJobs);
            }
            else //tentative de chargement en local
            {
                fiClient.Filepath = INI_FILE_CLIENT_APP;
                LoadDefaultConfiguration();
            }
            fiClientB.Filepath = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "\\Fuzible", "\\CLIENTAPP.INI");
        }

        private void LoadDefaultConfiguration()
        {
            BDDDriverClientApp = SQLTools_Enums.BDD.DB_SQLITE;

            if (File.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db"))
            {
                SQLConnexionStringClientApp = "Data Source=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db;foreign keys=true";

                string sQuery = string.Concat("SELECT up.user as li_user, up.language as li_lang, COUNT(cj.job_id) as nb_jobs ",
                                                "FROM user_parameters as up ",
                                                "LEFT JOIN client_jobs cj ON cj.user_jobs = up.user ",
                                                "GROUP BY up.user, up.language ",
                                                "ORDER BY COUNT(cj.job_id) DESC");
                DataSet dsResult = PerformSQLOperation(sQuery, true);

                if (dsResult.Tables.Count > 0 && dsResult.Tables[0].Rows.Count > 0)
                {
                    FuzibleUserJobs = dsResult.Tables[0].Rows[0]["li_user"].ToString();
                    Language = dsResult.Tables[0].Rows[0]["li_lang"].ToString();
                    FuzibleName = "Fuzible.exe";
                }
                else
                {
                    LoadingError = Languages.Languages.par_client_nouserconfigured;
                    FuzibleUserJobs = "";
                    Language = "EN";
                    FuzibleName = "Fuzible.exe";
                }
            }
            else
            {
                LoadingError = Languages.Languages.par_client_nouserconfigured;
                FuzibleUserJobs = "";
                Language = "EN";
                FuzibleName = "Fuzible.exe";
            }
        }

        public bool SaveClientINI()
        {
            StringBuilder sbINI = new();
            sbINI.AppendLine("SQL_CONNEXION_STRING=" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringClientApp, INI_PASSWORD));
            sbINI.AppendLine("SQL_CONNEXION_STRING_SCHEMA=" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringClientAppSchema, INI_PASSWORD));
            sbINI.AppendLine("SQL_DRIVER=" + FITools.EncryptionSystem.AES_Encrypt(BDDDriverClientApp.ToString(), INI_PASSWORD));
            sbINI.AppendLine("JOBS_USER=" + FITools.EncryptionSystem.AES_Encrypt(FuzibleUserJobs, INI_PASSWORD));
            sbINI.AppendLine("FUZIBLE_APP_NAME=" + FITools.EncryptionSystem.AES_Encrypt(FuzibleName, INI_PASSWORD));
            sbINI.AppendLine("LANGUAGE=" + FITools.EncryptionSystem.AES_Encrypt(Language, INI_PASSWORD));
            sbINI.AppendLine("INTERVAL=" + FITools.EncryptionSystem.AES_Encrypt(FloodingDelay.ToString(), INI_PASSWORD));
            try
            {
                if (!File.Exists(fiClientB.Filepath))
                {
                    FileStream fsClientB = File.Create(fiClientB.Filepath);
                    fsClientB.Close();
                }
                fiClientB.WriteStringToFile(sbINI.ToString(), false);

                if (File.Exists("FuzibleClient.exe"))
                {
                    try { File.Copy("FuzibleClient.exe", fiClientB.Filepath.Replace("CLIENTAPP.INI", "FUZIBLECLIENT.EXE", StringComparison.OrdinalIgnoreCase)); } catch { }
                }
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Microsoft.Data.SqlClient.SNI.dll")))
                {
                    try { File.Copy("Microsoft.Data.SqlClient.SNI.dll", fiClientB.Filepath.Replace("CLIENTAPP.INI", "Microsoft.Data.SqlClient.SNI.dll", StringComparison.OrdinalIgnoreCase)); } catch { }
                }

                try
                {
                    if (!File.Exists(fiClient.Filepath))
                    {
                        FileStream fsClient = File.Create(fiClient.Filepath);
                        fsClient.Close();
                    }
                    fiClient.WriteStringToFile(sbINI.ToString(), false);
                }
                catch { } //si le programme n'arrive pas à écrire dans le répertoire, on aura au moins le fichier dans le commun        

                return true;
            }
            catch (Exception) { throw; }
        }

        private void LoadINIClientApp(string sFileToRead, bool bLoadJobs)
        {
            bool bIsEncrypted = true;

            if (File.Exists(sFileToRead))
            {
                try
                {
                    var fs = new FileStream(sFileToRead, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    StreamReader sr = new(fs, System.Text.Encoding.UTF8);

                    string sRow = "";

                    while (sr.Peek() != -1)
                    {
                        sRow = sr.ReadLine();

                        if (sRow.StartsWith("SQL_DRIVER="))
                        {
                            if (!bIsEncrypted) { BDDDriverClientApp = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sRow[(sRow.IndexOf("=") + 1)..]); }
                            else { BDDDriverClientApp = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD)); }
                        }
                        else if (sRow.StartsWith("SQL_CONNEXION_STRING="))
                        {
                            if (!bIsEncrypted) { SQLConnexionStringClientApp = sRow[(sRow.IndexOf("=") + 1)..]; }
                            else { SQLConnexionStringClientApp = FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD); }
                        }
                        else if (sRow.StartsWith("SQL_CONNEXION_STRING_SCHEMA="))
                        {
                            if (!bIsEncrypted) { SQLConnexionStringClientAppSchema = sRow[(sRow.IndexOf("=") + 1)..]; }
                            else { SQLConnexionStringClientAppSchema = FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD); }
                        }
                        else if (sRow.StartsWith("JOBS_USER="))
                        {
                            if (!bIsEncrypted) { FuzibleUserJobs = sRow[(sRow.IndexOf("=") + 1)..]; }
                            else { FuzibleUserJobs = FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD); }
                        }
                        else if (sRow.StartsWith("FUZIBLE_APP_NAME="))
                        {
                            if (!bIsEncrypted) { FuzibleName = sRow[(sRow.IndexOf("=") + 1)..]; }
                            else { FuzibleName = FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD); }
                        }
                        else if (sRow.StartsWith("LANGUAGE="))
                        {
                            if (!bIsEncrypted) { Language = sRow[(sRow.IndexOf("=") + 1)..]; }
                            else { Language = FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD); }
                        }
                        else if (sRow.StartsWith("INTERVAL="))
                        {
                            if (!bIsEncrypted) { FloodingDelay = Convert.ToInt32(sRow[(sRow.IndexOf("=") + 1)..]); }
                            else { FloodingDelay = Convert.ToInt32(FITools.EncryptionSystem.AES_Decrypt(sRow[(sRow.IndexOf("=") + 1)..], INI_PASSWORD)); }
                        }
                    }
                    sr.Close();
                }
                catch (Exception ex)
                {
                    LoadingError = Languages.Languages.par_client_cantreadini + ex.Message;
                }
            }

            if (bLoadJobs && LoadingError.Length == 0)
            {
                //chargement des informations propres au client
                string sQuery = "SELECT job_id, job_name, job_description, job_password, job_params, job_queries, job_haschildren, job_priority, user_jobs, application_name, job_category " +
                                " FROM " + (SQLConnexionStringClientAppSchema.Length == 0 ? "" : SQLConnexionStringClientAppSchema + ".") + "client_jobs WHERE ((application_name = '" + FuzibleName + "' AND user_jobs = '" + FuzibleUserJobs + "') OR application_name != '" + FuzibleName + "')" +
                                " ORDER BY job_category ASC, application_name ASC, user_jobs ASC;";
                DataSet dsData = PerformSQLOperation(sQuery, true);
                List<ClientJob> sJobs = new();
                if (dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in dsData.Tables[0].Rows)
                    {
                        sJobs.Add(new ClientJob(dr[0].ToString(), dr[1].ToString(), dr[2].ToString(), dr[3].ToString(), dr[4].ToString(), dr[5].ToString(), Convert.ToInt32(dr[6].ToString()), Convert.ToInt32(dr[7].ToString()), dr[8].ToString(), dr[9].ToString(), dr[10].ToString()));
                    }
                    ListOfJobs = sJobs;
                }
            }
        }

        private DataSet PerformSQLOperation(string sQuery, bool bSelect)
        {
            DataSet dsData = new();

            string sResult = "";

            switch (BDDDriverClientApp)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    try
                    {
                        OleDbConnection ConnexionAccess;
                        ConnexionAccess = new OleDbConnection(SQLConnexionStringClientApp);
                        ConnexionAccess.Open();
                        try
                        {
                            OleDbCommand CommandeSQL = new(sQuery, ConnexionAccess);

                            if (bSelect)
                            {
                                OleDbDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionAccess.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    try
                    {
                        MySqlConnection ConnexionSQL;
                        ConnexionSQL = new MySqlConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            MySqlCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                MySqlDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    try
                    {
                        OdbcConnection ConnexionSQL;
                        ConnexionSQL = new OdbcConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            OdbcCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                OdbcDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    try
                    {
                        OracleConnection ConnexionSQL;
                        ConnexionSQL = new OracleConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            OracleCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                OracleDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    try
                    {
                        NpgsqlConnection ConnexionSQL;
                        ConnexionSQL = new NpgsqlConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            NpgsqlCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                NpgsqlDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    try
                    {
                        Microsoft.Data.SqlClient.SqlConnection ConnexionSQL;
                        ConnexionSQL = new Microsoft.Data.SqlClient.SqlConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            if (sQuery.StartsWith("INSERT INTO ", StringComparison.OrdinalIgnoreCase) && sQuery.Trim().EndsWith(";"))
                            {
                                sQuery = string.Concat(sQuery, " SELECT SCOPE_IDENTITY();");
                            }

                            Microsoft.Data.SqlClient.SqlCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                Microsoft.Data.SqlClient.SqlDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData);
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    try
                    {
                        SqliteConnection ConnexionSQL;
                        ConnexionSQL = new SqliteConnection(SQLConnexionStringClientApp);
                        ConnexionSQL.Open();
                        try
                        {
                            SqliteCommand CommandeSQL = new(sQuery, ConnexionSQL);

                            if (bSelect)
                            {
                                SqliteDataAdapter Adaptateur = new(CommandeSQL);
                                Adaptateur.Fill(dsData, "");
                            }
                            else
                            {
                                object oR = CommandeSQL.ExecuteScalar();
                                try { sResult = oR.ToString(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                            //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while executing SQL command [" + sQuery + "] : " + ex.Message));
                            //sr.Close();
                            LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsqlcommand, " [", sQuery, "] : ", ex.Message);
                        }
                        ConnexionSQL.Close();
                    }
                    catch (Exception ex)
                    {
                        //StreamWriter sr = new StreamWriter("ERRORS.TXT", true, Encoding.UTF8);
                        //sr.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", "Error while connecting to distant DB [" + BDDDriverClientApp.ToString() + "] : " + ex.Message));
                        //sr.Close();
                        LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_errorsdistantdb, " [", BDDDriverClientApp.ToString(), "] : ", ex.Message);
                    }
                    break;
            }

            if (!bSelect)
            {
                dsData.Tables.Add("RESULT");
                dsData.Tables[0].Columns.Add("RESULT");
                DataRow dr = dsData.Tables[0].NewRow();
                dr[0] = sResult;
                dsData.Tables[0].Rows.Add(dr);
            }

            return dsData;
        }

        public string CheckJobStatus_SQL(ClientJob cJ)
        {
            //création d'un INI pour se connecter à la BDD
            StringBuilder sbDetail = new();
            string sQuery = string.Concat("SELECT id_stack, dt_stack, dt_requested_execution, li_user, li_status, dt_start, dt_end, li_message" +
                                           " FROM " + (SQLConnexionStringClientAppSchema.Length == 0 ? "" : (SQLConnexionStringClientAppSchema + ".")) + "app_stacklaunch WHERE  id_job = '", cJ.JobID, "'" +
                                            " AND li_configfile = '", cJ.UserJob, "'" +
                                            " AND li_apptolaunch = '", cJ.ApplicationPathAndFile, "'" +
                                            " ORDER BY ", BDDDriverClientApp == SQLTools_Enums.BDD.DB_SQLITE ? "DATETIME(dt_stack)" : "dt_stack", " DESC;");
            DataSet dsResult = PerformSQLOperation(sQuery, true);

            if (dsResult.Tables.Count > 0 && dsResult.Tables[0].Rows.Count > 0)
            {
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_date, dsResult.Tables[0].Rows[0]["dt_stack"].ToString()));
                if (dsResult.Tables[0].Rows[0]["dt_requested_execution"].ToString().Length > 0)
                {
                    sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_datelaunch, dsResult.Tables[0].Rows[0]["dt_requested_execution"].ToString()));
                }
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_user, dsResult.Tables[0].Rows[0]["li_user"]));
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_status, dsResult.Tables[0].Rows[0]["li_status"]));
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_stacktime, dsResult.Tables[0].Rows[0]["dt_stack"].ToString()));
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_starttime, dsResult.Tables[0].Rows[0]["dt_start"].ToString()));
                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_endtime, dsResult.Tables[0].Rows[0]["dt_end"].ToString()));

                string sMessage = dsResult.Tables[0].Rows[0]["li_message"].ToString();
                sMessage = Regex.Replace(sMessage, "(\\[(INF||ERR||WNG||DBG)\\]){1}", Environment.NewLine + "$1");

                sbDetail.AppendLine(string.Concat(Languages.Languages.par_client_request_message, Environment.NewLine + sMessage.Trim()));
            }

            return string.Concat(Languages.Languages.par_client_request_laststatus, Environment.NewLine, sbDetail.ToString());
        }

        public bool CheckRunningJob(ClientJob cJ)
        {
            string sQuery = string.Concat("SELECT COUNT(*) FROM app_stacklaunch WHERE id_job = '", cJ.JobID, "' AND li_configfile = '", cJ.UserJob, "' AND li_apptolaunch = '", cJ.ApplicationPathAndFile, "' AND dt_end IS NULL;");
            DataSet dsResult = PerformSQLOperation(sQuery, true);
            string sResult = SQLTools.BuildStringFromDs(dsResult);
            if (sResult.Equals("0")) { return false; } else { return true; }
        }

        public int CheckFloodingInterval(ClientJob cJ, string sDynamicParams)
        {
            string sQuery = string.Concat("SELECT MAX(", BDDDriverClientApp == SQLTools_Enums.BDD.DB_SQLITE ? "DATETIME(dt_end)" : "dt_end", ") as dt_end" +
                                            " FROM " + (SQLConnexionStringClientAppSchema.Length == 0 ? "" : (SQLConnexionStringClientAppSchema + ".")) + "app_stacklaunch" +
                                            " WHERE id_job = '", cJ.JobID, "'" +
                                            " AND li_configfile = '", cJ.UserJob, "'" +
                                            " AND li_apptolaunch = '", cJ.ApplicationPathAndFile, "'" +
                                            " AND li_arguments = '", sDynamicParams.Replace("'", "''").Trim(), "';");
            DataSet dsMaxDtEnd = PerformSQLOperation(sQuery, true);
            string sMaxDtEnd = SQLTools.BuildStringFromDs(dsMaxDtEnd);

            try
            {
                if (sMaxDtEnd.Length > 0)
                {
                    var dtMax = Convert.ToDateTime(sMaxDtEnd); //datetime dernière fin de job
                    DateTime dtIteration = DateTime.Now.AddMinutes(-FloodingDelay); //datetime actuelle ôtée du délai du flood control
                    if (dtMax <= dtIteration)
                    {
                        return 0;
                    }
                    else
                    {
                        TimeSpan span = dtMax.Subtract(dtIteration);
                        double dDTDiff = span.TotalMinutes;
                        return Convert.ToInt32(dDTDiff);
                    }
                }
                else { return 0; }
            }
            catch (Exception ex) { LoadingError = string.Concat(LoadingError, Environment.NewLine, Languages.Languages.par_client_flood_datetimeconvertko, " (", sMaxDtEnd, ") : ", ex.Message); return 1; }
        }

        public string InsertJobStack(ClientJob cJ, string sDynamicParams, string sPriority, int iHourRequestedLaunch, DateTime dtFormatedRequestedDate)
        {
            string sIP = Toolbox.GetLocalIPAddress();
            string sClientUser = WindowsIdentity.GetCurrent().Name;

            StringBuilder sbQuery = new();
            sbQuery.Append("INSERT INTO " + (SQLConnexionStringClientAppSchema.Length == 0 ? "" : (SQLConnexionStringClientAppSchema + ".")) + "app_stacklaunch");
            if (iHourRequestedLaunch == -1)
            {
                sbQuery.Append(string.Concat(" (dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, id_job, li_job, li_arguments, li_status) VALUES ("));
                sbQuery.Append(string.Concat("'", DateConvert(BDDDriverClientApp, DateTime.Now), "', ", sPriority, ", '", sIP, "', '", sClientUser, "', '", cJ.UserJob, "', '", cJ.ApplicationPathAndFile, "', '", cJ.UserJob, "', '", cJ.JobID, "', '", cJ.JobName.Replace("'", "''"), "', '", sDynamicParams.Replace("'", "''"), "', '", SQLTools_Enums.JOB_STACK_STATUS.REQUESTED.ToString(), "');"));
            }
            else
            {
                sbQuery.Append(string.Concat(" (dt_stack, dt_requested_execution, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, id_job, li_job, li_arguments, li_status) VALUES ("));
                sbQuery.Append(string.Concat("'", DateConvert(BDDDriverClientApp, DateTime.Now), "', '", DateConvert(BDDDriverClientApp, dtFormatedRequestedDate), "', ", sPriority, ", '", sIP, "', '", sClientUser, "', '", cJ.UserJob, "', '", cJ.ApplicationPathAndFile, "', '", cJ.UserJob, "', '", cJ.JobID, "', '", cJ.JobName.Replace("'", "''"), "', '", sDynamicParams.Replace("'", "''"), "', '", SQLTools_Enums.JOB_STACK_STATUS.REQUESTED.ToString(), "');"));
            }
            DataSet dsInsert = PerformSQLOperation(sbQuery.ToString(), false);
            string sId = SQLTools.BuildStringFromDs(dsInsert);
            string sResult = sId;

            //recherche de stack
            if (sId.Length > 0)
            {
                string sQuery = string.Concat("SELECT COUNT(*) FROM " + (SQLConnexionStringClientAppSchema.Length == 0 ? "" : (SQLConnexionStringClientAppSchema + ".")) + "app_stacklaunch WHERE li_configfile = '", cJ.UserJob, "' AND dt_end IS NULL;");
                DataSet dsResult = PerformSQLOperation(sQuery, true);
                sResult = SQLTools.BuildStringFromDs(dsResult);
            }

            if (LoadingError.Length > 0)
            {
                return LoadingError;
            }
            else { return Languages.Languages.par_client_stacked_position + sResult + " (ID : " + sId + ")"; }
        }

        private static string DateConvert(SQLTools_Enums.BDD sBDD, DateTime dtDate)
        {
            string sDate;

            if (Regex.IsMatch(INIProgram.SYSTEM_CULTURE_DATE.ShortDatePattern, "M{1,2}/d{1,2}")) //système US
            {
                if (sBDD == SQLTools_Enums.BDD.DB_SQLSERVER)
                {
                    sDate = dtDate.ToString("MM/dd/yyyy HH:mm:ss");
                }
                else
                {
                    sDate = dtDate.ToString("yyyy-dd-MM HH:mm:ss");
                }
            }
            else
            {
                if (sBDD == SQLTools_Enums.BDD.DB_SQLSERVER)
                {
                    sDate = dtDate.ToString("dd/MM/yyyy HH:mm:ss");
                }
                else
                {
                    sDate = dtDate.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }

            return sDate;
        }

    }

    public class ClientJob
    {
        //job_id, job_name, job_description, job_password, job_params, job_queries, job_haschildren, job_priority, user_jobs, application_name
        public string JobID { get; internal set; } = "";
        public string JobName { get; internal set; } = "";
        public string JobDescription { get; internal set; } = "";
        public string JobPassword { get; internal set; } = "";
        public string JobDynParams { get; internal set; } = "";
        public string JobQueries { get; internal set; } = "";
        public int ChildrenCount { get; internal set; } = 0;
        public int JobPriority { get; internal set; } = 0;
        public string JobCategory { get; internal set; } = "General";
        public string ApplicationPathAndFile { get; internal set; } = "";
        //public string JobCategoryAndApp { get { return string.Concat("Category : ", JobCategory, " (", Path.GetFileNameWithoutExtension(ApplicationPathAndFile), " - Config. : ", UserJob, ")"); } }
        public string JobKey { get { return string.Concat(Path.GetFileNameWithoutExtension(ApplicationPathAndFile), "_", UserJob, "_", JobID); } }
        public string UserJob { get; internal set; } = "";
        public string AppAndUser { get { return string.Concat(Path.GetFileNameWithoutExtension(ApplicationPathAndFile), " (", UserJob, ")"); } }

        public ClientJob(string sID, string sName, string sDescription, string sPassword, string sParams, string sQueries, int iChildren, int iPriority, string sUser, string sApp, string sCategory)
        {
            JobID = sID;
            JobName = sName;
            JobDescription = sDescription;
            JobPassword = sPassword;
            JobDynParams = sParams;
            JobQueries = sQueries;
            ChildrenCount = iChildren;
            JobPriority = iPriority;
            ApplicationPathAndFile = sApp;
            UserJob = sUser;
            JobCategory = sCategory;
        }
    }

}
