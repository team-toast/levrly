module Tests

open Xunit
open Infrastructure
open Contracts
open Domain

[<Fact>]
let ``LendingPool ready`` () = 
    withContext 
    <| fun ctx -> 
        let lendingPool = lendingPool ctx
        let revision = lendingPool.LENDINGPOOL_REVISIONQuery()
        Assert.Equal(bigint 3, revision)
        let isPaused = lendingPool.pausedQuery()
        Assert.False(isPaused)

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
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx

        do! Dai.grabDai ctx dai 1_000m

        let! balance = await ^  dai.balanceOfQueryAsync ctx.Address
        Assert.Equal(balance, dollar 1_000m)
    }

[<Fact>]
let ``DAI deposited`` () =
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx
        let aDai = aDai ctx
        let lendingPool = lendingPool ctx
        
        do! Dai.grabDai ctx dai 1_000m
        do! Dai.approveLendingPoolOnDai ctx dai 1_000m

        do! Aave.depositDai ctx lendingPool 1_000m
        
        let! aTokenBalance = await ^ aDai.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(dollar 1_000m, aTokenBalance)

        let! daiBalance = await ^ dai.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(dollar 0m, daiBalance) 
    }

[<Fact>]
let ``SNX borrowed against DAI collaterall`` () =
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx
        let aDai = aDai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx

        do! Dai.grabDai ctx dai 1_000m
        do! Dai.approveLendingPoolOnDai ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 500I
        
        let! snxBalance = await ^ snx.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(500I, snxBalance)
    }
