// todo:
// add disclaimer

pragma solidity ^0.5.17;

import "@openzeppelin/contracts/token/ERC20/ERC20Detailed.sol";
import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "./DSMath.sol";
import "./DSProxy.sol";
import "./LongShortPairState.sol";

// TODO : automation fee should be optional if the user moves the ratio closer to the targetRatio.

// Ideas to make this less work:
// 1. move the range to a fixed 10% above, 10% below.
// 2. remove the issuer concept until a later version
// 3. make the automationFee fixed
// 4. make the settlementFee fixed
 
contract ILongShortPairToken
{
    using SafeMath for uint;

    // can be issued with long, short, or interest tokens
    function issue(Types.Asset _assetSupplied, address _receiver, uint _amount) public;
    // can redeem to long, short, or interest tokens
    function redeem(Types.Asset _assetRequested, address _receiver, uint _amount) public;
    // calculates the details of swapping.
    function swap(
            Types.Asset _providedAsset,     // what we're giving
            Types.Asset _requestedAsset,    // what we're expecting
            uint _amountProvided,           // how much msg.sender is giving
            uint _minAmountRequested,       // minimum amount msg.sender is expecting
            uint _expiryTime)               // how long the swap is good for
        public;

    function calculateSwapBreakDown(
            Types.Asset _providedAsset, 
            Types.Asset _requestedAsset,
            uint _amountProvided) 
        public 
        returns (
            uint _, 
            uint _shortAmount, 
            uint _interestAmount);

    // msg.sender probives all the excessive bedt in short tokens in return for long tokens 
    // + a fee for the debt below the lower bound
    function deleverAllExcessiveDebt() public;
    // msg.sender provides all the excessive debt in long tokens in return for short tokens
    // + a fee for the collateral above the upper bound
    function leverAllExcessiveCollateral() public;

    // price of the long token as a WAD decimal, denominated in the short token
    function priceOfLongInShortWAD() public view returns (uint);
    // price of the short token as a WAD decimal, denominated in the long token
    function priceOfShortInLongWAD() public view returns (uint);

    function changeSettings(
            uint _redemptionRatioLimit, 
            uint _lowerRatio, 
            uint _targetRatio, 
            uint _upperRatio,
            uint _automationFee) 
        public;

    function getRatio() public view returns (uint _ratio);
    function getInfo() 
        public 
        view 
        returns (
            uint _longPrice, 
            uint _longAmount, 
            uint _shortPrice, 
            uint _shortAmount,
            uint _longBalanceDenomenatedInShort,
            uint _excessLong);
    
    function calculateIssuanceAmount_UsingLong(uint _longAmount) 
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _actualLongAdded,
            uint _accreditedLong,
            uint _tokensIssued);

    function calculateIssuanceAmount_UsingShort(uint _shortAmount) 
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _actualLongAdded,
            uint _accreditedLong,
            uint _tokensIssued);
    
    function calculateRedemptionAmount_ForLong(uint _amount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _longRedeemed,
            uint _longReturned);
    
    function calculateRedemptionAmount_ForShort(uint _amount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _shortRedeemed,
            uint _shortReturned);

    // TODO : extend this to allow for a user supplying debt
    event Issued(
        address _receiver, 
        uint _suppliedLong,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee,
        uint _actualLongAdded,
        uint _accreditedLong,
        uint _tokensIssued);
    
    event Redeemed(
        address _redeemer,
        address _receiver, 
        uint _tokensRedeemed,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee,
        uint _longRedeemed,
        uint _longReturned);

    event ChangedSettings(
        uint _redemptionRatioLimit,
        uint _lowerRatio,
        uint _targetRatio,
        uint _upperRatio,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee);
}

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

contract LongShortPairToken is
    ILongShortPairToken,
    LongShortPairState,
    ERC20Detailed,
    ERC20, 
    DSProxy,
    DSMath 
{
    constructor(
            // what we're longing and what we're shorting
            IERC20 _longToken,
            IERC20 _interestToken,
            IERC20 _shortToken,
            IERC20 _debtToken,

            // what the ratios are
            uint _redemptionRatioLimitPERC, // target - 40 => 160
            uint _lowerRatioPERC,               // target - 20 => 180
            uint _targetRatioPERC,              // target      => 200
            uint _upperRatioPERC,               // target + 20 => 220
            /*
            uint _lowerDefiSaverRatio       // _lowerRatio - 10 => 170
            uint _upperDefiSaverRatio      // _upperRatio + 10 => 230
            */

            // what the issuance fees are
            uint _automationFeePERC,
            uint _issuerFeePERC,

            // the reward for resolving the excess debt.
            uint _settlementRewardPERC,

            // where the fees go
            // gulper is hardcoded and only settable by Foundry
            address _issuerFeeAccount)
        public
    {
        // set the token variables
        longToken = _longToken;
        interestToken = _interestToken; // should this rather be looked up?
        shortToken = _shortToken;
        debtToken = _debtToken;         // should this rather be looked up?

        // set the ratio variables
        redemptionRatioLimitPERC = _redemptionRatioLimitPERC;
        lowerRatioPERC = _lowerRatioPERC;   // should this just be range variable? IE 10%
        targetRatioPERC = _targetRatioPERC; // should this just be a midpoint? like 150% with ranges being 140%-160% is range is 10%? 
        upperRatioPERC = _upperRatioPERC;

        // set the issuance fee variables
        automationFeePERC = _automationFeePERC;
        protocolFeePERC = 9 * 10**15;

        // set the fee account(s)
        gulper = 0x123;
    }
}

// Naming convention for pairs:
// lLONGsSHORTxLEVERAGE
// examples:
// lDAIsETHx2
// lETHsWBTCx3