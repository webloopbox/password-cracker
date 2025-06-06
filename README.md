# password-cracker

## Running backend

Central server:

```bash

cd "backend - central"
dotnet restore && dotnet run

```

Calculating server:

```bash

cd "backend - calculating"
dotnet restore && dotnet run

```

## Running in docker

Central server and frontend:

```bash

docker compose up --build --force-recreate

```

Calculating server:

```bash

docker compose -f compose.calculating.yaml up --build --force-recreate

```

IMPORTANT! Generate user_passwords.txt file doing these things:

```bash
cd "backend - password generator"
python3 -m venv venv

# Activate the virtual environment:
source venv/bin/activate  # on macOS/Linux
venv\Scripts\activate     # on Windows CMD
.\venv\Scripts\activate   # on Windows PowerShell

python password_generator.py
```

In main project directory create file called .env with content:

```bash
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5099;http://+:5098
PASSWORD_FILE_PATH=./data/users_passwords.txt
CENTRAL_SERVER_IP=central_server_ip
CALCULATING_SERVER_IP=calculating_server_ip
```

For properly start calculating server you need at least one dictionary with .txt format at central server in dictionary directory.

In frontend dir directory you need to create .env file with this credentials:

```bash
VITE_CENTRAL_SERVER_IP=central_server_ip
```
