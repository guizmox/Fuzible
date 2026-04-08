using Fuzible.Controleurs;
using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour ImportJob.xaml
    /// </summary>
    public partial class ImportJob : Window
    {
        private readonly ImportJob_CTL FuzibleController;
        public Job NEWJOB;
        public bool IS_IMPORT_OK = false;

        public ImportJob(string sUsername)
        {
            FuzibleController = new ImportJob_CTL(sUsername);
            InitializeComponent();
            Activate();
            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.2;

            LoadUsers(false);
        }

        private void CbUsersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbUsersList.SelectedValue != null)
            {
                var cb = (ComboBox)e.Source;
                var cbI = (ComboBoxItem)cb.SelectedItem;
                string sUser = cbI == null ? cbUsersList.SelectedValue.ToString() : cbI.Tag.ToString();
                try
                {
                    FuzibleController.ChangeUser(sUser.Trim().ToUpper());
                    LoadUserJobs();
                }
                catch (Exception ex) 
                { 

                    MessageBox.Show(ex.Message); 
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnImportJob_Click(object sender, RoutedEventArgs e)
        {
            if (cbINISection.SelectedIndex > -1)
            {
                string sExtJob = cbINISection.SelectedValue.ToString();

                Job INIP = ImportExternalJob(sExtJob); //on récupère in INIP modifié (changement d'ID + config file export)
                if (INIP != null)
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_matchconnectionsbyname, Languages.Languages.mc_msg_matchconnectionsbyname_header, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        NEWJOB = FuzibleController.ConvertJobForUser(INIP, true);
                    }
                    else
                    {
                        NEWJOB = FuzibleController.ConvertJobForUser(INIP, false);
                    }
                    try
                    {
                        if (NEWJOB != null)
                        {
                            IS_IMPORT_OK = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Languages.Languages.ij_msg_cantloadexternal + ex.Message + ")");
                    }

                    Close();
                }
            }
            else
            { MessageBox.Show(Languages.Languages.ij_msg_nojobselected); }
        }

        private void BtnLoadAnotherDB_Click(object sender, RoutedEventArgs e)
        {
        //    OpenFileDialog fileDialog = new OpenFileDialog()
        //    {
        //        Filter = "(*.DB) | *.DB"
        //    };
        //    fileDialog.ShowDialog();

        //    if (fileDialog.FileName.Length > 0)
        //    {
        //        FuzibleUITools.Prompt fP = new FuzibleUITools.Prompt(Languages.Languages.ij_input_chooseuser, Languages.Languages.ij_input_chooseuser, false);
        //        fP.ShowDialog();
        //        string sUser = fP.PromptUserData.ToUpper();
        //        string file = fileDialog.FileName[(fileDialog.FileName.LastIndexOf("\\") + 1)..];
        //        string path = fileDialog.FileName.Substring(0, fileDialog.FileName.LastIndexOf("\\")) + "\\";
        //        string sDtSource = "Data Source=" + path + file + ";Version=3;foreign keys=true;";
        //        try
        //        {
        //            INIFile = new INIProgram(sDtSource, sUser.Trim().ToUpper(), false, true);
        //            List<string> sListUsers = INIFile.LoadUsersList(false);
        //            LoadUsers(true);
        //            if (sListUsers.Contains(sUser))
        //            {
        //                cbUsersList.SelectedValue = sUser.Trim().ToUpper();
        //                LoadUserJobs();
        //            }
        //            else { MessageBox.Show(Languages.Languages.ij_msg_userinexistant); }
        //        }
        //        catch (Exception ex) { MessageBox.Show(Languages.Languages.ij_msg_cantloadunknown + ex.Message); }
        //    }
        }

        private void LoadUserJobs()
        {
            cbINISection.Items.Clear();
            foreach (Job INIP in FuzibleController.UserJobsList)
            {
                ComboBoxItem cbNewItem = new()
                {
                    Tag = INIP.JobID,
                    Content = string.Concat(INIP.JobID, " ", INIP.JobNAME),
                    ToolTip = string.Concat(Languages.Languages.ij_msg_jobdescription, INIP.JobDescription),
                    Foreground = INIP.Job_IsSubJob ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.DarkBlue,
                };
                ;
                cbINISection.Items.Add(cbNewItem);
            }
        }

        private void LoadUsers(bool bWithMine)
        {
            cbUsersList.Items.Clear();
            cbUsersList.SelectedIndex = -1;
            List<string> sListUsers = FuzibleController.GetUsers();

            foreach (string sUser in sListUsers)
            {
                ComboBoxItem cbNewItem = new()
                {
                    Tag = sUser,
                    Content = sUser,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                ;
                cbUsersList.Items.Add(cbNewItem);
            }
        }

        private Job ImportExternalJob(string sExtJobID)
        {
            bool bOK = false;
            Job INIP = null;

            try
            {
                string sPwd = "";
                string sRawJobID = "";

                INIP = FuzibleController.GetJob(sExtJobID);
                sPwd = INIP.JobPassword;
                sRawJobID = INIP.RawJobID.ToString();

                if (sExtJobID.IndexOf("-") > -1) //possiblement un sous-job, donc on récupère le MDP et l'ID du job principal
                {
                    INIP = FuzibleController.GetJob(sExtJobID.Split(Convert.ToChar("-"))[0] + "]");
                    sPwd = INIP.JobPassword;
                    sRawJobID = INIP.RawJobID.ToString();
                    INIP = INIP = FuzibleController.GetJob(sExtJobID);
                }

                FuzibleUITools.Prompt fP = new(Languages.Languages.ij_input_password, Languages.Languages.ij_input_password, true, true);
                fP.ShowDialog();
                string sJobPassword = fP.PromptUserData;
                if (sJobPassword.Equals(FITools.EncryptionSystem.AES_Decrypt(INIP.JobPassword, INIP.RawJobID.ToString())))
                { bOK = true; }
                else
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.ij_msg_wrongpwd01, Languages.Languages.ij_msg_wrongpwd02, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        Task tExecuteTask;
                        tExecuteTask = Task.Factory.StartNew(() => INIP.AskForPassword(System.Diagnostics.Process.GetCurrentProcess().ProcessName, string.Concat(Environment.UserDomainName, "-", Environment.UserName), INIP.GlobalParameters.APP_ADMINS));
                        MessageBox.Show(Languages.Languages.ij_input_emailsent);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Languages.Languages.ij_msg_cantloadunknown + ex.Message + ")");
            }
            //LISTOFJOBS.Add(INIP);

            //sauvegarde de la nouvelle section dans le fichier INI
            if (bOK) { return INIP; }
            else { return null; }
        }
    }
}
