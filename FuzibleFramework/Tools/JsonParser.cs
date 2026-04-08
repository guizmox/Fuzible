using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FuzibleFramework
{
    public class JsonParser
    {
        DataSet dsJson = null;

        public delegate void JsonParserInfo(object sender, string sInfo);
        public event JsonParserInfo OnJsonEvent;

        public bool MultiThreadComputation { get; set; } = true;

        private const string REGEX_JSON_TABLE = @"^(.+)(\{\[\d+\]\})$";
        public bool IsValid { get; } = true;
        public bool IsBson { get; set; } = false;
        public bool Success { get; internal set; } = false;
        private bool Processing { get; set; } = false;
        public string ForcePrimaryKey { get; set; } = "";
        public int DepthToGet { get; set; } = 0;
        public bool AvoidSpecialCharsInColumnNames { get; set; } = true;
        public bool ArraysAsNewTables { get; set; } = false;
        public bool RemovePrimaryKey { get; set; } = true;
        public bool WithSmartDataReorganize { get; private set; } = true;

        private string _sJson = "";
        private readonly string _sOutput = "";
        private readonly string _sJsonPrefix = "";

        public JsonParser(string sJson, string sOutput)
        {
            _sJson = DecodeEncodedNonAsciiCharacters(sJson).Trim();
            _sOutput = sOutput.Length == 0 ? "json" : sOutput;
            bool bStart = Regex.IsMatch(_sJson, "^({|\\[)");

            if (!bStart) //cas des fichiers JS de Twitter par exemple : window.YTD.tweets.part0 = [
            {
                bStart = Regex.IsMatch(_sJson, "^(.*[^{|\\[])(\\s?=\\s?)({|\\[)");

                if (bStart)
                {
                    var reg = Regex.Match(_sJson, "^(.*[^{|\\[])(\\s?=\\s?)({|\\[)");
                    _sJsonPrefix = reg.Groups[1].Value;
                }
            }

            //nettoyage 
            //_sJson = _sJson.Replace("\\\\", "\\");

            bool bEnd = Regex.IsMatch(_sJson, "(}|\\])$");
            IsValid = bStart && bEnd;
        }

        public DataSet JsonToDataSet()
        {
            try
            {
                LoadJson(new CancellationToken());
            }
            catch (Exception)
            {
                SetDataSetError(true);
            }

            if (Success) { CleanupDataSet(); }

            if (!Success) { SetDataSetError(true); }

            return dsJson;
        }

        public Task<DataSet> JsonToDataSetAsync(CancellationToken cancellationToken)
        {
            dsJson = new DataSet(_sJsonPrefix.Length == 0 ? "SHSJsonParser" : _sJsonPrefix);

            return Task<DataSet>.Factory.StartNew(() =>
            {
                try
                {
                    dsJson = LoadJson(cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    OnJsonEvent?.Invoke(this, "\t * Operation Cancelled (" + ex.Message + ")");

                    while (Processing) { Thread.Sleep(100); }
                    SetDataSetError(false);
                }
                catch { }

                if (Success)
                {
                    CleanupDataSet();
                }

                return dsJson;
            }, cancellationToken);
        }

        private DataSet LoadJson(CancellationToken cancellationToken)
        {
            Processing = true;

            OnJsonEvent?.Invoke(this, "\t * Starting Json To DataSet Process (Length : " + _sJson.Length.ToString() + ")");

            OnJsonEvent?.Invoke(this, IsValid ? "\t * Json Data is valid : " + _sOutput : "\t * Json Data is not valid ! " + _sOutput);

            dsJson = new DataSet(_sJsonPrefix.Length == 0 ? "SHSJsonParser" : _sJsonPrefix);

            //int i1 = Regex.Matches(_sJson, "\\[").Count;
            //int i2 = Regex.Matches(_sJson, "\\]").Count;
            //int i3 = Regex.Matches(_sJson, "\\{").Count;
            //int i4 = Regex.Matches(_sJson, "\\}").Count;

            //if (i1 == i2 && i3 == i4)
            //{
            int iT = -1;

            //cleanup
            //_sJson = _sJson.Trim();

            //traitement particulier des documents BSON (MongoDB)
            if (IsBson)
            {
                OnJsonEvent?.Invoke(this, "\t * Bson Data detected, cleaning it up...");
                //new JsonWriterSettings { OutputMode = JsonOutputMode.Strict }
                _sJson = Regex.Replace(_sJson, @"(ObjectId\()(\"")([a-zA-Z0-9]+)(\"")(\))", "\"$3\"");
                //new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson }
                _sJson = Regex.Replace(_sJson, @"{\s{1}""\$oid""\s{1}:\s{1}(""[a-zA-Z0-9_]+"")\s{1}}", "$1");
            }

            if (IsValid) //valeur arbitraire de contrôle qu'il y a bien quelque chose à parser
            {
                OnJsonEvent?.Invoke(this, "\t * Parsing Json Data...");

                List<JsonPattern> jsPatterns = ParseJsonString(cancellationToken);

                int iLevels = 0;
                //recherche des niveaux 
                jsPatterns = jsPatterns.OrderBy(jsP => jsP.Depth).ToList();
                iLevels = jsPatterns.Last().Depth;

                OnJsonEvent?.Invoke(this, "\t * Json Data successfully Parsed : " + (iLevels + 1).ToString() + " level(s) detected.");
                OnJsonEvent?.Invoke(this, "\t * Found " + jsPatterns.Count.ToString() + " chunk(s).");

                DataSet dsData = null;

                for (int i = 0; i <= iLevels; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Processing = false;
                        Success = false;
                        throw new Exception("Cancellation Requested");
                    }

                    if (DepthToGet > 0) { if (i > (DepthToGet - 1)) { break; } }

                    var sJsonParent = jsPatterns.Where(jSP => jSP.Depth == i).ToList();

                    //MultiThreadComputation = false;
                    //multithread...
                    if (sJsonParent.Count > 0)
                    {
                        int number = sJsonParent.Count;
                        int numParts = MultiThreadComputation ? (number < Environment.ProcessorCount ? number : (Environment.ProcessorCount * 1)) : 1;
                        int partSize = number / numParts;
                        int remainder = number % numParts;

                        List<DataSet> dsList = new();
                        List<Task> lTasks = new();

                        for (int iR = 0; iR < numParts; iR++)
                        {
                            int iStart = iR * partSize;
                            int iCount = (iR == numParts - 1 ? partSize + remainder : partSize);
                            var JsPatterns = sJsonParent.GetRange(iStart, iCount);

                            List<string> sNodesInList = new();
                            foreach (var pat in JsPatterns)
                            {
                                if (!sNodesInList.Contains(pat.NodeName))
                                {
                                    sNodesInList.Add(pat.NodeName);
                                }
                            }
                            OnJsonEvent?.Invoke(this, "\t * [Thread " + (iR + 1).ToString() + "] Processing " + JsPatterns.Count.ToString() + " chunk(s) on Level " + (i + 1).ToString() + " (" + string.Join(",", sNodesInList) + ")");
                            //on attribue le bon node parent au niveau scanné

                            List<string> sParentNodes = new();
                            if (sParentNodes.Count > 1) { if (sParentNodes[1].Equals("q2")) { sParentNodes[1] = "q1"; } }

                            sParentNodes.Clear();
                            for (int iJ = 0; iJ < JsPatterns.Count; iJ++)
                            {
                                try
                                {
                                    if (dsData != null)
                                    {
                                        var js = JsPatterns[iJ].ParentElement - 1;
                                        if (dsData.Tables.Count > js)
                                        {
                                            sParentNodes.Add(dsData.Tables[js].TableName);
                                        }
                                        else
                                        {
                                            DataTable dtClone = dsData.Tables[dsData.Tables.Count - 1].Clone();
                                            dtClone.TableName = string.Concat(dtClone.TableName, "_", js.ToString());

                                            dsData.Tables.Add(dtClone);
                                            sParentNodes.Add(dsData.Tables[dsData.Tables.Count - 1].TableName);
                                        }

                                    }
                                    else { sParentNodes.Add(""); }
                                }
                                catch
                                { }
                            }

                            lTasks.Add(Task.Factory.StartNew(() =>
                            {
                                lock (dsList)
                                { dsList.Add(CreateDataTableFromJsonPatterns(JsPatterns, iStart, iLevels, sParentNodes, dsJson)); }
                            }
                            ));
                        }

                        while (lTasks.Count(t => t.IsCompleted) < lTasks.Count)
                        {
                            Thread.Sleep(100);
                        }

                        try
                        {
                            //mixer les dsList
                            int iDs = 0;
                            foreach (DataSet ds in dsList.Cast<DataSet>().OrderBy(dsName => dsName.DataSetName))
                            {
                                if (iDs == 0)
                                { dsData = ds; }
                                else
                                {
                                    foreach (DataTable dt in ds.Tables) //leBonOutil{[X]}
                                    {
                                        if (dsData.Tables.Contains(dt.TableName))
                                        {
                                            dsData.Tables[dt.TableName].Merge(dt);
                                        }
                                        else
                                        {
                                            dsData.Tables.Add(dt.Copy());
                                        }
                                    }
                                }
                                iDs++;
                            }
                            dsList.Clear();
                            dsList = null;
                        }
                        catch (Exception ex)
                        {
                            OnJsonEvent?.Invoke(this, "\t * Unable to Manipulate Datatables - Step 1 : " + ex.Message);

                            Success = false;
                            break;
                        }

                        try
                        {
                            if (dsData.Tables.Count > 0)
                            {
                                foreach (DataTable dt in dsData.Tables)
                                {
                                    iT++;
                                    if (dsJson.Tables.Contains(dt.TableName))
                                    {
                                        string sDtName = string.Concat(dt.TableName, "_", iT.ToString());

                                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Renaming DataTable '" + dt.TableName + "' -> '" + sDtName + "'");

                                        dt.TableName = sDtName;
                                    }
                                    if (dt.Rows.Count > 0)
                                    {
                                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding DataTable '" + dt.TableName + "' to DataSet");

                                        dsJson.Tables.Add(dt.Copy());
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            OnJsonEvent?.Invoke(this, "\t * Unable to Manipulate Datatables - Step 2 : " + ex.Message);

                            Success = false;
                            break;
                        }
                    }
                    else
                    {
                        Success = true;
                        break;
                    }
                }
                Success = true;
            }
            else
            {
                SetDataSetError(true);
            }

            Processing = false;

            return dsJson;
        }

        private void SetDataSetError(bool bCreateRaw)
        {
            dsJson.Clear();
            dsJson.Dispose();
            dsJson = new DataSet();
            if (bCreateRaw)
            {
                dsJson.Tables.Add("result");
                dsJson.Tables[0].Columns.Add("error");
                DataRow dr = dsJson.Tables[0].NewRow();
                dr[0] = _sJson;
                dsJson.Tables[0].Rows.Add(dr);
                Success = false;
            }
        }

        private void CleanupDataSet()
        {
            OnJsonEvent?.Invoke(this, "\t * DataSet Consolidation (" + dsJson.Tables.Count.ToString() + " table(s))");

            Success = false;

            //List<Tuple<string, List<string>>> sMergedList = new List<Tuple<string, List<string>>>();
            //foreach (DataTable dt in dsJson.Tables)
            //{
            //    //table de type : Organization:0 ou Organization_0
            //    string sTA = Regex.Replace(dt.TableName, REGEX_JSON_TABLE, "$1");
            //    if (sMergedList.Any(r => r.Item1.Equals(sTA)))
            //    {
            //        sMergedList.First(r => r.Item1.Equals(sTA)).Item2.Add(dt.TableName);
            //    }
            //    else
            //    {
            //        Tuple<string, List<string>> t = new Tuple<string, List<string>>(sTA, new List<string> { dt.TableName });
            //        sMergedList.Add(t);
            //    }
            //}

            ////----------------------------------------------------------------------------------------------------------------------------------------------------ULTRA LONG
            ////je me retrouve donc avec une liste contenant les différentes listes de tables à merger
            //List<string> sListMerge = new List<string>();
            //foreach (var sL in sMergedList)
            //{
            //    if (sL.Item2.Count > 0) { sListMerge.Add(string.Concat(sL.Item1, " : ", sL.Item2.Count.ToString(), " table(s)")); }
            //}
            //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Merging " + dsJson.Tables.Count + " Table(s) From List (" + string.Join(",", sListMerge) + ")"); }

            //for (int i = 0; i < sMergedList.Count; i++)
            //{

            //    List<Task> lTasks = new List<Task>();
            //    var sListDTToMerge = new List<string>();
            //    var sListDTToRemove = new List<string>();

            //    List<string> sListRange = sMergedList[i].Item2; //.GetRange(iStart, iCount);

            //    if (sListRange.Count > 0)
            //    {
            //        sListDTToMerge.Add(sListRange[0]);
            //    }

            //    for (int i2 = 1; i2 < sListRange.Count; i2++)
            //    {
            //        DataTable dt1 = dsJson.Tables[sListRange[0]];
            //        DataTable dt2 = dsJson.Tables[sListRange[i2]];

            //        foreach (DataColumn dc in dt2.Columns)
            //        {
            //            if (!dt1.Columns.Contains(dc.ColumnName))
            //            {
            //                dt1.Columns.Add(dc.ColumnName, dc.DataType);
            //            }
            //        }

            //        try
            //        {
            //            foreach (DataRow dr in dt2.Rows)
            //            {
            //                dt1.ImportRow(dr);
            //            }
            //            //dt1.Merge(dt2);
            //            dsJson.Tables.Remove(sListRange[i2]);
            //        }
            //        catch (Exception ex) //on ne merge pas
            //        {
            //            if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Merging '" + dt1.TableName + "' With '" + dt2.TableName + "' Failed : " + ex.Message); }
            //            dt2.TableName = string.Concat(dt2.TableName, "_error");
            //        }
            //    }

            //    //à ce stade, il reste autant de tables que de threads : celles-ci doivent être mergées en monothread
            //    sListDTToMerge.Sort();

            //    for (int iT = 1; iT < sListDTToMerge.Count; iT++)
            //    {
            //        DataTable dt1 = dsJson.Tables[sListDTToMerge[0]];
            //        DataTable dt2 = dsJson.Tables[sListDTToMerge[iT]];
            //        try
            //        {
            //            dt1.Merge(dt2);
            //            dsJson.Tables.Remove(dt2.TableName);
            //        }
            //        catch (Exception ex) //on ne merge pas
            //        {
            //            if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Merging '" + dt1.TableName + "' With '" + dt2.TableName + "' Failed : " + ex.Message); }
            //            //dt2.TableName = string.Concat(dt2.TableName, "_error");
            //        }
            //    }
            //}

            //----------------------------------------------------------------------------------------------------------------------------------------------------ULTRA LONG

            //après le merge, je renomme les tables restantes en enlevant les index           
            for (int i = 0; i < dsJson.Tables.Count; i++)
            {
                string sDtName = Regex.Replace(dsJson.Tables[i].TableName, REGEX_JSON_TABLE, "$1");
                string sNamespace = string.Concat(dsJson.Tables[0].TableName, i > 0 ? ("_" + dsJson.Tables[i].TableName) : "");

                //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Renaming '" + dsJson.Tables[i].TableName + "' To '" + sDtName + "' (Namespace : " + sNamespace + ")"); }

                dsJson.Tables[i].TableName = sDtName;
                dsJson.Tables[i].Namespace = sNamespace;
            }

            if (WithSmartDataReorganize)
            {
                OnJsonEvent?.Invoke(this, "\t * Data Reorganization : Looking for tables (.+)([_\\-:]\\d+) pattern");

                //recherche de logique dans l'organisation des tables (ex : Organization::1, Organization::2...) avec gestion des noms
                List<Tuple<string, List<string>>> sRoots = new();
                foreach (DataTable dt in dsJson.Tables)
                {
                    //table de type : Organization:0 ou Organization_0
                    System.Text.RegularExpressions.Match mc = Regex.Match(dt.TableName, "(.+)([_\\-:]\\d+)");
                    if (mc.Success && dt.Columns.Contains(string.Concat(dt.TableName, "_id")))
                    {
                        string sStructure = mc.Groups[1].Value;
                        while (sStructure.EndsWith(":")) { sStructure = sStructure[0..^1]; }
                        while (sStructure.EndsWith("_")) { sStructure = sStructure[0..^1]; }
                        while (sStructure.EndsWith("-")) { sStructure = sStructure[0..^1]; }
                        //sStructure = string.Concat(sStructure, "_id");

                        if (sRoots.Any(r => r.Item1.Equals(sStructure)))
                        {
                            sRoots.First(r => r.Item1.Equals(sStructure)).Item2.Add(dt.TableName);
                        }
                        else
                        {
                            Tuple<string, List<string>> t = new(sStructure, new List<string> { dt.TableName });
                            sRoots.Add(t);
                        }
                    }
                }
                //on considère donc qu'on a plusieurs tables avec des données identiques qui peuvent être fusionnées
                //on renomme donc la colonne visée
                DataSet dsCompiledTuple = new("tuples");

                foreach (var tuple in sRoots)
                {
                    OnJsonEvent?.Invoke(this, "\t * Data Reorganization : " + tuple.Item1 + " - Merging " + tuple.Item2.Count + " Table(s)");

                    DataTable dtTuple = new(tuple.Item1);

                    foreach (string sTableName in tuple.Item2) //liste des tables correspond au pattern
                    {
                        DataTable dt = dsJson.Tables[dsJson.Tables.IndexOf(sTableName)];
                        dt.Columns[string.Concat(dt.TableName, "_id")].ColumnName = tuple.Item1;
                        if (dtTuple.Rows.Count == 0)
                        {
                            dtTuple = dt.Clone();
                            dtTuple.TableName = tuple.Item1;
                        }
                        foreach (DataRow dr in dt.Rows)
                        {
                            dtTuple.ImportRow(dr);
                        }
                        dsJson.Tables.Remove(sTableName);
                    }
                    dtTuple.Namespace = tuple.Item1;
                    dsCompiledTuple.Tables.Add(dtTuple);
                }

                //------------------------------------------TROP LENT !
                //foreach (var tuple in sRoots)
                //{
                //    foreach (DataTable dt in dsJson.Tables)
                //    {
                //        foreach (string sTable in tuple.Item2)
                //        {
                //            if (!dt.TableName.Equals(sTable))
                //            {
                //                if (dt.Columns.Contains(string.Concat(sTable, "_id")))
                //                {
                //                    if (!dt.Columns.Contains(tuple.Item1))
                //                    {
                //                        dt.Columns[string.Concat(sTable, "_id")].ColumnName = tuple.Item1;
                //                    }
                //                    else
                //                    {
                //                        //transvasement de la donnée
                //                        foreach (DataRow dr in dt.Rows)
                //                        {
                //                            if (dr[string.Concat(sTable, "_id")].ToString().Length > 0 && dr[tuple.Item1].ToString().Length == 0)
                //                            {
                //                                dr[tuple.Item1] = dr[string.Concat(sTable, "_id")];
                //                            }
                //                        }
                //                        dt.Columns.Remove(string.Concat(sTable, "_id"));
                //                    }
                //                }
                //                if (dt.Columns.Contains(sTable) && dt.Rows.Count == 1 && dsJson.Tables[sTable].Rows.Count == 1) //organisation propre à mon parseur
                //                {
                //                    dt.Rows[0][sTable] = dsJson.Tables[sTable].Rows[0][tuple.Item1].ToString();
                //                }
                //            }
                //        }
                //    }
                //}
                //------------------------------------------TROP LENT !

                //suppression des tables réorganisées
                //foreach (var tuple in sRoots)
                //{
                //    foreach (string sTable in tuple.Item2)
                //    {
                //        dsJson.Tables.Remove(sTable);
                //    }
                //}

                foreach (DataTable dt in dsCompiledTuple.Tables)
                {
                    while (dsJson.Tables.Contains(dt.TableName))
                    { dt.TableName = string.Concat(dt.TableName, "_"); }
                    dsJson.Tables.Add(dt.Copy());
                }
            }

            if (AvoidSpecialCharsInColumnNames)
            {
                OnJsonEvent?.Invoke(this, "\t * Cleaning Up Special Characters in Column Names..");

                for (int iTt = 0; iTt < dsJson.Tables.Count; iTt++)
                {
                    dsJson.Tables[iTt].TableName = Toolbox.RemoveSpecialCharacters(dsJson.Tables[iTt].TableName, "_", true);
                    for (int iC = 0; iC < dsJson.Tables[iTt].Columns.Count; iC++)
                    {
                        dsJson.Tables[iTt].Columns[iC].ColumnName = Toolbox.RemoveSpecialCharacters(dsJson.Tables[iTt].Columns[iC].ColumnName, "_", true);
                    }
                }
            }

            if (ForcePrimaryKey.Length > 0)
            {

            }

            //retrait de la clé primaire
            if (RemovePrimaryKey)
            {
                OnJsonEvent?.Invoke(this, "\t * Removing Primary Key(s)");

                foreach (DataTable dt in dsJson.Tables)
                {
                    dt.PrimaryKey = null;
                }
            }
            Success = true;

            OnJsonEvent?.Invoke(this, "\t * Json To DataSet Succesffully Finished !");
        }

        public static List<Tuple<string, object>> DataTableToJsonStringList(DataTable dtData, string sRowName, bool bHandleNumerics, bool bSerialize, int iColOffset)
        {
            StringBuilder sbData = new();
            List<Tuple<string, object>> sListJson = new();

            for (int cptR = 0; cptR < dtData.Rows.Count; cptR++)
            {
                sbData.Clear();

                if (sRowName.Length > 0) { sbData.AppendLine(string.Concat("{\"", sRowName, "\":")); }

                sbData.AppendLine("{");
                for (int iC = iColOffset; iC < dtData.Columns.Count; iC++)
                {
                    string sValue = dtData.Rows[cptR][iC].ToString();
                    //on détermine si c'est un bloc Json
                    JsonParser jsp = new(sValue, "test");
                    if (jsp.IsValid)
                    {
                        try { jsp.JsonToDataSet(); }
                        catch { }
                        if (!jsp.Success)
                        {
                            sValue = CleanJsonValue(sValue, bHandleNumerics);
                        }
                    }
                    else
                    {
                        sValue = CleanJsonValue(sValue, bHandleNumerics);
                    }
                    sbData.AppendLine(string.Concat("", "\"", dtData.Columns[iC].ColumnName, "\"", ": ", sValue, iC < dtData.Columns.Count - 1 ? "," : ""));
                }
                sbData.AppendLine("}");

                if (sRowName.Length > 0) { sbData.AppendLine("}"); }

                if (bSerialize)
                {
                    sListJson.Add(new Tuple<string, object>(sbData.Replace(Environment.NewLine, "").ToString(), sbData.Replace(Environment.NewLine, "")));
                }
                else
                {
                    sListJson.Add(new Tuple<string, object>(sbData.ToString(), sbData));
                }
            }
            return sListJson;
        }

        public static string CleanJsonValue(string sValue, bool bHandleNumerics)
        {
            char[] cValue = sValue.ToCharArray();
            for (int iC = 0; iC < cValue.Length; iC++)
            {
                if (cValue[iC].Equals(Convert.ToChar("["))) { cValue[iC] = Convert.ToChar("("); }
                if (cValue[iC].Equals(Convert.ToChar("{"))) { cValue[iC] = Convert.ToChar("("); }
                if (cValue[iC].Equals(Convert.ToChar("]"))) { cValue[iC] = Convert.ToChar(")"); }
                if (cValue[iC].Equals(Convert.ToChar("}"))) { cValue[iC] = Convert.ToChar(")"); }
            }
            sValue = new string(cValue);
            sValue = sValue.Replace("\"", "\"\"");
            sValue = Regex.Replace(sValue, "\\r", " ");
            sValue = Regex.Replace(sValue, "\\n", " ");

            if (sValue.Length == 0)
            {
                if (!bHandleNumerics) { sValue = string.Concat("\"", "", "\""); } else { sValue = "null"; }
            }
            else if (Toolbox.IsNumeric(sValue, null))
            {
                if (!bHandleNumerics)
                {
                    sValue = string.Concat("\"", sValue, "\"");
                }
                else
                {
                    if (!Toolbox.IsInteger(sValue, false, false))
                    {
                        sValue = string.Concat("\"", sValue, "\"");
                    }
                }
            }
            else if (Toolbox.IsBit1OrBoolean2(sValue) > 0)
            {
                if (!bHandleNumerics) { sValue = string.Concat("\"", sValue.ToLower(), "\""); } else { sValue = sValue.ToLower(); }
            }
            else
            {
                sValue = string.Concat("\"", sValue, "\"");
            }

            return sValue;
        }

        private DataSet CreateDataTableFromJsonPatterns(List<JsonPattern> jsP, int iIndexOffset, int iLevels, List<string> sParentNodes, DataSet dsLevelDown)
        {
            bool bHasChildren = jsP.Last().Depth < iLevels;
            DataSet dsData = new(iIndexOffset.ToString());
            List<bool> bReferencesPrimaryKey = new();
            int iElementInLevel = 0;

            try
            {
                for (int iR = 0; iR < jsP.Count; iR++)
                {
                    //string sDtName = string.Concat(jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName, "{[", (iR + iIndexOffset).ToString() + "]}");
                    //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Creating Datatable : " + sDtName);
                    string sDtName = string.Concat(jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName);

                    if (!dsData.Tables.Contains(sDtName))
                    {
                        dsData.Tables.Add(new DataTable(sDtName));
                        bReferencesPrimaryKey.Add(false);
                    }
                }

                var jsPP = jsP.Where(j => j.IsArray == false).ToList();

                for (int iR = 0; iR < jsP.Count; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    string sLevelName = jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName;
                    string sParentName = sParentNodes[iR].Length == 0 ? _sOutput : sParentNodes[iR];

                    string sNode = jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName;
                    //int iTable = dsData.Tables.IndexOf(string.Concat(jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName, "{[", (iR + iIndexOffset).ToString() + "]}"));
                    int iTable = dsData.Tables.IndexOf(string.Concat(jsP[iR].NodeName.Length == 0 ? _sOutput : jsP[iR].NodeName));

                    if (!jsP[iR].IsArray)
                    {
                        iElementInLevel++;
                    }

                    string sDataDef = "";
                    //recherche du subniveau
                    char[] scJs = jsP[iR].Pattern.ToCharArray();
                    int iLevel = -1;
                    int iOpen = 0;
                    bool bClose = false;

                    //trouver la référence sur la table précédente
                    DataRow drDown = null;
                    if (!sParentName.Equals(sLevelName))
                    {
                        if (dsLevelDown.Tables.Contains(sParentName))
                        {
                            drDown = dsLevelDown.Tables[sParentName].Rows.Find(jsP[iR].ParentElement.ToString());
                        }
                        else
                        {
                            foreach (DataTable dtP in dsLevelDown.Tables)
                            {
                                if (dtP.Columns.Contains(jsP[iR].NodeName))
                                {
                                    drDown = dtP.Rows.Find(jsP[iR].ParentElement.ToString());
                                    if (drDown != null)
                                    { break; }
                                }
                            }
                        }
                    }

                    //if (drDown == null)
                    //{
                    //    Console.WriteLine("debug");
                    //}
                    int iIntoSentence = 0;

                    for (int iC = 0; iC < scJs.Length; iC++)
                    {
                        bClose = false;
                        if (scJs[iC].Equals(Convert.ToChar("{")) && iIntoSentence % 2 == 0) { iLevel++; if (iLevel == 1) { iOpen = iC; } }
                        else if (scJs[iC].Equals(Convert.ToChar("}")) && iIntoSentence % 2 == 0) { iLevel--; if (iLevel == 0) { bClose = true; } }
                        else if (scJs[iC].Equals(Convert.ToChar("[")) && iIntoSentence % 2 == 0) { iLevel++; if (iLevel == 1) { iOpen = iC; } }
                        else if (scJs[iC].Equals(Convert.ToChar("]")) && iIntoSentence % 2 == 0) { iLevel--; if (iLevel == 0) { bClose = true; } }
                        else if (scJs[iC].Equals(Convert.ToChar("\"")))
                        {
                            if (!scJs[iC - 1].Equals(Convert.ToChar("\\")) || (scJs[iC - 1].Equals(Convert.ToChar("\\")) && scJs[iC - 2].Equals(Convert.ToChar("\\"))))
                            {
                                iIntoSentence++;
                            }
                        }

                        if (iLevel == 0 && bClose)
                        {
                            for (int iS = iOpen; iS <= iC; iS++) //le "=" a beaucoup d'importance ! c'est le caractère qui referme le pattern
                            {
                                scJs[iS] = Convert.ToChar(" ");
                            }
                        }
                    }
                    sDataDef = new string(scJs);
                    if (sDataDef.IndexOf("\\/") > -1) { sDataDef = sDataDef.Replace("\\/", "/"); } //potentiellement couteux

                    MatchCollection mcKeyValue = Regex.Matches(sDataDef, @"(\s*""{1}.[^""\\]*""{1}\s*:{1}((""\s*"")|(\s*""([^\\""]|\\\\|\\""|\\t|\\s|\\n)*"")|(true)|(false)|(null)|(\s*""?[-.0-9]*""?)),?\s*)", RegexOptions.IgnoreCase);


                    //cas N°1 : on est sur un clé/valeur classique
                    if (mcKeyValue.Count > 0)
                    {
                        string sLevelID = string.Concat(sLevelName, "_", "id");
                        string sParentID = Regex.IsMatch(sParentNodes[iR], REGEX_JSON_TABLE) ? string.Concat(Regex.Replace(sParentNodes[iR], REGEX_JSON_TABLE, "$1"), "_id") : string.Concat(_sOutput, "_id");

                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Entering Key/Value Mode");

                        //colonnes
                        if (dsData.Tables[iTable].Columns.Count == 0)
                        {
                            if (bHasChildren && !dsData.Tables[iTable].Columns.Contains(sLevelID))
                            {
                                //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Primary Key Column : " + sLevelID);

                                dsData.Tables[iTable].Columns.Add(sLevelID);
                                dsData.Tables[iTable].PrimaryKey = new DataColumn[] { dsData.Tables[iTable].Columns[sLevelID] };
                            }
                            if (sParentNodes[iR].Length > 0 && !dsData.Tables[iTable].Columns.Contains(sParentID))
                            {
                                //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Foreign Key Column : " + sParentID);

                                dsData.Tables[iTable].Columns.Add(sParentID);
                            }
                            if (ForcePrimaryKey.Length > 0 && drDown != null && drDown.Table.Columns.Contains(ForcePrimaryKey))
                            {
                                //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Forced Foreign Key Column : " + ForcePrimaryKey);

                                dsData.Tables[iTable].Columns.Add(ForcePrimaryKey); bReferencesPrimaryKey[iTable] = true;
                            }

                            foreach (System.Text.RegularExpressions.Match mc in mcKeyValue)
                            {
                                string sCol = mc.Value; //.Trim();
                                char[] sSearchCol = sCol.ToCharArray();
                                int iGm = 0;
                                int iLastGuill = 0;
                                int iEnd = 0;
                                for (int iC = 0; iC < sSearchCol.Length; iC++)
                                {
                                    if (sSearchCol[iC].Equals(Convert.ToChar("\"")))
                                    {
                                        iGm++; iLastGuill = iC;
                                    }
                                    if (iGm > 1 && sSearchCol[iC].Equals(Convert.ToChar(":")))
                                    {
                                        iEnd = iC; break;
                                    }
                                }
                                //recherche d'un offset : ex : { "_id" : "5fa01aa4352e5e58fecd8b5a", "id_sample" 
                                //-> Il y a un espace avant le nom de colonne
                                int iOffset = 0;
                                for (int i = 0; i < sSearchCol.Length; i++)
                                {
                                    if (sSearchCol[i].Equals(Convert.ToChar(" "))
                                                  || sSearchCol[i].Equals(Convert.ToChar("\""))
                                                  || sSearchCol[i].Equals(Convert.ToChar("\r"))
                                                  || sSearchCol[i].Equals(Convert.ToChar("\t"))
                                                  || sSearchCol[i].Equals(Convert.ToChar("\n"))) { iOffset++; }
                                    else { break; }
                                }
                                sCol = new string(sSearchCol, iOffset, iEnd - (iEnd - iLastGuill) - iOffset);

                                //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Column : " + sCol);

                                if (!dsData.Tables[iTable].Columns.Contains(sCol))
                                { dsData.Tables[iTable].Columns.Add(sCol); }
                            }
                        }

                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding " + mcKeyValue.Count.ToString() + " row(s)");

                        //valeurs
                        if (dsData.Tables[iTable].Columns.Count > 0)
                        {
                            DataRow dr = dsData.Tables[iTable].NewRow();
                            for (int iC = 0; iC < mcKeyValue.Count; iC++)
                            {
                                string sCol = mcKeyValue[iC].Value; //.Trim();
                                char[] sSearchCol = sCol.ToCharArray();
                                int iGm = 0;
                                int iLastGuill = 0;
                                int iEnd = 0;
                                for (int iCb = 0; iCb < sSearchCol.Length; iCb++)
                                {
                                    if (sSearchCol[iCb].Equals(Convert.ToChar("\"")))
                                    {
                                        iGm++; iLastGuill = iCb;
                                    }
                                    if (iGm > 1 && sSearchCol[iCb].Equals(Convert.ToChar(":")))
                                    {
                                        iEnd = iCb; break;
                                    }
                                }
                                sCol = new string(sSearchCol, 0, iEnd + 1);

                                string sValue = mcKeyValue[iC].Value[sCol.Length..];

                                //sValue = sValue.Replace("\"", "").Trim();

                                //------------------------------------------------AVRIL 2021 : NETTOYAGE
                                sValue = sValue.Replace("\\\"", "\"").Trim();
                                if (sValue.StartsWith("\"")) { sValue = sValue[1..]; }
                                if (sValue.EndsWith("\",")) { sValue = string.Concat(sValue[0..^2], ","); }
                                else if (sValue.EndsWith("\"")) { sValue = sValue[0..^1]; }
                                if (sValue.IndexOf("\\n") > -1) { sValue = sValue.Replace("\\n", Environment.NewLine); }
                                //------------------------------------------------AVRIL 2021

                                int iOffset = 0;
                                for (int i = 0; i < sSearchCol.Length; i++)
                                {
                                    if (sSearchCol[i].Equals(Convert.ToChar(" "))
                                            || sSearchCol[i].Equals(Convert.ToChar("\""))
                                            || sSearchCol[i].Equals(Convert.ToChar("\r"))
                                            || sSearchCol[i].Equals(Convert.ToChar("\t"))
                                            || sSearchCol[i].Equals(Convert.ToChar("\n"))) { iOffset++; }
                                    else { break; }
                                }
                                sCol = new string(sSearchCol, iOffset, iEnd - (iEnd - iLastGuill) - iOffset);

                                if (!dsData.Tables[iTable].Columns.Contains(sCol))
                                {
                                    dsData.Tables[iTable].Columns.Add(sCol);
                                }

                                if (sValue.EndsWith(",")) { sValue = sValue[0..^1]; }
                                if (sValue.Equals("null")) { sValue = string.Empty; }
                                try { dr[sCol] = sValue; }
                                catch (Exception ex)
                                {
                                    OnJsonEvent?.Invoke(this, "\t * Unable to parse Value : '" + sValue + "' Into '" + sCol + "' (" + ex.Message + ")");
                                    throw;
                                }
                            }

                            if (bHasChildren)
                            {
                                try
                                {
                                    dr[sLevelID] = (iElementInLevel + iIndexOffset).ToString();
                                }
                                catch (Exception ex)
                                {
                                    OnJsonEvent?.Invoke(this, "\t * Unable to parse Value : '" + iElementInLevel.ToString() + "' Into '" + sLevelID + "' (" + ex.Message + ")");
                                    throw;
                                }
                            }
                            if (sParentNodes[iR].Length > 0)
                            {
                                try
                                {
                                    dr[sParentID] = jsP[iR].ParentElement.ToString();
                                }
                                catch (Exception ex)
                                {
                                    OnJsonEvent?.Invoke(this, "\t * Unable to parse Value : '" + jsP[iR].ParentElement.ToString() + "' Into '" + sParentID + "' (" + ex.Message + ")");
                                    throw;
                                }
                            }

                            dsData.Tables[iTable].Rows.Add(dr);
                        }
                    }
                    else
                    {
                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Entering Array Mode");

                        //cas N°2 : on est sur un array
                        sDataDef = sDataDef.Replace(System.Environment.NewLine, " ");

                        if (sDataDef.StartsWith("[") && sDataDef.EndsWith("]"))
                        {
                            MatchCollection mcKeyValueArrayB = Regex.Matches(sDataDef, "\\[[\\s]*(\".[^\"]+\"){1}(,[\\s]*\".[^\"]+\")*[\\s]*\\]", RegexOptions.IgnoreCase);

                            if (ArraysAsNewTables)
                            {
                                if (mcKeyValueArrayB.Count > 0)
                                {
                                    if (!dsData.Tables[iTable].Columns.Contains(sNode))
                                    {
                                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Column and Row : " + sNode);

                                        dsData.Tables[iTable].Columns.Add(sNode);
                                    }

                                    DataRow dr = dsData.Tables[iTable].NewRow();
                                    dr[sNode] = mcKeyValueArrayB.Count > 0 ? mcKeyValueArrayB[0].Value : sDataDef;

                                    if (sParentNodes[iR].Length > 0)
                                    {
                                        string sCID = string.Concat(sParentNodes[iR], "_id");
                                        //if (!dsData.Tables[iTable].Columns.Contains(sCID))
                                        //{ dsData.Tables[iTable].Columns.Add(sCID); }
                                        try
                                        {
                                            dr[sCID] = jsP[iR].ParentElement.ToString();
                                        }
                                        catch (Exception ex)
                                        {
                                            OnJsonEvent?.Invoke(this, "\t * Unable to parse Value : '" + jsP[iR].ParentElement.ToString() + "' Into '" + sCID + "' (" + ex.Message + ")");
                                            throw;
                                        }
                                    }
                                    dsData.Tables[iTable].Rows.Add(dr);
                                }
                            }
                            else
                            {
                                if (drDown != null) { drDown[sNode] = sDataDef[1..^1]; }
                            }
                        }
                        else
                        {
                            //    string sCol = string.Concat(sNode, "_unknown_data");
                            //    if (!dsData.Tables[iTable].Columns.Contains(sCol))
                            //    {
                            //        dsData.Tables[iTable].Columns.Add(sCol);
                            //        dsData.Tables[iTable].Columns[sCol].Namespace = "UNKNOWN";
                            //    }
                            //    bHasUnknownData[iTable] = true;
                            //    dsData.Tables[iTable].Columns[sCol].SetOrdinal(dsData.Tables[iTable].Columns.Count - 1);
                            //    DataRow dr = dsData.Tables[iTable].NewRow();
                            //    dr[sCol] = sP;
                            //    dsData.Tables[iTable].Rows.Add(dr);
                        }

                    }
                    //si on veut forcer une clé primaire (retrouver la référence de l'élément inférieur)
                    if (drDown != null && ForcePrimaryKey.Length > 0 && bReferencesPrimaryKey[iTable])
                    {
                        //if (OnJsonEvent != null) { OnJsonEvent(this, "\t * Adding Reference to Primary Key on Column : " + ForcePrimaryKey);

                        dsData.Tables[iTable].Rows[^1][ForcePrimaryKey] = drDown[ForcePrimaryKey];
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Processing = false;
                Success = false;
                throw;
            }
            catch (Exception ex)
            {
                OnJsonEvent?.Invoke(this, "\t * Unable to process Json Data (Element : " + iElementInLevel + ") : " + ex.Message);
                DataTable dtError = new("JSONPARSER_EXCEPTION");
                dtError.Columns.Add("data");
                dtError.Columns.Add("exception");
                dtError.Columns.Add("source");
                DataRow dr = dtError.NewRow();
                dr[0] = _sJson;
                dr[1] = ex.Message;
                dr[2] = ex.Source ?? "";
                dtError.Rows.Add(dr);
                dsData.Tables.Add(dtError);
            }

            return dsData;
        }

        private List<JsonPattern> ParseJsonString(CancellationToken cancellationToken)
        {
            List<JsonPattern> jsP = new();

            int iLevel = 0;
            List<int> iListPosParO = new();
            List<int> iElementInLevel = new();
            List<List<string>> sLevelNodeName = new();

            bool bIsArray = false;
            int iIntoSentence = 0;
            string sName;

            char[] scJs = _sJson.ToCharArray();

            for (int iC = 0; iC < scJs.Length; iC++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (iLevel >= iListPosParO.Count) { iListPosParO.Add(0); sLevelNodeName.Add(new List<string>()); }

                    bool bClose = false;

                    if (scJs[iC].Equals('{') && iIntoSentence % 2 == 0)
                    {
                        iListPosParO[iLevel] = iC;
                        iLevel++;
                        if (iElementInLevel.Count < iLevel) { iElementInLevel.Add(0); }
                        iElementInLevel[iLevel - 1]++;
                    }
                    else if (scJs[iC].Equals('}') && iIntoSentence % 2 == 0)
                    {
                        bClose = true;
                        iLevel--;
                        bIsArray = false;
                    }
                    else if (scJs[iC].Equals('[') && iIntoSentence % 2 == 0)
                    {
                        iListPosParO[iLevel] = iC;
                        bIsArray = true;
                    }
                    else if (scJs[iC].Equals(']') && iIntoSentence % 2 == 0)
                    {
                        bClose = true;
                    }
                    else if (scJs[iC].Equals('"'))
                    {
                        if (scJs[iC - 1].Equals('\\') && scJs[iC - 2].Equals('\\') && !scJs[iC - 3].Equals('\\'))
                        {
                            iIntoSentence++;
                        }
                        else if (scJs[iC - 1].Equals('\\') && scJs[iC - 2].Equals('\\') && scJs[iC - 3].Equals('\\'))
                        {

                        }
                        else if (!scJs[iC - 1].Equals('\\'))
                        {
                            iIntoSentence++;
                        }
                        //    iIntoSentence++;
                        //}
                    }

                    if (bClose && iLevel >= 0)
                    {
                        string sPattern = new(scJs, iListPosParO[iLevel], iC - iListPosParO[iLevel] + 1);

                        //if ((Regex.Matches(sPattern, "\\{").Count == Regex.Matches(sPattern, "\\}").Count) && (Regex.Matches(sPattern, "\\[").Count == Regex.Matches(sPattern, "\\]").Count))
                        //{
                        if ((sPattern.StartsWith("{") && sPattern.EndsWith("}")) || (sPattern.StartsWith("[") && sPattern.EndsWith("]")))
                        {
                            int iDepth = iLevel;

                            sName = JsonPattern.GetNameFromSubstring(scJs, iListPosParO[iLevel]);

                            //if (sName.Contains("<"))
                            //{
                            //    string s = new string(scJs);
                            //    s = s.Substring(iListPosParO[iLevel] - 500, 500);
                            //    s = sPattern;
                            //    File.WriteAllText("DUMP.TXT", _sJson);
                            //}

                            //ajout janvier 2021 : pour corriger le problème de l'API v3 youtube : 
                            //si on ne trouve pas le nom du noeud, on prend le dernier nom de noeud existant dans ce niveau
                            if (sName.Length == 0 && sLevelNodeName[iLevel].Count > 0)
                            {
                                sName = sLevelNodeName[iLevel].Last();
                            }

                            //if (sName.Contains("<"))
                            //{ Console.WriteLine(""); }
                            sLevelNodeName[iLevel].Add(sName);

                            int iParentNode;
                            if (iLevel == 0) { iParentNode = 1; }
                            else
                            {
                                if (bIsArray) { iParentNode = (iLevel - 1 < 0) ? 1 : iElementInLevel[iLevel - 1]; }
                                else { iParentNode = iElementInLevel[iLevel - 1]; }
                            }

                            jsP.Add(new JsonPattern(sName.Length == 0 ? sLevelNodeName[iLevel][0] : sName, sPattern, bIsArray, iDepth, iParentNode));
                        }

                        if (bIsArray)
                        {
                            bIsArray = false;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Processing = false;
                    Success = false;
                    throw;
                }
            }
            return jsP;
        }

        internal static string DecodeEncodedNonAsciiCharacters(string input)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < input.Length;)
                {
                    if (i + 6 <= input.Length && input[i] == '\\' && input[i + 1] == 'u')
                    {
                        string hex1 = input.Substring(i + 2, 4);
                        if (int.TryParse(hex1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code1))
                        {
                            if (0xD800 <= code1 && code1 <= 0xDBFF && i + 12 <= input.Length &&
                                input[i + 6] == '\\' && input[i + 7] == 'u')
                            {
                                string hex2 = input.Substring(i + 8, 4);
                                if (int.TryParse(hex2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code2) &&
                                    0xDC00 <= code2 && code2 <= 0xDFFF)
                                {
                                    // surrogate pair
                                    int utf32 = 0x10000 + ((code1 - 0xD800) << 10) + (code2 - 0xDC00);
                                    sb.Append(char.ConvertFromUtf32(utf32));
                                    i += 12;
                                    continue;
                                }
                            }

                            // single \uXXXX
                            sb.Append((char)code1);
                            i += 6;
                            continue;
                        }
                    }

                    sb.Append(input[i]);
                    i++;
                }

                return sb.ToString();
            }
            catch
            {
                return input;
            }
            //try
            //{
            //return Regex.Replace(
            //    value,
            //    @"\\u(?<Value>[a-zA-Z0-9]{4})",
            //    m =>
            //    {
            //        return ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString();
            //    });

            //}
            //catch
            //{
            //    return value;
            //}
        }

        internal class JsonPattern
        {
            public string NodeName
            {
                get; set;
            }
            public string Pattern
            {
                get; set;
            }
            public bool IsArray
            {
                get;
            }
            public int Depth
            {
                get;
            }
            public int ParentElement
            {
                get;
            }

            public JsonPattern(string sName, string sPattern, bool bIsArray, int iDepth, int iParent)
            {
                Pattern = sPattern;
                IsArray = bIsArray;
                Depth = iDepth;
                ParentElement = iParent;
                NodeName = sName;
            }

            public static string GetNameFromSubstring(char[] sJson, int iPosStart)
            {
                List<int> iPosQuotes = new();
                int iParPos = 0;
                string sNodeName = "";

                int iOffset = 50;
                if (iPosStart - iOffset < 0) { iOffset = iPosStart; }

                //string s = "";
                //for (int i = iPosStart - iOffset; i < iPosStart; i++)
                //{ s = string.Concat(s, sJson[i]); }

                //string s = new string(sJson);
                //s = s.Substring(iPosStart - iOffset, iOffset);

                for (int i = iPosStart - iOffset; i < iPosStart; i++)
                {
                    if (sJson[i].Equals(']') || sJson[i].Equals('}'))
                    {
                        iParPos = i;
                    }
                    if (sJson[i].Equals('"'))
                    {
                        iPosQuotes.Add(i);
                    }
                }
                if (iPosQuotes.Count >= 2)
                {
                    if (iPosQuotes.Last() > iParPos) // on est sur l'élément d'un niveau
                    {
                        for (int iQ = iPosQuotes[^2] + 1; iQ < iPosQuotes.Last(); iQ++)
                        {
                            sNodeName = string.Concat(sNodeName, sJson[iQ]);
                        }
                    }
                    //else //je sais pas mais ça fonctionne
                    //{
                    //    if (iPosLevelPrec > -1) 
                    //    { sNodeName = GetNameFromSubstring(sJson, iPosLevelPrec, -1); }
                    //}
                }

                //if (sNodeName.Contains("<"))
                //{
                //string s = new string(sJson);
                //s = s.Substring(iPosStart - iOffset, iOffset);
                //}

                return sNodeName;
            }
        }
    }
}
