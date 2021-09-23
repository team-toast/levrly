open System
open System.Numerics
open System.Text.RegularExpressions
open Nethereum.Web3
open Nethereum.Web3.Accounts
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs
open AbiTypeProvider.Common
open Domain

type Token =
    { Symbol: string;
      Address: string;
      Decimals: int; }

type Exchange = OneInch = 0 | ZeroEx = 1

let approveAsync (web3: Web3) chainId amount tokenAddress = async {
    printfn "Started approving"
    let (contractAddress, data, value, gasPrice) = OneInch.getApproveData chainId amount tokenAddress
    printfn "Received approve calldata"
    let oneInch = Contracts.OneInch(contractAddress, web3)
    let! tx = oneInch.SendTxAsync data (WeiValue(value)) (GasLimit(12450000I)) (GasPrice(gasPrice)) |> Async.AwaitTask
    printfn "Approve transaction executed"
    return HexBigInteger(1I) = tx.Status
}

let tryCastToExchange (strValue: string) =
    try
        let intValue = Convert.ToInt32(strValue)
        Some (enum<Exchange> intValue)
    with _ -> None

let tryCastToInt (strValue: string) =
    try
        Some (Convert.ToInt32(strValue))
    with _ -> None

let tryCastToBigInt (strValue: string) =
    try
        Some (BigInteger.Parse strValue)
    with _ -> None

let tryCastToSlippage (strValue: string) =
    try
        // XXX: check what type of data is accepted by API exchangers.
        // Perhaps we need to transfer an integer.
        let value = Convert.ToDecimal(strValue)
        if value < 0m || value > 100m then None
        else Some value
    with _ -> None

let readLineWithMessage message =
    printf message
    Console.ReadLine()

let getPrivateKey () =
    readLineWithMessage "Input private key: "

let getProjectId () =
    readLineWithMessage "Input Infura project ID: "

let getUserAddress () =
    readLineWithMessage "Input your address: "

let rec getChainId () =
    let chainIdOp =
        readLineWithMessage "Input chain ID (1 - Ethereum mainnet, 137 - Polygon/Matic): "
        |> tryCastToInt
    match chainIdOp with
    | Some chainId -> chainId
    | None -> getChainId ()

let rec getExchange () =
    let exchangeOp =
        readLineWithMessage "Available exchangers:\n0 - 1Inch\n1 - ZeroEx (Matcha)\nInput id of exhange: "
        |> tryCastToExchange
    match exchangeOp with
    | Some exchange -> exchange
    | None -> getExchange ()

let rec getAmount () =
    let amountOp =
        readLineWithMessage "Input amount: "
        |> tryCastToBigInt
    match amountOp with
    | Some amount -> amount
    | None -> getAmount ()

// TODO: fix this function
let isValidAddress address =
    let regex = Regex("/^0x[a-fA-F0-9]{40}$/")
    regex.IsMatch(address)

let getFromTokenAddress () =
    // TODO: validate received address
    readLineWithMessage "Input selling token address: "

let getToTokenAddress () =
    // TODO: validate received address
    readLineWithMessage "Input buying token address: "

let rec getSlippage () =
    let amountOp =
        readLineWithMessage "Input slippage percents: "
        |> tryCastToSlippage
    match amountOp with
    | Some amount -> amount
    | None -> getSlippage ()

let execute1InchSwapAsync
    (web3: Web3)
    (chainId: int)
    (amount: BigInteger)
    (fromTokenAddress: string)
    (toTokenAddress: string)
    (userAddress: string)
    (slippage: decimal) = async {
    try
        let! isApproved = approveAsync web3 chainId amount fromTokenAddress
        if isApproved then
            printfn "Approved"
            let (contractAddress, data, value, gas, gasPrice) =
                OneInch.getSwapData
                    chainId
                    None
                    fromTokenAddress
                    toTokenAddress
                    userAddress
                    amount
                    slippage
            let oneInch = Contracts.OneInch(contractAddress, web3)
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
}

[<EntryPoint>]
let main argv =
    let privateKey = getPrivateKey ()
    let userAddress = getUserAddress ()
    let projectId  = getProjectId ()
    let chainId  = getChainId ()
    let exchange = getExchange ()
    let amount = getAmount ()
    let fromTokenAddress = getFromTokenAddress ()
    let toTokenAddress = getToTokenAddress ()
    let slippage = getSlippage ()

    // TODO: change URI depending on the chain ID.
    let nodeURI = $"https://polygon-mainnet.infura.io/v3/{projectId}"

    let web3 = Web3(Account(privateKey, 137I), nodeURI)

    match exchange with
    | Exchange.OneInch ->
        execute1InchSwapAsync
            web3
            chainId
            amount
            fromTokenAddress
            toTokenAddress
            userAddress
            slippage
        |> Async.RunSynchronously
    // TODO: implement ZeroEx swap execution.
    | Exchange.ZeroEx ->
        ()
    | _ -> printfn "Unknown exchange type"
    Console.ReadLine() |> ignore
    0
