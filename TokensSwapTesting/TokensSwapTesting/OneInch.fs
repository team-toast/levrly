module OneInch

open System.Numerics
open FSharp.Data
open Newtonsoft.Json

[<Literal>]
let BaseApiUrl = "https://api.1inch.exchange/v3.0/137/"

let getSwapData fromTokenAddress toTokenAddress fromAddress amount slippage  =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}swap", httpMethod = "GET",
                query   =
                    [ "fromTokenAddress", fromTokenAddress;
                      "toTokenAddress", toTokenAddress;
                      "fromAddress", fromAddress;
                      "amount", amount.ToString();
                      "allowPartialFill", "true";
                      "slippage", slippage.ToString() ],
                headers = [ "Accept", "application/json" ])
        |> JsonConvert.DeserializeObject<Linq.JObject>
    let tx = json.["tx"]
    let toAddress = string tx.["to"]
    let data = string tx.["data"]
    let value = tx.Value<string>("value") |> BigInteger.Parse
    let gas = tx.Value<int>("gas") |> bigint
    let gasPrice = tx.Value<string>("gasPrice") |> BigInteger.Parse
    (toAddress, data, value, gas, gasPrice)

let approve amount tokenAddress =
    let json = 
        Http.RequestString
            ( $"{BaseApiUrl}approve/calldata", httpMethod = "GET",
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
