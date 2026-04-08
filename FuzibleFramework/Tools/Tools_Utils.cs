using HtmlAgilityPack;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static FuzibleFramework.SQLTools_Enums;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FuzibleFramework
{

    public class SHSOperations
    {
        private readonly Job JobParameters;
        private LogTools MyLog;
        private readonly Query FuzibleQuery;

        public SHSOperations(Job INIP, Query sQ, ref LogTools LogJob)
        {
            JobParameters = INIP;
            MyLog = LogJob;
            FuzibleQuery = sQ;
        }

        public bool IsFirstRowHeader(string[] sFile, string sCSVCharSeparator)
        {
            bool bIsHeader = false;
            int iFileLength = sFile.Length > JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER ? JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER : sFile.Length;

            //test sur une regex caractères interdits sur première ligne ? (entête = lettre ou chiffre ou underscore)

            if (sFile.Length >= 2)
            {
                string[] sArrFirstRow;

                sArrFirstRow = Regex.Matches(sFile[0 + JobParameters.CSVRowOffset], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();

                List<bool> bIsUnique = new();
                foreach (string s in sArrFirstRow)
                {
                    if (s.Length > 0) { bIsUnique.Add(true); } else { bIsUnique.Add(false); }
                }
                //--------------------------------------------------------------------------

                //test de la première ligne : on regarde si par chance, elle correspond aux champs spécifiés dans la requête
                bool bFirstRowMatchesQuery = true;
                int iMayQueryMatchHeader = 0;

                if (!FuzibleQuery.QueryAnalyzer.Fields[0].Name.Equals("*"))
                {
                    bIsHeader = true;
                    foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields)
                    {
                        //On a pas un SELECT * mais pour autant, le select contient des noms qui ne sont pas compris dans la première ligne du fichier : on en déduit que celui-ci ne possède pas d'entête
                        if (!sFile[JobParameters.CSVRowOffset].Contains(qF.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("Header mismatch between data and Job Query : ", qF.Name, " was not found in header. Query may fail)"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            bFirstRowMatchesQuery = false;
                            //break;
                        }
                        else { iMayQueryMatchHeader++; }
                    }
                }
                else { bFirstRowMatchesQuery = false; }

                //CAS 1 : la première ligne possède l'ensemble des champs requêtés : on en déduit que la première ligne du fichier est bien l'entête (logique)
                if (bFirstRowMatchesQuery)
                {
                    bIsHeader = true;
                }

                //CAS 2 : la première ligne ne matche pas la requête mais il semblerait que certains champs matchent quand même
                else if (iMayQueryMatchHeader > 0 && (!bFirstRowMatchesQuery))
                {
                    decimal dInt = iMayQueryMatchHeader * 100 / FuzibleQuery.QueryAnalyzer.Fields.Count;
                    decimal dRes = Math.Round(dInt, 0);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_header_mismatch, dRes.ToString(), " %"), SQLTools_Enums.LOG_TYPEINFO.INF);
                    bIsHeader = true;
                }

                ////CAS 2 : la première ligne ne possède aucun des champs requêtés : on en déduit qu'elle n'est pas un entête
                //else if (iMayQueryMatchHeader == 0 && !sQuery.QueryAnalyzer.Fields[0].Name.Equals("*"))
                //{
                //    bIsHeader = false;
                //}

                //AUTRES CAS : on teste la ressemblance des champs
                else
                {
                    //on va parcourir tout le fichier pour voir si l'entête est unique
                    MThread mtClass = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, sArrFirstRow.Length, JobParameters.GlobalParameters.MULTITHREADING_CORES);
                    //exécution des analyses en multithread
                    if (mtClass.QuantityOfThreadsToCompute > 0)
                    {
                        for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                        {
                            int numeroThread = numThread;
                            Task th = new(() => mtClass.IsFirstRowOfFileAnHeader(numeroThread, sFile, sArrFirstRow, bIsUnique, sCSVCharSeparator, iFileLength));
                            Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                            th.Start();
                        }

                        while (!mtClass.AreAllThreadsFinished)
                        {
                            //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                            Thread.Sleep(100);
                        }
                        if (mtClass.HasOperationBeenCancelled)
                        {
                            throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")");
                        }

                        bIsUnique = mtClass.IsHeaderRowUniqueInFile;
                    }
                    else
                    {
                        Exception ex = new(Languages.Languages.shs_header_computeko);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }

                    int iQteUnique = bIsUnique.Where(b => b == true).Count();
                    decimal dMoyUnique = Convert.ToDecimal(iQteUnique) / Convert.ToDecimal(bIsUnique.Count);

                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                        string.Concat(Languages.Languages.shs_header_unicity01, Math.Round(dMoyUnique * 100, 2).ToString(), Languages.Languages.shs_header_unicity02, bIsHeader.ToString()), SQLTools_Enums.LOG_TYPEINFO.DET);

                    int iLength = JobParameters.GlobalParameters.CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS;
                    if (sFile.Length < 1000 && sFile.Distinct().ToArray().Length < JobParameters.GlobalParameters.CSV_ROWS_OFFSET_BEFORE_DEPTH_ANALYSIS) { iLength = 1000; } //méthode de sorcier : certains fichiers ont peu de lignes (<1000) mais beaucoup de redondances.On veut 

                    if (dMoyUnique == 1 && sFile.Length > iLength) //constat : on peut affirmer qu'un grand fichier avec valeurs uniques est bien un header
                    {
                        bIsHeader = true;
                    }
                    else
                    {
                        List<decimal> dColAnalyzer = new();
                        List<decimal> dColAnalyzerUK = new();
                        for (int iB = 0; iB < bIsUnique.Count; iB++)
                        {
                            if (Monitoring.TaskCancellationToken.IsCancellationRequested)
                            { throw new OperationCanceledException(); }
                            if (!bIsUnique[iB]) //si le libellé de la colonne de la ligne est unique, on a POTENTIELLEMENT un entête
                            {
                                iQteUnique--;
                                dColAnalyzer.Add(AnalyzeHeaderForSmallFiles(sFile, iB, sCSVCharSeparator));
                            }
                            else { dColAnalyzerUK.Add(AnalyzeHeaderForSmallFiles(sFile, iB, sCSVCharSeparator)); }
                        }

                        if (dColAnalyzer.Count > 0 || dColAnalyzerUK.Count > 0)
                        {
                            //calcul de la longueur de la première ligne par rapport aux suivantes pour faire un calcul au prorata
                            //int iRMax = sFile.Length > JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER ? JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER : sFile.Length;

                            //int iLen1st = sFile[0 + JobParameters.CSVRowOffset].Length;
                            //int iLenAvg = 0;
                            //int iRows = 0;
                            //for (int iR = 0 + JobParameters.CSVRowOffset; iR < iRMax; iR++)
                            //{ iRows++; iLenAvg += sFile[iR].Length; }
                            //iLenAvg /= iRows; //152 vs 204
                            //decimal dRatio = (decimal)iLen1st / (decimal)iLenAvg; //calcul d'un facteur de pondération

                            //calcul de la ressemblance sur les colonnes aux valeurs non-uniques
                            decimal dHeader = 0;
                            foreach (decimal dH in dColAnalyzer)
                            {
                                dHeader += dH;
                            }
                            if (dColAnalyzer.Count > 0) { dHeader /= dColAnalyzer.Count; }

                            decimal dHeaderUK = 0;
                            foreach (decimal dH in dColAnalyzerUK)
                            {
                                dHeaderUK += dH;
                            }
                            if (dColAnalyzerUK.Count > 0) { dHeaderUK /= dColAnalyzerUK.Count; }

                            //dRatio sert à pondérer la moyenne calculée en comparant la longueur de la première ligne avec la longeur moyenne des autres
                            //dHeader *= dRatio;
                            //on pondère dHeader avec le nombre de valeurs uniques


                            //dHeader entre 0.5 et 1 = grosse ressemblance des données
                            //dMoyUnique entre 0.5 et 1 = faible taux de redondances des données (individuelles) de la première ligne

                            if (dMoyUnique == 1 && (dHeader == 0 ? dHeaderUK : dHeader) >= 0.5M) //on se pose pas de question, si tous les champs sont uniques, on considère direct que qu'il y a un header
                            {
                                bIsHeader = true;
                            }
                            else if (dHeader >= 0.5M && !sFile[0 + JobParameters.CSVRowOffset].Contains(sCSVCharSeparator))
                            {
                                //cas d'une liste type : 
                                //0100 - ADMINISTRATION
                                //0250 - BUANDERIE LINGERIE
                                //0800 - TRANSFERT
                                //6002 - SOINS DE SUITE
                                //6006 - LONG SEJOUR SOINS
                                bIsHeader = false;
                            }
                            else if (dMoyUnique >= JobParameters.GlobalParameters.CSV_HEADER_DETECTION_AVG_UNIQUE_OFFSET) //il y a plus de champs uniques dans la ligne 1
                            {
                                decimal dMoyPonderee = ((dHeader * dColAnalyzer.Count) + (dHeaderUK * dColAnalyzerUK.Count)) / (dColAnalyzer.Count + dColAnalyzerUK.Count);

                                //PRIORITE AUX CHAMPS IDENTIFIES COMME NON-UNIQUES, DISCRIMINANTS

                                //on priorise le fait qu'on a à priori un entête
                                //(plus de valeurs uniques sur ligne 1 que de valeurs non-uniques)
                                //on pondère les résultats en fonction de ça

                                //si la différence sur les colonnes non unique est très importante, et celle des colonnes uniques l'est assez pas 
                                if (dColAnalyzer.Count > 0)
                                {
                                    if (dHeader > JobParameters.GlobalParameters.CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET)
                                    {
                                        bIsHeader = true;
                                    }
                                    else
                                    {
                                        bIsHeader = false;
                                    }
                                }
                                else
                                {
                                    if (dHeaderUK > JobParameters.GlobalParameters.CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET)
                                    {
                                        bIsHeader = true;
                                    }
                                    else if (dHeaderUK <= JobParameters.GlobalParameters.CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET)
                                    {
                                        bIsHeader = false;
                                    }
                                }
                            }
                            else
                            {
                                //MOYENNE ENTRE LES CHAMPS UNIQUES ET NON-UNIQUES, PONDERES AUX QUANTITES
                                decimal dMoyPonderee = ((dHeader * dColAnalyzer.Count) + (dHeaderUK * dColAnalyzerUK.Count)) / (dColAnalyzer.Count + dColAnalyzerUK.Count);
                                decimal dRound = Math.Ceiling(dMoyPonderee * 100) / 100.0M;
                                //on priorise le faite qu'on n'a à priori pas d'entête
                                //(plus de valeurs non-uniques sur ligne 1 que de valeurs uniques)
                                //on pondère les résultats en fonction de ça
                                if (Math.Ceiling(dRound) >= JobParameters.GlobalParameters.CSV_HEADER_DETECTION_RESEMBLANCE_OFFSET)
                                {
                                    bIsHeader = true;
                                }
                                else
                                {
                                    bIsHeader = false;
                                }
                            }

                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null,
                                string.Concat(Languages.Languages.shs_header_unicity01, Math.Round(dHeader * 100, 2).ToString(), Languages.Languages.shs_header_unicity03, bIsHeader.ToString()), SQLTools_Enums.LOG_TYPEINFO.DET);
                        }
                    }
                }
            }

            return bIsHeader;

        }

        private decimal AnalyzeHeaderForSmallFiles(string[] sFile, int iCol, string sSeparator)
        {
            string[] sRow;
            //on va calculer la récurrence des pattern par ligne
            int[] iRec1 = new int[sFile.Length - JobParameters.CSVRowOffset];
            int[] iRec2 = new int[sFile.Length - JobParameters.CSVRowOffset];
            int[] iRec3 = new int[sFile.Length - JobParameters.CSVRowOffset];
            int[] iRec4 = new int[sFile.Length - JobParameters.CSVRowOffset];
            int[] iRec5 = new int[sFile.Length - JobParameters.CSVRowOffset];
            int[] iRec6 = new int[sFile.Length - JobParameters.CSVRowOffset];

            int iRMax = sFile.Length > JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER ? JobParameters.GlobalParameters.FILE_MAX_ROWS_ANALYZER : sFile.Length;
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < iRMax; iR++)
            {
                try
                {
                    //sRow = Regex.Matches(sFile[iR + JobParameters.CSVRowOffset], "(?:" + sSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sSeparator) ? m.Value[1..] : m.Value).ToArray();
                    sRow = Regex.Matches(sFile[iR + JobParameters.CSVRowOffset],
                            "(?:" + sSeparator + "|\\n|^)(\"(?:[^\"]|\"\")*\"|[^\"" + sSeparator + "\\n]*|(?:\\n|$))",
                            RegexOptions.None, TimeSpan.FromMilliseconds(500))
                            .Cast<Match>()
                            .Select(m => m.Value.StartsWith(sSeparator) ? m.Value[1..] : m.Value)
                            .Select(m => m.Trim('"').Replace("\"\"", "\"")) // Cette ligne permet de gérer les guillemets doubles
                            .ToArray();

                    if (iCol < sRow.Length)
                    {
                        string sVal = sRow[iCol];
                        //pattern chiffres
                        MatchCollection mcRecA = Regex.Matches(sVal, "[0-9]");
                        iRec1[iR - JobParameters.CSVRowOffset] = mcRecA.Count;
                        MatchCollection mcRec2 = Regex.Matches(sVal, "[A-Za-z]");
                        iRec2[iR - JobParameters.CSVRowOffset] = mcRec2.Count;
                        MatchCollection mcRec3 = Regex.Matches(sVal, "[^A-Za-z0-9]");
                        iRec3[iR - JobParameters.CSVRowOffset] = mcRec3.Count;
                        iRec4[iR - JobParameters.CSVRowOffset] = sVal.Length;
                        MatchCollection mcRec4 = Regex.Matches(sVal, "[A-Z]");
                        iRec4[iR - JobParameters.CSVRowOffset] = mcRec4.Count;
                        MatchCollection mcRec5 = Regex.Matches(sVal, "[a-z]");
                        iRec5[iR - JobParameters.CSVRowOffset] = mcRec5.Count;
                        //MatchCollection mcRec6 = Regex.Matches(sVal, "[\\s]");
                        //iRec6[iR - JobParameters.CSVRowOffset] = mcRec6.Count;
                        iRec6[iR - JobParameters.CSVRowOffset] = sVal.Length;
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_header_invalidrowlen, iR.ToString(), ",col:", iCol.ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
                catch (RegexMatchTimeoutException)
                {

                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            decimal dMoy1;
            int iCount1 = 0;
            int iLengthFile = sFile.Length - JobParameters.CSVRowOffset - 1;
            //calcul moyenne par ligne par rapport à la première ligne
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount1 += iRec1[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy1 = iCount1 / iLengthFile;

            decimal dMoy2;
            int iCount2 = 0;
            //calcul moyenne par ligne par rapport à la première ligne
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount2 += iRec2[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy2 = iCount2 / iLengthFile;

            decimal dMoy3;
            int iCount3 = 0;
            //calcul moyenne par ligne par rapport à la première ligne
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount3 += iRec3[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy3 = iCount3 / iLengthFile;

            decimal dMoy4;
            int iCount4 = 0;
            //calcul moyenne taille de chaque ligne / entête
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount4 += iRec4[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy4 = iCount4 / iLengthFile;

            decimal dMoy5;
            int iCount5 = 0;
            //calcul moyenne taille de chaque ligne / entête
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount5 += iRec5[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy5 = iCount5 / iLengthFile;

            decimal dMoy6;
            int iCount6 = 0;
            //calcul moyenne taille de chaque ligne / entête
            for (int iR = 0 + JobParameters.CSVRowOffset; iR < sFile.Length; iR++)
            {
                if (iR > 0)
                {
                    iCount6 += iRec6[iR - JobParameters.CSVRowOffset];
                }
            }
            dMoy6 = iCount6 / iLengthFile;

            decimal dDiff1 = iRec1[0] == 0 ? 0 : (dMoy1 / iRec1[0]);
            decimal dDiff2 = iRec2[0] == 0 ? 0 : (dMoy2 / iRec2[0]);
            decimal dDiff3 = iRec3[0] == 0 ? 0 : (dMoy3 / iRec3[0]);
            decimal dDiff4 = iRec4[0] == 0 ? 0 : (dMoy4 / iRec4[0]);
            decimal dDiff5 = iRec5[0] == 0 ? 0 : (dMoy5 / iRec5[0]);
            decimal dDiff6 = iRec6[0] == 0 ? 0 : (dMoy6 / iRec6[0]);

            //on ne dépasse pas les 100% (cas d'une valeur de première ligne / la moyenne du reste : ex : 11/7)
            if (dDiff1 > 1) { dDiff1 = dMoy1 == 0 ? 0 : (iRec1[0] / dMoy1); }
            if (dDiff2 > 1) { dDiff2 = dMoy2 == 0 ? 0 : (iRec2[0] / dMoy2); }
            if (dDiff3 > 1) { dDiff3 = dMoy3 == 0 ? 0 : (iRec3[0] / dMoy3); }
            if (dDiff4 > 1) { dDiff4 = dMoy4 == 0 ? 0 : (iRec4[0] / dMoy4); }
            if (dDiff5 > 1) { dDiff5 = dMoy5 == 0 ? 0 : (iRec5[0] / dMoy5); }
            if (dDiff6 > 1) { dDiff6 = dMoy6 == 0 ? 0 : (iRec6[0] / dMoy6); }

            //on ne doit pas tester le ratio quand les valeurs des tests sont à 0
            int iDiviseur = 6;
            if (dDiff1 == 0) { iDiviseur--; }
            if (dDiff2 == 0) { iDiviseur--; }
            if (dDiff3 == 0) { iDiviseur--; }
            if (dDiff4 == 0) { iDiviseur--; }
            if (dDiff5 == 0) { iDiviseur--; }
            if (dDiff6 == 0) { iDiviseur--; }

            decimal dHeader = iDiviseur == 0 ? 1 : ((dDiff1 + dDiff2 + dDiff3 + dDiff4 + dDiff5 + dDiff6) / iDiviseur);

            return dHeader;
        }

        public DataTable FillDtFromCSVArray(string[] sFileInRamA, List<SQLColumn> sListTypesFieldsSource, string sCSVCharSeparator, string sDTName, bool bFirstRowIsHead, Query sQ)
        {
            DataTable dtData = new(sDTName);
            //string[] sCSVSeparator = { JobParameters.CSVCharSeparator };
            DataColumn dtC = null;
            int iMaxCellsInFile = sListTypesFieldsSource.Count;
            string[] sRow;

            //test de comparaison entre l'entête et la première ligne pour éventuellement rajouter une colonne qui manquerait
            for (int cptR = (bFirstRowIsHead ? 1 : 0) + JobParameters.CSVRowOffset; cptR < sFileInRamA.Length; cptR++)
            {
                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                try
                {
                    //sRow = Regex.Matches(sFileInRamA[cptR], "(?:" + sCSVCharSeparator + "|\\n|^)(\"(?:(?:\"\")*[^\"]*)*\"|[^\"" + sCSVCharSeparator + "\\n]*|(?:\\n|$))", RegexOptions.None, TimeSpan.FromMilliseconds(500)).Cast<Match>().Select(m => m.Value.StartsWith(sCSVCharSeparator) ? m.Value[1..] : m.Value).ToArray();
                    sRow = CsvHelper.ParseCsvColumns(sFileInRamA[cptR]).ToArray();
                    if (FuzibleQuery.QueryAnalyzer.Fields[0].Equals("*"))
                    {
                        iMaxCellsInFile = sRow.Length > iMaxCellsInFile ? sRow.Length : iMaxCellsInFile;
                    }
                }
                catch { }
            }
            if (iMaxCellsInFile > sListTypesFieldsSource.Count) //excès de colonnes, on rajoute des colonnes dans le dataset
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_fillcsvdt_invalidheader01, iMaxCellsInFile.ToString(), Languages.Languages.shs_fillcsvdt_invalidheader02, sListTypesFieldsSource.Count.ToString(), ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                for (int iCol = 1; iCol <= (iMaxCellsInFile - sListTypesFieldsSource.Count); iCol++)
                {
                    sListTypesFieldsSource.Add(new SQLColumn(0, string.Concat("UNKNOWN_CELL_", iCol.ToString("000")), SQLTools_Enums.TYPE_DATA.TEXT, Type.GetType("System.String"), "", true, "", false, false));
                }
            }

            //on ordonne les colonnes selon l'index des champs requêtés
            //sListTypesFieldsSource.OrderBy(x => x.ColumnIndex);

            foreach (SQLColumn sC in sListTypesFieldsSource)
            {
                dtC = new DataColumn(sC.ColumnName.Length == 0 ? "COLUMN_" + (sListTypesFieldsSource.IndexOf(sC) + 1).ToString() : sC.ColumnName)
                {
                    AllowDBNull = sC.ColumnAllowsNullValues,
                    DataType = sC.ColumnLinqType,
                    Unique = sC.IsUnique
                };
                dtData.Columns.Add(dtC);
            }

            int iRowsToReplicate = sFileInRamA.Length - ((bFirstRowIsHead ? 1 : 0) + JobParameters.CSVRowOffset);

            MThread mtClass = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, iRowsToReplicate, JobParameters.GlobalParameters.MULTITHREADING_CORES);
            if (mtClass.QuantityOfThreadsToCompute > 0)
            {
                //exécution des analyses en multithread
                for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                {
                    int numeroThread = numThread;
                    mtClass.INIT_FillDatatableFromCSVArray(dtData);
                    Task th = new(() => mtClass.FillDatatableFromCSVArray(numeroThread, sFileInRamA, sCSVCharSeparator, bFirstRowIsHead, sListTypesFieldsSource, sQ));
                    Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                    th.Start();
                }

                while (!mtClass.AreAllThreadsFinished)
                {
                    Thread.Sleep(100);
                }
                if (mtClass.HasOperationBeenCancelled)
                { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();


                return mtClass.GetFillDatatableFromCSV;
            }
            else
            {
                Exception ex = new(Languages.Languages.shs_fillcsvdt_ko);
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);
                return dtData;
            }
        }

        public List<string[]> GetListStringFieldsFromDt(DataTable dtSource)
        {
            List<string[]> sFields = new();

            foreach (DataColumn dtC in dtSource.Columns)
            {
                if (!JobParameters.DISALLOWED_SQL_COLUMNS.Contains(dtC.ColumnName.ToUpper()))
                {
                    sFields.Add(new string[2] { dtC.ColumnName, dtC.ColumnName });
                }
            }

            return sFields;
        }

        public void RewriteDataSetWithoutDuplicates(DataTable dtData)
        {
            //MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, "Distinct Operation on Source Data : " + dtData.Rows.Count.ToString() + " Rows", SQLTools_Enums.LOG_TYPEINFO.INF);
            try
            {
                List<RowAndIndex> sRowsTmp = new();
                for (int iR = 0; iR < dtData.Rows.Count; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    sRowsTmp.Add(new RowAndIndex(string.Join("", dtData.Rows[iR].ItemArray), iR));
                }
                //Array.Sort(sRows.ToArray(), delegate (RowAndIndex ri1, RowAndIndex ri2) { return ri1.SRow.CompareTo(ri2.SRow); });
                var sRows = sRowsTmp.OrderBy(sR => sR.SRow).ToList();
                sRowsTmp.Clear();
                sRowsTmp = null;

                bool BTuple = false;
                List<DataRow> drRowsToRemove = new();
                for (int iR = 1; iR < sRows.Count; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    if (sRows[iR].SRow.Equals(sRows[iR - 1].SRow))
                    {
                        BTuple = true;
                    }
                    else { BTuple = false; }
                    if (BTuple) { drRowsToRemove.Add(dtData.Rows[sRows[iR - 1].IRow]); }
                }

                foreach (DataRow dr in drRowsToRemove)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    dtData.Rows.Remove(dr);
                }
                dtData.AcceptChanges();

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_distinct_info01 + drRowsToRemove.Count.ToString() + Languages.Languages.shs_distinct_info02, SQLTools_Enums.LOG_TYPEINFO.INF);

                drRowsToRemove.Clear();
                drRowsToRemove = null;
                sRows.Clear();
                sRows = null;
            }
            catch { }
        }

        public List<SQLColumn> GuessPKFromQuery(DataTable dtSource, SQLTools_Enums.TYPE_DATA SqlCharTypeCompatibility, List<SQLColumn> sListColumnsToCkeckExistence)
        {
            int iCountRows = 0;
            int iCountDistinct = 0;
            bool bPKFound = false;
            List<SQLColumn> sListColumnsPK = new();

            iCountRows = dtSource.Rows.Count;

            foreach (DataColumn dcK in dtSource.Columns)
            {
                iCountDistinct = -1;

                //Attention, je pars du postulat qu'une clé unique ne peut être déterminée qu'à partir d'un champ de type INTEGER
                //if (dcK.DataType == System.Type.GetType("System.Int16") || dcK.DataType == System.Type.GetType("System.Int32") || dcK.DataType == System.Type.GetType("System.Int64"))
                //{
                iCountDistinct = Toolbox.GetDistinctRecords(dtSource, new SQLColumn[] { SQLColumn.SQLColumnFromDataColumn(dcK, SqlCharTypeCompatibility) }, MyLog, 1);
                //}

                var SQLCPK = SQLColumn.SQLColumnFromDataColumn(dcK, SqlCharTypeCompatibility);

                bool bOKBothTargetAndSource = false;

                if (sListColumnsToCkeckExistence != null)
                {
                    foreach (SQLColumn SC in sListColumnsToCkeckExistence)
                    {
                        if (SC.ColumnName.Equals(SQLCPK.ColumnName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            bOKBothTargetAndSource = true;
                        }
                    }
                }
                else { bOKBothTargetAndSource = true; }

                if (iCountDistinct == iCountRows && bOKBothTargetAndSource)
                {
                    bPKFound = true;
                    sListColumnsPK.Add(SQLCPK);
                    break;
                }
            }

            //test de combinaisons sur 2 colonnes
            int iQteTests = 1;
            while (!bPKFound)
            {
                iQteTests++;

                if (!bPKFound && dtSource.Columns.Count >= iQteTests)
                {
                    List<SQLColumn[]> sListColumnCombination = Toolbox.GetColumnsCombinationsFromDataset(dtSource, iQteTests);

                    if (sListColumnCombination.Count > 0)
                    {
                        List<List<SQLColumn>> sFinalList = new();

                        int iCombinations = sListColumnCombination.Count;
                        MThread mtClass = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, iCombinations, JobParameters.GlobalParameters.MULTITHREADING_CORES);
                        //exécution des analyses en multithread
                        if (mtClass.QuantityOfThreadsToCompute > 0)
                        {
                            for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
                            {
                                int numeroThread = numThread;
                                mtClass.INIT_FindPrimaryKey();
                                Task th = new(() => mtClass.FindPrimaryKey(numeroThread, sListColumnCombination, dtSource, iQteTests));
                                Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                                th.Start();
                            }

                            while (!mtClass.AreAllThreadsFinished)
                            {
                                //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                                Thread.Sleep(100);
                            }
                            if (mtClass.HasOperationBeenCancelled)
                            { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }

                            sFinalList = mtClass.GetPrimaryKeyFields;
                        }
                        else
                        {
                            Exception ex = new(Languages.Languages.shs_findpk_ko);
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, "", SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }

                        foreach (List<SQLColumn> sPKL in sFinalList)
                        {
                            if (bPKFound) //potentiellement, plusieurs combinaisons ont ramené des possibles clés primaires, par défaut on ne prendra que la première détectée.
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_findpk_multiplekeys + string.Join(",", sPKL.Select(s => s.ColumnName))), SQLTools_Enums.LOG_TYPEINFO.DET);
                            }
                            else
                            {
                                sListColumnsPK = sPKL;
                                if (sListColumnsPK.Count > 0) { bPKFound = true; }
                            }
                        }
                    }
                    else { bPKFound = true; }
                }
                if (iQteTests == 5) { bPKFound = true; } //je ne veux pas tester plus de 5 champs
            }

            //cas particulier du mode synchro avec mémorisation des anciennes lignes : la clé primaire doit être augmentée de la colonne Timestamp
            if (sListColumnsPK.Count > 0 && JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN))
            {
                sListColumnsPK.Add(new SQLColumn(sListColumnsPK.Count, JobParameters.GlobalParameters.SQL_SYNCHRO_STORE_COLUMN, SQLTools_Enums.TYPE_DATA.BIGINT, Type.GetType("System.Int64"), "0", false, "", false, false));
            }

            //Cas foireux : la colonne est unique, mais ce n'est pas une clé primaire
            //Cas foireux : clé primaire > 5 colonnes non supporté !
            FuzibleQuery?.QueryAnalyzer.AddQueryProperty(dtSource.TableName, SQLTools_Enums.QUERY_PROPERTIES.GUESSED_PRIMARY_KEY, "True", true);

            return sListColumnsPK;
        }

        public void SetDBNullInDataSet(DataTable dtData)
        {
            int iQteColumns = dtData.Columns.Count;

            MThread mtClass = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, iQteColumns, JobParameters.GlobalParameters.MULTITHREADING_CORES);
            //exécution des analyses en multithread
            for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
            {
                int numeroThread = numThread;
                Task th = new(() => mtClass.FindDBNullInDataTable(numeroThread, dtData));
                Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                th.Start();
            }

            while (!mtClass.AreAllThreadsFinished)
            {
                //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }
            if (mtClass.HasOperationBeenCancelled)
            { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }

        }

        public DataSet CompareDataset(DataTable dtSource, DataTable dtTarget, ref List<SQLColumn> sFieldsPrimaryKey, SQLTools_Enums.CLASS_PURPOSE ClassPurpose, Query sQ)
        {
            List<SQLColumn> sqlCToReplace = new();
            List<string> sColumnNamesToReplace = new();

            //alignement des dataset (SELECT act.ACT_ID as "act.ACT_ID", li_activite FROM myTable, quand requêté sur cible, donne un ColumnName en act.ACT_ID alors que la source est en ACT_ID (rôle de FillSHS...))
            for (int iCT = 0; iCT < dtTarget.Columns.Count; iCT++)
            {
                for (int iCS = 0; iCS < dtSource.Columns.Count; iCS++)
                {
                    if (!dtTarget.Columns[iCT].ColumnName.Equals(dtSource.Columns[iCS].ColumnName) && dtTarget.Columns[iCT].ColumnName.Equals(dtSource.Columns[iCS].Caption))
                    {
                        sqlCToReplace.Add(sFieldsPrimaryKey.First(f => f.ColumnName.Equals(dtTarget.Columns[iCT].ColumnName)));
                        sColumnNamesToReplace.Add(dtTarget.Columns[iCT].ColumnName);
                        dtTarget.Columns[iCT].ColumnName = dtSource.Columns[iCS].ColumnName;
                        dtTarget.Columns[iCT].Caption = sColumnNamesToReplace.Last();
                        break;
                    }
                }
            }

            //réarrangement des colonnes de dataset : la source et la cible peuvent avoir les mêmes colonnes, mais pas dans le même ordre !
            //il faut d'abord enlever dans les sources les colonnes qui pourraient être en trop : il peut y avoir un nombre de colonnes différent de la source !

            List<string> sListColsToRemove = new();

            for (int iCpt = 0; iCpt < dtTarget.Columns.Count; iCpt++)
            {
                if (!dtSource.Columns.Contains(dtTarget.Columns[iCpt].ColumnName))
                {
                    sListColsToRemove.Add(dtTarget.Columns[iCpt].ColumnName);
                }
            }

            foreach (string sC in sListColsToRemove) { dtTarget.Columns.Remove(sC); }
            sListColsToRemove.Clear();

            for (int iCpt = 0; iCpt < dtSource.Columns.Count; iCpt++)
            {
                if (!dtTarget.Columns.Contains(dtSource.Columns[iCpt].ColumnName))
                {
                    sListColsToRemove.Add(dtSource.Columns[iCpt].ColumnName);
                }
            }
            //attention : j'ai désactivé la ligne ci-dessous en avril 2019 car dans le cas des multi-target (avec des colonnes différentes), ça me posait problème qu'on vire des colonnes de la source
            //foreach (string sC in sListColsToRemove) { dsSource.Tables[0].Columns.Remove(sC); }
            sListColsToRemove.Clear();

            //réarrangement des colonnes de dataset : la source et la cible peuvent avoir les mêmes colonnes, mais pas dans le même ordre !
            foreach (DataColumn dC in dtSource.Columns)
            {
                string sColName = dC.ColumnName;

                dtSource.Columns[sColName].SetOrdinal(dC.Ordinal);
                if (dtTarget.Columns.Contains(sColName))
                {
                    int iOrdinal = dC.Ordinal;
                    if (iOrdinal >= dtTarget.Columns.Count) { iOrdinal = dtTarget.Columns.Count - 1; }
                    dtTarget.Columns[sColName].SetOrdinal(iOrdinal);
                }
            }

            bool bOk = true;

            //comparaison des types sur la PK et convert si nécessaire
            foreach (SQLColumn sqlC in sFieldsPrimaryKey)
            {
                if ((dtSource.Columns.Contains(sqlC.ColumnName) || dtSource.Columns.Cast<DataColumn>().Any(c => c.Caption.Equals(sqlC.ColumnName)))
                    && (dtTarget.Columns.Contains(sqlC.ColumnName) || dtTarget.Columns.Cast<DataColumn>().Any(c => c.Caption.Equals(sqlC.ColumnName))))
                {
                    DataColumn dtCSource = dtSource.Columns[sqlC.ColumnName] ?? dtSource.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(sqlC.ColumnName));
                    DataColumn dtCTarget = dtTarget.Columns[sqlC.ColumnName] ?? dtTarget.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(sqlC.ColumnName)); ;
                    if (dtCSource.DataType != dtCTarget.DataType)
                    {
                        //privilégier la cible !                        
                        Toolbox.ConvertColumnType(dtSource, dtCSource, dtCTarget.DataType);
                        //alerte ? en message detail
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.shs_comparedt_convertcolumntype + dtCSource.ColumnName + "->" + dtCTarget.DataType.ToString(), SQLTools_Enums.LOG_TYPEINFO.DET);
                    }
                }
            }


            //le Dataset de retour aura 3 datatables : une pour l'insert, une pour l'update, une pour le delete

            var dtCPKS = new DataColumn[sFieldsPrimaryKey.Count];
            var dtCPKC = new DataColumn[sFieldsPrimaryKey.Count];

            for (int cpt = 0; cpt < sFieldsPrimaryKey.Count; cpt++)
            {
                string sF = sFieldsPrimaryKey[cpt].ColumnName;
                dtCPKS[cpt] = dtSource.Columns[sF] ?? dtSource.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(sF));
                dtCPKC[cpt] = dtTarget.Columns[sF] ?? dtTarget.Columns.Cast<DataColumn>().FirstOrDefault(c => c.Caption.Equals(sF));
            }

            try
            {
                dtSource.PrimaryKey = dtCPKS;
            }
            catch (Exception ex)
            {
                bOk = false;
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.shs_comparedt_unabletopksource + string.Join(",", Toolbox.GetColumnNamesFromSQLColumnObjects(sFieldsPrimaryKey)) + ") - " + ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }

            try
            {
                dtTarget.PrimaryKey = dtCPKC;
            }
            catch (Exception ex)
            {
                bOk = false;
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.shs_comparedt_unabletopktarget + string.Join(",", Toolbox.GetColumnNamesFromSQLColumnObjects(sFieldsPrimaryKey)) + ") - " + ex.Message, sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }

            if (string.Join(",", dtCPKS.Select(x => x.ColumnName.ToLower())) != string.Join(",", dtCPKC.Select(x => x.ColumnName.ToLower())))
            {
                bOk = false;
                Exception ex = new(Languages.Languages.shs_comparedt_pkmismatch);
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, ex.Message + string.Join(",", Toolbox.GetColumnNamesFromSQLColumnObjects(sFieldsPrimaryKey)) + ")", sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
            }

            if (dtSource.Rows.Count > 0 && bOk)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.shs_comparedt_perform + string.Join(",", Toolbox.GetColumnNamesFromSQLColumnObjects(sFieldsPrimaryKey)) + ")", SQLTools_Enums.LOG_TYPEINFO.INF);

                //en mode de synchronisation "normal" (sans le synchro tag) on ajoute cette colonne à la liste des colonnes à ne pas utiliser dans le process de comparaison
                if (!JobParameters.SynchroTargetTableBehavior.ToString().Contains("TAG", StringComparison.CurrentCulture))
                {
                    if (JobParameters.DISALLOWED_SQL_COLUMNS.Contains("SYNCHRO_TAG")) { JobParameters.DISALLOWED_SQL_COLUMNS.Remove("SYNCHRO_TAG"); }
                }

                //subtilité de cet appel multithread : je retire volontairement un processeur pour que le calcul imtstart/imtstop se fasse correctement
                //ensuite, je travaille donc avec un coeur en moins, mais je m'en réserve un pour le calcul des DELETE
                MThread mtClassUID = new(JobParameters, ClassPurpose, MyLog, dtSource.Rows.Count, JobParameters.GlobalParameters.MULTITHREADING_CORES - 1); //je laisse un coeur dispo pour traiter le DELETE

                //dégueulasse ! TODO
                SQLTools SQLTarget = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);

                for (int numThread = 0; numThread <= mtClassUID.QuantityOfThreadsToCompute; numThread += 1)
                {
                    mtClassUID.INIT_CompareDataTables(dtSource, dtTarget, FuzibleQuery, sFieldsPrimaryKey, JobParameters.SynchroTargetTableBehavior);
                    int numeroThread = numThread;
                    if (numeroThread < mtClassUID.QuantityOfThreadsToCompute)
                    {
                        Task thUI = new(() => mtClassUID.CompareDataTablesUI(numeroThread, SQLTarget.Connection, sColumnNamesToReplace.Count > 0, FuzibleQuery.OutputTable, FuzibleQuery));
                        Monitoring.AddThread(thUI, System.Reflection.MethodBase.GetCurrentMethod());
                        thUI.Start();
                    }
                    else //gestion des DELETE sur le dernier thread
                    {
                        Task thD = new(() => mtClassUID.CompareDataTablesD(numeroThread, sColumnNamesToReplace.Count > 0, FuzibleQuery.OutputTable, FuzibleQuery));
                        Monitoring.AddThread(thD, System.Reflection.MethodBase.GetCurrentMethod());
                        thD.Start();
                    }
                }

                while (!mtClassUID.AreAllThreadsFinished)
                {
                    Thread.Sleep(100);
                }
                if (mtClassUID.HasOperationBeenCancelled)
                { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
                //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < sColumnNamesToReplace.Count; i++) //remplacement du nom de la clé pour que la cible matche (act.ACT_ID)
                {
                    sFieldsPrimaryKey.First(sql => sql.ColumnName.Equals(sqlCToReplace[i].ColumnName)).ColumnName = sColumnNamesToReplace[i];
                }

                List<string> sColsWithChanges = mtClassUID.ListColumnsChanges_CompareDataTables;
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.shs_comparedt_changes + string.Join(", ", sColsWithChanges), SQLTools_Enums.LOG_TYPEINFO.DET);

                return mtClassUID.GetDataset_CompareDataTables;
            }
            else
            {
                DataTable dtInsert = new("INSERT");
                DataTable dtUpdate = new("UPDATE");
                DataTable dtDelete = new("DELETE");
                DataTable dtUpdateOldData = new("UPDATE_OLDDATA");
                DataSet dsFinal = new();
                dsFinal.Tables.Add(dtInsert);
                dsFinal.Tables.Add(dtUpdate);
                dsFinal.Tables.Add(dtDelete);
                dsFinal.Tables.Add(dtUpdateOldData);

                for (int i = 0; i < sColumnNamesToReplace.Count; i++) //remplacement du nom de la clé pour que la cible matche (act.ACT_ID)
                {
                    sFieldsPrimaryKey.First(sql => sql.ColumnName.Equals(sqlCToReplace[i].ColumnName)).ColumnName = sColumnNamesToReplace[i];
                }

                return dsFinal;
            }

        }

        public DataTable DataTransformation(DataTable dtData, ref Query sQ)
        {
            int iRows = dtData.Rows.Count - 1; //on retire 1 parce qu'il y a toujours la ligne d'entête 

            if (iRows >= 1)
            {
                DataTable dtReworkedData = new();

                switch (JobParameters.HyperFileArrayFieldTransformation)
                {
                    case 1:
                        try { dtReworkedData = PivotDataByArrayFields(dtData); }
                        catch (Exception ex) { dtReworkedData.Namespace = "ABNORMAL"; MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_transform_abnormal, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        sQ.QueryAnalyzer.AddQueryProperty(dtData.TableName, SQLTools_Enums.QUERY_PROPERTIES.DATA_TRANSFORMATION, Languages.Languages.shs_transform_hyperfile, true);
                        break;
                    case 2:
                        try { dtReworkedData = PivotDataByCommonRoot(dtData); }
                        catch (Exception ex) { dtReworkedData.Namespace = "ABNORMAL"; MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_transform_abnormal, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        sQ.QueryAnalyzer.AddQueryProperty(dtData.TableName, SQLTools_Enums.QUERY_PROPERTIES.DATA_TRANSFORMATION, Languages.Languages.shs_transform_commonroot, true);
                        break;
                    case 3:
                        try { dtReworkedData = PivotDataFromRowsToColumns(dtData); }
                        catch (Exception ex) { dtReworkedData.Namespace = "ABNORMAL"; MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_transform_abnormal, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                        sQ.QueryAnalyzer.AddQueryProperty(dtData.TableName, SQLTools_Enums.QUERY_PROPERTIES.DATA_TRANSFORMATION, Languages.Languages.shs_transform_rowstocol, true);
                        break;
                }

                if (dtReworkedData.Namespace.Equals("ABNORMAL"))
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_transform_abnormal, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    sQ.QueryAnalyzer.AddQueryProperty(dtData.TableName, SQLTools_Enums.QUERY_PROPERTIES.DATA_TRANSFORMATION_ERROR, Languages.Languages.shs_transform_abnormalprop, true);
                }

                //contrôle d'entête anormal sur la transformation du fichier
                if (dtReworkedData.Namespace.Equals("ABNORMAL") && JobParameters.DataTransformDontTransformIfVariableArraySizes)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_transform_inputrows, dtData.Rows.Count.ToString(), Languages.Languages.shs_transform_outputrows + dtData.Rows.Count.ToString()), SQLTools_Enums.LOG_TYPEINFO.INF);
                    return dtReworkedData;
                }
                else
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_transform_inputrows, dtData.Rows.Count.ToString(), Languages.Languages.shs_transform_outputrows + dtReworkedData.Rows.Count.ToString()), SQLTools_Enums.LOG_TYPEINFO.INF);

                    //si il y a eu transformation, réécriture de la requête d'origine, désormais non compatible
                    if (dtData.Rows.Count != dtReworkedData.Rows.Count)
                    {
                        Query sNewQuery = Toolbox.CreateQueryFromDataTable(dtReworkedData, JobParameters, sQ);
                        sQ = sNewQuery;
                    }
                    dtReworkedData.CaseSensitive = false;
                    return dtReworkedData;
                }
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_transform_nodatanopivot, SQLTools_Enums.LOG_TYPEINFO.WNG);
                return null;
            }
        }

        public DataTable PivotDataByArrayFields(DataTable dtDataToRework)
        {
            //Cette fonctionnalité avancée permet de renverser des données : 
            //exemple : si vous avez les colonnes A | B | C | D_1 | D_2 | D_3 | E...
            //le programme est capable de détecter les colonnes D comme une sorte d'array de colonnes et en fait des lignes
            //celà donnera dans le fichier final : A | B | C | D | E...
            //... avec autant de lignes qu'il y a de valeurs sur D (dans notre exemple, 3)
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, dtDataToRework.TableName + Languages.Languages.shs_transform_pivoting, SQLTools_Enums.LOG_TYPEINFO.INF);

            bool bAbnormalHeader = false; // identifie les fichiers anormaux (des colonnes tableaux avec des tailles variables ! (ex : COL1_01, COL1_02, COL2_01, COL2_02, COL2_03...)

            DataTable dtFinalData = new(dtDataToRework.TableName)
            {
                Namespace = dtDataToRework.Namespace
            };

            string sSeparator = JobParameters.DataTransformSeparatorOrLabel;

            List<string> sListeColonnes = new();
            List<int> iNumColonnesToKeep = new();
            List<string> sNomsColonnesToKeep = new();
            List<string> sNomsColonnesArray = new();
            string sColAnalyzeFIN = "";
            string sColAnalyzeDEB = "";
            string sColAnalyzeFULL = "";
            int iArrayMax = 0;

            for (int cpt = 0; cpt <= dtDataToRework.Columns.Count - 1; cpt += 1)
            {
                sListeColonnes.Add(dtDataToRework.Columns[cpt].ColumnName);
            }

            //analyse des colonnes pour réaliser le découpage

            for (int cpt = 0; cpt < sListeColonnes.Count; cpt += 1)
            {
                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                sColAnalyzeDEB = sListeColonnes[cpt];
                sColAnalyzeFIN = sListeColonnes[cpt];

                //on recherche les colonnes de type PER_xx
                if (sListeColonnes[cpt].IndexOf(sSeparator) > -1)
                {
                    sColAnalyzeFULL = sListeColonnes[cpt];
                    sColAnalyzeDEB = sListeColonnes[cpt][..sListeColonnes[cpt].LastIndexOf(sSeparator)];
                    //début de la chaine du nom de colonne (sur PER_01, on prend PER)
                    sColAnalyzeFIN = sListeColonnes[cpt][(sSeparator.Length + sListeColonnes[cpt].LastIndexOf(sSeparator))..];
                    //fin de la chaine du nom de colonne (sur PER_01, on prend 01)

                    //on recherche plue précisément les colonnes du type PER_1, PER_2...etc..
                    if (Toolbox.IsInteger(sColAnalyzeFIN, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0))
                    {
                        int iValArray = Convert.ToInt32(sColAnalyzeFIN);
                        if (iValArray > iArrayMax) { iArrayMax = iValArray; }

                        if (!sNomsColonnesArray.Contains(sColAnalyzeDEB))
                        {
                            sNomsColonnesArray.Add(sColAnalyzeDEB);
                            //on fait une liste avec uniquement les colonnes de type PER_xx (utile plus tard)
                            if (!sNomsColonnesToKeep.Contains(sColAnalyzeDEB))
                            {
                                //iNumColonnesToKeep.Add(cpt);
                                //son index
                                //sNomsColonnesToKeep.Add(sColAnalyzeDEB);
                                //son nom (ex : PER)
                            }
                        }

                        //si la colonne n'est pas du genre xx_10 mais plutôt xx_abc
                    }
                    else
                    {
                        if (!sNomsColonnesToKeep.Contains(sColAnalyzeFULL))
                        {
                            iNumColonnesToKeep.Add(cpt);
                            //son index
                            sNomsColonnesToKeep.Add(sListeColonnes[cpt]);
                            //son nom (ex : PER)
                        }
                    }

                    //si la colonne n'est pas du genre xx_xx
                }
                else
                {
                    if (!sNomsColonnesToKeep.Contains(sColAnalyzeDEB))
                    {
                        iNumColonnesToKeep.Add(cpt);
                        //si c'est une colonne sans "_xx", on l'ajoute comme une colonne normale
                        sNomsColonnesToKeep.Add(sListeColonnes[cpt]);
                    }
                }

            }
            //ajout d'une colonne d'identification correspondant au numéro de la colonne transformée : on remplace le dernier champ qui sera toujours vide (le fichier a toujours un ; final qui compte comme un champ)
            sNomsColonnesToKeep.Add("IDX_COL");
            //sNomsColonnesToKeep[sNomsColonnesToKeep.Count - 1] = "IDX_COL";
            iNumColonnesToKeep.Add(sListeColonnes.Count);
            //sNomsColonnesToKeep.Add(""); //ajout du champ vide final (un CSV cont chaque ligne se finit par ";" crée dans mon spli un champ à vide
            string sFieldsToPivot = string.Join(",", sNomsColonnesArray.ToArray());

            //comptage de l'abnormal
            List<int> iArrayCount = new();
            foreach (string sCol in sNomsColonnesArray)
            {
                int iC = 0;
                foreach (DataColumn dc in dtDataToRework.Columns)
                {
                    if (Regex.IsMatch(dc.ColumnName, string.Concat(sCol, Toolbox.RemoveRegexFromString(sSeparator), "\\d*"))) { iC++; }
                }
                iArrayCount.Add(iC);
            }
            iArrayCount = iArrayCount.Distinct().ToList();

            if (iArrayCount.Count > 1) { bAbnormalHeader = true; }

            if (sFieldsToPivot.Length == 0) { sFieldsToPivot = "(Nothing)"; }
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_transform_pivotingarray, sFieldsToPivot), SQLTools_Enums.LOG_TYPEINFO.INF);

            if (bAbnormalHeader && JobParameters.DataTransformDontTransformIfVariableArraySizes)
            {
                dtDataToRework.Namespace = "ABNORMAL";
                return dtDataToRework;
            }
            else
            {
                //et de la liste finale de colonnes
                for (int cpt = 0; cpt < iNumColonnesToKeep.Count; cpt += 1) //attention je ne mets pas le -1 à cause de la colonne INDEX ajoutée juste avant
                {
                    dtFinalData.Columns.Add(sNomsColonnesToKeep[cpt]);
                }

                foreach (string sC in sNomsColonnesArray) //on ajoute les colonnes "tableau"
                {
                    dtFinalData.Columns.Add(sC);
                }

                //création des lignes
                string[] sLigneSplit = null;
                List<string> sNewLine = new();
                List<string[]> sValuesToStoreInEachLine = new();
                //une liste pour chaque colonne découpée, contenant chacune la liste des valeurs

                //For cptLigne = 1 To _sFileInRam.Count - 1 Step 1 'ne pas mettre 0 car la ligne 0 = entête

                for (int cptRow = 0; cptRow < dtDataToRework.Rows.Count; cptRow += 1)
                {
                    try
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                        sLigneSplit = dtDataToRework.Rows[cptRow].ItemArray.Select(i => i == null ? string.Empty : i.ToString()).ToArray();

                        for (int cptColA = 0; cptColA <= sLigneSplit.Length - 1; cptColA += 1)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                            //je regarde si le numéro du compteur correspond à un numéro de colonne de type "array"
                            if (iNumColonnesToKeep.IndexOf(cptColA) != -1)
                            {
                                sNewLine.Add(sLigneSplit[cptColA]);
                            }
                            else
                            {
                                //sNewLine.Add("");
                                sValuesToStoreInEachLine.Add(new string[3] { sLigneSplit[cptColA], dtDataToRework.Columns[cptColA].ColumnName, sNomsColonnesArray.First(c => dtDataToRework.Columns[cptColA].ColumnName.StartsWith(c)) });
                            }
                        }

                        //sNewLine[sNewLine.Count - 1] = "1"; //ajout de la colonne INDEX - comme c'est la ligne MASTER, elle aura toujours l'index 1
                        sNewLine.Add("0"); //colonne INDEX

                        //écriture de la ligne "MASTER"

                        for (int i = 0; i < iArrayMax; i++)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            sNewLine[^1] = (i + 1).ToString();
                            dtFinalData.Rows.Add(sNewLine.ToArray());
                        }

                        //array : 0 = valeur, 1 = nom colonne original, 2 = nom colonne aggrégée
                        foreach (string[] sVal in sValuesToStoreInEachLine)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            string sRowToWrite = sVal[1][(sVal[1].LastIndexOf(sSeparator) + 1)..];
                            int iRowToWrite = Convert.ToInt32(sRowToWrite);
                            dtFinalData.Rows[dtFinalData.Rows.Count - iArrayMax - 1 + iRowToWrite][sVal[2]] = sVal[0];
                        }

                        sNewLine.Clear();
                        sValuesToStoreInEachLine.Clear();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
                if (bAbnormalHeader) { dtFinalData.Namespace = "ABNORMAL"; }
                return dtFinalData;
            }
        }

        public DataTable PivotDataByCommonRoot(DataTable dtDataToRework)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, dtDataToRework.TableName + Languages.Languages.shs_transform_pivotingcr, SQLTools_Enums.LOG_TYPEINFO.INF);

            DataTable dtPivot = new(dtDataToRework.TableName)
            {
                Namespace = dtDataToRework.Namespace
            };

            string[] sRoots = JobParameters.DataTransformSeparatorOrLabel.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            if (sRoots.Length > 0)
            {
                List<int> iRootedFields = new();
                List<int> iNotRootedFields = new();
                List<List<int>> iMultiListRootFields = new(); //sauvegarde des colonnes root
                List<List<Type>> typeMultiListRootFields = new(); //sauvegarde des types
                for (int i = 0; i < sRoots.Length; i++) { iMultiListRootFields.Add(new List<int>()); typeMultiListRootFields.Add(new List<Type>()); }
                int iCountFields;
                int iCountInsertedV;

                //recherche des colonnes à transformer
                for (int iR = 0; iR < sRoots.Length; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    for (int iC = 0; iC < dtDataToRework.Columns.Count; iC++)
                    {
                        if (dtDataToRework.Columns[iC].ColumnName.StartsWith(sRoots[iR], StringComparison.InvariantCultureIgnoreCase))
                        {
                            iMultiListRootFields[iR].Add(dtDataToRework.Columns[iC].Ordinal);
                            typeMultiListRootFields[iR].Add(dtDataToRework.Columns[iC].DataType);
                            iRootedFields.Add(dtDataToRework.Columns[iC].Ordinal);
                        }
                    }
                }

                //ajout des colonnes non transformées
                foreach (DataColumn dTc in dtDataToRework.Columns)
                {
                    if (!iRootedFields.Contains(dTc.Ordinal))
                    {
                        dtPivot.Columns.Add(dTc.ColumnName, dTc.DataType);
                        iNotRootedFields.Add(dTc.Ordinal);
                    }
                }

                //ajout des colonnes racines/pivot
                for (int iR = 0; iR < sRoots.Length; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    Type tFinal = null;
                    foreach (Type t in typeMultiListRootFields[iR])
                    {
                        if (tFinal == null) { tFinal = t; }
                        if (tFinal != t)
                        {
                            tFinal = Type.GetType("System.String"); break;
                        }
                    }
                    if (tFinal == null) { tFinal = Type.GetType("System.String"); }

                    dtPivot.Columns.Add(sRoots[iR], tFinal); //colonne valeur
                    dtPivot.Columns.Add(string.Concat(sRoots[iR], "_lbl"), Type.GetType("System.String")); //colonne libellé après racine
                    dtPivot.Columns.Add(string.Concat(sRoots[iR], "_idx"), Type.GetType("System.Int32")); //colonne index
                }

                iCountFields = dtPivot.Columns.Count;

                if (iRootedFields.Count > 0)
                {
                    //bloc d'identification des racines variables (on peut avoir demandé une découpe de 2 racines différentes qui n'ont pas le même nombre de champs
                    int iT = iMultiListRootFields[0].Count;
                    bool bOK = true;
                    foreach (List<int> iListR in iMultiListRootFields)
                    {
                        if (iListR.Count != iT) { bOK = false; }
                        iT = iListR.Count;
                    }

                    if (!bOK && JobParameters.DataTransformDontTransformIfVariableArraySizes)
                    {
                        dtDataToRework.Namespace = "ABNORMAL";
                        return dtDataToRework;
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_transform_pivotingcrinfo, iRootedFields.Count.ToString(), "/", dtDataToRework.Columns.Count.ToString(), " Fields - ", "Common Fields : ", iNotRootedFields.Count.ToString()), SQLTools_Enums.LOG_TYPEINFO.INF);

                        int iIntRow;
                        string sLabelRoot;

                        //construction des nouvelles lignes
                        for (int iRow = 0; iRow < dtDataToRework.Rows.Count; iRow++)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            iIntRow = 0;
                            iCountInsertedV = 0;
                            DataRow dNR = dtPivot.NewRow();

                            //on traite les colonnes root
                            for (int iC = 0; iC < iRootedFields.Count / sRoots.Length; iC++) //bouclage sur la liste des colonnes à transformer
                            {
                                iIntRow++;
                                for (int iR = 0; iR < sRoots.Length; iR++) //parcours des différentes racines retenues
                                {
                                    int iIdx01 = iMultiListRootFields[iR][iC];
                                    DataColumn dc = dtDataToRework.Rows[iRow].Table.Columns[iIdx01];
                                    sLabelRoot = dc.ColumnName;
                                    int iLen = sRoots[iR].Length;
                                    sLabelRoot = sLabelRoot[iLen..];

                                    dNR[sRoots[iR]] = dtDataToRework.Rows[iRow][iIdx01]; //la valeur qui était dans cette colonne
                                    dNR[string.Concat(sRoots[iR], "_lbl")] = sLabelRoot; //le nom de la colonne
                                    dNR[string.Concat(sRoots[iR], "_idx")] = iIntRow.ToString(); //le numéro de ligne
                                    iCountInsertedV += 3;
                                }

                                if (iCountInsertedV == (iCountFields - iNotRootedFields.Count)) //on a rempli le nombre de colonnes de la nouvelle ligne
                                {
                                    //on traite les colonnes non root
                                    foreach (DataColumn dCOR in dtDataToRework.Rows[iRow].Table.Columns)
                                    {
                                        if (dNR.Table.Columns.Contains(dCOR.ColumnName)) //identification des colonnes non transformées et ajout de la donnée
                                        {
                                            dNR[dCOR.ColumnName] = dtDataToRework.Rows[iRow][dCOR.ColumnName];
                                        }
                                    }

                                    dtPivot.Rows.Add(dNR);
                                    dNR = dtPivot.NewRow();
                                    iCountInsertedV = 0;
                                }
                            }
                        }
                        return dtPivot;
                    }
                }
                else
                {
                    dtDataToRework.Namespace = "UNMODIFIED";
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_transform_pivotingcrnoroot, SQLTools_Enums.LOG_TYPEINFO.WNG);
                    return dtDataToRework;
                }
            }
            else
            {
                dtDataToRework.Namespace = "UNMODIFIED";
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_transform_pivotingcrnothing, SQLTools_Enums.LOG_TYPEINFO.WNG);
                return dtDataToRework;
            }

        }

        public DataTable PivotDataFromRowsToColumns(DataTable dtDataToRework)
        {
            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, dtDataToRework.TableName + Languages.Languages.shs_transform_pivotingrc, SQLTools_Enums.LOG_TYPEINFO.INF);

            DataTable dtPivot = new(dtDataToRework.TableName)
            {
                Namespace = dtDataToRework.Namespace
            };

            int iColumns = dtDataToRework.Rows.Count;
            int iRows = dtDataToRework.Columns.Count;

            if (iColumns <= 999)
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_transform_pivotingrcinfo, iColumns.ToString(), Languages.Languages.shs_transform_pivotingrcrows, iRows.ToString(), Languages.Languages.shs_transform_pivotingrccols), SQLTools_Enums.LOG_TYPEINFO.INF);

                if (JobParameters.DataTransformRowsToColumnsAddLabelToValues) //ajout d'une colonne de propriété (prendra en libellé les anciens noms de colonnes)
                {
                    dtPivot.Columns.Add("PROPERTIES");
                }

                string sCol;
                for (int iR = 0; iR < dtDataToRework.Rows.Count; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    if (dtDataToRework.Columns.Contains(JobParameters.DataTransformSeparatorOrLabel))
                    {
                        sCol = Toolbox.RemoveSpecialCharacters(dtDataToRework.Rows[iR][JobParameters.DataTransformSeparatorOrLabel].ToString(), "_", false);
                        if (dtPivot.Columns.Contains(sCol)) { sCol = string.Concat(sCol, "_", dtPivot.Columns.Count.ToString()); } // 2 colonnes avec le même nom c'est ballot...
                        dtPivot.Columns.Add(sCol);
                    }
                    else
                    {
                        dtPivot.Columns.Add(string.Concat("R_", (iR + 1).ToString("000")));
                    }
                }

                DataRow dR;

                for (int iR = 0; iR < iRows; iR++)
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    dR = dtPivot.NewRow();

                    //ajout de la colonne propriété si souhaité
                    if (JobParameters.DataTransformRowsToColumnsAddLabelToValues) { dR[0] = dtDataToRework.Columns[iR].ColumnName; }

                    if (JobParameters.DataTransformRowsToColumnsAddLabelToValues)
                    {
                        for (int iC = 1; iC <= iColumns; iC++)
                        {
                            dR[iC] = dtDataToRework.Rows[iC - 1][iR].ToString();
                        }
                    }
                    else
                    {
                        for (int iC = 0; iC < iColumns; iC++)
                        {
                            dR[iC] = dtDataToRework.Rows[iC][iR].ToString();
                        }
                    }

                    dtPivot.Rows.Add(dR);
                }

                return dtPivot;
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_transform_pivotingrctoomuchrows, SQLTools_Enums.LOG_TYPEINFO.WNG);

                dtDataToRework.Namespace = "ABNORMAL";
                return dtDataToRework;
            }

        }

        public List<SQLColumn> GetListFieldsTypesFromDataset(DataTable dataTable, int iField, Query sQ, SQLTools_Enums.CLASS_PURPOSE ClassPurpose)
        {
            //iField == -1 : toutes les colonnes

            MThread mtClass = new(JobParameters, ClassPurpose, MyLog, iField == -1 ? dataTable.Columns.Count : 1, JobParameters.GlobalParameters.MULTITHREADING_CORES);
            //exécution des analyses en multithread
            for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
            {
                mtClass.INIT_AnalyzeDataFromDs(dataTable.Rows);
                //Thread th = new Thread(mtClass.CheckFieldsFromDataSet);
                //Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod().Name);
                //th.Start(numThread);
                // Recuperation du n° de thread pour le conserver lors de l'execution de la Task
                int numeroThread = numThread;

                Task th = new(() => mtClass.AnalyzeDataFromDs(numeroThread, iField, ClassPurpose == SQLTools_Enums.CLASS_PURPOSE.SRC ? sQ.ConnectionSrc.SqlCharTypeCompatibility : sQ.ConnectionTrg.SqlCharTypeCompatibility,
                                                                                 ClassPurpose == SQLTools_Enums.CLASS_PURPOSE.SRC ? sQ.ConnectionSrc.SqlDateTypeCompatibility : sQ.ConnectionTrg.SqlDateTypeCompatibility, sQ));
                Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                th.Start();

                // using var process = Process.GetCurrentProcess();
                // process.ProcessorAffinity = (IntPtr)new IntPtr(numThread + 1);

            }

            while (!mtClass.AreAllThreadsFinished)
            {
                Thread.Sleep(100);
                //on ne fait rien tant que les threads ne sont pas terminés !
            }
            if (mtClass.HasOperationBeenCancelled)
            { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }
            //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();


            List<SQLColumn> ListOfColumns = mtClass.GetFieldsFromDataset_OrFile_Result;

            for (int iC = 0; iC < ListOfColumns.Count; iC++) //plus facile de trouver les types après
            {
                if (dataTable.Columns.Contains(ListOfColumns[iC].ColumnName))
                {
                    dataTable.Columns[ListOfColumns[iC].ColumnName].Prefix = ListOfColumns[iC].ColumnType.ToString();

                    if (!sQ.ConnectionSrc.SConnDriverSuffix.Equals("DB")) //en général les driver SQL ramènent des types de colonnes qu'on ne va pas chercher à convertir (possiblé différence entre SHS et le driver SQL en revanche)
                    {
                        if (FuzibleQuery.QueryAnalyzer != null && (FuzibleQuery.QueryAnalyzer.GroupBy.Count > 0 ||
                                                                   FuzibleQuery.QueryAnalyzer.Where.Count > 0 ||
                                                                   FuzibleQuery.QueryAnalyzer.OrderBy.Count > 0))
                        {
                            Toolbox.ConvertColumnType(dataTable, dataTable.Columns[ListOfColumns[iC].ColumnName], ListOfColumns[iC].ColumnLinqType);
                        }
                    }
                    break;
                }
            }

            for (int iC = 0; iC < ListOfColumns.Count; iC++) //remplacer les noms des champs par les captions
            {
                if (dataTable.Columns.Contains(ListOfColumns[iC].ColumnName))
                {
                    DataColumn dc = dataTable.Columns[ListOfColumns[iC].ColumnName];
                    if (!dc.ColumnName.Equals(dc.Caption))
                    {
                        ListOfColumns[iC].ColumnName = dc.Caption;
                        //break;
                    }
                }
            }

            return ListOfColumns;
        }

        public DataTable GetCrossJoinDatatable(Query sQ, SQLTools_Enums.CROSSJOIN_TYPES ctTypeCrossJoin, DataTable dtPrimaryTable, DataTable dtSecondaryTable, List<string[]> sLinkFields, int iFT1, int iFT2)
        {
            //contrôle existence colonne
            StringBuilder sbProblem = new();

            foreach (string[] sL in sLinkFields)
            {
                if (ctTypeCrossJoin == SQLTools_Enums.CROSSJOIN_TYPES.RIGHT) //secondary table -> ift1, sinon, primary table -> ift1
                {
                    if (!dtPrimaryTable.Columns.Contains(sL[iFT2])) { sbProblem.Append(Languages.Languages.shs_crossjoin_misslinkprimary + sL[iFT2]); }
                    if (!dtSecondaryTable.Columns.Contains(sL[iFT1])) { sbProblem.Append(Languages.Languages.shs_crossjoin_misslinksecondary + sL[iFT1]); }
                }
                else
                {
                    if (!dtPrimaryTable.Columns.Contains(sL[iFT1])) { sbProblem.Append(Languages.Languages.shs_crossjoin_misslinkprimary + sL[iFT1]); }
                    if (!dtSecondaryTable.Columns.Contains(sL[iFT2])) { sbProblem.Append(Languages.Languages.shs_crossjoin_misslinksecondary + sL[iFT2]); }
                }
            }

            if (sbProblem.Length == 0)
            {
                string sKey = "";
                foreach (string[] sK in sLinkFields)
                {
                    sKey = string.Concat(sKey, sK[iFT1], "->", sK[iFT2], ",");
                }
                sKey = sKey[0..^1];

                sQ.QueryAnalyzer.AddQueryProperty(dtPrimaryTable.TableName, SQLTools_Enums.QUERY_PROPERTIES.CROSSJOIN_LINK, sKey, true);

                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_crossjoin_keys, sKey), SQLTools_Enums.LOG_TYPEINFO.INF);

                int iRowsCount = 0;

                switch (ctTypeCrossJoin)
                {
                    case SQLTools_Enums.CROSSJOIN_TYPES.RIGHT:
                        iRowsCount = dtSecondaryTable.Rows.Count;
                        break;
                    case SQLTools_Enums.CROSSJOIN_TYPES.LEFT:
                        iRowsCount = dtPrimaryTable.Rows.Count;
                        break;
                    case SQLTools_Enums.CROSSJOIN_TYPES.INNER:
                        iRowsCount = dtPrimaryTable.Rows.Count;
                        break;
                    case SQLTools_Enums.CROSSJOIN_TYPES.OUTER:
                        iRowsCount = dtPrimaryTable.Rows.Count; //le OUTER commence par un LEFT JOIN puis un RIGHT JOIN
                        break;
                }

                int iQteCPU = JobParameters.GlobalParameters.MULTITHREADING_CORES;

                MThread mtClass = new(JobParameters, SQLTools_Enums.CLASS_PURPOSE.SRC, MyLog, iRowsCount, iQteCPU);
                int iQteThreads = mtClass.QuantityOfThreadsToCompute;
                //exécution des analyses en multithread

                if (iQteThreads > 0)
                {
                    for (int numThread = 0; numThread <= iQteThreads - 1; numThread += 1)
                    {
                        mtClass.INIT_CrossJoinDataTables(iQteThreads, dtPrimaryTable, dtSecondaryTable);
                        int numeroThread = numThread;
                        Task th = new(() => mtClass.CrossJoinDataTables(numeroThread, iQteCPU, ctTypeCrossJoin, dtPrimaryTable, dtSecondaryTable, sLinkFields, iFT1, iFT2));
                        Monitoring.AddThread(th, System.Reflection.MethodBase.GetCurrentMethod());
                        th.Start();
                    }

                    while (!mtClass.AreAllThreadsFinished)
                    {
                        //Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(100);
                    }
                    if (mtClass.HasOperationBeenCancelled)
                    { throw new OperationCanceledException(Languages.Languages.shs_operation_cancelled + " (" + System.Reflection.MethodBase.GetCurrentMethod() + ")"); }

                    dtPrimaryTable.Clear();
                    dtSecondaryTable.Clear();
                    dtPrimaryTable = null;
                    dtSecondaryTable = null;

                    return mtClass.GetCrossJoinDatatable;
                }
                else
                {
                    DataTable dtNewTable = new(dtPrimaryTable.TableName + "_" + dtSecondaryTable.TableName);
                    foreach (DataColumn col in dtPrimaryTable.Columns) //copie des colonnes de la table 1 sur la nouvelle
                    {
                        if (dtSecondaryTable.Columns[col.ColumnName] == null)
                        {
                            dtSecondaryTable.Columns.Add(col.ColumnName, col.DataType);
                            dtSecondaryTable.Columns[col.ColumnName].Namespace = col.Namespace;
                        }
                        if (dtNewTable.Columns[col.ColumnName] == null)
                        {
                            dtNewTable.Columns.Add(col.ColumnName, col.DataType);
                            dtNewTable.Columns[col.ColumnName].Namespace = col.Namespace;
                        }
                    }

                    foreach (DataColumn col in dtSecondaryTable.Columns) //copie des colonnes de la table 2 sur la nouvelle
                    {
                        if (dtPrimaryTable.Columns[col.ColumnName] == null)
                        {
                            dtPrimaryTable.Columns.Add(col.ColumnName, col.DataType);
                            dtPrimaryTable.Columns[col.ColumnName].Namespace = col.Namespace;
                        }
                        if (dtNewTable.Columns[col.ColumnName] == null)
                        {
                            dtNewTable.Columns.Add(col.ColumnName, col.DataType);
                            dtNewTable.Columns[col.ColumnName].Namespace = col.Namespace;
                        }
                    }
                    if (dtPrimaryTable != null) { dtPrimaryTable.CaseSensitive = false; }
                    if (dtSecondaryTable != null) { dtSecondaryTable.CaseSensitive = false; }

                    return ctTypeCrossJoin switch
                    {
                        SQLTools_Enums.CROSSJOIN_TYPES.RIGHT => dtSecondaryTable,
                        SQLTools_Enums.CROSSJOIN_TYPES.LEFT => dtPrimaryTable,
                        SQLTools_Enums.CROSSJOIN_TYPES.INNER => dtNewTable,
                        SQLTools_Enums.CROSSJOIN_TYPES.OUTER => dtPrimaryTable.Rows.Count == 0 ? dtSecondaryTable : dtPrimaryTable,
                        SQLTools_Enums.CROSSJOIN_TYPES.UNKNOWN => dtNewTable,
                        _ => dtNewTable,
                    };
                }
            }
            else
            {
                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat(Languages.Languages.shs_crossjoin_ko, sbProblem.ToString()), sQ.RetryErrorOrWarning);
                sQ.QueryErrors += 1;
                return ctTypeCrossJoin == SQLTools_Enums.CROSSJOIN_TYPES.RIGHT ? dtSecondaryTable : dtPrimaryTable;
            }

        }

        public DataTable JoinDatatablesFromPseudoQuery(Query sQ, DataTable dtTableA, DataTable dtTableB, int iTable)
        {
            int iFieldTA;
            int iFieldTB;
            //la première table est la précédente, la deuxième est la table jointe
            if (iTable > 0)
            {
                dtTableA.TableName = FuzibleQuery.QueryAnalyzer.Tables[iTable - 1].Alias;
                dtTableB.TableName = FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias;
            }

            DataTable dtFiltered;
            DataTable dtJoinedTables = null;
            List<string[]> sListJoinFields = FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkFields;
            List<string[]> sListJoinToRemove = new();

            foreach (string[] sJoin in sListJoinFields)
            {
                if (sJoin[3].Length == 0 && sJoin[4].Length == 0)
                {
                    string sFilter = string.Concat(sJoin[0], " ", sJoin[2], " ", sJoin[1]);
                    try
                    {
                        dtFiltered = dtTableA.Select(sFilter).CopyToDataTable();
                        dtFiltered.Namespace = dtTableA.Namespace;
                        dtFiltered.TableName = dtTableA.TableName;
                        dtTableA.Clear();
                        dtTableA = dtFiltered.Copy();
                        dtTableA.Namespace = dtFiltered.Namespace;
                        dtTableA.TableName = dtFiltered.TableName;
                        dtFiltered.Clear();
                        dtFiltered = null;
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_joindt_cantfilter + sFilter, sQ.RetryErrorOrWarning);
                        sQ.QueryErrors += 1;
                    }

                    sListJoinToRemove.Add(sJoin);
                }
                else if (sJoin[3].Length == 0 || sJoin[4].Length == 0)
                {
                    //l'une des tables est filtrée mais sans lien avec une autre : genre items.monchamp <> 'coucou'
                    if (dtTableA.TableName.Equals(sJoin[3]) || dtTableA.TableName.Equals(sJoin[4]))
                    {
                        int iFilter = dtTableA.TableName.Equals(sJoin[3]) ? 1 : 0;
                        int iT = iFilter == 5 ? 1 : 0;
                        string sFilter = string.Concat(sJoin[iT], " ", sJoin[2], " ", sJoin[iFilter]);
                        try
                        {
                            dtFiltered = dtTableA.Select(sFilter).CopyToDataTable();
                            dtFiltered.Namespace = dtTableA.Namespace;
                            dtFiltered.TableName = dtTableA.TableName;
                            dtTableA.Clear();
                            dtTableA = dtFiltered.Copy();
                            dtTableA.Namespace = dtFiltered.Namespace;
                            dtTableA.TableName = dtFiltered.TableName;
                            dtFiltered.Clear();
                            dtFiltered = null;
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_joindt_cantfilter + sFilter, sQ.RetryErrorOrWarning);
                            sQ.QueryErrors += 1;
                        }

                        sListJoinToRemove.Add(sJoin);
                    }
                    if (dtTableB.TableName.Equals(sJoin[3]) || dtTableB.TableName.Equals(sJoin[4]))
                    {
                        int iFilter = dtTableB.TableName.Equals(sJoin[3]) ? 1 : 0;
                        int iT = iFilter == 5 ? 1 : 0;
                        string sFilter = string.Concat(sJoin[iT], " ", sJoin[2], " ", sJoin[iFilter]);
                        try
                        {
                            dtFiltered = dtTableB.Select(sFilter).CopyToDataTable();
                            dtFiltered.Namespace = dtTableB.Namespace;
                            dtFiltered.TableName = dtTableB.TableName;
                            dtTableB.Clear();
                            dtTableB = dtFiltered.Copy();
                            dtTableB.Namespace = dtFiltered.Namespace;
                            dtTableB.TableName = dtFiltered.TableName;
                            dtFiltered.Clear();
                            dtFiltered = null;
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_joindt_cantfilter + sFilter, sQ.RetryErrorOrWarning);
                            sQ.QueryErrors += 1;
                        }

                        sListJoinToRemove.Add(sJoin);
                    }
                }
            }

            foreach (string[] sJoin in sListJoinToRemove)
            {
                sListJoinFields.Remove(sJoin);
            }

            if (dtTableA.TableName.Equals(sListJoinFields[0][3], StringComparison.InvariantCultureIgnoreCase))
            {
                iFieldTA = 5;
                iFieldTB = 6;
            }
            else
            {
                iFieldTA = 6;
                iFieldTB = 5;
            }

            //mise du namespace correct pour chaque colonne (namespace = nom de table)
            if (iTable == 1)
            {
                string sTableA = FuzibleQuery.QueryAnalyzer.Tables[0].Alias;
                foreach (DataColumn dC in dtTableA.Columns)
                {
                    dC.ColumnName = string.Concat(sTableA, ".", dC.ColumnName);
                    dC.Namespace = sTableA;
                }
            }
            string sTableB = FuzibleQuery.QueryAnalyzer.Tables[iTable].Alias;
            foreach (DataColumn dC in dtTableB.Columns)
            {
                dC.ColumnName = string.Concat(sTableB, ".", dC.ColumnName);
                dC.Namespace = sTableB;
            }

            //récupération des champs de jointure : NOTE : le "on a.id = b.id" doit être dans le même ordre que les tables (from table1 as t1 left join table2 as t2 on t1.id = t2.id ET NON PAS from table1 as t1 left join table2 as t2 on t2.id = t1.id ) 
            //TODO : ne supporte qu'un seul champ de jointure ! (int ou string)

            try
            {
                if (sListJoinFields.Count > 0)
                {
                    SQLTools_Enums.CROSSJOIN_TYPES ctType = SQLTools_Enums.CROSSJOIN_TYPES.INNER;
                    if (FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("left join", StringComparison.InvariantCultureIgnoreCase) > -1
                        || FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("left outer join", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        ctType = SQLTools_Enums.CROSSJOIN_TYPES.LEFT;
                    }

                    else if (FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("right join", StringComparison.InvariantCultureIgnoreCase) > -1
                        || FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("right outer join", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        ctType = SQLTools_Enums.CROSSJOIN_TYPES.RIGHT;
                    }

                    else if (FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("inner join", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        ctType = SQLTools_Enums.CROSSJOIN_TYPES.INNER;
                    }

                    else if (FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("outer join", StringComparison.InvariantCultureIgnoreCase) > -1
                        || FuzibleQuery.QueryAnalyzer.Tables[iTable].LinkType.IndexOf("full outer join", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        ctType = SQLTools_Enums.CROSSJOIN_TYPES.OUTER;
                    }

                    dtJoinedTables = GetCrossJoinDatatable(sQ, ctType, dtTableA, dtTableB, sListJoinFields, iFieldTA, iFieldTB);
                }
                dtJoinedTables.CaseSensitive = false;
                return dtJoinedTables;
            }
            catch (OperationCanceledException)
            { throw; }
        }

        internal class RowAndIndex
        {
            private readonly string _sRow = "";
            private readonly int _iIndex = 0;

            public string SRow
            {
                get
                {
                    return _sRow;
                }
            }
            public int IRow
            {
                get
                {
                    return _iIndex;
                }
            }

            public RowAndIndex(string s, int i)
            {
                _sRow = s;
                _iIndex = i;
            }
        }

    }

    public static class DateTimeExtensions
    {
        public static DateTime AddWeeks(this DateTime dateTime, int numberOfWeeks)
        {
            return dateTime.AddDays(numberOfWeeks * 7);
        }
    }

    public static class Toolbox
    {
        public static class LevenshteinDistance
        {
            /// <summary>
            /// Compute the distance between two strings.
            /// </summary>
            public static int Compute(string s, string t)
            {
                int n = s.Length;
                int m = t.Length;
                int[,] d = new int[n + 1, m + 1];

                // Step 1
                if (n == 0)
                {
                    return m;
                }

                if (m == 0)
                {
                    return n;
                }

                // Step 2
                for (int i = 0; i <= n; d[i, 0] = i++)
                {
                }

                for (int j = 0; j <= m; d[0, j] = j++)
                {
                }

                // Step 3
                for (int i = 1; i <= n; i++)
                {
                    //Step 4
                    for (int j = 1; j <= m; j++)
                    {
                        // Step 5
                        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                        // Step 6
                        d[i, j] = Math.Min(
                            Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                            d[i - 1, j - 1] + cost);
                    }
                }
                // Step 7
                return d[n, m];
            }
        }

        public static class ScriptLanguage
        {
            static string GeneratePassword(string key, int length)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] seedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                    int seed = BitConverter.ToInt32(seedBytes, 0);
                    Random random = new Random(seed);

                    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
                    char[] password = new char[length];

                    for (int i = 0; i < length; i++)
                    {
                        password[i] = chars[random.Next(chars.Length)];
                    }

                    return new string(password);
                }
            }

            private static int GetIso8601WeekOfYear(DateTime time)
            {
                // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
                // be the same week# as whatever Thursday, Friday or Saturday are,
                // and we always get those right
                DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
                if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
                {
                    time = time.AddDays(3);
                }

                // Return the week of our adjusted day
                return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            }

            public static List<string> GetListScriptZonesFromString(string sStringToFindScriptIn)
            {
                string[] sMultiScriptZonesTemp;
                List<string> sMultiScriptZonesDef = new();

                if (sStringToFindScriptIn.IndexOf("{") > -1 && sStringToFindScriptIn.IndexOf("}") > 0)
                {
                    //attention : il peut y avoir plusieurs zones de script distinctes dans la chaîne, je dois donc les traiter séparément
                    sMultiScriptZonesTemp = sStringToFindScriptIn.Split(Convert.ToChar("{"));
                    foreach (string sSc in sMultiScriptZonesTemp) { if (sSc.IndexOf("}") > -1) { sMultiScriptZonesDef.Add("{" + sSc[..(sSc.IndexOf("}") + 1)]); } }

                }
                return sMultiScriptZonesDef;
            }

            public static string InterpretAndReplaceScriptLanguage(string sStringToFindScriptIn, List<string> sListSourceDynamicVariables, string sQueryTargetName = "")
            {
                string sFinalString = sStringToFindScriptIn;
                string sZoneScript;
                //script basique qui interprète %YYYY (annee), %%YY (annee), %MM (mois), %DD (jour), %HH (heure), %SS (secondes)
                //in interprète également les caractères _ et -
                // -%USER : nom de l'utilisateur connecté à l'appli
                // -%MM : numéro de mois sur 2 caractères
                // -%WW : numéro de semaine sur 2 caractères
                // - %YY : numéro d’année sur 2 caractères
                // - %YYYY : numéro d’année sur 4 caractères
                // - %DD : numéro de jour du mois sur 2 caractères
                // - %HH : heure du jour sur 2 caractères
                // - %mm : minute sur 2 caractères
                // - %SS : seconde sur 2 caractères
                // - %DTTS et %DTTSMILLI : date actuelle au format Unix Timestamp
                // - <XM : retire X mois à MM(si existant)
                // - <XW : retire X semaines à WW(si existant)
                // - <XY : retire X année à YY(ou YYYY)(si existant)
                // - <XD : retire X jours à DD(si existant)
                // - >XM : ajoute X mois à MM(si existant)
                // - >XW : ajoute X semaines à WW(si existant)
                // - >XY : ajoute X année à YY(ou YYYY)(si existant)
                // - >XD : ajoute X jours à DD(si existant)
                // - ?1, ?2, ?3 ... : remplace ces variables par les paramètres issus du champ de l'UI ou de l'argument (quand l'app est lancée en mode paramétré)
                // - %CS1, %CS2, %CS3... : quand saisi dans l'UI, sert pour dire que le paramètre prend la valeur de la requête exécutée en pré-commande dans la source
                // - %CT1, %CT2, %CT3... : quand saisi dans l'UI, sert pour dire que le paramètre prend la valeur de la requête exécutée en pré-commande dans le target
                // - CONNSTRING_T / CONNSTRING_S : chaînes de connexion Target / Source
                // --------------- EXEMPLES --------------------
                // SELECT * FROM MONFICHIER_{MMYYYY}.csv  --->  SELECT * FROM MONFICHIER_032018.csv
                // SELECT * FROM MONFICHIER{MM_DD_YYYY}.csv  --->  SELECT * FROM MONFICHIER03_20_2018.csv
                // SELECT * FROM MONFICHIER_{MMYYYY>3M}.csv  --->  SELECT * FROM MONFICHIER_062018.csv
                // SELECT * FROM MONFICHIER_{MMYYYY>3M>1Y}.csv  --->  SELECT * FROM MONFICHIER_062019.csv
                // SELECT * FROM MONFICHIER_{DD<10D}.csv  --->  SELECT * FROM MONFICHIER_10.csv
                // SELECT * FROM MATABLE WHERE ID_TEST={?1}  --->  SELECT * FROM MATABLE WHERE ID_TEST=’100’

                DateTime dtDate = DateTime.Now;
                DateTime dtDateB = dtDate;

                string sScript;
                string[] sMultiScriptZonesTemp;
                List<string> sMultiScriptZonesDef = new();
                if (sStringToFindScriptIn.IndexOf("{") > -1 && sStringToFindScriptIn.IndexOf("}") > 0)
                {
                    //attention : il peut y avoir plusieurs zones de script distinctes dans la chaîne, je dois donc les traiter séparément
                    sMultiScriptZonesTemp = sStringToFindScriptIn.Split(Convert.ToChar("{"));
                    foreach (string sSc in sMultiScriptZonesTemp) { if (sSc.IndexOf("}") > -1) { sMultiScriptZonesDef.Add("{" + sSc[..(sSc.IndexOf("}") + 1)]); } }

                    //analyse de chaque zone de script contenue dans la chaîne initiale
                    foreach (string sCleanScript in sMultiScriptZonesDef)
                    {
                        sScript = sCleanScript;
                        sZoneScript = sScript; // zone de script délimitée
                        sScript = sScript[1..^1];
                        string[] removeDT;
                        string[] addDT;
                        string[] varZones;
                        List<string> sListRemove = new();
                        List<string> sListAdd = new();
                        List<int> iListVarZones = new();
                        List<string[]> sListVarZonesWithValues = new();

                        //analyse des paramètres variables et remplacement des ?1, ?2... par leurs valeurs issues de L'INI. (attention : ne jamais dépasser 9 params !!)
                        //note amusante : si un paramètre dynamique a une valeur saisie de (exemple) MM, DD, YY ou autre code "réservé", celui-ci va être interprété ensuite comme une date, et remplacé
                        //exemple : {YYYY?1} avec ?1 = MM --> {YYYYMM} --> 201803
                        if (sScript.IndexOf("?") > -1)
                        {
                            varZones = sScript.Split(Convert.ToChar("?"));
                            for (int cptZ = 1; cptZ < varZones.Length; cptZ++)
                            {
                                if (IsNumeric(varZones[cptZ][..1], null)) { iListVarZones.Add(Convert.ToInt16(varZones[cptZ][..1])); }
                            }
                            //du coup j'ai dans iListVarZones la liste des paramètres sollicités par la zone script
                            //je crée ma liste de paramètres dynamiques en utilisant le numéro derrière le "?" pour chercher le bon paramètre
                            foreach (int iZ in iListVarZones)
                            {
                                if (sListSourceDynamicVariables.Count >= iZ)
                                {
                                    sListVarZonesWithValues.Add(new string[] { string.Concat("?" + iZ.ToString()), sListSourceDynamicVariables[iZ - 1] });
                                }
                                else { sListVarZonesWithValues.Add(new string[] { string.Concat("?" + iZ.ToString()), "" }); }
                                //throw new Exception("Queries Script Zones must match the number of configured Dynamic Parameters ! (Needed : " + iListVarZones.Count.ToString() + " - Found : " + sListSourceDynamicVariables.Count.ToString() + ")" );
                            }
                            foreach (string[] sParam in sListVarZonesWithValues)
                            {
                                sScript = sScript.Replace(sParam[0], sParam[1]);
                            }
                        }

                        if (sScript.IndexOf("%FD") > -1)
                        {
                            dtDate = new DateTime(dtDate.Year, dtDate.Month, 1);
                            dtDateB = dtDate;
                        }
                        if (sScript.IndexOf("%LD") > -1)
                        {
                            dtDate = new DateTime(dtDate.Year, dtDate.Month, 1).AddMonths(1).AddDays(-1);
                            dtDateB = dtDate;
                        }

                        int iDaysOffset = 0;

                        //analyses des transformations de dates
                        if (sScript.IndexOf("<") > -1)
                        {
                            removeDT = sScript[sScript.IndexOf("<")..].Split(Convert.ToChar("<"));
                            foreach (string s in removeDT)
                            {
                                if (s.IndexOf("M") > -1) { sListRemove.Add("<" + s[..(s.IndexOf("M") + 1)]); }
                                if (s.IndexOf("Y") > -1) { sListRemove.Add("<" + s[..(s.IndexOf("Y") + 1)]); }
                                if (s.IndexOf("D") > -1) { sListRemove.Add("<" + s[..(s.IndexOf("D") + 1)]); }
                                if (s.IndexOf("W") > -1) { sListRemove.Add("<" + s[..(s.IndexOf("W") + 1)]); }
                            }
                        }
                        if (sScript.IndexOf(">") > -1)
                        {
                            addDT = sScript[sScript.IndexOf(">")..].Split(Convert.ToChar(">"));
                            foreach (string s in addDT)
                            {
                                if (s.IndexOf("M") > -1) { sListAdd.Add(">" + s[..(s.IndexOf("M") + 1)]); }
                                if (s.IndexOf("Y") > -1) { sListAdd.Add(">" + s[..(s.IndexOf("Y") + 1)]); }
                                if (s.IndexOf("D") > -1) { sListAdd.Add(">" + s[..(s.IndexOf("D") + 1)]); }
                                if (s.IndexOf("W") > -1) { sListAdd.Add(">" + s[..(s.IndexOf("W") + 1)]); }
                            }
                        }
                        //retirer des unités de temps
                        foreach (string s in sListRemove)
                        {
                            try
                            {
                                if (s.IndexOf("M") > -1) { dtDateB = dtDate.AddMonths(-Convert.ToInt16(s[1..s.IndexOf("M")])); }
                                if (s.IndexOf("Y") > -1) { dtDateB = dtDate.AddYears(-Convert.ToInt16(s[1..s.IndexOf("Y")])); }
                                if (s.IndexOf("D") > -1) { iDaysOffset = -Convert.ToInt16(s[1..s.IndexOf("D")]); dtDateB = dtDate.AddDays(-Convert.ToInt16(s[1..s.IndexOf("D")])); }
                                if (s.IndexOf("W") > -1) { dtDateB = dtDate.AddWeeks(-Convert.ToInt16(s[1..s.IndexOf("W")])); }
                                sScript = sScript.Replace(s, "");  //nettoyage sScript des opérandes d'addition ou soustraction
                            }
                            catch (Exception) { sScript = sScript.Replace(s, ""); }
                        }
                        //ajouter des unités de temps
                        foreach (string s in sListAdd)
                        {
                            try
                            {
                                if (s.IndexOf("M") > -1) { dtDateB = dtDate.AddMonths(Convert.ToInt16(s[1..s.IndexOf("M")])); }
                                if (s.IndexOf("Y") > -1) { dtDateB = dtDate.AddYears(Convert.ToInt16(s[1..s.IndexOf("Y")])); }
                                if (s.IndexOf("D") > -1) { iDaysOffset = Convert.ToInt16(s[1..s.IndexOf("D")]); dtDateB = dtDate.AddDays(Convert.ToInt16(s[1..s.IndexOf("D")])); }
                                if (s.IndexOf("W") > -1) { dtDateB = dtDate.AddWeeks(Convert.ToInt16(s[1..s.IndexOf("W")])); }
                                sScript = sScript.Replace(s, "");  //nettoyage sScript des opérandes d'addition ou soustraction
                            }
                            catch (Exception) { sScript = sScript.Replace(s, ""); }
                        }

                        string sUserApp = "";
                        sUserApp = WindowsIdentity.GetCurrent().Name;
                        if (sUserApp.IndexOf("\\") > -1) { sUserApp = sUserApp.Split(Convert.ToChar("\\"))[1]; }

                        //Remplacer les "codes" datetime par les valeurs ainsi que la notion d'utilisateur
                        DateTime foo = new(dtDateB.Year, dtDateB.Month, dtDateB.Day);
                        long unixTime = ((DateTimeOffset)foo).ToUnixTimeSeconds();
                        DateTime fooB = new(dtDateB.Year, dtDateB.Month, dtDateB.Day, dtDateB.Hour, 0, 0);
                        long unixTimeMilli = ((DateTimeOffset)foo).ToUnixTimeMilliseconds();

                        DateTimeOffset dtDateBOffSet = dtDateB;

                        sScript = sScript.Replace("%DTTSMILLI", unixTimeMilli.ToString());
                        sScript = sScript.Replace("%DTTS", unixTime.ToString());

                        sScript = sScript.Replace("%YYYY", dtDateB.Year.ToString()); // YYYY en premier !!! (et non pas YY)
                        sScript = sScript.Replace("%YY", dtDateB.Year.ToString().Substring(2, 2));
                        sScript = sScript.Replace("%MM", dtDateB.Month.ToString("00"));
                        sScript = sScript.Replace("%DD", dtDateB.Day.ToString("00"));
                        sScript = sScript.Replace("%LD", (new DateTime(dtDateB.Year, dtDateB.Month, 1).AddMonths(1).AddDays(-1 + iDaysOffset).Day).ToString("00"));
                        sScript = sScript.Replace("%FD", (new DateTime(dtDateB.Year, dtDateB.Month, 1).AddDays(iDaysOffset).Day).ToString("00"));
                        sScript = sScript.Replace("%HH", dtDateB.Hour.ToString("00"));
                        sScript = sScript.Replace("%HUTC", (dtDateB.Hour - dtDateBOffSet.Offset.Hours).ToString("00"));
                        sScript = sScript.Replace("%mm", dtDateB.Minute.ToString("00"));
                        sScript = sScript.Replace("%SS", dtDateB.Second.ToString("00"));
                        sScript = sScript.Replace("%WW", GetIso8601WeekOfYear(dtDateB).ToString("00"));

                        sScript = sScript.Replace("%USER", sUserApp);


                        Match match = Regex.Match(sScript, "%RNDGEN(\\[\\d+\\])?");
                        if (match.Success)
                        {
                            int length = 12;

                            if (match.Groups.Count > 1 && match.Groups[1].Value.Length > 0)
                            {
                                int.TryParse(match.Groups[1].Value[1..^1], out length);
                            }

                            string sRandomGenerator = GeneratePassword(INIProgram.InstanceSeed.ToString(), length);

                            sScript = sScript.Replace(match.Value, sRandomGenerator);
                        }

                        sScript = sScript.Replace("%GUID", INIProgram.InstanceSeed.ToString().ToString());

                        sScript = sScript.Replace("%QUERYTARGETNAME", sQueryTargetName.Length == 0 ? "" : sQueryTargetName);

                        if (!sZoneScript.Equals(string.Concat("{", sScript, "}"))) //on vérifie que c'était une vraie zone de script (donc le contenu a été changé)
                        {
                            sFinalString = sFinalString.Replace(sZoneScript, sScript);
                        }
                    }
                }

                return sFinalString;

            }
        }

        internal static int GetAvailableTaskSlot(List<SQLStreamingData> tOperation, int iSlot)
        {
            for (int iT = 0; iT < tOperation.Count; iT++)
            {
                //le but consiste à toujours laisser la première itération toute seule (création de table SQL...etc...)
                if (iSlot == 1 && tOperation[0] != null && !tOperation[0].Operation.IsCompleted) { return -1; }
                else if (tOperation[iT] == null) { return iT; }
                else if (tOperation[iT].Operation.IsCompleted) { return iT; }
            }
            return -1;
        }

        #region "VARIABLES"

        //const string REGEX_ISNUMERIC = "^[-+.]?\\d+[,.]?\\d*$";
        //const string REGEX_0x = "^0\\d+$";
        private const string REGEX_1x = "^-?[.,]?\\d+([.,]?\\d+)?$"; //" ^ ([-\\.),|[\\.,]).*";

        private const string SQL_INSERT_ECHAP_VALUE = "'";

        #endregion

        #region "PUBLIC VOID"
        public static string ReplaceStringInString(string chaine, int index, int longueur, string nouvellePartie)
        {
            // Construction de la nouvelle chaîne en remplaçant la partie spécifiée
            string avant = chaine[..index];
            string apres = chaine[(index + longueur)..];
            string resultat = avant + nouvellePartie + apres;

            return resultat;
        }

        public static string ToUpperFirstLetter(this string source)
        {
            source = source.ToLower();
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }
            // convert to char array of the string
            char[] letters = source.ToCharArray();
            // upper case the first char
            letters[0] = char.ToUpper(letters[0]);
            // return the array made of the new char array
            return new string(letters);
        }

        public static string ReplaceString(this string str, string oldValue, string @newValue, StringComparison comparisonType)
        {

            // Check inputs.
            if (str == null)
            {
                // Same as original .NET C# string.Replace behavior.
                throw new ArgumentNullException(nameof(str));
            }
            if (str.Length == 0)
            {
                // Same as original .NET C# string.Replace behavior.
                return str;
            }
            if (oldValue == null)
            {
                // Same as original .NET C# string.Replace behavior.
                //throw new ArgumentNullException(nameof(oldValue));
            }
            if (oldValue.Length == 0)
            {
                // Same as original .NET C# string.Replace behavior.
                throw new ArgumentException(Languages.Languages.shs_tool_replacestringko);
                //resu
            }


            //if (oldValue.Equals(newValue, comparisonType))
            //{
            //This condition has no sense
            //It will prevent method from replacesing: "Example", "ExAmPlE", "EXAMPLE" to "example"
            //return str;
            //}



            // Prepare string builder for storing the processed string.
            // Note: StringBuilder has a better performance than String by 30-40%.
            StringBuilder resultStringBuilder = new(str.Length);



            // Analyze the replacement: replace or remove.
            bool isReplacementNullOrEmpty = string.IsNullOrEmpty(@newValue);



            // Replace all values.
            const int valueNotFound = -1;
            int foundAt;
            int startSearchFromIndex = 0;
            while ((foundAt = str.IndexOf(oldValue, startSearchFromIndex, comparisonType)) != valueNotFound)
            {

                // Append all characters until the found replacement.
                int @charsUntilReplacment = foundAt - startSearchFromIndex;
                bool isNothingToAppend = @charsUntilReplacment == 0;
                if (!isNothingToAppend)
                {
                    resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilReplacment);
                }



                // Process the replacement.
                if (!isReplacementNullOrEmpty)
                {
                    resultStringBuilder.Append(@newValue);
                }


                // Prepare start index for the next search.
                // This needed to prevent infinite loop, otherwise method always start search 
                // from the start of the string. For example: if an oldValue == "EXAMPLE", newValue == "example"
                // and comparisonType == "any ignore case" will conquer to replacing:
                // "EXAMPLE" to "example" to "example" to "example" … infinite loop.
                startSearchFromIndex = foundAt + oldValue.Length;
                if (startSearchFromIndex == str.Length)
                {
                    // It is end of the input string: no more space for the next search.
                    // The input string ends with a value that has already been replaced. 
                    // Therefore, the string builder with the result is complete and no further action is required.
                    return resultStringBuilder.ToString();
                }
            }


            // Append the last part to the result.
            int @charsUntilStringEnd = str.Length - startSearchFromIndex;
            resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilStringEnd);


            return resultStringBuilder.ToString();

        }

        public static List<string> AnalyzeDistinctRecordsInArray(List<string> sDef, string[] sCSV)
        {
            string[] sSorted = (string[])sCSV.Clone();
            Array.Sort(sSorted); //on trie le CSV pour trouver les doublons plus facilement

            bool BTuple;
            sDef.Add(sSorted[0]); //ajout de la première ligne (qui, par définition, n'est pas un doublon ;) )
            for (int iR = 1; iR < sSorted.Length; iR++)
            {
                if (sSorted[iR].Equals(sSorted[iR - 1])) { BTuple = true; } else { BTuple = false; } //a cause du tri, les doublons se suivent toujours
                if (!BTuple) { sDef.Add(sSorted[iR]); }
            }

            return sDef;
        }

        public static string ByteArrayToStr(System.Byte[] barr)
        {
            UTF8Encoding encoding = new();
            return encoding.GetString(barr, 0, barr.Length);
        }

        public static void ConvertColumnType(this DataTable dt, DataColumn dCMaster, Type newType)
        {
            if (dCMaster.DataType != newType)
            {
                DataColumn[] dcPK = dt.PrimaryKey;
                if (dcPK.Any(c => c.ColumnName.Equals(dCMaster.ColumnName)))
                {
                    dt.PrimaryKey = null;
                }

                bool bMasterIsBitArray = false;
                bool bMasterIsBitArrayV2 = false;

                bool bIsBit = false;
                bool bIsGuid = false;

                if (dCMaster.DataType.Name.Equals("Guid"))
                {
                    bIsGuid = true;
                }

                if (dCMaster.DataType.Name.Equals("BitArray"))
                {
                    bMasterIsBitArray = true;
                    int iMaxLength = 0;
                    foreach (DataRow dr in dt.Rows)
                    {
                        var bA = (BitArray)dr[dCMaster.ColumnName];
                        if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
                    }
                    if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
                }

                if (dCMaster.DataType.Name.Equals("Byte[]"))
                {
                    bMasterIsBitArrayV2 = true;
                    int iMaxLength = 0;
                    foreach (DataRow dr in dt.Rows)
                    {
                        var bA = (System.Byte[])dr[dCMaster.ColumnName];
                        if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
                    }
                    if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
                }

                var gCol = Guid.NewGuid();
                using DataColumn dc = new(dCMaster.ColumnName + "_" + gCol.ToString(), newType);
                // Add the new column which has the new type, and move it to the ordinal of the old column
                int ordinal = dt.Columns[dCMaster.ColumnName].Ordinal;
                dt.Columns.Add(dc);
                dc.SetOrdinal(ordinal);

                try
                {
                    dc.AllowDBNull = dCMaster.AllowDBNull;
                    dc.Unique = dCMaster.Unique;
                    dc.MaxLength = dCMaster.MaxLength;
                }
                catch { }

                dc.ColumnMapping = dCMaster.ColumnMapping;
                dc.Caption = string.Concat(dCMaster.Caption + "_" + gCol.ToString());
                dc.DefaultValue = dCMaster.DefaultValue;
                dc.Namespace = dCMaster.Namespace;

                // Get and convert the values of the old column, and insert them into the new
                int iError = 0;
                bool bJumpTOInvariant = false;

                foreach (DataRow dr in dt.Rows)
                {
                    if (dc.AllowDBNull)
                    {
                        if (dr[dCMaster.ColumnName].ToString().Length == 0)
                        {
                            dr[dc.ColumnName] = DBNull.Value;
                        }
                        else
                        {
                            if (bIsGuid)
                            {
                                var guid = (Guid)dr[dCMaster.ColumnName];
                                try { dr[dc.ColumnName] = guid.ToString(); } catch { }
                            }
                            else if (bMasterIsBitArray)
                            {
                                var bA = (BitArray)dr[dCMaster.ColumnName];
                                if (bIsBit)
                                {
                                    try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); } catch { }
                                }
                                else { try { dr[dc.ColumnName] = bA.ToBitString(); } catch { } }
                            }
                            else if (bMasterIsBitArrayV2)
                            {
                                var bA = (System.Byte[])dr[dCMaster.ColumnName];
                                if (bIsBit)
                                {
                                    try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); } catch { }
                                }
                                else { try { dr[dc.ColumnName] = System.Text.Encoding.UTF8.GetString(bA); } catch { } }
                            }
                            else
                            {
                                if (iError < 5) //éviter le débordement
                                {
                                    try
                                    {
                                        if (!bJumpTOInvariant)
                                        {
                                            try
                                            {

                                                if (newType.FullName.Equals("System.Guid")) //898ad694-ade0-dd11-99e2-00155d0a2d16
                                                {
                                                    Guid g;
                                                    Guid.TryParse(dr[dCMaster.ColumnName].ToString(), out g);
                                                    dr[dc.ColumnName] = g;
                                                }
                                                else
                                                {
                                                    dr[dc.ColumnName] = Convert.ChangeType(dr[dCMaster.ColumnName], newType);
                                                }
                                            }
                                            catch { bJumpTOInvariant = true; }
                                        }
                                        if (bJumpTOInvariant)
                                        {
                                            dr[dc.ColumnName] = Convert.ChangeType(dr[dCMaster.ColumnName], newType, CultureInfo.InvariantCulture);
                                        }
                                    }
                                    catch
                                    {
                                        iError++;
                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                    }
                    else
                    {
                        if (dr[dCMaster.ColumnName].ToString().Length == 0)
                        {
                            if (dc.DataType.Equals(Type.GetType("System.String")))
                            {
                                dr[dc.ColumnName] = "";
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Decimal")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Int32")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Int16")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Int64")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.DateTime")))
                            {
                                dr[dc.ColumnName] = Convert.ToDateTime("01/01/0001");
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.DateTimeOffset")))
                            {
                                dr[dc.ColumnName] = Convert.ToDateTime("01/01/0001");
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Boolean")))
                            {
                                dr[dc.ColumnName] = Convert.ToBoolean("false");
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Double")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Single")))
                            {
                                dr[dc.ColumnName] = 0;
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Byte")))
                            {
                                dr[dc.ColumnName] = Convert.ToByte(0);
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Collections.BitArray")))
                            {
                                dr[dc.ColumnName] = Convert.ToSByte(0);
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Byte[]")))
                            {
                                dr[dc.ColumnName] = Convert.ToSByte(0);
                            }
                            else if (dc.DataType.Equals(Type.GetType("System.Guid")))
                            {
                                dr[dc.ColumnName] = dr[dc.ColumnName] = "";
                            }
                        }
                        else
                        {
                            if (bIsGuid)
                            {
                                var guid = (Guid)dr[dCMaster.ColumnName];
                                try { dr[dc.ColumnName] = guid.ToString(); }
                                catch { }
                            }
                            else if (bMasterIsBitArray)
                            {
                                var bA = (BitArray)dr[dCMaster.ColumnName];
                                string sA = bA.ToBitString();
                                if (bIsBit)
                                {
                                    try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); }
                                    catch
                                    {
                                    }
                                }
                                else
                                {
                                    try { dr[dc.ColumnName] = bA.ToBitString(); }
                                    catch { }
                                }
                            }
                            else if (bMasterIsBitArrayV2)
                            {
                                var bA = (System.Byte[])dr[dCMaster.ColumnName];
                                string sA = System.Text.Encoding.UTF8.GetString(bA);
                                if (bIsBit)
                                {
                                    try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); }
                                    catch
                                    {
                                    }
                                }
                                else
                                {
                                    try { dr[dc.ColumnName] = System.Text.Encoding.UTF8.GetString(bA); }
                                    catch { }
                                }
                            }
                            else
                            {
                                try { dr[dc.ColumnName] = Convert.ChangeType(dr[dCMaster.ColumnName], newType); }
                                catch { }
                            }
                        }
                    }
                }

                // Remove the old column
                dt.Columns.Remove(dCMaster.ColumnName);

                // Give the new column the old column's name
                dc.ColumnName = dCMaster.ColumnName;
                dc.Caption = dCMaster.Caption;
            }
        }

        //public static void ConvertColumnType(this DataTable dt, DataColumn dCMaster, Type newType)
        //{
        //    Type sOldType = dCMaster.DataType;
        //    string sColumnName = dCMaster.ColumnName;

        //    if (sOldType != newType)
        //    {
        //        try
        //        {
        //            int ordinalMaster = -1;
        //            int ordinalCreated = -1;

        //            lock (dt)
        //            {
        //                // Add the new column which has the new type, and move it to the ordinal of the old column
        //                ordinalMaster = dt.Columns[sColumnName].Ordinal;
        //            }

        //            DataColumn[] dcPK = dt.PrimaryKey;
        //            if (dcPK.Any(c => c.ColumnName.Equals(sColumnName)))
        //            {
        //                dt.PrimaryKey = null;
        //            }

        //            bool bMasterIsBitArray = false;
        //            bool bMasterIsBitArrayV2 = false;

        //            bool bIsBit = false;
        //            bool bIsGuid = false;

        //            if (sOldType.Name.Equals("Guid"))
        //            {
        //                bIsGuid = true;
        //            }

        //            if (sOldType.Name.Equals("BitArray"))
        //            {
        //                bMasterIsBitArray = true;
        //                int iMaxLength = 0;
        //                foreach (DataRow dr in dt.Rows)
        //                {
        //                    var bA = (BitArray)dr[sColumnName];
        //                    if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
        //                }
        //                if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
        //            }

        //            if (sOldType.Name.Equals("Byte[]"))
        //            {
        //                bMasterIsBitArrayV2 = true;
        //                int iMaxLength = 0;
        //                foreach (DataRow dr in dt.Rows)
        //                {
        //                    var bA = (System.Byte[])dr[sColumnName];
        //                    if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
        //                }
        //                if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
        //            }

        //            var gCol = Guid.NewGuid();
        //            using DataColumn dc = new(sColumnName + "_" + gCol.ToString(), newType);

        //            lock (dt)
        //            {
        //                dt.Columns.Add(dc);
        //                ordinalCreated = dt.Columns[dc.ColumnName].Ordinal;
        //            }
        //            //dc.SetOrdinal(ordinal);

        //            try
        //            {
        //                dc.AllowDBNull = dCMaster.AllowDBNull;
        //                dc.Unique = dCMaster.Unique;
        //                dc.MaxLength = dCMaster.MaxLength;
        //            }
        //            catch { }

        //            dc.ColumnMapping = dCMaster.ColumnMapping;
        //            dc.Caption = string.Concat(dCMaster.Caption + "_" + gCol.ToString());
        //            dc.DefaultValue = dCMaster.DefaultValue;
        //            dc.Namespace = dCMaster.Namespace;

        //            // Get and convert the values of the old column, and insert them into the new
        //            int iError = 0;
        //            bool bJumpTOInvariant = false;

        //            foreach (DataRow dr in dt.Rows)
        //            {
        //                if (dc.AllowDBNull)
        //                {
        //                    if (dr[sColumnName].ToString().Length == 0)
        //                    {
        //                        dr[dc.ColumnName] = DBNull.Value;
        //                    }
        //                    else
        //                    {
        //                        if (bIsGuid)
        //                        {
        //                            var guid = (Guid)dr[sColumnName];
        //                            try { dr[dc.ColumnName] = guid.ToString(); } catch { }
        //                        }
        //                        else if (bMasterIsBitArray)
        //                        {
        //                            var bA = (BitArray)dr[sColumnName];
        //                            if (bIsBit)
        //                            {
        //                                try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); } catch { }
        //                            }
        //                            else { try { dr[dc.ColumnName] = bA.ToBitString(); } catch { } }
        //                        }
        //                        else if (bMasterIsBitArrayV2)
        //                        {
        //                            var bA = (System.Byte[])dr[sColumnName];
        //                            if (bIsBit)
        //                            {
        //                                try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); } catch { }
        //                            }
        //                            else { try { dr[dc.ColumnName] = System.Text.Encoding.UTF8.GetString(bA); } catch { } }
        //                        }
        //                        else
        //                        {
        //                            if (iError < 5) //éviter le débordement
        //                            {
        //                                try
        //                                {
        //                                    if (!bJumpTOInvariant)
        //                                    {
        //                                        try
        //                                        {

        //                                            if (newType.FullName.Equals("System.Guid")) //898ad694-ade0-dd11-99e2-00155d0a2d16
        //                                            {
        //                                                Guid g;
        //                                                Guid.TryParse(dr[sColumnName].ToString(), out g);
        //                                                dr[dc.ColumnName] = g;
        //                                            }
        //                                            else
        //                                            {
        //                                                dr[dc.ColumnName] = Convert.ChangeType(dr[sColumnName], newType);
        //                                            }
        //                                        }
        //                                        catch { bJumpTOInvariant = true; }
        //                                    }
        //                                    if (bJumpTOInvariant)
        //                                    {
        //                                        dr[dc.ColumnName] = Convert.ChangeType(dr[sColumnName], newType, CultureInfo.InvariantCulture);
        //                                    }
        //                                }
        //                                catch
        //                                {
        //                                    iError++;
        //                                }
        //                            }
        //                            else
        //                            {
        //                            }
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    if (dr[sColumnName].ToString().Length == 0)
        //                    {
        //                        if (dc.DataType.Equals(Type.GetType("System.String")))
        //                        {
        //                            dr[dc.ColumnName] = "";
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Decimal")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Int32")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Int16")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Int64")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.DateTime")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToDateTime("01/01/0001");
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.DateTimeOffset")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToDateTime("01/01/0001");
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Boolean")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToBoolean("false");
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Double")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Single")))
        //                        {
        //                            dr[dc.ColumnName] = 0;
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Byte")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToByte(0);
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Collections.BitArray")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToSByte(0);
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Byte[]")))
        //                        {
        //                            dr[dc.ColumnName] = Convert.ToSByte(0);
        //                        }
        //                        else if (dc.DataType.Equals(Type.GetType("System.Guid")))
        //                        {
        //                            dr[dc.ColumnName] = "";
        //                        }
        //                    }
        //                    else
        //                    {
        //                        if (bIsGuid)
        //                        {
        //                            var guid = (Guid)dr[sColumnName];
        //                            try { dr[dc.ColumnName] = guid.ToString(); }
        //                            catch { }
        //                        }
        //                        else if (bMasterIsBitArray)
        //                        {
        //                            var bA = (BitArray)dr[sColumnName];
        //                            string sA = bA.ToBitString();
        //                            if (bIsBit)
        //                            {
        //                                try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); }
        //                                catch
        //                                {
        //                                }
        //                            }
        //                            else
        //                            {
        //                                try { dr[dc.ColumnName] = bA.ToBitString(); }
        //                                catch { }
        //                            }
        //                        }
        //                        else if (bMasterIsBitArrayV2)
        //                        {
        //                            var bA = (System.Byte[])dr[sColumnName];
        //                            string sA = System.Text.Encoding.UTF8.GetString(bA);
        //                            if (bIsBit)
        //                            {
        //                                try { dr[dc.ColumnName] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); }
        //                                catch
        //                                {
        //                                }
        //                            }
        //                            else
        //                            {
        //                                try { dr[dc.ColumnName] = System.Text.Encoding.UTF8.GetString(bA); }
        //                                catch { }
        //                            }
        //                        }
        //                        else
        //                        {
        //                            try { dr[dc.ColumnName] = Convert.ChangeType(dr[sColumnName], newType); }
        //                            catch { }
        //                        }
        //                    }
        //                }
        //            }

        //            // Remove the old column
        //            lock (dt)
        //            {
        //                string sCreatedCol = dc.ColumnName;
        //                string sCaption = dCMaster.Caption;

        //                dt.Columns.RemoveAt(ordinalMaster);

        //                dc.ColumnName = sColumnName;
        //                dc.Caption = sCaption;
        //                dc.SetOrdinal(ordinalMaster);

        //            }
        //        }
        //        catch
        //        {
        //            throw;
        //        }
        //    }
        //}

        public static DataTable ConvertColumnType_Bulk(this DataTable dt, DataColumn dCMaster, Type newType)
        {
            DataTable dtBulk = new DataTable();

            Type sOldType = dCMaster.DataType;
            string sColumnName = dCMaster.ColumnName;

            if (sOldType != newType)
            {
                try
                {
                    DataColumn[] dcPK = dt.PrimaryKey;
                    if (dcPK.Any(c => c.ColumnName.Equals(sColumnName)))
                    {
                        dt.PrimaryKey = null;
                    }

                    bool bMasterIsBitArray = false;
                    bool bMasterIsBitArrayV2 = false;

                    bool bIsBit = false;
                    bool bIsGuid = false;

                    if (sOldType.Name.Equals("Guid"))
                    {
                        bIsGuid = true;
                    }

                    if (sOldType.Name.Equals("BitArray"))
                    {
                        bMasterIsBitArray = true;
                        int iMaxLength = 0;
                        foreach (DataRow dr in dt.Rows)
                        {
                            var bA = (BitArray)dr[sColumnName];
                            if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
                        }
                        if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
                    }

                    if (sOldType.Name.Equals("Byte[]"))
                    {
                        bMasterIsBitArrayV2 = true;
                        int iMaxLength = 0;
                        foreach (DataRow dr in dt.Rows)
                        {
                            var bA = (System.Byte[])dr[sColumnName];
                            if (bA.Length > iMaxLength) { iMaxLength = bA.Length; }
                        }
                        if (iMaxLength <= 1) { newType = Type.GetType("System.Boolean"); bIsBit = true; }
                    }

                    var gCol = Guid.NewGuid();
                    using DataColumn dc = new(sColumnName + "_" + gCol.ToString(), newType);

                    dtBulk.Columns.Add(dc);

                    try
                    {
                        dc.AllowDBNull = dCMaster.AllowDBNull;
                        dc.Unique = dCMaster.Unique;
                        dc.MaxLength = dCMaster.MaxLength;
                    }
                    catch { }

                    dc.Caption = dCMaster.ColumnName;
                    dc.DefaultValue = dCMaster.DefaultValue;

                    // Get and convert the values of the old column, and insert them into the new
                    int iError = 0;
                    bool bJumpTOInvariant = false;

                    foreach (DataRow dr in dt.Rows)
                    {
                        var newrow = dtBulk.NewRow();

                        if (dc.AllowDBNull)
                        {
                            if (dr[sColumnName].ToString().Length == 0)
                            {
                                newrow[0] = DBNull.Value;
                            }
                            else
                            {
                                if (bIsGuid)
                                {
                                    var guid = (Guid)dr[sColumnName];

                                    try { newrow[0] = guid.ToString(); } catch { }
                                }
                                else if (bMasterIsBitArray)
                                {
                                    var bA = (BitArray)dr[sColumnName];
                                    if (bIsBit)
                                    {
                                        try { newrow[0] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); } catch { }
                                    }
                                    else { try { newrow[0] = bA.ToBitString(); } catch { } }
                                }
                                else if (bMasterIsBitArrayV2)
                                {
                                    var bA = (System.Byte[])dr[sColumnName];
                                    if (bIsBit)
                                    {
                                        try { newrow[0] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); } catch { }
                                    }
                                    else { try { newrow[0] = System.Text.Encoding.UTF8.GetString(bA); } catch { } }
                                }
                                else
                                {
                                    if (iError < 5) //éviter le débordement
                                    {
                                        try
                                        {
                                            if (!bJumpTOInvariant)
                                            {
                                                try
                                                {

                                                    if (newType.FullName.Equals("System.Guid")) //898ad694-ade0-dd11-99e2-00155d0a2d16
                                                    {
                                                        Guid g;
                                                        Guid.TryParse(dr[sColumnName].ToString(), out g);
                                                        newrow[0] = g;
                                                    }
                                                    else
                                                    {
                                                        newrow[0] = Convert.ChangeType(dr[sColumnName], newType);
                                                    }
                                                }
                                                catch { bJumpTOInvariant = true; }
                                            }
                                            if (bJumpTOInvariant)
                                            {
                                                newrow[0] = Convert.ChangeType(dr[sColumnName], newType, CultureInfo.InvariantCulture);
                                            }
                                        }
                                        catch
                                        {
                                            iError++;
                                        }
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (dr[sColumnName].ToString().Length == 0)
                            {
                                if (dc.DataType.Equals(Type.GetType("System.String")))
                                {
                                    newrow[0] = "";
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Decimal")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Int32")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Int16")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Int64")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.DateTime")))
                                {
                                    newrow[0] = Convert.ToDateTime("01/01/0001");
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.DateTimeOffset")))
                                {
                                    newrow[0] = Convert.ToDateTime("01/01/0001");
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Boolean")))
                                {
                                    newrow[0] = Convert.ToBoolean("false");
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Double")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Single")))
                                {
                                    newrow[0] = 0;
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Byte")))
                                {
                                    newrow[0] = Convert.ToByte(0);
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Collections.BitArray")))
                                {
                                    newrow[0] = Convert.ToSByte(0);
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Byte[]")))
                                {
                                    newrow[0] = Convert.ToSByte(0);
                                }
                                else if (dc.DataType.Equals(Type.GetType("System.Guid")))
                                {
                                    newrow[0] = "";
                                }

                            }
                            else
                            {
                                if (bIsGuid)
                                {
                                    var guid = (Guid)dr[sColumnName];
                                    try { newrow[0] = guid.ToString(); }
                                    catch { }
                                }
                                else if (bMasterIsBitArray)
                                {
                                    var bA = (BitArray)dr[sColumnName];
                                    string sA = bA.ToBitString();
                                    if (bIsBit)
                                    {
                                        try { newrow[0] = Convert.ToBoolean(Convert.ToInt16(bA.ToBitString())); }
                                        catch
                                        {
                                        }
                                    }
                                    else
                                    {
                                        try { newrow[0] = bA.ToBitString(); }
                                        catch { }
                                    }
                                }
                                else if (bMasterIsBitArrayV2)
                                {
                                    var bA = (System.Byte[])dr[sColumnName];
                                    string sA = System.Text.Encoding.UTF8.GetString(bA);
                                    if (bIsBit)
                                    {
                                        try { newrow[0] = Convert.ToBoolean(Convert.ToInt16(System.Text.Encoding.UTF8.GetString(bA))); }
                                        catch
                                        {
                                        }
                                    }
                                    else
                                    {
                                        try { newrow[0] = System.Text.Encoding.UTF8.GetString(bA); }
                                        catch { }
                                    }
                                }
                                else
                                {
                                    try { newrow[0] = Convert.ChangeType(dr[sColumnName], newType); }
                                    catch { }
                                }
                            }
                        }

                        dtBulk.Rows.Add(newrow);
                    }
                }
                catch
                {
                    throw;
                }
            }
            return dtBulk;
        }

        public static long GetInt64HashCode(string strText)
        {
            long hashCode = 0;
            if (!string.IsNullOrEmpty(strText))
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] byteContents = Encoding.Unicode.GetBytes(strText);
                    byte[] hashText = sha256.ComputeHash(byteContents);

                    long hashCodeStart = BitConverter.ToInt64(hashText, 0);
                    long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
                    long hashCodeEnd = BitConverter.ToInt64(hashText, 24);

                    hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
                }
                ////Unicode Encode Covering all characterset
                //System.Byte[] byteContents = Encoding.Unicode.GetBytes(strText);
                //System.Security.Cryptography.SHA256 hash =
                //new System.Security.Cryptography.SHA256CryptoServiceProvider();
                //System.Byte[] hashText = hash.ComputeHash(byteContents);
                ////32Byte hashText separate
                ////hashCodeStart = 0~7  8Byte
                ////hashCodeMedium = 8~23  8Byte
                ////hashCodeEnd = 24~31  8Byte
                ////and Fold
                //long hashCodeStart = BitConverter.ToInt64(hashText, 0);
                //long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
                //long hashCodeEnd = BitConverter.ToInt64(hashText, 24);
                //hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
            }
            return hashCode;
        }

        public static List<SQLColumn> GetSQLColumnsFromDataTable(DataTable dt)
        {
            List<SQLColumn> SQLListC = new();

            foreach (DataColumn dc in dt.Columns)
            {
                SQLTools_Enums.TYPE_DATA cType;
                try
                {
                    string sCol = dc.DataType.Name.ToUpper();
                    sCol = sCol.Replace("[", "XXX").Replace("]", "ZZZ"); //problème de l'enum qui ne supporte pas des caractères spéciaux genre Byte[]
                    cType = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sCol);
                }
                catch { cType = SQLTools_Enums.TYPE_DATA.TEXT; }

                SQLListC.Add(new SQLColumn(dc.Ordinal, dc.Caption.Length > 0 && !dc.Caption.Equals(dc.ColumnName) ? dc.Caption : dc.ColumnName, cType, dc.DataType, dc.MaxLength.ToString(), dc.AllowDBNull, "", false, dc.Unique));
            }
            return SQLListC;
        }

        public async static Task<List<string>> CheckConnection(CONNString sConn, List<string> sListVariables, string sDBName, CancellationToken cancelToken)
        {
            //subtilité : le premier string de la liste de retour sera toujours le résultat du test de connection
            List<string> sAnswers = new();

            if (sDBName.Length > 0) { sDBName = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sDBName, sListVariables); }

            switch (sConn.SConnDriver.ToString()[..2])
            {
                case "AD":
                    string sA;
                    try
                    {
                        sA = ADTools.CheckConnection(sConn, sListVariables);
                    }
                    catch (Exception ex)
                    {
                        sA = Languages.Languages.shs_tool_checkconn_ad + ex.Message;
                    }
                    sAnswers.Add(Languages.Languages.shs_tool_checkconn_status + Environment.NewLine + sA);
                    break;

                case "MB":
                    string sM;
                    try
                    {
                        sM = MailTools.CheckConnection(sConn, sListVariables);
                    }
                    catch (Exception ex)
                    {
                        sM = Languages.Languages.shs_tool_checkconn_mb + ex.Message;
                    }
                    sAnswers.Add(Languages.Languages.shs_tool_checkconn_status + Environment.NewLine + sM);
                    break;

                case "WS":
                    try
                    {
                        var wsCheck = WebRequest.Create(sConn.SConnString(sListVariables).Split(Convert.ToChar("?"))[0]);
                        //wsCheck.Proxy = Toolbox.GetProxyFromConnectionString(WSVariables.WebServiceProxy);
                        string wsHost = wsCheck.RequestUri.Host;
                        bool bPingOK = Toolbox.PingHost(wsHost);
                        sAnswers.Add(bPingOK ? Languages.Languages.shs_tool_checkconn_ws01 + wsHost + ")" : Languages.Languages.shs_tool_checkconn_ws02 + wsHost + ")");
                    }
                    catch (Exception ex)
                    {
                        sAnswers.Add(Languages.Languages.shs_tool_checkconn_status + Environment.NewLine + ex.Message);
                    }
                    break;

                case "FI":

                    string sPathToImport = sConn.SConnString(sListVariables);

                    if (sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("FTP") > -1) // test de la spécificité Fuzible sur les chaines car ça peut être un (S)FTP : USERNAME=MyUser;PASSWORD=MyPassword;FTP=MyFTP;PORT=MyPort 
                    {
                        List<string> sFiles = new();
                        try
                        {
                            var ftpfiles = FTPTools.GetListOfFileFromFTPOrSFTPPath(sConn, "", sListVariables, false);
                            foreach (string[] sF in ftpfiles)
                            {
                                sFiles.Add(sF[0]);
                            }
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_file01, sFiles.Count.ToString(), Languages.Languages.shs_tool_checkconn_file02));
                        }
                        catch (Exception ex)
                        {
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathko, ex.Message, ")"));
                        }
                        if (sFiles != null)
                        {
                            foreach (string sF in sFiles) { sAnswers.Add(sF); }
                        }
                    }
                    else if (sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).IndexOf("NETWORK") > -1 && sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD).Length > 0) // test de la spécificité Fuzible sur les chaines car ça peut être un (S)FTP : USERNAME=MyUser;PASSWORD=MyPassword;FTP=MyFTP;PORT=MyPort 
                    {
                        string[] sFiles = Array.Empty<string>();

                        try
                        {
                            Match mcComputer = Regex.Match(sPathToImport, @"\\\\([^\\]+)\\");

                            string sServer = mcComputer.Value[2..^1];
                            var net = NetworkShareAccesser.Access(sServer, sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));
                            sFiles = Directory.GetFiles(sPathToImport);
                            net.Dispose();

                            //NetworkCredential theNetworkCredential = new NetworkCredential(sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));
                            //CredentialCache theNetCache = new CredentialCache
                            //{
                            //    { new Uri(mcComputer.Value[0..^1]), "Basic", theNetworkCredential }
                            //};
                            //sFiles = Directory.GetFiles(sPathToImport);
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_file01, sFiles.Length.ToString(), Languages.Languages.shs_tool_checkconn_file02));
                        }
                        catch (Exception ex)
                        {
                            //méthode 2
                            try
                            {
                                var networkPath = sPathToImport;
                                var credentials = new NetworkCredential(sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_USERNAME), sConn.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_NETWORK_PASSWORD));

                                using (new NetworkConnection(networkPath, credentials))
                                {
                                    var fileList = Directory.GetFiles(networkPath);

                                    sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_file01, fileList.Length.ToString(), Languages.Languages.shs_tool_checkconn_file02));
                                }
                            }
                            catch (Exception ex2)
                            {
                                sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathko, ex2.Message, ")"));
                            }

                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathko, ex.Message, ")"));
                        }
                        if (sFiles != null)
                        {
                            foreach (string sF in sFiles) { sAnswers.Add(Path.GetFileName(sF)); }
                        }
                        if (sFiles.Length == 0) //bricolage !
                        {
                            sAnswers.Add(string.Empty);
                        }
                    }
                    else if (Directory.Exists(sPathToImport))
                    {
                        string[] sFiles = Array.Empty<string>();
                        try
                        {
                            sFiles = Directory.GetFiles(sPathToImport);
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_file01, sFiles.Length.ToString(), Languages.Languages.shs_tool_checkconn_file02));
                        }
                        catch (Exception ex)
                        {
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathko, ex.Message, ")"));
                        }
                        if (sFiles != null)
                        {
                            foreach (string sF in sFiles) { sAnswers.Add(Path.GetFileName(sF)); }
                        }
                        if (sFiles.Length == 0) //bricolage !
                        {
                            sAnswers.Add(string.Empty);
                        }
                    }
                    else
                    {
                        try
                        {
                            Directory.CreateDirectory(sPathToImport);
                            sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathcreated));
                            sAnswers.Add(string.Empty);
                        }
                        catch { sAnswers.Add(string.Concat(Languages.Languages.shs_tool_checkconn_filepathko02)); }

                    }

                    break;

                case "DB":

                    if (!sConn.SConnString(sListVariables).Trim().Equals(""))
                    {
                        //ListOfJobs[GetIndexSection(sCurrentJobID)].DatabaseName_Target = SQLTools_Utils.GetDatabaseNameFromConnectionString(tbConnectionStringImport.Text.Trim(), (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverTarget.SelectedValue.ToString()));
                        try
                        {
                            sAnswers = await SQLTools.CheckConnection(sConn, sDBName, true, 10, sListVariables);
                        }
                        catch (Exception ex)
                        {
                            sAnswers.Add(Languages.Languages.shs_tool_checkconn_sql_ko + ex.Message);
                        }
                    }
                    else
                    {
                        sAnswers.Add(Languages.Languages.shs_tool_checkconn_sql_noparams);
                    }

                    break;

                case "NS":

                    if (!sConn.SConnString(sListVariables).Trim().Equals(""))
                    {
                        //ListOfJobs[GetIndexSection(sCurrentJobID)].DatabaseName_Target = SQLTools_Utils.GetDatabaseNameFromConnectionString(tbConnectionStringImport.Text.Trim(), (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), cbDriverTarget.SelectedValue.ToString()));
                        try
                        {
                            sAnswers = NOSQLTools.CheckConnection(sConn, true, 10, sListVariables);
                        }
                        catch (Exception ex)
                        {
                            sAnswers.Add(Languages.Languages.shs_tool_checkconn_sql_ko + ex.Message);
                        }
                    }
                    else
                    {
                        sAnswers.Add(Languages.Languages.shs_tool_checkconn_sql_noparams);
                    }

                    break;
            }
            return sAnswers;
        }

        public static bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                pinger?.Dispose();
            }

            return pingable;
        }

        public static string RemoveWhitespace(this string input)
        {
            return new string(input.ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());
        }

        public static string GetStringFromStream(Stream stream)
        {
            string data;
            try
            {
                System.Byte[] bytes = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(bytes, 0, (int)stream.Length);
                data = Encoding.ASCII.GetString(bytes);
                stream = null;
                bytes = null;
            }
            catch (Exception)
            {
                throw;
            }
            return data;
        }

        public static bool IsNumeric(this string s, CONNString sConn)
        {
            bool isNum;
            decimal decTest;

            //si il y a des données de type 01 ou 001, on garde le format varchar
            //note des nombres exotiques genre -.123 (-0,123)
            //-2,27373675443232E-13

            if (Regex.IsMatch(s, REGEX_1x))
            {
                //bool bOK = Decimal.TryParse(s.Replace(".", ","), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                try
                {
                    string sComma = ",";
                    string sDot = ".";
                    //format des décimaux
                    if ((sConn != null && !sConn.DecimalLocale) || INIProgram.SYSTEM_CULTURE_NUMBER.NumberDecimalSeparator.Equals('.'))
                    {
                        sComma = ".";
                        sDot = ",";
                    }

                    decTest = Convert.ToDecimal(s.Replace(sDot, sComma));
                    isNum = true;
                }
                catch { isNum = false; }
            }
            else { isNum = false; }

            //if (Regex.IsMatch(s, REGEX_ISNUMERIC))
            //{
            //    if (Regex.IsMatch(s, REGEX_0x)) //cas des "001"
            //    {
            //        isNum = false;
            //    }
            //    else
            //    {
            //        isNum = true;
            //    }
            //}
            //else
            //{
            //    bool bOK = Decimal.TryParse(s.Replace(".", ","), NumberStyles.Any, CultureInfo.InvariantCulture, out _);

            //    if (!bOK && Regex.IsMatch(s, REGEX_1x)) //cas des "-.01" ou "-,20" ou ".05" ou ",50"
            //        try { decTest = Convert.ToDecimal(s.Replace(".", ",")); bOK = true; } 
            //        catch { }

            //    //if (!bOK && (s.IndexOf("-,") == 0) || s.IndexOf("-.") == 0) //cas des "-.00" ou "-,00"
            //    //{ bOK = Decimal.TryParse(string.Concat("-0,", s.Substring(2)), NumberStyles.Any, CultureInfo.InvariantCulture, out Decimal outputB); }
            //    //if (!bOK && (s.IndexOf(",") == 0) || s.IndexOf(".") == 0) //cas des ".00" ou ",00"
            //    //{ bOK = Decimal.TryParse(string.Concat("0", s), NumberStyles.Any, CultureInfo.InvariantCulture, out Decimal outputB); }

            //    if (bOK)
            //    { isNum = true; }
            //    else { isNum = false; }
            //    //float.TryParse(s, out float output);
            //    //if (output.ToString().Equals(s))
            //    //{ isNum = true; }
            //    //else { isNum = false; }
            //}

            //if (s.Length > 1 & s.StartsWith("0") & s.IndexOf(".") == -1 & s.IndexOf(",") == -1)
            //{
            //    isNum = false;
            //}
            //else
            //{
            //    isNum = float.TryParse(s, out output);
            //}


            return isNum;
        }

        public static int IsDate1OrDateTime2(this string s, CONNString CS)
        {
            s = s.Trim();
            bool isDate;
            bool isDateTime = false;
            bool bIsNormal = false;

            if (s.Length <= 24) // peu de chance qu'un format date soit > 24 caractères, au pire on aurait 2018-01-01 01:12:50.8
            {
                //test multi-culture
                isDate = DateTime.TryParse(s, CS.DateLocale ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.GetCultureInfo("us-US"), DateTimeStyles.None, out _);
                //isDate = false;

                ////test culture actuelle
                //if (!isDate) { isDate = DateTime.TryParse(s.Trim(), out _); }

                if (!isDate)
                {
                    if (s.Length == 8) //test des dates de type YYYYMMDD 
                    {
                        if (Regex.IsMatch(s, "(1\\d{3}|2[01]\\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\\d|3[0-1])")) //YYYYMMDD
                        {
                            string sD = string.Concat(s.Substring(6, 2), "/", s.Substring(4, 2), "/", s[..4]);
                            isDate = DateTime.TryParse(sD, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out _);
                        }
                        else if (Regex.IsMatch(s, "(1\\d{3}|2[01]\\d{2})(0[1-9]|[12]\\d|3[0-1])(0[1-9]|1[0-2])")) //YYYYDDMM
                        {
                            string sD = string.Concat(s.Substring(6, 2), "/", s.Substring(4, 2), "/", s[..4]);
                            isDate = DateTime.TryParse(sD, CultureInfo.GetCultureInfo("us-US"), DateTimeStyles.None, out _);
                        }
                        else if (Regex.IsMatch(s, "(0[1-9]|[12]\\d|3[0-1])(0[1-9]|1[0-2])(1\\d{3}|2[01]\\d{2})")) //DDMMYYYY
                        {
                            string sD = string.Concat(s[..2], "/", s.Substring(2, 2), "/", s.Substring(4, 4));
                            isDate = DateTime.TryParse(sD, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out _);
                        }
                        else if (Regex.IsMatch(s, "(0[1-9]|[12]\\d|3[0-1])(1\\d{3}|2[01]\\d{2})(0[1-9]|1[0-2])")) //MMDDYYYY
                        {
                            string sD = string.Concat(s[..2], "/", s.Substring(2, 2), "/", s.Substring(4, 4));
                            isDate = DateTime.TryParse(sD, CultureInfo.GetCultureInfo("us-US"), DateTimeStyles.None, out _);
                        }
                    }
                }
                else { bIsNormal = true; }

                if (isDate && bIsNormal)
                {
                    if (!Regex.IsMatch(s, "^\\d*(-|\\/).*") || s.Length < 8) //TODO : postulat douteux
                    {
                        isDate = false;
                    }
                    else if (isDate && s.Length > 10)
                    {
                        if (s.Trim().IndexOf("00:00:00") > 0)
                        {
                            isDateTime = false;
                        }
                        else { isDateTime = true; }
                    }
                }
            }
            else
            {
                isDate = false;
            }

            return isDateTime ? 2 : isDate ? 1 : 0;
        }

        public static bool IsInteger(this string s, bool bAllowSpecials, bool bAllow0First)
        {
            if (!bAllowSpecials && (s.IndexOf(",") > -1 || s.IndexOf(".") > -1))
            {
                // même si je reçois du 0.00 ou 3,00
                return false;
            }

            if (Regex.IsMatch(s, SHSRegex.REGEX_ISINTEGER, RegexOptions.Multiline))
            {
                if (Regex.IsMatch(s, SHSRegex.REGEX_ISINTEGER_STARTSWITH_0))
                {
                    if (bAllow0First) { return true; } else { return false; }
                }
                else { return true; }
            }
            else if (bAllowSpecials && Regex.IsMatch(s, SHSRegex.REGEX_ISINTEGER_EXTENDED, RegexOptions.Multiline))
            {
                return true;
            }
            else { return false; }

            //if (!bAllowSpecials)
            //{
            //    if (s.IndexOf(",") > -1 || s.IndexOf(".") > -1) // même si je reçois du 0.00 ou 3,00
            //    { return false; }
            //}

            //Int64.TryParse(s, out long iResult);

            //if (iResult.ToString().Equals(s))
            //{ isInteger = true; }
            //else
            //{
            //    if (s.StartsWith(",") || s.StartsWith(".")) // cas de .05 ou -.1256 (par exemple)
            //    { isInteger = false; }
            //    else
            //    {
            //        s = s.Replace(".", ",");
            //        Decimal.TryParse(s, out Decimal dTest);
            //        Decimal dTestC = Math.Ceiling(dTest);
            //        Decimal dTestR = Math.Floor(dTest);
            //        if (dTestC.ToString().Equals(dTestR.ToString()) && (!dTest.ToString().Equals("0"))) //je rajoute != 0 parce que une valeur comme "2.47121089999996" donne 0 des 2 côtés
            //        { isInteger = true; }
            //        else { isInteger = false; }
            //    }
            //}
            //return isInteger;
        }

        public static bool IsText(this string s)
        {
            if (s.Contains('\n') || s.Contains('\r') || s.Contains('\t'))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsDecimal(this string s, CONNString CS, bool bAllow0First)
        {
            bool bIsD;

            string sComma = ",";
            string sDot = ".";

            //format des décimaux
            if (!CS.DecimalLocale)
            {
                sComma = ".";
                sDot = ",";
            }

            if (!bAllow0First && Regex.IsMatch(s, SHSRegex.REGEX_ISINTEGER_STARTSWITH_0))
            {
                bIsD = false;
            }
            else
            {
                bIsD = decimal.TryParse(s.Replace(sDot, sComma), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
            }
            //return decimal.TryParse(s, out decimal output);
            return bIsD;
        }

        public static int IsBit1OrBoolean2(string sValue)
        {
            sValue = sValue.Trim();
            int iOK = 0;

            if (sValue.Length <= 5) // on se fait pas chier à analyser des champs trop longs, ce ne sera pas du format boolean
            {
                sValue = sValue.ToLower();
                if (sValue.Equals("0") | sValue.Equals("1"))
                {
                    iOK = 1;
                }
                else if (sValue.Equals("true") | sValue.Equals("false") | sValue.Equals("oui") | sValue.Equals("non"))
                {
                    iOK = 2;
                }
            }

            return iOK;

        }

        internal class CharAnalyzer
        {
            internal char Char { get; set; }
            internal int TotalCountPerSep { get; set; } = 0;
            internal int AvgIterationsCharPerRow { get; set; } = 0;
            internal bool IsCharConstantPerRow { get; set; } = true;
            internal bool EquilibratedRow { get; set; } = false;
            internal List<int> CountPerRow = new List<int>();
            internal int RowsThatAreMatchingAvgCount { get; set; } = 0;
            internal List<int> QteCharOnEachRow = new List<int>();

            internal decimal PercentRowsThatMatchesAverageCharCount(int iFileLen)
            {
                if (iFileLen == 0)
                {
                    return -1;
                }
                else
                {
                    return (Convert.ToDecimal(RowsThatAreMatchingAvgCount) / Convert.ToDecimal(iFileLen)) * 100;
                }
            }
        }

        public static string CSVCharSeparator(string[] sFile, int iMaxRowsAnalyzer, string sChars = ";,|\t.")
        {
            List<CharAnalyzer> sCharAnalysis = new();
            sChars = sChars.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\b", "\b").Replace("\\v", "\v");

            char[] cArray = sChars.ToCharArray();
            foreach (char c in cArray)
            {
                sCharAnalysis.Add(new CharAnalyzer { Char = c });
            }

            char[] testchars;
            int iLength;
            int iRows = sFile.Length > iMaxRowsAnalyzer ? iMaxRowsAnalyzer : sFile.Length;

            if (sFile.Length > 1)
            {
                //puis on va vérifier que cette quantité est constante à chaque ligne
                foreach (var charAnalysis in sCharAnalysis)
                {
                    for (int iR = 0; iR < iRows; iR++) //inutile d'analyser plus de 10000 lignes
                    {
                        int iQteCharOnThatRow = 0;

                        if (!string.IsNullOrEmpty(sFile[iR]))
                        {
                            testchars = sFile[iR].ToCharArray();
                            iLength = sFile[iR].Length;

                            //on va compter le nombre d'occurence de chaque caractère séparateur sur chaque ligne
                            for (int n = 0; n < testchars.Length; n++)
                            {
                                if (testchars[n] == charAnalysis.Char)
                                {
                                    charAnalysis.TotalCountPerSep++;
                                    iQteCharOnThatRow++;
                                }
                            }
                            charAnalysis.QteCharOnEachRow.Add(iQteCharOnThatRow);

                            var avgPerRow = (int)Math.Ceiling((double)charAnalysis.TotalCountPerSep / (iR + 1));
                            if (charAnalysis.QteCharOnEachRow.Last() != avgPerRow)
                            {
                                charAnalysis.IsCharConstantPerRow = false;
                            }
                            else
                            {
                                charAnalysis.RowsThatAreMatchingAvgCount++;
                            }
                            charAnalysis.AvgIterationsCharPerRow = avgPerRow;
                        }
                    }
                }

                //analyse finale : on cherche d'abord quel caractère est constant. Si aucun n'est constant, on prend le plus grand nombre d'itérations
                int iDxMax = -1;
                int iQteMax = 0;

                foreach (var charAnalysis in sCharAnalysis)
                {
                    if (charAnalysis.IsCharConstantPerRow && charAnalysis.TotalCountPerSep > 0)
                    {
                        if (iQteMax <= charAnalysis.TotalCountPerSep)
                        {
                            iQteMax = charAnalysis.TotalCountPerSep;
                            charAnalysis.EquilibratedRow = true;
                            break;
                        }
                    }

                    if (!charAnalysis.IsCharConstantPerRow && charAnalysis.TotalCountPerSep > 0) //instabilité : la quantité mesurée n'est pas constante mais il y a plusieurs itérations malgré tout
                    {
                        if (iQteMax <= charAnalysis.TotalCountPerSep)
                        {
                            //iDxMax = iB;
                            iQteMax = charAnalysis.TotalCountPerSep;
                            //on contrôle l'équilibre des lignes pour décider quelle priorité on donne à l'un ou l'autre des caractères
                            for (int iR = 0; iR < iRows; iR++) //on cherche la première ligne qui s'approche de cette moyenne pour la considérer comme "prioritaire"
                            {
                                int iC1 = sFile[iR].Split(charAnalysis.Char).Length - 1;
                                if (iC1 > 0 && iC1 == charAnalysis.AvgIterationsCharPerRow) { charAnalysis.EquilibratedRow = true; }
                                //gérer par rapport aux quantités

                            }
                            //if (bFound) { break; }
                        }
                    }
                }

                //prendre l'item équilibré dans eQuilibratedRow en fonction de l'itération la plus fréquence trouvée dans sAvgCountPerRow
                int idxWithBestPercent = 0;
                decimal dMaxPercent = 0;
                int iBestAvg = 0;
                for (int i = 0; i < sCharAnalysis.Count; i++)
                {
                    decimal d = sCharAnalysis[i].PercentRowsThatMatchesAverageCharCount(iRows);
                    if (iBestAvg < sCharAnalysis[i].AvgIterationsCharPerRow && d > dMaxPercent)
                    {
                        iBestAvg = sCharAnalysis[i].AvgIterationsCharPerRow;
                        dMaxPercent = d;
                        idxWithBestPercent = i;
                    }
                    if (sCharAnalysis[i].AvgIterationsCharPerRow > iDxMax && sCharAnalysis[i].EquilibratedRow) { iDxMax = i; }
                }

                if (iDxMax == -1) 
                { 
                    //on prend l'analyse qui statistiquement a le plus d'itérations récurrentes
                    return sCharAnalysis[idxWithBestPercent].Char.ToString(); 
                }
                else
                {
                    //j'utilise des Regex Split pour analyser les fichiers donc je met des échappements pour tous les caractères spéciaux
                    char c = sCharAnalysis[iDxMax].Char;
                    if (c.Equals(Convert.ToChar("|"))) { return "\\|"; }
                    else if (c.Equals(Convert.ToChar("["))) { return "\\["; }
                    else if (c.Equals(Convert.ToChar("]"))) { return "\\]"; }
                    else if (c.Equals(Convert.ToChar("("))) { return "\\("; }
                    else if (c.Equals(Convert.ToChar(")"))) { return "\\)"; }
                    else
                    {
                        return c.ToString();
                    }
                }
            }
            else { return ";"; } //impossible de savoir le caractère séparateur avec une seule ligne de traitement ! 
        }

        public static string GetPercent(int iStart, int iStop, int iActuel)
        {
            double fFormuleA = iActuel - iStart;
            double fFormuleB = iStop - iStart;
            double fFinal = fFormuleA / fFormuleB;
            fFinal *= 100;
            fFinal = Math.Round(fFinal, 1);
            return " (" + fFinal.ToString() + " %)";
        }

        public static string SetCleanDate(string sValue, Job JobParameters, CONNString CS, bool bSetNullIfEmpty, bool bForSQLInsert, int bTargetIsDate1OrDateTime2)
        {
            //TODO : attention aux configurations par pays ?
            string sDate = "";
            bool bIsProbablyADate = false;

            if (sValue.Length > 0)
            {
                DateTime dDate;
                //juin 2021
                //if (sValue.Length == 8)
                //{ sValue = DateTime.Parse(sValue).ToString(); }
                if (DateTime.TryParse(sValue, out dDate) || DateTime.TryParseExact(sValue, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dDate))
                {
                    if (bTargetIsDate1OrDateTime2 == 3)
                    {
                        try { dDate = DateTimeOffset.Parse(sValue).UtcDateTime; } catch { }
                    }

                    if (dDate.ToString().Length >= 10)
                    {
                        bIsProbablyADate = true;
                    }

                    if (bIsProbablyADate)
                    {
                        if (dDate.Year < JobParameters.GlobalParameters.SQL_LOW_DATE)
                        {
                            dDate = dDate.AddYears(JobParameters.GlobalParameters.SQL_LOW_DATE - dDate.Year);
                        }
                        if (dDate.Year > JobParameters.GlobalParameters.SQL_HIGH_DATE)
                        {
                            dDate = dDate.AddYears(JobParameters.GlobalParameters.SQL_HIGH_DATE - dDate.Year);
                        }


                        string sD = "dd";
                        string sM = "MM";

                        //quand on ne peut pas forcer le driver à passer une commande qui définit les dates au format européen, 
                        //on fait l'inversion manuellement (système US: MM/dd/YYYY)
                        if (Regex.IsMatch(CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.DRIVER_DATE_LOCALE), "M{1,2}/d{1,2}"))
                        {
                            sD = "MM";
                            sM = "dd";
                        }

                        switch (CS.SqlDateTypeCompatibility)
                        {
                            case SQLTools_Enums.TYPE_DATA.DATE:
                                if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLSERVER)
                                {
                                    sDate = dDate.ToString(string.Concat(sD, "/", sM, "/yyyy"));
                                }
                                else if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_ORACLE)
                                {
                                    sDate = dDate.ToString(string.Concat(sD, "-", sM, "-yyyy"));
                                }
                                else if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                                {
                                    sDate = dDate.ToString("yyyy-MM-dd");
                                }
                                else
                                {
                                    sDate = dDate.ToString(string.Concat("yyyy-", sM, "-", sD));
                                }
                                break;
                            case SQLTools_Enums.TYPE_DATA.DATETIME:
                                if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLSERVER)
                                {
                                    sDate = sValue.Length > 10 ? dDate.ToString(string.Concat(sD, "/", sM, "/yyyy HH:mm:ss")) : dDate.ToString(string.Concat(sD, "/", sM, "/yyyy"));
                                }
                                else if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                                {
                                    sDate = dDate.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    sDate = sValue.Length > 10 ? dDate.ToString(string.Concat("yyyy-", sM, "-dd HH:mm:ss")) : dDate.ToString(string.Concat("yyyy-", sM, "-", sD));
                                }
                                break;
                            case SQLTools_Enums.TYPE_DATA.TIMESTAMP:
                                if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_ORACLE)
                                {
                                    sDate = sValue.Length > 10 ? dDate.ToString(string.Concat(sD, "-", sM, "-yyyy HH:mm:ss")) : dDate.ToString(string.Concat(sD, "-", sM, "-yyyy"));
                                }
                                else if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_SQLITE)
                                {
                                    sDate = dDate.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else { sDate = sValue.Length > 10 ? dDate.ToString(string.Concat("yyyy-", sM, "-dd HH:mm:ss")) : dDate.ToString(string.Concat("yyyy-", sM, "-", sD)); }
                                break;
                            default:
                                sDate = sValue.Length > 10 ? dDate.ToString(string.Concat("yyyy-", sM, "-dd HH:mm:ss")) : dDate.ToString(string.Concat("yyyy-", sM, "-", sD));
                                break;
                        }
                    }
                    else
                    {
                        sDate = "";
                    }

                }
                else
                {
                    sDate = "";
                }

            }

            if (sDate.Length == 0 && bSetNullIfEmpty)
            {
                sDate = "NULL";
            }
            else
            {
                if (bForSQLInsert)
                {
                    //if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_MYSQL)
                    //{ sDate = string.Concat(bTargetIsDate1OrDateTime2 == 1 ? "DATE" : "DATETIME", "('", sDate, "')"); }
                    //else
                    if (JobParameters.ConnectionString_Target.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS)
                    {
                        sDate = string.Concat(bTargetIsDate1OrDateTime2 == 1 ? "CDATE" : "CDATE", "('", sDate, "')");
                    }
                    else
                    {
                        sDate = string.Concat("CAST('", sDate, "' AS ", bTargetIsDate1OrDateTime2 == 1 ? "DATE" : CS.SqlDateTypeCompatibility.ToString(), ")");
                    }
                    //sDate = string.Concat(SQL_INSERT_ECHAP_VALUE, sDate, SQL_INSERT_ECHAP_VALUE);
                }
            }

            return sDate;

        }

        public static string SetCleanNumber(string sValue, CONNString CS, SQLTools_Enums.TYPE_DATA tdData, bool bNullIfEmpty, bool bAllowIntegersWithSpecials, bool bAllowIntegersStartingWith0)
        {
            //problème SQLite : quand une colonne d'entrée a vraiment un false ou un true, le SQLite ne proposant pas ce type, on convertit l'entrée true/false en INT
            //if (Regex.IsMatch(sValue, "[A-z]+"))
            //{
            //    if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase)) { return "0"; }
            //    else if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase)) { return "1"; }
            //    else if (sValue.Equals("non", StringComparison.OrdinalIgnoreCase)) { return "0"; }
            //    else if (sValue.Equals("oui", StringComparison.OrdinalIgnoreCase)) { return "1"; }
            //    else if (sValue.Equals("nein", StringComparison.OrdinalIgnoreCase)) { return "0"; }
            //    else if (sValue.Equals("ja", StringComparison.OrdinalIgnoreCase)) { return "1"; }
            //    else if (sValue.Equals("faux", StringComparison.OrdinalIgnoreCase)) { return "0"; }
            //    else if (sValue.Equals("vrai", StringComparison.OrdinalIgnoreCase)) { return "1"; }
            //    else if (sValue.Equals("no", StringComparison.OrdinalIgnoreCase)) { return "0"; }
            //    else if (sValue.Equals("si", StringComparison.OrdinalIgnoreCase)) { return "1"; }
            //}

            string sComma = ",";
            string sDot = ".";

            //format des décimaux
            if (!CS.SConnDriverSuffix.Equals("DB") && !CS.DecimalLocale)
            {
                sComma = ".";
                sDot = ",";
            }

            string sNombre = "";
            long iNombre;
            float fNombre;
            decimal dNombre;

            try
            {

                if (sValue.Length > 0)
                {
                    switch (tdData)
                    {
                        case SQLTools_Enums.TYPE_DATA.INT:
                            if (sValue.IndexOf(sComma) > -1)
                            {
                                sValue = sValue.Replace(sComma, sDot);
                                decimal.TryParse(sValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dTest);
                                long.TryParse(dTest.ToString().Split(Convert.ToChar(sComma))[0], out iNombre);
                            }
                            else { iNombre = Convert.ToInt64(sValue); }
                            sNombre = iNombre.ToString();
                            break;
                        case SQLTools_Enums.TYPE_DATA.DECIMAL:
                            decimal.TryParse(sValue.Replace(sComma, sDot), NumberStyles.Any, CultureInfo.InvariantCulture, out dNombre);
                            sNombre = dNombre.ToString("G29").Replace(sComma, sDot);
                            break;
                        case SQLTools_Enums.TYPE_DATA.FLOAT:
                            fNombre = float.Parse(sValue, NumberStyles.Any, CultureInfo.InvariantCulture);
                            sNombre = fNombre.ToString("G29").Replace(sComma, sDot);
                            break;
                        case SQLTools_Enums.TYPE_DATA.DOUBLE:
                            fNombre = float.Parse(sValue, NumberStyles.Any, CultureInfo.InvariantCulture);
                            sNombre = fNombre.ToString("G29").Replace(sComma, sDot);
                            break;
                        case SQLTools_Enums.TYPE_DATA.UNKNOWN:
                            if (IsInteger(sValue, bAllowIntegersWithSpecials, bAllowIntegersStartingWith0))
                            {
                                if (sValue.IndexOf(sComma) > -1)
                                {
                                    sValue = sValue.Replace(sComma, sDot);
                                    long.TryParse(sValue.Split(Convert.ToChar(sDot))[0], out iNombre);
                                }
                                else { long.TryParse(sValue, out iNombre); }
                                sNombre = iNombre.ToString();
                            }
                            else if (IsDecimal(sValue, CS, bAllowIntegersStartingWith0))
                            {
                                decimal.TryParse(sValue.Replace(sComma, sDot), NumberStyles.Any, CultureInfo.InvariantCulture, out dNombre);
                                sNombre = dNombre.ToString("G29").Replace(sComma, sDot);
                            }
                            break;

                    }
                }

            }
            catch (Exception)
            {
                sNombre = "";
            }

            if (sNombre.Length == 0 && bNullIfEmpty)
            {
                sNombre = "NULL";
            }

            return sNombre;

        }

        public static string SetCleanString(string sValue, bool bRemoveSpecialCharsWhenWritingInFile, string sCSVSeparatorChar, bool bNullIfEmpty, CONNString sConn)
        {
            if (sValue.Length > 0)
            {
                if (bRemoveSpecialCharsWhenWritingInFile)
                {
                    int bSep = -1;
                    int bJump1 = -1;
                    int bJump2 = -1;

                    bool bSearch = Regex.Match(sValue, "(\\r|\\n|" + Toolbox.RemoveRegexFromString(sCSVSeparatorChar) + ")").Success;

                    if (bSearch)
                    {
                        bSep = sValue.IndexOf(sCSVSeparatorChar);
                        bJump1 = sValue.IndexOf('\r');
                        bJump2 = sValue.IndexOf('\n');
                    }

                    if (sConn.SConnDriver == SQLTools_Enums.BDD.FI_CSV && bSep > -1)
                    {
                        sValue = string.Concat("\"", sValue, "\"");
                    } // sValue.Replace(sCSVSeparatorChar, "°");

                    if (sConn.SConnDriver == SQLTools_Enums.BDD.FI_JSON)
                    {
                        sValue = sValue.Replace("\"", "\"\"");
                    }

                    if (bJump1 > -1)
                    {
                        sValue = Regex.Replace(sValue, "\\r", " ");
                    }
                    //TODO : pourquoi ? ne peut-on pas détecter le format lorsque les données sont analysées à partir d'un fichier à plat ???
                    if (bJump2 > -1)
                    {
                        sValue = Regex.Replace(sValue, "\\n", " ");
                    }
                }
                else
                {
                    //déplacement de cette partie le 18 mai. AUcune certitude
                    if (sValue.IndexOf('\'') > -1)
                    {
                        sValue = sValue.Replace("''", "'");
                        sValue = sValue.Replace("'", "''");
                    }
                    //-------------------------------------------------------
                }

                //sValue = sValue.Replace("\\'", "'");
                if (sConn.SConnDriver == SQLTools_Enums.BDD.DB_MYSQL)
                {
                    if (sValue.EndsWith("\\"))
                    {
                        sValue = string.Concat(sValue, "'");
                    }

                    if (sValue.IndexOf("\\''") > -1)
                    {
                        sValue = sValue.Replace("\\''", "\\'");
                    }
                }

                if (!bRemoveSpecialCharsWhenWritingInFile) // a nouveau, on ne met pas les caractères d'échappement dans un fichier CSV
                {
                    sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, sValue, SQL_INSERT_ECHAP_VALUE);
                }
            }
            else
            {
                if (bNullIfEmpty)
                {
                    if (!bRemoveSpecialCharsWhenWritingInFile) // on n'écrit pas du NULL dans un fichier CSV
                    {
                        sValue = "NULL";
                    }
                }
                else
                {
                    switch (sConn.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            sValue = string.Concat(SQL_INSERT_ECHAP_VALUE, SQL_INSERT_ECHAP_VALUE);
                            break;
                    }
                }
            }

            return sValue;

        }

        public static string SetCleanBit(string sValue, SQLTools_Enums.BDD WhichBDD, bool bUseNullWhenBlankValue_OnInsert)
        {
            string sValueFinal = "";
            //sValue = sValue.ToLower().Trim();

            switch (WhichBDD)
            {
                case SQLTools_Enums.BDD.DB_MYSQL:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "1";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "0";
                    }
                    break;
                case SQLTools_Enums.BDD.DB_POSTGRE:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "B'1'";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "B'0'";
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ORACLE:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "1";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "0";
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLSERVER:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "1";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "0";
                    }
                    break;
                case SQLTools_Enums.BDD.DB_SQLITE:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "1";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "0";
                    }
                    break;
                case SQLTools_Enums.BDD.DB_ODBC:
                    if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "1";
                    }
                    else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
                    {
                        sValueFinal = "0";
                    }
                    break;
            }

            if (sValueFinal.Length == 0)
            {
                if (bUseNullWhenBlankValue_OnInsert) // on n'écrit pas du NULL dans un fichier CSV
                {
                    sValueFinal = "NULL";
                }
            }

            return sValueFinal;
        }

        public static string SetCleanVarbinary(object oValue, string sColName, SQLTools_Enums.BDD WhichBDD, bool bUseNullWhenBlankValue_OnInsert)
        {
            string s = Encoding.UTF8.GetString((System.Byte[])oValue);
            try
            {
                if (s.Length > 0)
                {
                    switch (WhichBDD)
                    {
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            s = BitConverter.ToString((System.Byte[])oValue);
                            s = "0x" + s.Replace("-", string.Empty);
                            //s = string.Concat("CAST('", s, "' AS VARBINARY(MAX))");
                            //s = "@" + sColName;
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            s = BitConverter.ToString((System.Byte[])oValue);
                            s = "0x" + s.Replace("-", string.Empty);
                            //s = string.Concat("CAST('", s, "' AS VARBINARY(MAX))");
                            //s = "@" + sColName;
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            s = BitConverter.ToString((System.Byte[])oValue);
                            s = s.Replace("-", string.Empty);
                            s = "decode('" + s + "', 'hex')";
                            //s = string.Concat("CAST('", s, "' AS BYTEA)");
                            //s = "@" + sColName;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            s = BitConverter.ToString((System.Byte[])oValue);
                            s = "0x" + s.Replace("-", string.Empty);
                            //s = string.Concat("CAST('", s, "' AS VARBINARY(MAX))");
                            //s = "@" + sColName;
                            break;
                        default:
                            s = string.Concat("'", s, "'");
                            break;
                    }
                }
                else { s = bUseNullWhenBlankValue_OnInsert ? "NULL" : ""; }
            }
            catch { }
            return s;
        }

        public static string SetCleanJsonPattern(string sValue)
        {
            MatchCollection mc = Regex.Matches(sValue, SHSRegex.REGEX_JSON_PATTERN); //nettoyage en cas de mauvaise saisie utilisateur
            if (mc.Count > 0)
            {
                foreach (Match m in mc)
                {
                    string s = m.Value;
                    s = s.Replace("{", "{\"");
                    s = s.Replace(",", "\",\"");
                    s = s.Replace("}", "\"}");
                    s = s.Replace(":", "\":\"");
                    sValue = sValue.Replace(m.Value, s);
                }
            }

            return sValue;
        }

        public static string SetCleanBoolean(string sValue, bool bIsBit, bool bIsSQLServer)
        {
            string sValueFinal = "";
            //sValue = sValue.ToLower().Trim();

            if (sValue.Equals("true", StringComparison.OrdinalIgnoreCase) | sValue.Equals("1") | sValue.Equals("oui", StringComparison.OrdinalIgnoreCase))
            {
                sValueFinal = bIsBit ? "1" : bIsSQLServer ? "1" : "TRUE";
            } //en mode fichier, on copie les données telles qu'elles existent dans la source
            else if (sValue.Equals("false", StringComparison.OrdinalIgnoreCase) | sValue.Equals("0") | sValue.Equals("non", StringComparison.OrdinalIgnoreCase))
            {
                sValueFinal = bIsBit ? "0" : bIsSQLServer ? "0" : "FALSE";
            } //en mode fichier, on copie les données telles qu'elles existent dans la source

            return sValueFinal;
        }

        private static string SetCleanBitArray(string sValueRetour)
        {
            return sValueRetour;
        }

        public static System.Byte[] GetBytesFromBinaryString(string binary)
        {
            var list = new List<byte>();

            for (int i = 0; i < binary.Length; i += 8)
            {
                string t = binary.Substring(i, 8);

                list.Add(Convert.ToByte(t, 2));
            }

            return list.ToArray();
        }

        public static string GetIntPartOfADouble(string sValue)
        {
            string sIntPart = "";

            try
            {
                if (sValue.IndexOf(Convert.ToChar(",")) != -1)
                {
                    sIntPart = sValue.Split(Convert.ToChar(","))[0];
                }
                else if (sValue.IndexOf(Convert.ToChar(".")) != -1)
                {
                    sIntPart = sValue.Split(Convert.ToChar("."))[0];
                }
                else
                {
                    sIntPart = sValue;
                }
            }
            catch (Exception ex)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG, true);
            }

            return sIntPart;
        }

        public static string GetDecimalPartOfADouble(string sValue)
        {
            string sDecimalPart = "0";
            //double fNumber = double.Parse(sValue);
            //Int64 iDecimalPart = 0;

            //iDecimalPart = Convert.ToInt64(fNumber - Math.Truncate(fNumber));
            //return iDecimalPart;

            try
            {
                if (sValue.IndexOf(Convert.ToChar(",")) != -1)
                {
                    sDecimalPart = sValue.Split(Convert.ToChar(","))[1];
                }
                else if (sValue.IndexOf(Convert.ToChar(".")) != -1)
                {
                    sDecimalPart = sValue.Split(Convert.ToChar("."))[1];
                }
            }
            catch (Exception ex)
            {
                LogTools.StaticMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, ex.Message, SQLTools_Enums.LOG_TYPEINFO.WNG, true);
            }

            return sDecimalPart;
        }

        public static string CleanTableName(string sTableName)
        {

            string sNomTableFinal;

            try
            {
                sNomTableFinal = sTableName.Split(Convert.ToChar("^"))[0];
                if (sNomTableFinal.Length == 0)
                {
                    sNomTableFinal = sTableName;
                }
            }
            catch (Exception)
            {
                sNomTableFinal = sTableName;
            }

            //if (!Regex.IsMatch(sNomTableFinal, "[A-Z]"))
            //{
            //    sNomTableFinal = Regex.Replace(sNomTableFinal, "[0-9]", "X");
            //}

            return sNomTableFinal;

        }

        public static List<Query.QField> MergeFieldsFromCrossQueries(Query FuzibleQuery)
        {
            List<Query.QField> ListFieldsInQuery = new();

            foreach (Query.QField qF in FuzibleQuery.QueryAnalyzer.Fields) { ListFieldsInQuery.Add(qF); }

            foreach (Query qCJ in FuzibleQuery.CrossJoinQueries)
            {
                foreach (Query.QField qF in qCJ.QueryAnalyzer.Fields)
                {
                    if (!ListFieldsInQuery.Any(q => q.Alias.Equals(qF.Alias, StringComparison.InvariantCultureIgnoreCase))) { ListFieldsInQuery.Add(qF); }
                }
            }

            return ListFieldsInQuery;
        }

        internal static string ColumnToAlphabet(int iColumns)
        {
            int dividend = iColumns;
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

        public static DataTable FilterDataTableWithPseudoQuery(DataTable dtTable, Query FuzibleQuery, Job JobParameters, ref LogTools MyLog)
        {
            try
            {
                //2 cas : soit on a remplacé le SELECT * par les noms contenus dans le datatable, soit on prend la requête d'origine
                StringBuilder sbWhere = new();
                int iWhere = 0;

                if (FuzibleQuery.QueryAnalyzer.Where.Count > 0)
                {
                    List<int> iFieldsToSHS = new();
                    bool bMustSHS = false;
                    string sRegexNum = ".{1}[<>]{1}.{1}"; //on cherche les nombres comparés (impossible de comparer des sup. ou inf. avec des string
                                                          //vraiment, on ne charge le SHSAnalyzzer que si strictement nécessaire (très couteux)
                    foreach (Query.QWhere qW in FuzibleQuery.QueryAnalyzer.Where)
                    {
                        if (Regex.Matches(qW.Where, sRegexNum).Count > 0)
                        {
                            foreach (DataColumn dc in dtTable.Columns)
                            {
                                if (Regex.Matches(qW.WhereCompare, "[^A-z0-9]" + dc.ColumnName + "[^A-z0-9]", RegexOptions.IgnoreCase).Count > 0)
                                {
                                    bMustSHS = true;
                                    iFieldsToSHS.Add(dc.Ordinal);
                                }
                                if (Regex.Matches(qW.RawWhereField, "[^A-z0-9]" + dc.ColumnName + "[^A-z0-9]", RegexOptions.IgnoreCase).Count > 0)
                                {
                                    bMustSHS = true;
                                    iFieldsToSHS.Add(dc.Ordinal);
                                }
                                if (qW.RawWhereField.Equals(dc.ColumnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    bMustSHS = true;
                                    iFieldsToSHS.Add(dc.Ordinal);
                                }
                                if (qW.WhereCompare.Equals(dc.ColumnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    bMustSHS = true;
                                    iFieldsToSHS.Add(dc.Ordinal);
                                }
                            }
                        }
                    }

                    if (bMustSHS)
                    {
                        foreach (int iF in iFieldsToSHS)
                        {
                            //obliger d'analyser les types à cause des possibles conditions sur nombres entiers
                            //rappel : les datatables issues d'autres sources que SQL sont généralement uniquement avec des colonnes STRING
                            SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                            List<SQLColumn> sColumns = SHS.GetListFieldsTypesFromDataset(dtTable, iF, FuzibleQuery, SQLTools_Enums.CLASS_PURPOSE.SRC);
                        }
                    }

                }

                foreach (Query.QWhere qW in FuzibleQuery.QueryAnalyzer.Where)
                {
                    iWhere++;
                    if (iWhere > 1) { sbWhere.Append(" AND "); }

                    string sWhere = qW.Where;
                    if (qW.IsSubQuery)
                    {
                        string sQ = qW.RawWhereField;
                        Query q = new(JobParameters, sQ);
                        MThread.GetSourceData(0, JobParameters, ref q, MyLog, false);
                        List<DataSet> dsData = MThread.GetSourceData(0, JobParameters, ref q, MyLog, false);
                        if (dsData.Count > 0 && dsData[0].Tables.Count > 0)
                        {
                            List<string> sListParams = SQLTools.BuildListFromDs(dsData[0]);
                            sWhere = sWhere.Replace(sQ, string.Concat(" (", string.Join(",", sListParams), ") "));
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_tool_filterquery_kosubq + sWhere, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }
                    if (qW.IsSubQueryComparedTo)
                    {
                        string sQ = qW.WhereCompare;
                        Query q = new(JobParameters, sQ);
                        MThread.GetSourceData(0, JobParameters, ref q, MyLog, false);
                        List<DataSet> dsData = MThread.GetSourceData(0, JobParameters, ref q, MyLog, false);
                        if (dsData.Count > 0 && dsData[0].Tables.Count > 0)
                        {
                            List<string> sListParams = SQLTools.BuildListFromDs(dsData[0]);
                            sWhere = sWhere.Replace(sQ, string.Concat(" (", string.Join(",", sListParams), ") "));
                        }
                        else
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_tool_filterquery_kosubq + sWhere, FuzibleQuery.RetryErrorOrWarning);
                            FuzibleQuery.QueryErrors += 1;
                        }
                    }

                    //on sait qu'avec 1 table, on a aucun alias dans le nom des colonnes
                    if (FuzibleQuery.QueryAnalyzer.Tables.Count == 1)
                    {
                        Query.QTable qT = FuzibleQuery.QueryAnalyzer.Tables[0];

                        if (sWhere.IndexOf(qT.Alias + ".") > -1)
                        {
                            sWhere = sWhere.Replace(qT.Alias + ".", "");
                        }
                        if (sWhere.IndexOf(qT.Name + ".") > -1)
                        {
                            sWhere = sWhere.Replace(qT.Name + ".", "");
                        }
                    }

                    if (qW.FieldAlias.Length > 0) //parfois avec un where len(li_test) > 1, on ne connait pas l'alias si il n'est pas dans le select
                    {
                        foreach (string sCol in JobParameters.DISALLOWED_SQL_COLUMNS)
                        {
                            if (sCol.IndexOf(qW.FieldAlias) > -1) // on a tenté de filtrer sur une colonne optionnelle
                            {
                                sWhere = "1 = 1";
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_tool_filterquery_kocol + qW.FieldAlias, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                break;
                            }
                        }
                        //bricolage : filtrage de la requête sur les colonnes paramétrées mais qui ne sont pas encore dans la datatable
                        string[] sMultiDynamic = JobParameters.OptionalDynamicParamField_OnInsert.Split(Convert.ToChar(";"));
                        foreach (string sCol in sMultiDynamic)
                        {
                            if (sCol.Split(Convert.ToChar("="))[0].IndexOf(qW.FieldAlias) > -1)
                            {
                                sWhere = "1 = 1";
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, Languages.Languages.shs_tool_filterquery_kocol + qW.FieldAlias, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                break;
                            }
                        }
                    }

                    sWhere = ConvertSQLConditionsForDotNet(sWhere);
                    sbWhere.Append(sWhere);
                }

                string sWhereCondition = sbWhere.ToString();

                //contrôle d'encadrement des champs : (si on a WHERE "myfield" > 1)
                foreach (DataColumn dc in dtTable.Columns)
                {
                    if (sWhereCondition.IndexOf(string.Concat("\"", dc.ColumnName, "\"")) > -1)
                    {
                        sWhereCondition = sWhereCondition.Replace(string.Concat("\"", dc.ColumnName, "\""), string.Concat("[", dc.ColumnName, "]"));
                    }
                }

                //string sWhereCondition = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetWhere(true);
                //if (sWhereCondition.StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase)) { sWhereCondition = sWhereCondition.Substring(5).Trim(); }

                DataTable dtFiltered;
                List<string> sListNamespaces = new();
                string sTblName = dtTable.TableName;
                string sNamespace = dtTable.Namespace;
                foreach (DataColumn dc in dtTable.Columns)
                {
                    if (dc.Namespace.Length == 0)
                    {
                        dc.Namespace = sNamespace;
                        if (!sListNamespaces.Contains(sNamespace)) { sListNamespaces.Add(sNamespace); }
                    }
                    else { sListNamespaces.Add(dc.Namespace); }
                }

                if (sWhereCondition.Length > 0)
                {
                    //Convert(DATUM, 'System.DateTime')
                    try
                    {
                        var dr = dtTable.Select(sWhereCondition);
                        if (dr.Length > 0)
                        {
                            dtFiltered = dr.CopyToDataTable();
                        }
                        else { dtFiltered = dtTable.Clone(); } //si il n'y a pas de lignes, on copie juste le schéma
                    }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, ex, Languages.Languages.shs_tool_filterquery_ko, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                        dtFiltered = dtTable.Clone();
                    }
                }
                else
                {
                    var dr = dtTable.Select(sWhereCondition);
                    if (dr.Length > 0)
                    {
                        dtFiltered = dr.CopyToDataTable();
                    }
                    else { dtFiltered = dtTable.Clone(); } //si il n'y a pas de lignes, on copie juste le schéma
                }

                dtFiltered.Namespace = sNamespace;
                dtFiltered.TableName = sTblName;
                if (dtTable.PrimaryKey != null)
                {
                    DataColumn[] pk = new DataColumn[dtTable.PrimaryKey.Length];
                    for (int iP = 0; iP < dtTable.PrimaryKey.Length; iP++)
                    { pk[iP] = dtFiltered.Columns[dtTable.PrimaryKey[iP].ColumnName]; }
                    dtFiltered.PrimaryKey = pk;
                }

                for (int i = 0; i < sListNamespaces.Count; i++) { dtFiltered.Columns[i].Namespace = sListNamespaces[i]; }

                return dtFiltered;
            }
            catch (OperationCanceledException)
            { throw; }
            catch (Exception)
            { throw; }
        }

        private static string ConvertSQLConditionsForDotNet(string sWhere)
        {
            sWhere = Regex.Replace(sWhere, "(\\s*)(LENGTH\\s*\\()", "$1LEN(", RegexOptions.IgnoreCase);

            if (Regex.Match(sWhere, "<>\\s*['\"]\\s*['\"]").Success)
            {
                sWhere = string.Concat("(", sWhere, " AND ", Regex.Replace(sWhere, "<>\\s*['\"]\\s*['\"]", "IS NOT NULL"), ")");
            }
            else if (Regex.Match(sWhere, "!=\\s*['\"]\\s*['\"]").Success)
            {
                sWhere = string.Concat("(", sWhere, " AND ", Regex.Replace(sWhere, "!=\\s*['\"]\\s*['\"]", "IS NOT NULL"), ")");
            }
            else if (Regex.Match(sWhere, "=\\s*['\"]\\s*['\"]").Success)
            {
                sWhere = string.Concat("(", sWhere, " AND ", Regex.Replace(sWhere, "=\\s*['\"]\\s*['\"]", "IS NULL"), ")");
            }

            sWhere = sWhere.Replace("!=", "<>");


            return sWhere;
        }

        public static void AddOptionalColumnsInDatasetFromINI(DataTable dT, Job INIP, Query Q, ref LogTools MyLog)
        {
            Int64 iRownumStart = 0;

            //comptons !
            if (INIP.OptionalRowID_OnInsert && INIP.OptionalRowID_OnInsert_ForceRecount)
            {
                switch (Q.ConnectionTrg.SConnDriverSuffix)
                {
                    case "DB":
                        SQLTools sql = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                        iRownumStart = sql.CountRowsInTargetTable(Q.OutputTable, Q);
                        break;
                    case "NS":
                        NOSQLTools noql = new(INIP, SQLTools_Enums.CLASS_PURPOSE.TRG, ref MyLog);
                        iRownumStart = noql.GetCollectionCount(Q.OutputTable);
                        break;
                    default:
                        break;
                }
            }

            if (dT.Columns.IndexOf("IDX_COL") != -1 && !INIP.GlobalParameters.RESERVED_SQL_COLUMNS.Contains("IDX_COL"))
            {
                INIP.GlobalParameters.AddReservedColumn("IDX_COL");
            } //Rappel : IDX_COL correspond à la colonne de compte des fichiers PIVOT hyperfile. On l'ajoute dans la liste des colonnes réservées si elle est dans la source

            if (INIP.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING && INIP.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1)
            {
                if (dT.Columns.IndexOf("SYNCHRO_TAG") == -1)
                {
                    DataColumn dc = new()
                    {
                        ColumnName = "SYNCHRO_TAG",
                        DataType = System.Type.GetType("System.String"),
                        ColumnMapping = MappingType.Hidden
                    };
                    dT.Columns.Add(dc);
                    foreach (DataRow dr in dT.Rows)
                    {
                        try
                        {
                            dr["SYNCHRO_TAG"] = "I";
                        }
                        catch { }
                    }
                }
                INIP.GlobalParameters.AddReservedColumn("SYNCHRO_TAG"); //ajout de la colonne crée dans la liste des colonnes SQL réservées
            }

            if (INIP.OptionalDBName_OnInsert)
            {
                if (dT.Columns.IndexOf(INIP.TargetAddDbName) == -1)
                {
                    DataColumn dc = new()
                    {
                        ColumnName = INIP.TargetAddDbName,
                        DataType = System.Type.GetType("System.String"),
                        ColumnMapping = MappingType.Hidden
                    };
                    dT.Columns.Add(dc);

                    string sDbname = GetDbName(INIP, Q, dT);

                    foreach (DataRow dr in dT.Rows)
                    {
                        try
                        {
                            dr[INIP.TargetAddDbName] = sDbname;
                        }
                        catch { }
                    }
                }
                INIP.GlobalParameters.AddReservedColumn(INIP.TargetAddDbName); //ajout de la colonne crée dans la liste des colonnes SQL réservées
            }
            if (INIP.OptionalRowID_OnInsert)
            {
                if (dT.Columns.IndexOf(INIP.TargetAddRowNum) == -1)
                {
                    DataColumn dc = new()
                    {
                        ColumnName = INIP.TargetAddRowNum,
                        DataType = System.Type.GetType("System.Int32"),
                        ColumnMapping = MappingType.Hidden
                    };
                    dT.Columns.Add(dc);

                    for (int cptR = 0; cptR < dT.Rows.Count; cptR++)
                    {
                        try
                        {
                            dT.Rows[cptR][INIP.TargetAddRowNum] = iRownumStart + cptR + 1;
                        }
                        catch { }
                    }
                }
                INIP.GlobalParameters.AddReservedColumn(INIP.TargetAddRowNum); //ajout de la colonne crée dans la liste des colonnes SQL réservées
            }
            if (INIP.OptionalTimestamp_OnInsert)
            {
                if (dT.Columns.IndexOf(INIP.TargetAddDtLoad) == -1)
                {
                    DataColumn dc = new()
                    {
                        ColumnName = INIP.TargetAddDtLoad,
                        DataType = System.Type.GetType("System.DateTime"),
                        ColumnMapping = MappingType.Hidden
                    };
                    dT.Columns.Add(dc);
                    for (int cptR = 0; cptR < dT.Rows.Count; cptR++)
                    {
                        try
                        {
                            dT.Rows[cptR][INIP.TargetAddDtLoad] = DateTime.Now.ToString();
                        }
                        catch { }
                    }
                }
                INIP.GlobalParameters.AddReservedColumn(INIP.TargetAddDtLoad); //ajout de la colonne crée dans la liste des colonnes SQL réservées
            }
            if (INIP.OptionalDynamicParamField_OnInsert.Length > 0)
            {
                string[] sMultiDynamic = INIP.OptionalDynamicParamField_OnInsert.Split(Convert.ToChar(";"));

                int iQteDyn = 0;
                foreach (string sCol in sMultiDynamic)
                {
                    iQteDyn++;
                    string sDynamicParam = sCol;
                    string sColName = string.Concat("DYNPARAM", "_", iQteDyn.ToString("00"));
                    if (sDynamicParam.IndexOf("=") > -1)
                    {
                        sColName = sDynamicParam.Split(Convert.ToChar("="))[0];
                        sDynamicParam = sDynamicParam.Split(Convert.ToChar("="))[1];
                    }
                    if (!sDynamicParam.Contains('{', StringComparison.CurrentCulture))
                    {
                        sDynamicParam = string.Concat("{", sDynamicParam, "}");
                    }

                    sDynamicParam = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sDynamicParam, INIP.DynParams);

                    INIP.GlobalParameters.AddReservedColumn(sColName); //ajout de la colonne crée dans la liste des colonnes SQL réservées

                    if (dT.Columns.IndexOf(sColName) == -1)
                    {
                        DataColumn dc = new()
                        {
                            ColumnName = sColName,
                            DataType = System.Type.GetType("System.String"),
                            ColumnMapping = MappingType.Hidden
                        };
                        dT.Columns.Add(dc);
                        foreach (DataRow dr in dT.Rows)
                        {
                            try
                            {
                                dr[sColName] = sDynamicParam;
                            }
                            catch { }
                        }
                    }
                }
            }

            List<string> sReserved = INIP.GlobalParameters.RESERVED_SQL_COLUMNS;
            for (int iC = 0; iC < sReserved.Count; iC++)
            {
                if (!Q.QueryAnalyzer.Fields.Any(q => q.Name.Equals(sReserved[iC], StringComparison.OrdinalIgnoreCase)))
                {
                    //dernier index : 
                    int iMaxIndex = Q.QueryAnalyzer.Fields.Max(f => f.Index);
                    Q.QueryAnalyzer.Fields.Add(new Query.QField(sReserved[iC], sReserved[iC], "SHS", "SHS", "", iMaxIndex + 1));
                }
            }

            var sCols = INIP.GlobalParameters.RESERVED_SQL_COLUMNS.Distinct().ToList();
            INIP.GlobalParameters.SetReservedColumns(sCols);
        }

        public static string GetDbName(Job INIP, Query Q, DataTable dt)
        {
            return Q.ConnectionSrc.SConnDriverSuffix switch
            {
                "DB" => INIP.DatabaseName_Source,
                "FI" => Q.QueryAnalyzer.Tables[0].Name,
                "MB" => Q.QueryAnalyzer.Tables[0].Name,
                "AD" => Q.QueryAnalyzer.Tables[0].Name,
                "WS" => Q.QueryAnalyzer.Tables[0].Name,
                _ => dt.TableName,
            };
        }

        public static string InitDsDtNames(DataSet dsSource, Job INIP, Query SQ)
        {
            string sNamespaceDS = "";
            string sNamespaceDT;

            switch (INIP.ConnectionString_Source.SConnDriver.ToString()[..2])
            {
                case "DB":
                    //sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source.Replace("\\", "_"), INIP.DynParams);
                    sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    break;
                case "FI":
                    sNamespaceDS = SQ.QueryAnalyzer.Tables[0].Name; //.Replace("\\", "_");
                    break;
                case "WS":
                    //sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source.Replace("\\", "_"), INIP.DynParams);
                    sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    break;
                case "MB":
                    //sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source.Replace("\\", "_"), INIP.DynParams);
                    sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    break;
                case "AD":
                    //sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source.Replace("\\", "_"), INIP.DynParams);
                    sNamespaceDS = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.DatabaseName_Source, INIP.DynParams);
                    break;
            }

            //nommage dans le dataset pour les utilisations ultérieures (DBNAME entre autres)
            sNamespaceDT = SQ.QueryAnalyzer.Tables[0].Alias; //.Replace("\\", "_");
            dsSource.Tables[0].Namespace = sNamespaceDT.Length > 0 ? sNamespaceDT.Split(Convert.ToChar("."))[0] : dsSource.Tables[0].Namespace; //namespace est utilisé pour entre autres pour la balise générale XML
            dsSource.Tables[0].TableName = sNamespaceDS.Length > 0 ? sNamespaceDS.Split(Convert.ToChar("."))[0] : dsSource.Tables[0].TableName;
            //dsSource.Namespace = sNamespaceDS.Split(Convert.ToChar("."))[0];
            //dsSource.DataSetName = SQ.OutputTable;

            for (int i = 0; i < dsSource.Tables.Count; i++)
            {
                if (i > 0 && dsSource.Tables[i].TableName.Equals(dsSource.Tables[0].TableName))
                {
                    dsSource.Tables[i].TableName = string.Concat(dsSource.Tables[i].TableName, "_", i.ToString("00"));
                }
            }

            return sNamespaceDS;
        }

        public static string RemoveSpecialCharacters(string str, string sReplacementChar, bool bSQL)
        {
            string sReturn = str;
            sReturn = RemoveDiacritics(sReturn);

            if (bSQL)
            {
                //Regex.Replace(dtC.ColumnName, @"[^\w\d]", "_")
                sReturn = Regex.Replace(sReturn, "[^a-zA-Z0-9]+", sReplacementChar, RegexOptions.Compiled);
            }

            return sReturn;
        }

        public static string RemoveDiacritics(string text)
        {

            try
            {
                if (text.Length > 0)
                {
                    string normalizedString = text.Normalize(NormalizationForm.FormD);
                    var stringBuilder = new StringBuilder();

                    foreach (char c in normalizedString)
                    {
                        UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                        if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                        {
                            stringBuilder.Append(c);
                        }
                    }

                    return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
                }
                else return "";
            }
            catch { return text; }
        }

        public static void RemoveAt<T>(ref T[] arr, int index)
        {
            for (int a = index; a < arr.Length - 1; a++)
            {
                // moving elements downwards, to fill the gap at [index]
                arr[a] = arr[a + 1];
            }
            // finally, let's decrement Array's size by one
            Array.Resize(ref arr, arr.Length - 1);
        }

        public static int GetDistinctRecords(DataTable dt, SQLColumn[] sListColumnName, LogTools MyLog, int iNumThread)
        {
            string sColumnCombination = "";
            foreach (var sC in sListColumnName)
            { sColumnCombination = string.Concat(sColumnCombination, sC.ColumnName, ","); }
            sColumnCombination = sColumnCombination[..^1];

            //List<string> sLC = new List<string>();
            string sColA;
            //string sColB = "";
            int iStep;
            int iDistinct = dt.Rows.Count;

            iStep = (int)Math.Floor(iDistinct / 1000000.0); // ???

            if (iStep == 0)
            {
                iStep = 1;
            }

            HashSet<string> sLC = new();

            //var items = list.ToDictionary(i => i.identifier, i => i);

            //on construit la liste des valeurs combinées
            for (int cptRa = 0; cptRa < dt.Rows.Count; cptRa += iStep)
            {
                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                sColA = "";
                foreach (SQLColumn sC in sListColumnName)
                {
                    sColA = string.Concat(sColA, dt.Rows[cptRa][sC.ColumnIndex].ToString());
                }

                if (sLC.Contains(sColA))
                {
                    iDistinct -= 1;
                    break;
                }

                sLC.Add(sColA);

                if (cptRa % 1000 == 0)
                {
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.SRC, null, string.Concat("T", (iNumThread + 1).ToString("00"), " ", Languages.Languages.mt_primarykeycombination_percent, sColumnCombination, " -> ", dt.TableName, " ", cptRa.ToString(), "/", dt.Rows.Count.ToString() + Toolbox.GetPercent(0, dt.Rows.Count, cptRa)), SQLTools_Enums.LOG_TYPEINFO.DET);
                }

            }

            //Analyse MT des résultats
            //if (sLC.Count > 0)
            //{
            //    for (int cptRb = 0; cptRb < dt.Rows.Count; cptRb++)
            //    {
            //        sColB = "";
            //        foreach (string sC in sListColumnName)
            //        {
            //            sColB += dt.Rows[cptRb][sC].ToString();
            //        }

            //        MultiThreadedOperations mtClass = new MultiThreadedOperations(null, sLC.Count);
            //        for (int numThread = 0; numThread <= mtClass.QuantityOfThreadsToCompute - 1; numThread += 1)
            //        {
            //            mtClass.INIT_FindPrimaryKey(sLC, sColB);
            //            Thread th = new Thread(mtClass.FindPrimaryKey);
            //            th.Start(numThread);
            //        }

            //        while (!mtClass.AreAllThreadsFinished)
            //        {
            //            Thread.Sleep(1);
            //        }

            //        if (!mtClass.IsValueDistinctForCheckPK)
            //        {
            //            iDistinct -= 1;
            //            break;
            //        }
            //    }
            //}
            /////////////

            return iDistinct;

        }

        public static List<SQLColumn[]> GetColumnsCombinationsFromDataset(DataTable dtTable, int iQteColumnsPossible)
        {
            List<SQLColumn[]> sListCombinations = new();

            DataTable dtTemp = dtTable.Copy();

            int iQteColumnsTable = dtTemp.Columns.Count;

            //Trop couteux de faire l'analyse sur plus de 3 colonnes pour le moment
            if (iQteColumnsPossible == 2)
            {
                //On détermine les combinaisons possibles
                //exemple pour 4 colonnes : 1-2 / 1-3 / 1-4 / 2-3 / 2-4 / 3-4

                for (int cptColA = 0; cptColA <= iQteColumnsTable - iQteColumnsPossible; cptColA += 1)
                {
                    for (int cptColB = cptColA; cptColB < iQteColumnsTable - 1; cptColB += 1)
                    {
                        if (!dtTemp.Columns[cptColA].AllowDBNull && !dtTemp.Columns[cptColB + 1].AllowDBNull)
                        {
                            sListCombinations.Add(new SQLColumn[] { SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColA], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColB + 1], SQLTools_Enums.TYPE_DATA.VARCHAR) });
                        }
                    }
                }
            }

            if (iQteColumnsPossible == 3)
            {
                //On détermine les combinaisons possibles
                //exemple pour 4 colonnes : 1-2-3 / 1-2-4 / 1-3-4 / 2-3-4 / 
                for (int cptColA = 0; cptColA <= iQteColumnsTable - iQteColumnsPossible; cptColA += 1)
                {
                    for (int cptColB = cptColA; cptColB < iQteColumnsTable - 1; cptColB += 1)
                    {
                        for (int cptColC = cptColB; cptColC < iQteColumnsTable - 2; cptColC += 1)
                        {
                            if (!dtTemp.Columns[cptColA].AllowDBNull && !dtTemp.Columns[cptColB + 1].AllowDBNull && !dtTemp.Columns[cptColC + 2].AllowDBNull)
                            {
                                sListCombinations.Add(new SQLColumn[] { SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColA], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColB + 1], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColC + 2], SQLTools_Enums.TYPE_DATA.VARCHAR) });
                            }
                        }
                    }
                }
            }

            if (iQteColumnsPossible == 4)
            {
                //On détermine les combinaisons possibles
                //exemple pour 5 colonnes : 1-2-3-4 / 1-2-4-5 / 1-3-4-5
                for (int cptColA = 0; cptColA <= iQteColumnsTable - iQteColumnsPossible; cptColA += 1)
                {
                    for (int cptColB = cptColA; cptColB < iQteColumnsTable - 1; cptColB += 1)
                    {
                        for (int cptColC = cptColB; cptColC < iQteColumnsTable - 2; cptColC += 1)
                        {
                            for (int cptColD = cptColC; cptColD < iQteColumnsTable - 3; cptColD += 1)
                            {
                                if (!dtTemp.Columns[cptColA].AllowDBNull && !dtTemp.Columns[cptColB + 1].AllowDBNull && !dtTemp.Columns[cptColC + 2].AllowDBNull && !dtTemp.Columns[cptColD + 3].AllowDBNull)
                                {
                                    sListCombinations.Add(new SQLColumn[] { SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColA], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColB + 1], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColC + 2], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColD + 3], SQLTools_Enums.TYPE_DATA.VARCHAR) });
                                }
                            }
                        }
                    }
                }
            }

            if (iQteColumnsPossible == 5)
            {
                //On détermine les combinaisons possibles
                //exemple pour 5 colonnes : 1-2-3-4-5-6
                for (int cptColA = 0; cptColA <= iQteColumnsTable - iQteColumnsPossible; cptColA += 1)
                {
                    for (int cptColB = cptColA; cptColB < iQteColumnsTable - 1; cptColB += 1)
                    {
                        for (int cptColC = cptColB; cptColC < iQteColumnsTable - 2; cptColC += 1)
                        {
                            for (int cptColD = cptColC; cptColD < iQteColumnsTable - 3; cptColD += 1)
                            {
                                for (int cptColE = cptColD; cptColE < iQteColumnsTable - 4; cptColE += 1)
                                {
                                    if (!dtTemp.Columns[cptColA].AllowDBNull && !dtTemp.Columns[cptColB + 1].AllowDBNull && !dtTemp.Columns[cptColC + 2].AllowDBNull && !dtTemp.Columns[cptColD + 3].AllowDBNull && !dtTemp.Columns[cptColE + 4].AllowDBNull)
                                    {
                                        sListCombinations.Add(new SQLColumn[] { SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColA], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColB + 1], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColC + 2], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColD + 3], SQLTools_Enums.TYPE_DATA.VARCHAR),
                                                                SQLColumn.SQLColumnFromDataColumn(dtTemp.Columns[cptColE + 4], SQLTools_Enums.TYPE_DATA.VARCHAR)});
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return sListCombinations;

        }

        public static List<string> GetColumnNamesFromSQLColumnObjects(List<SQLColumn> sListColumns)
        {
            List<string> sListColNames = new();

            foreach (SQLColumn sC in sListColumns)
            {
                sListColNames.Add(sC.ColumnName);
            }

            return sListColNames;
        }

        public static IEnumerable<IEnumerable<T>> GetPowerSet<T>(List<T> list)
        {
            return from m in Enumerable.Range(0, 1 << list.Count)
                   select
                       from i in Enumerable.Range(0, list.Count)
                       where (m & (1 << i)) != 0
                       select list[i];
        }

        public static string GetDatabaseNameFromConnectionString(string sC, SQLTools_Enums.BDD tdDriver)
        {

            string sDb = "";

            switch (tdDriver.ToString()[..2])
            {

                case "WS":
                    string[] sParamSplitA = { "URL=", "url=", "Url=" };
                    string[] CSsplitA = sC.Split(Convert.ToChar(";")); //.Split(Convert.ToChar(";"));

                    foreach (string s in CSsplitA)
                    {
                        if (s.ToUpper().StartsWith("URL="))
                        {
                            try { sDb = s.Split(sParamSplitA, StringSplitOptions.None)[1].Trim(); }
                            catch (Exception)
                            {
                                sDb = "";
                            }

                        }
                    }
                    break;

                case "FI":
                    //string[] sParamSplitB = { "FTP=", "ftp=", "Sftp=", "SFTP=", "sftp=", "SFtp=" };

                    //string[] CSsplitB = sC.Split(Convert.ToChar(";")); //.Split(Convert.ToChar(";"));

                    //if (CSsplitB.Length > 1)
                    //{
                    //    string[] sDir;
                    //    sDir = sC.Split(Convert.ToChar("\\"));
                    //    if (sDir.Length > 0)
                    //    { sDb = sDir[sDir.Length - 1]; }
                    //    else { sDb = ""; }
                    //}
                    //else
                    //{
                    //    foreach (string s in CSsplitB)
                    //    {
                    //        if (s.ToUpper().StartsWith("FTP="))
                    //        {
                    //            try { sDb = s.Split(sParamSplitB, StringSplitOptions.None)[1].Trim(); }
                    //            catch (Exception)
                    //            { sDb = ""; }

                    //        }
                    //        else { sDb = ""; }
                    //    }
                    //}
                    break;

                case "DB":
                    string[] CSsplitC = sC.Split(Convert.ToChar(";"));

                    foreach (string s in CSsplitC)
                    {
                        if (s.StartsWith("DATABASE=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                var rg = Regex.Match(s, "(" + "DATABASE=" + ")" + "(.[^;]+)(;?)", RegexOptions.IgnoreCase);
                                sDb = rg.Groups[2].Value;
                                break;
                            }
                            catch (Exception)
                            {
                                sDb = "";
                            }
                        }
                        if (s.StartsWith("INITIAL CATALOG=", StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                var rg = Regex.Match(s, "(" + "INITIAL CATALOG=" + ")" + "(.[^;]+)(;?)", RegexOptions.IgnoreCase);
                                sDb = rg.Groups[2].Value;
                                break;
                            }
                            catch (Exception)
                            {
                                sDb = "";
                            }
                        }
                    }
                    break;

                case "NS":
                    if (sC.LastIndexOf("/") < sC.Length && sC.IndexOf("/") > 0 && sC.LastIndexOf("/") > 9)
                    {
                        sDb = sC[(sC.LastIndexOf("/") + 1)..];
                    }
                    break;

                case "MB":
                    string[] sParamSplitD = { "USERNAME=", "username=", "Username=", "UserName=" };

                    string[] CSsplitD = sC.Split(Convert.ToChar(";"));

                    foreach (string s in CSsplitD)
                    {
                        if (s.ToUpper().StartsWith("USERNAME="))
                        {
                            try { sDb = s.Split(sParamSplitD, StringSplitOptions.None)[1].Trim(); }
                            catch (Exception)
                            {
                                sDb = "";
                            }
                        }
                    }
                    break;

                case "AD":
                    string[] sParamSplitE = { "LDAP://", "ldap://", "Ldap://", "LDap://" };

                    string[] CSsplitE = sC.Split(Convert.ToChar(";"));

                    foreach (string s in CSsplitE)
                    {
                        if (s.ToUpper().StartsWith("LDAP///"))
                        {
                            try { sDb = s.Split(sParamSplitE, StringSplitOptions.None)[1].Trim(); }
                            catch (Exception)
                            {
                                sDb = "";
                            }
                        }
                    }
                    break;
            }

            return sDb;
        }

        public static string ReplaceDBNameInConnectionString(CONNString sConnexionString, string sReplacementDBName, List<string> sListDynParams)
        {
            string sCS = "";
            string sDb = string.Concat("Database=", sReplacementDBName);
            string sDb2 = string.Concat("Initial Catalog=", sReplacementDBName);

            string[] CSsplit = sConnexionString.SConnString(sListDynParams).Split(Convert.ToChar(";")); //.Split(Convert.ToChar(";"));

            foreach (string s in CSsplit)
            {
                string sParam = s;
                if (sParam.EndsWith(";")) { sParam = sParam[..^1]; }

                if (sParam.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                {
                    sCS += string.Concat(sDb, ";");
                }
                else if (sParam.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                {
                    sCS += string.Concat(sDb2, ";");
                }
                else
                {
                    sCS += string.Concat(sParam, ";");
                }
            }

            return sCS;
        }

        public static List<string> ExtractItemsFromStringBuilderQuery(SQLTools_Enums.TYPE_REQUETE eTypeQuery, string sQuery)
        {

            string[] sParamSplit = { "REJECTED QUERY " };
            List<string> sFinalQueries = new();

            switch (eTypeQuery)
            {
                case SQLTools_Enums.TYPE_REQUETE.MAKE_DELETE:
                    sParamSplit = new string[] { "DELETE " };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_INSERT:
                    sParamSplit = new string[] { "INSERT " };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_UPDATE:
                    sParamSplit = new string[] { "UPDATE " };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_SYSTEM_COMMAND:
                    sParamSplit = new string[] { "" };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_ALTER_COMMAND:
                    sParamSplit = new string[] { "" };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_CREATE:
                    sParamSplit = new string[] { "" };
                    break;
                case SQLTools_Enums.TYPE_REQUETE.MAKE_DROP:
                    sParamSplit = new string[] { "" };
                    break;
            }

            string[] sQueries = sQuery.Split(sParamSplit, StringSplitOptions.RemoveEmptyEntries);
            string sTmp;

            if (sQueries.Length > 0)
            {
                foreach (string s in sQueries)
                {
                    //sTmp = s.Replace(SQLData.PrepareSQLTransaction(true), "").Replace(SQLData.PrepareSQLTransaction(false), "");
                    sTmp = s;
                    if (sTmp.Trim().Length > 0) { sFinalQueries.Add(string.Concat(sParamSplit[0], sTmp)); }
                }
            }
            else
            {
                sFinalQueries.Add(sQuery.ToString());
            }

            return sFinalQueries;

        }

        public static List<string> FromStringToList(string sText)
        {
            List<string> sFinalList = new();
            string[] sListVars = sText.Trim().Split(Convert.ToChar(";"));
            if (sListVars.Length > 0)
            {
                foreach (string s in sListVars)
                {
                    if (!s.Equals("")) { sFinalList.Add(s); }
                }
            }

            return sFinalList;
        }

        internal static object ConvertValue(object oValue, Type sourceColumnType, SQLColumn targetColumn)
        {
            if (targetColumn.ColumnLinqType.Equals(sourceColumnType))
            {
                return oValue;
            }
            else
            {
                object oConvertedValue;

                switch (targetColumn.ColumnLinqType.ToString())
                {
                    case "System.Guid":
                        oConvertedValue = oValue is Guid guid ? guid : Guid.TryParse(oValue.ToString(), out guid) ? guid : null;
                        break;

                    case "System.Int64":
                        oConvertedValue = oValue is long int64 ? int64 : long.TryParse(oValue.ToString(), out int64) ? int64 : null;
                        break;

                    case "System.Int32":
                        oConvertedValue = oValue is int int32 ? int32 : int.TryParse(oValue.ToString(), out int32) ? int32 : null;
                        break;

                    case "System.Int16":
                        oConvertedValue = oValue is short int16 ? int16 : short.TryParse(oValue.ToString(), out int16) ? int16 : null;
                        break;

                    case "System.DateTime":
                        oConvertedValue = oValue is DateTime dateTime ? dateTime : DateTime.TryParse(oValue.ToString(), out dateTime) ? dateTime : null;
                        break;

                    case "System.DateTimeOffset":
                        oConvertedValue = oValue is DateTimeOffset dateTimeOffset ? dateTimeOffset : DateTimeOffset.TryParse(oValue.ToString(), out dateTimeOffset) ? dateTimeOffset : null;
                        break;

                    case "System.Boolean":
                        oConvertedValue = oValue is bool boolean ? boolean : bool.TryParse(oValue.ToString(), out boolean) ? boolean : null;
                        break;

                    case "System.Decimal":
                        oConvertedValue = oValue is decimal @decimal ? @decimal : decimal.TryParse(oValue.ToString(), out @decimal) ? @decimal : null;
                        break;

                    case "System.Double":
                        oConvertedValue = oValue is double @double ? @double : double.TryParse(oValue.ToString(), out @double) ? @double : null;
                        break;

                    case "System.Single":
                        oConvertedValue = oValue is float single ? single : float.TryParse(oValue.ToString(), out single) ? single : null;
                        break;

                    case "System.String":
                        oConvertedValue = oValue is string @string ? @string : oValue.ToString();
                        break;

                    case "System.Byte":
                        oConvertedValue = oValue is byte @byte ? @byte : byte.TryParse(oValue.ToString(), out @byte) ? @byte : null;
                        break;

                    case "System.Collections.BitArray":
                        oConvertedValue = oValue is BitArray bitArray ? bitArray : new BitArray(Convert.FromBase64String(oValue.ToString()));
                        break;

                    case "System.Byte[]":
                        oConvertedValue = oValue is byte[] byteArray ? byteArray : Convert.FromBase64String(oValue.ToString());
                        break;

                    default:
                        oConvertedValue = oValue.ToString();
                        break;
                }

                return oConvertedValue;
            }
        }

        public static string FastConvertData(string sValue, Job JobParameters, CONNString CS, SQLColumn sTargetColumn)
        {
            if (sValue.Length == 0) { return sValue; }

            string sValueRetour = sValue;

            sValueRetour = sTargetColumn.ColumnLinqType.ToString() switch
            {
                "System.Guid" => SetCleanString(sValue, true, "", false, CS),
                //"System.Int64" => string.Concat(SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                //"System.Int32" => string.Concat(SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                //"System.Int16" => string.Concat(SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                "System.DateTime" => SetCleanDate(sValueRetour, JobParameters, CS, false, false, (sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP || sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME) ? 2 : 1),
                "System.DateTimeOffset" => SetCleanDate(sValueRetour, JobParameters, CS, false, false, (sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP || sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME) ? 3 : 1),
                "System.Boolean" => SetCleanBoolean(sValueRetour, sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.BIT, CS.SConnDriver == SQLTools_Enums.BDD.DB_SQLSERVER || CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS),
                "System.Decimal" => string.Concat("{d}", SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                "System.Double" => string.Concat("{d}", SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                "System.Single" => string.Concat("{d}", SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0)),
                //"System.String" => SetCleanString(sValue, true, "", false, CS),
                "System.Byte" => SetCleanBoolean(sValueRetour, true, true),
                "System.Collections.BitArray" => SetCleanBitArray(sValueRetour),
                "System.Byte[]" => SetCleanBitArray(sValueRetour),
                //_ => SetCleanString(sValue, true, "", false, CS),
                _ => sValue,
            };

            if (sValueRetour.StartsWith("{d}"))
            {
                sValueRetour = sValueRetour[3..].Replace(".", ",");
                if (sValueRetour.IndexOf("E-") > -1)
                {
                    decimal.TryParse(sValueRetour, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d);
                    sValueRetour = d.ToString().Replace(".", ",");
                }
            }

            return sValueRetour;
        }

        public static string FastCheckType(string sValue, Job JobParameters, CONNString CS, SQLColumn sTargetColumn)
        {
            //string sValueRetour = SetCleanValueExport(sValue, false);
            string sValueRetour = sValue;

            //note ACCESS : ne supporte pas d'utiliser le caractère d'échappement quand il s'agit d'un nombre
            sValueRetour = sTargetColumn.ColumnLinqType.ToString() switch
            {
                "System.Guid" => SetCleanString(sValue, false, JobParameters.CSVCharSeparator_Target, JobParameters.UseNull_Target, CS),
                "System.Int64" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.Int32" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.Int16" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.DateTime" => SetCleanDate(sValueRetour, JobParameters, CS, JobParameters.UseNull_Target, true, (sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP || sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME) ? 2 : 1),
                "System.DateTimeOffset" => SetCleanDate(sValueRetour, JobParameters, CS, JobParameters.UseNull_Target, true, (sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP || sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME) ? 3 : 1),
                "System.Boolean" => SetCleanBoolean(sValueRetour, sTargetColumn.ColumnType == SQLTools_Enums.TYPE_DATA.BIT, CS.SConnDriver == SQLTools_Enums.BDD.DB_SQLSERVER || CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS),
                "System.Decimal" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.Double" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.Single" => string.Concat(CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE, SetCleanNumber(sValueRetour, CS, SQLTools_Enums.TYPE_DATA.UNKNOWN, JobParameters.UseNull_Target, JobParameters.GlobalParameters.ALLOW_INTEGERS_WITH_SPECIALS, JobParameters.GlobalParameters.ALLOW_INTEGERS_STARTING_WITH_0), CS.SConnDriver == SQLTools_Enums.BDD.DB_ACCESS ? "" : SQL_INSERT_ECHAP_VALUE),
                "System.String" => SetCleanString(sValue, false, JobParameters.CSVCharSeparator_Target, JobParameters.UseNull_Target, CS),
                "System.Byte" => SetCleanBoolean(sValueRetour, true, true),
                "System.Collections.BitArray" => SetCleanBitArray(sValueRetour),
                "System.Byte[]" => SetCleanBitArray(sValueRetour),
                _ => SetCleanString(sValue, false, JobParameters.CSVCharSeparator_Target, JobParameters.UseNull_Target, CS),
            };
            if (JobParameters.TrimData)
            {
                sValueRetour = sValueRetour.Trim();
            }

            return sValueRetour;

        }

        public static SQLTools_Enums.TYPE_DATA ConvertSQLDataType(Query.QField sSourceType, CONNString cConnTarget)
        {
            SQLTools_Enums.TYPE_DATA sNewType = sSourceType.FieldAnalyzer.ColumnType;

            if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.TEXT)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                }
            }

            if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DECIMAL)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMERIC; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DOUBLE)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.REAL;
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DOUBLE_PRECISION)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.REAL;
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.NUMERIC)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.DECIMAL; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMERIC; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMBER; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMERIC; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMERIC; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.NUMERIC; //+ (sSourceType.ColumnSize.Length > 1 ? ("(" + sSourceType.ColumnSize + ")") : "");
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.FLOAT)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.FLOAT;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.REAL;
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.INT)
            {
                switch (sSourceType.FieldAnalyzer.ColumnLinqType.ToString())
                {
                    case "System.Int16":
                        switch (cConnTarget.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_ACCESS:
                                sNewType = SQLTools_Enums.TYPE_DATA.SMALLINT;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                                break;
                            case SQLTools_Enums.BDD.DB_ODBC:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_POSTGRE:
                                sNewType = SQLTools_Enums.TYPE_DATA.SMALLINT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                sNewType = SQLTools_Enums.TYPE_DATA.SMALLINT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLITE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                        }
                        break;
                    case "System.Int32":
                        switch (cConnTarget.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_ACCESS:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                                break;
                            case SQLTools_Enums.BDD.DB_ODBC:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_POSTGRE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLITE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                        }
                        break;
                    case "System.Int64":
                        switch (cConnTarget.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_ACCESS:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                                break;
                            case SQLTools_Enums.BDD.DB_ODBC:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_POSTGRE:
                                sNewType = SQLTools_Enums.TYPE_DATA.BIGINT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                sNewType = SQLTools_Enums.TYPE_DATA.BIGINT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLITE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                        }
                        break;
                    default:
                        switch (cConnTarget.SConnDriver)
                        {
                            case SQLTools_Enums.BDD.DB_ACCESS:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                            case SQLTools_Enums.BDD.DB_MYSQL:
                                sNewType = SQLTools_Enums.TYPE_DATA.UNSIGNED;
                                break;
                            case SQLTools_Enums.BDD.DB_ODBC:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_ORACLE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_POSTGRE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLSERVER:
                                sNewType = SQLTools_Enums.TYPE_DATA.INT;
                                break;
                            case SQLTools_Enums.BDD.DB_SQLITE:
                                sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                                break;
                        }
                        break;
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DATE ||
                sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME ||
                sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP)
            {
                if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.TIMESTAMP ||
                    sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.DATETIME)
                {
                    switch (cConnTarget.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATETIME;
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            sNewType = SQLTools_Enums.TYPE_DATA.TIMESTAMP;
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            sNewType = SQLTools_Enums.TYPE_DATA.TIMESTAMP;
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            sNewType = SQLTools_Enums.TYPE_DATA.TIMESTAMP;
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            sNewType = SQLTools_Enums.TYPE_DATA.TIMESTAMP;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATETIME;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            sNewType = SQLTools_Enums.TYPE_DATA.TEXT;
                            break;
                    }
                }
                else
                {
                    switch (cConnTarget.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            sNewType = SQLTools_Enums.TYPE_DATA.DATE;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            sNewType = SQLTools_Enums.TYPE_DATA.TEXT;
                            break;
                    }
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.BIT ||
                sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.BOOL ||
                sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.BOOLEAN)
            {
                if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.BIT)
                {
                    switch (cConnTarget.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                            break;
                    }
                }
                else
                {
                    switch (cConnTarget.SConnDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_MYSQL:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_ODBC:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_ORACLE:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            sNewType = SQLTools_Enums.TYPE_DATA.BOOLEAN;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            sNewType = SQLTools_Enums.TYPE_DATA.BIT;
                            break;
                        case SQLTools_Enums.BDD.DB_SQLITE:
                            sNewType = SQLTools_Enums.TYPE_DATA.INTEGER;
                            break;
                    }
                }
            }
            else if (sSourceType.FieldAnalyzer.ColumnType == SQLTools_Enums.TYPE_DATA.CHAR)
            {
                switch (cConnTarget.SConnDriver)
                {
                    case SQLTools_Enums.BDD.DB_ACCESS:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT;
                        break;
                    case SQLTools_Enums.BDD.DB_MYSQL:
                        sNewType = SQLTools_Enums.TYPE_DATA.VARCHAR;
                        break;
                    case SQLTools_Enums.BDD.DB_ODBC:
                        sNewType = SQLTools_Enums.TYPE_DATA.VARCHAR;
                        break;
                    case SQLTools_Enums.BDD.DB_ORACLE:
                        sNewType = SQLTools_Enums.TYPE_DATA.VARCHAR;
                        break;
                    case SQLTools_Enums.BDD.DB_POSTGRE:
                        sNewType = SQLTools_Enums.TYPE_DATA.VARCHAR;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLSERVER:
                        sNewType = SQLTools_Enums.TYPE_DATA.NVARCHAR;
                        break;
                    case SQLTools_Enums.BDD.DB_SQLITE:
                        sNewType = SQLTools_Enums.TYPE_DATA.TEXT;
                        break;
                }
            }

            return sNewType;
        }

        public static Query CreateQueryFromDataTable(DataTable dtData, Job JobParameters, Query sQ)
        {
            //Match mcOut = Regex.Match(sQ.RawQuery, ".+\\s*:\\s*SELECT", RegexOptions.IgnoreCase);

            //string sQuery = mcOut.Success ? (mcOut.Value + " ") : (sQ.OutputTable + ":SELECT ");

            string sQuery = sQ.RawOutput + ":SELECT ";

            string sOriginalCondition = sQ.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetWhere();

            foreach (DataColumn dtC in dtData.Columns)
            {
                //sQuery += string.Concat(sEchappementChar, dtC.ColumnName.ToLower(), sEchappementChar, " AS ", dtC.ColumnName.ToLower(), ", ");
                sQuery = string.Concat(sQuery, dtC.ColumnName.ToLower(), " AS ", dtC.ColumnName.ToLower(), ", ");
            }
            sQuery = sQuery[0..^2];

            sQuery = string.Concat(sQuery, " FROM \"", dtData.Namespace, "\" AS \"", sQ.QueryAnalyzer.Tables[0].Alias, "\" ", sOriginalCondition.Length > 0 ? sOriginalCondition : "");

            foreach (Query crossQ in sQ.CrossJoinQueries)
            {
                sQuery = string.Concat(sQuery, " ", crossQ.CrossQueryRawScript, " ", crossQ.RawQuery);
            }

            return new Query(JobParameters, sQuery);
        }

        public static Type GetSystemTypeFromListTypes(SQLTools_Enums.TYPE_DATA sSQLType)
        {
            System.Type TypeField;
            string sType = sSQLType.ToString();

            if (sType.IndexOf("LONG") > -1) { TypeField = Type.GetType("System.Int64"); }
            else if (sType.IndexOf("BIGINT") > -1) { TypeField = Type.GetType("System.Int64"); }
            else if (sType.IndexOf("SMALLINT") > -1) { TypeField = Type.GetType("System.Int16"); }
            else if (sType.IndexOf("INT") > -1) { TypeField = Type.GetType("System.Int32"); }
            else if (sType.IndexOf("DATE") > -1) { TypeField = Type.GetType("System.DateTime"); }
            else if (sType.IndexOf("TIME") > -1) { TypeField = Type.GetType("System.DateTime"); }
            else if (sType.IndexOf("BOOL") > -1) { TypeField = Type.GetType("System.Boolean"); }
            else if (sType.IndexOf("BIT") > -1) { TypeField = Type.GetType("System.Boolean"); }
            else if (sType.IndexOf("DEC") > -1) { TypeField = Type.GetType("System.Decimal"); }
            else if (sType.IndexOf("NUM") > -1) { TypeField = Type.GetType("System.Decimal"); }
            else if (sType.IndexOf("FLOAT") > -1) { TypeField = Type.GetType("System.Double"); }
            else if (sType.IndexOf("DOUBLE") > -1) { TypeField = Type.GetType("System.Double"); }
            else if (sType.IndexOf("REAL") > -1) { TypeField = Type.GetType("System.Double"); }
            else if (sType.IndexOf("CHAR") > -1) { TypeField = Type.GetType("System.String"); }
            else if (sType.IndexOf("TEXT") > -1) { TypeField = Type.GetType("System.String"); }
            else if (sType.IndexOf("STRING") > -1) { TypeField = Type.GetType("System.String"); }
            else if (sType.IndexOf("MONEY") > -1) { TypeField = Type.GetType("System.Decimal"); }
            else if (sType.IndexOf("BIN") > -1) { TypeField = Type.GetType("System.Byte"); }
            else if (sType.IndexOf("BITARRAY") > -1) { TypeField = Type.GetType("System.Collections.BitArray"); }
            else if (sType.IndexOf("BYTEXXXZZZ") > -1) { TypeField = Type.GetType("System.Byte[]"); }
            else if (sType.IndexOf("BYTEA") > -1) { TypeField = Type.GetType("System.Byte[]"); }
            else if (sType.IndexOf("SINGLE") > -1) { TypeField = Type.GetType("System.Double"); }
            else if (sType.IndexOf("GUID") > -1) { TypeField = Type.GetType("System.Guid"); }
            else if (sType.IndexOf("UNIQUEIDENTIFIER") > -1) { TypeField = Type.GetType("System.Guid"); }
            else { TypeField = Type.GetType("System.String"); }

            return TypeField;
        }

        public static bool CastValueToBoolean(string sValue)
        {
            bool bBool = false;
            if (sValue.Equals("true", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("1")
                || sValue.Equals("yes", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("oui", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("ja", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("si", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("vrai", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("right", StringComparison.InvariantCultureIgnoreCase))
            {
                bBool = true;
            }
            if (sValue.Equals("false", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("0")
                || sValue.Equals("no", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("non", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("nein", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("faux", StringComparison.InvariantCultureIgnoreCase)
                || sValue.Equals("false", StringComparison.InvariantCultureIgnoreCase))
            {
                bBool = false;
            }

            return bBool;
        }

        public static string CleanSQLConditionsForTarget_WithDynamicParams(string sSQLEchappementChar, string sDynColumns, List<string> sListVariables)
        {
            sDynColumns = ScriptLanguage.InterpretAndReplaceScriptLanguage(sDynColumns, sListVariables);
            sDynColumns = sDynColumns.Replace("'", "''").Replace(sSQLEchappementChar, "");

            StringBuilder sbWhere = new();
            int iC = 0;

            foreach (string sDC in sDynColumns.Split(Convert.ToChar(";")))
            {
                iC++;
                if (iC == 1 && sDynColumns.Length > 0) { sbWhere.Append(" WHERE "); }
                if (iC > 1) { sbWhere.Append(" AND "); }

                if (sDC.IndexOf("=") > 0)
                {
                    string sColName = sDC[..sDC.IndexOf("=")];
                    string sData = sDC[(sDC.IndexOf("=") + 1)..];
                    sbWhere.Append(string.Concat(sSQLEchappementChar, sColName, sSQLEchappementChar, " = ", sData.StartsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE, sData, sData.EndsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE));
                }
                else //nom générique par réplicator
                {
                    string sColName = string.Concat("DYNPARAM", "_", iC.ToString("00"));
                    sbWhere.Append(string.Concat(sSQLEchappementChar, sColName, sSQLEchappementChar, " = ", sDC.StartsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE, sDC, sDC.EndsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE));
                }

            }

            return sbWhere.ToString();
        }

        public static string CleanSQLConditionsForTarget_WithDbName(string sSQLEchappementChar, string sDbNameColumn, string sValue)
        {
            sValue = sValue.Replace("'", "''").Replace(sSQLEchappementChar, "");

            StringBuilder sbWhere = new();
            sbWhere.Append(" WHERE ");

            sbWhere.Append(string.Concat(sSQLEchappementChar, sDbNameColumn, sSQLEchappementChar, " = ", sValue.StartsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE, sValue, sValue.EndsWith("''") ? "" : SQL_INSERT_ECHAP_VALUE));

            return sbWhere.ToString();
        }

        public static string CleanSQLConditionsForTarget_WithSourceQuery(string sSQLEchappementChar, Query FuzibleQuery, DataTable dtOut)
        {
            string sSQLWhereFilter = FuzibleQuery.QueryAnalyzer.PreBuiltSynchroTargetQuery.PredictedSynchroTargetWhere();

            if (sSQLWhereFilter.Length > 0)
            {
                List<string> sListColumns = new();
                string sEchappementChar = sSQLEchappementChar;

                //remplacement des valeurs de colonnes par les alias
                if (dtOut != null)
                {
                    foreach (DataColumn dt in dtOut.Columns)
                    {
                        //problème colonne "id_anneemois" dans condition qui est "where id_anneemois_saisie >=..."
                        if (Regex.IsMatch(sSQLWhereFilter, string.Concat("[^\\w^\\d]", Toolbox.RemoveRegexFromString(dt.ColumnName), "[^\\w^\\d]"), RegexOptions.IgnoreCase))
                        //if (sSQLWhereFilter.IndexOf(dt.ColumnName, StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            sSQLWhereFilter = Regex.Replace(sSQLWhereFilter, string.Concat("([^\\w^\\d])(", Toolbox.RemoveRegexFromString(dt.ColumnName), ")([^\\w^\\d])"), "$1" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$2" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$3", RegexOptions.IgnoreCase);

                            //sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, dt.ColumnName, string.Concat(sEchappementChar, dt.ColumnName, sEchappementChar), StringComparison.InvariantCultureIgnoreCase);
                            sListColumns.Add(dt.ColumnName);
                        }
                    }
                }
                else
                {
                    foreach (Query.QField sC in FuzibleQuery.QueryAnalyzer.Fields)
                    {
                        if ((!sC.Name.Equals("*")) && Regex.IsMatch(sSQLWhereFilter, string.Concat("[^\\w^\\d]", Toolbox.RemoveRegexFromString(sC.Alias), "[^\\w^\\d]"), RegexOptions.IgnoreCase))
                        {
                            sSQLWhereFilter = Regex.Replace(sSQLWhereFilter, string.Concat("([^\\w^\\d])(", Toolbox.RemoveRegexFromString(sC.Alias), ")([^\\w^\\d])"), "$1" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$2" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$3", RegexOptions.IgnoreCase);

                            //sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, sC.Alias, string.Concat(sEchappementChar, sC.Alias, sEchappementChar), StringComparison.InvariantCultureIgnoreCase);
                            sListColumns.Add(sC.Alias);
                        }
                    }
                }

                foreach (Query.QField sC in FuzibleQuery.QueryAnalyzer.Fields)
                {
                    if ((!sC.Name.Equals("*")) && Regex.IsMatch(sSQLWhereFilter, string.Concat("[^\\w^\\d]", Toolbox.RemoveRegexFromString(sC.Name), "[^\\w^\\d]"), RegexOptions.IgnoreCase))
                    {
                        sSQLWhereFilter = Regex.Replace(sSQLWhereFilter, string.Concat("([^\\w^\\d])(", Toolbox.RemoveRegexFromString(sC.Name), ")([^\\w^\\d])"), "$1" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$2" + Toolbox.RemoveRegexFromString(sEchappementChar) + "$3", RegexOptions.IgnoreCase);

                        //sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, sC.Name, string.Concat(sEchappementChar, sC.Alias, sEchappementChar), StringComparison.InvariantCultureIgnoreCase);
                        sListColumns.Add(sC.Name);
                    }
                }

                //au cas ou on aurait dans le WHERE original des caractères de remplacement
                if (sSQLWhereFilter.IndexOf("`" + sEchappementChar) > -1)
                {
                    sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, "`" + sEchappementChar, sEchappementChar, StringComparison.InvariantCultureIgnoreCase);
                }
                if (sSQLWhereFilter.IndexOf(sEchappementChar + "`") > -1)
                {
                    sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, sEchappementChar + "`", sEchappementChar, StringComparison.InvariantCultureIgnoreCase);
                }
                if (sSQLWhereFilter.IndexOf("\"" + sEchappementChar) > -1)
                {
                    sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, "\"" + sEchappementChar, sEchappementChar, StringComparison.InvariantCultureIgnoreCase);
                }
                if (sSQLWhereFilter.IndexOf(sEchappementChar + "\"") > -1)
                {
                    sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, sEchappementChar + "\"", sEchappementChar, StringComparison.InvariantCultureIgnoreCase);
                }

                //analyse des xxx."test" : il faut virer les xxx.
                int iDx;
                int iDxSpace;
                string sTmpA;
                string sTmpB = "";
                foreach (string sCpd in sListColumns)
                {
                    iDx = sSQLWhereFilter.IndexOf(string.Concat(".", sEchappementChar, sCpd), StringComparison.InvariantCultureIgnoreCase);
                    if (iDx > 0)
                    {
                        //on recherche le dernier index d'un espace avant le "." pour faire la coupe
                        sTmpA = sSQLWhereFilter[..(iDx + 1)];
                        iDxSpace = sTmpA.LastIndexOf(" ");
                        if (iDxSpace > 0)
                        {
                            //isolation du "xxx."
                            sTmpB = sSQLWhereFilter.Substring(iDxSpace, iDx - iDxSpace + 1).Trim();
                        }
                        sSQLWhereFilter = Toolbox.ReplaceString(sSQLWhereFilter, sTmpB, " ", StringComparison.InvariantCultureIgnoreCase);
                    }
                }

                //if (sSQLWhereFilter.Trim().StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
                //{ sSQLWhereFilter = sSQLWhereFilter.Substring(5).Trim(); }

                if (!sSQLWhereFilter.Trim().StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
                {
                    sSQLWhereFilter = string.Concat(" WHERE ", sSQLWhereFilter);
                }

            }

            return sSQLWhereFilter;
        }

        public static string ToBitString(this BitArray bits)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < bits.Count; i++)
            {
                char c = bits[i] ? '1' : '0';
                sb.Append(c);
            }

            return sb.ToString();
        }

        public static int GetWeekNumberOfMonth(DateTime date)
        {
            date = date.Date;
            DateTime firstMonthDay = new(date.Year, date.Month, 1);
            DateTime firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            if (firstMonthMonday > date)
            {
                firstMonthDay = firstMonthDay.AddMonths(-1);
                firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            }
            return (date - firstMonthMonday).Days / 7 + 1;
        }

        public static string GetLocalIPAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception(Languages.Languages.shs_tool_checkipko);
        }

        public static int GetPercentUsageRam()
        {
            int iPercent = -1;

            try
            {
                ObjectQuery wql = new("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new(wql);
                ManagementObjectCollection results = searcher.Get();

                foreach (ManagementObject result in results)
                {
                    string sData = result["TotalVisibleMemorySize"].ToString();
                    decimal dTotalRam = Convert.ToDecimal(sData);
                    //Int32 iTotalVisibleMemorySize = (Int32)result["TotalVisibleMemorySize"];
                    sData = result["FreePhysicalMemory"].ToString();
                    decimal dFreePhysicalMemory = Convert.ToDecimal(sData);
                    //Int32 iTotalVirtualMemorySize = (Int32)result["TotalVirtualMemorySize"];
                    //Int32 iFreeVirtualMemory = (Int32)result["FreeVirtualMemory"];
                    var dPercent = dTotalRam - dFreePhysicalMemory;
                    dPercent = (dPercent / dTotalRam);
                    dPercent = dPercent * 100;
                    iPercent = Convert.ToInt32(dPercent);
                }

                return iPercent;
            }
            catch
            { return -1; }
        }

        public static DataTable GetDtDistinctRecords(DataTable dt, string[] Columns)
        {
            DataTable dtUniqRecords;
            dtUniqRecords = dt.DefaultView.ToTable(true, Columns);
            return dtUniqRecords;
        }

        public static string GetAppContext()
        {
            StringBuilder sbContext = new();

            AppDomain aD = Thread.GetDomain();
            Thread t = Thread.CurrentThread;

            string sUserName = System.Security.Principal.WindowsIdentity.GetCurrent().Name; // Gives NT AUTHORITY\SYSTEM
            string sUsername2 = Environment.UserName; // Gives SYSTEM

            sbContext.Append("--- [SYSINFO : Thread -> Culture:" + t.CurrentCulture.DisplayName);
            sbContext.Append(" / Priority:" + t.Priority.ToString());
            sbContext.Append(" | AppDomain -> Name:" + aD.FriendlyName);
            sbContext.Append(" / BaseDir:" + aD.BaseDirectory);
            sbContext.Append(" / DynDir:" + aD.DynamicDirectory);
            sbContext.Append(" | User Context(Ident., Envir., Sysinfo.) :  -> " + sUserName + "-" + sUsername2);
            sbContext.Append(" ] ---");

            return sbContext.ToString();

        }

        public static string RandomString(Random rND, int size, bool lowerCase)
        {
            StringBuilder builder = new();

            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * rND.NextDouble() + 65)));
                builder.Append(ch);
            }
            if (lowerCase)
            {
                return builder.ToString().ToLower();
            }

            return builder.ToString();
        }

        public static bool IsStringAllUpper(string input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (!Char.IsUpper(input[i]))
                    return false;
            }

            return true;
        }

        public static string RemoveRegexFromString(string s)
        {
            return s.Replace("*", "\\*").Replace(@"\", @"\\").Replace("[", "\\[").Replace("]", "\\]").Replace(".", "\\.").Replace("$", "\\$").Replace("|", "\\|").Replace("(", "\\(").Replace(")", "\\)");
        }

        public static bool IsUserAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.SystemOperator);
        }

        public static char[] GetMachineGuid(int i1CPU2HDD3PATH)
        {
            char[] sCpuInfo = null;

            if (i1CPU2HDD3PATH == 1)
            {
                try
                {
                    //var process = new Process
                    //{
                    //    StartInfo = new ProcessStartInfo
                    //    {
                    //        FileName = "wmic",
                    //        Arguments = "cpu get ProcessorId",
                    //        RedirectStandardOutput = true,
                    //        UseShellExecute = false,
                    //        CreateNoWindow = true
                    //    }
                    //};

                    //process.Start();
                    //string output = process.StandardOutput.ReadToEnd();
                    //process.WaitForExit();

                    //string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    //sCpuInfo = (lines.Length > 1 ? lines[1].Trim() : "").ToCharArray();
                    //méthode bien plus rapide mais nécessite les droits admin de la machine
                    var mbs = new ManagementObjectSearcher("Select ProcessorId From Win32_processor");
                    ManagementObjectCollection moc = mbs.Get();

                    if (moc.Count > 0)
                    {
                        foreach (ManagementObject mo in moc)
                        {
                            if (sCpuInfo == null)
                            {
                                string sV = mo["ProcessorId"].ToString();
                                sCpuInfo = sV.ToCharArray();
                                break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    ManagementClass mgmt = new("Win32_Processor");
                    ManagementObjectCollection objCol = mgmt.GetInstances();
                    foreach (ManagementObject obj in objCol)
                    {
                        sCpuInfo = obj.Properties["ProcessorId"].Value.ToString().ToCharArray(); break;
                    }
                }
                if (sCpuInfo.Length == 16 && new string(sCpuInfo).Equals("0000000000000000")) //certaines VM
                {
                    try
                    {
                        ManagementObjectSearcher mos = new("SELECT UUID FROM Win32_ComputerSystemProduct");
                        ManagementObjectCollection mbsList = mos.Get();
                        string systemId = string.Empty;
                        foreach (ManagementBaseObject mo in mbsList)
                        {
                            sCpuInfo = mo["UUID"].ToString().Replace("-", "")[..16].ToCharArray(); break;
                        }
                    }
                    catch { }
                }
            }
            else if (i1CPU2HDD3PATH == 2)
            {
                //sCpuInfo = "".ToCharArray();

                //var process = new Process
                //{
                //    StartInfo = new ProcessStartInfo
                //    {
                //        FileName = "cmd",
                //        Arguments = "/C vol C:",
                //        RedirectStandardOutput = true,
                //        UseShellExecute = false,
                //        CreateNoWindow = true
                //    }
                //};

                //process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //process.WaitForExit();

                //foreach (string line in output.Split(Environment.NewLine))
                //{
                //    Match mc = Regex.Match(line, "[A-Z0-9]{4}-[A-Z0-9]{4}");
                //    if (mc.Success)
                //    {
                //        sCpuInfo = mc.Value.Replace("-", "").ToCharArray();
                //        break;
                //    }
                //}
                ManagementObject dsk = new(@"win32_logicaldisk.deviceid=""" + "C" + @":""");
                dsk.Get();
                string sV = dsk["VolumeSerialNumber"].ToString();
                sCpuInfo = sV.ToCharArray();
            }
            else if (i1CPU2HDD3PATH == 3)
            {
                System.Byte[] asciiBytes = Encoding.ASCII.GetBytes(Directory.GetCurrentDirectory());
                int iB = 0;
                foreach (byte b in asciiBytes) { if (iB % 2 == 0) { iB += Convert.ToInt32(b); } else { iB *= Convert.ToInt32(b); } }
                sCpuInfo = (iB < 0 ? -iB : iB).ToString().ToCharArray();
            }

            return sCpuInfo;
        }

        public static DateTime GetLinkerTime(this Assembly assembly, TimeZoneInfo target = null)
        {
            string filePath = assembly.Location;
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;

            System.Byte[] buffer = new byte[2048];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                stream.Read(buffer, 0, 2048);
            }

            int offset = BitConverter.ToInt32(buffer, c_PeHeaderOffset);
            int secondsSince1970 = BitConverter.ToInt32(buffer, offset + c_LinkerTimestampOffset);
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            DateTime linkTimeUtc = epoch.AddSeconds(secondsSince1970);

            TimeZoneInfo tz = target ?? TimeZoneInfo.Local;
            DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, tz);

            return localTime;
        }

        public static bool CheckEmailValid(string emailaddress)
        {
            if (emailaddress.Length > 0)
            {
                try
                {
                    MailAddress m = new(emailaddress);

                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }
            else { return false; }
        }

        public static void ExtractSQLFunction(ref string sTransformation, ref string sS, ref string sField, ref string sZ, ref List<int> iOffset)
        {

            //note, l'index 0 de iPosFunc est la fonction la plus "profonde"
            MatchCollection mcFunctionsV2 = Regex.Matches(sTransformation, "(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + ")\\s*\\({1}", RegexOptions.IgnoreCase);

            if (mcFunctionsV2.Count > 0)
            {

                int iFuncCountV2 = mcFunctionsV2.Count - 1;

                //le champ
                string sFieldV2 = sTransformation[mcFunctionsV2[iFuncCountV2].Index..]; //li_test, 1, 2
                sFieldV2 = sFieldV2[(sFieldV2.IndexOf("(") + 1)..];

                Match mcNext;
                bool bFuncWithEmbracedFields = sFieldV2.Trim().StartsWith("\"");

                if (bFuncWithEmbracedFields)
                {
                    mcNext = Regex.Match(sFieldV2, "\"[,\\(\\)]");
                    sFieldV2 = sFieldV2[..(mcNext.Index + 1)];
                } //gestion cas "Queue - Type (QL,QF,QFP,HSK,ISO)","Désignation")
                else
                {
                    mcNext = Regex.Match(sFieldV2, "[,\\(\\)]");
                    sFieldV2 = sFieldV2[..mcNext.Index];
                } //li_test


                //le petit nom de la fonction
                string sZV2 = mcFunctionsV2[iFuncCountV2].Value[..(mcFunctionsV2[iFuncCountV2].Length - 1)].ToUpper();

                //retrait des parenthèses de fin de fonction
                string sExtract = "";
                //la fonction complète
                int iOff = mcFunctionsV2.Count - 1;
                sExtract = sTransformation[mcFunctionsV2[iFuncCountV2].Index..];
                string sExtractWithDecimal = sTransformation[mcFunctionsV2[iFuncCountV2].Index..];
                //sExtract = sExtract[..^iOff];
                try
                {
                    Match mcExtract = Regex.Match(sExtract, sZV2 + "\\((?:[^()']|'[^']*')+\\)");
                    sExtract = mcExtract.Value;
                    sExtractWithDecimal = sExtractWithDecimal[mcExtract.Length..];
                }
                catch { sExtract = sExtract[..^iOff]; }

                if (Regex.Match(sExtractWithDecimal, "\\s*[+|-]\\s*\\d+").Success && (sExtract.IndexOf("LENGTH", StringComparison.OrdinalIgnoreCase) > -1 || sExtract.IndexOf("CHARINDEX", StringComparison.OrdinalIgnoreCase) > -1)) //on empêche ainsi de détecter les trucs genre CONCAT(test, '+', test2)
                //if (Regex.Match(sExtract, "(.*?)[^'\"+-]+[\\+\\-](.*?)[^'\"+-]+").Success && (sExtract.IndexOf("LENGTH", StringComparison.OrdinalIgnoreCase) > -1 || sExtract.IndexOf("CHARINDEX", StringComparison.OrdinalIgnoreCase) > -1)) //on empêche ainsi de détecter les trucs genre CONCAT(test, '+', test2)
                {
                    var matches = Regex.Matches(sExtractWithDecimal, "([\\+\\-])(\\s*)(\\d+)");
                    if (matches.Count > 0)
                    {
                        foreach (Match m in matches)
                        {
                            iOffset.Add(Convert.ToInt32(string.Concat(m.Groups[1].Value.Trim(), m.Groups[3].Value.Trim())));
                            //else { sExtract = s.Trim(); }
                        }
                    }
                }
                //------------------

                int iClose = sExtract.LastIndexOf(")");
                string ssV2 = sTransformation[mcFunctionsV2[iFuncCountV2].Index..];
                if (iClose > -1)
                {
                    ssV2 = ssV2[..(iClose + 1)];
                }

                sS = ssV2;
                sField = sFieldV2;
                if (Regex.IsMatch(sField, SHSRegex.REGEX_SQL_FIELD_EMBRACE)) { sField = sField[1..^1]; }
                sZ = sZV2;
            }
            else { sField = ""; sS = ""; sZ = ""; }
        }

        public static string ReplaceXMLSpecials(string sXML, bool bWithInfSup)
        {
            sXML = Regex.Replace(sXML, "&(?![A-z\\d#]{2,4};)", "&amp;");
            sXML = Regex.Replace(sXML, "'(?![A-z\\d#]{2,4};)", "&apos;");
            sXML = Regex.Replace(sXML, "\"(?![A-z\\d#]{2,4};)", "&quot;");
            //sXML = Regex.Replace(sXML, Environment.NewLine, "&#10;");

            if (bWithInfSup)
            {
                if (sXML.Contains('>', StringComparison.CurrentCulture))
                {
                    sXML = sXML.Replace(">", "&gt;");
                }
                if (sXML.Contains('<', StringComparison.CurrentCulture))
                {
                    sXML = sXML.Replace("<", "&lt;");
                }
            }

            return sXML;
        }

        public static string ConvertHTMLValueToString(string sValue)
        {
            //List<string[]> sHTML = SHSRFramework.HTMLCodes.GetHTMLCodes();
            //foreach (string[] sH in sHTML)
            //{ sValue = sValue.Replace(sH[0], sH[1]); }
            sValue = sValue.Replace("\\r\\n", Environment.NewLine);
            sValue = HttpUtility.HtmlDecode(sValue);

            return sValue;
        }

        public static List<Tuple<string, string>> CreateRelationBetweenDataAndDynParam(DataSet dsReturn, bool bByRow, bool isSourceTrueOrTargetFalse, int iCommand, List<string> sDynParams, string sCharEchap, string sSQLStringEchap)
        {
            List<Tuple<string, string>> sListParamValues = new();
            //recherche du pattern %CSx si isSourceTrueOrTargetFalse = true
            //ou pattern %CTx si isSourceTrueOrTargetFalse = false
            foreach (string sParam in sDynParams)
            {
                //de type %CS1[1] - source command 1 avec colonne 1
                Match mcCommandWithColumn = Regex.Match(sParam, "^(%" + (isSourceTrueOrTargetFalse ? "S" : "T") + "C" + iCommand + ")(\\[\\d+\\])$");
                Match mcCommandWithoutColumn = Regex.Match(sParam, "^(%" + (isSourceTrueOrTargetFalse ? "S" : "T") + "C" + iCommand + ")$");

                if (mcCommandWithColumn.Success || mcCommandWithoutColumn.Success)
                {
                    int iColIdx = 0;

                    if (mcCommandWithColumn.Success) //si l'index de colonne a été précisé, on le sélectionne
                    {
                        string sColIndex = mcCommandWithColumn.Groups[2].Value;
                        sColIndex = sColIndex.Replace("[", "").Replace("]", "");
                        if (dsReturn.Tables[0].Columns.Count >= Convert.ToInt32(sColIndex) && Convert.ToInt32(sColIndex) > 0)
                        { iColIdx = Convert.ToInt32(sColIndex) - 1; } //attention, l'utilisateur est en base 1
                    }

                    Tuple<string, string> sNewParamValues;
                    if (dsReturn.Tables.Count > 0 && dsReturn.Tables[0].Columns.Count > 0 && dsReturn.Tables[0].Rows.Count > 0)
                    {
                        if (bByRow) //on assemble toutes les données ensemble (genre pour faire un IN (1,2,3,4,5)
                        {
                            StringBuilder sbAggregate = new();
                            for (int iR = 0; iR < dsReturn.Tables[0].Rows.Count; iR++)
                            {
                                sbAggregate.Append(string.Concat(sSQLStringEchap, dsReturn.Tables[0].Rows[iR][iColIdx].ToString().Replace(sSQLStringEchap, " "), sSQLStringEchap, iR == dsReturn.Tables[0].Rows.Count - 1 ? "" : sCharEchap));
                            }
                            sNewParamValues = new Tuple<string, string>(mcCommandWithColumn.Success ? mcCommandWithColumn.Value : mcCommandWithoutColumn.Value, sbAggregate.ToString());
                        }
                        else //on va démultiplier le job en considérant que chaque entrée est un nouveau paramètre dynamique
                        {
                            List<string> sResults = new();
                            for (int iR = 0; iR < dsReturn.Tables[0].Rows.Count; iR++)
                            {
                                sResults.Add(dsReturn.Tables[0].Rows[iR][iColIdx].ToString());
                            }
                            sNewParamValues = new Tuple<string, string>(mcCommandWithColumn.Success ? mcCommandWithColumn.Value : mcCommandWithoutColumn.Value, string.Join(SHSRegex.REGEX_SPLIT_DYNPARAMS, sResults));
                        }
                        sListParamValues.Add(sNewParamValues);
                    }
                    else
                    {
                        //sListParamValues.Add(new Tuple<string, string>(mcCommandWithColumn.Success ? mcCommandWithColumn.Value : mcCommandWithoutColumn.Value, "")); 
                    }
                }
            }

            return sListParamValues;
            //if (dsReturn.Tables.Count > 0 && dsReturn.Tables[0].Columns.Count > 0 && dsReturn.Tables[0].Rows.Count > 0)
            //{
            //    if (bByRow)
            //    {
            //        for (int iR = 0; iR < dsReturn.Tables[0].Rows.Count; iR++)
            //        {
            //            sbAggregate.Append(string.Concat(sSQLStringEchap, dsReturn.Tables[0].Rows[iR][iColIdx].ToString().Replace(sSQLStringEchap, " "), sSQLStringEchap, iR == dsReturn.Tables[0].Rows.Count - 1 ? "" : sCharEchap));
            //        }
            //        return new List<string> { sbAggregate.ToString() };
            //    }
            //    else
            //    {

            //        //for (int iC = 0; iC < dsReturn.Tables[0].Columns.Count; iC++)
            //        //{
            //        //    sbAggregate.Append(string.Concat(sSQLStringEchap, dsReturn.Tables[0].Rows[0][iC].ToString().Replace(sSQLStringEchap, " "), sSQLStringEchap, iC == dsReturn.Tables[0].Columns.Count - 1 ? "" : sCharEchap));
            //        //}
            //        return sResults;
            //    }
            //}
            //else { return new List<string>(); }
        }

        public static string GetProgramDependencies()
        {
            StringBuilder sbDep = new();
            AssemblyName[] assList = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            foreach (AssemblyName ass in assList)
            {
                if (!ass.Name.StartsWith("System.") || ass.Name.StartsWith("System.Data"))
                {
                    sbDep.AppendLine(string.Concat(ass.Name, " - ", ass.Version));
                }
            }
            return sbDep.ToString();
        }

        public static DataSet MergeAndSortDataTables(DataSet dsData)
        {
            if (dsData.Tables.Count > 1)
            {
                List<string> sListColumns = new();
                List<string> sListIndexTables = new();

                //recherche des pattern communs entre toutes les tables
                foreach (DataTable dT in dsData.Tables)
                {
                    StringBuilder sbCol = new();
                    foreach (DataColumn dC in dT.Columns)
                    {
                        sbCol.Append(string.Concat(dC.ColumnName + ";"));
                    }

                    if (!sListColumns.Contains(sbCol.ToString()))
                    {
                        sListColumns.Add(sbCol.ToString());
                        sListIndexTables.Add(string.Concat(dT.TableName, ";"));
                    }
                    else
                    {
                        sListIndexTables[sListColumns.IndexOf(sbCol.ToString())] = string.Concat(sListIndexTables[sListColumns.IndexOf(sbCol.ToString())], dT.TableName, ";");
                    }
                }

                //on a donc une liste contenant chaque liste de colonnes distinctes de toutes les tables du dataset
                //ainsi que les index associés
                foreach (string sIndex in sListIndexTables)
                {
                    string[] sSplitIndex = sIndex.Split(Convert.ToChar(";"), StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 1; i < sSplitIndex.Length; i++)
                    {
                        dsData.Tables[sSplitIndex[0]].Merge(dsData.Tables[sSplitIndex[i]]);
                    }
                }

                //suppression de tous les doubles
                foreach (string sIndex in sListIndexTables)
                {
                    string[] sSplitIndex = sIndex.Split(Convert.ToChar(";"), StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 1; i < sSplitIndex.Length; i++)
                    {
                        dsData.Tables.Remove(sSplitIndex[i]);
                    }
                }
            }

            return dsData;
        }

        internal static bool TasksNotFinished(List<SQLStreamingData> tOperation)
        {
            int iFinished = 0;
            foreach (SQLStreamingData t in tOperation)
            {
                if (t == null) { iFinished++; }
                else if (t.Operation.IsCompleted) { iFinished++; }
            }
            if (iFinished == tOperation.Count) { return false; } else { return true; }
        }

        public static int NextInt32(this Random rng)
        {
            int firstBits = rng.Next(0, 1 << 4) << 28;
            int lastBits = rng.Next(0, 1 << 28);
            return firstBits | lastBits;
        }

        public static decimal NextDecimal(this Random rng)
        {
            byte scale = (byte)rng.Next(29);
            bool sign = rng.Next(2) == 1;
            return new decimal(rng.NextInt32(),
                               rng.NextInt32(),
                               rng.NextInt32(),
                               sign,
                               scale);
        }

        public static bool IsStringHTML(string sData)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(sData);

            if (doc.ParseErrors.Any()) { return false; }
            else { return true; }
        }

        internal static string GetMimeTypeFromExt(string value)
        {
            switch (value.ToLower())
            {
                case ".aac": return "audio/aac";
                case ".abw": return "application/x-abiword";
                case ".arc": return "application/octet-stream";
                case ".avi": return "video/x-msvideo";
                case ".azw": return "application/vnd.amazon.ebook";
                case ".bin": return "application/octet-stream";
                case ".bmp": return "image/bmp";
                case ".bz": return "application/x-bzip";
                case ".bz2": return "application/x-bzip2";
                case ".csh": return "application/x-csh";
                case ".css": return "text/css";
                case ".csv": return "text/csv";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".eot": return "application/vnd.ms-fontobject";
                case ".epub": return "application/epub+zip";
                case ".gif": return "image/gif";
                case ".html": return "text/html";
                case ".ico": return "image/x-icon";
                case ".ics": return "text/calendar";
                case ".jar": return "application/java-archive";
                case ".jpeg": return ".jpg	image/jpeg";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".mid.": return "midi	audio/midi";
                case ".mpeg": return "video/mpeg";
                case ".mpkg": return "application/vnd.apple.installer+xml";
                case ".odp": return "application/vnd.oasis.opendocument.presentation";
                case ".ods": return "application/vnd.oasis.opendocument.spreadsheet";
                case ".odt": return "application/vnd.oasis.opendocument.text";
                case ".oga": return "audio/ogg";
                case ".ogv": return "video/ogg";
                case ".ogx": return "application/ogg";
                case ".otf": return "font/otf";
                case ".png": return "image/png";
                case ".pdf": return "application/pdf";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case ".rar": return "application/x-rar-compressed";
                case ".rtf": return "application/rtf";
                case ".sh": return "application/x-sh";
                case ".svg": return "image/svg+xml";
                case ".swf": return "application/x-shockwave-flash";
                case ".tar": return "application/x-tar";
                case ".tif": return "tiff	image/tiff";
                case ".ts": return "application/typescript";
                case ".ttf": return "font/ttf";
                case ".vsd": return "application/vnd.visio";
                case ".wav": return "audio/x-wav";
                case ".weba": return "audio/webm";
                case ".webm": return "video/webm";
                case ".webp": return "image/webp";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                case ".xhtml": return "application/xhtml+xml";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".xml": return "application/xml";
                case ".xul": return "application/vnd.mozilla.xul+xml";
                case ".zip": return "application/zip";
                case ".3gp": return "video/3gpp audio/3gpp dans le cas où le conteneur ne comprend pas de vidéo";
                case ".3g2": return "video/3gpp2 audio/3gpp2 dans le cas où le conteneur ne comprend pas de vidéo";
                case ".7z": return "application/x-7z-compressed";
                default: return "application/octet-stream";
            }
        }

        internal static string RemoveUnauthorizedCharsInFilename(string sFilename)
        {
            char[] cInvalid = Path.GetInvalidFileNameChars();

            foreach (char c in sFilename)
            {
                if (cInvalid.Contains(c))
                {
                    sFilename = sFilename.Replace(c, '_');
                }
            }
            return sFilename;
        }

        internal static void RandomData(List<string> sAnonomizationData, DataTable dtData, List<SQLColumn> sSqlCol, DataColumn dc, string sField, bool bRandomFromItSelf, string sScript, ref LogTools MyLog)
        {
            foreach (DataRow dr in dtData.Rows)
            {
                string sRandomData = "";
                //on prend une valeur aléatoire dans la liste
                if (sAnonomizationData.Count > 0)
                {
                    Random rnd = new();
                    int r = rnd.Next(sAnonomizationData.Count);
                    sRandomData = sAnonomizationData[r];

                    try { dr[dc] = sRandomData; }
                    catch (Exception ex)
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ANONYMIZE' (INVALID LIST-RANDOM DATA : " + sRandomData + ")" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        break;
                    }
                }
                else
                {
                    if (bRandomFromItSelf)
                    {
                        Random rnd = new();
                        int r = rnd.Next(dtData.Rows.Count);
                        sRandomData = dtData.Rows[r][sField].ToString();

                        try { dr[dc] = dtData.Rows[r][sField]; }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ANONYMIZE' (INVALID SELF-RANDOM DATA : " + sRandomData + ")" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        }
                    }
                    else
                    {
                        try
                        {
                            int iData;
                            Int16 iData16;
                            Int64 iData64;
                            Random iRandom = new();
                            bool bOK = false;

                            switch (sSqlCol[0].ColumnLinqType.FullName)
                            {
                                case "System.Int64":
                                    bOK = Int64.TryParse(dr[sField].ToString(), out iData64);
                                    if (bOK)
                                    {
                                        sRandomData = iData64.ToString();
                                        if (iData64 >= 0) { dr[dc] = iRandom.Next(0, Int32.MaxValue); }
                                        else { dr[dc] = iRandom.Next(-Int32.MaxValue, -1); }
                                    }
                                    break;
                                case "System.Int32":
                                    bOK = Int32.TryParse(dr[sField].ToString(), out iData);
                                    if (bOK)
                                    {
                                        sRandomData = iData.ToString();
                                        if (iData >= 0) { dr[dc] = iRandom.Next(0, iData); }
                                        else { dr[dc] = iRandom.Next(iData, -1); }
                                    }
                                    break;
                                case "System.Int16":
                                    bOK = Int16.TryParse(dr[sField].ToString(), out iData16);
                                    if (bOK)
                                    {
                                        sRandomData = iData16.ToString();
                                        if (iData16 >= 0) { dr[dc] = iRandom.Next(0, iData16); }
                                        else { dr[dc] = iRandom.Next(iData16, -1); }
                                    }
                                    break;
                                case "System.DateTime":
                                    DateTime start = new(1000, 1, 1);
                                    int range = (DateTime.Today - start).Days;
                                    DateTime dtFinal = start.AddDays(iRandom.Next(range));
                                    sRandomData = dtFinal.ToString();
                                    dr[dc] = dtFinal;
                                    break;
                                case "System.DateTimeOffset":
                                    DateTime start2 = new(1000, 1, 1);
                                    int range2 = (DateTime.Today - start2).Days;
                                    DateTime dtFinal2 = start2.AddDays(iRandom.Next(range2));
                                    sRandomData = dtFinal2.ToString();
                                    dr[dc] = dtFinal2;
                                    break;
                                case "System.Guid":
                                    string sg = Guid.NewGuid().ToString();
                                    dr[dc] = sg;
                                    break;
                                case "System.Boolean":
                                    bool randomBool = iRandom.Next(2) == 1;
                                    sRandomData = randomBool.ToString();
                                    dr[dc] = randomBool;
                                    break;
                                case "System.Decimal":
                                    decimal d = iRandom.NextDecimal();
                                    sRandomData = d.ToString();
                                    dr[dc] = d;
                                    break;
                                case "System.Double":
                                    double dd = iRandom.NextDouble();
                                    sRandomData = dd.ToString();
                                    dr[dc] = dd;
                                    break;
                                case "System.String":
                                    string ss = Toolbox.RandomString(iRandom, dr[sField].ToString().Length, !Toolbox.IsStringAllUpper(dr[sField].ToString()));
                                    sRandomData = ss;
                                    dr[dc] = ss;
                                    break;
                                case "System.Byte":
                                    System.Byte[] arrayB = new byte[1];
                                    iRandom.NextBytes(arrayB);
                                    sRandomData = arrayB.ToString();
                                    dr[dc] = arrayB;
                                    break;
                                case "System.Collections.BitArray":
                                    System.Byte[] array = new byte[8];
                                    iRandom.NextBytes(array);
                                    sRandomData = array.ToString();
                                    dr[dc] = array;
                                    break;
                                case "System.Byte[]":
                                    System.Byte[] array2 = new byte[8];
                                    iRandom.NextBytes(array2);
                                    sRandomData = array2.ToString();
                                    dr[dc] = array2;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.TRG, ex, string.Concat(Languages.Languages.mt_rework_bypassfunction01 + "'ANONYMIZE' (INVALID AUTO-RANDOM DATA : " + sRandomData + ")" + Languages.Languages.mt_rework_bypassfunction03, sScript), SQLTools_Enums.LOG_TYPEINFO.WNG);
                            break;
                        }
                    }
                }
            }
        }

        internal static SQLTools_Enums.TYPE_DATA GetSQLTypeFromLinqType(Type t, int iLenMax)
        {
            switch (t.FullName)
            {
                case "System.Boolean":
                    return SQLTools_Enums.TYPE_DATA.BOOL;
                case "System.Byte":
                    return SQLTools_Enums.TYPE_DATA.BYTE;
                case "System.Char":
                    return SQLTools_Enums.TYPE_DATA.CHAR;
                case "System.DateTime":
                    return SQLTools_Enums.TYPE_DATA.DATETIME;
                case "System.Decimal":
                    return SQLTools_Enums.TYPE_DATA.DECIMAL;
                case "System.Double":
                    return SQLTools_Enums.TYPE_DATA.DOUBLE;
                case "System.Int16":
                    return SQLTools_Enums.TYPE_DATA.SMALLINT;
                case "System.Int32":
                    return SQLTools_Enums.TYPE_DATA.INT;
                case "System.Int64":
                    return SQLTools_Enums.TYPE_DATA.INT64;
                case "System.SByte":
                    return SQLTools_Enums.TYPE_DATA.BYTEXXXZZZ;
                case "System.Single":
                    return SQLTools_Enums.TYPE_DATA.SINGLE;
                case "System.String":
                    return iLenMax > 4000 ? SQLTools_Enums.TYPE_DATA.TEXT : SQLTools_Enums.TYPE_DATA.VARCHAR;
                case "System.TimeSpan":
                    return SQLTools_Enums.TYPE_DATA.DATETIME;
                case "System.UInt16":
                    return SQLTools_Enums.TYPE_DATA.SMALLINT;
                case "System.UInt32":
                    return SQLTools_Enums.TYPE_DATA.INT;
                case "System.UInt64":
                    return SQLTools_Enums.TYPE_DATA.INT64;
                case "System.Object":
                    return iLenMax > 4000 ? SQLTools_Enums.TYPE_DATA.TEXT : SQLTools_Enums.TYPE_DATA.VARCHAR;
                default:
                    return iLenMax > 4000 ? SQLTools_Enums.TYPE_DATA.TEXT : SQLTools_Enums.TYPE_DATA.VARCHAR;
            }
        }


        //public static List<int[]> LicenseCalculator(int iActualJobsLimit)
        //{
        //    List<int> iAvailable = new List<int> { 10, 25, 50, 100, 150, 200, 250, 300 };

        //    List<int[]> iListLicenses = new List<int[]>();
        //    double dReduc = 0;
        //    int iActualBuy = 0;
        //    int iResult = 0;

        //    foreach (int iL in iAvailable)
        //    {
        //        double iMontant = iL * 3.9;

        //        iResult = Convert.ToInt32(Math.Floor(iMontant * ((100 - dReduc) / 100)));

        //        if (iActualJobsLimit >= iL) { iActualBuy = iResult; }

        //        iResult -= iActualBuy;

        //        if (iResult > 0) { iListLicenses.Add(new int[] { iL, iResult }); }

        //        //iListLicenses.Add(new int[] { iL, iResult });

        //        dReduc += 6.96;
        //    }
        //    return iListLicenses;
        //}
    }
    public static class Rfc4180Writer
    {
        public static void WriteDataTable(DataTable sourceTable, TextWriter writer, bool includeHeaders)
        {
            if (includeHeaders)
            {
                IEnumerable<String> headerValues = sourceTable.Columns
                    .OfType<DataColumn>()
                    .Select(column => QuoteValue(column.ColumnName));

                writer.WriteLine(String.Join(",", headerValues));
            }

            IEnumerable<String> items = null;

            foreach (DataRow row in sourceTable.Rows)
            {
                items = row.ItemArray.Select(o => QuoteValue(o?.ToString() ?? String.Empty));
                writer.WriteLine(String.Join(",", items));
            }

            writer.Flush();
        }

        private static string QuoteValue(string value)
        {
            return String.Concat("\"",
            value.Replace("\"", "\"\""), "\"");
        }
    }

    internal class SQLStreamingData
    {
        Stopwatch stopwatch = new Stopwatch();

        internal readonly Task Operation;
        internal readonly double ReadTime;
        internal readonly double QteRows;
        internal double ProjectedReadTime;

        internal double WriteTime { get; private set; } = 0;

        internal SQLStreamingData(double dReadingTime, int iQteRows, Task tTask)
        {
            Operation = tTask;
            ReadTime = dReadingTime;
            QteRows = iQteRows;
        }

        internal async void StartOperation()
        {
            stopwatch.Start();

            Operation.Start();

            await Operation;

            stopwatch.Stop();

            WriteTime = stopwatch.Elapsed.TotalMilliseconds;
        }

        internal int CalculateSleepTimePer100Rows(int iPriority, int iSleepRate, double dReadBatchMs)
        {
            WriteTime = stopwatch.Elapsed.TotalMilliseconds;

            int iMaxReadTime = 60;

            if (iPriority == 2) //stabilité
            {
                iMaxReadTime = 30;
            }

            TimeSpan oneMin = TimeSpan.FromSeconds(iMaxReadTime);
            double msIn1min = oneMin.TotalMilliseconds;

            double dGap = 0;
            //estimation du temps qu'il faudrait pour lire le batch en cours si la vitesse était celle qui est mesurée actuellement
            ProjectedReadTime = (dReadBatchMs * (QteRows / iSleepRate));

            if (iPriority >= 1 && WriteTime > ProjectedReadTime)
            {
                //me donne la différence entre le temps d'écriture et de lecture pour sortir un temps (en ms) de ralentissement
                dGap = (WriteTime - ProjectedReadTime) / Convert.ToDouble(QteRows / iSleepRate); //30 000 / 10 = 3000ms
            }

            //pondération du temps de ralentissement selon la priorité choisie
            if (iPriority >= 1)
            {
                if (WriteTime > msIn1min)
                {
                    if (dGap == 0)
                    {
                        dGap = (WriteTime * iSleepRate) / msIn1min;
                    }
                    else
                    {
                        double dQteRowsInLessTime = (msIn1min * QteRows) / WriteTime;
                        double dPercentInTime = dQteRowsInLessTime / QteRows; //me donne le pourcentage du total qu'il est possible de réaliser dans un temps imparti
                                                                              //si le résultat est 0,6 (on peut charger 60% du contenu dans le temps imparti) ça signifie qu'on doit ralentir d'autant le temps de lecture
                                                                              //c'est à dire le faire fonctionner à 60% de sa vitesse
                                                                              //donc ralentir chaque batch de 100/1000 lignes de 40%
                        dGap = dGap * (1.0 + (1.0 - dPercentInTime)); //soit ex : 63ms = 63ms * (1 + (1 - 0,6)) soit  88,2 ms
                    }
                }
            }
            else if (iPriority == 0)
            {
                if (ReadTime > msIn1min)
                {
                    if (dGap == 0)
                    {
                        dGap = (ReadTime * iSleepRate) / msIn1min;
                    }
                    else
                    {
                        double dQteRowsInLessTime = (msIn1min * QteRows) / ReadTime;
                        double dPercentInTime = dQteRowsInLessTime / QteRows; //me donne le pourcentage du total qu'il est possible de réaliser dans un temps imparti
                                                                              //si le résultat est 0,6 (on peut charger 60% du contenu dans le temps imparti) ça signifie qu'on doit ralentir d'autant le temps de lecture
                                                                              //c'est à dire le faire fonctionner à 60% de sa vitesse

                        dGap = dGap * (1.0 + (1.0 - dPercentInTime)); //soit ex : 63ms = 63ms * (1 + (1 - 0,6)) soit  88,2 ms
                    }
                }
            }

            return (int)Math.Floor(dGap);
        }
    }

    #endregion
}
