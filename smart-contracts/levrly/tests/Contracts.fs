module Contracts

open System
open Nethereum.Web3
open SolidityProviderNS
open Infrastructure

type Contracts = SolidityTypes<"./ABIs">

type LendingPool = Contracts.LendingPoolContract
type ERC20 = Contracts.ERC20Contract
type DAI = Contracts.DaiContract

let lendingPool (ctx: TestContext) = 
    let address = configration.DeployedContractAddresses.LendingPool
    LendingPool(address, ctx.Web3)

let dai (ctx: TestContext) = 
    let address = configration.DeployedContractAddresses.Dai
    DAI(address, ctx.Web3)

let aDai (ctx: TestContext) = 
    let address = configration.DeployedContractAddresses.aDai
    ERC20(address, ctx.Web3)