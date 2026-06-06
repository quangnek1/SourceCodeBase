# Applying Migrations in EF Core
	cd src/Services
## Create Migrations
	1. Windows command prompt: "Add-Migration MigrationName [options]"
	2. dotnet CLI: "dotnet ef migrations add MigrationName [options]"
	=>> EX-PMC: "Add-Migration InitialMigration"
	=>> EX-CLI: "dotnet ef migrations add InitialMigration --output-dir Migrations"
	
	dotnet ef migrations add "Initial" --project AerationSterilize.Persistence --startup-project AerationSterilize.API --output-dir Migrations

## Applying Created Migration
	1. Windows command prompt: "Update-Database [options]"
	2. dotnet CLI: "dotnet ef database update [options]"
	=>> EX-PMC: "Update-Database"
	dotnet ef database update --project AerationSterilize.Persistence --startup-project AerationSterilize.API

## Removing a Migration
	1. Windows command prompt: "Remove-Migration [options]"
	2. dotnet CLI: "dotnet ef migrations remove [options]"
	=>> EX-PMC: "Remove-Migration"
	dotnet ef migrations remove -p AerationSterilize.Persistence --startup-project AerationSterilize.API