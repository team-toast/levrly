module Domain

open Infrastructure
open Contracts

let decodeEvents<'event when 'event : (new : unit -> 'event)> 
    (txr: Nethereum.RPC.Eth.DTOs.TransactionReceipt) = 
    Nethereum.Contracts.EventExtensions.DecodeAllEvents<'event>(txr)

let dollar n = decimal (10f ** 18f) * n |> bigint

module Dai =

    let checkDaiBalance (ctx: TestContext) (dai: Contracts.DaiContract) = async {
        return! dai.balanceOfQueryAsync ctx.Connection.Account.Address |> Async.AwaitTask
    }

    let grabDai (ctx: TestContext) (dai: Contracts.DaiContract) amount = async {
        let dollar amount = decimal (10f ** 18f) * amount |> bigint
        let callData = 
            Contracts.DaiContract.transferFunction(
                dst = ctx.Connection.Account.Address,
                wad = dollar amount)
        let call = 
            ctx.Connection.MakeImpersonatedCallWithNoEtherAsync "0x40ec5b33f54e0e8a33a975908c5ba1c14e5bbbdf" dai.Address
        let! txr = call callData
        
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = decodeEvents<Contracts.DaiContract.TransferEventDTO>(txr)
        if events.Count = 0 then
            failwith "No Trnasfer event in transaction"
    }

    let approveLendingPoolOnDai (ctx: TestContext) (dai: Contracts.DaiContract) amount = async {
        let dollar amount = decimal (10f ** 18f) * amount |> bigint
        let! txr = dai.approveAsync(configration.DeployedContractAddresses.LendingPool, dollar amount)
                   |> Async.AwaitTask
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = decodeEvents<Contracts.DaiContract.ApprovalEventDTO>(txr)
        if events.Count = 0 then
            failwith "No Trnasfer event in transaction"
    }

module Aave =

    let depositDai (ctx: TestContext) (dai: Contracts.DaiContract) (lendingPool: Contracts.LendingPoolContract) amount = async {
        do! Dai.grabDai ctx dai amount
        do! Dai.approveLendingPoolOnDai ctx dai amount

        let! txr = lendingPool.depositAsync(configration.DeployedContractAddresses.Dai,
                                            dollar amount, 
                                            ctx.Connection.Account.Address, 
                                            uint16 0) |> Async.AwaitTask
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        let event = 
            decodeEvents<Contracts.LendingPoolContract.DepositEventDTO>(txr)
            |> Seq.find (fun e -> e.Event.user = ctx.Connection.Account.Address)
            |> fun e -> e.Event

        if event.amount <> dollar amount 
        || event.user <> ctx.Connection.Account.Address  // 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266
        || event.reserve <> "0x6B175474E89094C44Da98b954EedeAC495271d0F" then
            failwith "Invalid data in deposit event"
    }