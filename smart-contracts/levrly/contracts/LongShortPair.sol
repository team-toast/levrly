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

contract IPriceOracleGetter {
    function getAssetPrice(address _asset) 
        external 
        view 
        returns (uint256);
    function getAssetsPrices(address[] calldata _assets) 
        external 
        view 
        returns(uint256[] memory);
    function getSourceOfAsset(address _asset) 
        external 
        view 
        returns(address);
    function getFallbackOracle() 
        external 
        view 
        returns(address);
}

contract ILongShortPairToken
{
    using SafeMath for uint;

    // TODO: mve this to state
    ILendingPool public lendingPool;
    IPriceOracleGetter public priceOracle;

    // can be issued with long, short, or interest tokens
    function issue(
            Types.Asset _assetSupplied, 
            address _receiver, 
            uint _amount) 
        public;
    // can redeem to long, short, or interest tokens
    function redeem(
            Types.Asset _assetRequested, 
            address _receiver, 
            uint _amount) 
        public;
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
    using SafeMath for uint;

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
            uint _settlementRewardPERC,
            
            // AAVE price oracle
            IPriceOracleGetter _priceOracle,
            ILendingPool _lendingPool)
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

        priceOracle = _priceOracle;
        lendingPool = _lendingPool;
    }

    // issues the LSP by providing the longToken
    function issue( 
            address _receiver, 
            uint _amount) 
        public
    {
        (uint protocolFee,
        uint automationFee,
        uint actualLongAdded,
        uint accreditedLong,
        uint tokensIssued) = calculateIssuanceAmount_UsingLong(_amount);

        longToken.transferFrom(msg.sender, address(this), _amount);
        lendingPool.deposit(address(longToken), _amount, address(this), 0);    
        interestToken.transfer(gulper, protocolFee);

        emit Issued(
            _receiver, 
            _amount,
            protocolFee,
            automationFee,
            protocolFee,
            actualLongAdded,
            accreditedLong,
            tokensIssued);
    }

    function calculateIssuanceAmount_UsingLong(uint _longAmount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _actualLongAdded,
            uint _accreditedLong,
            uint _tokensIssued)
    {
        _protocolFee = _longAmount
            .mul(protocolFeePERC)
            .div(ONE_HUNDRED_PERC);

        _automationFee = _longAmount
            .mul(automationFeePERC)
            .div(ONE_HUNDRED_PERC);

        _actualLongAdded = _longAmount.sub(_protocolFee);

        _accreditedLong = _actualLongAdded.sub(_automationFee);

        _tokensIssued = _actualLongAdded
            .mul(ONE_HUNDRED_PERC)
            .div(getExcessCollateral())
            .div(ONE_HUNDRED_PERC);
    }

    function getExcessCollateral()
        public
        view
        returns (uint _excessCollateral)
    {
        (,,,,_excessCollateral) = getCollateral();
    }

    function getCollateral()
        public
        view
        returns (
            uint _collateralPriceWEI,
            uint _collateralBalance,
            uint _debtPriceWEI,
            uint _debtBalance,
            uint _excessCollateral)
    {
        _collateralPriceWEI = priceOracle.getAssetPrice(address(longToken));
        _collateralBalance = longToken.balanceOf(address(this));
        _debtPriceWEI = priceOracle.getAssetPrice(address(shortToken));
        _debtBalance = shortToken.balanceOf(address(this));

        uint collateralValue = _collateralBalance.mul(_collateralPriceWEI);
        uint debtValue = _debtBalance.mul(_debtPriceWEI);
        _excessCollateral = collateralValue.sub(debtValue).div(_collateralPriceWEI);
    }
}

contract ILendingPool
{
    function deposit(
            address asset,
            uint256 amount,
            address onBehalfOf,
            uint16 referralCode) 
        external;

    function withdraw(
            address asset,
            uint256 amount,
            address to) 
        public 
        returns (uint256);

    function borrow(
            address asset,
            uint256 amount,
            uint256 interestRateMode,
            uint16 referralCode,
            address onBehalfOf) 
        external;

    function repay(
            address asset,
            uint256 amount,
            uint256 rateMode,
            address onBehalfOf) 
        external returns (uint256);
}

// Naming convention for pairs:
// lLONGsSHORTxLEVERAGE
// examples:
// lDAIsETHx2
// lETHsWBTCx3