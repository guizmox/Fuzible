using Fuzible.Controleurs;
using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour JobOrchestration.xaml
    /// </summary>
    public partial class JobOrchestration : Window
    {
        private readonly JobOrchestration_CTL FuzibleController;

        public JobOrchestration(string sUsername, string sJobID)
        {
            FuzibleController = new JobOrchestration_CTL(sUsername, sJobID);
            InitializeComponent();

            AttachMouseDownEventToAllControls(this.cvConf);
            AttachMouseDownEventToAllControls(this.cvLists);

            GeneratePlanifItems();
            GenerateListOfPlanifsForJob();
            if (FuzibleController.Planifications.Count > 0)
            { cbPlanifsForJob.SelectedValue = FuzibleController.Planifications[0].PlanifID; }
            else 
            { 
                MessageBox.Show(Languages.Languages.jo_msg_noplanif); cbPlanifsForJob.SelectedIndex = 0; 
                tbDynParams.Text = string.Join(";", FuzibleController.DynamicParameters); 
                RefreshUI(); 
            }

            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.5;


        }

        private void JobOrchestration_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            { Program.LiveHelp.ChangeLockedStatus(); }
        }


        private void AttachMouseDownEventToAllControls(DependencyObject parent)
        {
            foreach (var child in GetVisualChildren(parent))
            {
                if (child is UIElement uiElement)
                {
                    switch (uiElement)
                    {
                        case System.Windows.Controls.TextBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        //case System.Windows.Controls.Label:
                        //    uiElement.MouseMove += UIElement_MouseDown;
                        //    break;
                        case System.Windows.Controls.Button:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.RichTextBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.ComboBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.CheckBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.Slider:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.ListBox:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                        case System.Windows.Controls.MenuItem:
                            uiElement.MouseMove += UIElement_MouseDown;
                            break;
                    }

                    // Récursivement, attacher des gestionnaires d'événements pour les éléments enfants
                    AttachMouseDownEventToAllControls(uiElement);
                }
            }
        }

        private static IEnumerable<DependencyObject> GetVisualChildren(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                yield return VisualTreeHelper.GetChild(parent, i);
            }
        }

        private async void UIElement_MouseDown(object sender, MouseEventArgs e)
        {
            if (Program.LiveHelp != null && Program.LiveHelp.IsOpen)
            {
                string sControlName = "";
                switch (sender)
                {
                    case System.Windows.Controls.TextBox:
                        sControlName = ((TextBox)sender).Name;
                        break;
                    //case System.Windows.Controls.Label:
                    //    sControlName = ((Label)sender).Name;
                    //    break;
                    case System.Windows.Controls.Button:
                        sControlName = ((Button)sender).Name;
                        break;
                    case System.Windows.Controls.RichTextBox:
                        sControlName = ((RichTextBox)sender).Name;
                        break;
                    case System.Windows.Controls.ComboBox:
                        sControlName = ((ComboBox)sender).Name;
                        break;
                    case System.Windows.Controls.CheckBox:
                        sControlName = ((CheckBox)sender).Name;
                        break;
                    case System.Windows.Controls.Slider:
                        sControlName = ((Slider)sender).Name;
                        break;
                    case System.Windows.Controls.MenuItem:
                        sControlName = ((MenuItem)sender).Name;
                        break;
                    case System.Windows.Controls.ListBox:
                        sControlName = ((ListBox)sender).Name;
                        break;
                }

                if (sControlName.Length > 0)
                {
                    await Program.LiveHelp.LoadHelpBlock(new BlockHelp("JobOrchestration", sControlName, "", ""));
                }
            }
        }


        private void CbPlanifsForJob_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = (ComboBox)e.Source;
            var cbI = (ComboBoxItem)cb.SelectedItem;
            if (cbI != null)
            {
                string sCurrentPlanif = cbI.Tag.ToString();
                if (sCurrentPlanif.Equals("NEW"))
                { }
                else { SetPlanificationElements(FuzibleController.Planifications.First(pm => pm.PlanifID.ToString().Equals(sCurrentPlanif))); }
            }
        }

        private void CkDayModel_Click(object sender, RoutedEventArgs e)
        {
            RefreshUI();
        }

        private void BtnSavePlanif_Click(object sender, RoutedEventArgs e)
        {
            string sElements = GetPlanificationElements();

            string sMessage = ServiceApp.CheckForValidPlanification(sElements);

            if (sMessage.Length == 0)
            {
                if (tbPlanifDescription.Text.Length > 0)
                {
                    //= "NEW" si nouveau
                    string sCurrentPlanif = cbPlanifsForJob.SelectedValue.ToString();

                    string sCollisions = ckPlanifActive.IsChecked.Value ? FuzibleController.PlanifCollisions(sElements, sCurrentPlanif) : "";
                    
                    bool bOK = true;

                    if (sCollisions.Length > 0)
                    {
                        MessageBoxResult msgRcol = MessageBox.Show(Languages.Languages.jo_msg_collisiondetected01, Languages.Languages.jo_msg_collisiondetected02, MessageBoxButton.YesNo);

                        if (msgRcol.ToString().ToUpper().Equals("NO"))
                        { bOK = false; }
                    }

                    if (bOK)
                    {
                        if (sCurrentPlanif.Equals("NEW"))
                        {
                            string sStatus = FuzibleController.CreateOrUpdatePlanification("", sElements, tbPlanifDescription.Text.Trim(), tbDynParams.Text.Trim(), ckPlanifActive.IsChecked.Value);
                            MessageBox.Show(sStatus);
                            ClearUI();
                            if (FuzibleController.Planifications.Count > 0) { cbPlanifsForJob.SelectedValue = FuzibleController.Planifications.Last().PlanifID; }
                            else { cbPlanifsForJob.SelectedIndex = 0; }
                        }
                        else
                        {
                            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.jo_input_suretoreplace01, Languages.Languages.jo_input_suretoreplace02, MessageBoxButton.OKCancel);

                            if (msgR.ToString().ToUpper().Equals("OK"))
                            {
                                string sStatus = FuzibleController.CreateOrUpdatePlanification(sCurrentPlanif, sElements, tbPlanifDescription.Text.Trim(), tbDynParams.Text.Trim(), ckPlanifActive.IsChecked.Value);

                                MessageBox.Show(sStatus);

                                ClearUI();
                                cbPlanifsForJob.SelectedValue = sCurrentPlanif;
                            }
                        }
                    }
                }
                else { MessageBox.Show(Languages.Languages.jo_msg_mustwritedescription); }
            }
            else
            { MessageBox.Show(sMessage); }
        }

        private void BtnDeletePlanif_Click(object sender, RoutedEventArgs e)
        {
            if (cbPlanifsForJob.SelectedIndex > 0)
            {
                string sCurrentPlanif = cbPlanifsForJob.SelectedValue.ToString();
                
                if (FuzibleController.PlanificationExists(sCurrentPlanif))
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.jo_input_suretodelete01, Languages.Languages.jo_input_suretodelete02, MessageBoxButton.OKCancel);

                    if (msgR.ToString().ToUpper().Equals("OK"))
                    {
                        string sStatus = FuzibleController.DeletePlanification(sCurrentPlanif);
                        MessageBox.Show(sStatus);
                        ClearUI();
                    }
                }
                else { MessageBox.Show(Languages.Languages.jo_msg_chooseplaniffirst); }
            }
        }

        private void GeneratePlanifItems()
        {
            lbFrequencyHOUR.Items.Clear();
            lbFrequencyMIN.Items.Clear();
            lbFrequencyMONTH.Items.Clear();
            lbFrequencyWEEK.Items.Clear();

            for (int iMin = 0; iMin < 60; iMin += 5)
            { lbFrequencyMIN.Items.Add(new ListViewItem { Content = iMin.ToString("00"), Tag = iMin.ToString("00") }); }

            for (int iHour = 1; iHour <= 12; iHour++)
            { lbFrequencyHOUR.Items.Add(new ListViewItem { Content = string.Concat(iHour.ToString("00"), " AM"), Tag = iHour.ToString("00") }); }

            for (int iHour = 1; iHour < 12; iHour++)
            { lbFrequencyHOUR.Items.Add(new ListViewItem { Content = string.Concat(iHour.ToString("00"), " PM"), Tag = (iHour + 12).ToString("00") }); }

            for (int iWeek = 1; iWeek <= 5; iWeek++)
            { lbFrequencyWEEK.Items.Add(new ListViewItem { Content = string.Concat("Week ", iWeek.ToString("00")), Tag = iWeek.ToString("00") }); }

            for (int iMonth = 1; iMonth <= 12; iMonth++)
            { lbFrequencyMONTH.Items.Add(new ListViewItem { Content = FuzibleController.GetMonthName(iMonth), Tag = iMonth.ToString("00") }); }
        }

        private void GenerateListOfPlanifsForJob()
        {
            cbPlanifsForJob.Items.Clear();

            ComboBoxItem cbNewItem = new()
            {
                Tag = string.Concat("NEW"),
                Content = string.Concat(Languages.Languages.jo_msg_new),
                FontWeight = FontWeights.Normal,
                Visibility = Visibility.Visible,
                Foreground = Brushes.DarkRed
            };
            cbPlanifsForJob.Items.Add(cbNewItem);

            foreach (PlanifModel pm in FuzibleController.Planifications)
            {
                ComboBoxItem cbNewItemS1 = new()
                {
                    Tag = pm.PlanifID,
                    Content = pm.Description,
                    ToolTip = pm.PlanifPattern,
                    FontWeight = FontWeights.Normal,
                    Visibility = Visibility.Visible,
                    Foreground = Brushes.DarkBlue
                };
                cbPlanifsForJob.Items.Add(cbNewItemS1);
            }
        }

        private string GetPlanificationElements()
        {
            StringBuilder sbPlanif = new();

            //modèle jour : jour de semaine ou jour de mois
            if (ckDayModel.IsChecked.Value) { sbPlanif.Append("1-"); }
            else { sbPlanif.Append("2-"); }

            //compilation de la planification
            foreach (ListViewItem lvI in lbFrequencyMIN.Items)
            { if (lvI.IsSelected) { sbPlanif.Append(lvI.Tag.ToString()); } }
            sbPlanif.Append('-');

            foreach (ListViewItem lvI in lbFrequencyHOUR.Items)
            { if (lvI.IsSelected) { sbPlanif.Append(lvI.Tag.ToString()); } }
            sbPlanif.Append('-');

            foreach (ListViewItem lvI in lbFrequencyDAY.Items)
            { if (lvI.IsSelected) { sbPlanif.Append(lvI.Tag.ToString()); } }
            sbPlanif.Append('-');

            foreach (ListViewItem lvI in lbFrequencyWEEK.Items)
            { if (lvI.IsSelected) { sbPlanif.Append(lvI.Tag.ToString()); } }
            sbPlanif.Append('-');

            foreach (ListViewItem lvI in lbFrequencyMONTH.Items)
            { if (lvI.IsSelected) { sbPlanif.Append(lvI.Tag.ToString()); } }

            return sbPlanif.ToString();
        }

        private bool SetPlanificationElements(PlanifModel pmPlanif)
        {
            tbDynParams.Text = pmPlanif.Arguments;
            tbPlanifDescription.Text = pmPlanif.Description;
            ckPlanifActive.IsChecked = pmPlanif.IsActive != 0;

            string[] sSplitPlanif = pmPlanif.PlanifPattern.Split(Convert.ToChar("-"));

            if (sSplitPlanif.Length == 6)
            {
                //type de modèle
                if (sSplitPlanif[0].Equals("1"))
                { ckDayModel.IsChecked = true; }
                else { ckDayModel.IsChecked = false; }

                RefreshUI();

                IEnumerable<ListViewItem> lvMIN = lbFrequencyMIN.Items.Cast<ListViewItem>();
                IEnumerable<ListViewItem> lvHOUR = lbFrequencyHOUR.Items.Cast<ListViewItem>();
                IEnumerable<ListViewItem> lvDAY = lbFrequencyDAY.Items.Cast<ListViewItem>();
                IEnumerable<ListViewItem> lvWEEK = lbFrequencyWEEK.Items.Cast<ListViewItem>();
                IEnumerable<ListViewItem> lvMONTH = lbFrequencyMONTH.Items.Cast<ListViewItem>();
                for (int iMIN = 0; iMIN < sSplitPlanif[1].Length; iMIN += 2)
                {
                    ListViewItem lvI = lvMIN.First(i => i.Tag.ToString().Equals(sSplitPlanif[1].Substring(iMIN, 2)));
                    lvI.IsSelected = true;
                }
                for (int iHOUR = 0; iHOUR < sSplitPlanif[2].Length; iHOUR += 2)
                {
                    ListViewItem lvI = lvHOUR.First(i => i.Tag.ToString().Equals(sSplitPlanif[2].Substring(iHOUR, 2)));
                    lvI.IsSelected = true;
                }
                for (int iDAY = 0; iDAY < sSplitPlanif[3].Length; iDAY += 2)
                {
                    ListViewItem lvI = lvDAY.First(i => i.Tag.ToString().Equals(sSplitPlanif[3].Substring(iDAY, 2)));
                    lvI.IsSelected = true;
                }
                if (ckDayModel.IsChecked.Value)
                {
                    for (int iWEEK = 0; iWEEK < sSplitPlanif[4].Length; iWEEK += 2)
                    {
                        ListViewItem lvI = lvWEEK.First(i => i.Tag.ToString().Equals(sSplitPlanif[4].Substring(iWEEK, 2)));
                        lvI.IsSelected = true;
                    }
                }
                for (int iMONTH = 0; iMONTH < sSplitPlanif[5].Length; iMONTH += 2)
                {
                    ListViewItem lvI = lvMONTH.First(i => i.Tag.ToString().Equals(sSplitPlanif[5].Substring(iMONTH, 2)));
                    lvI.IsSelected = true;
                }

                return true;
            }
            else { return false; }
        }

        private void RefreshUI()
        {
            foreach (ListViewItem lvI in lbFrequencyMIN.Items)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lbFrequencyHOUR.Items)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lbFrequencyDAY.Items)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lbFrequencyWEEK.Items)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lbFrequencyMONTH.Items)
            { lvI.IsSelected = false; }

            lbFrequencyDAY.Items.Clear();

            if (ckDayModel.IsChecked.Value)
            {
                lbFrequencyWEEK.Visibility = Visibility.Visible;

                lbFrequencyDAY.Items.Clear();
                for (int iDay = 0; iDay < 7; iDay++)
                { lbFrequencyDAY.Items.Add(new ListViewItem { Content = Enum.GetName(typeof(DayOfWeek), iDay), Tag = iDay.ToString("00") }); }

                foreach (ListViewItem lvI in lbFrequencyWEEK.Items)
                { lvI.IsSelected = false; }
            }
            else
            {
                lbFrequencyWEEK.Visibility = Visibility.Hidden;

                lbFrequencyDAY.Items.Clear();
                for (int iDay = 1; iDay <= 31; iDay++)
                { lbFrequencyDAY.Items.Add(new ListViewItem { Content = Languages.Languages.jo_msg_day + iDay.ToString("00"), Tag = iDay.ToString("00") }); }

                foreach (ListViewItem lvI in lbFrequencyWEEK.Items)
                { lvI.IsSelected = true; }
            }

        }

        private void ClearUI()
        {
            GeneratePlanifItems();
            GenerateListOfPlanifsForJob();

            tbPlanifDescription.Text = "";
            tbDynParams.Text = string.Join(";", FuzibleController.DynamicParameters);

            IEnumerable<ListViewItem> lvMIN = lbFrequencyMIN.Items.Cast<ListViewItem>();
            IEnumerable<ListViewItem> lvHOUR = lbFrequencyHOUR.Items.Cast<ListViewItem>();
            IEnumerable<ListViewItem> lvDAY = lbFrequencyDAY.Items.Cast<ListViewItem>();
            IEnumerable<ListViewItem> lvWEEK = lbFrequencyWEEK.Items.Cast<ListViewItem>();
            IEnumerable<ListViewItem> lvMONTH = lbFrequencyMONTH.Items.Cast<ListViewItem>();

            foreach (ListViewItem lvI in lvMIN)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lvHOUR)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lvDAY)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lvWEEK)
            { lvI.IsSelected = false; }
            foreach (ListViewItem lvI in lvMONTH)
            { lvI.IsSelected = false; }

            cbPlanifsForJob.SelectedIndex = 0;

        }

    }

}
