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
        Assert.True(1I < totalSupply, $"Actual supply: {totalSupply}")

[<Fact>]
let ``Some guy has DAI`` () = 
    withContext 
    <| fun ctx -> 
        let dai = dai ctx
        let balance = dai.balanceOfQuery("0x40ec5b33f54e0e8a33a975908c5ba1c14e5bbbdf")
        Assert.Equal(60309027128217166192534853I, balance)

[<Fact>]
let ``Some guy has SNX`` () = 
    withContext 
    <| fun ctx -> 
        let snx = snx ctx
        let balance = snx.balanceOfQuery("0xf05e2a70346560d3228c7002194bb7c5dc8fe100")
        Assert.Equal(16414200000000000000000I, balance)

[<Fact>]
let ``DAI acquired`` () = 
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx

        do! Dai.grab ctx dai 1_000m

        let! balance = await ^  dai.balanceOfQueryAsync ctx.Address
        Assert.Equal(balance, Dai.dollar 1_000m)
    }

[<Fact>]
let ``DAI deposited`` () =
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx
        let aDai = aDai ctx
        let lendingPool = lendingPool ctx
        
        do! Dai.grab ctx dai 1_000m
        do! Dai.approveLendingPool ctx dai 1_000m

        do! Aave.depositDai ctx lendingPool 1_000m
        
        let! aTokenBalance = await ^ aDai.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(Dai.dollar 1_000m, aTokenBalance)

        let! daiBalance = await ^ dai.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(Dai.dollar 0m, daiBalance) 
    }

[<Fact>]
let ``SNX borrowed against DAI collaterall`` () =
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx

        do! Dai.grab ctx dai 1_000m
        do! Dai.approveLendingPool ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 500I
        
        let! snxBalance = await ^ snx.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(500I, snxBalance)
    }

[<Fact>]
let ``SNX can be transferred`` () =
    withContextAsync
    <| fun ctx -> async {
        let dai = dai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx

        do! Dai.grab ctx dai 1_000m
        do! Dai.approveLendingPool ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 500I
        
        let! snxBalance = await ^ snx.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(500I, snxBalance)

        // Just transfer tokens to random address
        let! txr = await ^ snx.transferAsync("0x6b175474e89094c44da98b954eedeac495271d0f", 500I)
        ()
    }

[<Fact>]
let ``DAI price changed`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! oldPriceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let! priceOracle = deployContract ctx (priceOracleAt ctx) [ oldPriceOracleAddress ]

        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configuration.Addresses.Dai)
        Assert.Equal(371400000000000I, daiPrice)
        
        do! Aave.setPriceOracle ctx lpAddressProvider priceOracle.Address
        let! txr = Aave.setAssetPrice ctx priceOracle configuration.Addresses.Dai 471400000000000I
        
        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configuration.Addresses.Dai)
        Assert.Equal(471400000000000I, daiPrice)
    }

[<Fact>]
let ``Asset prices are as expected`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! priceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let priceOracle = priceOracleAt ctx priceOracleAddress
        
        let expected = {| Dai =  371400000000000I; 
                          Snx = 6286960000000000I |}
        let! daiPrice = await ^ priceOracle.getAssetPriceQueryAsync(configuration.Addresses.Dai)
        let! snxPrice = await ^ priceOracle.getAssetPriceQueryAsync(configuration.Addresses.Snx)
        let actual = {| Dai = daiPrice; Snx = snxPrice |}
        Assert.Equal(expected, actual)
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
        do! Dai.grab ctx dai 1_000m
        do! Dai.approveLendingPool ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 1_000I
        
        let! snxBalance = await ^ snx.balanceOfQueryAsync(ctx.Address)
        Assert.Equal(1_000I, snxBalance) 

        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(6I, acc.totalDebtETH)
        
        do! Aave.setAssetPrice ctx priceOracle configuration.Addresses.Snx 12286960000000000I

        // Assert user debt grown after borrowed asset price grown.
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(12I, acc.totalDebtETH)

        let! txr = await ^ snx.approveAsync(configuration.Addresses.AaveLendingPool, 3_000I)
        Assert.Equal(~~~1UL, txr.Status)

        let! txr = 
            await ^ lendingPool.repayAsync(
                asset = configuration.Addresses.Snx,
                amount = 1_000I,
                rateMode = Aave.interestRateMode.Variable,
                onBehalfOf = ctx.Address)
        let event = Seq.exactlyOne ^ LendingPool.RepayEventDTO.DecodeAllEvents(txr) 
        Assert.Equal(1_000I, event.amount)
        
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(0I, acc.totalDebtETH)
    }

