[<RequireQualifiedAccess>]
module Domain.ZeroEx

open FSharp.Data
open Newtonsoft.Json
open System.Numerics

[<Literal>]
let baseApiUrl = "https://api.0x.org/swap/v1/"

let getSwapData (buyTokenSymbol: string) (sellTokenSymbol:string) (sellAmount: bigint) =
    let json = 
        Http.RequestString
            ( $"{baseApiUrl}quote", httpMethod = "GET",
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