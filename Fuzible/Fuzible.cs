using Fuzible.Controleurs;
using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Fuzible
{
    internal class Program
    {
        public static Help LiveHelp = null;

        [STAThread]
        private static void Main(string[] args)
        {
            string DELEGATE_USERNAME = "";

            //ARG 1 : nom du fichier INI
            //ARG 2 : nom du job (lancement en tâche de fond)
            //ARG 3 : mot de passe (lancement en tâche de fond)
            //StreamWriter sw = new StreamWriter(Directory.GetCurrentDirectory() + "\\" + "test.txt");
            bool bStart = true;
            List<string> sMessage = new();

            try
            {
                if (!Directory.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible"))
                { Directory.CreateDirectory(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible"); }
            }
            catch (Exception ex)
            {
                Console.WriteLine(Languages.Languages.ma_msg_cantloadmainparameters + ex.Message);
                bStart = false;
                sMessage.Add(Languages.Languages.ma_msg_cantloadmainparameters + ex.Message);
            }

            if (Environment.ProcessorCount == 1)
            {
                bStart = false;
                sMessage.Add(Languages.Languages.ma_msg_musthave2cores);
            }

            if (bStart)
            {
                if (args.Length >= 1)
                {
                    if (args[0].Equals("?") || args[0].ToLower().Equals("help"))
                    {
                        StringBuilder sbMessage = new();
                        sbMessage.AppendLine("Fuzible Data Replicator and Synchronizer");
                        sbMessage.AppendLine("Required Arguments for Console Mode :");
                        sbMessage.AppendLine("1 -> User Name from which you want to launch a job from (ex : MYNAME)");
                        sbMessage.AppendLine("2 -> Job ID (not the name) To Launch");
                        sbMessage.AppendLine("3 -> The Password you set for decrypting Connection strings");
                        sbMessage.AppendLine("4 -> (Optional) : The Job Dynamic Parameters");
                        sbMessage.AppendLine("EX : Fuzible.exe MYNAME [JobID] MyPassword MyParameters");
                        Console.WriteLine(sbMessage.ToString());
                        bStart = false;
                        //MessageBox.Show(sbMessage.ToString());
                    }
                }
            }

            if (bStart)
            {
                string sCurrentJobID = "";
                if (args.Length > 1)
                { sCurrentJobID = args[1]; }

                string sPasswordArg = "";
                if (args.Length > 2)
                {
                    sPasswordArg = args[2];
                    //en mode paramétré, on peut charger le fichier INI avec le password passé en argument
                }

                string sDynamicParametersArg;
                List<string> sListDynamicParameters = new();
                if (args.Length > 3)
                {
                    sDynamicParametersArg = args[3];
                    //string[] sArrayDP = sDynamicParametersArg.Split(Convert.ToChar(";"));
                    string[] sArrayDP = Regex.Split(sDynamicParametersArg, ";");

                    foreach (string sDP in sArrayDP)
                    { sListDynamicParameters.Add(sDP); }
                    //en mode paramétré, on peut charger le fichier INI avec le password passé en argument
                }

                //username facultatif
                if (args.Length > 4)
                { DELEGATE_USERNAME = args[4].Replace("\\", "-"); }

                if (!sCurrentJobID.Equals(""))
                {
                    if (!sCurrentJobID.StartsWith("["))
                    { sCurrentJobID = string.Concat("[", sCurrentJobID, "]"); }

                    string sUser = args[0].Trim().ToUpper();
                    try
                    {
                        //check user ! 
                        bool bUserExists = INIProgram.CheckUserExists(sUser);

                        if (bUserExists)
                        {
                            //chargement INI demandé
                            var INIFile = new INIProgram(sUser, false, false);
                            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(INIFile.GlobalParameters.APP_LANGUAGE);
                            //lancement d'un job en auto
                            if (INIFile.GetJob(sCurrentJobID) != null)
                            {
                                Job INIP = INIFile.GetJob(sCurrentJobID);
                                LogTools MyLogProgram = new(System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]).Replace(".exe", ""), System.Reflection.MethodBase.GetCurrentMethod(), INIP, false);

                                if (sPasswordArg.Equals(INIP.JobPassword))
                                {
                                    if (sListDynamicParameters.Count == 0)
                                    { sListDynamicParameters = INIP.DynParams; }
                                    //je substitue les paramètres dynamiques éventuellement stockés dans le job par ceux passés en argument
                                    INIFile.ExecuteJob(INIP.DeepCopy(), MyLogProgram, sListDynamicParameters, DELEGATE_USERNAME);

                                }
                                else
                                {
                                    MyLogProgram.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, new Exception("Wrong Password for " + INIP.JobNAME + " - Unable to Start the Job"), "Unable to load " + INIP.JobNAME, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    System.Windows.Application.Current.Shutdown();
                                }
                            }
                            else
                            {
                                Console.WriteLine(Languages.Languages.ma_msg_cantrunjobunknownid + sCurrentJobID);
                                System.Environment.Exit(1);
                            }
                        }
                        else
                        {
                            Console.WriteLine(Languages.Languages.ma_msg_cantrunjobunknownuser + sUser);
                            System.Environment.Exit(1);
                        }
                    }
                    catch (Exception ex) { Console.Write(Languages.Languages.ma_msg_cantloaduserdata + sUser + ") : " + ex.Message); System.Environment.Exit(1); }
                }
                else
                {
                    try
                    {
                        LoadHelp();
                    }
                    catch
                    {
                        
                    }

                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                    string sUser = Environment.UserName.ToUpper();
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(INIProgram.GetUserLanguage(sUser));

                    Fuzible_CTL controller = null;

                    Task.Factory.StartNew(() =>
                    {
                        controller = new Fuzible_CTL(sUser, true, false);
                    });

                    LoadingWindow loadingWindow = new LoadingWindow();
                    loadingWindow.ShowDialog();

                    while (controller == null)
                    {
                        Thread.Sleep(100);
                    }

                    UI uiWindow = new(DELEGATE_USERNAME, controller);
                }

            }
            else
            {
                try
                {
                    StreamWriter sw = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "UI_CRASH.TXT", true, Encoding.UTF8);
                    foreach (string sM in sMessage)
                    { sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";ERR;Start_Program;", sM, " (", Toolbox.GetAppContext(), ")")); }
                    sw.Close();
                    System.Environment.Exit(1);
                }
                catch { System.Environment.Exit(1); }
            }
        }

        private static void LoadHelp()
        {
            try
            {
                // Obtenez l'assembly en cours d'exécution
                Assembly assembly = Assembly.GetExecutingAssembly();

                // Construisez le chemin du fichier d'aide incorporé
                string resourceName = "Fuzible.Resources.Help.db";

                // Obtenez le chemin du répertoire en cours
                string outputFilePath = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\Help.db";

                // Accédez au flux du fichier incorporé
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null)
                    {
                        // Copiez le contenu du flux dans un nouveau fichier
                        using (FileStream fileStream = File.Create(outputFilePath))
                        {
                            resourceStream.CopyTo(fileStream);
                        }

                        //Console.WriteLine($"Fichier d'aide reconstruit à l'emplacement : {outputFilePath}");
                    }
                    else
                    {
                        //Console.WriteLine("Fichier d'aide incorporé introuvable.");
                    }
                }
            }
            catch { throw; }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                StreamWriter sw = new(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\" + "UI_CRASH.TXT", true, Encoding.UTF8);
                sw.WriteLine(string.Concat(DateTime.Now.ToString("HH:mm:ss"), ";ERR;UI;", ex.Message, " (", Toolbox.GetAppContext(), ")"));
                sw.Close();
            }
            // You can perform additional error handling here
        }
    }
}
