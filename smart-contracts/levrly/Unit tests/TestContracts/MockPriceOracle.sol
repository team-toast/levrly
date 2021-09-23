// SPDX-License-Identifier: agpl-3.0
pragma solidity 0.8.7;

interface IPriceOracleGetter {
  function getAssetPrice(address asset) external view returns (uint256);
}

/**
 * @title Mock contract for AaveOracle
 **/
contract MockPriceOracle is IPriceOracleGetter {
  address _originalPriceOracle;
  mapping(address => uint256) prices;

  constructor (address originalPriceOracle) {
    _originalPriceOracle = originalPriceOracle;
  }

  event AssetPriceUpdated(address _asset, uint256 _price, uint256 timestamp);

  function getAssetPrice(address _asset) external view override returns (uint256) {
    uint256 priceOrZero = prices[_asset];
    if (priceOrZero == 0) {
      return IPriceOracleGetter(_originalPriceOracle).getAssetPrice(_asset);
    } else {
      return priceOrZero;
    }
  }

  function setAssetPrice(address _asset, uint256 _price) external {
    prices[_asset] = _price;
    emit AssetPriceUpdated(_asset, _price, block.timestamp);
  }
}
