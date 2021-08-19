module Contracts

open System
open Nethereum.Web3
open SolidityProviderNS
open Infrastructure

type Contracts = SolidityTypes<"./tmp">

let lendingPool (ctx: TestContext) = 
    let address = configration.DeployedContractAddresses.LendingPool
    Contracts.LendingPoolContract(address, ctx.Web3)

let dai (ctx: TestContext) = 
    let address = configration.DeployedContractAddresses.Dai
    Contracts.DaiContract(address, ctx.Web3)