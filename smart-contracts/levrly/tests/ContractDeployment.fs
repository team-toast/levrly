module ContractDeployment

open System.IO
open Newtonsoft.Json

type Abi(abiJson: string, bytecode: string) =
    static member ParseFromFile path = async {
        let! text = await ^ File.ReadAllTextAsync path
        let json = JsonConvert.DeserializeObject<Linq.JObject>(text)
        let abi = json.GetValue("abi").ToString()
        let bytecode = json.GetValue("bytecode").ToString()
        return Abi(abi, bytecode)
    }

    member _.AbiString = abiJson
    member _.BytecodeString = bytecode