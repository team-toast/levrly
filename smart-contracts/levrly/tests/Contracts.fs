module Contracts

open System
open Nethereum.Web3
open SolidityProviderNS

type AaveContracts = SolidityTypes<"./tmp">

type TestContext() =
    let acc = Infrastructure.web3Account Infrastructure.configration.AccountPrivateKey
    
    let web3 = Web3(acc, "http://127.0.0.1:8545/")
    
    let lendingPool = 
        AaveContracts.LendingPoolContract(
            Infrastructure.configration.LendingPoolContractAddress,
            web3)

    member _.Web3 = web3
    member _.LendingPool = lendingPool

    interface IDisposable with
        member _.Dispose() = 
            // TODO: Find effective way to call HRE 'hardhat_reset'.
            ()

let withContext (f: TestContext -> unit) = 
    use ctx = new TestContext()
    f ctx