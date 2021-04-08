//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using AsyncPoco;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    
    /// <summary>
    /// Persisted objects are compared by id (comparison "by" reference). 
    /// Non persisted are value objects and thus its equalities of its properties is checked.
    /// </summary>
    [TableName("someentity")] //lowercase for postgresql compatibility
	[PrimaryKey("id")]
	[ExplicitColumns] 
    class SomeEntity {
        [Column("id")] public int Id { get; set; }
        [Column("anint")] public int AnInt { get; set; }
        [Column("astring")] public string AString { get; set; }
        [Column("nullableint")] public int? NullableInt { get; set; }
        [Column("actualname")] public string OfficialName { get; set; }

        public override int GetHashCode() {
            if (Id != 0) {
                return Id.GetHashCode();
            }
            
            return AnInt.GetHashCode() + 
                (AString?.GetHashCode() ?? 0) + 
                (NullableInt?.GetHashCode() ?? 0) +
                (OfficialName?.GetHashCode() ?? 0);
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
                oth.NullableInt == NullableInt &&
                oth.OfficialName == OfficialName;
        }
    }
}
