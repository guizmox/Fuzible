using Fuzible.Controleurs;
using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Fuzible
{

    public partial class JobsOrganize : Window
    {
        private readonly JobsOrganize_CTL FuzibleController;
        private bool WAITUSERACTION_CHOOSEJOB = false;
        private int WAITUSERACTION_MAINJOBINDEX = 0;

        public JobsOrganize(string sUsername)
        {
            FuzibleController = new JobsOrganize_CTL(sUsername);
            InitializeComponent();
            LoadJobs();

            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.5;
        }

        public void LoadJobs()
        {
            lbJobsList.Items.Clear();
            lbSubJobsList.Items.Clear();

            foreach (Job J in FuzibleController.UserJobsList)
            {
                ListBoxItem lI = new()
                {
                    Tag = J.JobID,
                    Content = string.Concat(J.JobID, " - ", J.JobNAME),
                    ToolTip = string.Concat(Languages.Languages.oj_msg_description, J.JobDescription),
                    FontWeight = FuzibleController.GetJobColor(J.RawJobID),
                    Visibility = J.Job_IsSubJob ? Visibility.Collapsed : Visibility.Visible
                };
                lbJobsList.Items.Add(lI);
            }
        }

        private void LbJobsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListBox)sender).SelectedIndex > -1)
            {
                btnJobToSubJob.Visibility = Visibility.Visible;
                lbJobsList.Visibility = Visibility.Visible;
                btnSubJobToJob.Visibility = Visibility.Hidden;
                lbSubJobsList.Visibility = Visibility.Visible;
            }

            if (!WAITUSERACTION_CHOOSEJOB)
            {
                lbSubJobsList.Items.Clear();

                if (lbJobsList.SelectedIndex > -1)
                {
                    var cb = (ListBox)e.Source;
                    var cbI = (ListBoxItem)cb.SelectedItem;
                    string sCurrentJobID = cbI == null ? lbJobsList.SelectedValue.ToString() : cbI.Tag.ToString();
                    Job J = FuzibleController.GetJob(sCurrentJobID);

                    if (FuzibleController.GetStepsFromJob(J.RawJobID).Count + 1 > 1) // en fait il y a toujours 1 (étape 1)
                    {
                        //chargement des éventuels subjobs
                        List<Job> sListSubJobs = FuzibleController.GetStepsFromJob(J.RawJobID);

                        foreach (Job Jc in sListSubJobs)
                        {
                            ListBoxItem lsJI = new()
                            {
                                Tag = Jc.JobID,
                                Content = string.Concat(Jc.JobID, " - ", Jc.JobNAME),
                                ToolTip = string.Concat(Languages.Languages.oj_msg_description, Jc.JobDescription)
                            };
                            lbSubJobsList.Items.Add(lsJI);
                        }
                    }
                    else { lbSubJobsList.Items.Clear(); }
                }
            }
            else { MessageBox.Show(Languages.Languages.oj_msg_movecancelled); lbSubJobsList.Items.Clear(); WAITUSERACTION_MAINJOBINDEX = 0; WAITUSERACTION_CHOOSEJOB = false; }
        }

        private void LbSubJobsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListBox)sender).SelectedIndex > -1)
            {
                btnJobToSubJob.Visibility = Visibility.Hidden;
                lbJobsList.Visibility = Visibility.Hidden;
                btnSubJobToJob.Visibility = Visibility.Visible;
                lbSubJobsList.Visibility = Visibility.Visible;
            }

            if (WAITUSERACTION_CHOOSEJOB)
            {
                if (WAITUSERACTION_MAINJOBINDEX != lbJobsList.SelectedIndex)
                {
                    MessageBox.Show(Languages.Languages.oj_msg_movecancelled);
                    WAITUSERACTION_MAINJOBINDEX = 0;
                    WAITUSERACTION_CHOOSEJOB = false;
                }
                else
                {
                    bool bDeletePlanif = true;

                    if (FuzibleController.IsJobPlanified(lbJobsList.SelectedValue.ToString()))
                    {
                        MessageBoxResult msgR = MessageBox.Show(Languages.Languages.oj_msg_planifattached01, Languages.Languages.oj_msg_planifattached02, MessageBoxButton.OKCancel);

                        if (msgR.ToString().ToUpper().Equals("OK"))
                        { bDeletePlanif = false; }
                    }

                    string sNewJobID = FuzibleController.MoveJob(JobsOrganize_CTL.MODE_ACTION.SETJOB_AS_SUBJOB, lbJobsList.SelectedValue.ToString(), lbSubJobsList.SelectedValue.ToString(), bDeletePlanif);

                    MessageBox.Show(Languages.Languages.oj_msg_jobsetassubjob + lbJobsList.SelectedValue.ToString() + " -> " + sNewJobID);

                    WAITUSERACTION_MAINJOBINDEX = 0;
                    WAITUSERACTION_CHOOSEJOB = false;
                    LoadJobs();
                    lbJobsList.SelectedValue = sNewJobID;
                }
            }
        }

        private void BtnMoveSubJobUp_Click(object sender, RoutedEventArgs e)
        {
            if (lbSubJobsList.SelectedIndex > -1)
            {
                try
                {
                    if (lbSubJobsList.SelectedIndex < lbSubJobsList.Items.Count - 1)
                    {
                        string sJob = lbSubJobsList.SelectedValue.ToString();
                        string sNewJobID = FuzibleController.MoveJob(JobsOrganize_CTL.MODE_ACTION.SWAP_UP, sJob);

                        int iJob = lbJobsList.SelectedIndex;
                        LoadJobs();
                        lbJobsList.SelectedIndex = iJob;
                        lbSubJobsList.SelectedValue = sNewJobID;
                    }
                }
                catch (Exception ex)
                { MessageBox.Show(Languages.Languages.oj_msg_cantswapsubjobs + ex.Message); }
            }
        }

        private void BtnMoveSubJobDown_Click(object sender, RoutedEventArgs e)
        {
            if (lbSubJobsList.SelectedIndex > -1)
            {
                try
                {
                    if (lbSubJobsList.SelectedIndex > 0)
                    {
                        string sJob = lbSubJobsList.SelectedValue.ToString();
                        string sNewJobID = FuzibleController.MoveJob(JobsOrganize_CTL.MODE_ACTION.SWAP_DOWN, sJob);

                        int iJob = lbJobsList.SelectedIndex;
                        LoadJobs();
                        lbJobsList.SelectedIndex = iJob;
                        lbSubJobsList.SelectedValue = sNewJobID;
                    }
                }
                catch (Exception ex)
                { MessageBox.Show(Languages.Languages.oj_msg_cantswapsubjobs + ex.Message); }
            }
        }

        private void BtnSubJobToJob_Click(object sender, RoutedEventArgs e)
        {
            if (lbSubJobsList.SelectedIndex > -1)
            {
                btnSubJobToJob.Visibility = Visibility.Visible;
                try
                {
                    string sJob = lbJobsList.SelectedValue.ToString();
                    string sSubJob = lbSubJobsList.SelectedValue.ToString();
                    Job J = FuzibleController.GetJob(sJob);

                    FuzibleUITools.Prompt fP = new(Languages.Languages.oj_input_passwordold, Languages.Languages.oj_input_passwordold, true, true);
                    fP.ShowDialog();
                    string sJobPassword = fP.PromptUserData;

                    sJobPassword = FITools.EncryptionSystem.AES_Encrypt(sJobPassword, J.RawJobID.ToString());

                    if (sJobPassword.Equals(J.JobPassword))
                    {
                        FuzibleUITools.Prompt fP2 = new(Languages.Languages.oj_input_passwordnew, Languages.Languages.oj_input_passwordnew, true, true);
                        fP2.ShowDialog();
                        string sPassword = fP2.PromptUserData;
                        if (sPassword.Length > 0)
                        {
                            bool bDeletePlanif = false;

                            if (FuzibleController.IsJobPlanified(sSubJob))
                            {
                                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.oj_msg_planifattached01, Languages.Languages.oj_msg_planifattached02, MessageBoxButton.OKCancel);

                                if (msgR.ToString().ToUpper().Equals("OK"))
                                { bDeletePlanif = false; }
                            }

                            string sNewJobID = FuzibleController.MoveJob(JobsOrganize_CTL.MODE_ACTION.SETSUBJOB_JOB, sSubJob, sPassword, bDeletePlanif);

                            MessageBox.Show(Languages.Languages.oj_msg_subjobsetasjob + sSubJob + " -> " + sNewJobID);

                            LoadJobs();
                            lbJobsList.SelectedValue = sNewJobID;
                        }
                    }
                    else { MessageBox.Show(Languages.Languages.oj_msg_wrongpwd); }
                }
                catch (Exception ex)
                { MessageBox.Show(Languages.Languages.oj_msg_cantswapsubjobs + ex.Message); }
            }

            btnJobToSubJob.Visibility = Visibility.Visible;
            lbJobsList.Visibility = Visibility.Visible;
            btnSubJobToJob.Visibility = Visibility.Hidden;
            lbSubJobsList.Visibility = Visibility.Hidden;

        }

        private void BtnJobToSubJob_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbJobsList.Items.Count > 1)
                {
                    string sJob = lbJobsList.SelectedValue.ToString();
                    Job J = FuzibleController.GetJob(sJob);

                    if (FuzibleController.GetStepsFromJob(J.RawJobID).Count > 0)
                    {
                        MessageBox.Show(Languages.Languages.oj_msg_multistepcantmove);
                    }
                    else
                    {
                        FuzibleUITools.Prompt fP = new(Languages.Languages.oj_input_passwordold, Languages.Languages.oj_input_passwordold, true, true);
                        fP.ShowDialog();
                        string sJobPassword = fP.PromptUserData;
                        sJobPassword = FITools.EncryptionSystem.AES_Encrypt(sJobPassword, J.RawJobID.ToString());

                        if (sJobPassword.Equals(J.JobPassword))
                        {
                            if (FuzibleController.GetStepsFromJob(J.RawJobID).Count + 1 > 1)
                            {
                                MessageBox.Show(Languages.Languages.oj_msg_cantmovemultistepsjob);
                            }
                            else
                            {
                                MessageBox.Show(Languages.Languages.oj_msg_subjobloadedwithmainjobs);
                                int iIdx = lbJobsList.SelectedIndex;
                                lbSubJobsList.Items.Clear();
                                foreach (Job mJ in FuzibleController.UserJobsList)
                                {
                                    if (!mJ.JobID.Equals(J.JobID))
                                    {
                                        ListBoxItem lI = new()
                                        {
                                            Tag = mJ.JobID,
                                            Content = string.Concat(mJ.JobID, " - ", mJ.JobNAME),
                                            ToolTip = string.Concat(Languages.Languages.oj_msg_description, mJ.JobDescription),
                                            FontWeight = FuzibleController.GetJobColor(J.RawJobID),
                                            Visibility = mJ.Job_IsSubJob ? Visibility.Collapsed : Visibility.Visible
                                        };
                                        lbSubJobsList.Items.Add(lI);
                                    }
                                }

                                btnJobToSubJob.Visibility = Visibility.Hidden;
                                lbJobsList.Visibility = Visibility.Hidden;
                                btnSubJobToJob.Visibility = Visibility.Hidden;
                                lbSubJobsList.Visibility = Visibility.Visible;

                                WAITUSERACTION_CHOOSEJOB = true;
                                WAITUSERACTION_MAINJOBINDEX = iIdx;
                            }
                        }
                        else { MessageBox.Show(Languages.Languages.oj_msg_wrongpwd); }
                    }
                }
                else { MessageBox.Show(Languages.Languages.oj_msg_singlejobcantmove); }
            }
            catch (Exception ex)
            { MessageBox.Show(Languages.Languages.oj_msg_cantswapsubjobs + ex.Message); }

        }
    }
}
