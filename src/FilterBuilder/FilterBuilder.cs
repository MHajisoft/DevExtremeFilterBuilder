﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;

namespace FilterBuilder
{
    public class FilterBuilder
    {

        string[] builtInConditionOperators = new[]
        {
            "<",
            ">",
            "<=",
            ">=",
            "<>",
            "=",
            "contains",
            "notcontains",
            "startswith",
            "endswith",
            "between"
        };

        string[] builtInGroupOperators = new[]
        {
            "and",
            "or"
        };

        readonly Dictionary<string, CustomConditionOperator> customOperators = new Dictionary<string, CustomConditionOperator>();

        public Expression<Func<T, bool>> GetExpression<T>(string jsonFilter)
        {

            var json = JsonDocument.Parse(jsonFilter);

            var param = Expression.Parameter(typeof(T));
            var body = GetExpression(param, json.RootElement);

            var lambda = Expression.Lambda<Func<T, bool>>(body, param);

            return lambda;

        }


        public void RegisterOperator(string @operator, Func<string, JsonElement, object> parameterParser, Func<object, object, bool> operatorFunction)
        {
            customOperators.Add(@operator, new CustomConditionOperator()
            {
                Name = @operator,
                ParameterParser = parameterParser,
                Method = operatorFunction
            });
        }

        Expression GetExpression(ParameterExpression @object, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                throw new Exception();

            if (IsANotExpression(el))
                return Expression.Not(GetExpression(@object, el[1]));

            var @operator = el[1].GetString();

            if (builtInGroupOperators.Contains(@operator))
            {
                return GetGroupExpression(@object, el);
            }
            else if (builtInConditionOperators.Contains(@operator))
            {
                return GetConditionExpression(@object, el);
            }
            else if (customOperators.ContainsKey(@operator))
            {
                return GetConditionExpression(@object, el);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "Unknown operator");
        }


        bool IsANotExpression(JsonElement el)
            => el.GetArrayLength() == 2 && el[0].GetString() == "!";


        Expression GetConditionExpression(ParameterExpression @object, JsonElement el)
        {
            var propertyOrFieldName = el[0].GetString();
            string @operator = el[1].GetString();
            object parameter = GetParameter(propertyOrFieldName, @operator, el[2]);

            return GetConditionExpression(@object, @operator, propertyOrFieldName, parameter);
        }

        object GetParameter(string propertyOrFieldName, string @operator, JsonElement el)
        {

            if(@operator == "between")
            {
                return new[] { el[0].GetDouble(), el[1].GetDouble() };
            }
            else if (builtInConditionOperators.Contains(@operator))
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.String:      return el.GetString();
                    case JsonValueKind.Number:      return el.GetDouble();
                    case JsonValueKind.True:        return true;
                    case JsonValueKind.False:       return false;
                    case JsonValueKind.Null:        return null;
                    default:
                        throw new NotImplementedException();
                }
            }

            else if (customOperators.ContainsKey(@operator))
                return customOperators[@operator].ParameterParser(propertyOrFieldName, el);

            else
                throw new ArgumentOutOfRangeException("Unknown operator");

        }


        Expression GetConditionExpression(ParameterExpression @object, string @operator, string propertyOrFieldName, object parameter)
        {
            var propertyExpression = Expression.PropertyOrField(@object, propertyOrFieldName);

            if(@operator == "between")
            {
                var lowerBound = ((double[])parameter)[0];
                var upperBound = ((double[])parameter)[1];
                var lowerBoundExpression = Expression.Convert(Expression.Constant(lowerBound), propertyExpression.Type);
                var upperBoundExpression = Expression.Convert(Expression.Constant(upperBound), propertyExpression.Type);
                return Expression.And(
                    Expression.GreaterThan(propertyExpression, lowerBoundExpression),
                    Expression.LessThan(propertyExpression, upperBoundExpression)
                    );
            }
            else if (builtInConditionOperators.Contains(@operator))
            {
                // We need to make sure it has the correct type, so convert it.
                // This would only work if there is a type coercion available (i.e. can't do string to int)
                var parameterExpression = Expression.Convert(Expression.Constant(parameter), propertyExpression.Type);

                if (@operator == "<>")
                    return Expression.MakeBinary(ExpressionType.NotEqual, propertyExpression, parameterExpression);

                else if (@operator == "=")
                    return Expression.MakeBinary(ExpressionType.Equal, propertyExpression, parameterExpression);

                else if (@operator == "<")
                    return Expression.MakeBinary(ExpressionType.LessThan, propertyExpression, parameterExpression);

                else if (@operator == "<=")
                    return Expression.MakeBinary(ExpressionType.LessThanOrEqual, propertyExpression, parameterExpression);

                else if (@operator == ">")
                    return Expression.MakeBinary(ExpressionType.GreaterThan, propertyExpression, parameterExpression);

                else if (@operator == ">=")
                    return Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, propertyExpression, parameterExpression);

                else if (@operator == "contains")
                {
                    var stringContainsMethodInfo = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                    return Expression.Call(propertyExpression, stringContainsMethodInfo, parameterExpression);
                }

                else if (@operator == "notcontains")
                {
                    var stringContainsMethodInfo = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                    var containsExpression = Expression.Call(propertyExpression, stringContainsMethodInfo, parameterExpression);
                    return Expression.Not(containsExpression);
                }

                else if (@operator == "startswith")
                {
                    var stringContainsMethodInfo = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                    return Expression.Call(propertyExpression, stringContainsMethodInfo, parameterExpression);
                }

                else if (@operator == "endswith")
                {
                    var stringContainsMethodInfo = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
                    return Expression.Call(propertyExpression, stringContainsMethodInfo, parameterExpression);
                }

                else
                    throw new NotImplementedException();
            }
            else if (customOperators.ContainsKey(@operator))
            {
                Expression<Func<object, object, bool>> methodExpression = (propertyValue, parameterValue) => customOperators[@operator].Method(propertyValue, parameterValue);
                var parameterExpression = Expression.Convert(Expression.Constant(parameter), typeof(object));
                var propertyExpressionAsObject = Expression.Convert(propertyExpression, typeof(object));

                return Expression.Invoke(methodExpression, propertyExpressionAsObject, parameterExpression);
            }

            else
                throw new NotImplementedException();

        }

        Expression GetGroupExpression(ParameterExpression @object, JsonElement el, int index = 0)
        {

            if (index == el.GetArrayLength()-1)
                return GetExpression(@object, el[index]);

            var conditionA = GetExpression(@object, el[index]);
            var @operator = el[index + 1].GetString();
            var conditionB = GetGroupExpression(@object, el, index+2);

            if (@operator == "and")
                return Expression.MakeBinary(ExpressionType.And, conditionA, conditionB);

            else if (@operator == "or")
                return Expression.MakeBinary(ExpressionType.Or, conditionA, conditionB);

            else
                throw new NotImplementedException();

        }

        class CustomConditionOperator
        {
            public string Name { get; set; }
            public Func<string, JsonElement, object> ParameterParser { get; set; }
            public Func<object, object, bool> Method { get; set; }

        }

    }
}
