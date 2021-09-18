[<RequireQualifiedAccess>]
module Domain.ZeroEx

open FSharp.Data
open Newtonsoft.Json

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
    (json.GetValue("to").ToString(), json.GetValue("data").ToString())