using Fuzible.Controleurs;
using FuzibleFramework;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fuzible
{
    public partial class CrossQuery : Window
    {
        private readonly bool bMultiTargetPurpose = false;     
        private readonly Query SQuery;
        private readonly CrossQuery_CTL FuzibleController;

        public string OUTPUT_SCRIPT { get; internal set; } = "";

        public CrossQuery(string sUser, Query _squery, CONNString _sCS, bool _bMultiTargetPurpose, string sTitle)
        {
            FuzibleController = new CrossQuery_CTL(sUser);
            SQuery = _squery;
            bMultiTargetPurpose = _bMultiTargetPurpose;
            InitializeComponent();
            LoadConnections(_sCS);
            Title = sTitle;
        }

        private void LoadConnections(CONNString _sCS)
        {
            try
            {
                tbOutputA.Text = SQuery.OutputTable;
                Query Q = SQuery.CreateSwapedTargetQuery();
                if (Q != null)
                { tbOutputB.Text = Q.OutputTable; }

                if (bMultiTargetPurpose)
                {
                    lbJoinType.Visibility = Visibility.Hidden;
                    cbJoinType.Visibility = Visibility.Hidden;
                    tbOutputA.Visibility = Visibility.Visible;
                    tbOutputB.Visibility = Visibility.Visible;
                    lbCrossQueryFilter.Visibility = Visibility.Hidden;
                    tbCrossQueryFilter.Visibility = Visibility.Hidden;
                    cbFirstConnection.IsEnabled = true;
                    lbTechnicalInfo.Content = Languages.Languages.cq_msg_multitarget_techrestriction;
                }
                else
                {
                    lbJoinType.Visibility = Visibility.Visible;
                    cbJoinType.Visibility = Visibility.Visible;
                    tbOutputA.Visibility = Visibility.Hidden;
                    tbOutputB.Visibility = Visibility.Hidden;
                    lbCrossQueryFilter.Visibility = Visibility.Visible;
                    tbCrossQueryFilter.Visibility = Visibility.Visible;
                    cbFirstConnection.IsEnabled = false;
                    lbTechnicalInfo.Content = Languages.Languages.cq_msg_crossquery_techrestriction;
                }

                cbFirstConnection.Items.Clear();
                cbSecondConnection.Items.Clear();
                foreach (CONNString CS in FuzibleController.GetConnections())
                {
                    List<string> sListUsage = FuzibleController.CheckConnStringUsage(CS.SConnID);
                    ComboBoxItem cbNewItem = new()
                    {
                        Tag = CS.SConnID,
                        Content = string.Concat(CS.SConnID, " -> ", CS.SConnDriver.ToString().Split(Convert.ToChar("_"))[1], " : ", CS.SConnName),
                        ToolTip = sListUsage.Count == 0 ? "" : string.Concat(Languages.Languages.cq_msg_usedinonemorejobs, Environment.NewLine, string.Join(Environment.NewLine, sListUsage)),
                        Foreground = sListUsage.Count > 0 ? Brushes.DarkRed : Brushes.DarkGreen
                    }; ;
                    ComboBoxItem cbNewItem2 = new()
                    {
                        Tag = CS.SConnID,
                        Content = string.Concat(CS.SConnID, " -> ", CS.SConnDriver.ToString().Split(Convert.ToChar("_"))[1], " : ", CS.SConnName),
                        ToolTip = sListUsage.Count == 0 ? "" : string.Concat(Languages.Languages.cq_msg_usedinonemorejobs, Environment.NewLine, string.Join(Environment.NewLine, sListUsage)),
                        Foreground = sListUsage.Count > 0 ? Brushes.DarkRed : Brushes.DarkGreen
                    }; ;
                    cbFirstConnection.Items.Add(cbNewItem);
                    cbSecondConnection.Items.Add(cbNewItem2);
                }

                try { cbFirstConnection.SelectedValue = _sCS.SConnID; }
                catch { }

            }
            catch (Exception ex)
            { MessageBox.Show(Languages.Languages.cq_msg_cantloadconn + ex.Message); }
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {

            if (tbCrossQueryFilter.Text.Length > 0 && (!tbCrossQueryFilter.Text.StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show(Languages.Languages.cq_msg_crossquery_filterinfo);
            }
            else
            {
                if (cbFirstConnection.SelectedIndex > -1 && cbSecondConnection.SelectedIndex > -1)
                {
                    string s1 = cbFirstConnection.SelectedValue.ToString();
                    string s2 = cbSecondConnection.SelectedValue.ToString();
                    string sOut1 = tbOutputA.Text.Trim();
                    string sOut2 = tbOutputB.Text.Trim();

                    if (bMultiTargetPurpose) //en mode multi-target on récupère le script target 
                    {
                        OUTPUT_SCRIPT = string.Concat(s1, sOut1.Length > 0 ? sOut1 : SQuery.OutputTable, s2, sOut2.Length > 0 ? sOut2 : SQuery.OutputTable);
                    }
                    else //sinon uniquement le script cross-join
                    {
                        string sScriptJoin = "";
                        switch (cbJoinType.SelectedValue.ToString())
                        {
                            case "INNER":
                                sScriptJoin = "--";
                                break;
                            case "LEFT":
                                sScriptJoin = "->";
                                break;
                            case "RIGHT":
                                sScriptJoin = "<-";
                                break;
                            case "OUTER":
                                sScriptJoin = "<>";
                                break;
                        }

                        OUTPUT_SCRIPT = string.Concat("[", sScriptJoin, s2, tbCrossQueryFilter.Text.Trim(), "]");

                        MessageBox.Show(Languages.Languages.cq_msg_crossquery_writequery);
                    }

                    Close();
                }
                else { MessageBox.Show(Languages.Languages.cq_msg_crossquery_musthave2conn); }
            }
        }
    }
}
