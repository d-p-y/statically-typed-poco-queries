//Copyright © 2021 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

namespace StaTypPocoQueries.Core

open System
open System.Linq.Expressions
open Microsoft.FSharp.Linq.RuntimeHelpers
open System.Runtime.InteropServices

module List =
    let appendIfSome x lst =
        match x with 
        |Some x -> x::lst
        |_ -> lst

module Translator = 
    type IQuoter =
        abstract member QuoteColumn: columnName:string-> string
 
    type ConjunctionWord =
    |And=1
    |Or=2

    let conjunctionWordAsSql x =
        match x with
        |ConjunctionWord.And -> " AND "
        |ConjunctionWord.Or -> " OR "
        |_ -> failwith "unsupported ConjunctionWord"

    type SqlDialect =
    |SqlServer
    |Sqlite
    |Postgresql
    |MySql
    |Oracle
    with
        member this.Quoter 
            with get() =
                match this with
                |SqlServer -> {new IQuoter with member __.QuoteColumn x = sprintf "[%s]" x}
                |Sqlite -> {new IQuoter with member __.QuoteColumn x = sprintf "`%s`" x}
                |Postgresql -> //https://www.postgresql.org/docs/8.2/static/sql-syntax-lexical.html
                    {new IQuoter with member __.QuoteColumn x = sprintf "\"%s\"" x} 
                |MySql -> //https://dev.mysql.com/doc/refman/5.5/en/keywords.html
                    {new IQuoter with member __.QuoteColumn x = sprintf "`%s`" x} 
                |Oracle -> //https://docs.oracle.com/database/121/SQLRF/sql_elements008.htm#SQLRF51129
                    {new IQuoter with member __.QuoteColumn x = sprintf "\"%s\"" x}

    type SqlParams = obj list

    type LiteralType = {
        IsNull : bool
        SqlProduce : SqlParams->string
        Param : obj option
        Prop : System.Reflection.PropertyInfo option
    }
    with
        member this.maybeMapParam mapper = 
            match this.Param, mapper with
            |Some p, Some mapper -> {this with Param = Some (mapper p)}
            |_ -> this
    
    let nullableType = typedefof<Nullable<_>>

    let literalToSql (v:obj) =
        match v with
        | null | :? DBNull -> {
            LiteralType.IsNull = true
            SqlProduce = (fun _ -> "NULL")
            Param = None 
            Prop = None }
        | _ -> {
            LiteralType.IsNull = false
            SqlProduce = (fun prms -> sprintf "@%i" prms.Length)
            Param = Some v
            Prop = None }

    let extractImmediateValue body =
        let unaryExpr = Expression.Convert(body, typeof<obj>)
        let v = Expression.Lambda<Func<obj>>(unaryExpr).Compile()
        v.Invoke()

    let rec constantOrMemberAccessValue (quote:IQuoter) nameExtractor (body:Expression) =
        match (body.NodeType, body) with
        | ExpressionType.Constant, (:? ConstantExpression as body) -> 
            literalToSql body.Value
        | ExpressionType.Convert, (:? UnaryExpression as body) -> 
            constantOrMemberAccessValue quote nameExtractor body.Operand
        | ExpressionType.MemberAccess, (:? MemberExpression as body) ->
            match body.Expression with
            | :? MemberExpression as me when 
                    body.Member.DeclaringType.IsGenericType && 
                    body.Member.DeclaringType.GetGenericTypeDefinition() = nullableType && 
                    body.Member.Name = "Value" ->

                constantOrMemberAccessValue quote nameExtractor me
            | :? ParameterExpression ->
                match body.Member with
                | :? System.Reflection.PropertyInfo as pi -> 
                    {
                        LiteralType.IsNull = false
                        SqlProduce = (fun _ -> quote.QuoteColumn (nameExtractor body.Member))
                        Param = None
                        Prop = Some (pi)
                    }
                | :? System.Reflection.FieldInfo ->
                    body |> extractImmediateValue |> literalToSql
                |_ -> failwithf "constantOrMemberAccessValue->MemberAccess->ParameterExpression unsupported expression %A" body.Expression
            | x when x.NodeType = ExpressionType.Constant ->
                body |> extractImmediateValue |> literalToSql
            | :? MemberExpression ->
                body |> extractImmediateValue |> literalToSql
            |_ -> failwithf "constantOrMemberAccessValue->MemberAccess unsupported expression %A" body.Expression
        | _ -> failwithf "constantOrMemberAccessValue unsupported nodetype %A" body   

    let leafExpression quote nameExtractor paramValueMap (body:BinaryExpression) sqlOperator curParams =
        let left = constantOrMemberAccessValue quote nameExtractor body.Left 
        let right = constantOrMemberAccessValue quote nameExtractor body.Right
        
        let leftParamMap, rightParamMap =
            match left.Prop, right.Prop with
            |None, None -> //two constants
                None, None
            |Some _, Some _ -> //comparison is expressed between two columns
                None, None
            |Some x, _ ->  None, Some (paramValueMap x)
            |None, Some x -> Some (paramValueMap x), None

        let left = left.maybeMapParam leftParamMap
        let right = right.maybeMapParam rightParamMap
                
        let leftSql = left.SqlProduce curParams
        let curParams = List.appendIfSome left.Param curParams
        
        let rightSql = right.SqlProduce curParams
        let curParams = List.appendIfSome right.Param curParams

        let oper = 
            match right.IsNull, body.NodeType with
            | true, ExpressionType.NotEqual -> " IS NOT "
            | true, ExpressionType.Equal -> " IS "
            | _ -> sprintf " %s " sqlOperator
            
        leftSql + oper + rightSql, curParams

    let boolValueToWhereClause quote nameExtractor (body:MemberExpression) curParams isTrue = 
        let info = constantOrMemberAccessValue quote nameExtractor body
        (info.SqlProduce curParams) + (sprintf " = @%i" curParams.Length), (isTrue:obj)::curParams

    let binExpToSqlOper (body:BinaryExpression) =
        match body.NodeType with
        | ExpressionType.NotEqual -> Some "<>"
        | ExpressionType.Equal -> Some "="
        | ExpressionType.GreaterThan -> Some ">"
        | ExpressionType.GreaterThanOrEqual -> Some ">="
        | ExpressionType.LessThan -> Some "<"
        | ExpressionType.LessThanOrEqual -> Some "<="
        | _ -> None
        
    let junctionToSqlOper (body:BinaryExpression) =
        match body.NodeType with
        | ExpressionType.AndAlso -> Some "AND"
        | ExpressionType.OrElse -> Some "OR"
        | _ -> None
        
    let sqlInBracketsIfNeeded parentJunction junctionSql sql = 
        match parentJunction with
        | None -> sql
        | Some parentJunction when parentJunction = junctionSql -> sql
        | _ -> sprintf "(%s)" sql
    
    type SupportedExpression = 
    |NullableHasValue of bool * MemberExpression
    |EqualsBool of bool * MemberExpression
    |LogicalJunction of sqlJunction : string * BinaryExpression
    |Comparison of sqlComparison : string * BinaryExpression

    let rec asMaybeSupportedExpression (negated:bool) (input:Expression) =
        match input with
        | :? UnaryExpression as expr when expr.NodeType = ExpressionType.Not && expr.Type = typeof<System.Boolean> ->
            asMaybeSupportedExpression (not negated) expr.Operand
            
        | :? MemberExpression as expr when
                expr.NodeType = ExpressionType.MemberAccess && expr.Member.Name = "HasValue" &&
                expr.Member.DeclaringType.IsGenericType && expr.Member.DeclaringType.GetGenericTypeDefinition() = nullableType ->
            
            match expr.Expression with
            | :? MemberExpression as expr -> NullableHasValue(negated,expr) |> Some
            | _ -> None
        | :? MemberExpression as expr when expr.NodeType = ExpressionType.MemberAccess && expr.Type = typeof<System.Boolean> ->
            EqualsBool(negated, expr) |> Some

        | :? BinaryExpression as body -> 
            match junctionToSqlOper body, binExpToSqlOper body with
            | Some junctionOper, None -> LogicalJunction (junctionOper, body) |> Some
            | None, Some cmpOper -> Comparison(cmpOper, body) |> Some
            | _ -> None
        | _ -> None

    let rec comparisonToWhereClauseImpl
            (quote:IQuoter) nameExtractor paramMap
            (expr:Expression) parentJunction curSqlParams = 

        expr
        |> asMaybeSupportedExpression false
        |> function
        | None -> failwithf "unsupported expression %A" expr
        | Some(expr) ->
            expr
            |> function
            | NullableHasValue(negate, expr) ->
                let negate = if negate then "" else "NOT " //HasValue() means "is NOT null"
                let info = constantOrMemberAccessValue quote nameExtractor expr
                (info.SqlProduce curSqlParams) + " IS " + negate + "NULL", List.appendIfSome info.Param curSqlParams
            | EqualsBool(negate, expr) ->
                let boolValue = not negate // "x => someBool" has negate==false
                boolValueToWhereClause quote nameExtractor expr curSqlParams boolValue
            | Comparison(cmpSql, expr) -> 
                leafExpression quote nameExtractor paramMap expr cmpSql curSqlParams
            | LogicalJunction(junctionSql, expr) ->
                let lSql, curSqlParams = comparisonToWhereClauseImpl quote nameExtractor paramMap expr.Left (Some junctionSql) curSqlParams
                let rSql, curSqlParams = comparisonToWhereClauseImpl quote nameExtractor paramMap expr.Right (Some junctionSql) curSqlParams
                
                let sql = 
                    sprintf "%s %s %s" lSql junctionSql rSql
                    |> sqlInBracketsIfNeeded parentJunction junctionSql

                sql, curSqlParams

    let comparisonToWhereClause quote nameExtractor paramMap (expr:Expression<Func<'T,bool>>) parentJunction curSqlParams =
        comparisonToWhereClauseImpl quote nameExtractor paramMap expr.Body parentJunction curSqlParams

module LinqHelpers =
    //thanks Daniel
    //http://stackoverflow.com/questions/9134475/expressionfunct-bool-from-a-f-func
    let conv<'T> (quot:Microsoft.FSharp.Quotations.Expr<'T -> bool>) =
        let linq = quot |> LeafExpressionConverter.QuotationToExpression 
        let call = linq :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'T, bool>>(lambda.Body, lambda.Parameters)

