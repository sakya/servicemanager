#!/bin/bash
rm -rf ./dist/linux-x64

dotnet clean ServiceManager.sln -c Release
dotnet publish ServiceManager.sln -c Release --runtime linux-x64 -p:PublishReadyToRun=true --self-contained --output ./dist/linux-x64

