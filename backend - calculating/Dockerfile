FROM mcr.microsoft.com/dotnet/sdk:5.0 AS dev
WORKDIR /App

COPY *.csproj ./
RUN dotnet restore

COPY . ./

EXPOSE 5099

ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://0.0.0.0:5099"]