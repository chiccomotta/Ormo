using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CM.Ormo
{
    // Interfaccia per OrmoCache
    public interface IOrmoCache
    {
        T Get<T>(string key)  where T : class, new();
        void Set<T>(T instance) where T : class, new();
        void Delete<T>(string key) where T : class , new();
        long Count();
    }
}
