using Dapper;
using LinqToDB;
using LinqToDB.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using YesSql;
using static LinqToDB.Common.Configuration;
using Sql = LinqToDB.Common.Configuration.Sql;

namespace Lombiq.HelpfulLibraries.LinqToDb
{
    public static class LinqToDbQueryExecutor
    {
        static LinqToDbQueryExecutor()
        {
            // Generate aliases for final projection.
            Sql.GenerateFinalAliases = true;

            // We need to disable null comparison for joins. Otherwise it would generate a syntax like this:
            // JOIN Table2 ON Table1.Key = Table2.Key OR Table1.Key IS NULL AND Table2.Key IS NULL
            Linq.CompareNullsAsValues = false;
        }

        /// <summary>
        /// Use this extension method for running LINQ syntax-based DB queries.
        /// </summary>
        /// <typeparam name="TResult">The type of results to return.</typeparam>
        /// <param name="session">A YesSql session whose connection is used instead of creating a new one.</param>
        /// <param name="query">The <see cref="IQueryable"/> which will be run as a DB query.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> with the input type.</returns>
        public static async Task<IEnumerable<TResult>> LinqQueryAsync<TResult>(
            this ISession session,
            Func<ITableAccessor, IQueryable> query)
        {
            var transaction = await session.DemandAsync();
            var convertedSql = ConvertSqlToDialect(session, transaction, query);

            return await transaction.Connection.QueryAsync<TResult>(convertedSql, transaction: transaction);
        }

        private static string ConvertSqlToDialect(
            ISession session,
            IDbTransaction transaction,
            Func<ITableAccessor, IQueryable> query)
        {
            // Instantiating a LinqToDB connection object as it is required to start building the query. Note that it
            // won't create an actual connection with the database.
            var dataProvider = DataConnection.GetDataProvider(
                GetDatabaseProviderName(session.Store.Dialect.Name),
                transaction.Connection.ConnectionString);

            using var linqToDbConnection = new LinqToDbConnection(
                dataProvider,
                transaction,
                session.Store.Configuration.TablePrefix);
            return query(linqToDbConnection).ToString();
        }

        private static string GetDatabaseProviderName(string dbName) =>
            dbName switch
            {
                // Using explicit string instead of LinqToDB.ProviderName.SqlServer because if the
                // "System.Data.SqlClient" provider will be used it will cause "Could not load type
                // System.Data.SqlClient.SqlCommandBuilder" exception.
                // See: https://github.com/linq2db/linq2db/issues/2191#issuecomment-618450439
                "SqlServer" => "Microsoft.Data.SqlClient",
                "Sqlite" => ProviderName.SQLite,
                "MySql" => ProviderName.MySql,
                "PostgreSql" => ProviderName.PostgreSQL,
                _ => throw new NotSupportedException("The provider name is not supported."),
            };
    }
}
