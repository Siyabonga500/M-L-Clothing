#!/bin/sh
# Starts the M & L Clothing app (Mac/Linux): ./run.sh
cd "$(dirname "$0")"
dotnet run --project src/ClothingStore --launch-profile https
