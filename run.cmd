@echo off
REM Starts the M ^& L Clothing app. Run from the payment folder: run
cd /d "%~dp0"
dotnet run --project src\ClothingStore --launch-profile https
