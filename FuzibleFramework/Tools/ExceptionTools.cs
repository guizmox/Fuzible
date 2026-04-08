using Microsoft.Data.Sqlite;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Data.Odbc;
using System.Data.OleDb;
using MySqlConnector;

namespace FuzibleFramework
{
    public static class EXCTools
    {

        public enum EXCEPTION_TYPE : int
        {
            EXCEPTION = 1,
            ODBCEXCEPTION = 2,
            SQLEXCEPTION = 3,
            MYSQLEXCEPTION = 4,
            NPGEXCEPTION = 5,
            ORACLEEXCEPTION = 7,
            SQLiteException = 8,
            ACCESSEXCEPTION = 9
        }

        public static string GetDetailledException(object ex, EXCEPTION_TYPE typeEx, string mbCalledFrom, string whichBDD, string sRequete, string eTypeQuery)
        {
            string sMessage = "";

            switch (typeEx)
            {
                case EXCEPTION_TYPE.ACCESSEXCEPTION:

                    var exceptionACCESS = (OleDbException)ex;
                    sMessage = Environment.NewLine +
                                " - Instruction: " + "-" + Environment.NewLine +
                                " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                " - Message: " + exceptionACCESS.Message + Environment.NewLine +
                                " - Error Code: " + exceptionACCESS.ErrorCode + Environment.NewLine +
                                " - Source: " + exceptionACCESS.Source + Environment.NewLine +
                                " - Target Site: " + exceptionACCESS.TargetSite;
                    break;
                case EXCEPTION_TYPE.SQLEXCEPTION:

                    var exceptionSQL = (Microsoft.Data.SqlClient.SqlException)ex;

                    string sRequeteExacte;

                    for (int i = 0; i < exceptionSQL.Errors.Count; i++)
                    {
                        try
                        { sRequeteExacte = sRequete.Split(Convert.ToChar(";"))[exceptionSQL.Errors[i].LineNumber - 1]; }
                        catch (Exception)
                        { sRequeteExacte = exceptionSQL.Errors[i].Procedure; }

                        sMessage = Environment.NewLine +
                                    " - Instruction: " + sRequeteExacte + Environment.NewLine +
                                    " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                    " - Message: " + exceptionSQL.Errors[i].Message + Environment.NewLine +
                                    " - Number: " + exceptionSQL.Errors[i].Number + Environment.NewLine +
                                    " - Source: " + exceptionSQL.Errors[i].Source + Environment.NewLine +
                                    " - Server: " + exceptionSQL.Errors[i].Server + Environment.NewLine +
                                    " - Ligne: " + exceptionSQL.Errors[i].LineNumber;
                    }
                    break;
                case EXCEPTION_TYPE.SQLiteException:

                    var exceptionSQLite = (SqliteException)ex;
                    sMessage = Environment.NewLine +
                                " - Instruction: " + "-" + Environment.NewLine +
                                " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                " - Message: " + exceptionSQLite.Message + Environment.NewLine +
                                " - Error Code: " + exceptionSQLite.ErrorCode + Environment.NewLine +
                                " - Source: " + exceptionSQLite.Source + Environment.NewLine +
                                " - SQLite Error Code: " + exceptionSQLite.SqliteErrorCode;
                    break;
                case EXCEPTION_TYPE.MYSQLEXCEPTION:

                    var exceptionMSQL = (MySqlException)ex;

                    sMessage = Environment.NewLine +
                                " - Instruction: " + "-" + Environment.NewLine +
                                " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                " - ErrorCode: " + exceptionMSQL.ErrorCode + Environment.NewLine +
                                " - Number: " + exceptionMSQL.Number + Environment.NewLine +
                                " - SQLState: " + (exceptionMSQL.SqlState ?? "") + Environment.NewLine +
                                " - Is Transient: " + exceptionMSQL.IsTransient.ToString() + Environment.NewLine +
                                " - Source: " + exceptionMSQL.Source;
                    break;
                case EXCEPTION_TYPE.ODBCEXCEPTION:

                    var exceptionODBC = (OdbcException)ex;
                    for (int i = 0; i < exceptionODBC.Errors.Count; i++)
                    {
                        sMessage = Environment.NewLine +
                                    " - Instruction: " + "-" + Environment.NewLine +
                                    " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                    " - Message: " + exceptionODBC.Errors[i].Message + Environment.NewLine +
                                    " - NativeError: " + exceptionODBC.Errors[i].NativeError + Environment.NewLine +
                                    " - Source: " + exceptionODBC.Errors[i].Source + Environment.NewLine +
                                    " - State: " + exceptionODBC.Errors[i].SQLState;
                    }
                    break;

                case EXCEPTION_TYPE.NPGEXCEPTION:

                    var exceptionNPG = (NpgsqlException)ex;
                    sMessage = Environment.NewLine +
                                " - Instruction: " + "-" + Environment.NewLine +
                                " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                " - Message: " + exceptionNPG.Message + Environment.NewLine +
                                " - ErrorCode: " + exceptionNPG.ErrorCode + Environment.NewLine +
                                " - Is Transient: " + exceptionNPG.IsTransient.ToString() + Environment.NewLine +
                                " - Source: " + exceptionNPG.Source;
                    break;
                case EXCEPTION_TYPE.ORACLEEXCEPTION:

                    var exceptionORACLE = (OracleException)ex;
                    System.Text.StringBuilder sbDataO = new();

                    if (exceptionORACLE.Data.Count > 0)
                    {
                        foreach (DictionaryEntry de in exceptionORACLE.Data)
                        { sbDataO.AppendLine(string.Concat("{Key:", de.Key.ToString().Trim(), "}", "{Value:", de.Value.ToString().Trim(), "}")); }
                    }
                    sMessage = Environment.NewLine +
                                " - Instruction: " + "-" + Environment.NewLine +
                                " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery + Environment.NewLine +
                                " - Message: " + exceptionORACLE.Message + Environment.NewLine +
                                " - Data: " + sbDataO.ToString() + Environment.NewLine +
                                " - Source: " + exceptionORACLE.Source + Environment.NewLine +
                                " - Procedure: " + exceptionORACLE.Procedure + Environment.NewLine +
                                " - Is Recoverable: " + exceptionORACLE.IsRecoverable.ToString() + Environment.NewLine +
                                " - Number: " + exceptionORACLE.Number.ToString() + Environment.NewLine +
                                " - ErrorCode: " + exceptionORACLE.ErrorCode;
                    break;
                case EXCEPTION_TYPE.EXCEPTION:

                    var exception = (Exception)ex;
                    {
                        System.Text.StringBuilder sbData = new();

                        if (exception.Data.Count > 0)
                        {
                            foreach (DictionaryEntry de in exception.Data)
                            {
                                sbData.AppendLine(string.Concat("{Key:", de.Key.ToString().Trim(), "}", "{Value:", de.Value.ToString().Trim(), "}"));
                            }
                        }

                        sMessage = Environment.NewLine +
                                    " - Message: " + exception.Message + Environment.NewLine +
                                    " - TargetSite: " + exception.TargetSite + Environment.NewLine +
                                    " - Source: " + exception.Source + Environment.NewLine +
                                    " - StackFrame:" + exception.StackTrace + Environment.NewLine +
                                    " - Data: " + sbData.ToString() + Environment.NewLine +
                                    " - Additionnal Data: " + sRequete + Environment.NewLine +
                                    " - Parameters: " + mbCalledFrom + " - " + whichBDD + " - " + eTypeQuery;
                    }
                    break;
            }

            return sMessage;

        }

    }
}
