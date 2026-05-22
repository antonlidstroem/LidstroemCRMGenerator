# Lidstroem - Setup Guide

> **Windows users:** The `.sh` scripts in this repo are for macOS/Linux.
> Use the `.ps1` PowerShell equivalents provided alongside them, or run the
> commands manually. All `dotnet` commands work identically on Windows.

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server - LocalDB (included with Visual Studio) or Docker (see Step 1)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) *(optional - only needed for the SQL Server container)*

---

## Backend Setup

### 1. Configure the database connection

Edit `WebAPI/appsettings.Development.json`.

**Option A - LocalDB (Windows, ships with Visual Studio, no Docker needed):**
```json
"DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LidstroemDb;Integrated Security=True;Trust Server Certificate=True"
```

**Option B - SQL Server via Docker:**

*PowerShell or Windows CMD (one line - the backslash continuation does NOT work in CMD):*
```
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 --name lidstroem-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

*Bash / macOS / Linux:*
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name lidstroem-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

Then use this connection string:
```json
"DefaultConnection": "Server=localhost,1433;Database=LidstroemDb;User Id=sa;Password=YourStrong@Passw0rd;Trust Server Certificate=True"
```

---

### 2. Create the solution file *(one time only)*

**Windows (PowerShell):**
```powershell
cd lidstroem
.\create-sln.ps1
```
If you see "running scripts is disabled", run this once in an admin PowerShell then retry:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

**macOS / Linux:**
```bash
cd lidstroem
chmod +x create-sln.sh && ./create-sln.sh
```

---

### 3. Create and apply EF migrations

> **Important:** The repo does not ship migration files. You must run
> `migrations add` before `database update` - without it, `database update`
> does nothing and the app will crash on first run with table-not-found errors.

> If `dotnet ef` is not installed:
> ```
> dotnet tool install --global dotnet-ef
> ```

**Windows (PowerShell) - run from the `lidstroem\` repo root:**
```powershell
cd Infrastructure
dotnet ef migrations add InitialCreate --startup-project ..\WebAPI
dotnet ef database update --startup-project ..\WebAPI
cd ..
```

**macOS / Linux - run from the `lidstroem/` repo root:**
```bash
cd Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../WebAPI
dotnet ef database update --startup-project ../WebAPI
cd ..
```

---

### 4. Start the backend

```
cd WebAPI
dotnet run
```

API docs: `https://localhost:7209/scalar`

Default SuperAdmin credentials (from `appsettings.Development.json`):
- Email: `admin@lidstroem.dev`
- Password: `ChangeMe123!`

> **Production note:** `SuperAdmin:Password` must be set explicitly in production
> config (minimum 16 characters). The app will refuse to start without it in
> non-Development environments.

---

## Frontend Setup

### 5. Start the Blazor frontend *(new terminal)*

```
cd lidstroem-frontend
dotnet run
```

Open: `https://localhost:5001`

The dev frontend is pre-configured to call the backend at `https://localhost:7209`
via `wwwroot/appsettings.Development.json`.

> **Production note:** Before publishing, set `ApiBaseUrl` in
> `wwwroot/appsettings.json` to your real backend URL
> (e.g. `https://api.yourdomain.com`). Leaving it empty causes the frontend
> to call its own origin and all API requests will 404.

---

## Optional: Email (SMTP)

The dev config points to `localhost:1025`. To test email locally, spin up
[smtp4dev](https://github.com/rnwood/smtp4dev) (cross-platform):

```
docker run -p 1025:1025 -p 5000:5000 rnwood/smtp4dev
```

Web UI at `http://localhost:5000`.

Or [MailHog](https://github.com/mailhog/MailHog):
```
docker run -p 1025:1025 -p 8025:8025 mailhog/mailhog
```
Web UI at `http://localhost:8025`.

---

## Running Tests

**Windows (PowerShell):**
```powershell
cd lidstroem
.\run-tests.ps1          # full suite
.\run-tests.ps1 -Fast    # skip integration tests
```

**macOS / Linux:**
```bash
cd lidstroem
chmod +x run-tests.sh && ./run-tests.sh
./run-tests.sh --fast    # skip integration tests
```

---

## Adding a New Plugin

See `PLUGIN_TEMPLATE.md` for the full guide. Quick steps:

1. Call `GET /api/plugin-manifest` (login as SuperAdmin first)
2. Give the response + `PLUGIN_TEMPLATE.md` to an AI
3. Put generated files in `Plugins/MyPlugin/`
4. Add `ProjectReference` to `WebAPI/Lidstroem.WebAPI.csproj`
5. **Windows:** add entry to `create-sln.ps1`; **macOS/Linux:** add to `create-sln.sh`
6. Create a migration:

**Windows:**
```powershell
cd Infrastructure
dotnet ef migrations add AddMyPlugin --startup-project ..\WebAPI
dotnet ef database update --startup-project ..\WebAPI
```

**macOS / Linux:**
```bash
cd Infrastructure
dotnet ef migrations add AddMyPlugin --startup-project ../WebAPI
dotnet ef database update --startup-project ../WebAPI
```
