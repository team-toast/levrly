pragma solidity ^0.5.17;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "./DSMath.sol";
import "./DSProxy.sol";

contract Types 
{
    enum Asset 
    { 
        Long, 
        Short,
        Interest,
        Debt
    }
}

contract LongShortPairState 
{
    uint constant public ONE_PERC = 10 ** 16;
    uint constant public ONE_HUNDRED_PERC = 10 ** 18;

    IERC20 public longToken;
    IERC20 public interestToken; // the AAVE interest token to which interest accrues
    IERC20 public shortToken;
    IERC20 public debtToken;     // the AAVE debt token to which short accrues

    // Ratios
    // The PERC suffix indicates that the ratio is expressed as a percentage
    uint public redemptionRatioLimitPERC; 
    uint public lowerRatioPERC;
    uint public targetRatioPERC;
    uint public upperRatioPERC;

    // Fees
    uint public automationFeePERC;
    uint public protocolFeePERC;

    // This is the reward applicable to the portion of outside of the lower or upper
    // bounds. It forms part of the methods to fully lever or delever the contract. 
    uint public settlementRewardPERC;

    // Thought: So what will happen if we fixed the ratios and the fees?
    // Ratios: There would be no capacity to interfene and there would be losses
    // due to rebalancing too soon or due to rebalancing too late.
    // Fees: If the automation fee were too high, people might now purchase.
    // If the automation fee was too low, then the contract might bleed out on gas
    // settlementRewardPERC, here's it's hard to imagine anything really bad 
    // happening since this is competitive and the first called would do it at very
    // low income the moment it becomes viable. 

    // NOTE: Uniswap did exactly this, they dictated the fees to the market and the
    // market loved it. Think about it, if they had multiple fee options on a scale
    // they would have split up the liquidity.
    // So what if we use "tolerable" fees like 0.5% for the protocol and 0.5% for
    // the pool holders and accept those as standard?
    
    // fee recipients
    address public gulper;
}

contract LSP_IssuerRewardState
{
    uint public issuerFeePERC;
    address public issuerFeeAccount; 
}

contract LSP_AMMState is LongShortPairState 
{
    // Trading fees
    uint public withinRangeFeePERC; // the fee charged if the trade is beneficial to the target ratio
    uint public aboveRangeFeePERC;  // 
    uint public belowRangeFeePERC;
    uint public panicRangeFeePERC;
}
