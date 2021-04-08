﻿//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using AsyncPoco;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    
    /// <summary>
    /// Column names definitely require quoting
    /// 
    /// Persisted objects are compared by id (comparison "by" reference). 
    /// Non persisted are value objects and thus its equalities of its properties is checked.
    /// </summary>
    [TableName("specialentity")] //lowercase for postgresql compatibility
    [PrimaryKey("id")]
	[ExplicitColumns] 
    class SpecialEntity {
        [Column("id")] public int Id { get; set; }
        [Column("table")] public int Table { get; set; } 
        [Column("create")] public string Create { get; set; }
        [Column("null")] public int? Null { get; set; }

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
