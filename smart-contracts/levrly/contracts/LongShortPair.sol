// todo:
// add disclaimer

pragma solidity ^0.5.17;

import "@openzeppelin/contracts/token/ERC20/ERC20Detailed.sol";
import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "./DSMath.sol";
import "./DSProxy.sol";

contract ILongShortPairToken is
    ERC20Detailed,
    ERC20, 
    DSProxy,
    DSMath 
{
    using SafeMath for uint;

    enum Asset 
    { 
        Long, 
        Short
    }

    uint constant public ONE_PERC = 10 ** 16;
    uint constant public ONE_HUNDRED_PERC = 10 ** 18;

    ERC20 public longToken;
    ERC20 public interestToken; // the AAVE interest token to which interest accrues
    ERC20 public shortToken;
    ERC20 public debtToken;     // the AAVE debt token to which short accrues

    // Ratios
    // The PERC suffix indicates that the ratio is expressed as a percentage
    uint public minRedemptionRatioPERC; 
    uint public lowerRatioPERC;
    uint public targetRatioPERC;
    uint public upperRatioPERC;

    // Leverage fees
    uint public automationFeePERC;
    uint public protocolFeePERC;
    uint public issuerFeePERC;
    
    // Trading fees
    uint public withinRangeFeePERC; // the fee charged if the trade is beneficial to the target ratio
    uint public aboveRangeFeePERC;  // 
    uint public belowRangeFeePERC;
    uint public panicRangeFeePERC;

    address public gulper;
    address public issuerFeeAccount;

    // can be issued with long, short, or interest tokens
    function issue(Asset _assetSupplied, address _receiver, uint _amount) public;
    // unsure if these should be hidden inside of the issue function
    function issueWithLong(address _receiver, uint _longAmount) public;
    function issueWithShort(address _receiver, uint _shortAmount) public;

    // can redeem to long, short, or interest tokens
    function redeem(Asset _assetRequested, address _receiver, uint _amount) public;
    // unsure if these should be hidden inside of the redeem function
    function redeemToLong(address _receiver, uint _amount) public;
    function redeemToShort(address _receiver, uint _amount) public;
    
    // allows trading of coins by external party
    // TODO: May need to refine this to allow minimum slippage
    function trade(Asset _inAsset, uint _amountIn, uint _minAmountOut, uint _expirationTime) public;
    // unsure if these should be hidden inside of the trade function
    function tradeCollatralForShort(address _receiver, uint _longAmount, uint _expirationTime) public;
    function tradeShortForLong(address _receiver, uint _shortAmount, uint _expirationTime) public;
    
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
            uint _issuerFee,
            uint _actualLongAdded,
            uint _accreditedLong,
            uint _tokensIssued);

    function calculateIssuanceAmount_UsingShort(uint _shortAmount) 
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _issuerFee,
            uint _actualLongAdded,
            uint _accreditedLong,
            uint _tokensIssued);
    
    function calculateRedemptionAmount_ForLong(uint _amount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _issuerFee,
            uint _longRedeemed,
            uint _longReturned);
    
    function calculateRedemptionAmount_ForShort(uint _amount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _issuerFee,
            uint _shortRedeemed,
            uint _shortReturned);


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

    event Trade(
        Asset _inAsset,
        Asset _outAsset,
        address _receiver,
        uint _inAmount,
        uint _outAmount);

    event ChangedSettings(
        uint _redemptionRatioLimit,
        uint _lowerRatio,
        uint _targetRatio,
        uint _upperRatio,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee);
}

// Naming convention for pairs:
// lLONGsSHORTxLEVERAGE
// examples:
// lDAIsETHx2
// lETHsWBTCx3