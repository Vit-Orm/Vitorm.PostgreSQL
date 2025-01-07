using System.Linq;

namespace Vitorm.PostgreSQL.TranslateService
{
    public class QueryTranslateService : Vitorm.Sql.SqlTranslate.QueryTranslateService
    {
        public QueryTranslateService(SqlTranslateService sqlTranslator) : base(sqlTranslator)
        {
        }

        public override string BuildQuery(QueryTranslateArgument arg, CombinedStream stream)
        {

            string sql = "";

            // #0  select
            sql += ReadSelect(arg, stream);


            #region #1 from
            sql += "\r\n from " + ReadInnerTable(arg, stream.source);
            #endregion

            #region #2 join
            if (stream.joins != null)
            {
                sql += ReadJoin(arg, stream);
            }
            #endregion

            // #3 where 1=1
            if (stream.where != null)
            {
                var where = sqlTranslator.EvalExpression(arg, stream.where);
                if (!string.IsNullOrWhiteSpace(where)) sql += "\r\n where " + where;
            }

            #region #4 group by
            if (stream.groupByFields != null)
            {
                sql += "\r\n group by " + ReadGroupBy(arg, stream);
            }
            #endregion

            #region #5 having
            if (stream.having != null)
            {
                var where = sqlTranslator.EvalExpression(arg, stream.having);
                if (!string.IsNullOrWhiteSpace(where)) sql += "\r\n having " + where;
            }
            #endregion


            // #6 OrderBy
            if (stream.orders?.Any() == true)
            {
                sql += "\r\n order by " + ReadOrderBy(arg, stream);
            }

            // #7 Range,  LIMIT 4 OFFSET 3
            if (stream.take != null || stream.skip != null)
            {
                string sqlRange = (stream.take == null ? "" : (" LIMIT " + stream.take)) + (stream.skip == null ? "" : (" OFFSET " + stream.skip));
                sql += "\r\n " + sqlRange;
            }

            return sql;
        }
    }
}
