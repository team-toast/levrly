[<AutoOpen>]
module Operators

open System.Numerics
open Nethereum.Hex.HexTypes

let inline (^) f x = f x

let (~~) (n: uint64) = BigInteger(n)
let (~~~) (n: uint64) = HexBigInteger(BigInteger(n))

let inline await t = Async.AwaitTask t
