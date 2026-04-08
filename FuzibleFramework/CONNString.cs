using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FuzibleFramework
{
    public class CONNString
    {
        #region "VARIABLES"

        private string _sConn_String = "";

        #endregion

        #region "PROPRIETES"
        public string SConnSQLLangage
        {
            get
            {
                return SConnDriver switch
                {
                    SQLTools_Enums.BDD.DB_ACCESS => "Access SQL",
                    SQLTools_Enums.BDD.DB_MYSQL => "MySQL SQL",
                    SQLTools_Enums.BDD.DB_ODBC => "ODBC-related SQL",
                    SQLTools_Enums.BDD.DB_ORACLE => "PL-SQL",
                    SQLTools_Enums.BDD.DB_SQLITE => "SQLite SQL",
                    SQLTools_Enums.BDD.DB_SQLSERVER => "Transact-SQL",
                    _ => "Fuzible SQL",
                };
            }
        }
        public int SConnRawID
        {
            get
            {
                return Convert.ToInt16(SConnID[1..^1]);
            }
        }
        public string SConnID { get; private set; } = "";
        public string SRawConnID
        {
            get
            {
                return SConnID.Replace("[", "").Replace("]", "");
            }
        }
        public string SConnName { get; set; } = "";
        public SQLTools_Enums.BDD SConnDriver { get; set; } = SQLTools_Enums.BDD.DB_SQLSERVER;
        public string SConnDriverSuffix
        {
            get;
        }
        public string SConnDriverFriendlyName
        {
            get
            {
                return SConnDriver switch
                {
                    SQLTools_Enums.BDD.AD_ACTIVEDIRECTORY => "Active Directory",
                    SQLTools_Enums.BDD.DB_MYSQL => "MySQL/MariaDB Database",
                    SQLTools_Enums.BDD.DB_ODBC => "Odbc Database",
                    SQLTools_Enums.BDD.DB_ORACLE => "Oracle Database",
                    SQLTools_Enums.BDD.DB_POSTGRE => "Postgres Database",
                    SQLTools_Enums.BDD.DB_SQLSERVER => "SQL Server Database",
                    SQLTools_Enums.BDD.DB_SQLITE => "Sqlite Database",
                    SQLTools_Enums.BDD.DB_ACCESS => "MS Access Database",
                    SQLTools_Enums.BDD.NS_MONGODB => "MongoDB Database",
                    SQLTools_Enums.BDD.FI_CSV => "CSV File",
                    SQLTools_Enums.BDD.FI_FILE => "File",
                    SQLTools_Enums.BDD.FI_XLS => "Excel File",
                    SQLTools_Enums.BDD.FI_XML => "XML File",
                    SQLTools_Enums.BDD.FI_JSON => "JSON File",
                    SQLTools_Enums.BDD.MB_MAIL => "Mailbox",
                    SQLTools_Enums.BDD.WS_REST => "API REST",
                    _ => "Unknown",
                };
            }
        }
        public string SConnDriverSuffixFriendlyName
        {
            get
            {
                return SConnDriverSuffix switch
                {
                    "FI" => "File",
                    "DB" => "Database",
                    "NS" => "NOSQL DB",
                    "MB" => "Mailbox",
                    "AD" => "Active Directory",
                    "WS" => "Webservice",
                    _ => "Unknown",
                };
            }
        }
        public string SConnDB
        {
            get
            {
                return Toolbox.GetDatabaseNameFromConnectionString(_sConn_String, SConnDriver);
            }
        }
        public List<string> SConnParams = new();
        public SQLTools_Enums.TYPE_DATA SqlDateTypeCompatibility
        {
            get;
            private set;
        } = SQLTools_Enums.TYPE_DATA.DATE;
        public SQLTools_Enums.TYPE_DATA SqlCharTypeCompatibility
        {
            get;
            private set;
        } = SQLTools_Enums.TYPE_DATA.VARCHAR;
        public string SqlEchappementChar
        {
            get
            {
                string sEscapeChar = GetParam(SQLTools_Enums.DRIVER_PARAMS.ESCAPE_CHAR);
                //if (sEscapeChar.Length == 0) { sEscapeChar = "\""; }
                return sEscapeChar;
            }
        }
        public string SqlDateFormat
        {
            get
            {
                return SConnDriver switch
                {
                    SQLTools_Enums.BDD.DB_ORACLE => "alter SESSION set NLS_DATE_FORMAT = 'DD-MM-YYYY HH24:MI:SS'",
                    SQLTools_Enums.BDD.DB_SQLSERVER => "SET DATEFORMAT DMY;",
                    SQLTools_Enums.BDD.DB_POSTGRE => "SET datestyle = \"ISO, DMY\";",
                    _ => "",
                };
            }
        }
        public bool DecimalLocale
        {
            get;
            private set;
        } = Convert.ToChar(INIProgram.SYSTEM_CULTURE_NUMBER.NumberDecimalSeparator).Equals(','); //true=, / false=.
        public bool DateLocale
        {
            get;
            private set;
        } = INIProgram.SYSTEM_CULTURE_DATE.ShortDatePattern.Equals("dd/MM/yyyy"); //true=fr / false=en

        #endregion

        #region "PUBLIC VOID"

        public CONNString(SQLTools_Enums.BDD _bDD, string _sConnID, string _sConnName, string _sConnString, List<string> _sConnParams = null)
        {
            _sConn_String = Regex.Replace(_sConnString, "(; )([A-z0-9-_]+)", ";$2");

            //2023 : changement de driver SQLITE
            if (_bDD == SQLTools_Enums.BDD.DB_SQLITE && _sConn_String.IndexOf("Version=3;", StringComparison.OrdinalIgnoreCase) > 0)
            {
                _sConn_String = _sConn_String.Replace("Version=3;", "", StringComparison.OrdinalIgnoreCase);
            }

            SConnID = _sConnID;
            SConnName = _sConnName;
            SConnDriver = _bDD;
            SConnDriverSuffix = SConnDriver.ToString()[0..2];
            if (_sConnParams != null)
            {
                SConnParams = _sConnParams;

                DecimalLocale = Convert.ToChar(GetParam(SQLTools_Enums.DRIVER_PARAMS.DRIVER_DECIMALS_LOCALE)).Equals(',');
                DateLocale = GetParam(SQLTools_Enums.DRIVER_PARAMS.DRIVER_DATE_LOCALE).Equals("dd/MM/yyyy");

                string sDateType = GetParam(SQLTools_Enums.DRIVER_PARAMS.DATE_FORMAT);
                if (sDateType.Length == 0) { sDateType = "DATETIME"; }
                SqlDateTypeCompatibility = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sDateType.ToUpper());

                string sCharType = GetParam(SQLTools_Enums.DRIVER_PARAMS.STRING_FORMAT);
                if (sCharType.Length == 0) { sCharType = "VARCHAR"; }
                SqlCharTypeCompatibility = (SQLTools_Enums.TYPE_DATA)Enum.Parse(typeof(SQLTools_Enums.TYPE_DATA), sCharType.ToUpper());

            }
        }

        public override string ToString()
        {
            return SConnID;
        }

        public string SConnString(List<string> sDynamicVariables)
        {
            if (sDynamicVariables != null)
            {
                return sDynamicVariables.Count > 0 ? Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(_sConn_String, sDynamicVariables) : _sConn_String;
            }
            else { return _sConn_String; }
        }

        public string GetParam(SQLTools_Enums.DRIVER_PARAMS sSection)
        {
            string sQ = "";
            foreach (string sP in SConnParams)
            {
                if (sP.IndexOf(string.Concat(sSection.ToString(), "=")) == 2)
                {
                    sQ = sP[(sP.IndexOf("=") + 1)..];
                    break;
                }
            }

            if (sQ.Length == 0 && sSection == SQLTools_Enums.DRIVER_PARAMS.DRIVER_DECIMALS_LOCALE)
            {
                sQ = INIProgram.SYSTEM_CULTURE_NUMBER.NumberDecimalSeparator;
            }
            if (sQ.Length == 0 && sSection == SQLTools_Enums.DRIVER_PARAMS.DRIVER_DATE_LOCALE)
            {
                sQ = INIProgram.SYSTEM_CULTURE_DATE.ShortDatePattern; if (sQ.Equals("M/d/yyyy", StringComparison.Ordinal)) { sQ = "MM/dd/yyyy"; }
            }
            if (sQ.Length == 0 && sSection == SQLTools_Enums.DRIVER_PARAMS.CREATE_FROM_SELECT)
            {
                sQ = SQLQueries.GetQuery_CreateFromSelect(SConnDriver);
            }

            return sQ;
        }

        #endregion

        #region "PRIVATE VOID"

        internal void ReplaceConnString(string sConnString)
        {
            _sConn_String = sConnString;
        }

        internal void RewriteConnID(string sConnID2)
        {
            SConnID = sConnID2;
        }

        internal string ExportConnStringAsXML(string sTypeConn, string sPassword)
        {
            StringBuilder sbCS = new();
            sbCS.AppendLine("\t<Conn" + sTypeConn + ">");
            sbCS.AppendLine("\t\t<ID>" + SConnID + "</ID>");
            sbCS.AppendLine("\t\t<Name>" + SConnName + "</Name>");
            sbCS.AppendLine("\t\t<Driver>" + SConnDriver.ToString() + "</Driver>");
            sbCS.AppendLine("\t\t<Database>" + SConnDB + "</Database>");
            sbCS.AppendLine("\t\t<String>" + FITools.EncryptionSystem.AES_Encrypt(_sConn_String, sPassword) + "</String>");
            sbCS.AppendLine("\t\t<ParamCount>" + SConnParams.Count.ToString() + "</ParamCount>"); ;

            for (int i = 0; i < SConnParams.Count; i++)
            {
                sbCS.AppendLine("\t\t<Param" + (i + 1).ToString() + ">" + FITools.EncryptionSystem.AES_Encrypt(SConnParams[i], sPassword) + "</Param" + (i + 1).ToString() + ">");
            }
            //sbCS.AppendLine("\t\t<SqlDate>" + SqlDateTypeCompatibility + "</SqlDate>");
            //sbCS.AppendLine("\t\t<SQLChar>" + SqlCharTypeCompatibility + "</SQLChar>");
            //sbCS.AppendLine("\t\t<EchapChar>" + SqlEchappementChar + "</EchapChar>");
            sbCS.AppendLine("\t</Conn" + sTypeConn + ">");

            return sbCS.ToString().TrimEnd();
        }

        #endregion

    }

}
