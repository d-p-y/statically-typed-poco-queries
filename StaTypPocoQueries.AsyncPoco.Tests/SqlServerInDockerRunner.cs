//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncPoco;
using DisposableSoftwareContainer;
using Microsoft.FSharp.Core;

namespace StaTypPocoQueries.AsyncPoco.Tests {
    public class SqlServerInDockerRunner : IRunner {
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

	            CONSTRAINT PK_SomeEntity PRIMARY KEY CLUSTERED (id))",
            @"create table SpecialEntity (
                id integer IDENTITY(1,1) not null,
                [table] int not null, 
                [create] nvarchar(50) null, 
                [null] int null,

	            CONSTRAINT PK_SpecialEntity PRIMARY KEY CLUSTERED (id))"};
        
        public async Task Run(Action<string> logger, Func<Database,Task> testBody) {
            var fsLog = FSharpOption<Action<string>>.Some(logger);
            
            var dockerFileFolder = Path.Combine(DllsPath, @"..\..\..\SqlServerInDocker");
            const string saPasswd = "somePASSWD12345"; //present in build.args file
            const int serverPort = 1433;

            //Sql Server images are huge at this moment, not cleaning same image over and over saves 5s per test
            var cl = FSharpOption<Docker.CleanMode>.Some(Docker.CleanMode.ContainerOnly);

            using (var sqlServerCont = new Docker.AutostartedDockerContainer(dockerFileFolder,logger:fsLog,cleanMode:cl)) {
                var connStr = $"Data Source={sqlServerCont.IpAddress},{serverPort};Initial Catalog=master;Trusted_Connection=False;User=sa;Password={saPasswd};Connection Timeout=2";
                logger($"Using connection string {connStr}");

                var sqlConn = Attempt(logger, () => {
                    var result = new SqlConnection(connStr);
                    result.Open();
                    return result;
                });
                
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
