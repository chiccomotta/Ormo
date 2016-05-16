using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CM.Ormo
{
    /***
        Attributo per impostare il nome della tabella sulla clasee
    ***/
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public class TableMapper : System.Attribute
    {
        private readonly string tableName;

        public TableMapper(string tableName)
        {
            this.tableName = tableName;
        }

        public string GetTableName()
        {
            return this.tableName;
        }
    }
}