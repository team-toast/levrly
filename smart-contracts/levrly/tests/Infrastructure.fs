module Infrastructure

#nowarn "25"

open System
open System.IO
open System.Net.Http
open Nethereum
open Nethereum.Web3

/// Common configuration values.
let configration = {|
    LendingPoolContractAddress = "0x7d2768dE32b0b80b7a3454c06BdAc94A69DDc7A9" 
    //TODO: Find a way to get keys from hardhat.
    AccountPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
    |}

/// Returns Nethereum account object that represents account with given private key (key mast be 
/// base16 string with '0x' prefix).
let web3Account privateKey =
    let keyBytes = 
        privateKey
        |> Seq.skip 2
        |> Seq.windowed 2
        |> Seq.indexed
        |> Seq.where (fun (n, _) -> n % 2 = 0)
        |> Seq.map (fun (_, [|a; b|]) -> 
            let hex = $"{a}{b}"
            let byte = Convert.ToByte(hex, 16)
            byte
            )
        |> Seq.toArray
    let acc = Accounts.Account(keyBytes, Signer.Chain.Private)
    acc
