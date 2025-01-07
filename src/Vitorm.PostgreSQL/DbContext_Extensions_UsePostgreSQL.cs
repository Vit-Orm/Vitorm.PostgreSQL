using Vitorm.PostgreSQL;

namespace Vitorm
{
    public static class DbContext_Extensions_UsePostgreSQL
    {
        public static SqlDbContext UsePostgreSQL(this SqlDbContext dbContext, string connectionString, int? commandTimeout = null)
                => UsePostgreSQL(dbContext, new DbConfig(connectionString: connectionString, commandTimeout: commandTimeout));

        public static SqlDbContext UsePostgreSQL(this SqlDbContext dbContext, DbConfig config)
        {
            dbContext.Init(
                sqlTranslateService: Vitorm.PostgreSQL.SqlTranslateService.Instance,
                dbConnectionProvider: config.ToDbConnectionProvider()
                );

            if (config.commandTimeout.HasValue) dbContext.commandTimeout = config.commandTimeout.Value;

            return dbContext;
        }



    }
}
