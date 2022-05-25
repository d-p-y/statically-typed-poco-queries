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

    type SqlAndParams = string * SqlParams

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
    
    ///input: itemSqlFragment; collectionSqlFragment. Returns sql fragment
    type ItemInCollectionImpl = delegate of (string * string) -> string

    let nullableType = typedefof<Nullable<_>>
    let linqEnumerableType = typedefof<System.Linq.Enumerable>

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
                        SqlProduce = (fun _ -> quote.QuoteColumn (nameExtractor body.Member body.Expression.Type))
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
    |ItemInCollection of item:Expression * coll:Expression

    let rec asMaybeSupportedExpression (negated:bool) (input:Expression) =
        match input with
        | :? MethodCallExpression as expr when 
                expr.NodeType = ExpressionType.Call && expr.Method.DeclaringType = linqEnumerableType && expr.Method.Name= "Contains" && 
                expr.Arguments.Count = 2 ->

            let memberOrConstant x =
                match (x:Expression) with
                | :? MemberExpression -> Some x
                | :? ConstantExpression -> Some x
                | _ -> None

            match memberOrConstant expr.Arguments.[1], memberOrConstant expr.Arguments.[0] with 
            | Some itm, Some coll -> ItemInCollection(itm, coll) |> Some
            | _ -> None
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
            (quote:IQuoter) nameExtractor paramMap itemInCollGenerator
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
                let lSql, curSqlParams = 
                    comparisonToWhereClauseImpl quote nameExtractor paramMap itemInCollGenerator expr.Left (Some junctionSql) curSqlParams
                let rSql, curSqlParams = 
                    comparisonToWhereClauseImpl quote nameExtractor paramMap itemInCollGenerator expr.Right (Some junctionSql) curSqlParams
                
                let sql = 
                    sprintf "%s %s %s" lSql junctionSql rSql
                    |> sqlInBracketsIfNeeded parentJunction junctionSql

                sql, curSqlParams
            | ItemInCollection(itm, coll) ->
                let itm = constantOrMemberAccessValue quote nameExtractor itm
                let coll = constantOrMemberAccessValue quote nameExtractor coll

                match itemInCollGenerator with
                | None -> failwithf "item-in-collection syntax not supported because itemInCollGenerator is not provided"
                | Some itemInCollGenerator ->
                    let itmSql = itm.SqlProduce curSqlParams
                    let curSqlParams = curSqlParams |> List.appendIfSome itm.Param

                    let collSql = coll.SqlProduce curSqlParams
                    let curSqlParams = curSqlParams |> List.appendIfSome coll.Param
                                        
                    itemInCollGenerator itmSql collSql, curSqlParams
                
    let comparisonToWhereClause quote nameExtractor paramMap itemInCollGenerator (expr:Expression<Func<'T,bool>>) parentJunction curSqlParams =
        comparisonToWhereClauseImpl quote nameExtractor paramMap itemInCollGenerator expr.Body parentJunction curSqlParams

module LinqHelpers =
    //thanks Daniel
    //http://stackoverflow.com/questions/9134475/expressionfunct-bool-from-a-f-func
    let conv<'T> (quot:Microsoft.FSharp.Quotations.Expr<'T -> bool>) =
        let linq = quot |> LeafExpressionConverter.QuotationToExpression 
        let call = linq :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'T, bool>>(lambda.Body, lambda.Parameters)

