using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static FuzibleFramework.Query;
using static FuzibleFramework.SQLTools_Enums;

namespace FuzibleFramework
{

    public class MThread
    {

        #region "VARIABLES"

        private readonly SQLTools_Enums.CLASS_PURPOSE ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.PRG;
        private Job JobParameters;
        private LogTools MyLog;

        public static readonly List<bool> _hasQueryData = new();

        private readonly List<bool> _isMThreadFinished = new();
        private readonly List<int> _iMTStart = new();
        private readonly List<int> _iMTQuantity = new();
        private readonly List<int> _iMTStop = new();

        private readonly List<DataTable> _dtJoin2Dt = new();
        private readonly List<DataTable> _dtFillCSV = new();

        private readonly List<DataTable> _dtListData = new();
        private readonly List<string> _sListTablesNames = new();
        private readonly List<string> _sListEntetes = new();
        private readonly List<List<SQLColumn>> _sListFieldsWhere = new();

        private readonly List<List<SQLColumn>> _sListTypesFieldsTarget = new();
        private readonly List<List<string>> _sListFieldsToKeep = new();
        private readonly List<DataRowCollection> _drListDataRows = new();

        private readonly List<DataTable> _dtRowsUpdate = new();
        private readonly List<DataTable> _dtRowsInsert = new();
        private readonly List<DataTable> _dtRowsDelete = new();
        private readonly List<DataTable> _dtRowsUpdateOldValues = new();
        private readonly List<List<string>> _sListColumnsDiff = new();

        private DataTable _dtSourceData = new();
        private DataTable _dtCibleData = new();
        private readonly List<List<SQLColumn>> _sFieldsPrimaryKey = new();
        private List<SQLColumn> _sPrimaryKeyForSynchro = new();
        private List<Query> _modifiedQueries = new();

        private readonly List<List<SQLColumn>> _sListFinalFieldsTypes = new();

        private List<DataTable> _dtNewBulkColumns = new();

        private List<bool> _operationCancelled = new();

        #endregion

        #region "PROPRIETES"

        public List<Query> GetFinalQueries { get { return _modifiedQueries; } }
        public List<List<SQLColumn>> GetPrimaryKeyFields
        {
            get
            {
                return _sFieldsPrimaryKey;
            }
        }
        public List<bool> IsHeaderRowUniqueInFile
        {
            get; private set;
        }
        public DataTable GetFillDatatableFromCSV
        {
            get
            {
                if (_dtFillCSV.Count > 0)
                {
                    if (_dtFillCSV.Count == 1)
                    {
                        return _dtFillCSV[0];
                    }
                    else
                    {
                        for (int cpt = 1; cpt < QuantityOfThreadsToCompute; cpt++)
                        {
                            foreach (DataRow dr in _dtFillCSV[1].Rows) { _dtFillCSV[0].ImportRow(dr); }
                            //_dtFillCSV[0].Merge(_dtFillCSV[1]); 
                            _dtFillCSV.RemoveAt(1);
                        }
                        return _dtFillCSV[0];
                    }
                }
                else { return null; }
            }
        }
        public DataTable GetCrossJoinDatatable
        {
            get
            {
                for (int cpt = 1; cpt < QuantityOfThreadsToCompute; cpt++) //subtilité : il y a un + 1 sur la quantité de threads car il y en a un réservé pour le DELETE et les autres sont pour UPDATE/INSERT
                {
                    foreach (DataRow dr in _dtJoin2Dt[cpt].Rows) { _dtJoin2Dt[0].ImportRow(dr); }
                }

                return _dtJoin2Dt[0];
            }
        }
        public List<SQLColumn> GetFieldsFromDataset_OrFile_Result
        {
            get
            {
                List<SQLColumn> sListe = new();
                for (int cpt = 0; cpt <= _sListFinalFieldsTypes.Count - 1; cpt += 1)
                {
                    sListe.AddRange(_sListFinalFieldsTypes[cpt]);
                }
                return sListe;
            }
        }
        public DataSet GetDataset_CompareDataTables
        {
            get
            {
                DataSet dsData = new();
                for (int cpt = 1; cpt < QuantityOfThreadsToCompute; cpt++) //subtilité : il y a un + 1 sur la quantité de threads car il y en a un réservé pour le DELETE et les autres sont pour UPDATE/INSERT
                {
                    foreach (DataRow dr in _dtRowsInsert[cpt].Rows) { _dtRowsInsert[0].ImportRow(dr); }
                    foreach (DataRow dr in _dtRowsUpdate[cpt].Rows) { _dtRowsUpdate[0].ImportRow(dr); }
                    foreach (DataRow dr in _dtRowsUpdateOldValues[cpt].Rows) { _dtRowsUpdateOldValues[0].ImportRow(dr); }
                    //_dtRowsInsert[0].Merge(_dtRowsInsert[cpt]);
                    //_dtRowsUpdate[0].Merge(_dtRowsUpdate[cpt]);
                }
                foreach (DataRow dr in _dtRowsDelete[QuantityOfThreadsToCompute].Rows) { _dtRowsDelete[0].ImportRow(dr); }

                dsData.Tables.Add(_dtRowsInsert[0]);
                dsData.Tables.Add(_dtRowsUpdate[0]);
                dsData.Tables.Add(_dtRowsDelete[0]);
                dsData.Tables.Add(_dtRowsUpdateOldValues[0]);
                return dsData;
            }
        }
        public List<string> ListColumnsChanges_CompareDataTables
        {
            get
            {
                List<string> sListDef = new();
                List<(string, int)> sListCols = new();

                foreach (List<string> sThreadList in _sListColumnsDiff)
                {
                    foreach (string sCol in sThreadList)
                    {
                        if (!sListCols.Any(t => t.Item1.Equals(sCol)))
                        { sListCols.Add((sCol, 1)); }
                        else
                        {
                            var item = sListCols.First(c => c.Item1.Equals(sCol));
                            item.Item2 += 1;
                            sListCols.Remove(sListCols.First(c => c.Item1.Equals(sCol)));
                            sListCols.Add(item);
                        }
                    }
                }

                foreach (var c in sListCols)
                {
                    sListDef.Add(c.Item1 + " (" + c.Item2 + ")");
                }
                return sListDef;
            }
        }
        public bool AreAllThreadsFinished
        {
            get
            {
                bool bFinished = true;

                for (int cpt = 0; cpt <= _isMThreadFinished.Count - 1; cpt += 1)
                {
                    if (_isMThreadFinished[cpt] == false)
                    {
                        bFinished = false;
                        break;
                    }
                }

                return bFinished;
            }
        }

        public static bool DataHasBeenRetrieved { get { return _hasQueryData.Count > 0; } }

        public bool HasOperationBeenCancelled
        {
            get
            {
                bool bCancelled = false;

                for (int cpt = 0; cpt <= _operationCancelled.Count - 1; cpt += 1)
                {
                    if (_operationCancelled[cpt] == true)
                    {
                        bCancelled = true;
                        break;
                    }
                }

                return bCancelled;
            }
        }

        public int QuantityOfThreadsToCompute { get; private set; } = 0;

        #endregion

        #region "PUBLIC VOID"

        public MThread(Job INIP, SQLTools_Enums.CLASS_PURPOSE classPurpose, LogTools LogMT, int iMTQuantityToCompute = 8, int iForceQteThreads = 0)
        {
            if (iMTQuantityToCompute > 0) //pas envie que tout pète si rien à calculer
            {
                JobParameters = INIP.DeepCopy();
                MyLog = LogMT;
                ClassPurpose = classPurpose;

                int iQteThreadsWanted;

                if (classPurpose == SQLTools_Enums.CLASS_PURPOSE.SRC)
                {
                    iQteThreadsWanted = INIP.Threads_Source;
                }
                else { iQteThreadsWanted = INIP.Threads_Target; }

                if (iForceQteThreads > 0)
                {
                    iQteThreadsWanted = iForceQteThreads;
                }

                int iQteT = CalculateStartQuantityStop(iMTQuantityToCompute, iQteThreadsWanted);

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.mt_preparing, iQteT.ToString(), Languages.Languages.mt_threadsinvoked), SQLTools_Enums.LOG_TYPEINFO.DET);

