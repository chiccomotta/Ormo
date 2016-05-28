using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;

       /*********************************************
       *    ORMO simple and minimalist SQLite ORM   *  
       *--------------------------------------------*
       *     Author:     Cristiano Motta            *
       *       Date:     10 Maggio 2016             *
       *    Version:     0.1.3                      *
       *********************************************/

namespace CM.Ormo
{   
    public class Ormo : IDisposable
    {
        // SET YOUR CONNECTION STRING TO SQLIte DB!
        private readonly string connectionString = "<my connection string to Database>";

        // Connection
        private SQLiteConnection SQLiteConnection = null;

        // Ormo Cache 
        private IOrmoCache ormoCache;

        // Constructor
        public Ormo(IOrmoCache cacheProvider = null)
        {         
            // OrmoCache injected by dependency injection (Ormocache implements IOrmoCache interface)
            ormoCache = (cacheProvider != null) ? cacheProvider : new OrmoCache();

            // hosted in IIS
            string AppData = HttpContext.Current.Server.MapPath("~/App_Data");

            // SqLite DB
            var SqliteDB = Path.Combine(AppData, "mydb2.db");

            // Set and open connection
            SQLiteConnection = new SQLiteConnection($"Data Source={SqliteDB};Version=3;");
            SQLiteConnection.Open();
        }
        
      
        // Close Connection
        public void closeConnection()
        {
            if (SQLiteConnection.State == ConnectionState.Open)
                SQLiteConnection.Close();
        }

        

        /******************************************************
         * Retrieve entity using the primary key (using cache)
        *******************************************************/
        public T Query<T>(int rowId) where T : class, new()
        {
            // trye to get entity from cache
            var entity = ormoCache.Get<T>(Convert.ToString(rowId));

            // if entity exists return it otherwise get it from database
            if (entity != null)
                return entity;

            // Type of the entity
            Type t = typeof(T);

            // read attribute TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;

            if (tableMapperAttribute == null)
                throw new Exception("Attributo TableMapper non trovato sulla classe.");

            // get all attributes of type 'ColumnMapper' 
            var attributes = from p in t.GetProperties()
                             let attr = p.GetCustomAttribute(typeof(ColumnMapper), false)
                             select attr as ColumnMapper;

            // get ColumnMapper primary key
            var primarykeyAttribute = from a in attributes
                                      where a != null && a.IsPrimaryKey()
                                      select a;

            // check if exists one and only one primary key attribute 
            if (!primarykeyAttribute.Any() || primarykeyAttribute.Count() > 1)
                throw new Exception("Primary Key non specificata per la classe oppure più campi marcati come primary key. " +
                                    "Controllare l'attributo ColumnMapper sulla classe.");

            // column name primary key
            var columnNamePK = primarykeyAttribute.First().GetColumnName();

            // compose query
            string sql = string.Format("SELECT * FROM {0} WHERE {1} = {2}",
                tableMapperAttribute.GetTableName(), columnNamePK, rowId);

            // retrieve entity
            entity = Query<T>(sql);

            // get all class properties 
            PropertyInfo[] props = t.GetProperties();

            // loop on properties
            foreach (PropertyInfo prop in props)
            {
                // looking for custom attribute RelatedEntityMapper
                var attribute = (RelatedEntityMapper)Attribute.GetCustomAttribute(prop, typeof(RelatedEntityMapper));

                // if exists
                if (attribute != null)
                {
                    // read related table and foreign key
                    var relatedTable = attribute.GetRelatedTableName();
                    var relatedForeignKey = attribute.GetRelatedForeignKey();

                    // get type of List<T> (first item of GenericTypeArguments collection)
                    var genericListType = prop.GetMethod.ReturnParameter.ParameterType.GenericTypeArguments[0];

                    // compose query and get related entities
                    string sqlRelated = string.Format("SELECT * FROM {0} WHERE {1} = {2}",
                        relatedTable, relatedForeignKey, rowId);

                    // invoke QueryMany<T> building dynamic call 
                    MethodInfo method = this.GetType().GetMethod("QueryMany").MakeGenericMethod(new Type[] { genericListType });
                    dynamic relatedEntities = method.Invoke(this, new object[] { sqlRelated });

                    // set list<T> of related entities
                    SetValue(entity, prop.Name, relatedEntities, relatedEntities.GetType());
                }
            }         

            // return entity
            return entity;
        }



        /*************************
        * Query 1 row (no cache)
        *************************/
        public T Query<T>(string sql) where T : class, new()
        {
            // eseguo la query per prendere i dati che riempiranno la POCO class
            DataRow row = executeQuery(sql);

            // nessun record trovato
            if (row == null) return null;

            // Creo l'oggetto dalla row
            T result = MapToObject<T>(row);

            // update cache
            ormoCache.Set<T>(result);

            // ritorno l'oggetto
            return result;
        }




