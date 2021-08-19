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

/// Common configuration values.
let configration = 
  {|DeployedContractAddresses = 
      {|LendingPool = "0x7d2768dE32b0b80b7a3454c06BdAc94A69DDc7A9"
        Dai = "0x6b175474e89094c44da98b954eedeac495271d0f"|}
    //TODO: Find a way to get keys from hardhat.
    AccountPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"|}


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

    member this.MakeImpersonatedCallAsync 
        weiValue 
        gasLimit 
        gasPrice 
        addressFrom 
        addressTo 
        (functionArgs:#FunctionMessage) =
        async {
            do! this.ImpersonateAccountAsync addressFrom

            let txInput = functionArgs.CreateTransactionInput(addressTo)
            
            txInput.From <- addressFrom
            txInput.Gas <- gasLimit
            txInput.GasPrice <- gasPrice
            txInput.Value <- weiValue

            return! this.Web3Unsigned.TransactionManager
                .SendTransactionAndWaitForReceiptAsync(txInput, tokenSource = null)
                |> Async.AwaitTask
        }
       
    member this.MakeImpersonatedCallWithNoEtherAsync addressFrom addressTo (functionArgs:#FunctionMessage) = 
        this.MakeImpersonatedCallAsync (hexBigInt 0UL) (hexBigInt 9500000UL) (hexBigInt 0UL) addressFrom addressTo functionArgs
    
    member this.HardhatResetAsync =
        let input = 
            HardhatResetInput(
                Forking=
                    HardhatForkInput(
                        BlockNumber=12330245UL,
                        JsonRpcUrl="https://eth-mainnet.alchemyapi.io/v2/5VaoQ3iNw3dVPD_PNwd5I69k3vMvdnNj"))
        
        HardhatReset(this.Web3.Client).SendRequestAsync input None

type TestContext() =
    let connection = EthereumConnection("http://127.0.0.1:8545/", configration.AccountPrivateKey)
    let mutable disposing = false
    member _.Web3 = connection.Web3

    member _.Connection = connection

    interface IDisposable with
        member _.Dispose() = 
            // TODO: Find effective way to call HRE 'hardhat_reset'.
            connection.HardhatResetAsync.Wait()

let withContext (f: TestContext -> unit) = 
    use ctx = new TestContext()
    f ctx

let withContextBind (f: TestContext -> Async<unit>) = 
    use ctx = new TestContext()
    f ctx |> Async.RunSynchronously
    