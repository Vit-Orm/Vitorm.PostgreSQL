using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Vit.Linq;
using Vit.Linq.ExpressionNodes.ComponentModel;

using Vitorm.Entity;
using Vitorm.Sql.SqlTranslate;
using Vitorm.StreamQuery;

namespace Vitorm.PostgreSQL
{
    public class SqlTranslateService : Vitorm.Sql.SqlTranslate.SqlTranslateService
    {
        public static readonly SqlTranslateService Instance = new SqlTranslateService();

        protected override BaseQueryTranslateService queryTranslateService { get; }
        protected override BaseQueryTranslateService executeUpdateTranslateService => throw new NotImplementedException();
        protected override BaseQueryTranslateService executeDeleteTranslateService { get; }

        public SqlTranslateService()
        {
            queryTranslateService = new QueryTranslateService(this);

            executeDeleteTranslateService = new Vitorm.PostgreSQL.TranslateService.ExecuteDeleteTranslateService(this);
        }
        /// <summary>
        ///     Generates the delimited SQL representation of an identifier (column name, table name, etc.).
        /// </summary>
        /// <param name="identifier">The identifier to delimit.</param>
        /// <returns>
        ///     The generated string.
        /// </returns>
        public override string DelimitIdentifier(string identifier) => $"\"{EscapeIdentifier(identifier)}\""; // Interpolation okay; strings

        /// <summary>
        ///     Generates the escaped SQL representation of an identifier (column name, table name, etc.).
        /// </summary>
        /// <param name="identifier">The identifier to be escaped.</param>
        /// <returns>
        ///     The generated string.
        /// </returns>
        public override string EscapeIdentifier(string identifier) => identifier?.Replace("\"", "\\\"");


        #region EvalExpression
        /// <summary>
        /// read where or value or on
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <param name="node"></param>
        public override string EvalExpression(QueryTranslateArgument arg, ExpressionNode node)
        {
            switch (node.nodeType)
            {
                case NodeType.MethodCall:
                    {
                        ExpressionNode_MethodCall methodCall = node;
                        switch (methodCall.methodName)
                        {
                            // ##1 ToString
                            case nameof(object.ToString):
                                {
                                    return $"cast({EvalExpression(arg, methodCall.@object)} as Nullable(String) )";
                                }

                            #region ##2 String method:  StartsWith EndsWith Contains
                            case nameof(string.StartsWith): // String.StartsWith
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat({EvalExpression(arg, value)},'%')";
                                }
                            case nameof(string.EndsWith): // String.EndsWith
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat('%',{EvalExpression(arg, value)})";
                                }
                            case nameof(string.Contains) when methodCall.methodCall_typeName == "String": // String.Contains
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat('%',{EvalExpression(arg, value)},'%')";
                                }
                                #endregion
                        }
                        break;
                    }

