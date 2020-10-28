//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.CsTests {
    
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
        [Column("actualName")] public string OfficialName { get; set; }

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
