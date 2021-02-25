using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using PetaPoco;
using PetaPoco.Providers;
using Xunit;

namespace StaTypPocoQueries.Core.PetaPoco.CsTests {

    public class NullableBoolAsString : ValueConverterAttribute {
        public override object ConvertToDb(object value) {
            if (value == null) {
                return "null";
            }

            if (value is bool x) {
                return x ? "true" : "false";
            }
            
            throw new Exception("NullableBoolAsString->ConvertToDb doesn't know how to map input");
        }

        public override object ConvertFromDb(object value) {
            if (value is string x) {
                switch (x) {
                    case "null": return null;
                    case "true": return true;
                    case "false": return false;
                    default: throw new Exception("NullableBoolAsString->ConvertFromDb doesn't know how to map string"); ;
                }
            }
            throw new Exception("NullableBoolAsString->ConvertFromDb doesn't know how to map nonstring");
        }
    }

    [PrimaryKey("Id")]
    [TableName("SomeEntity")]
    class SomeEntity {
        
        [Column]
        public int Id { get; set; }

        [Column]
        [NullableBoolAsString] 
        public bool? ABool { get; set; }

        [Column]
        public bool? OtherBool { get; set; }

        [Column]
        public string AString { get; set; }
        
        public override bool Equals(object rawOther) {
            if (!(rawOther is SomeEntity)) {
                return false;
            }
            var oth = (SomeEntity)rawOther;
            
            return
                oth.Id == Id &&
                oth.ABool == ABool &&
                oth.OtherBool  == OtherBool &&
                oth.AString == AString;
        }

        public override int GetHashCode() =>
            Id+AString.GetHashCode()+(ABool ?? true).GetHashCode() + (OtherBool ?? true).GetHashCode();
    }

    public class ValueConverterTests : IDisposable {
        private readonly IDatabase _pp;
        private readonly SqliteConnection _db;

        public void Dispose() {
            _pp.Dispose();
            _db.Dispose();
        }

        public ValueConverterTests() {
            _db = new SqliteConnection("Data Source=:memory:");
            _db.Open();

            var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                create table SomeEntity (
                    id integer primary key autoincrement not null,
                    aBool varchar(5) not null,
                    otherBool int null, 
                    aString varchar(50) null )";
            cmd.ExecuteNonQuery();

            _pp = new Database(_db);
        }
        
        [Fact]
        public void DocumentPetaPocoNativeConverterUsage() {
            //note: entity saving invokes converter
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi"});
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });
            
            //note: querying doesn't invoke converter automatically
            var lst = _pp.Query<SomeEntity>(
                "where aBool = @0",
                new NullableBoolAsString().ConvertToDb((bool?)true)
            ).ToArray();

            //note: entity retrieving invokes converter
            Assert.Equal(
                new[] { new SomeEntity { ABool = true, AString = "hi", Id = 1} }, 
                lst);
        }

        static object InvokePetaPocoConverterIfAny(PropertyInfo pocoClassProperty, object toConvert) {
            var maybeConverter = pocoClassProperty.GetCustomAttribute<ValueConverterAttribute>(true);
            
            return maybeConverter == null
                ? toConvert
                : maybeConverter.ConvertToDb(toConvert);
        }

        [Fact]
        public void StaTypPocoUsesConvertersForMemberEqualsVariable() {
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi" });
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });
            
            var v = (bool?)true;

            //no need to call converter manually
            var (query,prms) = 
                ExpressionToSql.Translate<SomeEntity>(
                    Translator.SqlDialect.Sqlite.Quoter,
                    x => x.ABool == v,
                    customParameterValueMap:InvokePetaPocoConverterIfAny);

            Assert.Equal("WHERE `ABool` = @0", query);
            Assert.Equal(new []{"true"}, prms);

            var lst = _pp.Query<SomeEntity>(query, prms).ToArray();
            
            Assert.Equal(
                new[] { new SomeEntity { ABool = true, AString = "hi", Id = 1 } },
                lst);
        }

        [Fact]
        public void StaTypPocoUsesConvertersForMemberEqualsIndirectVariable() {
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi" });
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });

            var someInstance = new SomeEntity { ABool = true};
            var v = (bool?)true;

            //no need to call converter manually
            var (query, prms) =
                ExpressionToSql.Translate<SomeEntity>(
                    Translator.SqlDialect.Sqlite.Quoter,
                    x => x.ABool == someInstance.ABool,
                    customParameterValueMap: InvokePetaPocoConverterIfAny);

            Assert.Equal("WHERE `ABool` = @0", query);
            Assert.Equal(new[] { "true" }, prms);

            var lst = _pp.Query<SomeEntity>(query, prms).ToArray();

            Assert.Equal(
                new[] { new SomeEntity { ABool = true, AString = "hi", Id = 1 } },
                lst);
        }

        [Fact]
        public void StaTypPocoUsesConvertersForVariableEqualsMember() {
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi" });
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });

            var v = (bool?)false;

            //no need to call converter manually
            var (query, prms) =
                ExpressionToSql.Translate<SomeEntity>(
                    Translator.SqlDialect.Sqlite.Quoter,
                    x => v == x.ABool,
                    customParameterValueMap: InvokePetaPocoConverterIfAny);

            Assert.Equal("WHERE @0 = `ABool`", query);
            Assert.Equal(new[] { "false" }, prms);

            var lst = _pp.Query<SomeEntity>(query, prms).ToArray();

            Assert.Equal(
                new[] { new SomeEntity { ABool = false, AString = "test", Id = 2 } },
                lst);
        }

        [Fact]
        public void StaTypPocoDoesntUseConvertersForMemberEqualsAnotherMember() {
            //because evaluation of parameters is done by database
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi" });
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });

            var v = (bool?)false;

            //no need to call converter manually
            var (query, prms) =
                ExpressionToSql.Translate<SomeEntity>(
                    Translator.SqlDialect.Sqlite.Quoter,
                    x => x.OtherBool == x.ABool,
                    customParameterValueMap: InvokePetaPocoConverterIfAny);

            Assert.Equal("WHERE `OtherBool` = `ABool`", query);
            Assert.Equal(new object[0], prms);

            var lst = _pp.Query<SomeEntity>(query, prms).ToArray();

            Assert.Equal(
                new object[0],
                lst);
        }

        [Fact]
        public void StaTypPocoDoesntUseConvertersForVariableEqualsVariable() {
            //because variable is not related to any property hence converter may not be inferred
            _pp.Insert(new SomeEntity { ABool = true, AString = "hi" });
            _pp.Insert(new SomeEntity { ABool = false, AString = "test" });

            var v1 = (bool?)false;
            var v2 = (bool?)false;

            //no need to call converter manually
            var (query, prms) =
                ExpressionToSql.Translate<SomeEntity>(
                    Translator.SqlDialect.Sqlite.Quoter,
                    x => v1 == v2,
                    customParameterValueMap: InvokePetaPocoConverterIfAny);

            Assert.Equal("WHERE @0 = @1", query);
            Assert.Equal(new object[] { false, false }, prms);

            var lst = _pp.Query<SomeEntity>(query, prms);

            Assert.Equal(
                new[] {
                    new SomeEntity { ABool = true, AString = "hi", Id = 1 },
                    new SomeEntity { ABool = false, AString = "test", Id = 2 }
                },
                lst.OrderBy(x => x.Id).ToArray());
        }
    }
}
