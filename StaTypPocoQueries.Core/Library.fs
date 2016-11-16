namespace StaTypPocoQueries.Core

open System
open System.Linq.Expressions

module Translator = 
    let literalToSql (v:obj) (curParams:_ list) =
        match v with
        | null | :? DBNull -> true, "null", None
        | _ -> false, sprintf "@%i" curParams.Length, Some v 
        //use sqlparameters instead of inline sql due to PetaPoco's static query cache growing

    let rec constantOrMemberAccessValue (body:Expression) curParams = 
        match (body.NodeType, body) with
        | ExpressionType.Constant, (:? ConstantExpression as body) -> 
            literalToSql body.Value curParams
        | ExpressionType.Convert, (:? UnaryExpression as body) -> 
            constantOrMemberAccessValue body.Operand curParams
        | ExpressionType.MemberAccess, (:? MemberExpression as body) -> 
            match body.Member.GetType().Name with
            |"RtFieldInfo" -> 
                let unaryExpr = Expression.Convert(body, typeof<obj>)
                let v = Expression.Lambda<Func<obj>>(unaryExpr).Compile()
                literalToSql (v.Invoke ()) curParams
            |"RuntimePropertyInfo" -> false, body.Member.Name, None
            |_ as name -> sprintf "unsupported member type name %s" name |> failwith    
        | _ -> sprintf "unsupported nodetype %A" body|> failwith    

    let leafExpression (body:BinaryExpression) sqlOperator curParams =
        let lNull, lVal, lParam = constantOrMemberAccessValue body.Left curParams
        let rNull, rVal, rParam = constantOrMemberAccessValue body.Right curParams
                
        let oper = 
            match (rNull, body.NodeType) with
            | true, ExpressionType.NotEqual -> " is "
            | true, ExpressionType.Equal -> " is not "
            | false, _ -> sprintf " %s " sqlOperator
            | _ -> sprintf "unsupported nodetype %A" body |> failwith

        lVal + oper + rVal,
        match lParam,rParam with
        | None, None -> curParams
        | Some l, None -> l::curParams
        | None, Some r -> r::curParams
        | Some l, Some r -> r::l::curParams
        
    let boolValueToWhereClause (body:MemberExpression) curParams isTrue = 
        let _, value, _ = constantOrMemberAccessValue body curParams
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
        | ExpressionType.AndAlso -> Some "and"
        | ExpressionType.OrElse -> Some "or"
        | _ -> None
        
    let sqlInBracketsIfNeeded parentJunction junctionSql sql = 
        match parentJunction with
        | None -> sql
        | Some parentJunction when parentJunction = junctionSql -> sql
        | _ -> sprintf "(%s)" sql

    let rec comparisonToWhereClause (body:Expression) parentJunction curSqlParams = 
        match body with
        | :? UnaryExpression as body when body.NodeType = ExpressionType.Not && body.Type = typeof<System.Boolean> ->
            match body.Operand with
            | :? MemberExpression as body -> boolValueToWhereClause body curSqlParams false
            | _ -> sprintf "not condition has unexpected type %A" body |> failwith
        | :? MemberExpression as body when body.NodeType = ExpressionType.MemberAccess && body.Type = typeof<System.Boolean> ->
            boolValueToWhereClause body curSqlParams true
        | :? BinaryExpression as body -> 
            match junctionToSqlOper body with
            | Some junctionSql ->
                let consumeExpr (expr:Expression) (curSqlParams:_ list) = 
                    match expr with
                    | :? BinaryExpression as expr ->
                        match junctionToSqlOper expr, binExpToSqlOper expr with
                        | Some _, None -> 
                            let sql, parms = comparisonToWhereClause expr (Some junctionSql) curSqlParams
                            let sql = sql |> sqlInBracketsIfNeeded parentJunction junctionSql
                            sql, parms
                        | None, Some sqlOper -> leafExpression expr sqlOper curSqlParams
                        | _ -> "expression is not a junction and not a supported leaf" |> failwith
                    | _ -> comparisonToWhereClause expr parentJunction curSqlParams

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
                | Some sqlOper -> leafExpression body sqlOper curSqlParams
                | None -> sprintf "unsupported operator %A in leaf" body.NodeType |> failwith
        | _ -> sprintf "conditions has unexpected type %A" body |> failwith

type ExpressionToSql = 
    static member Translate<'T> (conditions:Expression<Func<'T, bool>>) =
        let sql, parms = Translator.comparisonToWhereClause conditions.Body None List.empty
        new Tuple<_,_>("where " + sql, parms |> List.rev |> Array.ofList)