module Hlp =
    let translateOne quoter nameExtractor paramValueExtractor conditions includeWhere =
        let sql, parms = 
            Translator.comparisonToWhereClause 
                quoter nameExtractor paramValueExtractor conditions None List.empty
        let where = if includeWhere then "WHERE " else ""
        new Tuple<_,_>(where + sql, parms |> List.rev |> Array.ofList)

    let translateMultiple includeWhere quoter separator nameExtractor paramValueExtractor conditions =
        let queries, prms =
            conditions
            |> Array.fold
                (fun (queries,prms) condition -> 
                    let query, prms = 
                        Translator.comparisonToWhereClause 
                            quoter nameExtractor paramValueExtractor condition None prms
                    (sprintf "(%s)" query)::queries, prms)
                (List.empty, List.empty)
 
        let query = System.String.Join(Translator.conjunctionWordAsSql separator, queries |> List.rev)        
        let where = if includeWhere then "WHERE " else ""

        where + query, prms |> List.rev |> Array.ofList

    let cneToFun (customNameExtractor:Func<System.Reflection.MemberInfo,string>) = 
        if customNameExtractor = null 
        then (fun (x:System.Reflection.MemberInfo) -> x.Name) 
        else (fun x -> customNameExtractor.Invoke x)
    
    let cpvmToFun (customParameterValueMap:Func<System.Reflection.PropertyInfo,obj,obj>) =
        if customParameterValueMap = null
        then (fun _ x -> x)
        else (fun pi inp -> customParameterValueMap.Invoke(pi,inp) )

