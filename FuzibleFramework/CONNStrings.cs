using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FuzibleFramework
{

    public class CONNStrings
    {
        #region "VARIABLES"

        private readonly string USER = "";

        private string FUZIBLE_SERVER
        {
            get; set;
        }
        private readonly List<CONNString> _sListConn = new();
        #endregion

        #region "PROPRIETES"

        public List<CONNString> CSList
        {
            get
            {
                return _sListConn.OrderBy(c => c.SConnDriver).ThenBy(c => c.SConnRawID).ToList();
            }
        }

        #endregion

        #region "PUBLIC VOID"

        public string AddCONNString(SQLTools_Enums.BDD sConnDriver, string sConnName, string sConnString, List<string> sConnParams)
        {
            sConnParams = sConnParams.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            sConnParams = sConnParams.Where(s => s.Contains('=')).ToList();

            //correctifs de saisie
            if (sConnDriver.ToString().StartsWith("FI_") && Directory.Exists(sConnString) && !sConnString.EndsWith("\\"))
            {
                sConnString = string.Concat(sConnString, "\\");
            }

            //nettoyage genre : Database=test; UserId=bonjour;password=akjzhkejahze; Integrated Security=true
            sConnString = Regex.Replace(sConnString, "(; )([A-z0-9-_]+)", ";$2");

            //retourne un message contenant le nouvel ID
            string sNewID;

            try
            {
                sNewID = GetNewConnID();

                //pas encore de connexions existantes
                //if (sNewID.Length == 0) { sNewID = "[1]"; }

                if (sNewID.Length > 0)
                {
                    try
                    {
                        StringBuilder sbQuery = new();
                        sbQuery.Append("INSERT INTO user_connstrings (\"user\",\"connstring_id\",\"connstring_name\",\"connstring\",\"connstring_driver\") VALUES (");
                        sbQuery.Append(string.Concat("'", USER, "',"));
                        sbQuery.Append(string.Concat("'", sNewID, "',"));
                        sbQuery.Append(string.Concat("'", sConnName, "',"));
                        sbQuery.Append(string.Concat("'", FITools.EncryptionSystem.AES_Encrypt(sConnString, SHSConstantes.PROGRAM_PWD + USER.ToLower()), "',"));
                        sbQuery.Append(string.Concat("'", sConnDriver.ToString(), "');"));

                        try
                        {
                            //insertion nouvelle connexion
                            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                            {
                                sqlConn.Open();
                                using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                                sqlCommand.ExecuteNonQuery();
                            }
                            //insertion paramètres optionnels
                            sbQuery.Clear();
                            foreach (string sP in sConnParams)
                            {
                                sbQuery.Append("INSERT INTO user_connstrings_params (\"user\",\"connstring_id\",\"param_name\",\"param_value\") VALUES (");
                                sbQuery.Append(string.Concat("'", USER, "',"));
                                sbQuery.Append(string.Concat("'", sNewID, "',"));
                                sbQuery.Append(string.Concat("'", sP[..sP.IndexOf("=")], "',"));
                                sbQuery.Append(string.Concat("'", FITools.EncryptionSystem.AES_Encrypt(sP[(sP.IndexOf("=") + 1)..], SHSConstantes.PROGRAM_PWD + USER.ToLower()), "');"));
                                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                                {
                                    sqlConn.Open();
                                    using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                                    sqlCommand.ExecuteNonQuery();
                                }
                                sbQuery.Clear();
                            }
                            //seulement là on peut créer la connexion
                            CONNString CS = new(sConnDriver, sNewID, sConnName, sConnString, sConnParams);
                            _sListConn.Add(CS);
                        }
                        catch (Exception ex) { sNewID = Languages.Languages.par_conn_cantcreatenewconnparams + ex.Message; }
                    }
                    catch (Exception ex)
                    {
                        sNewID = Languages.Languages.par_conn_cantcreatenewconn + ex.Message;
                    }
                }
                else { sNewID = Languages.Languages.par_conn_cantcreatenewconnid; }
            }
            catch (Exception ex) { sNewID = Languages.Languages.par_conn_cantgetdatafromdb + ex.Message; }

            return sNewID;
        }

        public string RemoveCONNString(string sConnID)
        {
            if (GetIndexSection(sConnID) > -1)
            {
                try
                {
                    StringBuilder sbQuery = new();
                    sbQuery.Append("DELETE FROM user_connstrings WHERE \"user\" = '" + USER + "' AND \"connstring_id\" = '" + sConnID + "';");
                    //sbQuery.Append(" DELETE FROM user_connstrings_params WHERE \"user\" = '" + USER + "' AND \"connstring_id\" = '" + sConnID + "';");

                    using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    _sListConn.RemoveAt(GetIndexSection(sConnID));

                    return "OK";
                }
                catch (Exception ex) { return "KO : " + ex.Message; }

            }
            else { return "KO"; }
        }

        public string ModifyCONNString(string sConnID, string sConnName, string sConnString, SQLTools_Enums.BDD sConnDriver, List<string> sConnParams)
        {
            sConnParams = sConnParams.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            sConnParams = sConnParams.Where(s => s.Contains('=')).ToList();

            sConnString = Regex.Replace(sConnString, "(; )([A-z0-9-_]+)", ";$2");

            if (GetIndexSection(sConnID) > -1)
            {
                try
                {
                    //correctifs de saisie
                    if (sConnDriver.ToString().StartsWith("FI_") && Directory.Exists(sConnString) && !sConnString.EndsWith("\\"))
                    {
                        sConnString = string.Concat(sConnString, "\\");
                    }

                    StringBuilder sbQuery = new();
                    sbQuery.Append("UPDATE user_connstrings SET ");
                    sbQuery.Append(string.Concat("\"connstring_name\" = '", sConnName.Replace("'", "''"), "',"));
                    sbQuery.Append(string.Concat("\"connstring\" = '", FITools.EncryptionSystem.AES_Encrypt(sConnString, SHSConstantes.PROGRAM_PWD + USER.ToLower()), "',"));
                    sbQuery.Append(string.Concat("\"connstring_driver\" = '", sConnDriver.ToString(), "'"));
                    sbQuery.Append(string.Concat(" WHERE \"user\" = '", USER, "' AND \"connstring_id\" = '", sConnID, "';"));

                    sbQuery.AppendLine(string.Concat("DELETE FROM user_connstrings_params WHERE \"user\" = '", USER, "' AND \"connstring_id\" = '", sConnID, "';"));

                    if (sConnParams.Count == 0)
                    {
                        sConnParams = SQLQueries.ParamsForSQLDriver(sConnDriver, "");
                    }

                    foreach (string sP in sConnParams)
                    {
                        sbQuery.AppendLine("INSERT INTO user_connstrings_params (\"user\", \"connstring_id\", \"param_name\", \"param_value\") VALUES (");
                        sbQuery.Append(string.Concat("'", USER, "',"));
                        sbQuery.Append(string.Concat("'", sConnID, "',"));
                        sbQuery.Append(string.Concat("'", sP[..sP.IndexOf("=")], "',"));
                        sbQuery.Append(string.Concat("'", FITools.EncryptionSystem.AES_Encrypt(sP[(sP.IndexOf("=") + 1)..], SHSConstantes.PROGRAM_PWD + USER.ToLower()), "');"));
                    }
                    using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                    {
                        sqlConn.Open();
                        using SqliteCommand sqlCommand = new(sbQuery.ToString(), sqlConn);
                        sqlCommand.ExecuteNonQuery();
                    }

                    _sListConn[GetIndexSection(sConnID)].SConnName = sConnName;
                    _sListConn[GetIndexSection(sConnID)].ReplaceConnString(sConnString);
                    _sListConn[GetIndexSection(sConnID)].SConnDriver = sConnDriver;
                    _sListConn[GetIndexSection(sConnID)].SConnParams = sConnParams;

                    return "OK";
                }
                catch (Exception ex) { return "KO : " + ex.Message; }

            }
            else { return "KO"; }
        }

        public CONNString GetConnByConnString(string sConnString)
        {

            foreach (CONNString CS in _sListConn)
            {
                if (CS.SConnString(null).Equals(sConnString, StringComparison.InvariantCultureIgnoreCase))
                {
                    return CS;
                }
            }

            return null;
        }

        public CONNString GetConnByID(string sConnID)
        {
            if (GetIndexSection(sConnID) > -1)
            {
                CONNString CS = _sListConn[GetIndexSection(sConnID)];
                return CS;
            }
            else { return null; }
        }

        public List<string> CheckConnStringUsage(string sConnID)
        {
            //source, target, dyn params, queries
            List<string> sListUsage = new();
            DataSet dsData = new();
            try
            {
                string sQuery = "select jp.job_id, acss.connexion_string_export, acst.connexion_string_import, acppcs.post_job_sql_command_source_conn, acppct.post_job_sql_command_target_conn, adq.source_queries_dynamic_parameters, jq.query" +
                                " from job_parameters jp" +
                                " left join job_parameters acss ON jp.job_id = acss.job_id AND jp.\"user\" = acss.\"user\" AND acss.connexion_string_export = '" + sConnID + "'" +
                                " left join job_parameters acst ON jp.job_id = acst.job_id AND jp.\"user\" = acst.\"user\" AND acst.connexion_string_import = '" + sConnID + "'" +
                                " left join job_parameters acppcs ON jp.job_id = acppcs.job_id AND jp.\"user\" = acppcs.\"user\" AND acppcs.post_job_sql_command_source_conn = '" + sConnID + "'" +
                                " left join job_parameters acppct ON jp.job_id = acppct.job_id AND jp.\"user\" = acppct.\"user\" AND acppct.post_job_sql_command_target_conn = '" + sConnID + "'" +
                                " left join job_parameters adq ON jp.job_id = adq.job_id AND jp.\"user\" = adq.\"user\" AND adq.source_queries_dynamic_parameters LIKE '%" + sConnID + "%'" +
                                " left join job_queries jq on jp.\"user\" = jq.\"user\" and jp.job_id = jq.job_id AND jq.query LIKE '%" + sConnID + "%'" +
                                " where jp.\"user\" = '" + USER + "'" +
                                " and(acss.job_id IS NOT NULL OR acst.job_id IS NOT NULL OR acppcs.job_id IS NOT NULL OR acppct.job_id IS NOT NULL OR adq.job_id IS NOT NULL OR jq.job_id IS NOT NULL)";
                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Conn Strings Usage");
                }
                if (dsData != null && dsData.Tables.Count > 0 && dsData.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in dsData.Tables[0].Rows)
                    {
                        StringBuilder sbJob = new();
                        string sSource = dr[1].ToString();
                        string sTarget = dr[2].ToString();
                        string sPrePostSource = dr[3].ToString();
                        string sPrePostTarget = dr[4].ToString();
                        string sDynParam = dr[5].ToString();
                        string sQueries = dr[6].ToString();
                        sbJob.Append("JOB " + dr[0].ToString() + " : ");
                        if (sSource.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagesource); }
                        if (sTarget.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagetarget); }
                        if (sPrePostSource.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagesourceprepost); }
                        if (sPrePostTarget.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagetargetprepost); }
                        if (sDynParam.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagedynparams); }
                        if (sQueries.Length > 0)
                        { sbJob.Append(Languages.Languages.par_conn_usagequeries); }
                        sListUsage.Add(sbJob.ToString());
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return sListUsage;
        }

        public int GetIndexSection(string sConnID)
        {
            return _sListConn.FindIndex(s => s.SConnID.Equals(sConnID));
        }

        #endregion

        #region "PRIVATE VOID"

        private string GetNewConnID()
        {
            //rechercher d'abord un trou dans les séquences
            var iConns = _sListConn.Select(j => j.SConnRawID).Distinct().ToList();
            iConns.Sort();

            for (int iC = 1; iC < iConns.Count; iC++)
            {
                if (iC != iConns[iC - 1])
                {
                    return string.Concat("[", iC.ToString(), "]");
                }
            }

            int iMaxID = _sListConn.Count > 0 ? _sListConn.Max(j => j.SConnRawID) : 0;
            return string.Concat("[", (iMaxID + 1).ToString(), "]");
        }

        internal CONNStrings(string sServer, string sUser, bool bReadOnly)
        {
            USER = sUser;
            FUZIBLE_SERVER = sServer;
            LoadConnections(bReadOnly);
        }

        public CONNStrings(string sUser)
        {
            USER = sUser;
            LoadConnections(true);
        }

        internal void LoadConnections(bool bReadOnly)
        {
            DataSet dsData = new();
            DataSet dsDataParams = new();

            try
            {
                string sQuery = "";
                sQuery = string.Concat("SELECT * FROM user_connstrings WHERE \"user\" = '", USER, "'", " ORDER BY \"connstring_id\" ASC;");

                using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                {
                    sqlConn.Open();
                    using SqliteCommand sqlCommand = new(sQuery, sqlConn);
                    SqliteDataAdapter SQLAdapter = new(sqlCommand);
                    SQLAdapter.Fill(dsData, "Conn Strings");
                }
                if (dsData != null && dsData.Tables.Count > 0)
                {
                    if (dsData.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow dr in dsData.Tables[0].Rows)
                        {
                            string sConnID = dr["connstring_id"].ToString();
                            string sConnName = dr["connstring_name"].ToString();
                            string sConnString = FITools.EncryptionSystem.AES_Decrypt(dr["connstring"].ToString(), SHSConstantes.PROGRAM_PWD + USER.ToLower());
                            var sConnDriver = (SQLTools_Enums.BDD)Enum.Parse(typeof(SQLTools_Enums.BDD), dr["connstring_driver"].ToString());

                            List<string> sListParams = new();
                            dsDataParams.Clear();
                            string sQueryParams = string.Concat("SELECT * FROM \"user_connstrings_params\" WHERE \"user\" = '", USER, "' AND \"connstring_id\" = '", sConnID, "';");
                            using (SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB))
                            {
                                sqlConn.Open();
                                using SqliteCommand sqlCommand = new(sQueryParams, sqlConn);
                                SqliteDataAdapter SQLAdapter = new(sqlCommand);
                                SQLAdapter.Fill(dsDataParams, "Conn Strings Params");
                            }
                            if (dsDataParams != null && dsDataParams.Tables.Count > 0)
                            {
                                if (dsDataParams.Tables[0].Rows.Count > 0)
                                {
                                    foreach (DataRow drP in dsDataParams.Tables[0].Rows)
                                    {
                                        sListParams.Add(string.Concat(drP["param_name"].ToString(), "=", FITools.EncryptionSystem.AES_Decrypt(drP["param_value"].ToString(), SHSConstantes.PROGRAM_PWD + USER.ToLower())));
                                    }
                                }
                            }
                            CONNString CS = new(sConnDriver, sConnID, sConnName, sConnString, sListParams);
                            _sListConn.Add(CS);
                        }
                    }
                    else
                    {
                        if (!bReadOnly)
                        {
                            string sFuzibleServer = FUZIBLE_SERVER;
                            string sDir = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\Demo.db";

                            //création du fichier SQLite
                            try
                            {
                                if (!System.IO.File.Exists(sDir))
                                {
                                    File.WriteAllBytes(sDir, new byte[0]);
                                }

                                using SqliteConnection sqlConn = new(INIProgram.INTERNAL_DB);
                                sqlConn.Open();
                                sqlConn.Close();
                            }
                            catch { }

                            AddCONNString(SQLTools_Enums.BDD.FI_CSV, Languages.Languages.par_conn_localpath, System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\", new List<string>());
                            AddCONNString(SQLTools_Enums.BDD.FI_XLS, Languages.Languages.par_conn_localpath, System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\", new List<string>());
                            AddCONNString(SQLTools_Enums.BDD.FI_XML, Languages.Languages.par_conn_localpath, System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\", new List<string>());
                            AddCONNString(SQLTools_Enums.BDD.FI_JSON, Languages.Languages.par_conn_localpath, System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\Fuzible" + "\\FILES\\", new List<string>());
                            //creation nouvelle DB
                            AddCONNString(SQLTools_Enums.BDD.DB_SQLITE, Languages.Languages.par_conn_localsqlite, "Data Source=" + sDir + ";Foreign Keys=true", SQLQueries.ParamsForSQLDriver(SQLTools_Enums.BDD.DB_SQLITE, ""));

                            //ws demo
                            if (sFuzibleServer.EndsWith("/")) { sFuzibleServer = sFuzibleServer[0..^1]; }
                            List<string> sListParams = new()
                            {
                                string.Concat("P_WS_PROXY_URL=", ""),
                                string.Concat("P_WS_PROXY_PORT=", ""),
                                string.Concat("P_WS_HEADERS=", ""),
                                string.Concat("P_WS_AUTH_METHOD=", "API_AUTH_HEADER"),
                                string.Concat("P_WS_AUTH_KEY=", "Authorization"),
                                string.Concat("P_WS_AUTH_VALUE=", "e5d80f61-fa63-430c-882d-c3074150376b"),
                                string.Concat("P_WS_AUTH_URL=", ""),
                                string.Concat("P_WS_QUERY_PARAMS=", ""),
                                string.Concat("P_WS_TEMPLATE=", "DEFAULT")
                            };
                            AddCONNString(SQLTools_Enums.BDD.WS_REST, Languages.Languages.par_conn_wsdemo, sFuzibleServer, sListParams);
                        }
                    }
                }
            }
            catch (Exception) { throw; }
        }

        internal void SwitchConnID(string sConnID1, CONNString sConn2)
        {
            int iIdx = GetIndexSection(sConnID1);
            if (iIdx > -1)
            {
                _sListConn.RemoveAt(iIdx); _sListConn.Add(sConn2);
            }
            else
            {
                _sListConn.Add(sConn2);
            }
        }

        #endregion

    }

}
