using Fuzible.Controleurs;
using FuzibleFramework;
using FuzibleUITools;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using Org.BouncyCastle.Tls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Fuzible
{
    /// <summary>
    /// Logique d'interaction pour Configuration.xaml
    /// </summary>
    public partial class Configuration : Window
    {
        #region "EVENEMENTS"

        private readonly Configuration_CTL FuzibleController;
        private readonly string USERNAME;
        public readonly string JobDbSource = "";
        public readonly string JobDbTarget = "";


        public Configuration(string sUsername, CONNString _defaultCS, int _iIndex, string _dbSource, string _dbTarget)
        {
            FuzibleController = new Configuration_CTL(sUsername);
            USERNAME = sUsername;
            JobDbSource = _dbSource;
            JobDbTarget = _dbTarget;

            InitializeComponent();

            AttachMouseDownEventToAllControls(this);

            Activate();
            Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.75;
            tcMenu.SelectedIndex = _iIndex;
            LoadParams(_defaultCS);
            ColorizeRichTextBox();

        }

        private void Configuration_KeyDown(object sender, KeyEventArgs e)
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
                    await Program.LiveHelp.LoadHelpBlock(new BlockHelp("Configuration", sControlName, "", ""));
                }
            }
        }

        private void TbWSHeaders_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            SetColorsParameters(";", ":", tbWSHeaders);
        }

        private void TbWSQueryParams_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            SetColorsParameters("&", "=", tbWSQueryParams);
        }

        private void CbListConnections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbListConnections.SelectedIndex > -1)
            {
                var cbI = (ComboBoxItem)cbListConnections.Items[cbListConnections.SelectedIndex];
                CONNString ActualConn = FuzibleController.GetConnection(cbI.Tag.ToString());
                if (ActualConn != null)
                {
                    LoadConnection(ActualConn);
                    ConnectionsParamsCanvas(ActualConn);
                }
            }
        }

        private void CbFileComingFrom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFileComingFrom.SelectedIndex > -1)
            {
                var cbI = (ComboBoxItem)cbFileComingFrom.Items[cbFileComingFrom.SelectedIndex];
                switch (cbI.Tag.ToString())
                {
                    case "LOCAL":
                        CanvasFileFTPParams.Visibility = Visibility.Hidden;
                        CanvasFileNetworkParams.Visibility = Visibility.Hidden;
                        break;
                    case "NETWORK":
                        CanvasFileFTPParams.Visibility = Visibility.Hidden;
                        CanvasFileNetworkParams.Visibility = Visibility.Visible;
                        break;
                    case "FTP":
                        CanvasFileFTPParams.Visibility = Visibility.Visible;
                        CanvasFileNetworkParams.Visibility = Visibility.Hidden;
                        break;
                    case "SFTP":
                        CanvasFileFTPParams.Visibility = Visibility.Visible;
                        CanvasFileNetworkParams.Visibility = Visibility.Hidden;
                        break;
                }
            }
        }

        private void CbDriverSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDriverSource.SelectedIndex > -1)
            {
                var cbI = (ComboBoxItem)cbDriverSource.Items[cbDriverSource.SelectedIndex];
                string sDriver = cbI.Tag.ToString();
                var bDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
                CONNString CST = new(bDD, "", "", "", null);
                switch (bDD)
                {
                    case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                        btnScanSQLInstances.Visibility = Visibility.Hidden;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_ad;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_mysql;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        ProgramHelp AppHelp = new(FuzibleController.MainParams.APP_LANGUAGE);
                        Help xamlHelp = new(AppHelp.GetHelpBlock("SQL_COMPATIBILITY_PARAMS"));
                        xamlHelp.ShowDialog();
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_odbc;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_odbc;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_oracle;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_postgres;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_sqlserver;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_sqlite;
                        break;
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_access;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_access;
                        break;
                    case SQLTools_Enums.BDD.NS_MONGODB:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_sql;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_mongodb;
                        break;
                    case SQLTools_Enums.BDD.FI_CSV:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_file;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_file;
                        break;
                    case SQLTools_Enums.BDD.FI_FILE:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_file;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_file;
                        break;
                    case SQLTools_Enums.BDD.FI_JSON:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_file;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_file;
                        break;
                    case SQLTools_Enums.BDD.FI_XLS:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_file;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_file;
                        break;
                    case SQLTools_Enums.BDD.FI_XML:
                        btnScanSQLInstances.Visibility = Visibility.Visible;
                        btnScanSQLInstances.Content = Languages.Languages.mc_connections_btn_scan_file;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_file;
                        break;
                    case SQLTools_Enums.BDD.MB_MAIL:
                        btnScanSQLInstances.Visibility = Visibility.Hidden;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_mail;
                        break;
                    case SQLTools_Enums.BDD.WS_REST:
                        btnScanSQLInstances.Visibility = Visibility.Hidden;
                        lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_ws;
                        //lbExportConnectionString.Content = Languages.Languages.mc_connections_lbl_howtoconnstring_nuxeo;
                        break;
                }
                ConnectionsParamsCanvas(CST);
            }
        }

        private void CbWSAuthorizationMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbWSAuthorizationMethod.SelectedIndex > -1)
            {
                var cbI = (ComboBoxItem)cbWSAuthorizationMethod.Items[cbWSAuthorizationMethod.SelectedIndex];
                var wAuth = (SQLTools_Enums.WEBSERVICE_AUTHORIZATION)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_AUTHORIZATION), cbI.Tag.ToString());
                switch (wAuth)
                {
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.NO_AUTH:
                        lbWSAuthorizationKey.Content = "";
                        lbWSAuthorizationValue.Content = "";
                        lbWSAuthorizationURL.Content = "";
                        lbWSAuthorizationKey.Visibility = Visibility.Hidden;
                        lbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSAuthorizationKey.Visibility = Visibility.Hidden;
                        tbWSAuthorizationValue.Visibility = Visibility.Hidden;
                        tbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Hidden;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_HEADER:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_key_01;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_01;
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_auth_url_01;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        tbWSAuthorizationValue.Visibility = Visibility.Visible;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Visible;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.API_AUTH_PARAM:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_key_01;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_01;
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_auth_url_01;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        tbWSAuthorizationValue.Visibility = Visibility.Visible;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Visible;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BASIC_AUTH:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_key_02;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_02;
                        lbWSAuthorizationURL.Content = "";
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        tbWSAuthorizationValue.Visibility = Visibility.Visible;
                        tbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Visible;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.BEARER:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_key_01;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_03;
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_auth_url_01;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        tbWSAuthorizationValue.Visibility = Visibility.Visible;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Visible;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.HTTP:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_key_01;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_04;
                        lbWSAuthorizationURL.Content = "";
                        lbWSAuthorizationKey.Visibility = Visibility.Hidden;
                        lbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSAuthorizationKey.Visibility = Visibility.Hidden;
                        tbWSAuthorizationValue.Visibility = Visibility.Hidden;
                        tbWSAuthorizationURL.Visibility = Visibility.Hidden;
                        tbWSQueryParams.Visibility = Visibility.Visible;
                        btnShowRestPassword.Visibility = Visibility.Hidden;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Hidden;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2:
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Visible;
                        lbWSSalesforceConsumerKS.Content = string.Concat(Languages.Languages.mc_connections_lbl_rest_oauth2_clientid, " + ", Languages.Languages.mc_connections_lbl_rest_oauth2_clientsecret);
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_oauth_tokenurl;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_oauth_scope;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationValue.Visibility = Visibility.Visible;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_04;
                        tbWSAuthorizationValue.Visibility = Visibility.Hidden;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Visible;
                        btnShowRestPassword.Visibility = Visibility.Hidden;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;

                        RichTB.SetTextRTB(tbWSHeaders, "");
                        string sParams = RichTB.GetTextRTB(tbWSQueryParams);
                        if (sParams.IndexOf("grant_type=client_credentials", StringComparison.OrdinalIgnoreCase) == -1)
                        { sParams = string.Concat("grant_type=client_credentials", (sParams.Length == 0 ? "" : "&"), sParams); }
                        RichTB.SetTextRTB(tbWSQueryParams, sParams);
                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.OAUTH2DELEGATED:
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Visible;
                        lbWSSalesforceConsumerKS.Content = string.Concat(Languages.Languages.mc_connections_lbl_rest_oauth2_clientid, " + ", Languages.Languages.mc_connections_lbl_rest_oauth2_clientsecret);
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_oauth_tokenurl;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_oauth_scope;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationValue.Visibility = Visibility.Visible;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_value_04;
                        tbWSAuthorizationValue.Visibility = Visibility.Hidden;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Visible;
                        btnShowRestPassword.Visibility = Visibility.Hidden;
                        lbWSSalesforceUserToken.Visibility = Visibility.Hidden;
                        tbWSSalesforceUserToken.Visibility = Visibility.Hidden;

                        //en délégué on vérifie que les headers user, pwd et tenant id sont bien ajoutés

                        //Tenant-Id=xxx;userName=xxx;password=xxx
                        string sHeaders = RichTB.GetTextRTB(tbWSHeaders);
                        if (sHeaders.IndexOf("Tenant-Id", StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            sHeaders = string.Concat(sHeaders.EndsWith(";") ? sHeaders[0..^1] : sHeaders, ";", "Tenant-Id={Tenant-Id}");
                        }
                        if (sHeaders.IndexOf("userName", StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            sHeaders = string.Concat(sHeaders.EndsWith(";") ? sHeaders[0..^1] : sHeaders, ";", "userName={userName}");
                        }
                        if (sHeaders.IndexOf("password", StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            sHeaders = string.Concat(sHeaders.EndsWith(";") ? sHeaders[0..^1] : sHeaders, ";", "password={password}");
                        }
                        RichTB.SetTextRTB(tbWSHeaders, sHeaders);

                        RichTB.SetTextRTB(tbWSQueryParams, "grant_type=password");

                        break;
                    case SQLTools_Enums.WEBSERVICE_AUTHORIZATION.SALESFORCE_OAUTH:
                        lbWSAuthorizationKey.Content = Languages.Languages.mc_connections_lbl_rest_auth_sf_userlogin;
                        lbWSAuthorizationValue.Content = Languages.Languages.mc_connections_lbl_rest_auth_sf_userpwd;
                        lbWSAuthorizationURL.Content = Languages.Languages.mc_connections_lbl_rest_auth_sf_urltoken;
                        lbWSAuthorizationKey.Visibility = Visibility.Visible;
                        lbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSAuthorizationKey.Visibility = Visibility.Visible;
                        tbWSAuthorizationValue.Visibility = Visibility.Visible;
                        tbWSAuthorizationURL.Visibility = Visibility.Visible;
                        tbWSQueryParams.Visibility = Visibility.Hidden;
                        btnShowRestPassword.Visibility = Visibility.Visible;
                        lbWSSalesforceUserToken.Visibility = Visibility.Visible;
                        tbWSSalesforceUserToken.Visibility = Visibility.Visible;
                        lbWSSalesforceConsumerKS.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerSecret.Visibility = Visibility.Visible;
                        tbWSSalesforceConsumerKey.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private async void BtnTestSQLLogConnection_Click(object sender, RoutedEventArgs e)
        {
            string sStatus = await Configuration_CTL.CheckGenericConnection(cbDriverLog.SelectedValue.ToString(), RichTB.GetTextRTB(tbConnectionStringLog));
            MessageBox.Show(sStatus);
        }

        private async void BtnTestServiceConnection_Click(object sender, RoutedEventArgs e)
        {
            string sStatus = await Configuration_CTL.CheckGenericConnection(cbDriverServiceApp.SelectedValue.ToString(), RichTB.GetTextRTB(tbConnectionStringServiceApp));
            MessageBox.Show(sStatus);
        }

        private async void BtnTestLogMailConnection_Click(object sender, RoutedEventArgs e)
        {
            string sStatus = await Configuration_CTL.CheckMailConnection(BuildMailConnString(true), ckMAILUseSSLLog.IsChecked.Value, cbMailAuthProtocolLog.SelectedValue.ToString());
            MessageBox.Show(sStatus);
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            string sConnString = RichTB.GetTextRTB(tbConnectionString).Trim();

            try
            {
                if ((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()) == SQLTools_Enums.BDD.MB_MAIL)
                {
                    sConnString = BuildMailConnString(false);
                }
            }
            catch { }

            if (cbDriverSource.SelectedIndex > -1 && sConnString.Length > 0)
            {
                var sDriver = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString());

                string[] sConnVars = SetConnectionParams(true, sDriver).Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                CONNString sConn = new(sDriver, "[0]", "", sConnString, sConnVars.ToList());
                if (sConn.SConnString(null).IndexOf("{?") > 0)
                {
                    MessageBox.Show(Languages.Languages.mc_msg_tryconndynparamsbypassed);
                }
                List<string> sAnswers = await Toolbox.CheckConnection(sConn, new List<string>(), "", Monitoring.TaskCancellationToken);
                MessageBox.Show(sAnswers[0]);
                sAnswers.RemoveAt(0);
            }
            else { MessageBox.Show(Languages.Languages.mc_msg_tryconnnothingtotry); }
        }

        private void BtnShowPassword_Click(object sender, RoutedEventArgs e)
        {
            var btnPwd = (Button)sender;
            string sPassword = "";
            switch (btnPwd.Name)
            {
                case "btnShowFTPPassword":
                    sPassword = tbFTPPassword.Password;
                    break;
                case "btnShowNetwordPassword":
                    sPassword = tbNetworkPassword.Password;
                    break;
                case "btnShowNuxeoPassword":
                    sPassword = tbWSNuxeoPassword.Password;
                    break;
                case "btnShowRestPassword":
                    sPassword = tbWSAuthorizationValue.Password;
                    break;
                case "btnShowSalesforceSecret":
                    sPassword = tbWSSalesforceConsumerSecret.Password;
                    break;
                case "btnShowMailPassword":
                    sPassword = tbMailConnPasswordSend.Password;
                    break;
                case "btnShowMailLogPassword":
                    sPassword = tbMailLogPassword.Password;
                    break;
            }
            Help xamlHelp = new(new BlockHelp("Configuration", Languages.Languages.mc_msg_showpassword, sPassword, ""));
            xamlHelp.ShowDialog();
        }

        private void BtnCreateStackTable_Click(object sender, RoutedEventArgs e)
        {
            string sSchema = "";

            if (cbDriverServiceApp.SelectedValue.ToString().Equals("DB_POSTGRE"))
            {
                var fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_schema, Languages.Languages.mc_input_assconn_schema, false, true, "public");
                fP.ShowDialog();
                sSchema = fP.PromptUserData.Trim();
            }
            else if (cbDriverServiceApp.SelectedValue.ToString().Equals("DB_SQLSERVER"))
            {
                var fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_schema, Languages.Languages.mc_input_assconn_schema, false, true, "dbo");
                fP.ShowDialog();
                sSchema = fP.PromptUserData.Trim();
            }

            string sStatus = FuzibleController.CreateStackTable(RichTB.GetTextRTB(tbConnectionStringServiceApp), cbDriverServiceApp.SelectedValue.ToString(), sSchema);
            MessageBox.Show(sStatus);
        }

        private void BtnOpenClientINIPath_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo psi = new()
            {
                FileName = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible\\",
                UseShellExecute = true
            };
            Process.Start(psi);
            //VistaFolderBrowserDialog lFD = new VistaFolderBrowserDialog();
            //lFD.ShowDialog();
        }

        private void BtnCreateLogTable_Click(object sender, RoutedEventArgs e)
        {
            string sSchema = "";
            if (cbDriverLog.SelectedValue.ToString().Equals("DB_POSTGRE"))
            {
                var fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_schema, Languages.Languages.mc_input_assconn_schema, false, true, "public");
                fP.ShowDialog();
                sSchema = fP.PromptUserData.Trim();
            }
            else if (cbDriverLog.SelectedValue.ToString().Equals("DB_SQLSERVER"))
            {
                var fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_schema, Languages.Languages.mc_input_assconn_schema, false, true, "dbo");
                fP.ShowDialog();
                sSchema = fP.PromptUserData.Trim();
            }
            string sStatus = FuzibleController.CreateLogTables(RichTB.GetTextRTB(tbConnectionStringLog), cbDriverLog.SelectedValue.ToString(), sSchema);
            MessageBox.Show(sStatus);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            //sauvegarde modifs connexion en cours ?
            if (cbListConnections.SelectedIndex > -1 && (tbConnectionName.Text.Length > 0 || RichTB.GetTextRTB(tbConnectionString).Length > 0))
            {
                CONNString ActualConn = FuzibleController.GetConnection(cbListConnections.SelectedValue.ToString());

                string sP1 = SetConnectionParams(true, (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()));
                string sP2 = string.Join(Environment.NewLine, ActualConn.SConnParams);

                if (!ActualConn.SConnString(null).Equals(RichTB.GetTextRTB(tbConnectionString))
                    || !ActualConn.SConnName.Equals(tbConnectionName.Text)
                    || !ActualConn.SConnDriver.Equals((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()))
                    || !sP1.Equals(sP2))
                {
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_saveconnectionbeforeexit01, Languages.Languages.mc_msg_saveconnectionbeforeexit02, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        ModifyExistingConnection(ActualConn);
                    }
                }
            }
            //création nouvelle connexion
            else if (cbListConnections.SelectedIndex == -1 && (tbConnectionName.Text.Length > 0 || RichTB.GetTextRTB(tbConnectionString).Length > 0))
            {
                MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_saveconnectionbeforeexit01, Languages.Languages.mc_msg_saveconnectionbeforeexit02, MessageBoxButton.YesNo);
                if (msgR.ToString().ToUpper().Equals("YES"))
                {
                    SaveNewConnection();
                }
            }

            try
            {
                bool bCheck = CheckFieldsForErrors();

                if (bCheck)
                {
                    var sParameters = new List<object>();

                    //debut
                    sParameters.Add(tbMAILAdmin.Text.Split(Convert.ToChar(";")).ToList());
                    sParameters.Add(Convert.ToInt32(tbProcessorCount.Text.Trim()));
                    sParameters.Add((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverLog.SelectedValue.ToString()));

                    string sLogConnString = RichTB.GetTextRTB(tbConnectionStringLog);
                    sParameters.Add(Regex.Replace(sLogConnString, "(; )([A-z0-9-_]+)", ";$2"));

                    //

                    //if (RichTB.GetTextRTB(tbConnectionStringMail).Length > 0)
                    //{
                    //    ReplaceAuthProtocolInMailCS(tbConnectionStringMail, cbMailAuthProtocolLog);
                    //    ReplaceSSLInMailCS(tbConnectionStringMail, ckMAILUseSSLLog);
                    //}
                    string sConnStringMailLog = BuildMailConnString(true);

                    //suite
                    sParameters.Add(sConnStringMailLog);
                    sParameters.Add(Convert.ToInt32(tbDaysKeepLog.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbAbortJobErrors.Text.Trim()));
                    sParameters.Add(ckAvoidMailIfSuccess.IsChecked.Value);
                    sParameters.Add(ckAllowSpecialIntegers.IsChecked.Value);
                    sParameters.Add(ckAllowIntegersWith0.IsChecked.Value);
                    sParameters.Add(Convert.ToInt32(tbSQLErrorsOverflow.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSQLKeepCharsDebug.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSQLCommandTimeout.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSQLCommit.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSQLDirectStreamCommit.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSQLMaxDecimals.Text.Trim()));
                    sParameters.Add(Toolbox.RemoveSpecialCharacters(tbSQLSynchroModeTable.Text.Trim(), "_", false));
                    sParameters.Add(Toolbox.RemoveSpecialCharacters(tbFILEExported.Text.Trim(), "_", false));
                    sParameters.Add(Toolbox.RemoveSpecialCharacters(tbFILEProcessed.Text.Trim(), "_", false));
                    sParameters.Add(ckAddDatatimeOnMovedFiles.IsChecked.Value);
                    sParameters.Add(Convert.ToInt32(tbFileMaxRowsHeaderSeparatorAnalyzer.Text.Trim()));
                    sParameters.Add(ckForceCSVIntegrationWrongLength.IsChecked.Value);
                    sParameters.Add(Convert.ToInt32(tbFileMaxRowsBeforeSplit.Text.Trim()));
                    sParameters.Add(!ckCSVSplitDontCheckFTP.IsChecked.Value);
                    sParameters.Add(Convert.ToDecimal(tbCSVAverageUniqueOffset.Text.Trim()));
                    sParameters.Add(Convert.ToDecimal(tbCSVResemblanceOffset.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbCSVRowsOffsetBeforeDepthAnalysis.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbDaysKeepProcessedFiles.Text.Trim()));
                    sParameters.Add(tbCSVSeparators.Text.Trim().Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\b", "\b").Replace("\\v", "\v"));
                    sParameters.Add(Convert.ToInt32(tbMAILMaxPJSize.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbMAILMaxTimeout.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbMAILMaxLengthBeforePJ.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbWSMaxTimeout.Text.Trim()));
                    sParameters.Add(ckAllowChangeSchemaInSource.IsChecked.Value);
                    sParameters.Add(ckWSSOQLRecordsOnly.IsChecked.Value);
                    sParameters.Add(tbWSDefaultEncoding.Text.Trim());
                    sParameters.Add(tbWSNuxeoDefaultEncoding.Text.Trim());
                    sParameters.Add(ckMultiTargetInParallel.IsChecked.Value);
                    sParameters.Add(ckAutoshrinktables.IsChecked.Value);
                    sParameters.Add(ckShowSystemAlerts.IsChecked.Value);
                    //

                    FuzibleController.SetClientParams(cbDriverServiceApp.SelectedValue.ToString(), RichTB.GetTextRTB(tbConnectionStringServiceApp), tbConnectionStringServiceAppSchema.Text.ToString(), cbServiceAppUsers.SelectedValue.ToString());

                    //fin
                    sParameters.Add(ckRemoveSpecialsCharsJsonParser.IsChecked.Value);
                    sParameters.Add(ckEnableQueryAssistant.IsChecked.Value);
                    sParameters.Add(Convert.ToInt32(tbQueryAssistantMaxFileSize.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSHellOperationsFuzibleTimeout.Text.Trim()));
                    sParameters.Add(Convert.ToInt32(tbSHellOperationsExtTimeout.Text.Trim()));
                    sParameters.Add(ckSecuritySharedUsers.IsChecked.Value);
                    sParameters.Add(cbSecurityUsers.SelectedValue.ToString());
                    sParameters.Add(Convert.ToInt32(tbProcessorCount_BigData.Text.Trim()));
                    sParameters.Add(ckDebugFunctions.IsChecked.Value);
                    sParameters.Add(Convert.ToInt32(tbSQLBulkRate.Text.Trim()));
                    sParameters.Add(ckJsonAlternativeProcessingMode.IsChecked.Value);
                    sParameters.Add(tbConnectionStringLogSchema.Text.Trim());
                    //


                    FuzibleController.SaveMainConfig(sParameters);

                    var sBDDService = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverServiceApp.SelectedValue.ToString());
                    int iKeepLog = Convert.ToInt32(tbDaysKeepStackDataServiceApp.Text.Trim());
                    int iKillIdleJobs = Convert.ToInt32(tbKillRunningJobsServiceApp.Text.Trim());
                    string sUserService = cbServiceAppUsers.SelectedValue.ToString();
                    int iParallelJobs = Convert.ToInt32(tbParallelJobsServiceApp.Text.Trim());


                    string sSendJobReport = cbSendJobReports.SelectedValue.ToString();
                    int iSendJobReportHour = Convert.ToInt32(tbSendJobReportsHour.Text.Trim());
                    string sSendJobReportAmPm = cbSendJobReportsAmPm.SelectedValue.ToString();
                    string sRecipientsJobbReports = tbMailSendJobReport.Text.Trim();

                    List<string> sListSQLParams = SQLQueries.ParamsForSQLDriver(sBDDService, tbConnectionStringLogSchema.Text.Trim());
                    CONNString csService = new(sBDDService, "[0]", "", RichTB.GetTextRTB(tbConnectionStringServiceApp), sListSQLParams);

                    bool bCheckChangeService = FuzibleController.CheckChangeService(sBDDService, sUserService, csService.SConnString(null), csService.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA), 0);
                    bool bOKModificationService = true;

                    if (bCheckChangeService)
                    {
                        //juste un changement d'user
                        if (FuzibleController.CheckChangeService(sBDDService, sUserService, csService.SConnString(null), csService.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA), 1))
                        {
                            MessageBox.Show(Languages.Languages.mc_msg_changeduserspaceserviceconfirm);
                        }
                        else //un changement total de BDD
                        {
                            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_changedserviceconfmigration01, Languages.Languages.mc_msg_changedserviceconfmigration02, MessageBoxButton.YesNo);
                            if (msgR.ToString().ToUpper().Equals("YES"))
                            {
                                string sStatus = FuzibleController.MigrateServiceConfiguration(csService, sUserService);
                                Help xamlHelp = new(new BlockHelp("Configuration", Languages.Languages.mc_msg_migrationlog, sStatus, ""));
                                xamlHelp.ShowDialog();
                            }
                            MessageBoxResult msgRB = MessageBox.Show(Languages.Languages.mc_msg_overwriteserviceparams01, Languages.Languages.mc_msg_overwriteserviceparams02, MessageBoxButton.YesNo);
                            if (msgRB.ToString().ToUpper().Equals("YES"))
                            {
                                bOKModificationService = true;
                            }
                            else { bOKModificationService = false; }
                        }
                    }


                    if (bOKModificationService)
                    {
                        string sError = FuzibleController.SetServiceParams(sBDDService, iKeepLog, iKillIdleJobs, iParallelJobs, Convert.ToInt32(tbSameJobLatencyClientApp.Text), sUserService, csService, sSendJobReport, iSendJobReportHour, sSendJobReportAmPm, sRecipientsJobbReports);
                        if (sError.Length > 0)
                        {
                            MessageBox.Show(sError);
                        }
                    }

                    ColorizeRichTextBox();

                    MessageBox.Show(Languages.Languages.mc_msg_configurationsavecok);
                }
                else { MessageBox.Show(Languages.Languages.mc_msg_configurationsaveckoinputerrors); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Languages.Languages.mc_msg_configurationsavecko + ex.Message);
            }

        }

        private void BtnScanSQLInstances_Click(object sender, RoutedEventArgs e)
        {
            if (cbDriverSource.SelectedIndex == -1)
            {
                MessageBox.Show(Languages.Languages.mc_msg_scanconnmustselectsqldriver);
            }
            else
            {
                var sDriver = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString());
                if (sDriver.ToString().StartsWith("DB_"))
                {
                    IsEnabled = false;
                    GetSQLInstances(sDriver);
                    IsEnabled = true;
                }
                if (sDriver.ToString().StartsWith("NS_"))
                {
                    IsEnabled = false;
                    GetSQLInstances(sDriver);
                    IsEnabled = true;
                }
                if (sDriver.ToString().StartsWith("FI_"))
                {
                    VistaFolderBrowserDialog lFD = new();
                    lFD.ShowDialog();
                    if (lFD.SelectedPath.Length > 0)
                    {
                        string sPath = lFD.SelectedPath;
                        string[] files = Directory.GetFiles(sPath);
                        MessageBox.Show(Languages.Languages.mc_msg_scanconnfilesfound + files.Length.ToString());
                        RichTB.SetTextRTB(tbConnectionString, sPath);
                        if (sPath.StartsWith("\\\\")) //network path genre \\ONYXIA\factures
                        {
                            cbFileComingFrom.SelectedValue = "NETWORK";
                        }
                    }
                }
            }
        }

        private void BtnAddConnection_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_addconnwithactualparams01, Languages.Languages.mc_msg_addconnwithactualparams02, MessageBoxButton.YesNo);
            if (msgR.ToString().ToUpper().Equals("YES"))
            {
                if (cbDriverSource.SelectedIndex > -1)
                {
                    tbConnectionName.Text = string.Concat(Languages.Languages.mc_msg_copyof, tbConnectionName.Text);
                    SaveNewConnection();
                }
                else
                {
                    MessageBox.Show(Languages.Languages.mc_msg_addconnnoconnselected);
                }
            }
            else
            {
                tbConnectionName.Text = "";
                RichTB.SetTextRTB(tbConnectionString, "");
                cbDriverSource.SelectedIndex = -1;
                cbListConnections.SelectedIndex = -1;
                MessageBox.Show(Languages.Languages.mc_msg_addconnfillfieldsandsave);
                ConnectionsParamsCanvas(null);
            }
        }

        private void BtnFillConnectionString_Click(object sender, RoutedEventArgs e)
        {
            FuzibleUITools.Prompt fP;

            if (cbDriverSource.SelectedIndex > -1)
            {
                SQLTools_Enums.BDD bddCall;
                string sPreFill = "";

                bddCall = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString());

                string sServer, sDriver, sPwd, sUser, sDB, sPort, sURL;

                switch (bddCall)
                {
                    case SQLTools_Enums.BDD.NS_MONGODB:
                        //TODO NOSQL
                        break;
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        //Persist Security Info=False;
                        //Jet OLEDB:Database Password=MyDbPassword;
                        string sMsgPersistSecurity;
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_datasource_access, Languages.Languages.mc_input_assconn_datasource_access, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        MessageBoxResult msgPwd = MessageBox.Show(Languages.Languages.mc_input_assconn_pwdask_access, Languages.Languages.mc_input_assconn_pwdask_access, MessageBoxButton.YesNo);
                        if (msgPwd.ToString().ToUpper().Equals("YES"))
                        {
                            fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_access, Languages.Languages.mc_input_assconn_pwd_access, true, true);
                            fP.ShowDialog();
                            sPwd = fP.PromptUserData.Trim();
                        }
                        else { sPwd = ""; }
                        MessageBoxResult msgPersistSecurity = MessageBox.Show(Languages.Languages.mc_input_assconn_persistsecurity_access, Languages.Languages.mc_input_assconn_persistsecurity_access, MessageBoxButton.YesNo);
                        if (msgPersistSecurity.ToString().ToUpper().Equals("YES")) { sMsgPersistSecurity = ";Persist Security Info=True;"; } else { sMsgPersistSecurity = ";Persist Security Info=False;"; }
                        sPreFill = string.Concat("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=", sServer, sPwd.Length > 0 ? (";Jet OLEDB:Database Password=" + sPwd) : "", sMsgPersistSecurity);
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        string sSSL = "SslMode=none";
                        string sZD = ";";
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_server_sql, Languages.Languages.mc_input_assconn_server_sql, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_username_sql, Languages.Languages.mc_input_assconn_username_sql, false, true);
                        fP.ShowDialog();
                        sUser = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_sql, Languages.Languages.mc_input_assconn_pwd_sql, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_port_sql, Languages.Languages.mc_input_assconn_port_sql, false, true, "3306");
                        fP.ShowDialog();
                        sPort = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbname_sql, Languages.Languages.mc_input_assconn_dbname_sql, false, true);
                        fP.ShowDialog();
                        sDB = fP.PromptUserData.Trim();
                        MessageBoxResult msgZD = MessageBox.Show(Languages.Languages.mc_input_assconn_convert0datetime_mysql, Languages.Languages.mc_input_assconn_convert0datetime_mysql, MessageBoxButton.YesNo);
                        MessageBoxResult msgSSL = MessageBox.Show(Languages.Languages.mc_input_assconn_usessl_postgres, Languages.Languages.mc_input_assconn_usessl_postgres, MessageBoxButton.YesNo);
                        if (msgZD.ToString().ToUpper().Equals("YES")) { sZD = "Zero datetime=True;"; }
                        if (msgSSL.ToString().ToUpper().Equals("YES")) { sSSL = "SslMode=Preferred"; }
                        sPreFill = string.Concat("Server=", sServer, ";Uid=", sUser, ";Pwd=", sPwd, ";Port=", sPort, ";DATABASE=", sDB, ";", sZD, sSSL + ";");
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_driver_odbc, Languages.Languages.mc_input_assconn_driver_odbc, false, true);
                        fP.ShowDialog();
                        sDriver = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_server_sql, Languages.Languages.mc_input_assconn_server_sql, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_username_sql, Languages.Languages.mc_input_assconn_username_sql, false, true);
                        fP.ShowDialog();
                        sUser = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_sql, Languages.Languages.mc_input_assconn_pwd_sql, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_port_sql, Languages.Languages.mc_input_assconn_port_sql, false, true);
                        fP.ShowDialog();
                        sPort = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbname_sql, Languages.Languages.mc_input_assconn_dbname_sql, false, true);
                        fP.ShowDialog();
                        sDB = fP.PromptUserData.Trim();
                        sPreFill = string.Concat("DRIVER={", sDriver, "};Server Name=", sServer, ";Server Port=", sPort, ";Database=", sDB, ";UID=", sUser, ";PWD=", sPwd, ";");
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        string sIntSecO;
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_server_sql, Languages.Languages.mc_input_assconn_server_sql, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_username_sql, Languages.Languages.mc_input_assconn_username_sql, false, true);
                        fP.ShowDialog();
                        sUser = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_sql, Languages.Languages.mc_input_assconn_pwd_sql, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_port_sql, Languages.Languages.mc_input_assconn_port_sql, false, true, "5432");
                        fP.ShowDialog();
                        sPort = fP.PromptUserData.Trim();
                        MessageBoxResult msgIntSecO = MessageBox.Show(Languages.Languages.mc_input_assconn_intsecurity_sqlserver, Languages.Languages.mc_input_assconn_intsecurity_sqlserver, MessageBoxButton.YesNo);
                        if (msgIntSecO.ToString().ToUpper().Equals("YES")) { sIntSecO = "Integrated Security=true;"; } else { sIntSecO = "Integrated Security=false;"; }
                        sPreFill = string.Concat("Data Source=", sServer, ";Port=", sPort, ";User Id=", sUser, ";Password=", sPwd, ";", sIntSecO);
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        string sSSLP;
                        string sTrustServCert;
                        string sSchema = "";
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_server_sql, Languages.Languages.mc_input_assconn_server_sql, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_username_sql, Languages.Languages.mc_input_assconn_username_sql, false, true);
                        fP.ShowDialog();
                        sUser = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_sql, Languages.Languages.mc_input_assconn_pwd_sql, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_port_sql, Languages.Languages.mc_input_assconn_port_sql, false, true, "5432");
                        fP.ShowDialog();
                        sPort = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbname_sql, Languages.Languages.mc_input_assconn_dbname_sql, false, true);
                        fP.ShowDialog();
                        sDB = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_schema_postgres, Languages.Languages.mc_input_assconn_schema_postgres, true, false);
                        fP.ShowDialog();
                        sSchema = fP.PromptUserData.Trim();
                        MessageBoxResult msgTSC = MessageBox.Show(Languages.Languages.mc_input_assconn_trustedcertif_postgres, Languages.Languages.mc_input_assconn_trustedcertif_postgres, MessageBoxButton.YesNo);
                        MessageBoxResult msgSSLP = MessageBox.Show(Languages.Languages.mc_input_assconn_usessl_postgres, Languages.Languages.mc_input_assconn_usessl_postgres, MessageBoxButton.YesNo);
                        if (msgTSC.ToString().ToUpper().Equals("YES")) { sTrustServCert = "Trust Server Certificate=true;"; } else { sTrustServCert = "Trust Server Certificate=false;"; }
                        if (msgSSLP.ToString().ToUpper().Equals("YES")) { sSSLP = "Ssl Mode=Require"; } else { sSSLP = "Ssl Mode=Disable"; }
                        sPreFill = string.Concat("Server=", sServer, ";Port=", sPort, ";DATABASE=", sDB, ";Userid=", sUser, ";Password=", sPwd, ";", sSSLP, ";", sTrustServCert, ";", "Search_Path=", sSchema);
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        string sIntSec;
                        string sTrustedC;
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_server_sql, Languages.Languages.mc_input_assconn_server_sql, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_username_sql, Languages.Languages.mc_input_assconn_username_sql, false, true);
                        fP.ShowDialog();
                        sUser = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_sql, Languages.Languages.mc_input_assconn_pwd_sql, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbname_sql, Languages.Languages.mc_input_assconn_dbname_sql, false, true);
                        fP.ShowDialog();
                        sDB = fP.PromptUserData.Trim();
                        MessageBoxResult msgIntSec = MessageBox.Show(Languages.Languages.mc_input_assconn_intsecurity_sqlserver, Languages.Languages.mc_input_assconn_intsecurity_sqlserver, MessageBoxButton.YesNo);
                        MessageBoxResult msgTrustedC = MessageBox.Show(Languages.Languages.mc_input_assconn_trustedconn_sqlserver, Languages.Languages.mc_input_assconn_trustedconn_sqlserver, MessageBoxButton.YesNo);
                        if (msgIntSec.ToString().ToUpper().Equals("YES")) { sIntSec = "Integrated Security=true;"; } else { sIntSec = "Integrated Security=false;"; }
                        if (msgTrustedC.ToString().ToUpper().Equals("YES")) { sTrustedC = "Trusted_Connection=True;"; } else { sTrustedC = "Trusted_Connection=False;"; }
                        sPreFill = string.Concat("server=", sServer, ";Database=", sDB, ";User ID=", sUser, ";Password=", sPwd, ";Connection Timeout=120;", sIntSec, sTrustedC);
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbfile_sqlite, Languages.Languages.mc_input_assconn_dbfile_sqlite, false, true);
                        fP.ShowDialog();
                        sServer = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbversion_sqlite, Languages.Languages.mc_input_assconn_dbversion_sqlite, false, true);
                        fP.ShowDialog();
                        string sVersion = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dbpwd_sqlite, Languages.Languages.mc_input_assconn_dbpwd_sqlite, true, true);
                        fP.ShowDialog();
                        sPwd = fP.PromptUserData.Trim();
                        sPreFill = string.Concat("Data Source=", sServer, ";Version=", sVersion, sPwd.Length > 0 ? (";Password=" + sPwd) : "");
                        break;
                    case SQLTools_Enums.BDD.WS_REST:
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_urlprompt_ws01, Languages.Languages.mc_input_assconn_urlprompt_ws02, false, true);
                        fP.ShowDialog();
                        sURL = fP.PromptUserData.Trim();
                        sPreFill = string.Concat("URL=", sURL);
                        break;
                    case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                        //  --> LDAP://DC=securit,DC=fr
                        string sDomain, sLanguage;
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_domain_ad, Languages.Languages.mc_input_assconn_domain_ad, false, true);
                        fP.ShowDialog();
                        sDomain = fP.PromptUserData.Trim();
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_lang_ad01, Languages.Languages.mc_input_assconn_lang_ad02, false, true);
                        fP.ShowDialog();
                        sLanguage = fP.PromptUserData.Trim();
                        sPreFill = string.Concat("LDAP://", sDomain, ",", "DC=", sLanguage);
                        break;
                    default:
                        break;
                }

                if (bddCall.ToString().StartsWith("FI_"))
                {
                    bool bEnd = false;
                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_input_assconn_getftp_file, Languages.Languages.mc_input_assconn_getftp_file, MessageBoxButton.YesNo);
                    if (msgR.ToString().ToUpper().Equals("YES"))
                    {
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_ftpurl_file, Languages.Languages.mc_input_assconn_ftpurl_file, false, true);
                        fP.ShowDialog();
                        sURL = fP.PromptUserData.Trim();
                        sPreFill = sURL;
                        cbFileComingFrom.SelectedValue = "FTP";
                        bEnd = true;
                    }
                    if (!bEnd)
                    {
                        MessageBoxResult msgR2 = MessageBox.Show(Languages.Languages.mc_input_assconn_getsftp_file, Languages.Languages.mc_input_assconn_getsftp_file, MessageBoxButton.YesNo);
                        if (msgR2.ToString().ToUpper().Equals("YES"))
                        {
                            fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_sftpurl_file, Languages.Languages.mc_input_assconn_sftpurl_file, false, true);
                            fP.ShowDialog();
                            sURL = fP.PromptUserData.Trim();
                            sPreFill = sURL;
                            cbFileComingFrom.SelectedValue = "SFTP";
                            bEnd = true;
                        }
                    }
                    if (!bEnd)
                    {
                        MessageBoxResult msgR3 = MessageBox.Show(Languages.Languages.mc_input_assconn_network_file, Languages.Languages.mc_input_assconn_network_file, MessageBoxButton.YesNo);
                        if (msgR3.ToString().ToUpper().Equals("YES"))
                        {
                            fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_dnspath_file, Languages.Languages.mc_input_assconn_dnspath_file, false, true);
                            fP.ShowDialog();
                            sURL = fP.PromptUserData.Trim();
                            sPreFill = sURL;
                            cbFileComingFrom.SelectedValue = "NETWORK";
                            bEnd = true;
                        }
                    }
                    if (!bEnd)
                    {
                        fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_local_file, Languages.Languages.mc_input_assconn_local_file, false, true);
                        fP.ShowDialog();
                        sURL = fP.PromptUserData.Trim();
                        sPreFill = sURL;
                        cbFileComingFrom.SelectedValue = "LOCAL";
                    }
                }

                if (bddCall.ToString().StartsWith("MB_"))
                {
                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_smtphost_mb, Languages.Languages.mc_input_assconn_smtphost_mb, false, true);
                    fP.ShowDialog();
                    tbMailConnServerSmtp.Text = fP.PromptUserData.Trim();

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_popimaphost_mb, Languages.Languages.mc_input_assconn_popimaphost_mb, false, true);
                    fP.ShowDialog();
                    tbMailConnServerPopImap.Text = fP.PromptUserData.Trim();

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_protocol_mb, Languages.Languages.mc_input_assconn_protocol_mb, false, true);
                    fP.ShowDialog();
                    string sProtocol = fP.PromptUserData.Trim();
                    if (sProtocol.ToUpper().Equals("POP") || sProtocol.ToUpper().Equals("IMAP"))
                    {
                        sProtocol = sProtocol.ToUpper();
                    }
                    else { sProtocol = "POP"; }
                    try { cbMailConnProtocol.SelectedValue = sProtocol; } catch { }

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_authprotocol_mb01, Languages.Languages.mc_input_assconn_authprotocol_mb02, false, true);
                    fP.ShowDialog();
                    string sAuthProtocol = fP.PromptUserData.Trim();
                    sAuthProtocol = sAuthProtocol.ToUpper();
                    try { cbMailConnAuthProtocol.SelectedValue = sAuthProtocol; } catch { cbMailConnAuthProtocol.SelectedValue = "NONE"; }

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_sender_mb, Languages.Languages.mc_input_assconn_sender_mb, false, true);
                    fP.ShowDialog();
                    tbMailConnUserSend.Text = fP.PromptUserData.Trim();

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_pwd_mb, Languages.Languages.mc_input_assconn_pwd_mb, true, true);
                    fP.ShowDialog();
                    tbMailConnPasswordSend.Password = fP.PromptUserData.Trim();

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_smtpport_mb, Languages.Languages.mc_input_assconn_smtpport_mb, false, true);
                    fP.ShowDialog();
                    tbMailConnPortSend.Text = fP.PromptUserData.Trim();

                    fP = new FuzibleUITools.Prompt(Languages.Languages.mc_input_assconn_popimapport_mb, Languages.Languages.mc_input_assconn_popimapport_mb, false, true);
                    fP.ShowDialog();
                    tbMailConnPortReceive.Text = fP.PromptUserData.Trim();
                }

                RichTB.SetTextRTB(tbConnectionString, sPreFill);

                ColorizeRichTextBox();
            }
            else { MessageBox.Show(Languages.Languages.mc_input_assconn_mustsetdriver); }

        }

        private void BtnBrowseSSHFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog lFD = new()
            {
                Filter = "(*.ppk) | *.ppk",
                DefaultExt = "ppk",
                Title = "OpenSSH"
            };
            lFD.ShowDialog();

            if (lFD.FileName.Length > 0)
            {
                tbSFTPSSHKeyPath.Text = lFD.FileName;
            }
        }

        private void BtnLoadSQLCompatibilityParams_Click(object sender, RoutedEventArgs e)
        {
            if (cbDriverSource.SelectedIndex == -1)
            {
                MessageBox.Show(Languages.Languages.mc_msg_scanconnmustselectsqldriver);
            }
            else
            {
                var sBDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString());
                string sParams = LoadSQLCompatibilityParams(false, sBDD);
                if (sParams.Trim().Length == 0)
                {
                    MessageBox.Show(Languages.Languages.mc_msg_sqlloaddefaultnoparams);
                }
                else { MessageBox.Show(Languages.Languages.mc_msg_sqlloaddefaultok); tbConnectionParams.Text = sParams; }
            }
        }

        private void BtnConfigureTaskManager_Click(object sender, RoutedEventArgs e)
        {
            if (RichTB.GetTextRTB(tbConnectionStringServiceApp).Length > 0)
            {
                try
                {
                    bool bActivateNow = false;
                    bool bSystemUser = false;

                    MessageBoxResult msgR = MessageBox.Show(Languages.Languages.mc_msg_createtaskenable01, Languages.Languages.mc_msg_createtaskenable02, MessageBoxButton.YesNo);

                    if (!msgR.ToString().ToUpper().Equals("YES"))
                    {
                        bActivateNow = true;
                    }

                    MessageBoxResult msgR2 = MessageBox.Show(Languages.Languages.mc_msg_createtaskenable03, Languages.Languages.mc_msg_createtaskenable04, MessageBoxButton.YesNo);

                    if (msgR2.ToString().ToUpper().Equals("YES"))
                    {
                        bSystemUser = true;
                    }

                    ConfigureWindowsTaskManager(bActivateNow, bSystemUser);
                    MessageBox.Show(Languages.Languages.mc_msg_createtaskchangeaccount);
                }
                catch (Exception ex) { MessageBox.Show(Languages.Languages.mc_msg_createtaskko + Environment.NewLine + ex.Message); }
            }
            else { MessageBox.Show(Languages.Languages.mc_msg_mustsetconnection); }
        }

        private void BtnSaveConnection_Click(object sender, RoutedEventArgs e)
        {
            if (cbListConnections.SelectedIndex > -1)
            {
                ModifyExistingConnection(FuzibleController.GetConnection(cbListConnections.SelectedValue.ToString()));
            }
            else
            {
                SaveNewConnection();
            }
        }

        private void BtnDeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (cbListConnections.SelectedIndex >= 0)
            {
                CONNString CS = FuzibleController.GetConnection(cbListConnections.SelectedValue.ToString());

                if (CS.SConnRawID <= 5) //impossible de supprimer les connexions par défaut
                {
                    MessageBox.Show(Languages.Languages.mc_msg_defaultconncantbedeleted);
                }
                else
                {
                    List<string> sListUsage = FuzibleController.CheckConnStringUsage(cbListConnections.SelectedValue.ToString());
                    bool bDel = true;

                    if (sListUsage.Count > 0)
                    {
                        MessageBoxResult msgR = MessageBox.Show(string.Concat(Languages.Languages.mc_msg_connstringalreadyused01, sListUsage.Count.ToString(), ") : ", Environment.NewLine, string.Join(Environment.NewLine, sListUsage), Environment.NewLine, Languages.Languages.mc_msg_connstringalreadyused02), Languages.Languages.mc_msg_connstringalreadyused03, MessageBoxButton.OKCancel);
                        if (!msgR.ToString().ToUpper().Equals("OK"))
                        {
                            bDel = false;
                        }
                    }

                    if (bDel)
                    {
                        string sResult = FuzibleController.DeleteConnection(cbListConnections.SelectedValue.ToString());
                        tbConnectionName.Text = "";
                        RichTB.SetTextRTB(tbConnectionString, "");
                        tbConnectionParams.Text = "";
                        LoadListOfConnections();
                        MessageBox.Show(Languages.Languages.mc_msg_deleteconnstringok + Environment.NewLine + sResult);
                    }
                }
            }
            else { MessageBox.Show(Languages.Languages.mc_msg_deleteconnstringko); }
        }

        private void BtnLoadWSTemplate_Click(object sender, RoutedEventArgs e)
        {
            var cbI = (ComboBoxItem)cbWSTemplate.Items[cbWSTemplate.SelectedIndex];
            var wsTemplate = (SQLTools_Enums.WEBSERVICE_TEMPLATE)Enum.Parse(typeof(SQLTools_Enums.WEBSERVICE_TEMPLATE), cbI.Tag.ToString());

            if (cbWSTemplate.SelectedValue.ToString().Equals("NUXEO"))
            {
                CanvasWsNuxeoParams.Visibility = Visibility.Visible;
                CanvasWsRestParams.Visibility = Visibility.Hidden;
            }
            else
            {
                CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                CanvasWsRestParams.Visibility = Visibility.Visible;
            }

            //*********************************************************************************
            //penser à CHANGER SmartQueries (Classe Fuzible/UI/IntellisenseData) en cas d'ajout
            //*********************************************************************************
            switch (wsTemplate)
            {
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.NUXEO:
                    RichTB.SetTextRTB(tbConnectionString, "https://myinstance.url/nuxeo/");
                    RichTB.SetTextRTB(tbWSHeaders, "");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    cbWSAuthorizationMethod.SelectedValue = "";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSAuthorizationURL.Text = "";
                    tbWSNuxeoUser.Text = "Username";
                    tbWSNuxeoPassword.Password = "Password";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.DEFAULT:
                    RichTB.SetTextRTB(tbConnectionString, "http://myinstance.url/myresttobject/");
                    RichTB.SetTextRTB(tbWSHeaders, "");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    cbWSAuthorizationMethod.SelectedValue = "NO_AUTH";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSAuthorizationURL.Text = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.GLPI:
                    cbWSAuthorizationMethod.SelectedValue = "API_AUTH_PARAM";
                    RichTB.SetTextRTB(tbConnectionString, "http://myglpi_instance/glpi/apirest.php/");
                    RichTB.SetTextRTB(tbWSHeaders, "Authorization:user_token myUserToken;app-token:myAppToken");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    tbWSAuthorizationKey.Text = "session_token";
                    tbWSAuthorizationURL.Text = "http://myglpi_instance/glpi/apirest.php/initSession/";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.SALESFORCE:
                    cbWSAuthorizationMethod.SelectedValue = "SALESFORCE_OAUTH";
                    RichTB.SetTextRTB(tbConnectionString, "https://my_instance.salesforce.com/services/data/v51.0/");
                    RichTB.SetTextRTB(tbWSHeaders, "Content-Type=application/json");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    tbWSAuthorizationURL.Text = "https://my_instance.salesforce.com/services/oauth2/token";
                    tbWSSalesforceConsumerKey.Text = "ConsumerKeyConnectedApp";
                    tbWSSalesforceConsumerSecret.Password = "ConsumerSercretConnnectedApp";
                    tbWSSalesforceUserToken.Text = "SalesforceUserToken";
                    tbWSAuthorizationKey.Text = "SalesforceUserLogin";
                    tbWSAuthorizationValue.Password = "SalesforceUserPassword";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.MICROSOFT_GRAPH:
                    cbWSAuthorizationMethod.SelectedValue = "OAUTH2";
                    RichTB.SetTextRTB(tbConnectionString, "https://graph.microsoft.com/v1.0/");
                    RichTB.SetTextRTB(tbWSHeaders, "Content-Type=application/json");
                    RichTB.SetTextRTB(tbWSQueryParams, "grant_type=client_credentials");
                    tbWSAuthorizationURL.Text = "https://login.microsoftonline.com/myDirectoryID/oauth2/v2.0/token";
                    tbWSSalesforceConsumerKey.Text = "MyClientAppID";
                    tbWSSalesforceConsumerSecret.Password = "MyClientAppSecret";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "https://graph.microsoft.com/.default";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.MICROSOFT_ONEDRIVE:
                    cbWSAuthorizationMethod.SelectedValue = "OAUTH2";
                    RichTB.SetTextRTB(tbConnectionString, "https://graph.microsoft.com/v1.0/drives/{drive_id}/items/{item_id}");
                    RichTB.SetTextRTB(tbWSHeaders, "");
                    RichTB.SetTextRTB(tbWSQueryParams, "grant_type=client_credentials");
                    tbWSAuthorizationURL.Text = "https://login.microsoftonline.com/myDirectoryID/oauth2/v2.0/token";
                    tbWSSalesforceConsumerKey.Text = "MyClientAppID";
                    tbWSSalesforceConsumerSecret.Password = "MyClientAppSecret";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "https://graph.microsoft.com/.default";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.FACEBOOK_GRAPH:
                    cbWSAuthorizationMethod.SelectedValue = "OAUTH2";
                    RichTB.SetTextRTB(tbConnectionString, "https://graph.facebook.com/");
                    RichTB.SetTextRTB(tbWSHeaders, "Content-Type=application/json");
                    RichTB.SetTextRTB(tbWSQueryParams, "grant_type=client_credentials");
                    tbWSAuthorizationURL.Text = "https://graph.facebook.com/oauth/access_token";
                    tbWSSalesforceConsumerKey.Text = "MyClientAppID";
                    tbWSSalesforceConsumerSecret.Password = "MyClientAppSecret";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.ITOP:
                    cbWSAuthorizationMethod.SelectedValue = "HTTP";
                    RichTB.SetTextRTB(tbConnectionString, "<itop-root>/webservices/rest.php?version=1.3");
                    RichTB.SetTextRTB(tbWSHeaders, "Content-Type=application/json");
                    RichTB.SetTextRTB(tbWSQueryParams, "auth_user=<user>&auth_pwd=<password>");
                    tbWSAuthorizationURL.Text = "";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.SALESFORCE_SOQL:
                    cbWSAuthorizationMethod.SelectedValue = "SALESFORCE_OAUTH";
                    RichTB.SetTextRTB(tbConnectionString, "https://my_instance.salesforce.com/services/data/v51.0/query/");
                    RichTB.SetTextRTB(tbWSHeaders, "Content-Type=application/json");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    tbWSAuthorizationURL.Text = "https://my_instance.salesforce.com/services/oauth2/token";
                    tbWSSalesforceConsumerKey.Text = "ConsumerKeyConnectedApp";
                    tbWSSalesforceConsumerSecret.Password = "ConsumerSercretConnnectedApp";
                    tbWSSalesforceUserToken.Text = "SalesforceUserToken";
                    tbWSAuthorizationKey.Text = "SalesforceUserLogin";
                    tbWSAuthorizationValue.Password = "SalesforceUserPassword";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.CEGID:
                    cbWSAuthorizationMethod.SelectedValue = "HTTP";
                    RichTB.SetTextRTB(tbConnectionString, "https://myinstance.cegid.com/webplace/rhpi_sri.jsp?webServiceApplication=STANDARD");
                    RichTB.SetTextRTB(tbWSHeaders, "");
                    RichTB.SetTextRTB(tbWSQueryParams, "tokenId=MyUser&tokenValue=MyPassword");
                    tbWSAuthorizationURL.Text = "";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "";
                    tbWSAuthorizationValue.Password = "";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
                case SQLTools_Enums.WEBSERVICE_TEMPLATE.YOUTUBE_V3:
                    cbWSAuthorizationMethod.SelectedValue = "API_AUTH_PARAM";
                    RichTB.SetTextRTB(tbConnectionString, "https://www.googleapis.com/youtube/v3/");
                    RichTB.SetTextRTB(tbWSHeaders, "");
                    RichTB.SetTextRTB(tbWSQueryParams, "");
                    tbWSAuthorizationURL.Text = "";
                    tbWSSalesforceConsumerKey.Text = "";
                    tbWSSalesforceConsumerSecret.Password = "";
                    tbWSSalesforceUserToken.Text = "";
                    tbWSAuthorizationKey.Text = "key";
                    tbWSAuthorizationValue.Password = "MyGoogleAuthorizationKey";
                    tbWSNuxeoUser.Text = "";
                    tbWSNuxeoPassword.Password = "";
                    break;
            }
            SetColorsParameters(";", ":", tbWSHeaders);
            SetColorsParameters("&", "=", tbWSQueryParams);
            ColorizeRichTextBox();
        }

        private void TcMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Equals(sender, e.OriginalSource))
            {
                Canvas contentPresenter = tcMenu.SelectedContent as Canvas;
                if (contentPresenter != null) { AttachMouseDownEventToAllControls(contentPresenter); }

            }
        }

        private void cbDriverServiceApp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDriverServiceApp.SelectedIndex > -1 && tbConnectionStringServiceAppSchema != null)
            {
                var cbI = (ComboBoxItem)cbDriverServiceApp.Items[cbDriverServiceApp.SelectedIndex];
                string sDriver = cbI.Tag.ToString();
                var bDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
                switch (bDD)
                {
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        tbConnectionStringServiceAppSchema.IsEnabled = true;
                        tbConnectionStringServiceAppSchema.Text = "dbo";
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        tbConnectionStringServiceAppSchema.IsEnabled = true;
                        tbConnectionStringServiceAppSchema.Text = "public";
                        break;
                    default:
                        tbConnectionStringServiceAppSchema.Text = "";
                        tbConnectionStringServiceAppSchema.IsEnabled = false;
                        break;
                }
            }
        }

        private void cbDriverLog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDriverLog.SelectedIndex > -1 && tbConnectionStringLogSchema != null)
            {
                var cbI = (ComboBoxItem)cbDriverLog.Items[cbDriverLog.SelectedIndex];
                string sDriver = cbI.Tag.ToString();
                var bDD = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), sDriver);
                switch (bDD)
                {
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        tbConnectionStringLogSchema.IsEnabled = true;
                        tbConnectionStringLogSchema.Text = "dbo";
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        tbConnectionStringLogSchema.IsEnabled = true;
                        tbConnectionStringLogSchema.Text = "public";
                        break;
                    default:
                        tbConnectionStringLogSchema.Text = "";
                        tbConnectionStringLogSchema.IsEnabled = false;
                        break;
                }
            }
        }

        #endregion

        #region "PRIVATE VOID"


        private string BuildMailConnString(bool bIsLog)
        {
            StringBuilder sbConnectionStringLogMail = new();

            if (bIsLog)
            {
                sbConnectionStringLogMail.Append("SERVER_SEND=" + tbMailLogServer.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("PORT_SEND=" + tbMailLogPort.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("AUTH_PROTOCOL=" + cbMailAuthProtocolLog.SelectedValue.ToString() + ";");
                sbConnectionStringLogMail.Append("SSL=" + (ckMAILUseSSLLog.IsChecked.Value ? "1" : "0") + ";");
                sbConnectionStringLogMail.Append("USERNAME=" + tbMailLogUsername.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("PASSWORD=" + tbMailLogPassword.Password);
            }
            else
            {
                sbConnectionStringLogMail.Append("SERVER_SEND=" + tbMailConnServerSmtp.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("PORT_SEND=" + tbMailConnPortSend.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("SERVER_RECEIVE=" + tbMailConnServerPopImap.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("PORT_RECEIVE=" + tbMailConnPortReceive.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("GET_PROTOCOL=" + cbMailConnProtocol.SelectedValue.ToString() + ";");
                sbConnectionStringLogMail.Append("AUTH_PROTOCOL=" + cbMailConnAuthProtocol.SelectedValue.ToString() + ";");
                sbConnectionStringLogMail.Append("SSL=" + (ckMAILConnUseSSL.IsChecked.Value ? "1" : "0") + ";");
                sbConnectionStringLogMail.Append("USERNAME=" + tbMailConnUserSend.Text.Trim() + ";");
                sbConnectionStringLogMail.Append("PASSWORD=" + tbMailConnPasswordSend.Password);
            }

            return sbConnectionStringLogMail.ToString();
        }

        private void DispatchMailConnStringToControls(string sConnString, bool bIsLog)
        {
            MailTools.MAILConnectionVariables mVars = new(new CONNString(SQLTools_Enums.BDD.MB_MAIL, "0", "MailConn", sConnString), 0, 0, null);

            if (bIsLog)
            {
                tbMailLogServer.Text = mVars.Host_Send;
                tbMailLogPort.Text = mVars.SENDPort.ToString();
                cbMailAuthProtocolLog.SelectedValue = mVars.AuthentificationProtocol.ToString();
                ckMAILUseSSLLog.IsChecked = mVars.IsSSL;
                tbMailLogUsername.Text = mVars.SenderAddress;
                tbMailLogPassword.Password = mVars.SenderPassword;
            }
            else
            {
                tbMailConnServerSmtp.Text = mVars.Host_Send;
                tbMailConnPortSend.Text = mVars.SENDPort.ToString();
                tbMailConnServerPopImap.Text = mVars.Host_Receive;
                tbMailConnPortReceive.Text = mVars.RECEIVEPort.ToString();
                cbMailConnProtocol.SelectedValue = mVars.MailGetProtocol.ToString();
                cbMailConnAuthProtocol.SelectedValue = mVars.AuthentificationProtocol.ToString();
                ckMAILConnUseSSL.IsChecked = mVars.IsSSL;
                tbMailConnUserSend.Text = mVars.SenderAddress;
                tbMailConnPasswordSend.Password = mVars.SenderPassword;
            }
        }

        private string SetConnectionParams(bool bAddDriverLocales, SQLTools_Enums.BDD sDriver)
        {
            StringBuilder sbConnParams = new();

            if (bAddDriverLocales)
            {
                tbConnectionParams.Text = Regex.Replace(tbConnectionParams.Text, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                tbConnectionParams.Text = Regex.Replace(tbConnectionParams.Text, "P_DRIVER_DATE_LOCALE=.{10}", "");
                sbConnParams.AppendLine("P_DRIVER_DATE_LOCALE=" + cbDriverDateFormat.SelectedValue.ToString());
                sbConnParams.AppendLine("P_DRIVER_DECIMALS_LOCALE=" + cbDriverNumberFormat.SelectedValue.ToString());
            }

            string sParams = "";

            switch (sDriver)
            {
                case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                    sParams = "";

                    var listParams = tbConnectionParams.Text.Trim().Split(Environment.NewLine).ToList();
                    if (listParams.Count == 0)
                    { listParams = SQLQueries.ParamsForADDriver(); }

                    for (int iP = 0; iP < listParams.Count; iP++)
                    {
                        if (listParams[iP].StartsWith("P_AD_USERNAME=") || listParams[iP].StartsWith("P_AD_PASSWORD=") || listParams[iP].StartsWith("P_AD_AUTH=") || listParams[iP].StartsWith("P_AD_OU="))
                        { }
                        else
                        {
                            sParams = string.Concat(sParams, listParams[iP], Environment.NewLine);
                        }
                    }
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    sbConnParams.AppendLine(string.Concat("P_AD_USERNAME=", tbADUsername.Text));
                    sbConnParams.AppendLine(string.Concat("P_AD_PASSWORD=", tbADPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_AD_AUTH=", cbADAuthentification.SelectedValue.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_AD_OU=", tbADOU.Text.Trim()));
                    break;
                case SQLTools_Enums.BDD.DB_ACCESS:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.DB_MYSQL:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(tbConnectionParams.Text.Trim());
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    sParams = tbConnectionParams.Text.Trim();
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DECIMALS_LOCALE=[.,]{1}", "");
                    //sParams = Regex.Replace(sParams, "P_DRIVER_DATE_LOCALE=.{10}", "");
                    sbConnParams.AppendLine(sParams);
                    break;
                case SQLTools_Enums.BDD.NS_MONGODB:
                    sbConnParams.AppendLine(string.Concat("P_MONGODB_DOCUMENT=", cbNOSQLContentType.SelectedIndex > -1 ? cbNOSQLContentType.SelectedValue : ""));
                    //TODO
                    break;
                case SQLTools_Enums.BDD.FI_CSV:
                    sbConnParams.AppendLine(string.Concat("P_FILE_COMEFROM=", cbFileComingFrom.SelectedIndex > -1 ? cbFileComingFrom.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_USERNAME=", tbNetworkUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_PASSWORD=", tbNetworkPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_URL=", tbFTPProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_PORT=", tbFTPProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_USERNAME=", tbFTPUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PASSWORD=", tbFTPPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PORT=", tbFTPPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PATH=", tbFTPPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_SSL=", ckFTPUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_SFTP_SSH_KEY_PATH=", tbSFTPSSHKeyPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_IGNORE_TLS_ERRORS=", ckFTPIgnoreTLSErrors.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PROXY_TYPE=", cbFileProxyType.SelectedValue.ToString()));
                    if (tbFTPProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbFTPProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.FI_FILE:
                    sbConnParams.AppendLine(string.Concat("P_FILE_COMEFROM=", cbFileComingFrom.SelectedIndex > -1 ? cbFileComingFrom.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_USERNAME=", tbNetworkUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_PASSWORD=", tbNetworkPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_URL=", tbFTPProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_PORT=", tbFTPProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_USERNAME=", tbFTPUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PASSWORD=", tbFTPPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PORT=", tbFTPPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PATH=", tbFTPPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_SSL=", ckFTPUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_SFTP_SSH_KEY_PATH=", tbSFTPSSHKeyPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_IGNORE_TLS_ERRORS=", ckFTPIgnoreTLSErrors.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PROXY_TYPE=", cbFileProxyType.SelectedValue.ToString()));
                    if (tbFTPProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbFTPProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.FI_JSON:
                    sbConnParams.AppendLine(string.Concat("P_FILE_COMEFROM=", cbFileComingFrom.SelectedIndex > -1 ? cbFileComingFrom.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_USERNAME=", tbNetworkUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_PASSWORD=", tbNetworkPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_URL=", tbFTPProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_PORT=", tbFTPProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_USERNAME=", tbFTPUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PASSWORD=", tbFTPPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PORT=", tbFTPPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PATH=", tbFTPPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_SSL=", ckFTPUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_SFTP_SSH_KEY_PATH=", tbSFTPSSHKeyPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_IGNORE_TLS_ERRORS=", ckFTPIgnoreTLSErrors.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PROXY_TYPE=", cbFileProxyType.SelectedValue.ToString()));
                    if (tbFTPProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbFTPProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.FI_XLS:
                    sbConnParams.AppendLine(string.Concat("P_FILE_COMEFROM=", cbFileComingFrom.SelectedIndex > -1 ? cbFileComingFrom.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_USERNAME=", tbNetworkUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_PASSWORD=", tbNetworkPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_URL=", tbFTPProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_PORT=", tbFTPProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_USERNAME=", tbFTPUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PASSWORD=", tbFTPPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PORT=", tbFTPPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PATH=", tbFTPPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_SSL=", ckFTPUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_SFTP_SSH_KEY_PATH=", tbSFTPSSHKeyPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_IGNORE_TLS_ERRORS=", ckFTPIgnoreTLSErrors.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PROXY_TYPE=", cbFileProxyType.SelectedValue.ToString()));
                    if (tbFTPProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbFTPProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.FI_XML:
                    sbConnParams.AppendLine(string.Concat("P_FILE_COMEFROM=", cbFileComingFrom.SelectedIndex > -1 ? cbFileComingFrom.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_USERNAME=", tbNetworkUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_NETWORK_PASSWORD=", tbNetworkPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_URL=", tbFTPProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_PROXY_PORT=", tbFTPProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_USERNAME=", tbFTPUsername.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PASSWORD=", tbFTPPassword.Password));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PORT=", tbFTPPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PATH=", tbFTPPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_SSL=", ckFTPUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_SFTP_SSH_KEY_PATH=", tbSFTPSSHKeyPath.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_IGNORE_TLS_ERRORS=", ckFTPIgnoreTLSErrors.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_FILE_FTP_PROXY_TYPE=", cbFileProxyType.SelectedValue.ToString()));
                    if (tbFTPProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbFTPProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_USERNAME=", tbFTPProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_FTP_PROXY_PASSWORD=", tbFTPProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.MB_MAIL:
                    sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_URL=", tbMAILProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_PORT=", tbMAILProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_MAIL_USE_SSL=", ckMAILConnUseSSL.IsChecked.Value.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_MAIL_PROTOCOL=", cbMailConnProtocol.SelectedValue.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_MAIL_AUTH_PROTOCOL=", cbMailConnAuthProtocol.SelectedValue == null ? "NONE" : cbMailConnAuthProtocol.SelectedValue.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_TYPE=", cbMailProxyType.SelectedValue.ToString()));
                    if (tbMailProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_USERNAME=", tbMailProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_PASSWORD=", tbMailProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbMailProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_USERNAME=", tbMailProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_MAIL_PROXY_PASSWORD=", tbMailProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    break;
                case SQLTools_Enums.BDD.WS_REST:
                    sbConnParams.AppendLine(string.Concat("P_WS_TEMPLATE=", cbWSTemplate.SelectedValue.ToString()));
                    sbConnParams.AppendLine(string.Concat("P_WS_PROXY_URL=", tbWSProxyURL.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_PROXY_PORT=", tbWSProxyPort.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_HEADERS=", RichTB.GetTextRTB(tbWSHeaders)));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_METHOD=", cbWSAuthorizationMethod.SelectedIndex > -1 ? cbWSAuthorizationMethod.SelectedValue : ""));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_KEY=", tbWSAuthorizationKey.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_VALUE=", tbWSAuthorizationValue.Password));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_URL=", tbWSAuthorizationURL.Text.Trim()));

                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_SF_USERTOKEN=", tbWSSalesforceUserToken.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_SF_CONSUMERKEY=", tbWSSalesforceConsumerKey.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_AUTH_SF_CONSUMERSECRET=", tbWSSalesforceConsumerSecret.Password));

                    sbConnParams.AppendLine(string.Concat("P_WS_QUERY_PARAMS=", RichTB.GetTextRTB(tbWSQueryParams)));
                    sbConnParams.AppendLine(string.Concat("P_WS_PROXY_TYPE=", cbWSProxyType.SelectedValue.ToString()));
                    if (tbWSProxyUserPwd.Text.Split(",").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_WS_PROXY_USERNAME=", tbWSProxyUserPwd.Text.Split(",")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_WS_PROXY_PASSWORD=", tbWSProxyUserPwd.Text.Split(",")[1].Trim()));
                    }
                    else if (tbWSProxyUserPwd.Text.Split(";").Length > 1)
                    {
                        sbConnParams.AppendLine(string.Concat("P_WS_PROXY_USERNAME=", tbWSProxyUserPwd.Text.Split(";")[0].Trim()));
                        sbConnParams.AppendLine(string.Concat("P_WS_PROXY_PASSWORD=", tbWSProxyUserPwd.Text.Split(";")[1].Trim()));
                    }
                    sbConnParams.AppendLine(string.Concat("P_WS_NUXEO_USER=", tbWSNuxeoUser.Text.Trim()));
                    sbConnParams.AppendLine(string.Concat("P_WS_NUXEO_PASSWORD=", tbWSNuxeoPassword.Password));
                    break;
            }
            return sbConnParams.ToString().Trim();
        }

        private void GetSQLInstances(SQLTools_Enums.BDD sDriver)
        {
            //List<string[]> sListConn = new List<string[]>();

            MessageBoxResult msgR1 = MessageBox.Show(Languages.Languages.mc_msg_getsqlinstances_01, Languages.Languages.mc_msg_getsqlinstances_02, MessageBoxButton.YesNo);

            if (msgR1.ToString().ToUpper().Equals("YES"))
            {
                Prompt fP;
                try
                {
                    int iPort = FuzibleController.GetDefaultSQLPort(sDriver);
                    fP = new Prompt(Languages.Languages.mc_msg_getsqlinstances_port01, Languages.Languages.mc_msg_getsqlinstances_port02, false, true, iPort.ToString());
                    fP.ShowDialog();
                    try { iPort = Convert.ToInt32(fP.PromptUserData); } catch { }

                    List<string[]> sListConn = SQLTools.ScanForDBInstancesInNetwork(sDriver, iPort);

                    if (sListConn.Count == 0)
                    {
                        MessageBox.Show(Languages.Languages.mc_msg_getsqlinstances_noinstance01 + iPort.ToString() + Languages.Languages.mc_msg_getsqlinstances_noinstance02);
                    }
                    else
                    {
                        foreach (string[] sConn in sListConn)
                        {
                            MessageBoxResult msgR2 = MessageBox.Show(string.Concat(Languages.Languages.mc_msg_getsqlinstances_add01, sConn[0], " (", sConn[3], ")", Languages.Languages.mc_msg_getsqlinstances_add02), Languages.Languages.mc_msg_getsqlinstances_add03, MessageBoxButton.YesNo);

                            if (msgR2.ToString().ToUpper().Equals("YES"))
                            {
                                List<string> sListParams = SQLQueries.ParamsForSQLDriver(sDriver, "");
                                tbConnectionParams.Text = string.Join(Environment.NewLine, sListParams);
                                tbConnectionName.Text = sConn[1];
                                RichTB.SetTextRTB(tbConnectionString, sConn[4]);
                                SaveNewConnection();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Languages.Languages.mc_msg_getsqlinstancesko + ex.Message);
                }
            }
        }

        private bool CheckFieldsForErrors()
        {
            bool bOK = true;

            _ = int.TryParse(tbAbortJobErrors.Text, out int iValue);
            if (iValue < 1 || iValue > 99) { bOK = false; tbAbortJobErrors.Background = Brushes.IndianRed; } else { tbAbortJobErrors.Background = Brushes.White; }

            _ = int.TryParse(tbQueryAssistantMaxFileSize.Text, out iValue);
            if (iValue < 1 || iValue > 100000000) { bOK = false; tbQueryAssistantMaxFileSize.Background = Brushes.IndianRed; } else { tbQueryAssistantMaxFileSize.Background = Brushes.White; }

            _ = int.TryParse(tbDaysKeepLog.Text, out iValue);
            if (iValue < 1 || iValue > 999) { bOK = false; tbDaysKeepLog.Background = Brushes.IndianRed; } else { tbDaysKeepLog.Background = Brushes.White; }

            _ = int.TryParse(tbDaysKeepProcessedFiles.Text, out iValue);
            if (iValue < 1 || iValue > 999) { bOK = false; tbDaysKeepProcessedFiles.Background = Brushes.IndianRed; } else { tbDaysKeepProcessedFiles.Background = Brushes.White; }

            _ = int.TryParse(tbProcessorCount.Text, out iValue);
            if (iValue < 1 || iValue > Environment.ProcessorCount) { bOK = false; tbProcessorCount.Background = Brushes.IndianRed; } else { tbProcessorCount.Background = Brushes.White; }

            _ = int.TryParse(tbProcessorCount_BigData.Text, out iValue);
            if (iValue < 1 || iValue > Environment.ProcessorCount) { bOK = false; tbProcessorCount_BigData.Background = Brushes.IndianRed; } else { tbProcessorCount_BigData.Background = Brushes.White; }

            _ = int.TryParse(tbSQLCommandTimeout.Text, out iValue);
            if (iValue < 15 || iValue > 9999) { bOK = false; tbSQLCommandTimeout.Background = Brushes.IndianRed; } else { tbSQLCommandTimeout.Background = Brushes.White; }

            _ = int.TryParse(tbSQLMaxDecimals.Text, out iValue);
            if (iValue < 1 || iValue > 30) { bOK = false; tbSQLMaxDecimals.Background = Brushes.IndianRed; } else { tbSQLMaxDecimals.Background = Brushes.White; }

            _ = int.TryParse(tbSQLErrorsOverflow.Text, out iValue);
            if (iValue < 1 || iValue > 9999) { bOK = false; tbSQLErrorsOverflow.Background = Brushes.IndianRed; } else { tbSQLErrorsOverflow.Background = Brushes.White; }

            _ = int.TryParse(tbSQLCommit.Text, out iValue);
            if (iValue < 1 || iValue > 99999) { bOK = false; tbSQLCommit.Background = Brushes.IndianRed; } else { tbSQLCommit.Background = Brushes.White; }

            _ = int.TryParse(tbSQLBulkRate.Text, out iValue);
            if (iValue < 100 || iValue > 99999) { bOK = false; tbSQLBulkRate.Background = Brushes.IndianRed; } else { tbSQLBulkRate.Background = Brushes.White; }

            _ = int.TryParse(tbSQLDirectStreamCommit.Text, out iValue);
            if (iValue < 1 || iValue > 99999) { bOK = false; tbSQLDirectStreamCommit.Background = Brushes.IndianRed; } else { tbSQLDirectStreamCommit.Background = Brushes.White; }

            _ = int.TryParse(tbSQLKeepCharsDebug.Text, out iValue);
            if (iValue < 100 || iValue > 100000) { bOK = false; tbSQLKeepCharsDebug.Background = Brushes.IndianRed; } else { tbSQLKeepCharsDebug.Background = Brushes.White; }

            if (tbSQLSynchroModeTable.Text.Length < 2 || tbSQLSynchroModeTable.Text.Length > 40) { bOK = false; tbSQLSynchroModeTable.Background = Brushes.IndianRed; } else { tbSQLSynchroModeTable.Background = Brushes.White; }

            if (tbFILEProcessed.Text.Length < 1 || tbFILEProcessed.Text.Length > 50) { bOK = false; tbFILEProcessed.Background = Brushes.IndianRed; } else { tbFILEProcessed.Background = Brushes.White; }

            if (tbFILEExported.Text.Length < 1 || tbFILEExported.Text.Length > 50) { bOK = false; tbFILEExported.Background = Brushes.IndianRed; } else { tbFILEExported.Background = Brushes.White; }

            if (tbCSVSeparators.Text.Length < 1 || tbCSVSeparators.Text.Length > 20) { bOK = false; tbCSVSeparators.Background = Brushes.IndianRed; } else { tbCSVSeparators.Background = Brushes.White; }

            if (tbWSNuxeoDefaultEncoding.Text.Length < 1) { bOK = false; tbWSNuxeoDefaultEncoding.Background = Brushes.IndianRed; } else { tbWSNuxeoDefaultEncoding.Background = Brushes.White; }

            if (tbWSDefaultEncoding.Text.Length < 1) { bOK = false; tbWSDefaultEncoding.Background = Brushes.IndianRed; } else { tbWSDefaultEncoding.Background = Brushes.White; }

            _ = int.TryParse(tbFileMaxRowsHeaderSeparatorAnalyzer.Text, out iValue);
            if (iValue < 100 || iValue > 100000) { bOK = false; tbFileMaxRowsHeaderSeparatorAnalyzer.Background = Brushes.IndianRed; } else { tbFileMaxRowsHeaderSeparatorAnalyzer.Background = Brushes.White; }

            _ = decimal.TryParse(tbCSVResemblanceOffset.Text, out decimal dValue);
            if (dValue <= 0 || dValue > 1) { bOK = false; tbCSVResemblanceOffset.Background = Brushes.IndianRed; } else { tbCSVResemblanceOffset.Background = Brushes.White; }

            _ = decimal.TryParse(tbCSVAverageUniqueOffset.Text, out dValue);
            if (dValue <= 0 || dValue > 1) { bOK = false; tbCSVAverageUniqueOffset.Background = Brushes.IndianRed; } else { tbCSVAverageUniqueOffset.Background = Brushes.White; }

            _ = int.TryParse(tbCSVRowsOffsetBeforeDepthAnalysis.Text, out iValue);
            if (iValue < 10 || iValue > 999999) { bOK = false; tbCSVRowsOffsetBeforeDepthAnalysis.Background = Brushes.IndianRed; } else { tbCSVRowsOffsetBeforeDepthAnalysis.Background = Brushes.White; }

            _ = int.TryParse(tbFileMaxRowsBeforeSplit.Text, out iValue);
            if (iValue < 100000 || iValue > 100000000) { bOK = false; tbFileMaxRowsBeforeSplit.Background = Brushes.IndianRed; } else { tbFileMaxRowsBeforeSplit.Background = Brushes.White; }

            _ = int.TryParse(tbMAILMaxLengthBeforePJ.Text, out iValue);
            if (iValue < 1 || iValue > 1000000) { bOK = false; tbMAILMaxLengthBeforePJ.Background = Brushes.IndianRed; } else { tbMAILMaxLengthBeforePJ.Background = Brushes.White; }

            _ = int.TryParse(tbMAILMaxPJSize.Text, out iValue);
            if (iValue < 512 || iValue > 16536) { bOK = false; tbMAILMaxPJSize.Background = Brushes.IndianRed; } else { tbMAILMaxPJSize.Background = Brushes.White; }

            _ = int.TryParse(tbMAILMaxTimeout.Text, out iValue);
            if (iValue < 100 || iValue > 1000000) { bOK = false; tbMAILMaxTimeout.Background = Brushes.IndianRed; } else { tbMAILMaxTimeout.Background = Brushes.White; }

            _ = int.TryParse(tbWSMaxTimeout.Text, out iValue);
            if (iValue < 100 || iValue > 1000000) { bOK = false; tbWSMaxTimeout.Background = Brushes.IndianRed; } else { tbWSMaxTimeout.Background = Brushes.White; }

            _ = int.TryParse(tbParallelJobsServiceApp.Text, out iValue);
            if (iValue < 1 || iValue > 8) { bOK = false; tbParallelJobsServiceApp.Background = Brushes.IndianRed; } else { tbParallelJobsServiceApp.Background = Brushes.White; }

            _ = int.TryParse(tbDaysKeepStackDataServiceApp.Text, out iValue);
            if (iValue < 1 || iValue > 100) { bOK = false; tbDaysKeepStackDataServiceApp.Background = Brushes.IndianRed; } else { tbDaysKeepStackDataServiceApp.Background = Brushes.White; }

            _ = int.TryParse(tbKillRunningJobsServiceApp.Text, out iValue);
            if (iValue < 1 || iValue > 24) { bOK = false; tbKillRunningJobsServiceApp.Background = Brushes.IndianRed; } else { tbKillRunningJobsServiceApp.Background = Brushes.White; }

            _ = int.TryParse(tbSameJobLatencyClientApp.Text, out iValue);
            if (iValue < 1 || iValue > 100) { bOK = false; tbSameJobLatencyClientApp.Background = Brushes.IndianRed; } else { tbSameJobLatencyClientApp.Background = Brushes.White; }

            _ = int.TryParse(tbSHellOperationsFuzibleTimeout.Text, out iValue);
            if (iValue < 1 || iValue > 999) { bOK = false; tbSHellOperationsFuzibleTimeout.Background = Brushes.IndianRed; } else { tbSHellOperationsFuzibleTimeout.Background = Brushes.White; }

            _ = int.TryParse(tbSHellOperationsExtTimeout.Text, out iValue);
            if (iValue < 1 || iValue > 999) { bOK = false; tbSHellOperationsExtTimeout.Background = Brushes.IndianRed; } else { tbSHellOperationsExtTimeout.Background = Brushes.White; }

            _ = int.TryParse(tbSendJobReportsHour.Text, out iValue);
            if (iValue < 0 || iValue > 12) { bOK = false; tbSendJobReportsHour.Background = Brushes.IndianRed; } else { tbSendJobReportsHour.Background = Brushes.White; }

            string[] sRecipients = tbMailSendJobReport.Text.Trim().Split(';');
            foreach (string sMail in sRecipients)
            {
                if (sMail.Length > 0)
                {
                    if (!Toolbox.CheckEmailValid(sMail))
                    { bOK = false; tbMailSendJobReport.Background = Brushes.IndianRed; break; }
                    else { tbMailSendJobReport.Background = Brushes.White; }
                }
                else { tbMailSendJobReport.Background = Brushes.White; }
            }

            return bOK;
        }

        private void ModifyExistingConnection(CONNString CS)
        {
            if (CS.SConnRawID <= 5) //impossible de modifier les connexions par défaut //j'ai abandonné l'idée, ça me les brise
            {
                MessageBox.Show(Languages.Languages.mc_msg_modifyconncantmodifydefault);
            }
            else
            {
                string sConnString = RichTB.GetTextRTB(tbConnectionString).Trim();
                if (CS.SConnDriver == SQLTools_Enums.BDD.MB_MAIL)
                {
                    sConnString = BuildMailConnString(false);
                }

                if (tbConnectionName.Text.Trim().Length > 0 && sConnString.Length > 0)
                {
                    List<string> sListUsage = FuzibleController.CheckConnStringUsage(cbListConnections.SelectedValue.ToString());
                    bool bSave = true;

                    if (sListUsage.Count > 0)
                    {
                        MessageBoxResult msgR = MessageBox.Show(string.Concat(Languages.Languages.mc_msg_connstringalreadyused01, sListUsage.Count.ToString(), ") : ", Environment.NewLine, string.Join(Environment.NewLine, sListUsage), Environment.NewLine, Languages.Languages.mc_msg_connstringalreadyused02), Languages.Languages.mc_msg_connstringalreadyused03, MessageBoxButton.OKCancel);

                        if (!msgR.ToString().ToUpper().Equals("OK"))
                        {
                            bSave = false;
                        }
                    }
                    if (bSave)
                    {
                        //if ((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()) == SQLTools_Enums.BDD.MB_MAIL)
                        //{
                        //    if (RichTB.GetTextRTB(tbConnectionString).Length > 0)
                        //    {
                        //        ReplaceAuthProtocolInMailCS(tbConnectionString, cbMailAuthProtocol);
                        //        ReplaceSSLInMailCS(tbConnectionString, ckMAILUseSSL);
                        //        ReplaceGetProtocolInMailCS(tbConnectionString, cbMailProtocol);
                        //    }
                        //}

                        //cette fonction récupère les éléments d'interface
                        string[] sConnVars = SetConnectionParams(true, (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString())).Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        var sConnParams = sConnVars.ToList();

                        string sOK = FuzibleController.UpdateConnection(cbListConnections.SelectedValue.ToString(), tbConnectionName.Text.Trim(),
                                                                        sConnString.Trim(),
                                                                        cbDriverSource.SelectedValue.ToString(), sConnParams);

                        MessageBox.Show(string.Concat(Languages.Languages.mc_msg_modifyconnstatus, sOK.ToString()));
                        int iIdx = cbListConnections.SelectedIndex;
                        LoadListOfConnections();
                        cbListConnections.SelectedIndex = iIdx;
                    }
                }
            }
        }

        private void SaveNewConnection()
        {
            string sConnString = RichTB.GetTextRTB(tbConnectionString).Trim();
            if ((SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()) == SQLTools_Enums.BDD.MB_MAIL)
            {
                sConnString = BuildMailConnString(false);
            }

            if (tbConnectionName.Text.Trim().Length > 0 && sConnString.Length > 0 && cbDriverSource.SelectedIndex >= 0)
            {
                string sParams = SetConnectionParams(false, (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()));

                if (sParams.Length == 0)
                {
                    sParams = LoadSQLCompatibilityParams(true, (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverSource.SelectedValue.ToString()));
                }

                List<string> sConnParams = new();
                if (sParams.Length > 0)
                {
                    sConnParams = sParams.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
                }

                string sA = FuzibleController.CreateNewConnection(cbDriverSource.SelectedValue.ToString(), tbConnectionName.Text.Trim(), sConnString, sConnParams);

                MessageBox.Show(Languages.Languages.mc_msg_newconnadded + sA);

                LoadListOfConnections();
                int iIdx = -1;
                foreach (ComboBoxItem cbI in cbListConnections.Items) { iIdx++; if (cbI.Tag.Equals(sA)) { break; } }
                cbListConnections.SelectedIndex = iIdx;
            }
            else { MessageBox.Show(Languages.Languages.mc_msg_newconnfillfields); }
        }

        private void ColorizeRichTextBox()
        {
            List<RTBColorizer> sListPatterns = new();

            //Colorisation chaîne de Connection
            MatchCollection mcRegex = Regex.Matches(RichTB.GetTextRTB(tbConnectionString), "(^.[^;=]*=)|(;.[^;=]*=)");
            foreach (Match mc in mcRegex) { sListPatterns.Add(new RTBColorizer(mc.Value[(mc.Value.StartsWith(";") ? 1 : 0)..], new SolidColorBrush(Colors.Red), FontWeights.DemiBold, FontStyles.Italic)); }
            RichTB.SetColorsRTB(tbConnectionString, sListPatterns, true);

            sListPatterns.Clear();
            mcRegex = Regex.Matches(RichTB.GetTextRTB(tbConnectionStringLog), "(^.[^;=]*=)|(;.[^;=]*=)");
            foreach (Match mc in mcRegex) { sListPatterns.Add(new RTBColorizer(mc.Value[(mc.Value.StartsWith(";") ? 1 : 0)..], new SolidColorBrush(Colors.Red), FontWeights.DemiBold, FontStyles.Italic)); }
            RichTB.SetColorsRTB(tbConnectionStringLog, sListPatterns, true);

            sListPatterns.Clear();
            mcRegex = Regex.Matches(RichTB.GetTextRTB(tbConnectionStringServiceApp), "(^.[^;=]*=)|(;.[^;=]*=)");
            foreach (Match mc in mcRegex) { sListPatterns.Add(new RTBColorizer(mc.Value[(mc.Value.StartsWith(";") ? 1 : 0)..], new SolidColorBrush(Colors.Red), FontWeights.DemiBold, FontStyles.Italic)); }
            RichTB.SetColorsRTB(tbConnectionStringServiceApp, sListPatterns, true);

        }

        private static void SetColorsParameters(string sP1, string sP2, RichTextBox rtb)
        {
            string sText = RichTB.GetTextRTB(rtb);
            string[] sParam = sText.Split(Convert.ToChar(sP1));
            List<RTBColorizer> sListPatterns = new();
            foreach (string s in sParam)
            {
                string[] sP = s.Split(Convert.ToChar(sP2));
                if (sP.Length > 1)
                {
                    sListPatterns.Add(new RTBColorizer(sP[0], new SolidColorBrush(Colors.DarkBlue), FontWeights.Normal, FontStyles.Normal));
                    sListPatterns.Add(new RTBColorizer(sP[1], new SolidColorBrush(Colors.DarkRed), FontWeights.Normal, FontStyles.Normal));
                }
            }
            sListPatterns.Add(new RTBColorizer(sP1, new SolidColorBrush(Colors.Black), FontWeights.Bold, FontStyles.Normal));
            sListPatterns.Add(new RTBColorizer(sP2, new SolidColorBrush(Colors.Black), FontWeights.Bold, FontStyles.Normal));
            RichTB.SetColorsRTB(rtb, sListPatterns, true);
        }

        private void LoadParams(CONNString _defaultCS)
        {
            try
            {
                cbWSAuthorizationMethod.SelectedIndex = 0;
                cbFileComingFrom.SelectedIndex = 0;
                tbMAILAdmin.Text = string.Join(";", FuzibleController.MainParams.APP_ADMINS);
                tbProcessorCount.Text = FuzibleController.MainParams.MULTITHREADING_CORES.ToString();
                tbProcessorCount_BigData.Text = FuzibleController.MainParams.MULTITHREADING_CORES_BIGDATA.ToString();

                RichTB.SetTextRTB(tbConnectionStringLog, FuzibleController.MainParams.LOG_CONNECTIONSTRING);
                switch (FuzibleController.MainParams.LOG_BDDDRIVER)
                {
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        tbConnectionStringLogSchema.Text = FuzibleController.MainParams.LOG_CONNECTIONSTRINGSCHEMA.Length == 0 ? "public" : FuzibleController.MainParams.LOG_CONNECTIONSTRINGSCHEMA;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        tbConnectionStringLogSchema.Text = FuzibleController.MainParams.LOG_CONNECTIONSTRINGSCHEMA.Length == 0 ? "dbo" : FuzibleController.MainParams.LOG_CONNECTIONSTRINGSCHEMA;
                        break;
                }

                //if (FuzibleController.MainParams.MAIL_CONNECTIONSTRING.IndexOf("auth_protocol=", StringComparison.InvariantCultureIgnoreCase) > -1)
                //{
                //    string sProt = FuzibleController.MainParams.MAIL_CONNECTIONSTRING[FuzibleController.MainParams.MAIL_CONNECTIONSTRING.IndexOf("auth_protocol=", StringComparison.InvariantCultureIgnoreCase)..];
                //    sProt = sProt.Split(Convert.ToChar(";"))[0];
                //    sProt = sProt[(sProt.IndexOf("=") + 1)..].ToUpper();
                //    try { cbMailAuthProtocolLog.SelectedValue = sProt; } catch { }
                //}
                //if (FuzibleController.MainParams.MAIL_CONNECTIONSTRING.IndexOf("ssl=", StringComparison.InvariantCultureIgnoreCase) > -1)
                //{
                //    string sSSL = FuzibleController.MainParams.MAIL_CONNECTIONSTRING[FuzibleController.MainParams.MAIL_CONNECTIONSTRING.IndexOf("ssl=", StringComparison.InvariantCultureIgnoreCase)..];
                //    sSSL = sSSL.Split(Convert.ToChar(";"))[0];
                //    sSSL = sSSL[(sSSL.IndexOf("=") + 1)..].ToUpper();
                //    try { ckMAILUseSSLLog.IsChecked = sSSL.Equals("1"); } catch { }
                //}

                DispatchMailConnStringToControls(FuzibleController.MainParams.MAIL_CONNECTIONSTRING, true);

                tbAbortJobErrors.Text = FuzibleController.MainParams.ABORT_JOB_ON_ERRORS.ToString();
                tbDaysKeepLog.Text = FuzibleController.MainParams.LOG_DAYSTOKEEP.ToString();
                cbDriverLog.SelectedValue = FuzibleController.MainParams.LOG_BDDDRIVER.ToString();
                ckAvoidMailIfSuccess.IsChecked = FuzibleController.MainParams.LOG_MAIL_DONTSEND_IFSUCCESS;
                ckDebugFunctions.IsChecked = FuzibleController.MainParams.LOG_DEBUG_FUNCTIONS;
                ckAllowSpecialIntegers.IsChecked = FuzibleController.MainParams.ALLOW_INTEGERS_WITH_SPECIALS;
                ckAllowIntegersWith0.IsChecked = FuzibleController.MainParams.ALLOW_INTEGERS_STARTING_WITH_0;
                tbSQLMaxDecimals.Text = FuzibleController.MainParams.SQL_DECIMALS.ToString();
                tbSQLKeepCharsDebug.Text = FuzibleController.MainParams.MAX_SQL_QUERY_DEBUG_MODE.ToString();
                tbSQLErrorsOverflow.Text = FuzibleController.MainParams.MAX_SQL_ERRORS.ToString();
                tbSQLCommit.Text = FuzibleController.MainParams.SQL_COMMIT.ToString();
                tbSQLDirectStreamCommit.Text = FuzibleController.MainParams.SQL_DIRECTSTREAM_COMMIT.ToString();
                tbSQLCommandTimeout.Text = FuzibleController.MainParams.SQL_COMMAND_TIMEOUT.ToString();
                tbSQLSynchroModeTable.Text = FuzibleController.MainParams.SQL_SYNCHRO_MODE_TABLE;
                tbFILEExported.Text = FuzibleController.MainParams.FILE_EXPORT_DIR;
                tbFILEProcessed.Text = FuzibleController.MainParams.FILE_PROCESSED_DIR;
                ckForceCSVIntegrationWrongLength.IsChecked = FuzibleController.MainParams.CSV_FORCE_INTEGRATION_WRONG_LENGTH;
                tbCSVAverageUniqueOffset.Text = FuzibleController.MainParams.CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET.ToString();
                tbCSVResemblanceOffset.Text = FuzibleController.MainParams.CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET.ToString();
                tbCSVRowsOffsetBeforeDepthAnalysis.Text = FuzibleController.MainParams.CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS.ToString();
                ckAddDatatimeOnMovedFiles.IsChecked = FuzibleController.MainParams.FILE_ADD_DATETIME_PREFIX;
                tbDaysKeepProcessedFiles.Text = FuzibleController.MainParams.FILE_DAYS_TO_KEEP_PROCESSED.ToString();
                tbCSVSeparators.Text = FuzibleController.MainParams.CSV_SEPARATORS.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\b", "\\b").Replace("\v", "\\v");
                tbFileMaxRowsBeforeSplit.Text = FuzibleController.MainParams.CSV_MAXLINES_BEFORE_SPLIT.ToString();
                ckCSVSplitDontCheckFTP.IsChecked = !FuzibleController.MainParams.CSV_MAXLINES_BEFORE_SPLIT_CHECK_DISTANT;
                tbMAILMaxPJSize.Text = FuzibleController.MainParams.MAIL_MAXPJ_SIZE.ToString();
                tbMAILMaxTimeout.Text = FuzibleController.MainParams.MAIL_TIMEOUT.ToString();
                tbMAILMaxLengthBeforePJ.Text = FuzibleController.MainParams.MAIL_MAXCHAR_BEFORE_PJ.ToString();
                tbWSMaxTimeout.Text = FuzibleController.MainParams.WS_TIMEOUT.ToString();
                tbWSDefaultEncoding.Text = FuzibleController.MainParams.WS_DEFAULT_ENCODING;
                tbWSNuxeoDefaultEncoding.Text = FuzibleController.MainParams.WS_NUXEO_DEFAULT_ENCODING;
                ckMultiTargetInParallel.IsChecked = FuzibleController.MainParams.MULTI_TARGET_IN_PARALLEL;
                ckAutoshrinktables.IsChecked = FuzibleController.MainParams.SQL_SHRINK_TABLES;
                ckShowSystemAlerts.IsChecked = FuzibleController.MainParams.SHOW_SYSTEM_ALERTS;
                ckAllowChangeSchemaInSource.IsChecked = FuzibleController.MainParams.WS_ALLOW_RESPONSES_ALTER_COLUMNS;
                ckWSSOQLRecordsOnly.IsChecked = FuzibleController.MainParams.WS_SOQL_GETRECORDSONLY;

                if (FuzibleController.CSParameters.SQLConnexionStringServiceApp != null)
                {
                    tbConnectionStringServiceApp.SetTextRTB(FuzibleController.CSParameters.SQLConnexionStringServiceApp.SConnString(null));
                    switch (FuzibleController.CSParameters.SQLConnexionStringServiceApp.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            tbConnectionStringServiceAppSchema.Text = FuzibleController.CSParameters.SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "public" : FuzibleController.CSParameters.SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA);
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            tbConnectionStringServiceAppSchema.Text = FuzibleController.CSParameters.SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA).Length == 0 ? "public" : FuzibleController.CSParameters.SQLConnexionStringServiceApp.GetParam(SQLTools_Enums.DRIVER_PARAMS.DEFAULT_SCHEMA);
                            break;
                    }
                }

                tbDaysKeepStackDataServiceApp.Text = FuzibleController.CSParameters.IKeepLogUntil.ToString();
                tbKillRunningJobsServiceApp.Text = FuzibleController.CSParameters.IPurgeIdleJobsAfterHours.ToString();
                tbParallelJobsServiceApp.Text = FuzibleController.CSParameters.IQteParallelJobs.ToString();
                cbDriverServiceApp.SelectedValue = FuzibleController.CSParameters.BDDDriverServiceApp.ToString();
                tbSameJobLatencyClientApp.Text = FuzibleController.CSParameters.FloodingInterval.ToString();

                tbSHellOperationsFuzibleTimeout.Text = FuzibleController.MainParams.SHELL_OPERATIONS_Fuzible_TIMEOUT.ToString();
                tbSHellOperationsExtTimeout.Text = FuzibleController.MainParams.SHELL_OPERATIONS_EXT_TIMEOUT.ToString();
                ckEnableQueryAssistant.IsChecked = FuzibleController.MainParams.ENABLE_QUERY_ASSISTANT;
                tbQueryAssistantMaxFileSize.Text = FuzibleController.MainParams.QASSISTANT_MAXFILESIZEANALYZE.ToString();
                ckRemoveSpecialsCharsJsonParser.IsChecked = FuzibleController.MainParams.JSON_PARSER_REPLACE_SPECIAL_CHARS;
                ckJsonAlternativeProcessingMode.IsChecked = FuzibleController.MainParams.JSON_ALTERNATIVE_PROCESSING_MODE;
                ckSecuritySharedUsers.IsChecked = FuzibleController.MainParams.SECURITY_SHARED_USERS;

                LoadListOfConnections();

                LoadListOfUsersServiceApp();
                cbServiceAppUsers.SelectedValue = FuzibleController.CSParameters.SUsername;
                if (FuzibleController.MainParams.SECURITY_SHARED_USERS)
                { cbSecurityUsers.SelectedValue = FuzibleController.MainParams.SECURITY_PRINCIPAL_USER; }
                else { cbSecurityUsers.SelectedValue = USERNAME; }

                if (FuzibleController.CSParameters.SUsername.Equals(USERNAME))
                {
                    cbServiceAppUsers.IsEnabled = true;
                    cbSecurityUsers.IsEnabled = true;
                }
                else
                {
                    cbServiceAppUsers.IsEnabled = false;
                    cbSecurityUsers.IsEnabled = false;
                }

                if (_defaultCS != null)
                {
                    cbListConnections.SelectedValue = _defaultCS.SConnID;
                    LoadConnection(_defaultCS);
                }

                cbSendJobReports.SelectedValue = FuzibleController.CSParameters.SendJobReport;
                cbSendJobReportsAmPm.SelectedValue = FuzibleController.CSParameters.SendJobReport_AmPm;
                tbSendJobReportsHour.Text = FuzibleController.CSParameters.SendJobReport_Hour.ToString();
                tbMailSendJobReport.Text = FuzibleController.CSParameters.SendJobReport_Recipients;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Languages.Languages.mc_msg_loadparamserror + ex.Message);
            }
        }

        private void LoadListOfUsersServiceApp()
        {
            cbSecurityUsers.Items.Clear();
            cbServiceAppUsers.Items.Clear();
            List<string> sListUsers = Configuration_CTL.GetUsers();

            foreach (string sUser in sListUsers)
            {
                ComboBoxItem cbNewItem = new()
                {
                    Tag = sUser,
                    Content = sUser,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                ComboBoxItem cbNewItem2 = new()
                {
                    Tag = sUser,
                    Content = sUser,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                cbServiceAppUsers.Items.Add(cbNewItem);
                cbSecurityUsers.Items.Add(cbNewItem2);
            }
        }

        private void LoadListOfConnections()
        {
            cbListConnections.Items.Clear();

            SQLTools_Enums.BDD sBDD = SQLTools_Enums.BDD.FI_FILE;
            int iC = 0;

            foreach (CONNString CS in FuzibleController.GetConnections())
            {
                iC++;
                if (!CS.SConnDriver.Equals(sBDD) || iC == 1)
                {
                    ComboBoxItem cbNewItemS1 = new()
                    {
                        Tag = string.Concat("[SEP_", CS.SConnDriverFriendlyName, "]"),
                        Content = string.Concat("[", CS.SConnDriverFriendlyName, "]"),
                        Foreground = Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        IsEnabled = false
                    };
                    cbListConnections.Items.Add(cbNewItemS1);
                }

                List<string> sListUsage = FuzibleController.CheckConnStringUsage(CS.SConnID);
                ComboBoxItem cbNewItem = new()
                {
                    Tag = CS.SConnID,
                    Content = string.Concat("   ", CS.SConnID, " -> ", CS.SConnName),
                    ToolTip = sListUsage.Count == 0 ? "" : string.Concat(Languages.Languages.mc_msg_cbconnused, Environment.NewLine, string.Join(Environment.NewLine, sListUsage)),
                    Foreground = sListUsage.Count > 0 ? Brushes.DarkRed : Brushes.DarkGreen
                }; ;
                cbListConnections.Items.Add(cbNewItem);

                sBDD = CS.SConnDriver;
            }
        }

        private void ConnectionsParamsCanvas(CONNString CS)
        {
            tbWSNuxeoUser.Text = "";
            tbWSNuxeoPassword.Password = "";
            cbWSAuthorizationMethod.SelectedIndex = 0;
            tbWSAuthorizationKey.Text = "";
            tbWSAuthorizationURL.Text = "";
            tbWSAuthorizationValue.Password = "";
            tbWSSalesforceConsumerSecret.Password = "";
            tbWSSalesforceConsumerKey.Text = "";
            tbWSSalesforceUserToken.Text = "";
            RichTB.SetTextRTB(tbWSHeaders, "");
            tbWSProxyPort.Text = "";
            tbWSProxyURL.Text = "";
            RichTB.SetTextRTB(tbWSQueryParams, "");
            lbWSAuthorizationKey.Content = "";
            lbWSAuthorizationURL.Content = "";
            lbWSAuthorizationValue.Content = "";
            cbFileComingFrom.SelectedIndex = 0;
            tbFTPPassword.Password = "";
            tbFTPPath.Text = "";
            tbFTPPort.Text = "";
            tbFTPProxyPort.Text = "";
            tbFTPProxyURL.Text = "";
            tbFTPUsername.Text = "";
            tbSFTPSSHKeyPath.Text = "";
            ckFTPIgnoreTLSErrors.IsChecked = false;
            ckFTPUseSSL.IsChecked = true;
            tbNetworkPassword.Password = "";
            tbNetworkUsername.Text = "";
            cbMailConnProtocol.SelectedIndex = 0;
            cbMailConnAuthProtocol.SelectedIndex = 0;
            tbMAILProxyURL.Text = "";
            tbMAILProxyPort.Text = "";
            ckMAILConnUseSSL.IsChecked = false;
            tbMailConnServerSmtp.Text = "";
            tbMailConnPortSend.Text = "";
            tbMailConnServerPopImap.Text = "";
            tbMailConnPortReceive.Text = "";
            cbMailConnProtocol.SelectedIndex = 0;
            tbMailConnUserSend.Text = "";
            tbMailConnPasswordSend.Password = "";
            cbNOSQLContentType.SelectedIndex = 0;
            cbFileProxyType.SelectedIndex = 0;
            tbFTPProxyUserPwd.Text = "";
            cbWSProxyType.SelectedIndex = 0;
            tbWSProxyUserPwd.Text = "";
            cbWSTemplate.SelectedIndex = 0;
            cbMailProxyType.SelectedIndex = 0;
            tbMailProxyUserPwd.Text = "";
            cbADAuthentification.SelectedIndex = 0;
            tbADOU.Text = "";
            tbADPassword.Password = "";
            tbADUsername.Text = "";

            if (CS == null)
            {
                CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                CanvasWsRestParams.Visibility = Visibility.Hidden;
                CanvasWsTemplate.Visibility = Visibility.Hidden;
                CanvasSQLParams.Visibility = Visibility.Hidden;
                CanvasMailParams.Visibility = Visibility.Hidden;
                tbConnectionString.Visibility = Visibility.Visible;
                CanvasADParams.Visibility = Visibility.Hidden;
                CanvasFileParams.Visibility = Visibility.Hidden;
                CanvasNOSQLParams.Visibility = Visibility.Hidden;
            }
            else
            {
                switch (CS.SConnDriver)
                {
                    case SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Visible;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        tbADUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_USERNAME);
                        tbADPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_PASSWORD);
                        cbADAuthentification.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_AUTH).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_AUTH) : "None";
                        tbADOU.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_OU).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_OU) : "";
                        break;
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Visible;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        break;
                    case SQLTools_Enums.BDD.NS_MONGODB:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Visible;
                        cbNOSQLContentType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MONGODB_DOCUMENT).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MONGODB_DOCUMENT) : "STANDARDBSON";
                        break;
                    case SQLTools_Enums.BDD.FI_CSV:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Visible;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        cbFileComingFrom.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) : "LOCAL";
                        tbNetworkUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME);
                        tbNetworkPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD);
                        tbFTPProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL);
                        tbFTPProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT);
                        tbFTPUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME);
                        tbFTPPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD);
                        tbFTPPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT);
                        tbFTPPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH);
                        try { ckFTPUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL)); } catch { }
                        tbSFTPSSHKeyPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);
                        try { ckFTPIgnoreTLSErrors.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS)); } catch { }
                        try { cbFileProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE); if (cbFileProxyType.SelectedIndex == -1) { cbFileProxyType.SelectedIndex = 0; } } catch { cbFileProxyType.SelectedIndex = 0; }
                        tbFTPProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.FI_FILE:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Visible;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        cbFileComingFrom.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) : "LOCAL";
                        tbNetworkUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME);
                        tbNetworkPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD);
                        tbFTPProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL);
                        tbFTPProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT);
                        tbFTPUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME);
                        tbFTPPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD);
                        tbFTPPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT);
                        tbFTPPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH);
                        try { ckFTPUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL)); }
                        catch { }
                        tbSFTPSSHKeyPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);
                        try { ckFTPIgnoreTLSErrors.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS)); } catch { }
                        try { cbFileProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE); if (cbFileProxyType.SelectedIndex == -1) { cbFileProxyType.SelectedIndex = 0; } } catch { cbFileProxyType.SelectedIndex = 0; }
                        tbFTPProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.FI_JSON:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Visible;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        cbFileComingFrom.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) : "LOCAL";
                        tbNetworkUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME);
                        tbNetworkPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD);
                        tbFTPProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL);
                        tbFTPProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT);
                        tbFTPUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME);
                        tbFTPPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD);
                        tbFTPPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT);
                        tbFTPPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH);
                        try { ckFTPUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL)); }
                        catch { }
                        tbSFTPSSHKeyPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);
                        try { ckFTPIgnoreTLSErrors.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS)); } catch { }
                        try { cbFileProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE); if (cbFileProxyType.SelectedIndex == -1) { cbFileProxyType.SelectedIndex = 0; } } catch { cbFileProxyType.SelectedIndex = 0; }
                        tbFTPProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.FI_XLS:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Visible;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        cbFileComingFrom.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) : "LOCAL";
                        tbNetworkUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME);
                        tbNetworkPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD);
                        tbFTPProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL);
                        tbFTPProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT);
                        tbFTPUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME);
                        tbFTPPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD);
                        tbFTPPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT);
                        tbFTPPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH);
                        try { ckFTPUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL)); }
                        catch { }
                        tbSFTPSSHKeyPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);
                        try { ckFTPIgnoreTLSErrors.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS)); } catch { }
                        try { cbFileProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE); if (cbFileProxyType.SelectedIndex == -1) { cbFileProxyType.SelectedIndex = 0; } } catch { cbFileProxyType.SelectedIndex = 0; }
                        tbFTPProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.FI_XML:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Visible;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        cbFileComingFrom.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) : "LOCAL";
                        tbNetworkUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME);
                        tbNetworkPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD);
                        tbFTPProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL);
                        tbFTPProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT);
                        tbFTPUsername.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME);
                        tbFTPPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD);
                        tbFTPPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT);
                        tbFTPPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH);
                        try { ckFTPUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL)); }
                        catch { }
                        tbSFTPSSHKeyPath.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);
                        try { ckFTPIgnoreTLSErrors.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS)); } catch { }
                        try { cbFileProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE); if (cbFileProxyType.SelectedIndex == -1) { cbFileProxyType.SelectedIndex = 0; } } catch { cbFileProxyType.SelectedIndex = 0; }
                        tbFTPProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.MB_MAIL:
                        CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                        CanvasWsRestParams.Visibility = Visibility.Hidden;
                        CanvasWsTemplate.Visibility = Visibility.Hidden;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Visible;
                        tbConnectionString.Visibility = Visibility.Hidden;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;
                        tbMAILProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_URL);
                        tbMAILProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_PORT);

                        DispatchMailConnStringToControls(CS.SConnString(null), false);
                        try { ckMAILConnUseSSL.IsChecked = Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_USE_SSL)); }
                        catch { }
                        cbMailConnProtocol.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROTOCOL).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROTOCOL) : "POP";
                        cbMailConnAuthProtocol.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_AUTH_PROTOCOL).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_AUTH_PROTOCOL) : "NONE";
                        try { cbMailProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_TYPE); if (cbMailProxyType.SelectedIndex == -1) { cbMailProxyType.SelectedIndex = 0; } } catch { cbMailProxyType.SelectedIndex = 0; }
                        tbMailProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_PASSWORD));
                        break;
                    case SQLTools_Enums.BDD.WS_REST:
                        CanvasWsTemplate.Visibility = Visibility.Visible;
                        CanvasSQLParams.Visibility = Visibility.Hidden;
                        CanvasMailParams.Visibility = Visibility.Hidden;
                        tbConnectionString.Visibility = Visibility.Visible;
                        CanvasADParams.Visibility = Visibility.Hidden;
                        CanvasFileParams.Visibility = Visibility.Hidden;
                        CanvasNOSQLParams.Visibility = Visibility.Hidden;

                        cbWSTemplate.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) : "DEFAULT";

                        if (cbWSTemplate.SelectedValue.ToString().Equals("NUXEO"))
                        {
                            CanvasWsNuxeoParams.Visibility = Visibility.Visible;
                            CanvasWsRestParams.Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            CanvasWsNuxeoParams.Visibility = Visibility.Hidden;
                            CanvasWsRestParams.Visibility = Visibility.Visible;
                        }

                        tbWSNuxeoUser.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_NUXEO_USER);
                        tbWSNuxeoPassword.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_NUXEO_PASSWORD);

                        tbWSProxyURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_URL);
                        tbWSProxyPort.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_PORT);
                        tbWSAuthorizationKey.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY);
                        tbWSAuthorizationValue.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE);
                        tbWSAuthorizationURL.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL);
                        tbWSSalesforceConsumerSecret.Password = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERSECRET);
                        tbWSSalesforceConsumerKey.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERKEY);
                        tbWSSalesforceUserToken.Text = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_USERTOKEN);
                        RichTB.SetTextRTB(tbWSHeaders, CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_HEADERS));
                        RichTB.SetTextRTB(tbWSQueryParams, CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS));
                        cbWSAuthorizationMethod.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD).Length > 0 ? CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD) : "NO_AUTH";
                        try { cbWSProxyType.SelectedValue = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_TYPE); if (cbWSProxyType.SelectedIndex == -1) { cbWSProxyType.SelectedIndex = 0; } } catch { cbWSProxyType.SelectedIndex = 0; }
                        tbWSProxyUserPwd.Text = string.Concat(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_USERNAME), ",", CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_PASSWORD));
                        break;
                }
                SetColorsParameters("&", "=", tbWSQueryParams);
                SetColorsParameters(";", ":", tbWSHeaders);
            }
        }

        private void LoadConnection(CONNString ActualConn)
        {
            tbConnectionName.Text = ActualConn.SConnName;
            cbDriverSource.SelectedValue = ActualConn.SConnDriver;
            RichTB.SetTextRTB(tbConnectionString, ActualConn.SConnString(null));
            tbConnectionParams.Text = string.Join(Environment.NewLine, ActualConn.SConnParams);
            cbDriverDateFormat.SelectedValue = ActualConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.DRIVER_DATE_LOCALE);
            cbDriverNumberFormat.SelectedValue = ActualConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.DRIVER_DECIMALS_LOCALE);

            ColorizeRichTextBox();
        }

        private static string LoadSQLCompatibilityParams(bool bAddLocales, SQLTools_Enums.BDD sBDD)
        {
            List<string> sListParams;

            if (sBDD == SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY)
            {
                sListParams = SQLQueries.ParamsForADDriver();
            }
            else { sListParams = SQLQueries.ParamsForSQLDriver(sBDD, ""); }

            if (bAddLocales)
            {
                sListParams.Add("P_DRIVER_DATE_LOCALE=" + INIProgram.SYSTEM_CULTURE_DATE.ShortDatePattern);
                sListParams.Add("P_DRIVER_DECIMALS_LOCALE=" + INIProgram.SYSTEM_CULTURE_NUMBER.NumberDecimalSeparator);
            }

            return string.Join(Environment.NewLine, sListParams);
        }

        private static void ConfigureWindowsTaskManager(bool bActivateNow, bool bSystemUser)
        {
            ServiceApp.ConfigureWindowsTaskManager(bActivateNow, bSystemUser);
        }

        #endregion

    }
}
