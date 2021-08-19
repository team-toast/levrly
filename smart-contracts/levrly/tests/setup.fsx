open System.IO
open System.Net.Http

#nowarn "25"

let inline (^) f x = f x


let configration =  {|
    abiFiles = [ 
        ("LendingPool.json", "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/LendingPool.json")
        ("ReentrancyGuard.json", "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/ReentrancyGuard.json")
        ("VersionedInitializable.json", "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/VersionedInitializable.json")
        ("Dai.json", "http://api.etherscan.io/api?module=contract&action=getabi&address=0x6b175474e89094c44da98b954eedeac495271d0f&format=raw")
    ]
    infrastructureContractsDirectory = "./tmp/"
|}

/// Recursively creates directory with given path if it not exists.
let ensureDirectoryExists (path: string) =
    let join = List.toArray >> Array.rev >> Path.Combine
    let exists = join >> Directory.Exists 
    let mkdir = Directory.CreateDirectory
    
    let pathSegments = Array.toList ^ Array.rev ^ path.Split(Path.DirectorySeparatorChar)
    
    let rec inner = function
        | [] -> ()
        | path when exists path -> ()
        | f::cf when not ^ exists ^ f::cf -> 
            inner cf
            Directory.CreateDirectory ^ join ^ (f::cf) |> ignore 
            
    inner pathSegments

/// Downloads ABI files for already deployed contracts.
let downloadFiles = 
    [ for filename, address in configration.abiFiles -> async {
        use http = new HttpClient()
        let path = Path.Join(configration.infrastructureContractsDirectory, filename)
        let! response = http.GetAsync(address) |> Async.AwaitTask
        use file = File.Create(path)
        do! response.Content.CopyToAsync(file) |> Async.AwaitTask
    }]


let setupInfrastructure = async {
    ensureDirectoryExists configration.infrastructureContractsDirectory
    let! x = Async.Parallel downloadFiles in ignore x
    }

Async.RunSynchronously setupInfrastructure
printfn "Setup done!"