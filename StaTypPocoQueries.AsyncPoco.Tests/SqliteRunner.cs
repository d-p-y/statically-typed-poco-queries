//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.Tests {
    public class SqliteRunner : IRunner {
        private static string[] SqlCreateTestTable =  {
            @"create table SomeEntity (
                id integer primary key autoincrement not null,
                anInt int not null, 
                aString varchar(50) null, 
                nullableInt int null,
                actualName varchar(50) null)",
            @"create table SpecialEntity (
                id integer primary key autoincrement not null,
                `table` int not null, 
                `create` nvarchar(50) null, 
                `null` int null
	            )" };
        
        public async Task Run(Action<string> logger, Func<Database,Task> testBody) {
            using (var db = new SqliteConnection("Data Source=:memory:")) {
                db.Open();

                Array.ForEach(SqlCreateTestTable, x=> {
                    using (var cmd = db.CreateCommand()) {
                        cmd.CommandText = x;
                        cmd.ExecuteNonQuery();
                    }
                });
                
                var asyncPocoDb = new Database(db);
                try {
                    await testBody(asyncPocoDb);
                } catch(Exception ex) {
                    logger($"got exception {ex}");
                    logger($"last sql: {asyncPocoDb.LastSQL}");
                    throw;
                }
            }        
        }
    }
}
