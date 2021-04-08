//Copyright © 2021 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;
using AsyncPoco;
using Npgsql;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    public class PostgreSqlRunner : IRunner {
        private static object lck = new object();
        private readonly Action<Database> _extraInitStep;
        private string DbName => "statyppocoq_asyncpocodpy"; //keep it lowercase

        private static string[] SqlCreateTestTable = {
            @"create table someentity (
                id serial not null,
                anInt integer not null, 
                aString text null, 
                nullableInt integer null,
                actualName text null,

	            CONSTRAINT pk_someentity PRIMARY KEY (id));",
            @"create table specialentity (
                id serial not null,
                ""table"" integer not null, 
                ""create"" text null, 
                ""null"" integer null,

	            CONSTRAINT pk_specialentity PRIMARY KEY (id))"
        };

        public PostgreSqlRunner(Action<Database> extraInitStep = null) => _extraInitStep = extraInitStep;
        
        public async Task Run(Action<string> logger, Func<Database, Task> testBody) {
            //needed to really close connections as formerly "closed" connection goes to pool and is implicitly blocking database on the server side.
            //Without it will get error 55006
            NpgsqlConnection.ClearAllPools(); 

            var connStr =
                Environment.GetEnvironmentVariable("TEST_POSTGRESQL_CONNECTIONSTRING")
                ??
                "Host=127.0.0.1;Username=statyppocoqueries_tester_user;Password=statyppocoqueries_tester_passwd;Database=postgres";
        
            using (var rawDbConn = new Npgsql.NpgsqlConnection(connStr)) {
                rawDbConn.Open();
                rawDbConn.ChangeDatabase("postgres");

                var dbDoesntExistRaw = await ExecuteScalar(
                    rawDbConn, $"SELECT 0 FROM pg_database WHERE datname='{DbName}'");
                var dbDoesntExist = dbDoesntExistRaw is DBNull || dbDoesntExistRaw == null;

                if (!dbDoesntExist) {
                    await ExecuteNonQuery(rawDbConn, $"drop database {DbName}");
                }

                await ExecuteNonQuery(rawDbConn, $"create database {DbName}");
                rawDbConn.ChangeDatabase(DbName);

                foreach (var query in SqlCreateTestTable) {
                    await ExecuteNonQuery(rawDbConn, query);
                }

                using (var asyncPocoDb = new Database(rawDbConn)) {
                    _extraInitStep?.Invoke(asyncPocoDb);
                    await testBody(asyncPocoDb);

                    rawDbConn.ChangeDatabase("postgres");
                    await ExecuteScalar(rawDbConn, @"SELECT 1");

                    //This doesn't really close connection. Connection goes back to pool STILL referencing test database (on the server side).
                    //Next test will fail because of it (error 55006).
                    rawDbConn.Close(); 
                }
            }
        }

        private Task<int> ExecuteNonQuery(NpgsqlConnection dbConn, string sql) {
            using (var cmd = new NpgsqlCommand(sql, dbConn)) {
                return cmd.ExecuteNonQueryAsync();
            }
        }

        private Task<object> ExecuteScalar(NpgsqlConnection dbConn, string sql) {
            using (var cmd = new NpgsqlCommand(sql, dbConn)) {
                return cmd.ExecuteScalarAsync();
            }
        }
    }
}
