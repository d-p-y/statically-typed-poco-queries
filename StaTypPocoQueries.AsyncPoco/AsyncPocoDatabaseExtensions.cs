using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AsyncPoco;
using StaTypPocoQueries.Core;

namespace StaTypPocoQueries.AsyncPoco {
    public static class AsyncPocoDatabaseExtensions {

        public static Task<int> DeleteAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query);
            return self.DeleteAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<bool> ExistsAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query, false);
            return self.ExistsAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<List<T>> FetchAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query);
            return self.FetchAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<T> FirstAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query);
            return self.FirstAsync<T>(translated.Item1, translated.Item2);
        }

        public static Task<T> SingleAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query);
            return self.SingleAsync<T>(translated.Item1, translated.Item2);
        }
        
        public static Task<int> UpdateAsync<T>(this Database self, Expression<Func<T, bool>> query) {
            var translated = ExpressionToSql.Translate(query);
            return self.UpdateAsync<int>(translated.Item1, translated.Item2);
        }
    }
}
