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

    uint constant public ONE_PERC = 10 ** 16;
    uint constant public ONE_HUNDRED_PERC = 10 ** 18;

    ERC20 public collateralToken;
    ERC20 public debtToken;

    // Ratios
    // The PERC suffix indicates that the ratio is expressed as a percentage
    uint public redemptionRatioLimitPERC; 
    uint public lowerRatioPERC;
    uint public targetRatioPERC;
    uint public upperRatioPERC;

    // Fees
    uint public automationFeePERC;
    uint public protocolFeePERC;
    uint public issuerFeePERC;

    address public gulper;

    function issueWithCollateral(address _receiver, uint _collateralAmount) public;
    function issueWithDebt(address _receiver, uint _debtAmount) public;
    function tradeCollatralForDebt(address _receiver, uint _collateralAmount, uint _expirationTime) public;
    function tradeDebtForCollateral(address _receiver, uint _debtAmount, uint _expirationTime) public;
    function changeSettings(uint _redemptionRatioLimit, uint _lowerRatio, uint _targetRatio, uint _upperRatio) public;

    function getRatio() public view returns (uint _ratio);
    function getCollateralInfo() 
        public 
        view 
        returns (
            uint _collateralPrice, 
            uint _collateralAmount, 
            uint _debtPrice, 
            uint _debtAmount,
            uint _collateralDenomenatedDebt,
            uint _excessCollateral);
    
    function calculateIssuanceAmount(uint _amount) 
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _issuerFee,
            uint _actualCollateralAdded,
            uint _accreditedCollateral,
            uint _tokensIssued);
    
    function calculateRedemptionAmount(uint _amount)
        public
        view
        returns (
            uint _protocolFee,
            uint _automationFee,
            uint _issuerFee,
            uint _collateralRedeemed,
            uint _collateralReturned);

    // This function returns the exact amount of the debt token that would be returned for
    // the given amount of the collateral token.
    // It will not run if the collateralization ratio is below the target ratio.
    function calculateTradeOutput_CollateralForDebt(uint _collateralAmount)
        public  
        view
        returns (uint _debtAmount); 
    
    // This function returns the exact amount of the collateral token that would be returned for
    // the given amount of the debt token.
    // It will not run if the collateralization ratio is above the target ratio.
    function calculateTradeOutput_DebtForCollateral(uint _debtAmount)
        public
        view
        returns (uint _collateralAmount);

    event Issued(
        address _receiver, 
        uint _suppliedCollateral,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee,
        uint _actualCollateralAdded,
        uint _accreditedCollateral,
        uint _tokensIssued);
    
    event Redeemed(
        address _redeemer,
        address _receiver, 
        uint _tokensRedeemed,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee,
        uint _collateralRedeemed,
        uint _collateralReturned);

    event ChangedSettings(
        uint _redemptionRatioLimit,
        uint _lowerRatio,
        uint _targetRatio,
        uint _upperRatio,
        uint _protocolFee,
        uint _automationFee,
        uint _issuerFee);
}
