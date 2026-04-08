using DataConvert;
using Microsoft.EntityFrameworkCore;
using MihaZupan;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using Renci.SshNet;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FuzibleFramework
{

    public class WSTools
    {
        #region "CONSTANTES"
        private static readonly string QUERY_SOQL_OBJECTS = "SELECT SObjectType FROM ObjectPermissions GROUP BY SObjectType ORDER BY SObjectType ASC";
        private static readonly string QUERY_SOQL_ATTRIBUTES = "SELECT FIELDS(ALL) FROM [OBJECT] LIMIT 1";
        private static readonly string QUERY_OQL_JSON_TEMPLATE = "{\"operation\": \"core/get\",\"class\": \"{CLASS}\",\"key\": \"{QUERY}\",\"output_fields\": \"{FIELDS}\"}"; //SELECT Person WHERE email LIKE '%.com'
        #endregion

        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose { get; }
        public LogTools MyLog;
        public Job JobParameters;
        private readonly WebserviceConnectionVariables WSVariables = null;

        private static readonly string WORKING_DIRECTORY = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\WEBSERVICES_LOG\\";

        private readonly CONNString Connection = null;

        #endregion

        #region "PROPRIETES"

        #endregion

        #region "PUBLIC VOID"

        public WSTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            MyLog = LogJob;
            JobParameters = INIP;

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.SRC:
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.SRC;
                    Connection = INIP.ConnectionString_Source;
                    WSVariables = new WebserviceConnectionVariables(INIP.ConnectionString_Source, INIP.GlobalParameters.WS_TIMEOUT, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG:
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.TRG;
                    Connection = INIP.ConnectionString_Target;
                    WSVariables = new WebserviceConnectionVariables(INIP.ConnectionString_Target, INIP.GlobalParameters.WS_TIMEOUT, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.LOG:
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.LOG;
                    Connection = new CONNString(INIP.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", INIP.GlobalParameters.LOG_CONNECTIONSTRING) { };
                    CONNString WSLog = new(INIP.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", INIP.GlobalParameters.LOG_CONNECTIONSTRING);
                    WSVariables = new WebserviceConnectionVariables(WSLog, INIP.GlobalParameters.WS_TIMEOUT, INIP.DynParams);
                    break;
            }

        }

        public async Task<DataSet> GetDataFromWebService(Query FuzibleQuery)
        {
            Query FuzibleQueryAsync = FuzibleQuery.DeepCopy();

            DataSet dsData = null;

            try
            {
                switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                {
                    case "NUXEO":
                        if (JobParameters.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL)
                        {
                            dsData = await GetDataFromNUXEO(FuzibleQueryAsync);
                        }
                        else
                        {
                            dsData = await GetDataFromNUXEO(FuzibleQueryAsync);
                        }
                        break;
                    default:
                        string sTokenA = "";
                        sTokenA = await GetToken(FuzibleQuery);
                        if (JobParameters.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL)
                        {
                            dsData = await GetDataFromREST(FuzibleQueryAsync, sTokenA, JobParameters.WebServiceCallMethod);
                        }
                        else
                        {
                            dsData = await GetDataFromREST_UnknownSQL(FuzibleQueryAsync, sTokenA, JobParameters.WebServiceCallMethod);
                        }
                        break;
                }

                FuzibleQuery = FuzibleQueryAsync;

                if (dsData != null) { dsData.CaseSensitive = false; }

                return dsData;
            }
            catch (OperationCanceledException)
            { throw; }
        }

        public async Task SendDataToWebservice(Query FuzibleQuery, DataTable dtData, string sWSName)
        {
            string sToken = await GetToken(FuzibleQuery);
            List<Tuple<string, object>> sInputs = CreateRESTQueriesFromDataset(JobParameters, ref MyLog, dtData, FuzibleQuery.QueryAnalyzer.Tables[0].Alias);

            switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
            {
                default:
                    //TODO
                    await SendDataToWebServiceUsingREST(sToken, sWSName, sInputs, dtData, FuzibleQuery);
                    break;
            }
        }

        public static List<Tuple<string, object>> CreateRESTQueriesFromDataset(Job JobParameters, ref LogTools MyLog, DataTable dtData, string sAlias)
        {

            if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("FI") && JobParameters.FileRawOutput && JobParameters.WebserviceContentStructure.Length == 0)
            {
                List<Tuple<string, object>> sListQueries = new();
                foreach (DataRow dr in dtData.Rows) { sListQueries.Add(new Tuple<string, object>(dr[0].ToString(), dr[0])); }
                return sListQueries;
            }
            else if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("WS") && JobParameters.WebserviceRawOutput && JobParameters.WebserviceContentStructure.Length == 0)
            {
                List<Tuple<string, object>> sListQueries = new();
                foreach (DataRow dr in dtData.Rows) { sListQueries.Add(new Tuple<string, object>(dr[0].ToString(), dr[0])); }
                return sListQueries;
            }
            else if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("FI") && JobParameters.FileRawOutput && JobParameters.WebserviceContentStructure.Length > 0)
            {
                List<Tuple<string, object>> sListQueries = CreateWSContentFromStructure(JobParameters.WebserviceContentStructure, dtData);
                return sListQueries;
            }
            else if (JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("WS") && JobParameters.WebserviceRawOutput && JobParameters.WebserviceContentStructure.Length > 0)
            {
                List<Tuple<string, object>> sListQueries = CreateWSContentFromStructure(JobParameters.WebserviceContentStructure, dtData);
                return sListQueries;
            }
            else
            {
                if (JobParameters.WebserviceContentStructure.Length > 0)
                {
                    List<Tuple<string, object>> sListQueries = CreateWSContentFromStructure(JobParameters.WebserviceContentStructure, dtData);
                    return sListQueries;
                }
                else
                {
                    if (JobParameters.WebserviceHTTP_SendColumnsOffset >= dtData.Columns.Count)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.ws_createrestquery_offset01, JobParameters.WebserviceHTTP_SendColumnsOffset.ToString(), Languages.Languages.ws_createrestquery_offset02, dtData.Columns.Count.ToString(), Languages.Languages.ws_createrestquery_offset03), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        JobParameters.WebserviceHTTP_SendColumnsOffset = 0;
                    }

                    switch (JobParameters.WebServiceContentType_Target)
                    {
                        case SQLTools_Enums.WEBSERVICE_CONTENT.JSON:
                            return JsonParser.DataTableToJsonStringList(dtData, sAlias, false, true, JobParameters.WebserviceHTTP_SendColumnsOffset);

                        case SQLTools_Enums.WEBSERVICE_CONTENT.XML:
                            //TODO return new List<string> { }
                            return new List<Tuple<string, object>>();

                        case SQLTools_Enums.WEBSERVICE_CONTENT.HTTP_PARAMS:
                            List<Tuple<string, object>> sListQueries = new();
                            StringBuilder sbRow = new();
                            foreach (DataRow dtR in dtData.Rows)
                            {
                                sbRow.Clear();
                                for (int cptC = JobParameters.WebserviceHTTP_SendColumnsOffset; cptC < dtData.Columns.Count; cptC++)
                                {
                                    if (JobParameters.WebserviceHTTP_DontSendEmptyValues && dtR[cptC].ToString().Length == 0) //si le champ est vide on envoie rien
                                    { }
                                    else
                                    {
                                        if (cptC > JobParameters.WebserviceHTTP_SendColumnsOffset) { sbRow.Append('&'); }
                                        sbRow.Append(dtData.Columns[cptC].ColumnName.ToLower());
                                        sbRow.Append('=');
                                        sbRow.Append(JobParameters.TrimData ? dtR[cptC].ToString().Trim().Replace("&", ",") : dtR[cptC].ToString().Replace("&", ","));
                                    }
                                }

                                if (JobParameters.WebserviceHTTP_FormatURLInUpper)
                                {
                                    string sUpper = sbRow.ToString().ToUpper();
                                    sbRow.Clear();
                                    sbRow.Append(sUpper);
                                }
                                if (sbRow.ToString().Length > 0)
                                { sListQueries.Add(new Tuple<string, object>(sbRow.ToString(), sbRow)); }
                            }
                            return sListQueries;
                        default:
                            return new List<Tuple<string, object>>();
                    }
                }
            }
        }

        public static List<Tuple<string, object>> CreateWSContentFromStructure(string sStructure, DataTable dtData)
        {
            List<Tuple<string, object>> sListQueries = new();
            foreach (DataRow dr in dtData.Rows)
            {
                string sData = sStructure;
                object oData = null;
                foreach (DataColumn col in dtData.Columns)
                {
                    string sValue = dr[col].ToString();
                    if (dr[col].GetType() == Type.GetType("System.Byte[]"))
                    {
                        sValue = Convert.ToBase64String((byte[])dr[col]);
                        oData = dr[col];
                    }
                    else if (dr[col].GetType() == Type.GetType("System.DateTime")) //formattage de la date au format  ISO 8601 UTC
                    {
                        DateTime dtTest;
                        if (DateTime.TryParse(sValue, out dtTest))
                        {
                            sValue = dtTest.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        }
                    }
                    else if (dr[col].GetType() == Type.GetType("System.DateTimeOffset")) //formattage de la date au format  ISO 8601 UTC
                    {
                        DateTime dtTest;
                        if (sValue.Length > 0)
                        {
                            dtTest = ((DateTimeOffset)dr[col]).UtcDateTime;
                            sValue = dtTest.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        }
                    }

                    sData = sData.Replace("[" + col.ColumnName + "]", sValue, StringComparison.OrdinalIgnoreCase);
                }
                sListQueries.Add(new Tuple<string, object>(sData, oData));
            }
            return sListQueries;
        }

        public static List<string> GetDomainSiteMap()
        {
            List<string> sListURL = new();

            return sListURL;
        }

        public class WebserviceConnectionVariables
        {
            private string _sProxy = "";

            public string WebServiceURL { get; set; } = "";
            public List<string> WebServiceHeader { get; set; } = new List<string>();
            public string WebServiceProxyURL { get { return _sProxy; } set { _sProxy = value.ToLower(); } }
            public int WebServiceProxyPort { get; set; } = 0;
            public int ConnectionTimeout { get; set; } = 30000;
            public ProxyTypes WebServiceProxyType { get; set; } = ProxyTypes.None;
            public string WebServiceProxyUsername { get; internal set; } = "";
            public string WebServiceProxyPassword { get; internal set; } = "";
            public WebserviceConnectionVariables(CONNString CS, int iTimeout, List<string> sListVariableParameters)
            {
                ConnectionTimeout = iTimeout;

                string[] sSplitURL = { "URL=", "url=", "Url=" };

                //découpage de la chaine de connexion du webservice pour intégration des variables
                if (CS.SConnString(sListVariableParameters).ToLower().IndexOf("url=") > -1)
                { WebServiceURL = CS.SConnString(sListVariableParameters).Split(sSplitURL, StringSplitOptions.None)[1].Split(Convert.ToChar(";"))[0]; }
                else { WebServiceURL = CS.SConnString(sListVariableParameters); }

                string sHeaders = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_HEADERS);
                if (sHeaders.Length > 0)
                {
                    string[] sArrayHeader = sHeaders.Split(Convert.ToChar(";"));
                    List<string> sListHeaders = new();
                    foreach (string sH in sArrayHeader)
                    {
                        sListHeaders.Add(sH.Replace("{", "").Replace("}", "").Replace("\"", ""));
                    }
                    WebServiceHeader = sListHeaders;
                }

                WebServiceProxyURL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_URL), sListVariableParameters);
                WebServiceProxyPort = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_PORT).Length > 0 ? Convert.ToInt32(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_PORT)) : 0;

                try
                {
                    WebServiceProxyType = (ProxyTypes)Enum.Parse(typeof(ProxyTypes), Toolbox.ToUpperFirstLetter(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_TYPE)));
                    WebServiceProxyUsername = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_USERNAME);
                    WebServiceProxyPassword = CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_PROXY_PASSWORD);
                }
                catch { }
            }

        }

        #endregion

        #region "PRIVATE VOID"

        private async Task SendDataToWebServiceUsingREST(string sToken, string sWSPath, List<Tuple<string, object>> sListInputs, DataTable dtSourceQuery, Query FuzibleQuery)
        {
            string sContentType = GetContentTypeString(JobParameters.WebServiceContentType_Target);
            DataSet dsResponse;
            List<Tuple<RestResponse, string>> sAnswers = new();

            RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);

            try
            {
                //HttpClient wsRest = GetRestConnexion(true);
                //wsRest.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(sContentType));
                //if (sToken.Length > 0) { wsRest.DefaultRequestHeaders.Add(sToken.Split(Convert.ToChar("="))[0], sToken.Split(Convert.ToChar("="))[1]); }
                int cptQ = 0;
                string sWSName = "";
                string sUrl = "";

                foreach (Tuple<string, object> sQData in sListInputs)
                {
                    switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                    {
                        case "MICROSOFT_ONEDRIVE":
                            sUrl = WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0];
                            if (sUrl.EndsWith("/")) { sUrl = sUrl[0..^1]; }

                            sUrl = sUrl[0..(sUrl.IndexOf("/drives/", StringComparison.OrdinalIgnoreCase) + 1)];
                            string sSubUrl = "";

                            if (WSVariables.WebServiceURL.IndexOf('?') > 0)
                            {
                                sSubUrl = WSVariables.WebServiceURL.Split('?')[0].Replace(sUrl, "");
                            }
                            else
                            {
                                sSubUrl = WSVariables.WebServiceURL;
                            }
                            ///drives/{?1}/items/{?3}:/[RAWFILE_NAME]:/content
                            string sOutput = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(FuzibleQuery.OutputTable, JobParameters.DynParams);
                            foreach (DataColumn dt in dtSourceQuery.Columns)
                            {
                                if (sOutput.Contains("[" + dt.ColumnName + "]"))
                                {
                                    sOutput = sOutput.Replace("[" + dt.ColumnName + "]", dtSourceQuery.Rows[cptQ][dt.ColumnName].ToString());
                                    break;
                                }
                                else if (sOutput.IndexOf("*") > -1)
                                {
                                    sOutput = sOutput.Replace("*", FuzibleQuery.QueryAnalyzer.Tables[0].Name);
                                    break;
                                }
                            }
                            
                            //format items/itemid:/file:/content
                            //if (!sSubUrl.EndsWith(":/")) { sSubUrl = sSubUrl.Insert(sSubUrl.Length - 1, ":"); }

                            sWSName = string.Concat(sSubUrl, (sSubUrl.EndsWith('/') ? "" : ":/"), sOutput.Replace("\\", "/"), ":/content");
                            RSClient = GetRSConnexion(sUrl.Length == 0 ? sWSName : sUrl);

                            break;
                        default:
                            sWSName = sWSPath;

                            if (dtSourceQuery.Rows.Count > cptQ)
                            {
                                //recherche colonne SQL scriptée 
                                foreach (DataColumn dt in dtSourceQuery.Columns)
                                {
                                    if (sWSName.Contains("[" + dt.ColumnName + "]"))
                                    {
                                        sWSName = sWSName.Replace("[" + dt.ColumnName + "]", dtSourceQuery.Rows[cptQ][dt.ColumnName].ToString()).Replace("\\", "/");
                                        break;
                                    }
                                    else if (sWSName.IndexOf("*") > -1)
                                    {
                                        sWSName = sWSName.Replace("*", FuzibleQuery.QueryAnalyzer.Tables[0].Name).Replace("\\", "/");
                                        break;
                                    }
                                }
                            }
                            break;
                    }

                    cptQ++;

                    if (cptQ % 100 == 0) //Log de l'avancement
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_senddata_sent + cptQ.ToString() + "/" + sListInputs.Count.ToString() + Toolbox.GetPercent(0, sListInputs.Count, cptQ), SQLTools_Enums.LOG_TYPEINFO.DET); }

                    if (!JobParameters.RunInSimulationMode)
                    {
                        Method rsMethod = Method.Post;
                        switch (JobParameters.WebServiceCallMethod_Target)
                        {
                            case SQLTools_Enums.WEBSERVICE_METHOD.DELETE:
                                rsMethod = Method.Delete;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.GET:
                                rsMethod = Method.Get;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.PATCH:
                                rsMethod = Method.Patch;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.POST:
                                rsMethod = Method.Post;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.PUT:
                                rsMethod = Method.Put;
                                break;
                        }

                        RestRequest request = null;
                        if (sWSName.IndexOf("=") > 0 && !sWSName.EndsWith("="))
                        {
                            request = new RestRequest
                            {
                                Method = rsMethod
                            };
                            request.AddQueryParameter(sWSName[..sWSName.IndexOf("=")], sWSName[(sWSName.IndexOf("=") + 1)..]);
                        }
                        else
                        {
                            if (sWSName.StartsWith("/") && WSVariables.WebServiceURL.EndsWith("/"))
                            { sWSName = sWSName[1..]; }
                            request = new RestRequest(sWSName, rsMethod);
                        }

                        //si l'adresse du WS contient déjà des paramètres HTTP on les redéfinit proprement
                        if (WSVariables.WebServiceURL.IndexOf("?") > 0)
                        {
                            string sSubURL = WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[1];
                            string[] sQP = sSubURL.Split(Convert.ToChar("&"));
                            foreach (string sP in sQP)
                            { request.AddQueryParameter(sP[..sP.IndexOf("=")], sP[(sP.IndexOf("=") + 1)..]); }
                        }

                        //gestion de l'authentification (job parameters)
                        SetAuthentification(request, sToken);

                        ////param HTTP optionnels
                        //foreach (string sP in JobParameters.WebserviceHTTPParameters.Split('&'))
                        //{
                        //    request.AddQueryParameter(sP.Substring(0, sP.IndexOf("=")), sP[(sP.IndexOf("=") + 1)..]);
                        //}

                        //gestion de l'header (connexion param)
                        foreach (string sH in WSVariables.WebServiceHeader)
                        {
                            char sSep = sH.IndexOf(":") > -1 ? Convert.ToChar(":") : Convert.ToChar("=");
                            string[] sHsplit = sH.Split(sSep);
                            if (sHsplit.Length > 1)
                            { request.AddHeader(sHsplit[0], sHsplit[1]); }
                        }

                        bool bHasMimeType = false;
                        foreach (var param in request.Parameters)
                        {
                            if (param.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            { bHasMimeType = true; break; }
                        }

                        switch (JobParameters.WebServiceContentType_Target)
                        {
                            case SQLTools_Enums.WEBSERVICE_CONTENT.JSON:
                                if (!bHasMimeType)
                                {
                                    request.AddHeader("Accept", sContentType);
                                    request.AddHeader("Content-Type", sContentType);
                                }
                                request.AddJsonBody(sQData.Item1);
                                break;
                            case SQLTools_Enums.WEBSERVICE_CONTENT.HTTP_PARAMS:
                                if (!bHasMimeType)
                                {
                                    request.AddHeader("Accept", sContentType);
                                    request.AddHeader("Content-Type", sContentType);
                                }
                                string[] sQSplit = sQData.Item1.Split(Convert.ToChar("&"));
                                foreach (string sP in sQSplit)
                                {
                                    if (sP.IndexOf("=") > 0)
                                    {
                                        string sData = sP[(sP.IndexOf("=") + 1)..].Replace(Environment.NewLine, "");
                                        //sData = sData.Replace(": \"", ":\""); //cleanup bien looseux
                                        //sData = sData.Replace("\" :", "\":"); //cleanup bien looseux
                                        //sData = sData.Replace(", \"", ",\""); //cleanup bien looseux
                                        //sData = sData.Replace("\", ", "\","); //cleanup bien looseux
                                        //JsonParser js = new JsonParser(sData, "");
                                        //if (js.IsValid)
                                        //{
                                        //    try
                                        //    {
                                        //        js.JsonToDataSet();
                                        //        if (js.Success)
                                        //        {
                                        //            sData = JsonConvert.SerializeObject(sData);
                                        //            if (sData.StartsWith("\"") && sData.EndsWith("\""))
                                        //            { sData = sData[1..^1]; }
                                        //        }
                                        //    }
                                        //    catch { }
                                        //}
                                        request.AddQueryParameter(sP[..sP.IndexOf("=")], sData);
                                    }
                                    else
                                    {
                                        Exception ex = new(Languages.Languages.ws_senddata_cantsplitquery + sQData.Item1);
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                                        FuzibleQuery.QueryErrors += 1;
                                    }
                                }
                                break;
                            case SQLTools_Enums.WEBSERVICE_CONTENT.XML:
                                if (!bHasMimeType)
                                {
                                    request.AddHeader("Accept", sContentType);
                                    request.AddHeader("Content-Type", sContentType);
                                }
                                request.AddXmlBody(sQData.Item1);
                                break;
                            case SQLTools_Enums.WEBSERVICE_CONTENT.TXT:
                                if (!bHasMimeType)
                                {
                                    request.AddHeader("Accept", sContentType);
                                    request.AddHeader("Content-Type", sContentType);
                                    request.AddParameter(sContentType, sQData.Item1, ParameterType.RequestBody);
                                }
                                else { request.AddParameter("text/plain", sQData.Item1, ParameterType.RequestBody); }
                                break;
                            case SQLTools_Enums.WEBSERVICE_CONTENT.BINARY:
                                if (!bHasMimeType)
                                {
                                    string sMimeType = "application/octet-stream";
                                    Match mcExt = Regex.Match(Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(FuzibleQuery.OutputTable, JobParameters.DynParams), "\\.[A-z0-9]{2,}");
                                    if (mcExt.Success)
                                    {
                                        sMimeType = Toolbox.GetMimeTypeFromExt(mcExt.Value);
                                    }
                                    else
                                    {
                                        switch (JobParameters.ConnectionString_Source.SConnDriver)
                                        {
                                            case SQLTools_Enums.BDD.FI_CSV:
                                                sMimeType = "text/csv";
                                                break;
                                            case SQLTools_Enums.BDD.FI_XML:
                                                sMimeType = "text/xml";
                                                break;
                                            case SQLTools_Enums.BDD.FI_XLS:
                                                sMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                                                break;
                                            case SQLTools_Enums.BDD.FI_JSON:
                                                sMimeType = "application/json";
                                                break;
                                            default:
                                                sMimeType = "text/plain";
                                                break;
                                        }
                                    }
                                    request.AddHeader("Content-Type", sMimeType);
                                    //request.AddHeader("Content-Length", sQ.Length);
                                    request.AddParameter(sMimeType, sQData.Item2 ?? sQData.Item1, ParameterType.RequestBody);
                                }
                                else
                                {
                                    //request.AddFile("calcul_notedefrais.xlsx", @"C:\Users\Public\Documents\FUZIBLE\FILES\calcul_notedefrais.xlsx");
                                    //request.AddHeader("Content-Length", sQ.Length);
                                    request.AddParameter(request.Parameters.First(p => p.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value.ToString(), sQData.Item2 ?? sQData.Item1, ParameterType.RequestBody);
                                }
                                if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) != "MICROSOFT_ONEDRIVE" && JobParameters.WebserviceSpecialHttpParameters.Length > 0)
                                {
                                    string sParameters = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebserviceSpecialHttpParameters, JobParameters.DynParams);
                                    foreach (string sParam in sParameters.Split(';'))
                                    {
                                        if (sParam.IndexOf("=") > 0)
                                        {
                                            request.AddParameter(sParam.Split('=')[0], sParam.Split('=')[1]);
                                        }
                                    }
                                }

                                //request.AddHeader("Content-Type", "application/octet-stream");
                                //request.AddHeader("Content-Length", sQ.Length);
                                //request.AddFile("test", @"C:\Users\Public\Documents\FUZIBLE\FILES\SAMPLE.CSV");
                                //request.AddParameter("application/octet-stream", sQ, ParameterType.RequestBody);
                                break;
                        }

                        RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);
                        string content = response.Content;

                        if (response.ErrorException == null)
                        {
                            if (content.Length > 0)
                            {
                                //interprétation des réponses
                                if (response.ContentType != null)
                                {
                                    string sCharSet = "ISO-8859-1";
                                    if (response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) > -1)
                                    {
                                        sCharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..];
                                        sCharSet = sCharSet.Split(Convert.ToChar(";"))[0];
                                    }
                                    var encoding = Encoding.GetEncoding(sCharSet);
                                    string result = encoding.GetString(response.RawBytes);
                                    sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                                }
                                else
                                {
                                    string sCharSet = "ISO-8859-1";
                                    var encoding = Encoding.GetEncoding(sCharSet);
                                    string result = response.RawBytes != null ? encoding.GetString(response.RawBytes) : response.StatusCode.ToString();
                                    sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                                }

                                //mise à jour des champs optionnels onedrive
                                if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE" && JobParameters.WebserviceSpecialHttpParameters.Length > 0)
                                {
                                    string sParameters = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebserviceSpecialHttpParameters, JobParameters.DynParams);
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_updateonedrivefield + sParameters, SQLTools_Enums.LOG_TYPEINFO.INF);

                                    RSClient = GetRSConnexion(sUrl);
                                    request = new RestRequest(sWSName.Replace(":/content", ":/listItem/fields"), Method.Patch);

                                    request.AddHeader("Content-Type", "application/json");

                                    // Ajouter le jeton d'accès au header de la requête
                                    SetAuthentification(request, sToken);

                                    // Ajouter la description dans le corps de la requête en tant que JSON
                                    var body = new Dictionary<string, object>();

                                    foreach (string sParam in sParameters.Split(';'))
                                    {
                                        string sKey = sParam.Split('=')[0];
                                        string sValue = sParam.Split('=')[1];
                                        //2023-05-22T07:27:57Z

                                        DateTime dtTest;
                                        if (DateTime.TryParse(sValue, out dtTest))
                                        {
                                            sValue = dtTest.ToString("yyyy-MM-ddTHH:mm:ssZ");
                                        }

                                        if (sParam.IndexOf("=") > 0)
                                        {
                                            body[sKey] = sValue;
                                        }
                                    }
                                    request.AddJsonBody(body);

                                    // Exécuter la requête de mise à jour de la description
                                    response = RSClient.Execute(request);

                                    // Vérifier si la requête de mise à jour a réussi
                                    if (response.IsSuccessful)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_updateonedrivefield_ok + response.StatusDescription != null ? response.StatusDescription : response.Content, SQLTools_Enums.LOG_TYPEINFO.INF);
                                    }
                                    else
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_updateonedrivefield_ko + response.StatusDescription != null ? response.StatusDescription : response.Content, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }
                            }
                            else
                            {
                                sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                            }
                        }
                        else
                        {
                            switch (response.StatusCode)
                            {
                                case System.Net.HttpStatusCode.NotFound:
                                    string sNotFound = string.Concat("Data : ", sQData.Item1.Replace(Environment.NewLine, " "), " - [", response.Content ?? "", ",", response.Server ?? "", ",", response.ErrorMessage == null ? "" : response.ErrorMessage.ToString(), ",", response.ResponseUri.ToString(), "]");
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sNotFound, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                                    break;
                                case System.Net.HttpStatusCode.NoContent:
                                    string sNoContent = string.Concat("Data : ", sQData.Item1.Replace(Environment.NewLine, " "), " - [", response.Content ?? "", ",", response.Server ?? "", ",", response.ErrorMessage == null ? "" : response.ErrorMessage.ToString(), ",", response.ResponseUri.ToString(), "]");
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sNoContent, SQLTools_Enums.LOG_TYPEINFO.INF);
                                    sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                                    break;
                                default:
                                    Exception ex = new(Languages.Languages.ws_senddata_ko);
                                    string sAnswer = string.Concat("Data : ", sQData.Item1.Replace(Environment.NewLine, " "), " - [", Languages.Languages.ws_senddata_koanswer, response.Content ?? "", ",", response.Server ?? "", ",", response.ErrorMessage == null ? "" : response.ErrorMessage.ToString(), ",", response.ResponseUri.ToString(), "]");
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sAnswer, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                    sAnswers.Add(new Tuple<RestResponse, string>(response, sQData.Item1));
                                    break;
                            }
                        }
                    }
                    else
                    {
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", Languages.Languages.ws_senddata_simuquery, sQData.Item1));
                    }
                }

                CheckSuccessQueries(sAnswers, sWSName, FuzibleQuery);
                dsResponse = await CreateDataSetFromHTTPAnswers(dtSourceQuery, sAnswers, FuzibleQuery);
                SaveWSAnswersInSource(sWSName, FuzibleQuery, dsResponse);
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
        }

        private void SetAuthentification(RestRequest request, string sToken)
        {
            //    if (sToken.Length > 0)
            //    {
            //        switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD))
            //        {
            //            //key-value
            //            case "API_AUTH_PARAM":
            //                request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
            //                break;
            //            case "API_AUTH_HEADER":
            //                request.AddHeader(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
            //                break;
            //            //user + password
            //            case "BASIC_AUTH":
            //                RSClient.Authenticator = new RestSharp.Authenticators.HttpBasicAuthenticator(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE));
            //                //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE));
            //                break;
            //            //token (key/value)
            //            case "BEARER":
            //                request.AddHeader("Authorization", string.Format("Bearer {0}", sToken));
            //                //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
            //                break;
            //            //token (key/value)
            //            case "OAUTH2":
            //                request.AddHeader("Authorization", string.Format("Bearer {0}", sToken));
            //                //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
            //                //request.AddQueryParameter("access_token=", sToken);
            //                break;
            //            case "HTTP":
            //                string[] sQP = sToken.Split(Convert.ToChar("&"));
            //                foreach (string sP in sQP)
            //                { request.AddQueryParameter(sP.Substring(0, sP.IndexOf("=")), sP[(sP.IndexOf("=") + 1)..]); }
            //                break;
            //            case "SALESFORCE_OAUTH":
            //                request.AddHeader("Authorization", "Bearer " + sToken);
            //                break;
            //        }
            //    }

            if (sToken.Length > 0)
            {
                switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD))
                {
                    //key-value
                    case "API_AUTH_PARAM":
                        request.AddQueryParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
                        break;
                    case "API_AUTH_HEADER":
                        request.AddHeader(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
                        break;
                    //user + password
                    case "BASIC_AUTH":
                        request.AddQueryParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE));
                        break;
                    //token (key/value)
                    case "BEARER":
                        request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                        break;
                    //token (key/value)
                    case "OAUTH2":
                        //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                        request.AddHeader("Authorization", "Bearer " + sToken);
                        //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                        //request.AddQueryParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);

                        break;
                    case "OAUTH2DELEGATED":
                        //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                        request.AddHeader("Authorization", "Bearer " + sToken);
                        //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                        //request.AddQueryParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);

                        break;
                    case "HTTP":
                        string[] sQP = sToken.Split(Convert.ToChar("&"));
                        foreach (string sP in sQP)
                        { request.AddQueryParameter(sP[..sP.IndexOf("=")], sP[(sP.IndexOf("=") + 1)..]); }
                        break;
                    case "SALESFORCE_OAUTH":
                        request.AddHeader("Authorization", "Bearer " + sToken);
                        break;
                }
            }
        }

        private async Task<DataSet> GetDataFromREST_UnknownSQL(Query FuzibleQuery, string sToken, SQLTools_Enums.WEBSERVICE_METHOD WSCallMethod)
        {
            DataSet dsData = new();
            DataSet dsJoined = new();
            RestClient RSClient = null;

            if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD) == "BASIC_AUTH")
            {
                RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0], -1, new RestSharp.Authenticators.HttpBasicAuthenticator(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE)));
            }
            else
            {
                RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);
            }

            try
            {
                Method rsMethod = Method.Get;

                string sURL = WSVariables.WebServiceURL;
                RestRequest request = new(sURL, rsMethod);

                string sQ = FuzibleQuery.SQLQuery;
                while (sQ.Contains("  ")) { sQ = sQ.Replace("  ", " "); }

                switch (JobParameters.WebserviceSQLLanguage)
                {
                    case SQLTools_Enums.WEBSERVICE_SQL.SOQL:
                        if (sURL.EndsWith("/")) { sURL = sURL[0..^1]; }
                        if (!sURL.EndsWith("/query")) { sURL += "/query/"; }
                        if (!sURL.EndsWith("?q=")) { sURL += sURL.EndsWith("/") ? "?q=" : "/?q="; }
                        sQ = sQ.Replace(" ", "+");
                        request = new RestRequest(string.Concat(sURL, sQ), rsMethod);
                        break;
                    case SQLTools_Enums.WEBSERVICE_SQL.OQL:

                        if (FuzibleQuery.QueryAnalyzer.Tables.Count > 1)
                        {
                            foreach (var t in FuzibleQuery.QueryAnalyzer.Tables)
                            {
                                DataSet dsTemp = new DataSet(t.Name);
                                Query qTemp = BuildOQLQuery(FuzibleQuery, t);

                                dsTemp = await GetDataFromREST_UnknownSQL(qTemp, sToken, SQLTools_Enums.WEBSERVICE_METHOD.GET);
                                if (dsTemp.Tables.Count > 0)
                                {
                                    if (!dsTemp.Tables[0].TableName.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
                                    { dsTemp.Tables[0].TableName = t.Name; }
                                    dsTemp.Tables[0].Namespace = t.Alias;
                                    dsData.Tables.Add(dsTemp.Tables[0].Copy());
                                    dsTemp.Clear();
                                    dsTemp = null;
                                }
                            }
                            return OQLDataBuilder(dsData, FuzibleQuery);
                        }
                        else
                        {
                            if (sURL.EndsWith("/")) { sURL = sURL[0..^1]; }
                            sQ = QUERY_OQL_JSON_TEMPLATE.Replace("{CLASS}", FuzibleQuery.QueryAnalyzer.Tables[0].Name);

                            List<string> sFields = FuzibleQuery.QueryAnalyzer.Fields.Select(f => f.Name).ToList();
                            sQ = sQ.Replace("{FIELDS}", sFields.Count == 1 && sFields[0].Equals("") ? "*" : string.Join(",", sFields));
                            //réécriture de la requête car le OQL c'est SELECT Object
                            string sNewQ = FuzibleQuery.SQLQuery;
                            Match mcFindTable = Regex.Match(sNewQ, "\\s+" + FuzibleQuery.QueryAnalyzer.Tables[0].Name + "\\b");
                            if (mcFindTable.Success)
                            {
                                sNewQ = sNewQ[mcFindTable.Index..].Trim();
                                sNewQ = string.Concat("SELECT ", sNewQ).Trim();

                            }

                            sQ = sQ.Replace("{QUERY}", sNewQ);
                            sQ = sQ.Replace(" as ", " AS ");
                            sQ = sQ.Replace(" As ", " AS ");
                            sQ = sQ.Replace(" on ", " ON ");
                            sQ = sQ.Replace(" On ", " ON ");
                            sQ = sQ.Replace(" from ", " FROM ");
                            sQ = sQ.Replace(" From ", " FROM ");
                            sQ = sQ.Replace(" select ", " SELECT ");
                            sQ = sQ.Replace(" Select ", " SELECT ");
                        }

                        request.AddQueryParameter("json_data", sQ);
                        break;
                    default:
                        request = new RestRequest(string.Concat(sURL, sQ), rsMethod);
                        break;
                }

                //gestion de l'authentification (job parameters)
                if (sToken.Length > 0)
                {
                    switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD))
                    {
                        //key-value
                        case "API_AUTH_PARAM":
                            request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
                            break;
                        case "API_AUTH_HEADER":
                            request.AddHeader(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
                            break;
                        //user + password
                        case "BASIC_AUTH":
                            //RSClient.Authenticator = ;
                            //request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE));
                            break;
                        //token (key/value)
                        case "BEARER":
                            request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), string.Concat("Bearer ", sToken), ParameterType.HttpHeader);
                            break;
                        //token (key/value)
                        case "OAUTH2":
                            request.AddParameter(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY), sToken);
                            break;
                        case "HTTP":
                            string[] sQP = sToken.Split(Convert.ToChar("&"));
                            foreach (string sP in sQP)
                            { request.AddQueryParameter(sP[..sP.IndexOf("=")], sP[(sP.IndexOf("=") + 1)..]); }
                            break;
                        case "SALESFORCE_OAUTH":
                            request.AddHeader("Authorization", "Bearer " + sToken);
                            break;
                    }
                }

                //gestion de l'header (connexion param)
                foreach (string sH in WSVariables.WebServiceHeader)
                {
                    char sSep = sH.IndexOf(":") > -1 ? Convert.ToChar(":") : Convert.ToChar("=");
                    string[] sHsplit = sH.Split(sSep);
                    if (sHsplit.Length > 1)
                    { request.AddHeader(sHsplit[0], sHsplit[1]); }
                }

                RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);

                string content = response.Content;

                if (response.ErrorException == null)
                {
                    if (response.IsSuccessful)
                    {
                        string sAnswer = string.Concat(Languages.Languages.ws_getdata_koanswer, response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);

                        MediaTypeHeaderValue mType = new(response.ContentType.Split(Convert.ToChar(";"))[0].Trim());
                        string sCharSet = JobParameters.GlobalParameters.WS_DEFAULT_ENCODING;
                        string sOctetStreamType = "";
                        if (response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            mType.CharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..].Trim();
                            sCharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..];
                            sCharSet = sCharSet.Split(Convert.ToChar(";"))[0];
                        }
                        if (response.ContentType.IndexOf("application/octet-stream") > -1 && response.ContentHeaders.Count(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)) > 0)
                        {
                            sOctetStreamType = response.ContentHeaders.FirstOrDefault(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)).Value;
                            if (sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) > -1)
                            {
                                sOctetStreamType = sOctetStreamType.Substring(sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) + 9);
                                sOctetStreamType = sOctetStreamType[1..^1];
                            }
                        }
                        var encoding = Encoding.GetEncoding(sCharSet);
                        string sResult = encoding.GetString(response.RawBytes);

                        dsData = await DeserializeResponse(FuzibleQuery, mType, sOctetStreamType, sResult, response.RawBytes, FuzibleQuery.QueryAnalyzer.Tables[0].Name, 0, "", FuzibleQuery.OutputTable, sToken);

                        if (JobParameters.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.OQL)
                        {
                            //il y a une erreur OQL
                            if (dsData.Tables.Count == 1 && dsData.Tables[0].Columns.Contains("message") && dsData.Tables[0].Columns.Contains("code"))
                            {
                                throw new Exception(string.Concat(dsData.Tables[0].Rows[0]["code"], " : ", dsData.Tables[0].Rows[0]["message"]));
                            }
                        }

                        if (JobParameters.GlobalParameters.WS_SOQL_GETRECORDSONLY)
                        {
                            //recherche de la datatable qui possède les données recherchées
                            int iFields = FuzibleQuery.QueryAnalyzer.Fields.Count;
                            int iFound = 0;
                            string sTable = "";
                            List<string> sListFields = new();

                            switch (JobParameters.WebserviceSQLLanguage)
                            {
                                case SQLTools_Enums.WEBSERVICE_SQL.SOQL:
                                    foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                                    { sListFields.Add(qF.Alias); }
                                    break;
                                default:
                                    foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                                    { sListFields.Add(qF.Name); }
                                    break;
                            }

                            foreach (DataTable dt in dsData.Tables)
                            {
                                iFound = 0;
                                dt.CaseSensitive = false;
                                foreach (string sC in sListFields)
                                { if (dt.Columns.Contains(sC)) { iFound++; sTable = dt.TableName; }; }
                                if (iFound == iFields && dt.Rows.Count > 1) { break; } //dt.Rows.Count > 1 : à cause des tables de référence foireuses
                            }
                            //recherche de la table de données par les noms de champs
                            if (iFields == iFound)
                            {
                                //suppression des tables inutiles
                                List<string> sTToRemove = new();
                                foreach (DataTable dt in dsData.Tables)
                                { if (!dt.TableName.Equals(sTable)) { sTToRemove.Add(dt.TableName); } }
                                foreach (string sT in sTToRemove)
                                { dsData.Tables.Remove(sT); }

                                //suppression des colonnes inutiles                            
                                List<string> sCToRemove = new();
                                foreach (DataColumn dc in dsData.Tables[sTable].Columns)
                                {
                                    if (sListFields.FindIndex(x => x.Equals(dc.ColumnName, StringComparison.OrdinalIgnoreCase)) == -1)
                                    { sCToRemove.Add(dc.ColumnName); }
                                }
                                foreach (string sC in sCToRemove)
                                {
                                    if (dsData.Tables[sTable].PrimaryKey.Contains(dsData.Tables[sTable].Columns[sC]))
                                    { }
                                    else { dsData.Tables[sTable].Columns.Remove(sC); }
                                }
                            }
                            else //recherche de la table portant le nom de "records"
                            {
                                List<string> sListDTToRemove = new();
                                foreach (DataTable dt in dsData.Tables)
                                {
                                    if (!dt.TableName.Contains("records", StringComparison.OrdinalIgnoreCase))
                                    { sListDTToRemove.Add(dt.TableName); }
                                }
                                if (sListDTToRemove.Count != dsData.Tables.Count) //faut quand même laisser une table si aucune ne se nomme "records"
                                {
                                    foreach (string sT in sListDTToRemove)
                                    { dsData.Tables.Remove(sT); }
                                }
                            }
                        }
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.ws_getdata_ko);
                        string sAnswer = string.Concat(Languages.Languages.ws_getdata_koanswer, response.Content, ",", response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sAnswer, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Exception ex = response.ErrorException;

                        switch (JobParameters.NoSourceDataNoError)
                        {
                            case 0:
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.INF);
                                break;
                            case 1:
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                break;
                            case 2:
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                                break;
                        }
                    }
                    else
                    {
                        Exception ex = response.ErrorException;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return dsData;
        }

        private Query BuildOQLQuery(Query fuzibleQuery, Query.QTable t)
        {
            string sFieldsToQuery = OQLSearchFields(fuzibleQuery.SQLQuery, t);

            string sWhere = "";
            foreach (var c in fuzibleQuery.QueryAnalyzer.Where)
            {
                if (c.TableField.Equals(t.Name) || c.RawWhereField.StartsWith(t.Alias + "."))
                {
                    sWhere = string.Concat(sWhere, c.Where, " AND ");
                }
            }
            if (sWhere.Length > 0) { sWhere = " WHERE " + sWhere[0..^5]; }

            Query qTemp = new Query(JobParameters, "SELECT " + sFieldsToQuery + " FROM " + t.Name + " AS " + t.Alias + sWhere);

            return qTemp;
        }

        private string OQLSearchFields(string sQuery, Query.QTable t)
        {
            List<string> sFields = new List<string>();
            MatchCollection mcF1 = Regex.Matches(sQuery, @"[\s|,\s*]" + t.Name + @"\.([A-z0-9-_]+)[\s|,\s*]");
            MatchCollection mcF2 = Regex.Matches(sQuery, @"[\s|,\s*]" + t.Alias + @"\.([A-z0-9-_]+)[\s|,\s*]");

            string sValue = "";
            foreach (Match m in mcF1)
            {
                sValue = m.Value.Trim();
                if (sValue.StartsWith(",")) { sValue = sValue[1..].Trim(); }
                if (sValue.EndsWith(",")) { sValue = sValue[0..^1].Trim(); }
                sValue = sValue.Replace(t.Name + ".", t.Alias + ".");
                if (!sFields.Contains(sValue)) { sFields.Add(sValue); }
            }
            foreach (Match m in mcF2)
            {
                sValue = m.Value.Trim();
                if (sValue.StartsWith(",")) { sValue = sValue[1..].Trim(); }
                if (sValue.EndsWith(",")) { sValue = sValue[0..^1].Trim(); }
                if (!sFields.Contains(sValue)) { sFields.Add(sValue); }
            }

            return string.Join(",", sFields);
        }

        private DataSet OQLDataBuilder(DataSet dsData, Query fuzibleQuery)
        {
            for (int iT = 0; iT < fuzibleQuery.QueryAnalyzer.Tables.Count; iT++)
            {
                if (iT == 0)
                {
                    foreach (DataColumn dc in dsData.Tables[0].Columns)
                    {
                        dc.ColumnName = dc.Table.Namespace + "." + dc.ColumnName;
                        dc.Namespace = dc.Table.Namespace;
                    }
                }
                else
                {
                    List<string[]> joins = new List<string[]>();
                    foreach (var join in fuzibleQuery.QueryAnalyzer.Tables[iT].LinkFields)
                    {
                        joins.Add(new string[] { dsData.Tables[0].Columns.Contains(join[5]) ? join[5] : join[6], dsData.Tables[0].Columns.Contains(join[5]) ? join[6] : join[5] });
                    }

                    foreach (DataColumn dc in dsData.Tables[iT].Columns)
                    {
                        dsData.Tables[0].Columns.Add(dc.Table.Namespace + "." + dc.ColumnName, dc.DataType);
                        dc.ColumnName = dc.Table.Namespace + "." + dc.ColumnName;
                        dsData.Tables[0].Columns[dsData.Tables[0].Columns.Count - 1].Namespace = dc.Table.Namespace;
                    }

                    foreach (DataRow drMaster in dsData.Tables[0].Rows)
                    {
                        string sDataMaster = "";

                        foreach (string[] s in joins)
                        {
                            sDataMaster = string.Concat(sDataMaster, drMaster[s[0]].ToString(), ",");
                        }

                        foreach (DataRow drSlave in dsData.Tables[iT].Rows)
                        {
                            string sDataSlave = "";
                            foreach (string[] s in joins)
                            {
                                sDataSlave = string.Concat(sDataSlave, drSlave[s[1]].ToString(), ",");
                            }

                            if (sDataMaster.Equals(sDataSlave))
                            {
                                foreach (DataColumn dc in drSlave.Table.Columns)
                                {
                                    drMaster[dc.ColumnName] = drSlave[dc.ColumnName];
                                }
                            }
                        }
                    }
                }
            }

            int iCount = dsData.Tables.Count;
            for (int i = 1; i < iCount; i++)
            {
                dsData.Tables.RemoveAt(1);
            }

            List<string> fieldsToPreserve = new List<string>();
            foreach (var field in fuzibleQuery.QueryAnalyzer.Fields)
            {
                fieldsToPreserve.Add(field.TableAlias + "." + field.Name);
            }

            List<string> sColsToRemove = new List<string>();
            foreach (DataColumn dc in dsData.Tables[0].Columns)
            {
                if (!fieldsToPreserve.Contains(dc.ColumnName))
                {
                    sColsToRemove.Add(dc.ColumnName);
                }
            }
            foreach (string s in sColsToRemove)
            {
                dsData.Tables[0].Columns.Remove(s);
            }

            //foreach (DataColumn dc in dsData.Tables[0].Columns)
            //{
            //    string sNewName = dc.ColumnName[(dc.ColumnName.IndexOf(".") + 1)..];
            //    if (!dsData.Tables[0].Columns.Contains(sNewName))
            //    {
            //        //dc.ColumnName = sNewName;
            //    }
            //}

            return dsData;
        }

        public static async Task<List<string>> GetFileListFromAPIPattern(Job INIP, LogTools MyLog, CONNString Connection, string sFilePattern, Query FuzibleQuery)
        {
            List<string> sListFiles = new();

            WSTools WS = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);

            string sToken = await WS.GetToken(FuzibleQuery);
            RestClient RSClient = WS.GetRSConnexion(Connection.SConnString(INIP.DynParams).Split(Convert.ToChar("?"))[0]);

            Method rsMethod = Method.Get;

            string sURL = "";


            if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
            {
                sURL = Connection.SConnString(INIP.DynParams);
                string sSubURL = sFilePattern.Split(Convert.ToChar("["))[0];
                if (sSubURL.StartsWith("/") && sURL.EndsWith("/")) { sSubURL = sSubURL[1..]; }

                if (sURL.Contains("/items/", StringComparison.OrdinalIgnoreCase))
                {
                    //$filter=startswith(name, 'test')
                    if (sURL.EndsWith("/"))
                    { sSubURL = "children/?$filter=startswith(name, '" + sFilePattern + "')"; }
                    else { sSubURL = "/children/?$filter=startswith(name, '" + sFilePattern + "')"; }
                }
                else
                {
                    //sSubURL = "/?$filter=name eq '" + sQuery.QueryAnalyzer.Tables[0].Name + "'";
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.ws_graphapi_onedrive_urlko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                sURL = string.Concat(sURL, sSubURL);
            }

            //sURL = sURL.Replace("//", "/");

            if (sURL.Length > 0)
            {
                RestRequest request = WS.CreateSendRequest(RSClient, sURL, rsMethod, sToken, new Query.QTable(sFilePattern, sFilePattern, "", new List<string[]>(), Connection), FuzibleQuery);

                RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);
                string content = response.Content;

                if (response.ErrorException == null)
                {
                    if (response.IsSuccessful)
                    {
                        JsonParser JsParser = new(content, "Files")
                        {
                            AvoidSpecialCharsInColumnNames = false,
                            RemovePrimaryKey = true
                        };
                        DataSet dsData = await JsParser.JsonToDataSetAsync(Monitoring.TaskCancellationToken);

                        switch (INIP.WebserviceTypeData)
                        {
                            case SQLTools_Enums.API_OPTIONS.GRAPH_DOWNLOAD_FILE_DRIVE:
                                if (dsData.Tables.Count > 0)
                                {
                                    int iMaxTables = dsData.Tables.Count;
                                    for (int iT = 0; iT < iMaxTables; iT++)
                                    {
                                        if (dsData.Tables[iT].Columns.Contains("@microsoft.graph.downloadUrl"))
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.ws_deserialize_foundgraphdownload + dsData.Tables[iT].TableName + " (" + iT.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.DET);

                                            //on récupère la liste des liens générés pour télécharger les fichiers
                                            int iRowsMax = dsData.Tables[iT].Rows.Count;
                                            for (int iRow = 0; iRow < iRowsMax; iRow++)
                                            {
                                                DataRow dr = dsData.Tables[iT].Rows[iRow];
                                                string sFilename = dr["name"].ToString();
                                                sListFiles.Add(sFilename);
                                            }
                                        }
                                    }
                                }
                                break;
                            case SQLTools_Enums.API_OPTIONS.LOCKED_BY_TEMPLATE:
                                switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                                {
                                    case "MICROSOFT_ONEDRIVE":
                                        if (dsData.Tables.Count > 0)
                                        {
                                            int iMaxTables = dsData.Tables.Count;
                                            for (int iT = 0; iT < iMaxTables; iT++)
                                            {
                                                if (dsData.Tables[iT].Columns.Contains("@microsoft.graph.downloadUrl"))
                                                {
                                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.ws_deserialize_foundgraphdownload + dsData.Tables[iT].TableName + " (" + iT.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.DET);

                                                    //on récupère la liste des liens générés pour télécharger les fichiers
                                                    int iRowsMax = dsData.Tables[iT].Rows.Count;
                                                    for (int iRow = 0; iRow < iRowsMax; iRow++)
                                                    {
                                                        DataRow dr = dsData.Tables[iT].Rows[iRow];
                                                        string sFilename = dr["name"].ToString();
                                                        sListFiles.Add(sFilename);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;
                        }
                    }
                }
            }

            return sListFiles;
        }

        private async Task<DataSet> GetDataFromREST(Query FuzibleQuery, string sToken, SQLTools_Enums.WEBSERVICE_METHOD WSCallMethod)
        {
            DataSet dsData = new();
            DataSet dsJoined = new();
            RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);

            try
            {
                int iTable = -1;
                foreach (Query.QTable qT in FuzibleQuery.QueryAnalyzer.Tables)
                {
                    iTable++;

                    //recherche de subquery
                    if (Regex.IsMatch(qT.Name, "\\(\\s*SELECT\\s+", RegexOptions.IgnoreCase) && qT.Name.EndsWith(")"))
                    {
                        string sSq = qT.Name[1..];
                        sSq = sSq[0..^1];
                        Query SubQuery = new(JobParameters, string.Concat("SUBQUERY_", iTable.ToString(), ":", sSq));
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_getdata_subquery + SubQuery.RawQuery, SQLTools_Enums.LOG_TYPEINFO.INF);

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
                        Method rsMethod = Method.Post;
                        switch (WSCallMethod)
                        {
                            case SQLTools_Enums.WEBSERVICE_METHOD.DELETE:
                                rsMethod = Method.Delete;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.GET:
                                rsMethod = Method.Get;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.PATCH:
                                rsMethod = Method.Patch;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.POST:
                                rsMethod = Method.Post;
                                break;
                            case SQLTools_Enums.WEBSERVICE_METHOD.PUT:
                                rsMethod = Method.Put;
                                break;
                        }

                        string sURL = WSVariables.WebServiceURL;

                        string sSubURL = qT.Name.Split(Convert.ToChar("["))[0];
                        if (sSubURL.StartsWith("/") && sURL.EndsWith("/")) { sSubURL = sSubURL[1..]; }

                        if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_ONEDRIVE")
                        {
                            if (sURL.Contains("/items/", StringComparison.OrdinalIgnoreCase))
                            {
                                //$filter=startswith(name, 'test')
                                if (sURL.EndsWith("/"))
                                { sSubURL = "children/?$filter=startswith(name, '" + FuzibleQuery.QueryAnalyzer.Tables[0].Name + "')"; }
                                else { sSubURL = "/children/?$filter=startswith(name, '" + FuzibleQuery.QueryAnalyzer.Tables[0].Name + "')"; }
                            }
                            else
                            {
                                //sSubURL = "/?$filter=name eq '" + sQuery.QueryAnalyzer.Tables[0].Name + "'";
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_graphapi_onedrive_urlko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }

                        sURL = string.Concat(sURL, sSubURL);
                        //sURL = sURL.Replace("//", "/");

                        RestRequest request = CreateSendRequest(RSClient, sURL, rsMethod, sToken, qT, FuzibleQuery);

                        bool bIterate = true; //gestion de l'itération pour compléter les résultats liés à la pagination de l'API graph de Microsoft
                        int iIteration = 0;
                        string sPastLink = "";

                        while (bIterate)
                        {
                            iIteration++;

                            RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);
                            string content = response.Content;

                            if (response.ErrorException == null)
                            {
                                if (response.IsSuccessful)
                                {
                                    string sAnswer = string.Concat(Languages.Languages.ws_getdata_koanswer, response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);

                                    MediaTypeHeaderValue mType = new(response.ContentType.Split(Convert.ToChar(";"))[0].Trim());
                                    string sCharSet = JobParameters.GlobalParameters.WS_DEFAULT_ENCODING;
                                    string sOctetStreamType = "";
                                    if (response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) > -1)
                                    {
                                        mType.CharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..].Trim();
                                        sCharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..];
                                        sCharSet = sCharSet.Split(Convert.ToChar(";"))[0];
                                    }
                                    if (response.ContentType.IndexOf("application/octet-stream") > -1 && response.ContentHeaders.Count(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)) > 0)
                                    {
                                        sOctetStreamType = response.ContentHeaders.FirstOrDefault(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)).Value;
                                        if (sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) > -1)
                                        {
                                            sOctetStreamType = sOctetStreamType.Substring(sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) + 9);
                                            sOctetStreamType = sOctetStreamType[1..^1];
                                        }
                                    }
                                    var encoding = Encoding.GetEncoding(sCharSet);
                                    string sResult = encoding.GetString(response.RawBytes);

                                    string sAlias = "";

                                    if (!FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias.Equals(FuzibleQuery.QueryAnalyzer.Tables[iTable].Name))
                                    { sAlias = FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias; }

                                    if (iTable == 0)
                                    {
                                        if (bIterate && iIteration > 1) //pagination graph API
                                        {
                                            DataSet dsNextSet = await DeserializeResponse(FuzibleQuery, mType, sOctetStreamType, sResult, response.RawBytes, sAlias.Length == 0 ? sSubURL : sAlias, 0, "", FuzibleQuery.OutputTable, sToken);

                                            if (dsNextSet != null && dsNextSet.Tables.Count > 0)
                                            {
                                                //alignement des noms de tables
                                                foreach (DataTable dt in dsNextSet.Tables) //en cas de désérialisation, on peut avoir plusieurs tables, on veut que le nom commence toujours par le nom défini dans le job !
                                                {
                                                    if (!dt.TableName.Equals(FuzibleQuery.OutputTable, StringComparison.InvariantCultureIgnoreCase))
                                                    { dt.TableName = string.Concat(FuzibleQuery.OutputTable, "_", dt.TableName); }
                                                }

                                                dsData = MergeGraphAPIDataIterations(dsNextSet, dsData, iIteration);
                                            }
                                        }
                                        else
                                        {
                                            dsData = await DeserializeResponse(FuzibleQuery, mType, sOctetStreamType, sResult, response.RawBytes, sAlias.Length == 0 ? sSubURL : sAlias, 0, "", FuzibleQuery.OutputTable, sToken);

                                            foreach (DataTable dt in dsData.Tables) //en cas de désérialisation, on peut avoir plusieurs tables, on veut que le nom commence toujours par le nom défini dans le job !
                                            {
                                                if (!dt.TableName.Equals(FuzibleQuery.OutputTable, StringComparison.InvariantCultureIgnoreCase))
                                                { dt.TableName = string.Concat(FuzibleQuery.OutputTable, "_", dt.TableName); }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        dsJoined = await DeserializeResponse(FuzibleQuery, mType, sOctetStreamType, sResult, response.RawBytes, sAlias.Length == 0 ? sSubURL : sAlias, 1, "", FuzibleQuery.OutputTable, sToken);
                                        foreach (DataTable dt in dsData.Tables) //en cas de désérialisation, on peut avoir plusieurs tables, on veut que le nom commence toujours par le nom défini dans le job !
                                        {
                                            if (!dt.TableName.Equals(FuzibleQuery.OutputTable, StringComparison.InvariantCultureIgnoreCase))
                                            { dt.TableName = string.Concat(FuzibleQuery.OutputTable, "_", dt.TableName); }
                                        }
                                    }
                                }
                                else
                                {
                                    Exception ex = new(Languages.Languages.ws_getdata_ko);
                                    string sAnswer = string.Concat(Languages.Languages.ws_getdata_koanswer, response.Content, ",", response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sAnswer, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    Exception ex = response.ErrorException;

                                    switch (JobParameters.NoSourceDataNoError)
                                    {
                                        case 0:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.INF);
                                            break;
                                        case 1:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            break;
                                        case 2:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                                            FuzibleQuery.QueryErrors += 1;
                                            break;
                                    }
                                }
                                else
                                {
                                    Exception ex = response.ErrorException;
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                            }

                            if (iTable == 0) { if (dsData.Tables.Count > 0) { dsData.Tables[0].Namespace = qT.Alias; } }
                            if (iTable > 0) { if (dsJoined.Tables.Count > 0) { dsJoined.Tables[0].Namespace = qT.Alias; } }

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

                            //*****************************************************************************************
                            bIterate = false;
                            string sNewLink = CheckGraphNextLink(dsData, FuzibleQuery, Connection); //contrôle du lien pour la prochaine requête
                            if (sNewLink.Length > 0 && !sNewLink.Equals(sPastLink)) //on peut itérer : il y'a une pagination proposée par l'API
                            {
                                sPastLink = sNewLink;
                                if (dsData.Tables.Count > 1 && (dsData.Tables[1].Rows.Count < FuzibleQuery.QueryAnalyzer.LimitedResults || FuzibleQuery.QueryAnalyzer.LimitedResults == 0)) //ma deuxème table présente les résultats, on les compte
                                {
                                    bIterate = true;
                                    request = CreateSendRequest(RSClient, sNewLink, rsMethod, sToken, qT, FuzibleQuery);
                                }
                            }
                            //*****************************************************************************************
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }

            return dsData;
        }

        private DataSet MergeGraphAPIDataIterations(DataSet dsNextSet, DataSet dsData, int iIteration)
        {
            foreach (DataTable dt in dsData.Tables)
            {
                if (!dt.Columns.Contains("idGraphAPIiteration"))
                {
                    dt.Columns.Add("idGraphAPIiteration", Type.GetType("System.Int32"));
                    dt.Columns["idGraphAPIiteration"].SetOrdinal(0);
                    foreach (DataRow dr in dt.Rows)
                    {
                        dr["idGraphAPIiteration"] = 1;
                    }
                }
            }
            foreach (DataTable dt in dsNextSet.Tables)
            {
                if (!dt.Columns.Contains("idGraphAPIiteration"))
                {
                    dt.Columns.Add("idGraphAPIiteration", Type.GetType("System.Int32"));
                    dt.Columns["idGraphAPIiteration"].SetOrdinal(0);
                    foreach (DataRow dr in dt.Rows)
                    {
                        dr["idGraphAPIiteration"] = iIteration;
                    }
                }
            }

            if (dsNextSet.Tables.Count == dsData.Tables.Count)
            {
                for (int iT = 0; iT < dsData.Tables.Count; iT++)
                {
                    dsData.Tables[iT].Merge(dsNextSet.Tables[iT]);
                }
            }
            if (dsData.Tables.Count == 0) //premier passage
            {
                dsData = dsNextSet.Copy();
            }

            dsNextSet.Clear();
            dsNextSet = null;

            //ajouter un ID iteration


            return dsData;
        }

        private RestRequest CreateSendRequest(RestClient RSClient, string sURL, Method rsMethod, string sToken, Query.QTable qT, Query FuzibleQuery)
        {
            RestRequest request = new(sURL, rsMethod);

            //si l'adresse du WS contient déjà des paramètres HTTP on les redéfinit proprement
            if (WSVariables.WebServiceURL.IndexOf("?") > 0)
            {
                string sSubURL2 = WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[1];
                string[] sQP = sSubURL2.Split(Convert.ToChar("&"));
                foreach (string sP in sQP)
                { request.AddQueryParameter(sP[..sP.IndexOf("=")], sP[(sP.IndexOf("=") + 1)..]); }
            }

            //gestion de l'authentification (job parameters)
            SetAuthentification(request, sToken);

            //gestion de l'header (connexion param)
            foreach (string sH in WSVariables.WebServiceHeader)
            {
                if (!sH.Contains(sToken)) //si j'ai utilisé le bearertoken dans oauth je ne veux pas l'ajouter une seconde fois
                {
                    char sSep = sH.IndexOf(":") > -1 ? Convert.ToChar(":") : Convert.ToChar("=");
                    string[] sHsplit = sH.Split(sSep);
                    if (sHsplit.Length > 1)
                    { request.AddHeader(sHsplit[0], sHsplit[1]); }
                }
            }

            //gestion du body (table name)
            if (qT.Name.IndexOf("[") > 0 && qT.Name.EndsWith("]")) //on cherche le body du WS
            {
                string sBody = qT.Name[(qT.Name.IndexOf("[") + 1)..];
                sBody = sBody[0..^1];

                switch (JobParameters.WebServiceRequestBodyType)
                {
                    case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.RAW_JSON:
                        //test validité
                        JsonParser jsP = new(sBody, "json");
                        jsP.OnJsonEvent += Event_JsonParser;
                        DataSet dsTest = jsP.JsonToDataSet();
                        if (dsTest != null && dsTest.Tables.Count > 0 && dsTest.Tables[0].Rows.Count > 0)
                        { request.AddJsonBody(sBody); }
                        else
                        {
                            Exception ex = new(Languages.Languages.ws_getdata_jsonerror);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sBody, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                        break;

                    case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.RAW_XML:
                        //test validité
                        try
                        {
                            DataSet dsTest2 = new();
                            dsTest2.ReadXml(sBody);
                            request.AddXmlBody(sBody);
                        }
                        catch (Exception ex2)
                        {
                            Exception ex3 = new(Languages.Languages.ws_getdata_xmlerror01 + ex2.Message + Languages.Languages.ws_getdata_xmlerror02);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex3, sBody, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                        break;

                    case SQLTools_Enums.WEBSERVICE_REQUEST_BODY_TYPE.FORM_DATA:
                        //découpage foreach du contenu param=value;param=value
                        request.AlwaysMultipartFormData = true;
                        string[] sParams = sBody.Split(Convert.ToChar(";"));
                        foreach (string sP in sParams)
                        {
                            if (sP.IndexOf("=") > 0)
                            { request.AddParameter(sP.Split(Convert.ToChar("="))[0], sP.Split(Convert.ToChar("="))[1]); }
                            else if (sP.IndexOf(":") > 0)
                            { request.AddParameter(sP.Split(Convert.ToChar(":"))[0], sP.Split(Convert.ToChar(":"))[1]); }
                            else
                            {
                                Exception ex = new(Languages.Languages.ws_getdata_cantparsebody);
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sBody, FuzibleQuery.RetryErrorOrWarning);
                                FuzibleQuery.QueryErrors += 1;
                            }
                        }
                        break;
                }
            }
            return request;
        }

        private static string CheckGraphNextLink(DataSet dsData, Query Q, CONNString CS)
        {
            string sNextLink = "";
            if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE) == "MICROSOFT_GRAPH")
            {
                if (dsData.Tables.Count > 0)
                {
                    foreach (DataTable dt in dsData.Tables)
                    {
                        if (Q.QueryAnalyzer.LimitedResults > 0 && dt.Rows.Count > Q.QueryAnalyzer.LimitedResults) //éviter de trop charger si l'utilisateur a fait un TOP x
                        {
                            return "";
                        }
                        else
                        {
                            foreach (DataColumn dc in dt.Columns)
                            {
                                if (dc.ColumnName.Contains("nextLink", StringComparison.OrdinalIgnoreCase) && dt.Rows.Count > 0)
                                {
                                    return dt.Rows[dt.Rows.Count - 1][dc.ColumnName].ToString();
                                }
                            }
                        }
                    }
                }
            }
            return sNextLink;
        }

        private void Event_JsonParser(object sender, string sInfo)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, sInfo, SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        private RestClient GetRSConnexion(string sUrl, int iMaxTimeout = -1, HttpBasicAuthenticator auth = null)
        {
            RestClientOptions rsOptions = new(sUrl);

            if (iMaxTimeout > 0) { rsOptions.MaxTimeout = iMaxTimeout; }

            if (auth != null) { rsOptions.Authenticator = auth; }

            switch (WSVariables.WebServiceProxyType)
            {
                case ProxyTypes.None:
                    rsOptions.Proxy = null;
                    break;
                case ProxyTypes.Http:
                    rsOptions.Proxy = new System.Net.WebProxy(WSVariables.WebServiceProxyURL, WSVariables.WebServiceProxyPort);
                    break;
                case ProxyTypes.Socks4:
                    throw new Exception("SOCKS4 Unsupported !");
                case ProxyTypes.Socks5:
                    if (WSVariables.WebServiceProxyUsername.Length > 0)
                    { rsOptions.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(WSVariables.WebServiceProxyURL, WSVariables.WebServiceProxyPort, WSVariables.WebServiceProxyUsername, WSVariables.WebServiceProxyPassword) }); }
                    else { rsOptions.Proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo(WSVariables.WebServiceProxyURL, WSVariables.WebServiceProxyPort) }); }
                    break;
            }

            RestClient RSClient = new(rsOptions);

            return RSClient;
        }

        private async Task<DataSet> GetDataFromNUXEO(Query FuzibleQuery)
        {
            DataSet dsData = new();
            RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0], 10000000, new RestSharp.Authenticators.HttpBasicAuthenticator(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_NUXEO_USER), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_NUXEO_PASSWORD)));

            Method rsMethod = Method.Post;
            switch (JobParameters.WebServiceCallMethod)
            {
                case SQLTools_Enums.WEBSERVICE_METHOD.DELETE:
                    rsMethod = Method.Delete;
                    break;
                case SQLTools_Enums.WEBSERVICE_METHOD.GET:
                    rsMethod = Method.Get;
                    break;
                case SQLTools_Enums.WEBSERVICE_METHOD.PATCH:
                    rsMethod = Method.Patch;
                    break;
                case SQLTools_Enums.WEBSERVICE_METHOD.POST:
                    rsMethod = Method.Post;
                    break;
                case SQLTools_Enums.WEBSERVICE_METHOD.PUT:
                    rsMethod = Method.Put;
                    break;
            }

            string sURL = WSVariables.WebServiceURL;
            string sSubURL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebserviceNuxeo_EndpointSource, JobParameters.DynParams);

            sURL = string.Concat(sURL, "api/v1/automation/", sSubURL);
            RestRequest request = new(sURL, rsMethod);

            //gestion de l'header (connexion param)
            request.AddHeader("Content-Type", "application/json+nxrequest; charset=UTF-8");
            request.AddHeader("X-NXVoidOperation", "*");
            request.AddHeader("X-NXDocumentProperties", "*");

            string sBody = string.Concat("{\"params\":{\"query\":\"", FuzibleQuery.SQLQuery, "\"}}");
            request.AddJsonBody(sBody);

            RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);

            if (response.ErrorException == null)
            {
                if (response.IsSuccessful)
                {
                    //string sAnswer = string.Concat("Webservice Answer (Error Message, Status Code, URI) : ", response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);

                    MediaTypeHeaderValue mType = new(response.ContentType.Split(Convert.ToChar(";"))[0].Trim());
                    string sCharSet = JobParameters.GlobalParameters.WS_NUXEO_DEFAULT_ENCODING;
                    string sOctetStreamType = "";
                    if (response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        mType.CharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..].Trim();
                        sCharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..];
                        sCharSet = sCharSet.Split(Convert.ToChar(";"))[0];
                    }
                    if (response.ContentType.IndexOf("application/octet-stream") > -1 && response.ContentHeaders.Count(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)) > 0)
                    {
                        sOctetStreamType = response.ContentHeaders.FirstOrDefault(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)).Value;
                        if (sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            sOctetStreamType = sOctetStreamType.Substring(sOctetStreamType.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) + 9);
                            sOctetStreamType = sOctetStreamType[1..^1];
                        }
                    }

                    var encoding = Encoding.GetEncoding(sCharSet);
                    string sResult = encoding.GetString(response.RawBytes);
                    string sPK = FuzibleQuery.QueryAnalyzer.Tables[0].Name switch
                    {
                        "Document" => "uid",
                        "FolderSalarie" => "uid",
                        _ => "uid",
                    };

                    string sAlias = "";
                    if (!FuzibleQuery.QueryAnalyzer.Tables[0].Alias.Equals(FuzibleQuery.QueryAnalyzer.Tables[0].Name))
                    { sAlias = FuzibleQuery.QueryAnalyzer.Tables[0].Alias; }

                    dsData = await DeserializeResponse(FuzibleQuery, mType, sOctetStreamType, sResult, response.RawBytes, sAlias.Length == 0 ? sSubURL : sAlias, 0, sPK, FuzibleQuery.OutputTable, "");

                    foreach (DataTable dt in dsData.Tables) //en cas de désérialisation, on peut avoir plusieurs tables, on veut que le nom commence toujours par le nom défini dans le job !
                    {
                        if (!dt.TableName.Equals(FuzibleQuery.OutputTable, StringComparison.InvariantCultureIgnoreCase))
                        { dt.TableName = string.Concat(FuzibleQuery.OutputTable, "_", dt.TableName); }
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.ws_getdata_ko);
                    string sAnswer = string.Concat(Languages.Languages.ws_getdata_koanswer, (response.Content ?? ""), ",", response.ErrorMessage, ",", response.StatusCode.ToString(), ",", response.ResponseUri);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sAnswer, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Exception ex = response.ErrorException;

                    switch (JobParameters.NoSourceDataNoError)
                    {
                        case 0:
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, response.Content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case 1:
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, response.Content ?? Languages.Languages.ws_getdata_ko, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        case 2:
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, response.Content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                            break;
                    }
                }
                else
                {
                    Exception ex = response.ErrorException;
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, response.Content ?? Languages.Languages.ws_getdata_ko, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }

            return dsData;
        }

        private async Task<string> GetToken(Query FuzibleQuery)
        {
            string sToken = "";
            RestResponse response = null;

            switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD))
            {
                case "OAUTH2":
                case "OAUTH2DELEGATED":
                    if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Length > 0)
                    {
                        string clientID = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERKEY);
                        string clientSecret = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERSECRET);
                        string sUrl = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL);
                        string scope = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY);
                        string sparams = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS);

                        //if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_HEADERS)).
                        //{

                        //}
                        string contentWS = "";
                        try
                        {
                            RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);

                            RestRequest rsQ = new(sUrl, sparams.Length > 0 ? Method.Post : Method.Get);

                            string[] sListParams = sparams.Split("&", StringSplitOptions.RemoveEmptyEntries);
                            foreach (string sP in sListParams)
                            { rsQ.AddParameter(sP[..sP.IndexOf("=")], sP[(sP.IndexOf("=") + 1)..]); }
                            if (clientID.Length > 0) { rsQ.AddParameter("client_id", clientID); }
                            if (clientSecret.Length > 0) { rsQ.AddParameter("client_secret", clientSecret); }
                            if (scope.Length > 0) { rsQ.AddParameter("Scope", scope); }

                            if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_METHOD).Equals("OAUTH2DELEGATED"))
                            {
                                foreach (string sH in WSVariables.WebServiceHeader)
                                {
                                    if (sH.StartsWith("userName", StringComparison.OrdinalIgnoreCase) && sH.IndexOf("=") > 0)
                                    {
                                        rsQ.AddParameter(sH[0..sH.IndexOf("=")], sH[(sH.IndexOf("=") + 1)..]);
                                    }
                                    if (sH.StartsWith("password", StringComparison.OrdinalIgnoreCase) && sH.IndexOf("=") > 0)
                                    {
                                        rsQ.AddParameter(sH[0..sH.IndexOf("=")], sH[(sH.IndexOf("=") + 1)..]);
                                    }
                                }
                            }

                            response = await RSClient.ExecuteAsync(rsQ, Monitoring.TaskCancellationToken);

                            contentWS = response.Content;
                            JsonParser jsp = new(contentWS, "Token");
                            jsp.OnJsonEvent += Event_JsonParser;
                            DataSet dsToken = await jsp.JsonToDataSetAsync(Monitoring.TaskCancellationToken);
                            if (dsToken != null && dsToken.Tables.Count > 0 && dsToken.Tables[0].Rows.Count > 0)
                            {
                                DataRow dr = dsToken.Tables[0].Rows[0];
                                foreach (DataColumn dc in dr.Table.Columns)
                                {
                                    if (dc.ColumnName.IndexOf("token", StringComparison.OrdinalIgnoreCase) > -1) //TODO bien foireux
                                    {
                                        string sTest = dr[dc].ToString();
                                        if (Regex.IsMatch(sTest, "[A-z\\d\\.\\-_|]{32,}"))
                                        { sToken = sTest; break; }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            string sAddData = response != null && response.Content != null ? response.Content : "";
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.ws_gettoken_ko, sUrl, ") : ", sToken, " - ", "Response : ", sAddData), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }

                        if (sToken.Length > 0)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_gettoken_ok, sUrl, ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("Token : ", sToken), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, new Exception(string.Concat(Languages.Languages.ws_gettoken_ko, sUrl)), contentWS, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    else //recherche du token dans l'header
                    {
                        string sparams = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_HEADERS);
                        string[] sListParams = sparams.Split(";", StringSplitOptions.RemoveEmptyEntries);
                        foreach (string sP in sListParams)
                        {
                            if (sP.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase) > 0)
                            {
                                sToken = sP[(sP.IndexOf("Bearer ") + 7)..].Trim();
                                break;
                            }
                        }
                    }
                    break;

                case "SALESFORCE_OAUTH":
                    string username = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_KEY);
                    string password = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE);
                    string usertoken = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_USERTOKEN);
                    string consumerKey = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERKEY);
                    string consumerSecret = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_SF_CONSUMERSECRET);

                    string url = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL);
                    string content = "";
                    //Version API :
                    string sURL = Connection.SConnString(JobParameters.DynParams);
                    var authClient = new Salesforce.Common.AuthenticationClient();

                    Match mcVersionURL = Regex.Match(sURL, "\\/v\\d+\\.\\d+\\/*");
                    if (mcVersionURL.Success)
                    {
                        if (mcVersionURL.Value.EndsWith("/")) { authClient.ApiVersion = mcVersionURL.Value[1..^1]; }
                        else { authClient.ApiVersion = mcVersionURL.Value[1..]; }
                    }
                    else
                    {
                        RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);
                        if (sURL.Contains("salesforce.com", StringComparison.OrdinalIgnoreCase))
                        { sURL = sURL[..(sURL.IndexOf("salesforce.com") + 14)]; }
                        RestRequest rsQ = new(sURL + (sURL.EndsWith("/") ? "services/data" : "/services/data"), Method.Get);
                        response = await RSClient.ExecuteAsync(rsQ, Monitoring.TaskCancellationToken);
                        content = response.Content;
                        JsonParser jsp = new(content, "Version");
                        jsp.OnJsonEvent += Event_JsonParser;
                        DataSet dsVersion = await jsp.JsonToDataSetAsync(Monitoring.TaskCancellationToken);

                        if (dsVersion != null && dsVersion.Tables.Count > 0 && dsVersion.Tables[0].Rows.Count > 0)
                        {
                            dsVersion.Tables[0].CaseSensitive = false;
                            if (dsVersion.Tables[0].Columns.Contains("version"))
                            { authClient.ApiVersion = dsVersion.Tables[0].Rows[^1]["version"].ToString(); }
                        }
                    }

                    try
                    {
                        //await authClient.UsernamePasswordAsync(sfdcConsumerKey, sfdcConsumerSecret, username, password + token, ".net-api-client", url);
                        await authClient.UsernamePasswordAsync(consumerKey, consumerSecret, username, password + usertoken, url);
                    }
                    catch (Exception ex)
                    {
                        string sAddData = response != null && response.Content != null ? response.Content : "";
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.ws_gettoken_ko, url, ") : ", sToken, " - ", "Response : ", sAddData), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    sToken = authClient.AccessToken;

                    if (sToken.Length > 0)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_gettoken_ok, url, ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("Token : ", sToken), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, new Exception(string.Concat(Languages.Languages.ws_gettoken_ko, url)), content, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }

                    break;

                default:
                    if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL).Length > 0) //récupération du token de session
                    {
                        string sInitSession = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_URL), JobParameters.DynParams);
                        sInitSession = sInitSession.Replace(WSVariables.WebServiceURL, "");
                        switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                        {
                            default:
                                Query sQInit = new(JobParameters, string.Concat("TOKEN:SELECT * FROM ", sInitSession));
                                DataSet dsInit = await GetDataFromREST(sQInit, "", SQLTools_Enums.WEBSERVICE_METHOD.POST);
                                if (dsInit.Tables.Count > 0 && dsInit.Tables[0].Rows.Count > 0)
                                {
                                    sToken = dsInit.Tables[0].Rows[0][0].ToString();
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_gettoken_ok, sInitSession, ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat("Token : ", sToken), SQLTools_Enums.LOG_TYPEINFO.DET);
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_gettoken_ko, sInitSession, ") : ", sToken), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }

                                break;
                            case "NUXEO":
                                sToken = "";
                                //TODO GetToken HTTP
                                break;
                        }
                    }
                    else if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE).Length > 0)
                    {
                        sToken = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_AUTH_VALUE);
                    }
                    else if (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS).Length > 0)
                    {
                        sToken = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_QUERY_PARAMS);
                    }
                    break;
            }

            return sToken;
        }

        private void CheckSuccessQueries(List<Tuple<RestResponse, string>> sListRespHTTP, string sWSName, Query FuzibleQuery)
        {
            for (int cptR = 0; cptR < sListRespHTTP.Count; cptR++)
            {
                if (JobParameters.WebServiceSuccessString.Length > 0)
                {

                    if (sListRespHTTP[cptR].Item1.StatusCode.ToString().Contains(Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebServiceSuccessString, JobParameters.DynParams)))
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(sWSName, " : ", Languages.Languages.ws_checksuccess01, (cptR + 1).ToString(), Languages.Languages.ws_checksuccess02), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                    else
                    {
                        Exception ex = new(string.Concat(sWSName, " : ", Languages.Languages.ws_checksuccessko))
                        {
                            Source = Connection.SConnString(JobParameters.DynParams)
                        };
                        ex.Data.Add("Row Number", (cptR + 1).ToString());
                        ex.Data.Add(sListRespHTTP[cptR].Item2, sListRespHTTP[cptR].Item1.StatusCode.ToString() + " : " + sListRespHTTP[cptR].Item1.Content == null ? sListRespHTTP[cptR].Item1.ResponseStatus.ToString() : sListRespHTTP[cptR].Item1.Content);

                        string sQuery;
                        if (WSVariables.WebServiceURL.IndexOf("&") > 0)
                        { sQuery = sListRespHTTP[cptR].Item2; }
                        else { sQuery = sListRespHTTP[cptR].Item2; }

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, (cptR + 1).ToString(), FuzibleQuery.RetryErrorOrWarning);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.ws_checksuccesskorow01, (cptR + 1).ToString(), Languages.Languages.ws_checksuccesskorow02, sListRespHTTP[cptR].Item2, Languages.Languages.ws_checksuccesskorow03, sListRespHTTP[cptR].Item1.StatusCode.ToString() + " : " + sListRespHTTP[cptR].Item1.Content == null ? "No Content" : sListRespHTTP[cptR].Item1.Content), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sQuery);
                        FuzibleQuery.QueryErrors += 1;

                    }
                }
            }
        }

        private async Task<DataSet> CreateDataSetFromHTTPAnswers(DataTable dtSourceQuery, List<Tuple<RestResponse, string>> sListRespHTTP, Query FuzibleQuery)
        {
            DataSet dsResponse = new();

            //analyse des colonnes sources à tracker et renvoyer dans le LOG retour
            List<string> sListTrackingColumn = new();
            string sJobTrack = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebServiceTrackingColumnInResponses, JobParameters.DynParams);

            //dans l'UI, on peut en invoquer plusieurs en séparant avec un ";"
            if (sJobTrack.Length > 0 && sJobTrack.IndexOf(";") > -1)
            { sListTrackingColumn = new List<string>(sJobTrack.Split(Convert.ToChar(";"))); }
            if (sJobTrack.Length > 0 && !sJobTrack.Contains(';', StringComparison.CurrentCulture))
            { sListTrackingColumn.Add(sJobTrack); }

            //recherche de clé primaire sur dataset source pour ajouter dans la table de log le "tracking source column"
            if (sListTrackingColumn.Count == 0)
            {
                List<SQLColumn> SQLCListTracking;
                SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                SQLCListTracking = SHS.GuessPKFromQuery(dtSourceQuery, SQLTools_Enums.TYPE_DATA.VARCHAR, null);
                if (SQLCListTracking.Count > 0)
                {
                    foreach (SQLColumn SQLC in SQLCListTracking)
                    { sListTrackingColumn.Add(SQLC.ColumnName); }
                }
            }
            //-------------------------------------------------------------------------

            List<int> iListQteRowsDT = new();
            int iDiff;
            for (int cpt = 0; cpt < 100; cpt++) { iListQteRowsDT.Add(0); } //TODO : douteux

            try
            {
                //parcours des datatables pour ajouter la clé
                for (int iCptStr = 0; iCptStr < sListRespHTTP.Count; iCptStr++)
                {
                    for (int cptT = 0; cptT < dsResponse.Tables.Count; cptT++)
                    { iListQteRowsDT[cptT] = dsResponse.Tables[cptT].Rows.Count; }

                    string sMimeType = sListRespHTTP[iCptStr].Item1.ContentType ?? "";
                    if (sMimeType.IndexOf("/") > 0) { sMimeType = sMimeType[(sMimeType.IndexOf("/") + 1)..].ToLower(); }

                    switch (sMimeType)
                    {
                        case "json":
                            try
                            {
                                string sJSON = sListRespHTTP[iCptStr].Item1.Content;
                                char[] sRChar = sJSON.ToCharArray(); //contrôle d'erreurs
                                int iInf = 0;
                                int iSup = 0;
                                foreach (char c in sRChar)
                                {
                                    if (c.Equals(Convert.ToChar("{"))) { iInf++; }
                                    if (c.Equals(Convert.ToChar("}"))) { iSup++; }
                                }

                                if (iInf == iSup)
                                {
                                    if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                                    {
                                        dsResponse = DataJson.jsonToDataSet(sJSON);
                                    }
                                    else
                                    {
                                        JsonParser Jsp = new(sJSON, "log")
                                        {
                                            DepthToGet = 1,
                                            AvoidSpecialCharsInColumnNames = JobParameters.GlobalParameters.JSON_PARSER_REPLACE_SPECIAL_CHARS
                                        };
                                        Jsp.OnJsonEvent += Event_JsonParser;
                                        dsResponse = await Jsp.JsonToDataSetAsync(Monitoring.TaskCancellationToken);
                                    }
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_answerdeserialize_ko, iCptStr.ToString(), ") ", sJSON.Replace("\r\n", string.Empty), Languages.Languages.ws_answerdeserialize_kojson), SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    if (dsResponse.Tables.Count == 0) { dsResponse.Tables.Add("TABLE"); }
                                    if (dsResponse.Tables[0].Columns.Count == 0)
                                    {
                                        DataColumn dc = new()
                                        {
                                            ColumnName = "WS_TXTANSWER",
                                            DataType = Type.GetType("System.String")
                                        };
                                        dsResponse.Tables[0].Columns.Add(dc);
                                        DataColumn dc2 = new()
                                        {
                                            ColumnName = "WS_TXTID",
                                            DataType = Type.GetType("System.Int32"),
                                            AutoIncrement = true,
                                            AutoIncrementStep = 1
                                        };
                                        dsResponse.Tables[0].Columns.Add(dc2);
                                    }
                                    dsResponse.Tables[0].Rows.Add(sJSON);
                                }
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.ws_answerdeserialize_ko, iCptStr.ToString(), ") ", sListRespHTTP[iCptStr].Item1.Content == null ? "No Content" : sListRespHTTP[iCptStr].Item1.Content.Replace(Environment.NewLine, " "), Languages.Languages.ws_answerdeserialize_koxml), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                            break;
                        case "xml":
                            try
                            {
                                string sXML = sListRespHTTP[iCptStr].Item1.Content;
                                char[] sRChar = sXML.ToCharArray(); //contrôle d'erreurs
                                int iInf = 0;
                                int iSup = 0;
                                foreach (char c in sRChar)
                                {
                                    if (c.Equals(Convert.ToChar("<"))) { iInf++; }
                                    if (c.Equals(Convert.ToChar(">"))) { iSup++; }
                                }

                                if (iInf == iSup)
                                {
                                    var xmlReader = XmlReader.Create(new StringReader(sXML));
                                    dsResponse.ReadXml(xmlReader, XmlReadMode.Auto);
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ws_answerdeserialize_ko, iCptStr.ToString(), ") ", sXML.Replace("\r\n", string.Empty), Languages.Languages.ws_answerdeserialize_kostring), SQLTools_Enums.LOG_TYPEINFO.WNG);

                                    if (dsResponse.Tables.Count == 0) { dsResponse.Tables.Add("TABLE"); }
                                    if (dsResponse.Tables[0].Columns.Count == 0)
                                    {
                                        DataColumn dc = new()
                                        {
                                            ColumnName = "WS_TXTANSWER",
                                            DataType = Type.GetType("System.String")
                                        };
                                        dsResponse.Tables[0].Columns.Add(dc);
                                        DataColumn dc2 = new()
                                        {
                                            ColumnName = "WS_TXTID",
                                            DataType = Type.GetType("System.Int32"),
                                            AutoIncrement = true,
                                            AutoIncrementStep = 1
                                        };
                                        dsResponse.Tables[0].Columns.Add(dc2);
                                    }
                                    dsResponse.Tables[0].Rows.Add(sXML);
                                }
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, string.Concat(Languages.Languages.ws_answerdeserialize_ko, iCptStr.ToString(), ") ", sListRespHTTP[iCptStr].Item1.Content == null ? "No Content" : sListRespHTTP[iCptStr].Item1.Content.Replace(Environment.NewLine, " "), Languages.Languages.ws_answerdeserialize_kostring), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                            break;
                        case "txt":
                            if (dsResponse.Tables.Count == 0) { dsResponse.Tables.Add("TABLE"); }
                            if (dsResponse.Tables[0].Columns.Count == 0)
                            {
                                DataColumn dc = new()
                                {
                                    ColumnName = "WS_TXTANSWER",
                                    DataType = Type.GetType("System.String")
                                };
                                dsResponse.Tables[0].Columns.Add(dc);
                                DataColumn dc2 = new()
                                {
                                    ColumnName = "WS_TXTID",
                                    DataType = Type.GetType("System.Int32"),
                                    AutoIncrement = true,
                                    AutoIncrementStep = 1
                                };
                                dsResponse.Tables[0].Columns.Add(dc2);
                            }
                            dsResponse.Tables[0].Rows.Add(sListRespHTTP[iCptStr].Item1.Content);
                            break;
                        case "octet-stream":
                            if (dsResponse.Tables.Count == 0) { dsResponse.Tables.Add("TABLE"); }
                            if (dsResponse.Tables[0].Columns.Count == 0)
                            {
                                DataColumn dc = new()
                                {
                                    ColumnName = "WS_TXTANSWER",
                                    DataType = Type.GetType("System.String")
                                };
                                dsResponse.Tables[0].Columns.Add(dc);
                                DataColumn dc2 = new()
                                {
                                    ColumnName = "WS_TXTID",
                                    DataType = Type.GetType("System.Int32"),
                                    AutoIncrement = true,
                                    AutoIncrementStep = 1
                                };
                                dsResponse.Tables[0].Columns.Add(dc2);
                            }
                            dsResponse.Tables[0].Rows.Add(sListRespHTTP[iCptStr].Item1.Content);
                            break;
                    }

                    if (dsResponse.Tables.Count == 0) //on force l'ajout d'une table même si la désérialisation a merdé
                    { dsResponse.Tables.Add("TABLE"); }

                    //ajout colonnes pour repérer chaque élément envoyé
                    for (int cptT = 0; cptT < dsResponse.Tables.Count; cptT++)
                    {
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_RowId", Type.GetType("System.Int32"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_Query", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_TargetName", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_Content", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_ContentLength", Type.GetType("System.Int64"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_ContentType", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_ErrorMessage", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_IsSuccessStatusCode", Type.GetType("System.Boolean"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_IsSuccessful", Type.GetType("System.Boolean"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_Request", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_ResponseStatus", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_ResponseUri", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_RootElement", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_Server", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_StatusCode", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_StatusDescription", Type.GetType("System.String"));
                        AddWSResponseColumn(dsResponse.Tables[cptT], "WS_Version", Type.GetType("System.String"));
                    }

                    //intégration des données dans chaque ligne
                    for (int cptT = 0; cptT < dsResponse.Tables.Count; cptT++)
                    {
                        iDiff = dsResponse.Tables[cptT].Rows.Count - iListQteRowsDT[cptT];
                        for (int cptDiff = iDiff; cptDiff > 0; cptDiff--)
                        {
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_RowId"] = iCptStr + 1;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_Query"] = sListRespHTTP[iCptStr].Item2;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_TargetName"] = dtSourceQuery.Namespace;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_Content"] = sListRespHTTP[iCptStr].Item1.Content ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_ContentLength"] = sListRespHTTP[iCptStr].Item1.ContentLength == null ? -1 : sListRespHTTP[iCptStr].Item1.ContentLength;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_ContentType"] = sListRespHTTP[iCptStr].Item1.ContentType ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_ErrorMessage"] = sListRespHTTP[iCptStr].Item1.ErrorMessage ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_IsSuccessStatusCode"] = sListRespHTTP[iCptStr].Item1.IsSuccessStatusCode;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_IsSuccessful"] = sListRespHTTP[iCptStr].Item1.IsSuccessful;
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_Request"] = sListRespHTTP[iCptStr].Item1.Request == null ? "" : (sListRespHTTP[iCptStr].Item1.Request.Method.ToString());
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_ResponseStatus"] = sListRespHTTP[iCptStr].Item1.ResponseStatus.ToString();
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_ResponseUri"] = sListRespHTTP[iCptStr].Item1.ResponseUri == null ? "" : sListRespHTTP[iCptStr].Item1.ResponseUri.ToString();
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_RootElement"] = sListRespHTTP[iCptStr].Item1.RootElement ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_Server"] = sListRespHTTP[iCptStr].Item1.Server ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_StatusCode"] = sListRespHTTP[iCptStr].Item1.StatusCode.ToString();
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_StatusDescription"] = sListRespHTTP[iCptStr].Item1.StatusDescription ?? "";
                            dsResponse.Tables[cptT].Rows[^cptDiff]["WS_Version"] = sListRespHTTP[iCptStr].Item1.Version == null ? "" : sListRespHTTP[iCptStr].Item1.Version.ToString();
                            //et les colonnes de tracking
                            if (sListTrackingColumn.Count > 0)
                            {
                                foreach (string sColName in sListTrackingColumn)
                                {
                                    if (!dsResponse.Tables[cptT].Columns.Contains(sColName))
                                    {
                                        dsResponse.Tables[cptT].Columns.Add(sColName);
                                    }

                                    if (dtSourceQuery.Columns.Contains(sColName))
                                    {
                                        dsResponse.Tables[cptT].Rows[^cptDiff][sColName] = dtSourceQuery.Rows[iCptStr][sColName].ToString();
                                    }
                                    else
                                    {
                                        dsResponse.Tables[cptT].Rows[^cptDiff][sColName] = DBNull.Value;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                dsResponse.Tables.Add("ERREUR");
                dsResponse.Tables[0].Columns.Add("MSG", Type.GetType("System.String"));
                dsResponse.Tables[0].Columns.Add("FUNCTION", Type.GetType("System.String"));
                dsResponse.Tables[0].Columns.Add("CLASS", Type.GetType("System.String"));
                dsResponse.Tables[0].Columns.Add("WSRESULT", Type.GetType("System.String"));

                string sAnswers = "";
                foreach (var t in sListRespHTTP)
                { sAnswers = string.Concat(sAnswers, ",", t.Item1.StatusCode.ToString()); }

                dsResponse.Tables[0].Rows.Add(new string[4] { ex.Message, System.Reflection.MethodBase.GetCurrentMethod().ToString(), ClassPurpose.ToString(), sAnswers });
            }

            //la désérialisation XML peut créer plusieurs tables "inutiles", je prends le parti de les supprimer pour n'en garder qu'une
            //if (dsResponse.Tables.Count > 1)
            //{
            //    for (int iT = 1; iT < dsResponse.Tables.Count; iT++)
            //    { dsResponse.Tables.RemoveAt(1); }
            //}

            if (dsResponse.Tables.Count > 0 && dsResponse.Tables[0].Columns.Contains("WS_RowId"))
            {
                dsResponse.Tables[0].Columns["WS_RowId"].SetOrdinal(0);
            }

            return dsResponse;

        }

        private void AddWSResponseColumn(DataTable dataTable, string sColumn, Type sColumnType)
        {
            DataColumn dc = new()
            {
                ColumnMapping = MappingType.Hidden,
                ColumnName = sColumn,
                DataType = sColumnType
            };
            if (!dataTable.Columns.Contains(sColumn)) { dataTable.Columns.Add(dc); }
        }

        private async Task<DataSet> DeserializeResponse(Query FuzibleQuery, MediaTypeHeaderValue cType, string sOctetStreamType, string sResponse, byte[] rawdata, string sExtractedNode, int iDepthToGet, string sForcePrimaryKeyColumn, string sOutput, string sToken)
        {
            DataSet dsData = new();

            switch (JobParameters.WebserviceTypeData)
            {
                case SQLTools_Enums.API_OPTIONS.NOTHING:

                    if (JobParameters.WebserviceRawOutput)
                    {
                        DataTable dtData = new("RAW_OUTPUT");
                        dtData.Columns.Add("RAWFILE_NAME");
                        dtData.Columns.Add("RAWFILE_BINARY", Type.GetType("System.Byte[]"));
                        dtData.Columns.Add("RAWFILE_PLAINTEXT", Type.GetType("System.String"));
                        dsData.Tables.Add(dtData);

                        DataRow dr = dsData.Tables[0].NewRow();
                        dr[0] = sOutput;
                        dr[1] = rawdata;
                        dr[2] = sResponse;
                        dsData.Tables[0].Rows.Add(dr);
                    }
                    else
                    {
                        byte[] bContent = Encoding.GetEncoding(JobParameters.GlobalParameters.WS_DEFAULT_ENCODING).GetBytes(sResponse);

                        switch (cType.MediaType)
                        {
                            case "application/json":
                                try
                                {
                                    if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                                    {
                                        dsData = DataJson.jsonToDataSet(sResponse);
                                    }
                                    else
                                    {
                                        JsonParser JsParser = new(sResponse, sExtractedNode)
                                        {
                                            DepthToGet = iDepthToGet,
                                            AvoidSpecialCharsInColumnNames = JobParameters.GlobalParameters.JSON_PARSER_REPLACE_SPECIAL_CHARS,
                                            ForcePrimaryKey = sForcePrimaryKeyColumn,
                                            RemovePrimaryKey = true
                                        };
                                        JsParser.OnJsonEvent += Event_JsonParser;
                                        dsData = await JsParser.JsonToDataSetAsync(Monitoring.TaskCancellationToken);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ws_deserialize_kojson, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sResponse);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                                break;
                            case "application/xml":
                                try
                                {
                                    StringReader xmlR = new(sResponse);
                                    dsData.ReadXml(xmlR);

                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ws_deserialize_koxml, FuzibleQuery.RetryErrorOrWarning);
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, sResponse);
                                    FuzibleQuery.QueryErrors += 1;
                                }
                                break;
                            case "application/octet-stream":
                                if (sOctetStreamType.Length > 0)
                                {
                                    Job INIP = new(JobParameters.GlobalParameters, "[0]", "WebServiceTools", false);
                                    string sFileName = Toolbox.RemoveUnauthorizedCharsInFilename(sOctetStreamType);

                                    if (sOctetStreamType.EndsWith(".CSV", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_switchcsv + " : " + sOctetStreamType, SQLTools_Enums.LOG_TYPEINFO.INF);
                                        CreateFileFromAPI(Toolbox.RemoveUnauthorizedCharsInFilename(sOctetStreamType), FuzibleQuery, bContent, dsData, new List<string>(), 0);
                                    }
                                    else if (sOctetStreamType.EndsWith(".XML", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_noparser + " : " + sOctetStreamType, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        //INIP.ConnectionString_Source = new CONNString(SQLTools_Enums.BDD.FI_CSV, "[0]", "Local XML", sPath);
                                    }
                                    else if (sOctetStreamType.EndsWith(".XLS", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_noparser + " : " + sOctetStreamType, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        //INIP.ConnectionString_Source = new CONNString(SQLTools_Enums.BDD.FI_CSV, "[0]", "Local XLS", sPath);
                                    }
                                    else if (sOctetStreamType.EndsWith(".XLSX", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_noparser + " : " + sOctetStreamType, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        //INIP.ConnectionString_Source = new CONNString(SQLTools_Enums.BDD.FI_CSV, "[0]", "Local XLS", sPath);
                                    }
                                    else
                                    {
                                        dsData.Tables.Add(new DataTable(sExtractedNode));
                                        dsData.Tables[0].Columns.Add("result");
                                        dsData.Tables[0].Columns.Add("raw", System.Type.GetType("System.Byte[]"));
                                        DataRow dr1 = dsData.Tables[0].NewRow();
                                        dr1[0] = sResponse;
                                        dr1[1] = bContent;
                                        dsData.Tables[0].Rows.Add(dr1);
                                    }
                                }
                                else
                                {
                                    dsData.Tables.Add(new DataTable(sExtractedNode));
                                    dsData.Tables[0].Columns.Add("result");
                                    dsData.Tables[0].Columns.Add("raw", System.Type.GetType("System.Byte[]"));
                                    DataRow dr2 = dsData.Tables[0].NewRow();
                                    dr2[0] = sResponse;
                                    dr2[1] = bContent;
                                    dsData.Tables[0].Rows.Add(dr2);
                                }
                                break;
                            default:

                                dsData.Tables.Add(new DataTable(sExtractedNode));
                                dsData.Tables[0].Columns.Add("result");
                                dsData.Tables[0].Columns.Add("raw", System.Type.GetType("System.Byte[]"));
                                DataRow dr = dsData.Tables[0].NewRow();
                                dr[0] = sResponse;
                                dr[1] = bContent;
                                dsData.Tables[0].Rows.Add(dr);
                                break;
                        }
                    }
                    break;

                case SQLTools_Enums.API_OPTIONS.GRAPH_DOWNLOAD_FILE_DRIVE:

                    //téléchargement d'un fichier sur le drive : la requête a donné 
                    dsData = await ProcessFileDownloadFromAPI(FuzibleQuery, cType, sResponse, rawdata, sExtractedNode, iDepthToGet, sForcePrimaryKeyColumn, sOutput, sToken);
                    break;

                case SQLTools_Enums.API_OPTIONS.LOCKED_BY_TEMPLATE:

                    switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                    {
                        case "MICROSOFT_ONEDRIVE":
                            {
                                //téléchargement d'un fichier sur le drive : la requête a donné 
                                dsData = await ProcessFileDownloadFromAPI(FuzibleQuery, cType, sResponse, rawdata, sExtractedNode, iDepthToGet, sForcePrimaryKeyColumn, sOutput, sToken);
                                break;
                            }
                        default:
                            break;
                    }
                    break;
            }

            return dsData;
        }

        private async Task<DataSet> ProcessFileDownloadFromAPI(Query FuzibleQuery, MediaTypeHeaderValue cType, string sResponse, byte[] rawdata, string sExtractedNode, int iDepthToGet, string sForcePrimaryKeyColumn, string sOutput, string sToken)
        {
            DataSet dsData = new();

            JsonParser Jsp = new(sResponse, sExtractedNode)
            {
                DepthToGet = iDepthToGet,
                AvoidSpecialCharsInColumnNames = false,
                ForcePrimaryKey = sForcePrimaryKeyColumn,
                RemovePrimaryKey = true
            };
            Jsp.OnJsonEvent += Event_JsonParser;
            dsData = await Jsp.JsonToDataSetAsync(Monitoring.TaskCancellationToken);

            if (dsData.Tables.Count > 0)
            {
                string sTableToKeep = "";
                List<string> sTablesToRemove = new();

                int iMaxTables = dsData.Tables.Count;
                for (int iT = 0; iT < iMaxTables; iT++)
                {
                    if (dsData.Tables[iT].Columns.Contains("@microsoft.graph.downloadUrl"))
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_foundgraphdownload + dsData.Tables[iT].TableName + " (" + iT.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.DET);

                        sTableToKeep = dsData.Tables[iT].TableName;
                        dsData.Tables[iT].Columns.Add("RAWFILE_NAME", Type.GetType("System.String"));
                        dsData.Tables[iT].Columns.Add("RAWFILE_BINARY", Type.GetType("System.Byte[]"));
                        dsData.Tables[iT].Columns["RAWFILE_NAME"].SetOrdinal(0);
                        dsData.Tables[iT].Columns["RAWFILE_BINARY"].SetOrdinal(1);
                        //on récupère la liste des liens générés pour télécharger les fichiers
                        int iRowsMax = dsData.Tables[iT].Rows.Count;
                        for (int iRow = 0; iRow < iRowsMax; iRow++)
                        {
                            DataRow dr = dsData.Tables[iT].Rows[iRow];

                            string sFilename = dr["name"].ToString();
                            string sURL = dr["@microsoft.graph.downloadUrl"].ToString();
                            string sIDFile = dr["id"].ToString();
                            byte[] bResult = null;

                            if (sURL.Length > 0)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_graphapiurlfound + sFilename, SQLTools_Enums.LOG_TYPEINFO.DET);

                                //téléchargement du fichier
                                RestClient RSClient = GetRSConnexion(WSVariables.WebServiceURL.Split(Convert.ToChar("?"))[0]);
                                RestRequest request = CreateSendRequest(RSClient, sURL, Method.Get, "", new Query.QTable(sFilename, sFilename, "", new List<string[]>(), Connection), FuzibleQuery);
                                RestResponse response = await RSClient.ExecuteAsync(request, Monitoring.TaskCancellationToken);
                                string content = response.Content;
                                if (response.ErrorException == null)
                                {
                                    if (response.IsSuccessful)
                                    {
                                        MediaTypeHeaderValue mType = new(response.ContentType.Split(Convert.ToChar(";"))[0].Trim());
                                        string sCharSet = JobParameters.GlobalParameters.WS_DEFAULT_ENCODING;
                                        string sOctetStreamType = "";
                                        if (response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) > -1)
                                        {
                                            mType.CharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..].Trim();
                                            sCharSet = response.ContentType[(response.ContentType.IndexOf("charset=", StringComparison.InvariantCultureIgnoreCase) + 8)..];
                                            sCharSet = sCharSet.Split(Convert.ToChar(";"))[0];
                                        }
                                        var encoding = Encoding.GetEncoding(sCharSet);
                                        bResult = response.RawBytes;
                                    }
                                }
                                if (bResult != null)
                                {
                                    FuzibleQuery.QueryAnalyzer.Tables[0].ReplaceName(sFilename);
                                    switch (Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
                                    {
                                        case "MICROSOFT_GRAPH":

                                            dr["RAWFILE_BINARY"] = bResult;
                                            dr["RAWFILE_NAME"] = sFilename;

                                            break;

                                        case "MICROSOFT_ONEDRIVE":

                                            //---------------------------------------------CONSTRUCTION POSTWORK-------------------------------------------------------
                                            string sUrl = JobParameters.ConnectionString_Source.SConnString(JobParameters.DynParams);
                                            sUrl = sUrl[0..(sUrl.IndexOf("/items/", StringComparison.OrdinalIgnoreCase) + 7)];
                                            sUrl = string.Concat(sUrl, sIDFile);


                                            if (JobParameters.WebserviceSourcePostWork.Equals("RENAME"))
                                            {
                                                //PATCH /drives/{drive-id}/items/{item-id} AVEC Content-type: application/json { "name": "new-file-name.docx" }
                                                FuzibleQuery.QueryAnalyzer.AddQueryProperty(sFilename, SQLTools_Enums.QUERY_PROPERTIES.WS_SOURCE_POSTWORK, sUrl, true);
                                            }
                                            else if (JobParameters.WebserviceSourcePostWork.Equals("DELETE"))
                                            {
                                                //DELETE /drives/{drive-id}/items/{item-id}
                                                FuzibleQuery.QueryAnalyzer.AddQueryProperty(sFilename, SQLTools_Enums.QUERY_PROPERTIES.WS_SOURCE_POSTWORK, sUrl, true);
                                            }
                                            //---------------------------------------------CONSTRUCTION POSTWORK-------------------------------------------------------

                                            CreateFileFromAPI(sFilename, FuzibleQuery, bResult, dsData, sTablesToRemove, iT);

                                            break;
                                    }
                                }
                                else
                                {
                                    bResult = new Byte[1];
                                    Array.Clear(bResult, 0, bResult.Length);
                                    dr["RAWFILE_BINARY"] = bResult;
                                    dr["RAWFILE_NAME"] = sFilename;
                                    string sAnswer = string.Concat(Languages.Languages.ws_senddata_koanswer, response.Content ?? "", ",", response.Server ?? "", ",", response.ErrorMessage == null ? "" : response.ErrorMessage.ToString(), ",", response.StatusCode.ToString(), ",", response.StatusDescription == null ? "" : response.StatusDescription.ToString(), ",", response.ResponseUri.ToString());
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, response.ErrorException ?? null, Languages.Languages.ws_deserialize_graphapidownloadko + sFilename + " (" + sAnswer + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }
                            else //c'est un sous répertoire, on va l'interroger pour récupérer les fichiers
                            {
                                bResult = new Byte[1];
                                Array.Clear(bResult, 0, bResult.Length);
                                dr["RAWFILE_BINARY"] = bResult;
                                dr["RAWFILE_NAME"] = sFilename;
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_graphapiurlnotfound + sFilename, SQLTools_Enums.LOG_TYPEINFO.DET);
                            }
                        }
                    }
                    else { sTablesToRemove.Add(dsData.Tables[iT].TableName); }
                    //suppression des tables trouvées
                }
                if (sTableToKeep.Length > 0)
                {
                    foreach (string sT in sTablesToRemove)
                    {
                        if (dsData.Tables.Contains(sT))
                        { dsData.Tables.Remove(sT); }
                    }
                    if (dsData.Tables.Contains(sTableToKeep))
                    { dsData.Tables[sTableToKeep].TableName = sOutput; }
                }
                else
                {
                    if (dsData.Tables.Count > 1) //si il y avait un jeu de données complet (se présente sous forme de plusieurs tables), c'est qu'il y a un pb de détection
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ws_deserialize_graphapinodownloadlink + " (" + "@microsoft.graph.downloadUrl" + ")", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                    else
                    {
                        foreach (string sT in sTablesToRemove)
                        {
                            if (dsData.Tables.Contains(sT))
                            { dsData.Tables.Remove(sT); }
                        }
                    }
                }
            }

            if (dsData.Tables.Count == 0)
            {
                dsData.Tables.Add(new DataTable(sOutput));
            }

            return dsData;
        }

        private void CreateFileFromAPI(string sFilename, Query FuzibleQuery, byte[] bResult, DataSet dsData, List<string> sTablesToRemove, int iT)
        {
            Job JTemp = JobParameters.DeepCopy();
            JTemp.ConnectionString_Source = JobParameters.GlobalParameters.Connections.GetConnByID("[1]"); //connexion CSV par défaut

            string sFile = sFilename;
            if (File.Exists(JTemp.ConnectionString_Source.SConnString(null) + sFile)) 
            {
                while (true) //race condition
                {
                    try
                    {
                        File.Delete(JTemp.ConnectionString_Source.SConnString(null) + sFile);
                        break;
                    }
                    catch { }
                }
            }
            
            var writer = new BinaryWriter(File.OpenWrite(JTemp.ConnectionString_Source.SConnString(null) + sFile));
            writer.Write(bResult);
            writer.Close();

            //problème des requêtes qui ont renvoyé plusieurs fichiers !!
            //parti pris : créer une table de jeu de données pour chacun des fichiers (filtrable avec un TABLE X ONLY)
            string sTable = FuzibleQuery.QueryAnalyzer == null ? ("SELECT * FROM " + sFilename) : FuzibleQuery.SQLQuery.Replace(FuzibleQuery.QueryAnalyzer.Tables[0].Raw, sFile);
            Query qTemp = new(JTemp, FuzibleQuery.RawOutput + ":" + sTable);
            FITools fiR = new(JTemp, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
            DataSet dsIntermediate = fiR.GetDataFromFile(SQLTools_Enums.BDD.FI_FILE, ref qTemp);

            if (dsIntermediate != null && dsIntermediate.Tables.Count > 0)
            {
                if (dsData.Tables.Count > iT)
                {
                    //ici on veut effacer la table qui contient les ID pour ne garder que des jeux de données préparés
                    if (!sTablesToRemove.Contains(dsData.Tables[iT].TableName)) { sTablesToRemove.Add(dsData.Tables[iT].TableName); }
                }

                dsData.Tables.Add(dsIntermediate.Tables[0].Copy());
                dsData.Tables[dsData.Tables.Count - 1].Namespace = sFile;
            }
            //MThread mtClass = new MThread(JTemp, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, 1, 0);

            //if (mtClass.QuantityOfThreadsToCompute > 0)
            //{
            //    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
            //    {
            //        int numeroThread = numThread;
            //        System.Threading.Tasks.Task th = new System.Threading.Tasks.Task(() => mtClass.RepSyncTask(numeroThread, new List<Query> { qTemp }, SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY, false, Monitoring.TaskCancellationToken));
            //        Monitoring.AddThread(th);
            //        th.Start();
            //    }

            //    while (!mtClass.AreAllThreadsFinished)
            //    {
            //        //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
            //        Thread.Sleep(100);
            //    }
            //    if (mtClass.HasOperationBeenCancelled)
            //    {
            //        throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")");
            //    }
            //}


            if (File.Exists(JTemp.ConnectionString_Source.SConnString(null) + sFile)) 
            {
                while (true) //race condition
                {
                    try
                    {
                        File.Delete(JTemp.ConnectionString_Source.SConnString(null) + sFile);
                        break;
                    }
                    catch { }
                }
            }
        }

        private void SaveWSAnswersInSource(string sWSName, Query FuzibleQuery, DataSet dsResponse)
        {
            if (JobParameters.WebServiceSaveResponseFile)
            {
                string sLogTable;
                if (JobParameters.WebserviceLogTableResponses.Length > 0)
                {
                    string sL = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobParameters.WebserviceLogTableResponses, JobParameters.DynParams);
                    sLogTable = Toolbox.RemoveSpecialCharacters(sL, "_", true).ToLower();
                }
                else { sLogTable = Toolbox.RemoveSpecialCharacters(sWSName, "_", true).ToLower(); }

                switch (JobParameters.ConnectionString_Source.SConnDriver.ToString()[..2])
                {
                    case "DB":

                        Job INILog = new(JobParameters.GlobalParameters, JobParameters.JobID, JobParameters.USER, false)
                        {
                            JobMethod = SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT,
                            OptionalTimestamp_OnInsert = true,
                            OptionalDBName_OnInsert = true,
                            OptionalRowID_OnInsert = true,
                            TrimData = true,
                            UseNull_Target = false,
                            ConnectionString_Source = JobParameters.ConnectionString_Source,
                            ConnectionString_Target = JobParameters.ConnectionString_Source,
                            DatabaseName_Source = JobParameters.DatabaseName_Source,
                            DatabaseName_Target = JobParameters.DatabaseName_Source,
                            TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING
                        };

                        if (JobParameters.GlobalParameters.WS_ALLOW_RESPONSES_ALTER_COLUMNS)
                        {
                            INILog.AlterColumnTypeOnInsert = true;
                            INILog.AlterColumnTypeOptions = 3;
                            INILog.CreatePrimaryKeyAfterHavingCreatedATable = false;
                        }
                        else
                        {
                            INILog.AlterColumnTypeOnInsert = false;
                            INILog.CreatePrimaryKeyAfterHavingCreatedATable = false;
                        }

                        Query sLogQuery = new(INILog, FuzibleQuery.RawQuery)
                        {
                            ConnectionTrg = INILog.ConnectionString_Source //lors de l'insert, pour le type date/char, on va chercher la compatibilité avec la connection SOURCE de la requête et non du job
                        };

                        SQLTools SQLLog = new(INILog, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                        int iTables = 0;
                        foreach (DataTable dtWS in dsResponse.Tables)
                        {
                            iTables++;
                            if (iTables > 1) { sLogTable = string.Concat(sLogTable, "_", iTables.ToString()); }
                            Toolbox.AddOptionalColumnsInDatasetFromINI(dtWS, INILog, sLogQuery, ref MyLog);
                            SQLLog.InsertDataInBDD(dtWS, sLogTable, sLogQuery);
                        }
                        break;

                    default:
                        if (JobParameters.ConnectionString_Source.SConnString(JobParameters.DynParams).StartsWith("SFTP") || JobParameters.ConnectionString_Source.SConnString(JobParameters.DynParams).StartsWith("FTP"))
                        {
                            Job INILogF = new(JobParameters.GlobalParameters, JobParameters.JobID, JobParameters.USER, false)
                            {
                                ConnectionString_Source = JobParameters.ConnectionString_Source,
                                ConnectionString_Target = new CONNString(SQLTools_Enums.BDD.FI_CSV, "[0]", "", WORKING_DIRECTORY),
                                MaxRowsInAFile = 1000000,
                            };
                            FITools fiT = new(INILogF, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                            //string sPath = "";
                            string sFile;
                            //sPath = string.Concat(Directory.GetCurrentDirectory(), "\\LOG\\", SQLTools_Utils.RemoveSpecialCharacters(Parameters.JobNAME.Replace("[", "").Replace("]", ""), ""), "\\");
                            for (int cptT = 0; cptT < dsResponse.Tables.Count; cptT++)
                            {
                                if (dsResponse.Tables[cptT].Rows.Count > 0)
                                {
                                    sFile = string.Concat(sLogTable, "_", DateTime.Now.ToString("yyyymmddHHmm"), "_", (cptT + 1).ToString("00"), ".TXT");
                                    fiT.BuildFileFromDs(dsResponse.Tables[cptT], sFile, FuzibleQuery);
                                }
                            }
                        }
                        else
                        {
                            Job INILogF = new(JobParameters.GlobalParameters, JobParameters.JobID, JobParameters.USER, false)
                            {
                                ConnectionString_Source = JobParameters.ConnectionString_Source,
                                ConnectionString_Target = new CONNString(SQLTools_Enums.BDD.FI_CSV, "[0]", "", WORKING_DIRECTORY),
                                MaxRowsInAFile = 1000000,
                            };
                            FITools fiT = new(INILogF, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                            string sFile;
                            for (int cptT = 0; cptT < dsResponse.Tables.Count; cptT++)
                            {
                                if (dsResponse.Tables[cptT].Rows.Count > 0)
                                {
                                    sFile = string.Concat(sLogTable, "_", DateTime.Now.ToString("yyyymmddHHmm"), "_", (cptT + 1).ToString("00"), ".TXT");
                                    fiT.BuildFileFromDs(dsResponse.Tables[cptT], sFile, FuzibleQuery);
                                }
                            }
                        }
                        break;

                }
            }
        }

        private static string GetContentTypeString(SQLTools_Enums.WEBSERVICE_CONTENT wsContent)
        {
            string sContentType = wsContent switch
            {
                SQLTools_Enums.WEBSERVICE_CONTENT.JSON => @"application/json",
                SQLTools_Enums.WEBSERVICE_CONTENT.XML => @"application/xml",
                SQLTools_Enums.WEBSERVICE_CONTENT.TXT => @"text/plain",
                SQLTools_Enums.WEBSERVICE_CONTENT.BINARY => @"application/octet-stream",
                _ => @"text/plain",
            };
            return sContentType;
        }

        private static SQLTools_Enums.WEBSERVICE_CONTENT GetContentType(string sType)
        {
            SQLTools_Enums.WEBSERVICE_CONTENT cType;
            sType = sType.Split(Convert.ToChar(";"))[0];
            if (sType.IndexOf("/json") > -1) { cType = SQLTools_Enums.WEBSERVICE_CONTENT.JSON; }
            else if (sType.IndexOf("/xml") > -1) { cType = SQLTools_Enums.WEBSERVICE_CONTENT.XML; }
            else { cType = SQLTools_Enums.WEBSERVICE_CONTENT.TXT; }

            return cType;
        }

        public async Task<List<string>> GetSOQLObjectsList(CancellationToken ctsToken)
        {
            List<string> sListObjects = new();
            DataSet dsData = new();
            string sQuery = "OBJECTS:" + QUERY_SOQL_OBJECTS;
            Query Q = new(JobParameters, sQuery);
            string sToken = await GetToken(Q);
            if (sToken.Length > 0) { dsData = await GetDataFromREST_UnknownSQL(Q, sToken, SQLTools_Enums.WEBSERVICE_METHOD.GET); }
            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow dr in dsData.Tables[0].Rows)
                { sListObjects.Add(dr[0].ToString()); }
            }
            return sListObjects;
        }

        public async Task<List<string>> GetSOQLAttributesList(string sObject)
        {
            List<string> sListObjects = new();
            DataSet dsData = new();
            string sQuery = "RECORDS:" + QUERY_SOQL_ATTRIBUTES.Replace("[OBJECT]", sObject); //valable uniquement en API>=51.0
            Query Q = new(JobParameters, sQuery);
            string sToken = await GetToken(Q);
            if (sToken.Length > 0) { dsData = await GetDataFromREST_UnknownSQL(Q, sToken, SQLTools_Enums.WEBSERVICE_METHOD.GET); }
            if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
            {
                //très bancal !
                foreach (DataTable dt in dsData.Tables)
                {
                    if (dt.TableName.Contains("records"))
                    {
                        bool bOK = false;
                        foreach (DataColumn dC in dt.Columns)
                        {
                            if (dC.ColumnName.Equals("Id")) { bOK = true; }
                            if (bOK) { sListObjects.Add(dC.ColumnName); }
                        }
                        break;
                    }
                }
            }
            return sListObjects;
        }

        internal string ExecutePostWorkOperation(Query.QProperty qP, Query FuzibleQuery)
        {
            string sResult = "";
            switch (JobParameters.ConnectionString_Source.GetParam(SQLTools_Enums.DRIVER_PARAMS.WS_TEMPLATE))
            {
                case "MICROSOFT_ONEDRIVE":
                    RestClient RSClient = GetRSConnexion(qP.Value);
                    string sToken = GetToken(FuzibleQuery).Result;
                    RestRequest request = new()
                    {
                        Method = JobParameters.WebserviceSourcePostWork == "RENAME" ? Method.Patch : Method.Delete
                    };
                    SetAuthentification(request, sToken);

                    if (JobParameters.WebserviceSourcePostWork == "RENAME")
                    {
                        //request.AddParameter("Content-type", "application/json");
                        string sFilenameFinal = string.Concat("PROCESSED_", DateTime.Now.ToString("yyyyMMddhhmm"), "_", qP.Element);
                        //request.AddJsonBody("{ \"name\": \"" + sFilenameFinal + "\" }", "\"application/json\"");
                        request.AddJsonBody(new { name = sFilenameFinal });
                    }
                    else if (JobParameters.WebserviceSourcePostWork == "DELETE")
                    {

                    }

                    RestResponse response = RSClient.Execute(request);
                    string content = response.Content;

                    if (response.ErrorException == null)
                    {
                        JsonParser Jsp = new(content, "response")
                        {
                            AvoidSpecialCharsInColumnNames = false,
                            RemovePrimaryKey = true
                        };
                        Jsp.OnJsonEvent += Event_JsonParser;
                        DataSet dsData = Jsp.JsonToDataSet();
                        foreach (DataTable dt in dsData.Tables)
                        {
                            if (dt.Columns.Contains("name"))
                            {
                                if (dt.Rows.Count > 0)
                                {
                                    sResult = string.Concat(response.StatusCode.ToString(), " : ", dt.Rows[0]["name"].ToString());
                                    break;
                                }
                                else
                                { sResult = content; break; }
                            }
                        }
                        if (sResult.Length == 0)
                        { sResult = string.Concat(Languages.Languages.ws_senddata_koanswer, response.Content ?? "", ",", response.Server ?? "", ",", response.ErrorMessage == null ? "" : response.ErrorMessage.ToString(), ",", response.StatusCode.ToString(), ",", response.StatusDescription == null ? "" : response.StatusDescription.ToString(), ",", response.ResponseUri.ToString()); }
                    }
                    else
                    { sResult = response.ErrorException.Message + " (" + response.Content + ")"; }

                    break;
                default:
                    break;
            }

            return sResult;
        }
    }
    #endregion
}