module ExpressionToSqlImpl =
    let translateFsSingle<'T>
            quoter
            (conditions:Quotations.Expr<('T -> bool)>)
            includeWhere
            customNameExtractor
            customParameterValueMap =

        let nameExtractor = customNameExtractor |> Option.defaultValue (fun (x:System.Reflection.MemberInfo) -> x.Name)
        let parameterValueMap = customParameterValueMap |> Option.defaultValue (fun _ x -> x)

        Hlp.translateOne quoter 
            nameExtractor
            parameterValueMap
            (LinqHelpers.conv conditions)
            (Option.defaultValue true includeWhere)

    let translateFsMultiple<'T>
            quoter
            separator
            (conditions:Quotations.Expr<('T -> bool)>[])
            includeWhere
            customNameExtractor
            customParameterValueMap =
                
        let nameExtractor = customNameExtractor |> Option.defaultValue (fun (x:System.Reflection.MemberInfo) -> x.Name)        
        let parameterValueMap = customParameterValueMap |> Option.defaultValue (fun _ x -> x)

        let includeWhere = 
            includeWhere
            |> function
            |Some false -> false
            | _ -> true

        let conditions =
            conditions
            |> Array.map LinqHelpers.conv

        Hlp.translateMultiple 
            includeWhere quoter separator nameExtractor parameterValueMap conditions
            
type ExpressionToSql =
    
    ///single Linq expression
    static member Translate<'T>(quoter, conditions:Expression<Func<'T, bool>>, 
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]
                customNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap) =
        
        Hlp.translateOne quoter 
            (Hlp.cneToFun customNameExtractor)
            (Hlp.cpvmToFun customParameterValueMap)
            conditions
            includeWhere 

    ///multiple Linq expressions
    static member Translate<'T>(quoter:Translator.IQuoter, separator:Translator.ConjunctionWord, 
            conditions:Expression<Func<'T, bool>>[],
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]
                customNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap) = 
        
        Hlp.translateMultiple 
            includeWhere 
            quoter 
            separator 
            (Hlp.cneToFun customNameExtractor) 
            (Hlp.cpvmToFun customParameterValueMap)
            conditions 

    ///single F# quotation - this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters
    static member Translate(quoter, conditions, ?includeWhere, ?customNameExtractor) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere customNameExtractor None

    ///single F# quotation - full
    static member Translate(quoter, conditions, includeWhere, customNameExtractor, customParameterValueMap) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere customNameExtractor customParameterValueMap

    ///multiple F# quotation - this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters
    static member Translate(quoter, separator, conditions, ?includeWhere, ?customNameExtractor) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere customNameExtractor None

    ///multiple F# quotations - full
    static member Translate(quoter, separator, conditions, includeWhere, customNameExtractor, customParameterValueMap) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere customNameExtractor customParameterValueMap

    static member AsFsFunc (x:Func<_,_>) = fun y -> x.Invoke(y)
