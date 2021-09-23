[<RequireQualifiedAccess>]
module Domain.ZeroEx

open FSharp.Data
open Newtonsoft.Json
open System.Numerics

let generateBaseApiUrl chainId =
    let prefix =
        match chainId with
        | 3 -> "ropsten."
        | 56 -> "bsc."
        | 137 -> "polygon."
        | 43114 -> "avalanche."
        | _ -> System.String.Empty
    $"https://{prefix}api.0x.org/"

let getSwapData chainId (buyTokenSymbol: string) (sellTokenSymbol:string) (sellAmount: bigint) =
    let baseApiUrl = generateBaseApiUrl chainId

    let json = 
        Http.RequestString
            ( $"{baseApiUrl}swap/v1/quote", httpMethod = "GET",
                query   =
                    [ "buyToken", buyTokenSymbol;
                      "sellToken", sellTokenSymbol;
                      "sellAmount", sellAmount.ToString() ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>
    let toAddress = string json.["to"]
    let data = string json.["data"]
    let value = json.Value<string>("value") |> BigInteger.Parse
    let gas = json.Value<string>("gas") |> BigInteger.Parse
    let gasPrice = json.Value<string>("gasPrice") |> BigInteger.Parse
    (toAddress, data, value, gas, gasPrice)
