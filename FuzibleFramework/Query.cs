using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FuzibleFramework
{

    public class Query
    {
        internal int ProcessingState { get; set; } = 0; //0=non traité, 1=processing, 2=terminé

        internal Query DeepCopy()
        {
            return (Query)this.MemberwiseClone();
        }

        public class QGroupByOrderBy
        {
            public string RawField { get; private set; } = "";
            public string Type { get; private set; } = "";
            public string Field { get; private set; } = "";
            public string Alias { get; private set; } = "";
            public bool IsAliasField { get; private set; } = false;
            public string From { get; private set; } = "";
            public string AscDesc { get; private set; } = "";
            private readonly List<QField> QueryFields = new();

            public QGroupByOrderBy(string sType, string sField, List<QField> qFields, List<QTable> qTables)
            {
                if (qFields != null) { QueryFields = qFields; }

                if (sField.StartsWith("(") && sField.EndsWith(")")) { sField = sField[1..^1]; } //en cas de ORDER BY (myfield)

                string sFieldTmp = sField;
                int iAsc = sFieldTmp.IndexOf(" ASC", StringComparison.InvariantCultureIgnoreCase);
                int iDesc = sFieldTmp.IndexOf(" DESC", StringComparison.InvariantCultureIgnoreCase);
                if (iAsc > 0) { sFieldTmp = sFieldTmp[..iAsc]; AscDesc = "ASC"; }
                if (iDesc > 0) { sFieldTmp = sFieldTmp[..iDesc]; AscDesc = "DESC"; }

                FindFieldIndexAndAlias(sFieldTmp, qTables);

                sFieldTmp = sFieldTmp.IndexOf(".") > 0 ? sFieldTmp.Split(Convert.ToChar("."))[1] : sFieldTmp;

                Type = sType;
                Field = sFieldTmp;

                string sTmp = Regex.Replace(sField, "(.+)((ASC)|(DESC))$", "$1", RegexOptions.IgnoreCase).Trim();
                From = QTools.FindTableFromFilterField(sTmp, qTables, qFields);

                RawField = sField;
            }

            private void FindFieldIndexAndAlias(string sFieldTmp, List<QTable> qTables)
            {
                string sTest = sFieldTmp;
                bool bFound = false;
                foreach (QField q in QueryFields)
                {
                    if (q.Table.Length > 0)
                    {
                        string sT1 = string.Concat(q.TableAlias, ".");
                        string sT2 = string.Concat(q.Table, ".");

                        if (sTest.IndexOf(sT1) > -1 && qTables.Count == 1) { sTest = sTest.Replace(sT1, ""); }
                        else if (sTest.IndexOf(sT2) > -1 && qTables.Count == 1) { sTest = sTest.Replace(sT2, ""); }

                        if (q.Name.Equals("*"))
                        {
                            bFound = true; break;
                        }
                        if (sTest.IndexOf(q.Raw, StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            sTest = sTest.Replace(q.Raw, q.Alias); bFound = true; break;
                        }
                        if (q.Transformation.Length > 0 && sTest.IndexOf(q.Transformation, StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            sTest = sTest.Replace(q.Transformation, q.Alias); bFound = true; break;
                        }
                        if (sTest.IndexOf(q.Name, StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            sTest = sTest.Replace(q.Name, q.Alias); bFound = true; break;
                        }
                        if (sTest.IndexOf(q.Alias, StringComparison.InvariantCultureIgnoreCase) > -1)
                        {
                            bFound = true;
                        }
                        if (sTest.Equals(q.Alias, StringComparison.InvariantCultureIgnoreCase) || sTest.Equals(q.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            IsAliasField = true;
                        }
                    }
                    else
                    {
                        foreach (QTable qT in qTables)
                        {
                            Match mc = Regex.Match(Toolbox.RemoveRegexFromString(sFieldTmp), "^(" + Toolbox.RemoveRegexFromString(qT.Alias) + "|" + Toolbox.RemoveRegexFromString(qT.Name) + ")\\.");
                            if (mc.Success)
                            {
                                IsAliasField = false;
                                Alias = sFieldTmp;
                            }
                        }
                    }
                }
                if (bFound) { Alias = sTest; }
                if (Alias.Length == 0) { Alias = sTest; }

                //gestion des champs encadrés
                if (Alias.StartsWith("\"") && Alias.EndsWith("\""))
                {
                    Alias = Alias[1..^1];
                }
            }
        }

        public class QTable
        {
            public string Raw { get; private set; } = "";
            public string Name { get; private set; } = "";
            public string Alias { get; private set; } = "";
            public string LinkType { get; private set; } = "";
            public List<string[]> LinkFields { get; private set; } = new List<string[]>();
            public bool IsSubQuery { get; private set; } = false;

            public QTable(string sTable, string sAlias, string sLinkedWith, List<string[]> sLink, CONNString CS)
            {
                if (CS != null && CS.SConnDriverSuffix.Equals("DB")) { sTable = Regex.Replace(sTable, SHSRegex.REGEX_SQL_SEPARATORS, ""); }

                Raw = sTable;
                Name = sTable.StartsWith("\"") && sTable.EndsWith("\"") ? sTable[1..^1] : sTable;
                Alias = sAlias.Length > 0 ? sAlias : sTable;
                LinkType = sLinkedWith;
                //0 : champ t1
                //1 : champ t2
                //2 : link type
                //3 : alias table t1
                //4 : alias table t2
                //5 : table t1.champ t1
                //6 : table t2.champ t2
                if (sLink != null) { LinkFields = sLink; }
                IsSubQuery = Regex.Match(Name, "^\\(\\s*SELECT", RegexOptions.IgnoreCase).Success;
            }

            internal void ReplaceName(string sNewName)
            {
                Name = sNewName;
                //Alias = sNewName;
            }
        }

        public class QField
        {
            public string Name { get; private set; } = "";
            public string Alias { get; set; } = "";
            public string Table { get; internal set; } = "";
            public string TableAlias { get; internal set; } = "";
            public string Transformation { get; set; } = "";
            public List<string> TransformationDetails { get; set; } = new List<string>();
            public List<string> Functions { get; private set; } = new List<string>();
            public string Raw { get; private set; } = "";
            public bool IsSubQuery { get; private set; } = false;
            public int Index { get; set; } = 0;
            public SQLColumn FieldAnalyzer
            {
                get; internal set;
            }
            public bool IsAggregate { get; internal set; } = false;

            internal List<QError> TransformationErrors = new();
            private List<SQLColumn> colSource;

            public QField(string sQField, List<QTable> sQTables, int iIndex, CONNString sCS)
            {
                //sQField = Regex.Replace(sQField, SHSRegex.REGEX_SQL_SEPARATORS, "");
                string sQFullField = sQField;
                Index = iIndex;
                string sQAlias = "";
                string sQTable = "";
                string sQAliasTable = "";
                string sQTransformation = "";

                //gestion de l'alias de champ (1) --on prend sciemment la dernière itération
                string sF = sQField;
                int iAlias = sF.LastIndexOf(" as ", StringComparison.InvariantCultureIgnoreCase);
                //subtilité : une requête SQL peut avoir un select monchamp test (test devenant un alias) alors qu'on ne peut pas dans tous les autres cas
                //éviter le "test mon champ", "bonjour as test"
                int iSpace = -1;
                if (sCS != null && sCS.SConnDriverSuffix.Equals("DB") && sF.LastIndexOf(" ") > sF.LastIndexOf(sCS.SqlEchappementChar)) { iSpace = sF.LastIndexOf(" ", StringComparison.InvariantCultureIgnoreCase); }

                if (iAlias > -1)
                {
                    sQAlias = sF[(iAlias + 4)..].Trim();
                    sQField = sF[..iAlias].Trim();
                }
                if (iAlias == -1 && iSpace > -1)
                {
                    if (sF.Length > (iSpace + 4))
                    {
                        sQAlias = sF[(iSpace + 4)..].Trim();
                        sQTable = sF[..iSpace].Trim();
                    }
                }

                //if (iAlias == -1 && iSpace == -1) { sListColumns[iF][1] = sF; } // l'alias est saisi comme le champ

                if (iAlias > -1) { sF = sF[..iAlias].Trim(); } // on garde le reste en excluant l'alias de colonne
                if (iAlias == -1 && iSpace > -1 && sQAlias.Length > 0) { sF = sF[..iSpace].Trim(); } // on garde le reste en excluant l'alias de colonne

                //gestion de l'alias de table (3) --on prend sciemment la dernière itération
                if (sQTables.Count == 1)
                {
                    sQAliasTable = sQTables[0].Alias;
                }
                else
                {
                    MatchCollection mcFields = Regex.Matches(sF, SHSRegex.REGEX_FIELD_ALIAS);
                    if (mcFields.Count > 0) { sQAliasTable = mcFields[^1].Value.Split(Convert.ToChar("."))[0]; }
                }

                //gestion des champs complexes sur les fichiers genre SELECT "ceci%(est)un[champ]" FROM
                if (Regex.IsMatch(sF, SHSRegex.REGEX_SQL_FIELD_EMBRACE))
                {
                    //pas de traitement particulier, pas de recherche de fonction.
                }
                else
                {

                    if (sF.StartsWith("CASE ", StringComparison.InvariantCultureIgnoreCase)
                        && sF.EndsWith(" END", StringComparison.InvariantCultureIgnoreCase)) //gestion des fonctions SQL : le CASE
                    {
                        if (sF.IndexOf(" WHEN ", StringComparison.InvariantCultureIgnoreCase) >= 5) //cas du CASE Something WHEN
                        {
                            sQField = sF[4..sF.IndexOf(" WHEN ", StringComparison.InvariantCultureIgnoreCase)].Trim();
                            sQTransformation = sF;
                            Functions.Add("CASE");
                        }
                        else //cas du CASE WHEN SOMETHING
                        {
                            //on tente d'abord de retrouver l'alias d'une table dans la ligne pour espérer trouver un des champs associés
                            foreach (QTable qT in sQTables)
                            {
                                //regex : tente de matcher n'importe quel caractère, alias, ., champ, n'importe quel caractère
                                Match mcA = Regex.Match(sF, "[\\s\"$&+,:;=?@#|'<>*()%!]" + qT.Alias + "\\.[^$&+,:;=?@#|'<>*()%!]+[\\s\"$&+,:;=?@#|'<>*()%!]", RegexOptions.IgnoreCase);
                                Match mcB = Regex.Match(sF, "[\\s\"$&+,:;=?@#|'<>*()%!]" + qT.Name + "\\.[^$&+,:;=?@#|'<>*()%!]+[\\s\"$&+,:;=?@#|'<>*()%!]", RegexOptions.IgnoreCase);
                                if (mcA.Success)
                                {
                                    sQField = mcA.ToString();
                                    sQField = sQField[1..];
                                    sQField = sQField[0..^1];
                                    break;
                                }
                                else if (mcB.Success)
                                {
                                    sQField = mcB.ToString();
                                    sQField = sQField[1..];
                                    sQField = sQField[0..^1];
                                    break;
                                }
                            }
                            if (sQField.Length == 0) //sinon on met l'alias car les CASE WHEN complexes ne permettent pas de trouver le nom du champ SQL
                            {
                                sQField = sQAlias;
                            }
                            sQTransformation = sF;
                            Functions.Add("CASE");
                        }
                    }
                    else //les fonctions classiques (substring( , replace( ...)
                    {
                        //subquery
                        if (Regex.Match(sF, "^\\(\\s*SELECT", RegexOptions.IgnoreCase).Success)
                        {
                            IsSubQuery = true;
                        }
                        else
                        {
                            //gestion du transformateur (4) --on prend sciemment la dernière itération

                            //case du CONCAT("test(essai)", "coucou")
                            int iFirstParLeft = sF.IndexOf("(");
                            int iFirstParRight = sF.LastIndexOf(")");
                            if (iFirstParRight > iFirstParLeft && iFirstParLeft >= 0 && iFirstParRight > 1)
                            {
                                //on récupère le mot qui est juste devant la parenthèse gauche
                                string sFtemp = sF[..(iFirstParRight + 1)];
                                MatchCollection mcTrans = Regex.Matches(sFtemp, SHSRegex.REGEX_FIELD_FUNC);
                                if (mcTrans.Count > 0)
                                {
                                    for (int iF = 0; iF < mcTrans.Count; iF++)
                                    {
                                        Functions.Add(mcTrans[iF].Value[0..^1]);
                                    }

                                    sQTransformation = sF;
                                    //ici, je vais chercher le vrai nom du champ dans le bloc de fonctions
                                    int iField = mcTrans[^1].Index + mcTrans[^1].Length;
                                    string sFTemp = sF[iField..];
                                    string sField = sFTemp;
                                    if (sFTemp.Equals(")")) { sField = sF; } //patch foireux "select count()"

                                    //on découpe tous les morceaux de la fonction selon les virgules pour tenter de retrouver un nom de table - donc un champ de cette table potentiel
                                    //ne fonctionne pas si le champ est de type : CONCAT("\"Queue - Type (QL,QF,QFP,HSK,ISO)\",\"Désignation\")")
                                    //on va donc le découper comme un CSV
                                    //MatchCollection mcCol = Regex.Matches(sField, "(?:,|\\n|^)((\"`\\[)(?:(?:(\"`\\[)(\"`\\[))*[^(\"`])]*)*(\"`])|[^(\"`]),\\n]*|(?:\\n|$))");
                                    MatchCollection mcCol = Regex.Matches(sField, SHSRegex.REGEX_FUNC_FIELDS_EMBRACE);
                                    string[] sInsideFunctions = mcCol.Select(m => m.Value.StartsWith(",") ? m.Value[1..] : m.Value).ToArray();
                                    if (sInsideFunctions.Length == 0) { sInsideFunctions = new string[] { sField }; }
                                    //string[] sInsideFunctions = sField.Split(new string[] { ",", " As ", " AS ", " aS ", " as " }, StringSplitOptions.RemoveEmptyEntries);

                                    bool iExists = false;
                                    foreach (string sFunc in sInsideFunctions)
                                    {
                                        if (Regex.IsMatch(sFunc.Trim(), SHSRegex.REGEX_FIELD_ALIAS) || Regex.IsMatch(sFunc, SHSRegex.REGEX_SQL_FIELD_EMBRACE)) //rappel : xx1.1xx ou y.x ou matable.monchamp ou t1.1
                                        {
                                            sQField = sFunc.Trim(); iExists = true; break;
                                        }
                                    }
                                    if (!iExists)
                                    {
                                        sQField = sInsideFunctions[0].Trim();
                                    }

                                    //je met un WHILE pour gérer les fonctions imbriquées ( =========== SAUF LES CHAMPS ENCADRES ! "my(test)"
                                    while (sQField.EndsWith(")") && !Regex.IsMatch(sQField, SHSRegex.REGEX_SQL_FIELD_EMBRACE))
                                    {
                                        sQField = sQField[0..^1];
                                    } //cas des fonctions genre GROUP_CONCAT(CHAMP) : sans plusieurs arguments à l'intérieur
                                }
                            }
                        }
                    }
                }

                //gestion du nom de la table (2) --on prend sciemment la dernière itération
                foreach (QTable qT in sQTables)
                {
                    if (sQAliasTable.Equals(qT.Alias, StringComparison.InvariantCultureIgnoreCase) || sQAliasTable.Equals(qT.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        sQTable = qT.Name;
                    }
                    //on nettoie le champ de base pour ne conserver que le nom et pas la référence à la table
                    if (sQField.StartsWith(string.Concat(qT.Alias, "."), StringComparison.InvariantCultureIgnoreCase))
                    {
                        sQField = sQField[string.Concat(qT.Alias, ".").Length..];
                    }
                    if (sQField.StartsWith(string.Concat(qT.Name, "."), StringComparison.InvariantCultureIgnoreCase))
                    {
                        sQField = sQField[string.Concat(qT.Name, ".").Length..];
                    }
                }

                Name = Regex.Replace(sQField, SHSRegex.REGEX_SQL_SEPARATORS, "");
                //Alias = sQAlias.Length > 0 ? Regex.Replace(sQAlias, SHSRegex.REGEX_SQL_SEPARATORS, "") : Regex.Replace(sQField, SHSRegex.REGEX_SQL_SEPARATORS, "");
                Alias = "";
                if (sQAlias.Length > 0)
                {
                    Alias = Regex.Replace(sQAlias, SHSRegex.REGEX_SQL_SEPARATORS, "");
                }
                else
                {
                    if (Regex.IsMatch(sQFullField, SHSRegex.REGEX_SQL_FIELD_EMBRACE)) //gestion d'un champ qui serait du type SELECT "d'accord" FROM
                    {
                        Alias = sQFullField[1..^1];
                    }
                    else { Alias = sQFullField; }
                }

                //typiquement un driver ODBC Excel , c'est SELECT * from [mySheet$], et je ne veux pas retirer les []
                if (sCS.SConnDriver != SQLTools_Enums.BDD.DB_ODBC)
                {
                    Table = Regex.Replace(sQTable, SHSRegex.REGEX_SQL_SEPARATORS, "");
                    TableAlias = sQAliasTable.Length > 0 ? Regex.Replace(sQAliasTable, SHSRegex.REGEX_SQL_SEPARATORS, "") : Regex.Replace(sQTable, SHSRegex.REGEX_SQL_SEPARATORS, "");
                }
                else
                {
                    Table = sQTable; TableAlias = sQAliasTable.Length > 0 ? sQAliasTable : sQTable;
                }

                Transformation = sQTransformation;
                TransformationDetails = AnalyzeTransformation(sQTransformation, sCS);

                Raw = sQFullField;
                IsSubQuery = Name.StartsWith("(SELECT", StringComparison.InvariantCultureIgnoreCase);
                FieldAnalyzer = new SQLColumn(iIndex, Alias, SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "", true, "", false, false);
            }

            public QField(string sName, string sAlias, string sTable, string sTableAlias, string sTransform, int iIndex)
            {
                Raw = sName;
                Name = sName;
                Alias = sAlias.Length > 0 ? sAlias : sName;
                Table = sTable;
                TableAlias = sTableAlias.Length > 0 ? sTableAlias : sTable;
                Transformation = sTransform;

                Index = iIndex;
            }

            public QField(SQLColumn colSource, string sTableName, int iIndex)
            {
                Raw = colSource.ColumnName;
                Name = colSource.ColumnName;
                Alias = colSource.ColumnName;
                Table = sTableName;
                TableAlias = sTableName;
                Transformation = "";

                Index = iIndex;
                FieldAnalyzer = colSource;
            }

            private List<string> AnalyzeTransformation(string sDtTransform, CONNString sCS)
            {
                List<string> sListTransform = new();

                if (sDtTransform.Length > 0)
                {
                    if (sDtTransform.IndexOf("CASE ", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        string[] sCase = new string[] { " WHEN ", " THEN ", " ELSE ", " END", " when ", " then ", " else ", " end", " When ", " Then ", " Else ", " End" };
                        //CASE monchamp WHEN x THEN 1 WHEN y THEN 2 ELSE 3 END
                        string[] sWhen = sDtTransform[4..].Split(sCase, StringSplitOptions.RemoveEmptyEntries);
                        if (sWhen.Length < 4)
                        {
                            TransformationErrors.Add(new QError(QError.ErrorLevel.WARNING, sDtTransform, "'CASE [...] WHEN [...] ELSE [...] END' function wrongly formatted. Missing argument(s)."));
                        }
                    }
                    else if (sDtTransform.Contains('(') && sDtTransform.EndsWith(")"))
                    {
                        //int iQtePars = sDtTransform.Transformation.Split(Convert.ToChar("(")).Length; //on compte le nombre de parenthèses ouvertes dans la fonction SQL (pour gérer l'imbriquement : substring(replace(substring...
                        //int iQtePars = Regex.Matches(sDtTransform, "(" + string.Join("||", SHSConstantes.SQL_FUNCTIONS) + ")\\s*\\({1}").Count;
                        string sS = "";
                        string sField = "";
                        string sZ = "";
                        string sAlias = "";
                        string sTransformation = sDtTransform;
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
                            //dernière ou unique itération

                            //on est dans la boucle des fonctions imbriquées
                            if (!sTransformation.Equals(sAlias))
                            {
                                sAlias = "SHS_FUNC_TMP_" + iIteration.ToString("00000");
                            }


                            //la dernière transformation est simplement l'alias (boucle imbriquée)
                            if (!sTransformation.Equals(sAlias))
                            {
                                sListTransform.Add(string.Concat(iIteration.ToString(), " : ", sZ, " [FUNC] ", sS, " [TEMP] ", sAlias));

                                if (sS.Length == 0 && !sCS.SConnDriverSuffix.Equals("DB"))
                                {
                                    TransformationErrors.Add(new QError(QError.ErrorLevel.ERROR, sDtTransform, "SQL function wrongly formatted."));
                                    break;
                                }

                                if (sField.Length == 0 && !sCS.SConnDriverSuffix.Equals("DB"))
                                {
                                    TransformationErrors.Add(new QError(QError.ErrorLevel.ERROR, sDtTransform, "SQL function wrongly formatted."));
                                    break;
                                }

                                if (!SHSConstantes.SQL_FUNCTIONS.Contains(sZ.ToUpper()) && !sCS.SConnDriverSuffix.Equals("DB"))
                                {
                                    TransformationErrors.Add(new QError(QError.ErrorLevel.ERROR, sDtTransform, "Unrecognized SQL Function."));
                                    break;
                                }
                            }

                            if (SHSConstantes.SQL_AGGREGATE_FUNCTIONS.Contains(sZ.ToUpper()))
                            {
                                IsAggregate = true;
                            }

                            mcFunctions = Regex.Matches(sTransformation, "(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + ")\\s*\\({1}", RegexOptions.IgnoreCase);
                        }
                    }
                    else
                    {
                        TransformationErrors.Add(new QError(QError.ErrorLevel.WARNING, sDtTransform, "SQL function wrongly formatted."));
                    }
                }
                return sListTransform;
            }
        }

        public class QWhere
        {
            //l'ordre est important !
            private readonly List<string> WHERE_FILTERS = new() { ">=", "<=", "!=", "<>", "=", ">", "<", "[\\s)]+IS\\s+NOT[\\s(]+", "[\\s)]+NOT\\s+LIKE[\\s(]+", "[\\s)]+LIKE[\\s(]+", "[\\s)]+NOT\\s+IN[\\s(]+", "[\\s)]+IN[\\s(]+", "[\\s)]+IS[\\s(]+" };

            public string SubConditionOperator { get; private set; } = "";
            public List<QWhere> SubConditions { get; private set; } = new List<QWhere>();
            public bool ReversedCondition { get; private set; } = false;
            public string Where { get; private set; } = "";
            public string RawWhereField { get; } = "";
            public string WhereSign { get; } = "";
            public string WhereCompare { get; private set; } = "";
            public bool IsSubQuery { get; private set; } = false;
            public bool IsSubQueryComparedTo { get; private set; } = false;
            public string FieldAlias { get; private set; } = "";
            public bool IsAliasField { get; private set; } = false;
            public bool UnrecognizedField { get; private set; } = true;
            public string TableField { get; private set; } = "";
            public string TableWhereCompare { get; private set; } = "";

            private readonly List<QField> QueryFields = new();
            private readonly List<QTable> QueryTables = new();

            public QWhere(string sWhere, CONNString sConnSource, CONNString sConnTarget, List<QField> qFields, List<QTable> qTables, string sConditionSubWhere)
            {
                SubConditionOperator = sConditionSubWhere;
                List<QWhere> wQSubList = new();

                string sEchapCharSource = "\"";
                string sEchapCharTarget = "\"";

                if (sConnSource != null) { sEchapCharSource = sConnSource.SqlEchappementChar; }
                if (sConnTarget != null) { sEchapCharTarget = sConnTarget.SqlEchappementChar; }
                bool iFound = false;

                if (sWhere.StartsWith("(") && sWhere.EndsWith(")")) { sWhere = sWhere[1..^1]; }

                //(id_sample > 50 AND id_sample < 90)
                string[] sMultiWhere = Regex.Split(sWhere, "\\s+(AND|OR)\\s+", RegexOptions.IgnoreCase);
                if (sMultiWhere.Length > 1) //on aura les conditions sur 0,2,4...
                {
                    for (int iW = 2; iW < sMultiWhere.Length; iW += 2)
                    {
                        wQSubList.Add(new QWhere(sMultiWhere[iW], sConnSource, sConnTarget, qFields, qTables, sMultiWhere[iW - 1]));
                    }
                }
                SubConditions = wQSubList;

                Where = sWhere;

                if (sMultiWhere.Length > 0) { sWhere = sMultiWhere[0]; }

                foreach (string sF in WHERE_FILTERS) //recherche du champ, du séparateur, puis du comparateur
                {
                    if (Regex.Match(sWhere, sF, RegexOptions.IgnoreCase).Index > 0) //dernier champ
                    {
                        Match mC = Regex.Match(sWhere, sF, RegexOptions.IgnoreCase);
                        //ex : WHERE x < (select * from test where 1 = 1)
                        //on compte le nombre de parenthèses avant et après

                        string s1 = sWhere[..(mC.Value.StartsWith(")") ? mC.Index + 1 : mC.Index)];
                        string s2 = sWhere[((mC.Value.EndsWith("(") ? mC.Index - 1 : mC.Index) + mC.Length)..];
                        //ancienne analyse : [\\(\\)] attention !! pète sur WHERE x <> '(' !

                        //problème du cast('2021-01-01' as date)
                        //contrôle : que si la première parenthèse ( est avant le premier '
                        //              alors la dernière parenthèse ) doit être après le dernier '
                        bool bAddCheckS1 = false;
                        bool bAddCheckS2 = false;

                        if (s1.Contains('(') && s1.Contains(')') && s1.Contains('\'') && s1.IndexOf("(") < s1.IndexOf("'") && s1.LastIndexOf(")") > s1.LastIndexOf("'"))
                        {
                            bAddCheckS1 = true;
                        }
                        else if (Regex.Matches(s1, "(\\(|\\))(?![^']*')").Count % 2 == 0) { bAddCheckS1 = true; }

                        if (s2.Contains('(') && s2.Contains(')') && s2.Contains('\'') && s2.IndexOf("(") < s2.IndexOf("'") && s2.LastIndexOf(")") > s2.LastIndexOf("'"))
                        {
                            bAddCheckS2 = true;
                        }
                        else if (Regex.Matches(s2, "(\\(|\\))(?![^']*')").Count % 2 == 0) { bAddCheckS2 = true; }


                        if (bAddCheckS1 && bAddCheckS2)
                        {
                            WhereSign = mC.Value.Trim().Replace("(", "").Replace(")", "");
                            int iIdxSign = mC.Index;
                            WhereCompare = sWhere[(iIdxSign + (mC.Value.EndsWith("(") ? mC.Length - 1 : mC.Length))..].Trim();
                            RawWhereField = sWhere[..(mC.Value.StartsWith(")") ? mC.Index + 1 : mC.Index)].Trim();
                            iFound = true;
                            //signalisation de l'inversion du signe si requête genre 20 < idsample
                            break;
                        }
                    }
                    if (iFound) { break; }
                }

                //recherche des champs encadrés (cas du "ADRESSE.RUE.1")
                Match mcSubQ = Regex.Match(WhereCompare, SHSRegex.REGEX_BASIC_SUBQUERY, RegexOptions.IgnoreCase);
                if (mcSubQ.Success) { IsSubQueryComparedTo = true; }
                mcSubQ = Regex.Match(RawWhereField, SHSRegex.REGEX_BASIC_SUBQUERY, RegexOptions.IgnoreCase);
                if (mcSubQ.Success) { IsSubQuery = true; }

                if (qFields != null) { QueryFields = qFields; }
                if (qTables != null) { QueryTables = qTables; }

                FindFieldIndexAndAlias();

                TableField = QTools.FindTableFromFilterField(FieldAlias, qTables, qFields);
                TableWhereCompare = QTools.FindTableFromFilterField(WhereCompare, qTables, qFields);

            }

            private void FindFieldIndexAndAlias()
            {
                bool bFound = false;
                for (int i = 0; i < 2; i++)
                {
                    bFound = false;
                    string sTest = i == 0 ? RawWhereField : WhereCompare;

                    foreach (QField qF in QueryFields)
                    {
                        if (qF.Table.Length == 0) //à priori on a un "SELECT * FROM x INNER JOIN y ON x.id = y.id WHERE x.var = 'coucou'"
                        {
                            foreach (QTable qT in QueryTables)
                            {
                                string sT1 = string.Concat(qT.Name, ".");
                                string sT2 = string.Concat(qT.Alias, ".");
                                if (sTest.IndexOf(sT1) > -1)
                                {
                                    bFound = true; break;
                                }
                                else if (sTest.IndexOf(sT2) > -1)
                                {
                                    bFound = true; break;
                                }
                            }
                        }
                        else
                        {
                            //tous les champs de type SELECT x.* FROM table WHERE x.id = 1 ou bien SELECT * FROM table WHERE table.id = 1 ne doivent pas
                            //subir le remplacement du champ dans la condition WHERE car on ignore leur présence dans la requête.
                            if (!qF.Name.EndsWith("*"))
                            {
                                string sT1 = string.Concat(qF.TableAlias, ".", qF.Name);
                                string sT2 = string.Concat(qF.Table, ".", qF.Name);
                                if (sTest.StartsWith(sT1))
                                {
                                    sTest = sTest.Replace(sT1, qF.Alias); bFound = true; break;
                                }
                                else if (sTest.StartsWith(sT2))
                                {
                                    sTest = sTest.Replace(sT2, qF.Alias); bFound = true; break;
                                }

                                //gestion du WHERE en mode synchro (remplacement des noms de colonne de la source par l'alias de la cible)
                                else if (qF.Raw.Length > 0 && sTest.StartsWith(qF.Raw, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sTest = sTest.Replace(qF.Raw, qF.Alias); bFound = true; break;
                                }
                                else if (qF.Transformation.Length > 0 && sTest.StartsWith(qF.Transformation, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sTest = sTest.Replace(qF.Transformation, qF.Alias); bFound = true; break;
                                }
                                else if (qF.Name.Length > 0 && sTest.StartsWith(qF.Name, StringComparison.InvariantCultureIgnoreCase)) //remplacement du nom initial de la colonne par son alias (pour gérer le WHERE en mode synchro)
                                {
                                    sTest = sTest.Replace(qF.Name, qF.Alias); bFound = true; break;
                                }
                                else if (qF.Alias.Length > 0 && sTest.StartsWith(qF.Alias, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    bFound = true; break;
                                }
                                else if ((qF.Alias.Length > 0 && sTest.Equals(qF.Alias, StringComparison.InvariantCultureIgnoreCase)) || (qF.Name.Length > 0 && sTest.Equals(qF.Name, StringComparison.InvariantCultureIgnoreCase)))
                                {
                                    IsAliasField = true; break;
                                }
                            }
                        }
                    }
                    if (bFound)
                    {
                        UnrecognizedField = false;
                        FieldAlias = sTest;
                        if (i == 1) { ReversedCondition = true; }
                        break;
                    }
                    //gestion d'un cas particulier : si on a une requête de type 20 > idsample (et non pas idsample < 20) on met un flag parce que
                    //la requête de synchro, quand elle se construit, va écrire idsample > 20, donc le signe doit être inversé
                }
                if (!bFound)
                {
                    UnrecognizedField = true;
                    FieldAlias = RawWhereField;

                    if (QueryFields[0].Name.EndsWith("*")) //en cas d'une requête de type SELECT * ou SELECT table.*, on va supprimer l'alias de table (ou son nom) dans la condition WHERE pour gérer la condition de synchro cible sans que ça pète
                    {
                        string sTest = RawWhereField;
                        foreach (QField q in QueryFields)
                        {
                            string sT1 = string.Concat(q.TableAlias, ".");
                            string sT2 = string.Concat(q.Table, ".");
                            if (sTest.IndexOf(sT1) > -1) { sTest = sTest.Replace(sT1, ""); }
                            else if (sTest.IndexOf(sT2) > -1) { sTest = sTest.Replace(sT2, ""); }
                        }
                        FieldAlias = sTest;
                    }
                    else //le champ de filtrage est inconnu des champs choisis dans la requête
                    {
                    }

                }
            }

            public string GetSubConditions()
            {
                StringBuilder sbSub = new();
                foreach (QWhere qW in SubConditions)
                {
                    sbSub.Append(string.Concat(" ", qW.SubConditionOperator, " ", qW.Where));
                }
                return sbSub.ToString();
            }
        }

        public class QSynchroQuery
        {
            private readonly string _sPredictedSynchroTargetWhere = "";

            public string SynchroTargetQuery { get; set; } = "";
            public List<SQLColumn> PredictedSynchroTargetColumns { get; set; } = new List<SQLColumn>();
            public List<SQLColumn> RealSynchroTargetColumns { get; set; } = new List<SQLColumn>();
            public string SynchroTargetTable { get; set; } = "";
            public CONNString CSTarget { get; set; } = null;

            public QSynchroQuery(string sTargetTable, string sQuery, string sWhereTarget, List<SQLColumn> sColumns, CONNString csT)
            {
                SynchroTargetTable = sTargetTable;
                SynchroTargetQuery = sQuery;
                PredictedSynchroTargetColumns = sColumns;
                _sPredictedSynchroTargetWhere = sWhereTarget;
                CSTarget = csT;
            }

            public string PredictedSynchroTargetWhere()
            {
                return _sPredictedSynchroTargetWhere;
            }


            private List<string> FilterTargetQueryWithOptionalColumns(Job JobParameters, Query FuzibleQuery, DataTable dtSource)
            {
                List<string> sFilters = new List<string>();

                foreach (DataColumn dt in dtSource.Columns)
                {
                    if (JobParameters.OptionalDBName_OnInsert && JobParameters.TargetAddDbName.Length > 0)
                    {
                        if (dt.ColumnName.Equals(JobParameters.TargetAddDbName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (dtSource != null && dtSource.Rows.Count > 0)
                            {
                                sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", dtSource.Rows[0][dt].ToString(), "'"));
                            }
                            else
                            {
                                dtSource = new DataTable("UNKNOWN");
                                sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", Toolbox.GetDbName(JobParameters, FuzibleQuery, dtSource), "'"));
                            }
                        }
                    }

                    //if (JobParameters.OptionalTimestamp_OnInsert && JobParameters.TargetAddDtLoad.Length > 0)
                    //{
                    //    if (dt.ColumnName.Equals(JobParameters.TargetAddDtLoad, StringComparison.OrdinalIgnoreCase))
                    //    {
                    //        if (dtSource != null && dtSource.Rows.Count > 0)
                    //        {
                    //            sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", dtSource.Rows[0][dt].ToString(), "'"));
                    //        }
                    //        else
                    //        {
                    //            sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", Toolbox.SetCleanDate(DateTime.Now.ToString(), JobParameters, JobParameters.ConnectionString_Target, false, true, 2), "'"));
                    //        }
                    //    }
                    //}

                    if (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0)
                    {
                        string[] sDynamicParam = JobParameters.OptionalDynamicParamField_OnInsert.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string sParam in sDynamicParam)
                        {
                            string[] sP = sParam.Split('=');

                            if (sP.Length > 1)
                            {
                                if (dt.ColumnName.Equals(sP[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    if (dtSource != null && dtSource.Rows.Count > 0)
                                    {
                                        sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", dtSource.Rows[0][dt].ToString(), "'"));
                                    }
                                    else
                                    {
                                        sFilters.Add(string.Concat(CSTarget.SqlEchappementChar, dt.ColumnName, CSTarget.SqlEchappementChar, " = '", sP[1], "'"));
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                return sFilters;
            }

            public string AddOptionalFilters(string sTargetQuery, Job JobParameters, Query FuzibleQuery, DataTable dtSource)
            {
                if (!JobParameters.SynchroBypassQueryFiltersInTarget) //attention ! parti pris
                {
                    //traitement spécial pour l'UI : 
                    if (dtSource == null)
                    {
                        dtSource = new DataTable(FuzibleQuery.OutputTable);
                        foreach (var col in FuzibleQuery.QueryAnalyzer.Fields)
                        {
                            dtSource.Columns.Add(col.Alias);
                        }
                        if (JobParameters.OptionalDBName_OnInsert && JobParameters.TargetAddDbName.Length > 0 && !dtSource.Columns.Contains(JobParameters.TargetAddDbName))
                        {
                            dtSource.Columns.Add(JobParameters.TargetAddDbName);
                        }
                        if (JobParameters.OptionalTimestamp_OnInsert && JobParameters.TargetAddDtLoad.Length > 0 && !dtSource.Columns.Contains(JobParameters.TargetAddDtLoad))
                        {
                            dtSource.Columns.Add(JobParameters.TargetAddDtLoad);
                        }
                        string[] optparams = JobParameters.OptionalDynamicParamField_OnInsert.Split(',');
                        foreach (var par in optparams)
                        {
                            if (!dtSource.Columns.Contains(par.Split('"')[0]))
                            {
                                dtSource.Columns.Add(par.Split('"')[0]);
                            }
                        }
                    }

                    //traitement des clés primaires qui, dans la cible, utiliseraient des colonnes issues de colonnes optionnelles
                    List<string> sFilters = FilterTargetQueryWithOptionalColumns(JobParameters, FuzibleQuery, dtSource);

                    if (sTargetQuery.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        for (int i = 0; i < sFilters.Count; i++)
                        {
                            sTargetQuery = string.Concat(sTargetQuery, " AND ", sFilters[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < sFilters.Count; i++)
                        {
                            if (i == 0)
                            {
                                sTargetQuery = string.Concat(sTargetQuery, " WHERE ", sFilters[i]);
                            }
                            else
                            {
                                sTargetQuery = string.Concat(sTargetQuery, " AND ", sFilters[i]);
                            }
                        }
                    }
                }

                return sTargetQuery;
            }

            public string GetTargetQuery(bool bIsReal, bool bBypassFilters, bool bCheckValidity)
            {
                //en construisant la requête cible, on va vérifier que la source et la cible possèdent bien les mêmes colonnes
                StringBuilder sbQuery = new();
                sbQuery.Append("SELECT ");
                if (bIsReal)
                {
                    for (int i = 0; i < RealSynchroTargetColumns.Count; i++)
                    {
                        foreach (SQLColumn dc in PredictedSynchroTargetColumns)
                        {
                            if (RealSynchroTargetColumns[i].ColumnName.Equals(dc.ColumnName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (CSTarget != null)
                                {
                                    sbQuery.Append(string.Concat(",", CSTarget.SqlEchappementChar, dc.ColumnName, CSTarget.SqlEchappementChar));
                                    break;
                                }
                                else
                                {
                                    sbQuery.Append(string.Concat(",", dc.ColumnName));
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (SQLColumn dc in PredictedSynchroTargetColumns)
                    {
                        if (dc.ColumnName.Equals("*"))
                        {
                            sbQuery.Append(",*");
                        }
                        else
                        {
                            if (CSTarget != null)
                            {
                                sbQuery.Append(string.Concat(",", CSTarget.SqlEchappementChar, dc.ColumnName, CSTarget.SqlEchappementChar));
                            }
                            else { sbQuery.Append(string.Concat(",", dc.ColumnName)); }
                        }
                    }
                }

                sbQuery = sbQuery.Replace("SELECT ,", "SELECT ");

                if (!bCheckValidity) // lors de l'exécution, j'essaie de prendre tous les champs pour tenter quand même la comparaison, tandis que sur l'UI, il y a un risque que ça ne marche pas
                {
                    if (sbQuery.ToString().Trim().Equals("SELECT")) { sbQuery.Append(" *"); } //??????
                }
                else
                {
                    if (sbQuery.ToString().Trim().Equals("SELECT"))
                    {
                        sbQuery.Append(string.Join(",", PredictedSynchroTargetColumns.Select(c => c.ColumnName)));
                    }
                }

                sbQuery.Append(" FROM ");

                if (CSTarget.SConnDriverSuffix.Equals("DB"))
                {
                    sbQuery.Append(string.Concat(CSTarget.SqlEchappementChar, SynchroTargetTable, CSTarget.SqlEchappementChar));
                }
                else { sbQuery.Append(SynchroTargetTable); }

                if (!bBypassFilters) { sbQuery.Append(string.Concat(" ", PredictedSynchroTargetWhere())); }

                if (bIsReal && (RealSynchroTargetColumns == null || RealSynchroTargetColumns.Count == 0)) { sbQuery.Clear(); }

                return sbQuery.ToString();
            }

        }

        public class QError
        {
            public string ErrorPattern { get; set; } = "";
            public string ErrorDescription { get; set; } = "";

            public ErrorLevel ErrorType { get; set; } = ErrorLevel.ERROR;

            public enum ErrorLevel
            {
                WARNING = 1,
                ERROR = 2
            }

            public QError(ErrorLevel errL, string sPattern, string sDescription)
            {
                ErrorPattern = sPattern;
                ErrorDescription = sDescription;
                ErrorType = errL;
            }
        }

        public class QProperty
        {
            public SQLTools_Enums.QUERY_PROPERTIES Property
            {
                get; internal set;
            }
            public string Value { get; internal set; } = "";
            public string Element { get; set; } = "";
            public bool HasToBeShownToUser { get; set; } = true;

            public QProperty(string sElement, SQLTools_Enums.QUERY_PROPERTIES qProperty, string sProperty, bool bHasToBeShownToUser)
            {
                Property = qProperty;
                Value = sProperty;
                Element = sElement;
                HasToBeShownToUser = bHasToBeShownToUser;
            }
        }

        public class QAnalyzer
        {
            private Job INIP
            {
                get;
            }
            private List<QProperty> _sProperties = new();
            private Query QQuery
            {
                get;
            }
            public int FromStartsAt { get; internal set; } = 0;
            public List<QProperty> QueryProperties
            {
                get
                {
                    return _sProperties;
                }
            }
            public List<QError> Errors { get; internal set; } = new List<QError>();
            public QSynchroQuery PreBuiltSynchroTargetQuery { get; set; } = null;
            public List<QField> Fields { get; private set; } = new List<QField>();
            public List<QTable> Tables { get; private set; } = new List<QTable>();
            public List<QWhere> Where { get; private set; } = new List<QWhere>();
            public List<QGroupByOrderBy> GroupBy { get; private set; } = new List<QGroupByOrderBy>();
            public List<QGroupByOrderBy> OrderBy { get; private set; } = new List<QGroupByOrderBy>();
            public List<string> UnionQueries { get; private set; } = new List<string>();
            public bool IsDistinct { get; private set; } = false;
            public int LimitedResults { get; internal set; } = 0;
            public int OffSetStart { get; internal set; } = 0;
            public int FetchCount { get; internal set; } = 0;
            public int SubQueriesCount { get; private set; } = 0;
            public string RawQuery { get; private set; } = "";
            public CONNString ConnectionSource
            {
                get; private set;
            }
            public CONNString ConnectionTarget
            {
                get; private set;
            }
            public string OutputTable { get; private set; } = "";
            public int SourceTableToHandle { get; private set; } = 0;
            public bool GetOneTableOnlyFromSource { get; private set; } = false;


            public QAnalyzer(Job JobParameters, Query Q, string sQuery, int iFromStartsAt, List<QField> sListColumns, List<QTable> sListTABLES, List<QWhere> sListWHERE, List<QGroupByOrderBy> sListGROUPBY,
                List<QGroupByOrderBy> sListORDERBY, List<string> sListUNION, bool bIsDistinct, int iHasLimit, int iFetchCount, int iOffSetStart,
                int iTableToHandle, bool bOneTableOnly, QSynchroQuery sSynchroTargetQuery, CONNString CSSource, CONNString CSTarget, string sOutputTable)
            {
                this.INIP = JobParameters;
                this.SourceTableToHandle = iTableToHandle > 0 ? iTableToHandle - 1 : 0;
                this.GetOneTableOnlyFromSource = bOneTableOnly;
                this.FromStartsAt = iFromStartsAt;
                this.Fields = sListColumns;
                this.Tables = sListTABLES;
                this.Where = sListWHERE;
                this.GroupBy = sListGROUPBY;
                this.OrderBy = sListORDERBY;
                this.UnionQueries = sListUNION;
                this.IsDistinct = bIsDistinct;
                this.LimitedResults = iHasLimit;
                this.OffSetStart = iOffSetStart;
                this.FetchCount = iFetchCount;
                this.RawQuery = sQuery;
                if (sSynchroTargetQuery != null) { this.PreBuiltSynchroTargetQuery = sSynchroTargetQuery; }
                this.QQuery = Q;
                ConnectionSource = CSSource;
                ConnectionTarget = CSTarget;
                OutputTable = sOutputTable;

                MatchCollection rDynParams = Regex.Matches(Q.RawQuery, SHSRegex.REGEX_DYNPARAM);

                foreach (Match mcDP in rDynParams)
                {
                    int iP = Convert.ToInt32(mcDP.Value.Substring(2, 1));
                    if (iP > JobParameters.DynParams.Count)
                    {
                        Errors.Add(new QError(QError.ErrorLevel.ERROR, mcDP.Value, Languages.Languages.par_errors_nodynparam));
                    }
                }

                //possibles subqueries sur les tables, champs, where : les compter !
                foreach (QTable qT in Tables)
                {
                    if (qT.IsSubQuery) { SubQueriesCount++; }
                }

                foreach (QField qF in Fields)
                {
                    if (qF.IsSubQuery) { SubQueriesCount++; }
                }

                foreach (QWhere qW in sListWHERE)
                {
                    if (qW.IsSubQuery) { SubQueriesCount++; }
                }

                //MatchCollection mcParA = Regex.Matches(sQuery, "(\\()(?![^']*')");
                //MatchCollection mcParB = Regex.Matches(sQuery, "(\\))(?![^']*')");
                string sSearchPar = Regex.Replace(sQuery, "'\\([^']*'", "", RegexOptions.IgnoreCase);
                sSearchPar = Regex.Replace(sQuery, "'\\)[^']*'", "", RegexOptions.IgnoreCase);
                MatchCollection mcParA = Regex.Matches(sSearchPar, "\\(");
                MatchCollection mcParB = Regex.Matches(sSearchPar, "\\)");

                //MatchCollection mcParA = Regex.Matches(sSearchPar, "(?<!(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + "))\\(", RegexOptions.IgnoreCase);
                //MatchCollection mcParB = Regex.Matches(sSearchPar, "(?<!(" + string.Join("|", SHSConstantes.SQL_FUNCTIONS) + "))\\)", RegexOptions.IgnoreCase);

                if (mcParA.Count != mcParB.Count)
                {
                    Errors.Add(new QError(QError.ErrorLevel.WARNING, string.Concat("'(' = ", mcParA.Count.ToString(), " , ')' = ", mcParB.Count.ToString()), Languages.Languages.par_errors_parenthesismismatch));
                }

                if (sOutputTable.Equals(""))
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Languages.Languages.par_errors_invalidoutput01, Languages.Languages.par_errors_invalidoutput02));
                }

                if (Tables.Count == 1 && Tables[0].Name.Equals("NOT_FOUND"))
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Languages.Languages.par_errors_missingfrom01, Languages.Languages.par_errors_missingfrom02));
                }

                if (Fields.Count == 1 && Fields[0].Name.Equals(""))
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Languages.Languages.par_errors_missingfields01, Languages.Languages.par_errors_missingfields02));
                }

                if (CSSource.SConnDriverSuffix.Equals("FI") && Tables.Count > 1 && Tables[0].Name.Contains('*') && !Tables[0].IsSubQuery)
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Languages.Languages.par_errors_cantjoinmultiples01, Languages.Languages.par_errors_cantjoinmultiples02));
                }

                if (CSSource.SConnDriverSuffix.Equals("DB") && Tables.Count > 1 && Tables[0].Name.Contains('%') && !Tables[0].IsSubQuery)
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Languages.Languages.par_errors_cantjoinmultiples01, Languages.Languages.par_errors_cantjoinmultiples02));
                }

                if (CSSource.SConnDriverSuffix.Equals("FI") && Tables.Count > 1 && Tables[0].Name.Contains('*') && !Tables[0].IsSubQuery && Q.CrossJoinQueries.Count > 0)
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Tables[0].Name, Languages.Languages.par_errors_cantcrossquerymultiples));
                }

                if (CSSource.SConnDriverSuffix.Equals("DB") && Tables.Count > 1 && Tables[0].Name.Contains('%') && !Tables[0].IsSubQuery && Q.CrossJoinQueries.Count > 0)
                {
                    Errors.Add(new QError(QError.ErrorLevel.ERROR, Tables[0].Name, Languages.Languages.par_errors_cantcrossquerymultiples));
                }

                //if (CSSource.SConnDriverSuffix.Equals("DB") && Regex.IsMatch(RawQuery, "[^\\[]--"))
                //{ Errors.Add(new QError(QError.ErrorLevel.ERROR, "--", "Please remove all SQL comments from the Query.")); }

                //if (CSSource.SConnDriverSuffix.Equals("DB") && RawQuery.IndexOf("/*") > 0)
                //{ Errors.Add(new QError(QError.ErrorLevel.ERROR, "/*", "Please remove all SQL comments from the Query.")); }

                //recherche des erreurs
                foreach (QField qF in Fields)
                {
                    Errors.AddRange(qF.TransformationErrors);

                    if (!qF.Name.Equals("*"))
                    {
                        if (Tables.Count > 1 && qF.Table.Length == 0)
                        {
                            if (CSSource.SConnDriverSuffix.Equals("DB"))
                            {
                                if (!qF.IsSubQuery)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, qF.Raw, Languages.Languages.par_errors_notabletofield));
                                }
                            }
                            else
                            {
                                if (!qF.IsSubQuery)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.ERROR, qF.Raw, Languages.Languages.par_errors_notableassociated));
                                }
                                else { Errors.Add(new QError(QError.ErrorLevel.ERROR, qF.Raw, Languages.Languages.par_errors_cantsubqueryfield)); }
                            }
                        }

                        //if (Tables.Count > 1 && (!Tables.Any(t => t.Alias.Equals(qF.TableAlias, StringComparison.InvariantCultureIgnoreCase))))
                        //{
                        //    if (!CSSource.SConnDriverSuffix.Equals("DB"))
                        //    { Errors.Add(new QError(QError.ErrorLevel.ERROR, qF.Raw, "Associated table does not exists in Query.")); }
                        //    else { Errors.Add(new QError(QError.ErrorLevel.WARNING, qF.Raw, "Missing table reference.")); }
                        //}

                    }
                    if (Fields.Any(f => f.Alias.Equals(qF.Alias) && f.Index != qF.Index))
                    {
                        Errors.Add(new QError(QError.ErrorLevel.ERROR, qF.Raw, Languages.Languages.par_errors_cantmultiplesamealiases));
                    }
                    //tester la transformation
                }

                if (CSSource != null)
                {
                    int iTable = 0;
                    foreach (QTable qT in Tables)
                    {
                        iTable++;
                        switch (CSSource.SConnDriverSuffix)
                        {
                            case "FI":
                                if (!CSSource.GetParam(SQLTools_Enums.DRIVER_PARAMS.FILE_COMEFROM).Contains("FTP", StringComparison.CurrentCulture))
                                {
                                    if (!qT.Name.Contains('*', StringComparison.CurrentCulture) && !qT.IsSubQuery)
                                    {
                                        string sPath = CSSource.SConnString(JobParameters.DynParams);
                                        if (!sPath.EndsWith("\\")) { sPath = string.Concat(sPath, "\\"); }
                                        if (!File.Exists(sPath + qT.Name)) { Errors.Add(new QError(QError.ErrorLevel.WARNING, qT.Name, Languages.Languages.par_errors_filenotfound)); }
                                    }
                                }
                                break;
                            case "MB":
                                string sMail = qT.Name;
                                if (sMail.IndexOf("[") > 0) { sMail = sMail[0..sMail.IndexOf("[")]; } //gestion du mot de passe saisi à la main
                                if (!Toolbox.CheckEmailValid(sMail))
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sMail, Languages.Languages.par_errors_unrecognizedmail));
                                }
                                break;
                            case "DB":
                                if (!Regex.IsMatch(qT.Name, SHSRegex.REGEX_VALID_SQL_TABLE) && !qT.Name.Contains('%'))
                                {
                                    //subquery
                                    if (Regex.IsMatch(qT.Name, "\\(\\s*SELECT\\s+", RegexOptions.IgnoreCase) && qT.Name.EndsWith(")"))
                                    {
                                    }
                                    else { Errors.Add(new QError(QError.ErrorLevel.WARNING, qT.Name, Languages.Languages.par_errors_unrecognizedsqltable)); }
                                }
                                break;
                            default:
                                break;
                        }
                        if (Tables.Count > 1 && qT.Alias.Length == 0)
                        {
                            Errors.Add(new QError(QError.ErrorLevel.ERROR, qT.Name, Languages.Languages.par_errors_multitablesmissaliases));
                        }

                        //0 : champ t1
                        //1 : champ t2
                        //2 : link type
                        //3 : alias table t1
                        //4 : alias table t2
                        //5 : table t1.champ t1
                        //6 : table t2.champ t2
                        string sJ = GetTablesJoinLinkAsString(qT);

                        //pas de règle de jointure saisie sur la table jointe
                        if (iTable > 1 && sJ.Length == 0)
                        {
                            Errors.Add(new QError(QError.ErrorLevel.ERROR, qT.Name, string.Concat(Languages.Languages.par_errors_missingjoinfield, qT.Name)));
                        }

                        foreach (string[] sJF in qT.LinkFields)
                        {
                            bool bTA = Tables.Any(t => t.Alias.Equals(sJF[3], StringComparison.InvariantCultureIgnoreCase));
                            bool bTB = Tables.Any(t => t.Alias.Equals(sJF[4], StringComparison.InvariantCultureIgnoreCase));

                            //contrôle que la comparaison n'est pas faite sur une table mais une valeur 
                            if (!bTA) //[0]
                            {
                                if (Toolbox.IsNumeric(sJF[0], CSSource)) { bTA = true; }
                                else if (sJF[0].StartsWith("'") && sJF[0].EndsWith("'")) { bTA = true; }
                                else if (sJF[0].StartsWith(CSSource.SqlEchappementChar) && sJF[0].EndsWith(CSSource.SqlEchappementChar)) { bTA = true; }
                            }
                            if (!bTB) //[1]
                            {
                                if (Toolbox.IsNumeric(sJF[1], CSSource)) { bTB = true; }
                                else if (sJF[1].StartsWith("'") && sJF[1].EndsWith("'")) { bTB = true; }
                                else if (sJF[1].StartsWith(CSSource.SqlEchappementChar) && sJF[1].EndsWith(CSSource.SqlEchappementChar)) { bTB = true; }
                            }

                            if (CSSource.SConnDriverSuffix.Equals("DB"))
                            {
                                if (!bTA)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_unreferencedlinkjoin, sJF[5], sJF[2], sJF[6], ").")));
                                }

                                if (!bTB)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_unreferencedlinkjoin, sJF[5], sJF[2], sJF[6], ").")));
                                }

                                if (!bTA && !bTB)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_unreferencedlinkjoin, sJF[5], sJF[2], sJF[6], ").")));
                                }
                            }
                            else
                            {
                                if (!bTA)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_linkmissestableref01, sJF[5], sJF[2], sJF[6], Languages.Languages.par_errors_linkmissestableref02)));
                                }

                                if (!bTB)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_linkmissestableref01, sJF[5], sJF[2], sJF[6], Languages.Languages.par_errors_linkmissestableref02)));
                                }

                                if (!bTA && !bTB)
                                {
                                    Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, string.Concat(Languages.Languages.par_errors_linkmissestableref03, sJF[5], sJF[2], sJF[6], Languages.Languages.par_errors_linkmissestableref02)));
                                }


                            }
                            //on ne peut pas trop contrôler les champs car ils ne sont pas nécessairement utilisés dans la requête

                            //if (!Fields.Any(t => t.Name.Equals(sJF[1])))
                            //{ Errors.Add(new QError(sJ, "Link field misses field reference")); }

                            //if (!Fields.Any(t => t.Name.Equals(sJF[2])))
                            //{ Errors.Add(new QError(sJ, "Link field misses field reference")); }

                            if (!SHSConstantes.JOIN_SEPARATORS.Contains(sJF[2]))
                            {
                                Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, Languages.Languages.par_errors_unknownjoinop));
                            }

                            if (qT.LinkType.Length == 0)
                            {
                                Errors.Add(new QError(QError.ErrorLevel.WARNING, sJ, Languages.Languages.par_errors_unknownjoin));
                            }
                        }
                    }
                }

                foreach (QWhere qW in Where)
                {
                    if (qW.FieldAlias.Length == 0) { Errors.Add(new QError(QError.ErrorLevel.WARNING, qW.Where, Languages.Languages.par_errors_wheremissingfield)); }

                    if (JobParameters.JobMethod == SQLTools_Enums.JOB_PURPOSE.STREAMING && Fields.Count > 0 && !Fields[0].Alias.Equals("*") && !Fields.Any(f => f.Alias.Equals(qW.FieldAlias)))
                    {
                        if (CSSource.SConnDriverSuffix.Equals("DB"))
                        {
                            Errors.Add(new QError(QError.ErrorLevel.WARNING, qW.Where, Languages.Languages.par_errors_whereweird));
                        }
                        else
                        {
                            if (JobParameters.SynchroBypassQueryFiltersInTarget)
                            {
                                //Errors.Add(new QError(QError.ErrorLevel.WARNING, qW.Where, Languages.Languages.par_errors_wheresynchrofail));
                            }
                            else
                            {
                                Errors.Add(new QError(QError.ErrorLevel.ERROR, qW.Where, Languages.Languages.par_errors_wheresynchrofail));
                            }
                        }
                    }
                }

                foreach (QGroupByOrderBy qGB in GroupBy)
                {
                    if (qGB.Field.Length == 0) { Errors.Add(new QError(QError.ErrorLevel.WARNING, qGB.Field, Languages.Languages.par_errors_ordergroupmissesfieldref)); }
                }
            }

            public void AddQueryProperty(string sElement, SQLTools_Enums.QUERY_PROPERTIES qP, string sValue, bool bHasToBeShownToUser)
            {
                bool bExists = false;
                foreach (var p in _sProperties)
                {
                    if (p.Property == qP)
                    {
                        int i;
                        int i2;
                        if (int.TryParse(sValue, out i) && int.TryParse(p.Value, out i2) && p.Element.Equals(sElement))
                        {
                            p.Value = (i + i2).ToString();
                        }
                        else
                        {
                            p.Value = string.Concat(p.Value, ",", sValue);
                            p.Element = string.Concat(p.Element, ",", sElement);
                        }
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    _sProperties.Add(new QProperty(sElement, qP, sValue, bHasToBeShownToUser));
                }
            }

            public void SetFieldsAnalyzer(List<SQLColumn> sListAnalyzer)
            {
                int iQteFields = Fields.Count;

                for (int iQ = 0; iQ < iQteFields; iQ++)
                {
                    SQLColumn sQ = sListAnalyzer.Any(f => f.ColumnName.Equals(Fields[iQ].Alias, StringComparison.InvariantCultureIgnoreCase)) ? sListAnalyzer.First(f => f.ColumnName.Equals(Fields[iQ].Alias, StringComparison.InvariantCultureIgnoreCase)) : null;
                    if (sQ != null)
                    {
                        Fields[iQ].FieldAnalyzer = sQ;
                        sListAnalyzer.Remove(sQ);
                    }
                }
                //gestion des manquants : il peut y avoir plus de champs analysés (select x.*, id_bidule, id_truc FROM...)
                foreach (SQLColumn sQ in sListAnalyzer)
                {
                    Fields.Add(new QField(sQ.ColumnName, sQ.ColumnName, "", "", "", Fields.Count - 1));
                    Fields[^1].FieldAnalyzer = sQ;
                }
                UpdateQueryAnalyzer();
            }

            internal void SetFieldsByQFields(List<QField> sListQFields)
            {
                Fields.Clear();
                for (int iF = 0; iF < sListQFields.Count; iF++)
                {
                    Fields.Add(sListQFields[iF]);
                }
                UpdateQueryAnalyzer();
            }

            public void SetFieldsBySQLColumns(string sTable, List<SQLColumn> sListColumns)
            {
                //on n'écrase pas si ce sont les mêmes colonnes
                int iOk = 0;
                foreach (Query.QField qF in Fields)
                {
                    if (sListColumns.Any(c => c.ColumnName.Equals(qF.Alias, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        qF.FieldAnalyzer = sListColumns.First(c => c.ColumnName.Equals(qF.Alias, StringComparison.InvariantCultureIgnoreCase));
                        iOk++;
                    }
                }

                if (iOk != Fields.Count)
                {
                    Fields.Clear();
                    for (int iC = 0; iC < sListColumns.Count; iC++)
                    {
                        Fields.Add(new QField(sListColumns[iC].ColumnName, sListColumns[iC].ColumnName, sTable, sTable, "", iC));
                        Fields[^1].FieldAnalyzer = sListColumns[iC];
                    }
                }
                UpdateQueryAnalyzer();
            }

            public static string GetTablesJoinLinkAsString(QTable qF)
            {
                string sJ = "";
                for (int iLf = 0; iLf < qF.LinkFields.Count; iLf++)
                {
                    sJ = string.Concat(sJ, qF.LinkFields[iLf][3],
                                              qF.LinkFields[iLf][3].Length > 0 ? "." : "",
                                              qF.LinkFields[iLf][0],
                                              " ",
                                              qF.LinkFields[iLf][2],
                                              " ",
                                              qF.LinkFields[iLf][4],
                                              qF.LinkFields[iLf][4].Length > 0 ? "." : "",
                                              qF.LinkFields[iLf][1],
                                              iLf == qF.LinkFields.Count - 1 ? "" : " AND ");
                }
                return sJ;
            }

            internal void UpdateQueryAnalyzer()
            {
                int iF = 0;
                foreach (Query.QField qF in Fields)
                {
                    iF++;
                    qF.Index = iF;
                    if (qF.Table.Length == 0)
                    {
                        foreach (Query.QTable qT in Tables)
                        {
                            Match mc = Regex.Match(Toolbox.RemoveRegexFromString(qF.Name), "^(" + Toolbox.RemoveRegexFromString(qT.Alias) + "|" + Toolbox.RemoveRegexFromString(qT.Name) + ")\\.");
                            if (mc.Success)
                            {
                                qF.Table = qT.Name;
                                qF.TableAlias = qT.Alias;
                                break;
                            }
                        }
                    }
                }

                List<QWhere> sListWHERE = Query_GetWhereV2(RawQuery, ConnectionSource, ConnectionTarget, Fields, Tables);

                //recherche des GROUP BY
                List<QGroupByOrderBy> sListGROUPBY = Query_GetGroupByOrderByV2(RawQuery, " GROUP BY ", new List<string> { " ORDER BY ", " HAVING " }, Fields, Tables);

                //recherche des ORDER BY
                List<QGroupByOrderBy> sListORDERBY = Query_GetGroupByOrderByV2(RawQuery, " ORDER BY ", new List<string> { " HAVING " }, Fields, Tables);

                QSynchroQuery sSynchroTargetQuery = QQuery.BuildSynchroTargetQuery(INIP, OutputTable, Fields, sListWHERE, ConnectionSource, ConnectionTarget);

                List<QProperty> sListProperties = QQuery.QueryAnalyzer.QueryProperties;

                QQuery.QueryAnalyzer = new QAnalyzer(INIP, QQuery, QQuery.RawQuery, FromStartsAt,
                                                    this.Fields, this.Tables, sListWHERE, sListGROUPBY, sListORDERBY,
                                                    this.UnionQueries, this.IsDistinct, this.LimitedResults, this.FetchCount, this.OffSetStart, this.SourceTableToHandle + 1,
                                                    this.GetOneTableOnlyFromSource, sSynchroTargetQuery, ConnectionSource, ConnectionTarget, QQuery.OutputTable);

                foreach (QProperty qP in sListProperties)
                {
                    QQuery.QueryAnalyzer.AddQueryProperty(qP.Element, qP.Property, qP.Value, qP.HasToBeShownToUser);
                }
            }

            internal void RemoveProperty(SQLTools_Enums.QUERY_PROPERTIES qp)
            {
                try
                {
                    _sProperties = _sProperties.Where(q => !q.Property.Equals(qp)).ToList();
                }
                catch
                {

                }
            }

            internal void MergeProperties(List<QProperty> qMergeProp)
            {
                foreach (QProperty newq in qMergeProp)
                {
                    QProperty oldq = _sProperties.FirstOrDefault(p => p.Property == newq.Property);
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
                            _sProperties.Add(newq);
                        }
                    }
                    else
                    {
                        _sProperties.Add(newq);
                    }
                }
            }

            internal void ChangeColumnPosition(string sColumnName, int iNewPosition)
            {
                QField sqlC = Fields.First(f => f.Name.Equals(sColumnName));
                int iOrdinal = Fields.IndexOf(sqlC);

                if (sqlC != null)
                {
                    for (int i = iOrdinal + 1; i <= Fields.Count; i++)
                    {
                        Fields[i - 1].Index -= 1;
                    }

                    Fields.Remove(sqlC);
                    Fields.Insert(iNewPosition - 1, sqlC);

                    sqlC.Index = iNewPosition;

                    for (int i = iNewPosition + 1; i <= Fields.Count; i++)
                    {
                        Fields[i - 1].Index += 1;
                    }
                }
            }

            internal void ChangeSourceTableToHandle(System.Data.DataSet dsData)
            {
                int iKeep = SourceTableToHandle;
                int iQteTables = dsData.Tables.Count;

                List<string> sTbToRemove = new();
                int iQteRemoved = 0;
                for (int iDt = 0; iDt < dsData.Tables.Count; iDt++)
                {
                    if (iDt != iKeep)
                    {
                        sTbToRemove.Add(dsData.Tables[iDt].TableName);
                        iQteRemoved += 1;
                    }
                }
                foreach (string sDt in sTbToRemove)
                {
                    dsData.Tables.Remove(sDt);
                }

                if (iQteRemoved == iQteTables - 1)
                {
                    SourceTableToHandle = 0;
                }
            }
        }

        internal class QTools
        {
            internal static string FindTableFromFilterField(string sField, List<QTable> qtList, List<QField> qfList)
            {
                string sTable = sField.IndexOf(".") > 0 ? sField.Split(Convert.ToChar("."))[0] : "";
                if (qtList.Count > 1)
                {
                    foreach (QTable qT in qtList)
                    {
                        if (sTable.Equals(qT.Name, StringComparison.OrdinalIgnoreCase) || sTable.Equals(qT.Alias, StringComparison.OrdinalIgnoreCase))
                        {
                            sTable = qT.Name; break;
                        }
                    }
                }
                else
                {
                    if (sTable.Length > 0)
                    {
                        sTable = qtList[0].Name;
                    }
                    else
                    {
                        //cas mono-table WHERE id_sample = 50 : on essaie de déterminer quand même si un champ est dispo dans la requête
                        foreach (QField qF in qfList)
                        {
                            if (sField.Equals(qF.Name, StringComparison.OrdinalIgnoreCase) || sField.Equals(qF.Alias, StringComparison.OrdinalIgnoreCase))
                            {
                                sTable = qF.Table; break;
                            }
                        }
                    }
                }

                return sTable;
            }
        }

        #region "VARIABLES"

        #endregion

        #region "PROPRIETES"

        private readonly Job JobParameters;

        public QAnalyzer QueryAnalyzer
        {
            get; internal set;
        }
        private QAnalyzer QueryAnalyzerB
        {
            get; set;
        }
        public string RawQuery { get; } = "";
        public CONNString ConnectionSrc
        {
            get; set;
        }
        public CONNString ConnectionTrg
        {
            get; set;
        }
        private CONNString ConnectionTrgB
        {
            get; set;
        }
        public List<Query> CrossJoinQueries { get; internal set; } = new List<Query>();
        public SQLTools_Enums.CROSSJOIN_TYPES CrossJoinType
        {
            get; internal set;
        }

        public string CrossQueryRawScript { get; private set; } = "";
        public string CrossJoinQueryWhere { get; private set; } = "";
        public List<string> CrossQueryForceJoinFields { get; private set; } = new List<string>();
        public bool HasMultiTarget { get; private set; } = false;
        public int CrossJoinDepth { get; private set; } = 0;
        public string SQLQuery { get; private set; } = "";
        public string OutputTable { get; private set; } = "";
        private string OutputTableB { get; set; } = "";
        public string RawOutput { get; private set; } = "";
        public bool IsEnabled { get; private set; } = true;
        private bool AnalyzeErrors { get; set; } = true;
        public int QueryErrors { get; internal set; } = 0;
        public SQLTools_Enums.LOG_TYPEINFO RetryErrorOrWarning { get { return Retries < JobRetry ? SQLTools_Enums.LOG_TYPEINFO.WNG : SQLTools_Enums.LOG_TYPEINFO.ERR; } }
        public int Retries { get; internal set; } = 0;
        public int JobRetry { get; internal set; } = 0;

        #endregion

        #region "STATIC"

        private QAnalyzer AnalyzeQuery(string sSQLQuery, CONNString CSs, CONNString CSt, ref string sOutputTable)
        {
            if (CSs.SConnDriverSuffix.Equals("DB"))
            {
                //new version pour ne pas casser les tables type mail (select * from myadress@myserver.com[password])
                sSQLQuery = Regex.Replace(sSQLQuery, "([,.(\\s\\n\\t\\r]{1})(\\[)([^--<>]{2})(\\s*)(\\w+)(\\s*)(\\])", "$1$3$5");
                //sSQLQuery = Regex.Replace(sSQLQuery, "(\\[{1})([\\d\\w_]+)(\\]{1})", "$2");
            }

            string sOriginalQ = sSQLQuery;


            bool bIsDistinct = false;
            int iLimitedResults = 0;
            int iTableToHandle = 1;
            bool bOneTableOnly = false;

            int iFrom = 0;

            //test LIMIT 100
            MatchCollection mcBottom = Regex.Matches(sSQLQuery, SHSRegex.REGEX_QUERY_BOTTOM, RegexOptions.IgnoreCase);
            if (mcBottom.Count > 0)
            {
                sSQLQuery = sSQLQuery[..mcBottom[^1].Index];
                string sLimited = mcBottom[^1].Value;
                sLimited = sLimited.Trim()[6..];
                try { iLimitedResults = Convert.ToInt32(sLimited); } catch { }
            }

            //test FETCH 
            string fetchOffset = @"(?:OFFSET\s+(\d+)\s+(?:ROW|ROWS))|(?:FETCH\s+(?:FIRST|NEXT)\s*(\d*)\s*(?:ROW|ROWS)\s*(?:ONLY|WITH\s+TIES)?)";
            Regex regex = new Regex(fetchOffset, RegexOptions.IgnoreCase);

            int iOffSetStart = 0;
            int iFetchCount = 0;
            MatchCollection matches = regex.Matches(sSQLQuery);

            foreach (Match match in matches)
            {
                // Capturing groups
                if (iOffSetStart == 0) { int.TryParse(match.Groups[1].Value, out iOffSetStart); }
                if (iFetchCount == 0) { int.TryParse(match.Groups[2].Value, out iFetchCount); }
            }

            //recherche des requêtes UNION
            int iPosUnion = 0;
            List<string> sListUNIONS = Query_GetUnionQueries(sSQLQuery, ref iPosUnion);

            //si il y a des UNION, on coupe la requête initiale pour ne garder que la première 
            if (sListUNIONS.Count > 0)
            {
                sSQLQuery = sSQLQuery[..iPosUnion];
            }

            //recherche des tables dans la requêtes
            List<QTable> sListTABLES = Query_GetTablesV2(sSQLQuery, ref iFrom, CSs);

            if (sListTABLES.Count == 0)
            {
                throw new Exception(Languages.Languages.sql_query_notable);
            }

            //recherche des champs
            List<QField> sListCOLUMNS = new();

            sSQLQuery = sSQLQuery[..iFrom].Trim();

            //on vire le premier select
            if (sSQLQuery.StartsWith("SELECT DISTINCT", StringComparison.InvariantCultureIgnoreCase)) { sSQLQuery = sSQLQuery[15..]; bIsDistinct = true; }

            if (sSQLQuery.StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase)) { sSQLQuery = sSQLQuery[6..]; }

            //test TOP 100
            MatchCollection mcTop = Regex.Matches(sSQLQuery, SHSRegex.REGEX_QUERY_TOP, RegexOptions.IgnoreCase);
            if (mcTop.Count > 0)
            {
                sSQLQuery = sSQLQuery[(mcTop[0].Index + mcTop[0].Length)..];
                //string sLimited = mcTop[^1].Value;
                //sLimited = sLimited.Trim()[4..];
                string sLimited = mcTop[^1].Groups[mcTop[^1].Groups.Count - 1].Value;
                try { iLimitedResults = Convert.ToInt32(sLimited); } catch { }
            }

            //test TABLE X (méthode qui permet de traiter une table en particulier dans les résultats retournés par la requête
            MatchCollection mcTable = Regex.Matches(sSQLQuery, SHSRegex.REGEX_QUERY_TABLE, RegexOptions.IgnoreCase);
            if (mcTable.Count > 0)
            {
                sSQLQuery = sSQLQuery[(mcTable[0].Index + mcTable[0].Length)..];
                iTableToHandle = Convert.ToInt16(Regex.Match(mcTable[0].Value, "\\d").Value);
                if (mcTable[0].Value.IndexOf("ONLY", StringComparison.InvariantCultureIgnoreCase) > -1) { bOneTableOnly = true; }
            }

            sSQLQuery = sSQLQuery.Trim();

            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iGuill = 0;
            int iVirgBefore = 0;
            int iVirgAfter = -1;
            char[] scQuery = sSQLQuery.ToCharArray();

            //attention !! pète si on met SELECT '(' as par, id_sample as sample FROM mytable
            for (int iC = 0; iC < scQuery.Length; iC++)
            {
                if (scQuery[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                if (scQuery[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                if (scQuery[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scQuery[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                if (scQuery[iC].Equals(Convert.ToChar(",")) && iParLeft == iParRight) //on a trouvé un champ
                {
                    iVirgBefore = iVirgAfter + 1;
                    iVirgAfter = iC;
                    string sF = sSQLQuery[iVirgBefore..iVirgAfter].Trim();
                    sListCOLUMNS.Add(new QField(sF, sListTABLES, sListCOLUMNS.Count, CSs));
                }
                else if (scQuery[iC].Equals(Convert.ToChar(",")) && (iParLeft != iParRight && iApos % 2 != 0)) //gestion des STRING_AGG(ActiviteId, ',') et des //gestion des SELECT MAX(d'accord)
                {
                    //contrôle logique : on doit avoir un apostrophe, une fermeture de parenthèse et une virgule à la suite
                    int iNextPar = 0;
                    int iNextAPos = 0;
                    int iNextVirg = 0;
                    for (int iCtrl = iC + 1; iCtrl < scQuery.Length; iCtrl++)
                    {
                        if (scQuery[iCtrl].Equals(Convert.ToChar(")"))) { iNextPar = iCtrl; }
                        if (scQuery[iCtrl].Equals(Convert.ToChar("'"))) { iNextAPos = iCtrl; }
                        if (scQuery[iCtrl].Equals(Convert.ToChar(","))) { iNextVirg = iCtrl; }
                        if (iNextAPos > 0 && iNextPar > 0 && iNextVirg > 0) { break; }
                    }
                    if (iNextAPos < iNextPar && (iNextPar < iNextVirg || iNextVirg == 0))
                    {
                        //on laisse tourner pour le moment : ça veut dire qu'on est à STRING_AGG(ActiviteId,
                    }
                    else
                    {
                        iVirgBefore = iVirgAfter + 1;
                        iVirgAfter = iC;
                        string sF = sSQLQuery[iVirgBefore..iVirgAfter].Trim();
                        sListCOLUMNS.Add(new QField(sF, sListTABLES, sListCOLUMNS.Count, CSs));
                    }
                }
            }
            if (iParLeft == iParRight) //dernier champ
            {
                string sF = sSQLQuery[(iVirgAfter + 1)..].Trim();
                sListCOLUMNS.Add(new QField(sF, sListTABLES, sListCOLUMNS.Count, CSs));
            }
            else if (iParLeft != iParRight && iApos % 2 != 0) //gestion des SELECT MAX(d'accord)
            {
                string sF = sSQLQuery[(iVirgAfter + 1)..].Trim();
                sListCOLUMNS.Add(new QField(sF, sListTABLES, sListCOLUMNS.Count, CSs));
            }

            //mise en place des index de colonnes (dans le cas des inner join et des champs mélangés, on remet en ordre - surtout utile pour les requêtes sur fichiers avec jointures)
            int iNewFile = 0;
            for (int iC = 0; iC < sListCOLUMNS.Count; iC++) //pose d'un index
            {
                if (iC > 0 && (!sListCOLUMNS[iC].Table.Equals(sListCOLUMNS[iC - 1].Table))) { iNewFile = iC; } //gestion du changement de table/fichier
                sListCOLUMNS[iC].Index = iC - iNewFile;
            }

            if (sListCOLUMNS.Count == 0)
            {
                throw new Exception(Languages.Languages.sql_query_nocolumn);
            }

            //recherche des WHERE
            List<QWhere> sListWHERE = Query_GetWhereV2(sOriginalQ, CSs, CSt, sListCOLUMNS, sListTABLES);

            //recherche des GROUP BY
            List<QGroupByOrderBy> sListGROUPBY = Query_GetGroupByOrderByV2(sOriginalQ, " GROUP BY ", new List<string> { " ORDER BY ", " HAVING " }, sListCOLUMNS, sListTABLES);

            //recherche des ORDER BY
            List<QGroupByOrderBy> sListORDERBY = Query_GetGroupByOrderByV2(sOriginalQ, " ORDER BY ", new List<string> { " HAVING " }, sListCOLUMNS, sListTABLES);

            //le cas ou la requête avait été initialisée sans "matable:select..."
            if (sOutputTable.Equals("QUERY"))
            {
                sOutputTable = sListTABLES[0].Alias;
            }

            QSynchroQuery sSynchroTargetQuery = BuildSynchroTargetQuery(JobParameters, sOutputTable, sListCOLUMNS, sListWHERE, CSs, CSt);

            return new QAnalyzer(JobParameters, this, sOriginalQ, iFrom, sListCOLUMNS, sListTABLES, sListWHERE, sListGROUPBY, sListORDERBY, sListUNIONS, bIsDistinct, iLimitedResults, iFetchCount, iOffSetStart, iTableToHandle, bOneTableOnly, sSynchroTargetQuery, CSs, CSt, sOutputTable);
        }

        private static List<QGroupByOrderBy> Query_GetGroupByOrderByV2(string sSQLQuery, string sGroup, List<string> sNextGroups, List<QField> qFields, List<QTable> qTables)
        {
            List<QGroupByOrderBy> sListGroupBy = new();

            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iGroupBy = 0;

            char[] scQuery = sSQLQuery.ToCharArray();

            for (int iC = 0; iC < scQuery.Length; iC++)
            {
                if (scQuery[iC].Equals(Convert.ToChar("'"))) { iApos++; }
                if (scQuery[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scQuery[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                if (iParLeft == iParRight
                    && sSQLQuery[iC..].Length >= sGroup.Length
                    && sSQLQuery.Substring(iC, sGroup.Length).Equals(sGroup, StringComparison.InvariantCultureIgnoreCase))
                {
                    iGroupBy = iC; break;
                }
            }

            if (iGroupBy > 0)
            {
                string sGroupBy = sSQLQuery[(iGroupBy + sGroup.Length)..];
                char[] scGB = sGroupBy.ToCharArray();

                iParLeft = 0;
                iParRight = 0;
                iApos = 0;
                int iPosGB = 0;
                int iPosNextG = sGroupBy.Length;

                for (int iC = 0; iC < scGB.Length; iC++)
                {
                    if (scGB[iC].Equals(Convert.ToChar("'"))) { iApos++; }
                    if (scGB[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                    if (scGB[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                    if (scGB[iC].Equals(Convert.ToChar(","))
                        && iParLeft == iParRight)
                    {
                        string sG = sGroupBy[iPosGB..iC].Trim();
                        sListGroupBy.Add(new QGroupByOrderBy(sGroup.Trim(), sG, qFields, qTables));
                        iPosGB = iC + 1;
                    }
                }

                if (iParLeft == iParRight)
                {
                    foreach (string sGN in sNextGroups)
                    {
                        string sTmp = sGroupBy[iPosGB..];
                        if (sTmp.Contains(sGN, StringComparison.InvariantCultureIgnoreCase))
                        {
                            iPosNextG = sGroupBy.IndexOf(sGN, StringComparison.InvariantCultureIgnoreCase); break;
                        }
                    }
                }

                if (sListGroupBy.Count == 0) { sListGroupBy.Add(new QGroupByOrderBy(sGroup.Trim(), sGroupBy[..iPosNextG].Trim(), qFields, qTables)); }
                else { sListGroupBy.Add(new QGroupByOrderBy(sGroup.Trim(), sGroupBy[iPosGB..iPosNextG].Trim(), qFields, qTables)); }
            }

            return sListGroupBy;
        }

        private static List<QWhere> Query_GetWhereV2(string sSQLQuery, CONNString sCS, CONNString sCT, List<QField> qFields, List<QTable> qTables)
        {
            List<QWhere> sListWhere = new();

            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iWhere = 0;

            char[] scQuery = sSQLQuery.ToCharArray();

            for (int iC = 0; iC < scQuery.Length; iC++)
            {
                if (scQuery[iC].Equals(Convert.ToChar("'"))) { iApos++; }
                if (scQuery[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scQuery[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                if (iParLeft == iParRight
                    && sSQLQuery[iC..].Length >= 7
                    && sSQLQuery.Substring(iC, 7).Equals(" WHERE ", StringComparison.InvariantCultureIgnoreCase))
                {
                    iWhere = iC; break;
                }
            }

            if (iWhere > 0)
            {
                string sWhere = sSQLQuery[(iWhere + 7)..];
                char[] scW = sWhere.ToCharArray();

                iParLeft = 0;
                iParRight = 0;
                iApos = 0;
                int iPosWhere = 0;
                int iPosNextG = sWhere.Length;

                for (int iC = 0; iC < scW.Length; iC++)
                {
                    if (scW[iC].Equals(Convert.ToChar("'"))) { iApos++; }
                    if (scW[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                    if (scW[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                    if (iParLeft == iParRight
                        && sWhere[iC..].Length >= 5
                        && sWhere.Substring(iC, 5).Equals(" AND ", StringComparison.InvariantCultureIgnoreCase))
                    {
                        sListWhere.Add(new QWhere(sWhere.Substring(iPosWhere, iC - iPosWhere + 1).Trim(), sCS, sCT, qFields, qTables, ""));
                        iPosWhere = iC + 5;
                    }
                    if (iParLeft == iParRight
                        && sWhere[iC..].Length >= 10
                        && sWhere.Substring(iC, 10).Equals(" GROUP BY ", StringComparison.InvariantCultureIgnoreCase))
                    {
                        iPosNextG = sWhere.IndexOf(" GROUP BY ", StringComparison.InvariantCultureIgnoreCase);
                        sWhere = sWhere[..iPosNextG];
                        break;
                    }
                    if (iParLeft == iParRight
                        && sWhere[iC..].Length >= 10
                        && sWhere.Substring(iC, 10).Equals(" ORDER BY ", StringComparison.InvariantCultureIgnoreCase))
                    {
                        iPosNextG = sWhere.IndexOf(" ORDER BY ", StringComparison.InvariantCultureIgnoreCase);
                        sWhere = sWhere[..iPosNextG];
                        break;
                    }
                }

                if (sListWhere.Count == 0)
                {
                    sListWhere.Add(new QWhere(sWhere[..iPosNextG].Trim(), sCS, sCT, qFields, qTables, ""));
                }
                else { sListWhere.Add(new QWhere(sWhere[iPosWhere..].Trim(), sCS, sCT, qFields, qTables, "")); }
            }

            return sListWhere;
        }

        private static List<string> Query_GetUnionQueries(string sSQLQuery, ref int iPosUnion)
        {
            List<string> sListUnions = new();
            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;

            //on recherche d'abord le "FROM" qui suit les champs de requête, donc celui qui intervient après un "équilibre de parenthèses" (en cas de requête complexe)
            char[] scQuery = sSQLQuery.ToCharArray();

            List<Match> mcUnion = new();
            List<int> iIdxUnion = new();

            for (int iC = 0; iC < scQuery.Length; iC++)
            {
                if (scQuery[iC].Equals(Convert.ToChar("'"))) { iApos++; }
                if (scQuery[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scQuery[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                if (iParLeft == iParRight)
                {
                    Match mc = Regex.Match(sSQLQuery[iC..], "UNION[\\s]*[(]*[\\s]*SELECT", RegexOptions.IgnoreCase);
                    if (mc.Length > 0 && mc.Index == 0)
                    {
                        mcUnion.Add(mc);
                        iIdxUnion.Add(iC);
                        if (iPosUnion == 0)
                        {
                            iPosUnion = iC;
                        }
                    }
                }
            }
            for (int iM = 0; iM < mcUnion.Count; iM++)
            {
                if (iM < mcUnion.Count - 1)
                {
                    sListUnions.Add(string.Concat("SELECT ", sSQLQuery.Substring(iIdxUnion[iM] + mcUnion[iM].Length, iIdxUnion[iM + 1] - iIdxUnion[iM] - mcUnion[iM].Length).Trim()));
                }
                else { sListUnions.Add(string.Concat("SELECT ", sSQLQuery[(iIdxUnion[iM] + mcUnion[iM].Length)..].Trim())); }
            }
            return sListUnions;
        }

        private static List<QTable> Query_GetTablesV2(string sSQLQuery, ref int iFrom, CONNString sCS)
        {
            List<QTable> sListTables = new();

            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iGuill = 0;
            iFrom = 0;

            //on recherche d'abord le "FROM" qui suit les champs de requête, donc celui qui intervient après un "équilibre de parenthèses" (en cas de requête complexe)
            char[] scQuery = sSQLQuery.ToCharArray();

            for (int iC = 0; iC < scQuery.Length; iC++)
            {
                if (scQuery[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                if (scQuery[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                if (scQuery[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scQuery[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }

                //exemple de requête foireuse : select x, case when y in ('1','2' then 1 else 2 end as test FROM matable
                if (iC >= sSQLQuery.Length - 6 && sSQLQuery.LastIndexOf(" FROM ", StringComparison.InvariantCultureIgnoreCase) > 0)
                {
                    return new List<QTable>();
                }

                if (iParLeft == iParRight
                    && sSQLQuery.Contains(" FROM ", StringComparison.InvariantCultureIgnoreCase)
                    && sSQLQuery.Substring(iC, 6).Equals(" FROM ", StringComparison.InvariantCultureIgnoreCase))
                {
                    iFrom = iC; break;
                }
                else if (iParLeft != iParRight && iApos % 2 != 0 //cas spécial SELECT MAX(test d'avancement) FROM
                    && sSQLQuery.Contains(" FROM ", StringComparison.InvariantCultureIgnoreCase)
                    && sSQLQuery.Substring(iC, 6).Equals(" FROM ", StringComparison.InvariantCultureIgnoreCase))
                {
                    iFrom = iC; break;
                }
            }

            //recherche des FROM (et des alias)
            string sQueue = sSQLQuery[(iFrom + 6)..].Trim();

            iParLeft = 0;
            iParRight = 0;
            iApos = 0;
            iGuill = 0;

            int iPosInterrupt = sQueue.Length;
            int iFirstPar = 0;

            char[] scQueue = sQueue.ToCharArray();
            for (int iC = 0; iC < scQueue.Length; iC++)
            {
                if (scQueue[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                if (scQueue[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                if (scQueue[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; if (iFirstPar == 0) { iFirstPar = iC; } }
                if (scQueue[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }

                //on cherche les jointures pour trouver les différentes tables requêtées
                if (sQueue.Length >= iC && iParLeft == iParRight)
                {
                    //string sTemp = sQueue[iC..];
                    //détection d'une fonction SQL et non pas un sous-select ( select * from dbo.myfunction(blabla) )
                    //FROM [dbo].[CNV_fnGetListeCollaborateursNayantPasRempliChronosProchain15j] (DATEADD(DAY, 4, GETDATE()),DATEADD(DAY, 15, GETDATE()))
                    //if (iParLeft == iParRight && iParLeft > 0
                    //   && iFirstPar > 2)
                    //{
                    //    iPosInterrupt = iC + 1; break;
                    //}
                    //on gère les interruptions //problème du WHERE 1 = 1 AND y = (select test from x = 1)
                    if (iParLeft == iParRight
                        && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+WHERE\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                    if (iParLeft == iParRight
                        && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+GROUP BY\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                    if (iParLeft == iParRight
                       && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+ORDER BY\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                    if (iParLeft == iParRight
                      && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+LIMIT \\d\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                    if (iParLeft == iParRight
                      && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+OFFSET \\d\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                    if (iParLeft == iParRight
                        && Regex.Match(sQueue[iC..], "^[\\]\\)]?\\s+FETCH\\s+(NEXT|FIRST)\\s+\\d\\s*[\\[\\(]?", RegexOptions.IgnoreCase).Success)
                    {
                        iPosInterrupt = iC + 1; break;
                    }
                }
            }

            //on extrait la partie "FROM" de la requête jusqu'à la partie "WHERE" ou "GROUP BY" ou "ORDER BY"
            //on a potentiellement des jointures normales ou bien des jointures avec des sous-requêtes ou bien rien du tout
            string sPattern = sQueue[..iPosInterrupt].Trim();
            string sTable;
            string sJoinType = "";


            //nettoyage du NOLOCK SQL SERVER
            sPattern = Regex.Replace(sPattern, "\\s+WITH\\s+\\(NOLOCK\\)", " ", RegexOptions.IgnoreCase);

            char[] cSubPattern = sPattern.ToCharArray();
            int iParSubLeft = 0;
            int iParSubRight = 0;
            int iAposSub = 0;
            int iGuillSub = 0; // gestion des requêtes type SELECT 'madata' as "plus d'attention" FROM mytable
            int iPosPrec = 0;
            bool bFirstOK = false;
            int iPosON = 0;


            for (int iC2 = 0; iC2 < cSubPattern.Length; iC2++)
            {
                string sTestTable = sPattern[iC2..];

                if (cSubPattern[iC2].Equals(Convert.ToChar("\""))) { iGuillSub++; }
                if (cSubPattern[iC2].Equals(Convert.ToChar("'")) && iGuillSub % 2 == 0) { iAposSub++; }
                if (cSubPattern[iC2].Equals(Convert.ToChar("(")) && iAposSub % 2 == 0) { iParSubLeft++; }
                if (cSubPattern[iC2].Equals(Convert.ToChar(")")) && iAposSub % 2 == 0) { iParSubRight++; }

                //on cherche les tables jointes---------------------------------------------------------
                if (iParSubLeft == iParSubRight
                    && bFirstOK)
                {
                    if (Regex.Match(sTestTable, "^(([\\)\\]]{1}\\s*)|\\s+)ON(\\s+|(\\s*[\\(\\[]{1}))", RegexOptions.IgnoreCase).Success)
                    {
                        iPosON = iC2;
                    }
                    else
                    {
                        Match mcJoin = Regex.Match(sTestTable, "^(([\\)\\]]{1}\\s*)|\\s+)((LEFT|RIGHT|INNER|OUTER|CROSS|LEFT\\s+OUTER|RIGHT\\s+OUTER|FULL\\s+OUTER)\\s+)?(JOIN|APPLY)(\\s+|(\\s*[\\(\\[]{1}))", RegexOptions.IgnoreCase);
                        if (mcJoin.Success)
                        {
                            sTable = sPattern[iPosPrec..].Trim();
                            //gestion du " ON " de la jointure
                            if ((iPosON - iPosPrec) > 0)
                            {
                                sTable = sTable[..(iPosON - iPosPrec)];
                            }
                            string[] sT = Query_GetTableAndAlias(sTable, sCS);
                            //lien entre les tables
                            List<string[]> sListLink = Query_GetLinkInJoinV2(sPattern[iPosON..iC2]);
                            QTable qT = new(sT[0], sT[1], sJoinType, sListLink, sCS);
                            sListTables.Add(qT);
                            sJoinType = Toolbox.RemoveSpecialCharacters(mcJoin.Value, " ", true).Trim().ToUpper();

                            int iOffset = mcJoin.Length;
                            if (mcJoin.Value.EndsWith("(")) { iOffset -= 1; }
                            else if (mcJoin.Value.EndsWith("[")) { iOffset -= 1; }

                            iPosPrec = iC2 + iOffset;
                            iC2 = iC2 + iOffset - 1;
                        }

                        if (iC2 == (sPattern.Length - 1)) // cas de la dernière jointure
                        {
                            sTable = sPattern[iPosPrec..].Trim();
                            //gestion du " ON " de la jointure
                            Match mcON = Regex.Match(sTable, "(([\\)\\]]{1}\\s*)|\\s+)ON((\\s*[\\(\\[]){1}|\\s*)", RegexOptions.IgnoreCase);
                            if (mcON.Success)
                            {
                                sTable = sTable[..mcON.Index].Trim();
                            }
                            string[] sT = Query_GetTableAndAlias(sTable, sCS);
                            //lien entre les tables
                            List<string[]> sListLink = Query_GetLinkInJoinV2(sPattern[iPosPrec..].Trim()[mcON.Index..]);
                            QTable qT = new(sT[0], sT[1], sJoinType, sListLink, sCS);
                            sListTables.Add(qT);
                        }
                    }
                }

                //on cherche la première table---------------------------------------------------------
                if (iParSubLeft == iParSubRight
                    && (!bFirstOK))
                {
                    //WITH\\s+\\(NOLOCK\\)|
                    Match mcJoinB = Regex.Match(sTestTable, "^(([\\)\\]]{1}\\s*)|\\s+)((LEFT|RIGHT|INNER|OUTER|CROSS|LEFT\\s+OUTER|RIGHT\\s+OUTER|FULL\\s+OUTER)\\s+)?(JOIN|APPLY)(\\s+|(\\s*[\\(\\[]{1}))", RegexOptions.IgnoreCase);
                    if (mcJoinB.Success)
                    {
                        sTable = sPattern[..iC2].Trim();
                        string[] sT = Query_GetTableAndAlias(sTable, sCS);
                        QTable qT = new(sT[0], sT[1], "", null, sCS);
                        sListTables.Add(qT);
                        sJoinType = Toolbox.RemoveSpecialCharacters(mcJoinB.Value, " ", true).Trim().ToUpper();

                        int iOffset = mcJoinB.Length;
                        if (mcJoinB.Value.EndsWith("(")) { iOffset -= 1; }
                        else if (mcJoinB.Value.EndsWith("[")) { iOffset -= 1; }

                        iPosPrec = iC2 + iOffset;
                        iC2 = iC2 + iOffset - 1;
                        bFirstOK = true;
                    }
                    if (iC2 == (sPattern.Length - 1)) // cas d'une table unique sans jointures
                    {
                        sTable = sPattern[..(iC2 + 1)].Trim();
                        string[] sT = Query_GetTableAndAlias(sTable, sCS);
                        QTable qT = new(sT[0], sT[1], "", null, sCS);
                        sListTables.Add(qT);
                        bFirstOK = true;
                    }
                }

            }

            if (sListTables.Count == 0)
            {
                sListTables.Add(new QTable("NOT_FOUND", "NOT_FOUND", "", null, sCS));
            }

            return sListTables;
        }

        private static string[] Query_GetTableAndAlias(string sTable, CONNString sCS)
        {
            sTable = sTable.Trim();
            string REGEX_FIELD = "\\w+"; //chars, digits, underscores
            string[] sCompletedTable = new string[] { "", "" };
            if (sTable.Length > 0)
            {
                string sAlias = "";
                int iAsIndex = -1;
                int iSpaceIndex = -1;
                int iParLeft = 0;
                int iParRight = 0;
                int iApos = 0;
                int iGuill = 0;

                char[] scQueue = sTable.ToCharArray();
                for (int iC = 0; iC < scQueue.Length; iC++)
                {
                    if (scQueue[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                    if (scQueue[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                    if (scQueue[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                    if (scQueue[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }

                    //on cherche les jointures pour trouver les différentes tables requêtées
                    if (sTable.Length >= iC && iParLeft == iParRight)
                    {
                        //on gère les interruptions //problème du WHERE 1 = 1 AND y = (select test from x = 1)
                        if (iParLeft == iParRight && Regex.IsMatch(sTable[iC..], "\\)\\s*AS ", RegexOptions.IgnoreCase))
                        {
                            if (iC > 0)
                            { iAsIndex = iC + 1; break; }
                            else
                            { iAsIndex = Regex.Match(sTable[iC..], "\\)\\s*AS ", RegexOptions.IgnoreCase).Index + 1; break; }
                        }
                        else if (iParLeft == iParRight
                            && sTable[iC..].StartsWith(" AS ", StringComparison.InvariantCultureIgnoreCase))
                        {
                            iAsIndex = iC; break;
                        }
                    }
                }

                //iAsIndex = sTable.LastIndexOf(" as ", StringComparison.InvariantCultureIgnoreCase);

                //subtilité : une requête SQL peut avoir un select monchamp test (test devenant un alias) alors qu'on ne peut pas dans tous les autres cas
                if (sCS != null && sCS.SConnDriverSuffix.Equals("DB") && (sTable.LastIndexOf(")") < sTable.LastIndexOf(" "))) { iSpaceIndex = sTable.LastIndexOf(" ", StringComparison.InvariantCultureIgnoreCase); }
                if (sCS != null && sCS.SConnDriverSuffix.Equals("DB") && Regex.IsMatch(sTable, "\\(\\s*SELECT", RegexOptions.IgnoreCase) && Regex.IsMatch(sTable, "\\)\\s*.+", RegexOptions.IgnoreCase))
                {
                    iSpaceIndex = sTable.LastIndexOf(")") + 1;
                }


                //alias
                if (iAsIndex > 0)
                {
                    string sTmp = sTable[(iAsIndex + 4)..].Trim();
                    sAlias = Regex.IsMatch(sTmp, REGEX_FIELD) ? sTmp : "";
                }
                else if (iSpaceIndex > 0)
                {
                    string sTmp = sTable[iSpaceIndex..].Trim();
                    sAlias = Regex.IsMatch(sTmp, REGEX_FIELD) ? sTmp : "";
                }
                //nom table

                if (iAsIndex > 0)
                {
                    sCompletedTable = new string[] { sTable[..iAsIndex].Trim(), sAlias };
                }
                else if (iSpaceIndex > 0)
                {
                    sCompletedTable = new string[] { sTable[..iSpaceIndex].Trim(), sAlias };
                }
                else
                {
                    sCompletedTable = new string[] { sTable.Trim(), "" };
                }
            }

            return sCompletedTable;
        }

        private static List<string[]> Query_GetLinkInJoinV2(string sPattern)
        {
            List<string[]> sListLink = new();

            Match mcON = Regex.Match(sPattern, "^\\s*ON\\s*", RegexOptions.IgnoreCase);

            if (mcON.Success)
            {
                sPattern = sPattern[mcON.Length..];
            }

            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iPosPrec = 0;
            int iGuill = 0;

            string sLink;
            //on peut avoir des parenthèses dans le on (et même des subqueries !)
            char[] scLink = sPattern.ToCharArray();

            for (int iC = 0; iC < scLink.Length; iC++)
            {
                if (scLink[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                if (scLink[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                if (scLink[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scLink[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }

                //cas des : (T1.GENERALJOURNALENTRY=T2.RECID AND (T1.PARTITION = T2.PARTITION))
                if ((iParLeft == iParRight || (sPattern.StartsWith("(") && sPattern.EndsWith(")")))
                    && (sPattern[iC..].StartsWith(" and ", StringComparison.InvariantCultureIgnoreCase) || sPattern[iC..].StartsWith(" or ", StringComparison.InvariantCultureIgnoreCase)))
                {
                    sLink = sPattern[iPosPrec..iC];
                    if (iParLeft != iParRight) { while (sLink.StartsWith("(")) { sLink = sLink[1..]; } }
                    if (iParLeft != iParRight) { while (sLink.EndsWith(")")) { sLink = sLink[0..^1]; } }
                    string[] sL = Query_GetLinkSeparator(sLink);
                    if (sL[0].Length > 0) { sListLink.Add(sL); }
                    iPosPrec = iC + (sPattern[iC..].StartsWith(" and ", StringComparison.InvariantCultureIgnoreCase) ? 5 : 4);

                }
                //dernier lien
                if (iParLeft == iParRight
                    && iC == scLink.Length - 1)
                {
                    sLink = sPattern.Substring(iPosPrec, iC - (iPosPrec - 1));
                    while (sLink.StartsWith("(")) { sLink = sLink[1..]; }
                    while (sLink.EndsWith(")")) { sLink = sLink[0..^1]; }
                    string[] sL = Query_GetLinkSeparator(sLink);
                    if (sL[0].Length > 0) { sListLink.Add(sL); }
                }
            }

            return sListLink;
        }

        private static void GetCrossJoinScriptsFromString(ref List<string> sCrossJoinPatterns, ref List<int> iCrossJoinPatterns, string sQ, CONNStrings Connections, List<string> sListDynParams)
        {
            sQ = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sQ, sListDynParams);

            //MatchCollection mcCJ = Regex.Matches(sQ, "\\[..\\[[0-9]+\\](\\s*WHERE\\s+.+)*\\]", RegexOptions.IgnoreCase);
            MatchCollection mcCJ = Regex.Matches(sQ, SHSRegex.REGEX_CROSSJOINSCRIPT_B, RegexOptions.IgnoreCase);
            foreach (Match mc in mcCJ)
            {
                //contrôle quantité parenthèses avant et après
                MatchCollection mcP1A = Regex.Matches(sQ[..mc.Index], "\\(");
                MatchCollection mcP1B = Regex.Matches(sQ[..mc.Index], "\\)");
                MatchCollection mcP2A = Regex.Matches(sQ[(mc.Index + mc.Length)..], "\\(");
                MatchCollection mcP2B = Regex.Matches(sQ[(mc.Index + mc.Length)..], "\\)");

                if (mcP1A.Count == mcP1B.Count && mcP2A.Count == mcP2B.Count)
                {
                    string sConn = Regex.Match(mc.Value, "\\[[0-9]+\\]").Value;
                    if (Connections != null)
                    {
                        if (sConn.Length > 0)
                        {
                            if (Connections.GetIndexSection(sConn) > -1)
                            {
                                sCrossJoinPatterns.Add(mc.Value);
                                iCrossJoinPatterns.Add(mc.Index);
                            }
                        }
                    }
                    else //attention, ici c'est un simple postulat
                    {
                        sConn = sConn[1..];
                        sConn = sConn[0..^1];
                        if (int.TryParse(sConn, out _))
                        {
                            sCrossJoinPatterns.Add(mc.Value);
                            iCrossJoinPatterns.Add(mc.Index);
                        }
                    }
                }
            }
        }

        public static string[] Query_GetLinkSeparator(string sLinkPattern)
        {
            //0 : champ table 1 / 1 : champ table 2 / 3 : caractère de séparation / 4 : table 1 / 5 : table 2
            string[] sLinkSeparator = new string[] { "", "", "", "", "", "", "" };
            int iParLeft = 0;
            int iParRight = 0;
            int iApos = 0;
            int iGuill = 0;

            char[] scLink = sLinkPattern.ToCharArray();

            for (int iC = 0; iC < scLink.Length; iC++)
            {
                if (scLink[iC].Equals(Convert.ToChar("\""))) { iGuill++; }
                if (scLink[iC].Equals(Convert.ToChar("'")) && iGuill % 2 == 0) { iApos++; }
                if (scLink[iC].Equals(Convert.ToChar("(")) && iApos % 2 == 0) { iParLeft++; }
                if (scLink[iC].Equals(Convert.ToChar(")")) && iApos % 2 == 0) { iParRight++; }
                if (iParLeft == iParRight)
                {
                    foreach (string sSep in SHSConstantes.JOIN_SEPARATORS)
                    {
                        if (sLinkPattern[iC..].StartsWith(sSep, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //risque identifié : INNER JOIN COL_COL_Collaborateur as inte on inte.COL_ID = ent.ENE_Intervenant_ID
                            //avec "inte" étant un début de séparateur
                            bool bBypassAnalysis = false;

                            if (Regex.IsMatch(sSep, "[A-z ]+")) //contrôle additionnel de contournement
                            {
                                if (!sLinkPattern[iC..].Substring(sSep.Length, 1).Equals(string.Empty))
                                { bBypassAnalysis = true; }
                            }

                            if (!bBypassAnalysis)
                            {
                                string s1 = sLinkPattern[..iC].Trim();
                                string s2 = sLinkPattern[(iC + sSep.Length)..].Trim();
                                sLinkSeparator[0] = s1.IndexOf(".") > 0 ? s1.Split(Convert.ToChar("."))[1] : s1;
                                sLinkSeparator[1] = s2.IndexOf(".") > 0 ? s2.Split(Convert.ToChar("."))[1] : s2;
                                sLinkSeparator[2] = sSep;
                                sLinkSeparator[3] = s1.IndexOf(".") > 0 ? s1.Split(Convert.ToChar("."))[0].Trim() : "";
                                sLinkSeparator[4] = s2.IndexOf(".") > 0 ? s2.Split(Convert.ToChar("."))[0].Trim() : "";
                                sLinkSeparator[5] = s1;
                                sLinkSeparator[6] = s2;
                                iC += sLinkPattern.Length;
                                break;
                            }
                        }
                    }
                    if (iC == scLink.Length - 1 && iParLeft == iParRight) //cas : ((sites.SIT_KNUM_CLIENT = clients.CLI_NUMERO)) n'as pas pu passer dans l'autre condition
                    {
                        foreach (string sSep in SHSConstantes.JOIN_SEPARATORS)
                        {
                            int iIdx = sLinkPattern.IndexOf(sSep, StringComparison.InvariantCultureIgnoreCase);
                            if (iIdx > 0)
                            {
                                string s1 = sLinkPattern[..iIdx].Replace("(", "").Trim();
                                string s2 = sLinkPattern[(iIdx + sSep.Length)..].Replace(")", "").Trim();
                                sLinkSeparator[0] = s1.IndexOf(".") > 0 ? s1.Split(Convert.ToChar("."))[1] : s1;
                                sLinkSeparator[1] = s2.IndexOf(".") > 0 ? s2.Split(Convert.ToChar("."))[1] : s2;
                                sLinkSeparator[2] = sSep;
                                sLinkSeparator[3] = s1.IndexOf(".") > 0 ? s1.Split(Convert.ToChar("."))[0].Trim() : "";
                                sLinkSeparator[4] = s2.IndexOf(".") > 0 ? s2.Split(Convert.ToChar("."))[0].Trim() : "";
                                iC += sLinkPattern.Length;
                                break;
                            }
                        }
                    }
                }
            }

            return sLinkSeparator;
        }

        public void ChangeLimitedResults(int iQteRows)
        {
            if (iQteRows > 0)
            {
                //if (ConnectionSrc.SConnDriverSuffix.Equals("DB"))
                //{
                //    string sDriverSyntax = ConnectionSrc.GetParam(SQLTools_Enums.DRIVER_PARAMS.LIMITED_RESULTS);
                //    if (sDriverSyntax.Length > 0 && sDriverSyntax.IndexOf("_") > 0)
                //    {
                //        string sPattern = sDriverSyntax.Split('_')[0];
                //        bool bIsTop = sDriverSyntax.Split('_')[1].Equals("TOP", StringComparison.OrdinalIgnoreCase);
                //        //modèle : TOP_TOP
                //        //modèle : LIMIT_BOTTOM

                //        //TOP\\s+(PERCENT\\s+)?(\\d+)\\s+
                //        MatchCollection mcSearch = Regex.Matches(SQLQuery, (sPattern + "\\s+(PERCENT\\s+)?(\\d+)\\s*"), RegexOptions.IgnoreCase);
                //        if (mcSearch.Count > 0)
                //        {
                //            //on remplace la data dans la requête
                //            string sReplaced = bIsTop ? mcSearch[0].Value : mcSearch[mcSearch.Count - 1].Value;
                //            sReplaced = Regex.Replace(sReplaced, "\\d+", iQteRows.ToString());

                //            if (bIsTop)
                //            {
                //                SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, mcSearch[0].Index, mcSearch[0].Length, sReplaced);
                //            }
                //            else
                //            {
                //                SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, mcSearch[mcSearch.Count - 1].Index, mcSearch[mcSearch.Count - 1].Length, sReplaced);
                //            }
                //        }
                //        else
                //        {
                //            //on l'ajoute à la requête
                //            if (bIsTop)
                //            {
                //                //remplacer le SELECT
                //                SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, 0, 6, ("SELECT " + sPattern + " " + iQteRows + " "));
                //            }
                //            //on l'ajoute à la requête
                //            else
                //            {
                //                if (SQLQuery.EndsWith(";"))
                //                {
                //                    SQLQuery = SQLQuery[..^1];
                //                    SQLQuery = string.Concat(SQLQuery, " ", sPattern, " ", iQteRows, ";");
                //                }
                //                else
                //                {
                //                    SQLQuery = string.Concat(SQLQuery, " ", sPattern, " ", iQteRows);
                //                }
                //                //remplacer le SELECT                
                //            }
                //        }
                //    }
                //    else
                //    {
                //        bool bIsTop = false;

                //        switch (ConnectionSrc.SConnDriver)
                //        {
                //            case SQLTools_Enums.BDD.DB_ACCESS:
                //                //top
                //                bIsTop = true;
                //                break;
                //            case SQLTools_Enums.BDD.DB_SQLSERVER:
                //                //top
                //                bIsTop = true;
                //                break;
                //            case SQLTools_Enums.BDD.DB_SQLITE:
                //                //limit
                //                bIsTop = false;
                //                break;
                //            case SQLTools_Enums.BDD.DB_MYSQL:
                //                //limit
                //                bIsTop = false;
                //                break;
                //            case SQLTools_Enums.BDD.DB_POSTGRE:
                //                //limit
                //                bIsTop = false;
                //                break;
                //        }

                //        if (ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_ODBC && ConnectionSrc.SConnDriver != SQLTools_Enums.BDD.DB_ORACLE) //on ne peut pas deviner la syntaxe, on oublie. En oracle, trop compliqué
                //        {
                //            if (bIsTop)
                //            {
                //                //test TOP x
                //                MatchCollection mcTop = Regex.Matches(SQLQuery, SHSRegex.REGEX_QUERY_TOP, RegexOptions.IgnoreCase);
                //                if (mcTop.Count == 0)
                //                {
                //                    SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, 0, 6, ("SELECT TOP " + iQteRows + " "));
                //                }
                //                else
                //                {
                //                    //on remplace la data dans la requête
                //                    string sReplaced = mcTop[0].Value;
                //                    sReplaced = Regex.Replace(sReplaced, "\\d+", iQteRows.ToString());

                //                    SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, mcTop[0].Index, mcTop[0].Length, sReplaced);
                //                }
                //            }
                //            else
                //            {
                //                //test LIMIT x
                //                MatchCollection mcBottom = Regex.Matches(SQLQuery, SHSRegex.REGEX_QUERY_BOTTOM, RegexOptions.IgnoreCase);
                //                if (mcBottom.Count == 0)
                //                {
                //                    if (SQLQuery.EndsWith(";"))
                //                    {
                //                        SQLQuery = SQLQuery[..^1];
                //                        SQLQuery = string.Concat(SQLQuery, " LIMIT ", iQteRows, ";");
                //                    }
                //                    else
                //                    {
                //                        SQLQuery = string.Concat(SQLQuery, " LIMIT ", iQteRows);
                //                    }
                //                }
                //                else
                //                {
                //                    //on remplace la data dans la requête
                //                    string sReplaced = mcBottom[mcBottom.Count - 1].Value;
                //                    sReplaced = Regex.Replace(sReplaced, "\\d+", iQteRows.ToString());

                //                    SQLQuery = Toolbox.ReplaceStringInString(SQLQuery, mcBottom[mcBottom.Count - 1].Index, mcBottom[mcBottom.Count - 1].Length, sReplaced);
                //                }
                //            }
                //        }
                //    }
                //}

                QueryAnalyzer.LimitedResults = iQteRows;
            }
        }

        #endregion

        #region "PUBLIC VOID"

        public Query()
        {

        }

        public Query(Job INIP, string sQuery, CONNString ConnStringCrossJoinSrc = null)
        {
            JobParameters = INIP;
            RawQuery = sQuery.Trim();

            sQuery = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sQuery, INIP.DynParams);

            Match mcDisabled = Regex.Match(sQuery, "^\\n*\\s*--");
            if (mcDisabled.Success) { IsEnabled = false; sQuery = sQuery[mcDisabled.Length..]; }

            if (sQuery.RemoveWhitespace().Length > 0)
            {
                if (sQuery.StartsWith("(") && sQuery.EndsWith(")")) //potentiellement une subquery
                {
                    sQuery = sQuery[1..^1];
                }

                sQuery = Query_CleanQuery(sQuery);
                //sQuery = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(sQuery, INIP.DynParams);

                if (ConnStringCrossJoinSrc != null)
                {
                    ConnectionSrc = ConnStringCrossJoinSrc;
                }
                else { ConnectionSrc = INIP.ConnectionString_Source ?? null; }

                if (ConnectionSrc != null)
                {
                    //pas de qError en cas de languages SQL douteux (SOQL, NxQL...)
                    if (ConnectionSrc.SConnDriverSuffix.Equals("WS") && INIP.WebserviceSQLLanguage != SQLTools_Enums.WEBSERVICE_SQL.FUZIBLE_SQL)
                    {
                        AnalyzeErrors = false;
                    }
                }

                ConnectionTrg = INIP.ConnectionString_Target;

                //recherche de requêtes cross join (rappel : [<>[5]] ou [--[74]] - les numéros étant les identifiants des chaînes de connection)
                List<string> sCrossJoinPatterns = new();
                List<int> iCrossJoinPatterns = new();
                GetCrossJoinScriptsFromString(ref sCrossJoinPatterns, ref iCrossJoinPatterns, sQuery, null, INIP.DynParams);
                CrossJoinDepth = sCrossJoinPatterns.Count;

                if (sCrossJoinPatterns.Count > 0)
                {
                    List<string> sCrossJoinQueries = ExtractCrossJoinQueries(sQuery, sCrossJoinPatterns);
                    if (sCrossJoinQueries.Count > 0)
                    {
                        //on coupe la requête initiale au premier script
                        sQuery = sQuery[..iCrossJoinPatterns[0]].Trim();

                        if (sCrossJoinPatterns.Count == sCrossJoinQueries.Count)
                        {
                            //on ajoute les cross-queries
                            List<Query> sQCJQueries = new();
                            for (int iCQ = 0; iCQ < sCrossJoinQueries.Count; iCQ++)
                            {
                                if (Regex.IsMatch(string.Concat(sCrossJoinPatterns[iCQ], sCrossJoinQueries[iCQ]), "[\\n\\r\\s]*" + SHSRegex.REGEX_CROSSJOINSCRIPT_B + "[\\n\\r\\s]*SELECT", RegexOptions.IgnoreCase))
                                {
                                    Query qCJ = GetCrossJoinQueryDefinition(INIP, sCrossJoinPatterns[iCQ], sCrossJoinQueries[iCQ]);
                                    sQCJQueries.Add(qCJ);
                                }
                            }
                            CrossJoinQueries = sQCJQueries;
                        }
                    }
                }
                //GetCrossJoinQuery(INIP, FuzibleQuery);

                //if (CrossJoinQuery != null) //a ce stade, on vire de la requête principale toute la sous-requête
                //{ sQuery = sQuery.Split(new string[] { CrossJoinString }, StringSplitOptions.RemoveEmptyEntries)[0].Trim(); }

                SQLQuery = RemoveTableCibleFromQuery(sQuery);

                if (SQLQuery.StartsWith("(") && SQLQuery.EndsWith(")")) //potentiellement une subquery
                {
                    SQLQuery = SQLQuery[1..^1];
                }

                //pas envie d'avoir les ";" qui trainent en queue de requête
                if (SQLQuery.EndsWith(";")) { SQLQuery = SQLQuery[0..^1]; }

                if (ConnectionTrg != null && ConnectionTrg.SConnDriver.ToString().StartsWith("DB"))
                {
                    OutputTable = Toolbox.RemoveSpecialCharacters(ExtractTableCibleFromQueryUsingSeparator(sQuery, INIP), "_", false);
                }
                else { OutputTable = ExtractTableCibleFromQueryUsingSeparator(sQuery, INIP); }

                //raw de l'output
                Match mcOutput = Regex.Match(sQuery, SHSRegex.REGEX_STARTQUERY, RegexOptions.IgnoreCase);
                string sRawTemp = mcOutput.Value;
                if (sRawTemp.Length > 0 && sRawTemp.IndexOf(":") > 0)
                {
                    RawOutput = sRawTemp[..sRawTemp.LastIndexOf(":")];
                }
                else { RawOutput = OutputTable; }

                //TARGET A ------------------------
                string sOutPut = OutputTable;

                if (ConnectionSrc != null && Regex.IsMatch(SQLQuery, "^\\s*SELECT\\s*", RegexOptions.IgnoreCase))
                {
                    QueryAnalyzer = AnalyzeQuery(SQLQuery, ConnectionSrc, ConnectionTrg, ref sOutPut);
                    if (!AnalyzeErrors) { QueryAnalyzer.Errors = new List<QError>(); }


                    OutputTable = sOutPut;

                    //TARGET B ------------------------
                    if (ConnectionTrgB != null)
                    {
                        sOutPut = OutputTableB;
                        QueryAnalyzerB = AnalyzeQuery(SQLQuery, ConnectionSrc, ConnectionTrgB, ref sOutPut);
                        if (!AnalyzeErrors) { QueryAnalyzer.Errors = new List<QError>(); }
                        OutputTableB = sOutPut;
                    }
                }
            }
        }

        public Query CreateSwapedTargetQuery()
        {
            Query cSwaped = this.DeepCopy();

            if (cSwaped.ConnectionTrg != null && cSwaped != null)
            {
                CONNString CSold = ConnectionTrg; ;
                string OutputTableOld = OutputTable; ;
                QAnalyzer QueryAnalyzerOld = QueryAnalyzer;

                //DBNameTrg = DBNameTargetB;
                cSwaped.ConnectionTrg = ConnectionTrgB;
                cSwaped.OutputTable = OutputTableB;
                cSwaped.QueryAnalyzer = QueryAnalyzerB;

                //DBNameTargetB = DBNameOld;
                cSwaped.ConnectionTrgB = CSold;
                cSwaped.OutputTableB = OutputTableOld;
                cSwaped.QueryAnalyzerB = QueryAnalyzerOld;

                return cSwaped;
            }
            else { return null; }
        }

        public static string RemoveSQLComments(string input, bool preservePositions, bool removeLiterals = false)
        {
            var match = Regex.Match(input, SHSRegex.REGEX_STARTQUERY_PAR);
            if (match.Success)
            {
                input = input[match.Groups[1].Length..];
            }

            Regex everythingExceptNewLines = new("[^\r\n]");

            //based on https://stackoverflow.com/questions/3524317/regex-to-strip-line-comments-from-c-sharp/3524689#3524689

            string lineComments = @"--(.*?)\r?\n";
            string lineCommentsOnLastLine = @"--(.*?)$"; // because it's possible that there's no \r\n after the last line comment
                                                         // literals ('literals'), bracketedIdentifiers ([object]) and quotedIdentifiers ("object"), they follow the same structure:
                                                         // there's the start character, any consecutive pairs of closing characters are considered part of the literal/identifier, and then comes the closing character
            string literals = @"('(('')|[^'])*')"; // 'John', 'O''malley''s', etc
            string bracketedIdentifiers = @"\[((\]\])|[^\]])* \]"; // [object], [ % object]] ], etc
            string quotedIdentifiers = @"(\""((\""\"")|[^""])*\"")"; // "object", "object[]", etc - when QUOTED_IDENTIFIER is set to ON, they are identifiers, else they are literals
                                                                     //var blockComments = @"/\*(.*?)\*/";  //the original code was for C#, but Microsoft SQL allows a nested block comments // //https://msdn.microsoft.com/en-us/library/ms178623.aspx
                                                                     //so we should use balancing groups // http://weblogs.asp.net/whaggard/377025
            string nestedBlockComments = @"/\*
                                (?>
                                /\*  (?<LEVEL>)      # On opening push level
                                | 
                                \*/ (?<-LEVEL>)     # On closing pop level
                                |
                                (?! /\* | \*/ ) . # Match any char unless the opening and closing strings   
                                )+                         # /* or */ in the lookahead string
                                (?(LEVEL)(?!))             # If level exists then fail
                                \*/";

            string noComments = Regex.Replace(input,
                    nestedBlockComments + "|" + lineComments + "|" + lineCommentsOnLastLine + "|" + literals + "|" + bracketedIdentifiers + "|" + quotedIdentifiers,
                me =>
                {
                    if (me.Value.StartsWith("/*") && preservePositions)
                    {
                        return everythingExceptNewLines.Replace(me.Value, " "); // preserve positions and keep line-breaks // return new string(' ', me.Value.Length);
                    }
                    else if (me.Value.StartsWith("/*") && !preservePositions)
                    {
                        return "";
                    }
                    else if (me.Value.StartsWith("--") && preservePositions)
                    {
                        return everythingExceptNewLines.Replace(me.Value, " "); // preserve positions and keep line-breaks
                    }
                    else if (me.Value.StartsWith("--") && !preservePositions)
                    {
                        return everythingExceptNewLines.Replace(me.Value, ""); // preserve only line-breaks // Environment.NewLine;
                    }
                    else if (me.Value.StartsWith("[") || me.Value.StartsWith("\""))
                    {
                        return me.Value; // do not remove object identifiers ever
                    }
                    else if (!removeLiterals) // Keep the literal strings
                    {
                        return me.Value;
                    }
                    else if (removeLiterals && preservePositions) // remove literals, but preserving positions and line-breaks
                    {
                        string literalWithLineBreaks = everythingExceptNewLines.Replace(me.Value, " ");
                        return "'" + literalWithLineBreaks[1..^1] + "'";
                    }
                    else if (removeLiterals && !preservePositions) // wrap completely all literals
                    {
                        return "''";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                },
                RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

            if (match.Success)
            {
                noComments = string.Concat(match.Groups[1].Value, noComments);
            }

            return noComments;
        }

        public static string Query_CleanQuery(string sSQLQuery)
        {
            //MatchCollection mcQC = Regex.Matches(sSQLQuery, "\\w\\*");
            //for (int iR = 0; iR < mcQC.Count; iR++)
            //{ sSQLQuery = sSQLQuery.Insert(mcQC[iR].Index + 1 + iR, " "); }

            //MatchCollection mcQD = Regex.Matches(sSQLQuery, "\\*\\w");
            //for (int iR = 0; iR < mcQD.Count; iR++)
            //{ sSQLQuery = sSQLQuery.Insert(mcQD[iR].Index + 1 + iR, " "); }
            sSQLQuery = RemoveSQLComments(sSQLQuery, false);

            sSQLQuery = Regex.Replace(sSQLQuery, SHSRegex.REGEX_WHITESPACES, " ");
            sSQLQuery = Regex.Replace(sSQLQuery, "([^\\s]+)(\\s*)(:)(\\s*)(SELECT)", "$1$3$5", RegexOptions.IgnoreCase); //nettoyage du "output   : select"
            sSQLQuery = Regex.Replace(sSQLQuery, "\\s{2,}", " ");

            //TRAITEMENT GENRE : SELECT CONCAT ("Queue - Type (QL,QF,QFP,HSK,ISO)"," + ","Désignation")
            //V1 --Gestion par encadrement de parenthèses
            sSQLQuery = Regex.Replace(sSQLQuery, "(\\s*)(\\()(\\s*)(?![^\\(]*\\))", "$2"); //je cherche à enlever les espaces foireux genre select id_test from(select * from table) where(1=1) qui est une syntaxe valide mais perturbante
            //V2 --Gestion par encadrement de guillemets
            //sSQLQuery = Regex.Replace(sSQLQuery, "(\\s*)(\\()(\\s*)(?:(?<=[\"'](\\s*)(\\()(\\s*))|(?=[\"']))", "$2");
            //----------------------------------------

            sSQLQuery = Regex.Replace(sSQLQuery, "(\\s*)(\\))(\\s*)([A-Za-z0-9]+)", "$2 $4");
            //sSQLQuery = Regex.Replace(sSQLQuery, "(\\s*)(\\))(\\s*)", "$2 ");

            //TRAITEMENT GENRE : excelcomplique4:select \"Affûtage (INT, EXT, NON)\",     test from e-Tools.xlsm as etools 
            //V1 --Gestion par encadrement de guillemets          
            sSQLQuery = Regex.Replace(sSQLQuery, "(\\s*)(,)(\\s*)(?:(?<=[\"'](\\s*)(,)(\\s*))|(?=[\"']))", "$2");
            ////old version : sSQLQuery = Regex.Replace(sSQLQuery, "([^'])(\\s*)(\\,)(\\s*)([^'])", "$1$3$5");


            sSQLQuery = Regex.Replace(sSQLQuery, "([A-Za-z0-9]+)(\\*)(\\s*)(FROM)", "$1 $2 $4", RegexOptions.IgnoreCase); //cas du select* (qui est valide)
            sSQLQuery = Regex.Replace(sSQLQuery, "(\\*)(FROM)", "$1 $2", RegexOptions.IgnoreCase); //cas du select *from (qui est valide)
            sSQLQuery = Regex.Replace(sSQLQuery, "([A-Za-z0-9]+)(\\s*)(\\.)(\\s*)([A-Za-z0-9]+)", "$1$3$5"); //cas du file . field

            List<string> sSQLAvoid = new() { "HAVING", "GROUP BY", "ORDER BY", "SELECT", "JOIN", "ON", "AND", "WHERE", "FROM", "AS", "LIKE" };
            //nettoyage requête (retrait des X( ou )X
            foreach (string sC in sSQLAvoid)
            {
                sSQLQuery = Regex.Replace(sSQLQuery, string.Concat("(", sC, ")(\\()"), "$1 $2", RegexOptions.IgnoreCase);
                sSQLQuery = Regex.Replace(sSQLQuery, string.Concat("(\\))", "(", sC, ")"), "$2 $1", RegexOptions.IgnoreCase);
            }

            return sSQLQuery.Trim();
        }

        #endregion

        #region "PRIVATE VOID"

        private static List<string> ExtractCrossJoinQueries(string sQuery, List<string> sCrossJoinPatterns)
        {
            List<string> sQueries = new();
            string sExtracted = sQuery;
            for (int iC = 0; iC < sCrossJoinPatterns.Count; iC++)
            {
                if (iC > 0) { sQueries.Add(sExtracted[..sExtracted.IndexOf(sCrossJoinPatterns[iC])].Trim()); }

                if (sExtracted.IndexOf(sCrossJoinPatterns[iC]) + sCrossJoinPatterns[iC].Length + 1 <= sExtracted.Length)
                {
                    sExtracted = sExtracted[(sExtracted.IndexOf(sCrossJoinPatterns[iC]) + sCrossJoinPatterns[iC].Length + 1)..];
                }
                else { sExtracted = ""; break; }
            }
            sQueries.Add(sExtracted);
            return sQueries;
        }

        private Query GetCrossJoinQueryDefinition(Job INIP, string sPattern, string sQuery)
        {
            string sConn = Regex.Match(sPattern, "\\[\\d+\\]").Value;

            string sJoinType = sPattern.Substring(1, 1);

            CONNString csCQ = INIP.GlobalParameters.Connections.GetConnByID(sConn);

            Match mcWhere = Regex.Match(sPattern, "\\]\\s*WHERE\\s*.+\\]"); //ex : [<>[0]WHERE 1 = 1]
            Match mcForceKey = Regex.Match(sPattern, "\\[[<>-]{1}[\\d\\w\\s,;=_\\-'\".]+[<>-]{1}\\["); //ex : [<id_matricule>[0]]

            Query qCrossJoinQuery = new(JobParameters, sQuery, csCQ)
            {
                CrossQueryRawScript = sPattern
            };

            if (mcWhere.Success)
            {
                //on a récupéré
                string sWhere = mcWhere.Value;
                sWhere = sWhere[1..];
                sWhere = sWhere[0..^1];
                qCrossJoinQuery.CrossJoinQueryWhere = sWhere;
            }

            if (mcForceKey.Success)
            {
                //on a récupéré [<id_matricule>[
                string sFields = mcForceKey.Value;
                sFields = sFields[2..];
                sFields = sFields[0..^2];
                qCrossJoinQuery.CrossQueryForceJoinFields = sFields.Split(Convert.ToChar(",")).ToList();
                sJoinType = string.Concat(sJoinType, mcForceKey.Value.Substring(mcForceKey.Length - 2, 1));
            }

            //pas de clé forcée : on a donc un pattern de type [<>[0]... ou [--[0]...
            if (sJoinType.Length == 1)
            {
                sJoinType = string.Concat(sJoinType, sPattern.Substring(2, 1));
            }

            switch (sJoinType)
            {
                case "<>":
                    qCrossJoinQuery.CrossJoinType = SQLTools_Enums.CROSSJOIN_TYPES.OUTER;
                    break;
                case "->":
                    qCrossJoinQuery.CrossJoinType = SQLTools_Enums.CROSSJOIN_TYPES.LEFT;
                    break;
                case "<-":
                    qCrossJoinQuery.CrossJoinType = SQLTools_Enums.CROSSJOIN_TYPES.RIGHT;
                    break;
                case "--":
                    qCrossJoinQuery.CrossJoinType = SQLTools_Enums.CROSSJOIN_TYPES.INNER;
                    break;
            }
            return qCrossJoinQuery;
        }

        private string ExtractTableCibleFromQueryUsingSeparator(string sQuery, Job JobParameters)
        {
            string sOutputTableA = "QUERY";

            Match rg = Regex.Match(sQuery, ":\\s*SELECT", RegexOptions.IgnoreCase);
            if (rg.Success)
            {
                sOutputTableA = sQuery[0..rg.Index];
            } //.Split(Convert.ToChar(":"))[0];

            sOutputTableA = sOutputTableA.Trim();
            string sNomTableOriginal = sOutputTableA;
            //on regarde si une connexion forcée a été demandée
            MatchCollection mcConn = Regex.Matches(sOutputTableA, "\\[\\d+\\]");

            if (mcConn.Count > 0 && mcConn[0].Index == 0)
            {
                string sConn = mcConn[0].Value;

                if (JobParameters.GlobalParameters.Connections.GetIndexSection(sConn) > -1)
                {
                    ConnectionTrg = JobParameters.GlobalParameters.Connections.GetConnByID(sConn);
                }

                sOutputTableA = sOutputTableA[mcConn[0].Length..];

                if (sOutputTableA.Length > 36 && ConnectionTrg != null && ConnectionTrg.SConnDriverSuffix.Equals("DB")) { sOutputTableA = sOutputTableA[..36]; } //restriction technique des tables SQL

                if (mcConn.Count > 1) //on cherche un éventuel multi-target
                {
                    //if (sNomTable.IndexOf("]") < (sNomTable.IndexOf("[") + 5))
                    if (mcConn.Count > 1)
                    {
                        string sConnMT = mcConn[1].Value;
                        if (JobParameters.GlobalParameters.Connections.GetIndexSection(sConnMT) > -1)
                        {
                            HasMultiTarget = true;
                            ConnectionTrgB = JobParameters.GlobalParameters.Connections.GetConnByID(sConnMT);
                            OutputTableB = sNomTableOriginal[(mcConn[1].Index + mcConn[1].Length)..];
                            if (mcConn.Count > 2) //interdit de multiplier les multi targets : limite posée à 2 !
                            {
                                OutputTableB = OutputTableB[..Regex.Match(OutputTableB, "\\[\\d+\\]").Index];
                            }

                            if (OutputTableB.Length > 36 && ConnectionTrgB.SConnDriverSuffix.Equals("DB")) { OutputTableB = OutputTableB[..36]; } //restriction technique des tables SQL
                        }
                        else { HasMultiTarget = false; ConnectionTrgB = null; OutputTableB = ""; }
                    }
                    sOutputTableA = sOutputTableA[..Regex.Match(sOutputTableA, "\\[\\d+\\]").Index];
                }
            }

            return sOutputTableA;
        }

        private QSynchroQuery BuildSynchroTargetQuery(Job JobParameters, string sTargetTable, List<QField> sListFieldsFromSource, List<QWhere> sListWhere, CONNString CSs, CONNString CSt)
        {
            if (CSt != null)
            {
                string sEchapCharSource = "";
                string sEchapCharTarget = "";
                string sReplacementChar = "_";

                if (CSs != null) { sEchapCharSource = CSs.SqlEchappementChar; }
                if (CSt != null) { sEchapCharTarget = CSt.SqlEchappementChar; }

                //astuce pour éviter de planter le remplacement du caractère déchappement (comparaison entre une source excel avec noms de colonne encadrés par des " et cible SQL ne supportant pas les " dans les noms de champs
                if (CSt != null)
                {
                    if (CSt.SConnDriver == SQLTools_Enums.BDD.FI_CSV || CSt.SConnDriver == SQLTools_Enums.BDD.FI_XLS)
                    {
                        sEchapCharTarget = "\""; sReplacementChar = "_";
                    }
                }


                StringBuilder sbRequeteCible = new();
                sbRequeteCible.Append("SELECT ");
                List<SQLColumn> sColAlreadyUsed = new();

                int iIdxCol = -1;
                foreach (QField s in sListFieldsFromSource)
                {
                    iIdxCol++;

                    if (s.Name.IndexOf("*") > -1)
                    {
                        sbRequeteCible.Append("*,");
                        sColAlreadyUsed.Add(new SQLColumn(iIdxCol, "*", SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", true, "", false, false));
                    }
                    //je construis une requête cible à partir d'une source, en retirant les caractères d'échappement de la BDD source pour mettre ceux de la BDD cible
                    else
                    {
                        string sField = "";
                        if (sEchapCharSource.Length == 0)
                        {
                            if (sEchapCharTarget.Length > 0) { sField = s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(sEchapCharTarget, sReplacementChar).Trim(); }
                            else { sField = s.Alias; }
                        }
                        else
                        {
                            if (sEchapCharSource.Length == 0) { sField = s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim(); }
                            else
                            {
                                if (sEchapCharTarget.Length > 0) { sField = s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(sEchapCharSource, "").Trim(); }
                                else { sField = s.Alias; }
                            }
                        }
                        //(sEchapCharSource.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(sEchapCharTarget, sReplacementChar).Trim() : (sEchapCharSource.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim() : s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(sEchapCharSource, "").Trim()))
                        sbRequeteCible.Append(string.Concat(sEchapCharTarget, sField, sEchapCharTarget, ","));
                        sColAlreadyUsed.Add(new SQLColumn(iIdxCol, sField, SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", true, "", false, false));
                    }
                }
                //on ajoute les champs de la requête cross-join
                foreach (Query qCJ in CrossJoinQueries)
                {
                    foreach (QField s in qCJ.QueryAnalyzer.Fields)
                    {
                        if (sEchapCharTarget.Length > 0)
                        {
                            if (!s.Name.Contains('*', StringComparison.CurrentCulture) && (!sColAlreadyUsed.Select(c => c.ColumnName).Contains(qCJ.ConnectionSrc.SqlEchappementChar.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim() : s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(qCJ.ConnectionSrc.SqlEchappementChar, "").Trim())))
                            {
                                sbRequeteCible.Append(string.Concat(sEchapCharTarget + (qCJ.ConnectionSrc.SqlEchappementChar.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim() : s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(qCJ.ConnectionSrc.SqlEchappementChar, "").Trim()) + sEchapCharTarget, ","));
                                sColAlreadyUsed.Add(new SQLColumn(iIdxCol, sEchapCharSource.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim() : (sEchapCharSource.Length == 0 ? s.Alias.Replace(sEchapCharTarget, sReplacementChar).Trim() : s.Alias.Replace(sEchapCharTarget, sReplacementChar).Replace(sEchapCharSource, "").Trim()), SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", true, "", false, false));
                            }
                        }
                        else //en mode target = mail par exemple, pas de caractère d'échappemenbt
                        {
                            if (!s.Name.Contains('*', StringComparison.CurrentCulture) && (!sColAlreadyUsed.Select(c => c.ColumnName).Contains(qCJ.ConnectionSrc.SqlEchappementChar.Length == 0 ? s.Alias.Trim() : s.Alias.Replace(qCJ.ConnectionSrc.SqlEchappementChar, "").Trim())))
                            {
                                sbRequeteCible.Append(string.Concat((qCJ.ConnectionSrc.SqlEchappementChar.Length == 0 ? s.Alias.Trim() : s.Alias.Replace(qCJ.ConnectionSrc.SqlEchappementChar, "").Trim()), ","));
                                sColAlreadyUsed.Add(new SQLColumn(iIdxCol, sEchapCharSource.Length == 0 ? s.Alias.Trim() : (sEchapCharSource.Length == 0 ? s.Alias.Trim() : s.Alias.Replace(sEchapCharSource, "").Trim()), SQLTools_Enums.TYPE_DATA.VARCHAR, Type.GetType("System.String"), "0", true, "", false, false));
                            }
                        }
                    }
                }

                //ici j'ajoute les champs optionnels parce qu'ils peuvent être dans la cible mais pas dans la source : je dois malgré tout les ajouter manuellement
                //car le dataset source sera prochainement peuplé de ces colonnes (voir un peu plus bas)
                if (JobParameters.OptionalDBName_OnInsert && (!sbRequeteCible.ToString().Contains(JobParameters.TargetAddDbName)))
                {
                    sbRequeteCible.Append(string.Concat(sEchapCharTarget + JobParameters.TargetAddDbName + sEchapCharTarget + ","));
                }
                if (JobParameters.OptionalRowID_OnInsert && (!sbRequeteCible.ToString().Contains(JobParameters.TargetAddRowNum)))
                {
                    sbRequeteCible.Append(string.Concat(sEchapCharTarget + JobParameters.TargetAddRowNum + sEchapCharTarget + ","));
                }
                if (JobParameters.OptionalTimestamp_OnInsert && (!sbRequeteCible.ToString().Contains(JobParameters.TargetAddDtLoad)))
                {
                    sbRequeteCible.Append(string.Concat(sEchapCharTarget + JobParameters.TargetAddDtLoad + sEchapCharTarget + ","));
                }
                if (JobParameters.OptionalDynamicParamField_OnInsert.Length > 0 && (!sbRequeteCible.ToString().Contains(JobParameters.OptionalDynamicParamField_OnInsert.IndexOf("=") > -1 ? JobParameters.OptionalDynamicParamField_OnInsert.Split(Convert.ToChar("="))[0] : "DYNPARAM")))
                {
                    sbRequeteCible.Append(sEchapCharTarget + (JobParameters.OptionalDynamicParamField_OnInsert.IndexOf("=") > -1 ? JobParameters.OptionalDynamicParamField_OnInsert.Split(Convert.ToChar("="))[0] : "DYNPARAM") + sEchapCharTarget + ",");
                }

                sbRequeteCible = new StringBuilder(sbRequeteCible.ToString().Remove(sbRequeteCible.ToString().Length - 1, 1)); //suppression dernière virgule


                if (CSt != null && CSt.SConnDriverSuffix.Equals("DB"))
                {
                    sbRequeteCible.Append(string.Concat(" FROM ", sEchapCharTarget, sTargetTable, sEchapCharTarget));
                }
                else { sbRequeteCible.Append(string.Concat(" FROM ", sTargetTable)); }


                StringBuilder sbWhereTarget = new();
                StringBuilder sbWhereSource = new();

                if (sListWhere.Count > 0) { sbWhereTarget.Append("WHERE "); sbWhereSource.Append("WHERE "); }
                for (int iQ = 0; iQ < sListWhere.Count; iQ++)
                {
                    sbWhereSource.Append(sListWhere[iQ].Where);

                    string sWhereTarget = ExtractTargetCondition(sListWhere[iQ], CSs, CSt);
                    if (sListWhere[iQ].SubConditions.Count > 0)
                    {
                        sWhereTarget = string.Concat("(", sWhereTarget);
                        foreach (QWhere qSub in sListWhere[iQ].SubConditions)
                        {
                            sWhereTarget = string.Concat(sWhereTarget, " ", qSub.SubConditionOperator, " ", ExtractTargetCondition(qSub, CSs, CSt));
                        }
                        sWhereTarget = string.Concat(sWhereTarget, ")");
                    }

                    sbWhereTarget.Append(sWhereTarget);
                    if (iQ < sListWhere.Count - 1) { sbWhereTarget.Append(" AND "); sbWhereSource.Append(" AND "); }
                }

                return new QSynchroQuery(sTargetTable, sbRequeteCible.ToString(), sbWhereTarget.ToString(), sColAlreadyUsed, CSt);
            }
            else { return null; }
        }

        private static string ExtractTargetCondition(QWhere qWhere, CONNString CSs, CONNString CSt)
        {
            string sWhere;

            string sSign = qWhere.WhereSign;

            if (CSt != null && CSt.SConnDriverSuffix.Equals("DB"))
            {
                if (qWhere.IsAliasField)
                {
                    if (qWhere.ReversedCondition)
                    {
                        sWhere = string.Concat(qWhere.RawWhereField, " ", sSign, " ", CSt.SqlEchappementChar, qWhere.FieldAlias, CSt.SqlEchappementChar);
                    }
                    else { sWhere = string.Concat(CSt.SqlEchappementChar, qWhere.FieldAlias, CSt.SqlEchappementChar, " ", sSign, " ", qWhere.WhereCompare); }
                }
                else
                {
                    if (qWhere.UnrecognizedField)
                    {
                        sWhere = string.Concat(qWhere.FieldAlias, " ", sSign, " ", qWhere.WhereCompare);
                    }
                    else
                    {
                        if (qWhere.ReversedCondition)
                        {
                            sWhere = string.Concat(qWhere.RawWhereField, " ", sSign, " ", CSt.SqlEchappementChar, qWhere.FieldAlias, CSt.SqlEchappementChar);
                        }
                        else { sWhere = string.Concat(CSt.SqlEchappementChar, qWhere.FieldAlias, CSt.SqlEchappementChar, " ", sSign, " ", qWhere.WhereCompare); }
                    }

                    if (sWhere.Trim().Length == 0) // cas du CAST('2021-01-01' as date) >= mydate
                    {
                        sWhere = qWhere.Where;
                    }
                }
            }
            else
            {
                if (qWhere.ReversedCondition)
                {
                    sWhere = string.Concat(qWhere.RawWhereField, " ", sSign, " ", qWhere.FieldAlias);
                }
                else { sWhere = string.Concat(qWhere.FieldAlias, " ", sSign, " ", qWhere.WhereCompare); }
            }
            return sWhere;
        }

        public static string RemoveTableCibleFromQuery(string sQuery)
        {
            string sQueryWithoutTableName;

            try
            {
                Match rg = Regex.Match(sQuery, ":\\s*SELECT", RegexOptions.IgnoreCase);
                if (rg.Success)
                {
                    sQueryWithoutTableName = sQuery[(rg.Index + 1)..];
                } //.Split(Convert.ToChar(":"))[0];
                //on identifie TABLE:SELECT
                //if (sQuery.IndexOf(":") > 0 && sQuery[(sQuery.IndexOf(":") + 1)..].Trim().StartsWith("SELECT", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    sQueryWithoutTableName = sQuery.Remove(0, FuzibleQuery.IndexOf(":") + 1).Trim();
                //}
                else { sQueryWithoutTableName = sQuery; }
            }
            catch (Exception)
            {
                sQueryWithoutTableName = sQuery.Trim();
            }

            return sQueryWithoutTableName;
        }

        internal List<string[]> RewriteWithoutAnonymization()
        {
            List<string[]> sListAnonymization = new();

            foreach (var col in QueryAnalyzer.Fields)
            {
                if (col.Functions.Any(f => f.StartsWith("ANONYMIZE", StringComparison.OrdinalIgnoreCase)))
                {
                    //SELECT ANONIMYZATION(myField, Source, Index) as field
                    string sCol = col.Raw;
                    sCol = sCol[(sCol.IndexOf("(") + 1)..];
                    string[] sAno = sCol.Split(',');

                    //index colonne SQL ou fichier CSV
                    string sIndex = "";
                    if (sAno.Length > 2)
                    {
                        sIndex = sAno[2];
                        sIndex = sIndex[..sIndex.IndexOf(")")];
                        if (sIndex.StartsWith("\"") && sIndex.EndsWith("\"")) { sIndex = sIndex[1..^1]; }
                        if (sIndex.StartsWith("'") && sIndex.EndsWith("'")) { sIndex = sIndex[1..^1]; }
                    }
                    else
                    {
                        sIndex = "0";
                    }

                    //source : fichier en local ou nom de table
                    string sFileOrTable = sAno[1];
                    if (sFileOrTable.IndexOf(")") > 0)
                    {
                        sFileOrTable = sFileOrTable[..sFileOrTable.IndexOf(")")];
                    }
                    if (sFileOrTable.StartsWith("\"") && sFileOrTable.EndsWith("\"")) { sFileOrTable = sFileOrTable[1..^1]; }
                    if (sFileOrTable.StartsWith("'") && sFileOrTable.EndsWith("'")) { sFileOrTable = sFileOrTable[1..^1]; }
                    string sType = "TABLE";
                    if (Regex.Match(sFileOrTable, "\\.[A-z0-9]{3}").Success)
                    {
                        sType = "FILE";
                    }
                    else if (sFileOrTable.Equals("RND", StringComparison.OrdinalIgnoreCase) || sFileOrTable.Equals("RANDOM", StringComparison.OrdinalIgnoreCase))
                    {
                        sType = "RANDOM";
                    }

                    sCol = sCol[..sCol.IndexOf(",")];
                    sCol = string.Concat(sCol, " AS ", col.Alias);
                    SQLQuery = SQLQuery.Replace(col.Raw, sCol);
                    sListAnonymization.Add(new string[4] { col.Alias, sFileOrTable, sType, sIndex });
                }
            }
            return sListAnonymization;
        }

        #endregion

    }

}
