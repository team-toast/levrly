open System.IO
open System.Net.Http

#nowarn "25"

let inline (^) f x = f x

let configration =  {|
    abiFiles = [ 
        "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/LendingPool.json"
        "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/ReentrancyGuard.json"
        "https://raw.githubusercontent.com/aave/aave-protocol/master/build/contracts/VersionedInitializable.json"
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
    [ for abiFile in configration.abiFiles -> async {
        use http = new HttpClient()
        let fileName = Path.GetFileName(abiFile)
        let path = Path.Join(configration.infrastructureContractsDirectory, fileName)
        let! response = http.GetAsync(abiFile) |> Async.AwaitTask
        use file = File.Create(path)
        do! response.Content.CopyToAsync(file) |> Async.AwaitTask
    }]


let setupInfrastructure = async {
    ensureDirectoryExists configration.infrastructureContractsDirectory
    let! x = Async.Parallel downloadFiles in ignore x
    }

Async.RunSynchronously setupInfrastructure