using DataConvert;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FuzibleFramework
{
    public class NOSQLTools
    {
        #region "CONSTANTES"

        private const string MONGO_INTERNAL_INDEX = "_id";
        private const string FUZIBLE_INTERNAL_INDEX = "idx_bsondoc";
        private const int TIMEOUT = 10;
        #endregion

        #region "VARIABLES"

        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose { get; }
        public Job JobParameters;
        public CONNString Connection;
        public LogTools MyLog;

        #endregion

        #region "PROPRIETES"

        public SQLTools_Enums.CHARACTER_SET CharacterSet { get; set; } = SQLTools_Enums.CHARACTER_SET.utf8;
        private string DatabaseName { get; } = "";
        public SQLTools_Enums.MONGODB_DOCUMENT DocumentFormat { get; } = SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON;

        #endregion

        #region "PUBLIC VOID"

        public NOSQLTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            JobParameters = INIP;
            MyLog = LogJob;

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.SRC: //SOURCE
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.SRC;
                    Connection = INIP.ConnectionString_Source;
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG: //TARGET
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.TRG;
                    Connection = INIP.ConnectionString_Target;
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Target, INIP.DynParams);
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.LOG: //LOG
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.LOG;
                    List<string> sListParams;
                    sListParams = SQLQueries.ParamsForSQLDriver(JobParameters.GlobalParameters.LOG_BDDDRIVER, JobParameters.GlobalParameters.LOG_CONNECTIONSTRINGSCHEMA);
                    Connection = new CONNString(JobParameters.GlobalParameters.LOG_BDDDRIVER, "[0]", "SHS", JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, sListParams) { };
                    DatabaseName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(Toolbox.GetDatabaseNameFromConnectionString(JobParameters.GlobalParameters.LOG_CONNECTIONSTRING, JobParameters.GlobalParameters.LOG_BDDDRIVER), INIP.DynParams);
                    break;
            }
            //BsonDocumentFormat = new FuzibleBSonDocument();
            if (DatabaseName.Length == 0)
            { DatabaseName = Toolbox.GetDatabaseNameFromConnectionString(Connection.SConnString(INIP.DynParams), Connection.SConnDriver); }

            DocumentFormat = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.MONGODB_DOCUMENT).Length > 0 ? (SQLTools_Enums.MONGODB_DOCUMENT)Enum.Parse(typeof(SQLTools_Enums.MONGODB_DOCUMENT), Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.MONGODB_DOCUMENT)) : SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON;
        }

        public DataSet GetDataFromMongoDB(ref Query FuzibleQuery)
        {
            DataSet dsData = new();
            DataSet dsJoined = new();


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
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_get_subquery + SubQuery.RawQuery, SQLTools_Enums.LOG_TYPEINFO.INF);
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
                    if (iTable == 0)
                    {
                        dsData = GetCollectionData(ref FuzibleQuery, qT.Name);
                    }
                    else
                    {
                        dsJoined = GetCollectionData(ref FuzibleQuery, qT.Name);
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
            }
            dsData.CaseSensitive = false;

            //ajout des colonnes optionnelles
            //if (DocumentFormat != SQLTools_Enums.MONGODB_DOCUMENT.FUZIBLEBSON) //le format "maison" ajoute les colonnes dans le premier dataset
            //{
            for (int iT = 0; iT < dsData.Tables.Count; iT++)
            { Toolbox.AddOptionalColumnsInDatasetFromINI(dsData.Tables[iT], JobParameters, FuzibleQuery, ref MyLog); }
            //}

            return dsData;
        }

        public Int64 GetCollectionCount(string sCollection)
        {
            MongoClient clMongo = Connect();
            Int64 iCount = 0;

            string sDatabase = DatabaseName;
            if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
            IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

            if (CollectionExists(dbMongo, sCollection))
            {
                switch (DocumentFormat)
                {
                    case SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON:
                        var collecA = dbMongo.GetCollection<BsonDocument>(sCollection);
                        iCount = collecA.CountDocuments(new BsonDocument());
                        break;
                    case SQLTools_Enums.MONGODB_DOCUMENT.FUZIBLEBSON:
                        var collecB = dbMongo.GetCollection<FuzibleBSonDocument>(sCollection);
                        iCount = collecB.CountDocuments(new BsonDocument());
                        break;
                    case SQLTools_Enums.MONGODB_DOCUMENT.STRINGBSON:
                        var collecC = dbMongo.GetCollection<BsonDocument>(sCollection);
                        iCount = collecC.CountDocuments(new BsonDocument());
                        //IMongoCollection<string> mongoCollectionS = dbMongo.GetCollection<string>(sCollection);
                        //cursor = mongoCollectionS.Find(bFilter).Sort(bSort).Project(bProjection).Limit(FuzibleQuery.QueryAnalyzer.LimitedResults).ToCursor();
                        break;
                }
            }

            return iCount;
        }

        private DataSet GetCollectionData(ref Query FuzibleQuery, string sCollection)
        {
            DataSet dsData = new();

            MongoClient clMongo = Connect();

            string sDatabase = DatabaseName;
            if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
            IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

            if (CollectionExists(dbMongo, sCollection))
            {
                int iDoc = 0;
                int iTDX = -1;
                string sColIdx = "";
                List<string> sCol1;
                List<string> sCol2;
                DataSet dsTemp = null;
                IAsyncCursor<dynamic> cursor = null;

                switch (DocumentFormat)
                {
                    case SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON:
                        IMongoCollection<BsonDocument> mongoCollectionB = dbMongo.GetCollection<BsonDocument>(sCollection);
                        ProjectionDefinition<BsonDocument> bProjectionB = GetProjectionStandardBson(FuzibleQuery, sCollection);
                        FilterDefinition<BsonDocument> bFilterB = GetFilterStandardBson(FuzibleQuery, sCollection);
                        SortDefinition<BsonDocument> bSortB = GetSortingStandardBson(FuzibleQuery, sCollection);
                        cursor = mongoCollectionB.Find(bFilterB).Sort(bSortB).Project(bProjectionB).Limit(FuzibleQuery.QueryAnalyzer.LimitedResults).ToCursor();
                        break;
                    case SQLTools_Enums.MONGODB_DOCUMENT.FUZIBLEBSON:
                        IMongoCollection<FuzibleBSonDocument> mongoCollectionF = dbMongo.GetCollection<FuzibleBSonDocument>(sCollection);
                        ProjectionDefinition<FuzibleBSonDocument> bProjectionF = GetProjectionFuzibleBson(FuzibleQuery, sCollection);
                        FilterDefinition<FuzibleBSonDocument> bFilterF = GetFilterFuzibleBson(FuzibleQuery, sCollection);
                        SortDefinition<FuzibleBSonDocument> bSortF = GetSortingFuzibleBson(FuzibleQuery, sCollection);
                        cursor = mongoCollectionF.Find(bFilterF).Sort(bSortF).Project(bProjectionF).Limit(FuzibleQuery.QueryAnalyzer.LimitedResults).ToCursor();
                        break;
                    case SQLTools_Enums.MONGODB_DOCUMENT.STRINGBSON:
                        //IMongoCollection<string> mongoCollectionS = dbMongo.GetCollection<string>(sCollection);
                        //cursor = mongoCollectionS.Find(bFilter).Sort(bSort).Project(bProjection).Limit(FuzibleQuery.QueryAnalyzer.LimitedResults).ToCursor();
                        break;
                }

                foreach (dynamic bsD in cursor.ToEnumerable())
                {
                    iDoc++;
                    sColIdx = string.Concat("bsondoc_" + sCollection);

                    switch (DocumentFormat)
                    {
                        case SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON:
                            BsonDocument docB = BsonSerializer.Deserialize<BsonDocument>(bsD);
                            string jsonB = docB.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

                            if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                            {
                                dsTemp = DataJson.jsonToDataSet(jsonB);
                            }
                            else
                            {
                                JsonParser jsPB = new(jsonB, sColIdx) { IsBson = true };
                                jsPB.OnJsonEvent += Event_JsonParser;
                                dsTemp = jsPB.JsonToDataSetAsync(Monitoring.TaskCancellationToken).Result;
                            }

                            break;
                        case SQLTools_Enums.MONGODB_DOCUMENT.FUZIBLEBSON:
                            BsonDocument docF = BsonSerializer.Deserialize<BsonDocument>(bsD);
                            string jsonF = docF.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

                            if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                            {
                                dsTemp = DataJson.jsonToDataSet(jsonF);
                            }
                            else
                            {
                                JsonParser jsPF = new(jsonF, sColIdx) { IsBson = true };
                                jsPF.OnJsonEvent += Event_JsonParser;
                                dsTemp = jsPF.JsonToDataSetAsync(Monitoring.TaskCancellationToken).Result;
                            }

                            break;
                        case SQLTools_Enums.MONGODB_DOCUMENT.STRINGBSON:
                            string docS = (string)bsD;
                            dsTemp = new DataSet(sColIdx);
                            dsTemp.Tables.Add(sColIdx);
                            dsTemp.Tables[0].Columns.Add("data", Type.GetType("System.String"));
                            DataRow dr = dsTemp.Tables[0].NewRow();
                            dr[0] = docS;
                            dsTemp.Tables[0].Rows.Add(dr);
                            break;
                    }
                    for (int iT = 0; iT < dsTemp.Tables.Count; iT++)
                    {
                        //dsTemp.Tables[iT].Columns.Add(FUZIBLE_INTERNAL_INDEX, Type.GetType("System.Int32"));
                        //foreach (DataRow dr in dsTemp.Tables[iT].Rows)
                        //{ dr[FUZIBLE_INTERNAL_INDEX] = iDoc; }

                        iTDX = dsData.Tables.IndexOf(dsTemp.Tables[iT].TableName);
                        if (iTDX > -1)
                        {
                            //si une table avec le même nom existe, est-ce qu'elle possède les mêmes colonnes ? 
                            //si oui, on fusionne, sinon, on crée une table supplémentaire
                            sCol1 = dsData.Tables[iTDX].Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToList();
                            sCol2 = dsTemp.Tables[iT].Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToList();
                            if (sCol1.SequenceEqual(sCol2))
                            { dsData.Tables[iTDX].Merge(dsTemp.Tables[iT]); }
                            else
                            {
                                dsTemp.Tables[iT].TableName = dsTemp.Tables[iT].TableName + "_" + iDoc.ToString();
                                dsData.Tables.Add(dsTemp.Tables[iT].Copy());
                            }
                        }
                        else { dsData.Tables.Add(dsTemp.Tables[iT].Copy()); }
                    }
                }
                Toolbox.MergeAndSortDataTables(dsData);
            }

            return dsData;
        }

        private void Event_JsonParser(object sender, string sInfo)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, sInfo, SQLTools_Enums.LOG_TYPEINFO.DET);
        }

        public void InsertDataToMongoDB(DataTable dtData, string sCollName, Query sQ)
        {
            MongoClient clMongo = Connect();

            string sDatabase = DatabaseName;
            if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
            IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

            if (!CollectionExists(dbMongo, sCollName)) { dbMongo.CreateCollection(sCollName); }

            int iRows = dtData.Rows.Count;

            //ajout colonne de référencement fuzible ???

            if (iRows > 0)
            {
                //compatibilité INT64 : je ne sais pas gérer le INT64
                for (int iC = 0; iC < dtData.Columns.Count; iC++)
                {
                    if (dtData.Columns[iC].DataType == Type.GetType("System.Int64"))
                    { Toolbox.ConvertColumnType(dtData, dtData.Columns[iC], Type.GetType("System.Int32")); }
                }

                //exécution des insert en multithread
                MThread mtClass = new(JobParameters, ClassPurpose, MyLog, iRows, 0);

                if (mtClass.QuantityOfThreadsToCompute > 0)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_get_insertdata + sCollName + "(" + iRows.ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.INF);
                    for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                    {
                        mtClass.INIT_InsertNOSQLFromDt(dtData, sCollName);
                        //Thread th = new Thread(mtClass.InsertInBDDFromDatatable);
                        //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                        //th.Start(numThread);
                        // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                        int numeroThread = numThread;
                        Task th = new(() => mtClass.InsertInNOSQLFromDt(numeroThread, sQ));
                        Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                        th.Start();
                    }

                    //si on doit attendre la fin de l'opération multithread, on boucle, sinon on passe outre !
                    if (!JobParameters.TurboMode)
                    {
                        while (!mtClass.AreAllThreadsFinished)
                        {
                            //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                            Thread.Sleep(100);
                        }
                        if (mtClass.HasOperationBeenCancelled)
                        { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                    }
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_get_nothingtoinsert + sCollName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }
                JobParameters.IsMultiThreadedImportToBDDFinished = true;
            }
            else
            {
                Exception ex = new(Languages.Languages.ns_get_norowstoinsert);
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, dtData.TableName, SQLTools_Enums.LOG_TYPEINFO.WNG);
            }
        }

        public DataSet ExecuteCommand(string sCommand)
        {
            DataSet dsResult = new();

            try
            {
                BsonDocument bsResult = null;
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                //{ dropDatabase: 1 }
                //{ drop: "sample2" }
                var command = new JsonCommand<BsonDocument>(sCommand);
                bsResult = dbMongo.RunCommand(command);

                if (bsResult != null)
                {
                    if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                    {
                        dsResult = DataJson.jsonToDataSet(bsResult.ToString());
                    }
                    else
                    {
                        JsonParser jsP = new(bsResult.ToString(), Languages.Languages.ns_prepostcommand);
                        jsP.OnJsonEvent += Event_JsonParser;
                        dsResult = jsP.JsonToDataSetAsync(Monitoring.TaskCancellationToken).Result;
                    }
                    //sResult = SQLTools.BuildStringFromDs(dsResult);
                    string sFullReturn = "Nothing";
                    if (dsResult.Tables.Count > 0 && dsResult.Tables[0].Rows.Count > 0)
                    {
                        sFullReturn = "col(s):" + dsResult.Tables[0].Columns.Count.ToString() + " - row(s):" + dsResult.Tables[0].Rows.Count.ToString();
                        //sFullReturn = "";
                        //foreach (DataColumn dc in dsResult.Tables[0].Columns)
                        //{ sFullReturn = string.Concat(sFullReturn, dc.ColumnName, ":", dsResult.Tables[0].Rows[0][dc.ColumnName].ToString(), " - "); }
                    }
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_prepostcommand_result + sFullReturn, SQLTools_Enums.LOG_TYPEINFO.INF);
                }
            }
            catch (Exception ex)
            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR); }

            return dsResult;
        }
        #endregion

        #region "PRIVATE VOID"

        internal void InsertRows(DataTable dtData, string sCollName, int iStart, int iStop, Query FuzibleQuery)
        {
            if (DatabaseName.Length > 0)
            {
                try
                {
                    MongoClient clMongo = Connect();

                    string sDatabase = DatabaseName;
                    if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                    IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                    switch (DocumentFormat)
                    {
                        case SQLTools_Enums.MONGODB_DOCUMENT.FUZIBLEBSON:
                            IMongoCollection<FuzibleBSonDocument> icMongoFUZ = dbMongo.GetCollection<FuzibleBSonDocument>(sCollName);
                            List<FuzibleBSonDocument> bsonToInsertFUZ = new();
                            for (int iR = iStart; iR <= iStop; iR++)
                            {
                                var dictionaryFUZIBLE = dtData.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => dtData.Rows[iR][col.ColumnName]);
                                FuzibleBSonDocument fuzibleDoc = new()
                                {
                                    LastUpdateUtc = DateTime.Now.ToString(),
                                    Identifier = "Fuzible",
                                    Data = dictionaryFUZIBLE
                                };
                                bsonToInsertFUZ.Add(fuzibleDoc);
                            }
                            icMongoFUZ.InsertMany(bsonToInsertFUZ.AsEnumerable());
                            break;
                        case SQLTools_Enums.MONGODB_DOCUMENT.STANDARDBSON:
                            IMongoCollection<BsonDocument> icMongoBSON = dbMongo.GetCollection<BsonDocument>(sCollName);
                            List<BsonDocument> bsonToInsertBSON = new();
                            for (int iR = iStart; iR <= iStop; iR++)
                            {
                                var dictionaryBSON = dtData.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => dtData.Rows[iR][col.ColumnName]);
                                BsonDocument bsonDoc = new(dictionaryBSON);
                                bsonToInsertBSON.Add(bsonDoc);
                            }
                            icMongoBSON.InsertMany(bsonToInsertBSON.AsEnumerable());
                            break;
                        case SQLTools_Enums.MONGODB_DOCUMENT.STRINGBSON:
                            IMongoCollection<string> icMongoSTR = dbMongo.GetCollection<string>(sCollName);
                            List<string> bsonToInsertSTR = new();
                            for (int iR = iStart; iR <= iStop; iR++)
                            {
                                string sData = string.Join(",", dtData.Rows[iR].ItemArray);
                                bsonToInsertSTR.Add(sData);
                            }
                            icMongoSTR.InsertMany(bsonToInsertSTR.AsEnumerable());
                            break;
                    }
                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
        }

        private ProjectionDefinition<BsonDocument> GetProjectionStandardBson(Query FuzibleQuery, string sTable)
        {
            List<string> sFieldsToAdd = new();
            if (FuzibleQuery.QueryAnalyzer.Fields.Count > 1 && !FuzibleQuery.QueryAnalyzer.Fields[0].Equals("*"))
            {
                foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                {

                    if (qF.Table.Equals(sTable))
                    { sFieldsToAdd.Add(qF.Name); }
                }
            }

            if (JobParameters.RemoveIDFromMongoDBQuery)
            { return Builders<BsonDocument>.Projection.Combine(sFieldsToAdd.Select(x => Builders<BsonDocument>.Projection.Include(x)).ToList()).Exclude(MONGO_INTERNAL_INDEX); }
            else
            { return Builders<BsonDocument>.Projection.Combine(sFieldsToAdd.Select(x => Builders<BsonDocument>.Projection.Include(x)).ToList()); }
        }

        private ProjectionDefinition<FuzibleBSonDocument> GetProjectionFuzibleBson(Query FuzibleQuery, string sTable)
        {
            List<string> sFieldsToAdd = new();
            if (FuzibleQuery.QueryAnalyzer.Fields.Count > 1 && !FuzibleQuery.QueryAnalyzer.Fields[0].Equals("*"))
            {
                foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                {

                    if (qF.Table.Equals(sTable))
                    { sFieldsToAdd.Add(qF.Name); }
                }
            }

            if (JobParameters.RemoveIDFromMongoDBQuery)
            { return Builders<FuzibleBSonDocument>.Projection.Combine(sFieldsToAdd.Select(x => Builders<FuzibleBSonDocument>.Projection.Include(x)).ToList()).Exclude(MONGO_INTERNAL_INDEX); }
            else
            { return Builders<FuzibleBSonDocument>.Projection.Combine(sFieldsToAdd.Select(x => Builders<FuzibleBSonDocument>.Projection.Include(x)).ToList()); }
        }

        private static FilterDefinition<BsonDocument> GetFilterStandardBson(Query FuzibleQuery, string sTable)
        {
            FilterDefinition<BsonDocument> bFilter = FilterDefinition<BsonDocument>.Empty;

            foreach (Query.QWhere qW in FuzibleQuery.QueryAnalyzer.Where)
            {
                if (qW.TableField.Equals(sTable) || qW.TableWhereCompare.Equals(sTable) || FuzibleQuery.QueryAnalyzer.Tables.Count == 1)
                {
                    string sCompare = qW.WhereCompare;
                    int iCompare = 0;
                    decimal dCompare = 0;
                    DateTime dtCompare = DateTime.Now;
                    int iType = 0;

                    if (int.TryParse(qW.WhereCompare, out int i)) { iCompare = Convert.ToInt32(qW.WhereCompare); iType = 1; }
                    else if (decimal.TryParse(qW.WhereCompare, out decimal d)) { dCompare = Convert.ToDecimal(qW.WhereCompare); iType = 2; }
                    else if (DateTime.TryParse(qW.WhereCompare, out DateTime dt)) { dtCompare = Convert.ToDateTime(qW.WhereCompare); iType = 3; }

                    dynamic sWCompare = null;
                    switch (iType)
                    {
                        case 0:
                            sWCompare = sCompare;
                            break;
                        case 1:
                            sWCompare = iCompare;
                            break;
                        case 2:
                            sWCompare = dCompare;
                            break;
                        case 3:
                            sWCompare = dtCompare;
                            break;
                    }
                    //pourquoi ça marche pas ???
                    sWCompare = sCompare;

                    switch (qW.WhereSign.ToUpper())
                    {
                        case "<":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Lt(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Lt(qW.RawWhereField, sWCompare);
                            break;
                        case ">":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Gt(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Gt(qW.RawWhereField, sWCompare);
                            break;
                        case ">=":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Gte(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Gte(qW.RawWhereField, sWCompare);
                            break;
                        case "<=":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Lte(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Lte(qW.RawWhereField, sWCompare);
                            break;
                        case "!=":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Ne(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Ne(qW.RawWhereField, sWCompare);
                            break;
                        case "<>":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Ne(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Ne(qW.RawWhereField, sWCompare);
                            break;
                        case "=":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Eq(qW.RawWhereField, sWCompare) : bFilter & Builders<BsonDocument>.Filter.Eq(qW.RawWhereField, sWCompare);
                            break;
                        case "LIKE":
                            //bFilter = Builders<BsonDocument>.Filter.ElemMatch(qW.FieldAlias, sWCompare);
                            break;
                        case "NOT LIKE":
                            //bFilter = Builders<BsonDocument>.Filter.Gt(qW.FieldAlias, sWCompare);
                            break;
                        case "IN":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.In(qW.RawWhereField, qW.WhereCompare) : bFilter & Builders<BsonDocument>.Filter.In(qW.RawWhereField, qW.WhereCompare);
                            break;
                        case "NOT IN":
                            bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Nin(qW.RawWhereField, qW.WhereCompare) : bFilter & Builders<BsonDocument>.Filter.Nin(qW.RawWhereField, qW.WhereCompare);
                            break;
                    }
                }
            }
            return bFilter;
        }

        private static FilterDefinition<FuzibleBSonDocument> GetFilterFuzibleBson(Query FuzibleQuery, string sTable)
        {
            FilterDefinition<FuzibleBSonDocument> bFilter = FilterDefinition<FuzibleBSonDocument>.Empty;

            foreach (Query.QWhere qW in FuzibleQuery.QueryAnalyzer.Where)
            {
                if (qW.TableField.Equals(sTable) || qW.TableWhereCompare.Equals(sTable) || FuzibleQuery.QueryAnalyzer.Tables.Count == 1)
                {
                    string sCompare = qW.WhereCompare;
                    int iCompare = 0;
                    decimal dCompare = 0;
                    DateTime dtCompare = DateTime.Now;
                    int iType = 0;

                    if (int.TryParse(qW.WhereCompare, out int i)) { iCompare = Convert.ToInt32(qW.WhereCompare); iType = 1; }
                    else if (decimal.TryParse(qW.WhereCompare, out decimal d)) { dCompare = Convert.ToDecimal(qW.WhereCompare); iType = 2; }
                    else if (DateTime.TryParse(qW.WhereCompare, out DateTime dt)) { dtCompare = Convert.ToDateTime(qW.WhereCompare); iType = 3; }

                    dynamic sWCompare = null;
                    switch (iType)
                    {
                        case 0:
                            sWCompare = sCompare;
                            break;
                        case 1:
                            sWCompare = iCompare;
                            break;
                        case 2:
                            sWCompare = dCompare;
                            break;
                        case 3:
                            sWCompare = dtCompare;
                            break;
                    }
                    sWCompare = sCompare;

                    switch (qW.WhereSign.ToUpper())
                    {
                        case "<":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Lt(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Lt(qW.RawWhereField, sWCompare);
                            break;
                        case ">":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Gt(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Gt(qW.RawWhereField, sWCompare);
                            break;
                        case ">=":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Gte(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Gte(qW.RawWhereField, sWCompare);
                            break;
                        case "<=":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Lte(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Lte(qW.RawWhereField, sWCompare);
                            break;
                        case "!=":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Ne(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Ne(qW.RawWhereField, sWCompare);
                            break;
                        case "<>":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Ne(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Ne(qW.RawWhereField, sWCompare);
                            break;
                        case "=":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Eq(qW.RawWhereField, sWCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Eq(qW.RawWhereField, sWCompare);
                            break;
                        case "LIKE":
                            //bFilter = Builders<FuzibleBSonDocument>.Filter.ElemMatch(qW.FieldAlias, sWCompare);
                            break;
                        case "NOT LIKE":
                            //bFilter = Builders<FuzibleBSonDocument>.Filter.Gt(qW.FieldAlias, sWCompare);
                            break;
                        case "IN":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.In(qW.RawWhereField, qW.WhereCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.In(qW.RawWhereField, qW.WhereCompare);
                            break;
                        case "NOT IN":
                            bFilter = bFilter == null ? Builders<FuzibleBSonDocument>.Filter.Nin(qW.RawWhereField, qW.WhereCompare) : bFilter & Builders<FuzibleBSonDocument>.Filter.Nin(qW.RawWhereField, qW.WhereCompare);
                            break;
                    }
                }
            }
            return bFilter;
        }

        private static SortDefinition<BsonDocument> GetSortingStandardBson(Query FuzibleQuery, string sTable)
        {
            SortDefinition<BsonDocument> bSort = null;
            foreach (Query.QGroupByOrderBy qOB in FuzibleQuery.QueryAnalyzer.OrderBy)
            {
                if (qOB.From.Equals(sTable) || FuzibleQuery.QueryAnalyzer.Tables.Count == 1)
                {
                    switch (qOB.Type.ToUpper())
                    {
                        case "ASC":
                            bSort = Builders<BsonDocument>.Sort.Ascending(qOB.Field);
                            break;
                        case "DESC":
                            bSort = Builders<BsonDocument>.Sort.Descending(qOB.Field);
                            break;
                    }
                }
            }
            return bSort;
        }

        private static SortDefinition<FuzibleBSonDocument> GetSortingFuzibleBson(Query FuzibleQuery, string sTable)
        {
            SortDefinition<FuzibleBSonDocument> bSort = null;
            foreach (Query.QGroupByOrderBy qOB in FuzibleQuery.QueryAnalyzer.OrderBy)
            {
                if (qOB.From.Equals(sTable) || FuzibleQuery.QueryAnalyzer.Tables.Count == 1)
                {
                    switch (qOB.Type.ToUpper())
                    {
                        case "ASC":
                            bSort = Builders<FuzibleBSonDocument>.Sort.Ascending(qOB.Field);
                            break;
                        case "DESC":
                            bSort = Builders<FuzibleBSonDocument>.Sort.Descending(qOB.Field);
                            break;
                    }
                }
            }
            return bSort;
        }

        internal static List<string> CheckConnection(CONNString CS, bool bWithDBList, int iTimeout, List<string> sListDynParams)
        {
            List<string> sDB = new();

            switch (CS.SConnDriver)
            {
                case SQLTools_Enums.BDD.NS_MONGODB:
                    try
                    {
                        MongoClient clTest = new(CS.SConnString(sListDynParams));
                        MongoClientSettings mcS = new() { Server = clTest.Settings.Server, Credential = clTest.Settings.Credential, ConnectTimeout = TimeSpan.FromSeconds(iTimeout) };
                        clTest = new MongoClient(mcS);
                        sDB.Add(Languages.Languages.ns_connectionok + clTest.Cluster.Description.State.ToString() + ")");

                        if (bWithDBList)
                        {
                            try
                            {
                                using IAsyncCursor<BsonDocument> cursor = clTest.ListDatabases();
                                while (cursor.MoveNext())
                                {
                                    foreach (BsonDocument doc in cursor.Current)
                                    { sDB.Add(doc["name"].ToString()); }
                                }
                            }
                            catch (Exception ex)
                            { sDB.Add(Languages.Languages.ns_connectionokcantgetdb + ex.Message); }
                        }
                    }
                    catch (Exception ex)
                    { sDB.Add(Languages.Languages.ns_connectstatus + ex.Message); }
                    break;
            }
            return sDB;
        }

        public List<string> GetAllCollectionsFromDatabase(string sDatabase, string sFilter, CancellationToken ctsToken)
        {
            List<string> sListColl = new();

            if (sDatabase.Length > 0)
            {
                MongoClient clMongo = Connect();
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                foreach (BsonDocument col in dbMongo.ListCollectionsAsync(null, ctsToken).Result.ToListAsync<BsonDocument>().Result)
                {
                    if (sFilter.Length > 0)
                    { if (col["name"].ToString().Contains(sFilter, StringComparison.OrdinalIgnoreCase)) { sListColl.Add(col["name"].ToString()); } }
                    else { sListColl.Add(col["name"].ToString()); }
                }
            }

            return sListColl;
        }

        public List<SQLColumn> GetFieldsFromCollection(string sTable)
        {
            List<SQLColumn> sListFields = new();

            if (DatabaseName.Length > 0)
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                IMongoCollection<BsonDocument> collection = dbMongo.GetCollection<BsonDocument>(sTable);
                BsonDocument document = collection.Find(new BsonDocument()).FirstOrDefault();

                DataSet dsData = null;
                if (JobParameters.GlobalParameters.JSON_ALTERNATIVE_PROCESSING_MODE)
                {
                    dsData = DataJson.jsonToDataSet(document.ToString());
                }
                else
                {
                    JsonParser jsP = new(document.ToString(), "data")
                    { IsBson = true };
                    jsP.OnJsonEvent += Event_JsonParser;
                    dsData = jsP.JsonToDataSetAsync(Monitoring.TaskCancellationToken).Result;
                }

                int i = 0;
                foreach (DataColumn sCol in dsData.Tables[0].Columns)
                {
                    i++;
                    sListFields.Add(new SQLColumn(i, sCol.ColumnName, (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sCol.DataType.ToString().ToUpper()), sCol.DataType, sCol.MaxLength.ToString(), sCol.AllowDBNull, sCol.DefaultValue.ToString(), false, sCol.Unique));
                }

            }

            return sListFields;
        }

        internal DataSet CompareDsWithTargetDB(Query FuzibleQuery, string sCollection, bool bAvoidPerform, DataTable dtSource)
        {
            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                DataSet dsTarget = new();

                List<SQLColumn> sListFieldsPK = new();

                DataSet dsCompare = new();

                //on vérifie qu'il y a des données dans la collection cible, sinon, aucun intérêt !
                bool bCollTargetExists = CollectionExists(dbMongo, sCollection);

                //si le fichier cible n'existe pas on la crée complètement avec les données
                if (!bCollTargetExists)
                { InsertDataToMongoDB(dtSource, sCollection, FuzibleQuery); }

                //récupération des données dans la cible
                //construction de la requête cible
                FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetColumns = Toolbox.GetSQLColumnsFromDataTable(dtSource);

                string sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.GetTargetQuery(false, JobParameters.SynchroBypassQueryFiltersInTarget, false);
                sTargetQuery = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.AddOptionalFilters(sTargetQuery, JobParameters, FuzibleQuery, dtSource);

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.sql_synchro_loadsourcedata_query + sTargetQuery, SQLTools_Enums.LOG_TYPEINFO.DET);

                Query sQ = new(JobParameters, sTargetQuery)
                { ConnectionSrc = JobParameters.ConnectionString_Target };

                dsTarget = GetDataFromMongoDB(ref sQ);

                FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.RealSynchroTargetColumns = Toolbox.GetSQLColumnsFromDataTable(dsTarget.Tables[0]);

                //-----------------------------------------------

                SHSOperations SHSX = new(JobParameters, FuzibleQuery, ref MyLog); //on recherche les champs NUL/NOT NULL car la recherche de PK requiert des champs NOT NULL
                SHSX.SetDBNullInDataSet(dsTarget.Tables[0]);

                DataTable dtTarget = new();
                if (dsTarget.Tables.Count > 0)
                {
                    dtTarget = dsTarget.Tables[0].Copy();
                    dsTarget.Clear();
                }

                ///////////////////////////////////////////////////////////////////////////////////////////////////////
                //METHODE 1 : on essaye de trouver une clé primaire dans la table cible (ne marche que si elle existe !)
                string sMessageKey = "";
                if (sListFieldsPK.Count == 0)
                {
                    sMessageKey = Languages.Languages.ns_synchro_pkfromsource + dtSource.TableName;
                    DataColumn[] dtCPK = dtSource.PrimaryKey;
                    if (dtCPK.Length != 0)
                    {
                        foreach (DataColumn dtC in dtCPK)
                        { sListFieldsPK.Add(SQLColumn.SQLColumnFromDataColumn(dtC, SQLTools_Enums.TYPE_DATA.VARCHAR)); }
                    }
                }

                //METHODE 2 : on cherche une clé unique sur la collection cible
                if (sListFieldsPK.Count == 0)
                {
                    sMessageKey = Languages.Languages.ns_synchro_pkfromtarget + dtSource.TableName;
                    sListFieldsPK = GetCollectionIndex(sCollection);
                }

                //METHODE 2=3 : toujours rien, on va tâcher de deviner
                if (sListFieldsPK.Count == 0)
                {
                    //METHODE 3A : méthode heuristique basée sur source (cible vide)
                    if (!bCollTargetExists)
                    {
                        sMessageKey = Languages.Languages.ns_synchro_pkfromsource_shs + dtSource.TableName + ")";
                        SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                        sListFieldsPK = SHS.GuessPKFromQuery(dtSource, SQLTools_Enums.TYPE_DATA.VARCHAR, null);
                    }
                    //METHODE 3B : méthode heuristique basée sur cible
                    else
                    {
                        bool bID = JobParameters.RemoveIDFromMongoDBQuery;
                        JobParameters.RemoveIDFromMongoDBQuery = true;

                        sMessageKey = Languages.Languages.ns_synchro_pkfromtarget_shs + sCollection + ")";
                        Query sQPK = new(JobParameters, "PK:SELECT * FROM " + sCollection)
                        { ConnectionSrc = JobParameters.ConnectionString_Target };
                        DataSet dsTargetFull = GetDataFromMongoDB(ref sQ);
                        SHSOperations SHS = new(JobParameters, sQPK, ref MyLog);
                        sListFieldsPK = SHS.GuessPKFromQuery(dsTargetFull.Tables[0], SQLTools_Enums.TYPE_DATA.VARCHAR, null);

                        JobParameters.RemoveIDFromMongoDBQuery = bID;
                    }
                    if (sListFieldsPK.Count > 0)
                    {
                        //si clé découverte, on appose une clé unique sur la collection
                        if (JobParameters.CreatePKForMongoCollection)
                        { SetCollectionUniqueKey(sCollection, sListFieldsPK); }
                    }
                }


                if (sListFieldsPK.Count == 0)
                {
                    Exception ex = new(Languages.Languages.ns_synchro_errorsintargetquery);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
                ///////////////////////////////////////////////////////////////////////////////////////////////////////

                //Inutile d'aller plus loin sans clé primaire
                if (sListFieldsPK.Count > 0)
                {
                    FuzibleQuery.QueryAnalyzer.AddQueryProperty(sCollection, SQLTools_Enums.QUERY_PROPERTIES.SYNCHRO_PRIMARY_KEY, string.Join(",", sListFieldsPK.Select(pk => pk.ColumnName)), true);

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, sMessageKey, SQLTools_Enums.LOG_TYPEINFO.INF);

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
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_rowssource + dtSource.TableName + ") : " + dtSource.Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_rowstarget + sCollection + ") : " + dtTarget.Rows.Count.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

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

                        if (!bAvoidPerform)
                        {
                            List<string> sPKList = sListFieldsPK.ConvertAll(x => x.ColumnName);

                            if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("I") > -1 && iInserted > 0)
                            {
                                int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_rowstoinsert + sCollection + ") : " + iInserted.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);
                                InsertDataToMongoDB(dsCompare.Tables[0], sCollection, FuzibleQuery);

                                int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                if (iErrWarnB > iErrWarnA)
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + iInserted.ToString() + @" !", false);
                                }
                                else
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, iInserted.ToString(), false);
                                }   
                            }
                            else { iInserted = 0; }

                            if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("U") > -1 && iUpdated > 0)
                            {
                                int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_rowstoupdate + sCollection + ") : " + iUpdated.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                                if (JobParameters.SynchroStoreChanges)
                                { SynchroModeStoreUpdateDeleteChanges("D", dsCompare.Tables[1], sCollection, sListFieldsPK, FuzibleQuery); }

                                UpdateDataInMongoDB(dsCompare.Tables[1], sPKList, sCollection, FuzibleQuery);

                                int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                if (iErrWarnB > iErrWarnA)
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, @"! " + iUpdated.ToString() + @" !", false);
                                }
                                else
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, iUpdated.ToString(), false);
                                }
                            }
                            else { iUpdated = 0; }

                            //pas de merge possible en suppression de lignes... Méthode manuelle
                            if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("D") > -1 && iDeleted > 0)
                            {
                                int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_rowstodelete + sCollection + ") : " + iDeleted.ToString(), SQLTools_Enums.LOG_TYPEINFO.INF);

                                if (JobParameters.SynchroStoreChanges)
                                { SynchroModeStoreUpdateDeleteChanges("D", dsCompare.Tables[1], sCollection, sListFieldsPK, FuzibleQuery); }

                                DeleteDataInMongoDB(dsCompare.Tables[2], sPKList, sCollection, FuzibleQuery);


                                int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                if (iErrWarnB > iErrWarnA)
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, @"! " + iDeleted.ToString() + @" !", false);
                                }
                                else
                                {
                                    FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, iDeleted.ToString(), false);
                                }
                                
                            }
                            else { iDeleted = 0; }

                            MyLog.AddJobReportRow(sCollection, dtSource.Rows.Count, dtTarget.Rows.Count, iInserted, iUpdated, iDeleted);
                        }
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.ns_synchro_errorsintargetquery02 + sCollection + Languages.Languages.ns_synchro_errorsintargetquery03);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_synchro_noprimarykey01 + sCollection + Languages.Languages.ns_synchro_errorsintargetquery03, SQLTools_Enums.LOG_TYPEINFO.WNG);
                }

                dtTarget.TableName = sCollection;
                dsCompare.Tables.Add(dtTarget.Copy());
                dtTarget.Clear();
                dtTarget = null;

                return dsCompare;
            }
            catch (OperationCanceledException)
            {
                throw;
            }

        }

        private void DeleteDataInMongoDB(DataTable dtToDelete, List<string> sListPK, string sCollName, Query FuzibleQuery)
        {
            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                if (CollectionExists(dbMongo, sCollName))
                {
                    IMongoCollection<BsonDocument> icMongo = dbMongo.GetCollection<BsonDocument>(sCollName);
                    foreach (DataRow drDel in dtToDelete.Rows)
                    {
                        try
                        {
                            FilterDefinition<BsonDocument> bFilter = null;
                            foreach (string sPK in sListPK)
                            { bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Eq(sPK, drDel[sPK]) : bFilter & Builders<BsonDocument>.Filter.Eq(sPK, drDel[sPK]); }
                            DeleteResult result = icMongo.DeleteOne(bFilter);
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_synchro_unabletodeleterow + string.Join(",", drDel.ItemArray) + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_synchro_unabletodeletedata + sCollName + ")", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
        }

        private void UpdateDataInMongoDB(DataTable dtToUpdate, List<string> sListPK, string sCollName, Query FuzibleQuery)
        {
            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                if (CollectionExists(dbMongo, sCollName))
                {
                    IMongoCollection<BsonDocument> icMongo = dbMongo.GetCollection<BsonDocument>(sCollName);
                    foreach (DataRow drUpd in dtToUpdate.Rows)
                    {
                        try
                        {
                            FilterDefinition<BsonDocument> bFilter = null;
                            foreach (string sPK in sListPK)
                            { bFilter = bFilter == null ? Builders<BsonDocument>.Filter.Eq(sPK, drUpd[sPK]) : bFilter & Builders<BsonDocument>.Filter.Eq(sPK, drUpd[sPK]); }

                            UpdateDefinition<BsonDocument> bUpdate = null;
                            foreach (DataColumn dc in drUpd.Table.Columns)
                            {
                                if (!sListPK.Contains(dc.ColumnName))
                                { bUpdate = bUpdate == null ? Builders<BsonDocument>.Update.Set(dc.ColumnName, drUpd[dc.ColumnName]) : bUpdate.Set(dc.ColumnName, drUpd[dc.ColumnName]); }
                            }

                            UpdateResult result = icMongo.UpdateOne(bFilter, bUpdate);
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_synchro_unabletoupdaterow + string.Join(",", drUpd.ItemArray) + ")", SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_synchro_unabletoupdatedata + sCollName + ")", FuzibleQuery.RetryErrorOrWarning);
                FuzibleQuery.QueryErrors += 1;
            }
        }

        private List<SQLColumn> GetCollectionIndex(string sCollection)
        {
            List<SQLColumn> sqlColList = new();

            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                IMongoCollection<BsonDocument> collection = dbMongo.GetCollection<BsonDocument>(sCollection);
                var iIndexes = collection.Indexes.List().ToList();
                foreach (BsonDocument index in iIndexes)
                {
                    string sName = index.GetElement("name").Value.ToString();
                    if (sName.StartsWith(JobParameters.GlobalParameters.MONGODB_INDEXNAME))
                    {
                        try
                        {
                            //{ "id_sample" : 1 }
                            string sK = index.GetElement("key").Value.ToString();
                            JsonParser jsP = new(sK, "PK");
                            jsP.OnJsonEvent += Event_JsonParser;
                            DataSet dsPK = jsP.JsonToDataSetAsync(Monitoring.TaskCancellationToken).Result;
                            int iI = 0;
                            if (dsPK.Tables.Count > 0)
                            {
                                foreach (DataColumn dC in dsPK.Tables[0].Columns)
                                {
                                    string sCol = dC.ColumnName;
                                    sqlColList.Add(new SQLColumn(iI, sCol, SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", false, "", false, false));
                                    iI++;
                                }
                            }
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_searchpk_cantgetindex01 + sCollection + "' - Index : " + sName, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_searchpk_cantgetindex02 + sCollection + Languages.Languages.ns_searchpk_cantgetindex03 + JobParameters.GlobalParameters.MONGODB_INDEXNAME + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
            }
            return sqlColList;
        }

        private void SetCollectionUniqueKey(string sCollection, List<SQLColumn> sListFieldsPK)
        {
            string sIndex = string.Concat(JobParameters.GlobalParameters.MONGODB_INDEXNAME, DateTime.Now.ToString("yyyyMMdd"));
            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                IMongoCollection<BsonDocument> collection = dbMongo.GetCollection<BsonDocument>(sCollection);
                IndexKeysDefinitionBuilder<BsonDocument> bIndex = Builders<BsonDocument>.IndexKeys;

                CreateIndexModel<BsonDocument> indexModel = null;
                var idXOptions = new CreateIndexOptions
                { Name = sIndex };

                switch (sListFieldsPK.Count)
                {
                    case 1:
                        indexModel = new CreateIndexModel<BsonDocument>(bIndex.Ascending(sListFieldsPK[0].ColumnName), idXOptions);
                        break;
                    case 2:
                        indexModel = new CreateIndexModel<BsonDocument>(bIndex.Ascending(sListFieldsPK[0].ColumnName).Ascending(sListFieldsPK[1].ColumnName), idXOptions);
                        break;
                    case 3:
                        indexModel = new CreateIndexModel<BsonDocument>(bIndex.Ascending(sListFieldsPK[0].ColumnName).Ascending(sListFieldsPK[1].ColumnName).Ascending(sListFieldsPK[2].ColumnName), idXOptions);
                        break;
                    case 4:
                        indexModel = new CreateIndexModel<BsonDocument>(bIndex.Ascending(sListFieldsPK[0].ColumnName).Ascending(sListFieldsPK[1].ColumnName).Ascending(sListFieldsPK[2].ColumnName).Ascending(sListFieldsPK[3].ColumnName), idXOptions);
                        break;
                    case 5:
                        indexModel = new CreateIndexModel<BsonDocument>(bIndex.Ascending(sListFieldsPK[0].ColumnName).Ascending(sListFieldsPK[1].ColumnName).Ascending(sListFieldsPK[2].ColumnName).Ascending(sListFieldsPK[3].ColumnName).Ascending(sListFieldsPK[4].ColumnName), idXOptions);
                        break;
                }
                collection.Indexes.CreateOne(indexModel);
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ns_searchpk_cantcreateindex + sCollection + ",name:" + sIndex + ",index:" + sListFieldsPK.ConvertAll(x => x.ColumnName) + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
            }
        }

        private void SynchroModeStoreUpdateDeleteChanges(string sUorD, DataTable dtData, string sCollBackup, List<SQLColumn> sListFieldsWhere, Query sQ)
        {
            sCollBackup = string.Concat(sCollBackup, "_back");

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ns_synchrostore01, sUorD, " - ", dtData.Rows.Count.ToString(), Languages.Languages.ns_synchrostore02, sCollBackup), SQLTools_Enums.LOG_TYPEINFO.INF);

            //ajout colonne ID ligne dans dataset
            string sRecordIDCol = JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN;
            string sRecordTagCol = JobParameters.GlobalParameters.SQL_SYNCHRO_TAG_COLUMN;
            long sRecordIDValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            dtData.Columns.Add(sRecordIDCol, Type.GetType("System.Int64"));
            dtData.Columns.Add(sRecordTagCol, Type.GetType("System.String"));
            foreach (DataRow dt in dtData.Rows)
            { dt[sRecordIDCol] = sRecordIDValue; dt[sRecordTagCol] = sUorD; }
            sListFieldsWhere.Add(new SQLColumn(dtData.Columns[sRecordIDCol].Ordinal, sRecordIDCol, SQLTools_Enums.TYPE_DATA.BIGINT, Type.GetType("System.Int64"), "0", false, "", false, false));

            SQLTools_Enums.TARGET_TABLE_METHOD ttm = JobParameters.TargetTableBehavior;
            JobParameters.TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
            InsertDataToMongoDB(dtData, sCollBackup, sQ);
            JobParameters.TargetTableBehavior = ttm;
        }

        internal bool DropCollection(string sCollName, Query FuzibleQuery)
        {
            bool bOK = false;
            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                if (CollectionExists(dbMongo, sCollName))
                {
                    IMongoCollection<BsonDocument> icMongo = dbMongo.GetCollection<BsonDocument>(sCollName);
                    dbMongo.DropCollection(sCollName);
                    bool bExists = CollectionExists(dbMongo, sCollName);
                    if (!bExists)
                    {
                        bOK = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "Collection '" + sCollName + Languages.Languages.ns_dropcollection_ok, SQLTools_Enums.LOG_TYPEINFO.INF);
                    }
                    else
                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "Collection '" + sCollName + Languages.Languages.ns_dropcollection_ko, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
            }
            catch (Exception ex)
            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }

            return bOK;
        }

        internal long DeleteDataFromMongoDB(Query FuzibleQuery, string sCollName)
        {
            long iCount = 0;

            try
            {
                MongoClient clMongo = Connect();

                string sDatabase = DatabaseName;
                if (sDatabase.Length == 0) { List<string> sListDB = ListDBOnServer(); sDatabase = sListDB.Count > 0 ? sListDB[0] : ""; }
                IMongoDatabase dbMongo = clMongo.GetDatabase(sDatabase);

                if (CollectionExists(dbMongo, sCollName))
                {
                    IMongoCollection<BsonDocument> icMongo = dbMongo.GetCollection<BsonDocument>(sCollName);

                    switch (JobParameters.TargetMongoCollectionBehavior)
                    {
                        case SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE:
                            FilterDefinition<BsonDocument> bFilter = GetFilterStandardBson(FuzibleQuery, sCollName);
                            DeleteResult result = icMongo.DeleteMany(bFilter);
                            iCount = Convert.ToInt64(result.DeletedCount);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_partialdelete01 + iCount.ToString() + Languages.Languages.ns_partialdelete02 + sCollName + "'.", SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM:
                            FilterDefinition<BsonDocument> bFilterB = GetFilterStandardBson(FuzibleQuery, sCollName);
                            DeleteResult resultB = icMongo.DeleteMany(bFilterB);
                            iCount = Convert.ToInt64(resultB.DeletedCount);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_partialdelete01 + iCount.ToString() + Languages.Languages.ns_partialdelete02 + sCollName + "'.", SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                        case SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME:
                            FilterDefinition<BsonDocument> bFilterC = GetFilterStandardBson(FuzibleQuery, sCollName);
                            DeleteResult resultC = icMongo.DeleteMany(bFilterC);
                            iCount = Convert.ToInt64(resultC.DeletedCount);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ns_partialdelete01 + iCount.ToString() + Languages.Languages.ns_partialdelete02 + sCollName + "'.", SQLTools_Enums.LOG_TYPEINFO.INF);
                            break;
                    }
                }
            }
            catch (Exception ex)
            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            return iCount;
        }

        private static bool CollectionExists(IMongoDatabase iMongoDB, string sColName)
        {
            var filter = new BsonDocument("name", sColName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            return iMongoDB.ListCollectionNames(options).Any();
        }

        private List<string> ListDBOnServer()
        {
            List<string> sListDB = new();

            try
            {
                MongoClient clMongo = Connect();
                using IAsyncCursor<BsonDocument> cursor = clMongo.ListDatabases();
                while (cursor.MoveNext())
                {
                    foreach (BsonDocument doc in cursor.Current)
                    { sListDB.Add(doc["name"].ToString()); }
                }
            }
            catch (Exception ex)
            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);  }

            return sListDB;
        }

        private MongoClient Connect()
        {
            MongoClient clMongo = new(Connection.SConnString(JobParameters.DynParams));
            MongoClientSettings mcS = new() { Server = clMongo.Settings.Server, Credential = clMongo.Settings.Credential, ConnectTimeout = TimeSpan.FromSeconds(TIMEOUT) };
            clMongo = new MongoClient(mcS);
            return clMongo;
        }
        #endregion
    }

    internal class FuzibleBSonDocument
    {
        public string Identifier { get; set; }
        public string LastUpdateUtc { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}