                for (int iT = 0; iT < iQteT; iT++)
                {
                    _isMThreadFinished.Add(false);
                    _operationCancelled.Add(false);
                }
            }
            else { QuantityOfThreadsToCompute = -1; }
        }

        public void INIT_FillDatatableFromCSVArray(DataTable dtData)
        {
            _dtFillCSV.Add(dtData.Clone());
        }

        public void INIT_CrossJoinDataTables(int iQteCPU, DataTable dtPrimaryTable, DataTable dtSecondaryTable)
        {
            //subtilité : à cause de la boucle OUTER JOIN, j'ajoute des _iMTStart/_iMTStop pour le calcul du RIGHT JOIN (2ème passage boucle)
            CalculateStartQuantityStop(dtSecondaryTable.Rows.Count, iQteCPU);

            _dtJoin2Dt.Add(new DataTable(dtPrimaryTable.TableName + "_" + dtSecondaryTable.TableName));

            foreach (DataColumn col in dtPrimaryTable.Columns) //copie des colonnes de la table 1 sur la nouvelle
            {
                if (_dtJoin2Dt[^1].Columns[col.ColumnName] == null)
                {
                    _dtJoin2Dt[^1].Columns.Add(col.ColumnName, col.DataType);
                    _dtJoin2Dt[^1].Columns[col.ColumnName].Namespace = col.Namespace;
                }
            }

            foreach (DataColumn col in dtSecondaryTable.Columns) //copie des colonnes de la table 2 sur la nouvelle
            {
                if (_dtJoin2Dt[^1].Columns[col.ColumnName] == null)
                {
                    _dtJoin2Dt[^1].Columns.Add(col.ColumnName, col.DataType);
                    _dtJoin2Dt[^1].Columns[col.ColumnName].Namespace = col.Namespace;
                }
            }
        }

        public void INIT_BulkInsertBDDFromDt(DataTable dtData, string sNomTable, int iThread, List<SQLColumn> sListeTypeColonnesTarget, List<string> sListeColonnesToKeep)
        {
            if (QuantityOfThreadsToCompute == 1) //pas besoin de faire une copie de données si on a qu'un thread
            {
                _dtListData.Add(dtData);
            }
            else
            {
                DataTable dtThread = dtData.Clone();

                for (int cptRow = _iMTStart[iThread]; cptRow < _iMTStop[iThread]; cptRow += 1)
                {
                    var row = dtData.Rows[0];
                    dtThread.ImportRow(row);
                    dtData.Rows.Remove(row);
                }

                _dtListData.Add(dtThread);
                dtData.AcceptChanges();
            }

            _sListTablesNames.Add(sNomTable);
            _sListTypesFieldsTarget.Add(sListeTypeColonnesTarget);
            _sListFieldsToKeep.Add(sListeColonnesToKeep);
        }

        public void INIT_InsertBDDFromDt(DataTable dtData, string sNomTable, string sEntete, List<SQLColumn> sListeTypeColonnesTarget, List<string> sListeColonnesToKeep)
        {
            _dtListData.Add(dtData);
            _sListTablesNames.Add(sNomTable);
            _sListEntetes.Add(sEntete);
            _sListTypesFieldsTarget.Add(sListeTypeColonnesTarget);
            _sListFieldsToKeep.Add(sListeColonnesToKeep);

        }

        public void INIT_InsertNOSQLFromDt(DataTable dtData, string sNomTable)
        {
            _dtListData.Add(dtData);
            _sListTablesNames.Add(sNomTable);
        }

        public void INIT_DeleteBDDFromDt(DataTable dtData, string sNomTable, List<SQLColumn> sListFieldsWhere)
        {
            _dtListData.Add(dtData);
            _sListTablesNames.Add(sNomTable);
            _sListFieldsWhere.Add(sListFieldsWhere);
        }

        public void INIT_UpdateBDDFromDt(DataTable dtData, string sNomTable, List<SQLColumn> sListFieldsWhere, List<SQLColumn> sListeTypeColonnesTarget)
        {
            _dtListData.Add(dtData);
            _sListTablesNames.Add(sNomTable);
            _sListFieldsWhere.Add(sListFieldsWhere);
            _sListTypesFieldsTarget.Add(sListeTypeColonnesTarget);

        }

        public void INIT_AnalyzeDataFromDs(DataRowCollection dataRows)
        {
            _sListFinalFieldsTypes.Add(new List<SQLColumn>());
            _drListDataRows.Add(dataRows);
        }

        public void INIT_FindPrimaryKey()
        {
            _sFieldsPrimaryKey.Add(new List<SQLColumn>());
        }

        public void INIT_CompareDataTables(DataTable dtSource, DataTable dtCible, Query Q, List<SQLColumn> sFieldsPrimaryKey, SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR SynchroTargetBehavior)
        {
            if (_dtSourceData.Rows.Count == 0)
            {
                _dtSourceData = dtSource;
                _dtCibleData = dtCible;
                _sPrimaryKeyForSynchro = sFieldsPrimaryKey;
            }

            DataTable dtInsert = new()
            {
                TableName = "INSERT"
            };
            DataTable dtUpdate = new()
            {
                TableName = "UPDATE"
            };
            DataTable dtDelete = new()
            {
                TableName = "DELETE"
            };
            DataTable dtUpdateOldValues = new()
            {
                TableName = "UPDATE_OLDDATA"
            };

            _sListColumnsDiff.Add(new List<string>());
            _dtRowsInsert.Add(dtInsert);
            _dtRowsUpdate.Add(dtUpdate);
            _dtRowsDelete.Add(dtDelete);
            _dtRowsUpdateOldValues.Add(dtUpdateOldValues);

            Type dtTypeS;
            Type dtTypeT;
            //ajout des colonnes aux datatables
            for (int cpt = 0; cpt < dtCible.Columns.Count; cpt++)
            {
                dtTypeS = dtSource.Columns[cpt].DataType;
                dtTypeT = dtCible.Columns[cpt].DataType;

                if (dtTypeS != dtTypeT) //parfois, l'analyse intelligente des champs peut avoir provoqué des variations entre la cible et la source
                {
                    dtTypeT = System.Type.GetType("System.String");
                }

                //if (dtTypeS != dtTypeT) //parfois, l'analyse intelligente des champs peut avoir provoqué des variations entre la cible et la source
                //{
                //    if (dtTypeS.Name.IndexOf("INT", StringComparison.OrdinalIgnoreCase) == 0 && dtTypeT.Name.IndexOf("INT", StringComparison.OrdinalIgnoreCase) == 0)
                //    { } //on ne fait rien si les types sont tous les deux entiers (Int32 vs Int64 par exemple) 
                //    else //un petit shs dans le doute : on compare le type source/target pour être sûr de mettre le bon type lors du DELETE/INSERT/UPDATE qui suivra
                //    {
                //        SHSOperations SHS = new SHSOperations(JobParameters, Q, ref MyLog);
                //        List<SQLColumn> sColumns = SHS.GetListFieldsTypesFromDataset(dtSource, cpt, Q, SQLTools_Enums.CLASS_PURPOSE.SRC);
                //        if (dtTypeT != sColumns[0].ColumnLinqType)
                //        { dtTypeT = System.Type.GetType("System.String"); }
                //    }
                //}

                dtInsert.Columns.Add(dtCible.Columns[cpt].ColumnName, dtTypeT);
                dtInsert.Columns[dtInsert.Columns.Count - 1].Caption = dtCible.Columns[cpt].Caption;
                dtUpdate.Columns.Add(dtCible.Columns[cpt].ColumnName, dtTypeT);
                dtUpdate.Columns[dtUpdate.Columns.Count - 1].Caption = dtCible.Columns[cpt].Caption;
                dtDelete.Columns.Add(dtCible.Columns[cpt].ColumnName, dtTypeT);
                dtDelete.Columns[dtDelete.Columns.Count - 1].Caption = dtCible.Columns[cpt].Caption;
                dtUpdateOldValues.Columns.Add(dtCible.Columns[cpt].ColumnName, dtTypeT);
                dtUpdateOldValues.Columns[dtUpdateOldValues.Columns.Count - 1].Caption = dtCible.Columns[cpt].Caption;
                //_dtRowsInsert[_dtRowsInsert.Count - 1].Columns.Add(dtCible.Columns[cpt].ColumnName, System.Type.GetType("System.String"));
                //_dtRowsUpdate[_dtRowsUpdate.Count - 1].Columns.Add(dtCible.Columns[cpt].ColumnName, System.Type.GetType("System.String"));
                //_dtRowsDelete[_dtRowsDelete.Count - 1].Columns.Add(dtCible.Columns[cpt].ColumnName, System.Type.GetType("System.String"));
            }

            //ajout de la colonne de TAG si le mode synchro le demande
            if (SynchroTargetBehavior == SQLTools_Enums.SYNCHRO_TARGET_TABLE_BEHAVIOR.TAG)
            {
                if (!_dtRowsInsert[^1].Columns.Contains("SYNCHRO_TAG"))
                {
                    _dtRowsInsert[^1].Columns.Add("SYNCHRO_TAG", System.Type.GetType("System.String"));
                }
                _dtRowsInsert[^1].Columns["SYNCHRO_TAG"].DefaultValue = "I";

                if (!_dtRowsUpdate[^1].Columns.Contains("SYNCHRO_TAG"))
                {
                    _dtRowsUpdate[^1].Columns.Add("SYNCHRO_TAG", System.Type.GetType("System.String"));
                }
                _dtRowsUpdate[^1].Columns["SYNCHRO_TAG"].DefaultValue = "U";

                if (!_dtRowsDelete[^1].Columns.Contains("SYNCHRO_TAG"))
                {
                    _dtRowsDelete[^1].Columns.Add("SYNCHRO_TAG", System.Type.GetType("System.String"));
                }
                _dtRowsDelete[^1].Columns["SYNCHRO_TAG"].DefaultValue = "D";

                if (!_dtRowsUpdateOldValues[^1].Columns.Contains("SYNCHRO_TAG"))
                {
                    _dtRowsUpdateOldValues[^1].Columns.Add("SYNCHRO_TAG", System.Type.GetType("System.String"));
                }
                _dtRowsUpdateOldValues[^1].Columns["SYNCHRO_TAG"].DefaultValue = "D";
            }

            //ajout de la clé primaire aux datatables
            var dtCPKI = new DataColumn[sFieldsPrimaryKey.Count];
            var dtCPKD = new DataColumn[sFieldsPrimaryKey.Count];
            var dtCPKU = new DataColumn[sFieldsPrimaryKey.Count];
            var dtCPKUOV = new DataColumn[sFieldsPrimaryKey.Count];

            for (int cpt = 0; cpt < sFieldsPrimaryKey.Count; cpt++)
            {
                dtCPKI[cpt] = _dtRowsInsert[^1].Columns[sFieldsPrimaryKey[cpt].ColumnName];
                dtCPKU[cpt] = _dtRowsUpdate[^1].Columns[sFieldsPrimaryKey[cpt].ColumnName];
                dtCPKD[cpt] = _dtRowsDelete[^1].Columns[sFieldsPrimaryKey[cpt].ColumnName];
                dtCPKUOV[cpt] = _dtRowsUpdateOldValues[^1].Columns[sFieldsPrimaryKey[cpt].ColumnName];
            }
            _dtRowsInsert[^1].PrimaryKey = dtCPKI;
            _dtRowsUpdate[^1].PrimaryKey = dtCPKU;
            _dtRowsDelete[^1].PrimaryKey = dtCPKD;
            _dtRowsUpdateOldValues[^1].PrimaryKey = dtCPKUOV;

        }

        public void CompareDataTablesUI(object cptT, CONNString CSt, bool bSearchByCaption, string sTable, Query FuzibleQuery)
        {
            int numThread = (int)cptT;

            DataRow rowTemp = null;

            bool bSynchroTagSource = _dtSourceData.Columns.Contains("SYNCHRO_TAG");

            //int iRowsSource = _dtSourceData.Rows.Count;
            string[] sValuesPK = new string[_sPrimaryKeyForSynchro.Count];
            bool bBroken;
            //CAS DES INSERT + UPDATE A PARTIR DE LA SOURCE
            //for (int cptSource = 0; cptSource < iRowsSource; cptSource++)

            DateTime dtStart = DateTime.Now; bool bFirstPass = false;

            for (int cptSource = _iMTStart[numThread]; cptSource < _iMTStop[numThread]; cptSource += 1)
            {
                if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                {
                    bFirstPass = true;
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_comparedt_compareiu, sTable, " ", cptSource.ToString(), "/", _iMTStop[numThread].ToString(), Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptSource)), SQLTools_Enums.LOG_TYPEINFO.DET);
                }
                else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                {
                    bFirstPass = false;
                }

                try
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    for (int cptField = 0; cptField < _sPrimaryKeyForSynchro.Count; cptField++)
                    {
                        sValuesPK[cptField] = bSearchByCaption ? _dtSourceData.Rows[cptSource][_dtSourceData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(_sPrimaryKeyForSynchro[cptField].ColumnName)).ColumnName].ToString()
                                                                : _dtSourceData.Rows[cptSource][_sPrimaryKeyForSynchro[cptField].ColumnName].ToString();
                    }

                    bBroken = false;
                    try
                    {
                        rowTemp = _dtCibleData.Rows.Find(sValuesPK);
                    }
                    catch (Exception ex)
                    {
                        bBroken = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.mt_comparedt_invalidpk01 + sTable + " : " + string.Join(",", sValuesPK) + Languages.Languages.mt_comparedt_invalidpk02, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }

                    if (rowTemp != null)
                    {
                        // LIGNE TROUVEE : UPDATE ?
                        try
                        {
                            string sResult = DataRowsAreEqual(rowTemp, _dtSourceData.Rows[cptSource], FuzibleQuery.QueryAnalyzer.Fields, JobParameters, CSt);
                            if (sResult != null)
                            {
                                _sListColumnsDiff[numThread].Add(sResult);
                                //string s1 = string.Join(",", rowTemp.ItemArray);
                                //string s2 = string.Join(",", _dtSourceData.Rows[cptSource].ItemArray);
                                if (bSynchroTagSource) { _dtSourceData.Rows[cptSource]["SYNCHRO_TAG"] = 'U'; }
                                _dtRowsUpdate[numThread].ImportRow(_dtSourceData.Rows[cptSource]);
                                _dtRowsUpdateOldValues[numThread].ImportRow(rowTemp);
                            }
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.mt_comparedt_indexbroken, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }

                    }
                    else
                    {
                        // LIGNE NON TROUVEE DANS CIBLE : INSERT
                        if (!bBroken) { _dtRowsInsert[numThread].ImportRow(_dtSourceData.Rows[cptSource]); }
                    }

                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                    break;
                }
            }

            _isMThreadFinished[numThread] = true;

        }

        public void CompareDataTablesD(object cptT, bool bSearchByCaption, string sTable, Query FuzibleQuery)
        {
            int numThread = (int)cptT;
            _isMThreadFinished.Add(false); //astuce débile pour ajouter artificiellement un boolean (à cause du multitasking en ProcessorCount -1

            DataRow rowTemp = null;

            bool bSynchroTagTarget = _dtCibleData.Columns.Contains("SYNCHRO_TAG");

            //int iRowsSource = _dtSourceData.Rows.Count;
            int iRowsCible = _dtCibleData.Rows.Count;
            string[] sValuesPK = new string[_sPrimaryKeyForSynchro.Count];

            bool bBroken;

            DateTime dtStart = DateTime.Now; bool bFirstPass = false;

            //CAS DES DELETE A PARTIR DE LA CIBLE
            for (int cptCible = 0; cptCible < iRowsCible; cptCible++)
            {
                if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                {
                    bFirstPass = true;
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_comparedt_comparedel, sTable, " ", cptCible.ToString(), "/", iRowsCible.ToString(), Toolbox.GetPercent(0, iRowsCible, cptCible)), SQLTools_Enums.LOG_TYPEINFO.DET);
                }
                else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                {
                    bFirstPass = false;
                }

                try
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    for (int cptField = 0; cptField < _sPrimaryKeyForSynchro.Count; cptField++)
                    {
                        sValuesPK[cptField] = bSearchByCaption ? _dtCibleData.Rows[cptCible][_dtSourceData.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(_sPrimaryKeyForSynchro[cptField].ColumnName)).ColumnName].ToString()
                                                     : _dtCibleData.Rows[cptCible][_sPrimaryKeyForSynchro[cptField].ColumnName].ToString();
                    }

                    bBroken = false;
                    try
                    {
                        rowTemp = _dtSourceData.Rows.Find(sValuesPK);
                    }
                    catch (Exception ex)
                    {
                        bBroken = true;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.mt_comparedt_invalidpk01 + sTable + " : " + string.Join(",", sValuesPK) + Languages.Languages.mt_comparedt_invalidpk02, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }

                    if (rowTemp == null && !bBroken)
                    {
                        // LIGNE NON TROUVEE DANS SOURCE : DELETE
                        if (bSynchroTagTarget) { _dtCibleData.Rows[cptCible]["SYNCHRO_TAG"] = "D"; }
                        _dtRowsDelete[numThread].ImportRow(_dtCibleData.Rows[cptCible]);
                    }
                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                    break;
                }
            }
            //_dtListDataTables[0] = _dtRowsDelete[numThread];

            _isMThreadFinished[numThread] = true;
        }

        public void FindPrimaryKey(object cptT, List<SQLColumn[]> sListColumnCombination, DataTable dtSource, int iQteColumns)
        {
            int numThread = (int)cptT;

            SQLColumn[] sListCombination = null;
            int iCountDistinct;
            int iCountRows = dtSource.Rows.Count;

            for (int iC = _iMTStart[numThread]; iC < _iMTStop[numThread]; iC++)
            {
                try
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    //on découpe la combinaison de colonnes pour avoir les noms séparés
                    switch (iQteColumns)
                    {
                        case 2:
                            sListCombination = new SQLColumn[] { sListColumnCombination[iC][0], sListColumnCombination[iC][1] };
                            break;
                        case 3:
                            sListCombination = new SQLColumn[] { sListColumnCombination[iC][0], sListColumnCombination[iC][1], sListColumnCombination[iC][2] };
                            break;
                        case 4:
                            sListCombination = new SQLColumn[] { sListColumnCombination[iC][0], sListColumnCombination[iC][1], sListColumnCombination[iC][2], sListColumnCombination[iC][3] };
                            break;
                        case 5:
                            sListCombination = new SQLColumn[] { sListColumnCombination[iC][0], sListColumnCombination[iC][1], sListColumnCombination[iC][2], sListColumnCombination[iC][3], sListColumnCombination[iC][4] };
                            break;
                    }
                    //on procède à la comparation de types
                    iCountDistinct = Toolbox.GetDistinctRecords(dtSource, sListCombination, MyLog, numThread);

                    //si match, on considère qu'il s'agit effectivement d'une clé primaire
                    if (iCountDistinct == iCountRows)
                    {
                        foreach (SQLColumn sC in sListCombination)
                        {
                            _sFieldsPrimaryKey[numThread].Add(new SQLColumn(sC.ColumnIndex, sC.ColumnName, sC.ColumnType, sC.ColumnLinqType, sC.ColumnSize, sC.ColumnAllowsNullValues, sC.DefaultValue, sC.IsKey, sC.IsUnique));
                        }
                        break; //inutile d'aller plus loin si on a trouvé une clé primaire
                    }
                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                    break;
                }
            }

            _isMThreadFinished[numThread] = true;

        }

        public void FindDBNullInDataTable(object cptT, DataTable dtSource)
        {
            int numThread = (int)cptT;

            for (int iC = _iMTStart[numThread]; iC < _iMTStop[numThread]; iC++)
            {
                bool bHasEmpty = false;
                for (int iR = 0; iR < dtSource.Rows.Count; iR++)
                {
                    if (string.IsNullOrEmpty(dtSource.Rows[iR][iC].ToString()) || string.IsNullOrWhiteSpace(dtSource.Rows[iR][iC].ToString()))
                    {
                        bHasEmpty = true;
                        dtSource.Columns[iC].AllowDBNull = true;
                        break;
                    }
                    else if (JobParameters.ConvertHTMLPatternsForTarget && dtSource.Columns[iC].DataType == Type.GetType("System.String"))
                    {
                        try { dtSource.Rows[iR][iC] = Toolbox.ConvertHTMLValueToString(dtSource.Rows[iR][iC].ToString()); }
                        catch { }
                    }
                }
                if (!bHasEmpty)
                {
                    dtSource.Columns[iC].AllowDBNull = false;
                }
            }

            _isMThreadFinished[numThread] = true;
        }

        public void BulkInsertInBDDFromDt(object cptT, ref Query sQ)
        {
            int numThread = (int)cptT;
            SQLTools SQLConnTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

            try
            {
                try
                {
                    //création des datasets séparés
                    SQLConnTarget.BulkInsertInBDD(numThread, _dtListData[numThread], _sListTablesNames[numThread], _sListTypesFieldsTarget[numThread], _sListFieldsToKeep[numThread], sQ);
                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                }

                _isMThreadFinished[numThread] = true;

            }
            catch (Exception ex)
            {
                SQLConnTarget.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }
        }

        public void InsertInBDDFromDt(object cptT, Query sQ)
        {
            int numThread = (int)cptT;
            Task tOperation = null;

            SQLTools SQLConnTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
            //instantiation BDD
            //SQLTools SQLConnect = new SQLTools(SQLMTConnexion[numThread].WhichBDD, SQLMTConnexion[numThread].ConnexionString, SQLMTConnexion[numThread].DatabaseName);
            //SQLConnect.KEEPCONNECTION = True
            //Autorisation d'écrire dans les champs d'identité
            SQLConnTarget.SetIdentityInsert(_sListTablesNames[numThread], true, sQ);

            try
            {
                var sQteFieldsInDatatable = _dtListData[numThread].Columns.Cast<DataColumn>().Select(x => x.Caption).ToList();

                StringBuilder sLigne = new();
                StringBuilder sbCommitRequete = new();
                //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));

                int iActualRow = 0;

                DateTime dtStart = DateTime.Now; bool bFirstPass = false;

                for (int cptRow = _iMTStart[numThread]; cptRow < _iMTStop[numThread]; cptRow += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        iActualRow = cptRow;

                        try
                        {
                            sLigne.Append(string.Concat("INSERT INTO ", SQLConnTarget.EchappementChar, _sListTablesNames[numThread], SQLConnTarget.EchappementChar, " (", _sListEntetes[numThread], ") VALUES ("));

                            //MT !
                            //List<Task> thList = new List<Task>();
                            var sKeyValue = new List<KeyValuePair<int, string>>();
                            int iCptC = 0;
                            foreach (string sColumn in sQteFieldsInDatatable)
                            {
                                string sRealColumn = sColumn;
                                iCptC++;
                                int iC = iCptC;
                                //thList.Add(Task.Factory.StartNew(() =>
                                //{
                                if (_sListFieldsToKeep[numThread].Contains(sColumn)) //on regarde si la colonne actuelle est contenue dans la table cible
                                {
                                    string sValue = "";

                                    if (_dtListData[numThread].Columns.Contains(sColumn))
                                    {
                                        sValue = _dtListData[numThread].Rows[cptRow][sColumn].ToString();
                                    }
                                    else
                                    {
                                        DataColumn dt = _dtListData[numThread].Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(sColumn));
                                        sValue = _dtListData[numThread].Rows[cptRow][dt.ColumnName].ToString();
                                        sRealColumn = dt.ColumnName;
                                    }

                                    //on retrouve la colonne cible à partir de la liste des colonne sources (pour faire l'analyse comparative des champs source-cible)
                                    int iIndexColTarget = SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn);
                                    SQLColumn SQLColTarget = null;
                                    SQLColumn SQLColSource = null;
                                    if (iIndexColTarget > -1)
                                    {
                                        SQLColTarget = _sListTypesFieldsTarget[numThread][iIndexColTarget];
                                        var sField = sQ.QueryAnalyzer.Fields.FirstOrDefault(f => f.Alias.Equals(sColumn, StringComparison.OrdinalIgnoreCase));
                                        SQLColSource = sField != null ? sField.FieldAnalyzer : null;
                                    }
                                    else
                                    {
                                        if (_sListTypesFieldsTarget[numThread].Count == 0) //je force une colonne au pif car aucune colonne récupérée dans la source
                                        {
                                            SQLColTarget = new SQLColumn(sQteFieldsInDatatable.IndexOf(sColumn), sColumn, SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", true, "", false, false);
                                        }
                                        else { SQLColTarget = null; }
                                    }
                                    sKeyValue.Add(new KeyValuePair<int, string>(iC, CleanData(SQLColTarget, SQLColSource, _dtListData[numThread].Rows[cptRow][sRealColumn], SQLConnTarget)));
                                    //lock (sKeyValue) { sKeyValue.Add(new KeyValuePair<int, string>(iC, CleanData(SQLColTarget, sValue, SQLConnTarget))); }
                                }
                                //}, cancellationToken));
                            }

                            //Task.WaitAll(thList.ToArray(), cancellationToken);

                            sKeyValue = sKeyValue.OrderBy(s => s.Key).ToList();
                            sLigne.Append(string.Join(",", sKeyValue.Select(s => s.Value).ToList()));
                            sLigne.Append(");");
                            sbCommitRequete.AppendLine(sLigne.ToString());
                            sLigne.Clear();
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, sLigne.ToString(), sQ.RetryErrorOrWarning);
                            sQ.QueryErrors += 1;
                        }


                        //commit un certain nombre de lignes
                        if (((cptRow + 1) % SQLConnTarget.JobParameters.GlobalParameters.SQL_COMMIT == 0) | ((cptRow + 1) == _iMTStop[numThread]))
                        {
                            //permet de continuer à construire les requêtes pendant le chargement
                            if (tOperation != null)
                            {
                                Task.WaitAll(tOperation);
                            }

                            //suppression des contraintes de clés
                            SQLConnTarget.AlterTableConstraints(_sListTablesNames[numThread], true, sQ);

                            //insertion des lignes
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(false));
                            string sQuery = sbCommitRequete.ToString();
                            tOperation = Task.Factory.StartNew(() => SQLConnTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT, sQuery, sQ, _sListTablesNames[numThread], false));

                            //vidage stringbuilder
                            sbCommitRequete.Clear();
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));
                        }

                        if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                        {
                            bFirstPass = true;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, null, string.Concat("T", (numThread + 1).ToString("00"), _sListTablesNames[numThread], " ", Languages.Languages.mt_insertsql, " ", cptRow.ToString(), "/", _iMTStop[numThread].ToString() + Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptRow)), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                        {
                            bFirstPass = false;
                        }

                        //}
                    }
                    catch (OperationCanceledException)
                    {
                        _operationCancelled[numThread] = true;
                        break;
                    }
                }

                _isMThreadFinished[numThread] = true;

            }
            catch (Exception ex)
            {
                SQLConnTarget.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }


            //remise des contraintes de clés
            //SQLConnect.AlterTableConstraints(_sListTablesNames[numThread], false);

            //SQLConnect.Disconnect(_eBDD(numThread))
        }

        public void InsertInNOSQLFromDt(object cptT, Query sQ)
        {
            int numThread = (int)cptT;

            NOSQLTools NOSQLConnTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

            try
            {
                int iActualRow = 0;
                int iLast = 0;

                DateTime dtStart = DateTime.Now; bool bFirstPass = false;

                for (int cptRow = _iMTStart[numThread]; cptRow < _iMTStop[numThread]; cptRow += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        iActualRow = cptRow;

                        //commit un certain nombre de lignes
                        if ((cptRow + 1) % NOSQLConnTarget.JobParameters.GlobalParameters.SQL_COMMIT == 0)
                        {
                            iLast = cptRow + 1;
                            NOSQLConnTarget.InsertRows(_dtListData[numThread], _sListTablesNames[numThread], iLast - NOSQLConnTarget.JobParameters.GlobalParameters.SQL_COMMIT, cptRow, sQ);
                        }
                        else if ((cptRow + 1) == _iMTStop[numThread])
                        {
                            NOSQLConnTarget.InsertRows(_dtListData[numThread], _sListTablesNames[numThread], iLast, cptRow, sQ);
                        }

                        if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                        {
                            bFirstPass = true;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), NOSQLConnTarget.ClassPurpose, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_insertsql, " ", _sListTablesNames[numThread], " ", cptRow.ToString(), "/", _iMTStop[numThread].ToString() + Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptRow)), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                        {
                            bFirstPass = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _operationCancelled[numThread] = true;
                        break;
                    }
                }

                _isMThreadFinished[numThread] = true;

            }
            catch (Exception ex)
            {
                NOSQLConnTarget.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), NOSQLConnTarget.ClassPurpose, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }


            //remise des contraintes de clés
            //SQLConnect.AlterTableConstraints(_sListTablesNames[numThread], false);

            //SQLConnect.Disconnect(_eBDD(numThread))

        }

        public void UpdateInBDDFromDt(object cptT, ref Query sQ)
        {
            int numThread = (int)cptT;

            SQLTools SQLConnTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

            int iQteFieldsInDatatable = _dtListData[numThread].Columns.Count;

            try
            {
                StringBuilder sLigne = new();
                StringBuilder sbCommitRequete = new();
                SQLColumn SQLCSource;
                SQLColumn SQLCTarget;
                int iIndexSQLCTarget = -1;

                //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));

                int iActualRow = 0;
                bool bFirstCol = true;

                DateTime dtStart = DateTime.Now; bool bFirstPass = false;

                for (int cptRow = _iMTStart[numThread]; cptRow < _iMTStop[numThread]; cptRow += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        iActualRow = cptRow;

                        try
                        {
                            sLigne.Append(string.Concat("UPDATE ", SQLConnTarget.EchappementChar, _sListTablesNames[numThread], SQLConnTarget.EchappementChar, " SET "));

                            bFirstCol = true;
                            foreach (DataColumn dtC in _dtListData[numThread].Columns)
                            {
                                string sColumn = dtC.Caption.Length > 0 && !dtC.Caption.Equals(dtC.ColumnName) ? dtC.Caption : dtC.ColumnName;

                                //on met à jour les colonnes qui ne sont pas dans les conditions WHERE, évidemment
                                if (!Toolbox.GetColumnNamesFromSQLColumnObjects(_sListFieldsWhere[numThread]).Contains(sColumn))
                                {
                                    if (bFirstCol)
                                    {
                                        SQLCSource = sQ.QueryAnalyzer.Fields.First(f => f.Alias.Equals(sColumn, StringComparison.OrdinalIgnoreCase)).FieldAnalyzer;

                                        //on retrouve la colonne cible à partir de la liste des colonne sources (pour faire l'analyse comparative des champs source-cible)
                                        iIndexSQLCTarget = SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn);
                                        if (iIndexSQLCTarget > -1)
                                        {
                                            SQLCTarget = _sListTypesFieldsTarget[numThread][SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn)];
                                        }
                                        else { SQLCTarget = null; }

                                        if (_dtListData[numThread].Columns.Contains(SQLCSource.ColumnName))
                                        {
                                            SQLCTarget = _sListTypesFieldsTarget[numThread][SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn)];
                                            sLigne.Append(string.Concat(SQLConnTarget.EchappementChar,
                                                            _dtListData[numThread].Columns[SQLCSource.ColumnName].ColumnName,
                                                            SQLConnTarget.EchappementChar, " = ",
                                                            CleanData(SQLCTarget, SQLCSource, _dtListData[numThread].Rows[iActualRow][SQLCSource.ColumnName], SQLConnTarget)));
                                        }
                                    }
                                    else
                                    {
                                        SQLCSource = sQ.QueryAnalyzer.Fields.First(f => f.Alias.Equals(sColumn, StringComparison.OrdinalIgnoreCase)).FieldAnalyzer;

                                        //on retrouve la colonne cible à partir de la liste des colonne sources (pour faire l'analyse comparative des champs source-cible)
                                        iIndexSQLCTarget = SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn);
                                        if (iIndexSQLCTarget > -1)
                                        {
                                            SQLCTarget = _sListTypesFieldsTarget[numThread][SQLColumn.GetIndexSQLColumn(_sListTypesFieldsTarget[numThread], sColumn)];
                                        }
                                        else { SQLCTarget = null; }

                                        if (_dtListData[numThread].Columns.Contains(SQLCSource.ColumnName))
                                        {
                                            sLigne.Append(string.Concat(", ", SQLConnTarget.EchappementChar,
                                                            _dtListData[numThread].Columns[SQLCSource.ColumnName].ColumnName,
                                                            SQLConnTarget.EchappementChar, " = ",
                                                            CleanData(SQLCTarget, SQLCSource, _dtListData[numThread].Rows[iActualRow][SQLCSource.ColumnName], SQLConnTarget)));

                                        }
                                    }
                                    bFirstCol = false;
                                }

                            }


                            //Ajout des conditions WHERE (condition crée à partir des colonnes clé primaire)
                            sLigne.Append(" WHERE ");

                            for (int cptW = 0; cptW < _sListFieldsWhere[numThread].Count; cptW += 1)
                            {
                                if (cptW == 0)
                                {
                                    sLigne.Append(string.Concat(SQLConnTarget.EchappementChar,
                                        _sListFieldsWhere[numThread][cptW].ColumnName, SQLConnTarget.EchappementChar, " = ",
                                        Toolbox.FastCheckType(_dtListData[numThread].Rows[iActualRow][_sListFieldsWhere[numThread][cptW].ColumnName].ToString(), JobParameters, SQLConnTarget.Connection, _sListFieldsWhere[numThread][cptW])));
                                }
                                else
                                {
                                    sLigne.Append(string.Concat(" AND ", SQLConnTarget.EchappementChar,
                                        _sListFieldsWhere[numThread][cptW].ColumnName,
                                        SQLConnTarget.EchappementChar, " = ",
                                        Toolbox.FastCheckType(_dtListData[numThread].Rows[iActualRow][_sListFieldsWhere[numThread][cptW].ColumnName].ToString(), JobParameters, SQLConnTarget.Connection, _sListFieldsWhere[numThread][cptW])));
                                }
                            }
                            sbCommitRequete.AppendLine(string.Concat(sLigne.ToString(), ";"));
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, sLigne.ToString(), sQ.RetryErrorOrWarning);
                            sQ.QueryErrors += 1;
                        }
                        //effacement de la ligne
                        sLigne.Clear();

                        //commit un certain nombre de lignes
                        if (((cptRow + 1) % SQLConnTarget.JobParameters.GlobalParameters.SQL_COMMIT == 0) | ((cptRow + 1) == _iMTStop[numThread]))
                        {
                            //suppression des contraintes de clés
                            SQLConnTarget.AlterTableConstraints(_sListTablesNames[numThread], true, sQ);

                            //insertion des lignes
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(false));
                            SQLConnTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE, sbCommitRequete.ToString(), sQ, _sListTablesNames[numThread], false);

                            //vidage stringbuilder
                            sbCommitRequete.Clear();
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));
                        }

                        if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                        {
                            bFirstPass = true;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, null, string.Concat("T", (numThread + 1).ToString("00"), _sListTablesNames[numThread], " ", Languages.Languages.mt_updatesql, cptRow.ToString(), "/", _iMTStop[numThread].ToString(), Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptRow)), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                        {
                            bFirstPass = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _operationCancelled[numThread] = true;
                        break;
                    }
                }

                _isMThreadFinished[numThread] = true;

            }
            catch (Exception ex)
            {
                SQLConnTarget.MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }
        }

        public void DeleteInBDDFromDt(object cptT, Query sQ)
        {
            int numThread = (int)cptT;

            SQLTools SQLConnTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

            try
            {
                StringBuilder sLigne = new();
                StringBuilder sbCommitRequete = new();
                //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));

                int iActualRow = 0;

                DateTime dtStart = DateTime.Now; bool bFirstPass = false;

                for (int cptRow = _iMTStart[numThread]; cptRow < _iMTStop[numThread]; cptRow += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        iActualRow = cptRow;

                        try
                        {
                            sLigne.Append(string.Concat("DELETE FROM ", SQLConnTarget.EchappementChar, _sListTablesNames[numThread], SQLConnTarget.EchappementChar, " WHERE "));

                            for (int cptW = 0; cptW < _sListFieldsWhere[numThread].Count; cptW += 1)
                            {
                                string sValue = "";

                                if (_dtListData[numThread].Columns.Contains(_sListFieldsWhere[numThread][cptW].ColumnName))
                                {
                                    sValue = _dtListData[numThread].Rows[cptRow][_sListFieldsWhere[numThread][cptW].ColumnName].ToString();
                                }
                                else
                                {
                                    DataColumn dt = _dtListData[numThread].Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(_sListFieldsWhere[numThread][cptW].ColumnName));
                                    sValue = _dtListData[numThread].Rows[iActualRow][dt.ColumnName].ToString();
                                }

                                if (cptW == 0)
                                {
                                    sLigne.Append(string.Concat(SQLConnTarget.EchappementChar, _sListFieldsWhere[numThread][cptW].ColumnName,
                                        SQLConnTarget.EchappementChar, " = ",
                                        Toolbox.FastCheckType(sValue, JobParameters, SQLConnTarget.Connection, _sListFieldsWhere[numThread][cptW])));

                                }
                                else
                                {
                                    sLigne.Append(string.Concat(" AND ", SQLConnTarget.EchappementChar, _sListFieldsWhere[numThread][cptW].ColumnName,
                                        SQLConnTarget.EchappementChar, " = ",
                                        Toolbox.FastCheckType(sValue, JobParameters, SQLConnTarget.Connection, _sListFieldsWhere[numThread][cptW])));
                                }
                            }
                            sbCommitRequete.AppendLine(string.Concat(sLigne.ToString(), ";"));
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, sLigne.ToString(), sQ.RetryErrorOrWarning);
                            sQ.QueryErrors += 1;
                        }
                        //effacement de la ligne
                        sLigne.Clear();

                        //commit un certain nombre de lignes
                        if (((cptRow + 1) % SQLConnTarget.JobParameters.GlobalParameters.SQL_COMMIT == 0) | ((cptRow + 1) == _iMTStop[numThread]))
                        {
                            //suppression des contraintes de clés
                            SQLConnTarget.AlterTableConstraints(_sListTablesNames[numThread], true, sQ);

                            //insertion des lignes
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(false));
                            SQLConnTarget.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, sbCommitRequete.ToString(), sQ, _sListTablesNames[numThread], false);

                            //vidage stringbuilder
                            sbCommitRequete.Clear();
                            //sbCommitRequete.AppendLine(SQLConnTarget.PrepareSQLTransaction(true));
                        }

                        if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                        {
                            bFirstPass = true;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, null, string.Concat("T", (numThread + 1).ToString("00"), _sListTablesNames[numThread], " ", Languages.Languages.mt_deletesql, cptRow.ToString(), "/", _iMTStop[numThread].ToString(), Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptRow)), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                        {
                            bFirstPass = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _operationCancelled[numThread] = true;
                        break;
                    }
                }

                _isMThreadFinished[numThread] = true;

            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLConnTarget.ClassPurpose, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }
        }

        public void AnalyzeDataFromDs(object cptT, int iField, SQLTools_Enums.TYPE_DATA SqlCharType, SQLTools_Enums.TYPE_DATA SqlDateType, Query sQ)
        {
            int numThread = (int)cptT;

            //iField == -1 : tous les champs, iField > -1 : un champ spécifique
            if (iField > -1)
            {
                _iMTStart[numThread] = _iMTStart[numThread] + iField;
                _iMTStop[numThread] = _iMTStop[numThread] + iField;
            }

            try
            {

                string sValue = "";
                List<SQLColumn> sListeFields = new();

                int iStep = 0;
                switch (JobParameters.FieldAnalyzerLevel)
                {
                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.HIGH_PRECISION:
                        iStep = 1;
                        break;
                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.NORMAL_PRECISION:
                        iStep = 5;
                        break;
                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.LOW_PRECISION:
                        iStep = 10;
                        break;
                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.VERY_LOW_PRECISION:
                        iStep = 20;
                        break;
                }


                if (_drListDataRows[numThread].Count < 100000)
                {
                    iStep = 1;
                }

                //parcours de chaque ligne pour tester l'intégralité des données

                for (int cptCell = _iMTStart[numThread]; cptCell <= _iMTStop[numThread] - 1; cptCell += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        bool bIsBitArray = false;
                        int hasQteNull = 0;
                        bool bIsUnique = true;
                        List<string> sListValuesToIdentifyUnicity = new();
                        bool hasString = false;
                        bool hasText = false;
                        bool hasDecimal = false;
                        bool hasFloat = false;
                        bool hasBool = true; //je mets celui-là à true par défaut car je procède par déduction pour l'identifier
                        bool hasBit = true;
                        bool bTestNumeric = true;
                        int iTestBool = 1;
                        string sTestBitValueCheck = ""; //contrôle qu'on a bien les 2 valeurs (1 et 0) pour s'assurer que c'est bien un bit et pas un int
                        bool bIsRealBit = false;
                        int iTestDate = 1;
                        bool bIsPK = false;
                        SQLTools_Enums.TYPE_DATA sTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                        string sSizeData = "";
                        int iLenMax = 1;
                        int iDecimalesMax = 1;
                        int iPartieEntiereMax = 1;
                        SQLTools_Enums.TYPE_DATA iTypeData = SQLTools_Enums.TYPE_DATA.UNKNOWN;
                        string sColumnName = "";
                        if (_drListDataRows[numThread].Count > 0) { sColumnName = _drListDataRows[numThread][0].Table.Columns[cptCell].ColumnName; }

                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_shsfield, sColumnName, " ", cptCell.ToString(), "/", (_iMTStop[numThread] - 1).ToString(), Toolbox.GetPercent(_iMTStart[numThread], _iMTStop[numThread], cptCell)), SQLTools_Enums.LOG_TYPEINFO.DET);

                        for (int cptRow = 0; cptRow <= _drListDataRows[numThread].Count - 1; cptRow += iStep)
                        {
                            sValue = _drListDataRows[numThread][cptRow][cptCell].ToString();

                            if (JobParameters.CheckFieldsBeforeInsert)
                            {
                                try
                                {
                                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                                    //test d'unicité des valeurs
                                    //if (bIsUnique)
                                    //{
                                    //    if (sListValuesToIdentifyUnicity.Contains(sValue)) { bIsUnique = false; }
                                    //    sListValuesToIdentifyUnicity.Add(sValue);
                                    //}

                                    if (sValue.Length > iLenMax)
                                    {
                                        iLenMax = sValue.Length;
                                    }

                                    if (sValue.Length > 0)
                                    {
                                        if (!bIsBitArray)
                                        {
                                            if (_drListDataRows[numThread][cptRow][cptCell].GetType().Name == "Byte[]")
                                            {
                                                int iLen = ((byte[])_drListDataRows[numThread][cptRow][cptCell]).Length;
                                                iLenMax = iLen > iLenMax ? iLen : iLenMax;
                                                bIsBitArray = true;
                                                hasBool = false;
                                                hasBit = false;
                                            }
                                        }
                                        else
                                        {
                                            int iLen = ((byte[])_drListDataRows[numThread][cptRow][cptCell]).Length;
                                            bIsBitArray = true;
                                            iLenMax = iLen > iLenMax ? iLen : iLenMax;
                                            hasBool = false;
                                            hasBit = false;
                                        }

                                        if (!bIsBitArray)
                                        {
                                            //check de boolean
                                            if (iTestBool > 0) // on ne teste le format que si et seulement si on n'a pas trouvé autre chose
                                            {
                                                iTestBool = Toolbox.IsBit1OrBoolean2(sValue);
                                                if (iTestBool == 0)
                                                {
                                                    hasBit = false; hasBool = false;
                                                }
                                                else if (iTestBool == 1)
                                                {
                                                    hasBit = true; hasBool = false;

                                                    if (sTestBitValueCheck.Length > 0 && !sTestBitValueCheck.Equals(sValue))
                                                    { bIsRealBit = true; }
                                                    else { sTestBitValueCheck = sValue; }
                                                }
                                                else if (iTestBool == 2)
                                                {
                                                    hasBool = true; hasBit = false;
                                                }
                                            }

                                            if (iTestDate > 0)
                                            {
                                                iTestDate = Toolbox.IsDate1OrDateTime2(sValue, JobParameters.ConnectionString_Source);
                                                if (iTestDate == 1) { iTypeData = SQLTools_Enums.TYPE_DATA.DATE; }
                                                else if (iTestDate == 2) { iTypeData = SQLTools_Enums.TYPE_DATA.DATETIME; }
                                            }
                                            else if (bTestNumeric)
                                            {
                                                bTestNumeric = Toolbox.IsNumeric(sValue, JobParameters.ConnectionString_Source);
                                                //If Regex.IsMatch(sValue, "^[+-]?(\d+(\.\d+)?|\.\d+)$") Then
                                                if (bTestNumeric)
                                                {
                                                    //si la valeur entier = valeur, alors c'est bien un nombre entier
                                                    if (Toolbox.IsInteger(sValue, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0))
                                                    {
                                                        sValue = Toolbox.SetCleanNumber(sValue, JobParameters.ConnectionString_Source, SQLTools_Enums.TYPE_DATA.INT, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                                                        iTypeData = SQLTools_Enums.TYPE_DATA.INT;
                                                        if (sValue.Length > iPartieEntiereMax)
                                                        {
                                                            iPartieEntiereMax = sValue.Length;
                                                        }
                                                    }
                                                    else if (Toolbox.IsDecimal(sValue, JobParameters.ConnectionString_Source, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0))
                                                    {
                                                        sValue = Toolbox.SetCleanNumber(sValue, JobParameters.ConnectionString_Source, SQLTools_Enums.TYPE_DATA.DECIMAL, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                                                        iTypeData = SQLTools_Enums.TYPE_DATA.DECIMAL;
                                                        //sinon c'est un décimal
                                                        hasDecimal = true;
                                                        if (Toolbox.GetDecimalPartOfADouble(sValue).Length > iDecimalesMax)
                                                        {
                                                            iDecimalesMax = Toolbox.GetDecimalPartOfADouble(sValue).Length;
                                                        }
                                                        if (Toolbox.GetIntPartOfADouble(sValue).Length > iPartieEntiereMax)
                                                        {
                                                            iPartieEntiereMax = Toolbox.GetIntPartOfADouble(sValue).Length;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (!JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0 && Regex.IsMatch(sValue, SHSRegex.REGEX_ISINTEGER_STARTSWITH_0))
                                                        {
                                                            iTypeData = SQLTools_Enums.TYPE_DATA.VARCHAR;
                                                        }
                                                        else
                                                        {
                                                            iTypeData = SQLTools_Enums.TYPE_DATA.FLOAT;
                                                            hasFloat = true;
                                                            if (Toolbox.GetDecimalPartOfADouble(sValue).Length > iDecimalesMax)
                                                            {
                                                                iDecimalesMax = Toolbox.GetDecimalPartOfADouble(sValue).Length;
                                                            }
                                                            if (Toolbox.GetIntPartOfADouble(sValue).Length > iPartieEntiereMax)
                                                            {
                                                                iPartieEntiereMax = Toolbox.GetIntPartOfADouble(sValue).Length;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    hasString = true;
                                                    //iTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                                                }
                                            }
                                            else
                                            {
                                                hasString = true;
                                                //iTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                                                ////format "TEXT" déprécié en SQL Server
                                                //if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLSERVER)
                                                //{
                                                //    hasString = true;
                                                //    iTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                                                //}
                                                //else
                                                //{
                                                //    if (Toolbox.IsText(sValue))
                                                //    {
                                                //        hasText = true;
                                                //        iTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                                                //    }
                                                //    else
                                                //    {
                                                //        hasString = true;
                                                //        iTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                                                //    }
                                                //}
                                            }
                                        }
                                    }
                                    else
                                    {
                                        hasQteNull += 1;
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, ex.Message, sQ.RetryErrorOrWarning);
                                    sQ.QueryErrors += 1;
                                }

                            }
                            else
                            {
                                iTypeData = SQLTools_Enums.TYPE_DATA.VARCHAR;
                                hasString = true;
                                hasBool = false;
                                hasBit = false;
                                hasQteNull += 1;
                                iLenMax = sValue.Length > iLenMax ? sValue.Length : iLenMax;
                            }
                        }

                        if (bIsBitArray)
                        {
                            iTypeData = SQLTools_Enums.TYPE_DATA.BYTEXXXZZZ;
                        }

                        //attention, l'ordre est très important !!! TEST DECIMAL AVANT STRING
                        if (hasDecimal)
                        {
                            iTypeData = SQLTools_Enums.TYPE_DATA.DECIMAL;
                        }

                        if (hasFloat)
                        {
                            iTypeData = SQLTools_Enums.TYPE_DATA.FLOAT;
                        }

                        //attention, l'ordre est très important !!! TEST DECIMAL AVANT STRING
                        if (hasString)
                        {
                            if (iLenMax == 1)
                            {
                                iTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                            }
                            else
                            {
                                iTypeData = SQLTools_Enums.TYPE_DATA.VARCHAR;
                            }
                        }

                        if (hasText)
                        {
                            iTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                        }

                        if (hasBool)
                        {
                            iTypeData = SQLTools_Enums.TYPE_DATA.BOOL;
                        }

                        if (hasBit)
                        {
                            if (bIsRealBit)
                            {
                                iTypeData = SQLTools_Enums.TYPE_DATA.BIT;
                            }
                            else { iTypeData = SQLTools_Enums.TYPE_DATA.INT; }
                        }

                        //en niveau d'analyse approximatif, on ajoute de l'espace aux champs au cas ou des lignes avec des champs plus grands seraient passés en dehors des mailles de l'analyse
                        if (JobParameters.CheckFieldsBeforeInsert)
                        {
                            if (JobParameters.FieldAnalyzerLevel != SQLTools_Enums.PRECISION_COLUMN_ANALYZER.HIGH_PRECISION)
                            {
                                switch (JobParameters.FieldAnalyzerLevel)
                                {
                                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.NORMAL_PRECISION:
                                        iLenMax += Convert.ToInt32(iLenMax * 0.25);
                                        iPartieEntiereMax += Convert.ToInt32(iPartieEntiereMax * 0.75);
                                        iDecimalesMax += Convert.ToInt32(iDecimalesMax * 0.25);
                                        break;
                                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.LOW_PRECISION:
                                        iLenMax += Convert.ToInt32(iLenMax * 0.5);
                                        iPartieEntiereMax += Convert.ToInt32(iPartieEntiereMax * 1);
                                        iDecimalesMax += Convert.ToInt32(iDecimalesMax * 0.5);
                                        break;
                                    case SQLTools_Enums.PRECISION_COLUMN_ANALYZER.VERY_LOW_PRECISION:
                                        iLenMax += Convert.ToInt32(iLenMax * 1);
                                        iPartieEntiereMax += Convert.ToInt32(iPartieEntiereMax * 1.5);
                                        iDecimalesMax += Convert.ToInt32(iDecimalesMax * 0.75);
                                        break;
                                }
                            }
                        }

                        switch (iTypeData)
                        {
                            case SQLTools_Enums.TYPE_DATA.BYTEXXXZZZ:
                                sTypeData = SQLTools_Enums.TYPE_DATA.BYTEXXXZZZ;
                                sSizeData = iLenMax.ToString();
                                break;
                            case SQLTools_Enums.TYPE_DATA.UNKNOWN:
                                sTypeData = SqlCharType;
                                if (iLenMax > 4000)
                                {
                                    //sTypeData = SQLTools_Enums.TYPE_DATA;
                                    sSizeData = "4000";
                                }
                                else
                                if (iLenMax == 1) { sTypeData = SQLTools_Enums.TYPE_DATA.CHAR; }
                                else { sSizeData = iLenMax.ToString(); }
                                break;
                            case SQLTools_Enums.TYPE_DATA.CHAR:
                                sTypeData = SQLTools_Enums.TYPE_DATA.CHAR;
                                sSizeData = "1";
                                break;
                            case SQLTools_Enums.TYPE_DATA.VARCHAR:
                                if (iLenMax > 4000)
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                                    sSizeData = "";
                                }
                                else
                                {
                                    sTypeData = SqlCharType;
                                    sSizeData = iLenMax.ToString();
                                }
                                break;
                            case SQLTools_Enums.TYPE_DATA.TEXT:
                                sTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                                sSizeData = "";
                                break;
                            case SQLTools_Enums.TYPE_DATA.INT:
                                if (iLenMax <= 2)
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.SMALLINT; sSizeData = iLenMax.ToString();
                                }
                                else if (iLenMax <= 10)
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.INT; sSizeData = iLenMax.ToString();
                                }
                                else { sTypeData = SQLTools_Enums.TYPE_DATA.BIGINT; sSizeData = iLenMax.ToString(); }
                                break;
                            case SQLTools_Enums.TYPE_DATA.DECIMAL:
                                if (iDecimalesMax > JobParameters.GlobalParameters.SQL_DECIMALS)
                                {
                                    iDecimalesMax = JobParameters.GlobalParameters.SQL_DECIMALS;
                                }
                                sTypeData = SQLTools_Enums.TYPE_DATA.DECIMAL;
                                sSizeData = (iPartieEntiereMax + iDecimalesMax + 1).ToString() + "," + (iDecimalesMax).ToString();
                                break;
                            case SQLTools_Enums.TYPE_DATA.FLOAT:
                                //sTypeData = " FLOAT";
                                if (iPartieEntiereMax + iDecimalesMax > 38)
                                {
                                    if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_POSTGRE)
                                    {
                                        sTypeData = SQLTools_Enums.TYPE_DATA.DOUBLE;
                                        sSizeData = "";
                                    }
                                    else
                                    {
                                        sTypeData = SQLTools_Enums.TYPE_DATA.FLOAT;
                                        sSizeData = "";
                                    }
                                }
                                else
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.NUMERIC;
                                    sSizeData = (iPartieEntiereMax + iDecimalesMax).ToString() + "," + iDecimalesMax.ToString();
                                }
                                break;
                            case SQLTools_Enums.TYPE_DATA.DATE:
                                sTypeData = SQLTools_Enums.TYPE_DATA.DATE;
                                sSizeData = "";
                                break;
                            case SQLTools_Enums.TYPE_DATA.DATETIME:
                                sTypeData = JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE ? SQLTools_Enums.TYPE_DATA.DATETIME : SqlDateType;
                                sSizeData = "";
                                break;
                            case SQLTools_Enums.TYPE_DATA.BOOL:
                                if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.TEXT;
                                    sSizeData = "";
                                }
                                else
                                {
                                    if (iLenMax == 2) //dangereux deconsidérer une colonne vide comme booleenne
                                    {
                                        sTypeData = SqlCharType;
                                        sSizeData = "1";
                                    }
                                    else
                                    {
                                        sTypeData = SQLTools_Enums.TYPE_DATA.BOOL;
                                        sSizeData = "";
                                    }
                                }
                                break;
                            case SQLTools_Enums.TYPE_DATA.BIT:
                                if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                                {
                                    sTypeData = SQLTools_Enums.TYPE_DATA.INTEGER;
                                    sSizeData = "";
                                }
                                else
                                {
                                    if (iLenMax == 2) //dangereux deconsidérer une colonne vide comme booleenne
                                    {
                                        sTypeData = SqlCharType;
                                        sSizeData = "1";
                                    }
                                    else
                                    {
                                        sTypeData = SQLTools_Enums.TYPE_DATA.BIT;
                                        sSizeData = "";
                                    }
                                }
                                break;
                        }

                        //problème du ROWNUM quand on n'a que 2 lignes dans le fichier (il interprète du BIT
                        if (_drListDataRows[numThread].Count < 2 && (sColumnName.Equals("ROWNUM") || sColumnName.Equals("WS_ROWID")))
                        {
                            sTypeData = SQLTools_Enums.TYPE_DATA.INT; sSizeData = "";
                        }

                        if (hasQteNull == _drListDataRows[numThread].Count) //que des NULL : on prend le type original de la colonne
                        {
                            iLenMax = _drListDataRows[numThread][0].Table.Columns[cptCell].MaxLength;
                            var t = _drListDataRows[numThread][0].Table.Columns[cptCell].DataType;
                            sTypeData = Toolbox.GetSQLTypeFromLinqType(t, iLenMax);
                        }

                        if (_drListDataRows[numThread].Count > 0)
                        {
                            //on regarde dans la source  si la colonne est considérée comme clé primaire (on ne peut pas deviner cette information)
                            DataColumn[] dtPKList = _drListDataRows[numThread][0].Table.PrimaryKey;
                            foreach (DataColumn dt in dtPKList)
                            {
                                if (dt.ColumnName.Equals(_drListDataRows[numThread][0].Table.Columns[cptCell].ColumnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    bIsPK = true;
                                }
                            }
                            bIsUnique = _drListDataRows[numThread][0].Table.Columns[cptCell].Unique;
                            //on ajoute le paramétrage UNIQUE, PK, DefaultValue du Dataset Source (impossible à deviner !)

                            string sDriverData = "";
                            if (_drListDataRows[numThread][0].Table.Columns[cptCell].ExtendedProperties.Contains("Driver Data"))
                            {
                                sDriverData = _drListDataRows[numThread][0].Table.Columns[cptCell].ExtendedProperties["Driver Data"].ToString();
                            }

                            sListeFields.Add(new SQLColumn(cptCell, sColumnName, sTypeData, Toolbox.GetSystemTypeFromListTypes(sTypeData), sSizeData, hasQteNull > 0 ? true : false, _drListDataRows[numThread][0].Table.Columns[cptCell].DefaultValue.ToString(), bIsPK, bIsUnique, sDriverData));
                        }
                        else
                        {
                            sListeFields.Add(new SQLColumn(cptCell, sColumnName, sTypeData, Toolbox.GetSystemTypeFromListTypes(sTypeData), sSizeData, hasQteNull > 0 ? true : false, "", false, false, ""));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _operationCancelled[numThread] = true;
                        break;
                    }
                }

                _isMThreadFinished[numThread] = true;
                _sListFinalFieldsTypes[numThread] = sListeFields;

            }
            catch (OperationCanceledException oce)
            {
                _operationCancelled[numThread] = true;
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, oce, oce.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }
            catch (Exception ex)
            {
                _operationCancelled[numThread] = true;
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }

        }

        public void CrossJoinDataTables(object cptT, int iQteCPU, SQLTools_Enums.CROSSJOIN_TYPES ctCrossJoinType, DataTable dtPrimaryTable, DataTable dtSecondaryTable, List<string[]> sLinkFields, int iFT1, int iFT2)
        {
            int numThread = (int)cptT;

            int iStart = _iMTStart[numThread];
            int iStop = _iMTStop[numThread];

            DateTime dtStart = DateTime.Now; bool bFirstPass = false;

            for (int iR = iStart; iR < iStop; iR++) //on parcourt un échantillon de la table secondaire (partage entre threads)
            {
                if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass)
                {
                    bFirstPass = true;
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_crossjoinmerge, iR.ToString(), "/", iStop.ToString() + Toolbox.GetPercent(iStart, iStop, iR)), SQLTools_Enums.LOG_TYPEINFO.DET);
                }
                else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                {
                    bFirstPass = false;
                }

                if (ctCrossJoinType == SQLTools_Enums.CROSSJOIN_TYPES.RIGHT)
                {
                    CrossJoinLeftRight(dtSecondaryTable, dtPrimaryTable, sLinkFields, ctCrossJoinType, iR, iFT1, iFT2, numThread);
                }
                else
                {
                    CrossJoinLeftRight(dtPrimaryTable, dtSecondaryTable, sLinkFields, ctCrossJoinType, iR, iFT1, iFT2, numThread);
                }
            }


            if (ctCrossJoinType == SQLTools_Enums.CROSSJOIN_TYPES.OUTER)
            {
                iStart = _iMTStart[numThread + iQteCPU];
                iStop = _iMTStop[numThread + iQteCPU];

                DateTime dtStart2 = DateTime.Now;

                dtStart = DateTime.Now;

                for (int iR = iStart; iR < iStop; iR++) //on parcourt un échantillon de la table secondaire (partage entre threads)
                {
                    if ((DateTime.Now - dtStart2).Seconds % 5 == 0)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_crossjoinmerge, iR.ToString(), "/", iStop.ToString() + Toolbox.GetPercent(iStart, iStop, iR)), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }

                    CrossJoinLeftRight(dtSecondaryTable, dtPrimaryTable, sLinkFields, ctCrossJoinType, iR, iFT1, iFT2, numThread);
                }
            }

            _isMThreadFinished[numThread] = true;

        }

        private void CrossJoinLeftRight(DataTable dtA, DataTable dtB, List<string[]> sLinkFields, SQLTools_Enums.CROSSJOIN_TYPES ctCrossJoinType, int iR, int iFT1, int iFT2, int numThread)
        {
            //parcourt toutes les lignes de la table 2
            bool bAtLeastOneFound = false; ;
            for (int iS = 0; iS < dtB.Rows.Count; iS++)
            {
                //recherche de l'égalité de la jointure
                bool bFound = true;
                foreach (string[] sL in sLinkFields)
                {
                    if (!dtA.Rows[iR][sL[iFT1]].ToString().Equals(dtB.Rows[iS][sL[iFT2]].ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        bFound = false; break;
                    }
                }
                if (bFound)
                {
                    DataRow insertRow = _dtJoin2Dt[numThread].NewRow();
                    //données principales première table
                    foreach (DataColumn col1 in dtA.Columns)
                    {
                        insertRow[col1.ColumnName] = dtA.Rows[iR][col1.ColumnName];
                    }
                    //complétion informations par colonnes de la table jointe
                    foreach (DataColumn col2 in dtB.Columns)
                    {
                        if (!JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(col2.ColumnName))
                        {
                            insertRow[col2.ColumnName] = dtB.Rows[iS][col2.ColumnName];
                        }

                    }
                    _dtJoin2Dt[numThread].Rows.Add(insertRow);
                    bAtLeastOneFound = true;
                }
            }
            if (!bAtLeastOneFound)
            {
                if (ctCrossJoinType != SQLTools_Enums.CROSSJOIN_TYPES.INNER)
                {
                    DataRow insertRow = _dtJoin2Dt[numThread].NewRow();
                    foreach (DataColumn col1 in dtA.Columns)
                    {
                        insertRow[col1.ColumnName] = dtA.Rows[iR][col1.ColumnName];
                    }
                    _dtJoin2Dt[numThread].Rows.Add(insertRow);
                }
            }
        }

        public void FillDatatableFromCSVArray(object cptT, string[] sFileInRamA, string sCSVCharSeparator, bool bFirstRowIsHead, List<SQLColumn> sListTypesFieldsSource, Query sQ)
        {
            int numThread = (int)cptT;

            int iTooManyErrors = 0;
            string[] sRow;
            DataRow dtR;
            string sValue;

            int iOffset = (bFirstRowIsHead ? 1 : 0) + JobParameters.CSVRowOffset;
            int iStart = _iMTStart[numThread] + iOffset;
            int iStop = _iMTStop[numThread] + iOffset;
            int iDx;

            DateTime dtStart = DateTime.Now; bool bFirstPass = false;

            for (int cptR = iStart; cptR < iStop; cptR++)
            {
                try
                {
                    try
                    {
                        sRow = Regex.Matches(sFileInRamA[cptR], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();
                    }
                    catch { sRow = null; }

                    if (sRow != null && (sRow.Length >= sListTypesFieldsSource.Count || JobParameters.GlobalParameters.CSV_FORCE_INTEGRATION_WRONG_LENGTH))
                    {
                        if (sRow.Length < sListTypesFieldsSource.Count)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("Invalid Row (Row Number:", cptR.ToString(), ",Row Length:", sRow.Length.ToString(), ",Header Length:", sListTypesFieldsSource.Count.ToString(), ") - ", sFileInRamA[cptR]), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }

                        dtR = _dtFillCSV[numThread].NewRow();

                        for (int cptC = 0; cptC < sListTypesFieldsSource.Count; cptC++)
                        {
                            try
                            {
                                iDx = sListTypesFieldsSource[cptC].ColumnIndex;

                                if (sRow.Length > cptC)
                                {
                                    if (JobParameters.TrimData)
                                    {
                                        if (sRow[iDx].Trim().StartsWith("\"") && sRow[iDx].Trim().EndsWith("\""))
                                        {
                                            sValue = sRow[iDx].Trim()[1..^1];
                                        }
                                        else
                                        {
                                            if (Regex.Match(sRow[iDx], "^" + sCSVCharSeparator + "{1}.*").Success)
                                            {
                                                sValue = sRow[iDx][1..].Trim();
                                            }
                                            else { sValue = sRow[iDx].Trim(); }
                                        }
                                    }
                                    else
                                    {
                                        if (sRow[iDx].Trim().StartsWith("\"") && sRow[iDx].Trim().EndsWith("\""))
                                        {
                                            sValue = sRow[iDx].Trim()[1..^1];
                                        }
                                        else
                                        {
                                            if (Regex.Match(sRow[iDx], "^" + sCSVCharSeparator + "{1}.*").Success)
                                            {
                                                sValue = sRow[iDx][1..];
                                            }
                                            else { sValue = sRow[iDx]; }
                                        }
                                    }

                                    if (string.IsNullOrEmpty(sValue))
                                    {
                                        dtR[cptC] = DBNull.Value;
                                    }
                                    else { dtR[cptC] = sValue; }
                                }
                                else //ajout d'une valeur vide si l'index sur la ligne est inexistant par rapport à la taille de l'entête
                                {
                                    sValue = "";
                                    dtR[cptC] = DBNull.Value;
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("Invalid Row (Row Number:", cptR.ToString(), ",Row Length:", sRow.Length.ToString(), ",Header Length:", sListTypesFieldsSource.Count.ToString(), ") - ", sFileInRamA[cptR]), SQLTools_Enums.LOG_TYPEINFO.DET);
                                }
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("ROWLEN:", sRow.Length.ToString(), "|DATAROWLEN:", dtR.Table.Columns.Count.ToString(), "|FIELDS:", sListTypesFieldsSource.Count.ToString(), "|COLIDX:", cptC.ToString(), "|ROW:", cptR.ToString()), sQ.RetryErrorOrWarning);
                                sQ.QueryErrors += 1;
                            }
                        }
                        _dtFillCSV[numThread].Rows.Add(dtR);

                        if ((DateTime.Now - dtStart).Seconds % 5 == 0 && !bFirstPass) //Log de l'avancement
                        {
                            bFirstPass = true;
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, " - WRITE " + cptR.ToString() + "/" + sFileInRamA.Length.ToString() + Toolbox.GetPercent(JobParameters.XLSRowOffset, sFileInRamA.Length, cptR), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                        else if ((DateTime.Now - dtStart).Seconds % 5 != 0)
                        {
                            bFirstPass = false;
                        }

                    }
                    else
                    {
                        iTooManyErrors += 1;
                        if (iTooManyErrors > 20)
                        {
                            Exception ex = new(string.Concat(Languages.Languages.mt_csvtodt_errors01, iTooManyErrors.ToString(), Languages.Languages.mt_csvtodt_errors02));
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "", SQLTools_Enums.LOG_TYPEINFO.ERR);
                            break;
                        }
                        else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("Invalid Row (Row Number:", cptR.ToString(), ",Row Length:", sRow == null ? "-1" : sRow.Length.ToString(), ",Header Length:", sListTypesFieldsSource.Count.ToString(), ") - ", sFileInRamA[cptR]), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                    }
                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                    break;
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
                }
            }
            _isMThreadFinished[numThread] = true;
        }

        public void IsFirstRowOfFileAnHeader(object cptT, string[] sFile, string[] sArrFirstRow, List<bool> bIsUnique, string sCSVCharSeparator, int iFileLength)
        {
            int numThread = (int)cptT;

            string[] sArrRow;

            if (IsHeaderRowUniqueInFile == null) { IsHeaderRowUniqueInFile = bIsUnique; }

            try
            {
                for (int iC = _iMTStart[numThread]; iC < _iMTStop[numThread]; iC++)
                {
                    if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    if (!string.IsNullOrEmpty(sArrFirstRow[iC])) //si on a un caractère séparateur à la fin de la ligne, on a toujours une colonne à vide, pour toutes les lignes, donc un IsUnique à false
                    {
                        for (int iR = JobParameters.CSVRowOffset + 1; iR < iFileLength; iR++)
                        {
                            if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException();
                            }

                            if (sArrFirstRow.Contains(sCSVCharSeparator)) //cas des listes mono-colonne sans header
                            {
                                try { sArrRow = Regex.Matches(sFile[iR], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray(); }
                                catch (Exception) { sArrRow = null; }

                                if (sArrRow != null && sArrRow.Length == sArrFirstRow.Length)
                                {
                                    if (sArrRow[iC].Equals(sArrFirstRow[iC]))
                                    {
                                        bIsUnique[iC] = false;
                                    }
                                }
                            }
                            else
                            {
                                bIsUnique[iC] = false;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _operationCancelled[numThread] = true;
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, ex.Message, SQLTools_Enums.LOG_TYPEINFO.ERR);
            }

            _isMThreadFinished[numThread] = true;

        }

        public void RepSyncTask(int numThread, List<Query> ListQueries, SQLTools_Enums.REPSYNC_INSERT_METHOD rsInsertMethod, bool bAvoidPerform)
        {
            //putain ça sert à quoi ça ???? Guillaume !!!!
            bool Continue = true;

            JobParameters.GlobalParameters.SetReservedColumns(new List<string>());

            List<OneDSOneQuery> dsAllInOne = new();
            List<string> sListDistinctOutput = new();

            int iLogErrorsBefore = 0;
            int iLogErrorsAfter = 0;
            bool bNoLoadErrors = true;

            int iQueryRetry = 0;

            while (ListQueries.Count(q => q.ProcessingState == 0) > 0)
            {
                Query Q = null;
                Query ProcessingQuery = null;

                lock (ListQueries)
                {
                    //gestion de la notion de nouvelle tentative
                    Q = ListQueries.FirstOrDefault(q => q.ProcessingState == 0);
                    Q.ProcessingState = 1;
                    ProcessingQuery = Q.DeepCopy();
                    ProcessingQuery.Retries = iQueryRetry;
                    ProcessingQuery.JobRetry = JobParameters.QueryRetries;
                }

                Continue = true;

                if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                { break; }

                //rustine qui me permet de savoir si une requête donnée a produit des erreurs
                if (iLogErrorsAfter > iLogErrorsBefore) { bNoLoadErrors = false; } else { bNoLoadErrors = true; }
                iLogErrorsBefore = MyLog.JobErrors;

                try
                {
                    string sNamespaceDS = "";

                    //extraction des données
                    OneDSOneQuery SourceData = RepSyncTask_Source(numThread, false, ref ProcessingQuery, ref sListDistinctOutput, JobParameters, ref MyLog);

                    if (SourceData != null && SourceData.DSDATA.Tables.Count > 0)
                    {
                        SourceData = CrossJoinBetweenSources(numThread, SourceData, ref JobParameters, ref MyLog);
                    }

                    //envoi des données en target
                    try
                    {
                        if (SourceData.DSDATA != null && SourceData.DSDATA.Tables.Count > 0)
                        {
                            foreach (DataTable dt in SourceData.DSDATA.Tables)
                            {
                                SourceData.DSQUERY.QueryAnalyzer.AddQueryProperty(dt.Namespace, SQLTools_Enums.QUERY_PROPERTIES.ROWS_RETRIEVED, dt.Rows.Count.ToString(), false);
                            }
                        }
                        else
                        {
                            SourceData.DSQUERY.QueryAnalyzer.AddQueryProperty(SourceData.DSQUERY.QueryAnalyzer.Tables[0].Name, SQLTools_Enums.QUERY_PROPERTIES.ROWS_RETRIEVED, "-1", false);
                        }

                        if (SourceData.DSDATA != null && SourceData.DSDATA.Tables.Count > 0 && SourceData.DSDATA.Tables[0].Rows.Count > 0)
                        {
                            _hasQueryData.Add(true);

                            if (rsInsertMethod == SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY)
                            {
                                Thread t2 = null;
                                Thread t1 = new(delegate () { RepSyncTask_Target(numThread, rsInsertMethod, false, new OneDSOneQuery(SourceData.DSDATA, SourceData.DSQUERY, SourceData.DSQUERY.OutputTable, sNamespaceDS), JobParameters.DeepCopy(), ref MyLog, bAvoidPerform, bNoLoadErrors, false); });
                                t1.Start();

                                if (ProcessingQuery.HasMultiTarget)
                                {
                                    //si le programme n'est pas configuré pour que les cibles soient alimentées en parallèle, on attend que la première cible ait terminé
                                    if (!JobParameters.GlobalParameters.MULTI_TARGET_IN_PARALLEL)
                                    {
                                        while (t1.ThreadState != System.Threading.ThreadState.Stopped)
                                        {
                                            Thread.Sleep(100);
                                        }
                                    }
                                    Query Qq = SourceData.DSQUERY.CreateSwapedTargetQuery();
                                    if (Qq != null)
                                    {
                                        //note : le multithreading target peut créer des problèmes d'intégrité de datatable (remplacement de colonnes pour coller à la cible par exemmple)
                                        //donc c'est pourquoi dans un cas il y a une copie, et dans l'autre, non 
                                        t2 = new Thread(delegate () { RepSyncTask_Target(numThread, rsInsertMethod, true, new OneDSOneQuery(JobParameters.GlobalParameters.MULTI_TARGET_IN_PARALLEL ? SourceData.DSDATA.Copy() : SourceData.DSDATA, Qq.DeepCopy(), SourceData.DSQUERY.OutputTable, sNamespaceDS), JobParameters.DeepCopy(), ref MyLog, bAvoidPerform, bNoLoadErrors, false); });
                                        t2.Start();
                                    }
                                }

                                while (t1.ThreadState != System.Threading.ThreadState.Stopped)
                                {
                                    Thread.Sleep(100);
                                }

                                if (t2 != null)
                                {
                                    while (t2.ThreadState != System.Threading.ThreadState.Stopped)
                                    {
                                        Thread.Sleep(100);
                                    }
                                }

                            }
                            else
                            {
                                dsAllInOne.Add(new OneDSOneQuery(SourceData.DSDATA.Copy(), SourceData.DSQUERY, SourceData.DSQUERY.OutputTable, sNamespaceDS));
                            }
                            SourceData.DSDATA.Clear();
                        }
                        else
                        {
                            if (SourceData.DSDATA == null || SourceData.DSDATA.Tables.Count == 0)
                            {
                                if (!JobParameters.DynParams_LoopThroughRows) //en cas de boucle du job par les pré-commandes, on veut pouvoir continuer le job même si l'une rdes requêtes a foiré
                                {
                                    Continue = false;
                                    Exception ex = new(string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed01, ProcessingQuery.QueryAnalyzer.Tables[0].Name, Languages.Languages.mt_repsync_queryfailed02));
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, ProcessingQuery.SQLQuery, ProcessingQuery.RetryErrorOrWarning);
                                    ProcessingQuery.QueryErrors += 1;
                                }
                            }
                            else
                            {
                                if (!JobParameters.SQLDirectStream) //en mode directquery, tout a déjà été inséré : ce n'est pas un bug que la table soit vide
                                {
                                    switch (JobParameters.NoSourceDataNoError)
                                    {
                                        case 0:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed03, ProcessingQuery.QueryAnalyzer.Tables[0].Name, Languages.Languages.mt_repsync_queryfailed02), SQLTools_Enums.LOG_TYPEINFO.INF);
                                            break;
                                        case 1:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed03, ProcessingQuery.QueryAnalyzer.Tables[0].Name, Languages.Languages.mt_repsync_queryfailed02), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                            break;
                                        case 2:
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed03, ProcessingQuery.QueryAnalyzer.Tables[0].Name, Languages.Languages.mt_repsync_queryfailed02), ProcessingQuery.RetryErrorOrWarning);
                                            ProcessingQuery.QueryErrors += 1;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Continue = false;
                        _operationCancelled[numThread] = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Continue = false;
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed04), SQLTools_Enums.LOG_TYPEINFO.ERR);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    Continue = false;
                    _operationCancelled[numThread] = true;
                    break;
                }
                catch (Exception)
                {
                    Continue = false;
                    _operationCancelled[numThread] = true;
                    break;
                }

                iLogErrorsAfter = MyLog.JobErrors;

                //si la requête a généré des erreurs, on regarde si on peut faire un retry
                if (ProcessingQuery.QueryErrors > 0 && JobParameters.QueryRetries > iQueryRetry)
                {
                    Monitoring.RenewCancellationToken(); //on a appelé le cancellationtoken dans certains cas d'erreur
                    iQueryRetry++;
                    Q.QueryAnalyzer.AddQueryProperty("RETRY", QUERY_PROPERTIES.QUERY_RETRY, iQueryRetry.ToString(), true);
                    MyLog.AddJobReportRetry(string.Concat("Query Retry : ", ProcessingQuery.OutputTable, " -> ", iQueryRetry.ToString(), " X"));
                    Q.ProcessingState = 0;
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), " -> ", Languages.Languages.mt_repsync_queryfailed_retry, " (", iQueryRetry, " / ", JobParameters.QueryRetries, ")"), SQLTools_Enums.LOG_TYPEINFO.INF);
                }
                else
                {
                    iQueryRetry = 0;
                    Q.ProcessingState = 2;
                }

                if (rsInsertMethod == SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY)
                {
                    lock (_modifiedQueries)
                    {
                        _modifiedQueries.Add(ProcessingQuery);
                    }
                }

            }

            if (Continue)
            {
                try
                {
                    if (rsInsertMethod != SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY)
                    {
                        for (int iDSName = 0; iDSName < sListDistinctOutput.Count; iDSName++)
                        {
                            DataSet dsCopy = new(sListDistinctOutput[iDSName]);
                            List<Query.QProperty> qMergeProp = new();

                            Query dsQ = null;
                            string dsOutput = null;
                            string dsNamespace = null;

                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergingdata, sListDistinctOutput[iDSName], ") - ", (iDSName + 1).ToString(), "/", sListDistinctOutput.Count.ToString()), SQLTools_Enums.LOG_TYPEINFO.INF);

                            int iT = 0;
                            int iDS = 0;

                            foreach (OneDSOneQuery dsS in dsAllInOne)
                            {
                                iDS++;

                                //création d'un jeu de propriétés complet (cas des fichiers mergés qu'on doit zipper/déplacer/supprimer à la fin du traitement)
                                if (iDS < dsAllInOne.Count) //on ne merge pas les propriétés du dernier bloc puisque ce sera fait plus bas => dsQ.QueryAnalyzer.MergeProperties(qMergeProp);
                                {
                                    qMergeProp = MergeProperties(qMergeProp, dsS);
                                }

                                if (dsS.DSOUTPUT.Equals(sListDistinctOutput[iDSName], StringComparison.InvariantCultureIgnoreCase))
                                {
                                    dsQ = dsS.DSQUERY;
                                    dsOutput = dsS.DSOUTPUT;
                                    dsNamespace = dsS.DSNAMESPACE;
                                    dsCopy.Namespace = dsS.DSNAMESPACE;
                                    iT++;

                                    switch (rsInsertMethod)
                                    {
                                        case SQLTools_Enums.REPSYNC_INSERT_METHOD.ALL_IN_ONE:
                                            foreach (DataTable dtS in dsS.DSDATA.Tables)
                                            {
                                                iT++;
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergingcopydt, dtS.Namespace, ")"), SQLTools_Enums.LOG_TYPEINFO.DET);
                                                if (dsCopy.Tables.Contains(dtS.TableName)) { dsCopy.Tables[dtS.TableName].TableName = string.Concat(dtS.TableName, "_", iT.ToString("00")); }
                                                dsCopy.Tables.Add(dtS.Copy());
                                            }
                                            break;
                                        case SQLTools_Enums.REPSYNC_INSERT_METHOD.MERGE:
                                            foreach (DataTable dtS in dsS.DSDATA.Tables)
                                            {
                                                iT++;
                                                //dtS.TableName = string.Concat(dtS.TableName, "_", iT.ToString("00"));
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergingdt, dtS.Namespace, ")"), SQLTools_Enums.LOG_TYPEINFO.DET);

                                                if (dsCopy.Tables.Count == 0)
                                                {
                                                    dsCopy.Tables.Add(dtS.Clone());
                                                }
                                                else
                                                {
                                                    int iQteCol = dsCopy.Tables[0].Columns.Count;
                                                    //subtilité : lors de l'analyse indépendante des fichiers multiples, les types détectés peuvent être différents, on procède donc à la conversion si besoin
                                                    for (int iC = 0; iC < iQteCol; iC++)
                                                    {
                                                        bool bHasColumn = false;
                                                        foreach (DataColumn col in dtS.Columns)
                                                        {
                                                            if (col.ColumnName.Equals(dsCopy.Tables[0].Columns[iC].ColumnName, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                bHasColumn = true;
                                                                break;
                                                            }
                                                        }
                                                        if (bHasColumn)
                                                        {
                                                            if (dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].DataType != dsCopy.Tables[0].Columns[iC].DataType)
                                                            {
                                                                string dtType1 = dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].DataType.ToString();
                                                                string dtType2 = dsCopy.Tables[0].Columns[iC].DataType.ToString();
                                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                                                                                string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergetypemismatch, dtType1.ToString(), "<>", dtType2.ToString(), "] : ", dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].ColumnName, " -> Conversion"), SQLTools_Enums.LOG_TYPEINFO.DET);

                                                                //le "string" est prioritaire sur tout le reste
                                                                if (dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].DataType == Type.GetType("System.String"))
                                                                //{ Toolbox.ConvertColumnType(dsCopy.Tables[0], dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName], dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].DataType); }
                                                                {
                                                                    Toolbox.ConvertColumnType(dsCopy.Tables[0], dsCopy.Tables[0].Columns[dsCopy.Tables[0].Columns[iC].ColumnName], dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].DataType);
                                                                }
                                                                //else { Toolbox.ConvertColumnType(dtS, dsCopy.Tables[0].Columns[iC], dsCopy.Tables[0].Columns[iC].DataType); }
                                                                else { Toolbox.ConvertColumnType(dtS, dtS.Columns[iC], dsCopy.Tables[0].Columns[iC].DataType); }
                                                            }
                                                        }
                                                        else { dtS.Columns.Add(dsCopy.Tables[0].Columns[iC].ColumnName, dsCopy.Tables[0].Columns[iC].DataType); }
                                                    }
                                                    //subtilité : analyse des DBNull
                                                    for (int iC = 0; iC < iQteCol; iC++)
                                                    {
                                                        bool bHasColumn = false;
                                                        foreach (DataColumn col in dtS.Columns)
                                                        {
                                                            if (col.ColumnName.Equals(dsCopy.Tables[0].Columns[iC].ColumnName, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                bHasColumn = true;
                                                                break;
                                                            }
                                                        }

                                                        if (bHasColumn)
                                                        {
                                                            if (dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].AllowDBNull != dsCopy.Tables[0].Columns[iC].AllowDBNull)
                                                            {
                                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                                                                                 string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergeallownull, dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].ColumnName, " -> Conversion"), SQLTools_Enums.LOG_TYPEINFO.DET);

                                                                if (dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].AllowDBNull)
                                                                {
                                                                    dsCopy.Tables[0].Columns[iC].AllowDBNull = true;
                                                                }
                                                                else { dtS.Columns[dsCopy.Tables[0].Columns[iC].ColumnName].AllowDBNull = true; }
                                                            }
                                                        }
                                                        else { dsCopy.Tables[0].Columns[iC].AllowDBNull = true; }
                                                    }

                                                    //si on a sur le dscopy une colonne qui manque par rapport au datatable actuel il faut autoriser le null
                                                    for (int iC = 0; iC < dtS.Columns.Count; iC++)
                                                    {
                                                        bool bHasColumn = false;
                                                        foreach (DataColumn col in dsCopy.Tables[0].Columns)
                                                        {
                                                            if (col.ColumnName.Equals(dtS.Columns[iC].ColumnName, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                bHasColumn = true;
                                                                break;
                                                            }
                                                        }

                                                        if (!bHasColumn)
                                                        {
                                                            dtS.Columns[iC].AllowDBNull = true;
                                                        }
                                                    }
                                                }
                                                try
                                                {
                                                    if (!JobParameters.ConnectionString_Source.SConnDriverSuffix.Equals("DB"))
                                                    {
                                                        //attention méthode foireuse qui consiste en cas de merge de plein de fichiers, si un fichier est vide, la première ligne pouvait être juste un entête et
                                                        //l'analyseur l'a vue comme une ligne donc imagine un entête COLUMN_1, COLUMN_2...etc et risque de compromettre l'intégrité du dataset
                                                        bool bOK = CheckMergeBetweenTwoDT(dsCopy.Tables[0], dtS);

                                                        if (bOK)
                                                        {
                                                            dsCopy.Tables[0].Merge(dtS);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        dsCopy.Tables[0].Merge(dtS);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    string[] sListcolumnNames = (from dc in dtS.Columns.Cast<DataColumn>() select dc.ColumnName).ToArray();
                                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_mergeko01, dtS.TableName, Languages.Languages.mt_repsync_mergeko02, string.Join(",", sListcolumnNames), ")"), dsQ.RetryErrorOrWarning);
                                                    dsQ.QueryErrors += 1;
                                                }
                                            }
                                            break;
                                    }
                                }
                            }

                            if (dsCopy != null && dsCopy.Tables.Count > 0 && dsCopy.Tables[0].Rows.Count > 0)
                            {
                                //si le premier fichier avant 4 colonnes + les colonnes spéciales et que le deuxième a 6 colonnes, les colonnes spéciales vont être intercalées en plein milieu et foirer la suite
                                foreach (DataTable dt in dsCopy.Tables)
                                {
                                    ReorderDsCopy(dt);
                                }

                                //dsQ.QueryAnalyzer.ReplaceProperties(qMergeProp);

                                Thread t2 = null;
                                Thread t1 = new(delegate () { RepSyncTask_Target(numThread, rsInsertMethod, false, new OneDSOneQuery(dsCopy, dsQ, dsOutput, dsNamespace), JobParameters.DeepCopy(), ref MyLog, bAvoidPerform, MyLog.HasNoErrors, false); });
                                t1.Start();

                                if (dsQ.HasMultiTarget)
                                {
                                    //si le programme n'est pas configuré pour que les cibles soient alimentées en parallèle, on attend que la première cible ait terminé
                                    if (!JobParameters.GlobalParameters.MULTI_TARGET_IN_PARALLEL)
                                    {
                                        while (t1.ThreadState != System.Threading.ThreadState.Stopped)
                                        {
                                            Thread.Sleep(100);
                                        }
                                    }

                                    Query Q = dsQ.CreateSwapedTargetQuery();
                                    if (Q != null)
                                    {
                                        t2 = new Thread(delegate () { RepSyncTask_Target(numThread, rsInsertMethod, true, new OneDSOneQuery(dsCopy, Q, Q.OutputTable, dsNamespace), JobParameters.DeepCopy(), ref MyLog, bAvoidPerform, MyLog.HasNoErrors, false); });
                                        t2.Start();
                                    }
                                }

                                while (t1.ThreadState != System.Threading.ThreadState.Stopped)
                                {
                                    Thread.Sleep(100);
                                }

                                if (t2 != null)
                                {
                                    while (t2.ThreadState != System.Threading.ThreadState.Stopped)
                                    {
                                        Thread.Sleep(100);
                                    }
                                }

                                dsCopy.Clear();
                            }
                            else
                            {
                                switch (JobParameters.NoSourceDataNoError)
                                {
                                    case 0:
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_queryfailed03, dsOutput, Languages.Languages.mt_repsync_queryfailed02), SQLTools_Enums.LOG_TYPEINFO.INF);
                                        break;
                                    case 1:
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_queryfailed03, dsOutput, Languages.Languages.mt_repsync_queryfailed02), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        break;
                                    case 2:
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_repsync_queryfailed03, dsOutput, Languages.Languages.mt_repsync_queryfailed02), dsQ.RetryErrorOrWarning);
                                        dsQ.QueryErrors += 1;
                                        break;
                                }
                            }

                            if (dsQ != null) //dsQ est null quand il n'y a pas eu de donnée processée
                            {
                                dsQ.QueryAnalyzer.MergeProperties(qMergeProp);
                                lock (_modifiedQueries)
                                {
                                    _modifiedQueries.Add(dsQ);
                                }
                            }
                        }

                        dsAllInOne.Clear();

                    }
                }
                catch (OperationCanceledException)
                {
                    _operationCancelled[numThread] = true;
                }
                catch (Exception)
                {
                    _operationCancelled[numThread] = true;
                }
                _isMThreadFinished[numThread] = true;
            }
            else { _isMThreadFinished[numThread] = true; }
        }

        private List<QProperty> MergeProperties(List<QProperty> qMergeProp, OneDSOneQuery dsS)
        {
            foreach (QProperty newq in dsS.DSQUERY.QueryAnalyzer.QueryProperties)
            {
                QProperty oldq = qMergeProp.FirstOrDefault(p => p.Property == newq.Property);
                if (oldq != null)
                {
                    int oldv = 0;
                    int newv = 0;
                    if (int.TryParse(oldq.Value.Replace("!", "").Trim(), out oldv) && int.TryParse(newq.Value.Replace("!", "").Trim(), out newv))
                    {
                        oldq.Value = (oldv + newv).ToString();
                    }
                    else
                    {
                        qMergeProp.Add(newq);
                    }
                }
                else
                {
                    qMergeProp.Add(newq);
                }
            }
            return qMergeProp;
        }

        private void ReorderDsCopy(DataTable dt)
        {
            List<string> sColsToReorder = new();

            foreach (DataColumn dc in dt.Columns)
            {
                if ((JobParameters.OptionalDBName_OnInsert && dc.ColumnName.Equals(JobParameters.TargetAddDbName)) ||
                    (JobParameters.OptionalTimestamp_OnInsert && dc.ColumnName.Equals(JobParameters.TargetAddDtLoad)) ||
                    // (dc.ColumnName.Equals(JobParameters.TargetAddRowNum) && JobParameters.OptionalRowID_OnInsert) ||   ROWNUM est toujours en premier
                    (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0 && JobParameters.OptionalDynamicParamField_OnInsert.Contains(dc.ColumnName)))
                {
                    sColsToReorder.Add(dc.ColumnName);
                }
            }

            foreach (string sCol in sColsToReorder)
            {
                dt.Columns[sCol].SetOrdinal(dt.Columns.Count - 1);
            }

            if (JobParameters.OptionalRowID_OnInsert && dt.Columns.Contains(JobParameters.TargetAddRowNum))
            {
                dt.Columns[JobParameters.TargetAddRowNum].SetOrdinal(0);
            }
        }

        private bool CheckMergeBetweenTwoDT(DataTable dtCopy, DataTable dtS)
        {
            if (dtS.Rows.Count == 1 && dtS.Columns.Contains("COLUMN_1")) //une ligne c'est suspect, surtout si elle a des noms de colonne chelou. On peut considérer que c'est un entête de fichier passé en ligne
            {
                //suppression des colonnes optionnelles qui vont créer des données foireuses -----------------------------------------
                List<DataColumn> sColsToRemoveFromDT = new();

                foreach (DataColumn dc in dtS.Columns)
                {
                    if ((JobParameters.OptionalDBName_OnInsert && dc.ColumnName.Equals(JobParameters.TargetAddDbName)) ||
                        (JobParameters.OptionalTimestamp_OnInsert && dc.ColumnName.Equals(JobParameters.TargetAddDtLoad)) ||
                        (JobParameters.OptionalRowID_OnInsert && dc.ColumnName.Equals(JobParameters.TargetAddRowNum)) ||
                        (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0 && JobParameters.OptionalDynamicParamField_OnInsert.Contains(dc.ColumnName)))
                    {
                        sColsToRemoveFromDT.Add(dc);
                    }
                }

                foreach (DataColumn dc in sColsToRemoveFromDT) { dtS.Columns.Remove(dc); }
                //suppression des colonnes optionnelles qui vont créer des données foireuses -----------------------------------------
            }

            List<string> sCols = new();
            List<string> sColsToBeMerged = dtS.Rows[0].ItemArray.Select(x => x.ToString()).OrderBy(q => q).ToList();
            sColsToBeMerged.RemoveAll(x => string.IsNullOrEmpty(x));

            //contrôle de colonnes qui auraient d'un côté le nom en majuscule et l'autre en minuscule : la fusion (Merge)
            //maintiendra les 2 colonnes, ce qui n'est pas souhaité du tout
            foreach (DataColumn dc1 in dtCopy.Columns)
            {
                foreach (DataColumn dc2 in dtS.Columns)
                {
                    if (dc1.ColumnName.Equals(dc2.ColumnName, StringComparison.OrdinalIgnoreCase) && !dc1.ColumnName.Equals(dc2.ColumnName))
                    {
                        dc2.ColumnName = dc1.ColumnName;
                    }
                    if (dc1.ColumnName.Equals(dc2.ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        //vérification des incohérences
                        if (dc1.AllowDBNull != dc2.AllowDBNull)
                        {
                            if (!dc1.AllowDBNull)
                            { dc1.AllowDBNull = true; }
                            else if (!dc2.AllowDBNull)
                            { dc2.AllowDBNull = true; }
                        }
                        if (dc1.MaxLength != dc2.MaxLength)
                        {
                            if (dc1.MaxLength < dc2.MaxLength)
                            { dc1.MaxLength = dc2.MaxLength; }
                            else
                            { dc2.MaxLength = dc1.MaxLength; }
                        }
                    }
                }
            }

            foreach (DataColumn dc in dtCopy.Columns)
            {
                sCols.Add(dc.ColumnName);
            }

            sCols = sCols.OrderBy(q => q).ToList();

            if (dtS.Rows.Count == 1 && !sColsToBeMerged.Except(sCols).Any())
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static List<DataSet> GetSourceData(int numThread, Job JobParameters, ref Query sQ, LogTools MyLog, bool bWithSynchroData)
        {
            JobParameters.GlobalParameters.SetReservedColumns(new List<string>());
            List<string> sListDistinct = new();

            try
            {
                OneDSOneQuery SourceData = RepSyncTask_Source(0, false, ref sQ, ref sListDistinct, JobParameters, ref MyLog);

                if (SourceData != null && SourceData.DSDATA.Tables.Count > 0)
                {
                    SourceData = CrossJoinBetweenSources(numThread, SourceData, ref JobParameters, ref MyLog);
                }

                DataSet DsSynchro = new();

                if (JobParameters.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                {
                    if (bWithSynchroData)
                    {
                        if (SourceData.DSDATA != null && SourceData.DSDATA.Tables.Count > 0)
                        {
                            DsSynchro = RepSyncTask_Target(numThread, SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY, false, SourceData, JobParameters.DeepCopy(), ref MyLog, true, MyLog.HasNoErrors, false);
                        }
                    }
                }

                if (DsSynchro.Tables.Count > 0)
                {
                    return new List<DataSet> { SourceData.DSDATA, DsSynchro };
                }
                else
                {
                    return new List<DataSet> { SourceData.DSDATA };
                }
            }
            catch (OperationCanceledException)
            { return new List<DataSet>(); }
            catch (Exception)
            {
                return new List<DataSet>();
            }

        }

        #endregion

        #region "PRIVATE VOID"

        private static OneDSOneQuery RepSyncTask_Source(int numThread, bool bSubSource, ref Query sQ, ref List<string> sListDistinctOutput, Job JobParameters, ref LogTools MyLog)
        {
            _hasQueryData.Clear();
            Job INIP = JobParameters.DeepCopy();

            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_start, sQ.QueryAnalyzer.Tables[0].Alias, ")"), SQLTools_Enums.LOG_TYPEINFO.INF);

            //
            if (MyLog.DebugFunctions)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                string.Concat("T", (numThread + 1).ToString("00"), " Creating Dataset"), SQLTools_Enums.LOG_TYPEINFO.DBG);
            }
            //

            OneDSOneQuery dSAndQuery = new(new DataSet(), sQ, "", "");

            //
            if (MyLog.DebugFunctions)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                string.Concat("T", (numThread + 1).ToString("00"), " Cloning Source Connection"), SQLTools_Enums.LOG_TYPEINFO.DBG);
            }
            //

            CONNString CSold = new(INIP.ConnectionString_Source.SConnDriver,
                                                INIP.ConnectionString_Source.SConnID,
                                                INIP.ConnectionString_Source.SConnName,
                                                INIP.ConnectionString_Source.SConnString(null),
                                                INIP.ConnectionString_Source.SConnParams);
            string DBSold = (string)INIP.DatabaseName_Source.Clone();

            //
            if (MyLog.DebugFunctions)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                string.Concat("T", (numThread + 1).ToString("00"), " Set Analyzer Params"), SQLTools_Enums.LOG_TYPEINFO.DBG);
            }
            //

            //***************************************************************************
            if (sQ.CrossJoinQueries.Count > 0) //on ne peut pas effectuer de cross-queries si on utilise une méthode d'insertion partielle
            {
                INIP.SQLDirectStream = false;
                INIP.GlobalParameters.SetAnalyzerParams(INIP.GlobalParameters.FILE_MAX_ROWS_ANALYZER, 999999999);
            }
            //de la même manière, pas de jointure sans les données complètes
            if (dSAndQuery.DSQUERY.ConnectionSrc.SConnDriverSuffix.Equals("FI") && sQ.QueryAnalyzer.Tables.Count > 1)
            {
                INIP.SQLDirectStream = false;
                INIP.GlobalParameters.SetAnalyzerParams(INIP.GlobalParameters.FILE_MAX_ROWS_ANALYZER, 999999999);
            }
            //***************************************************************************

            //note : pas besoin de faire de deepcopy de INIP car les sources sotn récupérées les unes après les autres, pas en parallèle
            if (bSubSource)
            {
                INIP.ConnectionString_Source = sQ.ConnectionSrc; //en cas de cross-join, la connection n'est pas celle du job
                INIP.DatabaseName_Source = sQ.ConnectionSrc.SConnDB;
            }

            try
            {
                switch (dSAndQuery.DSQUERY.ConnectionSrc.SConnDriverSuffix)
                {
                    case "DB":
                        SQLTools SQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);

                        //en mode direct stream je regarde si la table cible existe pour pouvoir gérer l'offset des 99999 lignes de la parallélisation                        
                        bool bTableExists = false;
                        if (JobParameters.SQLDirectStream)
                        {
                            if (dSAndQuery.DSQUERY.ConnectionTrg != null) //null si on est en sandbox
                            {
                                if (dSAndQuery.DSQUERY.ConnectionTrg.SConnDriverSuffix.Equals("DB"))
                                {
                                    try
                                    {
                                        Job jpT = INIP.DeepCopy();
                                        jpT.ConnectionString_Target = dSAndQuery.DSQUERY.ConnectionTrg;
                                        SQLTools SQLTarget = new(jpT, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                                        bTableExists = SQLTarget.CheckForExistingTable(dSAndQuery.DSQUERY.OutputTable, dSAndQuery.DSQUERY);
                                        jpT = null;
                                        SQLTarget = null;
                                    }
                                    catch { }
                                }
                                else { bTableExists = true; }
                            }
                            else { bTableExists = true; }

                            if (dSAndQuery.DSQUERY.HasMultiTarget)
                            {
                                Query qTB = dSAndQuery.DSQUERY.CreateSwapedTargetQuery();
                                if (qTB.ConnectionTrg.SConnDriverSuffix.Equals("DB"))
                                {
                                    try
                                    {
                                        Job jpT = INIP.DeepCopy();
                                        jpT.ConnectionString_Target = dSAndQuery.DSQUERY.ConnectionTrg;
                                        SQLTools SQLTarget = new(jpT, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                                        bTableExists = SQLTarget.CheckForExistingTable(dSAndQuery.DSQUERY.OutputTable, dSAndQuery.DSQUERY);
                                        jpT = null;
                                        SQLTarget = null;
                                        bTableExists = false;
                                    }
                                    catch { }
                                }
                            }
                        }

                        dSAndQuery.DSDATA = SQL.GetDataFromDatabase(dSAndQuery.DSQUERY, bTableExists).Result;

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            dSAndQuery.ChangeBitArrayColumns(numThread, 0, INIP, ref MyLog);
                            dSAndQuery.DataTransform(numThread, 0, INIP, ref MyLog);

                            dSAndQuery.DSNAMESPACE = Toolbox.InitDsDtNames(dSAndQuery.DSDATA, INIP, dSAndQuery.DSQUERY);
                            //ajout des colonnes optionnelles
                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                            {
                                Toolbox.AddOptionalColumnsInDatasetFromINI(dSAndQuery.DSDATA.Tables[iT], INIP, sQ, ref MyLog);
                            }

                            dSAndQuery.AddOriginalColumnName(numThread, 0, INIP, ref MyLog);
                            dSAndQuery.SetNullFields(numThread, 0, INIP, ref MyLog); // passage des colonnes NULL en DBNull
                            dSAndQuery.FillSHSFieldsWithDataSet(numThread, 0, INIP, ref MyLog);
                        }
                        break;

                    case "FI":
                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T", (numThread + 1).ToString("00"), " Creating File Connection"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //

                        FITools FILE = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);

                        //
                        if (MyLog.DebugFunctions)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                            string.Concat("T", (numThread + 1).ToString("00"), " Opening FI Class"), SQLTools_Enums.LOG_TYPEINFO.DBG);
                        }
                        //
                        dSAndQuery.DSDATA = FILE.GetDataFromFile(INIP.ConnectionString_Source.SConnDriver, ref dSAndQuery.DSQUERY);

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            //gestion du UNION
                            if (dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count > 0)
                            {
                                for (int iU = 0; iU < dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count; iU++)
                                {
                                    Query QUnion = new(INIP, dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries[iU]);
                                    DataSet dsUnion = FILE.GetDataFromFile(INIP.ConnectionString_Source.SConnDriver, ref QUnion);
                                    if (dsUnion != null && dsUnion.Tables.Count > 0)
                                    {
                                        try
                                        {
                                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                            {
                                                dSAndQuery.DSDATA.Tables[iT].Merge(dsUnion.Tables[iT]);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_invalidunion), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                                            dSAndQuery.DSQUERY.QueryErrors += 1;
                                        }
                                    }
                                }
                            }

                            dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);
                        }
                        //sNamespaceDS = sQ.InputTableAlias.Replace("\\", "_");
                        break;

                    case "WS":

                        Query q = dSAndQuery.DSQUERY;
                        Query newQuery = null;
                        Query.QTable qT = q.QueryAnalyzer.Tables[0];
                        while (qT.IsSubQuery)
                        {
                            newQuery = new Query(JobParameters, qT.Name);
                            qT = newQuery.QueryAnalyzer.Tables[0];
                        }
                        if (newQuery != null)
                        { q = newQuery; }

                        WSTools WS = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        dSAndQuery.DSDATA = WS.GetDataFromWebService(q).Result;

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            switch (INIP.WebserviceSQLLanguage)
                            {
                                case SQLTools_Enums.WEBSERVICE_SQL.NXQL:
                                    dSAndQuery.ChangeBitArrayColumns(numThread, 0, INIP, ref MyLog);

                                    int iProcessedTableWS = dSAndQuery.DSDATA.Tables.Count > dSAndQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle ? dSAndQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle : -1;
                                    dSAndQuery.AddOriginalColumnName(numThread, iProcessedTableWS, INIP, ref MyLog);
                                    dSAndQuery.SetNullFields(numThread, iProcessedTableWS, INIP, ref MyLog); // passage des colonnes NULL en DBNull
                                    dSAndQuery.FillSHSFieldsWithDataSet(numThread, iProcessedTableWS, INIP, ref MyLog);
                                    dSAndQuery.DataTransform(numThread, iProcessedTableWS, INIP, ref MyLog);
                                    dSAndQuery.LimitResults(numThread, iProcessedTableWS, INIP, ref MyLog);
                                    break;

                                case SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL:
                                    if (dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count > 0)
                                    {
                                        for (int iU = 0; iU < dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count; iU++)
                                        {
                                            Query QUnion = new(INIP, dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries[iU]);

                                            q = QUnion;
                                            newQuery = null;
                                            qT = q.QueryAnalyzer.Tables[0];
                                            while (qT.IsSubQuery)
                                            {
                                                newQuery = new Query(JobParameters, qT.Name);
                                            }
                                            if (newQuery != null)
                                            { q = newQuery; }

                                            DataSet dsUnion = WS.GetDataFromWebService(q).Result;
                                            if (dsUnion != null && dsUnion.Tables.Count > 0)
                                            {
                                                try
                                                {
                                                    for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                                    {
                                                        dSAndQuery.DSDATA.Tables[iT].Merge(dsUnion.Tables[iT]);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_invalidunion), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                                                    dSAndQuery.DSQUERY.QueryErrors += 1;
                                                }
                                            }
                                        }
                                    }
                                    dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);
                                    break;

                                case SQLTools_Enums.WEBSERVICE_SQL.SOQL:
                                    break;
                                case SQLTools_Enums.WEBSERVICE_SQL.OQL:
                                    dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);
                                    break;
                                case SQLTools_Enums.WEBSERVICE_SQL.GRAPHQL:
                                    dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);
                                    break;

                            }

                            if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                            {
                                dSAndQuery.DSNAMESPACE = Toolbox.InitDsDtNames(dSAndQuery.DSDATA, INIP, dSAndQuery.DSQUERY);
                                //ajout des colonnes optionnelles
                                for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                {
                                    Toolbox.AddOptionalColumnsInDatasetFromINI(dSAndQuery.DSDATA.Tables[iT], INIP, sQ, ref MyLog);
                                }
                            }
                        }

                        break;

                    case "MB":
                        MailTools MAIL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        dSAndQuery.DSDATA = MAIL.GetDataFromMailBox(dSAndQuery.DSQUERY).Result;

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            //gestion du UNION
                            if (dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count > 0)
                            {
                                for (int iU = 0; iU < dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count; iU++)
                                {
                                    Query QUnion = new(INIP, dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries[iU]);
                                    DataSet dsUnion = MAIL.GetDataFromMailBox(QUnion).Result;
                                    if (dsUnion != null && dsUnion.Tables.Count > 0)
                                    {
                                        try
                                        {
                                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                            {
                                                dSAndQuery.DSDATA.Tables[iT].Merge(dsUnion.Tables[iT]);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_invalidunion), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                                            dSAndQuery.DSQUERY.QueryErrors += 1;
                                        }
                                    }
                                }
                            }

                            dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);

                        }

                        break;

                    case "AD":
                        ADTools AD = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);

                        Query qAD = dSAndQuery.DSQUERY;
                        Query newQueryAD = null;
                        Query.QTable qTAD = qAD.QueryAnalyzer.Tables[0];
                        while (qTAD.IsSubQuery)
                        {
                            newQueryAD = new Query(JobParameters, qTAD.Name);
                            qTAD = newQueryAD.QueryAnalyzer.Tables[0];
                        }
                        if (newQueryAD != null)
                        { qAD = newQueryAD; }

                        dSAndQuery.DSDATA = AD.GetDataFromAD(ref qAD);

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            //gestion du UNION
                            if (dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count > 0)
                            {
                                for (int iU = 0; iU < dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count; iU++)
                                {
                                    Query QUnion = new(INIP, dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries[iU]);

                                    qAD = QUnion;
                                    newQueryAD = null;
                                    qTAD = qAD.QueryAnalyzer.Tables[0];
                                    while (qTAD.IsSubQuery)
                                    {
                                        newQueryAD = new Query(JobParameters, qTAD.Name);
                                    }
                                    if (newQueryAD != null)
                                    { qAD = newQueryAD; }

                                    DataSet dsUnion = AD.GetDataFromAD(ref qAD);
                                    if (dsUnion != null && dsUnion.Tables.Count > 0)
                                    {
                                        try
                                        {
                                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                            {
                                                dSAndQuery.DSDATA.Tables[iT].Merge(dsUnion.Tables[iT]);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_invalidunion), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                                            dSAndQuery.DSQUERY.QueryErrors += 1;
                                        }
                                    }
                                }
                            }
                            dSAndQuery.ReworkDataSet(numThread, INIP, ref MyLog);
                        }

                        break;

                    case "NS":
                        NOSQLTools NOSQL = new(INIP, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        dSAndQuery.DSDATA = NOSQL.GetDataFromMongoDB(ref dSAndQuery.DSQUERY);

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            //gestion du UNION
                            if (dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count > 0)
                            {
                                for (int iU = 0; iU < dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries.Count; iU++)
                                {
                                    Query QUnion = new(INIP, dSAndQuery.DSQUERY.QueryAnalyzer.UnionQueries[iU]);
                                    DataSet dsUnion = NOSQL.GetDataFromMongoDB(ref QUnion);
                                    if (dsUnion != null && dsUnion.Tables.Count > 0)
                                    {
                                        try
                                        {
                                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                                            {
                                                dSAndQuery.DSDATA.Tables[iT].Merge(dsUnion.Tables[iT]);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_invalidunion), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                                            dSAndQuery.DSQUERY.QueryErrors += 1;
                                        }
                                    }
                                }
                            }

                            if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                            {
                                dSAndQuery.DSNAMESPACE = Toolbox.InitDsDtNames(dSAndQuery.DSDATA, INIP, dSAndQuery.DSQUERY);
                            }

                            int iProcessedTableNS = dSAndQuery.DSDATA.Tables.Count > dSAndQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle ? dSAndQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle : -1;
                            //on ne fait pas tout le rework car la classe MongoDB procède déjà au filtrage des données (on essaie toujours de filtrer à priori et non à postériori)
                            dSAndQuery.ChangeBitArrayColumns(numThread, iProcessedTableNS, INIP, ref MyLog);
                            dSAndQuery.AddOriginalColumnName(numThread, iProcessedTableNS, INIP, ref MyLog); //appose en information additionnelle de chaque colonne leur nom "raw" d'origine
                            dSAndQuery.ApplySQLTransformations(numThread, iProcessedTableNS, INIP, ref MyLog); //transformations SQL sur pseudo SQL language
                            dSAndQuery.PseudoMathOperations(numThread, iProcessedTableNS, INIP, ref MyLog);
                            dSAndQuery.RenameColumnsWithAliasesAndGetUnused(numThread, iProcessedTableNS, INIP, ref MyLog); //supprime les colonnes qui ne sont pas dans la requête (sauf si un select *)
                            dSAndQuery.RemoveDuplicatesFromDataTable(numThread, iProcessedTableNS, INIP, ref MyLog); //en cas de select distinct
                            dSAndQuery.SetNullFields(numThread, iProcessedTableNS, INIP, ref MyLog); // passage des colonnes NULL en DBNull
                            dSAndQuery.FillSHSFieldsWithDataSet(numThread, iProcessedTableNS, INIP, ref MyLog);
                            dSAndQuery.SetColumnsOrderFromQuery(numThread, iProcessedTableNS, INIP, ref MyLog);
                            dSAndQuery.DataTransform(numThread, iProcessedTableNS, INIP, ref MyLog); //transforme le résultat selon les méthodes supportées
                        }

                        break;
                }

                if (sListDistinctOutput != null)
                {
                    if (!sListDistinctOutput.Contains(dSAndQuery.DSQUERY.OutputTable))
                    {
                        sListDistinctOutput.Add(dSAndQuery.DSQUERY.OutputTable);
                    }
                }

                if (JobParameters.OptionalRowID_OnInsert)
                {
                    foreach (DataTable dt in dSAndQuery.DSDATA.Tables)
                    {
                        if (dt.Columns.Contains(JobParameters.TargetAddRowNum))
                        {
                            //positionnement de la colonne d'ID en premier
                            int iOrdinal = dt.Columns[JobParameters.TargetAddRowNum].Ordinal;
                            dt.Columns[JobParameters.TargetAddRowNum].SetOrdinal(0);
                            //modification du query analyzer pour positionner la colonne aussi dans le bon ordre
                            dSAndQuery.DSQUERY.QueryAnalyzer.ChangeColumnPosition(JobParameters.TargetAddRowNum, 1);
                        }
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, oce, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_aborted), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                dSAndQuery.DSQUERY.QueryErrors += 1;
                return null;
            }
            catch (Exception ex)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_aborted), dSAndQuery.DSQUERY.RetryErrorOrWarning);
                dSAndQuery.DSQUERY.QueryErrors += 1;
            }

            if (!Monitoring.TaskCancellationToken.IsCancellationRequested)
            {
                if (bSubSource)
                {
                    INIP.ConnectionString_Source = CSold; //en cas de cross-join, la connection n'est pas celle du job, on remet l'ancienne valeur
                    INIP.DatabaseName_Source = DBSold;
                }

                //suppression des tables non demandées si SELECT TABLE x ONLY
                if (dSAndQuery.DSDATA.Tables.Count > 0 && dSAndQuery.DSQUERY.QueryAnalyzer.GetOneTableOnlyFromSource)
                {
                    dSAndQuery.DSQUERY.QueryAnalyzer.ChangeSourceTableToHandle(dSAndQuery.DSDATA);
                }

                JobParameters.SQLDirectStream = INIP.SQLDirectStream; //si on a inséré les données en mode direct stream, on doit pouvoir les bidouiller ici

                return dSAndQuery;
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, new OperationCanceledException(), string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_getdata_aborted), SQLTools_Enums.LOG_TYPEINFO.ERR);
                return null;
            }
        }

        private static OneDSOneQuery CrossJoinBetweenSources(int numThread, OneDSOneQuery OriginalSource, ref Job JobParameters, ref LogTools MyLog)
        {
            int iDataTransform = JobParameters.HyperFileArrayFieldTransformation; //on ne fait pas de transformation sur les subqueries
            if (!JobParameters.DataTransformAlsoCrossQueries) { JobParameters.HyperFileArrayFieldTransformation = 0; }

            List<List<string>> ListManualJoins = new();
            List<OneDSOneQuery> ListJoinSources = new();
            List<SQLTools_Enums.CROSSJOIN_TYPES> ListCrossJoinTypes = new();
            List<string> sListDistinct = new();

            foreach (Query qCJ in OriginalSource.DSQUERY.CrossJoinQueries)
            {
                try
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                    string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_crossjoin_perform, qCJ.ConnectionSrc.SConnDriver.ToString(), "] - ", qCJ.QueryAnalyzer.Tables[0].Alias), SQLTools_Enums.LOG_TYPEINFO.INF);
                    Query Q1 = qCJ;
                    ListJoinSources.Add(RepSyncTask_Source(0, true, ref Q1, ref sListDistinct, JobParameters, ref MyLog));

                    ListCrossJoinTypes.Add(qCJ.CrossJoinType);
                    if (qCJ.CrossQueryForceJoinFields.Count > 0) { ListManualJoins.Add(qCJ.CrossQueryForceJoinFields); }
                }
                catch (OperationCanceledException)
                {
                    JobParameters.HyperFileArrayFieldTransformation = iDataTransform;
                    throw;
                }
                catch (Exception)
                {
                    JobParameters.HyperFileArrayFieldTransformation = iDataTransform;
                    throw;
                }
            }

            try
            {
                //recherche de la ou des colonnes identiques pour faire la jointure
                int iSubQ = -1;
                foreach (OneDSOneQuery SubQuery in ListJoinSources)
                {
                    iSubQ++;
                    List<DataColumn> ListDCJoin = new();

                    if (SubQuery.DSDATA.Tables.Count > 0)
                    {
                        List<string> sListColumns = new();

                        List<string> mJoin = ListManualJoins.Count == 0 ? new List<string>() : ListManualJoins[iSubQ];
                        mJoin = mJoin.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

                        if (mJoin.Count > 0)
                        {

                            int iOK = 0;
                            foreach (string sC in mJoin)
                            {
                                if (OriginalSource.DSDATA.Tables[OriginalSource.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns.Contains(sC)
                                    && SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns.Contains(sC)) //on vérifie quand même que les colonnes saisies manuellement dans le job sont valides
                                {
                                    iOK++;
                                }
                            }
                            if (iOK == mJoin.Count)
                            {
                                sListColumns = mJoin;
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_crossjoin_manualjoinbypass, string.Join(",", ListManualJoins), "] : "), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }

                        if (ListManualJoins.Count == 0)
                        {
                            foreach (DataColumn dtC in SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns) { sListColumns.Add(dtC.ColumnName); }
                        }

                        foreach (string sC in sListColumns)
                        {
                            //on ne compare pas les colonnes optionnelles ! 
                            if (OriginalSource.DSDATA.Tables[OriginalSource.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns.Contains(SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns[sC].ColumnName)
                                & (!JobParameters.DISALLOWED_SQL_COLUMNS.Contains(SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns[sC].ColumnName))
                                    & (!JobParameters.OptionalDynamicParamField_OnInsert.Contains(SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns[sC].ColumnName, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                ListDCJoin.Add(SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle].Columns[sC]);
                            }
                        }
                    }

                    List<string[]> sListColKeys = new();
                    if (ListDCJoin.Count >= 1)
                    {
                        foreach (DataColumn dc in ListDCJoin)
                        {
                            sListColKeys.Add(new string[] { dc.ColumnName, dc.ColumnName, "=", dc.Namespace, dc.Namespace, dc.ColumnName, dc.ColumnName });
                        }

                        SHSOperations SHS = new(JobParameters, OriginalSource.DSQUERY, ref MyLog);
                        DataTable dtData = SHS.GetCrossJoinDatatable(OriginalSource.DSQUERY, ListCrossJoinTypes[iSubQ], OriginalSource.DSDATA.Tables[OriginalSource.DSQUERY.QueryAnalyzer.SourceTableToHandle], SubQuery.DSDATA.Tables[SubQuery.DSQUERY.QueryAnalyzer.SourceTableToHandle], sListColKeys, 0, 1);
                        OriginalSource.RewriteDataset(dtData, SubQuery.DSQUERY.CrossJoinQueryWhere, MyLog);
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.mt_crossjoin_nolink, Environment.NewLine, Environment.NewLine, "[Source 1] : ", OriginalSource.DSQUERY.SQLQuery, Environment.NewLine, Environment.NewLine, "[Source 2] : ", SubQuery.DSQUERY.SQLQuery), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
                JobParameters.HyperFileArrayFieldTransformation = iDataTransform;
                return OriginalSource;
            }
            catch (OperationCanceledException)
            {
                JobParameters.HyperFileArrayFieldTransformation = iDataTransform;
                throw;
            }
        }

        private static DataSet RepSyncTask_Target(int numThread, SQLTools_Enums.REPSYNC_INSERT_METHOD rsMethod, bool bSubTarget, OneDSOneQuery dsAndQueryRun, Job JobRun, ref LogTools MyLog, bool bAvoidPerform, bool bNoLoadErrors, bool bPartialTask)
        {
            DataSet dsSynchroData = new();

            if (!JobRun.SQLDirectStream)
            {
                int iSourceRows;
                int iT = 0;
                bool bNoTargetErrors = true;
                //partial delete

                try
                {
                    int iErrors = MyLog.JobErrors;

                    foreach (DataTable dtOut in dsAndQueryRun.DSDATA.Tables)
                    {
                        if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                        { break; }

                        iSourceRows = dtOut.Rows.Count;

                        //gestion du nom de la cible avec particularité sur multi-tables (cas des JSON en source par exemple)
                        string sOutput = dsAndQueryRun.DSQUERY.OutputTable;
                        if (iT > 0)
                        {
                            //je ne veux pas un fichier du genre monfichier.csvCOUCOU.csv
                            //string sExt = JobRun.ConnectionString_Target.SConnDriver.ToString();
                            //sExt = sExt[(JobRun.ConnectionString_Target.SConnDriver.ToString().IndexOf("_") + 1)..]; // sOutput.Contains("." + sExt, StringComparison.OrdinalIgnoreCase)
                            if (JobRun.ConnectionString_Target.SConnDriverSuffix.Equals("FI") && Regex.Match(sOutput, "\\.[A-z0-9]{1,5}").Success)
                            {
                                //pour les fichiers excel, je ne veux pas créer de nouveaux fichiers mais plutôt synchroniser
                                if (rsMethod != SQLTools_Enums.REPSYNC_INSERT_METHOD.ALL_IN_ONE)
                                {
                                    string sExt = Regex.Match(sOutput, "\\.[A-z0-9]{1,5}").Value;
                                    sOutput = string.Concat(sOutput.Replace(sExt, ""), "_", dtOut.TableName, sExt);
                                }
                            }
                            else if (JobRun.ConnectionString_Target.SConnDriverSuffix.Equals("MB"))
                            {
                                sOutput = dsAndQueryRun.DSQUERY.OutputTable;
                            }
                            else
                            {
                                sOutput = string.Concat(dsAndQueryRun.DSQUERY.OutputTable, "_", dtOut.TableName);
                            }
                        }

                        CONNString CSTold = new(JobRun.ConnectionString_Target.SConnDriver,
                                        JobRun.ConnectionString_Target.SConnID,
                                        JobRun.ConnectionString_Target.SConnName,
                                        JobRun.ConnectionString_Target.SConnString(null),
                                        JobRun.ConnectionString_Target.SConnParams);
                        string sDBTold = (string)JobRun.DatabaseName_Target.Clone();

                        if (dsAndQueryRun.DSQUERY.ConnectionTrg.SConnID != JobRun.ConnectionString_Target.SConnID) //si on a forcé une autre connexion que celle du job
                        {
                            JobRun.DatabaseName_Target = dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDB;
                        }

                        JobRun.ConnectionString_Target = dsAndQueryRun.DSQUERY.ConnectionTrg;
                        if (bSubTarget) //le subtarget ne doit pas se baser sur la DB qui est indiquée dans le job
                        {
                            JobRun.DatabaseName_Target = dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDB;
                        }

                        string sJobMethod = JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT ? (Languages.Languages.mt_target_replication + iSourceRows.ToString() + Languages.Languages.mt_target_rows) : (Languages.Languages.mt_target_synchro + iSourceRows.ToString() + Languages.Languages.mt_target_rows);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), bSubTarget ? Languages.Languages.mt_target_b : Languages.Languages.mt_target_a, " -> ", bPartialTask ? Languages.Languages.mt_target_partial + " " : "", sJobMethod, Languages.Languages.mt_target_to, sOutput), SQLTools_Enums.LOG_TYPEINFO.INF);

                        int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                        switch (dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDriver.ToString()[..2])
                        {
                            case "DB":
                                SQLTools SQL = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                                bool bTableExists = SQL.CheckForExistingTable(sOutput, dsAndQueryRun.DSQUERY);

                                //nettoyage des noms de colonnes pour éviter les conflits
                                foreach (DataColumn dc in dtOut.Columns)
                                {
                                    if (dc.ColumnName.Contains(SQL.EchappementChar)) { dc.ColumnName = dc.ColumnName.Replace(SQL.EchappementChar, "_"); }
                                }

                                //remplacement des caractères chelous des noms de colonnes sur certaines BDD
                                switch (dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDriver)
                                {
                                    case SQLTools_Enums.BDD.DB_ACCESS:
                                        dsAndQueryRun.RenameColumnsForSQL(numThread, 0, JobRun, ref MyLog);
                                        break;
                                    case SQLTools_Enums.BDD.DB_ODBC:
                                        dsAndQueryRun.RenameColumnsForSQL(numThread, 0, JobRun, ref MyLog);
                                        break;
                                }

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {
                                    int iErrorsBefore = MyLog.JobErrors;
                                    bool bPostProcessed = false;

                                    if (bTableExists)
                                    {
                                        switch (JobRun.TargetTableBehavior)
                                        {
                                            case SQLTools_Enums.TARGET_TABLE_METHOD.TRUNCATE:
                                                SQL.AlterTableConstraints(sOutput, true, dsAndQueryRun.DSQUERY);
                                                if (SQL.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE || SQL.Connection.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
                                                {
                                                    SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", SQL.EchappementChar, sOutput, SQL.EchappementChar, ";"), dsAndQueryRun.DSQUERY, sOutput);
                                                }
                                                else
                                                {
                                                    SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, "TRUNCATE TABLE " + SQL.EchappementChar + sOutput + SQL.EchappementChar + (SQL.Connection.SConnDriver == SQLTools_Enums.BDD.DB_POSTGRE ? " CASCADE" : "") + ";", dsAndQueryRun.DSQUERY, sOutput);
                                                }
                                                SQL.AlterTableConstraints(sOutput, false, dsAndQueryRun.DSQUERY);
                                                bPostProcessed = true;
                                                break;

                                            case SQLTools_Enums.TARGET_TABLE_METHOD.FULL_DELETE:
                                                SQL.AlterTableConstraints(sOutput, true, dsAndQueryRun.DSQUERY);
                                                SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", SQL.EchappementChar, sOutput, SQL.EchappementChar, ";"), dsAndQueryRun.DSQUERY, sOutput);
                                                SQL.AlterTableConstraints(sOutput, false, dsAndQueryRun.DSQUERY);
                                                bPostProcessed = true;
                                                break;
                                        }
                                    }

                                    if (bTableExists)
                                    {
                                        if (JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE || JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM || JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME)
                                        {
                                            string sWhereToFilter = "";

                                            if (JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE) //création du where selon la requête
                                            {
                                                sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithSourceQuery(SQL.EchappementChar, dsAndQueryRun.DSQUERY, dtOut);
                                            }
                                            else if (JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM) //création du where selon la ou les colonnes dynparam du job
                                            {
                                                sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithDynamicParams(SQL.EchappementChar, JobRun.OptionalDynamicParamField_OnInsert, JobRun.DynParams);
                                            }
                                            else if (JobRun.TargetTableBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME) //création du where selon la ou les colonnes dynparam du job
                                            {
                                                string sValue = dsAndQueryRun.DSQUERY.ConnectionSrc.SConnDriverSuffix switch
                                                {
                                                    "DB" => JobRun.DatabaseName_Source,
                                                    "FI" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                    "MB" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                    "AD" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                    "WS" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                    _ => dsAndQueryRun.DSDATA.Tables[0].TableName,
                                                };
                                                sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithDbName(SQL.EchappementChar, JobRun.TargetAddDbName, sValue);
                                            }

                                            if (JobRun.SQLTargetDisableConstraints)
                                            {
                                                SQL.AlterTableConstraints(sOutput, true, dsAndQueryRun.DSQUERY);
                                            }
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_sqlpartialdelete, sWhereToFilter, "]"), SQLTools_Enums.LOG_TYPEINFO.INF);
                                            DataSet dsDelete = SQL.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, string.Concat("DELETE FROM ", sOutput, " ", sWhereToFilter), dsAndQueryRun.DSQUERY, sOutput, true);
                                            string sDeleted = SQLTools.BuildStringFromDs(dsDelete);
                                            if (int.TryParse(sDeleted, out int iDeleted)) { iDeleted = int.Parse(sDeleted); }
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_sqlpartialdeleteok01, sDeleted, Languages.Languages.mt_target_sqlpartialdeleteok02), SQLTools_Enums.LOG_TYPEINFO.INF);
                                            if (JobRun.SQLTargetDisableConstraints)
                                            {
                                                SQL.AlterTableConstraints(sOutput, false, dsAndQueryRun.DSQUERY);
                                            }
                                            bPostProcessed = true;
                                        }
                                    }

                                    if (bPostProcessed && iErrorsBefore == MyLog.JobErrors) //tout s'est à priori bien passé
                                    {
                                        //on va consigner l'information pour l'utiliser dans le cas d'un job itératif (ne pas itérer le truncate, par exemple)
                                        //en revanche, si on utilise des méthodes de suppression en fonction de paramètres, on bouge ceux-ci à chaque itération
                                        //donc on veut appliquer les suppressions conditionnelles à chaque fois
                                        if (JobRun.TargetTableBehavior == TARGET_TABLE_METHOD.DROP
                                            || JobRun.TargetTableBehavior == TARGET_TABLE_METHOD.TRUNCATE
                                            || JobRun.TargetTableBehavior == TARGET_TABLE_METHOD.FULL_DELETE)
                                        {
                                            dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, QUERY_PROPERTIES.TARGET_BEHAVIOR_PROCESSED, "True", false);
                                        }
                                    }

                                    SQL.InsertDataInBDD(dtOut, sOutput, dsAndQueryRun.DSQUERY, bTableExists ? 1 : 0);

                                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                    if (iErrWarnB > iErrWarnA)
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                    }
                                    else
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                    }
                                }

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    if (dsAndQueryRun.DSQUERY.ConnectionSrc.SConnDriver.ToString()[..2].Equals("DB"))
                                    {
                                        SQLTools SQLB = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                                        dsSynchroData = SQLB.CompareDsWithTargetDB(dsAndQueryRun.DSQUERY, sOutput, SQL, !bAvoidPerform, dtOut);
                                    }
                                    else
                                    {
                                        //en mode synchro, si la source n'est pas une DB, on doit récupérer des types corrects : la récupération de fichier ne produit que des colonnes "string"
                                        SHSOperations SHS = new(JobRun, dsAndQueryRun.DSQUERY, ref MyLog);
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.SetFieldsAnalyzer(SHS.GetListFieldsTypesFromDataset(dtOut, -1, dsAndQueryRun.DSQUERY, SQLTools_Enums.CLASS_PURPOSE.SRC));

                                        dsSynchroData = SQL.CompareDsWithTargetDB(dsAndQueryRun.DSQUERY, sOutput, SQL, !bAvoidPerform, dtOut);
                                    }
                                }
                                //shrink des tables
                                if (JobRun.GlobalParameters.SQL_SHRINK_TABLES)
                                {
                                    SQL.ShrinkTable(sOutput, dsAndQueryRun.DSQUERY);
                                }
                                break;

                            case "NS":
                                NOSQLTools NOSQL = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                                //dsAndQueryRun.RenameColumnsForSQL(numThread, 0, JobRun, ref MyLog);
                                //--------------------------

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {

                                    if (JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.DROP)
                                    {
                                        bool bOK = NOSQL.DropCollection(sOutput, dsAndQueryRun.DSQUERY);
                                    }
                                    else if (JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE || JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM || JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME)
                                    {
                                        string sWhereToFilter = "";

                                        if (JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE) //création du where selon la requête
                                        {
                                            sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithSourceQuery("", dsAndQueryRun.DSQUERY, dtOut);
                                        }
                                        else if (JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DYNPARAM) //création du where selon la ou les colonnes dynparam du job
                                        {
                                            sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithDynamicParams("", JobRun.OptionalDynamicParamField_OnInsert, JobRun.DynParams);
                                        }
                                        else if (JobRun.TargetMongoCollectionBehavior == SQLTools_Enums.TARGET_TABLE_METHOD.PARTIAL_DELETE_COL_DBNAME) //création du where selon la ou les colonnes dynparam du job
                                        {
                                            string sValue = dsAndQueryRun.DSQUERY.ConnectionSrc.SConnDriverSuffix switch
                                            {
                                                "DB" => JobRun.DatabaseName_Source,
                                                "FI" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                "MB" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                "AD" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                "WS" => dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name,
                                                _ => "?",
                                            };
                                            sWhereToFilter = Toolbox.CleanSQLConditionsForTarget_WithDbName("", JobRun.TargetAddDbName, sValue);
                                        }

                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_sqlpartialdelete, sWhereToFilter, "]"), SQLTools_Enums.LOG_TYPEINFO.INF);
                                        long iDelete = NOSQL.DeleteDataFromMongoDB(dsAndQueryRun.DSQUERY, sOutput);
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_sqlpartialdeleteok01, iDelete.ToString(), Languages.Languages.mt_target_sqlpartialdeleteok02), SQLTools_Enums.LOG_TYPEINFO.INF);
                                    }
                                    NOSQL.InsertDataToMongoDB(dtOut, sOutput, dsAndQueryRun.DSQUERY);

                                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                    if (iErrWarnB > iErrWarnA)
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                    }
                                    else
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                    }
                                }
                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    dsSynchroData = NOSQL.CompareDsWithTargetDB(dsAndQueryRun.DSQUERY, sOutput, bAvoidPerform, dtOut);
                                }
                                break;

                            case "FI":
                                FITools FILE = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                                if ((dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDriver == SQLTools_Enums.BDD.FI_CSV && JobRun.CSVAddQuotes) || dsAndQueryRun.DSQUERY.ConnectionTrg.SConnDriver == SQLTools_Enums.BDD.FI_XLS)
                                {
                                    //nettoyage des noms de colonnes pour éviter les conflits
                                    foreach (DataColumn dc in dtOut.Columns)
                                    {
                                        if (dc.ColumnName.Contains('"')) { dc.ColumnName = dc.ColumnName.Replace("\"", "_"); }
                                    }
                                }

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {
                                    if (iT == 0) //cas de fichiers excel à construire à partir de plusieurs requêtes au nom de fichier identique
                                    {
                                        FILE.BuildFileFromDs(dtOut, sOutput, dsAndQueryRun.DSQUERY, true);
                                    }
                                    else
                                    {
                                        FILE.BuildFileFromDs(dtOut, sOutput, dsAndQueryRun.DSQUERY, false);
                                    }

                                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                    if (iErrWarnB > iErrWarnA)
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                    }
                                    else
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                    }
                                }
                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    string sTargetPath = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(JobRun.ConnectionString_Target.SConnString(JobRun.DynParams), JobRun.DynParams);
                                    
                                    dsSynchroData = FILE.CompareDsWithTargetFile(dsAndQueryRun.DSQUERY, sTargetPath, sOutput, true, dtOut);
                                    if (!JobRun.RunInSimulationMode)
                                    {
                                        FILE.JobParameters.AppendFileCreation = false; //je force la réécriture complète du fichier
                                        FILE.BuildFileFromDs(dsSynchroData.Tables[dsSynchroData.Tables.Count - 1], sOutput, dsAndQueryRun.DSQUERY);
                                    }
                                }
                                break;

                            case "WS":
                                WSTools WS = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {
                                    WS.SendDataToWebservice(dsAndQueryRun.DSQUERY, dtOut, sOutput).Wait(Monitoring.TaskCancellationToken); //TODO : multithreading

                                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                    if (iErrWarnB > iErrWarnA)
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                    }
                                    else
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                    }
                                }
                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    // NOT SUPPORTED
                                }
                                break;

                            case "MB":
                                MailTools MAIL = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {
                                    List<string> sListTO = new();
                                    if (dsAndQueryRun.DSQUERY.OutputTable.IndexOf(",") > 0)
                                    { sListTO = dsAndQueryRun.DSQUERY.OutputTable.Split(Convert.ToChar(",")).ToList(); }
                                    else if (dsAndQueryRun.DSQUERY.OutputTable.IndexOf(";") > 0)
                                    { sListTO = dsAndQueryRun.DSQUERY.OutputTable.Split(Convert.ToChar(";")).ToList(); }
                                    else { sListTO.Add(dsAndQueryRun.DSQUERY.OutputTable); }

                                    if (JobRun.MailAssembleQueriesSameRecipient)
                                    {
                                        if (iT == 0)
                                        {
                                            string sContent = "";
                                            List<string> sListPJ = MAIL.PrepareDataToBeSent(ref sContent, dsAndQueryRun.DSDATA, dtOut.Namespace, dtOut.Rows.Count, dsAndQueryRun.DSQUERY);

                                            string sMail = JobRun.JobDescription.Length == 0 ? JobRun.JobNAME : JobRun.JobDescription;
                                            sMail = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sMail, JobRun.DynParams);

                                            MAIL.SendMail(sMail, sContent, sListTO, null, sListPJ, false);

                                            int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                            if (iErrWarnB > iErrWarnA)
                                            {
                                                dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                            }
                                            else
                                            {
                                                dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (iT == 0) //quand notre source possède plusieurs datatables (ex:json), on fait un seul mail donc on bypasse la boucle
                                        {
                                            string sContent = "";
                                            List<string> sListPJ = MAIL.PrepareDataToBeSent(ref sContent, dsAndQueryRun.DSDATA, dtOut.Namespace, dtOut.Rows.Count, dsAndQueryRun.DSQUERY);

                                            string sMail = JobRun.JobDescription.Length == 0 ? JobRun.JobNAME : JobRun.JobDescription;
                                            sMail = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sMail, JobRun.DynParams);

                                            MAIL.SendMail(sMail, sContent, sListTO, null, sListPJ, false);

                                            int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                            if (iErrWarnB > iErrWarnA)
                                            {
                                                dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                            }
                                            else
                                            {
                                                dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                            }
                                        }
                                    }
                                }
                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    // NOT SUPPORTED
                                }
                                break;

                            case "AD":
                                ADTools AD = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                                {
                                    AD.AddObjectsToAD(sOutput, dtOut, dsAndQueryRun.DSQUERY);

                                    int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                                    if (iErrWarnB > iErrWarnA)
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + dtOut.Rows.Count.ToString() + @" !", false);
                                    }
                                    else
                                    {
                                        dsAndQueryRun.DSQUERY.QueryAnalyzer.AddQueryProperty(sOutput, SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, dtOut.Rows.Count.ToString(), false);
                                    }
                                }
                                if (JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING)
                                {
                                    dsSynchroData = AD.CompareDsWithAD(sOutput, dtOut, JobRun.ConnectionString_Target, dsAndQueryRun.DSQUERY);
                                }
                                break;
                        }
                        iT++;

                        //TODO : avec synchro
                        if (!bPartialTask && JobRun.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                        {
                            MyLog.AddJobReportRow(sOutput, iSourceRows, 0, iSourceRows, 0, 0);
                        }

                        //if (dsAndQueryRun.DSQUERY.ConnectionTrg.SConnID != JobRun.ConnectionString_Target.SConnID)
                        //{ JobRun.DatabaseName_Target = sDBTold; }

                        //JobRun.ConnectionString_Target = CSTold;
                        //if (bSubTarget) //le subtarget ne doit pas se baser sur la DB qui est indiquée dans le job
                        //{ JobRun.DatabaseName_Target = sDBTold; }
                    }

                    bNoTargetErrors = iErrors == MyLog.JobErrors;

                    if (!Monitoring.TaskCancellationToken.IsCancellationRequested)
                    {
                        //suppression des données SQL dans la source
                        if (JobRun.ConnectionString_Source.SConnDriverSuffix.Equals("DB"))
                        {
                            if (JobRun.SourceTableDeleteAfterInsert) //création du where selon la requête
                            {
                                if (bNoTargetErrors && bNoLoadErrors && dsAndQueryRun.DSDATA.Tables.Count > 0)
                                {
                                    if (dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables.Count == 1)
                                    {
                                        string sTable = dsAndQueryRun.DSQUERY.QueryAnalyzer.Tables[0].Name;
                                        List<Query.QWhere> sWhereToDeleteSource = dsAndQueryRun.DSQUERY.QueryAnalyzer.Where;
                                        StringBuilder sbWhere = new();
                                        sbWhere.Append(sWhereToDeleteSource.Count > 0 ? " WHERE " : "");
                                        foreach (Query.QWhere qW in sWhereToDeleteSource)
                                        {
                                            sbWhere.Append(qW.Where + " AND ");
                                        }
                                        string sFilterDelete = sbWhere.ToString()[0..^5];
                                        string sQuery = string.Concat("DELETE FROM ", sTable, sFilterDelete);

                                        if (JobRun.RunInSimulationMode)
                                        {
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willavoiddeletesql, sQuery));
                                        }
                                        else
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_source_sqlpartialdelete, sFilterDelete, "]"), SQLTools_Enums.LOG_TYPEINFO.INF);
                                            SQLTools SQLSource = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                                            DataSet dsDelete = SQLSource.ExecQuery(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE, sQuery, dsAndQueryRun.DSQUERY, sTable, true);
                                            string sDeleted = SQLTools.BuildStringFromDs(dsDelete);
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_source_sqlpartialdeleteok01, sDeleted, Languages.Languages.mt_source_sqlpartialdeleteok02), SQLTools_Enums.LOG_TYPEINFO.INF);
                                        }
                                    }
                                    else
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_source_sqlpartialdeletemorethan2tables), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_source_sqlpartialdeleteerrors), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }
                        }
                        //suppression, zippage, déplacement des fichiers source si demandé
                        else if (JobRun.ConnectionString_Source.SConnDriverSuffix.Equals("FI"))
                        {
                            if (JobRun.FileCleanup_Import != SQLTools_Enums.CSV_CLEANUP_METHOD.RIEN) //création du where selon la requête
                            {
                                if (bNoTargetErrors && bNoLoadErrors && dsAndQueryRun.DSDATA.Tables.Count > 0)
                                {
                                    //post-traitement des fichiers sur FTP/SFTP
                                    var qPP = dsAndQueryRun.DSQUERY.QueryAnalyzer.QueryProperties.Where(q => q.Property == SQLTools_Enums.QUERY_PROPERTIES.FILE_FTP_PROCESSED).ToList();
                                    foreach (Query.QProperty qP in qPP)
                                    {
                                        if (JobRun.RunInSimulationMode)
                                        {
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willpostworksftp, JobRun.FileCleanup_Import.ToString(), " : ", Path.GetFileName(qP.Value)));
                                        }
                                        else
                                        {
                                            FTPTools FTP = new(JobRun, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                                            if (qP.Element.Length > 0) { FTP.FTPVariables.AlterDistantPath(qP.Element); }
                                            switch (JobRun.FileCleanup_Import)
                                            {
                                                case SQLTools_Enums.CSV_CLEANUP_METHOD.DEPLACER:
                                                    FTP.MoveFileFromFTPOrSFTPPath(qP.Value, dsAndQueryRun.DSQUERY);
                                                    break;
                                                case SQLTools_Enums.CSV_CLEANUP_METHOD.SUPPRIMER:
                                                    FTP.DeleteFileFromFTPOrSFTPPath(qP.Value, dsAndQueryRun.DSQUERY);
                                                    break;
                                                case SQLTools_Enums.CSV_CLEANUP_METHOD.ZIPPER:
                                                    FTP.ZipFileFromFTPOrSFTPPath(qP.Value, dsAndQueryRun.DSQUERY);
                                                    break;
                                            }
                                        }
                                    }

                                    //fichiers locaux
                                    List<string> sFilesToProcess = new();
                                    qPP = dsAndQueryRun.DSQUERY.QueryAnalyzer.QueryProperties.Where(q => q.Property == SQLTools_Enums.QUERY_PROPERTIES.FILE_LOCAL_PROCESSED).ToList();
                                    foreach (Query.QProperty qP in qPP)
                                    {
                                        sFilesToProcess.Add(qP.Value);
                                    }

                                    if (sFilesToProcess.Count > 0)
                                    {
                                        if (JobRun.RunInSimulationMode)
                                        {
                                            MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, string.Concat("[SIMULATION MODE] ", Languages.Languages.fi_simulation_willpostwork, JobRun.FileCleanup_Import.ToString(), " : ", string.Join(",", sFilesToProcess)));
                                        }
                                        else
                                        {
                                            try
                                            {
                                                switch (JobRun.FileCleanup_Import)
                                                {
                                                    case SQLTools_Enums.CSV_CLEANUP_METHOD.SUPPRIMER:
                                                        FITools.DeleteListOfFiles(sFilesToProcess);
                                                        break;
                                                    case SQLTools_Enums.CSV_CLEANUP_METHOD.ZIPPER:
                                                        FITools.ZipListOfFiles(sFilesToProcess, qPP[0].Element);
                                                        break;
                                                    case SQLTools_Enums.CSV_CLEANUP_METHOD.DEPLACER:
                                                        FITools.MoveListOfFiles(JobRun, sFilesToProcess, qPP[0].Element);
                                                        break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "", dsAndQueryRun.DSQUERY.RetryErrorOrWarning);
                                                dsAndQueryRun.DSQUERY.QueryErrors += 1;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_source_filesourcepostworkko), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, oce, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_failed), dsAndQueryRun.DSQUERY.RetryErrorOrWarning);
                    dsAndQueryRun.DSQUERY.QueryErrors += 1;
                    return null;
                }
                catch (Exception ex)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat("T", (numThread + 1).ToString("00"), Languages.Languages.mt_target_failed), dsAndQueryRun.DSQUERY.RetryErrorOrWarning);
                    dsAndQueryRun.DSQUERY.QueryErrors += 1;
                }
            }

            return dsSynchroData;
        }

        internal static bool RepSyncTask_DirectSQLStream(int iSlot, int iPass, int iRows, DataSet dsData, Job JobParameters, Query Q, ref LogTools MyLog)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_target_directstreamcopy, " [R.", dsData.Tables[0].Rows.Count.ToString(), " - S.", (iSlot + 1).ToString(), " - P.", (iPass + 1).ToString(), "]"), SQLTools_Enums.LOG_TYPEINFO.DET);

            try
            {
                _hasQueryData.Add(true); //variable idiote qui permet d'indiquer qu'il y avait des données dans la source

                bool bOk = true;
                OneDSOneQuery dSAndQuery = null;

                switch (Q.ConnectionSrc.SConnDriverSuffix)
                {
                    case "DB":
                        dSAndQuery = new OneDSOneQuery(dsData, Q, Q.OutputTable, Q.OutputTable);
                        dSAndQuery.ChangeBitArrayColumns(1, 0, JobParameters, ref MyLog);
                        dSAndQuery.DataTransform(1, 0, JobParameters, ref MyLog);

                        if (dSAndQuery.DSDATA != null && dSAndQuery.DSDATA.Tables.Count > 0) //contrôle supplémentaire (le getdata SQL peut remonter un dataset, mais sans tables)
                        {
                            dSAndQuery.DSNAMESPACE = Toolbox.InitDsDtNames(dSAndQuery.DSDATA, JobParameters, dSAndQuery.DSQUERY);
                            //ajout des colonnes optionnelles
                            for (int iT = 0; iT < dSAndQuery.DSDATA.Tables.Count; iT++)
                            {
                                Toolbox.AddOptionalColumnsInDatasetFromINI(dSAndQuery.DSDATA.Tables[iT], JobParameters, Q, ref MyLog);
                            }
                        }

                        //dSAndQuery.AddOriginalColumnName(1, 0, JobParameters, ref MyLog);
                        dSAndQuery.SetNullFields(1, 0, JobParameters, ref MyLog); // passage des colonnes NULL en DBNull
                        dSAndQuery.FillSHSFieldsWithDataSet(1, 0, JobParameters, ref MyLog);
                        break;
                    case "FI":
                        dSAndQuery = new OneDSOneQuery(dsData, Q, Q.OutputTable, Q.OutputTable);
                        dSAndQuery.ReworkDataSet(1, JobParameters, ref MyLog);
                        break;
                }

                //réglage pour le format de fichier
                //ipass = 0 : début, ipass = 1 : en cours, ipass = 2 : dernière itération
                if (iPass < 2 && JobParameters.SourceTableDeleteAfterInsert)
                {
                    JobParameters.SourceTableDeleteAfterInsert = false;
                    JobParameters.FileCleanup_Import = SQLTools_Enums.CSV_CLEANUP_METHOD.RIEN;
                }

                if (iPass >= 1)
                {
                    JobParameters.AppendFileCreation = true;
                    JobParameters.TargetTableBehavior = SQLTools_Enums.TARGET_TABLE_METHOD.NOTHING;
                }

                Q.QueryAnalyzer.AddQueryProperty(dSAndQuery.DSDATA.Tables[0].Namespace, SQLTools_Enums.QUERY_PROPERTIES.ROWS_RETRIEVED, dSAndQuery.DSDATA.Tables[0].Rows.Count.ToString(), false);

                JobParameters.SQLDirectStream = false;

                RepSyncTask_Target(iSlot, SQLTools_Enums.REPSYNC_INSERT_METHOD.BY_QUERY, false, dSAndQuery, JobParameters, ref MyLog, false, true, true);

                JobParameters.SQLDirectStream = true;

                if (iPass == 2 && JobParameters.JobMethod == SQLTools_Enums.JOB_PURPOSE.EXPORT_IMPORT)
                {
                    MyLog.AddJobReportRow(dSAndQuery.DSOUTPUT, iRows, 0, iRows, 0, 0);
                }

                dSAndQuery = null;
                dsData.Clear();
                dsData = null;

                return bOk;
            }
            catch (OperationCanceledException)
            { return false; }
            catch (Exception)
            { return false; }
        }

        private static IEnumerable<int> DivideEvenly(int numerator, int denominator)
        {
            int div = Math.DivRem(numerator, denominator, out int rem);

            for (int i = 0; i < denominator; i++)
            {
                yield return i < rem ? div + 1 : div;
            }
        }

        private int CalculateStartQuantityStop(int iMTQuantityToCompute, int iQteWantedThreads)
        {

            QuantityOfThreadsToCompute = iQteWantedThreads;
            int iQueries = iMTQuantityToCompute;
            //pas la peine de mettre plus de threads que de traitements à réaliser !!
            if (QuantityOfThreadsToCompute > iQueries)
            {
                QuantityOfThreadsToCompute = iQueries;
            }

            int iQueriesPerThread = Convert.ToInt32(Math.Floor((decimal)(iQueries / QuantityOfThreadsToCompute)));
            List<int> ilistQueries2 = new();
            List<int> ilistQteQueries2 = new();
            for (int cpt = 0; cpt <= QuantityOfThreadsToCompute - 1; cpt += 1)
            {
                ilistQueries2.Add(cpt * iQueriesPerThread);
                ilistQteQueries2.Add(iQueriesPerThread);
            }

            ////calcul des valeurs restantes
            ilistQteQueries2[QuantityOfThreadsToCompute - 1] = iQueries - ilistQueries2[QuantityOfThreadsToCompute - 1];

            List<int> ilistQueries = new();
            List<int> ilistQteQueries = new();

            int iT = 0;
            foreach (int i in DivideEvenly(iQueries, QuantityOfThreadsToCompute))
            {
                ilistQueries.Add(iT);
                ilistQteQueries.Add(i);
                iT += i;
            }

            //ventiler les valeurs restantes selon différents threads (ex : 7 opérations = 2 pour T1, 2 pour T2, 2 pour T3, 1 pour T4)

            //ajout des start et quantités
            for (int cptT = 0; cptT <= QuantityOfThreadsToCompute - 1; cptT += 1)
            {
                _iMTStart.Add(ilistQueries[cptT]);
                _iMTQuantity.Add(ilistQteQueries[cptT]);
                _iMTStop.Add(ilistQueries[cptT] + ilistQteQueries[cptT]);
            }

            return QuantityOfThreadsToCompute;

        }

        private static string CleanData(SQLColumn SQLcolTarget, SQLColumn SQLcolSource, object oValue, SQLTools SQLData)
        {
            string sFinalValue = "";
            bool bAnalyze = true;

            //if (SQLData.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE) //en SQLITE on cherche uniquement les performances : on analyse rien
            //{ bAnalyze = false; sFinalValue = sValue.Length > 0 ? string.Concat("'", SQLData.JobParameters.TrimData ? sValue.Trim() : sValue, "'") : SQLData.JobParameters.UseNull_Target ? "NULL" : "''"; }

            //nettoyer les données
            if (bAnalyze)
            {
                if (SQLcolTarget != null)
                {
                    string sType = SQLcolTarget.ColumnType.ToString();

                    //gestion particulière pour SQLITE ou très peu de types existent : on s'appuie donc sur la source
                    if (SQLData.Connection.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE && sType.Equals("TEXT") && !SQLcolSource.ColumnType.Equals(Type.GetType("System.String")))
                    {
                        switch (SQLcolSource.ColumnLinqType.ToString())
                        {
                            case "System.Guid":
                                sType = "TEXT";
                                break;
                            case "System.Int64":
                                sType = "INT";
                                break;
                            case "System.Int32":
                                sType = "INT";
                                break;
                            case "System.Int16":
                                sType = "INT";
                                break;
                            case "System.DateTime":
                                sType = "DATETIME";
                                break;
                            case "System.DateTimeOffset":
                                sType = "DATETIME";
                                break;
                            case "System.Boolean":
                                sType = "BOOL";
                                break;
                            case "System.Decimal":
                                sType = "DECIMAL";
                                break;
                            case "System.Double":
                                sType = "DOUBLE";
                                break;
                            case "System.Single":
                                sType = "SINGLE";
                                break;
                            case "System.Byte":
                                sType = "BYTE";
                                break;
                            case "System.Collections.BitArray":
                                sType = "VARBINARY";
                                break;
                            case "System.Byte[]":
                                sType = "VARBINARY";
                                break;
                            default:
                                sType = "TEXT";
                                break;
                        }
                    }

                    if (sType.IndexOf("DECIMAL") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.DECIMAL, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("DOUBLE") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.DOUBLE, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("NUM") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.DECIMAL, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("FLOAT") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.FLOAT, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("REAL") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.FLOAT, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("UNSIGNED") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.INT, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("INT") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanNumber(oValue.ToString(), SQLData.Connection, SQLTools_Enums.TYPE_DATA.INT, true, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, SQLData.JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0);
                    }
                    else if (sType.IndexOf("TIME") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanDate(oValue.ToString(), SQLData.JobParameters, SQLData.Connection, true, true, 2);
                    }
                    else if (sType.IndexOf("DATE") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanDate(oValue.ToString(), SQLData.JobParameters, SQLData.Connection, true, true, 1);
                    }
                    else if (sType.IndexOf("BIT") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanBit(oValue.ToString(), SQLData.Connection.SConnDriver, true);
                    }
                    else if (sType.IndexOf("BOOL") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanBit(oValue.ToString(), SQLData.Connection.SConnDriver, true);
                    }
                    else if (sType.IndexOf("VARBINARY") > -1 || sType.IndexOf("BYTEA") > -1)
                    {
                        sFinalValue = Toolbox.SetCleanVarbinary(oValue, SQLcolTarget.ColumnName, SQLData.Connection.SConnDriver, true);
                    }
                    else
                    {
                        if (!SQLcolTarget.ColumnAllowsNullValues)
                        {
                            sFinalValue = Toolbox.SetCleanString(oValue.ToString(), false, SQLData.JobParameters.CSVCharSeparator_Target, false, SQLData.Connection);
                        }
                        else { sFinalValue = Toolbox.SetCleanString(oValue.ToString(), false, SQLData.JobParameters.CSVCharSeparator_Target, SQLData.JobParameters.UseNull_Target, SQLData.Connection); }
                    }
                }
                else { sFinalValue = "NULL"; }

                if (SQLData.JobParameters.TrimData) { sFinalValue = sFinalValue.Trim(); }

            }

            return sFinalValue;

        }

        private static string DataRowsAreEqual(DataRow rowA, DataRow rowB, List<QField> sourceFields, Job JobParameters, CONNString CSt)
        {
            string sA;
            string sB;
            Type dtA;
            Type dtB;

            foreach (DataColumn dtC in rowA.Table.Columns)
            {
                if (!rowA.Table.PrimaryKey.Contains(dtC))
                {
                    //on ne compare pas les colonnes utilisées par réplicator parce que dans le cas du DTLOAD par exemple, la date ne sera jamais la même donc
                    //toutes les lignes devront systématiquement être mises à jour... ce qui est dommage...
                    if (!JobParameters.DISALLOWED_SQL_COLUMNS.Contains(rowA.Table.Columns[dtC.ColumnName].ColumnName.ToUpper()))
                    {
                        sA = rowA[dtC.ColumnName].ToString().Trim();
                        sB = rowB[dtC.ColumnName].ToString().Trim();

                        //gestion du cas ou les données source sont issues d'une source dont la datatable ne sera remplie qu'avec des string (cas des CSV, etc...)
                        var sourceCol = sourceFields.FirstOrDefault(f => f.Alias.Equals(dtC.ColumnName, StringComparison.OrdinalIgnoreCase));
                        dtA = sourceCol != null ? sourceCol.FieldAnalyzer.ColumnLinqType : rowA.Table.Columns[dtC.ColumnName].DataType;
                        
                        dtB = rowB.Table.Columns[dtC.ColumnName].DataType;

                        //note plus bas : pour les dates je compare les types en utilisant le format date de la cible pour à la fois la donnée source et cible : 
                        //sinon le formatage va être différent et la comparaisaon va considérer que ce n'est pas la même donnée

                        if (dtA.Equals(Type.GetType("System.String")))
                        {
                            sA = Toolbox.SetCleanString(sA, true, JobParameters.CSVCharSeparator_Target, false, CSt);
                            sB = Toolbox.SetCleanString(sB, true, JobParameters.CSVCharSeparator_Target, false, CSt);
                        }
                        else if (dtA.Equals(Type.GetType("System.DateTime")))
                        {
                            sA = Toolbox.SetCleanDate(sA, JobParameters, CSt, false, false, 2);
                            sB = Toolbox.SetCleanDate(sB, JobParameters, CSt, false, false, 2);
                            if (sA.Length >= 10 && sB.Length >= 10 && sA.Length != sB.Length) //correctif datetime : quand on compare une date avec un datetime
                            {
                                if (sA.Length > sB.Length && sA[..10].Equals(sA[..10]))
                                {
                                    sA = sA[..10];
                                }
                                else if (sA.Length < sB.Length && sA[..10].Equals(sA[..10]))
                                {
                                    sB = sB[..10];
                                }
                            }
                        }
                        else if (dtA.Equals(Type.GetType("System.DateTimeOffset")))
                        {
                            sA = Toolbox.SetCleanDate(sA, JobParameters, CSt, false, false, 3);
                            sB = Toolbox.SetCleanDate(sB, JobParameters, CSt, false, false, 3);
                            if (sA.Length >= 10 && sB.Length >= 10 && sA.Length != sB.Length) //correctif datetime : quand on compare une date avec un datetime
                            {
                                if (sA.Length > sB.Length && sA[..10].Equals(sA[..10]))
                                {
                                    sA = sA[..10];
                                }
                                else if (sA.Length < sB.Length && sA[..10].Equals(sA[..10]))
                                {
                                    sB = sB[..10];
                                }
                            }
                        }
                        else if (dtA.Equals(Type.GetType("System.Decimal")))
                        {
                            sA = Toolbox.SetCleanNumber(sA, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                            sB = Toolbox.SetCleanNumber(sB, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                        }
                        else if (dtA.Equals(Type.GetType("System.Double")))
                        {
                            sA = Toolbox.SetCleanNumber(sA, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                            sB = Toolbox.SetCleanNumber(sB, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                        }
                        else if (dtA.Equals(Type.GetType("System.Boolean")))
                        {
                            sA = Toolbox.SetCleanBit(sA, SQLTools_Enums.BDD.DB_SQLSERVER, false);
                            sB = Toolbox.SetCleanBit(sB, SQLTools_Enums.BDD.DB_SQLSERVER, false);
                        }
                        else if (dtA.Equals(Type.GetType("System.Int16")) || dtA.Equals(Type.GetType("System.Int32")) || dtA.Equals(Type.GetType("System.Int64")) || dtA.Equals(Type.GetType("System.SByte")))
                        {
                            sA = Toolbox.SetCleanNumber(sA, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                            sB = Toolbox.SetCleanNumber(sB, CSt, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                        }
                        else if (dtA.Equals(Type.GetType("System.Guid")))
                        {
                            sA = Toolbox.SetCleanString(sA, true, JobParameters.CSVCharSeparator_Target, false, CSt);
                            sB = Toolbox.SetCleanString(sB, true, JobParameters.CSVCharSeparator_Target, false, CSt);
                        }

                        if (!sA.Equals(sB))
                        {
                            return dtC.ColumnName;
                        }
                    }
                }
            }

            return null;

        }


        internal class OneDSOneQuery
        {
            public DataSet DSDATA;
            public Query DSQUERY;
            public string DSOUTPUT;
            public string DSNAMESPACE;

            public OneDSOneQuery(DataSet dsData, Query dsQuery, string dsOutput, string dsNamespace)
            {
                DSDATA = dsData;
                DSQUERY = dsQuery;
                DSOUTPUT = dsOutput;
                DSNAMESPACE = dsNamespace;
            }

            internal void ReworkDataSet(int numThread, Job JobParameters, ref LogTools MyLog)
            {
                try
                {
                    int iTable = DSDATA.Tables.Count > DSQUERY.QueryAnalyzer.SourceTableToHandle ? DSQUERY.QueryAnalyzer.SourceTableToHandle : -1;
                    if (iTable > -1 && DSDATA != null && DSDATA.Tables.Count > 0)
                    {
                        //nettoyage
                        //foreach (DataColumn dc in DSDATA.Tables[iTable].Columns) 
                        //{ 
                        //    dc.ColumnName = dc.ColumnName.Trim(); 
                        //}
                        //RemoveSameColumnsNames(numThread, iTable, JobParameters, ref MyLog);

                        ChangeBitArrayColumns(numThread, iTable, JobParameters, ref MyLog);

                        //impératif que le filtrage soit avant les transfo. mathétiques
                        //select id_sample, max(nb_random_number) from sample.csv where len(li_sample) = 10 group by id_sample

                        if (JobParameters.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.OQL || JobParameters.WebserviceSQLLanguage == SQLTools_Enums.WEBSERVICE_SQL.SOQL)
                        {
                            //pas de filtrage (réalisé directement dans l'API)
                        }
                        else
                        {
                            DataFiltering(numThread, iTable, JobParameters, ref MyLog);
                        }

                        AddOriginalColumnName(numThread, iTable, JobParameters, ref MyLog); //appose en information additionnelle de chaque colonne leur nom "raw" d'origine

                        ApplySQLTransformations(numThread, iTable, JobParameters, ref MyLog); //transformations SQL sur pseudo SQL language
                        PseudoMathOperations(numThread, iTable, JobParameters, ref MyLog);
                        ApplyOrderByTransformations(numThread, iTable, JobParameters, ref MyLog);

                        //je remplace les noms par les alias, je récupère les colonnes à virer et je les vire après filtrage
                        RenameColumnsWithAliasesAndGetUnused(numThread, iTable, JobParameters, ref MyLog); //supprime les colonnes qui ne sont pas dans la requête (sauf si un select *)

                        RemoveDuplicatesFromDataTable(numThread, iTable, JobParameters, ref MyLog); //en cas de select distinct
                        SetNullFields(numThread, iTable, JobParameters, ref MyLog); // passage des colonnes NULL en DBNull
                        FillSHSFieldsWithDataSet(numThread, iTable, JobParameters, ref MyLog);
                        SetColumnsOrderFromQuery(numThread, iTable, JobParameters, ref MyLog);
                        DataTransform(numThread, iTable, JobParameters, ref MyLog); //transforme le résultat selon les méthodes supportées
                        LimitResults(numThread, iTable, JobParameters, ref MyLog);
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypass01, (DSQUERY.QueryAnalyzer.SourceTableToHandle + 1).ToString(), Languages.Languages.mt_rework_bypass02, DSDATA.Tables.Count.ToString(), Languages.Languages.mt_rework_bypass03), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                    //ajout des colonnes optionnelles dans chaque table récupérée
                    for (int iT = 0; iT < DSDATA.Tables.Count; iT++)
                    {
                        if (DSDATA.Tables[iT].Columns.Count > 0)
                        {
                            Toolbox.AddOptionalColumnsInDatasetFromINI(DSDATA.Tables[iT], JobParameters, DSQUERY, ref MyLog);
                        }
                    }
                }
                catch (OperationCanceledException)
                { throw; }
                catch (Exception)
                { throw; }
            }

            //private void RemoveSameColumnsNames(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            //{
            //    if (JobParameters.ConnectionString_Target.SConnDriverSuffix.Equals("DB"))
            //    {
            //        //contrôle si 2 colonnes auraient peu ou prou le même nom (ex : "coucou", "coucou ") car ça ne marche pas en SQL Server
            //        for (int iT = 0; iT < DSDATA.Tables.Count; iT++)
            //        {
            //            for (int iC = 0; iC < DSDATA.Tables[iT].Columns.Count; iC++)
            //            {
            //                for (int iCB = 0; iCB < DSDATA.Tables[iT].Columns.Count; iCB++)
            //                {
            //                    if (iC != iCB && DSDATA.Tables[iT].Columns[iC].ColumnName.Trim().Equals(DSDATA.Tables[iT].Columns[iCB].ColumnName.Trim()))
            //                    {
            //                        int iN = 1;
            //                        bool bOk = false;
            //                        while (!bOk)
            //                        {
            //                            try
            //                            {
            //                                DSDATA.Tables[iT].Columns[iCB].ColumnName = string.Concat(DSDATA.Tables[iT].Columns[iC].ColumnName, "_", iN);
            //                                bOk = true;
            //                            }
            //                            catch { iN++; }
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}

            internal void LimitResults(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                if (DSQUERY.QueryAnalyzer.LimitedResults > 0)
                {
                    int iLimit = DSQUERY.QueryAnalyzer.LimitedResults;
                    int iRows = DSDATA.Tables[iTable].Rows.Count;
                    if (iRows > iLimit)
                    {
                        for (int i = 0; i < iRows - iLimit; i++)
                        {
                            DSDATA.Tables[iTable].Rows[iLimit].Delete();
                        }
                        DSDATA.Tables[iTable].AcceptChanges();
                    }
                }
            }

            internal void SetColumnsOrderFromQuery(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                int iCMax = DSDATA.Tables[iTable].Columns.Count - 1;
                //mettre les colonnes dans l'ordre demandé
                if (DSQUERY.QueryAnalyzer.Fields.Count > 1)
                {
                    for (int iQF = 0; iQF < DSQUERY.QueryAnalyzer.Fields.Count; iQF++)
                    {
                        int iIdx = DSDATA.Tables[iTable].Columns.IndexOf(DSQUERY.QueryAnalyzer.Fields[iQF].Alias);
                        if (iIdx > -1)
                        {
                            DSDATA.Tables[iTable].Columns[iIdx].SetOrdinal(iQF > iCMax ? iCMax : iQF);
                        }
                    }
                }
            }

            internal void ChangeBitArrayColumns(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                ////conversion des colonnes BitArray en string
                //for (int iC = 0; iC < DSDATA.Tables[iTable].Columns.Count; iC++)
                //{
                //    if (DSDATA.Tables[iTable].Columns[iC].DataType.Name.Equals("BitArray", StringComparison.OrdinalIgnoreCase))
                //    {
                //        Toolbox.ConvertColumnType(DSDATA.Tables[iTable], DSDATA.Tables[iTable].Columns[iC], Type.GetType("System.String"));
                //    }
                //    else if (DSDATA.Tables[iTable].Columns[iC].DataType.Name.Equals("Byte[]", StringComparison.OrdinalIgnoreCase))
                //    {
                //        Toolbox.ConvertColumnType(DSDATA.Tables[iTable], DSDATA.Tables[iTable].Columns[iC], Type.GetType("System.String"));
                //    }
                //}
            }

            internal void FillSHSFieldsWithDataSet(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                //suppression des éventuels alias de table devant des champs non aliasés
                //ex : select ent.id from x as ent -> a ce stade, le "ent" est encore présent
                foreach (DataColumn dc in DSDATA.Tables[iTable].Columns)
                {
                    dc.Caption = dc.ColumnName;
                    foreach (Query.QTable qT in DSQUERY.QueryAnalyzer.Tables)
                    {
                        if (dc.ColumnName.StartsWith(qT.Name + "."))
                        {
                            string sOldName = dc.ColumnName;
                            string sCol = dc.ColumnName.Replace(qT.Name + ".", "");
                            if (!DSDATA.Tables[iTable].Columns.Contains(sCol)) { dc.ColumnName = sCol; dc.Caption = sOldName; } //je stocke l'ancien nom de colonne à cause du problème de "act.ACT_ID"
                        }
                        else if (dc.ColumnName.StartsWith(qT.Alias + "."))
                        {
                            string sOldName = dc.ColumnName;
                            string sCol = dc.ColumnName.Replace(qT.Alias + ".", "");
                            if (!DSDATA.Tables[iTable].Columns.Contains(sCol)) { dc.ColumnName = sCol; dc.Caption = sOldName; }
                        }
                    }
                }

                //intégration des noms de colonne dans l'analyzer
                //if (DSDATA.Tables.Count > 0)
                //{
                //    if (DSQUERY.QueryAnalyzer.SourceTableToHandle < DSDATA.Tables.Count)
                //    { DSQUERY.QueryAnalyzer.SetFieldsAnalyzer(Toolbox.GetSQLColumnsFromDataTable(DSDATA.Tables[DSQUERY.QueryAnalyzer.SourceTableToHandle])); }
                //    else { DSQUERY.QueryAnalyzer.SetFieldsAnalyzer(Toolbox.GetSQLColumnsFromDataTable(DSDATA.Tables[0])); }
                //}
            }

            internal void DataFiltering(int numThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                //application des clauses filtrantes (WHERE)
                DataSet dsFiltered = new()
                {
                    Namespace = DSDATA.Namespace,
                    DataSetName = DSDATA.DataSetName
                };

                for (int iT = 0; iT < DSDATA.Tables.Count; iT++)
                {
                    if (iT == iTable)
                    {
                        dsFiltered.Tables.Add(Toolbox.FilterDataTableWithPseudoQuery(DSDATA.Tables[iT], DSQUERY, JobParameters, ref MyLog));
                    }
                    else
                    {
                        DataTable dtFiltered;
                        string sTblName = DSDATA.Tables[iT].TableName;
                        string sNamespace = DSDATA.Tables[iT].Namespace;
                        dtFiltered = DSDATA.Tables[iT].Copy();

                        //application de la suppression
                        if (dsFiltered.Tables.Count > 0 && dsFiltered.Tables[0].PrimaryKey != null && DSDATA.Tables.Count > 1)
                        {
                            foreach (DataColumn dc in dsFiltered.Tables[0].PrimaryKey)
                            {
                                string sColForeign = dc.ColumnName;
                                if (dc.ColumnName.StartsWith(dc.Namespace + "."))
                                { sColForeign = dc.ColumnName[(dc.Namespace.Length + 1)..]; }

                                if (dsFiltered.Tables[0].Rows.Count > 0)
                                {
                                    //on vérifie d'abord que la table en question contienne bien les colonnes de la Primary Key de la table master
                                    dtFiltered = DSDATA.Tables[iT].AsEnumerable().Where(x => dsFiltered.Tables[0].AsEnumerable().Any(z => z.Field<string>(dc.ColumnName) == x.Field<string>(sColForeign))).CopyToDataTable();
                                }
                                else
                                {
                                    dtFiltered = DSDATA.Tables[iT].Clone();
                                }
                            }
                        }

                        dtFiltered.Namespace = sNamespace;
                        dtFiltered.TableName = sTblName;

                        for (int i = 0; i < dtFiltered.Columns.Count; i++) { dtFiltered.Columns[i].Namespace = sNamespace; }
                        dsFiltered.Tables.Add(dtFiltered);

                    }
                }
                DSDATA.Clear();
                DSDATA = null;
                DSDATA = dsFiltered;
                DSDATA.CaseSensitive = false;
            }

            internal void DataTransform(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                if (JobParameters.HyperFileArrayFieldTransformation > 0) //hyperfile method
                {
                    SHSOperations SHS = new(JobParameters, DSQUERY, ref MyLog);
                    DataTable dtTransform = SHS.DataTransformation(DSDATA.Tables[iTable], ref DSQUERY);
                    if (dtTransform == null)
                    {
                        //on vire tout
                        DSDATA.Tables[iTable].Clear();
                    }
                    else if (dtTransform.Namespace.Equals("ABNORMAL") && JobParameters.DataTransformDontTransformIfVariableArraySizes)
                    {
                        //On ne change pas la source
                    }
                    else if (dtTransform.Namespace.Equals("UNMODIFIED"))
                    {
                        //On ne change pas la source
                    }
                    else
                    {
                        //on réécrit le dataset source
                        string sDS = DSDATA.DataSetName;
                        string sDS2 = DSDATA.Namespace;
                        DataSet DSOLDDATA = DSDATA.Copy();
                        DSOLDDATA.DataSetName = DSDATA.DataSetName;
                        DSOLDDATA.Namespace = DSOLDDATA.Namespace;
                        DSDATA.Clear();
                        DSDATA = new DataSet
                        {
                            Namespace = sDS2,
                            DataSetName = sDS
                        };

                        for (int iT = 0; iT < DSOLDDATA.Tables.Count; iT++)
                        {
                            if (iT == iTable)
                            {
                                DSDATA.Tables.Add(dtTransform);
                            }
                            else
                            {
                                DSDATA.Tables.Add(DSOLDDATA.Tables[iT].Copy());
                            }
                        }
                        DSOLDDATA.Clear();
                    }
                }
            }

            internal void RenameColumnsWithAliasesAndGetUnused(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                List<string> sColToRemove = new();

                if (DSQUERY.QueryAnalyzer.Fields.Count > 0 && !DSQUERY.QueryAnalyzer.Fields[0].Name.Equals("*"))
                {
                    for (int iC = 0; iC < DSDATA.Tables[iTable].Columns.Count; iC++)
                    {
                        bool bExists = false;
                        foreach (Query.QField qF in DSQUERY.QueryAnalyzer.Fields)
                        {
                            string sCol = DSDATA.Tables[iTable].Columns[iC].ColumnName;
                            //Affûtage (INT, EXT, NON) Affûtage (INT,EXT,NON)
                            if (sCol.Equals(qF.Alias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                bExists = true;
                                break;
                            }
                            else if (sCol.Equals(string.Concat(qF.TableAlias, ".", qF.Name), StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (!DSDATA.Tables[iTable].Columns.Contains(qF.Alias))
                                {
                                    DSDATA.Tables[iTable].Columns[iC].ColumnName = qF.Alias;
                                    bExists = true;
                                    break;
                                }
                                else
                                {
                                    break;
                                }
                            }

                        }
                        if (!bExists)
                        {
                            if (!JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(DSDATA.Tables[iTable].Columns[iC].ColumnName) && !JobParameters.DISALLOWED_SQL_COLUMNS.Contains(DSDATA.Tables[iTable].Columns[iC].ColumnName))
                            {
                                sColToRemove.Add(DSDATA.Tables[iTable].Columns[iC].ColumnName);
                            }
                        }
                    }
                }
                foreach (string sC in sColToRemove)
                {
                    if (DSDATA.Tables[iTable].PrimaryKey.Length > 0 && DSDATA.Tables[iTable].PrimaryKey.Contains(DSDATA.Tables[iTable].Columns[sC]))
                    {
                        DSDATA.Tables[iTable].PrimaryKey = null;
                        DSDATA.Tables[iTable].Columns.Remove(sC);
                    }
                    else { DSDATA.Tables[iTable].Columns.Remove(sC); }
                }
            }

            internal void RewriteDataset(DataTable dtData, string sWhereFilter, LogTools MyLog)
            {
                DataTable dtCopy = null;

                if (DSDATA.Tables.Count > 0)
                {
                    if (sWhereFilter.Length > 0 && dtData.Rows.Count > 0)
                    {
                        string sWhere = sWhereFilter;
                        if (sWhere.StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase)) { sWhere = sWhere[5..].Trim(); }
                        try
                        {
                            dtCopy = dtData.Select(sWhere).CopyToDataTable();
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.mt_rework_cantfiltercrossjoin, SQLTools_Enums.LOG_TYPEINFO.INF);
                            dtCopy = dtData.Copy();
                            dtCopy.Rows.Clear();
                        }
                    }
                }
                //petite astuce pour conserver un nom de namespace si toutes les tables jointes n'avaient pas le namespace
                string sNamespace = DSDATA.Tables[0].Namespace;
                if (dtData.Namespace.Length == 0)
                {
                    dtData.Namespace = sNamespace;
                    if (dtCopy != null)
                    {
                        dtCopy.Namespace = sNamespace;
                        dtCopy.TableName = DSDATA.Tables[0].TableName;
                    }
                }

                DSDATA.Tables.Clear();
                DSDATA.Tables.Add(dtCopy ?? dtData);
                DSDATA.CaseSensitive = false;
            }

            internal void RemoveDuplicatesFromDataTable(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                if (DSQUERY.SQLQuery.IndexOf("SELECT DISTINCT ", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    if (!DSQUERY.ConnectionSrc.SConnDriver.ToString()[..2].Equals("DB"))
                    {
                        SHSOperations SHS = new(JobParameters, DSQUERY, ref MyLog);
                        SHS.RewriteDataSetWithoutDuplicates(DSDATA.Tables[iTable]);
                    }
                }
            }

            internal void ApplySQLTransformations(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                try
                {
                    List<List<string>> sListMathOps = new();
                    List<string> sListAddedColumns = new();

                    if (DSDATA.Tables[iTable].Rows.Count > 0)
                    {
                        foreach (Query.QField sDtTransform in DSQUERY.QueryAnalyzer.Fields)
                        {
                            //rappel : 0->nom col / 1->nom col alias / 2->table / 3->table alias / 4->transform  
                            if (sDtTransform.Transformation.Length > 1)
                            {
                                //gestion du CASE
                                if (sDtTransform.Transformation.IndexOf("CASE ", StringComparison.InvariantCultureIgnoreCase) == 0)
                                {
                                    string[] sCase = new string[] { " WHEN ", " THEN ", " ELSE ", " END", " when ", " then ", " else ", " end", " When ", " Then ", " Else ", " End" };
                                    //CASE monchamp WHEN x THEN 1 WHEN y THEN 2 ELSE 3 END
                                    string[] sWhen = sDtTransform.Transformation[4..].Split(sCase, StringSplitOptions.RemoveEmptyEntries);
                                    //0 est forcément le champ conditionnel
                                    //1 est le premier when
                                    //2 est le premier then
                                    //3 est le deuxième when ou le else
                                    if (sWhen.Length >= 4)
                                    {
                                        bool bFieldExists = false;
                                        string sFieldCase = sWhen[0].Trim();

                                        string sElse = sWhen[^1].Replace("'", "").Trim();
                                        List<string[]> sWhenThen = new();
                                        for (int iS = 1; iS < sWhen.Length - 2; iS++)
                                        {
                                            sWhenThen.Add(new string[] { sWhen[iS].Replace("'", "").Trim(), sWhen[iS + 1].Trim() });
                                        }

                                        if (DSQUERY.QueryAnalyzer.Tables.Count == 1) //risque d'avoir saisi select * from x where x = 1 OU select * from x as y where x = 1 OU select * from x as y where y = 1
                                        {
                                            //on sait qu'avec 1 table, on a aucun alias dans le nom des colonnes
                                            if (sFieldCase.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                            {
                                                sFieldCase = sFieldCase.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                            }
                                            else if (sFieldCase.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                            {
                                                sFieldCase = sFieldCase.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                            }

                                            if (sElse.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                            {
                                                sElse = sElse.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                            }
                                            else if (sElse.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                            {
                                                sElse = sElse.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                            }

                                            foreach (string[] sWT in sWhenThen)
                                            {
                                                if (sWT[0].IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sWT[0] = sWT[0].Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                                }
                                                else if (sWT[0].IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sWT[0] = sWT[0].Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                                }
                                                if (sWT[1].IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sWT[1] = sWT[1].Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                                }
                                                else if (sWT[1].IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sWT[1] = sWT[1].Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                                }
                                            }
                                        }

                                        foreach (DataColumn dc in DSDATA.Tables[iTable].Columns)
                                        {
                                            if (dc.ColumnName.Equals(sFieldCase, StringComparison.InvariantCultureIgnoreCase) || dc.ColumnName.Equals(sFieldCase.Replace("\"", ""), StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                bFieldExists = true; sFieldCase = dc.ColumnName; break;
                                            }
                                        }
                                        if (bFieldExists)
                                        {
                                            AddColumn(DSDATA.Tables[iTable], sDtTransform.Alias.Trim(), Type.GetType("System.String"));
                                            PseudoCaseWhen(DSDATA.Tables[iTable], sDtTransform.Alias.Trim(), sFieldCase, sWhenThen, sElse, ref MyLog);
                                        }
                                    }
                                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassingcasewhen, sDtTransform.Transformation), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                                }
                                //gestion des fonctions SQL
                                else if (sDtTransform.Transformation.Contains('(') && sDtTransform.Transformation.EndsWith(")"))
                                {
                                    try
                                    {
                                        //gestion du plus resséré vers le plus large : substring(replace(x, 'a', 'b'), 0, 3)
                                        //string sScript = Toolbox.RemoveWhitespace(sDtTransform[4].ToUpper());
                                        //string[] sT = sDtTransform.Transformation.Split(Convert.ToChar("("));
                                        //int iQtePars = sT.Length; //on compte le nombre de parenthèses ouvertes dans la fonction SQL (pour gérer l'imbriquement : substring(replace(substring...
                                        string sS = ""; //paramètres
                                        string sField = ""; //nom du champ
                                        string sZ = ""; //nom de la fonction  
                                        string sAlias = "";
                                        string sTransformation = sDtTransform.Transformation;
                                        List<int> iOffset = new();
                                        int iIteration = 0;
                                        MatchCollection mcFunctions = Regex.Matches(sTransformation, "(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + ")\\s*\\({1}", RegexOptions.IgnoreCase);

                                        while (mcFunctions.Count > 0)
                                        {
                                            iIteration++;
                                            sField = sAlias;
                                            if (sS.Length > 0)
                                            {
                                                //SUBSTRING(3, CHARINDEX("test,bonjour", ",) + 3)
                                                if (iOffset.Count > 0) // on obtient un truc du genre SUBSTRING(3, SHS_FUNC_TRUCMUCHE + 3) : il faut viter aussi le +3
                                                {
                                                    for (int iO = 0; iO < iOffset.Count; iO++)
                                                    {
                                                        string sToReplace = Toolbox.RemoveRegexFromString(iO == 0 ? sS : sField) + "\\s*" + (iOffset[iO] > 0 ? "\\+\\s*" : "\\-\\s*") + Math.Abs(iOffset[iO]).ToString();
                                                        sTransformation = Regex.Replace(sTransformation, sToReplace, sField);
                                                    }
                                                }
                                                else
                                                {
                                                    sTransformation = sTransformation.Replace(sS, sField);
                                                }

                                            }

                                            iOffset.Clear();

                                            Toolbox.ExtractSQLFunction(ref sTransformation, ref sS, ref sField, ref sZ, ref iOffset);

                                            //gestion des champs encadrés genre CONCAT("Queue - Type (QL,QF,QFP,HSK,ISO)","Désignation") 
                                            //il faut virer les encadrements pour permettre l'analyse comparative entre les champs du dataset (non encadrés) et ceux contenus dans les fonctions (encadrés)
                                            string sScript = sS;

                                            //dernière ou unique itération
                                            //on est dans la boucle des fonctions imbriquées
                                            if (!sTransformation.Equals(sAlias))
                                            {
                                                sAlias = "SHS_FUNC_TMP_" + iIteration.ToString("00000");
                                            }

                                            if (DSQUERY.QueryAnalyzer.Tables.Count == 1) //risque d'avoir saisi  select * from x where x = 1 
                                                                                         //OU select * from x as y where x = 1       
                                                                                         //OU select * from x as y where y = 1
                                            {
                                                //on sait qu'avec 1 table, on a aucun alias dans le nom des colonnes
                                                if (sField.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sField = sField.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                                }
                                                else if (sField.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sField = sField.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                                }

                                                //problème : select SUBSTRING(test.field, 1, 3) from file as test
                                                if (sScript.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sScript = sScript.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                                    sTransformation = sTransformation.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", "");
                                                }
                                                else if (sScript.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                                {
                                                    sScript = sScript.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                                    sTransformation = sTransformation.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", "");
                                                }
                                            }

                                            switch (sZ)
                                            {
                                                case "ANONYMIZE":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, sDtTransform.FieldAnalyzer.ColumnLinqType);
                                                    PseudoAnonymization(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, JobParameters, DSQUERY, ref MyLog);
                                                    break;
                                                case "LENGTH":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.Int32"));
                                                    PseudoLength(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, iOffset, ref MyLog);
                                                    break;
                                                case "CONCAT":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    PseudoConcat(DSDATA.Tables[iTable], sScript, sAlias, ref MyLog);
                                                    break;
                                                case "CONVERT":
                                                    string sEnd = sS[(sS.IndexOf("(") + 1)..];
                                                    sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                                                    sEnd = sEnd.Replace("\"", "");
                                                    if (sEnd.StartsWith(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith("\"" + sField + "\"") || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                                                    {
                                                        MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                                                        string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                                                        if (sSplit.Length == 2 && DSDATA.Tables[iTable].Columns.Contains(sSplit[0])) // 0=field, 1=transformation (INTEGER, VARCHAR...)
                                                        {
                                                            AddColumn(DSDATA.Tables[iTable], sAlias, Toolbox.GetSystemTypeFromListTypes((SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sSplit[1].Trim().ToUpper())));
                                                            //copie des données
                                                            try
                                                            {
                                                                foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                                {
                                                                    dr[sAlias] = dr[sSplit[0]].ToString();
                                                                }
                                                            }
                                                            catch (Exception ex) { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CONVERT'" + Languages.Languages.mt_rework_bypassfunction02, sS), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                                        }
                                                        else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CONVERT'" + Languages.Languages.mt_rework_bypassfunction02, sS), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                                    }
                                                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CONVERT'" + Languages.Languages.mt_rework_bypassfunction03, sS), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                                    break;
                                                case "SUBSTRING":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoSubstring(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, ref MyLog);
                                                    break;
                                                case "CHARINDEX":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.Int32"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //si fonction imbriquée, pas de offset (sera géré par la f
                                                    PseudoCharindex(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, iOffset, ref MyLog);
                                                    break;
                                                case "ISNULL":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoIsNull(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, ref MyLog);
                                                    break;
                                                case "COALESCE":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoIsNull(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, ref MyLog);
                                                    break;
                                                case "LTRIM":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //sField = sField.EndsWith(")") ? sField[0..^1] : sField; //habituellement, une fonction contient des virgules, avec d'autres paramètres, sauf UPPER et LOWER
                                                    PseudoTrim(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, 1, ref MyLog);
                                                    break;
                                                case "RTRIM":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //sField = sField.EndsWith(")") ? sField[0..^1] : sField; //habituellement, une fonction contient des virgules, avec d'autres paramètres, sauf UPPER et LOWER
                                                    PseudoTrim(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, 2, ref MyLog);
                                                    break;
                                                case "TRIM":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //sField = sField.EndsWith(")") ? sField[0..^1] : sField; //habituellement, une fonction contient des virgules, avec d'autres paramètres, sauf UPPER et LOWER
                                                    PseudoTrim(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, 0, ref MyLog);
                                                    break;
                                                case "LPAD":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoPad(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, true, ref MyLog);
                                                    break;
                                                case "RPAD":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoPad(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, false, ref MyLog);
                                                    break;
                                                case "REPLACE":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    PseudoReplace(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, ref MyLog);
                                                    break;
                                                case "UPPER":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //sField = sField.EndsWith(")") ? sField[0..^1] : sField; //habituellement, une fonction contient des virgules, avec d'autres paramètres, sauf UPPER et LOWER
                                                    PseudoCaseChange(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, true, ref MyLog);
                                                    break;
                                                case "LOWER":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    //ChangeColumnType(DSDATA, sField.Trim(), sAlias, Type.GetType("System.String"));
                                                    //sField = sField.EndsWith(")") ? sField[0..^1] : sField;
                                                    PseudoCaseChange(DSDATA.Tables[iTable], sScript, sField.Trim(), sAlias, false, ref MyLog);
                                                    break;
                                                //aggrétation : toujours en premier, comme en SQL
                                                case "MAX":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                    {
                                                        dr[sAlias] = Convert.ToString(dr[sField]);
                                                    }
                                                    break;
                                                case "AVG":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.Decimal"));
                                                    foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                    {
                                                        dr[sAlias] = Convert.ToDecimal(dr[sField].ToString().Replace(".", ","));
                                                    }
                                                    break;
                                                case "COUNT":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.Int32"));
                                                    //foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                    //{ dr[sAlias] = Convert.ToInt64(dr[sField]); }
                                                    break;
                                                case "MIN":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.String"));
                                                    foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                    {
                                                        dr[sAlias] = Convert.ToString(dr[sField]);
                                                    }
                                                    break;
                                                case "SUM":
                                                    AddColumn(DSDATA.Tables[iTable], sAlias, Type.GetType("System.Decimal"));
                                                    foreach (DataRow dr in DSDATA.Tables[iTable].Rows)
                                                    {
                                                        dr[sAlias] = Convert.ToDecimal(dr[sField.ToString().Replace(".", ",")]);
                                                    }
                                                    break;

                                            }
                                            mcFunctions = Regex.Matches(sTransformation, "(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + ")\\s*\\({1}", RegexOptions.IgnoreCase);
                                        }
                                        //suppression des colonnes temporaires
                                        List<string> sDtToRemove = new();
                                        for (int iC = 0; iC < DSDATA.Tables[iTable].Columns.Count; iC++)
                                        {
                                            if (DSDATA.Tables[iTable].Columns[iC].ColumnName.StartsWith("SHS_FUNC_TMP_"))
                                            {
                                                sDtToRemove.Add(DSDATA.Tables[iTable].Columns[iC].ColumnName);
                                            }
                                        }
                                        for (int iC = 0; iC < sDtToRemove.Count; iC++)
                                        {
                                            if (iC == sDtToRemove.Count - 1) //dernière transformation : renommage colonne
                                            {
                                                //attention ! cas du SUM(nb_nombre) as nb_nombre : ne pourra pas renommer la colonne !
                                                if (DSDATA.Tables[iTable].Columns.Contains(sDtTransform.Alias))
                                                {
                                                    DSDATA.Tables[iTable].Columns.Remove(sDtTransform.Alias);
                                                }
                                                DSDATA.Tables[iTable].Columns[sDtToRemove[iC]].ColumnName = sDtTransform.Alias;
                                            }
                                            else { DSDATA.Tables[iTable].Columns.Remove(sDtToRemove[iC]); }
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (iNumThread + 1).ToString("00"), Languages.Languages.mt_rework_cantapplytransformation, sDtTransform.Transformation, ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }
                                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassingtransformation, sDtTransform.Transformation), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                            }
                            else //pas de transformation mais peut-être une colonne alias "li_test as li_alias"
                            {
                                if (!sDtTransform.Name.Equals(sDtTransform.Alias))
                                {
                                    if (DSDATA.Tables[iTable].Columns.Contains(sDtTransform.Name))
                                    {
                                        AddColumn(DSDATA.Tables[iTable], sDtTransform.Alias, DSDATA.Tables[iTable].Columns[sDtTransform.Name].DataType);
                                        foreach (DataRow dR in DSDATA.Tables[iTable].Rows)
                                        {
                                            dR[sDtTransform.Alias] = dR[sDtTransform.Name];
                                        }
                                    }
                                    //colonne de type 'test' as mycolumn
                                    else if (Regex.IsMatch(sDtTransform.Raw, "^['].[^']*[']" + "\\s+as\\s+" + sDtTransform.Alias + "$", RegexOptions.IgnoreCase))
                                    {
                                        if (!DSDATA.Tables[iTable].Columns.Contains(sDtTransform.Alias) && !DSDATA.Tables[iTable].Columns.Contains(sDtTransform.Name))
                                        {
                                            string sData = Regex.Match(sDtTransform.Raw, "^['].[^']*[']", RegexOptions.IgnoreCase).Value;
                                            sData = sData[1..^1];
                                            sData = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sData, JobParameters.DynParams);
                                            AddColumn(DSDATA.Tables[iTable], sDtTransform.Alias, Type.GetType("System.String"));
                                            foreach (DataRow dR in DSDATA.Tables[iTable].Rows)
                                            {
                                                dR[sDtTransform.Alias] = sData;
                                            }
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
            }

            internal void PseudoMathOperations(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {

                var QFAggregate = DSQUERY.QueryAnalyzer.Fields.Where(f => f.IsAggregate).ToList();

                if (DSQUERY.QueryAnalyzer.GroupBy.Count > 0 || QFAggregate.Count > 0)
                {
                    //TODO : supporter les fonctions mathématiques basiques comme pour les fonctions SQL (ex : SUM(id_test) + 10

                    DataView dv = null;
                    DataTable dtGroup = null;
                    List<string> sGroupBy = new();

                    try
                    {
                        dv = new DataView(DSDATA.Tables[iTable]);

                        foreach (Query.QGroupByOrderBy qGB in DSQUERY.QueryAnalyzer.GroupBy)
                        {
                            if (DSQUERY.QueryAnalyzer.Tables.Count == 1) //risque d'avoir saisi select * from x where x = 1 OU select * from x as y where x = 1 OU select * from x as y where y = 1
                            {
                                //on sait qu'avec 1 table, on a aucun alias dans le nom des colonnes
                                if (qGB.Alias.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                {
                                    sGroupBy.Add(qGB.Alias.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", ""));
                                }
                                else if (qGB.Alias.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                                {
                                    sGroupBy.Add(qGB.Alias.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", ""));
                                }
                                else { sGroupBy.Add(qGB.Alias); }
                            }
                            else { sGroupBy.Add(qGB.Alias); }
                        }
                        //cas des select count(*) from table (pas de groupy)
                        if (DSQUERY.QueryAnalyzer.GroupBy.Count == 0)
                        {
                            foreach (Query.QField qFAGG in QFAggregate)
                            {
                                sGroupBy.Add(qFAGG.Alias);
                            }
                        }
                        dtGroup = dv.ToTable(DSQUERY.QueryAnalyzer.GroupBy.Count > 0, sGroupBy.ToArray());
                    }
                    catch (Exception ex)
                    {
                        DSDATA.Tables[iTable].Clear();
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (iNumThread + 1).ToString("00"), Languages.Languages.mt_rework_cantperformgroupby01), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    if (sGroupBy.Count > 0 && dtGroup != null)
                    {
                        //on ajoute dans le nouveau dataset toutes les colonnes qui ne font pas parties du group by (donc qui sont normalement des opérations d'aggrégation)
                        List<Query.QField> sListAgg = new();

                        foreach (Query.QField qF in DSQUERY.QueryAnalyzer.Fields)
                        {
                            string sField = qF.Name;
                            string sFieldAlias = qF.Alias;

                            if (!sGroupBy.Contains(sFieldAlias))
                            {
                                int iIdx = -1;
                                if (DSDATA.Tables[iTable].Columns.Contains(sField))
                                {
                                    iIdx = DSDATA.Tables[iTable].Columns[sField].Ordinal;
                                }
                                else if (DSDATA.Tables[iTable].Columns.Contains(sFieldAlias))
                                {
                                    iIdx = DSDATA.Tables[iTable].Columns[sFieldAlias].Ordinal; sField = sFieldAlias;
                                }

                                string sColType = "System.Decimal";
                                if (iIdx > -1)
                                {
                                    sColType = DSDATA.Tables[iTable].Columns[iIdx].DataType.ToString();
                                    switch (sColType)
                                    {
                                        case "System.String":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(string));
                                            break;
                                        case "System.Decimal":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(decimal));
                                            break;
                                        case "System.DateTime":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(DateTime));
                                            break;
                                        case "System.DateTimeOffset":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(DateTime));
                                            break;
                                        case "System.Int32":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(int));
                                            break;
                                        case "System.Int16":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(short));
                                            break;
                                        case "System.Int64":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(long));
                                            break;
                                        case "System.Boolean":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(bool));
                                            break;
                                        case "System.Guid":
                                            dtGroup.Columns.Add(sFieldAlias, typeof(string));
                                            break;
                                        default:
                                            dtGroup.Columns.Add(sFieldAlias, typeof(string));
                                            break;
                                    }
                                }
                                else { dtGroup.Columns.Add(sFieldAlias, typeof(decimal)); }

                                dtGroup.Columns[sFieldAlias].Namespace = qF.TableAlias; //je colle le nom original de la colonne
                                sListAgg.Add(qF);

                                if (DSDATA.Tables[iTable].Columns[sField].DataType != Type.GetType(sColType))
                                {
                                    Toolbox.ConvertColumnType(DSDATA.Tables[iTable], DSDATA.Tables[iTable].Columns[sField], Type.GetType(sColType));
                                }
                            }
                        }

                        try
                        {
                            if (DSQUERY.QueryAnalyzer.GroupBy.Count > 0)
                            {
                                foreach (DataRow dr in dtGroup.Rows)
                                {
                                    string sFilter = "";

                                    for (int i = 0; i < sGroupBy.Count; i++)
                                    {
                                        sFilter = string.Concat(sFilter, "[", sGroupBy[i], "] = '", dr[sGroupBy[i]].ToString(), i == sGroupBy.Count - 1 ? "'" : "' AND ");
                                    }

                                    foreach (Query.QField qF in sListAgg)
                                    {
                                        //attention : en cas de SELECT id_test, COUNT(*) FROM matable.csv GROUP BY id_test, ça ne fonctione pas
                                        string sField = qF.Name;
                                        string sFieldAlias = qF.Alias;
                                        string sCompute = qF.Transformation; //a ce stade on a potentiellement le nom de la colonne d'origine dans la transformation, il faut la remplacer par l'alias                                                                       

                                        if (!qF.IsAggregate)
                                        {
                                            sCompute = sCompute.Replace(sField, string.Concat("[", sField, "]"));
                                        } //Dataset compute n'aime pas les champs table.champ
                                        else
                                        {
                                            switch (qF.Transformation[..3].ToUpper())
                                            {
                                                case "SUM":
                                                    sCompute = string.Concat("SUM([", sFieldAlias, "])");
                                                    break;
                                                case "MAX":
                                                    sCompute = string.Concat("MAX([", sFieldAlias, "])");
                                                    break;
                                                case "MIN":
                                                    sCompute = string.Concat("MIN([", sFieldAlias, "])");
                                                    break;
                                                case "AVG":
                                                    sCompute = string.Concat("AVG([", sFieldAlias, "])");
                                                    break;
                                                case "COU": //count
                                                    sCompute = string.Concat("COUNT([", sGroupBy[0], "])");
                                                    break;
                                            }
                                        }

                                        dr[sFieldAlias] = DSDATA.Tables[iTable].Compute(sCompute, sFilter);
                                    }
                                }
                            }
                            else
                            {
                                foreach (Query.QField qF in QFAggregate)
                                {
                                    switch (qF.Transformation[..3].ToUpper())
                                    {
                                        case "SUM":
                                            decimal sSum = Convert.ToDecimal(dtGroup.Compute("SUM([" + qF.Alias + "])", string.Empty));
                                            foreach (DataRow dr in dtGroup.Rows) { dr[qF.Alias] = sSum; }
                                            break;
                                        case "MAX":
                                            string sMax = dtGroup.Compute("MAX([" + qF.Alias + "])", string.Empty).ToString();
                                            foreach (DataRow dr in dtGroup.Rows) { dr[qF.Alias] = sMax; }
                                            break;
                                        case "MIN":
                                            string sMin = dtGroup.Compute("MAX([" + qF.Alias + "])", string.Empty).ToString();
                                            foreach (DataRow dr in dtGroup.Rows) { dr[qF.Alias] = sMin; }
                                            break;
                                        case "AVG":
                                            string sAvg = dtGroup.Compute("MAX([" + qF.Alias + "])", string.Empty).ToString();
                                            foreach (DataRow dr in dtGroup.Rows) { dr[qF.Alias] = sAvg; }
                                            break;
                                        case "COU": //count
                                            int iCount = dtGroup.Rows.Count;
                                            foreach (DataRow dr in dtGroup.Rows) { dr[qF.Alias] = iCount; }
                                            break;
                                    }
                                }
                                //retrait de toutes les lignes sauf la première
                                while (dtGroup.Rows.Count > 1)
                                {
                                    dtGroup.Rows.RemoveAt(1);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (iNumThread + 1).ToString("00"), Languages.Languages.mt_rework_cantperformgroupby02, string.Join(",", sGroupBy), " -> Math Operations : ", string.Join(",", sListAgg.Select(x => x.Transformation).ToArray()), ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }

                        DataSet dsNewData = new()
                        {
                            DataSetName = DSDATA.DataSetName,
                            Namespace = DSDATA.Namespace
                        };

                        for (int iT = 0; iT < DSDATA.Tables.Count; iT++)
                        {
                            if (iT == iTable)
                            {
                                dsNewData.Tables.Add(dtGroup);
                            }
                            else { dsNewData.Tables.Add(DSDATA.Tables[iT].Copy()); }
                        }
                        DSDATA.Clear();
                        DSDATA = null;
                        DSDATA = dsNewData.Copy();
                        DSDATA.CaseSensitive = false;
                        dsNewData.Clear();
                        dsNewData = null;
                    }
                }
            }

            private static void PseudoCaseWhen(DataTable DtData, string sFieldToPutIn, string sFieldCase, List<string[]> sWhenThen, string sElse, ref LogTools MyLog)
            {
                int iColCase = DtData.Columns.IndexOf(sFieldCase);
                int iColNewField = DtData.Columns.IndexOf(sFieldToPutIn);
                if (iColCase > -1 && iColNewField > -1)
                {
                    foreach (DataRow dr in DtData.Rows)
                    {
                        bool bFound = false;
                        //analyse des when then
                        foreach (string[] sTH in sWhenThen)
                        {
                            if (dr[iColCase].ToString().Equals(sTH[0]))
                            {
                                if (!sTH[1].Contains('\'', StringComparison.CurrentCulture) && DtData.Columns.Contains(sTH[1])) //on veut prendre la valeur d'une autre colonne
                                {
                                    dr[iColNewField] = dr[sTH[1]].ToString();
                                }
                                else { dr[iColNewField] = sTH[1].Replace("'", ""); } //sinon c'est une valeur simple
                                bFound = true;
                                break;
                            }
                        }
                        if (!bFound) { dr[iColNewField] = sElse; }
                    }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, Languages.Languages.mt_rework_casewhen_bypass, SQLTools_Enums.LOG_TYPEINFO.WNG); }
            }

            internal void ApplyOrderByTransformations(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                //table.Columns["column,name"].ColumnName = "Temporary";
                //table.DefaultView.Sort = "Temporary DESC";
                //table = table.DefaultView.ToTable();
                //table.Columns["Temporary"].ColumnName = "column,name";

                string sOrderBy = "";
                int iPos = 0;
                List<string[]> sUnsafeColumns = new();

                foreach (Query.QGroupByOrderBy qGB in DSQUERY.QueryAnalyzer.OrderBy)
                {
                    iPos++;
                    if (qGB.Alias.Contains(',')) //bug asp.net
                    {

                        if (DSDATA.Tables[iTable].Columns.Contains(qGB.Alias))
                        {
                            sUnsafeColumns.Add(new string[] { qGB.Alias, ("FuzTempCol_" + iPos.ToString("0000")) });
                            DSDATA.Tables[iTable].Columns[qGB.Alias].ColumnName = sUnsafeColumns.Last()[1];
                            sOrderBy = string.Concat(sOrderBy, "[", sUnsafeColumns.Last()[1], "]", " ", qGB.AscDesc, ",");
                        }
                    }
                    else
                    {
                        sOrderBy = string.Concat(sOrderBy, "[", qGB.Alias, "]", " ", qGB.AscDesc, ",");
                    }

                }

                if (sOrderBy.Length > 0)
                {
                    sOrderBy = sOrderBy[0..^1];

                    if (DSQUERY.QueryAnalyzer.Tables.Count == 1) //risque d'avoir saisi select * from x where x = 1 OU select * from x as y where x = 1 OU select * from x as y where y = 1
                    {
                        //on sait qu'avec 1 table, on a aucun alias dans le nom des colonnes
                        //if (sOrderBy.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", StringComparison.OrdinalIgnoreCase) > -1)
                        //{ sOrderBy = sOrderBy.Replace(DSQUERY.QueryAnalyzer.Tables[0].Alias + ".", ""); }
                        //else if (sOrderBy.IndexOf(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", StringComparison.OrdinalIgnoreCase) > -1)
                        //{ sOrderBy = sOrderBy.Replace(DSQUERY.QueryAnalyzer.Tables[0].Name + ".", ""); }
                    }

                    try
                    {
                        DataTable dtOrderBy = DSDATA.Tables[iTable].Clone();
                        DSDATA.Tables[iTable].DefaultView.Sort = sOrderBy;
                        dtOrderBy = DSDATA.Tables[iTable].DefaultView.ToTable();

                        DataSet dsNewData = new()
                        {
                            DataSetName = DSDATA.DataSetName,
                            Namespace = DSDATA.Namespace
                        };

                        for (int iT = 0; iT < DSDATA.Tables.Count; iT++)
                        {
                            if (iT == iTable)
                            {
                                dsNewData.Tables.Add(dtOrderBy);
                            }
                            else { dsNewData.Tables.Add(DSDATA.Tables[iT].Copy()); }
                        }
                        DSDATA.Clear();
                        DSDATA = null;
                        DSDATA = dsNewData.Copy();
                        dsNewData.Clear();
                        dsNewData = null;

                        foreach (string[] sCol in sUnsafeColumns)
                        {
                            DSDATA.Tables[iTable].Columns[sCol[1]].ColumnName = sCol[0];
                        }
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, string.Concat("T", (iNumThread + 1).ToString("00"), Languages.Languages.mt_rework_cantorderby01, sOrderBy, Languages.Languages.mt_rework_cantorderby02), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
            }

            internal void SetNullFields(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                try
                {
                    SHSOperations SHS = new(JobParameters, DSQUERY, ref MyLog);
                    SHS.SetDBNullInDataSet(DSDATA.Tables[iTable]);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            internal void AddOriginalColumnName(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                List<Query.QField> sDTColumns = new();

                for (int iF = 0; iF < DSQUERY.QueryAnalyzer.Fields.Count; iF++)
                {
                    if (DSQUERY.QueryAnalyzer.Fields[iF].Name.Equals("*") && DSQUERY.QueryAnalyzer.Fields[iF].Transformation.Length == 0)
                    {
                        foreach (DataColumn dC in DSDATA.Tables[iTable].Columns)
                        {
                            if (DSQUERY.QueryAnalyzer.Fields[iF].Table.Length == 0
                                || dC.Namespace.Equals(DSQUERY.QueryAnalyzer.Fields[iF].TableAlias, StringComparison.OrdinalIgnoreCase)
                                || dC.Namespace.Equals(DSQUERY.QueryAnalyzer.Fields[iF].Table, StringComparison.OrdinalIgnoreCase))
                            {
                                sDTColumns.Add(new Query.QField(dC.ColumnName, dC.ColumnName, DSQUERY.QueryAnalyzer.Fields[iF].Table, DSQUERY.QueryAnalyzer.Fields[iF].TableAlias, "", iF));
                            }
                            //cas particulier des subqueries
                            else
                            {
                                Query.QTable qT = DSQUERY.QueryAnalyzer.Tables.FirstOrDefault(t => t.Name.Equals(DSQUERY.QueryAnalyzer.Fields[iF].Table, StringComparison.OrdinalIgnoreCase));
                                if (qT != null && qT.IsSubQuery)
                                {
                                    Query q = new(JobParameters, qT.Name, DSQUERY.ConnectionSrc);
                                    if (dC.Namespace.Equals(q.QueryAnalyzer.Tables[0].Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        sDTColumns.Add(new Query.QField(dC.ColumnName, dC.ColumnName, DSQUERY.QueryAnalyzer.Fields[iF].Table, DSQUERY.QueryAnalyzer.Fields[iF].TableAlias, "", iF));
                                    }
                                }
                            }
                        }
                    }
                    else { sDTColumns.Add(DSQUERY.QueryAnalyzer.Fields[iF]); }
                }

                if (sDTColumns.Count > 0)
                {
                    DSQUERY.QueryAnalyzer.SetFieldsByQFields(sDTColumns);
                }
            }

            internal void RenameColumnsForSQL(int iNumThread, int iTable, Job JobParameters, ref LogTools MyLog)
            {
                for (int iC = 0; iC < DSQUERY.QueryAnalyzer.Fields.Count; iC++)
                {
                    DSQUERY.QueryAnalyzer.Fields[iC].Alias = Toolbox.RemoveSpecialCharacters(DSQUERY.QueryAnalyzer.Fields[iC].Alias, "_", true);
                }

                for (int iC = 0; iC < DSDATA.Tables[iTable].Columns.Count; iC++)
                {
                    DSDATA.Tables[iTable].Columns[iC].ColumnName = Toolbox.RemoveSpecialCharacters(DSDATA.Tables[iTable].Columns[iC].ColumnName, "_", true);
                }

            }

            private static DataTable PseudoSubstring(DataTable DtData, string sScript, string sField, string sAlias, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.StartsWith(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith(string.Concat("\"", sField, "\""), StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                {
                    sEnd = sEnd.Replace(sField, "");
                    string[] sSplit = sEnd.Split(",");

                    if (sSplit.Length == 3) // 0=champ, 1=valeur1, 2=valeur2
                    {
                        bool bPass1 = int.TryParse(sSplit[1].Trim(), out int iRa);
                        bool bPass2 = int.TryParse(sSplit[2].Trim(), out int iRb);
                        //string sParam1IsColumn = "";
                        //string sParam2IsColumn = "";

                        //if (!bPass1 && DtData.Columns.Contains(sSplit[1].Trim()))
                        //{
                        //    sParam1IsColumn = sSplit[1].Trim();
                        //    bPass1 = true;
                        //}
                        //if (!bPass2 && DtData.Columns.Contains(sSplit[2].Trim()))
                        //{
                        //    sParam2IsColumn = sSplit[2].Trim();
                        //    bPass2 = true;
                        //}

                        int iOffset1 = 0;
                        int iOffset2 = 0;

                        int iMath1 = 0;
                        int iMath2 = 0;

                        //Le numérique peut être devant ou derrière (1+ field) ou (field - 2)
                        string[] sPlusS1 = sSplit[1].Split(Convert.ToChar("+"));
                        Match mcNum;

                        //1 + test - 2  --> 1, test, 2
                        if (sPlusS1.Length > 1)
                        {
                            foreach (string s in sPlusS1)
                            {
                                mcNum = Regex.Match(s, "^\\s*\\d+\\s*$");
                                if (mcNum.Success)
                                {
                                    iOffset1 += Convert.ToInt32(mcNum.Value.Trim()); iMath1++;
                                }
                                else { sSplit[1] = s.Trim(); }
                            }
                        }
                        string[] sMoinsS1 = sSplit[1].Split(Convert.ToChar("-"));
                        if (sMoinsS1.Length > 1)
                        {
                            foreach (string s in sMoinsS1)
                            {
                                mcNum = Regex.Match(s, "^\\s*\\d+\\s*$");
                                if (mcNum.Success)
                                {
                                    iOffset1 -= Convert.ToInt32(mcNum.Value.Trim()); iMath1++;
                                }
                                else { sSplit[1] = s.Trim(); }
                            }
                        }

                        string[] sPlusS2 = sSplit[2].Split(Convert.ToChar("+"));
                        if (sPlusS2.Length > 1)
                        {
                            foreach (string s in sPlusS2)
                            {
                                mcNum = Regex.Match(s, "^\\s*\\d+\\s*$");
                                if (mcNum.Success)
                                {
                                    iOffset2 += Convert.ToInt32(mcNum.Value.Trim()); iMath2++;
                                }
                                else { sSplit[2] = s.Trim(); }
                            }
                        }
                        string[] sMoinsS2 = sSplit[2].Split(Convert.ToChar("-"));
                        if (sMoinsS2.Length > 1)
                        {
                            foreach (string s in sMoinsS2)
                            {
                                mcNum = Regex.Match(s, "^\\s*\\d+\\s*$");
                                if (mcNum.Success)
                                {
                                    iOffset2 -= Convert.ToInt32(mcNum.Value.Trim()); iMath2++;
                                }
                                else { sSplit[2] = s.Trim(); }
                            }
                        }

                        //on est pas à l'abri dun SUBSTRING(x, 1, 2 + 2)
                        if (iMath1 > 0)
                        {
                            if (iMath1 == ((sMoinsS1.Length == 1 ? 0 : sMoinsS1.Length) + (sPlusS1.Length == 1 ? 0 : sPlusS1.Length)))
                            {
                                bPass1 = true; iRa += iOffset1;
                            }
                        }
                        if (iMath2 > 0)
                        {
                            if (iMath2 == ((sMoinsS2.Length == 1 ? 0 : sMoinsS2.Length) + (sPlusS2.Length == 1 ? 0 : sPlusS2.Length)))
                            {
                                bPass2 = true; iRb += iOffset2;
                            }
                        }

                        string sData;

                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                foreach (DataRow dr in DtData.Rows)
                                {
                                    ////gestion du substring('data', charindex('xxx', ','))
                                    //int iParam1Offset = 0;
                                    //int iParam2Offset = 0;

                                    //if (sParam1IsColumn.Length > 0)
                                    //{
                                    //    iParam1Offset = Convert.ToInt32(dr[sParam1IsColumn]);
                                    //}
                                    //if (sParam2IsColumn.Length > 0)
                                    //{
                                    //    iParam2Offset = Convert.ToInt32(dr[sParam2IsColumn]);
                                    //}

                                    sData = dr[sField].ToString();

                                    if (!bPass1) // c'est pas un chiffre mais un nom de colonne (fonctions imbriquées)
                                    {
                                        int i = Convert.ToInt32(dr[sSplit[1].Trim()]) + iOffset1;
                                        if (sData.Length >= i && i > 0) { sData = sData[i..]; }
                                    }
                                    else
                                    {
                                        if (sData.Length >= iRa && iRa > 0) //contrôle de longueur absurde
                                        {
                                            sData = sData[iRa..];
                                        }
                                    }
                                    if (!bPass2)
                                    {
                                        //SHS_FUNC_TMP_00001
                                        int i = Convert.ToInt32(dr[sSplit[2].Trim()]) + iOffset2;
                                        if (sData.Length >= i && i > 0) { sData = sData[..i]; }
                                    }
                                    else
                                    {
                                        if (sData.Length >= iRb && iRb > 0) //contrôle de longueur absurde
                                        {
                                            sData = sData[..iRb];
                                        }
                                    }
                                    dr[dc] = sData;
                                }
                                break;
                            }
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'SUBSTRING'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'SUBSTRING'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoCharindex(DataTable DtData, string sScript, string sField, string sAlias, List<int> iOffset, ref LogTools MyLog)
            {
                int iO = 0;
                foreach (int i in iOffset)
                {
                    iO += i;
                }

                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.StartsWith(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith("\"" + sField + "\"") || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                {
                    MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                    string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                    if (sSplit.Length == 2) // 0=sub recherché, 1=chaine complete
                    {
                        //le script est valide
                        string sData;
                        int iIdx;

                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                string sPatternToSearch = sSplit[1];
                                if (sSplit[1].Length > 1)
                                {
                                    if (sSplit[1].StartsWith("\"") && sSplit[1].EndsWith("\""))
                                    {
                                        sPatternToSearch = sPatternToSearch[1..^1];
                                    }
                                    else if (sSplit[1].StartsWith("'") && sSplit[1].EndsWith("'"))
                                    {
                                        sPatternToSearch = sPatternToSearch[1..^1];
                                    }
                                }

                                //string sColumn = DtData.Columns.Contains(sField) ? sField : sAlias;
                                foreach (DataRow dr in DtData.Rows)
                                {
                                    sData = dr[sField].ToString();

                                    iIdx = sData.IndexOf(sPatternToSearch);
                                    dr[dc] = iIdx + iO;
                                }
                                break;
                            }
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CHARINDEX'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CHARINDEX'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoIsNull(DataTable DtData, string sScript, string sField, string sAlias, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.StartsWith(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith("\"" + sField + "\"") || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                {
                    MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                    string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                    if (sSplit.Length == 2) // 0=sub recherché, 1=chaine complete
                    {
                        //le script est valide
                        string sData;

                        bool bFromColumn = false;
                        if (DtData.Columns.Contains(sSplit[1].Trim()))
                        {
                            bFromColumn = true;
                        }
                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                foreach (DataRow dr in DtData.Rows)
                                {
                                    sData = dr[sField].ToString();
                                    if (sData.Length == 0)
                                    {
                                        if (bFromColumn)
                                        {
                                            dr[dc] = dr[sSplit[1].Trim()];
                                        }
                                        else { dr[dc] = sSplit[1].Trim().Replace("'", ""); }
                                    }
                                    else { dr[dc] = dr[sField]; }
                                }
                                break;
                            }
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ISNULL'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ISNULL'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoLength(DataTable DtData, string sScript, string sField, string sAlias, List<int> iOffset, ref LogTools MyLog)
            {
                int iO = 0;
                foreach (int i in iOffset)
                {
                    iO += i;
                }

                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.Equals(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.Equals(string.Concat("\"", sField, "\""), StringComparison.InvariantCultureIgnoreCase)) // on a bien au minimum FONCTION(CHAMP
                {
                    //le script est valide
                    string sData;

                    foreach (DataColumn dc in DtData.Columns)
                    {
                        if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (DataRow dr in DtData.Rows)
                            {
                                sData = dr[sField].ToString();
                                dr[dc] = sData.Length + iO;
                            }
                            break;
                        }
                    }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'LENGTH'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoTrim(DataTable DtData, string sScript, string sField, string sAlias, int bType, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.Equals(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.Equals(string.Concat("\"", sField, "\""), StringComparison.InvariantCultureIgnoreCase)) // on a bien au minimum FONCTION(CHAMP
                {
                    //le script est valide
                    string sData;

                    foreach (DataColumn dc in DtData.Columns)
                    {
                        if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (DataRow dr in DtData.Rows)
                            {
                                sData = dr[sField].ToString();
                                if (bType == 0)
                                {
                                    dr[dc] = sData.Trim();
                                }
                                else if (bType == 1)
                                {
                                    dr[dc] = sData.TrimStart();
                                }
                                else { dr[dc] = sData.TrimEnd(); }
                            }
                            break;
                        }
                    }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'R(L)TRIM'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoPad(DataTable DtData, string sScript, string sField, string sAlias, bool bLeft, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.StartsWith(sField) || sEnd.StartsWith(string.Concat("\"", sField, "\"")) || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                {
                    MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                    string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();


                    if (sSplit.Length == 3 && int.TryParse(sSplit[1].Trim(), out _) && char.TryParse(sSplit[2].Replace("'", "").Trim(), out char _)) //0=champ, 1=quantite champ, 2=caractère remplacement
                    {
                        //le script est valide
                        string sData;
                        int i1 = Convert.ToInt32(sSplit[1].Trim());
                        char c1 = Convert.ToChar(sSplit[2].Trim().Replace("'", ""));

                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                foreach (DataRow dr in DtData.Rows)
                                {
                                    sData = dr[sField].ToString();
                                    if (sData.Length > i1)
                                    {
                                        if (bLeft) { sData = sData[^i1..]; }
                                        else { sData = sData[..i1]; }
                                        dr[dc] = sData;
                                    }
                                    else
                                    {
                                        if (bLeft)
                                        {
                                            dr[dc] = sData.PadLeft(i1, c1);
                                        }
                                        else { dr[dc] = sData.PadRight(i1, c1); }
                                    }
                                }
                                break;
                            }
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'R(L)PAD'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'R(L)PAD'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                return DtData;
            }

            private static DataTable PseudoReplace(DataTable DtData, string sScript, string sField, string sAlias, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse

                MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE_REPLACE);
                string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                string s1 = sSplit[1].Replace("'", "").Trim();
                string s2 = sSplit[2].Replace("'", "").Trim();

                bool bFromColumn = false;
                if (DtData.Columns.Contains(sSplit[2].Trim()))
                {
                    bFromColumn = true;
                }

                if (sSplit.Length == 3) // 0=sub à remplace, 1=chaine complete,2=valeur remplacement
                {
                    if (sSplit[0].Equals(sField, StringComparison.InvariantCultureIgnoreCase) || sSplit[0].StartsWith(string.Concat("\"", sField, "\""), StringComparison.InvariantCultureIgnoreCase)) // on a bien au minimum FONCTION(CHAMP
                    {
                        //le script est valide
                        string sData;

                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                foreach (DataRow dr in DtData.Rows)
                                {
                                    sData = dr[sField].ToString();
                                    if (sData.Length > 0)
                                    {
                                        sData = sData.Replace(s1, bFromColumn ? dr[sSplit[2].Trim()].ToString() : s2);
                                        if (sData.Length == 0)
                                        {
                                            if (!dc.AllowDBNull) { dc.AllowDBNull = true; }
                                            dr[dc] = DBNull.Value;
                                        }
                                        else
                                        {
                                            dr[dc] = sData;
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                    else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'REPLACE'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'REPLACE'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }
                return DtData;
            }

            private static DataTable PseudoConcat(DataTable DtData, string sScript, string sAlias, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                                                     //(?:,|\n|^)(["`\[](?:(?:["`\[]["`\[])*[^"`\]]*)*["`\]]|[^"`\],\n]*|(?:\n|$)) "(?:,|\\n|^)([\"`\\[](?:(?:[\"`\\[][\"`\\[]) *[^\"`\\]]*)*[\"`\\]]|[^\"`\\],\\n]*|(?:\\n|$))"
                MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                List<bool> bsExists = new();
                for (int iS = 0; iS < sSplit.Length; iS++) { bsExists.Add(false); sSplit[iS] = sSplit[iS].Trim(); }

                if (sSplit.Length >= 2) // 0=première chaine, 1=deuxième chaine
                {
                    for (int iS = 0; iS < sSplit.Length; iS++)
                    {
                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sSplit[iS], StringComparison.InvariantCultureIgnoreCase) || string.Concat("\"", dc.ColumnName, "\"").Equals(sSplit[iS], StringComparison.InvariantCultureIgnoreCase))
                            {
                                bsExists[iS] = true; sSplit[iS] = dc.ColumnName; break;
                            }
                        }
                        if (!bsExists[iS])//pas une colonne mais une chaine
                        {
                            sSplit[iS] = sSplit[iS].Replace("\"", "").Replace("'", "");
                        }

                    }

                    //le script est valide
                    foreach (DataColumn dc in DtData.Columns)
                    {
                        if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (DataRow dr in DtData.Rows)
                            {
                                List<string> sData = new();
                                for (int iS = 0; iS < sSplit.Length; iS++)
                                {
                                    if (bsExists[iS])
                                    {
                                        sData.Add(dr[sSplit[iS]].ToString());
                                    }
                                    else { sData.Add(sSplit[iS]); }
                                }
                                dr[dc] = string.Join("", sData);
                            }
                            break;
                        }
                    }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'CONCAT'" + Languages.Languages.mt_rework_bypassfunction02, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                return DtData;
            }

            private static DataTable PseudoCaseChange(DataTable DtData, string sScript, string sField, string sAlias, bool bUpper, ref LogTools MyLog)
            {
                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse

                if (sEnd.Equals(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.Equals(string.Concat("\"", sField, "\""), StringComparison.InvariantCultureIgnoreCase)) // on a bien au minimum FONCTION(CHAMP
                {
                    //le script est valide
                    string sData;

                    foreach (DataColumn dc in DtData.Columns)
                    {
                        if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (DataRow dr in DtData.Rows)
                            {
                                sData = dr[sField].ToString();
                                dr[dc] = bUpper ? sData.ToUpper() : sData.ToLower();
                            }
                            break;
                        }
                    }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'UPPER/LOWER'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                return DtData;
            }

            public static DataTable PseudoAnonymization(DataTable DtData, string sScript, string sField, string sAlias, Job jParam, Query qQuery, ref LogTools MyLog)
            {
                bool bRandomFromItSelf = true;

                //si on fournit ANONIMIZATION(CHAMP, nomdefichier) et que le nom de fichier est valide, on tente de pomper une donnée au pif dedans
                //sinon, on met une valeur aléatoire
                List<string> sAnonomizationData = new();

                string sEnd = sScript[(sScript.IndexOf("(") + 1)..];
                sEnd = sEnd.Remove(sEnd.Length - 1); //on enlève dernière parenthèse
                if (sEnd.StartsWith(sField, StringComparison.InvariantCultureIgnoreCase) || sEnd.StartsWith("\"" + sField + "\"") || sEnd.StartsWith(sAlias)) // on a bien au minimum FONCTION(CHAMP
                {
                    MatchCollection mcCol = Regex.Matches(sEnd, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                    string[] sSplit = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();

                    if (sSplit.Length >= 2) // 0=sub recherché, 1=chaine complete, 2=index
                    {
                        string sFuncData = sSplit[1];
                        if (sFuncData.StartsWith("\"")) { sFuncData = sFuncData[1..]; }
                        else if (sFuncData.StartsWith("'")) { sFuncData = sFuncData[1..]; }

                        if (sFuncData.EndsWith("\"")) { sFuncData = sFuncData[..^1]; }
                        else if (sFuncData.EndsWith("'")) { sFuncData = sFuncData[..^1]; }

                        string sIndex = sSplit.Length > 2 ? sSplit[2] : "0";
                        if (sIndex.StartsWith("\"")) { sIndex = sIndex[1..]; }
                        else if (sIndex.StartsWith("'")) { sIndex = sIndex[1..]; }

                        if (sIndex.EndsWith("\"")) { sIndex = sIndex[..^1]; }
                        else if (sIndex.EndsWith("'")) { sIndex = sIndex[..^1]; }

                        string sPossibleFile = sFuncData;
                        //en 2ème index, le nom du fichier ou la méthoode

                        var JTemp = Job.CreateDummyJob(jParam.GlobalParameters, SQLTools_Enums.BDD.FI_CSV, sPossibleFile, Path.GetFileName(sPossibleFile));
                        Query Q = JTemp.Item2;
                        FITools fi = new FITools(JTemp.Item1, SQLTools_Enums.CLASS_PURPOSE.SRC, ref MyLog);
                        DataSet dsAno = fi.GetDataFromFile(SQLTools_Enums.BDD.FI_CSV, ref Q);
                        if (dsAno.Tables.Count > 0 && int.TryParse(sIndex, out int i) && dsAno.Tables[0].Columns.Count > i)
                        {
                            sAnonomizationData = SQLTools.BuildListFromDs(dsAno, 0, i);
                        }
                        else
                        {
                            sAnonomizationData = SQLTools.BuildListFromDs(dsAno);
                        }

                        if (sAnonomizationData.Count == 0)
                        {
                            if (sFuncData.ToUpper().Equals("RANDOM") || sFuncData.ToUpper().Equals("RND"))
                            {
                                bRandomFromItSelf = false;
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction04 + sPossibleFile + " -> " + Languages.Languages.mt_rework_bypassfunction05, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }


                    }

                    try
                    {
                        foreach (DataColumn dc in DtData.Columns)
                        {
                            if (dc.ColumnName.Equals(sAlias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                //obligé de faire une analyse des données pour pouvoir randomiser proprement
                                SHSOperations SHS = new(jParam, qQuery, ref MyLog);
                                List<SQLColumn> sSqlCol = SHS.GetListFieldsTypesFromDataset(DtData, DtData.Columns[sField].Ordinal, qQuery, SQLTools_Enums.CLASS_PURPOSE.SRC);

                                Toolbox.RandomData(sAnonomizationData, DtData, sSqlCol, dc, sField, bRandomFromItSelf, sScript, ref MyLog);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    { throw; }
                }
                else { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, null, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ANONYMIZE'" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG); }

                return DtData;
            }

            private static DataTable AddColumn(DataTable dtData, string sAlias, Type tColType)
            {
                if (!dtData.Columns.Contains(sAlias))
                {
                    dtData.Columns.Add(sAlias, tColType);
                }

                return dtData;
            }

        }

        #endregion

    }
}
