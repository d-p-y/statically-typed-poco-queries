# Statically Typed Poco Queries

# What is this?

It is a library providing ability to query "Poco family of ORMish things" using statically typed queries. Strictly speaking ```StaTypPocoQueries.Core``` is a translator of Linq Expressions into sql text query and its bound parameters. ```StaTypPocoQueries.AsyncPoco``` provides extensions to [AsyncPoco](https://github.com/tmenier/AsyncPoco) ```AsyncPoco.Database``` class so that queries become available 'natively'. It is possible to use ```StaTypPocoQueries.Core``` to provide extensions to other Poco libraries. Use [StaTypPocoQueries.PetaPoco](https://github.com/asherber/StaTypPocoQueries.PetaPoco) to use it with PetaPoco. 
Original idea for the library: Tytusse (https://github.com/tytusse)

Given one has following entity defined:
```csharp
[TableName("SomeEntity")]
[PrimaryKey("id")]
[ExplicitColumns] 
class SomeEntity {
    [Column] public int Id { get; set; }
    [Column] public string SomeColumn { get; set; }
}
```

one can write 
```csharp
db.FetchAsync<SomeEntity>(x => x.SomeColumn == "foo"); //db is of type AsyncPoco.Database
```

and call will be translated to 

```csharp
db.FetchAsync<SomeEntity>("where [SomeColumn] = @0", new []{"foo"}) 
```

# Full Example

```csharp
using AsyncPoco;
using StaTypPocoQueries.AsyncPoco;

[TableName("SomeEntity")]
[PrimaryKey("id")]
[ExplicitColumns] 
class SomeEntity {
    [Column] public int Id { get; set; }
    [Column] public string SomeColumn { get; set; }
    [Column] public int anInt { get; set; }
    [Column] public int? nullableInt { get; set; }
}

class YourDataAccessLayer {
	async Task<List<SomeEntity>> Something(Database db) { 
		return await db.FetchAsync<SomeEntity>(x => x.SomeColumn == "foo");
	}
}
```

# How to install it?

Install it from nuget using Visual Studio's dialogs or from command line
```
Install-Package StaTypPocoQueries.Core
```
```
Install-Package StaTypPocoQueries.AsyncPoco
```

... or compile it from sources.

# Is it stable?

I think so. There are plenty of tests for both the [translator](https://github.com/d-p-y/statically-typed-poco-queries/tree/master/StaTypPocoQueries.Core.Tests) and for [AsyncPoco wrapper](https://github.com/d-p-y/statically-typed-poco-queries/tree/master/StaTypPocoQueries.AsyncPoco.Tests) using Sqlite and containerized Sql Server.

# Features (which constructs are supported?)

**NOTE:** for brevity all examples below assume quoting of special names using Sql Server dialect: _[something]_ is used to quote _something_

* Database support  
  Supported and tested are Sql Server and Sqlite. Supported but not tested are: PostgreSQL, Mysql and Oracle.
* .NET version support (tested)  
  - .NET Framework 5.0.5 under Windows 10  
  - Mono 6.12 under Windows 10
  - dotnet standard 2.0 or [meaning dotnet core 2.0 or later is fine](https://devblogs.microsoft.com/dotnet/announcing-net-standard-2-0/)
* Language support  
  Both C&#35; and F&#35; is supported. More specifically translation is possible for System.Linq.Expressions and Microsoft.FSharp.Quotations
* Quoting of reserved names such as 'table'  
  All column names are quoted to avoid usage of special names. Internally database dialect is inferred from database connection class and proper quoting mechanism is chosen.
  NOTE: all following examples assume square brackets as quoting characters but that is just for sake of example.
* Parameters in queries can be constants, nullable variables, not nullable properties  
* Simple equals / not equals  
  ```csharp
  //following code...
  var aVar = 5;
  db.MethodNeedingQuery<SomeEntity>(x => x.anInt == aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<SomeEntity>("[anInt] = @0", new [] {5})
  ```
* Is greater, smaller, greater-or-equal, smaller-or-equal than  
  ```csharp
  //following code...
  var aVar = 5;
  db.MethodNeedingQuery<SomeEntity>(x => x.anInt >= aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<SomeEntity>("[anInt] >= @0", new [] {5})
  ```
* Is null / is not null  
  ```csharp
  //following code...
  string aVar = null;
  db.MethodNeedingQuery<SomeEntity>(x => x.SomeColumn == aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<SomeEntity>("[SomeColumn] is null")
  ```
* 'or' and 'and' junctions without superfluous square brackets  
  ```csharp
  //following code...
  db.MethodNeedingQuery<SomeEntity>(x => x.anInt == 3 || x.anInt == 4)
  
  //...is translated to:
  db.MethodNeedingQuery<SomeEntity>("[anInt] = @0 or [anInt] = @1", new [] {3,4})
  ```
  
  ```csharp
  //following code...
  db.MethodNeedingQuery<SomeEntity>(x => (x.anInt == 6 || x.SomeColumn == "foo") &&  (x.anInt == 4 || x.SomeColumn == "bar") )
  
  //...is translated to:
  db.MethodNeedingQuery<SomeEntity>("([anInt] = @0 or [SomeColumn] = @1) and ([anInt] = @2 or [SomeColumn] = @3)", new [] {6,"foo",4,"bar"})
  ```

 * 'Nullable<T>.HasValue and Nullable<T>.Value'  
   ```csharp
   db.MethodNeedingQuery<SomeEntity>(x => !x.nullableInt.HasValue || x.nullableInt.Value == 5)

   //...is translated to:
   db.MethodNeedingQuery<SomeEntity>("[nullableInt] IS NULL OR [nullableInt] = @0", new []{5})
   ```

* Array.Contains(item) translated into native array without expanding array into as many parameters as there is in collection. It is optional feature. Needs support from both *Poco library and underlying database client library.
  ```csharp
    var neededStrs = new[] { "foo2", "foo1" };
    db.MethodNeedingQuery<SomeEntity>(x => neededStrs.Contains(x.AString));
  ```
  for more info:
  * see [tests](https://github.com/d-p-y/statically-typed-poco-queries/blob/master/StaTypPocoQueries.AsyncPocoDpy.Tests/AsyncPocoDpyWrapperArrayTests.cs) for more info about usage
  * [my fork of AsyncPoco that supports it](https://github.com/d-p-y/AsyncPoco)
