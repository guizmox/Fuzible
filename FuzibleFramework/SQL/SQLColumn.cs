using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace FuzibleFramework
{
    public class SQLColumn
    {
        private string _sDriverData = "";
        private string _sSizeColumn = "";
        private SQLTools_Enums.TYPE_DATA _sTypeColumn = SQLTools_Enums.TYPE_DATA.TEXT;
        private string _sColumnName = "";
        private Type _tTypeLinqColumn = System.Type.GetType("System.String");
        private int _iColumnPosition = 0;
        private bool _bWithNullValues = true;
        private bool _bIsKey = false;
        private bool _bIsUnique = false;
        private string _sDefaultValue = "";

        public string ColumnSize
        {
            get
            {
                if ((new List<string> { "0", "-1" }).Contains(_sSizeColumn))
                { return ""; }
                else if ((new List<string> { "DATE", "TIME", "BIT", "BOOL" }.Any(term => _sTypeColumn.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))))
                { return ""; }
                else return _sSizeColumn.Replace(',', '.');
            }
            set { _sSizeColumn = value; }
        }

        public SQLTools_Enums.TYPE_DATA ColumnType
        {
            get { return _sTypeColumn; }
            set { _sTypeColumn = value; }
        }

        public Type ColumnLinqType
        {
            get { return _tTypeLinqColumn; }
            set { _tTypeLinqColumn = value; }
        }

        public string ColumnName
        {
            get { return _sColumnName; }
            set { _sColumnName = value; }
        }

        public bool ColumnAllowsNullValues
        {
            get { return _bWithNullValues; }
            set { _bWithNullValues = value; }
        }

        public bool IsKey
        {
            get { return _bIsKey; }
            set { _bIsKey = value; }
        }

        public bool IsUnique
        {
            get { return _bIsUnique; }
            set { _bIsUnique = value; }
        }

        public int ColumnIndex
        {
            get { return _iColumnPosition; }
            set { _iColumnPosition = value; }
        }

        public string DefaultValue
        {
            get { return _sDefaultValue; }
            set { _sDefaultValue = value; }
        }

        public string DriverData 
        {
            get { return _sDriverData; }
            set { _sDriverData = value; }
        }

        public SQLColumn(int iColumnIndex, string sColumnName, SQLTools_Enums.TYPE_DATA sColumnType, Type sColumnLinqType, string sColumnSize, bool bWithNullValues, string sDefaultValue, bool bIsKey, bool bIsUnique, string sDriverData = "")
        {
            ColumnIndex = iColumnIndex;
            ColumnName = sColumnName;
            ColumnSize = sColumnSize;
            ColumnType = sColumnType;
            ColumnLinqType = sColumnLinqType ?? Type.GetType("System.String");
            ColumnAllowsNullValues = bWithNullValues;
            DefaultValue = sDefaultValue;
            IsKey = bIsKey;
            IsUnique = bIsUnique;
            DriverData = sDriverData;
        }

        public string BuildTypeForTableCreation(SQLTools_Enums.BDD sDriver, bool bNumericDot)
        {
            if (sDriver == SQLTools_Enums.BDD.DB_SQLITE)
            { 
                if (ColumnType.ToString().IndexOf("INT") > -1)
                {
                    return " INTEGER";
                }
                else if (ColumnType.ToString().Equals("BYTEXXXZZZ"))
                {
                    return string.Concat(" BLOB");
                }
                else if (ColumnType.ToString().IndexOf("BOOL") > -1)
                {
                    return " TEXT";
                }
                else if (ColumnType.ToString().IndexOf("DEC") > -1)
                {
                    return " NUMERIC";
                }
                else if (ColumnType.ToString().IndexOf("DOUBLE") > -1)
                {
                    return " REAL";
                }
                else if (ColumnType.ToString().IndexOf("DATE") > -1)
                {
                    return " TEXT";
                }
                else if (ColumnType.ToString().Equals("STRING"))
                {
                    return " TEXT";
                }
                else if (ColumnType.ToString().Equals("CHAR"))
                {
                    return " TEXT";
                }
                else { return string.Concat(" ", ColumnType.ToString()); }  
            }
            //else if (sDriver == SQLTools_Enums.BDD.DB_ACCESS)
            //{ return string.Concat(" ", ColumnType.IndexOf("DECIMAL") > -1 ? "NUMERIC" : ColumnType); }
            else
            {
                if (ColumnType.ToString().IndexOf("BIGINT") > -1)
                {
                    if (sDriver == SQLTools_Enums.BDD.DB_ORACLE)
                    {
                        return " NUMBER(19,0)";
                    }
                    else
                    {
                        return " BIGINT";
                    }
                }
                if (ColumnType.ToString().IndexOf("INT") > -1)
                {
                    return " INTEGER";
                }
                else if (ColumnType.ToString().IndexOf("DOUBLE") > -1)
                {
                    if (sDriver == SQLTools_Enums.BDD.DB_ORACLE)
                    {
                        return " BINARY_DOUBLE";
                    }
                    else if (sDriver == SQLTools_Enums.BDD.DB_MYSQL)
                    {
                        return " DOUBLE";
                    }
                    else if (sDriver == SQLTools_Enums.BDD.DB_SQLSERVER)
                    {
                        ColumnSize = "53";
                        return " FLOAT(53)";
                    }
                    else
                    {
                        return " FLOAT";
                    }
                }
                else if (sDriver == SQLTools_Enums.BDD.DB_SQLSERVER && ColumnType.ToString().IndexOf("TEXT") > -1)
                {
                    return " NVARCHAR(MAX)";
                }
                else if (ColumnType.ToString().Equals("STRING"))
                {
                    if (sDriver == SQLTools_Enums.BDD.DB_SQLSERVER)
                    {
                        return " NVARCHAR";
                    }
                    else
                    {
                        return " VARCHAR";
                    }
                }
                //remplacer Byte[]
                else if (ColumnType.ToString().Equals("BYTEXXXZZZ"))
                {
                    switch (sDriver)
                    {
                        case SQLTools_Enums.BDD.DB_ODBC:
                            return string.Concat(" ", "TEXT");
                        case SQLTools_Enums.BDD.DB_POSTGRE:
                            return string.Concat(" ", "BYTEA");
                        case SQLTools_Enums.BDD.DB_ACCESS:
                            return string.Concat(" ", "TEXT");
                        case SQLTools_Enums.BDD.DB_SQLSERVER:
                            return string.Concat(" ", "VARBINARY(MAX)");
                        default:
                            return string.Concat(" ", "VARBINARY", ColumnSize.Equals("") ? "" : "(" + ColumnSize + ")");
                    }
                }
                //compatibilité multi-SQL : j'ai identifié le type "BOOL" mais je force l'utilisation du "BIT" pour la création de colonnes : plus pratique
                else
                {
                    return string.Concat(" ", ColumnType.ToString().IndexOf("BOOL") > -1 ? "BIT" : ColumnType.ToString(), ColumnSize.Equals("") ? "" : "(" + ColumnSize.Replace('.', ',') + ")");
                }
            }
        }

        public static SQLColumn SQLColumnFromDataColumn(DataColumn dcK, SQLTools_Enums.TYPE_DATA sqlCharType)
        {
            bool bIsPK = false;
            DataColumn[] dtPKList = dcK.Table.PrimaryKey;
            foreach (DataColumn dt in dtPKList)
            {
                if (dt.ColumnName.Equals(dcK.ColumnName) || dt.ColumnName.Equals(dcK.Caption))
                { bIsPK = true; }
            }

            if (dcK.DataType.FullName.Equals("System.Byte[]"))
            { sqlCharType = SQLTools_Enums.TYPE_DATA.BYTEXXXZZZ; }

            return new SQLColumn(dcK.Ordinal, dcK.Caption.Length > 0 && !dcK.Caption.Equals(dcK.ColumnName) ? dcK.Caption : dcK.ColumnName, sqlCharType, dcK.DataType, dcK.MaxLength.ToString(), dcK.AllowDBNull, dcK.DefaultValue.ToString(), bIsPK, dcK.Unique, "");
        }

        public static int GetIndexSQLColumn(List<SQLColumn> ListOfSQLColumns, string sColumnName)
        {
            return ListOfSQLColumns.FindIndex(s => s.ColumnName.Equals(sColumnName, StringComparison.InvariantCultureIgnoreCase));
        }

    }
}
