# Statically Typed Poco Queries

# What is this?

It is a library providing ability to query "Poco family of ORMish things" using statically typed queries. Strictly speaking ```StaTypPocoQueries.Core``` is a translator of Linq Expressions into sql text query and its bound parameters. ```StaTypPocoQueries.AsyncPoco``` provides extensions to [AsyncPoco](https://github.com/tmenier/AsyncPoco) ```AsyncPoco.Database``` class so that queries become available 'natively'. It is possible to use ```StaTypPocoQueries.Core``` to provide extensions to other Poco libraries such as [PetaPoco](https://github.com/CollaboratingPlatypus/PetaPoco).
Idea by: Tytusse (https://github.com/tytusse)

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

**NOTE:** for brewity all examples below assume quoting of special names using Sql Server dialect: _[something]_ is used to quote _something_

* Database support  
  Supported and tested are Sql Server and Sqlite. Supported but not tested are: Postgresql, Mysql and Oracle.
* .NET version support (tested)  
  - .NET Framework 4.5.2 under Windows 7  
  - Mono 4.2.1 under Ubuntu 16.04
  - dotnet core 2.1 or newer
* Language support  
  Both C&#35; and F&#35; is supported. More specifically translation is possible for System.Linq.Expressions and Microsoft.FSharp.Quotations
* Quoting of reserved names such as 'table'  
  All column names are quoted to avoid usgae of special names. Internally database dialect is infered from database connection class and proper quoting mechanism is choosen.
* Parameters in queries can be constants, nullable variables, not nullable properties  
* Simple equals / not equals  
  ```csharp
  //following code...
  var aVar = 5;
  db.MethodNeedingQuery<Entity>(x => x.anInt == aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<Entity>("[anInt] = @0", new [] {5})
  ```
* Is greater, smaller, greater-or-equal, smaller-or-equal than  
  ```csharp
  //following code...
  var aVar = 5;
  db.MethodNeedingQuery<Entity>(x => x.anInt >= aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<Entity>("[anInt] >= @0", new [] {5})
  ```
* Is null / is not null  
  ```csharp
  //following code...
  string aVar = null;
  db.MethodNeedingQuery<Entity>(x => x.SomeColumn == aVar)
  
  //...is translated to:
  db.MethodNeedingQuery<Entity>("[SomeColumn] is null")
  ```
* 'or' and 'and' junctions without superfluous square brackets  
  ```csharp
  //following code...
  db.MethodNeedingQuery<Entity>(x => x.anInt == 3 || x.anInt == 4)
  
  //...is translated to:
  db.MethodNeedingQuery<Entity>("[anInt] = @0 or [anInt] = @1", new [] {3,4})
  ```
  
  ```csharp
  //following code...
  db.MethodNeedingQuery<Entity>(x => (x.anInt == 6 || x.SomeColumn == "foo") &&  (x.anInt == 4 || x.SomeColumn == "bar") )
  
  //...is translated to:
  db.MethodNeedingQuery<Entity>("([anInt] = @0 or [SomeColumn] = @1) and ([anInt] = @2 or [SomeColumn] = @3)", new [] {6,"foo",4,"bar"})
  ```
  