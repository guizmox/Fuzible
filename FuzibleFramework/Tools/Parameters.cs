using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml;

namespace FuzibleFramework
{
    public static class SHSRegex
    {
        public const string REGEX_DYNPARAM = "{\\?\\d}";
        public const string REGEX_STARTQUERY = "\\n*.+:\\s*SELECT.*";
        public const string REGEX_STARTQUERY_PAR = "(\\n*.+:)(\\s*SELECT.*)";
        //public static string REGEX_STARTQUERY_B = "\\n.+\\s*:\\s*SELECT";
        public const string REGEX_CROSSJOINSCRIPT = "\\[..\\[[0-9]+(\\]\\]|\\]WHERE .[^\\]]+\\])\\s+SELECT";
        public const string REGEX_CROSSJOINSCRIPT_B = "\\[[<-]{1}[^><-]*[->]{1}\\[[0-9]+(\\]\\]|\\]WHERE .[^\\]]+\\])";
        public const string REGEX_FIELD_ALIAS = "(([a-zA-Z_]+)?(\\d+)?[a-zA-Z_]+(\\d+)?)\\.([A-Za-z0-9_* ]+)";
        public const string REGEX_FIELD_FUNC = "(?=[^a-zA-Z]?)\\w+\\(";
        public const string REGEX_QUERY_TOP = "TOP\\s+(PERCENT\\s+)?(\\d+)\\s+";
        public const string REGEX_QUERY_BOTTOM = "LIMIT\\s+(\\d+)";
        public const string REGEX_QUERY_TABLE = "(TABLE){1}\\s+(\\d+){1}\\s+(ONLY\\s*)?";
        public const string REGEX_VALID_SQL_TABLE = "[\\['\"]?[\\d\\w_.]+['\"\\]]?";
        public const string REGEX_WHITESPACES = @"\t|\n|\r";
        //public const string REGEX_SQL_SEPARATORS = "[\\[\\]'\"`]";
        public const string REGEX_SQL_SEPARATORS = "[\\[\\]\"`]";
        public const string REGEX_SQL_FIELD = "\\w";
        public const string REGEX_JSON_PATTERN = @"[{,][\w\/()]+\s*:\s*[\w\/()]+[^:][,}]";
        public const string REGEX_ISINTEGER = @"^[-]?[\d ]+[ ]*$"; //supporte nombre entier ou bien 1 5654 ou bien 4 564 , 00
        public const string REGEX_ISINTEGER_EXTENDED = @"^[-]?[\d ]+[ ]*[,.]{1}[ 0]+$";
        public const string REGEX_FILE_OUTPUT_PATTERN = "\\[[\\d\\w\\s_%{}()\\-'\"|&+=!?]+\\]";
        public const string SQL_COMMENT_A = "--.*\\n";
        public const string SQL_COMMENT_B = "\\/\\*[\\s\\S][^(\\*\\/)]*\\*\\/";
        public const string REGEX_BASIC_SUBQUERY = "\\(\\s*SELECT\\s*.*\\s*FROM\\s*.*\\s*\\)";
        public const string REGEX_BASIC_QUERY = "^SELECT\\s+.+\\s+FROM\\s+(.+)(.[^><=!?()])\\s*$";
        public const string REGEX_BASIC_QUERY_AGG = "^SELECT\\s+(.*)(SUM\\(|AVG\\(|MIN\\(|MAX\\(|COUNT\\()+(.+)\\s+FROM\\s+(.+)(.[^><=!?()])\\s*$";
        public const string REGEX_ISINTEGER_STARTSWITH_0 = @"^[0]{1}\d+$";
        public const string REGEX_SQL_FIELD_EMBRACE = "^([\"'`\\[]).+([\"'`\\]])$";
        public const string REGEX_FUNC_FIELDS_EMBRACE = "(?:,|\\n|^)([\"`\\[](?:(?:[\"`\\[][\"`\\[])*[^\"`\\]]*)*[\"`\\]]|[^\"`\\],\\n]*|(?:\\n|$))";
        public const string REGEX_FUNC_FIELDS_EMBRACE_REPLACE = "(?:,|\\n|^)([`\\[](?:(?:[`\\[][`\\[])*[^`\\]]*)*[`\\]]|[^`\\],\\n]*|(?:\\n|$))";
        public const string REGEX_SPLIT_DYNPARAMS = "{#};{#}";
    }