        /*****************************************************************
         * Query multiple rows (non uso la cache ma la aggiorno sempre)
        *****************************************************************/
        public IEnumerable<T> QueryMany<T>(string sql) where T : class, new()
        {
            // eseguo la query per prendere i dati
            DataTable dt = executeQuery(sql);

            // creo la lista di oggetti da restituire
            List<T> results = new List<T>();

            // if no records, return empty List<T>
            if (dt == null)
                return results;

            foreach (DataRow row in dt.Rows)
            {
                // Creo l'oggetto dalla row
                T result = MapToObject<T>(row);

                // aggiungo alla lista
                results.Add(result);

                // aggiungo l'entità alla cache  
                // ATTENZIONE: se aggiungo le entità correlate alla cache può capitare che se aggiorno le entità correlate 
                // esse non si aggiornano nell'oggetto padre; 
                // es: utente 1 ha 3 prodotti (utente 1 in cache). Aggiorno uno dei 3 prodotti e finisce in cache ma l'elenco delle entità correlate di
                // utente 1 ha ancora le vecchie entità. ATTENZIONE!
                
                // Soluzione 1: il metodo QueryMany non mette niente in cache
                //ormoCache.Set<T>(result);                 
            }
            
            // ritorno la lista
            return results;            
        }




        /*************************************
         * Query rows related by foreign key
        *************************************/
        public IEnumerable<T> QueryManyRelated<T>(string table, string foreignKey, int primaryKeyValue) where T : class, new()
        {
            // compongo la query
            string sql = string.Format("SELECT * FROM {0} WHERE {1} = {2}", table, foreignKey, primaryKeyValue);

            return QueryMany<T>(sql);  
        } 



        /*****************************
         * Insert Entity in table
        *****************************/
        public int Insert<T>(T instance) where T : class, new()
        {
            // Type della classe
            Type t = typeof(T);

            // leggo l'attributo TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;

            if (tableMapperAttribute == null)
                throw new Exception("Attributo TableMapper non trovato sulla classe.");

            // Creo command e collection dei parametri
            using (SQLiteCommand command = new SQLiteCommand(SQLiteConnection))
            {
                // prendo tutte le properties della classe
                PropertyInfo[] props = t.GetProperties();

                // columns list
                List<string> columnList = new List<string>();

                // per ogni proprietà prendo il nome della colonna della tabella
                foreach (PropertyInfo prop in props)
                {
                    // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
                    var attr = (ColumnMapper)Attribute.GetCustomAttribute(prop, typeof(ColumnMapper));

                    if (attr != null)
                    {
                        // leggo la colonna per la mappatura della tabella e il suo valore              
                        string columnName = attr.GetColumnName();

                        // escludo la colonna PrimaryKey
                        if (!attr.IsPrimaryKey())
                        {
                            var property = instance.GetType().GetProperty(prop.Name);
                            var value = property.GetValue(instance);

                            // Add parameter
                            command.Parameters.AddWithValue("@" + columnName, value);
                            columnList.Add(columnName);
                        }
                    }
                }

                // collection dei parametri
                var parametersList = command.Parameters.Cast<SQLiteParameter>().Select(p => p.ParameterName);

                // Prepare query
                command.CommandText = string.Format("INSERT INTO {0}  ({1})  VALUES ({2})",
                    tableMapperAttribute.GetTableName(), String.Join(",", columnList), String.Join(",", parametersList));

                Debug.WriteLine(command.CommandText);

                // preparo la query
                command.Prepare();

                // eseguo la query
                command.ExecuteNonQuery();

                // ritorno il last row id
                command.CommandText = "SELECT last_insert_rowid()";
                var rowId = (Int64)command.ExecuteScalar(CommandBehavior.Default);

                // cast a int 
                return Convert.ToInt32(rowId);                
            }            
        }



