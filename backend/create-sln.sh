#!/usr/bin/env bash
# Run once from the lidstroem/ directory to create the solution file.
set -e

dotnet new sln -n Lidstroem --force

# Core layers
dotnet sln add Core/Lidstroem.Core.csproj
dotnet sln add Infrastructure/Lidstroem.Infrastructure.csproj
dotnet sln add Shared/Lidstroem.Shared.csproj
dotnet sln add WebAPI/Lidstroem.WebAPI.csproj

# Plugins
dotnet sln add Plugins/ACL/Lidstroem.Plugins.ACL.csproj
dotnet sln add Plugins/CMS/Lidstroem.Plugins.CMS.csproj
dotnet sln add Plugins/Communication/Lidstroem.Plugins.Communication.csproj
dotnet sln add Plugins/Donations/Lidstroem.Plugins.Donations.csproj
dotnet sln add Plugins/FieldReports/Lidstroem.Plugins.FieldReports.csproj
dotnet sln add Plugins/GDPR/Lidstroem.Plugins.GDPR.csproj
dotnet sln add Plugins/Invitations/Lidstroem.Plugins.Invitations.csproj
dotnet sln add Plugins/Resources/Lidstroem.Plugins.Resources.csproj
dotnet sln add Plugins/Schema/Lidstroem.Plugins.Schema.csproj
dotnet sln add Plugins/SuperAdmin/Lidstroem.Plugins.SuperAdmin.csproj
dotnet sln add Plugins/WorkManagement.Activities/Lidstroem.Plugins.WorkManagement.Activities.csproj
dotnet sln add Plugins/WorkManagement.Projects/Lidstroem.Plugins.WorkManagement.Projects.csproj

# Tests
dotnet sln add Tests/Common/Lidstroem.Tests.Common.csproj
dotnet sln add Tests/Core/Lidstroem.Tests.Core.csproj
dotnet sln add Tests/Infrastructure/Lidstroem.Tests.Infrastructure.csproj
dotnet sln add Tests/Integration/Lidstroem.Tests.Integration.csproj
dotnet sln add Tests/Plugins/Lidstroem.Tests.Plugins.csproj

echo ""
echo "Solution created: Lidstroem.sln"
echo "Open with: start Lidstroem.sln   (Windows)"
echo "       or: open Lidstroem.sln    (macOS)"
