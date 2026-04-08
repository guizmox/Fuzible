using MihaZupan;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace FuzibleFramework
{
    public class FTPTools
    {
        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose { get; }
        public Job JobParameters;
        public LogTools MyLog;
        internal readonly FTPConnectionVariables FTPVariables;

        public static readonly string WORKING_DIRECTORY_GET = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "\\FTPSHARE\\GET\\");
        public static readonly string WORKING_DIRECTORY_SET = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "\\FTPSHARE\\SET\\");

        #endregion

        #region "PROPRIETES"

        #endregion

        #region "PUBLIC VOID"

        public FTPTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            if (!Directory.Exists(WORKING_DIRECTORY_GET))
            { Directory.CreateDirectory(WORKING_DIRECTORY_GET); }

            if (!Directory.Exists(WORKING_DIRECTORY_SET))
            { Directory.CreateDirectory(WORKING_DIRECTORY_SET); }

            JobParameters = INIP;
            MyLog = LogJob;
            ClassPurpose = sSourceTargetLog;

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.LOG:
                    CONNString CSLog = new(INIP.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", INIP.GlobalParameters.LOG_CONNECTIONSTRING);
                    FTPVariables = new FTPConnectionVariables(CSLog, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.SRC:
                    FTPVariables = new FTPConnectionVariables(INIP.ConnectionString_Source, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG:
                    FTPVariables = new FTPConnectionVariables(INIP.ConnectionString_Target, INIP.DynParams);
                    break;
            }

        }

        public void SendFileInFTPOrSFTPPath(string sPathAndFilename, Query FuzibleQuery)
        {

            if (FTPVariables.Is_SFTP)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ftp_uploadonsftp, FTPVariables.FTPURL, ") : ", Path.GetFileName(sPathAndFilename)), SQLTools_Enums.LOG_TYPEINFO.INF);
                SendFileInSFTPPath(sPathAndFilename, FuzibleQuery);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ftp_uploadonftp, FTPVariables.FTPURL, ") : ", Path.GetFileName(sPathAndFilename)), SQLTools_Enums.LOG_TYPEINFO.INF);
                SendFileInFTPPath(sPathAndFilename, FuzibleQuery);
            }

            CleanSFTPLocalPath(false);

        }

        public static List<string[]> GetListOfFileFromFTPOrSFTPPath(CONNString CS, string sFilter, List<string> sListVariables, bool bWithSubDirs)
        {
            FTPConnectionVariables FTPVariables = new(CS, sListVariables);
            List<string[]> sListFiles = new();

            if (FTPVariables.Is_SFTP)
            {
                ConnectionInfo ConnNfo = null;
                bool bOk = true;

                if (FTPVariables.SFTPUseAuthentificationByKeyFile && File.Exists(FTPVariables.SFTPSSHKeyFile))
                {
                    PrivateKeyAuthenticationMethod privKeyAuthMethod;
                    PrivateKeyFile pvKeyFile = null;
                    try { pvKeyFile = new PrivateKeyFile(FTPVariables.SFTPSSHKeyFile); } catch { bOk = false; }
                    if (!bOk)
                    { try { pvKeyFile = new PrivateKeyFile(FTPVariables.SFTPSSHKeyFile, FTPVariables.FTPPassword); } catch { bOk = false; } }
                    if (bOk)
                    {
                        privKeyAuthMethod = new PrivateKeyAuthenticationMethod(FTPVariables.FTPUsername, new PrivateKeyFile[] { pvKeyFile });

                        if (FTPVariables.FTPProxyType == ProxyTypes.None)
                        {
                            ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername, new AuthenticationMethod[] { privKeyAuthMethod });
                        }
                        else
                        {
                            ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername,
                                                    FTPVariables.FTPProxyType, FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword,
                                                    new AuthenticationMethod[] { privKeyAuthMethod });
                        }
                    }
                }
                else
                {
                    try
                    {
                        PasswordAuthenticationMethod pwdAuthMethod;
                        pwdAuthMethod = new PasswordAuthenticationMethod(FTPVariables.FTPUsername, FTPVariables.FTPPassword);

                        if (FTPVariables.FTPProxyType == ProxyTypes.None)
                        {
                            ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername, new AuthenticationMethod[] { pwdAuthMethod });
                        }
                        else
                        {
                            ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername,
                                                  FTPVariables.FTPProxyType, FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword,
                                                  new AuthenticationMethod[] { pwdAuthMethod });
                        }
                    }
                    catch { bOk = false; }
                }

                if (bOk)
                {
                    SftpClient sftpClient = new(ConnNfo);

                    try
                    {
                        sftpClient.Connect();
                        var fileList = sftpClient.ListDirectory(FTPVariables.FTPRemotePath).ToList();

                        foreach (SftpFile sFile in fileList)
                        {
                            if (sFile.IsRegularFile)
                            {
                                string sAddPath = sFile.FullName.Replace(FTPVariables.FTPRemotePath + "/", "");
                                if (sAddPath.IndexOf("/") == -1) { sAddPath = ""; }
                                else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("/") + 1)]; }

                                if (sFilter.Length > 0)
                                {
                                    if (Regex.IsMatch(sFile.Name, "^" + sFilter, RegexOptions.IgnoreCase))
                                    { 

                                        sListFiles.Add(new string[] { sFile.FullName.StartsWith(FTPVariables.FTPRemotePath, StringComparison.OrdinalIgnoreCase) ? sFile.Name : sFile.FullName, sAddPath });
                                    }
                                }
                                else
                                {
                                    sListFiles.Add(new string[] { sFile.FullName.StartsWith(FTPVariables.FTPRemotePath, StringComparison.OrdinalIgnoreCase) ? sFile.Name : sFile.FullName, sAddPath });
                                }
                            }
                        }

                        if (bWithSubDirs)
                        {
                            try
                            {
                                foreach (SftpFile sFile in fileList)
                                {
                                    if (sFile.IsDirectory && sFile.Name.Length >= 3)
                                    { sListFiles.AddRange(GetFilesInSFTPSubDir(sftpClient, sFile.FullName, sFilter, FTPVariables)); }
                                }
                            }
                            catch { }
                        }

                        sftpClient.Disconnect();
                    }
                    catch (Exception)
                    {
                        if (sftpClient != null && sftpClient.IsConnected) { sftpClient.Disconnect(); }
                        throw;
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.sftp_sshkeyfile_invalid + " (" + FTPVariables.SFTPSSHKeyFile + ")");
                    throw ex;
                }
            }
            else
            {
                try
                {
                    var ftpRequest = (FtpWebRequest)WebRequest.Create(FTPVariables.FTPURL + FTPVariables.FTPRemotePath);

                    switch (FTPVariables.FTPProxyType)
                    {
                        case ProxyTypes.None:
                            ftpRequest.Proxy = null;
                            break;
                        case ProxyTypes.Http:
                            ftpRequest.Proxy = new WebProxy(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort);
                            break;
                        case ProxyTypes.Socks4:
                            throw new Exception("SOCKS4 Unsupported !");
                        case ProxyTypes.Socks5:
                            if (FTPVariables.FTPProxyUsername.Length > 0)
                            { ftpRequest.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword) }); }
                            else { ftpRequest.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort) }); }
                            break;
                    }

                    ftpRequest.EnableSsl = FTPVariables.FTPSsl;
                    ftpRequest.Timeout = FTPVariables.FTPTimeout;
                    if (FTPVariables.FTPSsl)
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    }
                    if (FTPVariables.FTPIgnoreTLSErrors)
                    {
                        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
                    }

                    //ftpRequest.KeepAlive = true;
                    ftpRequest.Credentials = new NetworkCredential(FTPVariables.FTPUsername, FTPVariables.FTPPassword);
                    ftpRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                    var response = (FtpWebResponse)ftpRequest.GetResponse();
                    StreamReader streamReader = new(response.GetResponseStream());

                    bool bIsHTML = false;
                    string sRow = streamReader.ReadLine();

                    if (sRow != null)
                    {
                        if (sRow.StartsWith("<!DOCTYPE"))
                        { bIsHTML = true; }
                        while (sRow != null && !string.IsNullOrEmpty(sRow))
                        {
                            if (bIsHTML && sRow.StartsWith("<A HREF=", StringComparison.OrdinalIgnoreCase))
                            {
                                string sAddPath = sRow[9..].Split(Convert.ToChar(">"))[0].Replace("\"", "").Replace(FTPVariables.FTPRemotePath + "/", "");
                                if (sAddPath.IndexOf("/") == -1) { sAddPath = ""; }
                                else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("/") + 1)]; }

                                if (sFilter.Length > 0)
                                {
                                    if (Regex.IsMatch(sRow[9..].Split(Convert.ToChar(">"))[0].Replace("\"", ""), "^" + sFilter, RegexOptions.IgnoreCase))
                                    { sListFiles.Add(new string[] { sRow[9..].Split(Convert.ToChar(">"))[0].Replace("\"", ""), sAddPath }); }
                                }
                                else { sListFiles.Add(new string[] { sRow[9..].Split(Convert.ToChar(">"))[0].Replace("\"", ""), sAddPath }); }
                            }
                            else if (!bIsHTML && sRow.LastIndexOf(".") < sRow.Length - 5) //c'est peut-être un répertoire
                            {
                                if (bWithSubDirs)
                                {
                                    try
                                    { sListFiles.AddRange(GetFilesInFTPSubDir(ftpRequest, sRow, sFilter, FTPVariables)); }
                                    catch { }
                                }
                            }
                            sRow = streamReader.ReadLine();
                        }
                    }
                    streamReader.Close();
                }
                catch (Exception)
                { throw; }
            }

            return sListFiles;

        }

        private static List<string[]> GetFilesInSFTPSubDir(SftpClient sftpClient, string sPath, string sFilter, FTPConnectionVariables ftpVariables)
        {
            List<string[]> sListFiles = new();

            try
            {
                var subList = sftpClient.ListDirectory(sPath).ToList();

                foreach (SftpFile sFile in subList)
                {
                    if (sFile.IsRegularFile)
                    {
                        if (sFilter.Length > 0)
                        {
                            if (Regex.IsMatch(sFile.Name, sFilter, RegexOptions.IgnoreCase))
                            {
                                string sAddPath = sFile.FullName.Replace(ftpVariables.FTPRemotePath + "/", "");
                                if (sAddPath.IndexOf("/") == -1) { sAddPath = ""; }
                                else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("/") + 1)]; }
                                sListFiles.Add(new string[] { sFile.FullName[(ftpVariables.FTPRemotePath.Length + 1)..].Replace("/", "\\"), sAddPath }); 
                            }
                        }
                        else
                        {
                            sListFiles.Add(new string[] { sFile.FullName[(ftpVariables.FTPRemotePath.Length + 1)..].Replace("/", "\\"), "" });
                        }
                    }
                    else if (sFile.IsDirectory && sFile.Name.Length >= 3)
                    { sListFiles.AddRange(GetFilesInSFTPSubDir(sftpClient, sFile.FullName, sFilter, ftpVariables)); }
                }
            }
            catch (Exception) { throw; }

            return sListFiles;
        }

        private static List<string[]> GetFilesInFTPSubDir(FtpWebRequest ftpMain, string sPath, string sFilter, FTPConnectionVariables ftpVariables)
        {
            List<string[]> sListFiles = new();

            try
            {
                var ftpRequest = (FtpWebRequest)WebRequest.Create(ftpMain.RequestUri.OriginalString + "/" + sPath);
                ftpRequest.Proxy = ftpMain.Proxy;
                ftpRequest.EnableSsl = ftpMain.EnableSsl;
                ftpRequest.Timeout = ftpMain.Timeout;
                //ftpRequest.KeepAlive = true;
                ftpRequest.Credentials = ftpMain.Credentials;
                ftpRequest.Method = ftpMain.Method;
                var response = (FtpWebResponse)ftpRequest.GetResponse();
                StreamReader streamReader = new(response.GetResponseStream());

                string sRow = streamReader.ReadLine();

                if (sRow != null)
                {
                    while (sRow != null && !string.IsNullOrEmpty(sRow))
                    {
                        string sAddPath = sRow.Replace(ftpVariables.FTPRemotePath, "");
                        if (sAddPath.IndexOf("/") == -1) { sAddPath = ""; }
                        else { sAddPath = sAddPath[0..(sAddPath.LastIndexOf("/") + 1)]; }

                        if (sRow.LastIndexOf(".") < sRow.Length - 5) //c'est un répertoire ou un fichier : le truc est bien foireux
                        {
                          
                            sListFiles.AddRange(GetFilesInFTPSubDir(ftpRequest, sRow[(sRow.LastIndexOf("/") + 1)..].Replace("/", "\\"), sFilter, ftpVariables));
                        }
                        else 
                        {
                            string sFile = (ftpMain.RequestUri.LocalPath.Equals("/") ? "" : ftpMain.RequestUri.LocalPath) + (sRow.StartsWith("/") ? "" : "/");
                            sListFiles.Add(new string[] { sFile + sRow, sAddPath.Replace("/", "\\") }); 
                        }
                        sRow = streamReader.ReadLine();
                    }
                }
                streamReader.Close();
            }
            catch (Exception) { throw; }

            return sListFiles;
        }

        #endregion

        #region "PRIVATE VOID"

        internal List<string[]> ListFilesFromFTPOrSMTPPath(bool bWithSubDirs)
        {
            return GetListOfFileFromFTPOrSFTPPath(ClassPurpose == SQLTools_Enums.CLASS_PURPOSE.TRG ? JobParameters.ConnectionString_Target : JobParameters.ConnectionString_Source, "", JobParameters.DynParams, bWithSubDirs);
        }
        private FtpWebRequest GetFTPProxy(FtpWebRequest ftpRequest)
        {
            switch (FTPVariables.FTPProxyType)
            {
                case ProxyTypes.None:
                    ftpRequest.Proxy = null;
                    break;
                case ProxyTypes.Http:
                    ftpRequest.Proxy = new WebProxy(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort);
                    break;
                case ProxyTypes.Socks4:
                    throw new Exception("SOCKS4 Unsupported !");
                case ProxyTypes.Socks5:
                    if (FTPVariables.FTPProxyUsername.Length > 0)
                    { ftpRequest.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword) }); }
                    else { ftpRequest.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort) }); }
                    break;
            }

            ftpRequest.EnableSsl = FTPVariables.FTPSsl;
            ftpRequest.Timeout = FTPVariables.FTPTimeout;
            //ftpRequest.KeepAlive = true;
            ftpRequest.Credentials = new NetworkCredential(FTPVariables.FTPUsername, FTPVariables.FTPPassword);

            return ftpRequest;
        }

        private ConnectionInfo GetConnInfo()
        {
            // Setup Credentials and Server Information
            ConnectionInfo ConnNfo = null;
            bool bOk = true;

            if (FTPVariables.SFTPUseAuthentificationByKeyFile && File.Exists(FTPVariables.SFTPSSHKeyFile))
            {
                PrivateKeyAuthenticationMethod privKeyAuthMethod;
                PrivateKeyFile pvKeyFile = null;
                try { pvKeyFile = new PrivateKeyFile(FTPVariables.SFTPSSHKeyFile); } catch { bOk = false; }
                if (!bOk)
                { try { pvKeyFile = new PrivateKeyFile(FTPVariables.SFTPSSHKeyFile, FTPVariables.FTPPassword); } catch { bOk = false; } }
                if (bOk)
                {
                    privKeyAuthMethod = new PrivateKeyAuthenticationMethod(FTPVariables.FTPUsername, new PrivateKeyFile[] { pvKeyFile });
                    if (FTPVariables.FTPProxyType == ProxyTypes.None)
                    {
                        ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername, new AuthenticationMethod[] { privKeyAuthMethod });
                    }
                    else
                    {
                        ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername,
                                                        FTPVariables.FTPProxyType, FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword,
                                                        new AuthenticationMethod[] { privKeyAuthMethod });
                    }
                }
            }
            else
            {
                try
                {
                    PasswordAuthenticationMethod pwdAuthMethod;
                    pwdAuthMethod = new PasswordAuthenticationMethod(FTPVariables.FTPUsername, FTPVariables.FTPPassword);

                    if (FTPVariables.FTPProxyType == ProxyTypes.None)
                    {
                        ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername, new AuthenticationMethod[] { pwdAuthMethod });
                    }
                    else
                    {
                        ConnNfo = new ConnectionInfo(FTPVariables.FTPURL, FTPVariables.FTPPort, FTPVariables.FTPUsername,
                                                        FTPVariables.FTPProxyType, FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort, FTPVariables.FTPProxyUsername, FTPVariables.FTPProxyPassword,
                                                        new AuthenticationMethod[] { pwdAuthMethod });
                    }
                }
                catch { bOk = false; }
            }

            return ConnNfo;
        }

        private void SendFileInFTPPath(string sPathAndFilename, Query FuzibleQuery)
        {
            FtpWebRequest ftpRequest;

            try
            {
                string absoluteFileName = Path.GetFileName(sPathAndFilename);

                ftpRequest = WebRequest.Create(new Uri(string.Format(FTPVariables.FTPURL + FTPVariables.FTPRemotePath + "//" + absoluteFileName))) as FtpWebRequest;
                ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
                if (FTPVariables.FTPProxyURL.Equals(""))
                { ftpRequest.Proxy = null; }
                else
                { ftpRequest.Proxy = new WebProxy(FTPVariables.FTPProxyURL, FTPVariables.FTPProxyPort); }
                ftpRequest.EnableSsl = FTPVariables.FTPSsl;
                ftpRequest.Timeout = FTPVariables.FTPTimeout;
                ftpRequest.KeepAlive = true;
                if (FTPVariables.FTPSsl)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }
                if (FTPVariables.FTPIgnoreTLSErrors)
                {
                    ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
                }
                //ftpRequest.KeepAlive = true;
                ftpRequest.Credentials = new NetworkCredential(FTPVariables.FTPUsername, FTPVariables.FTPPassword);

                try
                {
                    using FileStream fs = File.OpenRead(sPathAndFilename);
                    System.Byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                    fs.Close();
                    Stream requestStream = ftpRequest.GetRequestStream();
                    requestStream.Write(buffer, 0, buffer.Length);
                    requestStream.Flush();
                    requestStream.Close();
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, FTPVariables.FTPRemotePath, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
        }

        private void SendFileInSFTPPath(string sPathAndFilename, Query FuzibleQuery)
        {

            try
            {
                ConnectionInfo ConnNfo = GetConnInfo();

                using var sftp = new SftpClient(ConnNfo);
                try
                {
                    sftp.Connect();

                    sftp.ChangeDirectory(string.Concat(FTPVariables.FTPRemotePath.StartsWith("/") ? "" : "/", FTPVariables.FTPRemotePath, FTPVariables.FTPRemotePath.EndsWith("/") ? "" : "//"));
                    using (FileStream uplfileStream = System.IO.File.OpenRead(sPathAndFilename))
                    {
                        sftp.UploadFile(uplfileStream, Path.GetFileName(sPathAndFilename), true);
                    }
                    sftp.Disconnect();
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, FTPVariables.FTPRemotePath, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
        }

        private string GetFileFromFTPPath(string sFile, Query FuzibleQuery)
        {

            string sFinalFile = "";

            FtpWebRequest ftpRequest;
            try
            {
                // Get the object used to communicate with the server.  
                ftpRequest = WebRequest.Create(new Uri(string.Format(FTPVariables.FTPURL + FTPVariables.FTPRemotePath + "//" + sFile))) as FtpWebRequest;
                ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                ftpRequest = GetFTPProxy(ftpRequest);
                try
                {
                    var response = (FtpWebResponse)ftpRequest.GetResponse();

                    Stream responseStream = response.GetResponseStream();
                    StreamReader reader = new(responseStream);

                    StreamWriter sw = new(WORKING_DIRECTORY_GET + sFile, false, reader.CurrentEncoding);
                    sw.Write(reader.ReadToEnd());
                    sFinalFile = WORKING_DIRECTORY_GET + sFile;
                    sw.Close();

                    LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null,string.Concat(Languages.Languages.ftp_download_ftpok, response.StatusDescription), SQLTools_Enums.LOG_TYPEINFO.DET, true);

                    reader.Close();
                    response.Close();
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sFinalFile;

        }

        private string GetFileFromSFTPPath(string sFile, Query FuzibleQuery)
        {

            string sFinalFile = "";

            try
            {

                ConnectionInfo ConnNfo = GetConnInfo();

                // Download A File
                using var sftp = new SftpClient(ConnNfo);
                try
                {
                    sftp.Connect();
                    sftp.ChangeDirectory(string.Concat("/", FTPVariables.FTPRemotePath, "//"));
                    sFinalFile = WORKING_DIRECTORY_GET + sFile;
                    using (Stream fileStream = File.Create(WORKING_DIRECTORY_GET + sFile))
                    { sftp.DownloadFile(sFile, fileStream); }
                    sftp.Disconnect();
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sFinalFile;

        }

        public string GetFileFromFTPOrSFTPPath(string sFilename, Query FuzibleQuery)
        {
            string sFinalFile;

            if (FTPVariables.Is_SFTP)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ftp_downloadfromsftp, FTPVariables.FTPURL, ") : ", sFilename), SQLTools_Enums.LOG_TYPEINFO.INF);
                sFinalFile = GetFileFromSFTPPath(sFilename, FuzibleQuery);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ftp_downloadfromftp, FTPVariables.FTPURL, ") : ", sFilename), SQLTools_Enums.LOG_TYPEINFO.INF);
                sFinalFile = GetFileFromFTPPath(sFilename, FuzibleQuery);
            }

            CleanSFTPLocalPath(true);

            return sFinalFile;

        }

        private void CleanSFTPLocalPath(bool bSet)
        {
            string sPath = bSet ? WORKING_DIRECTORY_SET : WORKING_DIRECTORY_GET;

            if (Directory.Exists(sPath))
            {
                FileInfo fiI;

                foreach (string sFile in Directory.GetFiles(sPath))
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

        public void MoveFileFromFTPOrSFTPPath(string sFilename, Query FuzibleQuery)
        {
            string sStatus = "";

            if (FTPVariables.Is_SFTP)
            {
                try
                {
                    ConnectionInfo ConnNfo = GetConnInfo();
                    using var sftp = new SftpClient(ConnNfo);
                    try
                    {
                        sftp.Connect();
                        sftp.ChangeDirectory(string.Concat("/", FTPVariables.FTPRemotePath, "//"));

                        string sProcessedDir = string.Concat("/", FTPVariables.FTPRemotePath, "//Processed//");

                        try
                        {
                            sftp.CreateDirectory(sProcessedDir);
                            sStatus = Languages.Languages.ftp_sftpcreatedirok;
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ftp_sftpunabletocreatepath + sProcessedDir, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                        try
                        {
                            if (JobParameters.GlobalParameters.FILE_ADD_DATETIME_PREFIX)
                            {
                                sftp.RenameFile(string.Concat("/", FTPVariables.FTPRemotePath, "//", sFilename), string.Concat("/", FTPVariables.FTPRemotePath, "//Processed//", DateTime.Now.ToString("yyyyMMdd"), "_", sFilename));
                                sStatus = Languages.Languages.ftp_sftpmovefileok;
                            }
                            else
                            {
                                sftp.RenameFile(string.Concat("/", FTPVariables.FTPRemotePath, "//", sFilename), string.Concat("/", FTPVariables.FTPRemotePath, "//Processed//", sFilename));
                                sStatus = Languages.Languages.ftp_sftpmovefileok;
                            }
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ftp_sftpunabletomovefile + sFilename, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                        //try
                        //{
                        //    sftp.Delete(string.Concat("/", FTPVariables.FTPRemotePath, "//", sFilename));
                        //    sStatus = "SFTP DELETE : Success";
                        //}
                        //catch (Exception ex)
                        //{ MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "Unable to delete file on distant server : " + sFilename, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                        sftp.Disconnect();
                    }
                    catch (Exception ex)
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }

                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
            else
            {
                try
                {
                    FtpWebRequest ftpRequest;
                    ftpRequest = WebRequest.Create(new Uri(string.Format(FTPVariables.FTPURL + FTPVariables.FTPRemotePath + "//Processed//"))) as FtpWebRequest;
                    ftpRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
                    ftpRequest = GetFTPProxy(ftpRequest);
                    //ftpRequest.KeepAlive = true;

                    try
                    {
                        //création répertoire
                        using (var response = (FtpWebResponse)ftpRequest.GetResponse())
                        { sStatus = Languages.Languages.ftp_ftpcreatedirok + response.StatusDescription; }

                        //déplace fichier
                        ftpRequest = WebRequest.Create(new Uri(string.Format(FTPVariables.FTPRemotePath + sFilename))) as FtpWebRequest;
                        ftpRequest.Method = WebRequestMethods.Ftp.Rename;
                        if (JobParameters.GlobalParameters.FILE_ADD_DATETIME_PREFIX)
                        { ftpRequest.RenameTo = string.Concat(FTPVariables.FTPRemotePath + "//Processed//", DateTime.Now.ToString("yyyyMMdd"), "_", sFilename); }
                        else { ftpRequest.RenameTo = string.Concat(FTPVariables.FTPRemotePath + "//Processed//", sFilename); }
                        ftpRequest = GetFTPProxy(ftpRequest);

                        using (var response = (FtpWebResponse)ftpRequest.GetResponse())
                        { sStatus = sStatus + Languages.Languages.ftp_ftpmovefileok + response.StatusDescription; }
                    }
                    catch (Exception ex)
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, FTPVariables.FTPRemotePath, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sStatus, SQLTools_Enums.LOG_TYPEINFO.INF);
        }

        public void DeleteFileFromFTPOrSFTPPath(string sFilename, Query FuzibleQuery)
        {
            string sStatus = "";

            if (FTPVariables.Is_SFTP)
            {
                try
                {
                    ConnectionInfo ConnNfo = GetConnInfo();
                    using var sftp = new SftpClient(ConnNfo);
                    try
                    {
                        sftp.Connect();
                        sftp.ChangeDirectory(string.Concat("/", FTPVariables.FTPRemotePath, "//"));
                        try
                        {
                            sftp.Delete(string.Concat("/", FTPVariables.FTPRemotePath, "//", sFilename));
                            sStatus = Languages.Languages.ftp_sftpdeleteok;
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ftp_sftpdeleteko + sFilename, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                        sftp.Disconnect();
                    }
                    catch (Exception ex)
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }

                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
            else
            {
                try
                {
                    FtpWebRequest ftpRequest;
                    ftpRequest = WebRequest.Create(new Uri(string.Format(FTPVariables.FTPURL + FTPVariables.FTPRemotePath + "//" + sFilename))) as FtpWebRequest;
                    ftpRequest.Method = WebRequestMethods.Ftp.DeleteFile;
                    ftpRequest = GetFTPProxy(ftpRequest);

                    try
                    {
                        using var response = (FtpWebResponse)ftpRequest.GetResponse();
                        sStatus = Languages.Languages.ftp_ftpdeletestatus + response.StatusDescription;
                    }
                    catch (Exception ex)
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, FTPVariables.FTPRemotePath, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sStatus, SQLTools_Enums.LOG_TYPEINFO.INF);
        }

        public void ZipFileFromFTPOrSFTPPath(string sFilename, Query FuzibleQuery)
        {
            if (FTPVariables.Is_SFTP)
            {

            }
            else
            {

            }
        }

        #endregion

        public class FTPConnectionVariables
        {
            private string _sRemotePath = "";
            private string _sURL = "";
            private string _sProxyAdress = "";

            public string FTPUsername { get; set; } = "";
            public string FTPPassword { get; set; } = "";
            public string FTPURL
            {
                get { return (Is_SFTP ? "" : "ftp://") + _sURL; }
                set { _sURL = value.ToLower().Replace("sftp://", "").Replace("ftp://", ""); }
            }
            public string SFTPSSHKeyFile { get; set; } = "";
            public string FTPRemotePath
            {
                get { return _sRemotePath; }
                set
                {
                    //if (value.StartsWith("/")) désactivé en juin 2019
                    //{ value = value.Substring(1); }
                    if (!value.StartsWith("/"))
                    { value = string.Concat("/", value); }
                    if (value.EndsWith("/"))
                    { value = value[0..^1]; }
                    _sRemotePath = value;
                }
            }
            public int FTPPort { get; set; } = 0;
            public bool FTPSsl { get; set; } = false;
            public int FTPTimeout { get; set; } = 30000;
            public string FTPProxyURL
            {
                get { return _sProxyAdress; }
                set { _sProxyAdress = value.ToLower(); }
            }
            public int FTPProxyPort { get; set; } = 0;
            public bool Is_SFTP { get; } = false;
            public bool SFTPUseAuthentificationByKeyFile { get { if (SFTPSSHKeyFile.Length > 0) { return true; } else { return false; } } }
            public ProxyTypes FTPProxyType { get; set; } = ProxyTypes.None;
            public string FTPProxyUsername { get; internal set; }
            public string FTPProxyPassword { get; internal set; }
            public bool FTPIgnoreTLSErrors { get; set; } = false;

            public FTPConnectionVariables(CONNString CS, List<string> sListVariableParameters)
            {
                Is_SFTP = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM) != "FTP";

                FTPUsername = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_USERNAME), sListVariableParameters);
                FTPPassword = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PASSWORD), sListVariableParameters);
                
                FTPURL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.SConnString(sListVariableParameters), sListVariableParameters);
                FTPPort = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT).Length > 0 ? Convert.ToInt32(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PORT)) : Is_SFTP ? 22 : 21;
                FTPSsl = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL).Length > 0 && Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_SSL));
                FTPRemotePath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PATH), sListVariableParameters);
                if (Is_SFTP && FTPRemotePath.Length > 0 && !FTPRemotePath.StartsWith("/")) { FTPRemotePath = string.Concat("/" + FTPRemotePath); }
                SFTPSSHKeyFile = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_SFTP_SSH_KEY_PATH);

                FTPIgnoreTLSErrors = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS).Length > 0 && Convert.ToBoolean(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_IGNORE_TLS_ERRORS));

                FTPProxyURL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_URL), sListVariableParameters);
                FTPProxyPort = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT).Length > 0 ? Convert.ToInt32(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_PROXY_PORT)) : 0;

                try
                {
                    FTPProxyType = (ProxyTypes)Enum.Parse(typeof(ProxyTypes), Toolbox.ToUpperFirstLetter(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_TYPE)));
                    FTPProxyUsername = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_USERNAME);
                    FTPProxyPassword = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_FTP_PROXY_PASSWORD);
                }
                catch { }
            }

            internal void AlterDistantPath(string sAdditionnalPath)
            {
                string sDistantPath = FTPRemotePath;
                if (sAdditionnalPath.StartsWith("\\")) { sAdditionnalPath = sAdditionnalPath[1..]; }
                if (!sAdditionnalPath.EndsWith("\\")) { sAdditionnalPath = string.Concat(sAdditionnalPath, "\\"); }
                if (sAdditionnalPath.StartsWith("/")) { sAdditionnalPath = sAdditionnalPath[1..]; }
                if (!sAdditionnalPath.EndsWith("/")) { sAdditionnalPath = string.Concat(sAdditionnalPath, "/"); }
                if (sDistantPath.EndsWith("\\")) { sDistantPath = string.Concat(sDistantPath, sAdditionnalPath); }
                if (sDistantPath.EndsWith("/")) { sDistantPath = string.Concat(sDistantPath, sAdditionnalPath); }
                if ((!sDistantPath.EndsWith("/")) && (!sDistantPath.EndsWith("\\"))) { sDistantPath = string.Concat(sDistantPath, "/", sAdditionnalPath); }
                sDistantPath = sDistantPath.Replace("\\", "/");
                FTPRemotePath = sDistantPath;
            }

            public void RewritePath(string sFilename)
            {
                string sAdditionnalPath = "";
                string sModifiedWorkingDirectory = "";

                if (sFilename.LastIndexOf("\\") > 0) //on peut avoir crée un output qui contient un subdir (ex : \OUTPUT\coucou.csv:select...
                {
                    if (sFilename.StartsWith("\\")) { sFilename = sFilename[1..]; }
                    sAdditionnalPath = sFilename[..sFilename.LastIndexOf("\\")];
                    sModifiedWorkingDirectory = string.Concat(sModifiedWorkingDirectory, sAdditionnalPath);
                    sFilename = sFilename[(sFilename.LastIndexOf("\\") + 1)..];
                    if (sAdditionnalPath.Length > 0) { AlterDistantPath(sAdditionnalPath); }
                }
                else if (sFilename.LastIndexOf("/") > 0) //on peut avoir crée un output qui contient un subdir (ex : /OUTPUT/coucou.csv:select...
                {
                    if (sFilename.StartsWith("/")) { sFilename = sFilename[1..]; }
                    sAdditionnalPath = sFilename[..sFilename.LastIndexOf("/")];
                    sModifiedWorkingDirectory = string.Concat(sModifiedWorkingDirectory, sAdditionnalPath);
                    sFilename = sFilename[(sFilename.LastIndexOf("/") + 1)..];
                    if (sAdditionnalPath.Length > 0) { AlterDistantPath(sAdditionnalPath); }
                }
            }
        }
    }
}
