# Completely Unsafe Messenger

## Building

To make a single executable binary, clone the repo and run:

    dotnet restore
    dotnet build
    dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true