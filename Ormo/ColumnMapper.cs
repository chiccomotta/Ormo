using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CM.Ormo
{
    /***
        Classe per mappare le proprietà degli oggetti POCO alle colonne della tabella
    ***/
    [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
    public class ColumnMapper : System.Attribute
    {
        private readonly string columnName;
        private readonly bool isPrimaryKey;

        public ColumnMapper(string columnName, bool isPrimaryKey = false)
        {
            this.columnName = columnName;
            this.isPrimaryKey = isPrimaryKey;
        }

        public string GetColumnName()
        {
            return this.columnName;
        }

        public bool IsPrimaryKey()
        {
            return this.isPrimaryKey;
        }
    }
}