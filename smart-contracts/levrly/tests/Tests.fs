module Tests

open Xunit
open Infrastructure
open Contracts

[<Fact>]
let ``LendingPool revision matched`` () = 
    withContext 
    <| fun ctx -> 
        let lendingPool = lendingPool ctx
        let revision = lendingPool.LENDINGPOOL_REVISIONQuery()
        Assert.Equal(bigint 3, revision)

[<Fact>]
let ``DAI total supply non zero`` () = 
    withContext 
    <| fun ctx -> 
        let dai = dai ctx
        let totalSupply = dai.totalSupplyQuery()
        Assert.True(bigint 1 < totalSupply, $"Actual supply: {totalSupply}")

[<Fact>]
let ``Some huy has DAI`` () = 
    withContext 
    <| fun ctx -> 
        let dai = dai ctx
        let balance = dai.balanceOfQuery("0x40ec5b33f54e0e8a33a975908c5ba1c14e5bbbdf")
        Assert.Equal(bigint 60309027128217166192534853m, balance)

[<Fact>]
let ``DAI acquired`` () = 
    withContextBind
    <| fun ctx -> async {
        let dai = dai ctx
        let dollar n = decimal (10f ** 18f) * n |> bigint
        let callData = 
            Contracts.DaiContract.transferFunction(
                dst = ctx.Connection.Account.Address,
                wad = dollar 1_000m)
        let call = 
            ctx.Connection.MakeImpersonatedCallWithNoEtherAsync "0x40ec5b33f54e0e8a33a975908c5ba1c14e5bbbdf" dai.Address
        let! _ = call callData
        
        let! balance = dai.balanceOfQueryAsync ctx.Connection.Account.Address |> Async.AwaitTask
        Assert.Equal(balance, dollar 1_000m)
        ()
    }


