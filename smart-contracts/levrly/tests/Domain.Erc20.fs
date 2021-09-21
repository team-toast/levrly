module Domain.Erc20

open Infrastructure
open Contracts
open AbiTypeProvider.Common

let grab (ctx: TestContext) (token: ERC20) from amount = async {
    let callData = 
        token.transferTransactionInput(
            recipient = ctx.Address, 
            amount = amount,  
            weiValue = weiValue 0UL,
            gasLimit = gasLlimit 9500000UL,
            gasPrice = gasPrice 0UL,
            From = from,
            To = token.Address)
    try
        let! txr = ctx.Connection.MakeImpersonatedCallAsync callData
        
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = ERC20.TransferEventDTO.DecodeAllEvents(txr)
        if events.Length = 0 then
            failwith "No Trnasfer event in transaction"
    with e ->
        failwith $"Exception on attempt to grab {token.Address}: {e.Message}"
}

let approveLendingPool (ctx: TestContext) (token: ERC20) amount = async {
    try
        let! txr = await ^ token.approveAsync(configuration.Addresses.AaveLendingPool, amount)
        
        if txr.Status <> ~~~ 1UL then
            failwith "Transaction not succeed"
        
        let events = ERC20.ApprovalEventDTO.DecodeAllEvents(txr)
        if events.Length = 0 then
            failwith "No Trnasfer event in transaction"
    with e ->
        failwith $"Exception on attempt to approve lending pool on {token.Address}: {e.Message}"
}