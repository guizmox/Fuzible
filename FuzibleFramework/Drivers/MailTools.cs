using HtmlAgilityPack;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Proxy;
using MailKit.Search;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace FuzibleFramework
{
    public class MailTools
    {
        #region "CONSTANTES"
        public static List<string> MAIL_FIELDS = new() { "BCC", "BODY", "CC", "DATE", "FROM", "HEADERS", "HTMLBODY", "IMPORTANCE", "INREPLYTO", "MESSAGEID", "MIMEVERSION", "PRIORITY", "REPLYTO", "RESENTBCC", "RESENTCC", " RESENTDATE", "RESENTFROM", "RESENTMESSAGEID", "RESENTREPLYTO", "RESENTSENDER", "RESENTTO", "SENDER", " SUBJECT", "TEXTBODY", "TO" };
        public static string MAIL_MOVE_FOLDER = "Fuzible";
        public static readonly string WORKING_DIRECTORY_PJ = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "\\TEMP\\");
        
        #endregion

        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.PRG;
        public LogTools MyLog;
        public Job JobParameters;
        internal readonly MAILConnectionVariables MAILVariables = null;

        #endregion

        #region "PROPRIETES"

        #endregion

         #region "PUBLIC VOID"

        public MailTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            MyLog = LogJob;
            JobParameters = INIP;

            ClassPurpose = sSourceTargetLog;

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.LOG:
                    //CONNString CSLog = new CONNString(INIP.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", INIP.GlobalParameters.LOG_CONNECTIONSTRING);
                    //MAILVariables = new MAILConnectionVariables(CSLog, INIP.GlobalParameters.MAIL_TIMEOUT, INIP.GlobalParameters.MAIL_MAXPJ_SIZE, INIP.DynParams);
                    MAILVariables = new MAILConnectionVariables(new CONNString(SQLTools_Enums.BDD.MB_MAIL, "[0]", "SHS", JobParameters.GlobalParameters.MAIL_CONNECTIONSTRING), INIP.GlobalParameters.MAIL_TIMEOUT, INIP.GlobalParameters.MAIL_MAXPJ_SIZE, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.PRG:
                    MAILVariables = new MAILConnectionVariables(new CONNString(SQLTools_Enums.BDD.MB_MAIL, "[0]", "SHS", JobParameters.GlobalParameters.MAIL_CONNECTIONSTRING), INIP.GlobalParameters.MAIL_TIMEOUT, INIP.GlobalParameters.MAIL_MAXPJ_SIZE, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.SRC:
                    MAILVariables = new MAILConnectionVariables(INIP.ConnectionString_Source, INIP.GlobalParameters.MAIL_TIMEOUT, INIP.GlobalParameters.MAIL_MAXPJ_SIZE, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG:
                    MAILVariables = new MAILConnectionVariables(INIP.ConnectionString_Target, INIP.GlobalParameters.MAIL_TIMEOUT, INIP.GlobalParameters.MAIL_MAXPJ_SIZE, INIP.DynParams);
                    break;
            }
        }

        private static MailKit.Net.Smtp.SmtpClient GetSMTPProxy(MAILConnectionVariables MAILVariables, MailKit.Net.Smtp.SmtpClient smtpClient)
        {
            switch (MAILVariables.ProxyType)
            {
                case ProxyTypes.None:
                    smtpClient.ProxyClient = null;
                    break;
                case ProxyTypes.Http:
                    smtpClient.ProxyClient = new HttpProxyClient(MAILVariables.ProxyURL, MAILVariables.ProxyPort);
                    break;
                case ProxyTypes.Socks4:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { smtpClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { smtpClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
                case ProxyTypes.Socks5:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { smtpClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { smtpClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
            }

            return smtpClient;
        }

        private static MailKit.Net.Pop3.IPop3Client GetPOPProxy(MAILConnectionVariables MAILVariables, MailKit.Net.Pop3.IPop3Client popClient)
        {
            switch (MAILVariables.ProxyType)
            {
                case ProxyTypes.None:
                    popClient.ProxyClient = null;
                    break;
                case ProxyTypes.Http:
                    popClient.ProxyClient = new HttpProxyClient(MAILVariables.ProxyURL, MAILVariables.ProxyPort);
                    break;
                case ProxyTypes.Socks4:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { popClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { popClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
                case ProxyTypes.Socks5:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { popClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { popClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
            }

            return popClient;
        }

        private static MailKit.Net.Imap.ImapClient GetIMAPProxy(MAILConnectionVariables MAILVariables, MailKit.Net.Imap.ImapClient imapClient)
        {
            switch (MAILVariables.ProxyType)
            {
                case ProxyTypes.None:
                    imapClient.ProxyClient = null;
                    break;
                case ProxyTypes.Http:
                    imapClient.ProxyClient = new HttpProxyClient(MAILVariables.ProxyURL, MAILVariables.ProxyPort);
                    break;
                case ProxyTypes.Socks4:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { imapClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { imapClient.ProxyClient = new Socks4Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
                case ProxyTypes.Socks5:
                    if (MAILVariables.ProxyUsername.Length == 0)
                    { imapClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort); }
                    else
                    { imapClient.ProxyClient = new Socks5Client(MAILVariables.ProxyURL, MAILVariables.ProxyPort, new NetworkCredential(MAILVariables.ProxyUsername, MAILVariables.ProxyPassword)); }
                    break;
            }

            return imapClient;
        }

        public bool AddPJToMail(string sFile)
        {
            bool bOk;

            if (sFile.Length > 0)
            {
                if (File.Exists(sFile))
                {
                    double len = new FileInfo(sFile).Length;
                    len /= 1024; //octet -> ko
                    if (len < MAILVariables.MaxAttachementSizeInKo)
                    { bOk = true; }
                    else { bOk = false; }
                }
                else { bOk = false; }
            }
            else { bOk = false; }

            return bOk;
        }

        public static string DatasetToHTML(DataSet dsData, bool mustBeSexy, bool bHTMLEncode, bool bCustomHTMLTemplate, bool bWithBorder)
        {
            StringBuilder sbHTML = new();

            try
            {
                sbHTML.AppendLine("<html>");
                foreach (DataTable dt in dsData.Tables)
                {
                    sbHTML.AppendLine("<p class=\"" + dt.Namespace.Replace("\"", "_") + "\">");
                    double dC = dt.Columns.Count / 100.00;
                    double dFontSize = Math.Ceiling(dC) * 100;
                    sbHTML.AppendLine("<head>");
                    sbHTML.AppendLine("<style>");
                    sbHTML.AppendLine("table, th, td " + (bWithBorder ? "{border: 1px solid black;border-collapse: collapse;white-space:nowrap;}" : "{}"));
                    sbHTML.AppendLine("th, td {padding: 5px;font-size: " + Convert.ToInt16(dFontSize).ToString() + "%;white-space:nowrap;}");
                    sbHTML.AppendLine("</style>");
                    sbHTML.AppendLine("</head>");

                    if (mustBeSexy)
                    {
                        int cptRow = 0;

                        sbHTML.AppendLine("<body>");

                        if (!bCustomHTMLTemplate)
                        {
                            string sCell = FormatCells(HTMLEncode(dt.Namespace, bHTMLEncode));
                            sbHTML.AppendLine("<h2>" + string.Concat(sCell.Replace("\r\n", "<br />"), " : ", dt.Rows.Count.ToString(), (dt.Rows.Count > 1 ? Languages.Languages.mb_buildhtml_records : "")) + "</h2></br></br>");
                        }

                        sbHTML.AppendLine("<table " + (bWithBorder ? "border=\"1\"" : "") + ">");
                        sbHTML.AppendLine("<tr bgcolor=\"#F2F5A9\">");
                        foreach (DataColumn cell in dt.Columns)
                        {
                            string sCell = FormatCells(HTMLEncode(cell.ColumnName, bHTMLEncode));
                            sbHTML.AppendLine("<th>" + sCell.Replace("\r\n", "<br />") + "</th>"); 
                        }
                        sbHTML.AppendLine("</tr>");

                        foreach (DataRow row in dt.Rows)
                        {
                            cptRow += 1;
                            if (cptRow % 2 == 0)
                            { sbHTML.AppendLine("<tr bgcolor=\"#FBEFF2\">"); }
                            else
                            { sbHTML.AppendLine("<tr bgcolor=\"#FBFBEF\">"); }

                            foreach (DataColumn cell in dt.Columns)
                            {
                                string sCell = row[cell.ColumnName].ToString().Trim();
                                sCell = HTMLEncode(sCell, bHTMLEncode);

                                sbHTML.AppendLine("<td>" + sCell + "</td>");
                            }
                            sbHTML.AppendLine("</tr>");
                        }
                        sbHTML.AppendLine("</table>");
                        sbHTML.AppendLine("</body>");
                    }
                    else
                    { //TODO !!!!!!!! 
                    }
                    sbHTML.AppendLine("</p>");
                }
                sbHTML.AppendLine("</html>");
            }
            catch (Exception)
            { throw; }

            return sbHTML.ToString();

        }

        public static string DatatableToHTML(DataTable dtData, bool mustBeSexy, bool bHTMLEncode, bool bCustomHTMLTemplate, bool bWithBorder)
        {

            StringBuilder sbHTML = new();

            try
            {
                sbHTML.AppendLine("<html>");

                double dC = dtData.Columns.Count / 100.00;
                double dFontSize = Math.Ceiling(dC) * 100;

                sbHTML.AppendLine("<p class=\"" + dtData.Namespace.Replace("\"", "_") + "\">");

                sbHTML.AppendLine("<head>");
                sbHTML.AppendLine("<style>");
                sbHTML.AppendLine("table, th, td " + (bWithBorder ? "{border: 1px solid black;border-collapse: collapse;white-space:nowrap;}" : "{}")); 
                sbHTML.AppendLine("th, td {padding: 5px;font-size: " + Convert.ToInt16(dFontSize).ToString() + "%;white-space:nowrap;}");
                sbHTML.AppendLine("</style>");
                sbHTML.AppendLine("</head>");

                if (mustBeSexy)
                {
                    int cptRow = 0;

                    sbHTML.AppendLine("<body>");

                    if (!bCustomHTMLTemplate)
                    {
                        string sCell = FormatCells(HTMLEncode(dtData.Namespace, bHTMLEncode));
                        sbHTML.AppendLine("<h3>" + string.Concat(sCell.Replace("\r\n", "<br />"), " : ", dtData.Rows.Count.ToString(), (dtData.Rows.Count > 1 ? Languages.Languages.mb_buildhtml_records : "")) + "</h3>");
                    }

                    sbHTML.AppendLine("<table " + (bWithBorder ? "border=\"1\"" : "") + ">");
                    sbHTML.AppendLine("<tr bgcolor=\"#F2F5A9\">");
                    foreach (DataColumn cell in dtData.Columns)
                    {
                        string sCell = FormatCells(HTMLEncode(cell.ColumnName, bHTMLEncode));
                        sbHTML.AppendLine("<th>" + sCell.Replace("\r\n", "<br />") + "</th>"); 
                    }
                    sbHTML.AppendLine("</tr>");

                    foreach (DataRow row in dtData.Rows)
                    {
                        cptRow += 1;
                        if (cptRow % 2 == 0)
                        { sbHTML.AppendLine("<tr bgcolor=\"#FBEFF2\">"); }
                        else
                        { sbHTML.AppendLine("<tr bgcolor=\"#FBFBEF\">"); }

                        foreach (DataColumn cell in dtData.Columns)
                        {
                            string sCell = row[cell.ColumnName].ToString().Trim();

                            sCell = HTMLEncode(sCell, bHTMLEncode);

                            sbHTML.AppendLine("<td>" + sCell + "</td>");
                        }
                        sbHTML.AppendLine("</tr>");
                    }
                    sbHTML.AppendLine("</table>");
                    sbHTML.AppendLine("</body>");
                }
                else
                { //TODO !!!!!!!! 
                }
                sbHTML.AppendLine("</p>");
                sbHTML.AppendLine("</html>");
            }
            catch (Exception)
            { throw; }

            return sbHTML.ToString();

        }

        private static string HTMLEncode(string sCell, bool bHTMLEncode)
        {
            if (bHTMLEncode)
            {
                Match rgXml = Regex.Match(sCell, "(<.+>)(.+?)(<\\/.*>)");
                if (rgXml.Success)
                {
                    string sData = HttpUtility.HtmlEncode(rgXml.Groups[2].Value);
                    return rgXml.Value.Replace(rgXml.Groups[2].Value, sData);
                }
                else return HttpUtility.HtmlEncode(sCell);
            }
            else
            {
                sCell = sCell.Replace("\r\n", "<br />");
                return sCell;
            }
        }

        public async Task<DataSet> GetDataFromMailBox(Query FuzibleQuery)
        {
            try
            {
                DataTable dtData = new(FuzibleQuery.QueryAnalyzer.Tables[0].Alias);
                dtData.Columns.Add("BCC", Type.GetType("System.String"));
                dtData.Columns.Add("BODY", Type.GetType("System.String"));
                dtData.Columns.Add("CC", Type.GetType("System.String"));
                dtData.Columns.Add("DATE", Type.GetType("System.DateTime"));
                dtData.Columns.Add("FROM", Type.GetType("System.String"));
                dtData.Columns.Add("HEADERS", Type.GetType("System.String"));
                dtData.Columns.Add("HTMLBODY", Type.GetType("System.String"));
                dtData.Columns.Add("IMPORTANCE", Type.GetType("System.String"));
                dtData.Columns.Add("INREPLYTO", Type.GetType("System.String"));
                dtData.Columns.Add("MESSAGEID", Type.GetType("System.String"));
                dtData.Columns.Add("MIMEVERSION", Type.GetType("System.String"));
                dtData.Columns.Add("PRIORITY", Type.GetType("System.String"));
                dtData.Columns.Add("REPLYTO", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTBCC", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTCC", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTDATE", Type.GetType("System.DateTime"));
                dtData.Columns.Add("RESENTFROM", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTMESSAGEID", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTREPLYTO", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTSENDER", Type.GetType("System.String"));
                dtData.Columns.Add("RESENTTO", Type.GetType("System.String"));
                dtData.Columns.Add("SENDER", Type.GetType("System.String"));
                dtData.Columns.Add("SUBJECT", Type.GetType("System.String"));
                dtData.Columns.Add("TEXTBODY", Type.GetType("System.String"));
                dtData.Columns.Add("TO", Type.GetType("System.String"));
                dtData.Columns.Add("UID", Type.GetType("System.String"));

                bool bOk = true;
                string sUID;

                string sMailAdress = FuzibleQuery.QueryAnalyzer.Tables[0].Name;
                string sUser;
                string sPassword = MAILVariables.SenderPassword;
                //paramétrage : SELECT * from mymail@mymail.com[password]
                if (sMailAdress.IndexOf("[") > 0 && sMailAdress.EndsWith("]"))
                {
                    sUser = sMailAdress[..sMailAdress.IndexOf("[")];
                    sPassword = sMailAdress[(sMailAdress.IndexOf("[") + 1)..];
                    sPassword = sPassword[0..^1];
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_cantgeetpwdinquery, SQLTools_Enums.LOG_TYPEINFO.INF);
                    sUser = sMailAdress;
                }

                if (Toolbox.CheckEmailValid(sUser))
                {
                    int iMaxMailsToGet = JobParameters.MaxMailsToGet;
                    if (FuzibleQuery.QueryAnalyzer.LimitedResults > 0) { iMaxMailsToGet = FuzibleQuery.QueryAnalyzer.LimitedResults; }

                    switch (MAILVariables.MailGetProtocol)
                    {
                        case SQLTools_Enums.MAIL_GET_PROTOCOL.POP:
                            using (var client = new MailKit.Net.Pop3.Pop3Client())
                            {
                                GetPOPProxy(MAILVariables, client);
                                try
                                {
                                    switch (MAILVariables.AuthentificationProtocol)
                                    {
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                    }
                                    client.AuthenticationMechanisms.Remove("NTLM");
                                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                                    await client.AuthenticateAsync(sUser, sPassword, Monitoring.TaskCancellationToken);
                                }
                                catch (Exception ex)
                                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); bOk = false; FuzibleQuery.QueryErrors += 1; }

                                if (bOk)
                                {
                                    int iStop = 0;
                                    int iQteMail = await client.GetMessageCountAsync(Monitoring.TaskCancellationToken);
                                    if (iQteMail > iMaxMailsToGet) { iStop = iQteMail - iMaxMailsToGet; }

                                    for (int iM = iQteMail - 1; iM >= iStop; iM--)
                                    {
                                        MimeKit.MimeMessage UIMessage = await client.GetMessageAsync(iM, Monitoring.TaskCancellationToken);
                                        sUID = await client.GetMessageUidAsync(iM, Monitoring.TaskCancellationToken);
                                        DataRow dR = dtData.NewRow();
                                        dR.ItemArray = GetListStringFromMimeMessage(UIMessage, sUID).ToArray();
                                        dtData.Rows.Add(dR);

                                        if (iM % 100 == 0)
                                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.mb_retrieve, iM.ToString(), "/", iQteMail.ToString() + Toolbox.GetPercent(0, iQteMail, iM)), SQLTools_Enums.LOG_TYPEINFO.DET); }

                                        if (JobParameters.MailFlagRetrievedAsRead > 0)
                                        {
                                            if (JobParameters.MailFlagRetrievedAsRead == 1)
                                            {
                                                if (JobParameters.RunInSimulationMode)
                                                { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_delete, sUID)); }
                                                else
                                                { await client.DeleteMessageAsync(iM, Monitoring.TaskCancellationToken); }
                                            }
                                            else if (JobParameters.MailFlagRetrievedAsRead == 2)
                                            {
                                                if (JobParameters.RunInSimulationMode)
                                                { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_move, sUID)); }
                                                else
                                                { //unsupported in POP
                                                }
                                            }
                                            else if (JobParameters.MailFlagRetrievedAsRead == 3)
                                            {
                                                if (JobParameters.RunInSimulationMode)
                                                { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_flag, sUID)); }
                                                else
                                                { //unsupported in POP
                                                }
                                            }
                                        }
                                    }
                                    await client.DisconnectAsync(true, Monitoring.TaskCancellationToken);
                                }
                            }
                            break;

                        case SQLTools_Enums.MAIL_GET_PROTOCOL.IMAP:
                            using (var client = new ImapClient())
                            {
                                GetIMAPProxy(MAILVariables, client);
                                try
                                {
                                    switch (MAILVariables.AuthentificationProtocol)
                                    {
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                                            await client.ConnectAsync(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls, Monitoring.TaskCancellationToken);
                                            break;
                                    }
                                    client.AuthenticationMechanisms.Remove("NTLM");
                                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                                    await client.AuthenticateAsync(sUser, sPassword, Monitoring.TaskCancellationToken);
                                }
                                catch (Exception ex)
                                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); bOk = false; FuzibleQuery.QueryErrors += 1; }

                                if (bOk)
                                {
                                    // The Inbox folder is always available on all IMAP servers...
                                    IMailFolder inbox = client.Inbox;
                                    if (JobParameters.MailFlagRetrievedAsRead > 0 && !JobParameters.RunInSimulationMode)
                                    { await inbox.OpenAsync(FolderAccess.ReadWrite, Monitoring.TaskCancellationToken); }
                                    else { await inbox.OpenAsync(FolderAccess.ReadOnly, Monitoring.TaskCancellationToken); }

                                    try //tous les serveurs ne supportent pas ESEARCH. Solution rustine en dessous
                                    {
                                        SearchResults sMresults = new();

                                        sMresults = await inbox.SearchAsync(SearchOptions.All, JobParameters.MailGetUnreadOnly ? SearchQuery.NotSeen : SearchQuery.All, Monitoring.TaskCancellationToken);
                                        //int iResult = 0;
                                        int iStop = 0;
                                        if (sMresults.Count > iMaxMailsToGet) { iStop = sMresults.Count - iMaxMailsToGet; }

                                        //foreach (UniqueId UIMessage in sMresults.UniqueIds)
                                        for (int iM = sMresults.Count - 1; iM >= iStop; iM--)
                                        {
                                            MimeKit.MimeMessage mBmessage = await inbox.GetMessageAsync(sMresults.UniqueIds[iM], Monitoring.TaskCancellationToken);
                                            sUID = sMresults.UniqueIds[iM].Id.ToString();
                                            DataRow dR = dtData.NewRow();
                                            dR.ItemArray = GetListStringFromMimeMessage(mBmessage, sUID).ToArray();
                                            dtData.Rows.Add(dR);
                                            //Mark message as read

                                            if (JobParameters.MailFlagRetrievedAsRead > 0)
                                            {
                                                if (JobParameters.MailFlagRetrievedAsRead == 1)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_delete, mBmessage.MessageId)); }
                                                    else
                                                    { await inbox.AddFlagsAsync(sMresults.UniqueIds[iM], MessageFlags.Deleted, true, Monitoring.TaskCancellationToken); }
                                                }
                                                else if (JobParameters.MailFlagRetrievedAsRead == 2)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_move, mBmessage.MessageId)); }
                                                    else
                                                    {

                                                        IMailFolder mfFolder = await inbox.CreateAsync(MAIL_MOVE_FOLDER, true, Monitoring.TaskCancellationToken);
                                                        await inbox.MoveToAsync(sMresults.UniqueIds[iM], mfFolder, Monitoring.TaskCancellationToken);
                                                    }
                                                }
                                                else if (JobParameters.MailFlagRetrievedAsRead == 3)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_flag, mBmessage.MessageId)); }
                                                    else
                                                    { await inbox.AddFlagsAsync(sMresults.UniqueIds[iM], MessageFlags.Seen, true, Monitoring.TaskCancellationToken); }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.mb_retrieve_changemethod, SQLTools_Enums.LOG_TYPEINFO.WNG);

                                        int iStop = 0;
                                        int iQteMail = JobParameters.MailGetUnreadOnly ? inbox.Unread : inbox.Count;
                                        if (iQteMail > iMaxMailsToGet) { iStop = iQteMail - iMaxMailsToGet; }

                                        for (int iM = iQteMail - 1; iM >= iStop; iM--)
                                        {
                                            MimeKit.MimeMessage UIMessage = await inbox.GetMessageAsync(iM, Monitoring.TaskCancellationToken);
                                            if (UIMessage == null) { break; }

                                            sUID = UIMessage.MessageId;
                                            DataRow dR = dtData.NewRow();
                                            dR.ItemArray = GetListStringFromMimeMessage(UIMessage, sUID).ToArray();
                                            dtData.Rows.Add(dR);

                                            if (iM % 100 == 0)
                                            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.mb_retrieve, iM.ToString(), "/", iQteMail.ToString() + Toolbox.GetPercent(0, iQteMail, iM)), SQLTools_Enums.LOG_TYPEINFO.DET); }

                                            if (JobParameters.MailFlagRetrievedAsRead > 0)
                                            {
                                                IList<UniqueId> uids = await inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", UIMessage.MessageId), Monitoring.TaskCancellationToken);

                                                if (JobParameters.MailFlagRetrievedAsRead == 1 && uids != null)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_delete, sUID)); }
                                                    else
                                                    { await inbox.AddFlagsAsync(uids, MessageFlags.Deleted, true, Monitoring.TaskCancellationToken); }
                                                }
                                                else if (JobParameters.MailFlagRetrievedAsRead == 2)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_move, sUID)); }
                                                    else
                                                    {

                                                        IMailFolder mfFolder = await inbox.CreateAsync(MAIL_MOVE_FOLDER, true, Monitoring.TaskCancellationToken);
                                                        await inbox.MoveToAsync(uids, mfFolder, Monitoring.TaskCancellationToken);
                                                    }
                                                }
                                                else if (JobParameters.MailFlagRetrievedAsRead == 3)
                                                {
                                                    if (JobParameters.RunInSimulationMode)
                                                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_flag, sUID)); }
                                                    else
                                                    { await inbox.AddFlagsAsync(uids, MessageFlags.Seen, true, Monitoring.TaskCancellationToken); }
                                                }
                                            }
                                        }
                                    }

                                    await client.DisconnectAsync(true, Monitoring.TaskCancellationToken);
                                }
                            }
                            break;
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.mb_retrieve_unknownmail01 + sUser + Languages.Languages.mb_retrieve_unknownmail02);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }

                //je force la colonne messageID comme clé primaire
                //if (dtData.Columns.Contains("UID"))
                //{
                //    DataColumn[] dtCPKS = new DataColumn[1];
                //    dtCPKS[0] = dtData.Columns["UID"];
                //    dtData.PrimaryKey = dtCPKS;
                //}
                DataSet dsData = new(FuzibleQuery.QueryAnalyzer.Tables[0].Name);
                dtData.Namespace = FuzibleQuery.QueryAnalyzer.Tables[0].Alias;
                dsData.Tables.Add(dtData);
                dsData.CaseSensitive = false;
                return dsData;
            }
            catch (OperationCanceledException)
            { throw; }
        }

        public string ListOfStringToHTML(List<string> listData, string sNomListe = "Valeurs", bool mustBeSexy = true)
        {
            StringBuilder sbHTML = new();


            try
            {

                if (listData != null)
                {

                    if (mustBeSexy)
                    {
                        int cptRow = 0;

                        sbHTML.AppendLine("<html>");
                        sbHTML.AppendLine("<head>");
                        sbHTML.AppendLine("<style>");

                        sbHTML.AppendLine("table, th, td {border: 1px solid black;border-collapse: collapse;}");
                        sbHTML.AppendLine("th, td {padding: 5px;font-size: 75%}");

                        sbHTML.AppendLine("</style>");
                        sbHTML.AppendLine("</head>");

                        sbHTML.AppendLine("<body>");

                        sbHTML.AppendLine("<table border=\"1\">");

                        sbHTML.AppendLine("<tr bgcolor=\"#F2F5A9\">");
                        sbHTML.AppendLine("<th>" + sNomListe.ToUpper().Replace("_", " ") + "</th>");

                        sbHTML.AppendLine("</tr>");

                        foreach (string str in listData)
                        {
                            cptRow += 1;
                            if (cptRow % 2 == 0)
                            {
                                sbHTML.AppendLine("<tr bgcolor=\"#FBEFF2\">");
                            }
                            else
                            {
                                sbHTML.AppendLine("<tr bgcolor=\"#FBFBEF\">");
                            }
                            sbHTML.AppendLine("<td>" + str + "</td>");
                            sbHTML.AppendLine("</tr>");
                        }

                        sbHTML.AppendLine("</table>");
                        sbHTML.AppendLine("</body>");
                        sbHTML.AppendLine("</html>");


                    }
                    else
                    {
                        //TODO !!!!!!!!

                    }

                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
            }

            return sbHTML.ToString();

        }

        public List<string> PrepareDataToBeSent(ref string sContent, DataSet dsSourceData, string sNamespace, int iRows, Query FuzibleQuery)
        {
            List<string> sListPJ = new();

            if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput) //mode raw : on extraie le fichier à partir du tableau de bytes, on fait un fichier temporaire et GO
            {
                if (!Directory.Exists(WORKING_DIRECTORY_PJ)) { try { Directory.CreateDirectory(WORKING_DIRECTORY_PJ); } catch (Exception) { throw; } }
                CleanTempPath(); //litière du chat
                foreach (DataTable dt in dsSourceData.Tables)
                {
                    if (dt.Rows.Count > 0 && dt.Columns.Count >= 2)
                    {
                        //extraction des fichiers : on a toujours le nom du fichier dans col 0 et le tableau de bytes dans col 1
                        string sFile = dt.Rows[0][0].ToString();

                        sFile = Path.Combine(WORKING_DIRECTORY_PJ, sFile);
                        if (File.Exists(sFile))
                        { 
                            try 
                            { 
                                File.Delete(sFile);
                            } 
                            catch (Exception)
                            { throw; } 
                        }
                        try
                        {
                            using var writer = new BinaryWriter(File.OpenWrite(sFile));
                            var by = (byte[])dt.Rows[0][1];
                            writer.Write(by);
                            sListPJ.Add(sFile);
                            sContent = string.Concat(sContent, "<br>", Languages.Languages.mb_send_pj01, " : ", Path.GetFileName(sFile), " (", (by.Length / 1000).ToString() + " KB)");
                        }
                        catch (Exception)
                        { throw; }
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.mb_cantbuildfilefromraw);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dt.TableName + " (Rows:" + dt.Rows.Count + " - Cols:" + dt.Columns.Count + ")", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                }
            }
            else
            {
                sContent = DatasetToHTML(dsSourceData, true, true, JobParameters.MailTargetHTMLFileTemplate.Length > 0, false); //TODO : multithreading

                bool bPJ = JobParameters.MailTargetFormat != SQLTools_Enums.MAIL_TARGET_FORMAT.HTML_TABLE;
                string sExtAttach = JobParameters.MailTargetFormat.ToString().IndexOf("_CSV_") > -1 ? "CSV" : "XLSX";

                if ((!bPJ && sContent.Length > JobParameters.GlobalParameters.MAIL_MAXCHAR_BEFORE_PJ) || bPJ) //si tableau trop grand -> envoyer en PJ
                {
                    string sPath = string.Concat(WORKING_DIRECTORY_PJ, "\\FILES\\");
                    FITools FIAttachment = CreateFakeAttachmentTargetFromJob(sPath, JobParameters, MyLog);
                    for (int iTt = 0; iTt < dsSourceData.Tables.Count; iTt++)
                    {
                        string sFile = string.Concat(DateTime.Now.ToString("yyyyMMdd"), "_", dsSourceData.Tables[iTt].Namespace, iTt.ToString("00"), "." + sExtAttach);
                        FIAttachment.BuildFileFromDs(dsSourceData.Tables[iTt], sFile, FuzibleQuery);
                        sFile = string.Concat(sPath, sFile);
                        sListPJ.Add(sFile);
                        sContent = string.Concat(sContent, "<br>", Languages.Languages.mb_send_pj01, sExtAttach, Languages.Languages.mb_send_pj02, dsSourceData.Tables[iTt].Namespace.Replace("_", " "), " - ", dsSourceData.Tables[iTt].Rows.Count.ToString(), Languages.Languages.mb_send_pj03);
                    }

                    sContent = JobParameters.JobDescription.Length == 0 ? JobParameters.JobNAME : JobParameters.JobDescription;
                    sContent = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sContent, JobParameters.DynParams);

                    sContent = string.Concat(sContent, "<br>", Languages.Languages.mb_send_pj01, sExtAttach, Languages.Languages.mb_send_pj02, sNamespace.Replace("_", " "), " - ", iRows.ToString(), Languages.Languages.mb_send_pj03);
                }
            }

            return sListPJ;
        }

        private void CleanTempPath()
        {
            if (Directory.Exists(WORKING_DIRECTORY_PJ))
            {
                FileInfo fiI;

                foreach (string sFile in Directory.GetFiles(WORKING_DIRECTORY_PJ))
                {
                    try
                    {
                        fiI = new FileInfo(sFile);
                        if (fiI.CreationTime < DateTime.Now.AddDays(-JobParameters.GlobalParameters.FILE_DAYS_TO_KEEP_PROCESSED))
                        { File.Delete(sFile); }
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Path.GetFileName(sFile), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
            }
        }

        public bool SendMail(string sObjetMail, string sContenuMail, List<string> sListeDestinataires_TO, List<string> sListeDestinataires_CC, List<string> sListPJ, bool bSentFromLog)
        {
            bool bOK = false;

            try
            {
                MimeKit.MimeMessage oMail = new() { Sender = new MimeKit.MailboxAddress(MAILVariables.SenderAddress.Split("@")[0], MAILVariables.SenderAddress) };
                //oMail.ReplyTo.Add(new MimeKit.InternetAddress(MAILVariables.SenderAddress));

                if (JobParameters.RunInSimulationMode)
                { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_sendobject, sObjetMail)); }

                oMail.Subject = sObjetMail;
                //oMail.HtmlBody. = true;

                if (JobParameters.MailTargetFormat == SQLTools_Enums.MAIL_TARGET_FORMAT.HTML_TABLE &&
                    JobParameters.MailTargetHTMLFileTemplate.Length > 0 && 
                    !bSentFromLog)
                {
                    sContenuMail = PrepareMailWithTemplate(sContenuMail);
                }

                sContenuMail = string.Concat(sContenuMail, "<p>&nbsp;</p><p><span style=\"color: #000080;\"><em>" + Languages.Languages.mb_send_autogenerate + "</em></span><strong><br /></strong></p>");

                MimeKit.BodyBuilder bBody = new() { HtmlBody = sContenuMail };

                if (sListPJ != null)
                {
                    foreach (string sFile in sListPJ)
                    {
                        if (JobParameters.RunInSimulationMode)
                        { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_sendattachment, sFile)); }
                        if (AddPJToMail(sFile))
                        { bBody.Attachments.Add(sFile); }
                        else
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_send_exceedslimit + sFile + ")", SQLTools_Enums.LOG_TYPEINFO.INF); }
                    }
                }
                oMail.Body = bBody.ToMessageBody();

                foreach (string sMail in sListeDestinataires_TO)
                {
                    if (Toolbox.CheckEmailValid(sMail))
                    {
                        if (JobParameters.RunInSimulationMode)
                        { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_sendaddto, Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sMail, JobParameters.DynParams))); }
                        if (sMail.Length > 0)
                        {
                            string sEmail = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sMail, JobParameters.DynParams);
                            oMail.To.Add(new MimeKit.MailboxAddress(sEmail.Split("@")[0], sEmail));
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.mb_send_unrecognizedmail, sMail), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                }

                if (sListeDestinataires_CC != null)
                {
                    foreach (string sMail in sListeDestinataires_CC)
                    {
                        if (Toolbox.CheckEmailValid(sMail))
                        {
                            if (JobParameters.RunInSimulationMode)
                            { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_sendaddcc, sMail)); }
                            if (sMail.Length > 0)
                            { oMail.Cc.Add(new MimeKit.MailboxAddress(sMail.Split("@")[0], sMail)); }
                        }
                        else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.mb_send_unrecognizedmail, sMail), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    }
                }

                //si des admin sont définis sur l'appli, on les met en copie des mails de LOG
                if (bSentFromLog && JobParameters.GlobalParameters.APP_ADMINS != null)
                {
                    foreach (string sMadmin in JobParameters.GlobalParameters.APP_ADMINS)
                    {
                        if (sMadmin.Length > 0)
                        {
                            if (Toolbox.CheckEmailValid(sMadmin))
                            {
                                oMail.Bcc.Add(new MimeKit.MailboxAddress(sMadmin.Split("@")[0], sMadmin));
                            }
                            else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.mb_send_unrecognizedmail, sMadmin), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        }
                    }
                }

                //oMail.BodyEncoding = UTF8Encoding.UTF8;
                //oMail.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;


                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    GetSMTPProxy(MAILVariables, client);
                    switch (MAILVariables.AuthentificationProtocol)
                    {
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.None);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                    }

                    if (MAILVariables.SenderPassword.Length == 0 || MAILVariables.SenderAddress.Length == 0)
                    {
                    }
                    else
                    {
                        //client.AuthenticationMechanisms.Remove("NTLM");
                        //client.AuthenticationMechanisms.Remove("XOAUTH2");
                        client.Authenticate(MAILVariables.SenderAddress, MAILVariables.SenderPassword);
                    }


                    if (!JobParameters.RunInSimulationMode)
                    { client.Send(oMail); }
                    else
                    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.mb_simulation_sendbody, oMail.Body)); }
                }
                bOK = true;
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                //FuzibleQuery.QueryErrors += 1;
            }

            return bOK;

        }

        private string PrepareMailWithTemplate(string sContenuMail)
        {
            //ajout disclaimer Fuzible
            if (File.Exists(JobParameters.MailTargetHTMLFileTemplate))
            {
                try
                {
                    string sContent = File.ReadAllText(JobParameters.MailTargetHTMLFileTemplate);
                    HtmlDocument doc = new();
                    doc.LoadHtml(sContent);
                    if (doc.ParseErrors.Any())
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_parsing_ko + " : " + JobParameters.MailTargetHTMLFileTemplate, SQLTools_Enums.LOG_TYPEINFO.WNG);
                        return sContenuMail;
                    }
                    else
                    {
                        string sHtmlContent = doc.ParsedText;
                        sHtmlContent = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sHtmlContent, JobParameters.DynParams);

                        if (JobParameters.MailTargetHTMLDataSetKeyword.Length > 0)
                        {
                            if (sHtmlContent.Contains(JobParameters.MailTargetHTMLDataSetKeyword))
                            {
                                sHtmlContent = sHtmlContent.Replace(JobParameters.MailTargetHTMLDataSetKeyword, sContenuMail);
                                return sHtmlContent;
                            }
                            else // le mot clé n'existe pas, on met le tableau à la suite
                            { 
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword_not_found + " : " + JobParameters.MailTargetHTMLDataSetKeyword, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                sHtmlContent = string.Concat(sHtmlContent, sContenuMail);
                                return sHtmlContent;
                            }
                        }
                        else // le mot clé n'existe pas, on met le tableau à la suite
                        {
                            //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_empty_keyword, SQLTools_Enums.LOG_TYPEINFO.DET);
                            //on recherche d'abord si il y a dans le contenu sContenuMail des balises (p class) qui aurait un nom qu'on retrouve dans le HTML
                            MatchCollection mcClasses = Regex.Matches(sContenuMail, "(<p class=\")(.+)(\">)");
                            if (mcClasses.Count > 0)
                            {
                                bool bExists = false;

                                foreach (Match mc in mcClasses)
                                {
                                    string sKeyWord = mc.Groups[2].Value;
                                    int iOpen = mc.Groups[3].Index + 2;
                                    string sTableData = sContenuMail[iOpen..];
                                    sTableData = sTableData[..sTableData.IndexOf("</p>")];
                                    sTableData = sTableData.Trim();

                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword_detect_from_query + sKeyWord, SQLTools_Enums.LOG_TYPEINFO.DET);

                                    if (sHtmlContent.Contains(sKeyWord))
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword_but_found, SQLTools_Enums.LOG_TYPEINFO.DET);

                                        sHtmlContent = sHtmlContent.Replace(sKeyWord, sTableData);
                                        bExists = true;
                                    }
                                    else
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword_not_found, SQLTools_Enums.LOG_TYPEINFO.DET);
                                    }
                                }

                                if (bExists)
                                {
                                    return sHtmlContent;
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    sHtmlContent = string.Concat(sHtmlContent, sContenuMail);
                                    return sHtmlContent;
                                }
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_no_keyword, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                sHtmlContent = string.Concat(sHtmlContent, sContenuMail);
                                return sHtmlContent;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, Languages.Languages.mb_htmltemplate_file_ko + " : " + JobParameters.MailTargetHTMLFileTemplate, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    return sContenuMail;
                }
            }
            else
            {
                if (!File.Exists(JobParameters.MailTargetHTMLFileTemplate))
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mb_htmltemplate_file_ko + " : " + JobParameters.MailTargetHTMLFileTemplate, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }
                return sContenuMail;
            }
        }

        public void PerformOpOnMailbox(string sCommandToExecute)
        {
            try
            {

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                //FuzibleQuery.QueryErrors += 1;
            }
        }

        public static string CheckConnection(CONNString CS, List<string> sListVariables)
        {
            string sM = "";
            MAILConnectionVariables MAILVariables = new(CS, 3000, 2048, sListVariables);

            try
            {
                if (MAILVariables.Host_Send.Length > 0)
                {
                    using var client = new MailKit.Net.Smtp.SmtpClient();
                    GetSMTPProxy(MAILVariables, client);

                    client.Timeout = 5000;

                    switch (MAILVariables.AuthentificationProtocol)
                    {
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.None);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                            client.Connect(MAILVariables.Host_Send, MAILVariables.SENDPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                    }

                    if (MAILVariables.SenderPassword.Length == 0 || MAILVariables.SenderAddress.Length == 0)
                    {
                    }
                    else
                    {

                        client.AuthenticationMechanisms.Remove("NTLM");
                        client.AuthenticationMechanisms.Remove("XOAUTH2");
                        client.Authenticate(MAILVariables.SenderAddress, MAILVariables.SenderPassword);
                    }

                    sM = "SMTP : OK";
                }
                else { sM = "SMTP : Untested"; }
            }
            catch (Exception ex)
            { sM = "SMTP : " + ex.Message; }

            try
            {
                if (MAILVariables.Host_Receive.Length > 0)
                {
                    switch (MAILVariables.MailGetProtocol)
                    {
                        case SQLTools_Enums.MAIL_GET_PROTOCOL.POP:
                            using (var client = new MailKit.Net.Pop3.Pop3Client())
                            {
                                GetPOPProxy(MAILVariables, client);
                                try
                                {
                                    client.Timeout = 5000;

                                    switch (MAILVariables.AuthentificationProtocol)
                                    {
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                    }

                                    if (MAILVariables.SenderPassword.Length == 0 || MAILVariables.SenderAddress.Length == 0)
                                    {
                                    }
                                    else
                                    {
                                        client.AuthenticationMechanisms.Remove("NTLM");
                                        client.AuthenticationMechanisms.Remove("XOAUTH2");
                                        client.Authenticate(MAILVariables.SenderAddress, MAILVariables.SenderPassword);
                                    }

                                    client.Disconnect(true);
                                    sM = string.Concat(sM, Environment.NewLine, "POP : OK");
                                }
                                catch (Exception)
                                { throw; }

                            }
                            break;
                        case SQLTools_Enums.MAIL_GET_PROTOCOL.IMAP:
                            using (var client = new MailKit.Net.Imap.ImapClient())
                            {
                                GetIMAPProxy(MAILVariables, client);
                                try
                                {
                                    client.Timeout = 5000;

                                    switch (MAILVariables.AuthentificationProtocol)
                                    {
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.None;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS11:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls11;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS12:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                        case SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.TLS13:
                                            client.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
                                            client.Connect(MAILVariables.Host_Receive, MAILVariables.RECEIVEPort, MAILVariables.IsSSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls);
                                            break;
                                    }

                                    if (MAILVariables.SenderPassword.Length == 0 || MAILVariables.SenderAddress.Length == 0)
                                    {
                                    }
                                    else
                                    {
                                        client.AuthenticationMechanisms.Remove("NTLM");
                                        client.AuthenticationMechanisms.Remove("XOAUTH2");
                                        client.Authenticate(MAILVariables.SenderAddress, MAILVariables.SenderPassword);
                                    }

                                    client.Disconnect(true);
                                    sM = string.Concat(sM, Environment.NewLine, "IMAP : OK");
                                }
                                catch (Exception)
                                { throw; }

                            }
                            break;
                    }
                }
                else { sM = string.Concat(sM, Environment.NewLine, "POP/IMAP : Untested"); }
            }
            catch (Exception ex)
            { sM = string.Concat(sM, Environment.NewLine, "POP/IMAP : ", ex.Message); }

            return sM;
        }

        public static bool CheckMailValid(string sEmail)
        {
            return Regex.IsMatch(sEmail, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }

        public void SendMailLog()
        {
            bool bSend = true;

            if (JobParameters.GlobalParameters.LOG_MAIL_DONTSEND_IFSUCCESS && MyLog.HasNoErrors) //pas de mail si job succès
            { bSend = false; }

            if (bSend)
            {
                string sLog = MyLog.CreateJobHTMLSummary(JobParameters.JobNAME + " - " + JobParameters.JobVersion);
                //copie des fichiers de LOG dans de nouveaux fichiers
                string sPath = MyLog.PathToLog;
                string sF1 = string.Concat(sPath, "JobReport_", JobParameters.JobID, "_", DateTime.Now.ToString("yyyyMMddHHmmss"), ".TXT");
                string sF2 = string.Concat(sPath, "JobReportDebug_", JobParameters.JobID, "_", DateTime.Now.ToString("yyyyMMddHHmmss"), ".TXT");
                string sF3 = string.Concat(sPath, "JobReportQueries_", JobParameters.JobID, "_", DateTime.Now.ToString("yyyyMMddHHmmss"), ".TXT");
                try { File.Copy(MyLog.LOGFilename, sF1); } catch (Exception) { sF1 = ""; }
                try { File.Copy(MyLog.DEBUGFilename, sF2); } catch (Exception) { sF2 = ""; }
                try { File.Copy(MyLog.QUERIESFilename, sF3); } catch (Exception) { sF3 = ""; }


                List<string> sListPJ = new();
                if (sF1.Length > 0) { sListPJ.Add(sF1); }
                if (sF2.Length > 0) { sListPJ.Add(sF2); }
                if (sF3.Length > 0) { sListPJ.Add(sF3); }

                SendMail(string.Concat(Languages.Languages.mb_log_header, JobParameters.JobNAME), sLog, JobParameters.LogMailAdress, null, sListPJ, true);
            }
        }


        #endregion

        #region "PRIVATE VOID"

        private static List<string> GetListStringFromMimeMessage(MimeKit.MimeMessage mBmessage, string sUID)
        {
            List<string> sMContent = new()
            {
                mBmessage.Bcc == null ? "" : mBmessage.Bcc.ToString(),
                mBmessage.Body == null ? "" : mBmessage.Body.ToString(),
                mBmessage.Cc == null ? "" : mBmessage.Cc.ToString(),
                mBmessage.Date.ToString(),
                mBmessage.From == null ? "" : mBmessage.From.ToString(),
                mBmessage.Headers == null ? "" : mBmessage.Headers.ToString(),
                mBmessage.HtmlBody == null ? "" : mBmessage.HtmlBody.ToString(),
                mBmessage.Importance.ToString(),
                mBmessage.InReplyTo == null ? "" : mBmessage.InReplyTo.ToString(),
                mBmessage.MessageId == null ? "" : mBmessage.MessageId.ToString(),
                mBmessage.MimeVersion == null ? "" : mBmessage.MimeVersion.ToString(),
                mBmessage.Priority.ToString(),
                mBmessage.ReplyTo == null ? "" : mBmessage.ReplyTo.ToString(),
                mBmessage.ResentBcc == null ? "" : mBmessage.ResentBcc.ToString(),
                mBmessage.ResentCc == null ? "" : mBmessage.ResentCc.ToString(),
                mBmessage.ResentDate.ToString(),
                mBmessage.ResentFrom == null ? "" : mBmessage.ResentFrom.ToString(),
                mBmessage.ResentMessageId == null ? "" : mBmessage.ResentMessageId.ToString(),
                mBmessage.ResentReplyTo == null ? "" : mBmessage.ResentReplyTo.ToString(),
                mBmessage.ResentSender == null ? "" : mBmessage.ResentSender.ToString(),
                mBmessage.ResentTo == null ? "" : mBmessage.ResentTo.ToString(),
                mBmessage.Sender == null ? "" : mBmessage.Sender.ToString(),
                mBmessage.Subject == null ? "" : mBmessage.Subject.ToString(),
                mBmessage.TextBody == null ? "" : mBmessage.TextBody.ToString(),
                mBmessage.To == null ? "" : mBmessage.To.ToString(),
                sUID
            };

            return sMContent;
        }

        private static string FormatCells(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return (char.ToUpper(s[0]) + s[1..]).Replace("_", " ");
        }

        private static FITools CreateFakeAttachmentTargetFromJob(string sPath, Job INIP, LogTools MyLog)
        {
            FITools FIAttachment = null;

            switch (INIP.MailTargetFormat)
            {
                case SQLTools_Enums.MAIL_TARGET_FORMAT.ATT_CSV:
                    CONNString CSCSV = new(SQLTools_Enums.BDD.FI_CSV, "[0]", "Local CSV", sPath);
                    INIP.ConnectionString_Target = CSCSV;
                    INIP.MaxRowsInAFile = 1000000;
                    INIP.AppendFileCreation = false;
                    INIP.CSVAddHeader = false;
                    //INIP.CSVCharSeparator_EndRow = false;
                    INIP.CSVRowOffset = 0;
                    FIAttachment = new FITools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                    break;
                case SQLTools_Enums.MAIL_TARGET_FORMAT.ATT_CSV_H:
                    CONNString CSCSV_H = new(SQLTools_Enums.BDD.FI_CSV, "[0]", "Local CSV-H", sPath);
                    INIP.ConnectionString_Target = CSCSV_H;
                    INIP.MaxRowsInAFile = 1000000;
                    INIP.AppendFileCreation = false;
                    INIP.CSVAddHeader = true;
                    //INIP.CSVCharSeparator_EndRow = false;
                    INIP.CSVRowOffset = 0;
                    FIAttachment = new FITools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                    break;
                case SQLTools_Enums.MAIL_TARGET_FORMAT.ATT_EXCEL:
                    CONNString CSXLS = new(SQLTools_Enums.BDD.FI_XLS, "[0]", "Local XLS", sPath);
                    INIP.ConnectionString_Target = CSXLS;
                    INIP.XLSRowOffset = 0;
                    INIP.MaxRowsInAFile = 1000000;
                    INIP.AppendFileCreation = false;
                    INIP.XLSAddHeader = false;
                    FIAttachment = new FITools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                    break;
                case SQLTools_Enums.MAIL_TARGET_FORMAT.ATT_EXCEL_H:
                    CONNString CSXLS_H = new(SQLTools_Enums.BDD.FI_XLS, "[0]", "Local XLS-H", sPath);
                    INIP.ConnectionString_Target = CSXLS_H;
                    INIP.XLSRowOffset = 0;
                    INIP.MaxRowsInAFile = 1000000;
                    INIP.AppendFileCreation = false;
                    INIP.XLSAddHeader = true;
                    FIAttachment = new FITools(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                    break;
            }

            return FIAttachment;
        }

        #endregion

        public class MAILConnectionVariables
        {
            private string _sProxyAdress = "";
            private int _iMaxPJSize = 2048;

            public SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL AuthentificationProtocol { get; set; } = SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE;
            public string SenderAddress { get; set; } = "";
            public string SenderPassword { get; set; } = "";
            public string Host_Send { get; set; } = "";
            public string Host_Receive { get; set; } = "";
            public int SENDPort { get; set; } = 0;
            public int RECEIVEPort { get; set; } = 0;
            public bool IsSSL { get; set; } = false;
            public int ServerTimeout { get; set; } = 30000;
            public string ProxyURL
            { get { return _sProxyAdress; } set { _sProxyAdress = value.ToLower(); } }
            public int ProxyPort { get; set; } = 0;
            public ProxyTypes ProxyType { get; set; } = ProxyTypes.None;
            public string ProxyUsername { get; internal set; } = "";
            public string ProxyPassword { get; internal set; } = "";
            public SQLTools_Enums.MAIL_GET_PROTOCOL MailGetProtocol { get; set; } = SQLTools_Enums.MAIL_GET_PROTOCOL.POP;
            public int MaxAttachementSizeInKo { get { return _iMaxPJSize; } set { if (value <= 1 && value >= 16536) { _iMaxPJSize = value; } else { _iMaxPJSize = 2048; } } }

            public MAILConnectionVariables(CONNString CS, int iTimeout, int iMaxPJSize, List<string> sListVariableParameters)
            {
                ServerTimeout = iTimeout;
                MaxAttachementSizeInKo = iMaxPJSize;

                string[] sKeyWords = { "SERVER_SEND", "SERVER_RECEIVE", "USERNAME", "PORT_SEND", "PASSWORD", "PORT_RECEIVE", "SSL", "PROXY", "GET_PROTOCOL", "AUTH_PROTOCOL" };

                string[] sSplitServerSend = { "SERVER_SEND=", "server_send=", "Server_send=", "Server_Send" };
                string[] sSplitServerReceive = { "SERVER_RECEIVE=", "server_receive=", "Server_receive=", "Server_Receive" };

                string[] sSplitUser = { "USERNAME=", "username=", "Username=" };
                string[] sSplitPassword = { "PASSWORD=", "password=", "Password=" };
                string[] sSplitPortSend = { "PORT_SEND=", "port_send=", "Port_send=", "Port_Send" };
                string[] sSplitPortReceive = { "PORT_RECEIVE=", "port_receive=", "Port_receive=", "Port_Receive" };

                string[] sSplitSSL = { "SSL=", "ssl=", "Ssl=" };
                string[] sSplitProxy = { "PROXY=", "proxy=", "Proxy=" };
                string[] sSplitProtocol = { "GET_PROTOCOL=", "get_protocol=", "Get_Protocol=" };
                string[] sSplitAuthProtocol = { "AUTH_PROTOCOL=", "auth_protocol=", "Auth_Protocol=" };

                //découpage de la chaine de connexion du webservice pour intégration des variables
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("server_send=") > -1) { Host_Send = CS.SConnString(sListVariableParameters).Split(sSplitServerSend, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]; }
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("server_receive=") > -1) { Host_Receive = CS.SConnString(sListVariableParameters).Split(sSplitServerReceive, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]; }
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("username=") > -1) { SenderAddress = CS.SConnString(sListVariableParameters).Split(sSplitUser, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]; }
                
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("password=") > -1) 
                {
                    //cas des mots de passe genre huuiuezr;zeruaioz_'
                    
                    var sPwd = CS.SConnString(sListVariableParameters).Split(sSplitPassword, StringSplitOptions.None);
                    if (sPwd.Length > 1)
                    {
                        foreach (string sK in sKeyWords)
                        {
                            if (Regex.IsMatch(sPwd[1], sK + "=", RegexOptions.IgnoreCase))
                            {
                                SenderPassword = sPwd[1][..sPwd[1].IndexOf(sK, StringComparison.OrdinalIgnoreCase)];
                                break;
                            }
                        }
                        if (sPwd[1].EndsWith(";")) { sPwd[1] = sPwd[1][0..^1]; }
                        
                        SenderPassword = sPwd[1];
                    }
                }

                AuthentificationProtocol = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_AUTH_PROTOCOL).Length > 0 ? (SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL)Enum.Parse(typeof(SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_AUTH_PROTOCOL)) : SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE;
                MailGetProtocol = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROTOCOL).Length > 0 ? (SQLTools_Enums.MAIL_GET_PROTOCOL)Enum.Parse(typeof(SQLTools_Enums.MAIL_GET_PROTOCOL), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROTOCOL)) : SQLTools_Enums.MAIL_GET_PROTOCOL.POP;
                ProxyURL = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_URL);
                ProxyPort = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_PORT).Length > 0 ? Convert.ToInt32(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_PORT)) : 0;

                try
                {
                    ProxyType = (ProxyTypes)Enum.Parse(typeof(ProxyTypes), Toolbox.ToUpperFirstLetter(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_TYPE)));
                    ProxyUsername = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_USERNAME);
                    ProxyPassword = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_PASSWORD);
                }
                catch { }

                IsSSL = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_USE_SSL).Length > 0 && Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_USE_SSL));

                //maintien de compatibilité avec la chaîne de LOG mail (qui n'a pas tous les champs de paramétrage des chaînes de connexion classiques)
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROXY_URL).Length == 0 && CS.SConnString(sListVariableParameters).ToLower().IndexOf("proxy=") > -1)
                { ProxyURL = CS.SConnString(sListVariableParameters).Split(sSplitProxy, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0].ToUpper(); }
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_USE_SSL).Length == 0 && CS.SConnString(sListVariableParameters).ToLower().IndexOf("ssl=") > -1)
                { try { IsSSL = Convert.ToBoolean(Convert.ToInt16(CS.SConnString(sListVariableParameters).Split(sSplitSSL, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0])); } catch (Exception) { IsSSL = false; } }
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_PROTOCOL).Length == 0 && CS.SConnString(sListVariableParameters).ToLower().IndexOf("get_protocol=") > -1)
                { try { MailGetProtocol = (SQLTools_Enums.MAIL_GET_PROTOCOL)Enum.Parse(typeof(SQLTools_Enums.MAIL_GET_PROTOCOL), CS.SConnString(sListVariableParameters).Split(sSplitProtocol, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]); } catch (Exception) { MailGetProtocol = SQLTools_Enums.MAIL_GET_PROTOCOL.POP; } }
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.MAIL_AUTH_PROTOCOL).Length == 0 && CS.SConnString(sListVariableParameters).ToLower().IndexOf("auth_protocol=") > -1)
                { try { AuthentificationProtocol = (SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL)Enum.Parse(typeof(SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL), CS.SConnString(sListVariableParameters).Split(sSplitAuthProtocol, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]); } catch (Exception) { AuthentificationProtocol = SQLTools_Enums.MAIL_AUTHENTIFICATION_PROTOCOL.NONE; } }

                //--------------------------

                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("port_send=") > -1) { try { SENDPort = Convert.ToInt16(CS.SConnString(sListVariableParameters).Split(sSplitPortSend, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]); } catch (Exception) { SENDPort = 0; } }
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("port_receive=") > -1) { try { RECEIVEPort = Convert.ToInt16(CS.SConnString(sListVariableParameters).Split(sSplitPortReceive, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]); } catch (Exception) { RECEIVEPort = 0; } }
            }
        }
    }
}
