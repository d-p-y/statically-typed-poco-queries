//Copyright © 2016 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AsyncPoco;
using StaTypPocoQueries.Core;

namespace StaTypPocoQueries.AsyncPoco {
    public static class AsyncPocoDatabaseExtensions {

        private static Translator.SqlDialect GetDialect(Database db) {
            var type = db.Connection.GetType().FullName;

            if (type.Contains("SQLiteConnection")) {
                return Translator.SqlDialect.Sqlite;
            }
            if (type.Contains("System.Data.SqlClient")) {
                return Translator.SqlDialect.SqlServer;    
            }
            if (type.Contains("NpgsqlConnection")) {
                //according to https://github.com/npgsql/npgsql/blob/dev/src/Npgsql/NpgsqlConnection.cs
                return Translator.SqlDialect.Postgresql;
            }
            if (type.Contains(".MySql")) {
                //according to https://dev.mysql.com/doc/connector-net/en/connector-net-programming-connecting-open.html
                return Translator.SqlDialect.MySql;
            }
            if (type.Contains(".Oracle")) {
                //according to http://www.oracle.com/webfolder/technetwork/tutorials/obe/db/hol08/dotnet/getstarted-c/getstarted_c_otn.htm
                return Translator.SqlDialect.Oracle;
            }
            
            throw new Exception($"unsupported dialect for db: {type}");
        }
        
        public static Task<int> DeleteAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self), query);
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<bool> ExistsAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, false);
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self), query);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> FirstAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self), query);
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<T> SingleAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self), query);
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> UpdateAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self), query);
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
    }
}
