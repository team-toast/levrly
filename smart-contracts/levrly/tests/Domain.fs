module Domain

open Infrastructure
open Contracts

let decodeEvents<'event when 'event : (new : unit -> 'event)> 
    (txr: Nethereum.RPC.Eth.DTOs.TransactionReceipt) = 
    Nethereum.Contracts.EventExtensions.DecodeAllEvents<'event>(txr)

let dollar n = decimal (10f ** 18f) * n |> bigint

module Dai =

    let checkDaiBalance (ctx: TestContext) (dai: DAI) = async {
        return! dai.balanceOfQueryAsync ctx.Connection.Account.Address |> Async.AwaitTask
    }

    let grabDai (ctx: TestContext) (dai: DAI) amount = async {
        let dollar amount = decimal (10f ** 18f) * amount |> bigint
        let callData = 
            DAI.transferFunction(
                dst = ctx.Connection.Account.Address,
                wad = dollar amount)
        let call = 
            ctx.Connection.MakeImpersonatedCallWithNoEtherAsync "0x40ec5b33f54e0e8a33a975908c5ba1c14e5bbbdf" dai.Address
        let! txr = call callData
        
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = decodeEvents<DAI.TransferEventDTO>(txr)
        if events.Count = 0 then
            failwith "No Trnasfer event in transaction"
    }

    let approveLendingPoolOnDai (ctx: TestContext) (dai: DAI) amount = async {
        let dollar amount = decimal (10f ** 18f) * amount |> bigint
        let! txr = dai.approveAsync(configration.Addresses.AaveLendingPool, dollar amount)
                   |> Async.AwaitTask
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = decodeEvents<DAI.ApprovalEventDTO>(txr)
        if events.Count = 0 then
            failwith "No Trnasfer event in transaction"
    }

module Aave =
    let interestRateMode = {| Stable = bigint 1; Variable = bigint 2 |}

    let depositDai (ctx: TestContext) (lendingPool: LendingPool) amount = async {
        let! txr = lendingPool.depositAsync(configration.Addresses.Dai,
                                            dollar amount, 
                                            ctx.Connection.Account.Address, 
                                            uint16 0) |> Async.AwaitTask
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        let event = 
            decodeEvents<LendingPool.DepositEventDTO>(txr)
            |> Seq.find (fun e -> e.Event.user = ctx.Connection.Account.Address)
            |> fun e -> e.Event

        if event.amount <> dollar amount 
        || event.user <> ctx.Address  // 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266
        || event.reserve <> "0x6B175474E89094C44Da98b954EedeAC495271d0F" then
            failwith "Invalid data in deposit event"
    }

    let borrowSnx (ctx: TestContext) (lendingPool: LendingPool) amount = async {        
        let! txr = lendingPool.borrowAsync(configration.Addresses.Snx, amount, interestRateMode.Variable, 0us, ctx.Address) |> Async.AwaitTask
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        let event = 
            decodeEvents<LendingPool.BorrowEventDTO>(txr)
            |> Seq.find (fun e -> e.Event.user = ctx.Connection.Account.Address)
            |> fun e -> e.Event
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
            MockPriceOracle.setAssetPriceFunction(
                _asset = assetAddress,
                _price = price)
        let call = 
            ctx.Connection.MakeImpersonatedCallWithNoEtherAsync configration.AavePriceOracleOwnerAddress priceOracle.Address
        let! txr = call callData
        
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        }

    let setPriceOracle
        (ctx: TestContext)
        (lpAddressProvider: LendingPoolAddressesProvider)
        (priceOracleAddress: string) = async {
            let actualOwnerAddress = "0xee56e2b3d491590b5b31738cc34d5232f378a8d5"
            let callData = LendingPoolAddressesProvider.setPriceOracleFunction(priceOracle = priceOracleAddress)
            let call = 
                ctx.Connection.MakeImpersonatedCallWithNoEtherAsync actualOwnerAddress lpAddressProvider.Address
            let! txr = call callData

            let event = 
                txr
                |> decodeEvents<LendingPoolAddressesProvider.PriceOracleUpdatedEventDTO> 
                |> Seq.exactlyOne
                |> fun e -> e.Event
            
            if event.newAddress.ToLowerInvariant() <> priceOracleAddress then
                failwith "Price oracle not changed after transaction."
        }