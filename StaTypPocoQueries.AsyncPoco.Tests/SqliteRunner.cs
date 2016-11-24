using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
                nullableInt int null)",
            @"create table SpecialEntity (
                id integer primary key autoincrement not null,
                `table` int not null, 
                `create` nvarchar(50) null, 
                `null` int null
	            )" };
        
        public async Task Run(Action<string> logger, Func<Database,Task> testBody) {
            using (SQLiteConnection db = new SQLiteConnection("Data Source=:memory:")) {
                db.Open();

                Array.ForEach(SqlCreateTestTable, x=> {
                    using (var cmd = new SQLiteCommand(x, db)) {
                        cmd.ExecuteNonQuery();
                    }
                });
                
                var asyncPocoDb = new Database(db);
                await testBody(asyncPocoDb);
            }        
        }
    }
}
