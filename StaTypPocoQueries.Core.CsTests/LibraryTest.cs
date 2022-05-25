//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace StaTypPocoQueries.Core.CsTests {
    class ColumnAttribute : Attribute {
        public string Name { get; }

        public ColumnAttribute(string name) {
            Name = name;
        }
    }

    class SomeEntity {
        public int anInt { get; set; }
        public decimal aDecimal { get; set; }
        public long aLong { get; set; }
        public string aString { get; set; }
        public DateTime aDate { get; set; }
        public bool aBool { get; set; }

        public int? nullableInt { get; set; }
        public decimal? nullableDecimal { get; set; }
        public long? nullableLong { get; set; }
        public DateTime? nullableDate { get; set; }
        public bool? nullableBool { get; set; }

        [Column("thisOneIsTheProperName")]
        public string ActualColumnNameIsInAttribute { get; set; }
    }

    public abstract class ParentEntityWithoutColumnAttr {
        public virtual int Id { get; set; }
    }
    
    public class ChildEntityWithCollumnAttr : ParentEntityWithoutColumnAttr {
        [Column("OverridenEntityPropertyName")]
        public override int Id { get; set; }
    }
    
    public abstract class ParentEntityWithColumnAttr {
        [Column("OverridenEntityPropertyName")]
        public virtual int Id { get; set; }
    }
    
    public class ChildEntityWithoutColumnAttr : ParentEntityWithColumnAttr {
        public override int Id { get; set; }
    }
    
    public class TestQuoter : Translator.IQuoter {
        public static Translator.IQuoter Instance => new TestQuoter();

        public string QuoteColumn(string columnName) {
            return $"<{columnName}>";
        }
    }

    public class LibraryTest {
        private static void AreEqual(string expectedSql, object[] expectedParams, Tuple<string, object[]> fact) {
            Assert.Equal(expectedSql, fact.Item1);
            Assert.Equal(expectedParams, new List<object>(fact.Item2)); //to make it IComparable
        }

        private static void AreEqual(string expectedSql, Tuple<string, object[]> fact) {
            Assert.Equal(expectedSql, fact.Item1);
            Assert.Equal(new object[0], new List<object>(fact.Item2)); //to make it IComparable
        }
        
        [Fact]
        public void TestConstantsOnly() {
            var dt1 = new DateTime(2001, 2, 3, 4, 5, 6);
            var dt2 = new DateTime(2001, 2, 3, 4, 5, 7);
            AreEqual("WHERE @0 = @1", new object[] { dt1, dt2 },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => dt1 == dt2));
        }

        [Fact]
        public void TestEqualsNonNullVariable() {
            // equals not null variable

            var aVar = 5;
            AreEqual("WHERE <anInt> = @0", new object[] {5},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt == aVar));

            var dt = new DateTime(2001, 2, 3, 4, 5, 6);
            AreEqual("WHERE <aDate> = @0", new object[] {dt},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aDate == dt));
        }

        [Fact]
        public void TestGreaterSmaller() {
            var aVar = 5;
            AreEqual("WHERE <anInt> >= @0", new object[] {5},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt >= aVar));

            AreEqual("WHERE <anInt> <= @0", new object[] {5},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt <= aVar));

            AreEqual("WHERE <anInt> > @0", new object[] {5},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt > aVar));

            AreEqual("WHERE <anInt> < @0", new object[] {5},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt < aVar));
        }

        [Fact]
        public void TestEqualsNonNullBoolLiteral() {
            AreEqual("WHERE <aBool> = @0", new object[] {false},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !x.aBool));
            AreEqual("WHERE <aBool> = @0", new object[] {true},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aBool));
        }

        [Fact]
        public void TestEqualsNullableVariable() {
            AreEqual("WHERE <nullableInt> = @0", new object[] {(int?) 0},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableInt == 0));
            AreEqual("WHERE <nullableDecimal> = @0", new object[] {(decimal?) 0M},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDecimal == 0M));
            AreEqual("WHERE <nullableLong> = @0", new object[] {(long?) 123L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableLong == 123L));

            var dt = new DateTime(2001, 2, 3, 4, 5, 6);
            AreEqual("WHERE <nullableDate> = @0", new object[] {dt},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDate == dt));

            AreEqual("WHERE <nullableBool> = @0", new object[] {(bool?) true},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableBool == true));
        }

        [Fact]
        public void TestEqualsNullableIsNull() {
            AreEqual("WHERE <nullableInt> IS NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableInt == null));
            AreEqual("WHERE <nullableDecimal> IS NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDecimal == null));
            AreEqual("WHERE <nullableLong> IS NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableLong == null));
            AreEqual("WHERE <nullableDate> IS NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDate == null));
            AreEqual("WHERE <nullableBool> IS NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableBool == null));
        }

        [Fact]
        public void TestEqualsNullableIsNotNull() {
            AreEqual("WHERE <nullableInt> IS NOT NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableInt != null, true));
            AreEqual("WHERE <nullableDecimal> IS NOT NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDecimal != null, true));
            AreEqual("WHERE <nullableLong> IS NOT NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableLong != null));
            AreEqual("WHERE <nullableDate> IS NOT NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableDate != null));
            AreEqual("WHERE <nullableBool> IS NOT NULL",
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableBool != null));
        }

        [Fact]
        public void TestNullableHasValue() {
            int? prm = 5;

            AreEqual("WHERE <nullableInt> IS NOT NULL", new object[] { },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableInt.HasValue, true));

            AreEqual("WHERE <nullableInt> IS NULL", new object[] { },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !x.nullableInt.HasValue, true));

            AreEqual("WHERE @0 IS NOT NULL", new object[] {prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => prm.HasValue, true));

            AreEqual("WHERE @0 IS NULL", new object[] { prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue, true));

            AreEqual("WHERE @0 IS NOT NULL AND <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => prm.HasValue && x.nullableInt == prm, true));
            
            AreEqual("WHERE @0 IS NULL OR <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.nullableInt == prm, true));
        }

        [Fact]
        public void TestNullableValueIs() {
            int? prm = 5;
            
            AreEqual("WHERE <nullableInt> = @0", new object[] { prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.nullableInt.Value == prm, true));

            AreEqual("WHERE @0 IS NOT NULL AND <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => prm.HasValue && x.nullableInt.Value == prm, true));
            
            AreEqual("WHERE @0 IS NULL OR <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.nullableInt.Value == prm, true));

            AreEqual("WHERE @0 IS NULL OR <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.nullableInt.Value == prm.Value, true));

            AreEqual("WHERE @0 IS NULL OR <nullableInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.nullableInt == prm, true));

            AreEqual("WHERE @0 IS NULL OR <anInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.anInt == prm, true));

            AreEqual("WHERE @0 IS NULL OR <anInt> = @1", new object[] { prm, prm },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => !prm.HasValue || x.anInt == prm.Value, true));

            string str = "1122";

            AreEqual("WHERE @0 IS NULL OR <aString> = @1", new object[] { str, str },
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => str == null || x.aString == str, true));
        }

        [Fact]
        public void TestEqualsSomeLiteral() {
            var dt = new DateTime(2001, 2, 3);
            AreEqual("WHERE <aDate> = @0", new object[] {new DateTime(2001, 2, 3)},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aDate == dt));
            AreEqual("WHERE <anInt> = @0", new object[] {0},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.anInt == 0));
            AreEqual("WHERE <aDecimal> = @0", new object[] {0M},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aDecimal == 0M));
            AreEqual("WHERE <aLong> = @0", new object[] {123L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aLong == 123));
            AreEqual("WHERE <aString> = @0", new object[] {"foo"},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aString == "foo"));
            AreEqual("WHERE <aBool> = @0", new object[] {true},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance, x => x.aBool == true));
        }
        
        [Fact]
        public void TestMultipleConditions() {
            AreEqual("WHERE (<anInt> = @0) OR (<aLong> = @1)", new object[] {1, 3L}, 
                ExpressionToSqlV2.Translate(
                    TestQuoter.Instance, 
                    Translator.ConjunctionWord.Or,
                    new Expression<Func<SomeEntity,bool>>[] {
                        x => x.anInt == 1,
                        x => x.aLong == 3L
                    }));

            AreEqual("WHERE (<anInt> = @0) AND (<aString> = @1)", new object[] {1, "123"}, 
                ExpressionToSqlV2.Translate(
                    TestQuoter.Instance, 
                    Translator.ConjunctionWord.And,
                    new Expression<Func<SomeEntity,bool>>[] {
                        x => x.anInt == 1,
                        x => x.aString == "123"
                    }));

            AreEqual("WHERE (<anInt> = @0 OR <aLong> = @1) AND (<aString> = @2)", new object[] { 1, 3L, "123" },
                ExpressionToSqlV2.Translate(
                    TestQuoter.Instance,
                    Translator.ConjunctionWord.And,
                    new Expression<Func<SomeEntity, bool>>[] {
                        x => x.anInt == 1 || x.aLong == 3L,
                        x => x.aString == "123"
                    }));

            DateTime? dtFrom = null;
            DateTime? dtTo = new DateTime(2001,2,3);

            AreEqual("WHERE (NULL IS NULL OR <aDate> >= NULL) AND (@0 IS NULL OR <aDate> <= @1)", new object[] { dtTo, dtTo },
                ExpressionToSqlV2.Translate(
                    TestQuoter.Instance,
                    Translator.ConjunctionWord.And,
                    new Expression<Func<SomeEntity, bool>>[] {
                        x => !dtFrom.HasValue || x.aDate >= dtFrom,
                        x => !dtTo.HasValue || x.aDate <= dtTo
                    }));
        }

        [Fact]
        public void TestConjunctions() {
            AreEqual("WHERE <aBool> = @0 AND <aBool> = @1", new object[] {true, false},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.aBool && !x.aBool));

            AreEqual("WHERE <anInt> = @0 AND <aString> = @1", new object[] {0, "foo"},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.anInt == 0 && x.aString == "foo"));

            AreEqual("WHERE <anInt> = @0 OR <aString> = @1", new object[] {0, "foo"},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.anInt == 0 || x.aString == "foo"));

            AreEqual("WHERE <anInt> = @0 AND <aString> = @1 AND <aLong> = @2", new object[] {0, "foo", 0L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.anInt == 0 && x.aString == "foo" && x.aLong == 0));

            AreEqual("WHERE <anInt> = @0 OR <aString> = @1 OR <aLong> = @2", new object[] {0, "foo", 0L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.anInt == 0 || x.aString == "foo" || x.aLong == 0));

            AreEqual("WHERE <anInt> = @0 AND (<aString> = @1 OR <aLong> = @2)", new object[] {0, "foo", 0L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => x.anInt == 0 && (x.aString == "foo" || x.aLong == 0)));

            AreEqual("WHERE (<anInt> = @0 AND <aString> = @1) OR <aLong> = @2", new object[] {0, "foo", 0L},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => (x.anInt == 0 && x.aString == "foo") || x.aLong == 0));

            AreEqual("WHERE (<anInt> = @0 AND <aString> = @1) OR (<aLong> = @2 AND <aString> = @3)",
                new object[] {0, "foo", 0L, "foo"},
                ExpressionToSqlV2.Translate<SomeEntity>(TestQuoter.Instance,
                    x => (x.anInt == 0 && x.aString == "foo") || (x.aLong == 0 && x.aString == "foo")));
        }

        [Fact]
        public void TestUsesCustomNameExtractors() {
            AreEqual("WHERE <thisOneIsTheProperName> = @0", 
                new object[] { "foo" },
                ExpressionToSqlV2.Translate<SomeEntity>(
                    TestQuoter.Instance,
                    x => x.ActualColumnNameIsInAttribute == "foo",
                    true, 
                    (x,_) => x.GetCustomAttribute<ColumnAttribute>()?.Name ?? x.Name));
        }
        
        [Fact]
        public void VirtualPropertiesIssue10UsesChildColumnAttribute() {
            AreEqual("WHERE <OverridenEntityPropertyName> = @0", 
                new object[] { 123 },
                ExpressionToSqlV2.Translate<ChildEntityWithCollumnAttr>(
                    TestQuoter.Instance,
                    x => x.Id == 123,
                    true,
                    (mi, t) => t.GetProperty(mi.Name)?.GetCustomAttribute<ColumnAttribute>()?.Name ?? mi.GetCustomAttribute<ColumnAttribute>()?.Name ));
        }
        
        [Fact]
        public void VirtualPropertiesIssue10UsesParentColumnAttribute() {
            AreEqual("WHERE <OverridenEntityPropertyName> = @0", 
                new object[] { 123 },
                ExpressionToSqlV2.Translate<ChildEntityWithoutColumnAttr>(
                    TestQuoter.Instance,
                    x => x.Id == 123,
                    true,
                    (mi, t) => t.GetProperty(mi.Name)?.GetCustomAttribute<ColumnAttribute>()?.Name ?? mi.GetCustomAttribute<ColumnAttribute>()?.Name ));
        }
        
        [Fact]
        public void ChecksMemberAccessOwnerOrigin() {
            //issue #7
            var someInstance = new SomeEntity {aString = "123"};

            AreEqual("WHERE <aString> = @0", 
                new object[] { "123"},
                ExpressionToSqlV2.Translate<SomeEntity>(
                    TestQuoter.Instance, 
                    x => x.aString == someInstance.aString));
            
            AreEqual("WHERE @0 = <aString>", 
                new object[] { "123"},
                ExpressionToSqlV2.Translate<SomeEntity>(
                    TestQuoter.Instance, 
                    x => someInstance.aString == x.aString));
        }
    }
}
