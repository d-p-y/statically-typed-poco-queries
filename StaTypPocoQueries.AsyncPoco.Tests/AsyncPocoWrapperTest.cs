using System.Collections.Generic;
using System.Data.SQLite;
using Xunit;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.Tests {

    /// <summary>
    /// Persisted objects are compared by id (comparison "by" reference). 
    /// Non persisted are value objects and thus its equalities of its properties is checked.
    /// </summary>
    [TableName("SomeEntity")]
	[PrimaryKey("id")]
	[ExplicitColumns] 
    class SomeEntity {
        [Column] public int Id { get; set; }
        [Column] public int AnInt { get; set; }
        [Column] public string AString { get; set; }
        [Column] public int? NullableInt { get; set; }

        public override int GetHashCode() {
            if (Id != 0) {
                return Id.GetHashCode();
            }
            
            return AnInt.GetHashCode() + 
                (AString?.GetHashCode() ?? 0) + 
                (NullableInt?.GetHashCode() ?? 0); 
        }

        public override bool Equals(object rawOther) {
            if (!(rawOther is SomeEntity)) {
                return false;
            }
            var oth = (SomeEntity)rawOther;

            if (oth.Id != 0 && Id != 0) {
                //both are persisted
                return oth.Id == Id; 
            }

            if (oth.Id != 0 || Id != 0) {
                //only one persisted
                return false;
            }

            //none persisted yet
            return 
                oth.AString == AString && 
                oth.AnInt == AnInt && 
                oth.NullableInt == NullableInt;
        }
    }
    
    public class AsyncPocoWrapperTest {
        private readonly SQLiteConnection _db = new SQLiteConnection("Data Source=:memory:");
        private readonly Database _ap;
        private static string SqlCreateTestTable = @"
            create table SomeEntity (
                id integer primary key autoincrement not null,
                anInt int not null, 
                aString varchar(50) null, 
                nullableInt int null
            )";

        public AsyncPocoWrapperTest() {
            _db.Open();

            using (var cmd = new SQLiteCommand(SqlCreateTestTable, _db)) {
                cmd.ExecuteNonQuery();
            }

            _ap = new Database(_db);
        }
        
        [Fact]
        public async void SimpleInsertTest() {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await _ap.InsertAsync(inp);
            Assert.Equal(1, await _ap.ExecuteScalarAsync<int>("select count(*) from SomeEntity"));
            Assert.NotEqual(0, inp.Id);
        }

        [Fact]
        public async void SimpleSingleTest() {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await _ap.InsertAsync(inp);
        
            var outp = await _ap.SingleAsync<SomeEntity>(x => x.AString == "foo");
            Assert.Equal(inp, outp);
        }
        
        [Fact]
        public async void IsNullSingleTest() {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await _ap.InsertAsync(inp);
        
            var outp = await _ap.SingleAsync<SomeEntity>(x => x.NullableInt == null);
            Assert.Equal(inp, outp);
        }

        [Fact]
        public async void SimpleFetchTest() {
            var inp = new SomeEntity {
                AnInt = 5,
                AString = "foo"
            };

            await _ap.InsertAsync(inp);
            
            var outp = await _ap.FetchAsync<SomeEntity>(x => x.AString == "foo");
            Assert.Equal(new List<SomeEntity> {inp}, outp);
        }
        
        [Fact]
        public async void SimpleDeleteTest() {
            var inp1 = new SomeEntity {
                AnInt = 5,
                AString = "foo1"
            };

            var inp2 = new SomeEntity {
                AnInt = 5,
                AString = "foo2"
            };

            await _ap.InsertAsync(inp1);
            await _ap.InsertAsync(inp2);

            var outp = await _ap.DeleteAsync<SomeEntity>(x => x.AString == "foo1");
            Assert.Equal(1, outp);
            Assert.Equal(inp2, await _ap.SingleAsync<SomeEntity>(x => x.AnInt == 5));
        }
    }
}
