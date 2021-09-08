module Infrastructure

#nowarn "25"

open System
open System.IO
open System.Net.Http
open System.Numerics
open Microsoft.FSharp.Control
open Newtonsoft.Json
open Nethereum
open Nethereum.Contracts
open Nethereum.Hex.HexTypes
open Nethereum.JsonRpc.Client
open Nethereum.Web3
open Nethereum.Web3.Accounts
open Nethereum.RPC.Eth.DTOs
open Nethereum.RPC
open ContractDeployment

/// Common configuration values.
let configration = 
  {|Addresses = 
      {|AaveLendingPool = "0x7d2768dE32b0b80b7a3454c06BdAc94A69DDc7A9"
        AaveProtocolDataProvider = "0x057835Ad21a177dbdd3090bB1CAE03EaCF78Fc6d"
        AavePriceOracle = "0xA50ba011c48153De246E5192C8f9258A2ba79Ca9"
        Dai = "0x6b175474e89094c44da98b954eedeac495271d0f"
        aDai = "0x028171bCA77440897B824Ca71D1c56caC55b68A3"
        Snx = "0xC011a73ee8576Fb46F5E1c5751cA3B9Fe0af2a6F"|}
    AavePriceOracleOwnerAddress = "0xee56e2b3d491590b5b31738cc34d5232f378a8d5"
    //TODO: Find a way to get keys from hardhat.
    AccountAddress0 = "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266"
    AccountAddress1 = "0x70997970c51812dc3a010c7d01b50e0d17dc79c8"
    AccountPrivateKey0 = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
    AccountPrivateKey1 = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d"|}

/// Equal of 'uint(-1)' in solidity.
let uint256MaxValue = 
    115792089237316195423570985008687907853269984665640564039457584007913129639935I

let private hexBigInt (n: uint64) = HexBigInteger(BigInteger(n))

type private EvmSnapshot(client) = inherit RpcRequestResponseHandlerNoParam<string>(client, "evm_snapshot")

type HardhatForkInput() =
    [<JsonProperty(PropertyName = "jsonRpcUrl")>]
    member val JsonRpcUrl = "" with get, set
    [<JsonProperty(PropertyName = "blockNumber")>]
    member val BlockNumber = 0UL with get, set

type HardhatResetInput() =
    [<JsonProperty(PropertyName = "forking")>]
    member val Forking = HardhatForkInput() with get, set

type HardhatReset(client) = 
    inherit RpcRequestResponseHandler<bool>(client, "hardhat_reset")

    member __.SendRequestAsync (input:HardhatResetInput) (id:obj) = base.SendRequestAsync(id, input);


type EthereumConnection(nodeURI: string, privKey: string) =
    
    // this is needed to reset nonce.
    let getWeb3Unsigned () = (Web3(nodeURI))
    let getWeb3 () = Web3(Account(privKey), nodeURI)
    
    let mutable web3Unsigned = getWeb3Unsigned ()
    let mutable web3 = getWeb3 ()
    
    member val public Gas = hexBigInt 9500000UL
    member val public GasPrice = hexBigInt 8000000000UL

    member this.Account with get() = web3.TransactionManager.Account
    member this.Web3 with get() = web3
    member this.Web3Unsigned with get() = web3Unsigned
    member this.GetWeb3() = web3
    member this.GetWeb3Unsigned() = web3Unsigned

    member this.TimeTravel seconds =
        this.Web3.Client.SendRequestAsync(method = "evm_increaseTime", paramList = [| seconds |]) 
        |> Async.AwaitTask
        |> Async.RunSynchronously
        this.Web3.Client.SendRequestAsync(method = "evm_mine", paramList = [||]) 
        |> Async.AwaitTask
        |> Async.RunSynchronously

    member this.GetEtherBalance address = 
        this.Web3.Eth.GetBalance.SendRequestAsync(address) |> Async.AwaitTask
        // hexBigIntResult.Value

    member this.SendEtherAsync address (amount:BigInteger) =
        let transactionInput =
            TransactionInput(
                "", 
                address, 
                this.Account.Address, 
                hexBigInt 9500000UL, 
                hexBigInt 1000000000UL, 
                HexBigInteger(amount))
        this.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput, null)

    member this.ImpersonateAccountAsync (address:string) =
        this.Web3.Client.SendRequestAsync(RpcRequest(0, "hardhat_impersonateAccount", address)) 
        |> Async.AwaitTask

    member this.StopImpersonatingAccountAsync (address: string) =
        this.Web3.Client.SendRequestAsync(RpcRequest(0, "hardhat_stopImpersonatingAccount", address)) 
        |> Async.AwaitTask

    member this.MakeImpersonatedCallAsync 
        // weiValue 
        // gasLimit 
        // gasPrice 
        // addressFrom 
        // addressTo 
        (input:TransactionInput) =
        async {
            do! this.ImpersonateAccountAsync input.From

            let! txr = 
                this.Web3Unsigned.TransactionManager
                    .SendTransactionAndWaitForReceiptAsync(input, tokenSource = null)
                    |> Async.AwaitTask
            do! this.StopImpersonatingAccountAsync input.From
            return txr
        }

    member this.HardhatResetAsync =
        let input = 
            HardhatResetInput(
                Forking=
                    HardhatForkInput(
                        BlockNumber=12330245UL,
                        JsonRpcUrl="https://eth-mainnet.alchemyapi.io/v2/5VaoQ3iNw3dVPD_PNwd5I69k3vMvdnNj"))
        
        HardhatReset(this.Web3.Client).SendRequestAsync input None

    member this.DeployContract (abi: Abi) (constructorParams: list<#obj>) =
        let constructorParams = 
            constructorParams 
            |> List.map (fun x -> x :> obj)
            |> Array.ofList 
        this.Web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            abi.AbiString,
            abi.BytecodeString,
            from = this.Account.Address,
            gas = this.Gas,
            gasPrice = this.GasPrice,
            value = ~~~ 0UL,
            receiptRequestCancellationToken = null,
            values = constructorParams)

