# Starts the M & L Clothing app. Run from the payment folder:  .\run.ps1
Set-Location $PSScriptRoot
dotnet run --project src/ClothingStore --launch-profile https
