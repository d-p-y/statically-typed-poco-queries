//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AP = AsyncPoco;
using StaTypPocoQueries.Core;
using Microsoft.FSharp.Quotations;
using Microsoft.FSharp.Core;

namespace StaTypPocoQueries.AsyncPoco {
    public static class AsyncPocoDatabaseExtensions {

        private static Translator.SqlDialect GetDialect(AP.Database db) {
            var type = db.Connection.GetType().FullName;

            if (type.ToLower().Contains("sqliteconnection")) {
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

        private static string ExtractAsyncPocoColumnName(MemberInfo x, Type t) =>
            x.GetCustomAttribute<AP.ColumnAttribute>()?.Name ?? x.Name;

        private static FSharpOption<FSharpFunc<MemberInfo, FSharpFunc<Type,string>>> ExtractAsyncPocoColumnNameFsFunc() {
            return FSharpOption<FSharpFunc<MemberInfo, FSharpFunc<Type,string>>>.Some(
                ExpressionToSql.AsFsFunc3<MemberInfo,Type,string>(
                    ExtractAsyncPocoColumnName));
        }
        private static readonly FSharpOption<FSharpFunc<MemberInfo, FSharpFunc<Type,string>>> ExtractAsyncPocoColumnNameFs = ExtractAsyncPocoColumnNameFsFunc();

        public static Task<int> DeleteAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnName);
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> DeleteAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnNameFs, 
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<bool> ExistsAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, false, ExtractAsyncPocoColumnName);
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<bool> ExistsAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, false, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnName);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<List<T>> FetchAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
 
        public static Task<List<T>> FetchAsync<T>(
                this AP.Database self, Translator.ConjunctionWord wrd, params Expression<Func<T, bool>>[] queries) {
            
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, wrd, queries, true, ExtractAsyncPocoColumnName);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<List<T>> FetchAsync<T>(
                this AP.Database self, Translator.ConjunctionWord wrd, params FSharpExpr<FSharpFunc<T, bool>>[] queries) {
            
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, wrd, queries, true, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> FirstAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnName);
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> FirstAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<T> SingleAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnName);
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> SingleAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<int> UpdateAsync<T>(this AP.Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnName);
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> UpdateAsync<T>(this AP.Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var translated = ExpressionToSql.Translate(GetDialect(self).Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None, FSharpOption<Translator.ItemInCollectionImpl>.None);
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
    }
}
