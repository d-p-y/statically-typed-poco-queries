﻿//Copyright © 2021 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    public class AsyncPocoDpyWrapperCommonTests {
        private readonly Action<string> _logger;

        public AsyncPocoDpyWrapperCommonTests(ITestOutputHelper hlp) {
            _logger = hlp.WriteLine;
        }

        public static IEnumerable<object[]> DbProviders() => new[] {
            new object[] {new PostgreSqlRunner()},
            new object[] {new SqliteRunner()},
            new object[] {new SqlServerRunner()}
        };

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
        public async void NeedsQuotingSingleTest(IRunner runner) {
            var inp = new SpecialEntity {
                Table = 5,
                Create = "foo",
                Null = 3
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);
        
                var outp = await db.SingleAsync<SpecialEntity>(x => x.Create == "foo" || x.Null == 3 || x.Table == 5);
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
        public async void SimpleNotExistsTest(IRunner runner) {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo1"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);

                Assert.False(await db.ExistsAsync<SomeEntity>(x => x.AString == "foo"));
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

        [Theory]
        [MemberData(nameof(DbProviders))]
        public async void ColumnNameInAttribute(IRunner runner) {
            var inp = new SomeEntity {
                OfficialName = "foo"
            };

            await runner.Run(_logger, async db => {
                await db.InsertAsync(inp);

                //note actualName iso OfficialName
                var outp1 = (await db.FetchAsync<SomeEntity>("select Id,AnInt,AString,NullableInt,actualName from SomeEntity")).Single();
                Assert.Equal(inp, outp1);

                var outp2 = await db.SingleAsync<SomeEntity>(x => x.OfficialName == "foo");
                Assert.Equal(inp, outp2);
            });
        }
    }
}