module Hlp =
    let translateOne quoter nameExtractor paramValueExtractor itemInCollGenerator conditions includeWhere =
        let sql, parms = 
            Translator.comparisonToWhereClause 
                quoter nameExtractor paramValueExtractor itemInCollGenerator conditions None List.empty
        let where = if includeWhere then "WHERE " else ""
        new Tuple<_,_>(where + sql, parms |> List.rev |> Array.ofList)

    let translateMultiple includeWhere quoter separator nameExtractor paramValueExtractor itemInCollGenerator conditions =
        let queries, prms =
            conditions
            |> Array.fold
                (fun (queries,prms) condition -> 
                    let query, prms = 
                        Translator.comparisonToWhereClause 
                            quoter nameExtractor paramValueExtractor itemInCollGenerator condition None prms
                    (sprintf "(%s)" query)::queries, prms)
                (List.empty, List.empty)
 
        let query = System.String.Join(Translator.conjunctionWordAsSql separator, queries |> List.rev)        
        let where = if includeWhere then "WHERE " else ""

        where + query, prms |> List.rev |> Array.ofList

    let oldCneToFun (customNameExtractor:Func<System.Reflection.MemberInfo,string>) = 
        if customNameExtractor = null 
        then (fun (x:System.Reflection.MemberInfo) (_:System.Type)  -> x.Name) 
        else (fun mi _ -> customNameExtractor.Invoke(mi))
    
    let cneToFun (customNameExtractor:Func<System.Reflection.MemberInfo,System.Type,string>) = 
        if customNameExtractor = null 
        then (fun (x:System.Reflection.MemberInfo) (_:System.Type)  -> x.Name) 
        else (fun mi t -> customNameExtractor.Invoke(mi, t))
    
    let cpvmToFun (customParameterValueMap:Func<System.Reflection.PropertyInfo,obj,obj>) =
        if customParameterValueMap = null
        then (fun _ x -> x)
        else (fun pi inp -> customParameterValueMap.Invoke(pi,inp) )

    let iicgToOptFun (itemInCollGenerator:Translator.ItemInCollectionImpl) =
        if itemInCollGenerator = null
        then None
        else  
            (fun (itm:string) (coll:string) -> itemInCollGenerator.Invoke(itm, coll)) |> Some

    let optionalIicgToOptFun itemInCollGenerator =
        match itemInCollGenerator with
        | Some itemInCollGenerator ->
            (fun (itm:string) (coll:string) -> 
                (itemInCollGenerator:Translator.ItemInCollectionImpl).Invoke(itm, coll)) |> Some
        |_ -> None
        
    let addTypeParam f =
        match f with
        |None -> None
        |Some (f:System.Reflection.MemberInfo->string) ->
            Some (fun (mi:System.Reflection.MemberInfo) (_:System.Type) -> f mi)

module ExpressionToSqlImpl =
    let translateFsSingle<'T>
            quoter
            (conditions:Quotations.Expr<('T -> bool)>)
            includeWhere
            customNameExtractor
            customParameterValueMap 
            itemInCollGenerator =

        let nameExtractor = customNameExtractor |> Option.defaultValue (fun (x:System.Reflection.MemberInfo) (_:System.Type) -> x.Name)
        let parameterValueMap = customParameterValueMap |> Option.defaultValue (fun _ x -> x)

        Hlp.translateOne quoter 
            nameExtractor
            parameterValueMap
            (Hlp.optionalIicgToOptFun itemInCollGenerator)
            (LinqHelpers.conv conditions)
            (Option.defaultValue true includeWhere)

    let translateFsMultiple<'T>
            quoter
            separator
            (conditions:Quotations.Expr<('T -> bool)>[])
            includeWhere
            customNameExtractor
            customParameterValueMap 
            itemInCollGenerator =
                
        let nameExtractor = customNameExtractor |> Option.defaultValue (fun (x:System.Reflection.MemberInfo) (_:System.Type) -> x.Name)        
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
            includeWhere 
            quoter separator nameExtractor parameterValueMap 
            (Hlp.optionalIicgToOptFun itemInCollGenerator) conditions
            
type ExpressionToSql =
   
    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate<'T>(quoter, conditions:Expression<Func<'T, bool>>, 
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]
                oldCustomNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap,
            [<Optional; DefaultParameterValue(null:Translator.ItemInCollectionImpl)>] 
                itemInCollGenerator) =
        
        Hlp.translateOne quoter 
            (Hlp.oldCneToFun oldCustomNameExtractor)
            (Hlp.cpvmToFun customParameterValueMap)
            (Hlp.iicgToOptFun itemInCollGenerator)
            conditions
            includeWhere 

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate<'T>(quoter:Translator.IQuoter, separator:Translator.ConjunctionWord, 
            conditions:Expression<Func<'T, bool>>[],
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,string>)>]
                customNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap,
            [<Optional; DefaultParameterValue(null:Translator.ItemInCollectionImpl)>] 
                itemInCollGenerator) = 
        
        Hlp.translateMultiple 
            includeWhere 
            quoter 
            separator 
            (Hlp.oldCneToFun customNameExtractor) 
            (Hlp.cpvmToFun customParameterValueMap)
            (Hlp.iicgToOptFun itemInCollGenerator)
            conditions
            
    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly AND this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, conditions, ?includeWhere, ?customNameExtractor) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere (Hlp.addTypeParam customNameExtractor) None None

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly AND this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, conditions, includeWhere, customNameExtractor, customParameterValueMap) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere (Hlp.addTypeParam customNameExtractor) customParameterValueMap None

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, conditions, includeWhere, customNameExtractor, customParameterValueMap, itemInCollGenerator) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere (Hlp.addTypeParam customNameExtractor) customParameterValueMap itemInCollGenerator

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly AND this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, separator, conditions, ?includeWhere, ?customNameExtractor) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere (Hlp.addTypeParam customNameExtractor) None None

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly AND this overload doesn't expose all parameters due to ambiguous overload resolution issues caused by optional parameters. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, separator, conditions, includeWhere, customNameExtractor, customParameterValueMap) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere (Hlp.addTypeParam customNameExtractor) customParameterValueMap None

    [<Obsolete("limited customerNameExtractor that doesn't cover virtual properties cases properly. Use ExpressionToSqlV2.Translate() instead")>]
    static member Translate(quoter, separator, conditions, includeWhere, customNameExtractor, customParameterValueMap, itemInCollGenerator) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere (Hlp.addTypeParam customNameExtractor) customParameterValueMap itemInCollGenerator

    static member AsFsFunc (x:Func<_,_>) = fun y -> x.Invoke(y)
    static member AsFsFunc3 (x:Func<_,_,_>) = fun y z -> x.Invoke(y, z)

