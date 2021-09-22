open System
open Nethereum.Web3
open Contracts
open AbiTypeProvider.Common
open Nethereum.Web3.Accounts
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs

type Token =
    { Symbol: string;
      Address: string;
      Decimals: int; }

let approveAsync (web3: Web3) amount tokenAddress = async {
    printfn "Started approving"
    let (contractAddress, data, value, gasPrice) = OneInch.approve amount tokenAddress
    printfn "Received approve calldata"
    let oneInch = OneInch(contractAddress, web3)
    let! tx = oneInch.SendTxAsync data (WeiValue(value)) (GasLimit(12450000I)) (GasPrice(gasPrice)) |> Async.AwaitTask
    printfn "Approve transaction executed"
    return HexBigInteger(1I) = tx.Status
}

let getPrivateKey () =
    printf "Input private key: "
    Console.ReadLine()

let getProjectId () =
    printf "Input Infura project ID: "
    Console.ReadLine()

let getUserAddress () =
    printf "Input your address: "
    Console.ReadLine()

[<EntryPoint>]
let main argv =
    let timeToken = { Symbol = "TIME"; Address = "0x5c59d7cb794471a9633391c4927ade06b8787a90"; Decimals = 18 }
    let pearToken = { Symbol = "PEAR"; Address = "0xc8bcb58caef1be972c0b638b1dd8b0748fdc8a44"; Decimals = 18  }

    let privateKey = getPrivateKey ()
    let userAddress = getUserAddress ()
    let projectId  = getProjectId ()

    let nodeURI = $"https://polygon-mainnet.infura.io/v3/{projectId}"

    //let amount = bigint (0.2 * float (pown 10 timeToken.Decimals))
    let amount = 200000000000000000I // 0.2 TIME

    let web3 = Web3(Account(privateKey, 137I), nodeURI)

    async {
           try
               let! isApproved = approveAsync web3 amount timeToken.Address
               if isApproved then
                   printfn "Approved"
                   let (contractAddress, data, value, gas, gasPrice) =
                       OneInch.getSwapData
                           timeToken.Address
                           pearToken.Address
                           userAddress
                           amount
                           5
                   let oneInch = OneInch(contractAddress, web3)
                   let gas' = if gas = 0I then 12450000I else gas
                   if data.Length < 32 * 300 then printfn "Calling unoswap function"
                   else printfn "Calling swap function"
                   let! tx = oneInch.SendTxAsync data (WeiValue(value)) (GasLimit(gas')) (GasPrice(gasPrice)) |> Async.AwaitTask
                   if tx.Failed() then printfn "Exchange failed"
                   else printfn "Exchange done"
                else printfn "Not approved"
            with ex ->
                printfn
                    "Exception: %s\nInner exception: %s"
                    ex.Message
                    ex.InnerException.Message
       } |> Async.RunSynchronously
    Console.ReadLine() |> ignore
    0
