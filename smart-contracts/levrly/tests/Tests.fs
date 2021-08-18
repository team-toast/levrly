module Tests

open System
open Xunit
open Contracts

[<Fact>]
let ``LendingPool revision matched`` () = 
    withContext 
    <| fun ctx -> 
        let revision = ctx.LendingPool.LENDINGPOOL_REVISIONQuery()
        Assert.Equal(bigint 3, revision)
