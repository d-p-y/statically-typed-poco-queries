//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.CsTests {
    
    /// <summary>
    /// Column names definitely require quoting
    /// 
    /// Persisted objects are compared by id (comparison "by" reference). 
    /// Non persisted are value objects and thus its equalities of its properties is checked.
    /// </summary>
    [TableName("SpecialEntity")]
	[PrimaryKey("id")]
	[ExplicitColumns] 
    class SpecialEntity {
        [Column] public int Id { get; set; }
        [Column] public int Table { get; set; } 
        [Column] public string Create { get; set; }
        [Column] public int? Null { get; set; }

        public override int GetHashCode() {
            if (Id != 0) {
                return Id.GetHashCode();
            }
            
            return Table.GetHashCode() + 
                (Create?.GetHashCode() ?? 0) + 
                (Null?.GetHashCode() ?? 0); 
        }

        public override bool Equals(object rawOther) {
            if (!(rawOther is SpecialEntity)) {
                return false;
            }
            var oth = (SpecialEntity)rawOther;

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
                oth.Create == Create && 
                oth.Table == Table && 
                oth.Null == Null;
        }
    }
}
