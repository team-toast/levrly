[<RequireQualifiedAccess>]
module Domain.OneInch

open FSharp.Data
open Newtonsoft.Json
open System.Numerics

[<Literal>]
let BaseApiUrl = "https://api.1inch.exchange/v3.0/1/"

let getSwapData fromTokenAddress toTokenAddress fromAddress amount slippage  =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}swap", httpMethod = "GET",
                query   =
                    [ "fromTokenAddress", fromTokenAddress;
                      "toTokenAddress", toTokenAddress;
                      "fromAddress", fromAddress;
                      "amount", amount.ToString();
                      "slippage", slippage.ToString() ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>
    let tx = json.["tx"]
    let toAddress = string tx.["to"]
    let data = string tx.["data"]
    let value = tx.["value"].ToObject<int64>()
    let gas = tx.Value<bigint>("gas")
    let gasPrice = tx.["gasPrice"].ToObject<int64>()
    (toAddress, data, value, gas, gasPrice)
