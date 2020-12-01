//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

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

    let literalToSql (v:obj) (curParams:_ list) =
        match v with
        | null | :? DBNull -> true, "NULL", None
        | _ -> false, sprintf "@%i" curParams.Length, Some v 
        //use sqlparameters instead of inline sql due to PetaPoco's static query cache growing

    let rec constantOrMemberAccessValue (quote:IQuoter) nameExtractor (body:Expression) curParams =
        match (body.NodeType, body) with
        | ExpressionType.Constant, (:? ConstantExpression as body) -> 
            literalToSql body.Value curParams
        | ExpressionType.Convert, (:? UnaryExpression as body) -> 
            constantOrMemberAccessValue quote nameExtractor body.Operand curParams
        | ExpressionType.MemberAccess, (:? MemberExpression as body) ->
            match body.Expression with
            | :? ParameterExpression ->
                //maybe name is expected
                match body.Member.GetType().Name with
                |"RtFieldInfo"|"MonoField" -> 
                    let unaryExpr = Expression.Convert(body, typeof<obj>)
                    let v = Expression.Lambda<Func<obj>>(unaryExpr).Compile()
                    literalToSql (v.Invoke ()) curParams
                |"RuntimePropertyInfo"|"MonoProperty" -> false, quote.QuoteColumn (nameExtractor body.Member), None
                |_ as name -> failwithf "unsupported member type name %s" name
            |_ ->
                //constant is expected
                let unaryExpr = Expression.Convert(body, typeof<obj>)
                let v = Expression.Lambda<Func<obj>>(unaryExpr).Compile()
                literalToSql (v.Invoke ()) curParams
        | _ -> failwithf "unsupported nodetype %A" body   

    let leafExpression quote nameExtractor (body:BinaryExpression) sqlOperator curParams =
        let _, lVal, lParam = constantOrMemberAccessValue quote nameExtractor body.Left curParams
        let curParams = List.appendIfSome lParam curParams
        let rNull, rVal, rParam = constantOrMemberAccessValue quote nameExtractor body.Right curParams
        let curParams = List.appendIfSome rParam curParams

        let oper = 
            match (rNull, body.NodeType) with
            | true, ExpressionType.NotEqual -> " IS NOT "
            | true, ExpressionType.Equal -> " IS "
            | false, _ -> sprintf " %s " sqlOperator
            | _ -> failwithf "unsupported nodetype %A" body

        lVal + oper + rVal, curParams
        
    let boolValueToWhereClause quote nameExtractor (body:MemberExpression) curParams isTrue = 
        let _, value, _ = constantOrMemberAccessValue quote nameExtractor body curParams
        value + (sprintf " = @%i" curParams.Length), (isTrue:obj)::curParams
        
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

    let rec comparisonToWhereClause (quote:IQuoter) nameExtractor (body:Expression) parentJunction curSqlParams = 
        match body with
        | :? UnaryExpression as body when body.NodeType = ExpressionType.Not && body.Type = typeof<System.Boolean> ->
            match body.Operand with
            | :? MemberExpression as body -> boolValueToWhereClause quote nameExtractor body curSqlParams false
            | _ -> failwithf "not condition has unexpected type %A" body
        | :? MemberExpression as body when body.NodeType = ExpressionType.MemberAccess && body.Type = typeof<System.Boolean> ->
            boolValueToWhereClause quote nameExtractor body curSqlParams true
        | :? BinaryExpression as body -> 
            match junctionToSqlOper body with
            | Some junctionSql ->
                let consumeExpr (expr:Expression) (curSqlParams:_ list) = 
                    match expr with
                    | :? BinaryExpression as expr ->
                        match junctionToSqlOper expr, binExpToSqlOper expr with
                        | Some _, None -> 
                            let sql, parms = comparisonToWhereClause quote nameExtractor expr (Some junctionSql) curSqlParams
                            let sql = sql |> sqlInBracketsIfNeeded parentJunction junctionSql
                            sql, parms
                        | None, Some sqlOper -> leafExpression quote nameExtractor expr sqlOper curSqlParams
                        | _ -> failwith "expression is not a junction and not a supported leaf"
                    | _ -> comparisonToWhereClause quote nameExtractor expr parentJunction curSqlParams

                let lSql, curSqlParams = consumeExpr body.Left curSqlParams
                let rSql, curSqlParams = consumeExpr body.Right curSqlParams
                
                let sql = sprintf "%s %s %s" lSql junctionSql rSql
                let sql = 
                    match parentJunction with
                    | Some parentJunction when parentJunction <> junctionSql -> sprintf "(%s)" sql
                    | _ -> sql
                sql, curSqlParams

            | None -> 
                match binExpToSqlOper body with
                | Some sqlOper -> leafExpression quote nameExtractor body sqlOper curSqlParams
                | None -> failwithf "unsupported operator %A in leaf" body.NodeType
        | _ -> failwithf "conditions has unexpected type %A" body

