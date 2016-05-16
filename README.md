# Ormo
simple and minimalist SQLite ORM.

# How to use

1. add Ormo files in a folder named Ormo in your project
2. add SQLite assembly to your project (of course)
3. add reference to System.Runtime.Caching.dll assembly (for cache)
4. in Ormo class set connection string to your SQLite DB
3. create your model classes and decorate with Ormo Attributes (ColumnMapper and TableMapper) 
4. use Ormo methods to query your SQLite database.



Set connection string in Ormo class (Ormo.cs)
:
```
// SET YOUR CONNECTION STRING TO SQLite DB!
private readonly string connectionString = "<my connection string to Database>";
```

Decorate your model class:

```
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using CM.Ormo;


namespace ApiJS.Models
{
    [TableMapper("UTENTI")]         // table name
    public class Person
    {
        [ColumnMapper("ID", true)]          // column name, isPrimaryKey
        public int ID { get; set; }

        [ColumnMapper("Nome")]
        public string Nome { get; set; }

        [ColumnMapper("Cognome")]
        public string Cognome { get; set; }

        [ColumnMapper("Indirizzo")]
        public string Indirizzo { get; set; }

        [ColumnMapper("Cap")]
        public int CAP { get; set; }

        [ColumnMapper("Citta")]
        public string Citta { get; set; }

        [ColumnMapper("data_inserimento")]
        public DateTime data_inserimento { get; set; }

        [RelatedEntityMapper("PRODUCTS", "UserId")]     // related table, foreign key (use List<T> for collections                                                                    // of related entities)
        public List<Product> Prodotti { get; set; } 
    }
}
```

then use Ormo to retrieve 1 entity:

```
Person persona;

// Retrieve entity by ID
using (Ormo Ormo = new Ormo())
{
    persona = Ormo.Query<Person>(user.UserId);
}
```

Use ```insert``` to insert a entity in a table:

```
var persona = new Person();
persona.Nome = user.Nome;
persona.Cognome = user.Cognome;
persona.Indirizzo = user.Indirizzo;
persona.CAP = user.Cap;
persona.Citta = user.Citta;
persona.data_inserimento = DateTime.Now;

// Ormo
using (Ormo Ormo = new Ormo())
{
    var id = Ormo.Insert<Person>(persona);
}
 ```          

Use ```QueryMany``` to get many records from table:

 ```          
 // many persons
string sql = "SELECT * FROM UTENTI WHERE ID > 0";

// all users
IEnumerable<Person> persone;

using (Ormo Ormo = new Ormo())
{
    persone = Ormo.QueryMany<Person>(sql);                
}
 ```          

Use ```Update``` to update entity:

```
using (Ormo Ormo = new Ormo())
{
    Person persona = Ormo.Query<Person>(user.UserId);
    persona.Indirizzo = "TEST";
    var result = Ormo.Update<Person>(persona);
}
```
