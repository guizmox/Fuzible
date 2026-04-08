using FuzibleFramework;
using ProcessEventHandler;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FuzibleService
{
    class Program
    {
        #region "VARIABLES"

        static readonly string INTERNAL_DB = "Data Source=" + System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db;foreign keys=true";
        static readonly List<string> outData = new();

        static int PROCESS_TIMEOUT = 7200000; //2h
        static int PROCESS_EXT_TIMEOUT = 28800000; //8h

        static ServiceApp CSParameters = null;
        static INIProgram INIFile = null;

        static readonly List<string> sbAppLog = new();

        #endregion

        #region "PRIVATE VOID"

        static void Main()
        {
            DateTime dtNow = DateTime.Now; //Convert.ToDateTime("21/04/2023 09:00:00");

            string sDateTime = DateTime.Now.ToString("yyyyMMdd");
            string sDateTimeYesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            string sDayLogFile = string.Concat("SERVICE_", sDateTime).ToUpper();

            //clean log           
            if (File.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "SERVICE_" + sDateTimeYesterday + "_LOG.TXT"))
            { try { File.Delete(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "SERVICE_" + sDateTimeYesterday + "_LOG.TXT"); } catch { } }

            if (File.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "SERVICE_" + sDateTimeYesterday + "_CRASH.TXT"))
            { try { File.Delete(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "SERVICE_" + sDateTimeYesterday + "_CRASH.TXT"); } catch { } }


            if (File.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Fuzible.db"))
            {
                try
                {
                    sbAppLog.Add("START " + DateTime.Now.ToString());
                    sbAppLog.Add("[INF] Loading Parameters");
                    Console.WriteLine("[INF] Loading Parameters");
                    //WriteSerial();

                    string sUser = ServiceApp.GetServiceAppUser(INTERNAL_DB);

                    if (sUser.Length > 0)
                    {
                        sUser = FITools.EncryptionSystem.AES_Decrypt(sUser, SHSConstantes.PROGRAM_PWD);
                        sbAppLog.Add("[INF] User : " + sUser);
                        Console.WriteLine("[INF] User : " + sUser);

                        INIFile = new INIProgram(sUser, false, false);
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(INIFile.GlobalParameters.APP_LANGUAGE);

                        CSParameters = new ServiceApp(INIFile);
                        PROCESS_TIMEOUT = INIFile.GlobalParameters.SHELL_OPERATIONS_Fuzible_TIMEOUT * 60000;
                        PROCESS_EXT_TIMEOUT = INIFile.GlobalParameters.SHELL_OPERATIONS_EXT_TIMEOUT * 60000;

                        Job INIP = new(INIFile.GlobalParameters, "[0]", CSParameters.SUsername, false);
                        INIP.SetJobPassword("SHS", "", true);

                        if (CSParameters.SQLConnexionStringServiceApp != null)
                        {
                            INIP.ConnectionString_Target = CSParameters.SQLConnexionStringServiceApp;

                            LogTools MyLog = new(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), null, false);

                            sbAppLog.Add("[INF] " + Languages.Languages.svc_createsqlconn + " (" + CSParameters.BDDDriverServiceApp.ToString() + ")");
                            Console.WriteLine("[INF] " + Languages.Languages.svc_createsqlconn + " (" + CSParameters.BDDDriverServiceApp.ToString() + ")");
                            CSParameters.SQLConnexionServiceApp = new SQLTools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                            if (CSParameters.SQLConnexionServiceApp != null)
                            {
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_purgehistory + " (>" + CSParameters.IKeepLogUntil.ToString() + Languages.Languages.svc_purgehistory_days);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_purgehistory + " (>" + CSParameters.IKeepLogUntil.ToString() + Languages.Languages.svc_purgehistory_days);
                                sbAppLog.Add(CSParameters.PurgeHistoStack());
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_killidle + " (>" + CSParameters.IPurgeIdleJobsAfterHours.ToString() + Languages.Languages.svc_killidle_hours);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_killidle + " (>" + CSParameters.IPurgeIdleJobsAfterHours.ToString() + Languages.Languages.svc_killidle_hours);
                                sbAppLog.Add(CSParameters.KillIdleJobs());
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_searchabnormalstackstate);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_searchabnormalstackstate);
                                LookForAbnormalStackState(INIP, MyLog);
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_searchaddplanified);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_searchaddplanified);
                                string sPlanified = CSParameters.SearchAndInsertPlanifiedJobs();
                                if (sPlanified.Length > 0) { sbAppLog.Add(""); }
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_checkandlaunch);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_checkandlaunch);
                                CheckForJobsAndExecute();

                                int iTime = CheckForExecutionReportToBeSent(dtNow);
                                if (iTime > 0)
                                {
                                    sbAppLog.Add("[INF] " + Languages.Languages.svc_checkexecutionreport);
                                    Console.WriteLine("[INF] " + Languages.Languages.svc_checkexecutionreport);
                                    try
                                    {
                                        SendReportMail(INIP, MyLog, iTime);
                                    }
                                    catch (Exception ex)
                                    {
                                        StreamWriter sr = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + sDayLogFile + "_CRASH.TXT", true, Encoding.UTF8);
                                        string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                                        sr.WriteLine(sError);
                                        sr.Close();
                                        sbAppLog.Add(sError);
                                    }
                                }
                            }
                            else
                            {
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_unknowerror);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_unknowerror);
                            }
                            sbAppLog.Add("[INF] " + Languages.Languages.svc_end);
                            Console.WriteLine("[INF] " + Languages.Languages.svc_end);
                        }
                        else
                        {
                            Exception ex = new(Languages.Languages.svc_mustconfigureservice);
                            StreamWriter sr = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "SERVICEAPP_CRASH.TXT", true, Encoding.UTF8);
                            string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                            sr.WriteLine(sError);
                            sr.WriteLine(string.Join(Environment.NewLine, sbAppLog));
                            sr.Close();
                            sbAppLog.Add(sError);
                        }
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.svc_nouser);
                        StreamWriter sr = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + sDayLogFile + "_CRASH.TXT", true, Encoding.UTF8);
                        string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                        sr.WriteLine(sError);
                        sr.Close();
                        sbAppLog.Add(sError);
                    }
                }
                catch (Exception ex)
                {
                    StreamWriter sr = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + sDayLogFile + "_CRASH.TXT", true, Encoding.UTF8);
                    string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                    sr.WriteLine(sError);
                    sr.Close();
                    sbAppLog.Add(sError);
                }
                try
                {
                    sbAppLog.Add("STOP " + DateTime.Now.ToString());
                    StreamWriter sw = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + sDayLogFile + "_LOG.TXT", true, Encoding.UTF8);
                    sw.WriteLine(string.Join(Environment.NewLine, sbAppLog));
                    sw.Close();
                }
                catch
                {
                    sbAppLog.Add("STOP " + DateTime.Now.ToString());
                    StreamWriter sw = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + sDayLogFile + "_CRASH.TXT", true, Encoding.UTF8);
                    sw.WriteLine(string.Join(Environment.NewLine, sbAppLog));
                    sw.Close();
                }
            }
            else
            {
                Console.WriteLine("Database is missing. You have to start the main program first.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        private static void LookForAbnormalStackState(Job INIP, LogTools MyLog)
        {
            try
            {
                DataSet dsStatus = CSParameters.CheckAbnormalRunningJobs();
                if (dsStatus != null)
                {
                    int iRunning = dsStatus.Tables[0].Rows.Count;
                    int iRequested = dsStatus.Tables[1].Rows.Count;
                    //situation anormale
                    if (iRunning >= CSParameters.IQteParallelJobs && iRequested >= CSParameters.IQteParallelJobs)
                    {
                        dsStatus.Tables[0].TableName = Languages.Languages.svc_abnormalstack_runningjobs;
                        dsStatus.Tables[0].Namespace = Languages.Languages.svc_abnormalstack_runningjobs;

                        dsStatus.Tables[1].TableName = Languages.Languages.svc_abnormalstack_requestedjobs;
                        dsStatus.Tables[1].Namespace = Languages.Languages.svc_abnormalstack_requestedjobs;
                     
                        if (CSParameters.SendJobReport_Recipients.Length > 0)
                        {
                            MailTools mLog = new(INIP, SQLTools_Enums.CLASS_PURPOSE.LOG, ref MyLog);

                            mLog.SendMail(Languages.Languages.svc_abnormalstack_mailobject, MailTools.DatasetToHTML(dsStatus, true, false, false, true), CSParameters.SendJobReport_Recipients.Split(';').ToList(), null, null, true);
                        }
                        else
                        {
                            string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", Languages.Languages.svc_abnormalstack_mailobject, " (RUNNING : ", iRunning.ToString(), " / REQUESTED : ", iRequested.ToString());
                            outData.Add("[WNG] " + Languages.Languages.svc_crashed + sError);
                            sbAppLog.Add("[WNG] " + sError);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                outData.Add("[WNG] " + Languages.Languages.svc_crashed + ex.Message);
                sbAppLog.Add("[WNG] " + sError);
            }
        }

        private static void SendReportMail(Job INIP, LogTools MyLog, int iTime)
        {
            Tuple<string, DateTime, DateTime> sMailData = CSParameters.BuildExecutionReport(iTime, Languages.Languages.svc_qteexecuted_jobs);
            MailTools mLog = new(INIP, SQLTools_Enums.CLASS_PURPOSE.LOG, ref MyLog);

            string sInterval = "";

            if (iTime == 1)
            {
                sInterval = string.Concat(" (", sMailData.Item2.ToShortDateString(), ")");
            }
            else
            {
                sInterval = string.Concat(" (", sMailData.Item2.ToShortDateString(), " -> ", sMailData.Item3.ToShortDateString(), ")");
            }

            string sObject = string.Concat(Languages.Languages.svc_weeklyreport_01, " ", iTime, " ", Languages.Languages.svc_weeklyreport_02, sInterval);
            if (iTime == 1)
            {
                sObject = string.Concat(Languages.Languages.svc_dailyreport_01, sInterval);
            }
            else if (iTime == 24)
            {
                sObject = string.Concat(Languages.Languages.svc_last24report_01);
            }
            mLog.SendMail(sObject, sMailData.Item1, CSParameters.SendJobReport_Recipients.Split(';').ToList(), null, null, true);
        }

        private static int CheckForExecutionReportToBeSent(DateTime dtNow)
        {
            int iTime = 0;

            if (dtNow.Minute == 0)
            {
                if (CSParameters.SendJobReport_Recipients.Length > 0)
                {
                    if (((dtNow.Hour + 11) % 12) + 1 == CSParameters.SendJobReport_Hour && dtNow.ToString("tt", CultureInfo.InvariantCulture).Equals(CSParameters.SendJobReport_AmPm, StringComparison.OrdinalIgnoreCase))
                    {
                        if (CSParameters.SendJobReport.IndexOf("DAILY") > -1)
                        {
                            iTime = 1;
                        }
                        else if (CSParameters.SendJobReport.IndexOf("LAST24") > -1)
                        {
                            iTime = 24; //attention 
                        }
                        else if (CSParameters.SendJobReport.IndexOf("WEEKLY") > -1)
                        {
                            //vérifier qu'on est bien le premier jour de la semaine
                            if (dtNow.DayOfWeek == CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
                            {
                                iTime = 7;
                            }
                        }
                    }
                }
            }

            return iTime;
        }

        static void CheckForJobsAndExecute()
        {
            List<string[]> sListBuildEXELaunchs = new();

            //contrôle de dépassement du nombre de jobs exécutés en parallèle
            int iResult = CSParameters.CheckQuantityOfRunningJobs();

            if (iResult > CSParameters.IQteParallelJobs)
            {
                sbAppLog.Add("[INF] " + Languages.Languages.svc_cantlaunchmorejobs);
                Console.WriteLine("[INF] " + Languages.Languages.svc_cantlaunchmorejobs);
            }
            else
            {
                DataSet dsJobs = CSParameters.GetWaitingList();

                if (dsJobs != null && dsJobs.Tables.Count > 0 && dsJobs.Tables[0].Rows.Count > 0)
                {
                    sbAppLog.Add("[INF] " + Languages.Languages.svc_waitinglist + dsJobs.Tables[0].Rows.Count.ToString() + " Jobs.");
                    Console.WriteLine("[INF] " + Languages.Languages.svc_waitinglist + dsJobs.Tables[0].Rows.Count.ToString() + " Jobs.");
                    bool bFromFuzible = false;
                    //construction du lancement paramétré de l'EXE
                    for (int iR = 0; iR < 1; iR++)
                    {
                        string sAppToLaunch = dsJobs.Tables[0].Rows[iR]["li_apptolaunch"].ToString();
                        if (sAppToLaunch.Length > 0)
                        {
                            if (sAppToLaunch.Equals(INIProgram.APP_NAME, StringComparison.OrdinalIgnoreCase))
                            {
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_launchfuziblejob);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_launchfuziblejob);
                                string sU = dsJobs.Tables[0].Rows[iR]["li_configfile"].ToString();
                                sU = sU[(sU.IndexOf("_") + 1)..];
                                sU = sU[0..^4];
                                Job JobParameters = INIFile.GetJob(dsJobs.Tables[0].Rows[iR]["id_job"].ToString());
                                string sPassword;
                                //sPassword = FileTools.EncryptionSystem.AES_Decrypt(JobParameters.ProgramPassword, FileTools.EncryptionSystem.GetVolumeSerial("C"));
                                sPassword = JobParameters.JobPassword;
                                string sDynParams = dsJobs.Tables[0].Rows[iR]["li_arguments"].ToString();
                                sListBuildEXELaunchs.Add(new string[] { dsJobs.Tables[0].Rows[iR]["id_stack"].ToString(), string.Concat(dsJobs.Tables[0].Rows[iR]["li_apptolaunch"], "|", "\"",
                                                                dsJobs.Tables[0].Rows[iR]["li_configfile"], "\"", " ", "\"",
                                                                dsJobs.Tables[0].Rows[iR]["id_job"], "\"", " ", "\"",
                                                                sPassword, "\"", " ",
                                                                sDynParams.Length > 0 ? ("\"" + sDynParams + "\"") : ("\"" + "\""), " ", "\"",
                                                                dsJobs.Tables[0].Rows[iR]["li_user"], "\"") });
                                bFromFuzible = true;
                            }
                            else
                            {
                                sbAppLog.Add("[INF] " + Languages.Languages.svc_launchextjob);
                                Console.WriteLine("[INF] " + Languages.Languages.svc_launchextjob);
                                string sDynParamsEXEBAT = dsJobs.Tables[0].Rows[iR]["li_arguments"].ToString();
                                sListBuildEXELaunchs.Add(new string[] { dsJobs.Tables[0].Rows[iR]["id_stack"].ToString(), string.Concat(dsJobs.Tables[0].Rows[iR]["li_apptolaunch"], "|",
                                                                sDynParamsEXEBAT.Length > 0 ? ("\"" + sDynParamsEXEBAT + "\"") : "") });
                                bFromFuzible = false;
                            }
                        }
                        //mauvaise saisie : job passe en finished
                        else
                        {
                            outData.Add("[ERR] " + Languages.Languages.svc_launchextjob_noappname);
                            CSParameters.AddEndMessageInBDDFromExecutedJob(dsJobs.Tables[0].Rows[iR]["id_stack"].ToString(), string.Join(" ", outData));
                        }
                    }

                    //exécution des jobs
                    if (sListBuildEXELaunchs.Count > 0)
                    {
                        //on ne lance que la première ligne parce que le service fonctionne toutes les minutes, donc peut se relancer sur lui-même
                        //D'ailleurs, dans le planificateur de tâches windows, il faut donner la possibilité que le service se relance à l'infini sans attendre que l'ancienne instance se termine
                        StartFuzibleJob(sListBuildEXELaunchs[0], bFromFuzible);
                    }
                }
            }
        }

        static void StartFuzibleJob(object sCommand, bool bFromFuzible)
        {
            string[] sCommandToRun = (string[])sCommand;
            Process process = new();

            StringBuilder sbQuery = new();

            try
            {
                CSParameters.AddStartMessageInBDDFromExecutedJob(sCommandToRun[0]);

                string sAppToRun = sCommandToRun[1].Split(Convert.ToChar("|"))[0];
                sAppToRun = Path.GetFileName(sAppToRun);

                string sArgsAppToRun = sCommandToRun[1].Split(Convert.ToChar("|"))[1];

                string sAppPath = Directory.GetCurrentDirectory();
                if (sCommandToRun[1].Split(Convert.ToChar("|"))[0].IndexOf("\\") > 0) { sAppPath = sCommandToRun[1].Split(Convert.ToChar("|"))[0][..sCommandToRun[1].Split(Convert.ToChar("|"))[0].LastIndexOf("\\")]; }

                sbAppLog.Add("[INF] " + Languages.Languages.svc_startingjob + " (Id : " + sCommandToRun[0] + " - App : " + sAppToRun + " - Args : " + sArgsAppToRun + " - Path : " + sAppPath + ")");
                Console.WriteLine("[INF] " + Languages.Languages.svc_startingjob + " (Id : " + sCommandToRun[0] + " - App : " + sAppToRun + " - Args : " + sArgsAppToRun + " - Path : " + sAppPath + ")");

                //* Create your Process
                string sMessage = "";
                StdStreamReader stdoutReader = new();
                StdStreamReader stderrReader = new();

                //process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                //process.StartInfo.StandardOutputEncoding = Encoding.UTF8;

                stdoutReader.DataReceivedEvent += new EventHandler<DataReceived>((sender, e) => StdoutReader_DataReceivedEvent(e, sCommandToRun[0], bFromFuzible));
                stderrReader.DataReceivedEvent += new EventHandler<DataReceived>((sender, e) => StderrReader_DataReceivedEvent(e, sCommandToRun[0], bFromFuzible));

                //process.StartInfo.Verb = "runas";
                process.StartInfo.FileName = string.Concat(sAppPath, "\\", sAppToRun);
                if (sArgsAppToRun.Length > 0) { process.StartInfo.Arguments = sArgsAppToRun; }
                process.StartInfo.WorkingDirectory = sAppPath;

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = false;

                process.Start();

                stdoutReader.StartReader(process.StandardOutput.BaseStream, process);
                stderrReader.StartReader(process.StandardError.BaseStream, process);

                var processExited = process.WaitForExit(bFromFuzible ? PROCESS_TIMEOUT : PROCESS_EXT_TIMEOUT); //pour les commandes externes, pas de timeout
                stdoutReader.IsDone();
                stderrReader.IsDone();

                if (processExited == false) // we timed out...
                {
                    process.Kill();
                    outData.Add("[ERR] " + Languages.Languages.svc_killedtimeout);
                    sbAppLog.Add("[ERR] " + sMessage);
                    Console.WriteLine("[ERR] " + sMessage);
                }
            }

            catch (Exception ex)
            {
                string sError = string.Concat("[", DateTime.Now.ToString(), "] [", System.Diagnostics.Process.GetCurrentProcess().ProcessName, "] ", ex.Message);
                outData.Add("[ERR] " + Languages.Languages.svc_crashed + ex.Message);
                sbAppLog.Add(sError);
                sbAppLog.Add("[ERR] " + Languages.Languages.svc_crashed + ex.Message);
            }

            if (process != null) { StopFuzibleJob(process, sCommandToRun, bFromFuzible); }
            else { StopFuzibleJob(null, sCommandToRun, bFromFuzible); }
        }

        static void StderrReader_DataReceivedEvent(DataReceived e, string sId, bool bFromFuzible)
        {
            string sData = "";
            try
            {
                sData = e.Data;
            }
            catch (Exception ex)
            {
                sData = "Unable to Parse Console Message : " + ex.Message;
            }

            lock (outData)
            {
                if (Regex.Replace(sData, @"\s+", "").Length > 0)
                {
                    Console.WriteLine(sData.Trim());
                    outData.Add(bFromFuzible ? sData.Trim() : "[WNG] " + sData.Trim());
                    CSParameters.AddMessageInBDDFromExecutedJob(sId, string.Join(" ", outData));
                }
            }
        }

        static void StdoutReader_DataReceivedEvent(DataReceived e, string sId, bool bFromFuzible)
        {
            string sData = "";
            try
            {
                sData = e.Data;
            }
            catch (Exception ex)
            {
                sData = "Unable to Parse Console Message : " + ex.Message;
            }

            lock (outData)
            {
                if (Regex.Replace(sData, @"\s+", "").Length > 0)
                {
                    Console.WriteLine(sData.Trim());
                    outData.Add(bFromFuzible ? sData.Trim() : "[INF] " + sData.Trim());
                    CSParameters.AddMessageInBDDFromExecutedJob(sId, string.Join(" ", outData));
                }
            }
        }

        static void StopFuzibleJob(Process process, string[] sCommandToRun, bool bFromFuzible)
        {
            string sMessage = "";

            if (!bFromFuzible)
            {
                sMessage = "[INF] " + Languages.Languages.svc_commandended;
                Console.WriteLine(sMessage);
            }

            if (process != null)
            {
                if (process.ExitCode != 0)
                {
                    sMessage = string.Concat(sMessage, "[WNG] " + Languages.Languages.svc_commandended_nonzero, process.ExitCode, " (Output: ", process.StandardError.ReadToEnd() + ")");
                    sbAppLog.Add(string.Concat("[WNG] " + Languages.Languages.svc_commandended_nonzero, process.ExitCode, " (Output: ", process.StandardError.ReadToEnd() + ")"));
                    Console.WriteLine(sMessage);
                }
                process.Close();
                process.Dispose();
            }

            if (sMessage.Trim().Length > 0)
            { 
                outData.Add(sMessage); 
            }

            CSParameters.AddEndMessageInBDDFromExecutedJob(sCommandToRun[0], string.Join(" ", outData));
            sbAppLog.Add("[INF] " + Languages.Languages.svc_jobended + sCommandToRun[0] + ") : " + sCommandToRun[1].Split(Convert.ToChar("|"))[1]);
            Console.WriteLine("[INF] " + Languages.Languages.svc_jobended + sCommandToRun[0] + ") : " + sCommandToRun[1].Split(Convert.ToChar("|"))[1]);
        }

        #endregion
    }
}
