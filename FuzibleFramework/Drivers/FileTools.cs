using DataConvert;
using ExcelDataReader;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Ude;

namespace FuzibleFramework
{

    public class FITools
    {
        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose
        {
            get;
        }
        public Job JobParameters;
        public LogTools MyLog;
        public CONNString Connection;

        private static List<string> CACHED_NETWORK = new();
        #endregion

        #region "PROPRIETES"

        public string Path_Processed_Files { get; set; } = "Processed\\";
        public string Path_Exported_Files { get; set; } = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\EXPORT\\";
        public bool IsWorkingDirectoryAnFTPURL { get; } = false;
        public bool IsWorkingDirectoryAnSFTPURL { get; } = false;

        #endregion

        #region "STATIC"

        public static Encoding DetectFileEncoding(string file)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            byte[] bom = new byte[4];
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Read(bom, 0, 4);
            }

            // BOM fiables
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;

            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;

            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                return Encoding.UTF32;

            // Test UTF-8 STRICT
            try
            {
                using var sr = new StreamReader(
                    file,
                    new UTF8Encoding(false, true), // throwOnInvalidBytes = true
                    false
                );
                sr.Read(); // force la lecture
                return Encoding.UTF8;
            }
            catch
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var enc1252 = Encoding.GetEncoding(1252);

