# Lidstroem

Plugin-baserat CRM/plattformsramverk byggt på .NET 9 och Blazor WebAssembly.

## Komma igång

### 1. Skapa solution-filen

```bash
chmod +x create-sln.sh && ./create-sln.sh
```

### 2. Konfigurera databasen

Editera `WebAPI/appsettings.Development.json`:
- `ConnectionStrings:DefaultConnection` — peka på din SQL Server
- `Auth:Jwt:Secret` — minst 32 tecken
- `SuperAdmin:Email` / `SuperAdmin:Password` — admin-konto

### 3. Kör migrationer

```bash
cd Infrastructure
dotnet ef database update --startup-project ../WebAPI
```

### 4. Starta backend

```bash
cd WebAPI
dotnet run
```

API-dokumentation: `https://localhost:7209/scalar`

### 5. Starta frontend (separat terminal)

```bash
cd ../lidstroem-frontend
dotnet run
```

Öppna `https://localhost:5001`

---

## Arkitektur

```
Lidstroem.Core          — Entiteter (Actor), interfaces, events, GDPR-kontrakt
Lidstroem.Infrastructure — EF, JWT-auth, RBAC, interceptors
Lidstroem.Shared        — BaseLidstroemController, RequirePermission-attribut
Lidstroem.WebAPI        — Startpunkt, DI-registrering
Lidstroem.Frontend      — Blazor WASM, dynamisk rendering via schema-API
Plugins/                — Fristående feature-moduler
```

## Skapa ett nytt plugin

Se `PLUGIN_TEMPLATE.md` för komplett guide.

Snabbstart med AI:

1. Anropa `GET /api/plugin-manifest` (logga in som SuperAdmin först)
2. Ge svaret + `PLUGIN_TEMPLATE.md` till en AI
3. Be AI:n generera plugin-filerna för din feature
4. Lägg till i `Plugins/`, registrera i `WebAPI.csproj` och `create-sln.sh`
5. Kör `dotnet ef migrations add Add{PluginName}Plugin --startup-project ../WebAPI`

## Datamodell — nyckelrelationer

```
Actor           — central subjektentitet (login, GDPR, roller)
  └── ActorCredentials  — JWT-login
  └── ActorRoleAssignment — RBAC

Project         — arbetsyta / affärsmöjlighet
  └── ProjectMember     — Actor-koppling (join-tabell)
  └── Activity          — aktiviteter kopplade till projektet
      └── ActivityActor — Actor-koppling (join-tabell)
      └── Donation      — donationer kopplade till aktiviteten

Donation        — finansiell post
  DonorType: "Actor"
  TargetType: "Project" | "Activity"
```

## Tillgängliga plugins

| Plugin | RoutePrefix | Beskrivning |
|--------|-------------|-------------|
| WorkManagement.Projects | projects | Projekt med medlemmar |
| WorkManagement.Activities | activities | Aktiviteter, alltid kopplade till ett projekt |
| Donations | donations | Donationer från Actor till Project/Activity |
| FieldReports | fieldreports | Mötesanteckningar, besöksrapporter |
| Resources | resources | Filbilagor och länkar |
| Communication | notifications | E-post och in-app-notiser |
| Invitations | invitations | Bjud in nya användare |
| GDPR | gdpr | GDPR-radering med audit-logg |
| ACL | acl | Resursbehörigheter actor-till-actor |
| CMS | cms | Publika sidor och tenant-site-konfiguration |
| SuperAdmin | admin | Tenant-hantering, RBAC, auth |
| Schema | schema | Schema-API + plugin-manifest |
