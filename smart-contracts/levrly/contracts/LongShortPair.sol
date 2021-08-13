// todo:
// add disclaimer

pragma solidity ^0.5.17;

import "@openzeppelin/contracts/token/ERC20/ERC20Detailed.sol";
import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "./DSMath.sol";
import "./DSProxy.sol";
import "./LongShortPairState.sol";

// Ideas to make this less work:
// 1. make the automationFee fixed
// 2. make the settlementFee fixed
 
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

contract LongShortPairToken is
    ILongShortPairToken,
    LongShortPairState,
    ERC20Detailed,
    ERC20, 
    DSProxy,
    DSMath 
{
    constructor(
            // names
            string memory _code,
            string memory _name,

            address _DSProxyCache,

            // what we're longing and what we're shorting
            IERC20 _longToken,
            IERC20 _interestToken,
            IERC20 _shortToken,
            IERC20 _debtToken,

            // what the ratios are
            uint _redemptionRatioLimitPERC,     // target - 40 => 160
            uint _lowerRatioPERC,               // target - 20 => 180
            uint _targetRatioPERC,              // target      => 200
            uint _upperRatioPERC,               // target + 20 => 220
            /*
            uint _lowerDefiSaverRatio           // _lowerRatio - 10 => 170
            uint _upperDefiSaverRatio           // _upperRatio + 10 => 230
            */

            // what the issuance fees are
            uint _automationFeePERC,

            // the reward for resolving the excess debt.
            uint _settlementRewardPERC)
        public
            DSProxy(_DSProxyCache) //_proxyCache on mainnet
            ERC20Detailed(_name, _code, 18)
    {
        // set the token variables
        longToken = _longToken;
        interestToken = _interestToken;
        shortToken = _shortToken;
        debtToken = _debtToken;

        // set the ratio variables
        redemptionRatioLimitPERC = _redemptionRatioLimitPERC;
        lowerRatioPERC = _lowerRatioPERC;
        targetRatioPERC = _targetRatioPERC; 
        upperRatioPERC = _upperRatioPERC;

        // set the issuance fee variables
        automationFeePERC = _automationFeePERC;
        protocolFeePERC = 9 * 10**15;

        // set the settlement reward fee percentage
        settlementRewardPERC = _settlementRewardPERC;

        // set the fee account(s)
        gulper = address(1);
    }
}

// Naming convention for pairs:
// lLONGsSHORTxLEVERAGE
// examples:
// lDAIsETHx2
// lETHsWBTCx3