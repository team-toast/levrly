[<RequireQualifiedAccess>]
module Domain.OneInch

open System.Numerics
open FSharp.Data
open Newtonsoft.Json
open Nethereum.Web3
open Nethereum.RPC.Eth.DTOs
open AbiTypeProvider.Common
open Nethereum.Hex.HexTypes
open Infrastructure

[<Literal>]
let BaseApiUrl = "https://api.1inch.exchange/v3.0/"

[<Literal>]
let DefaultIntermediateAddress = "0x52bc44d5378309ee2abf1539bf71de1b7d7be3b5"

[<Literal>]
let MaximalGasLimit = 12450000

let spoofSellerAddress (newAddress: string) (calldata: string) =
    if calldata.Length < 32 * 300 then calldata // Unoswap call (fromAddress is ignored, uses msg.sender).
    else
        // Swap call.
        // Cutting out the intermediate address from the calldata and insert a @newAddress there.
        let offset1 = 418 // The first place to cut the intermediate address from. Value: 32 bytes * 13 blocks + "0x".length.
        let offset2 = 8936
        let offset3 = 176
        let calldataPart1 = calldata.Substring(0, offset1)
        let calldataPart2 = calldata.Substring(offset1 + DefaultIntermediateAddress.Length - 3, offset2) // -3: "0x".length + indexing offset (1)
        let calldataPart3 = calldata.Substring(offset1 + offset2 + (DefaultIntermediateAddress.Length * 2) - 5, offset3)
        let newAddress' = newAddress.Replace("0x", System.String.Empty)
        System.String.Concat(calldataPart1, newAddress', calldataPart2, newAddress', calldataPart3)

let getSwapData chainId intermediateAddress fromTokenAddress toTokenAddress fromAddress amount slippage =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}{chainId}/swap", httpMethod = "GET",
                query   =
                    [ "fromTokenAddress", fromTokenAddress;
                      "toTokenAddress", toTokenAddress;
                      "fromAddress", intermediateAddress |> Option.defaultValue fromAddress;
                      "amount", amount.ToString();
                      "destReceiver", fromAddress;
                      "slippage", slippage.ToString() ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>

    let tx = json.["tx"]
    let toAddress = string tx.["to"]
    let value = tx.Value<string>("value") |> BigInteger.Parse
    let gas = tx.Value<int>("gas") |> bigint
    let gasPrice = tx.Value<string>("gasPrice") |> BigInteger.Parse
    let data =
        match intermediateAddress with
        | Some _ -> string tx.["data"] |> spoofSellerAddress fromAddress
        | None -> string tx.["data"]

    (toAddress, data, value, gas, gasPrice)

let getApproveData chainId amount tokenAddress =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}{chainId}/approve/calldata", httpMethod = "GET",
                query   =
                    [ "amount", amount.ToString();
                      "tokenAddress", tokenAddress ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>

    let toAddress = string json.["to"]
    let data = string json.["data"]
    let value = json.Value<string>("value") |> BigInteger.Parse
    let gasPrice = json.Value<string>("gasPrice") |> BigInteger.Parse

    (toAddress, data, value, gasPrice)

let approveAsync (web3: Web3) chainId amount tokenAddress = async {
    let (contractAddress, data, value, gasPrice) = getApproveData chainId amount tokenAddress
    let oneInch = Contracts.OneInch(contractAddress, web3)
    let! tx = oneInch.SendTxAsync data (WeiValue(value)) (GasLimit(12450000I)) (GasPrice(gasPrice)) |> Async.AwaitTask
    return HexBigInteger(1I) = tx.Status
}
