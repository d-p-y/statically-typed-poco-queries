using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.Tests {
    public class SqliteRunner : IRunner {
        private static string SqlCreateTestTable = @"
            create table SomeEntity (
                id integer primary key autoincrement not null,
                anInt int not null, 
                aString varchar(50) null, 
                nullableInt int null
            )";
        
        public async Task Run(Action<string> logger, Func<Database,Task> testBody) {
            using (SQLiteConnection db = new SQLiteConnection("Data Source=:memory:")) {
                db.Open();

                using (var cmd = new SQLiteCommand(SqlCreateTestTable, db)) {
                    cmd.ExecuteNonQuery();
                }
            
                var asyncPocoDb = new Database(db);
                await testBody(asyncPocoDb);
            }        
        }
    }
}
