FROM mcr.microsoft.com/dotnet/sdk:6.0


COPY publish/linux-x64/arc-export .

#ENTRYPOINT ["/arc-export"]