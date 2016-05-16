using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Web;
using System.Diagnostics;
using System.Reflection;


namespace CM.Ormo
{
    /*
        NOTE: You should be aware that MemoryCache will be disposed every time IIS do app pool recycle.
      
        If your Web API :

        - Does not receive any request for more that 20 minutes
        - Or hit default IIS pool interval 1740 minutes
        - Or you copy a new version of your ASP.NET Web API project into an IIS folder (this will auto-trigger app pool recycle)        
        So you should have a workaround for this. If you don’t get value from the cache you should grab it for example from a database and then store it again in the cache:
        
        var result = memCacher.Get(token);
        if (result == null)
        {
            // for example get token from database and put grabbed token
            // again in memCacher Cache
        } 
     */
    public class OrmoCache : IOrmoCache
    {
        // Cache time expiration (spostarlo nel web.config)
        private DateTimeOffset absExpiration = new DateTimeOffset(DateTime.Now.AddHours(1));

        public T Get<T>(string key)  where T : class, new()
        {
            // Oggetto MemoryCache
            MemoryCache memoryCache = MemoryCache.Default;
            
            // ritorno l'oggetto
            return (T)memoryCache.Get(getCacheItemKey<T>(key));
        }

      
        // Overload Add
        public void Set<T>(T instance) where T : class, new()
        {
            // se l'oggetto è nullo esco subito
            if (instance == null)
                return;

            // Oggetto MemoryCache
            MemoryCache memoryCache = MemoryCache.Default;

            // aggiungo l'oggetto (il metodo set aggiunge un oggetto se non esiste la chiave oppure l'aggiorna se la chiave c'è già)
            memoryCache.Set(
                getCacheItemKey<T>(instance),
                instance,
                absExpiration);
        }


        public void Delete<T>(string key) where T : class , new()
        {
            // recupero la key
            var cacheItemKey = getCacheItemKey<T>(key);

            MemoryCache memoryCache = MemoryCache.Default;
            if (memoryCache.Contains(cacheItemKey))
                memoryCache.Remove(cacheItemKey);
        }

        private string getCacheItemKey<T>(string key) where T : class, new()
        {
            // Type della classe
            Type t = typeof(T);

            // Leggo l'attributo TableMapper
            var tableMapperAttribute = t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault() as TableMapper;
            
            // Compongo la key, in base al tipo di oggetto T, la compongo in questo modo: tableName_id, se non esiste uso la key passata direttamente.             
            var cacheItemKey = (tableMapperAttribute == null) ? key : String.Concat(tableMapperAttribute.GetTableName(), "[_", key, "_]");

            // ritorno la key
            return cacheItemKey;
        }


        // Overload: in caso non ho la key la ricavo dall'istanza dell'entità
        private string getCacheItemKey<T>(T instance) where T : class, new()
        {
            // Type della classe
            Type t = typeof(T);

            // Leggo l'attributo TableMapper
            var tableMapperAttribute = (TableMapper)t.GetCustomAttributes(typeof(TableMapper), false).FirstOrDefault();

            if (tableMapperAttribute == null)
                throw new Exception("Errore, l'entità " + t.ToString() + " non ha l'attributo TableMapper impostato");
        
            // prendo tutte le properties della classe
            PropertyInfo[] props = t.GetProperties();

            // init primaryKeyId
            int? primaryKeyId = null;

            foreach (PropertyInfo prop in props)
            {
                // se la proprietà ha il custom attribute ColumnMapper leggo il nome della colonna
                var attr = (ColumnMapper)Attribute.GetCustomAttribute(prop, typeof(ColumnMapper));

                if (attr.IsPrimaryKey())
                {
                    var property = instance.GetType().GetProperty(prop.Name);
                    primaryKeyId = Convert.ToInt32(property.GetValue(instance));
                    
                    // trovata la primary key, esco dal ciclo
                    break;
                }
            }
            
            // se non ho trovato la primary Key errore
            if(primaryKeyId == null)
                throw new Exception("Errore, ID dell'entità non trovata, non posso aggiungere l'entità alla cache");            

            // Compongo la key, in base al tipo di oggetto T, la compongo in questo modo: tableName_id, se non esiste uso la key passata direttamente.             
            var cacheItemKey = String.Concat(tableMapperAttribute.GetTableName(), "[_", primaryKeyId, "_]");

            // ritorno la key
            return cacheItemKey;
        }

        public long Count()
        {
            // Oggetto MemoryCache
            MemoryCache memoryCache = MemoryCache.Default;

            return memoryCache.GetCount();
        }
    }
}