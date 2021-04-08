//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AsyncPoco;
using Microsoft.FSharp.Core;

namespace StaTypPocoQueries.AsyncPoco.CsTests {
    public class SqlServerRunner : IRunner {
        private string DbName => "testingdb";
        
        //resharper shadow copy workaround thanks to mcdon
        // http://stackoverflow.com/questions/16231084/resharper-runs-unittest-from-different-location
        private static string DllsPath {get; } =
            Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
        
        private static string[] SqlCreateTestTable = {
            @"create table SomeEntity (
                id integer IDENTITY(1,1) not null,
                anInt int not null, 
                aString nvarchar(50) null, 
                nullableInt int null,
                actualName varchar(50) null,

	            CONSTRAINT PK_SomeEntity PRIMARY KEY CLUSTERED (id))",
            @"create table SpecialEntity (
                id integer IDENTITY(1,1) not null,
                [table] int not null, 
                [create] nvarchar(50) null, 
                [null] int null,

	            CONSTRAINT PK_SpecialEntity PRIMARY KEY CLUSTERED (id))"};
        
        public async Task Run(Action<string> logger, Func<Database,Task> testBody) {
            var connStr =
                Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTIONSTRING")
                ??
                @"Data Source=localhost\sqlexpress;Initial Catalog=master;Trusted_Connection=True;Connection Timeout=2;TrustServerCertificate=True";

            logger($"Using connection string {connStr}");

            using (var sqlConn = Attempt(logger, () => {
                var result = new SqlConnection(connStr);
                result.Open();
                return result;
            })) {
                ExecuteNonQuery(sqlConn, $"IF DB_ID('{DbName}') IS NOT NULL begin drop database {DbName} end");
                ExecuteNonQuery(sqlConn, $"create database {DbName}");
                ExecuteNonQuery(sqlConn, $"use {DbName}");

                Array.ForEach(SqlCreateTestTable, x => ExecuteNonQuery(sqlConn, x));

                using (var asyncPocoDb = new Database(sqlConn)) {
                    await testBody(asyncPocoDb);
                }
            }
        }

        private T Attempt<T>(Action<string> logger, Func<T> action, int sleepMs=500, int maxSleepMs=20000) {
            var until = DateTime.UtcNow.AddMilliseconds(maxSleepMs);

            var attempt = 0;
            while(true) {
                attempt++;

                try {
                    logger($"Attempt {attempt} to connect to sql server...");
                    var result = action();
                    logger($"Attempt {attempt} succeeded");
                    return result;
                } catch(Exception) {
                    logger($"Attempt {attempt} failed");
                    if (DateTime.UtcNow < until) {
                        Thread.Sleep(sleepMs);    
                    } else {
                        throw;
                    }
                }
            }
        }

        private void ExecuteNonQuery(SqlConnection sqlConn, string nonQuery) {
            using (var cmd = sqlConn.CreateCommand()) {
                cmd.CommandText = nonQuery;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
