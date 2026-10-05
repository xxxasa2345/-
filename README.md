# Saqer Accounting System

Saqer Accounting System is a modular accounting and ERP starter project for small and medium businesses. It includes the core domain for accounting, customer/supplier management, invoice storage, and financial journaling.

## Architecture

- Domain: core entities and business rules
- Application: services and use cases
- Infrastructure: EF Core data access and database context
- API: ASP.NET Core REST API to expose the system

## Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server

## Quick start

1. Open the solution in Visual Studio or VS Code.
2. Restore NuGet packages.
3. Set the connection string in `src/SaqerAccountingSystem.API/appsettings.json`.
4. Run the API project.

## Current milestone

This repository contains the initial project skeleton, domain entities, and the foundational accounting services.

## Planned modules

- Companies and branches
- Chart of accounts
- Journal entries
- Customers and suppliers
- Inventory items
- Sales and purchase invoices
- Payment processing
- Reports and dashboards
