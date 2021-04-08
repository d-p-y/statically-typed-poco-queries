module StaTypPocoQueries.Core.FsTests

open Xunit
open StaTypPocoQueries.Core
open System.Linq

type SomeEntity() =
    member val SomeInt = 0 with get, set
    member val SomeStr = "" with get, set

type Tests() =
    let quoter = {new Translator.IQuoter with member __.QuoteColumn x = sprintf "<%s>" x}
    let itemInColl = new Translator.ItemInCollectionImpl(fun (item, coll) -> sprintf "%s is_in_coll %s" item coll) |> Some
    
    [<Fact>]
    let ``test constants only`` () =
        let s = "abc"
        let query, parms = 
            ExpressionToSql.Translate<SomeEntity>(quoter, <@ fun (x:SomeEntity) -> 5 = 6 @>)

        Assert.Equal("WHERE @0 = @1", query)
        Assert.Equal([5 :> obj; 6 :> obj], parms)

    [<Fact>]
    let ``test single equals notnull quotation`` () =
        let s = "abc"
        let query, parms = 
            ExpressionToSql.Translate<SomeEntity>(quoter, <@ fun (x:SomeEntity) -> x.SomeInt = 5 && x.SomeStr = s @>)

        Assert.Equal("WHERE <SomeInt> = @0 AND <SomeStr> = @1", query)
        Assert.Equal([5 :> obj; "abc" :> obj], parms)
        
    [<Fact>]
    let ``test item in array`` () =
        let arrItems = [| 11; 4; 8|]
        let query, parms =
            ExpressionToSql.Translate<SomeEntity>(quoter, <@ fun (x:SomeEntity) -> arrItems.Contains x.SomeInt @>, Some true, None, None, itemInColl)

        Assert.Equal("WHERE <SomeInt> is_in_coll @0", query)
        Assert.Equal([arrItems :> obj], parms)

    [<Fact>]
    let ``test multiple quotations`` () =
        let s = "abc"
        let query, parms = 
            ExpressionToSql.Translate<SomeEntity>(quoter, Translator.ConjunctionWord.Or, 
                [| 
                    <@ fun (x:SomeEntity) -> x.SomeInt = 5 && x.SomeStr = s @> 
                    <@ fun (x:SomeEntity) -> x.SomeInt = 15@> 
                |])

        Assert.Equal("WHERE (<SomeInt> = @0 AND <SomeStr> = @1) OR (<SomeInt> = @2)", query)
        Assert.Equal([5 :> obj; "abc" :> obj; 15 :> obj], parms)

    [<Fact>]
    let ``test member access owner origin`` () =
        //issue #7
        let s = SomeEntity(SomeStr="123")
        let query, parms = 
            ExpressionToSql.Translate<SomeEntity>(quoter, <@ fun (x:SomeEntity) -> x.SomeStr = s.SomeStr @>)

        Assert.Equal("WHERE <SomeStr> = @0", query)
        Assert.Equal(["123" :> obj], parms)

        let query, parms = 
            ExpressionToSql.Translate<SomeEntity>(quoter, <@ fun (x:SomeEntity) -> s.SomeStr = x.SomeStr @>)

        Assert.Equal("WHERE @0 = <SomeStr>", query)
        Assert.Equal(["123" :> obj], parms)
