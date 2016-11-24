﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using AsyncPoco;
using DisposableSoftwareContainer;
using Microsoft.FSharp.Core;
using Xunit.Abstractions;

namespace StaTypPocoQueries.AsyncPoco.Tests {
    public class AsyncPocoWrapperTest {
        private readonly Action<string> _logger;

        public AsyncPocoWrapperTest(ITestOutputHelper hlp) {
            _logger = hlp.WriteLine;
        }

        // ReSharper disable once UnusedMethodReturnValue.Local: it is used in MemberData
        private static object[] DbProviders() {
            return new object[] {
                new object[] {new SqlServerInDockerRunner()},
                new object[] {new SqliteRunner()}
            };
        }

        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void FetchNothingWorksTest(IRunner runner) {
            await runner.Run(_logger, async db => {
                var outp = await db.FetchAsync<SomeEntity>(x => x.AString == "foo");
                Assert.Equal(new List<SomeEntity>(), outp);
            });
        }
        
        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void SimpleInsertTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };
            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
                Assert.Equal(1, await db.ExecuteScalarAsync<int>("select count(*) from SomeEntity"));
                Assert.NotEqual(0, inp.Id);
            });
        }

        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void SimpleSingleTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
        
                var outp = await db.SingleAsync<SomeEntity>(x => x.AString == "foo");
                Assert.Equal(inp, outp);
            });
        }
        
        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void IsNullSingleTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
        
                var outp = await db.SingleAsync<SomeEntity>(x => x.NullableInt == null);
                Assert.Equal(inp, outp);
            });
        }

        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void SimpleFetchTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
            
                var outp = await db.FetchAsync<SomeEntity>(x => x.AString == "foo");
                Assert.Equal(new List<SomeEntity> {inp}, outp);
            });
        }
        
        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void SimpleExistsTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
            
                Assert.True(await db.ExistsAsync<SomeEntity>(x => x.AString == "foo"));
            });
        }
        
        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void SimpleDeleteTest(IRunner runner) {
            var inp1 = new SomeEntity {
                AnInt = 5,
                AString = "foo1"
            };

            var inp2 = new SomeEntity {
                AnInt = 5,
                AString = "foo2"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp1);
                await db.InsertAsync(inp2);

                var outp = await db.DeleteAsync<SomeEntity>(x => x.AString == "foo1");
                Assert.Equal(1, outp);
                Assert.Equal(inp2, await db.SingleAsync<SomeEntity>(x => x.AnInt == 5));
            });
        }
    }
}
