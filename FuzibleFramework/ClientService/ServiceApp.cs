using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.TaskScheduler;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace FuzibleFramework
{

    public class ServiceApp
    {
        private static readonly string INI_PASSWORD = SHSConstantes.PROGRAM_PWD;
        private INIProgram INIFile
        {
            get; set;
        }

        //côté service
        public string SQL_STACK_TABLE = "app_stacklaunch";
        public string SQL_PLANIF_TABLE = "app_planifmodel";
        public string SQL_CLIENT_JOBS_TABLE = "client_jobs";
        private static readonly int MAX_MESSAGE = 1000000;

        public SQLTools_Enums.BDD BDDDriverServiceApp { get; set; } = SQLTools_Enums.BDD.DB_SQLITE;
        public string BDDDriverDefaultSchema { get; set; } = "";
        public CONNString SQLConnexionStringServiceApp { get; set; } = new CONNString(SQLTools_Enums.BDD.DB_SQLITE, "[0]", "Internal DB", "Data Source=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db;foreign keys=true");
        public SQLTools SQLConnexionServiceApp
        {
            get; set;
        }

        public int IQteParallelJobs { get; set; } = 4;
        //public string sAppPath { get; set; } = Directory.GetCurrentDirectory();
        public int IKeepLogUntil { get; set; } = 8;
        public int IPurgeIdleJobsAfterHours { get; set; } = 8;
        public string SUsername { get; set; } = Environment.UserName.ToUpper();
        public string ApplicationName { get; set; } = "Fuzible.exe";
        public int FloodingInterval { get; set; } = 15;
        public string SendJobReport { get; set; } = "NONE";
        public int SendJobReport_Hour { get; set; } = 8;
        public string SendJobReport_AmPm { get; set; } = "AM";
        public string SendJobReport_Recipients { get; set; } = "";
        public Query DummyQuery = new Query();


        public ServiceApp(INIProgram iniINIFile)
        {
            INIFile = iniINIFile;
            LoadINIServiceApp();

            //nécessaire pour créer la connexion SQL
            if (INIFile != null)
            {
                Job INIP = new(INIFile.GlobalParameters, "[0]", INIFile.USER, false);
                INIP.SetJobPassword("SHS", "", true);

                if (SQLConnexionStringServiceApp != null)
                {
                    INIP.ConnectionString_Target = SQLConnexionStringServiceApp;
                    LogTools MyLog = new(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, false);
                    SQLConnexionServiceApp = new SQLTools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                }
            }
        }

        private void LoadINIServiceApp()
        {
            bool bIsEncrypted = true;
            DataSet dsData = new();
            try
            {
                string sQuery = string.Concat("select \"sql_driver\", \"sql_connexion_string\", \"sql_connexion_string_schema\", \"parallel_jobs\", \"flooding_interval\", \"keep_log_until\", \"kill_running_jobs_after\", \"user_jobs\", \"send_job_report\", \"send_job_report_hour\", \"send_job_report_ampm\", \"send_job_report_recipients\" FROM service_parameters;");
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Service Params");
                }
                if (dsData != null && dsData.Tables.Count > 0)
                {
                    if (dsData.Tables[0].Rows.Count == 0) //premier arrivé, premier servi : création du service pour lui
                    {
                        //TODO ?
                    }
                    else
                    {
                        DataRow drService = dsData.Tables[0].Rows[0];

                        if (!bIsEncrypted) { BDDDriverServiceApp = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), drService["sql_driver"].ToString()); }
                        else { BDDDriverServiceApp = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), FITools.EncryptionSystem.AES_Decrypt(drService["sql_driver"].ToString(), INI_PASSWORD)); }

                        if (!bIsEncrypted) { BDDDriverDefaultSchema = drService["sql_connexion_string_schema"].ToString(); }
                        else { BDDDriverDefaultSchema = FITools.EncryptionSystem.AES_Decrypt(drService["sql_connexion_string_schema"].ToString(), INI_PASSWORD); }

                        List<string> sListParams = new();
                        sListParams = SQLQueries.ParamsForSQLDriver(BDDDriverServiceApp, BDDDriverDefaultSchema);
                        if (!bIsEncrypted)
                        {
                            SQLConnexionStringServiceApp = new CONNString(BDDDriverServiceApp, "[0]", "", drService["sql_connexion_string"].ToString(), sListParams);
                        }
                        else
                        {
                            SQLConnexionStringServiceApp = new CONNString(BDDDriverServiceApp, "[0]", "", FITools.EncryptionSystem.AES_Decrypt(drService["sql_connexion_string"].ToString(), INI_PASSWORD), sListParams);
                        }

                        if (!bIsEncrypted) { IQteParallelJobs = Convert.ToInt16(drService["parallel_jobs"].ToString()); }
                        else { IQteParallelJobs = Convert.ToInt16(FITools.EncryptionSystem.AES_Decrypt(drService["parallel_jobs"].ToString(), INI_PASSWORD)); }

                        if (!bIsEncrypted) { FloodingInterval = Convert.ToInt16(drService["flooding_interval"].ToString()); }
                        else { FloodingInterval = Convert.ToInt16(FITools.EncryptionSystem.AES_Decrypt(drService["flooding_interval"].ToString(), INI_PASSWORD)); }

                        if (!bIsEncrypted) { IKeepLogUntil = Convert.ToInt16(drService["keep_log_until"].ToString()); }
                        else { IKeepLogUntil = Convert.ToInt16(FITools.EncryptionSystem.AES_Decrypt(drService["keep_log_until"].ToString(), INI_PASSWORD)); }

                        if (!bIsEncrypted) { IPurgeIdleJobsAfterHours = Convert.ToInt16(drService["kill_running_jobs_after"].ToString()); }
                        else
                        {
                            try { IPurgeIdleJobsAfterHours = Convert.ToInt16(FITools.EncryptionSystem.AES_Decrypt(drService["kill_running_jobs_after"].ToString(), INI_PASSWORD)); } catch { IPurgeIdleJobsAfterHours = 8; }
                        }

                        if (!bIsEncrypted) { SUsername = drService["user_jobs"].ToString(); }
                        else { SUsername = FITools.EncryptionSystem.AES_Decrypt(drService["user_jobs"].ToString(), INI_PASSWORD); }

                        if (!bIsEncrypted) { SendJobReport = drService["send_job_report"].ToString(); }
                        else
                        {
                            try { SendJobReport = FITools.EncryptionSystem.AES_Decrypt(drService["send_job_report"].ToString(), INI_PASSWORD); } catch { SendJobReport = "NONE"; }
                            if (SendJobReport.Length == 0) { SendJobReport = "NONE"; }
                        }

                        if (!bIsEncrypted) { SendJobReport_Hour = Convert.ToInt32(drService["send_job_report_hour"].ToString()); }
                        else
                        {
                            string sInt = FITools.EncryptionSystem.AES_Decrypt(drService["send_job_report_hour"].ToString(), INI_PASSWORD);
                            if (sInt.Length > 0)
                            {
                                try { SendJobReport_Hour = Convert.ToInt32(sInt); } catch { SendJobReport_Hour = 8; }
                            }
                            else { SendJobReport_Hour = 8; }
                        }

                        if (!bIsEncrypted) { SendJobReport_AmPm = drService["send_job_report_ampm"].ToString(); }
                        else
                        {
                            try { SendJobReport_AmPm = FITools.EncryptionSystem.AES_Decrypt(drService["send_job_report_ampm"].ToString(), INI_PASSWORD); } catch { SendJobReport_AmPm = "AM"; }
                            if (SendJobReport_AmPm.Length == 0) { SendJobReport_AmPm = "AM"; }
                        }

                        if (!bIsEncrypted) { SendJobReport_Recipients = drService["send_job_report_recipients"].ToString(); }
                        else
                        {
                            try { SendJobReport_Recipients = FITools.EncryptionSystem.AES_Decrypt(drService["send_job_report_recipients"].ToString(), INI_PASSWORD); } catch { SendJobReport_Recipients = ""; }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public List<PlanifModel> GetJobPlanifications(Job INIP)
        {
            List<PlanifModel> pmListJobPlanif = new();

            StringBuilder sbQuery = new();
            sbQuery.Append("SELECT " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_planifmodel" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_description" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_arguments" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "is_active" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIP.JobID + "'");
            sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "'");
            sbQuery.Append(" ORDER BY " + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar + " ASC;");

            DataSet dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);

            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow drP in dsData.Tables[0].Rows)
                {
                    pmListJobPlanif.Add(new PlanifModel(drP["li_configfile"].ToString(), drP["id_job"].ToString(), Convert.ToInt32(drP["id_planifmodel"].ToString()), drP["li_planifmodel"].ToString(), drP["li_description"].ToString(), drP["li_arguments"].ToString(), Convert.ToInt32(drP["is_active"])));
                }
            }

            return pmListJobPlanif;
        }

        public void CreatePlanification(Job INIP, string sPlanifModel, string sDescription, string sArguments, int iIsActive)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("INSERT INTO ");
            sbQuery.Append(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".");
            sbQuery.Append(SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" (id_job, li_planifmodel, li_configfile, li_arguments, li_description, is_active) VALUES ");
            sbQuery.Append(string.Concat("('", INIP.JobID, "', '", sPlanifModel, "', '", INIFile.USER, "', ", sArguments.Length > 0 ? ("'" + sArguments.Trim().Replace("'", "''") + "', '") : "NULL, '", sDescription.Replace("'", "''"), "', ", iIsActive.ToString(), ");"));

            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);
        }

        public void UpdatePlanification(string sPlanifID, string sPlanifModel, string sDescription, string sArguments, int iIsActive)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("UPDATE ");
            sbQuery.Append(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".");
            sbQuery.Append(SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" SET " + SQLConnexionServiceApp.EchappementChar + "li_planifmodel" + SQLConnexionServiceApp.EchappementChar + " = '");
            sbQuery.Append(sPlanifModel);
            sbQuery.Append("', " + SQLConnexionServiceApp.EchappementChar + "li_arguments" + SQLConnexionServiceApp.EchappementChar + " = '");
            sbQuery.Append(sArguments.Replace("'", "''"));
            sbQuery.Append("', " + SQLConnexionServiceApp.EchappementChar + "li_description" + SQLConnexionServiceApp.EchappementChar + " = '");
            sbQuery.Append(sDescription.Replace("'", "''"));
            sbQuery.Append("', " + SQLConnexionServiceApp.EchappementChar + "is_active" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append(iIsActive);
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append(sPlanifID);
            sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "';");

            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);
        }

        public void DeletePlanification(string sPlanifID)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("DELETE FROM ");
            sbQuery.Append(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".");
            sbQuery.Append(SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append(sPlanifID);
            sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "';");

            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);
        }

        public void DeleteAllPlanifsForJob(string sJobID)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("DELETE FROM ");
            sbQuery.Append(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".");
            sbQuery.Append(SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + sJobID + "'");
            sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "';");

            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);
        }

        public void UpdateAllPlanifsForJob(string sOldJobID, string sNewJobID)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("UPDATE ");
            sbQuery.Append(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".");
            sbQuery.Append(SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" SET " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + sNewJobID + "'");
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + sOldJobID + "'");
            sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "';");

            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);
        }

        public async System.Threading.Tasks.Task<DataSet> GetPlanifCalendar(LogTools MyLog, int iWeeks, bool bRemoveEmptyRows, bool bShowInactives)
        {
            DataSet dsCalendar = new("Planifications")
            {
                Namespace = "Planifications"
            };

            await System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                CultureInfo myCI = new("en-US");
                Calendar cCal = myCI.Calendar;

                List<PlanifModel> pmPlanifs = GetListOfPlanifications(bShowInactives);

                Job INILog = new(INIFile.GlobalParameters, "[0]", INIFile.USER, false);
                MyLog.PrepareSQLLog(true, new SQLTools(INILog, SQLTools_Enums.CLASS_PURPOSE.LOG, ref MyLog));
                string sDateFormatLog = GetLocaleLog();

                if (pmPlanifs.Count > 0)
                {
                    DateTime now = DateTime.Now;
                    DateTime dtStart = new(now.Year, now.Month, now.Day, 0, 0, 0);

                    //préparation de la date : lundi de la semaine actuelle, 00h00
                    int diff = (7 + (dtStart.DayOfWeek - DayOfWeek.Monday)) % 7;
                    dtStart = dtStart.AddDays(-1 * diff).Date;

                    for (int iWeek = 0; iWeek < iWeeks; iWeek++)
                    {
                        string sDtName = Languages.Languages.par_planif_week + cCal.GetWeekOfYear(dtStart.AddDays(7 * iWeek), CalendarWeekRule.FirstDay, DayOfWeek.Monday).ToString("00");
                        DataTable dtW = new(sDtName)
                        {
                            Namespace = sDtName
                        };

                        //création des jours de la semaine en abcisse
                        dtW.Columns.Add("-");

                        for (int iD = 0; iD < 7; iD++)
                        {
                            dtW.Columns.Add(string.Concat(dtStart.AddDays(iD).DayOfWeek.ToString(), ", ", dtStart.AddDays(iD).ToShortDateString()));
                        }

                        //création des heures en ordonnée
                        for (int iH = 0; iH < 1440; iH += 5)
                        {
                            DataRow dr = dtW.NewRow();
                            dr[0] = dtStart.AddMinutes(iH).ToShortTimeString();
                            dtW.Rows.Add(dr);
                        }

                        int i = 0;
                        //insertion des planifications prévues
                        foreach (PlanifModel pm in pmPlanifs)
                        {
                            i++;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, string.Concat(Languages.Languages.ma_msg_planifcalendar_processing, " ", i.ToString(), "/", pmPlanifs.Count, " - JOB : ", pm.JobID), SQLTools_Enums.LOG_TYPEINFO.INF);
                            
                            for (int iH = 0; iH < 1440; iH += 5)
                            {
                                for (int iD = 0; iD < 7; iD++)
                                {
                                    bool bPlanified = IsPlanifiedAtDate(pm, dtStart.AddDays(iD).AddMinutes(iH));
                                    if (bPlanified)
                                    {
                                        Job INIP = INIFile.GetJob(pm.JobID);
                                        StringBuilder sbCell = new();
                                        string sActualData = dtW.Rows[iH / 5][iD + 1].ToString();
                                        if (sActualData.Length > 0) //déjà une planif à cette heure
                                        {
                                            sActualData = sActualData.Replace("<font color=\"blue\">", "<font color=\"red\">");
                                            sActualData = sActualData.Replace("<font color=\"orange\">", "<font color=\"red\">");
                                            sbCell.AppendLine(sActualData);
                                            sbCell.Append("<font color=\"red\">");
                                        }
                                        else { sbCell.Append(pm.IsActive == 0 ? "<font color=\"orange\">" : "<font color=\"blue\">"); }
                                        sbCell.AppendLine(string.Concat("- Job : ", INIP.JobNAME));
                                        sbCell.AppendLine(string.Concat("[Planif. : ", pm.Description, "]"));
                                        sbCell.AppendLine(string.Concat("[Args. : ", pm.Arguments, "]"));
                                        sbCell.AppendLine(string.Concat("[Status : ", pm.IsActive == 0 ? "Inactive" : "Active", "]"));


                                        string sLog = GetSQLSimpleLogReport(pm.ConfigFile, pm.JobID, sDateFormatLog, dtStart.AddDays(iD).AddMinutes(iH));
                                        if (sLog.StartsWith(Languages.Languages.par_planif_witherrors, StringComparison.OrdinalIgnoreCase))
                                        {
                                            sbCell.AppendLine(string.Concat("<font color=\"red\">", "[Log : ", sLog, "]", "</font>"));
                                        }
                                        else if (sLog.StartsWith(Languages.Languages.par_planif_running, StringComparison.OrdinalIgnoreCase)) { sbCell.AppendLine(string.Concat("<font color=\"orange\">", "[Log : ", sLog, "]", "</font>")); }
                                        else { sbCell.AppendLine(string.Concat("<font color=\"green\">", "[Log : ", sLog, "]", "</font>")); }

                                        //if (INIP.HasSqlLog)
                                        //{
                                        //    if (MyLog.SQLLog != null)
                                        //    {
                                        //        string sLog = MyLog.GetSQLSimpleLogReport(pm.ConfigFile, pm.JobID, sDateFormatLog, dtStart.AddDays(iD).AddMinutes(iH));
                                        //        if (sLog.StartsWith(Languages.Languages.par_planif_witherrors, StringComparison.OrdinalIgnoreCase))
                                        //        {
                                        //            sbCell.AppendLine(string.Concat("<font color=\"red\">", "[Log : ", sLog, "]", "</font>"));
                                        //        }
                                        //        else if (sLog.StartsWith(Languages.Languages.par_planif_running, StringComparison.OrdinalIgnoreCase)) { sbCell.AppendLine(string.Concat("<font color=\"orange\">", "[Log : ", sLog, "]", "</font>")); }
                                        //        else { sbCell.AppendLine(string.Concat("<font color=\"green\">", "[Log : ", sLog, "]", "</font>")); }
                                        //    }
                                        //}
                                        sbCell.Append("</font>");
                                        dtW.Rows[iH / 5][iD + 1] = sbCell.ToString().Trim();
                                    }
                                }
                            }
                        }

                        if (bRemoveEmptyRows)
                        {
                            List<DataRow> drToRemove = new();
                            foreach (DataRow dr in dtW.Rows)
                            {
                                string[] sDR = new string[dr.Table.Columns.Count - 1];
                                for (int iC = 1; iC < dr.Table.Columns.Count; iC++)
                                {
                                    sDR[iC - 1] = dr[iC].ToString();
                                }
                                if (string.Join("", sDR.ToList()).Length == 0)
                                {
                                    drToRemove.Add(dr);
                                }
                            }
                            int iQte = drToRemove.Count;
                            for (int iR = 0; iR < iQte; iR++)
                            {
                                dtW.Rows.Remove(drToRemove[iR]);
                            }
                        }

                        dsCalendar.Tables.Add(dtW);
                    }
                }
            });

            return dsCalendar;
        }

        public string GetLocaleLog()
        {
            if (SQLConnexionServiceApp != null)
            {
                string s = "";
                if (SQLConnexionServiceApp.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                {
                    s = SQLTools.BuildStringFromDs(SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT DATETIME('1900-12-31 00:00:00');", null, "date"));
                }
                else
                {
                    s = SQLTools.BuildStringFromDs(SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT CAST('1900-12-31 00:00:00' AS DATETIME);", null, "date"));
                }
                
                if (s.Length < 8) //si la requête a crashé
                {
                    return "yyyy-dd-MM HH:mm:ss";
                }
                else
                {
                    return "yyyy-MM-dd HH:mm:ss";
                }
            }
            else
            {
                return "";
            }
        }

        public string ProcessDateTimeNow(SQLTools_Enums.BDD sBDD)
        {
            string sFormatDate = GetLocaleLog();
            string sDate = "";

            if (sFormatDate.Length == 0)
            {
                sDate = Toolbox.SetCleanDate(DateTime.Now.ToString(), SQLConnexionServiceApp.JobParameters, SQLConnexionStringServiceApp, true, true, 2);
            }
            else
            {
                sDate = DateTime.Now.ToString(sFormatDate);
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
            if (SQLConnexionServiceApp.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
            {
                //sQuery = "SELECT dt_job, li_status, dt_end_job" +
                //            " FROM " + LogTables[0] +
                //            " WHERE li_userlaunch = '" + sUser + "'" +
                //            " AND REPLACE(REPLACE(li_job, '[', '-'), ']', '+') LIKE '" + sJobID + "%'" +
                //            " AND (DATETIME(dt_job) >= DATETIME('" + sDtSearch + "') AND DATETIME(dt_job) < DATETIME('" + sDtSearchAdd1Min + "'))";
                sQuery = "SELECT dt_start as dt_job, li_status, dt_end as dt_end_job FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "app_stacklaunch" +
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
                sQuery = "SELECT dt_start as dt_job, li_status, dt_end as dt_end_job FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "app_stacklaunch" +
                                " WHERE li_user = '" + sUser + "'" +
                                " AND id_job = '" + sJobID + "'" +
                                " AND (dt_start >= CAST('" + sDtSearch + "' AS DATETIME) AND dt_start < CAST('" + sDtSearchAdd1Min + "' AS DATETIME))";
            }

            DataSet dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sQuery, DummyQuery, "app_stacklaunch");

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

        public static bool IsPlanifiedAtDate(PlanifModel pm, DateTime dt)
        {
            string sMin = dt.Minute.ToString("00");
            string sHour = dt.Hour.ToString("00");
            string sDayWeek = ((int)dt.DayOfWeek).ToString("00");
            string sDayMonth = dt.Day.ToString("00");
            string sWeek = Toolbox.GetWeekNumberOfMonth(dt).ToString("00");
            string sMonth = dt.Month.ToString("00");

            bool bOK = false;
            string[] sSplitPlanif = pm.PlanifPattern.Split(Convert.ToChar("-"));
            if (sSplitPlanif.Length == 6)
            {
                switch (sSplitPlanif[0])
                {
                    //modèle jours de la semaine
                    case "1":
                        if (PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[1]).Contains(sMin)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[2]).Contains(sHour)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[3]).Contains(sDayWeek)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[4]).Contains(sWeek)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[5]).Contains(sMonth))
                        {
                            bOK = true;
                        }
                        break;
                    //modèle jours du mois
                    case "2":
                        if (PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[1]).Contains(sMin)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[2]).Contains(sHour)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[3]).Contains(sDayMonth)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[4]).Contains(sWeek)
                              && PlanifModel.GetPlanifElementsFromPattern(sSplitPlanif[5]).Contains(sMonth))
                        {
                            bOK = true;
                        }
                        break;
                }
            }
            return bOK;
        }

        public List<PlanifModel> GetListOfPlanifications(bool bWithInactives)
        {
            List<PlanifModel> pmJobsPlanifs = new();
            StringBuilder sbQuery = new();
            sbQuery.Append("SELECT " + SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_planifmodel" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_description" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "li_arguments" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append("," + SQLConnexionServiceApp.EchappementChar + "is_active" + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQLConnexionServiceApp.EchappementChar + SQL_PLANIF_TABLE + SQLConnexionServiceApp.EchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionServiceApp.EchappementChar + "li_configfile" + SQLConnexionServiceApp.EchappementChar + " = ");
            sbQuery.Append("'" + INIFile.USER + "'");
            if (!bWithInactives)
            {
                sbQuery.Append(" AND " + SQLConnexionServiceApp.EchappementChar + "is_active" + SQLConnexionServiceApp.EchappementChar);
                sbQuery.Append(" = 1 ");
            }
            sbQuery.Append(" ORDER BY " + SQLConnexionServiceApp.EchappementChar + "id_planifmodel" + SQLConnexionServiceApp.EchappementChar + " ASC;");

            DataSet dsJobs = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sbQuery.ToString(), DummyQuery, SQL_PLANIF_TABLE);

            if (dsJobs != null && dsJobs.Tables.Count > 0 && dsJobs.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow drP in dsJobs.Tables[0].Rows)
                {
                    pmJobsPlanifs.Add(new PlanifModel(drP["li_configfile"].ToString(), drP["id_job"].ToString(), Convert.ToInt32(drP["id_planifmodel"].ToString()), drP["li_planifmodel"].ToString(), drP["li_description"].ToString(), drP["li_arguments"].ToString(), Convert.ToInt32(drP["is_active"].ToString())));
                }

            }

            return pmJobsPlanifs;
        }

        public static string CheckForValidPlanification(string sElements)
        {
            StringBuilder sbResult = new();

            string[] sSplitPlanif = sElements.Split(Convert.ToChar("-"));
            if (sSplitPlanif.Length == 6)
            {
                if (sSplitPlanif[1].Length == 0) { sbResult.AppendLine(Languages.Languages.par_planif_missingminutes); }
                if (sSplitPlanif[2].Length == 0) { sbResult.AppendLine(Languages.Languages.par_planif_missinghours); }
                if (sSplitPlanif[3].Length == 0) { sbResult.AppendLine(Languages.Languages.par_planif_missingdays); }
                if (sSplitPlanif[4].Length == 0) { sbResult.AppendLine(Languages.Languages.par_planif_missingweeks); }
                if (sSplitPlanif[5].Length == 0) { sbResult.AppendLine(Languages.Languages.par_planif_missingmonths); }
            }
            else { sbResult.AppendLine(Languages.Languages.par_planif_invalidlength); }

            return sbResult.ToString().Trim();
        }

        public string CheckJobCollisions(List<PlanifModel> sPlanifications, string sCurrentPlanifPattern, string sCurrentPlanifID)
        {
            StringBuilder sbCheck = new();

            string[] sToCheck = sCurrentPlanifPattern.Split(Convert.ToChar("-"));

            foreach (PlanifModel pm in sPlanifications)
            {
                if (sCurrentPlanifID == null || (sCurrentPlanifID != null && !pm.PlanifID.ToString().Equals(sCurrentPlanifID)))
                {
                    string[] sExisting = pm.PlanifPattern.Split(Convert.ToChar("-"));
                    int iCheck = 0;
                    foreach (string sP in PlanifModel.GetPlanifElementsFromPattern(sToCheck[1]))
                    {
                        if (PlanifModel.GetPlanifElementsFromPattern(sExisting[1]).Contains(sP)) { iCheck++; break; }
                    }
                    foreach (string sP in PlanifModel.GetPlanifElementsFromPattern(sToCheck[2]))
                    {
                        if (PlanifModel.GetPlanifElementsFromPattern(sExisting[2]).Contains(sP)) { iCheck++; break; }
                    }
                    foreach (string sP in PlanifModel.GetPlanifElementsFromPattern(sToCheck[3]))
                    {
                        if (PlanifModel.GetPlanifElementsFromPattern(sExisting[3]).Contains(sP)) { iCheck++; break; }
                    }
                    foreach (string sP in PlanifModel.GetPlanifElementsFromPattern(sToCheck[4]))
                    {
                        if (PlanifModel.GetPlanifElementsFromPattern(sExisting[4]).Contains(sP)) { iCheck++; break; }
                    }
                    foreach (string sP in PlanifModel.GetPlanifElementsFromPattern(sToCheck[5]))
                    {
                        if (PlanifModel.GetPlanifElementsFromPattern(sExisting[5]).Contains(sP)) { iCheck++; break; }
                    }
                    if (iCheck == 5)
                    {
                        sbCheck.AppendLine(string.Concat(Languages.Languages.par_planif_collisionjob, INIFile != null ? INIFile.GetJob(pm.JobID).JobNAME : pm.JobID, Languages.Languages.par_planif_collisionjobmodel, pm.Description, " (Status : ", pm.IsActive == 0 ? "Inactive" : "Active", ")")); ;
                    }
                }
            }
            return sbCheck.ToString();
        }

        public void SaveServiceParameters(string sUser)
        {
            StringBuilder sbINI = new();

            try
            {
                DataSet dsData = new();
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new("SELECT COUNT(*) FROM service_parameters;", sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Service Params");
                }
                string sResult = SQLTools.BuildStringFromDs(dsData);

                if (sResult.Equals("0"))
                {
                    sbINI.Append("INSERT INTO service_parameters (\"sql_connexion_string\", \"sql_connexion_string_schema\", \"sql_driver\", \"parallel_jobs\", \"flooding_interval\", \"keep_log_until\", \"kill_running_jobs_after\", \"user_jobs\", \"send_job_report\", \"send_job_report_hour\", \"send_job_report_ampm\", \"send_job_report_recipients\") VALUES (");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringServiceApp.SConnString(null), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(BDDDriverServiceApp.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(IQteParallelJobs.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(FloodingInterval.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(IKeepLogUntil.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(IPurgeIdleJobsAfterHours.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SUsername, INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport, INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_Hour.ToString(), INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_AmPm, INI_PASSWORD) + "',");
                    sbINI.Append("'" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_Recipients, INI_PASSWORD) + "');");

                    using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sbINI.ToString(), sqlConn);
                    sqlCommand.ExecuteNonQuery();
                }
                else
                {
                    //check qu'on est bien l'user qui gère l'application service
                    using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new("SELECT COUNT(*) FROM service_parameters WHERE \"user_jobs\" = '" + FITools.EncryptionSystem.AES_Encrypt(SUsername, INI_PASSWORD) + "';", sqlConn);
                        SqliteDataAdapter SQLAdapter = new(sqlCommand);
                        SQLAdapter.Fill(dsData, "Service Params User");
                    }
                    sResult = SQLTools.BuildStringFromDs(dsData);

                    if (sResult.Equals("1"))
                    {
                        sbINI.Append("UPDATE service_parameters ");
                        sbINI.Append("SET \"sql_connexion_string\" = '" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringServiceApp.SConnString(null), INI_PASSWORD) + "',");
                        sbINI.Append("\"sql_connexion_string_schema\" = '" + FITools.EncryptionSystem.AES_Encrypt(SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA), INI_PASSWORD) + "',");
                        sbINI.Append("\"sql_driver\" = '" + FITools.EncryptionSystem.AES_Encrypt(BDDDriverServiceApp.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"parallel_jobs\" = '" + FITools.EncryptionSystem.AES_Encrypt(IQteParallelJobs.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"flooding_interval\" = '" + FITools.EncryptionSystem.AES_Encrypt(FloodingInterval.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"keep_log_until\" = '" + FITools.EncryptionSystem.AES_Encrypt(IKeepLogUntil.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"kill_running_jobs_after\" = '" + FITools.EncryptionSystem.AES_Encrypt(IPurgeIdleJobsAfterHours.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"user_jobs\" = '" + FITools.EncryptionSystem.AES_Encrypt(SUsername, INI_PASSWORD) + "',");
                        sbINI.Append("\"send_job_report\" = '" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport, INI_PASSWORD) + "',");
                        sbINI.Append("\"send_job_report_hour\" = '" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_Hour.ToString(), INI_PASSWORD) + "',");
                        sbINI.Append("\"send_job_report_ampm\" = '" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_AmPm, INI_PASSWORD) + "',");
                        sbINI.Append("\"send_job_report_recipients\" = '" + FITools.EncryptionSystem.AES_Encrypt(SendJobReport_Recipients, INI_PASSWORD) + "';");

                        using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sbINI.ToString(), sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { throw; }

            sbINI.Clear();

            //recréation de la connexion si elle a changé
            Job INIP = new(INIFile.GlobalParameters, "[0]", INIFile.USER, false);
            INIP.SetJobPassword("SHS", "", true);
            INIP.ConnectionString_Target = SQLConnexionStringServiceApp;
            LogTools MyLog = new(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, false);
            SQLConnexionServiceApp = new SQLTools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
        }

        public bool SaveJobToClient(List<Job> sListJobs, LogTools MyLog)
        {
            bool bOK = false;

            try
            {
                if (SQLConnexionStringServiceApp != null && SQLConnexionStringServiceApp.SConnString(null).Length > 0 && SQLConnexionServiceApp != null && INIFile != null)
                {
                    if (SQLConnexionServiceApp.QuickConnectionCheck())
                    {
                        bool bExists = SQLConnexionServiceApp.CheckForExistingTable("client_jobs", DummyQuery);

                        if (bExists)
                        {
                            List<string[]> sAllQueries = INIFile.GetAllJobsQueries();

                            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, "DELETE FROM client_jobs WHERE user_jobs = '" + INIFile.USER + "' AND application_name = '" + INIProgram.APP_NAME + "';", DummyQuery, "client_jobs");

                            StringBuilder sbQuery = new();

                            foreach (Job J in sListJobs)
                            {
                                if (J.IsJobVisibleInClientApp && !J.Job_IsSubJob)
                                {
                                    string sQueries = "";
                                    if (sAllQueries.Any(s => s[0].Equals(J.JobID)))
                                    {
                                        sQueries = sAllQueries.First(s => s[0].Equals(J.JobID))[1].Replace("'", "''");
                                    }

                                    sbQuery.Append("INSERT INTO " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "client_jobs (user_jobs, job_id, job_name, job_description, job_params, job_queries, job_haschildren, job_password, job_priority, application_name, job_category) VALUES (");
                                    sbQuery.Append("'" + INIFile.USER + "',");
                                    sbQuery.Append("'" + J.JobID + "',");
                                    sbQuery.Append("'" + J.JobNAME.Replace("'", "''") + "',");
                                    sbQuery.Append("'" + J.JobDescription.Replace("'", "''") + "',");
                                    sbQuery.Append("'" + string.Join(";", J.DynParams).Replace("'", "''") + "',");
                                    sbQuery.Append("'" + sQueries + "',");
                                    sbQuery.Append("'" + INIFile.GetChildrenCount(J.RawJobID).Count.ToString() + "',");
                                    sbQuery.Append("'" + J.JobPassword + "',");
                                    sbQuery.Append("'" + J.JobPriority + "',");
                                    sbQuery.Append("'" + INIProgram.APP_NAME + "',");
                                    sbQuery.Append("'" + J.JobCategory + "');");
                                }
                            }
                            if (sbQuery.ToString().Trim().Length > 0)
                            {
                                try
                                {
                                    SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQuery.ToString(), DummyQuery, "client_jobs");
                                }
                                catch { bOK = false; }
                            }
                            bOK = true;
                        }
                        else { return true; }
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, new Exception(Languages.Languages.ma_msg_saveclientjobs_ko), SQLConnexionServiceApp.Connection.SConnDriverFriendlyName, SQLTools_Enums.LOG_TYPEINFO.ERR);
                        bOK = false;
                    }
                }
                else { bOK = true; }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, ex, Languages.Languages.ma_msg_saveclientjobs_ko + " - " + SQLConnexionServiceApp.Connection.SConnDriverFriendlyName, SQLTools_Enums.LOG_TYPEINFO.ERR);
                bOK = false;
            }

            return bOK;
        }

        public string MigrateServiceData(CONNString csNewConnString, string sNewUser)
        {
            Job JM = new(INIFile.GlobalParameters, "[0]", sNewUser, false)
            {
                ConnectionString_Source = SQLConnexionStringServiceApp,
                ConnectionString_Target = csNewConnString,
                JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT,
                TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.FULL_DELETE,
                Threads_Source = 1,
                Threads_Target = 1
            };

            LogTools MyLog = new(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, false);

            SQLTools SQLConn = new(JM, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

            //création des tables dans le nouvel environnement
            CreateServiceAppTables(SQLConn, false);

            if (SQLConn.MyLog.HasNoErrors && SQLConn.MyLog.JobWarnings == 0)
            {
                string sOldPlanifTable = (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQL_PLANIF_TABLE;
                string sOldStackTable = (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQL_STACK_TABLE;
                List<Query> QMigration = new()
                {
                    new Query(JM, SQL_STACK_TABLE + ":SELECT dt_stack,nb_priority,li_machine,li_user,li_user_exec,li_apptolaunch,'" + sNewUser + "' as li_configfile,li_job,li_arguments,li_status,dt_start,dt_end,li_message,id_job,dt_requested_execution FROM " + sOldStackTable),
                    new Query(JM, SQL_PLANIF_TABLE + ":SELECT li_planifmodel,id_job,'" + sNewUser + "' as li_configfile,li_arguments,li_description,is_active FROM " + sOldPlanifTable)
                };
                //note : je ne migre pas les jobs car le nouvel utilisateur n'a pas les mêmes que l'ancien
                //QMigration.Add(new Query(JM, SQL_CLIENT_JOBS_TABLE + ":SELECT '" + sNewUser + "' as user_jobs,job_id,job_name,job_description,job_params,job_queries,job_haschildren,job_password,job_priority, application_name FROM " + SQL_CLIENT_JOBS_TABLE));

                //migration des données
                MThread mtClass = new(JM, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, QMigration.Count, 0);

                if (mtClass.QuantityOfThreadsToCompute > 0)
                {
                    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                    {
                        int numeroThread = numThread;
                        System.Threading.Tasks.Task th = new(() => mtClass.RepSyncTask(numeroThread, QMigration, SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY, false));
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
            }
            StringBuilder sbLog = new();
            foreach (LogObject lO in MyLog.LogEvents)
            {
                sbLog.AppendLine(lO.ToLightString());
            }

            return sbLog.ToString();
        }

        public string CreateServiceAppTables(SQLTools SQLConn, bool bClearLog)
        {
            string sTableStack = SQL_STACK_TABLE;
            string sTablePlanif = SQL_PLANIF_TABLE;
            string sTableJobs = SQL_CLIENT_JOBS_TABLE;
            string sSchema = SQLConn.Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA);

            StringBuilder sbReturn = new();

            bool bTableStack;
            bool bTablePlanif;
            bool btableJobs;
            bTableStack = SQLConn.CheckForExistingTable(sTableStack, DummyQuery);
            bTablePlanif = SQLConn.CheckForExistingTable(sTablePlanif, DummyQuery);
            btableJobs = SQLConn.CheckForExistingTable(sTableJobs, DummyQuery);

            if (!btableJobs)
            {
                string sReturn = SQLQueries.CreateTableClientJobs(SQLConn, sTableJobs, sSchema, bClearLog);
                sbReturn.AppendLine(sReturn);

            }
            else { sbReturn.AppendLine(Languages.Languages.par_createtables_client + sTableJobs + Languages.Languages.par_createtables_alreadycreated); }


            if (!bTableStack)
            {
                string sReturn = SQLQueries.CreateTableStackLauncher(SQLConn, sTableStack, sSchema, bClearLog);
                sbReturn.AppendLine(sReturn);
            }
            else { sbReturn.AppendLine(Languages.Languages.par_createtables_stack + sTableStack + Languages.Languages.par_createtables_alreadycreated); }

            if (!bTablePlanif)
            {
                string sReturn = SQLQueries.CreateTablePlanifModels(SQLConn, sTablePlanif, sSchema, bClearLog);
                sbReturn.AppendLine(sReturn);
            }
            else { sbReturn.AppendLine(Languages.Languages.par_createtables_planif + sTablePlanif + Languages.Languages.par_createtables_alreadycreated); }

            return sbReturn.ToString();
        }

        public static string CreateLogTables(SQLTools SQLConn, bool bClearLog)
        {
            List<string> sLogTables = SQLConn.MyLog.LogTables;

            StringBuilder sbReturn = new();

            bool bTableLogEnt;
            bTableLogEnt = SQLConn.CheckForExistingTable(sLogTables[0], new Query());

            if (!bTableLogEnt)
            {
                string sReturn = SQLQueries.CreateTablesLog(SQLConn, sLogTables, bClearLog);
                sbReturn.AppendLine(sReturn);

            }
            else { sbReturn.AppendLine(Languages.Languages.par_createtables_log + sLogTables[0] + "," + sLogTables[1] + Languages.Languages.par_createtables_alreadycreated); }

            return sbReturn.ToString();
        }

        public string PurgeHistoStack()
        {
            DataSet dsData = new();
            switch (SQLConnexionServiceApp.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE DateAdd(\"d\", -", IKeepLogUntil.ToString(), ", Now()) < dt_end;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_MYSQL:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE DATE_ADD(SYSDATE(), INTERVAL -", IKeepLogUntil.ToString(), " DAY) < dt_end;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;

                case SQLTools_Enums.BDD.DB_ORACLE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE dt_end < (SYSDATE - ", IKeepLogUntil.ToString(), ");"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_POSTGRE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE dt_end < CURRENT_DATE - INTERVAL '", IKeepLogUntil.ToString(), " day';"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE dt_end < DATEADD(day, -", IKeepLogUntil.ToString(), ",GETDATE());"), DummyQuery, SQL_STACK_TABLE);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " WHERE DATE(dt_end) < date('now', '-", IKeepLogUntil.ToString(), " day');"), DummyQuery, SQL_STACK_TABLE);
                    break;
            }
            return "[INF] Purge OK : " + SQLTools.BuildStringFromDs(dsData) + Languages.Languages.par_purgestack_rows;
        }

        public string KillIdleJobs()
        {
            DataSet dsData = new();
            switch (SQLConnexionServiceApp.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = Now(), li_status = 'KILLED' WHERE DateAdd(\"d\", -", IPurgeIdleJobsAfterHours.ToString(), ", Now()) < dt_start AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_MYSQL:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = SYSDATE(), li_status = 'KILLED' WHERE DATE_ADD(SYSDATE(), INTERVAL -", IPurgeIdleJobsAfterHours.ToString(), " HOUR) < dt_start AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_ODBC:
                    //TODO
                    break;

                case SQLTools_Enums.BDD.DB_ORACLE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = SYSDATE, li_status = 'KILLED' WHERE dt_start < SYSDATE - INTERVAL '", IPurgeIdleJobsAfterHours.ToString(), " hour' AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_POSTGRE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = CURRENT_DATE, li_status = 'KILLED' WHERE dt_start < CURRENT_DATE - INTERVAL '", IPurgeIdleJobsAfterHours.ToString(), " hour' AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;

                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = GETDATE(), li_status = 'KILLED' WHERE dt_start < DATEADD(hour, -", IPurgeIdleJobsAfterHours.ToString(), ",GETDATE()) AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    dsData = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, string.Concat("UPDATE ", (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."), SQL_STACK_TABLE, " SET dt_end = DATETIME('now'), li_status = 'KILLED' WHERE DATETIME(dt_start) < date('now', '-", IPurgeIdleJobsAfterHours.ToString(), " hour') AND dt_end IS NULL;"), DummyQuery, SQL_STACK_TABLE);
                    break;
            }
            return "[INF] Auto-Cleanup OK : " + SQLTools.BuildStringFromDs(dsData) + Languages.Languages.par_purgestack_rows;
        }

        public string SearchAndInsertPlanifiedJobs()
        {
            StringBuilder sbLog = new();

            List<PlanifModel> pmListJobPlanif = GetListOfPlanifications(false);

            foreach (PlanifModel pm in pmListJobPlanif)
            {
                bool bOK = IsPlanifiedAtDate(pm, DateTime.Now);
                if (bOK)
                {
                    sbLog.AppendLine(Languages.Languages.par_stack_insertplanified + pm.JobID + ")");

                    string sDate = ProcessDateTimeNow(SQLConnexionStringServiceApp.SConnDriver);

                    StringBuilder sbQueryInsert = new();
                    sbQueryInsert.Append("INSERT INTO " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "app_stacklaunch");
                    sbQueryInsert.Append(string.Concat(" (dt_stack, nb_priority, li_machine, li_user, li_user_exec, li_apptolaunch, li_configfile, id_job, li_job, li_arguments, li_status) VALUES ("));
                    sbQueryInsert.Append(string.Concat(sDate, ", ", 1, ", '", Toolbox.GetLocalIPAddress(), "', '" + INIFile.USER + "', '" + INIFile.USER + "', '", ApplicationName, "', '", SUsername, "', '", pm.JobID, "', '", pm.Description, "', '", pm.Arguments, "', '", SQLTools_Enums.JOB_STACK_STATUS.REQUESTED.ToString(), "');"));

                    //insert
                    SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sbQueryInsert.ToString(), DummyQuery, "app_stacklaunch");

                    //position
                    DataSet dsResultB;
                    dsResultB = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, string.Concat("SELECT COUNT(*) FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "" : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "app_stacklaunch WHERE ", SQLConnexionStringServiceApp.SqlEchappementChar, "dt_end", SQLConnexionStringServiceApp.SqlEchappementChar, " IS NULL;"), DummyQuery, "app_stacklaunch");
                    string sIdResultB = SQLTools.BuildStringFromDs(dsResultB);

                    LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, Languages.Languages.par_stack_insertplanified_stacked + sIdResultB, SQLTools_Enums.LOG_TYPEINFO.DET, false);

                }
            }
            return sbLog.ToString().Trim();
        }

        public void AddStartMessageInBDDFromExecutedJob(string sId)
        {
            StringBuilder sbQuery = new();
            //process en cours
            sbQuery.Append("UPDATE ");
            sbQuery.Append((SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."));
            sbQuery.Append(SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar);
            sbQuery.Append(" SET li_status = '");
            sbQuery.Append(SQLTools_Enums.JOB_STACK_STATUS.RUNNING.ToString());
            sbQuery.Append("', dt_start = ");
            sbQuery.Append(ProcessDateTimeNow(SQLConnexionStringServiceApp.SConnDriver));
            sbQuery.Append(" WHERE id_stack = ");
            sbQuery.Append(sId);
            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);

        }

        public void AddMessageInBDDFromExecutedJob(string sId, string sMessage)
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("UPDATE ");
            sbQuery.Append((SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."));
            sbQuery.Append(SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar);
            sbQuery.Append(" SET " + SQLConnexionStringServiceApp.SqlEchappementChar + "li_message" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            sbQuery.Append("'" + sMessage.Replace("'", "''") + "'");
            sbQuery.Append(" WHERE " + SQLConnexionStringServiceApp.SqlEchappementChar + "id_stack" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            sbQuery.Append(sId);
            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);
        }

        public void AddEndMessageInBDDFromExecutedJob(string sId, string sMessage)
        {
            StringBuilder sbQuery = new();
            //process terminé
            sbQuery.Append("UPDATE ");
            sbQuery.Append((SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."));
            sbQuery.Append(SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar);
            sbQuery.Append(" SET li_status = '");
            sbQuery.Append(SQLTools_Enums.JOB_STACK_STATUS.FINISHED.ToString());
            sbQuery.Append("', dt_end = ");
            sbQuery.Append(ProcessDateTimeNow(SQLConnexionStringServiceApp.SConnDriver));
            sbQuery.Append(", li_message = ");
            if (sMessage.Length > MAX_MESSAGE) { sMessage = sMessage[..(MAX_MESSAGE - 1)]; }
            sbQuery.Append(sMessage.Length > 0 ? ("'" + sMessage.Replace("'", "''") + "'") : "NULL");
            sbQuery.Append(" WHERE id_stack = ");
            sbQuery.Append(sId);
            SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);

        }

        public static string GetServiceAppUser(string sInternalDB)
        {
            DataSet dsData = new();
            //récupération du nom de l'utilisateur "service"
            string sQuery = string.Concat("select MIN(\"user_jobs\") FROM service_parameters;");
            using (SqliteConnection sqlConn = new(sInternalDB))
            {
                sqlConn.Open();
                using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                SQLAdapter.Fill(dsData, "Service Params");
            }
            return SQLTools.BuildStringFromDs(dsData);

        }

        public DataSet GetWaitingList()
        {
            StringBuilder sbQuery = new();
            //recherche des jobs qui sont en mode "DEMANDE (not running, not finished)
            sbQuery.Clear();
            sbQuery.Append("SELECT id_stack,dt_stack,dt_requested_execution,nb_priority,li_machine,li_user,li_user_exec,li_apptolaunch,li_configfile,id_job,li_job,li_arguments,li_status,dt_start,dt_end, li_message FROM ");
            sbQuery.Append((SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."));
            sbQuery.Append(SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_end" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL AND " + SQLConnexionStringServiceApp.SqlEchappementChar + "li_status" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            sbQuery.Append("'" + SQLTools_Enums.JOB_STACK_STATUS.REQUESTED.ToString() + "'");
            sbQuery.Append(" AND ((" + SQLConnexionStringServiceApp.SqlEchappementChar + "li_configfile" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            sbQuery.Append("'" + SUsername + "' AND " + SQLConnexionStringServiceApp.SqlEchappementChar + "li_apptolaunch" + SQLConnexionStringServiceApp.SqlEchappementChar + " = '" + INIProgram.APP_NAME + "') OR (" + SQLConnexionStringServiceApp.SqlEchappementChar + "li_apptolaunch" + SQLConnexionStringServiceApp.SqlEchappementChar + " != '" + INIProgram.APP_NAME + "'))");
            switch (SQLConnexionServiceApp.Connection.SConnDriver)
            {
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sbQuery.Append(" AND (" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL OR " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " < Now())");
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sbQuery.Append(" AND (" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL OR " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " < NOW())");
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sbQuery.Append(" AND (" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL OR " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " < CURRENT_TIMESTAMP)");
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sbQuery.Append(" AND (" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL OR " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " < GETDATE())");
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sbQuery.Append(" AND (" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL OR DATETIME(" + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + ") < DATETIME('now'))");
                    break;
                default:
                    sbQuery.Append(" AND 1 = 1");
                    break;
            }
            sbQuery.Append(" ORDER BY " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_requested_execution" + SQLConnexionStringServiceApp.SqlEchappementChar + " DESC, " + SQLConnexionStringServiceApp.SqlEchappementChar + "nb_priority" + SQLConnexionStringServiceApp.SqlEchappementChar + " ASC, " + SQLConnexionStringServiceApp.SqlEchappementChar + "id_stack" + SQLConnexionStringServiceApp.SqlEchappementChar + " ASC;");

            DataSet dsJobs = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);

            return dsJobs;
        }

        public int CheckQuantityOfRunningJobs()
        {
            StringBuilder sbQuery = new();
            sbQuery.Append("SELECT COUNT(*) FROM ");
            sbQuery.Append((SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + "."));
            sbQuery.Append(SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar);
            sbQuery.Append(" WHERE " + SQLConnexionStringServiceApp.SqlEchappementChar + "dt_end" + SQLConnexionStringServiceApp.SqlEchappementChar + " IS NULL");
            sbQuery.Append(" AND " + SQLConnexionStringServiceApp.SqlEchappementChar + "li_status" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            sbQuery.Append("'" + SQLTools_Enums.JOB_STACK_STATUS.RUNNING.ToString() + "';");
            //sbQuery.Append(" AND " + SQLConnexionStringServiceApp.SqlEchappementChar + "li_configfile" + SQLConnexionStringServiceApp.SqlEchappementChar + " = ");
            //sbQuery.Append("'" + SUsername + "';");
            DataSet dsJobs = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);
            string sResult = SQLTools.BuildStringFromDs(dsJobs);
            _ = int.TryParse(sResult, out int iResult);
            return iResult;
        }

        public DataSet CheckAbnormalRunningJobs()
        {
            string sFields = SQLConnexionServiceApp.EchappementChar + "id_job" + SQLConnexionServiceApp.EchappementChar + " as JobId, " +
                SQLConnexionServiceApp.EchappementChar + "dt_stack" + SQLConnexionServiceApp.EchappementChar + " as Date_Stack, " +
                SQLConnexionServiceApp.EchappementChar + "li_user" + SQLConnexionServiceApp.EchappementChar + " as From_User, " +
                SQLConnexionServiceApp.EchappementChar + "li_job" + SQLConnexionServiceApp.EchappementChar + " as Planification, " +
                SQLConnexionServiceApp.EchappementChar + "li_arguments" + SQLConnexionServiceApp.EchappementChar + " as With_Params, " +
                SQLConnexionServiceApp.EchappementChar + "dt_start" + SQLConnexionServiceApp.EchappementChar + " as Date_Start, " +
                SQLConnexionServiceApp.EchappementChar + "li_message" + SQLConnexionServiceApp.EchappementChar + " as Messages";
            DataSet dsRunning = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT " + sFields + " FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar + " WHERE li_status = 'RUNNING'", DummyQuery, "RUNNING JOBS");
            DataSet dsRequested = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, "SELECT " + sFields + " FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + SQLConnexionStringServiceApp.SqlEchappementChar + SQL_STACK_TABLE + SQLConnexionStringServiceApp.SqlEchappementChar + " WHERE li_status = 'REQUESTED'", DummyQuery, "REQUESTED JOBS");

            try
            {
                if (dsRunning != null && dsRunning.Tables.Count > 0 && dsRunning.Tables[0].Rows.Count > 0 &&
                    dsRequested != null && dsRequested.Tables.Count > 0 && dsRequested.Tables[0].Rows.Count > 0)
                {
                    DataSet dsData = new("RunningRequested");
                    dsData.Tables.Add(dsRunning.Tables[0].Copy());
                    dsData.Tables.Add(dsRequested.Tables[0].Copy());
                    return dsData;
                }
                else { return null; }
            }
            catch { return null; }
        }

        public Tuple<string, DateTime, DateTime> BuildExecutionReport(int iTime, string sHeader)
        {
            //conversion jour -> heures en tenant en compte l'offset
            DateTime dtNow = DateTime.Now;
            DateTime dtLastDay = dtNow.Date.AddDays(-iTime);

            TimeSpan diffDay = dtNow - dtLastDay;
            int iHours = (int)diffDay.TotalHours;

            DataSet dsExecuted = new("Job Executions");

            StringBuilder sbQuery = new();
            sbQuery.AppendLine("SELECT * ");
            sbQuery.AppendLine(" FROM " + (SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? " " : SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA) + ".") + "app_stacklaunch");

            if (iTime == 24) //les 24 dernières heures glissantes (last24)
            {
                //on aura la liste de tout ce qui a été exécuté depuis les dernières 24 heures
                switch (SQLConnexionServiceApp.Connection.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sbQuery.AppendLine(" WHERE dt_start >= DateAdd(\"h\", -24, Now());");
                        break;

                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sbQuery.AppendLine(" WHERE dt_start >= NOW() - INTERVAL 24 HOUR;");
                        break;

                    case SQLTools_Enums.BDD.DB_ODBC:
                        //TODO
                        break;

                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sbQuery.AppendLine(" WHERE dt_start >= SYSDATE - INTERVAL '24' HOUR;");
                        break;

                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sbQuery.AppendLine(" WHERE dt_start >= NOW() - INTERVAL '24 hours';");
                        break;

                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sbQuery.AppendLine(" WHERE dt_start >= DATEADD(hour, -24, GETDATE());");
                        break;

                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sbQuery.AppendLine(" WHERE dt_start >= datetime('now', '-24 hours');");
                        break;
                }
            }
            else // jour précédent ou 7 jours précédents (daily / weekly)
            {
                //on aura la liste de tout ce qui a été exécuté depuis les dernier 'iDays' jours
                switch (SQLConnexionServiceApp.Connection.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sbQuery.AppendLine(" WHERE DateAdd(\"h\", -" + iHours.ToString() + ", Now()) >= dt_start AND CDate(Int(dt_start)) < DATE();");
                        break;

                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sbQuery.AppendLine(" WHERE DATE_ADD(SYSDATE(), INTERVAL -" + iHours.ToString() + " HOUR) > dt_start AND DATE(dt_start) < CURDATE();");
                        break;

                    case SQLTools_Enums.BDD.DB_ODBC:
                        //TODO
                        break;

                    case SQLTools_Enums.BDD.DB_ORACLE: //(VotreDate - INTERVAL '10' HOUR)
                        sbQuery.AppendLine(" WHERE dt_start >= (SYSDATE - INTERVAL '" + iHours.ToString() + "' HOUR) AND CAST(dt_start AS DATE) < TRUNC(SYSDATE);");
                        break;

                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sbQuery.AppendLine(" WHERE dt_start >= CURRENT_DATE - INTERVAL '" + iHours.ToString() + " hour' AND CAST(dt_start AS DATE) < CURRENT_DATE;");
                        break;

                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sbQuery.AppendLine(" WHERE dt_start >= DATEADD(hour, -" + iHours.ToString() + ", GETDATE()) AND CAST(dt_start AS DATE) < CAST(GETDATE() AS DATE);");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sbQuery.AppendLine(" WHERE DATE(dt_start) >= date('now', '-" + iHours.ToString() + " hour') AND DATE(dt_start) < DATE('now');");
                        break;
                }
            }

            dsExecuted = SQLConnexionServiceApp.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_SELECT, sbQuery.ToString(), DummyQuery, SQL_STACK_TABLE);

            string sMailContent = "";

            //construction du contenu HTML
            if (dsExecuted.Tables.Count > 0)
            {
                try
                {
                    DataSet dsCleanExec = BuildNiceExecutionReportFromQuery(dsExecuted.Tables[0]);

                    //dsExecuted.Tables[0].Namespace = sHeader;
                    sMailContent = MailTools.DatasetToHTML(dsCleanExec, true, false, false, true);
                }
                catch (Exception ex)
                {
                    sMailContent = ex.Message;
                }
            }
            else
            {
                DataTable dtMail = new();
                dtMail.Columns.Add("Info");
                dtMail.Rows.Add("-");
                sMailContent = MailTools.DatatableToHTML(dtMail, true, true, false, true);
            }

            DateTime dDateMin = DateTime.Now.AddDays(-iTime);
            DateTime dDateMax = DateTime.Now;

            //if (dsExecuted.Tables[0].Rows.Count > 0)
            //{
            //    dDateMin = Convert.ToDateTime(dsExecuted.Tables[0].Compute("MIN([Date_Execution])", string.Empty));
            //    dDateMax = Convert.ToDateTime(dsExecuted.Tables[0].Compute("MAX([Date_Execution])", string.Empty));
            //}

            return new Tuple<string, DateTime, DateTime>(sMailContent, dDateMin, dDateMax);

        }

        private DataSet BuildNiceExecutionReportFromQuery(DataTable dtExecutions)
        {
            string[] dtFormats = { "yyyy-dd-MM HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss" };

            DataSet dsFinalReport = new();

            List<string> sListJobs = new();
            foreach (DataRow row in dtExecutions.Rows)
            {
                if (!sListJobs.Contains(row["id_job"].ToString()))
                { sListJobs.Add(row["id_job"].ToString()); }
            }

            DataTable dtStats = new(Languages.Languages.jr_col_statistics)
            {
                Namespace = Languages.Languages.jr_col_statistics
            };
            string sGlobalState = Languages.Languages.jr_col_globalstate;
            string sJobNameStat = Languages.Languages.jr_col_job;
            string sJobPlanif = Languages.Languages.jr_col_planif;
            string sNombreExecutions = Languages.Languages.jr_col_nbexec;
            string sAbnormalState = Languages.Languages.jr_col_abnormal;
            string sNbErr = Languages.Languages.jr_col_witherror;
            string sNbWng = Languages.Languages.jr_col_withwarn;
            string sNbMoyTemps = Languages.Languages.jr_col_avgtime;
            string sNbMaxTemps = Languages.Languages.jr_col_maxtime;
            string sNbMinTemps = Languages.Languages.jr_col_mintime;
            dtStats.Columns.Add(sGlobalState);
            dtStats.Columns.Add(sJobNameStat);
            dtStats.Columns.Add(sJobPlanif);
            dtStats.Columns.Add(sNombreExecutions);
            dtStats.Columns.Add(sAbnormalState);
            dtStats.Columns.Add(sNbErr);
            dtStats.Columns.Add(sNbWng);
            dtStats.Columns.Add(sNbMoyTemps);
            dtStats.Columns.Add(sNbMaxTemps);
            dtStats.Columns.Add(sNbMinTemps);

            dsFinalReport.Tables.Add(dtStats);

            foreach (string sJob in sListJobs)
            {
                DataRow dtStatRow = dtStats.NewRow();

                DataView dView = new(dtExecutions)
                {
                    RowFilter = "id_job = '" + sJob + "'" // query example = "id = 10"
                };
                DataTable dtView = dView.ToTable();

                DataTable dtJob = new();

                string sEtat = Languages.Languages.jr_col_state;
                string sDateExec = Languages.Languages.jr_col_dtexec;
                string sDuree = Languages.Languages.jr_col_time;
                string sStatut = Languages.Languages.jr_col_status;
                string sArguments = Languages.Languages.jr_col_args;
                string sErreurs = Languages.Languages.jr_col_messages;

                dtJob.Columns.Add(sEtat);
                dtJob.Columns.Add(sDateExec);
                dtJob.Columns.Add(sDuree);
                dtJob.Columns.Add(sStatut);
                dtJob.Columns.Add(sArguments);
                dtJob.Columns.Add(sErreurs);

                string sJobName = dtView.Rows[0]["li_message"].ToString();
                MatchCollection mcInf = Regex.Matches(sJobName, "(\\[INF\\])");
                if (mcInf.Count > 1)
                {
                    sJobName = sJobName[..mcInf[1].Index];
                    if (sJobName.IndexOf(":") > 0)
                    {
                        sJobName = sJobName[(sJobName.IndexOf(":") + 1)..];
                    }
                    var matchIt = Regex.Match(sJobName, "\\(\\d+\\/\\d+\\)$");
                    if (matchIt.Success)
                    {
                        sJobName = sJobName[0..matchIt.Index].Trim();
                    }

                    dtJob.Namespace = sJobName;
                    dtJob.TableName = sJobName;
                    dtStatRow[sJobNameStat] = string.Concat(dtView.Rows[0]["id_job"], " ", sJobName);
                }
                else
                {
                    if (mcInf.Count == 1)
                    {
                        if (sJobName.IndexOf(":") > 0)
                        {
                            sJobName = sJobName[(sJobName.IndexOf(":") + 1)..];
                            sJobName = string.Concat(dtView.Rows[0]["id_job"], " ", sJobName);
                        }
                        else
                        {
                            sJobName = string.Concat(dtView.Rows[0]["id_job"], " ", "?");
                        }
                    }
                    else
                    {
                        sJobName = string.Concat(dtView.Rows[0]["id_job"], " ", "?");
                    }
                    var matchIt = Regex.Match(sJobName, "\\(\\d+\\/\\d+\\)$");
                    if (matchIt.Success)
                    {
                        sJobName = sJobName[0..matchIt.Index].Trim();
                    }

                    dtJob.Namespace = sJobName;
                    dtJob.TableName = sJobName;
                    dtStatRow[sJobNameStat] = sJobName;
                }

                int iRows = dtView.Rows.Count;

                dtStatRow[sNombreExecutions] = iRows.ToString();
                int iErr = dtView.Select("li_message LIKE '%ERR%'").Where(row => row["li_message"].ToString().Contains("[ERR]")).Count();
                dtStatRow[sNbErr] = iErr.ToString();
                int iWng = dtView.Select("li_message LIKE '%WNG%'").Where(row => row["li_message"].ToString().Contains("[WNG]")).Count();
                dtStatRow[sNbWng] = iWng.ToString();
                int iAbnormal = dtView.Select("li_status NOT LIKE 'FINISHED'").Length;
                dtStatRow[sAbnormalState] = iAbnormal.ToString();

                dtStatRow[sGlobalState] = iErr > 0 || iAbnormal > 0 ? "<p>&#10060;" : "<p>&#9989;</p>";

                TimeSpan totalDuration = TimeSpan.Zero;
                TimeSpan MaxTime = TimeSpan.MinValue;
                TimeSpan MinTime = TimeSpan.MaxValue;
                int rowCount = 0;

                List<string> sListPlanif = new();

                foreach (DataRow row in dtView.Rows)
                {
                    if (!sListPlanif.Contains(row["li_job"].ToString()))
                    {
                        sListPlanif.Add(row["li_job"].ToString());
                    }

                    DateTime date1;
                    DateTime date2;
                    if (DateTime.TryParseExact(row["dt_start"].ToString(), dtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date1) &&
                        DateTime.TryParseExact(row["dt_end"].ToString(), dtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date2))
                    {
                        TimeSpan duration = date2 - date1;
                        if (duration > MaxTime) { MaxTime = duration; }
                        if (duration < MinTime) { MinTime = duration; }

                        totalDuration += duration;
                        rowCount++;
                    }
                }

                TimeSpan averageDuration = TimeSpan.Zero;
                if (rowCount > 0)
                {
                    averageDuration = TimeSpan.FromTicks(totalDuration.Ticks / rowCount);
                }

                dtStatRow[sJobPlanif] = string.Join(Environment.NewLine, sListPlanif);

                dtStatRow[sNbMoyTemps] = new TimeSpan(averageDuration.Hours, averageDuration.Minutes, averageDuration.Seconds);
                dtStatRow[sNbMaxTemps] = new TimeSpan(MaxTime.Hours, MaxTime.Minutes, MaxTime.Seconds);
                dtStatRow[sNbMinTemps] = new TimeSpan(MinTime.Hours, MinTime.Minutes, MinTime.Seconds);

                foreach (DataRow dr in dtView.Rows)
                {
                    var newrow = dtJob.NewRow();
                    newrow[sEtat] = "?";
                    newrow[sDateExec] = dr["dt_start"].ToString();

                    DateTime dtStart = DateTime.Now;
                    DateTime.TryParseExact(dr["dt_start"].ToString(), dtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dtStart);

                    DateTime dtStop = DateTime.Now;
                    DateTime.TryParseExact(dr["dt_end"].ToString(), dtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dtStop);

                    TimeSpan duration = dtStop - dtStart;
                    newrow[sDuree] = new TimeSpan(duration.Hours, duration.Minutes, duration.Seconds);
                    newrow[sStatut] = dr["li_status"].ToString();
                    newrow[sArguments] = dr["li_arguments"].ToString();
                    newrow[sErreurs] = "?";

                    if (!dr["li_status"].ToString().Equals("FINISHED"))
                    {
                        switch (dr["li_status"].ToString())
                        {
                            case "FAILED":
                                newrow[sEtat] = "<p>&#10006;</p>";
                                break;
                            case "KILLED":
                                newrow[sEtat] = "<p>&#10013;</p>";
                                break;
                            case "RUNNING":
                                newrow[sEtat] = "<p>&#8987;</p>";
                                break;
                        }
                        string sMessage = dr["li_message"].ToString();
                        string sCleanMessages = Regex.Replace(sMessage, "(\\[[A-Z]{3}\\])", Environment.NewLine + "$1");
                        newrow[sErreurs] = sCleanMessages;
                    }
                    else
                    {
                        string sMessage = dr["li_message"].ToString();

                        var summaryMatch = Regex.Matches(sMessage, @"\[INF\]\s*\{[^}]+\}");
                        if (summaryMatch.Count > 0) //le message de synthèse, qu'on souhaite afficher systématiquement
                        {
                            string sMatches = "";
                            foreach (Match match in summaryMatch)
                            {
                                string sSummary = match.Value.Trim();
                                sSummary = Regex.Replace(sSummary, @"(\[INF\]\s*\{)", "");
                                sSummary = sSummary[..^1];
                                string[] sQueries = sSummary.Split("->", StringSplitOptions.RemoveEmptyEntries);
                                if (sQueries.Length > 0)
                                {
                                    for (int iQ = 0; iQ < sQueries.Length; iQ++) { sQueries[iQ] = sQueries[iQ].Trim(); }
                                    sMatches = string.Concat(sMatches, Environment.NewLine, "<p style=\"display: inline; padding: 1px;\">&#128313;</p>" + " " + string.Join((Environment.NewLine + " -> "), sQueries.ToList()).Trim());
                                }
                                else
                                {
                                    sMatches = string.Concat(sMatches, Environment.NewLine, "<p style=\"display: inline; padding: 1px;\">&#128313;</p>" + " " + match.Value.Trim());
                                }
                            }

                            if (sMatches.Trim().Length > 0)
                            {
                                newrow[sErreurs] = sMatches.Trim();
                            }
                            else { newrow[sErreurs] = ""; }
                        }
                        else { newrow[sErreurs] = ""; }

                        if (sMessage.IndexOf("[ERR]") > 0)
                        {
                            newrow[sEtat] = "<p>&#10060;</p>";
                            List<string> sErr = GetLogPatternFromMessage(sMessage, "ERR", "10071");
                            newrow[sErreurs] = (newrow[sErreurs].ToString().Length > 0 ? newrow[sErreurs].ToString() + Environment.NewLine : "") + string.Join(Environment.NewLine, sErr);
                            //extraire les erreurs

                        }
                        else if (sMessage.IndexOf("[WNG]") > 0)
                        {
                            //newrow[sEtat] = "<p>&#10068;</p>";
                            newrow[sEtat] = "<p>&#9989;</p>";
                            List<string> sWng = GetLogPatternFromMessage(sMessage, "WNG", "10067");
                            newrow[sErreurs] = (newrow[sErreurs].ToString().Length > 0 ? newrow[sErreurs].ToString() + Environment.NewLine : "") + string.Join(Environment.NewLine, sWng);
                            //extraire les warning
                        }
                        else
                        {
                            newrow[sEtat] = "<p>&#9989;</p>";

                            //if (newrow[sErreurs].ToString().Length == 0)
                            //{
                            //    newrow[sErreurs] = "";
                            //}
                        }
                    }
                    dtJob.Rows.Add(newrow);
                }
                dsFinalReport.Tables.Add(dtJob);
                dtStats.Rows.Add(dtStatRow);
            }

            return dsFinalReport;
        }

        private static List<string> GetLogPatternFromMessage(string sMessage, string sPatternToFind, string sHtmlEntity)
        {
            List<string> sList = new();

            MatchCollection mcErr = Regex.Matches(sMessage, "(\\[" + sPatternToFind + "\\])");
            foreach (Match mc in mcErr)
            {
                string sData = sMessage[(mc.Index + 5)..].Trim();
                //suppression de la prochaine itération de [A-z]
                Match mcNext = Regex.Match(sData, "(\\[[A-Z]{3}\\])");
                if (mcNext.Success)
                {
                    sData = sData[..mcNext.Index].Trim();
                }
                sList.Add(string.Concat("<p style=\"display: inline; padding: 1px;\">&#" + sHtmlEntity + ";</p>", " ", sData));
            }
            return sList;
        }

        public static void ConfigureWindowsTaskManager(bool bIsActivate, bool bSystemUser)
        {
            try
            {
                using TaskService ts = new();
                // Create a new task definition and assign properties
                TaskDefinition td = ts.NewTask();

                if (bSystemUser)
                {
                    td.Principal.LogonType = TaskLogonType.S4U;
                    td.Principal.UserId = "SYSTEM";

                    //td.Principal.RunLevel = TaskRunLevel.Highest;
                    //td.Principal.UserId = "SYSTEM";
                    //td.Principal.UserId = Environment.UserDomainName + "\\" + Environment.UserName;
                    //td.Principal.LogonType = TaskLogonType.S4U;
                    //td.Settings.RunOnlyIfLoggedOn = false;
                }

                td.RegistrationInfo.Author = Environment.UserName;
                td.RegistrationInfo.Description = Languages.Languages.par_taskmanager_taskname;
                td.RegistrationInfo.Date = DateTime.Now;

                td.Settings.AllowDemandStart = true;
                td.Settings.AllowHardTerminate = true;
                td.Settings.DisallowStartIfOnBatteries = true;
                td.Settings.DisallowStartOnRemoteAppSession = false;
                td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(12);
                td.Settings.MultipleInstances = TaskInstancesPolicy.Parallel;
                td.Settings.Priority = System.Diagnostics.ProcessPriorityClass.Normal;
                td.Settings.RunOnlyIfIdle = false;

                td.Settings.RunOnlyIfNetworkAvailable = false;
                td.Settings.StartWhenAvailable = false;
                td.Settings.StopIfGoingOnBatteries = true;
                td.Settings.UseUnifiedSchedulingEngine = false;
                td.Settings.WakeToRun = false;
                // Create a trigger that will fire the task at this time every other day

                td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                td.Triggers[0].Repetition.Interval = TimeSpan.FromMinutes(1);
                td.Triggers[0].Repetition.Duration = TimeSpan.FromDays(1);
                td.Triggers[0].ExecutionTimeLimit = TimeSpan.FromHours(12);
                td.Triggers[0].StartBoundary = Convert.ToDateTime(DateTime.Now.Date);

                // Create an action that will launch Notepad whenever the trigger fires
                td.Actions.Add(new ExecAction(Directory.GetCurrentDirectory() + "\\FuzibleService.exe", null, Directory.GetCurrentDirectory()));

                if (bIsActivate)
                {
                    td.Settings.Enabled = true;
                }
                else { td.Settings.Enabled = false; }

                // Register the task in the root folder
                ts.RootFolder.RegisterTaskDefinition(@"Fuzible Service App", td);
            }
            catch { throw; }
        }
    }

    public class PlanifModel
    {
        public int IsActive { get; internal set; } = 0;
        public string ConfigFile { get; internal set; } = "";
        public string JobID { get; internal set; } = "";
        public int PlanifID { get; internal set; } = 0;
        public string PlanifPattern { get; internal set; } = "";
        public string Description { get; internal set; } = "";
        public string Arguments { get; internal set; } = "";

        public PlanifModel(string sConfigFile, string sJobID, int iId, string sPattern, string sDescription, string sArguments, int iIsActive)
        {
            IsActive = iIsActive;
            ConfigFile = sConfigFile;
            JobID = sJobID;
            PlanifID = iId;
            PlanifPattern = sPattern;
            Description = sDescription;
            Arguments = sArguments;
        }

        public static List<string> GetPlanifElementsFromPattern(string sPlanifElement)
        {
            List<string> sListPlanif = new();
            for (int iP = 0; iP < sPlanifElement.Length; iP += 2)
            {
                sListPlanif.Add(sPlanifElement.Substring(iP, 2));
            }

            return sListPlanif;
        }

    }


}