type TestContext(privateKey: string, preserveState: bool) =
    let connection = EthereumConnection("http://127.0.0.1:8545/", privateKey)
    
    do if not preserveState then connection.HardhatResetAsync.Wait()

    new() = new TestContext(configration.AccountPrivateKey0, false)


    member _.Web3 = connection.Web3

    member _.Connection = connection

    member _.Address = connection.Account.Address

    interface IDisposable with
        member _.Dispose() = 
            // TODO: Find effective way to call HRE 'hardhat_reset'.
            ()

let withContext (f: TestContext -> unit) = 
    use ctx = new TestContext()
    f ctx

let withContextAsync (f: TestContext -> Async<unit>) = 
    use ctx = new TestContext()
    f ctx |> Async.RunSynchronously

let inNestedContextAsync privateKey (f: TestContext -> Async<'a>) =
    use ctx = new TestContext(privateKey, true)
    f ctx
    
// TODO: Make it work.
let inNestedImpersonateContextAsync address (f: TestContext -> Async<'a>) = async {
    use ctx = new TestContext()
    do! ctx.Connection.ImpersonateAccountAsync address
    let! result = f ctx
    do! ctx.Connection.StopImpersonatingAccountAsync address
    return result
}

let inline deployContract< ^contract when ^contract: (static member FromFile: string with get) >
    (ctx: TestContext)
    (create: string -> ^contract)
    (contructorParams: obj list) = 
    async {
        let abiPath = (^contract: (static member FromFile: string with get) ())
        let! abi = Abi.ParseFromFile abiPath
        let! txr = await ^ ctx.Connection.DeployContract abi contructorParams
        let address = txr.ContractAddress
        if not ^ txr.Failed() then
            return create address
        else
            return (failwith "Transaction failed(")
    }