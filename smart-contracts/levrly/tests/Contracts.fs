module Contracts

open System
open Nethereum.Web3
// open AbiTypeProvider.Common
open Infrastructure

// type Contracts = SolidityTypes<"./ABIs">
type Contracts = AbiTypeProvider.AbiTypes<"./ABIs">

type LendingPool = Contracts.LendingPoolContract
type CreditDelegationToken = Contracts.ICreditDelegationTokenContract
type ProtocolDataProvider = Contracts.ProtocolDataProviderContract
type LendingPoolAddressesProvider = Contracts.LendingPoolAddressesProviderContract
type ERC20 = Contracts.ERC20Contract
type DAI = Contracts.DaiContract
type MockPriceOracle = Contracts.MockPriceOracleContract
type ZeroEx = Contracts.ZeroExContract
type OneInch = Contracts.OneInchContract

let lendingPool (ctx: TestContext) = 
    let address = configuration.Addresses.AaveLendingPool
    LendingPool(address, ctx.Web3)

let protocolDataProvider (ctx: TestContext) = 
    let address = "0x057835Ad21a177dbdd3090bB1CAE03EaCF78Fc6d"
    ProtocolDataProvider(address, ctx.Web3)

let dai (ctx: TestContext) = 
    let address = configuration.Addresses.Dai
    DAI(address, ctx.Web3)

let aDai (ctx: TestContext) = 
    let address = configuration.Addresses.aDai
    ERC20(address, ctx.Web3)

let snx (ctx: TestContext) =
    let address = configuration.Addresses.Snx
    ERC20(address, ctx.Web3)

let erc20 (ctx: TestContext) address =
    ERC20(address, ctx.Web3)

let priceOracleAt (ctx: TestContext) (address: string) =
    MockPriceOracle(address, ctx.Web3)
let lendingPoolAddressProvider (ctx: TestContext) = 
    let address = "0xB53C1a33016B2DC2fF3653530bfF1848a515c8c5"
    LendingPoolAddressesProvider(address, ctx.Web3)

let lendingPoolAddressProviderAt (ctx: TestContext) (address: string) = 
    LendingPoolAddressesProvider(address, ctx.Web3)
