using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CM.Ormo
{
    [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
    public class RelatedEntityMapper : System.Attribute
    {
        private readonly string relatedTableName;
        private readonly string relatedForeignKey;
        

        public RelatedEntityMapper(string relatedTableName, string relatedForeignKey)
        {
            this.relatedTableName = relatedTableName;
            this.relatedForeignKey = relatedForeignKey;
        }

        public string GetRelatedTableName()
        {
            return this.relatedTableName;
        }

        public string GetRelatedForeignKey()
        {
            return this.relatedForeignKey;
        }

    }
}