type ExpressionToSqlV2 =
    
    ///single Linq expression
    static member Translate<'T>(quoter, conditions:Expression<Func<'T, bool>>, 
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,System.Type,string>)>]
                customNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap,
            [<Optional; DefaultParameterValue(null:Translator.ItemInCollectionImpl)>] 
                itemInCollGenerator) =
        
        Hlp.translateOne quoter 
            (Hlp.cneToFun customNameExtractor)
            (Hlp.cpvmToFun customParameterValueMap)
            (Hlp.iicgToOptFun itemInCollGenerator)
            conditions
            includeWhere 

    ///multiple Linq expressions
    static member Translate<'T>(quoter:Translator.IQuoter, separator:Translator.ConjunctionWord, 
            conditions:Expression<Func<'T, bool>>[],
            [<Optional; DefaultParameterValue(true)>]includeWhere, 
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.MemberInfo,System.Type,string>)>]
                customNameExtractor,
            [<Optional; DefaultParameterValue(null:Func<System.Reflection.PropertyInfo,obj,obj>)>] 
                customParameterValueMap,
            [<Optional; DefaultParameterValue(null:Translator.ItemInCollectionImpl)>] 
                itemInCollGenerator) = 
        
        Hlp.translateMultiple 
            includeWhere 
            quoter 
            separator 
            (Hlp.cneToFun customNameExtractor) 
            (Hlp.cpvmToFun customParameterValueMap)
            (Hlp.iicgToOptFun itemInCollGenerator)
            conditions 
 
    ///single F# quotation - full
    static member Translate(quoter, conditions, includeWhere, customNameExtractor, customParameterValueMap, itemInCollGenerator) =
        ExpressionToSqlImpl.translateFsSingle 
            quoter conditions includeWhere customNameExtractor customParameterValueMap itemInCollGenerator

    ///multiple F# quotations - full
    static member Translate(quoter, separator, conditions, includeWhere, customNameExtractor, customParameterValueMap, itemInCollGenerator) =
        ExpressionToSqlImpl.translateFsMultiple quoter separator conditions includeWhere customNameExtractor customParameterValueMap itemInCollGenerator