        /*****************************
         * Update entity in table
        *****************************/
        public bool Update<T>(T instance) where T : class, new()
        {
            // Type della classe
            Type t = typeof(T);

            // leggo l'attributo TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;

            if (tableMapperAttribute == null)
                throw new Exception("Attributo TableMapper non trovato sulla classe.");

            // Creo command e collection dei parametri
            using (SQLiteCommand command = new SQLiteCommand(SQLiteConnection))
            {
                // prendo tutte le properties della classe
                PropertyInfo[] props = t.GetProperties();

                // columns list
                List<string> sqlParams = new List<string>();

                var sqlBuilder = new StringBuilder();
                var primaryKeyColumn = "";
                var primaryKeyId = 0;

                // per ogni proprietà prendo il nome della colonna della tabella
                foreach (PropertyInfo prop in props)
                {
                    // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
                    var attr = (ColumnMapper)Attribute.GetCustomAttribute(prop, typeof(ColumnMapper));

                    if (attr != null)
                    {
                        // escludo la colonna PrimaryKey
                        if (attr.IsPrimaryKey())
                        {
                            primaryKeyColumn = prop.Name;
                            var property = instance.GetType().GetProperty(prop.Name);
                            primaryKeyId = Convert.ToInt32(property.GetValue(instance));                            
                        }
                        else
                        {
                            // leggo la colonna per la mappatura della tabella e il suo valore              
                            string columnName = attr.GetColumnName();

                            // leggo il valore della proprietà
                            var property = instance.GetType().GetProperty(prop.Name);
                            var value = property.GetValue(instance);

                            // Add parameter
                            command.Parameters.AddWithValue("@" + columnName, value);
                            sqlParams.Add(columnName + "=@" + columnName);
                        }
                    }
                }

                // collection dei parametri
                var parametersList = command.Parameters.Cast<SQLiteParameter>().Select(p => p.ParameterName);

                // Prepare query
                command.CommandText = string.Format("UPDATE {0} SET {1} WHERE {2}",
                    tableMapperAttribute.GetTableName(), string.Join(",", sqlParams), primaryKeyColumn + "=" + primaryKeyId);

                Debug.WriteLine(command.CommandText);

                // preparo la query
                command.Prepare();

                // eseguo la query
                int rowsAffected = command.ExecuteNonQuery();
                
                // lo rimuovo dalla cache se l'update è OK
                if(rowsAffected == 1)
                {                    
                    ormoCache.Delete<T>(Convert.ToString(primaryKeyId));
                    return true;
                }
                else
                {
                    return false;
                }                
            }
        }



         /************************
         *   Delete entity
         ************************/
         public bool Delete<T>(int primaryKeyId) where T : class, new()
         {
            // Type della classe
            Type t = typeof(T);

            // leggo l'attributo TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;

            if (tableMapperAttribute == null)
                throw new Exception("Attributo TableMapper non trovato sulla classe.");

            // prendo tutti gli attributi di tipo ColumnMapper 
            var attributes = from p in t.GetProperties()
                             let attr = p.GetCustomAttribute(typeof(ColumnMapper), false)
                             select attr as ColumnMapper;

            // tra gli attributi ColumnMapper prendo quello della primary key
            var primarykeyAttribute = from a in attributes
                                      where a != null && a.IsPrimaryKey()
                                      select a;

            // se non ce ne sono oppure ce ne sono più di una errore
            if (!primarykeyAttribute.Any() || primarykeyAttribute.Count() > 1)
                throw new Exception("Primary Key non specificata per la classe oppure più campi marcati come primary key. " +
                                    "Controllare l'attributo ColumnMapper sulla classe.");

            // column name primary key
            var columnNamePK = primarykeyAttribute.First().GetColumnName();

            // Creo command e collection dei parametri
            using (SQLiteCommand command = new SQLiteCommand(SQLiteConnection))
            {
                // query
                string sql = string.Format("DELETE FROM {0} WHERE {1} = @ID", tableMapperAttribute.GetTableName(), columnNamePK);

                // add parameter
                command.Parameters.AddWithValue("@" + columnNamePK, primaryKeyId);

                // set command
                command.CommandText = sql;

                // preparo la query
                command.Prepare();

                // eseguo la query
                int rowsAffected = command.ExecuteNonQuery();

                // lo rimuovo dalla cache se delete è OK
                if (rowsAffected == 1)
                {
                    ormoCache.Delete<T>(Convert.ToString(primaryKeyId));
                    return true;
                }
                else
                {
                    return false;
                }                          
            }
         }


        

        /*******************************************************
         *   MapToObject: mappatura di un record ad un oggetto
         *******************************************************/
        private T MapToObject<T>(DataRow row) where T : class, new()
        {
            // Type della classe
            Type t = typeof (T);

            // prendo tutte le properties della classe
            PropertyInfo[] props = t.GetProperties();
            
            // istanzio la classe generica
            T returnClass = new T();

            // per ogni proprietà prendo il nome della colonna della tabella
            foreach (PropertyInfo prop in props)
            {
                // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
                var attr = Attribute.GetCustomAttribute(prop, typeof (ColumnMapper));

                if (attr != null)
                {
                    // leggo la colonna per la mappatura della tabella               
                    string columnName = ((ColumnMapper) attr).GetColumnName();

                    // leggo il valore dalla colonna
                    var value = row[columnName];
                    
                    // controllo la correttezza della mappatura con i nomi delle colonne
                    if (!CheckMappingToTable(row, prop))
                        throw new Exception("Mappatura errata delle proprietà con la tabella; controllare i nomi del mapper e delle colonne della tabella.");

                    // setto il valore nella proprietà dell'oggetto
                    SetValue(returnClass, prop.Name, value, prop.PropertyType);
                }
            }
           
            // ritorno l'oggetto
            return returnClass;
        }


