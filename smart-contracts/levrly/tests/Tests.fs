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
        let snx = snx ctx
        let lendingPool = lendingPool ctx

        do! Dai.grabDai ctx dai 1_000m
        do! Dai.approveLendingPoolOnDai ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 500I
        
        let! snxBalance = await ^ snx.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(500I, snxBalance)
    }

[<Fact>]
let ``DAI price changed`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! oldPriceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let! priceOracle = deployContract ctx (priceOracleAt ctx) [ oldPriceOracleAddress ]

        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configration.Addresses.Dai)
        Assert.Equal(371400000000000I, daiPrice)
        
        do! Aave.setPriceOracle ctx lpAddressProvider priceOracle.Address
        let! txr = Aave.setAssetPrice ctx priceOracle configration.Addresses.Dai 471400000000000I
        
        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configration.Addresses.Dai)
        Assert.Equal(471400000000000I, daiPrice)
    }

[<Fact>]
let ``Money lost`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! oldPriceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let! priceOracle = deployContract ctx (priceOracleAt ctx) [ oldPriceOracleAddress ]
        let dai = dai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx
        
        do! Aave.setPriceOracle ctx lpAddressProvider priceOracle.Address
        do! Dai.grabDai ctx dai 1_000m
        do! Dai.approveLendingPoolOnDai ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 1_000I

        // let! txr = await ^ priceOracle.setAssetPriceAsync(configration.Addresses.Snx, 271400000000000I)
        let! txr = Aave.setAssetPrice ctx priceOracle configration.Addresses.Snx 12286960000000000I

        let! approved = await ^ snx.approveQueryAsync(configration.Addresses.AaveLendingPool, 3_000I)
        Assert.True(approved)

        let! payed = 
            await ^ lendingPool.repayQueryAsync(
                asset = configration.Addresses.Snx,
                amount = 1_000I,
                rateMode = Aave.interestRateMode.Variable,
                onBehalfOf = ctx.Address)
        // let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        // Assert.True(false, Newtonsoft.Json.JsonConvert.SerializeObject(acc))
        Assert.Equal(0I, payed)
    }

[<Fact>]
let ``Asset prices are as expectd`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! priceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let priceOracle = priceOracleAt ctx priceOracleAddress
        
        let expected = {| Dai =  371400000000000I; 
                          Snx = 6286960000000000I |}
        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configration.Addresses.Dai)
        let! snxPrice = await ^ priceOracle.getAssetPriceQueryAsync(configration.Addresses.Snx)
        let actual = {| Dai = daiPrice; Snx = snxPrice |}
        Assert.Equal(expected, actual)
    }