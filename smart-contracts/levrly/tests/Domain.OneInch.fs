[<RequireQualifiedAccess>]
module Domain.OneInch

open System.Numerics
open FSharp.Data
open Newtonsoft.Json

[<Literal>]
let BaseApiUrl = "https://api.1inch.exchange/v3.0/1/"

[<Literal>]
let IntermediateAddress = "0x52bc44d5378309ee2abf1539bf71de1b7d7be3b5"

let spoofSellerAddress (newAddress: string) (calldata: string) =
    if calldata.Length < 32 * 300 then calldata // Unoswap call (fromAddress is ignored, uses msg.sender).
    else
        // Swap call.
        // Cutting out the intermediate address from the calldata and insert a @newAddress there.
        let offset1 = 418 // The first place to cut the intermediate address from. Value: 32 bytes * 13 blocks + "0x".length.
        let offset2 = 8936
        let offset3 = 176
        let calldataPart1 = calldata.Substring(0, offset1)
        let calldataPart2 = calldata.Substring(offset1 + IntermediateAddress.Length - 3, offset2) // -3: "0x".length + indexing offset (1)
        let calldataPart3 = calldata.Substring(offset1 + offset2 + (IntermediateAddress.Length * 2) - 5, offset3)
        let newAddress' = newAddress.Replace("0x", System.String.Empty)
        System.String.Concat(calldataPart1, newAddress', calldataPart2, newAddress', calldataPart3)

let getSwapData fromTokenAddress toTokenAddress fromAddress amount slippage  =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}swap", httpMethod = "GET",
                query   =
                    [ "fromTokenAddress", fromTokenAddress;
                      "toTokenAddress", toTokenAddress;
                      "fromAddress", IntermediateAddress;
                      "amount", amount.ToString();
                      "slippage", slippage.ToString() ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>
    let tx = json.["tx"]
    let toAddress = string tx.["to"]
    let data = string tx.["data"] |> spoofSellerAddress fromAddress
    let value = tx.Value<string>("value") |> BigInteger.Parse
    let gas = tx.Value<int>("gas") |> bigint
    let gasPrice = tx.Value<string>("gasPrice") |> BigInteger.Parse
    (toAddress, data, value, gas, gasPrice)