                return enc1252; // ANSI réel
            }
        }

        public static List<string> ExtractFileNameAndPathFromFullPath(string sFile)
        {

            //position 0 = nom fichier
            //position 1 = répertoire
            //position 2 = extension (contient le ".")

            List<string> sData = new();
            string sNomFichier;
            string[] sNomFichierSplit;

            try
            {
                if (File.Exists(sFile))
                {
                    sNomFichier = Path.GetFileNameWithoutExtension(sFile);
                    sData.Add(sNomFichier);
                    sData.Add(sFile.Replace(Path.GetFileName(sFile), ""));
                    sData.Add(Path.GetExtension(sFile));
                }
                else
                {
                    string[] sFichierSplite = sFile.Split(Convert.ToChar("\\"));
                    sNomFichier = Path.GetFileNameWithoutExtension(sFichierSplite[^1]);
                    sData.Add(sNomFichier);
                    sData.Add(sFile.Replace(Path.GetFileName(sFile), ""));
                    try
                    {
                        if (sFile.IndexOf(".") > -1)
                        {
                            sNomFichierSplit = sFile.Split(Convert.ToChar("."));
                            sData.Add("." + sNomFichierSplit[^1]);
                        }
                        else
                        {
                            sData.Add("");
                        }

                    }
                    catch (Exception)
                    {
                        sData.Add(".001");
                    }

                }

            }
            catch (Exception)
            {
                throw;
            }

            return sData;

        }

        public static StreamWriter CreateFile(string sFilename, int numThread, int cptFile, string sListHeaders, StreamWriter ioFileToClose, string sAdditionalInfo = "", bool bWithoutOldInfo = true)
        {

            cptFile += 1;
            //pour éviter l'extention .0000, je préfère .0001

            //fermeture ancien fichier
            ioFileToClose?.Close();

            string sNomFichier = ExtractFileNameAndPathFromFullPath(sFilename)[0];
            if (bWithoutOldInfo)
            {
                try
                {
                    sNomFichier = sNomFichier.Split(Convert.ToChar("^"))[0];
                }
                catch (Exception)
                {
                    //RAS
                }
            }

            string sNomFichierFinal;

            if (sAdditionalInfo.Equals(""))
            {
                sNomFichierFinal = sNomFichier + "^" + "[S-" + System.DateTime.Now.ToString("yyyyMMdd") + "- T" + (numThread + 1).ToString("000") + "]" + "." + cptFile.ToString("0000");
            }
            else
            {
                sNomFichierFinal = sNomFichier + "^" + "[S-" + sAdditionalInfo + "-" + System.DateTime.Now.ToString("yyyyMMdd") + "- T" + (numThread + 1).ToString("000") + "]" + "." + cptFile.ToString("0000");
            }

            string sPathFinal = ExtractFileNameAndPathFromFullPath(sFilename)[1];

            if (!Directory.Exists(sPathFinal))
            {
                Directory.CreateDirectory(sPathFinal);
            }

            StreamWriter ioNewFile = new(sPathFinal + sNomFichierFinal, false, Encoding.UTF8);

            ioNewFile.WriteLine(sListHeaders);
            //ioNewFile.Close();
            //écriture entête

            return ioNewFile;

        }

        public static string WriteFileFromList(List<string> sListQueries, string sFilename, string sPath, bool bRewrite = false)
        {
            string sFichierFinal = "";

            if (Directory.Exists(sPath))
            {
                sFichierFinal = sPath + sFilename;
                StreamWriter file = new(sFichierFinal, bRewrite, Encoding.UTF8);

                foreach (string s in sListQueries)
                {
                    file.WriteLine(s);
                }

                file.Close();
            }

            return sFichierFinal;

        }

        public static string ZipListOfFiles(List<string> sListFiles, string sPath)
        {

            string sFichierZip = "";
            string sNomPremierFichier = ExtractFileNameAndPathFromFullPath(sListFiles[0])[0];

            //suppression des caractères dégueu de mon traitement
            try
            {
                sNomPremierFichier = sNomPremierFichier.Split(Convert.ToChar("^"))[0];
            }
            catch (Exception)
            {
                //RIEN A FAIRE
            }
            string sTempPath = string.Concat(sPath, sNomPremierFichier, "_", DateTime.Now.ToString("yyyyMMdd"), "\\");

            //création répertoire temporaire
            if (!Directory.Exists(sTempPath))
            {
                try
                {
                    Directory.CreateDirectory(sTempPath);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            if (Directory.Exists(sPath))
            {

                //déplacement des fichiers dans un répertoire temporaire
                foreach (string sFichier in sListFiles)
                {
                    try
                    {
                        File.Move(sFichier, sTempPath + Path.GetFileName(sFichier));
                    }
                    catch (Exception)
                    {
                        File.Copy(sFichier, sTempPath + Path.GetFileName(sFichier));
                        //TODO si ça foire aussi
                    }

                }

                //création du ZIP
                try
                {
                    sFichierZip = string.Concat(sPath, sNomPremierFichier, "_", DateTime.Now.ToString("yyyyMMdd"), ".ZIP");
                    if (File.Exists(sFichierZip)) { File.Delete(sFichierZip); }
                    ZipFile.CreateFromDirectory(sTempPath, sFichierZip);
                }
                catch (Exception)
                {
                    throw;
                }

                //suppression du répertoire temporaire
                try
                {
                    Directory.Delete(sTempPath, true);
                }
                catch (Exception)
                {
                    throw;
                }

            }

            return sFichierZip;
        }

        public static void DeleteListOfFiles(List<string> sListFiles)
        {
            foreach (string sFichier in sListFiles)
            {
                try
                {
                    File.Delete(sFichier);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        public static List<string> NamingPatternGenerator(Job INIP, List<string> sPathFile, string sDriverExt, int iCptFile, int iCptRow, DataRowCollection dRs, string sQueryAlias, ref LogTools MyLog, Query FuzibleQuery)
        {
            string sOriginalPath = sPathFile[1];
            string sOriginalFile = sPathFile[0];
            string sOriginalExt = sPathFile[2].Replace("[EXTENSION]", sDriverExt); //si on a saisi par exemple monfichier.[EXTENSION]

            string[] sSplitScript = { "[" };

            string sColumnName;
            int iColumnPos;

            string sOldFSPattern = sOriginalFile;
            string sNewFSPattern = sOriginalFile;

            //---------------------------gestion pattern multifichier
            try
            {
                //if (sOldFSPattern.Length == 0) { sOldFSPattern = "[QUERYTARGETNAME]_[FILECOUNT]"; }
                //if (sNewFSPattern.Length == 0) { sNewFSPattern = "[QUERYTARGETNAME]_[FILECOUNT]"; }

                string[] sFSZones = sOldFSPattern.Split(sSplitScript, StringSplitOptions.RemoveEmptyEntries);

                foreach (string sZ in sFSZones)
                {
                    if (sZ.ToUpper().StartsWith("QUERYALIAS]"))
                    {
                        sOldFSPattern = sOldFSPattern.Replace("[QUERYALIAS]", sQueryAlias);
                        sNewFSPattern = sNewFSPattern.Replace("[QUERYALIAS]", sQueryAlias);
                    }
                    else if (sZ.ToUpper().StartsWith("EXTENSION]"))
                    {
                        sOldFSPattern = sOldFSPattern.Replace("[EXTENSION]", sOriginalExt.Length == 0 ? "." + sDriverExt : sDriverExt); //si on a saisi par exemple monfichier[EXTENSION]
                        sNewFSPattern = sNewFSPattern.Replace("[EXTENSION]", sOriginalExt.Length == 0 ? "." + sDriverExt : sDriverExt);
                    }
                    else if (sZ.ToUpper().StartsWith("FILECOUNT]"))
                    {
                        sOldFSPattern = sOldFSPattern.Replace("[FILECOUNT]", (iCptFile - 1).ToString());
                        sNewFSPattern = sNewFSPattern.Replace("[FILECOUNT]", iCptFile.ToString());
                    }
                    else if (sZ.ToUpper().StartsWith("ROWCOUNT]"))
                    {
                        sOldFSPattern = sOldFSPattern.Replace("[ROWCOUNT]", (iCptRow == 0 ? 0 : iCptRow - 1).ToString());
                        sNewFSPattern = sNewFSPattern.Replace("[ROWCOUNT]", iCptRow.ToString());
                    }
                    else if (sZ.ToUpper().Contains(']')) //interprétation des autres paramètres (usuellement, les noms de colonne de la requête source
                    {
                        iColumnPos = -1;
                        sColumnName = sZ[..sZ.IndexOf("]")];
                        //---------------------------------
                        sColumnName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sColumnName, INIP.DynParams);

                        //recherche de la colonne
                        if (dRs != null)
                        {
                            foreach (DataColumn dtC in dRs[iCptRow == 0 ? 0 : iCptRow - 1].Table.Columns)
                            {
                                if (dtC.ColumnName.ToUpper().Equals(sColumnName.ToUpper())) { iColumnPos = dtC.Ordinal; }
                            }

                            if (iColumnPos > -1) // en cas de colonne non trouvée, on n'affiche rien
                            {
                                sOldFSPattern = sOldFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), Toolbox.RemoveSpecialCharacters(dRs[iCptRow == 0 ? 0 : iCptRow - 1][iColumnPos].ToString(), "_", false));
                                if (dRs.Count > 1) //cas de la gestion multi-fichier avec un seul fichier à produire (1 ligne dans le dataset)
                                {
                                    sNewFSPattern = sNewFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), Toolbox.RemoveSpecialCharacters(dRs[iCptRow][iColumnPos].ToString(), "_", false));
                                }
                                else { sNewFSPattern = sOldFSPattern; }
                            }
                            else
                            {
                                sOldFSPattern = sOldFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), sColumnName);
                                sNewFSPattern = sNewFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), sColumnName);
                            }
                        }
                        else
                        {
                            sOldFSPattern = sOldFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), sColumnName);
                            sNewFSPattern = sNewFSPattern.Replace(string.Concat("[", sZ[..sZ.IndexOf("]")], "]"), sColumnName);
                        }
                    }
                }

                //on remplace les caractères spéciaux, pour éviter de créer des fichiers avec des caractères interdits
                sOldFSPattern = Toolbox.RemoveSpecialCharacters(sOldFSPattern, "_", false);
                sNewFSPattern = Toolbox.RemoveSpecialCharacters(sNewFSPattern, "_", false);
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return new List<string>(new string[] { sOriginalPath, sOriginalExt, sOldFSPattern, sNewFSPattern });

        }

        #endregion

        #region "PUBLIC VOID"

        public FITools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            MyLog = LogJob;
            JobParameters = INIP;

            string sPa = INIP.GlobalParameters.FILE_PROCESSED_DIR.Length > 0 ? INIP.GlobalParameters.FILE_PROCESSED_DIR.Replace("\\", "") : "Processed";
            Path_Processed_Files = string.Concat(sPa, "\\");

            string sPb = INIP.GlobalParameters.FILE_EXPORT_DIR.Length > 0 ? INIP.GlobalParameters.FILE_EXPORT_DIR.Replace("\\", "") : "Export";
            Path_Exported_Files = string.Concat(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible", "\\", sPb, "\\");

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.SRC: //SOURCE
                    Connection = INIP.ConnectionString_Source;
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.SRC;
                    if (INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                    {
                        if (INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Equals("FTP"))
                        {
                            IsWorkingDirectoryAnFTPURL = true;
                        }
                        if (INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Equals("SFTP"))
                        {
                            IsWorkingDirectoryAnSFTPURL = true;
                        }
                        Path_Exported_Files = FTPTools.WORKING_DIRECTORY_GET;
                        if (!Directory.Exists(Path_Exported_Files)) { try { Directory.CreateDirectory(Path_Exported_Files); } catch (Exception) { throw; } }
                    }
                    else if (INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("NETWORK") > -1 && INIP.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD).Length > 0) // test de la spécificité Fuzible sur les chaines car ça peut être un (S)FTP : USERNAME=MyUser;PASSWORD=MyPassword;FTP=MyFTP;PORT=MyPort 
                    {
                        string sPath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.ConnectionString_Source.SConnString(INIP.DynParams), INIP.DynParams);

                        AddNetworkPathToCache(INIP.ConnectionString_Source, sPath, ref MyLog);
                    }
                    else if (INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_CSV || INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_XLS || INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_XML || INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_JSON)
                    {
                        {
                            Path_Exported_Files = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.ConnectionString_Source.SConnString(INIP.DynParams), INIP.DynParams);
                        }
                        if (!Directory.Exists(Path_Exported_Files)) { try { Directory.CreateDirectory(Path_Exported_Files); } catch (Exception) { throw; } }
                    }
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG: //TARGET
                    Connection = INIP.ConnectionString_Target;
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.TRG;
                    if (INIP.ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1)
                    {
                        if (INIP.ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Equals("FTP"))
                        {
                            IsWorkingDirectoryAnFTPURL = true;
                        }
                        if (INIP.ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Equals("SFTP"))
                        {
                            IsWorkingDirectoryAnSFTPURL = true;
                        }
                        Path_Exported_Files = FTPTools.WORKING_DIRECTORY_SET;
                        if (!Directory.Exists(Path_Exported_Files)) { try { Directory.CreateDirectory(Path_Exported_Files); } catch (Exception) { throw; } }
                    }
                    else if (INIP.ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("NETWORK") > -1 && INIP.ConnectionString_Target.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD).Length > 0) // test de la spécificité Fuzible sur les chaines car ça peut être un (S)FTP : USERNAME=MyUser;PASSWORD=MyPassword;FTP=MyFTP;PORT=MyPort 
                    {
                        string sPath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.ConnectionString_Target.SConnString(INIP.DynParams), INIP.DynParams);

                        AddNetworkPathToCache(INIP.ConnectionString_Target, sPath, ref MyLog);
                    }
                    else if (INIP.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.FI_CSV || INIP.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.FI_XLS || INIP.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.FI_XML || INIP.ConnectionString_Source.SConnDriver == SQLTools_Enums.BDD.FI_JSON)
                    {
                        {
                            Path_Exported_Files = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.ConnectionString_Target.SConnString(INIP.DynParams), INIP.DynParams);
                        }
                        if (!Directory.Exists(Path_Exported_Files)) { try { Directory.CreateDirectory(Path_Exported_Files); } catch (Exception) { throw; } }
                    }
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.LOG: //LOG
                    Connection = INIP.ConnectionString_Target;
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.LOG;
                    Path_Exported_Files = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, INIP.DynParams);
                    break;
            }

        }

        public static void AddNetworkPathToCache(CONNString CS, string sPath, ref LogTools MyLog)
        {
            if (!CACHED_NETWORK.Contains(sPath))
            {
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("NETWORK") > -1 && CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD).Length > 0)
                {
                    Match mcComputer = Regex.Match(sPath, @"\\\\([^\\]+)\\");

                    try
                    {
                        string sServer = mcComputer.Value[2..^1];
                        var net = NetworkShareAccesser.Access(sServer, CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));
                        var fileList = Directory.GetFiles(sPath);
                        var pathList = Directory.GetDirectories(sPath);

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, "NetworkShareAccesser (" + sPath + ") - > Files : " + fileList.Length + " / Directories : " + pathList.Length, SQLTools_Enums.LOG_TYPEINFO.INF);

                        net.Dispose();

                        //var networkPath = sPath;
                        //var credentials = new NetworkCredential(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));

                        //using (new NetworkConnection(networkPath, credentials))
                        //{
                        //    var fileList = Directory.GetFiles(networkPath);
                        //}
                        CACHED_NETWORK.Add(sPath);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            NetworkCredential theNetworkCredential = new(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));
                            var NetCache = new CredentialCache
                            {
                                { new Uri(mcComputer.Value[0..^1]), "Basic", theNetworkCredential }
                            };
                            var fileList = Directory.GetFiles(sPath);
                            var pathList = Directory.GetDirectories(sPath);

                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, "NetworkCredential (" + sPath + ") - > Files : " + fileList.Length + " / Directories : " + pathList.Length, SQLTools_Enums.LOG_TYPEINFO.INF);

                            CACHED_NETWORK.Add(sPath);
                        }
                        catch (Exception ex2)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "NetworkCredential - >" + Languages.Languages.fi_cantaddnetworkpathtocache + " : " + sPath, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex2, "NetworkConnection - >" + Languages.Languages.fi_cantaddnetworkpathtocache + " : " + sPath, SQLTools_Enums.LOG_TYPEINFO.ERR);
                            //FuzibleQuery.QueryErrors += 1;
                        }
                    }
                }
            }
        }

        public List<SQLColumn> GetListFieldsTypesFromFile(string[] fileInRam, string sFileName, string sCSVCharSeparator, bool bFirstRowHeader, Query FuzibleQuery, int iTable)
        {
            //La pseudo-requête va me servir à nommer les champs d'entête si on part du postulat que le fichier CSV n'a pas de ligne d'entête (les champs de la pseudo-requête font office de)
            List<string> sListeTypesColonnes = new();

            int iTotalFieldsInFile = 0;

            iTotalFieldsInFile = Regex.Matches(fileInRam[0 + JobParameters.CSVRowOffset], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray().Length;

            List<string[]> sEnteteDef = new();

            //copie des champs de la requête originale (pour ne pas intégrer les noms des champs véritables en cas de SELECT *
            List<Query.QField> sEnteteTmp = new();
            for (int iF = 0; iF < FuzibleQuery.QueryAnalyzer.Fields.Count; iF++)
            {
                sEnteteTmp.Add(FuzibleQuery.QueryAnalyzer.Fields[iF]);
            }

            //cas des select *, si dans le mode "Get File Head from query file, on a mentionné un SELECT *, il faut quand même compter les colonnes

            if (sEnteteTmp[0].Name.Equals("*"))
            {
                try
                {
                    if (!bFirstRowHeader)
                    {
                        string[] sFields;

                        sFields = Regex.Matches(fileInRam[0 + JobParameters.CSVRowOffset], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();

                        for (int iCpt = 0; iCpt < sFields.Length; iCpt++)
                        {
                            sFields[iCpt] = string.Concat("COLUMN_", (iCpt + 1).ToString());
                        }
                        //FuzibleQuery.QueryAnalyzer.SetFieldsByArray(sFields);

                        sEnteteTmp.Clear();
                        for (int iCpt = 0; iCpt < sFields.Length; iCpt++)
                        {
                            sEnteteTmp.Add(new Query.QField(sFields[iCpt], sFields[iCpt], FuzibleQuery.QueryAnalyzer.Tables[iTable].Name, FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias, "", iCpt));
                        };

                        //sEnteteTmp = FuzibleQuery.QueryAnalyzer.Fields;
                    }
                    else
                    {
                        string[] sFields;

                        sFields = Regex.Matches(fileInRam[0 + JobParameters.CSVRowOffset], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();

                        sEnteteTmp.Clear();
                        for (int iCpt = 0; iCpt < sFields.Length; iCpt++)
                        {

                            sEnteteTmp.Add(new Query.QField(sFields[iCpt], sFields[iCpt], FuzibleQuery.QueryAnalyzer.Tables[iTable].Name, FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias, "", iCpt));
                        };
                    }
                }
                catch (Exception ex)
                {
                    sEnteteTmp = new List<Query.QField>(); MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(sFileName, Languages.Languages.fi_noheader), FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }

            if (bFirstRowHeader)
            {
                string[] sHeader;

                sHeader = Regex.Matches(fileInRam[0 + JobParameters.CSVRowOffset], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();

                for (int i = 0; i < sHeader.Length; i++) //ici, je cherche à ajouter toutes les colonnes de l'header, je veux tout récupérer, et éventuellement matcher si elles sont dans la requête pour avoir les alias, les transformations....
                {
                    sEnteteDef.Add(new string[] { sHeader[i], sHeader[i], i.ToString() });
                }
            }
            else
            {
                foreach (Query.QField s in sEnteteTmp)
                {
                    if (Path.GetFileName(s.Table).Equals(sFileName, StringComparison.InvariantCultureIgnoreCase) || Path.GetFileName(s.TableAlias).Equals(sFileName, StringComparison.InvariantCultureIgnoreCase)) // on vérifie bien que le champ qu'on ajoute est bien associé au fichier requêté
                    {
                        sEnteteDef.Add(new string[] { s.Name, s.Name, s.Index.ToString() });
                    }
                }
            }

            //sSplitEntete = sEnteteDef.ToArray();
            if (sEnteteDef.Count > 0)
            {
                List<SQLColumn> sListColumns = new();
                for (int iF = 0; iF < sEnteteDef.Count; iF++)
                {
                    //if (sEnteteDef[iF][0].StartsWith(sCSVCharSeparator))
                    //{ sEnteteDef[iF][0] = sEnteteDef[iF][0][1..]; }

                    //if (sEnteteDef[iF][1].StartsWith(sCSVCharSeparator))
                    //{ sEnteteDef[iF][1] = sEnteteDef[iF][1][1..]; }

                    if (sEnteteDef[iF][0].Trim().Equals(""))
                    {
                        sEnteteDef[iF][0] = string.Concat(Languages.Languages.fi_unknowncell, iF.ToString("000")); sEnteteDef[iF][1] = string.Concat(Languages.Languages.fi_unknowncell, iF.ToString("000"));
                    }

                    if (sEnteteDef[iF][0].StartsWith("\"") && sEnteteDef[iF][0].EndsWith("\""))
                    {
                        sEnteteDef[iF][0] = sEnteteDef[iF][0][1..^1];
                    }
                    if (sEnteteDef[iF][1].StartsWith("\"") && sEnteteDef[iF][1].EndsWith("\""))
                    {
                        sEnteteDef[iF][1] = sEnteteDef[iF][1][1..^1];
                    }

                    if (Regex.Match(sEnteteDef[iF][0], "^" + sCSVCharSeparator + "{1}.*").Success)
                    {
                        sEnteteDef[iF][0] = sEnteteDef[iF][0][1..];
                    }
                    if (Regex.Match(sEnteteDef[iF][1], "^" + sCSVCharSeparator + "{1}.*").Success)
                    {
                        sEnteteDef[iF][1] = sEnteteDef[iF][1][1..];
                    }

                    //colonnes avec le même nom... ça arrive...
                    if (sListColumns.Any(c => c.ColumnName.Equals(sEnteteDef[iF][1])) && sEnteteDef[iF][1].Length > 0)
                    {
                        sListColumns.Add(new SQLColumn(iF, string.Concat(sEnteteDef[iF][1], "_", iF.ToString()), SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "", true, "", false, false));
                    }
                    else if (sListColumns.Any(c => c.ColumnName.Trim().Equals(sEnteteDef[iF][1].Trim())) && sEnteteDef[iF][1].Trim().Length > 0)
                    {
                        sListColumns.Add(new SQLColumn(iF, string.Concat(sEnteteDef[iF][1], "_", iF.ToString()), SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "", true, "", false, false));
                    }
                    else { sListColumns.Add(new SQLColumn(iF, sEnteteDef[iF][1], SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "", true, "", false, false)); }
                }
                return sListColumns;
            }
            else
            {
                return new List<SQLColumn>();
            }
        }

        public DataSet CompareDsWithTargetFile(Query FuzibleQuery, string sTargetPath, string sTargetFile, bool withMergeSourceTarget, DataTable dtSource)
        {
            try
            {
                List<string> sTargetFiles = new List<string>();

                //ajout de l'extension au nom du fichier cible

                DataSet dsTarget = new();

                List<SQLColumn> sListFieldsPK = new();

                DataSet dsCompare = new();

                //on vérifie qu'il y a des données dans le fichier cible, sinon, aucun intérêt !
                List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sTargetPath + sTargetFile);
                List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, 0, dtSource.Rows, dtSource.TableName, ref MyLog, FuzibleQuery);
                bool bTargetFileExists = File.Exists(sFile[0] + sFile[3] + sFile[1]);

                //si le fichier cible n'existe pas on la crée complètement avec les données
                if (!bTargetFileExists)
                {
                    DataSet dsSource = new();
                    dsSource.Tables.Add(dtSource.Copy());
                    sTargetFiles = BuildFileFromDs(dtSource, sTargetFile, FuzibleQuery);
                    for (int i = 0; i < sTargetFiles.Count; i++)
                    {
                        sTargetFiles[i] = Path.GetFileName(sTargetFiles[i]);
                    }
                }
                else
                {
                    sTargetFiles.Add(sFile[3] + sFile[1]);
                }

                foreach (var file in sTargetFiles)
                {
                    //récupération des données dans la cible
                    //construction de la requête cible
                    FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetColumns = Toolbox.GetSQLColumnsFromDataTable(dtSource);
                    FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.SynchroTargetTable = Path.GetFileName(file);

                    string sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(false, JobParameters.SynchroBypassQueryFiltersInTarget, false);
                    sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sTargetQuery, JobParameters, FuzibleQuery, dtSource);

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_loadsourcedata_query + sTargetQuery, SQLTools_Enums.LOG_TYPEINFO.DET);

                    Query sQ = new(JobParameters, sTargetQuery)
                    {
                        ConnectionSrc = JobParameters.ConnectionString_Target
                    };

                    int iTableToWorkWith = 0;
                    dsTarget = GetDataFromFile(JobParameters.ConnectionString_Target.SConnDriver, ref sQ);
                    if (dsTarget.Tables.Count > 1) { iTableToWorkWith = dsTarget.Tables.Count - 1; }

                    FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.RealSynchroTargetColumns = Toolbox.GetSQLColumnsFromDataTable(dsTarget.Tables[iTableToWorkWith]);

                    //-----------------------------------------------

                    SHSOperations SHSX = new(JobParameters, FuzibleQuery, ref MyLog); //on recherche les champs NUL/NOT NULL car la recherche de PK requiert des champs NOT NULL
                    SHSX.SetDBNullInDataSet(dsTarget.Tables[iTableToWorkWith]);

                    //par la suite, on va travailler uniquement avec un datatable et non un dataset
                    DataTable dtTarget = new();
                    if (dsTarget.Tables.Count > 0)
                    {
                        dtTarget = dsTarget.Tables[iTableToWorkWith].Copy();
                        dsTarget.Clear();
                    }

                    ///////////////////////////////////////////////////////////////////////////////////////////////////////
                    //METHODE 1 : on essaye de trouver une clé primaire dans la table cible (ne marche que si elle existe !)
                    string sMessageKey = "";
                    if (sListFieldsPK.Count == 0)
                    {
                        sMessageKey = Languages.Languages.fi_synchro_pkfromsource + dtSource.TableName;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                        DataColumn[] dtCPK = dtSource.PrimaryKey;
                        if (dtCPK.Length != 0)
                        {
                            foreach (DataColumn dtC in dtCPK)
                            {
                                sListFieldsPK.Add(SQLColumn.SQLColumnFromDataColumn(dtC, SQLTools_Enums.TYPE_DATA.VARCHAR));
                            }
                        }
                    }

                    //METHODE 3 : toujours rien, on va tâcher de deviner
                    if (sListFieldsPK.Count == 0)
                    {
                        //METHODE 3A : basé sur source (cible vide)
                        if (bTargetFileExists)
                        {
                            sMessageKey = Languages.Languages.fi_synchro_pkfromshssource + dtSource.TableName + ")";
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);

                            string[] sFileInRam; //on rappatrie d'abord les colonnes de la source pour vérifier que les colonnes éventuellement trouvées comme PK dans la cile sont bien dans la source
                            sFileInRam = ConvertDtIntoArray(dtTarget);

                            bool bFirstRowHeader = true; //analyse CSV pour savoir si la première ligne = entête

                            List<SQLColumn> sListOfColumnsTargetFromDataset = GetListFieldsTypesFromFile(sFileInRam, sTargetPath + file, JobParameters.CSVCharSeparator_Target, bFirstRowHeader, FuzibleQuery, 0);

                            sListFieldsPK = SHS.GuessPKFromQuery(dtSource, SQLTools_Enums.TYPE_DATA.VARCHAR, sListOfColumnsTargetFromDataset);
                            sFileInRam = null;
                        }
                        //METHODE 3B : basé sur cible
                        else
                        {
                            //et si la requête n'a rien donné, on va essayer de deviner quelle est la clé
                            sMessageKey = Languages.Languages.fi_synchro_pkfromshstarget + file + ")";
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);

                            string[] sFileInRam; //on rappatrie d'abord les colonnes de la source pour vérifier que les colonnes éventuellement trouvées comme PK dans la cile sont bien dans la source
                            sFileInRam = ConvertDtIntoArray(dtSource);

                            bool bFirstRowHeader = true; //analyse CSV pour savoir si la première ligne = entête

                            List<SQLColumn> sListOfColumnsSourceFromDataset = GetListFieldsTypesFromFile(sFileInRam, FuzibleQuery.QueryAnalyzer.Tables[0].Name, JobParameters.CSVCharSeparator_Target, bFirstRowHeader, FuzibleQuery, 0);
                            sListFieldsPK = SHS.GuessPKFromQuery(dtTarget, SQLTools_Enums.TYPE_DATA.VARCHAR, sListOfColumnsSourceFromDataset);
                            sFileInRam = null;
                        }
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.fi_synchro_errortargetquery + " : " + Languages.Languages.fi_synchro_cantgofurther);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }

                    ///////////////////////////////////////////////////////////////////////////////////////////////////////

                    //Inutile d'aller plus loin sans clé primaire
                    if (sListFieldsPK.Count > 0)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty(file, SQLTools_Enums.QUERY_PROPERTIES.SYNCHRO_PRIMARY_KEY, string.Join(",", sListFieldsPK.Select(pk => pk.ColumnName)), true);

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_applypk, SQLTools_Enums.LOG_TYPEINFO.INF);

                        var dtCPKS = new DataColumn[sListFieldsPK.Count];
                        var dtCPKT = new DataColumn[sListFieldsPK.Count];
                        for (int iSQLC = 0; iSQLC < sListFieldsPK.Count; iSQLC++)
                        {
                            dtCPKS[iSQLC] = dtSource.Columns[sListFieldsPK[iSQLC].ColumnName];
                            dtCPKT[iSQLC] = dtTarget.Columns[sListFieldsPK[iSQLC].ColumnName];
                        }
                        dtSource.PrimaryKey = dtCPKS;
                        dtTarget.PrimaryKey = dtCPKT;

                        if (dtTarget != null)
                        {

                            //---------------------
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_rowssource + dtSource.TableName + ") : " + dtSource.Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_rowstarget + file + ") : " + dtTarget.Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                            //réarrangement des colonnes de dataset : la source et la cible peuvent avoir les mêmes colonnes, mais pas dans le même ordre !
                            //il faut d'abord enlever dans les sources les colonnes qui pourraient être en trop : il peut y avoir un nombre de colonnes différent de la source !
                            //List<string> sListColsToRemove = new List<string>();
                            //for (int iCpt = 0; iCpt < dtTarget.Columns.Count; iCpt++)
                            //{
                            //    if (!dtSource.Columns.Contains(dtTarget.Columns[iCpt].ColumnName))
                            //    { sListColsToRemove.Add(dtTarget.Columns[iCpt].ColumnName); }
                            //}
                            //foreach (string sC in sListColsToRemove) { dtTarget.Columns.Remove(sC); }
                            //sListColsToRemove.Clear();
                            //for (int iCpt = 0; iCpt < dtSource.Columns.Count; iCpt++)
                            //{
                            //    if (!dtTarget.Columns.Contains(dtSource.Columns[iCpt].ColumnName))
                            //    { sListColsToRemove.Add(dtSource.Columns[iCpt].ColumnName); }
                            //}
                            ////attention : j'ai désactivé la ligne ci-dessous en avril 2019 car dans le cas des multi-target (avec des colonnes différentes), ça me posait problème qu'on vire des colonnes de la source
                            ////foreach (string sC in sListColsToRemove) { dtSource.Columns.Remove(sC); }
                            //sListColsToRemove.Clear();

                            ////réarrangement des colonnes de dataset : la source et la cible peuvent avoir les mêmes colonnes, mais pas dans le même ordre !
                            //foreach (DataColumn dC in dtSource.Columns)
                            //{
                            //    dtSource.Columns[dC.ColumnName].SetOrdinal(dC.Ordinal);
                            //    if (dtTarget.Columns.Contains(dC.ColumnName))
                            //    {
                            //        int iOrdinal = dC.Ordinal;
                            //        if (iOrdinal >= dtTarget.Columns.Count) { iOrdinal = dtTarget.Columns.Count - 1; }
                            //        dtTarget.Columns[dC.ColumnName].SetOrdinal(iOrdinal);
                            //    }
                            //}

                            //comparaison source - cible
                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            dsCompare = SHS.CompareDataset(dtSource, dtTarget, ref sListFieldsPK, ClassPurpose, FuzibleQuery);
                            int iInserted = dsCompare.Tables[0].Rows.Count;
                            int iUpdated = dsCompare.Tables[1].Rows.Count;
                            int iDeleted = dsCompare.Tables[2].Rows.Count;

                            if (withMergeSourceTarget)
                            {
                                //merge des dataset à partir de la source et des 3 tables du comparateur (INSERT, UPDATE, DELETE)
                                //TODO
                                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("I") > -1 && iInserted > 0)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_rowstoinsert + file + ") : " + iInserted.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                                    foreach (DataRow dr in dsCompare.Tables[0].Rows) { dtTarget.ImportRow(dr); }

                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, iInserted.ToString(), false);
                                }
                                else
                                {
                                    iInserted = 0;
                                }

                                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("U") > -1 && iUpdated > 0)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_rowstoupdate + file + ") : " + iUpdated.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                                    if (JobParameters.SynchroStoreChanges)
                                    {
                                        SynchroModeStoreUpdateDeleteChanges("U", dsCompare.Tables[1], file, FuzibleQuery);
                                    }

                                    string[] sValuesPK = new string[sListFieldsPK.Count];

                                    for (int iR = 0; iR < dsCompare.Tables[1].Rows.Count; iR++)
                                    {
                                        for (int cptField = 0; cptField < sListFieldsPK.Count; cptField++)
                                        {
                                            sValuesPK[cptField] = dsCompare.Tables[1].Rows[iR][sListFieldsPK[cptField].ColumnName].ToString();
                                        }

                                        DataRow drToUpdate = dtTarget.Rows.Find(sValuesPK);
                                        //maj ligne champ/champ
                                        foreach (DataColumn dc in drToUpdate.Table.Columns)
                                        {
                                            if (sListFieldsPK.FindIndex(x => x != null && x.ColumnName.Equals(dc.ColumnName, StringComparison.InvariantCultureIgnoreCase)) == -1)
                                            {
                                                drToUpdate[dc] = dsCompare.Tables[1].Rows[iR][dc.ColumnName];
                                            }
                                        }

                                    }

                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, iUpdated.ToString(), false);
                                }
                                else
                                {
                                    iUpdated = 0;
                                }

                                //pas de merge possible en suppression de lignes... Méthode manuelle
                                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("D") > -1 && iDeleted > 0)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_rowstodelete + file + ") : " + iDeleted.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                                    if (JobParameters.SynchroStoreChanges)
                                    {
                                        SynchroModeStoreUpdateDeleteChanges("D", dsCompare.Tables[1], file, sQ);
                                    }

                                    string[] sValuesPK = new string[sListFieldsPK.Count];
                                    int iQteRows = dtTarget.Rows.Count;
                                    DataRow rowTemp;
                                    List<DataRow> rowsToDelete = new();
                                    for (int iR = 0; iR < iQteRows; iR++)
                                    {
                                        for (int cptField = 0; cptField < sListFieldsPK.Count; cptField++)
                                        {
                                            sValuesPK[cptField] = dtTarget.Rows[iR][sListFieldsPK[cptField].ColumnName].ToString();
                                        }
                                        rowTemp = dsCompare.Tables[2].Rows.Find(sValuesPK);
                                        if (rowTemp != null)
                                        {
                                            rowsToDelete.Add(dtTarget.Rows[iR]);
                                        }
                                    }
                                    foreach (DataRow dR in rowsToDelete)
                                    {
                                        dtTarget.Rows.Remove(dR);
                                    }

                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, iDeleted.ToString(), false);
                                }
                                else
                                {
                                    iDeleted = 0;
                                }

                                MyLog.AddJobReportRow(file, dtSource.Rows.Count, dtTarget.Rows.Count, iInserted, iUpdated, iDeleted);
                            }
                        }
                        else
                        {
                            Exception ex = new(Languages.Languages.fi_synchro_errortargetquery + " (" + file + ") " + Languages.Languages.fi_synchro_cantgofurther);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_synchro_noprimarykey + file + ") " + Languages.Languages.fi_synchro_cantgofurther, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    dtTarget.TableName = file;
                    dsCompare.Tables.Add(dtTarget.Copy());
                    dtTarget.Clear();
                    dtTarget = null;
                }

                return dsCompare;
            }
            catch (OperationCanceledException)
            {
                throw;
            }

        }

        public async Task<DataSet> GetRawFileData(string sPath, string sFilename, Query FuzibleQuery)
        {
            DataSet dsData = new();
            DataTable dtData = new("RAW_OUTPUT");
            dtData.Columns.Add("RAWFILE_NAME");
            dtData.Columns.Add("RAWFILE_BINARY", Type.GetType("System.Byte[]"));

            List<string> sFichiersSplitA = GetAllFilesFromSplitted(sPath, sFilename, FuzibleQuery);

            foreach (string sFile in sFichiersSplitA)
            {
                try
                {
                    DataRow dr = dtData.NewRow();
                    var bytes = await File.ReadAllBytesAsync(sFile, Monitoring.TaskCancellationToken);
                    dr[1] = bytes;
                    var str = System.Text.Encoding.UTF8.GetString(bytes);
                    dr[0] = Path.GetFileName(sFile); // str; //await File.ReadAllTextAsync(sFile, ctsToken);


                    //si on a mis un alias dynamique dans la requête on suppose qu'on veut l'utiliser comme nouveau nom de fichier
                    if (FuzibleQuery.QueryAnalyzer.Tables.Count == 1 &&
                        Path.GetFileName(sFile).LastIndexOf(".") > -1 &&
                        !FuzibleQuery.QueryAnalyzer.Tables[0].Name.Equals(FuzibleQuery.QueryAnalyzer.Tables[0].Alias) &&
                        FuzibleQuery.QueryAnalyzer.Tables[0].Alias.EndsWith(Path.GetFileName(sFile)[sFilename.LastIndexOf(".")..], StringComparison.OrdinalIgnoreCase))
                    {
                        dr[0] = FuzibleQuery.QueryAnalyzer.Tables[0].Alias;
                    }

                    dtData.Rows.Add(dr);

                    FuzibleQuery.QueryAnalyzer.AddQueryProperty(sPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED, sFile, false);

                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.fi_cantextractraw, sFile, Languages.Languages.fi_csv_filecantberead), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }
            }

            dsData.Tables.Add(dtData);

            return dsData;
        }

        public DataSet GetDataFromFile(SQLTools_Enums.BDD bDD, ref Query FuzibleQuery)
        {
            DataSet dsData = new();
            DataSet dsJoined = new();

            int iTable = -1;

            foreach (Query.QTable qT in FuzibleQuery.QueryAnalyzer.Tables)
            {
                //
                if (MyLog.DebugFunctions)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                    string.Concat("T00", " Searching for Subquery"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                }
                //

                iTable++;

                //recherche de subquery
                if (Regex.IsMatch(qT.Name, "\\(\\s*SELECT\\s+", RegexOptions.IgnoreCase) && qT.Name.EndsWith(")"))
                {
                    string sSq = qT.Name[1..];
                    sSq = sSq[0..^1];
                    Query SubQuery = new(JobParameters, string.Concat("SUBQUERY_", iTable.ToString(), ":", sSq));
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_copy_subquery + SubQuery.RawQuery, SQLTools_Enums.LOG_TYPEINFO.INF);
                    if (iTable == 0)
                    {
                        try
                        {
                            dsData = MThread.GetSourceData(0, JobParameters, ref SubQuery, MyLog, false)[0];
                        }
                        catch { break; }
                    }
                    else
                    {
                        try
                        {
                            dsJoined = MThread.GetSourceData(0, JobParameters, ref SubQuery, MyLog, false)[0];
                        }
                        catch { break; }
                    }
                }
                else
                {
                    string sPath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(FuzibleQuery.ConnectionSrc.SConnString(JobParameters.DynParams), JobParameters.DynParams); //a ce stade on a on un chemin, ou une adresse FTP
                    string sFilename = qT.Name;

                    string sOriginalPath = sPath;
                    string sAdditionnalPath = "";

                    if (sFilename.LastIndexOf("\\") > 0) //on peut avoir crée un output qui contient un subdir (ex : \OUTPUT\coucou.csv:select...
                    {
                        if (sFilename.StartsWith("\\")) { sFilename = sFilename[1..]; }
                        sAdditionnalPath = sFilename[..sFilename.LastIndexOf("\\")];
                        sPath = string.Concat(sPath, sPath.EndsWith("\\") ? "" : "\\", sAdditionnalPath);
                        sFilename = sFilename[(sFilename.LastIndexOf("\\") + 1)..];
                    }
                    else if (sFilename.LastIndexOf("/") > 0) //on peut avoir crée un output qui contient un subdir (ex : /OUTPUT/coucou.csv:select...
                    {
                        if (sFilename.StartsWith("/")) { sFilename = sFilename[1..]; }
                        sAdditionnalPath = sFilename[..sFilename.LastIndexOf("/")];
                        sPath = string.Concat(sPath, sPath.EndsWith("\\") ? "" : "\\", sAdditionnalPath);
                        sFilename = sFilename[(sFilename.LastIndexOf("/") + 1)..];
                    }

                    if (!sPath.EndsWith("\\")) { sPath = string.Concat(sPath, "\\"); }

                    //récupération des fichiers sur FTP si besoin
                    if (IsWorkingDirectoryAnFTPURL || IsWorkingDirectoryAnSFTPURL)
                    {
                        string sPathAndFileName = "";
                        FTPTools FTP = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        if (sAdditionnalPath.Length > 0) { FTP.FTPVariables.AlterDistantPath(sAdditionnalPath); }

                        if (JobParameters.FileSourceZippedIn.Length > 0)
                        {
                            sPathAndFileName = FTP.GetFileFromFTPOrSFTPPath(Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.FileSourceZippedIn, JobParameters.DynParams), FuzibleQuery);
                            sPathAndFileName = UnzipFile(sFilename, sPathAndFileName, FuzibleQuery);
                        }
                        else { sPathAndFileName = FTP.GetFileFromFTPOrSFTPPath(sFilename, FuzibleQuery); }

                        sFilename = Path.GetFileName(sPathAndFileName);
                        sPath = sPathAndFileName[..(sPathAndFileName.LastIndexOf("\\") + 1)];
                    }
                    else
                    {
                        if (JobParameters.FileSourceZippedIn.Length > 0 && File.Exists(sPath + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.FileSourceZippedIn, JobParameters.DynParams)))
                        {
                            string sPathAndFileName = UnzipFile(sFilename, sPath + Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.FileSourceZippedIn, JobParameters.DynParams), FuzibleQuery);
                            if (sPathAndFileName.Length > 1 && sPathAndFileName.LastIndexOf("\\") > 0)
                            {
                                sFilename = sPathAndFileName[(sPathAndFileName.LastIndexOf("\\") + 1)..];
                            }
                        }
                    }

                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                          string.Concat("T00", " File Format Autodetection"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                    //Check de type fichier
                    bDD = AutodetectFileType(bDD, sPath, sFilename);

                    try
                    {
                        switch (bDD)
                        {
                            case SQLTools_Enums.BDD.FI_CSV:
                                if (iTable == 0)
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        dsData.Tables.Add(GetRawFileData(sPath, sFilename, FuzibleQuery).Result.Tables[0].Copy());
                                    }
                                    else { dsData.Tables.Add(GetDtFromCSVArray(sPath, sFilename, FuzibleQuery, iTable)); }
                                }
                                else
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        dsJoined.Tables.Add(GetRawFileData(sPath, sFilename, FuzibleQuery).Result.Tables[0].Copy());
                                    }
                                    else
                                    {
                                        dsJoined.Tables.Add(GetDtFromCSVArray(sPath, sFilename, FuzibleQuery, iTable));
                                    }
                                }
                                break;
                            case SQLTools_Enums.BDD.FI_XLS:
                                if (iTable == 0)
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsData = GetRawFileData(sPath, sFilename, Q).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }
                                    }
                                    else
                                    {
                                        dsData = GetDsFromXLS(sPath, sFilename, FuzibleQuery, iTable);
                                    }
                                }
                                else
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsJoined = GetRawFileData(sPath, sFilename, Q).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }
                                    }
                                    else
                                    {
                                        dsJoined = GetDsFromXLS(sPath, sFilename, FuzibleQuery, iTable);
                                    }
                                }
                                break;
                            case SQLTools_Enums.BDD.FI_XML:
                                if (iTable == 0)
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsData = GetRawFileData(sPath, sFilename, Q).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }
                                    }
                                    else
                                    {
                                        dsData = GetDsFromXML(sPath, sFilename, FuzibleQuery);
                                    }
                                }
                                else
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsJoined = GetRawFileData(sPath, sFilename, Q).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }
                                    }
                                    else
                                    {
                                        dsJoined = GetDsFromXML(sPath, sFilename, FuzibleQuery);
                                    }
                                }
                                break;
                            case SQLTools_Enums.BDD.FI_JSON:
                                if (iTable == 0)
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsData = GetRawFileData(sPath, sFilename, Q).Result);
                                    }
                                    else
                                    {
                                        Query q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsData = GetDsFromJSON(sPath, sFilename, q, iTable).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }

                                    }
                                }
                                else
                                {
                                    if (JobParameters.FileRawOutput || JobParameters.WebserviceRawOutput)
                                    {
                                        Query Q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsJoined = GetRawFileData(sPath, sFilename, Q).Result);
                                    }
                                    else
                                    {
                                        Query q = FuzibleQuery.DeepCopy();
                                        Task t = Task.Factory.StartNew(() => dsJoined = GetDsFromJSON(sPath, sFilename, q, iTable).Result);
                                        while (!t.IsCompleted)
                                        {
                                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                            Thread.Sleep(100);
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    { throw; }


                    if (iTable == 0) { if (dsData.Tables.Count > 0) { dsData.Tables[0].Namespace = qT.Alias; } }
                    if (iTable > 0) { if (dsJoined.Tables.Count > 0) { dsJoined.Tables[0].Namespace = qT.Alias; } }

                    if (IsWorkingDirectoryAnFTPURL || IsWorkingDirectoryAnSFTPURL)
                    {
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty(sAdditionnalPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_FTP_PROCESSED, sFilename, false);
                    }
                }
                if (iTable > 0)
                {
                    DataTable dtJoinedTables = null;
                    SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                    dtJoinedTables = SHS.JoinDatatablesFromPseudoQuery(FuzibleQuery, dsData.Tables[FuzibleQuery.QueryAnalyzer.SourceTableToHandle], dsJoined.Tables[FuzibleQuery.QueryAnalyzer.SourceTableToHandle], iTable);

                    if (dtJoinedTables != null)
                    {
                        dsData.Clear();
                        dsData = new DataSet();
                        dsData.Tables.Add(dtJoinedTables.Copy());
                        dsJoined.Clear();
                        dsJoined = null;
                        dtJoinedTables.Clear();
                        dtJoinedTables = null;
                    }
                }
            }
            dsData.CaseSensitive = false;
            return dsData;

        }

        public List<string> BuildFileFromDs(DataTable dtData, string sFilename, Query FuzibleQuery, bool bFirstPass = false)
        {
            List<string> sReturnFiles = new List<string>();

            if (dtData.Rows.Count > 0)
            {
                string sAdditionnalPath = "";
                string sNomFichierFinal = "";
                string sModifiedWorkingDirectory = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.ConnectionString_Target.SConnString(JobParameters.DynParams), JobParameters.DynParams, sFilename);

                if (sFilename.LastIndexOf("\\") > 0) //on peut avoir crée un output qui contient un subdir (ex : \OUTPUT\coucou.csv:select...
                {
                    if (sFilename.StartsWith("\\")) { sFilename = sFilename[1..]; }
                    sAdditionnalPath = sFilename[..sFilename.LastIndexOf("\\")];
                    sModifiedWorkingDirectory = string.Concat(sModifiedWorkingDirectory, sAdditionnalPath);
                    sFilename = sFilename[(sFilename.LastIndexOf("\\") + 1)..];
                }
                else if (sFilename.LastIndexOf("/") > 0) //on peut avoir crée un output qui contient un subdir (ex : /OUTPUT/coucou.csv:select...
                {
                    if (sFilename.StartsWith("/")) { sFilename = sFilename[1..]; }
                    sAdditionnalPath = sFilename[..sFilename.LastIndexOf("/")];
                    sModifiedWorkingDirectory = string.Concat(sModifiedWorkingDirectory, sAdditionnalPath);
                    sFilename = sFilename[(sFilename.LastIndexOf("/") + 1)..];
                }

                //modification dynamique du répertoire TARGET si la commande {QUERYTARGETNAME} a été invoquée dans la chaîne de connexion
                if (IsWorkingDirectoryAnFTPURL || IsWorkingDirectoryAnSFTPURL)
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        sModifiedWorkingDirectory = Path_Exported_Files;
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_intermediatepathsftp, Path_Exported_Files));
                    }
                }
                else
                {
                    if (!sModifiedWorkingDirectory.EndsWith("\\")) { sModifiedWorkingDirectory += "\\"; }
                    if (!Directory.Exists(sModifiedWorkingDirectory))
                    {

                        if (!JobParameters.RunInSimulationMode)
                        {
                            try { Directory.CreateDirectory(sModifiedWorkingDirectory); } catch (Exception) { throw; }

                        }
                        else
                        {
                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatelocalpath, sModifiedWorkingDirectory));
                        }
                    }
                }

                //string sNomDatatable = dsData.Tables[numTable].TableName;

                try
                {
                    sNomFichierFinal = string.Concat(sModifiedWorkingDirectory, sFilename);

                    //extraction des données binaires
                    if (!JobParameters.FileRawOutput && !JobParameters.WebserviceRawOutput && dtData.Columns.Cast<DataColumn>().FirstOrDefault(col => col.DataType.Equals(Type.GetType("System.Byte[]"))) != null)
                    {
                        DataColumn dt = dtData.Columns.Cast<DataColumn>().FirstOrDefault(col => col.DataType.Equals(Type.GetType("System.Byte[]")));
                        for (int iR = 0; iR < dtData.Rows.Count; iR++)
                        {
                            //PARTI PRIS !!!!!!!!  TODO ???
                            //attention, là je suis très clair, je vais créer un index au fichier, qui n'est pas forcément désiré
                            //on part du principe que si il y a plusieurs colonnes, donc avec des données "standard", on doit produire à la fois les varbinary et le fichier final
                            //sinon, on produit des fichiers avec index pour éviter que l'extraction postérieure n'écrase le fichier varbinary
                            sReturnFiles = CreateRAWFile(dtData, iR, false, dt.Ordinal, sNomFichierFinal, FuzibleQuery);
                        }
                    }

                    //extraction brutes des données d'un Webservice si demandé
                    if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("WS") && JobParameters.WebserviceRawOutput)
                    {
                        try
                        {
                            for (int iR = 0; iR < dtData.Rows.Count; iR++)
                            {
                                sReturnFiles = CreateRAWFile(dtData, iR, false, 1, sNomFichierFinal, FuzibleQuery);
                            }
                            //File.WriteAllText(sNomFichierFinal, dtData.Rows[0][0].ToString(), Encoding.UTF8);
                            //sReturnFile = sNomFichierFinal;
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }

                    }
                    else if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("FI") && JobParameters.FileRawOutput)
                    {
                        //dans ce mode, je sais que c'est systématiquement la première ligne
                        for (int iR = 0; iR < dtData.Rows.Count; iR++)
                        {
                            sReturnFiles = CreateRAWFile(dtData, iR, false, 1, sNomFichierFinal, FuzibleQuery);
                        }
                    }
                    else
                    {
                        switch (JobParameters.ConnectionString_Target.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.FI_CSV:
                                sReturnFiles = CreateCSVFile(dtData, sNomFichierFinal, FuzibleQuery);
                                break;

                            case SQLTools_Enums.BDD.FI_XLS:
                                sReturnFiles = CreateExcelWorkbook(dtData, sNomFichierFinal, bFirstPass, FuzibleQuery);
                                break;

                            case SQLTools_Enums.BDD.FI_JSON:
                                sReturnFiles = CreateJSONFile(dtData, sNomFichierFinal, FuzibleQuery);
                                break;

                            case SQLTools_Enums.BDD.FI_XML:
                                sReturnFiles = CreateXMLFile(dtData, sNomFichierFinal, FuzibleQuery);
                                break;
                        }

                    }

                    if (IsWorkingDirectoryAnFTPURL)
                    {
                        if (!JobParameters.RunInSimulationMode)
                        {
                            //montée du fichier sur FTP
                            FTPTools FTP = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                            if (sAdditionnalPath.Length > 0) { FTP.FTPVariables.AlterDistantPath(sAdditionnalPath); }

                            foreach (var file in sReturnFiles)
                            {
                                FTP.SendFileInFTPOrSFTPPath(file, FuzibleQuery);
                            }
                        }
                        else
                        {
                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willuploadtoftp));
                        }
                    }
                    else if (IsWorkingDirectoryAnSFTPURL)
                    {
                        if (!JobParameters.RunInSimulationMode)
                        {
                            FTPTools FTP = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                            if (sAdditionnalPath.Length > 0) { FTP.FTPVariables.AlterDistantPath(sAdditionnalPath); }

                            foreach (var file in sReturnFiles)
                            {
                                FTP.SendFileInFTPOrSFTPPath(file, FuzibleQuery);
                            }
                        }
                        else
                        {
                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willuploadtosftp));
                        }
                    }


                    //intervention sur fichier final (ZIP, déplacer...)
                    //PostWorkWithProcessedFiles(new List<string> { sNomFichierFinal }, sModifiedWorkingDirectory, INIP.FileCleanup_Import);

                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_copy_norows, SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            return sReturnFiles;

        }

        public List<string> GetListStringFromFilesInDirectory(string sPath, Query FuzibleQuery, string sExtension = "", bool withPath = false)
        {



            List<string> sListeFichiers = new();
            string[] sListeArray;

            try
            {

                if (Directory.Exists(sPath))
                {
                    if (sExtension.Equals(""))
                    {
                        sListeArray = Directory.GetFiles(sPath);
                    }
                    else
                    {
                        sListeArray = Directory.GetFiles(sPath, "*." + sExtension);
                    }

                    foreach (string str in sListeArray)
                    {
                        if (withPath)
                        {
                            sListeFichiers.Add(str);
                        }
                        else
                        {
                            sListeFichiers.Add(Path.GetFileName(str));
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sListeFichiers;

        }

        public string ExecuteProcess(string sCommandToExecute, bool bGetStatusMessage)
        {
            int iProcessTimeout = JobParameters.GlobalParameters.SHELL_OPERATIONS_Fuzible_TIMEOUT * 60000; // 3600000; //1h
            string sReturn = "";

            try
            {
                string sMessage = "";

                Process process = new();
                StringBuilder outputStringBuilder = new();
                try
                {
                    //string[] sSplitCommand = sCommandToExecute.Split(Convert.ToChar("/"));
                    if (sCommandToExecute.ToUpper().EndsWith(".BAT") || sCommandToExecute.ToUpper().EndsWith(".CMD") || sCommandToExecute.ToUpper().EndsWith(".EXE") || sCommandToExecute.ToUpper().EndsWith(".COM"))
                    {
                        if (sCommandToExecute.IndexOf("/") > -1)
                        {
                            process.StartInfo.FileName = sCommandToExecute[..sCommandToExecute.IndexOf("/")];
                        }
                        else { process.StartInfo.FileName = sCommandToExecute; }
                        if (sCommandToExecute.IndexOf("/") > -1)
                        {
                            process.StartInfo.Arguments = sCommandToExecute[(sCommandToExecute.IndexOf("/") + 1)..];
                        }

                        process.StartInfo.WorkingDirectory = Path_Exported_Files;
                    }
                    else //c'est un appel de commande et non pas un fichier à lancer
                    {
                        process.StartInfo.FileName = "cmd.exe";
                        process.StartInfo.Arguments = "/C " + sCommandToExecute;
                    }

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_cmd_command, process.StartInfo.FileName, Languages.Languages.fi_cmd_args, process.StartInfo.Arguments), SQLTools_Enums.LOG_TYPEINFO.INF);

                    Encoding enc = Encoding.UTF8;
                    try
                    {
                        //System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                        //enc = Encoding.GetEncoding(GetCmdCodePage());
                        enc = Encoding.UTF8;
                    }
                    catch (Exception)
                    {
                        enc = Encoding.UTF8;
                    }

                    //process.StartInfo.Arguments = sCommandToRun[1].Split(Convert.ToChar("|"))[1];
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.StandardErrorEncoding = enc;
                    process.StartInfo.StandardOutputEncoding = enc;

                    process.EnableRaisingEvents = false;
                    process.OutputDataReceived += (sender, eventArgs) => GetMessagesFromExecutedProcess(eventArgs, ref outputStringBuilder); // je me sers des "|" pour découper en plusieurs lignes dans l'application client
                    process.ErrorDataReceived += (sender, eventArgs) => GetMessagesFromExecutedProcess(eventArgs, ref outputStringBuilder);
                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    bool processExited = process.WaitForExit(iProcessTimeout);

                    if (processExited == false) // we timed out...
                    {
                        process.Kill();
                        sMessage = Languages.Languages.fi_cmd_killedtimeout;
                        if (bGetStatusMessage) { outputStringBuilder.Append(sMessage + "|"); }
                        Exception ex = new(sMessage);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                        //FuzibleQuery.QueryErrors += 1;
                    }
                    else if (process.ExitCode != 0)
                    {
                        sMessage = Languages.Languages.fi_cmd_exitcode + process.ExitCode;
                        if (bGetStatusMessage)
                        {
                            outputStringBuilder.Append(sMessage + "|");
                        }
                        Exception ex = new(sMessage);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                        //FuzibleQuery.QueryErrors += 1;
                    }
                    else
                    {
                        sMessage = Languages.Languages.fi_cmd_success;
                        if (bGetStatusMessage)
                        {
                            outputStringBuilder.Append(sMessage + "|");
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessage, SQLTools_Enums.LOG_TYPEINFO.INF);
                    }
                }
                finally
                {
                    process.Close();
                    sReturn = outputStringBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                //FuzibleQuery.QueryErrors += 1;
            }
            return sReturn;
        }

        public List<string> GetAllFilesFromPath(string sFilter, bool bWithSubDirs)
        {
            string sPath = Connection.SConnString(JobParameters.DynParams);
            List<string> sListFiles = new();
            if (!JobParameters.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Contains("FTP", StringComparison.CurrentCulture))
            {
                if (Directory.Exists(sPath))
                {
                    string[] sFiles = Directory.GetFiles(sPath, "*.*", SearchOption.AllDirectories);
                    foreach (string sF in sFiles)
                    {
                        if (sFilter.Length > 0)
                        {
                            if (Path.GetFileName(sF).IndexOf(sFilter, StringComparison.OrdinalIgnoreCase) > -1)
                            {
                                sListFiles.Add(sF.Replace(sPath, ""));
                            }
                        }
                        else
                        {
                            sListFiles.Add(sF.Replace(sPath, ""));
                        }
                    }
                }
            }
            else
            {
                FTPTools FTP = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                List<string[]> sFiles = FTP.ListFilesFromFTPOrSMTPPath(bWithSubDirs);
                foreach (string[] sF in sFiles)
                {
                    if (sFilter.Length > 0)
                    {
                        if (sF[0].IndexOf(sFilter, StringComparison.OrdinalIgnoreCase) > -1) { sListFiles.Add(sF[0]); }
                    }
                    else { sListFiles.Add(sF[0]); }
                }
            }
            return sListFiles;
        }

        public class EncryptionSystem
        {
            public static string AES_Encrypt(string input, string pass)
            {
                string encrypted = "";
                if (input.Length > 0)
                {
                    try
                    {
                        var AES = System.Security.Cryptography.Aes.Create("AesManaged");
                        var Hash_AES = System.Security.Cryptography.MD5.Create();
                        //System.Security.Cryptography.RijndaelManaged AES = new System.Security.Cryptography.RijndaelManaged();
                        //System.Security.Cryptography.MD5CryptoServiceProvider Hash_AES = new System.Security.Cryptography.MD5CryptoServiceProvider();
                        System.Byte[] hash = new byte[32];
                        System.Byte[] temp = Hash_AES.ComputeHash(System.Text.ASCIIEncoding.UTF8.GetBytes(pass));
                        Array.Copy(temp, 0, hash, 0, 16);
                        Array.Copy(temp, 0, hash, 15, 16);
                        AES.Key = hash;
                        AES.Mode = System.Security.Cryptography.CipherMode.ECB;
                        System.Security.Cryptography.ICryptoTransform DESEncrypter = AES.CreateEncryptor();
                        System.Byte[] Buffer = System.Text.ASCIIEncoding.UTF8.GetBytes(input);
                        encrypted = Convert.ToBase64String(DESEncrypter.TransformFinalBlock(Buffer, 0, Buffer.Length));
                    }
                    catch (Exception ex)
                    {
                        LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG, true);
                    }
                }
                return encrypted;
            }

            public static string AES_Decrypt(string input, string pass)
            {
                string decrypted = "";

                if (input.Length > 0)
                {
                    try
                    {
                        var AES = System.Security.Cryptography.Aes.Create("AesManaged");
                        var Hash_AES = System.Security.Cryptography.MD5.Create();
                        //System.Security.Cryptography.RijndaelManaged AES = new System.Security.Cryptography.RijndaelManaged();
                        //System.Security.Cryptography.MD5CryptoServiceProvider Hash_AES = new System.Security.Cryptography.MD5CryptoServiceProvider();
                        System.Byte[] hash = new byte[32];
                        System.Byte[] temp = Hash_AES.ComputeHash(System.Text.ASCIIEncoding.UTF8.GetBytes(pass));
                        Array.Copy(temp, 0, hash, 0, 16);
                        Array.Copy(temp, 0, hash, 15, 16);
                        AES.Key = hash;
                        AES.Mode = System.Security.Cryptography.CipherMode.ECB;
                        System.Security.Cryptography.ICryptoTransform DESDecrypter = AES.CreateDecryptor();
                        System.Byte[] Buffer = Convert.FromBase64String(input);
                        decrypted = System.Text.ASCIIEncoding.UTF8.GetString(DESDecrypter.TransformFinalBlock(Buffer, 0, Buffer.Length));
                    }
                    catch (Exception ex)
                    {
                        LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG, true);
                    }
                }
                return decrypted;
            }

            //public static string GetVolumeSerial(string sDiskName)

            //{

            //    string sSerial = "0";


            //    try
            //    {

            //        ManagementObject dsk = new ManagementObject(@"win32_logicaldisk.deviceid=""" + sDiskName + @":""");
            //        dsk.Get();
            //        sSerial = dsk["VolumeSerialNumber"].ToString();

            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex.Message);
            //    }

            //    return sSerial;

            //    //ras le bol de gérer le password en fonction du disque donc j'ai pris le serial de SRVAPPETL1 et puis c'est tout!
            //    //return "0296953E";

            //}

        }

        #endregion

        #region "PRIVATE VOID"

        private string UnzipFile(string sOriginalFile, string sPathAndFileName, Query FuzibleQuery)
        {
            bool bFound = false;
            string sP = sPathAndFileName[..(sPathAndFileName.LastIndexOf("\\") + 1)];

            if (sPathAndFileName.Length > 0 && sPathAndFileName.LastIndexOf("\\") > 0)
            {
                try
                {
                    if (File.Exists(sP + sOriginalFile))
                    {
                        try { File.Move(sP + sOriginalFile, sP + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + sOriginalFile); }
                        catch { }
                    }
                    ZipFile.ExtractToDirectory(sPathAndFileName, sP);
                    foreach (string s in Directory.GetFiles(sP))
                    {
                        if (sOriginalFile.StartsWith(Path.GetFileNameWithoutExtension(s), StringComparison.InvariantCultureIgnoreCase))
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_zip_sourcefound + Path.GetFileName(sPathAndFileName), SQLTools_Enums.LOG_TYPEINFO.DET);
                            sPathAndFileName = s;
                            bFound = true;
                            break;
                        }
                    }
                }
                catch (Exception ex) { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG); };
            }
            if (!bFound)
            {
                if (File.Exists(sP + sOriginalFile))
                {
                    bFound = true;
                    sPathAndFileName = sP + sOriginalFile;
                }
            }
            if (!bFound)
            {
                Exception ex = new(Languages.Languages.fi_zip_nofilefound);
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sPathAndFileName, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sPathAndFileName;
        }

        private string[] ConvertDtIntoArray(DataTable dtData)
        {
            int iRows = dtData.Rows.Count;
            string[] sData = new string[iRows + 1];

            StringBuilder sbEntete = new();
            foreach (DataColumn dc in dtData.Columns)
            {
                sbEntete.Append(string.Concat(dc.ColumnName, JobParameters.CSVCharSeparator_Target));
            }
            sData[0] = sbEntete.ToString()[..(sbEntete.Length - 1)];

            for (int iR = 0; iR < dtData.Rows.Count; iR++)
            {
                sData[iR + 1] = string.Join(JobParameters.CSVCharSeparator_Target, dtData.Rows[iR].ItemArray);
            }

            return sData;
        }

        private void SynchroModeStoreUpdateDeleteChanges(string sUorD, DataTable dtData, string sTargetFile, Query FuzibleQuery)
        {
            sTargetFile = string.Concat("BACK_", sTargetFile);

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_synchro_store01, sUorD, " - ", dtData.Rows.Count.ToString(), Languages.Languages.fi_synchro_store02, sTargetFile), SQLTools_Enums.LOG_TYPEINFO.INF);

            //ajout colonne ID ligne dans dataset
            string sRecordIDCol = JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN;
            string sRecordTagCol = JobParameters.GlobalParameters.SQL_SYNCHRO_TAG_COLUMN;
            long sRecordIDValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            dtData.Columns.Add(sRecordIDCol, Type.GetType("System.Int32"));
            dtData.Columns.Add(sRecordTagCol, Type.GetType("System.String"));
            foreach (DataRow dt in dtData.Rows)
            {
                dt[sRecordIDCol] = sRecordIDValue; dt[sRecordTagCol] = sUorD;
            }

            bool bAppend = JobParameters.AppendFileCreation;
            JobParameters.AppendFileCreation = true; //je force l'écriture à la suite
            dtData.Namespace = "SynchroBackup_" + sRecordIDValue.ToString();
            BuildFileFromDs(dtData, sTargetFile, FuzibleQuery);
            JobParameters.AppendFileCreation = bAppend;
        }

        private DataSet GetDsFromXLS(string sPath, string sFile, Query FuzibleQuery, int iTable)
        {
            DataSet dsData = new();
            string sSeparator = "\";\"";
            string[] sCSVFromExcel = null;

            if (File.Exists(sPath + sFile))
            {
                try
                {
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                    FileStream stream = File.Open(string.Concat(sPath + sFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    IExcelDataReader excelReader;

                    ExcelReaderConfiguration xlsConfig = new();

                    if (JobParameters.XLSPasswordSource.Length > 0) { xlsConfig.Password = JobParameters.XLSPasswordSource; }

                    if (Path.GetExtension(sPath + sFile).ToUpper().Equals(".XLS"))
                    {
                        try { excelReader = ExcelReaderFactory.CreateBinaryReader(stream, xlsConfig); }
                        catch { excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream, xlsConfig); }
                    }
                    else
                    {
                        try { excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream, xlsConfig); }
                        catch { excelReader = ExcelReaderFactory.CreateBinaryReader(stream, xlsConfig); }
                    }

                    int iSheets = excelReader.ResultsCount;
                    int iSheet = 0;
                    int iStop = 0;

                    if (JobParameters.XLSSheetToRead == 0)
                    {
                        iStop = iSheets; iSheet = 0;
                    }
                    else { iStop = JobParameters.XLSSheetToRead; iSheet = JobParameters.XLSSheetToRead - 1; }

                    for (int iS = iSheet; iSheet < iStop; iSheet++)
                    {
                        excelReader.Reset();
                        for (int iSb = 0; iSb < iSheet; iSb++)
                        {
                            excelReader.NextResult();
                        }

                        string sSheetName = excelReader.Name;

                        try
                        {
                            int iFile = -1;
                            int iRecord = excelReader.RowCount;
                            int iFieldCount = excelReader.FieldCount;
                            sCSVFromExcel = new string[iRecord];
                            List<object[]> sRecords = new();

                            int iRowsLimit = FuzibleQuery.QueryAnalyzer.LimitedResults > 0 ? FuzibleQuery.QueryAnalyzer.LimitedResults : 999999999;

                            while (excelReader.Read())
                            {
                                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                                iFile++;
                                if (iFile >= iRowsLimit) { break; }

                                object[] sRecord = new object[iFieldCount];
                                for (int iF = 0; iF < iFieldCount; iF++)
                                {
                                    sRecord[iF] = excelReader.GetValue(iF);
                                }
                                sRecords.Add(sRecord);
                            }

                            int iStartCol = 0;
                            int iEndCol = iFieldCount;
                            int iStartRow = 0 + JobParameters.XLSRowOffset;
                            int iEndRow = iRecord - JobParameters.XLSRowOffset;

                            int iColumns = iStartCol + iEndCol;

                            bool[] bEmptyCol = new bool[iColumns];
                            for (int iB = 0; iB < bEmptyCol.Length; iB++) { bEmptyCol[iB] = true; }

                            int iColLoop = -1;
                            //boucle colonnes
                            for (int j = iStartCol; j < iEndCol; j++)
                            {
                                iColLoop++;
                                //analyse des colonnes vides
                                for (int i = iStartRow; i < iEndRow; i++)
                                {
                                    try
                                    {
                                        object excelCell = sRecords[i][j];
                                        //analyse des colonnes vides (offset colonnes)
                                        if (excelCell != null && excelCell.ToString().Length > 0 && bEmptyCol[iColLoop])
                                        {
                                            bEmptyCol[iColLoop] = false;
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.fi_xls_cantbeconverted01, sFile, Languages.Languages.fi_xls_cantreadatposition, " : ", i.ToString(), "-" + j.ToString(), ex.Message), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        break;
                                    }
                                }
                            }

                            //gestion de la limite
                            if (iEndRow > iRowsLimit) { iEndRow = iRowsLimit; }

                            int iRows = iStartRow + iEndRow;
                            sCSVFromExcel = new string[iRows];

                            int iEmpty = 0;
                            foreach (bool b in bEmptyCol)
                            {
                                if (b) { iEmpty++; }
                            }

                            //boucle lignes
                            for (int i = iStartRow; i < iEndRow; i++)
                            {
                                string[] sRow = new string[iColumns - iEmpty];

                                iRows++;
                                iColLoop = 0;
                                //boucle colonnes
                                for (int j = iStartCol; j < iEndCol; j++)
                                {
                                    object excelCell = sRecords[i][j];

                                    //add cell value to the datatable
                                    if (excelCell != null && (!bEmptyCol[j]))
                                    {
                                        string sData = excelCell.ToString();

                                        if (sData.Length > 1)
                                        {
                                            if (sData.EndsWith("\"") && sData.Split("\"").Length - 1 == 1) //Regex.IsMatch(sData[0..^2], "[^\"]\"")) 
                                            {
                                                sData = string.Concat(sData[0..^1], "'");
                                            }
                                        }//bug de mon interprétateur CSV qui ne sait pas gérer une valeur avec un '"' au bout
                                        else if (sData.Length == 1 && sData.Equals("\""))
                                        {
                                            sData = "'";
                                        }
                                        sRow[iColLoop] = sData;
                                    }

                                    if (!bEmptyCol[j])
                                    {
                                        iColLoop++;
                                    }
                                }

                                if (string.Join("", sRow).Length > 0)
                                {
                                    sCSVFromExcel[i] = string.Concat("\"", string.Join(sSeparator, sRow), "\"");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_cantbeconverted01, sFile, Languages.Languages.fi_xls_cantbeconverted02, ex.Message), FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }

                        if (sCSVFromExcel != null)
                        {
                            sSeparator = sSeparator.Substring(1, 1);
                            //nettoyage des lignes à vide
                            sCSVFromExcel = sCSVFromExcel.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                            SHSOperations SHSA = new(JobParameters, FuzibleQuery, ref MyLog); //analyse CSV pour savoir si la première ligne = entête
                            bool bFirstRowHeaderA = SHSA.IsFirstRowHeader(sCSVFromExcel, sSeparator);
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty(sFile, SQLTools_Enums.QUERY_PROPERTIES.CSV_HASHEADER, bFirstRowHeaderA.ToString(), true);

                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_xls_firstrowheader + bFirstRowHeaderA.ToString(), SQLTools_Enums.LOG_TYPEINFO.DET);

                            List<SQLColumn> sListeTypeColonnesSourceA = new();
                            sListeTypeColonnesSourceA = GetListFieldsTypesFromFile(sCSVFromExcel, sFile, sSeparator, bFirstRowHeaderA, FuzibleQuery, iTable);

                            SHSOperations SHSa = new(JobParameters, FuzibleQuery, ref MyLog);
                            DataTable dtTableA = SHSa.FillDtFromCSVArray(sCSVFromExcel, sListeTypeColonnesSourceA, sSeparator, FuzibleQuery.QueryAnalyzer.Tables[0].Name, bFirstRowHeaderA, FuzibleQuery);
                            dtTableA.TableName = sSheetName;
                            dsData.DataSetName = FuzibleQuery.QueryAnalyzer.Tables[0].Name;
                            sCSVFromExcel = null;

                            dsData.Tables.Add(dtTableA);
                        }
                    }
                    excelReader.Close();

                    FuzibleQuery.QueryAnalyzer.AddQueryProperty(sPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED, sPath + sFile, false);
                    //PostWorkWithProcessedFiles(new List<string> { sPath + sFile }, sPath);

                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat("File ", sFile, " Can't be Read !"), FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("File ", sFile, " Can't be Read !"), SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            return dsData;
        }

        private DataSet GetDsFromXML(string sPath, string sFilename, Query sQ)
        {

            DataSet dsData = new();

            if (File.Exists(sPath + sFilename))
            {
                try
                {
                    string sData = File.ReadAllText(sPath + sFilename, Encoding.UTF8);
                    XmlReader xml = XmlReader.Create(new StringReader(sData));
                    dsData.ReadXml(xml, XmlReadMode.Auto);
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xmljson_cantberead01, sFilename, Languages.Languages.fi_xmljson_cantberead02, ex.Message), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                if (dsData.Tables.Count > 0)
                {
                    dsData = Toolbox.MergeAndSortDataTables(dsData);
                    dsData.Tables[0].TableName = sQ.QueryAnalyzer.Tables[0].Name;
                    //dsData.Tables[0] = SQLTools_Utils.FilterDataTableWithPseudoQuery (dsData.Tables[0] , sPseudoRequete);
                    sQ.QueryAnalyzer.AddQueryProperty(sPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED, sPath + sFilename, false);
                }
                //PostWorkWithProcessedFiles(new List<string> { sPath + sFilename }, sPath);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xmljson_cantberead01, sFilename, Languages.Languages.fi_xmljson_cantberead02), SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            return dsData;
        }

        private async Task<DataSet> GetDsFromJSON(string sPath, string sFilename, Query sQ, int iTable)
        {
            DataSet dsData = new();

            if (File.Exists(sPath + sFilename))
            {
                string sJson = File.ReadAllText(sPath + sFilename);
                try
                {
                    if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                    {
                        dsData = DataJson.jsonToDataSet(sJson);
                    }
                    else
                    {
                        JsonParser Jsp = new(sJson, sQ.QueryAnalyzer.Tables[iTable].Alias)
                        {
                            DepthToGet = 0,
                            AvoidSpecialCharsInColumnNames = JobParameters.GlobalParameters.JSON_PARSER_REPLACE_SPECIAL_CHARS
                        };

                        Jsp.OnJsonEvent += Event_JsonParser;
                        dsData = await Jsp.JsonToDataSetAsync(Monitoring.TaskCancellationToken);

                        sQ.QueryAnalyzer.AddQueryProperty(sPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED, sPath + sFilename, false);
                    }
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xmljson_cantberead01, sFilename, Languages.Languages.fi_xmljson_cantberead02, ex.Message), SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                //if (dsData.Tables.Count > 0)
                //{
                //    dsData.Tables[0].TableName = sQ.QueryAnalyzer.Tables[0].Name;
                //    //dsData.Tables[0] = SQLTools_Utils.FilterDataTableWithPseudoQuery (dsData.Tables[0] , sPseudoRequete);
                //}
                //PostWorkWithProcessedFiles(new List<string> { sPath + sFilename }, sPath);
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xmljson_cantberead01, sFilename, Languages.Languages.fi_xmljson_cantberead02), SQLTools_Enums.LOG_TYPEINFO.WNG);
            }
            return dsData;
        }

        private void Event_JsonParser(object sender, string sInfo)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, sInfo, SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        private DataTable GetDtFromCSVArray(string sPath, string sFilename, Query FuzibleQuery, int iTable)
        {
            //
            if (MyLog.DebugFunctions)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                string.Concat("T00", " Getting CSV Data : " + sFilename), SQLTools_Enums.LOG_TYPEINFO.DBG);
            }
            //

            int iRowsLimit = FuzibleQuery.QueryAnalyzer.LimitedResults > 0 ? FuzibleQuery.QueryAnalyzer.LimitedResults : 999999999;
            string sCSVCharSeparatorA;
            bool bFirstRowHeaderA;

            //
            if (MyLog.DebugFunctions)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                string.Concat("T00", " Checking for files with Pattern"), SQLTools_Enums.LOG_TYPEINFO.DBG);
            }
            //

            List<string> sFichiersSplitA = GetAllFilesFromSplitted(sPath, sFilename, FuzibleQuery);

            if (sFichiersSplitA.Count > 0)
            {
                string[] sFichierInRamA = null;
                int iFileCount = 0;

                if (File.Exists(sPath + sFilename))
                {
                    //on compte la quantité de lignes totale
                    if (JobParameters.IsRunning) //pas besoin de compter les lignes si c'est juste pour analyser un fichier (intellisense)
                    {
                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T00", " Streamreader : File OpenText to retrieve rows quantity : " + sFilename), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //

                        Encoding encode = DetectFileEncoding(sPath + sFilename);
                        CsvHelper csv = new CsvHelper(sPath + sFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, encode);

                        iFileCount = csv.CountRows();
                    }
                    else { iFileCount = iRowsLimit; }

                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat("T00", " Streamreader - Rows Count : " + iFileCount.ToString()), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat("T00", " Streamreader - Getting Chunk for Data Analysis : " + sFilename), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                    //on récupère un échantillon de données pour l'analyse séparateur + header
                    sFichierInRamA = GetOneFileInRamFromMultipleFiles(sFichiersSplitA, JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER, 0, "", FuzibleQuery);
                }

                try
                {
                    if (iFileCount > 0)
                    {
                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T00", " Performing File Analysis"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //

                        sCSVCharSeparatorA = Toolbox.CSVCharSeparator(sFichierInRamA, JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER, JobParameters.GlobalParameters.CSV_SEPARATORS); //recherche caractère séparateur
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty(sFilename, SQLTools_Enums.QUERY_PROPERTIES.CSV_SEPARATOR, sCSVCharSeparatorA, true);

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_csv_separatorA + "<" + sCSVCharSeparatorA.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\b", "\\b").Replace("\v", "\\v") + ">", SQLTools_Enums.LOG_TYPEINFO.DET);

                        SHSOperations SHSA = new(JobParameters, FuzibleQuery, ref MyLog); //analyse CSV pour savoir si la première ligne = entête
                        bFirstRowHeaderA = SHSA.IsFirstRowHeader(sFichierInRamA, sCSVCharSeparatorA);
                        FuzibleQuery.QueryAnalyzer.AddQueryProperty(sFilename, SQLTools_Enums.QUERY_PROPERTIES.CSV_HASHEADER, bFirstRowHeaderA.ToString(), true);


                        if (JobParameters.IsRunning && iFileCount > iRowsLimit)
                        {
                            iFileCount = iRowsLimit;
                        }

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_csv_headerA + bFirstRowHeaderA.ToString(), SQLTools_Enums.LOG_TYPEINFO.DET);

                        List<SQLColumn> sListeTypeColonnesSourceA;
                        sListeTypeColonnesSourceA = GetListFieldsTypesFromFile(sFichierInRamA, sFilename, sCSVCharSeparatorA, bFirstRowHeaderA, FuzibleQuery, iTable);

                        if (sListeTypeColonnesSourceA.Count > 0)
                        {
                            //recherche du nombre de traitements à effectuer selon le nombre de lignes contenues dans le fichier. Arrondi au dessus !
                            decimal dCount = iFileCount - JobParameters.CSVRowOffset;
                            decimal dQteInsert = Math.Ceiling(dCount / JobParameters.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT);
                            int iQteInsert = Convert.ToInt32(dQteInsert);
                            //Remplissage Dataset A
                            DataTable dtTableA = null;
                            string sEntete = null;
                            if (bFirstRowHeaderA) { sEntete = sFichierInRamA[0]; }

                            if (JobParameters.IsRunning && iQteInsert > 1) //insertion en multiples étapes pour épargner de la RAM
                            {
                                var tOperation = new List<SQLStreamingData>();
                                for (int iP = 0; iP < JobParameters.GlobalParameters.MULTITHREADING_CORES_BIGDATA; iP++)
                                {
                                    tOperation.Add(null);
                                }

                                for (int i = 0; i < iQteInsert; i++)
                                {
                                    DateTime dtStart = DateTime.Now;

                                    sFichierInRamA = GetOneFileInRamFromMultipleFiles(sFichiersSplitA, JobParameters.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT, i * JobParameters.GlobalParameters.CSV_MAXLINES_BEFORE_SPLIT, i == 0 ? "" : sEntete, FuzibleQuery);

                                    SHSOperations SHSa = new(JobParameters, FuzibleQuery, ref MyLog);
                                    dtTableA = SHSa.FillDtFromCSVArray(sFichierInRamA, sListeTypeColonnesSourceA, sCSVCharSeparatorA, sFilename, bFirstRowHeaderA, FuzibleQuery);
                                    dtTableA.Namespace = FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias;

                                    DateTime dtStop = DateTime.Now;

                                    while (Toolbox.GetAvailableTaskSlot(tOperation, i) == -1)
                                    {
                                        Thread.Sleep(100);
                                    }
                                    int iSlot = Toolbox.GetAvailableTaskSlot(tOperation, i);

                                    int iPos = 0;
                                    if (i > 0) { iPos = 1; }
                                    if (i == iQteInsert - 1) { iPos = 2; }
                                    DataSet dsData = new();
                                    dsData.Tables.Add(dtTableA);

                                    if (iPos < 2) //le dernier passage doit absolument attendre l'insert final avant d'aller plus loin
                                    {
                                        DataSet dsCopy = dsData.Copy();
                                        SQLStreamingData op = new SQLStreamingData((dtStop - dtStart).TotalMilliseconds, dsCopy.Tables[0].Rows.Count, new Task(() => MThread.RepSyncTask_DirectSQLStream(iSlot, iPos, iFileCount - JobParameters.CSVRowOffset - (bFirstRowHeaderA ? 1 : 0), dsCopy, JobParameters.DeepCopy(), FuzibleQuery.DeepCopy(), ref MyLog)));
                                        tOperation[iSlot] = op;
                                        tOperation[iSlot].StartOperation();
                                    }
                                    else
                                    {
                                        MThread.RepSyncTask_DirectSQLStream(1, iPos, iFileCount - JobParameters.CSVRowOffset - (bFirstRowHeaderA ? 1 : 0), dsData.Copy(), JobParameters.DeepCopy(), FuzibleQuery.DeepCopy(), ref MyLog);
                                        dsData.Tables.Remove(dtTableA);
                                        dsData.Clear();
                                        dsData = null;
                                        dtTableA.Clear();

                                        while (Toolbox.TasksNotFinished(tOperation)) { Thread.Sleep(100); }
                                    }
                                }
                                JobParameters.SQLDirectStream = true;
                            }
                            else
                            {
                                int iRange = iRowsLimit + (bFirstRowHeaderA ? 1 : 0) + JobParameters.CSVRowOffset;

                                if (iRowsLimit < sFichierInRamA.Length) //show source: on demande la limite de lignes
                                {
                                    sFichierInRamA = sFichierInRamA[0..iRange];
                                }
                                else if (iRowsLimit > sFichierInRamA.Length)
                                {
                                    sFichierInRamA = GetOneFileInRamFromMultipleFiles(sFichiersSplitA, iRange, 0, "", FuzibleQuery);
                                }
                                SHSOperations SHSa = new(JobParameters, FuzibleQuery, ref MyLog);
                                dtTableA = SHSa.FillDtFromCSVArray(sFichierInRamA, sListeTypeColonnesSourceA, sCSVCharSeparatorA, sFilename, bFirstRowHeaderA, FuzibleQuery);
                            }

                            //traitement des fichiers CSV

                            //PostWorkWithProcessedFiles(sFichiersSplitA, sPath);
                            foreach (string sF in sFichiersSplitA)
                            {
                                FuzibleQuery.QueryAnalyzer.AddQueryProperty(sPath, SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED, sF, false);
                            }

                            return dtTableA;
                        }
                        else
                        {
                            Exception ex = new(string.Concat(Languages.Languages.fi_csv_headercantberead, sFilename, ")"));
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.fi_csv_unabletoprocess, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                            return new DataTable(sFilename);
                        }
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_csv_file, sFilename, Languages.Languages.fi_csv_fileempty), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        return new DataTable(sFilename);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_csv_file, sFilename, Languages.Languages.fi_csv_filecantberead), SQLTools_Enums.LOG_TYPEINFO.WNG);
                return new DataTable(sFilename);
            }
        }

        public static void MoveListOfFiles(Job JobParameters, List<string> sListFiles, string sPath)
        {
            string sPa = JobParameters.GlobalParameters.FILE_PROCESSED_DIR.Length > 0 ? JobParameters.GlobalParameters.FILE_PROCESSED_DIR.Replace("\\", "") : "Processed";
            string Path_Processed_Files = string.Concat(sPa, "\\");

            if (!Directory.Exists(string.Concat(sPath, "\\", Path_Processed_Files)))
            {
                try
                {
                    Directory.CreateDirectory(string.Concat(sPath, "\\", Path_Processed_Files));
                }
                catch (Exception)
                {
                    throw;
                }
            }

            //nettoyage de litière
            if (JobParameters.GlobalParameters.FILE_DAYS_TO_KEEP_PROCESSED > 0)
            {
                if (Directory.Exists(string.Concat(sPath, "\\", Path_Processed_Files)))
                {
                    FileInfo fiI;

                    foreach (string sFile in Directory.GetFiles(string.Concat(sPath, "\\", Path_Processed_Files)))
                    {
                        try
                        {
                            fiI = new FileInfo(sFile);
                            if (fiI.CreationTime < DateTime.Now.AddDays(-JobParameters.GlobalParameters.FILE_DAYS_TO_KEEP_PROCESSED))
                            {
                                File.Delete(sFile);
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
            }

            foreach (string sFile in sListFiles)
            {
                try
                {
                    //string sFileMoved = string.Concat(sPath, "\\", PATH_MOVED_SOURCE_FILES, DateTime.Now.ToString("yyyyMMdd"), "_", Path.GetFileName(sFile));
                    string sFileMoved = "";
                    if (JobParameters.GlobalParameters.FILE_ADD_DATETIME_PREFIX)
                    {
                        sFileMoved = string.Concat(sPath, "\\", Path_Processed_Files, DateTime.Now.ToString("yyyyMMdd"), "_", Path.GetFileName(sFile));
                    }
                    else { sFileMoved = string.Concat(sPath, "\\", Path_Processed_Files, Path.GetFileName(sFile)); }
                    if (File.Exists(sFileMoved)) { File.Delete(sFileMoved); }
                    File.Move(sFile, sFileMoved);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private List<string> GetAllFilesFromSplitted(string sPath, string sFilename, Query FuzibleQuery)
        {
            string sOldFile = sFilename;

            string[] sSeparatingChars = { JobParameters.CSVMultipleFilesInOnePattern };

            List<string> sListeFichiers = new();
            string[] sListeArray;

            if (sFilename.Contains(JobParameters.CSVMultipleFilesInOnePattern))
            {
                try
                {
                    sFilename = sFilename.Split(sSeparatingChars, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                catch (Exception)
                {
                    //RAS
                }
            }
            else
            {
                //sFilename = Path.GetFileNameWithoutExtension(sFilename); changement juillet 2019 : trop emmerdant quand 2 fichiers portent le même nom
                sFilename = Path.GetFileName(sFilename);
            }

            try
            {
                if (sOldFile.LastIndexOf("\\") > 0)
                {
                    if (sOldFile.StartsWith("\\")) { sOldFile = sOldFile[1..]; }
                    sPath = string.Concat(sPath, sPath.EndsWith("\\") ? "" : "\\", sOldFile[..sOldFile.LastIndexOf("\\")]);
                }

                if (Directory.Exists(sPath))
                {
                    sListeArray = Directory.GetFiles(sPath);

                    string strCheck = "";

                    foreach (string str in sListeArray)
                    {
                        if (str.Contains(JobParameters.CSVMultipleFilesInOnePattern))
                        {
                            strCheck = Path.GetFileNameWithoutExtension(str);
                            strCheck = strCheck.Split(sSeparatingChars, StringSplitOptions.RemoveEmptyEntries)[0];
                        }
                        else { strCheck = Path.GetFileName(str); }

                        if (strCheck.Equals(sFilename, StringComparison.InvariantCultureIgnoreCase))
                        {
                            sListeFichiers.Add(str);
                        }

                    }

                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sListeFichiers;

        }

        private string[] GetOneFileInRamFromMultipleFiles(List<string> sFilesSplit, int iRowsLimit, int iStartsFrom, string sEntete, Query FuzibleQuery)
        {
            string[] sFileInRam = Array.Empty<string>();
            List<string> sListFileInRam = new();
            string[] sFileInRamTemp;
            int iOldLength;
            string sInput;

            if (sEntete.Length > 0) { sListFileInRam.Add(sEntete); }

            try
            {

                //méthode préférée de chargement quand il y a un seul fichier
                if (sFilesSplit.Count == 1)
                {
                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat("T00", " Detecting Encoding : ", sFilesSplit[0]), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                    Encoding encode = DetectFileEncoding(sFilesSplit[0]);

                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat("T00", " FileStream Open, Read, ReadWrite : ", sFilesSplit[0]), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                    var CsvHelper = new CsvHelper(sFilesSplit[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite, encode);

                    int iR = -1;
                    while (CsvHelper.Read())
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        iR++;

                        sInput = CsvHelper.ReadCSVLine();

                        if (iR >= iRowsLimit + iStartsFrom + 1) //j'ajoute arbitrairement un +1 en partant du principe qu'il y a un entête (mais sans certitude) ! 
                        { break; }

                        if (iR >= iStartsFrom)
                        {
                            if (!string.IsNullOrEmpty(sInput))
                            {
                                sListFileInRam.Add(sInput.Trim());
                            }
                        }
                    }
                    CsvHelper.Close();
                    sFileInRam = sListFileInRam.ToArray();
                    sListFileInRam.Clear();

                    //
                    if (MyLog.DebugFunctions)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat("T00", " Data Retrieved"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                    }
                    //

                }
                else
                {
                    //méthode multifichiers
                    int iR = -1;
                    for (int cptFile = 0; cptFile < sFilesSplit.Count; cptFile++)
                    {
                        Encoding encode = DetectFileEncoding(sFilesSplit[cptFile]);
                        var CsvHelper = new CsvHelper(sFilesSplit[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite, encode);

                        while (CsvHelper.Read())
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            iR++;
                            sInput = CsvHelper.ReadCSVLine();

                            if (iR >= iRowsLimit + iStartsFrom) { break; }

                            if (iR >= iStartsFrom)
                            {
                                if (!string.IsNullOrEmpty(sInput))
                                {
                                    sListFileInRam.Add(sInput.Trim());
                                }
                            }
                        }
                        CsvHelper.Close();
                        sFileInRamTemp = sListFileInRam.ToArray();
                        sListFileInRam.Clear();

                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T00", " Data Retrieved : ", sFilesSplit[cptFile]), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //

                        if (!(cptFile == 0)) //je passe la ligne d'entête sur les fichiers sauf le premier que je reçois
                        {
                            //
                            if (MyLog.DebugFunctions)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                                string.Concat("T00", " Remove header row : ", sFilesSplit[cptFile]), SQLTools_Enums.LOG_TYPEINFO.DBG);
                            }
                            //

                            Toolbox.RemoveAt(ref sFileInRamTemp, 0);
                        }

                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T00", " Resizing Array : ", sFilesSplit[cptFile]), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //

                        iOldLength = sFileInRam.Length;
                        Array.Resize(ref sFileInRam, sFileInRam.Length + sFileInRamTemp.Length);
                        sFileInRamTemp.CopyTo(sFileInRam, iOldLength);
                        sFileInRamTemp = null;
                    }
                }

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return sFileInRam;

        }

        [DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        private static int GetCmdCodePage()
        {
            int lcid = GetSystemDefaultLCID();
            var ci = System.Globalization.CultureInfo.GetCultureInfo(lcid);
            return ci.TextInfo.OEMCodePage;
        }

        private void GetMessagesFromExecutedProcess(DataReceivedEventArgs eventArgs, ref StringBuilder outputStringBuilder)
        {
            if (eventArgs.Data != null)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, eventArgs.Data.Trim(), SQLTools_Enums.LOG_TYPEINFO.INF);
                outputStringBuilder.Append(eventArgs.Data.Trim() + "|");
            }
        }

        private List<string> CreateXMLFile(DataTable dtData, string sPathFile, Query FuzibleQuery)
        {
            List<string> CreatedFiles = new List<string>();

            //préparation du header
            string sHeader = "";
            if (JobParameters.XMLHeader.StartsWith("<?") && JobParameters.XMLHeader.EndsWith("?>")) { sHeader = JobParameters.XMLHeader; }
            else if (JobParameters.XMLHeader.StartsWith("<") && JobParameters.XMLHeader.EndsWith(">")) { sHeader = string.Concat("<?", JobParameters.XMLHeader[1..^1], "?>"); }
            else { sHeader = string.Concat("<?", JobParameters.XMLHeader, "?>"); }

            //préparation du row.parent
            string sRowNode = dtData.Namespace.Replace("\\\"\"", "\""); //cas des alias "cii_agents ref=\""C2AGT_CODE\"""
            if (!dtData.Namespace.Contains("\\\"\"", StringComparison.CurrentCulture)) { sRowNode = sRowNode.Replace("\"\"", "\""); }
            if (sRowNode.StartsWith("\"") && sRowNode.EndsWith("\"")) { sRowNode = sRowNode[1..^1]; }
            else if (sRowNode.StartsWith("`") && sRowNode.EndsWith("`")) { sRowNode = sRowNode[1..^1]; }

            string sTargetRowBuilder = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.XMLTargetRowBuilder, JobParameters.DynParams);
            if (sTargetRowBuilder.Length == 0 && JobParameters.MaxRowsInAFile > 1 && dtData.Rows.Count > 1)
            {
                sTargetRowBuilder = "Row";
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_xml_create_warning + sTargetRowBuilder + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
            }

            int cptFile = 1;

            try
            {
                List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sPathFile);
                List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, 0, dtData.Rows, dtData.TableName, ref MyLog, FuzibleQuery);
                sPathFile = sFile[0] + sFile[3] + sFile[1];
                if (!CreatedFiles.Contains(sPathFile)) { CreatedFiles.Add(sPathFile); }
                bool bAlreadyExists = File.Exists(sPathFile);
                //en mode append, il faut créer une ligne parent pour le "batch" précédent
                if (bAlreadyExists && JobParameters.AppendFileCreation) { AddXMLParentNodeIfNeeded(sPathFile, true); }

                StringBuilder sbData = new();

                if (!bAlreadyExists || (!JobParameters.AppendFileCreation && bAlreadyExists)) //si le fichier existe déjà, on n'écrit pas la ligne d'entête
                {
                    sbData.AppendLine(sHeader);
                    if (JobParameters.XMLWriteMode == 1)
                    {
                        sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, true));
                    }
                    else { sbData.AppendLine(string.Concat("<", sRowNode, ">")); }
                }
                else
                {
                    if (JobParameters.XMLWriteMode == 1)
                    {
                        sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, true));
                    }
                    else { sbData.AppendLine(string.Concat("<", sRowNode, ">")); }
                }

                StreamWriter sw = null;

                //scénario des output scriptés
                if (dtData.Rows.Count == 1 && sPathFileExt[0].IndexOf("[") > -1 && sPathFileExt[0].IndexOf("]") > -1)
                {
                    FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, null, cptFile, 1, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                    if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                    sw = fP.SW ?? null;
                }
                else
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        var fileStream = new FileStream(sPathFile, JobParameters.AppendFileCreation ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        sw = new StreamWriter(fileStream, Encoding.UTF8);
                        //sw = new StreamWriter(sPathFile, JobParameters.AppendFileCreation, Encoding.UTF8);
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatenewxml, sPathFile));
                    }
                }

                for (int cptR = 0; cptR < dtData.Rows.Count; cptR++)
                {
                    //gestion multi-fichiers
                    if (cptR % JobParameters.MaxRowsInAFile == 0 && cptR > 0)
                    {
                        sw?.WriteLine(sbData.ToString().Trim());
                        cptFile++;
                        FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, sw, cptFile, cptR, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                        if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                        sw = fP.SW ?? null;
                        sbData.Clear();

                        sbData.AppendLine(sHeader);

                        if (JobParameters.XMLWriteMode == 1)
                        {
                            sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[cptR], cptR, JobParameters.MaxRowsInAFile, true));
                        }
                        else { sbData.AppendLine(string.Concat("<", sRowNode, ">")); }
                    }

                    if (JobParameters.XMLWriteMode != 1 && sTargetRowBuilder.Length > 0)
                    {
                        sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[cptR], cptR, JobParameters.MaxRowsInAFile, true));
                    }

                    if (JobParameters.XMLWriteMode == 1)
                    {
                        StringBuilder sbRow = new();
                        sbRow.Append(string.Concat("    <", sRowNode, " "));
                        for (int cptC = 0; cptC < dtData.Rows[cptR].Table.Columns.Count; cptC++)
                        {
                            if (JobParameters.XMLRemoveTagForEmptyValues && dtData.Rows[cptR][cptC].ToString().Length == 0)
                            { //option qui n'écrit pas le tag si la valeur est vide
                            }
                            else
                            {
                                string sXML;
                                sXML = JobParameters.TrimData ? dtData.Rows[cptR][cptC].ToString().Trim() : dtData.Rows[cptR][cptC].ToString();
                                sXML = Regex.Replace(sXML, "\"(?![A-z\\d#]{2,4};)", "&quot;");
                                sXML = Regex.Replace(sXML, "\\r\\n", "&#10;");
                                sXML = Regex.Replace(sXML, "\\n", "&#10;");
                                sXML = Regex.Replace(sXML, "\\r", "&#10;");
                                //sXML = Toolbox.ReplaceXMLSpecials(sXML, false);
                                //sXML = Toolbox.SetCleanString(sXML, true, "°", false, SQLTools_Enums.BDD.FI_XML);

                                sbRow.Append(string.Concat(dtData.Rows[cptR].Table.Columns[cptC].ColumnName, "=\"", sXML, "\" "));
                            }
                        }
                        sbRow.Append("/>");
                        sbData.AppendLine(sbRow.ToString());
                    }
                    else
                    {
                        for (int cptC = 0; cptC < dtData.Rows[cptR].Table.Columns.Count; cptC++)
                        {
                            if (JobParameters.XMLRemoveTagForEmptyValues && dtData.Rows[cptR][cptC].ToString().Length == 0)
                            { //option qui n'écrit pas le tag si la valeur est vide
                            }
                            else
                            {
                                string sXML;
                                sXML = JobParameters.TrimData ? dtData.Rows[cptR][cptC].ToString().Trim() : dtData.Rows[cptR][cptC].ToString();

                                sXML = Toolbox.ReplaceXMLSpecials(sXML, false);

                                //on va fermer proprement la balise fermente (cas des colonnes genre : <comptes_comptables ref="CLI_KNUM_CPTE_AUX">
                                string sClose = dtData.Rows[cptR].Table.Columns[cptC].ColumnName;
                                if (sClose.IndexOf("=") > 0)
                                {
                                    int iEqual = sClose.IndexOf("=");
                                    sClose = sClose[..iEqual];
                                    int iLastWhiteSpace = sClose.LastIndexOf(" ");
                                    if (iLastWhiteSpace > 0)
                                    {
                                        sClose = sClose[..iLastWhiteSpace];
                                    }
                                    sClose = sClose.Trim();
                                }

                                if (JobParameters.XMLAddCDataTag)
                                {
                                    sbData.AppendLine(string.Concat("\t\t<", dtData.Rows[cptR].Table.Columns[cptC].ColumnName, "><![CDATA[", Toolbox.SetCleanString(sXML, true, "°", false, new CONNString(SQLTools_Enums.BDD.FI_XML, "0", "XML", "")), "]]></", sClose, ">"));
                                }
                                else
                                {
                                    sbData.AppendLine(string.Concat("\t\t<", dtData.Rows[cptR].Table.Columns[cptC].ColumnName, ">", Toolbox.SetCleanString(sXML, true, "°", false, new CONNString(SQLTools_Enums.BDD.FI_XML, "0", "XML", "")), "</", sClose, ">"));
                                }

                            }

                        }
                    }

                    if (sTargetRowBuilder.Length > 0 && JobParameters.XMLWriteMode != 1)
                    {
                        sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[cptR], cptR, JobParameters.MaxRowsInAFile, false));
                    }

                    if ((cptR + 1) % JobParameters.MaxRowsInAFile == 0 || (cptR + 1) == dtData.Rows.Count)
                    {
                        if (JobParameters.XMLWriteMode == 1)
                        {
                            sbData.AppendLine(GetXMLRowBuilder(JobParameters, sTargetRowBuilder, dtData.Rows[cptR], cptR, JobParameters.MaxRowsInAFile, false));
                        }
                        else
                        {
                            if (sRowNode.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase) > 0)
                            {
                                sbData.AppendLine(string.Concat("</", sRowNode[..sRowNode.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase)], ">"));
                            }
                            else { sbData.AppendLine(string.Concat("</", sRowNode, ">")); }
                        }
                    }
                }

                sw?.WriteLine(sbData.ToString().Trim());
                sw?.Close();

                if (bAlreadyExists && JobParameters.AppendFileCreation) { AddXMLParentNodeIfNeeded(sPathFile, false); }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
            return CreatedFiles;
        }

        private bool AddXMLParentNodeIfNeeded(string sPathFile, bool bStart)
        {
            bool bAdded = false;
            string sParentNode = JobParameters.GlobalParameters.XML_PARENT_NODE;
            sParentNode = sParentNode.Replace("[VERSION]", INIProgram.APP_VERSION.ToString());
            sParentNode = sParentNode.Replace("[USER]", JobParameters.USER);

            string[] sFile = File.ReadAllLines(sPathFile, Encoding.UTF8);

            if (bStart)
            {
                if (sFile.Length > 1)
                {
                    if (Regex.IsMatch(sFile[0], "^<?.+?>$"))
                    {
                        if (!Regex.IsMatch(sFile[1], "^<FuzibleXML version=\".+\" user=\".+\">$"))
                        {
                            //on doit ajouter le node parent 
                            var sFileAsList = sFile.ToList();
                            sFileAsList.Insert(1, sParentNode);
                            File.WriteAllLines(sPathFile, sFileAsList.ToArray(), Encoding.UTF8);
                            sFileAsList.Clear();
                            bAdded = true;
                        }
                        else
                        {
                            //il faut vérifier que la dernière ligne possède la balise fermente et il faut la virer sans pression
                            if (Regex.IsMatch(sFile[^1], "^</FuzibleXML>$") || Regex.IsMatch(sFile[^2], "^</FuzibleXML>$"))
                            {
                                var sFileAsList = sFile.ToList();
                                sFileAsList.RemoveAt(sFile.Length - (Regex.IsMatch(sFile[^1], "^</FuzibleXML>$") ? 1 : 2));
                                File.WriteAllLines(sPathFile, sFileAsList.ToArray(), Encoding.UTF8);
                                sFileAsList.Clear();
                            }
                        }
                    }
                }
            }
            else
            {
                if (sFile.Length > 1)
                {
                    for (int i = sFile.Length - 1; i > 0; i--)
                    {
                        if (!Regex.IsMatch(sFile[i], "^</FuzibleXML>$") && sFile[i].Length > 0)
                        {
                            //on doit ajouter le node parent 
                            var sFileAsList = sFile.ToList();
                            sFileAsList.Insert(i + 1, "</FuzibleXML>");
                            File.WriteAllLines(sPathFile, sFileAsList.ToArray(), Encoding.UTF8);
                            bAdded = true;
                            break;
                        }
                    }
                }
            }

            return bAdded;
        }

        private static string GetXMLRowBuilder(Job JobParameters, string sTargetRowBuilder, DataRow dr, int iRowInDt, int iMaxRowsInAFile, bool bStart)
        {
            string[] sColumns = dr.Table.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray();
            string sRowScript = sTargetRowBuilder;
            if (sRowScript.StartsWith("<")) { sRowScript = sRowScript[1..]; }
            if (sRowScript.EndsWith(">")) { sRowScript = sRowScript[0..^1]; }

            if (sRowScript.IndexOf("[ROWCOUNT]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                if (JobParameters.XMLWriteMode == 1)
                {
                    sRowScript = sRowScript.Replace("[ROWCOUNT]", (iRowInDt % iMaxRowsInAFile).ToString());
                }
                else { sRowScript = sRowScript.Replace("[ROWCOUNT]", iRowInDt.ToString()); }
            }
            if (sRowScript.IndexOf("[FILECOUNT]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                sRowScript = sRowScript.Replace("[FILECOUNT]", (iRowInDt % iMaxRowsInAFile).ToString());
            }
            if (sRowScript.IndexOf("[JOBNAME]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                sRowScript = sRowScript.Replace("[JOBNAME]", JobParameters.JobNAME.Replace(" ", "_"));
            }
            if (sRowScript.IndexOf("[FILECOUNT]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                sRowScript = sRowScript.Replace("[FILECOUNT]", (iRowInDt % iMaxRowsInAFile).ToString());
            }
            if (sRowScript.IndexOf("[USER]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                sRowScript = sRowScript.Replace("[USER]", JobParameters.USER.Replace(" ", "_"));
            }
            if (sRowScript.IndexOf("[DATETIME]", StringComparison.OrdinalIgnoreCase) > -1)
            {
                sRowScript = sRowScript.Replace("[DATETIME]", DateTime.Now.ToString().Replace(" ", "_"));
            }

            MatchCollection mcColumns = Regex.Matches(sRowScript, string.Concat("\\[(", string.Join("|", sColumns), ")\\]"), RegexOptions.IgnoreCase);
            foreach (Match mc in mcColumns)
            {
                sRowScript = sRowScript.Replace(mc.Value, dr[mc.Value[1..^1]].ToString());
            }

            if (bStart)
            {
                return string.Concat("\t", "<", sRowScript, ">");
            }
            else { return string.Concat("\t", "</", sRowScript.Split(" ")[0].Split("=")[0], ">"); }
        }

        private List<string> CreateJSONFile(DataTable dtData, string sPathFile, Query FuzibleQuery)
        {
            List<string> CreatedFiles = new List<string>();

            string sHeader = JobParameters.JSONHeader;
            string sBottom = JobParameters.JSONHeader.Length > 0 ? AddJSONBottom(ref sHeader) : "";

            int cptFile = 1;

            try
            {
                List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sPathFile);
                List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, 0, dtData.Rows, dtData.TableName, ref MyLog, FuzibleQuery);
                sPathFile = sFile[0] + sFile[3] + sFile[1];
                bool bAlreadyExists = File.Exists(sPathFile);
                //en mode append, il faut créer une ligne parent pour le "batch" précédent
                if (bAlreadyExists && JobParameters.AppendFileCreation) { AddJSONParentNodeIfNeeded(sPathFile, dtData.Namespace, true); }

                if (!CreatedFiles.Contains(sPathFile)) { CreatedFiles.Add(sPathFile); }

                StringBuilder sbData = new();

                if (!bAlreadyExists || (!JobParameters.AppendFileCreation && bAlreadyExists)) //si le fichier existe déjà, on n'écrit pas la ligne d'entête
                {
                    sbData.AppendLine("[");
                    if (JobParameters.JSONTargetRowBuilder.Length > 0)
                    {
                        sbData.AppendLine(GetJSONRowBuilder(JobParameters, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, true));
                    }
                }
                else
                {
                    sbData.AppendLine(AddJSONParentNodeIfNeeded(sPathFile, dtData.Namespace, false));
                    if (JobParameters.JSONTargetRowBuilder.Length > 0)
                    {
                        sbData.AppendLine(GetJSONRowBuilder(JobParameters, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, true));
                    }
                }

                StreamWriter sw = null;

                if (dtData.Rows.Count == 1 && sPathFileExt[0].IndexOf("[") > -1 && sPathFileExt[0].IndexOf("]") > -1)
                {
                    FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, null, cptFile, 1, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                    if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                    sw = fP.SW ?? null;
                }
                else
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        var fileStream = new FileStream(sPathFile, JobParameters.AppendFileCreation ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        sw = new StreamWriter(fileStream, Encoding.UTF8);
                        //sw = new StreamWriter(sPathFile, JobParameters.AppendFileCreation, Encoding.UTF8);
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatenewjson, sPathFile));
                    }
                }

                for (int cptR = 0; cptR < dtData.Rows.Count; cptR++)
                {
                    if (cptR % JobParameters.MaxRowsInAFile == 0 && cptR > 0)
                    {
                        sw?.WriteLine(sbData.ToString().Trim()); //subtilité : retrait de la dernière virgule
                        cptFile++;
                        FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, sw, cptFile, cptR, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                        if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                        sw = fP.SW ?? null;
                        sbData.Clear();

                        sbData.AppendLine("[");
                        if (JobParameters.JSONTargetRowBuilder.Length > 0)
                        {
                            sbData.AppendLine(GetJSONRowBuilder(JobParameters, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, true));
                        }
                    }

                    sbData.AppendLine("\t\t\t{");
                    for (int iC = 0; iC < dtData.Columns.Count; iC++)
                    {
                        string sValue = dtData.Rows[cptR][iC].ToString();
                        sValue = JsonParser.CleanJsonValue(sValue, true);

                        sbData.AppendLine(string.Concat("\t\t\t", "\"", dtData.Columns[iC].ColumnName, "\"", ": ", sValue, iC < dtData.Columns.Count - 1 ? "," : ""));
                    }
                    if (cptR < dtData.Rows.Count - 1 && (cptR + 1) % JobParameters.MaxRowsInAFile != 0)
                    {
                        sbData.AppendLine("\t\t\t},");
                    }
                    else { sbData.AppendLine("\t\t\t}"); }

                    if ((cptR + 1) % JobParameters.MaxRowsInAFile == 0 || (cptR + 1) == dtData.Rows.Count)
                    {
                        if (JobParameters.JSONTargetRowBuilder.Length > 0)
                        {
                            sbData.AppendLine(GetJSONRowBuilder(JobParameters, dtData.Rows[0], dtData.Rows.Count, JobParameters.MaxRowsInAFile, false));
                        }

                        if (bAlreadyExists && JobParameters.AppendFileCreation)
                        {
                            sbData.AppendLine("]}");
                        }

                        sbData.AppendLine("]");
                    }
                }

                sw?.Write(string.Concat(sHeader, sbData.ToString(), sBottom));
                sw?.Close();

                if (bAlreadyExists && JobParameters.AppendFileCreation) { AddJSONParentNodeIfNeeded(sPathFile, dtData.Namespace, false); }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
            return CreatedFiles;
        }

        private string AddJSONBottom(ref string sHeader)
        {
            //si dans l'entête il y a des balises ouvrantes, on doit les refermer en fin de fichier
            string sBottom = "";

            if (sHeader.StartsWith("{")) { sBottom = "}"; }
            else if (sHeader.StartsWith("[")) { sBottom = "]"; }

            if (sHeader.EndsWith("{")) { sBottom = string.Concat("}", sBottom); }
            else if (sHeader.EndsWith("[")) { sHeader = sHeader[0..^1]; } //sBottom = string.Concat("]", sBottom); }

            return sBottom;
        }

        private static string GetJSONRowBuilder(Job JobParameters, DataRow dr, int iRowInDt, int iMaxRowsInAFile, bool bStart)
        {
            StringBuilder sbRowScript = new();
            //{
            //"json_info":
            if (bStart)
            {
                string sRowScript = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.JSONTargetRowBuilder, JobParameters.DynParams).Replace("\"", "");
                if (sRowScript.IndexOf("[JOBNAME]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    sRowScript = sRowScript.Replace("[JOBNAME]", JobParameters.JobNAME);
                }
                if (sRowScript.IndexOf("[FILECOUNT]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    sRowScript = sRowScript.Replace("[FILECOUNT]", (iRowInDt % iMaxRowsInAFile).ToString());
                }
                if (sRowScript.IndexOf("[USER]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    sRowScript = sRowScript.Replace("[USER]", JobParameters.USER);
                }
                if (sRowScript.IndexOf("[DATETIME]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    sRowScript = sRowScript.Replace("[DATETIME]", DateTime.Now.ToString());
                }
                if (sRowScript.IndexOf("[ROWCOUNT]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    sRowScript = sRowScript.Replace("[ROWCOUNT]", iRowInDt.ToString());
                }

                sbRowScript.AppendLine("\t{");

                string sJInfo = "json_info";
                if (sRowScript.IndexOf("=") > 0 && sRowScript.IndexOf("=") < sRowScript.Length - 1)  //si on a scripté genre id_sample=[id_sample]
                {
                    sJInfo = sRowScript[0..sRowScript.IndexOf("=")];
                    sRowScript = sRowScript[(sRowScript.IndexOf("=") + 1)..];
                }

                sbRowScript.Append(string.Concat("\t\"", sJInfo, "\":"));

                string[] sColumns = dr.Table.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray();

                MatchCollection mcColumns = Regex.Matches(sRowScript, string.Concat("\\[(", string.Join("|", sColumns), ")\\]"), RegexOptions.IgnoreCase);
                foreach (Match mc in mcColumns)
                {
                    sRowScript = sRowScript.Replace(mc.Value, dr[mc.Value[1..^1]].ToString());
                }

                sbRowScript.AppendLine(string.Concat("\"", sRowScript, "\","));
                sbRowScript.Append("\t\"" + (dr.Table.Namespace.Length == 0 ? "data" : dr.Table.Namespace) + "\":[");
                return sbRowScript.ToString();
            }
            else
            {
                sbRowScript.AppendLine("\t\t]");
                sbRowScript.Append("\t}");
                return sbRowScript.ToString();
            }
        }

        private string AddJSONParentNodeIfNeeded(string sPathFile, string sNamespace, bool bPerform)
        {
            //{"version":"[VERSION]","user":"[USER]","content":[
            string sParentNode = JobParameters.GlobalParameters.JSON_PARENT_NODE;
            sParentNode = sParentNode.Replace("[VERSION]", INIProgram.APP_VERSION.ToString());
            sParentNode = sParentNode.Replace("[USER]", JobParameters.USER);
            sParentNode = sParentNode.Replace("[DATETIME]", DateTime.Now.ToString());
            sParentNode = sParentNode.Replace("[NAMESPACE]", sNamespace);

            string[] sFile = File.ReadAllLines(sPathFile, Encoding.UTF8);

            if (bPerform)
            {
                if (sFile.Length > 1)
                {
                    if (sFile[0].Equals("["))
                    {
                        if (!Regex.IsMatch(sFile[1], "^{\"version\":\".+\",\"user\":\".+\",\"content\":\\[$"))
                        {
                            //on doit ajouter le node parent 
                            var sFileAsList = sFile.ToList();
                            sFileAsList.Insert(1, sParentNode);
                            if (sFileAsList[^1].Equals("]"))
                            {
                                sFileAsList.RemoveAt(sFileAsList.Count - 1);
                            }
                            else if (sFileAsList[^2].Equals("]"))
                            {
                                sFileAsList.RemoveAt(sFileAsList.Count - 2);
                            }
                            sFileAsList.Add("]},");
                            File.WriteAllLines(sPathFile, sFileAsList.ToArray(), Encoding.UTF8);
                            sFileAsList.Clear();
                        }
                        else
                        {
                            var sFileAsList = sFile.ToList();
                            if (sFileAsList[^1].Equals("]"))
                            {
                                sFileAsList.RemoveAt(sFileAsList.Count - 1);
                            }
                            else if (sFileAsList[^2].Equals("]"))
                            {
                                sFileAsList.RemoveAt(sFileAsList.Count - 2);
                            }
                            sFileAsList.Add(",");
                            File.WriteAllLines(sPathFile, sFileAsList.ToArray(), Encoding.UTF8);
                            sFileAsList.Clear();
                        }
                    }
                }
            }

            return sParentNode;
        }

        private List<string> CreateRAWFile(DataTable dtData, int iRow, bool bAddSuffix, int iColBytes, string sPathFile, Query FuzibleQuery)
        {
            List<string> CreatedFiles = new List<string>();

            try
            {
                List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sPathFile);

                if (!JobParameters.RunInSimulationMode)
                {
                    List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, iRow, dtData.Rows, dtData.TableName, ref MyLog, FuzibleQuery);
                    sPathFile = sFile[0] + sFile[3] + (bAddSuffix ? ("_" + iRow.ToString()) : "") + sFile[1];
                    if (!CreatedFiles.Contains(sPathFile)) { CreatedFiles.Add(sPathFile); }

                    File.WriteAllBytes(sPathFile, (System.Byte[])dtData.Rows[iRow][iColBytes]);
                }
                else
                {
                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatenewcsv, sPathFile));
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
            return CreatedFiles;
        }

        private List<string> CreateCSVFile(DataTable dtData, string sPathFile, Query FuzibleQuery)
        {
            List<string> CreatedFiles = new List<string>();

            try
            {
                string sWorkingFile = "";
                long lFileSize = 0;
                StringBuilder sbLigne = new();

                int iAddForLastSeparator = JobParameters.CSVCharSeparator_EndRow ? 0 : 1;
                int cptFile = 1;
                List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sPathFile);
                StreamWriter sw = null;

                if (dtData.Rows.Count == 1 && sPathFileExt[0].IndexOf("[") > -1 && sPathFileExt[0].IndexOf("]") > -1)
                {
                    FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, null, cptFile, 1, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                    if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                    sw = fP.SW ?? null;
                }
                else
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, 0, dtData.Rows, dtData.TableName, ref MyLog, FuzibleQuery);
                        sPathFile = sFile[0] + sFile[3] + sFile[1];

                        var fileStream = new FileStream(sPathFile, JobParameters.AppendFileCreation ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        var enc1252 = Encoding.GetEncoding(1252);

                        sw = new StreamWriter(fileStream, JobParameters.CSVEncoding_Target.Equals("ANSI") ? enc1252 : Encoding.UTF8);
                        if (!CreatedFiles.Contains(sPathFile)) { CreatedFiles.Add(sPathFile); }
                        //sw = new StreamWriter(sPathFile, JobParameters.AppendFileCreation, Encoding.UTF8);
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatenewcsv, sPathFile));
                    }
                }

                if (sw != null)
                {
                    //écriture de l'entête
                    sWorkingFile = ((FileStream)sw.BaseStream).Name; //récupération du nom du fichier en cours de travail              
                    lFileSize = new System.IO.FileInfo(sWorkingFile).Length; //vérification : si il y a déjà un entête, pas besoin d'en crée un 2ème

                    if (lFileSize == 0)
                    {
                        if (JobParameters.CSVAddHeader)
                        {
                            for (int cptCell = 0; cptCell < dtData.Columns.Count; cptCell += 1)
                            {
                                sbLigne.Append(dtData.Columns[cptCell].ColumnName.Trim());
                                if (cptCell < dtData.Columns.Count - iAddForLastSeparator)
                                {
                                    sbLigne.Append(JobParameters.CSVCharSeparator_Target);
                                }
                            }
                            sw.WriteLine(sbLigne.ToString());
                            sbLigne.Length = 0;
                        }
                    }
                }

                string sValue = "";

                for (int cptR = 0; cptR < dtData.Rows.Count; cptR++)
                {

                    //gestion multi-fichiers
                    if (cptR % JobParameters.MaxRowsInAFile == 0 && cptR > 0)
                    {

                        cptFile++;
                        FilePattern fP = CreateNewFileUsingNamingPattern(sPathFileExt, sw, cptFile, cptR, dtData.Rows, dtData.TableName, true, FuzibleQuery);
                        if (!CreatedFiles.Contains(fP.SFILE)) { CreatedFiles.Add(fP.SFILE); }
                        sw = fP.SW ?? null;

                        //écriture de l'entête
                        if (sw != null)
                        {
                            sWorkingFile = ((FileStream)sw.BaseStream).Name; //récupération du nom du fichier en cours de travail              
                            lFileSize = new System.IO.FileInfo(sWorkingFile).Length; //vérification : si il y a déjà un entête, pas besoin d'en crée un 2ème

                            if (JobParameters.CSVAddHeader)
                            {
                                if (lFileSize == 0)
                                {
                                    for (int cptCell = 0; cptCell < dtData.Columns.Count; cptCell += 1)
                                    {
                                        sbLigne.Append(dtData.Columns[cptCell].ColumnName.Trim());
                                        if (cptCell < dtData.Columns.Count - iAddForLastSeparator)
                                        {
                                            sbLigne.Append(JobParameters.CSVCharSeparator_Target);
                                        }
                                    }
                                    sw.WriteLine(sbLigne.ToString());
                                    sbLigne.Clear();
                                }
                            }
                        }
                    }

                    //écriture des lignes
                    for (int cptCell = 0; cptCell < dtData.Columns.Count; cptCell += 1)
                    {
                        sValue = Toolbox.SetCleanString(dtData.Rows[cptR][cptCell].ToString(), true, JobParameters.CSVCharSeparator_Target, false, new CONNString(SQLTools_Enums.BDD.FI_CSV, "0", "CSV", ""));
                        //ci-dessous : cas du format BIT/BOOL : je veux pouvoir écrire dans le fichier "false", "true", ou bien "1" et "0" plutôt que systématiquement "false" ou "true" dans le cas des colonnes de type booléennes
                        if (dtData.Columns[cptCell].Prefix.Equals("BIT") || dtData.Columns[cptCell].Prefix.Equals("BOOL"))
                        {
                            sValue = Toolbox.SetCleanBoolean(sValue, dtData.Columns[cptCell].Prefix.Equals("BIT"), false);
                        }

                        if (dtData.Columns[cptCell].Prefix.Equals("DATE"))
                        {
                            sValue = sValue.Split(Convert.ToChar(" "))[0];
                        }

                        if (JobParameters.TrimData) { sValue = sValue.Trim(); }

                        if (JobParameters.CSVAddQuotes) { sbLigne.Append(string.Concat("\"", sValue.Replace("\"", "\"\""), "\"")); }
                        else { sbLigne.Append(sValue); }

                        if (cptCell < dtData.Columns.Count - iAddForLastSeparator)
                        {
                            sbLigne.Append(JobParameters.CSVCharSeparator_Target);
                        }
                    }
                    sw?.WriteLine(sbLigne.ToString());
                    sbLigne.Length = 0;
                }

                sw?.Close();
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
            return CreatedFiles;
        }

        private List<string> CreateExcelWorkbook(DataTable dtData, string sPathFile, bool bFirstPass, Query FuzibleQuery)
        {
            List<string> CreatedFiles = new List<string>();

            string sFileDef = "";
            List<string> sPathFileExt = FITools.ExtractFileNameAndPathFromFullPath(sPathFile);
            int cptFile = 0;
            int iRowsCountSource = dtData.Rows.Count;
            int iMaxRows = JobParameters.MaxRowsInAFile > iRowsCountSource ? iRowsCountSource : JobParameters.MaxRowsInAFile;
            int iRowStart = 0;
            DataTable dtTarget;
            bool bWasMulti = false;

            if (dtData.Rows.Count == 1 && sPathFileExt[0].IndexOf("[") > -1 && sPathFileExt[0].IndexOf("]") > -1)
            {
                sFileDef = CreateNewFileUsingNamingPattern(sPathFileExt, null, cptFile, 1, dtData.Rows, dtData.TableName, false, FuzibleQuery).SFILE;
                if (!CreatedFiles.Contains(sFileDef)) { CreatedFiles.Add(sFileDef); }
            }
            else
            {
                if (!JobParameters.RunInSimulationMode)
                {
                    sFileDef = sPathFile;
                }
                else
                {
                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willcreatenewxls, sPathFile));
                }
            }

            if (JobParameters.MaxRowsInAFile > iRowsCountSource) //création d'un seul fichier
            {
                bWasMulti = true; //feinte pour éviter que la création se fasse 2x (voir bas de procédure)
                List<string> sFile = NamingPatternGenerator(JobParameters, sPathFileExt, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], 1, 0, dtData.Rows, dtData.TableName, ref MyLog, FuzibleQuery);
                sFileDef = sFile[0] + sFile[3] + sFile[1];
                if (!CreatedFiles.Contains(sFileDef)) { CreatedFiles.Add(sFileDef); }

                dtTarget = dtData.Copy();
                CreateExcelWorkbook_Sub(sFileDef, dtTarget, JobParameters.AppendFileCreation, bFirstPass, FuzibleQuery);
            }
            else //gestion multi-fichiers
            {
                for (int cptR = 0; cptR <= iRowsCountSource; cptR++)
                {
                    if (cptR % JobParameters.MaxRowsInAFile == 0 && cptR > 0 || cptR == iRowsCountSource)
                    {
                        try
                        {
                            bWasMulti = true;
                            cptFile++;
                            iRowStart = cptR == iRowsCountSource ? iRowStart + iMaxRows : cptR - iMaxRows; //initialisation du prochain compteur pour connaitre la ligne de départ 
                            sFileDef = CreateNewFileUsingNamingPattern(sPathFileExt, null, cptFile, iRowStart, dtData.Rows, dtData.TableName, false, FuzibleQuery).SFILE;
                            if (!CreatedFiles.Contains(sFileDef)) { CreatedFiles.Add(sFileDef); }
                            dtTarget = dtData.Clone(); //on construit une datatable uniquement avec les données nécessaires

                            for (int iRow = iRowStart; iRow < cptR; iRow++)
                            {
                                dtTarget.ImportRow(dtData.Rows[iRow]);
                            }

                            CreateExcelWorkbook_Sub(sFileDef, dtTarget, JobParameters.AppendFileCreation, bFirstPass, FuzibleQuery);
                            dtTarget.Clear(); //nettoyage de la litière 
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.fi_xls_createcantcreate, Path.GetFileName(sFileDef), ")"), FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                }
            }

            if (!bWasMulti) //si on n'est pas passé une seule fois dans le "découpeur multi-fichiers", on crée un fichier complet
            {
                CreateExcelWorkbook_Sub(sFileDef, dtData, JobParameters.AppendFileCreation, bFirstPass, FuzibleQuery);
            }

            return CreatedFiles;
        }

        private void CreateExcelWorkbook_Sub(string sFileDef, DataTable dtData, bool bAppend, bool bFirstPass, Query FuzibleQuery)
        {
            if (!JobParameters.RunInSimulationMode)
            {
                //on ne veut pas append un fichier existant mais créer autant de feuilles que de requêtes
                //case du test.xlsx:select * from table1 || test.xlsx:select * from table2 ...
                //if (!bAppend && bFirstPass)
                //{
                //    FileInfo fiExcelDelete = new FileInfo(sFileDef);

                //    try
                //    {
                //        using ExcelPackage pck = new ExcelPackage(fiExcelDelete);
                //        if (pck.Workbook.Worksheets.Count > 0) //si pas d'append, écrasement du fichier par suppression des workbook existants
                //        {
                //            List<string> sListWStoRemove = new List<string>();
                //            foreach (ExcelWorksheet eWsA in pck.Workbook.Worksheets)
                //            {
                //                sListWStoRemove.Add(eWsA.Name);
                //            }
                //            foreach (string sWs in sListWStoRemove)
                //            {
                //                pck.Workbook.Worksheets.Delete(sWs);
                //            }
                //        }
                //    }
                //    catch { }
                //}

                int iColumns = dtData.Columns.Count;
                FileInfo fiExcel = new(sFileDef);
                var tS = (OfficeOpenXml.Table.TableStyles)Enum.Parse(typeof(OfficeOpenXml.Table.TableStyles), JobParameters.XLSStyle);

                string sTablename = dtData.Namespace.Length == 0 ? dtData.TableName : dtData.Namespace;

                if (sTablename.StartsWith("\"") && sTablename.EndsWith("\"")) //nettoyage en cas de SELECT * FROM mytable as "Mon Tableau"
                { sTablename = sTablename[1..^1]; }

                string sOldTB = dtData.TableName; //bug avec des noms chelous
                dtData.TableName = Toolbox.RemoveSpecialCharacters(dtData.TableName, "_", true);
                if (Regex.IsMatch(dtData.TableName, "^[\\d_]")) { dtData.TableName = string.Concat("t_", dtData.TableName); }

                try
                {
                    using ExcelPackage pck = new(fiExcel);

                    int iStartRow = 1;
                    string sFirstCell = "A1";
                    string sSecondCell = "A2";

                    if (JobParameters.XLSRowWriteOffset > 0)
                    {
                        iStartRow = JobParameters.XLSRowWriteOffset + 1;
                        sFirstCell = "A" + iStartRow.ToString();
                        sSecondCell = "A" + (iStartRow + 1).ToString();
                    }

                    if (!bAppend || pck.Workbook.Worksheets.Count == 0) //si pas d'append, écrasement du fichier par suppression des workbook existants
                    {
                        if (bFirstPass)
                        {
                            List<string> sListWStoRemove = new();
                            foreach (ExcelWorksheet eWsA in pck.Workbook.Worksheets)
                            {
                                sListWStoRemove.Add(eWsA.Name);
                            }
                            foreach (string sWs in sListWStoRemove)
                            {
                                pck.Workbook.Worksheets.Delete(sWs);
                            }
                        }

                        ExcelWorksheet ws = null;
                        try
                        {
                            ws = pck.Workbook.Worksheets.Add(sTablename);
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }

                        if (ws != null)
                        {
                            try
                            {
                                ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader, tS);
                                SetFormulas(ws, dtData);
                            }
                            catch
                            {
                                //bug EEplus !
                                dtData.TableName = string.Concat("t_", dtData.TableName);
                                try
                                {
                                    ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader, tS);
                                    SetFormulas(ws, dtData);
                                }
                                catch (Exception ex)
                                {

                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.fi_excelsub_cantapplystyle + " (" + dtData.TableName + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader);
                                }
                            }

                            if (JobParameters.XLSWithTitle)
                            {
                                string sObjet = JobParameters.JobDescription.Length == 0 ? JobParameters.JobNAME : JobParameters.JobDescription;
                                sObjet = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sObjet, JobParameters.DynParams);

                                ws.Cells[sFirstCell + ":" + Toolbox.ColumnToAlphabet(iColumns) + iStartRow.ToString()].Merge = true;
                                ws.Cells[sFirstCell].Value = sObjet;
                                ws.Cells[sFirstCell].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                                ws.Cells[sFirstCell].Style.Font.Bold = true;
                            }

                            //formatage de la date : par défaut, Excel stocke les champs date en nombres entiers
                            foreach (DataColumn dc in dtData.Columns)
                            {
                                if (dc.DataType == typeof(DateTime))
                                {
                                    ws.Cells[2, dc.Ordinal + 1, dtData.Rows.Count + 2, dc.Ordinal + 1].Style.Numberformat.Format = "dd/mm/yyyy hh:mm:ss AM/PM";
                                }
                                //if (dc.DataType == typeof(Boolean))
                                //{ ws.Cells[2, dc.Ordinal + 1, dtData.Rows.Count + 2, dc.Ordinal + 1].Style.Numberformat.Format = "dd/mm/yyyy hh:mm:ss AM/PM"; }
                            }

                            if (JobParameters.XLSPasswordTarget.Length > 0) { pck.Save(JobParameters.XLSPasswordTarget); }
                            else { pck.Save(); }
                        }
                    }
                    else //mode append ---------------------------------------------------------------------------------------------
                    {
                        bool bExists = false;
                        foreach (ExcelWorksheet ewS in pck.Workbook.Worksheets) //en mode append, on regarde d'abord si la feuille existe déjà
                        {
                            if (ewS.Name.Equals(sTablename, StringComparison.InvariantCultureIgnoreCase))
                            {
                                bExists = true;
                                List<string> sListColumnsExcel = new();
                                List<string> sListColumnsDatatable = new();

                                int iEndRow = 0;
                                int iStartCell = 0;
                                int iEndCell = 0;

                                if (ewS.Dimension == null)
                                {
                                    ewS.Cells[1, 1].Value = "";
                                    iStartRow = 1;
                                    iEndRow = 1;
                                    iStartCell = 1;
                                    iEndCell = 1;
                                }
                                else
                                {
                                    iStartRow = ewS.Dimension.Start.Row;
                                    iEndRow = ewS.Dimension.End.Row;
                                    iStartCell = ewS.Dimension.Start.Column;
                                    iEndCell = ewS.Dimension.End.Column;
                                }

                                string sCell = "";

                                if (JobParameters.XLSRowWriteOffset > 0)
                                {
                                    iStartRow = JobParameters.XLSRowWriteOffset + 1;
                                }
                                else
                                {
                                    //en mode AVEC HEADER, on recherche toujours systématiquement l'header en fonction de la définition utlisateur
                                    //si celle-ci est définie à 0. Si elle est forcée, on écrit dans le fichier à partir de la ligne indiquée
                                    //on bypasse la recherche de ligne vide
                                    if (!JobParameters.XLSAddHeader)
                                    {
                                        //en mode 0, c'est le mode "automatique", on recherche la première ligne vide
                                        ExcelRange exRange;

                                        sCell = string.Concat(GetExcelColumnName(iStartCell), iStartRow);
                                        exRange = ewS.SelectedRange[sCell];
                                        while (exRange.Value != null)
                                        {
                                            iStartRow++;
                                            sCell = string.Concat(GetExcelColumnName(iStartCell), iStartRow);
                                            exRange = ewS.SelectedRange[sCell];
                                            if (iStartRow >= iEndRow) { break; }
                                        }
                                    }
                                }

                                if (JobParameters.XLSAddHeader)
                                {
                                    //problème avec le iStartCell : ewS.Dimension.Start.Column rapporte souvent une valeur erronée. Il faut faire un test manuel !
                                    ExcelRange exRange2;
                                    //sCell = string.Concat(GetExcelColumnName(iStartCell), iStartRow);
                                    //exRange2 = ewS.SelectedRange[sCell];

                                    bool bFound = false;
                                    while (!bFound)
                                    {
                                        int iCount = iStartRow;
                                        while (iCount < iStartRow + 100) //100 est arbitraire évidemment
                                        {
                                            sCell = string.Concat(GetExcelColumnName(iStartCell), iCount);
                                            exRange2 = ewS.SelectedRange[sCell];
                                            if (exRange2.Value != null)
                                            {
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_startdimensiondetected, " (", ewS.Name, ") : ", sCell), SQLTools_Enums.LOG_TYPEINFO.DET);

                                                bFound = true;
                                                break;
                                            }
                                            iCount++;
                                        }
                                        if (!bFound) { iStartCell++; }
                                        if (iStartCell > 100)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_startdimensionnotfound, " (", ewS.Name, ") : ", sCell), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            iStartCell = 1;
                                            break;
                                        }
                                    }
                                }

                                //mode APPEND ----------------------------------------------------------
                                //si la feuille existe et qu'on ne veut pas traiter d'entête, on écrit simplement dans le fichier
                                if (!JobParameters.XLSAddHeader)
                                {
                                    //recalcul iEndRow : la ligne finale doit être celle qui n'a pas de données sur les colonnes d'origine du datatable
                                    iEndRow = CreateExcelWorkbook_FindRealEndRow(ewS, dtData, iStartRow, iStartCell);

                                    sCell = string.Concat(GetExcelColumnName(iStartCell), (iEndRow + 1).ToString());
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_createschemaappend, sTablename, " - ", sCell, ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                                    ExcelWorksheet wsAppend = pck.Workbook.Worksheets[sTablename];

                                    //bug EEplus !
                                    if (JobParameters.XLSWithTitle)
                                    {
                                        dtData.TableName = string.Concat("t_", dtData.TableName);
                                        try
                                        {
                                            wsAppend.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader, tS);
                                            SetFormulas(wsAppend, dtData);

                                            string sObjet = JobParameters.JobDescription.Length == 0 ? JobParameters.JobNAME : JobParameters.JobDescription;
                                            sObjet = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sObjet, JobParameters.DynParams);

                                            wsAppend.Cells[sFirstCell + ":" + Toolbox.ColumnToAlphabet(iColumns) + iStartRow.ToString()].Merge = true;
                                            wsAppend.Cells[sFirstCell].Value = sObjet;
                                            wsAppend.Cells[sFirstCell].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                                            wsAppend.Cells[sFirstCell].Style.Font.Bold = true;

                                        }
                                        catch (Exception ex)
                                        {

                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.fi_excelsub_cantapplystyle + " (" + dtData.TableName + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            wsAppend.Cells[sCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader);
                                        }
                                    }
                                    else
                                    {
                                        wsAppend.Cells[sCell].LoadFromDataTable(dtData, false);
                                    }

                                    if (JobParameters.XLSPasswordTarget.Length > 0) { pck.Save(JobParameters.XLSPasswordTarget); }
                                    else { pck.Save(); }
                                }
                                else  //si la feuille existe et qu'on ne veut l'entête, on recheche l'entête et on écrit dessous
                                {
                                    ExcelRange exRange;
                                    for (int iR = iStartCell; iR <= iEndCell; iR++) //si la feuille existe, on vérifie son entête pour voir si elle matche avec notre source : 
                                    {
                                        sCell = string.Concat(GetExcelColumnName(iR), iStartRow);
                                        exRange = ewS.SelectedRange[sCell];
                                        while (exRange.Value == null || exRange.Merge)
                                        {
                                            iStartRow++;
                                            sCell = string.Concat(GetExcelColumnName(iR), iStartRow);
                                            exRange = ewS.SelectedRange[sCell];
                                            if (iStartRow >= iEndRow) { break; }
                                        }
                                        if (exRange.Value != null)
                                        {
                                            sListColumnsExcel.Add(exRange.Value.ToString().ToUpper().Trim());
                                        }
                                        else { iStartRow--; } //heuuuuuu ?? bref...
                                    }

                                    foreach (DataColumn dt in dtData.Columns)
                                    {
                                        sListColumnsDatatable.Add(dt.ColumnName.ToUpper());
                                    }

                                    int iColIndex = 0;
                                    foreach (string sCol in sListColumnsExcel)
                                    {
                                        if (!sListColumnsDatatable.Contains(sCol))
                                        {
                                            dtData.Columns.Add(new DataColumn(sCol));

                                            //quand il y a des colonnes qui manquent un peu partout dans le fichier, l'index est corrompu
                                            iColIndex = iColIndex > dtData.Columns.Count - 1 ? dtData.Columns.Count - 1 : iColIndex;

                                            dtData.Columns[sCol].SetOrdinal(iColIndex);
                                            dtData.Columns[iColIndex].Namespace = "FROM_FILE";
                                            sListColumnsDatatable.Insert(iColIndex, sCol);
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_addcoltodatatable, sCol), SQLTools_Enums.LOG_TYPEINFO.DET);
                                        }
                                        iColIndex++;
                                    }

                                    foreach (string sCol in sListColumnsDatatable)
                                    {
                                        if (!sListColumnsExcel.Contains(sCol))
                                        {
                                            sListColumnsExcel.Add(sCol);
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_addcoltoexcel, sCol), SQLTools_Enums.LOG_TYPEINFO.DET);
                                            dtData.Columns[sCol].SetOrdinal(dtData.Columns.Count - 1);
                                        }
                                    }

                                    //recalcul iEndRow : la ligne finale doit être celle qui n'a pas de données sur les colonnes d'origine du datatable
                                    iEndRow = CreateExcelWorkbook_FindRealEndRow(ewS, dtData, iStartRow, iStartCell);

                                    sCell = string.Concat(GetExcelColumnName(iStartCell), (iEndRow + 1).ToString());
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xls_createschemaappend, sTablename, " - ", sCell, ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                                    ExcelWorksheet wsAppend = pck.Workbook.Worksheets[sTablename];
                                    wsAppend.Cells[sCell].LoadFromDataTable(dtData, false);
                                    SetFormulas(wsAppend, dtData);
                                    if (JobParameters.XLSPasswordTarget.Length > 0) { pck.Save(JobParameters.XLSPasswordTarget); }
                                    else { pck.Save(); }
                                }
                            }
                        }

                        //si on a demandé un append, mais que la feuille d'append n'existe pas on en crée une nouvelle
                        if (!bExists)
                        {
                            ExcelWorksheet ws = pck.Workbook.Worksheets.Add(sTablename);

                            try
                            {
                                //dtData.TableName = "t_" + dtData.Namespace;
                                ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader, tS);
                                SetFormulas(ws, dtData);
                            }
                            catch
                            {
                                //bug EEplus !
                                dtData.TableName = string.Concat("t_", dtData.TableName);
                                try
                                {
                                    //dtData.TableName = "t_" + dtData.Namespace;
                                    ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader, tS);
                                    SetFormulas(ws, dtData);
                                }
                                catch (Exception ex)
                                {

                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.fi_excelsub_cantapplystyle + " (" + dtData.TableName + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    ws.Cells[JobParameters.XLSWithTitle ? sSecondCell : sFirstCell].LoadFromDataTable(dtData, JobParameters.XLSAddHeader);
                                }
                            }

                            if (JobParameters.XLSWithTitle)
                            {
                                string sObjet = JobParameters.JobDescription.Length == 0 ? JobParameters.JobNAME : JobParameters.JobDescription;
                                sObjet = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sObjet, JobParameters.DynParams);

                                ws.Cells[sFirstCell + ":" + Toolbox.ColumnToAlphabet(iColumns) + "1"].Merge = true;
                                ws.Cells[sFirstCell].Value = sObjet;
                                ws.Cells[sFirstCell].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                                ws.Cells[sFirstCell].Style.Font.Bold = true;
                            }

                            if (JobParameters.XLSPasswordTarget.Length > 0) { pck.Save(JobParameters.XLSPasswordTarget); }
                            else { pck.Save(); }
                        }

                    }

                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }

                dtData.TableName = sOldTB;
            }
            else
            {
                MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willwriteintoxls, sFileDef));
            }
        }

        private void SetFormulas(ExcelWorksheet ws, DataTable dtData)
        {
            if (JobParameters.XLSInterpretFormulas)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.fi_excelsub_interpretformulas, SQLTools_Enums.LOG_TYPEINFO.INF);
                try
                {
                    foreach (var cell in ws.Cells)
                    {
                        if (Regex.Match(cell.Text, "=[a-zA-Z0-9\\()]+").Success)
                        {
                            string sTemp = Regex.Replace(cell.Text, "(\\d+)(,)(\\d+)", "$1.$3");
                            cell.Formula = sTemp.Replace(";", ","); //.Replace("\"", "\\\"");
                                                                    //cell.Calculate();
                        }
                    }
                }
                catch { throw; }
            }
        }

        private int CreateExcelWorkbook_FindRealEndRow(ExcelWorksheet ewS, DataTable dtData, int iStartRow, int iStartCell)
        {
            ExcelRange exRange;
            string sCell = "";
            bool bFound = false;
            int iCount = iStartRow;
            while (!bFound)
            {
                if (iStartCell > 0)
                {
                    while (dtData.Columns[iStartCell - 1].Namespace.Equals("FROM_FILE"))
                    {
                        iStartCell++;
                    }
                }

                sCell = string.Concat(GetExcelColumnName(iStartCell), iCount);
                exRange = ewS.SelectedRange[sCell];
                if (exRange.Value == null)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_xlsenddimensiondetected, " (", ewS.Name, ") : ", sCell), SQLTools_Enums.LOG_TYPEINFO.DET);

                    bFound = true;
                    break;
                }
                iCount++;
            }
            return iCount - 1;
        }

        private static string GetExcelColumnName(int columnNumber)
        {
            int dividend = columnNumber;
            string columnName = string.Empty;
            int modulo;

            while (dividend > 0)
            {
                modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo).ToString() + columnName;
                dividend = (int)((dividend - modulo) / 26);
            }

            return columnName;
        }

        private FilePattern CreateNewFileUsingNamingPattern(List<string> sPathFile, StreamWriter sw, int iCptFile, int iCptRow, DataRowCollection dRs, string sQueryAlias, bool bCreate, Query FuzibleQuery)
        {
            sw?.Close();

            List<string> sPattern = NamingPatternGenerator(JobParameters, sPathFile, Connection.SConnDriver.ToString()[(Connection.SConnDriver.ToString().IndexOf("_") + 1)..], iCptFile, iCptRow, dRs, sQueryAlias, ref MyLog, FuzibleQuery);
            //retourne : 
            string sOriginalPath = sPattern[0];
            string sOriginalExt = sPattern[1];
            string sOldFSPattern = sPattern[2];
            string sNewFSPattern = sPattern[3];

            //on renomme le précédent fichier crée (uniquement si c'était le premier, car par définition, le premier n'a pas de pattern)
            if (iCptFile == 2)
            {
                try
                {
                    //suppression du fichier si déjà existant
                    //if (File.Exists(string.Concat(sOriginalPath, sOldFSPattern, sOriginalExt)))
                    //{
                    //    if (!JobParameters.RunInSimulationMode)
                    //    { File.Delete(string.Concat(sOriginalPath, sOldFSPattern, sOriginalExt)); }
                    //    else
                    //    { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", "WILL DELETE EXISTING FILE : ", string.Concat(sOriginalPath, sOldFSPattern, sOriginalExt))); }
                    //}

                    if (File.Exists(string.Concat(sPathFile[1], sPathFile[0], sPathFile[2])))
                    {
                        if (!JobParameters.RunInSimulationMode)
                        {
                            File.Move(string.Concat(sPathFile[1], sPathFile[0], sPathFile[2]), string.Concat(sOriginalPath, sOldFSPattern, sOriginalExt));
                        }
                        else
                        {
                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", "WILL MOVE EXISTING FILE : ", string.Concat(sPathFile[1], sPathFile[0], sPathFile[2]), " -> ", string.Concat(sOriginalPath, sOldFSPattern, sOriginalExt)));
                        }

                    }
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            //---------------------------

            //suppression du fichier si déjà existant
            if (File.Exists(string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt)))
            {
                if (JobParameters.AppendFileCreation)
                {
                    sw?.Close();
                }
                else
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        File.Delete(string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt));
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_patterngenerator_delete, string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt)));
                    }
                }
            }

            try
            {
                if (bCreate)
                {
                    if (!JobParameters.RunInSimulationMode)
                    {
                        var fileStream = new FileStream(string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        var enc1252 = Encoding.GetEncoding(1252);

                        sw = new StreamWriter(fileStream, JobParameters.CSVEncoding_Target.Equals("ANSI") ? enc1252 : Encoding.UTF8);
                        //sw = new StreamWriter(string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt), true, Encoding.UTF8);
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_patterngenerator_create, string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt)));
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            FilePattern fP = new()
            {
                SW = sw,
                SFILE = string.Concat(sOriginalPath, sNewFSPattern, sOriginalExt)
            };

            return fP;
        }

        private SQLTools_Enums.BDD AutodetectFileType(SQLTools_Enums.BDD RequestedBDD, string sPath, string sFile)
        {
            NativeMethods.SHFILEINFO info = new();

            string fileName = sPath + sFile;

            if (File.Exists(fileName))
            {
                uint dwFileAttributes = NativeMethods.FILE_ATTRIBUTE.FILE_ATTRIBUTE_NORMAL;
                uint uFlags = (uint)(NativeMethods.SHGFI.SHGFI_TYPENAME | NativeMethods.SHGFI.SHGFI_USEFILEATTRIBUTES);

                NativeMethods.SHGetFileInfo(fileName, dwFileAttributes, ref info, (uint)Marshal.SizeOf(info), uFlags);

                if (info.szTypeName.Length == 0)
                {
                    if (sFile.EndsWith(".CSV", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_CSV; }
                    else if (sFile.EndsWith(".XML", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_XML; }
                    else if (sFile.EndsWith(".XLS", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_XLS; }
                    else if (sFile.EndsWith(".XLSX", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_XLS; }
                    else if (sFile.EndsWith(".JSON", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_JSON; }
                    else if (sFile.EndsWith(".JS", StringComparison.OrdinalIgnoreCase))
                    { RequestedBDD = SQLTools_Enums.BDD.FI_JSON; }
                }
                else
                {
                    if (info.szTypeName.ToUpper().Contains("CSV") || info.szTypeName.ToUpper().Contains("TXT") || info.szTypeName.ToUpper().Contains("TEXT"))
                    {
                        if (RequestedBDD != SQLTools_Enums.BDD.FI_CSV)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_typechange_wrong01, sFile, Languages.Languages.fi_typechange_wrong02, info.szTypeName), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                        RequestedBDD = SQLTools_Enums.BDD.FI_CSV;
                    }
                    else if (info.szTypeName.ToUpper().Contains("XML"))
                    {
                        if (RequestedBDD != SQLTools_Enums.BDD.FI_XML)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_typechange_wrong01, sFile, Languages.Languages.fi_typechange_wrong02, info.szTypeName), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                        RequestedBDD = SQLTools_Enums.BDD.FI_XML;
                    }
                    else if (info.szTypeName.ToUpper().Contains("XLS") || info.szTypeName.ToUpper().Contains("XLSX") || info.szTypeName.ToUpper().Contains("EXCEL"))
                    {
                        if (RequestedBDD != SQLTools_Enums.BDD.FI_XLS)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_typechange_wrong01, sFile, Languages.Languages.fi_typechange_wrong02, info.szTypeName), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                        RequestedBDD = SQLTools_Enums.BDD.FI_XLS;
                    }
                    else if (info.szTypeName.ToUpper().Contains("JSON") || info.szTypeName.ToUpper().Contains("JSO") || info.szTypeName.ToUpper().Contains("JS"))
                    {
                        if (RequestedBDD != SQLTools_Enums.BDD.FI_JSON)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.fi_typechange_wrong01, sFile, Languages.Languages.fi_typechange_wrong02, info.szTypeName), SQLTools_Enums.LOG_TYPEINFO.INF);
                        }
                        RequestedBDD = SQLTools_Enums.BDD.FI_JSON;
                    }
                }
            }

            return RequestedBDD;
        }

        //private static string GetCellValue(SpreadsheetDocument document, Cell cell)
        //{
        //    SharedStringTablePart stringTablePart = document.WorkbookPart.SharedStringTablePart;
        //    string sValue = "";

        //    try
        //    { sValue = cell.CellValue.InnerXml; }
        //    catch (Exception)
        //    { sValue = ""; }


        //    if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        //    { return stringTablePart.SharedStringTable.ChildElements[Int32.Parse(sValue)].InnerText.Trim(); }
        //    else
        //    { return sValue.Trim(); }
        //}

        private class FilePattern
        {
            public StreamWriter SW
            {
                get; set;
            }
            public string SFILE
            {
                get; set;
            }
        }

        internal static List<string> GetFileContentAsListOfString(string sPossibleFile, CONNString csF, List<string> sDynParams)
        {
            List<string> sData = new();

            StreamReader sR = null;

            //on teste le chemin en dur, et si on a rien, on teste le chemin fuzible
            if (File.Exists(sPossibleFile))
            {
                sR = File.OpenText(sPossibleFile);
            }
            else if (File.Exists(Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Fuzible", sPossibleFile)))
            {
                sR = File.OpenText(Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Fuzible", sPossibleFile));
            }
            else if (csF.SConnDriverSuffix.Equals("FI"))
            {
                string sPath = csF.SConnString(sDynParams);
                if (sPath.LastIndexOf("\\") > 0)
                {
                    sPath = sPath[..(sPath.LastIndexOf("\\") + 1)];
                }

                if (File.Exists(Path.Combine(sPath, sPossibleFile)))
                {
                    sR = File.OpenText(Path.Combine(sPath, sPossibleFile));
                }
            }

            if (sR != null)
            {
                while (sR.Peek() > -1)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                    sData.Add(sR.ReadLine());
                }
            }

            return sData;
        }

        #endregion

    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        public static class FILE_ATTRIBUTE
        {
            public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        }

        public static class SHGFI
        {
            public const uint SHGFI_TYPENAME = 0x000000400;
            public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);
    }

    public class NetworkConnection : IDisposable
    {
        private string _networkName;
        public NetworkConnection(string networkName, NetworkCredential credentials)
        {
            _networkName = networkName;

            dynamic netResource = new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = networkName
            };

            dynamic result = WNetAddConnection2(netResource, credentials.Password, credentials.UserName, 0);

            if (result != 0)
            {
                throw new IOException("Error connecting to remote share", result);
            }
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            WNetCancelConnection2(_networkName, 0, true);
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplaytype DisplayType;
        public int Usage;
        public string LocalName;
        public string RemoteName;
        public string Comment;
        public string Provider;
    }

    public enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork,
        Remembered,
        Recent,
        Context
    }

    public enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2,
        Reserved = 8
    }

    public enum ResourceDisplaytype : int
    {
        Generic = 0x0,
        Domain = 0x1,
        Server = 0x2,
        Share = 0x3,
        File = 0x4,
        Group = 0x5,
        Network = 0x6,
        Root = 0x7,
        Shareadmin = 0x8,
        Directory = 0x9,
        Tree = 0xa,
        Ndscontainer = 0xb
    }

    /// <summary>
    /// Provides access to a network share.
    /// </summary>
    public class NetworkShareAccesser : IDisposable
    {
        private string _remoteUncName;
        private string _remoteComputerName;

        public string RemoteComputerName
        {
            get
            {
                return this._remoteComputerName;
            }
            set
            {
                this._remoteComputerName = value;
                this._remoteUncName = @"\\" + this._remoteComputerName;
            }
        }

        public string UserName
        {
            get;
            set;
        }
        public string Password
        {
            get;
            set;
        }

        #region Consts

        private const int RESOURCE_CONNECTED = 0x00000001;
        private const int RESOURCE_GLOBALNET = 0x00000002;
        private const int RESOURCE_REMEMBERED = 0x00000003;

        private const int RESOURCETYPE_ANY = 0x00000000;
        private const int RESOURCETYPE_DISK = 0x00000001;
        private const int RESOURCETYPE_PRINT = 0x00000002;

        private const int RESOURCEDISPLAYTYPE_GENERIC = 0x00000000;
        private const int RESOURCEDISPLAYTYPE_DOMAIN = 0x00000001;
        private const int RESOURCEDISPLAYTYPE_SERVER = 0x00000002;
        private const int RESOURCEDISPLAYTYPE_SHARE = 0x00000003;
        private const int RESOURCEDISPLAYTYPE_FILE = 0x00000004;
        private const int RESOURCEDISPLAYTYPE_GROUP = 0x00000005;

        private const int RESOURCEUSAGE_CONNECTABLE = 0x00000001;
        private const int RESOURCEUSAGE_CONTAINER = 0x00000002;


        private const int CONNECT_INTERACTIVE = 0x00000008;
        private const int CONNECT_PROMPT = 0x00000010;
        private const int CONNECT_REDIRECT = 0x00000080;
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;
        private const int CONNECT_COMMANDLINE = 0x00000800;
        private const int CONNECT_CMD_SAVECRED = 0x00001000;

        private const int CONNECT_LOCALDRIVE = 0x00000100;

        #endregion

        #region Errors

        private const int NO_ERROR = 0;

        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_ALREADY_ASSIGNED = 85;
        private const int ERROR_BAD_DEVICE = 1200;
        private const int ERROR_BAD_NET_NAME = 67;
        private const int ERROR_BAD_PROVIDER = 1204;
        private const int ERROR_CANCELLED = 1223;
        private const int ERROR_EXTENDED_ERROR = 1208;
        private const int ERROR_INVALID_ADDRESS = 487;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_INVALID_PASSWORD = 1216;
        private const int ERROR_MORE_DATA = 234;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_NO_NET_OR_BAD_PATH = 1203;
        private const int ERROR_NO_NETWORK = 1222;

        private const int ERROR_BAD_PROFILE = 1206;
        private const int ERROR_CANNOT_OPEN_PROFILE = 1205;
        private const int ERROR_DEVICE_IN_USE = 2404;
        private const int ERROR_NOT_CONNECTED = 2250;
        private const int ERROR_OPEN_FILES = 2401;

        #endregion

        #region PInvoke Signatures

        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
            IntPtr hwndOwner,
            NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUserID,
            int dwFlags,
            string lpAccessName,
            string lpBufferSize,
            string lpResult
            );

        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce
            );

        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope = 0;
            public int dwType = 0;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }

        #endregion

        /// <summary>
        /// Creates a NetworkShareAccesser for the given computer name. The user will be promted to enter credentials
        /// </summary>
        /// <param name="remoteComputerName"></param>
        /// <returns></returns>
        public static NetworkShareAccesser Access(string remoteComputerName)
        {
            return new NetworkShareAccesser(remoteComputerName);
        }

        /// <summary>
        /// Creates a NetworkShareAccesser for the given computer name using the given domain/computer name, username and password
        /// </summary>
        /// <param name="remoteComputerName"></param>
        /// <param name="domainOrComuterName"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        public static NetworkShareAccesser Access(string remoteComputerName, string domainOrComuterName, string userName, string password)
        {
            return new NetworkShareAccesser(remoteComputerName,
                                            domainOrComuterName + @"\" + userName,
                                            password);
        }

        /// <summary>
        /// Creates a NetworkShareAccesser for the given computer name using the given username (format: domainOrComputername\Username) and password
        /// </summary>
        /// <param name="remoteComputerName"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        public static NetworkShareAccesser Access(string remoteComputerName, string userName, string password)
        {
            return new NetworkShareAccesser(remoteComputerName,
                                            userName,
                                            password);
        }

        private NetworkShareAccesser(string remoteComputerName)
        {
            RemoteComputerName = remoteComputerName;

            this.ConnectToShare(this._remoteUncName, null, null, true);
        }

        private NetworkShareAccesser(string remoteComputerName, string userName, string password)
        {
            RemoteComputerName = remoteComputerName;
            UserName = userName;
            Password = password;

            this.ConnectToShare(this._remoteUncName, this.UserName, this.Password, false);
        }

        private void ConnectToShare(string remoteUnc, string username, string password, bool promptUser)
        {
            NETRESOURCE nr = new()
            {
                dwType = RESOURCETYPE_DISK,
                lpRemoteName = remoteUnc
            };

            int result;
            if (promptUser)
            {
                result = WNetUseConnection(IntPtr.Zero, nr, "", "", CONNECT_INTERACTIVE | CONNECT_PROMPT, null, null, null);
            }
            else
            {
                result = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);
            }

            if (result != NO_ERROR)
            {
                throw new System.ComponentModel.Win32Exception(result);
            }
        }

        private void DisconnectFromShare(string remoteUnc)
        {
            int result = WNetCancelConnection2(remoteUnc, CONNECT_UPDATE_PROFILE, false);
            if (result != NO_ERROR)
            {
                throw new System.ComponentModel.Win32Exception(result);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            this.DisconnectFromShare(this._remoteUncName);
        }
    }

    internal class CsvHelper
    {
        private string file;
        private FileStream fs;
        private StreamReader sr;

        public CsvHelper(string file, FileMode open, FileAccess read, FileShare readWrite, Encoding encoder)
        {
            this.file = file;
            this.fs = new FileStream(file, open, read, readWrite);
            this.sr = new(fs, encoder);
        }

        public static List<string> ParseCsvColumns(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            fields.Add(sb.ToString());
            return fields;
        }

        public bool Read()
        {
            if (sr != null && !sr.EndOfStream)
            { 
                return true; 
            }
            else
            {
                return false;
            }
        }

        public string ReadCSVLine()
        {
            if (sr.EndOfStream)
                return null;

            StringBuilder sb = new();
            bool inQuotes = false;

            while (true)
            {
                int c = sr.Read();
                if (c == -1)
                    break;

                char ch = (char)c;

                if (ch == '"')
                {
                    if (inQuotes && sr.Peek() == '"')
                    {
                        sb.Append('"');
                        sr.Read();
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                        sb.Append('"');
                    }
                    continue;
                }

                if (!inQuotes)
                {
                    if (ch == '\n')
                        break;

                    if (ch == '\r')
                    {
                        if (sr.Peek() == '\n')
                            sr.Read();
                        break;
                    }
                }

                sb.Append(ch);
            }

            if (sb.Length == 0 && sr.EndOfStream)
                return null;

            return sb.ToString();
        }

        internal void Close()
        {
            fs.Close();
        }

        internal int CountRows()
        {
            int iR = 0;
            while (Read())
            {
                iR++;
                ReadCSVLine();
            }
            return iR;
        }
    }
}