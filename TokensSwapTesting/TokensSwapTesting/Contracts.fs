module Contracts

type Contracts = AbiTypeProvider.AbiTypes<"./ABIs">

type ZeroEx = Contracts.ZeroExContract
type OneInch = Contracts.OneInchContract