    public static class SHSConstantes
    {
        public static readonly string PROGRAM_PWD = "iddad";
        public static readonly string DEV_MAIL = "guizmox@hotmail.com";
        internal static readonly List<string> JOIN_SEPARATORS = new() { "<>", ">=", "<=", "=>", "=<", "!=", "=!", "LIKE", "NOT LIKE", "=", "<", ">", "NOT IN", "IN" };
        internal static readonly List<string> SQL_FUNCTIONS = new() { "SUM", "AVG", "MIN", "MAX", "COUNT", "CONCAT", "SUBSTRING", "CHARINDEX", "ISNULL", "COALESCE", "TRIM", "LTRIM", "RTRIM", "LPAD", "RPAD", "REPLACE", "UPPER", "LOWER", "LENGTH", "CONVERT", "ANONYMIZE" };
        internal static readonly List<string> SQL_AGGREGATE_FUNCTIONS = new() { "SUM", "AVG", "MIN", "MAX", "COUNT" };
    }

    public class ProgramHelp
    {

        #region "VARIABLES"

        private readonly List<BlockHelp> _sListBlockHelp = new();

        #endregion

        #region "PUBLIC_VOID"

        public ProgramHelp(string sLanguage)
        {
            DataSet dsHelp = new();

            try
            {
                XmlDocument xdc = new();
                xdc.LoadXml(Properties.Resources.QUICKHELP);
                XmlNodeList xnlNodes = xdc.SelectNodes("helpfile");

                foreach (XmlNode xnlNode in xnlNodes.Item(0).ChildNodes)
                {
                    if (xnlNode.Name.Equals("help_" + sLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        XmlTextReader xtr = new(xnlNode.OuterXml, XmlNodeType.Element, null);
                        dsHelp.ReadXml(xtr);
                    }
                }
                //dsHelp.ReadXml(_sHelpFile, XmlReadMode.Auto); 

            }
            catch (Exception)
            { throw; }

            if (dsHelp.Tables.Count > 0 && dsHelp.Tables[0].Rows.Count > 0)
            {
                foreach (DataTable dT in dsHelp.Tables)
                {
                    foreach (DataRow dR in dT.Rows)
                    {
                        string sName = "";
                        string sContent = "";
                        string sExemples = "";
                        try { sName = dR["Name"].ToString().Trim(); }
                        catch { }
                        try { sContent = dR["Content"].ToString(); }
                        catch { }
                        try { sExemples = dR["Exemples"].ToString(); }
                        catch { }
                        _sListBlockHelp.Add(new BlockHelp("Parameters", sName, sContent, sExemples));
                    }
                }
            }
        }

        public BlockHelp GetHelpBlock(string sBlockName)
        {
            int iIdx;
            iIdx = _sListBlockHelp.IndexOf(sBlockName);
            if (iIdx > -1)
            {
                string sXML = _sListBlockHelp[iIdx].BlockContent;
                sXML = sXML.Replace("&lt;", "<");
                sXML = sXML.Replace("&amp;", "&");
                sXML = sXML.Replace("&gt;", ">");
                sXML = sXML.Replace("&quot;", "\"");
                sXML = sXML.Replace("&apos;", "'");
                sXML = sXML.Replace("\t", "");

                string sExemples = _sListBlockHelp[iIdx].BlockExemples;
                sExemples = sExemples.Replace("&lt;", "<");
                sExemples = sExemples.Replace("&amp;", "&");
                sExemples = sExemples.Replace("&gt;", ">");
                sExemples = sExemples.Replace("&quot;", "\"");
                sExemples = sExemples.Replace("&apos;", "'");
                sExemples = sExemples.Replace("\t", "");
                BlockHelp bH = new("Parameters", _sListBlockHelp[iIdx].BlockName, sXML, sExemples);
                return bH;
            }
            else { return new BlockHelp("Parameters", "", "", ""); };
        }

        #endregion

    }

    public static class StringExtensions
    {
        public static int IndexOf(this List<BlockHelp> sListBlock, string sBlockToFind)
        {
            int iIndex = -1;

            if (sListBlock.Any(x => x.BlockName.ToUpper().Equals(sBlockToFind.ToUpper())))
            {
                for (int iC = 0; iC < sListBlock.Count; iC++)
                {
                    if (sListBlock[iC].BlockName.Equals(sBlockToFind, StringComparison.InvariantCultureIgnoreCase))
                    { iIndex = iC; }
                }
            }

            return iIndex;
        }
    }

    public class BlockHelp
    {
        #region "PROPRIETES"

        public string BlockName { get; } = "";
        public string BlockContent { get; private set; } = "";
        public string BlockExemples { get; } = "";
        public bool IsHTML { get; set; } = false;
        public string Language { get; set; } = "EN";
        public string View { get; set; } = "?";

        #endregion

        #region "PUBLIC VOID"

        public BlockHelp(string sView, string sBlockName, string sBlockContent, string sBlockExemples, bool isHTML = false)
        {
            View = sView;
            BlockName = sBlockName;
            BlockContent = sBlockContent;
            BlockExemples = sBlockExemples;
            IsHTML = isHTML;
        }

        public void SetBlockContent(string sContent)
        {
            BlockContent = sContent;
        }

        #endregion

    }
}