[<Fact>]
let ``Liquidation can be done on account with bad health`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! oldPriceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let! priceOracle = deployContract ctx (priceOracleAt ctx) [ oldPriceOracleAddress ]
        let dai = dai ctx
        let aDai = aDai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx
        
        do! Aave.setPriceOracle ctx lpAddressProvider priceOracle.Address
        do! Dai.grab ctx dai 1_000m
        
        do! Dai.approveLendingPool ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool 1_000_000I

        // Health factor value asserted this way because there little inaccurate calculation algorithm.
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.True(47266942514142081132675787470000I > acc.healthFactor, 
                    $"Health factor was {acc.healthFactor}")
        
        // Decrease collaterall cost
        do! Aave.setAssetPrice ctx priceOracle configuration.Addresses.Dai 1I

        // Assert account health factor below 1
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(127266942411708559I, acc.healthFactor)

        // Assert liquidator's DAI balance is zero before he get his reward for covering debt.
        let! liquidatorDaiBalace = await ^ dai.balanceOfQueryAsync(configuration.AccountAddress1)
        Assert.Equal(0I, liquidatorDaiBalace)


        do! inNestedContextAsync configuration.AccountPrivateKey1 
            <| fun ctx -> async {
                let snx = Contracts.snx ctx
                let lendingPool = Contracts.lendingPool ctx
                
                do! Erc20.grab ctx snx "0xf05e2a70346560d3228c7002194bb7c5dc8fe100" 16414200000000000000000I
                do! Erc20.approveLendingPool ctx snx 16414200000000000000000I
                
                let! txr = 
                    await ^ lendingPool.liquidationCallAsync(
                        collateralAsset = dai.Address,
                        debtAsset = snx.Address,
                        user = configuration.AccountAddress0,
                        debtToCover = 499_999I, // Half of debt minus one.
                        receiveAToken = false)
                let event = Seq.head ^ LendingPool.LiquidationCallEventDTO.DecodeAllEvents(txr)
                Assert.Equal(1000000010835640852399I, event.liquidatedCollateralAmount)
            }
        
        // Liquidator's SNX balance decreases. 151485 SNX spend on covering debt.
        let! liquidatorSnxBalace = await ^ snx.balanceOfQueryAsync(configuration.AccountAddress1)
        Assert.Equal(16414199999999999848515I, liquidatorSnxBalace)

        // So much DAI's recieved because it cost just 1 wei now.
        let! liquidatorDaiBalace = await ^ dai.balanceOfQueryAsync(configuration.AccountAddress1)
        Assert.Equal(1000000010835640852399I, liquidatorDaiBalace)

        // And collateral is lost.
        let! liquidatedAccountADaiBalance = await ^ aDai.balanceOfQueryAsync(configuration.AccountAddress0)
        Assert.Equal(0I, liquidatedAccountADaiBalance)
        
        // Health factor is 0 because whole collaterall lost.
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(0I, acc.healthFactor) 
    }

/// Just more realisic situation then in preveous test. Collateral cost is reduced by half, not
/// set to 1 wei.
[<Fact>]
let ``Liquidated account health more then 1`` () =
    withContextAsync
    <| fun ctx -> async {
        let lpAddressProvider = lendingPoolAddressProvider ctx
        let! oldPriceOracleAddress = await ^ lpAddressProvider.getPriceOracleQueryAsync()
        let! priceOracle = deployContract ctx (priceOracleAt ctx) [ oldPriceOracleAddress ]
        let dai = dai ctx
        let aDai = aDai ctx
        let snx = snx ctx
        let lendingPool = lendingPool ctx
        
        do! Aave.setPriceOracle ctx lpAddressProvider priceOracle.Address
        do! Dai.grab ctx dai 1_000m
        
        do! Dai.approveLendingPool ctx dai 1_000m
        do! Aave.depositDai ctx lendingPool 1_000m
        do! Aave.borrowSnx ctx lendingPool (40I * ``1e+18``)
        
        // Health factor value asserted this way because there little inaccurate calculation algorithm.
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.True(``1e+18`` < acc.healthFactor, $"Health factor was {acc.healthFactor}")
        
        // Decrease collaterall cost twice
        do! Aave.setAssetPrice ctx priceOracle configuration.Addresses.Dai 185700000000000I

        // Assert account health factor below 1
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.True(``1e+18`` > acc.healthFactor, $"Health factor was {acc.healthFactor}")

        do! inNestedContextAsync configuration.AccountPrivateKey1 
            <| fun ctx -> async {
                let snx = Contracts.snx ctx
                let lendingPool = Contracts.lendingPool ctx
                
                do! Erc20.grab ctx snx "0xf05e2a70346560d3228c7002194bb7c5dc8fe100" (16414I * ``1e+18``)
                do! Erc20.approveLendingPool ctx snx (16414I * ``1e+18``)
                
                let! txr = 
                    await ^ lendingPool.liquidationCallAsync(
                        collateralAsset = dai.Address,
                        debtAsset = snx.Address,
                        user = configuration.AccountAddress0,
                        debtToCover = (20I * ``1e+18`` - 1I), // Half of debt minus one.
                        receiveAToken = false)
                let event = Seq.head ^ LendingPool.LiquidationCallEventDTO.DecodeAllEvents(txr)
                Assert.Equal(710_964781906300484617I, event.liquidatedCollateralAmount)
            }
        
        // Liquidator's SNX balance decreases.
        let! liquidatorSnxBalace = await ^ snx.balanceOfQueryAsync(configuration.AccountAddress1)
        Assert.Equal(16394_000000000000000001I, liquidatorSnxBalace)

        // Lqiuidator receives DAI. 
        let! liquidatorDaiBalace = await ^ dai.balanceOfQueryAsync(configuration.AccountAddress1)
        Assert.Equal(710_964781906300484617I, liquidatorDaiBalace)

        // And collateral is partially lost.
        let! liquidatedAccountADaiBalance = await ^ aDai.balanceOfQueryAsync(configuration.AccountAddress0)
        Assert.Equal(289_035228929340367782I, liquidatedAccountADaiBalance)
        
        // Health factor increased.
        let! acc = await ^ lendingPool.getUserAccountDataQueryAsync(ctx.Address)
        Assert.Equal(341493125242737982I, acc.healthFactor)
    }

open AbiTypeProvider.Common
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs

[<Fact>]
let ``Swap ETH to DAI using 1Inch`` () =
    withContextAsync
    <| fun ctx -> async {
        let chainId = 1
        let slippage = 3
        let ethAddress = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
        let daiAddress = "0x6b175474e89094c44da98b954eedeac495271d0f"
        let amount = 10_000_000_000_000_000I
        let addressFrom = OneInch.DefaultIntermediateAddress

        // let! isApproved = OneInch.approveAsync ctx.Web3 chainId amount ethAddress
        // if not isApproved then Assert.True(false)

        printf "block number: %A\n" (ctx.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync() |> runNow) 
        let (contractAddress, data, value, gas, _) =
            OneInch.getSwapData
                chainId
                None
                ethAddress
                daiAddress
                addressFrom
                amount
                slippage

        let gas' = if gas = 0I then 12450000I else gas

        // Due to the lag in the block number in the fork, the gas price received from the API
        // will not be relevant and we are have to receive it from the network.
        let gasPrice = ctx.Web3.Eth.GasPrice.SendRequestAsync() |> runNow

        let callData = 
            TransactionInput(
                data=data, 
                addressTo = contractAddress, 
                addressFrom = addressFrom, 
                gas = HexBigInteger(gas'), 
                gasPrice = gasPrice,
                value = HexBigInteger(value))

        printf "gas price: %A\n" (gasPrice)

        let dai = dai ctx    
        let daiBefore = dai.balanceOfQueryAsync(addressFrom) |> runNow
        let! txr = ctx.Connection.MakeImpersonatedCallAsync callData
        let daiAfter = dai.balanceOfQueryAsync(addressFrom) |> runNow

        Assert.NotEqual(daiBefore, daiAfter);

        printf "daiBefore: %A\n" daiBefore
        printf "daiAfter: %A\n" daiAfter

        Assert.Equal(HexBigInteger(1I), txr.Status)
    }

[<Fact>]
let ``Swap ETH to DAI using ZeroEx`` () =
    withContextAsync
    <| fun ctx -> async {
        let chainId = 1
        let ethAddress = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
        let daiAddress = "0x6b175474e89094c44da98b954eedeac495271d0f"
        let amount = 10000000000000000I
        let addressFrom = OneInch.DefaultIntermediateAddress

        printf "block number: %A\n" (ctx.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync() |> runNow) 
        let (contractAddress, data, value, gas, gasPrice) =
            ZeroEx.getSwapData
                chainId
                ethAddress
                daiAddress
                amount

        let gas' = if gas = 0I then 12450000I else gas

        let callData = 
            TransactionInput(
                data=data, 
                addressTo = contractAddress, 
                addressFrom = addressFrom, 
                gas = HexBigInteger(gas'), 
                gasPrice = HexBigInteger(gasPrice),
                value = HexBigInteger(value))

        printf "gas price: %A\n" (gasPrice)

        let dai = dai ctx    
        let daiBefore = dai.balanceOfQueryAsync(addressFrom) |> runNow
        let! txr = ctx.Connection.MakeImpersonatedCallAsync callData 
        let daiAfter = dai.balanceOfQueryAsync(addressFrom) |> runNow

        Assert.NotEqual(daiBefore, daiAfter);

        printf "daiBefore: %A\n" daiBefore
        printf "daiAfter: %A\n" daiAfter

        Assert.Equal(HexBigInteger(1I), txr.Status)
    }
