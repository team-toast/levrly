# F# tests belong here

## Notes

`setup.fsx` is F# script, that restores common infrastructure to run tests.

Tests should be runned with `dotnet test` command.

## Steps to run tests
_will be optimazed later_  

0. Set cwd to repo root.
1. Run hardhat node `npx hardhat node`.
2. Build and deploy contracts with hardhat.
3. Set cwd to `smart-contracts\levrly\tests`.
4. Run setup script `dotnet fsi setup.fsx`.
5. Run tests `dotnet test`.
