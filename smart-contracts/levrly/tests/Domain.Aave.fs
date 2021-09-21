[<RequireQualifiedAccess>]
module Domain.Aave

open Infrastructure
open Contracts
open AbiTypeProvider.Common
open Domain

// let decodeEvents<'event when 'event : (new : unit -> 'event)> 
//     (txr: Nethereum.RPC.Eth.DTOs.TransactionReceipt) = 
//     Nethereum.Contracts.EventExtensions.DecodeAllEvents<'event>(txr)

let interestRateMode = {| Stable = bigint 1; Variable = bigint 2 |}

let depositDai (ctx: TestContext) (lendingPool: LendingPool) amount = async {
    let! txr = lendingPool.depositAsync(configuration.Addresses.Dai,
                                        Dai.dollar amount, 
                                        ctx.Connection.Account.Address, 
                                        uint16 0) |> Async.AwaitTask
    if txr.Status <> ~~~ 1UL then
        failwith "Transaction not succeed"
    let event = 
        LendingPool.DepositEventDTO.DecodeAllEvents(txr)
        |> Seq.find (fun e -> e.user = ctx.Connection.Account.Address)

    if event.amount <> Dai.dollar amount 
    || event.user <> ctx.Address  // 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266
    || event.reserve <> "0x6B175474E89094C44Da98b954EedeAC495271d0F" then
        failwith "Invalid data in deposit event"
}

let depositSnx (ctx: TestContext) (lendingPool: LendingPool) amount = async {
    let! txr = lendingPool.depositAsync(configuration.Addresses.Snx,
                                        amount, 
                                        ctx.Connection.Account.Address, 
                                        uint16 0) |> Async.AwaitTask
    if txr.Status <> ~~~ 1UL then
        failwith "Transaction not succeed"
    let event = 
        LendingPool.DepositEventDTO.DecodeAllEvents(txr)
        |> Seq.find (fun e -> e.user = ctx.Connection.Account.Address)

    if event.amount <> amount 
    || event.user <> ctx.Address  // 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266
    || event.reserve <> "0xC011a73ee8576Fb46F5E1c5751cA3B9Fe0af2a6F" then
        failwith $"Invalid data in deposit event"
}

let borrowSnx (ctx: TestContext) (lendingPool: LendingPool) amount = async {
    let! txr = await ^ lendingPool.borrowAsync(configuration.Addresses.Snx, amount, interestRateMode.Variable, 0us, ctx.Address)
    if txr.Status <> ~~~ 1UL then
        failwith "Transaction not succeed"
    let event = 
        LendingPool.BorrowEventDTO.DecodeAllEvents(txr)
        |> Seq.find (fun e -> e.user = ctx.Connection.Account.Address)
    if event.user <> ctx.Address || event.amount <> amount then
        failwith "Invalid data in deposit event"
}

let useReserveAsCollateral 
    (ctx: TestContext) 
    (lendingPool: LendingPool) 
    (assetAddress: string) = async {
    return! lendingPool.setUserUseReserveAsCollateralAsync(assetAddress, true) |> Async.AwaitTask
}

let notUseReserveAsCollateral 
    (ctx: TestContext) 
    (lendingPool: LendingPool) 
    (assetAddress: string) = async {
    return! lendingPool.setUserUseReserveAsCollateralAsync(assetAddress, false) |> Async.AwaitTask
}

let setAssetPrice 
    (ctx: TestContext)
    (priceOracle: MockPriceOracle)
    (assetAddress: string)
    (price: bigint) = async {
    let dollar amount = decimal (10f ** 18f) * amount |> bigint
    let callData = 
        priceOracle.setAssetPriceTransactionInput(
            _asset = assetAddress,
            _price = price,
            weiValue = weiValue 0UL,
            gasLimit = gasLlimit 9500000UL,
            gasPrice = gasPrice 0UL,
            From = configuration.AavePriceOracleOwnerAddress,
            To = priceOracle.Address)
    let! txr = ctx.Connection.MakeImpersonatedCallAsync callData
    
    if txr.Status <> ~~~ 1UL then
        failwith "Transaction not succeed"
    }

let setPriceOracle
    (ctx: TestContext)
    (lpAddressProvider: LendingPoolAddressesProvider)
    (priceOracleAddress: string) = async {
        let actualOwnerAddress = "0xee56e2b3d491590b5b31738cc34d5232f378a8d5"
        // let callData = LendingPoolAddressesProvider.setPriceOracleFunction(priceOracle = priceOracleAddress)
        let callData = 
            lpAddressProvider.setPriceOracleTransactionInput(
                priceOracle = priceOracleAddress,
                weiValue = weiValue 0UL,
                gasLimit = gasLlimit 9500000UL,
                gasPrice = gasPrice 0UL,
                From = actualOwnerAddress,
                To = lpAddressProvider.Address)
        let! txr = 
            ctx.Connection.MakeImpersonatedCallAsync callData

        let event = 
            txr
            |> LendingPoolAddressesProvider.PriceOracleUpdatedEventDTO.DecodeAllEvents
            |> Seq.exactlyOne
        
        if event.newAddress.ToLowerInvariant() <> priceOracleAddress then
            failwith "Price oracle not changed after transaction."
    }