module LinqHelpers =
    //thanks Daniel
    //http://stackoverflow.com/questions/9134475/expressionfunct-bool-from-a-f-func
    let conv<'T> (quot:Microsoft.FSharp.Quotations.Expr<'T -> bool>) =
        let linq = quot |> LeafExpressionConverter.QuotationToExpression 
        let call = linq :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'T, bool>>(lambda.Body, lambda.Parameters)

module Hlp =
    let translateOne quoter nameExtractor conditions includeWhere =
        let sql, parms = Translator.comparisonToWhereClause quoter nameExtractor conditions None List.empty
        let where = if includeWhere then "WHERE " else ""
        new Tuple<_,_>(where + sql, parms |> List.rev |> Array.ofList)

    let translateMultiple includeWhere quoter separator nameExtractor conditions toBody =
        let queries, prms =
            conditions
            |> Array.fold
                (fun (queries,prms) condition -> 
                    let query, prms = Translator.comparisonToWhereClause quoter nameExtractor (toBody condition) None prms
                    (sprintf "(%s)" query)::queries, prms)
                (List.empty, List.empty)
 
        let query = System.String.Join(Translator.conjunctionWordAsSql separator, queries |> List.rev)        
        let where = if includeWhere then "WHERE " else ""

        where + query, prms |> List.rev |> Array.ofList

    let cneToFun (customNameExtractor:Func<System.Reflection.MemberInfo,string>) = 
        if customNameExtractor = null 
        then (fun (x:System.Reflection.MemberInfo) -> x.Name) 
        else (fun (x:System.Reflection.MemberInfo) -> (customNameExtractor:Func<System.Reflection.MemberInfo,string>).Invoke x)

    let getBody x = (x:Expression<Func<'T, bool>>).Body

type ExpressionToSql =
    
    ///single Linq expression
    static member Translate<'T>(quoter, conditions:Expression<Func<'T, bool>>, 
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]customNameExtractor) =

        Hlp.translateOne quoter 
            (Hlp.cneToFun customNameExtractor) 
            (Hlp.getBody conditions) 
            includeWhere 

    ///multiple Linq expressions
    static member Translate<'T>(quoter:Translator.IQuoter, separator:Translator.ConjunctionWord, 
            conditions:Expression<Func<'T, bool>>[],
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]customNameExtractor) = 
        
        let nameExtractor = Hlp.cneToFun customNameExtractor        
        Hlp.translateMultiple includeWhere quoter separator nameExtractor conditions Hlp.getBody

    ///single F# quotation
    static member Translate(quoter:Translator.IQuoter, conditions:Quotations.Expr<(_ -> bool)>,
            ?includeWhere, ?customNameExtractor : (System.Reflection.MemberInfo->string)) =
                    
        Hlp.translateOne quoter 
            (customNameExtractor |> Option.defaultValue (fun x -> x.Name))
            (Hlp.getBody (LinqHelpers.conv conditions)) 
            (Option.defaultValue true includeWhere)
    
    ///multiple F# quotations
    static member Translate(quoter:Translator.IQuoter, separator:Translator.ConjunctionWord, 
            conditions:Quotations.Expr<(_ -> bool)>[],
            ?includeWhere, ?customNameExtractor : (System.Reflection.MemberInfo->string)) =
        
        let nameExtractor = customNameExtractor |> Option.defaultValue (fun x -> x.Name)        
        let includeWhere = 
           includeWhere
           |> function
           |Some false -> false
           | _ -> true

        Hlp.translateMultiple includeWhere quoter separator nameExtractor conditions (fun x -> LinqHelpers.conv x |> Hlp.getBody)

    static member AsFsFunc (x:Func<_,_>) = fun y -> x.Invoke(y)
