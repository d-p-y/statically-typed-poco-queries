//Copyright © 2021 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    public class AsyncPocoDpyWrapperArrayTests {
        private readonly Action<string> _logger;

        public AsyncPocoDpyWrapperArrayTests(ITestOutputHelper hlp) =>
            _logger = hlp.WriteLine;
        
        public static IEnumerable<object[]> DbProviders() => new[] {
            new object[] {new PostgreSqlRunner(PostgreSqlPreparer)},
            new object[] {new SqlServerRunner(SqlServerPreparer)}
        };
    
        private static void PostgreSqlPreparer(AsyncPoco.Database db) =>
            db.ShouldExpandSqlParametersToLists = false;
        
        private static void SqlServerPreparer(AsyncPoco.Database db) {
            db.ShouldExpandSqlParametersToLists = false;

            db.PrepareParameterValue = (paramKind, paramValue) => {
                switch (paramKind) {
                    case AsyncPoco.Database.ParameterKind.Collection:
                        if (paramValue is int[] arrayOfInt) {
                            var dt = new System.Data.DataTable();
                            dt.Columns.Add(new System.Data.DataColumn("V", typeof(int)));
                            foreach (var x in arrayOfInt) {
                                dt.Rows.Add(x);
                            }

                            return new System.Data.SqlClient.SqlParameter {
                                //ParameterName is supplied later by AsyncPoco
                                SqlDbType = System.Data.SqlDbType.Structured,
                                TypeName = "ArrayOfInt",
                                Value = dt
                            };
                        }
                        if (paramValue is string[] arrayOfString) {
                            var dt = new System.Data.DataTable();
                            dt.Columns.Add(new System.Data.DataColumn("V", typeof(string)));
                            foreach (var x in arrayOfString) {
                                dt.Rows.Add(x);
                            }

                            return new System.Data.SqlClient.SqlParameter {
                                //ParameterName is supplied later by AsyncPoco
                                SqlDbType = System.Data.SqlDbType.Structured,
                                TypeName = "ArrayOfString",
                                Value = dt
                            };
                        }
                        //if you need more types (such as ArrayOfFloat): create type in T-SQL and add support here
                        throw new Exception($"unsupported collection type {paramValue.GetType().FullName}");

                    default: return paramValue;
                }
            };
        }

        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void CollectionAsSqlParameter(IRunner runner) {
            var inp1 = new SomeEntity {
                AnInt = 5,
                AString = "foo1"
            };
            var inp2 = new SomeEntity {
                AnInt = 6,
                AString = "foo2"
            };
            var inp3 = new SomeEntity {
                AnInt = 7,
                AString = "foo3"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp1);
                await db.InsertAsync(inp2);
                await db.InsertAsync(inp3);

                {
                    var neededInts = new[] { 6, 7 };
                    var outp = await db.FetchAsync<SomeEntity>(x => neededInts.Contains(x.AnInt));
                    Assert.Equal(new List<SomeEntity> { inp2, inp3 }, outp.OrderBy(x => x.AnInt).ToList());
                }

                {
                    var neededStrs = new[] { "foo2", "foo1" };
                    var outp = await db.FetchAsync<SomeEntity>(x => neededStrs.Contains(x.AString));
                    Assert.Equal(new List<SomeEntity> { inp1, inp2 }, outp.OrderBy(x => x.AnInt).ToList());
                }
            });
        }
    }
}
