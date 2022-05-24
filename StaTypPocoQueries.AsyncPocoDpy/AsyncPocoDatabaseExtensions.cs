//Copyright © 2021 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AsyncPoco;
using StaTypPocoQueries.Core;
using Microsoft.FSharp.Quotations;
using Microsoft.FSharp.Core;

namespace StaTypPocoQueries.AsyncPocoDpy {
    public static class AsyncPocoDatabaseExtensions {

        public static Translator.SqlDialect GetDialect(Database db) {
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

        public static string ExtractAsyncPocoColumnName(MemberInfo x, Type t) =>
            x.GetCustomAttribute<AsyncPoco.ColumnAttribute>()?.Name ?? x.Name;
        
        public static readonly FSharpOption<FSharpFunc<MemberInfo, FSharpFunc<Type, string>>> ExtractAsyncPocoColumnNameFs = 
            FSharpOption<FSharpFunc<MemberInfo, FSharpFunc<Type, string>>>.Some(
                ExpressionToSql.AsFsFunc3<MemberInfo, Type, string>(
                    ExtractAsyncPocoColumnName));

        public static Translator.ItemInCollectionImpl BuildItemInCollectionImpl(this Database self, Translator.SqlDialect dialect) {
            if (self.ShouldExpandSqlParametersToLists) {
                return null;
            }
            
            if (dialect.IsSqlServer) {
                return (itmAndColl) => $"{itmAndColl.Item1} in (select V from {itmAndColl.Item2})";
            }

            if (dialect.IsPostgresql) {
                return (itmAndColl) => $"{itmAndColl.Item1} = ANY({itmAndColl.Item2})";
            }

            throw new NotImplementedException("Does not have collection parameters implementation for current sql dialect");
        }

        public static FSharpOption<Translator.ItemInCollectionImpl> BuildItemInCollectionImplFs(this Database self, Translator.SqlDialect dialect) {
            var res = BuildItemInCollectionImpl(self, dialect);
            return res == null
                ? FSharpOption<Translator.ItemInCollectionImpl>.None
                : FSharpOption<Translator.ItemInCollectionImpl>.Some(res);
        }
        
        public static Func<PropertyInfo,object,object> ExtractCustomParameterValueMap() {
            return null; //unsupported in AsyncPoco
        }

        public static readonly FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>> ExtractCustomParameterValueMapFs = 
            FSharpOption<FSharpFunc<PropertyInfo, FSharpFunc<object, object>>>.None; //unsupported in AsyncPoco

        public static Task<int> DeleteAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnName /*won't be used*/, 
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> DeleteAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnNameFs /*won't be used*/,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<bool> ExistsAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, false, ExtractAsyncPocoColumnName, 
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<bool> ExistsAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, false, ExtractAsyncPocoColumnNameFs,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnName, 
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<List<T>> FetchAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(
                this Database self, Translator.ConjunctionWord wrd, params Expression<Func<T, bool>>[] queries) {

            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, wrd, queries, true, ExtractAsyncPocoColumnName,
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(
                this Database self, Translator.ConjunctionWord wrd, params FSharpExpr<FSharpFunc<T, bool>>[] queries) {

            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, wrd, queries, true, ExtractAsyncPocoColumnNameFs,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<T> FirstAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnName, 
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> FirstAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<T> SingleAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnName,
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> SingleAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnNameFs,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<int> UpdateAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnName /*won't be used*/,
                ExtractCustomParameterValueMap(), self.BuildItemInCollectionImpl(dialect));
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> UpdateAsync<T>(this Database self, FSharpExpr<FSharpFunc<T, bool>> query) {
            var dialect = GetDialect(self);
            var translated = ExpressionToSql.Translate(
                dialect.Quoter, query, true, ExtractAsyncPocoColumnNameFs /*won't be used*/,
                ExtractCustomParameterValueMapFs, self.BuildItemInCollectionImplFs(dialect));
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
    }
}
