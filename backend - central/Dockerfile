FROM mcr.microsoft.com/dotnet/sdk:5.0 AS dev
WORKDIR /App

COPY *.csproj ./
RUN dotnet restore

COPY . ./

EXPOSE 5098

ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://0.0.0.0:5098"]