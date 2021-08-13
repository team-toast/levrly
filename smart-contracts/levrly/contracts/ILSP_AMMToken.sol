pragma solidity ^0.5.0;

import "./LongShortPair.sol";

contract ILSP_AMMToken is ILongShortPairToken
{
    // allows trading of coins by external party
    // TODO: May need to refine this to allow minimum slippage
    function trade(Types.Asset _inAsset, uint _amountIn, uint _minAmountOut, uint _expirationTime) public;
    // unsure if these should be hidden inside of the trade function
    function tradeCollatralForShort(address _receiver, uint _longAmount, uint _expirationTime) public;
    function tradeShortForLong(address _receiver, uint _shortAmount, uint _expirationTime) public;

    // This function returns the exact amount of the short token that would be returned for
    // the given amount of the long token.
    // It will not run if the collateralization ratio is below the target ratio.
    function calculateTradeOutput_LongForShort(uint _longAmount)
        public  
        view
        returns (uint _shortAmount); 
    
    // This function returns the exact amount of the long token that would be returned for
    // the given amount of the short token.
    // It will not run if the collateralization ratio is above the target ratio.
    function calculateTradeOutput_ShortForLong(uint _shortAmount)
        public
        view
        returns (uint _longAmount);

    event Trade(
        Types.Asset _inAsset,
        Types.Asset _outAsset,
        address _receiver,
        uint _inAmount,
        uint _outAmount);
}