                #region Read Value
                case NodeType.Convert:
                    {
                        // cast( 4.1 as signed)

                        ExpressionNode_Convert convert = node;

                        Type targetType = convert.valueType?.ToType();

                        if (targetType == typeof(object)) return EvalExpression(arg, convert.body);

                        // Nullable
                        if (targetType.IsGenericType) targetType = targetType.GetGenericArguments()[0];

                        string targetDbType = GetColumnDbType(targetType);

                        var sourceType = convert.body.Member_GetType();
                        if (sourceType != null)
                        {
                            if (sourceType.IsGenericType) sourceType = sourceType.GetGenericArguments()[0];

                            if (targetDbType == GetColumnDbType(sourceType)) return EvalExpression(arg, convert.body);
                        }

                        if (targetType == typeof(string))
                        {
                            return $"cast({EvalExpression(arg, convert.body)} as Nullable(String))";
                        }

                        return $"cast({EvalExpression(arg, convert.body)} as {targetDbType})";
                    }
                case nameof(ExpressionType.Add):
                    {
                        ExpressionNode_Binary binary = node;

                        // ##1 String Add
                        if (node.valueType?.ToType() == typeof(string))
                        {
                            // select ifNull( cast( (userFatherId) as Nullable(String) ) , '' )  from `User` 

                            return $"CONCAT( {BuildSqlSentence(binary.left)} , {BuildSqlSentence(binary.right)} )";

                            string BuildSqlSentence(ExpressionNode node)
                            {
                                if (node.nodeType == NodeType.Constant)
                                {
                                    ExpressionNode_Constant constant = node;
                                    if (constant.value == null) return "''";
                                    else return $"cast( ({EvalExpression(arg, node)}) as String )";
                                }
                                else
                                    return $"ifNull( cast( ({EvalExpression(arg, node)}) as Nullable(String) ) , '')";
                            }
                        }

                        // ##2 Numeric Add
                        return $"{EvalExpression(arg, binary.left)} + {EvalExpression(arg, binary.right)}";
                    }
                case nameof(ExpressionType.Coalesce):
                    {
                        ExpressionNode_Binary binary = node;
                        return $"COALESCE({EvalExpression(arg, binary.left)},{EvalExpression(arg, binary.right)})";
                    }
                case nameof(ExpressionType.Conditional):
                    {
                        // IF(`t0`.`fatherId` is not null,true, false)
                        ExpressionNode_Conditional conditional = node;
                        return $"IF({EvalExpression(arg, conditional.Conditional_GetTest())},{EvalExpression(arg, conditional.Conditional_GetIfTrue())},{EvalExpression(arg, conditional.Conditional_GetIfFalse())})";
                    }
                    #endregion

            }

            return base.EvalExpression(arg, node);
        }
        #endregion



        #region PrepareCreate
        public override string PrepareTryCreateTable(IEntityDescriptor entityDescriptor)
        {
            // https://www.postgresql.org/docs/current/sql-createtable.html
            /* //sql
CREATE TABLE IF NOT exists "User" (
    "id" int4 NOT NULL PRIMARY key ,
    "name" varchar(1000)  NULL,
);
              */
            List<string> sqlFields = new();

            // #1 columns
            entityDescriptor.allColumns?.ForEach(column => sqlFields.Add(GetColumnSql(column)));

            return $@"
CREATE TABLE IF NOT EXISTS {DelimitTableName(entityDescriptor)} (
  {string.Join(",\r\n  ", sqlFields)}
)
;";

            string GetColumnSql(IColumnDescriptor column)
            {
                var columnDbType = column.columnDbType ?? GetColumnDbType(column);
                var defaultValue = column.isNullable ? "default null" : "";
                if (column.isIdentity)
                {
                    throw new NotSupportedException("identity is not supported yet.");
                }

                /*
                  name  type    nullable        defaultValue    primaryKey
                  id    int     not null/null   default null    primary key

                 */

                return $"  {DelimitIdentifier(column.columnName)}  {columnDbType}  {(column.isNullable ? "null" : "not null")}  {defaultValue}  {(column.isKey ? "primary key" : "")}";
            }
        }

        // https://hasura.io/learn/database/postgresql/core-concepts/3-postgresql-data-types-columns/
        protected readonly static Dictionary<Type, string> columnDbTypeMap = new()
        {
            [typeof(DateTime)] = "timestamp",
            [typeof(string)] = "varchar(1000)",

            [typeof(float)] = "real",
            [typeof(double)] = "double",
            [typeof(decimal)] = "numeric",

            [typeof(Int64)] = "int8",
            [typeof(Int32)] = "int4",
            [typeof(Int16)] = "int2",

            //[typeof(UInt64)] = "UInt64",
            //[typeof(UInt32)] = "UInt32",
            //[typeof(UInt16)] = "UInt16",
            //[typeof(byte)] = "UInt8",

            [typeof(bool)] = "boolean",

            //[typeof(Guid)] = "UUID",
        };

        protected override string GetColumnDbType(IColumnDescriptor column)
        {
            Type type = column.type;

            if (column.columnLength.HasValue && type == typeof(string))
            {
                // varchar(1000)
                return $"varchar({(column.columnLength.Value)})";
            }

            return GetColumnDbType(type);
        }
        protected override string GetColumnDbType(Type type)
        {
            var underlyingType = TypeUtil.GetUnderlyingType(type);

            if (columnDbTypeMap.TryGetValue(underlyingType, out var dbType)) return dbType;

            if (underlyingType.Name.ToLower().Contains("int")) return "integer";

            throw new NotSupportedException("unsupported column type:" + underlyingType.Name);
        }

        #endregion

        public override string PrepareTryDropTable(IEntityDescriptor entityDescriptor)
        {
            // drop table if exists `User`;
            return $@"drop table if exists {DelimitTableName(entityDescriptor)};";
        }
        public override string PrepareTruncate(IEntityDescriptor entityDescriptor) => throw new NotSupportedException();

        public override string PrepareExecuteUpdate(QueryTranslateArgument arg, CombinedStream combinedStream) => throw new NotImplementedException();


        public override string PrepareDelete(SqlTranslateArgument arg)
        {
            // ALTER TABLE `User` DELETE WHERE `id`=2;
            var entityDescriptor = arg.entityDescriptor;

            // #2 build sql
            string sql = $@"ALTER TABLE {DelimitTableName(entityDescriptor)} DELETE where {DelimitIdentifier(entityDescriptor.keyName)}={GenerateParameterName(entityDescriptor.keyName)} ; ";

            return sql;
        }

        public override string PrepareDeleteByKeys<Key>(SqlTranslateArgument arg, IEnumerable<Key> keys)
        {
            //  ALTER TABLE `User` DELETE WHERE  id in ( 7 ) ;

            var entityDescriptor = arg.entityDescriptor;

            StringBuilder sql = new StringBuilder();

            sql.Append("ALTER TABLE ").Append(DelimitTableName(entityDescriptor)).Append(" DELETE where ").Append(DelimitIdentifier(entityDescriptor.keyName)).Append(" in (");

            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    sql.Append(GenerateParameterName(arg.AddParamAndGetName(key))).Append(",");
                }
                sql.Length--;
                sql.Append(");");
            }
            else
            {
                sql.Append("null);");
            }
            return sql.ToString();
        }


        public override (string sql, Func<object, Dictionary<string, object>> GetSqlParams) PrepareUpdate(SqlTranslateArgument arg) => throw new NotImplementedException();


    }
}