        private bool CheckMappingToTable(DataRow row, PropertyInfo prop)
        {
            // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
            var attr = Attribute.GetCustomAttribute(prop, typeof (ColumnMapper));

            // leggo la colonna per la mappatura della tabella               
            string columnName = ((ColumnMapper) attr).GetColumnName();

            // controllo che esista il nome dell'attributo ColumnMapper nelle colonne della tabella
            return  row.Table.Columns.Contains(columnName);
        }


        // Eseguo la query sul DB
        private dynamic executeQuery(string query)
        {
            // creo un command
            using (SQLiteCommand command = new SQLiteCommand(query, SQLiteConnection))
            {
                // eseguo la query
                using (var dr = command.ExecuteReader(CommandBehavior.Default))
                {
                    // carico il DataTable
                    DataTable dt = new DataTable();
                    dt.Load(dr);

                    // ritorno il DataTable o la riga o null se non ho trovato niente
                    if (dt.Rows.Count == 1)
                        return dt.Rows[0];
                    else if (dt.Rows.Count == 0)
                        return null;
                    else
                        return dt;                    
                }                
            }            
        }

        
        // setta il valore di una proprietà con la reflection
        protected void SetValue(object theObject, string theProperty, object theValue, Type type)
        {
            // se non riesco a settare il valore continuo e loggo l'errore
            try
            {
                var msgInfo = theObject.GetType().GetProperty(theProperty);
                msgInfo.SetValue(theObject, Convert.ChangeType(theValue, type), null);
            }            
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        public bool GenerateTable<T>() where T: class, new()
        {
            // Type della classe
            Type t = typeof(T);

            // leggo l'attributo TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;

            if (tableMapperAttribute == null)
                throw new Exception("Attributo TableMapper non trovato sulla classe.");

            List<string> columns = new List<string>();
            string columnNamePK = "";

            // prendo tutte le properties della classe
            PropertyInfo[] props = t.GetProperties();

            // per ogni proprietà prendo il nome della colonna della tabella
            foreach (PropertyInfo prop in props)
            {
                // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
                var attr = (ColumnMapper)Attribute.GetCustomAttribute(prop, typeof(ColumnMapper));
                
                // se la proprietà ha il custom attribute RelatedEntityMapper leggo il nome della colonna
                //var attrRelated = (RelatedEntityMapper)Attribute.GetCustomAttribute(prop, typeof(RelatedEntityMapper));
                
                if (attr != null && attr is ColumnMapper)
                {
                    // Convert CLR Type to SQLite data type
                    string SQLiteDataType = ToSQLiteType(prop.GetMethod.ReturnParameter.ParameterType);

                    // se primary key (è sempre INTEGER)
                    if (attr.IsPrimaryKey())
                    {
                        // leggo la colonna per la mappatura della tabella               
                        columnNamePK = attr.GetColumnName();
                    }
                    else
                    {
                        columns.Add('"' + attr.GetColumnName() + '"' + " " + SQLiteDataType);
                    }
                }
            }
            
            string sql = string.Format("CREATE TABLE \"{0}\" (\"{1}\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, {2});", 
                tableMapperAttribute.GetTableName(), columnNamePK, string.Join(",", columns));
            
            Debug.WriteLine(sql);

            // Create table
            try
            {                
                using (SQLiteCommand command = new SQLiteCommand(SQLiteConnection))
                {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }

                return true;
            }
            catch (Exception Ex)
            {
                return false;
            }
        }


        // Convert CLR Data types to Sqlite data type
        private string ToSQLiteType(Type CLRType)
        {
            switch (CLRType.ToString())
            {
                case "System.Int16":
                case "System.Int32":
                case "System.Int64":
                case "System.Byte":
                case "System.SByte":
                case "System.UInt16":
                case "System.UInt32":
                case "System.UInt64":
                    return "INTEGER";

                case "System.Decimal":
                case "System.Double":
                    return "DOUBLE";

                case "System.Single":
                    return "FLOAT";

                case "System.String":
                    return "TEXT";

                case "System.Char":
                    return "CHAR";

                case "System.DateTime":
                    return "DATETIME";

                case "System.Boolean":
                    return "BOOL";

                default:
                    throw new ArgumentOutOfRangeException(nameof(CLRType));
            }
        }

        
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    if(SQLiteConnection != null)
                    {
                        SQLiteConnection.Dispose();  
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Ormo() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}