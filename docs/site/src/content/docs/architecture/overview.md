---
title: Architecture Overview
description: Chronith's clean architecture layers, CQRS, and multi-tenancy design.
---

Chronith is built on clean architecture with four strict layers.

## Layer diagram

```
┌─────────────────────────────────────────────────────┐
│                      API Layer                      │
│         (FastEndpoints, middleware, DI wiring)      │
├─────────────────────────────────────────────────────┤
│                Infrastructure Layer                 │
│   (EF Core, repositories, notifications, payments)  │
├─────────────────────────────────────────────────────┤
│                 Application Layer                   │
│      (Commands, queries, handlers, validators)      │
├─────────────────────────────────────────────────────┤
│                   Domain Layer                      │
│        (Entities, value objects, exceptions)        │
└─────────────────────────────────────────────────────┘
```

Dependency direction: `Domain ← Application ← Infrastructure ← API`

## Layer responsibilities

| Layer | Contents |
|-------|----------|
| `Chronith.Domain` | Models (entities, value objects), enums, domain exceptions. No framework dependencies. |
| `Chronith.Application` | Commands, queries, handlers, validators, DTOs, mappers, repository interfaces, service interfaces |
| `Chronith.Infrastructure` | EF Core DbContext, entity configs, repositories, background services, notification channels, payment providers, caching, auth |
| `Chronith.API` | FastEndpoints endpoint classes, middleware, health checks, `Program.cs` DI wiring |

## Key architectural rules

- **No AutoMapper** — all mapping is manual via static extension methods
- **Domain zero dependencies** — no NuGet packages in `Chronith.Domain`
- **API via DTOs only** — the API layer never references domain models directly
- **Entities are sealed POCOs** — no navigation properties, EF `Include()` in repositories only

## CQRS via MediatR

All operations go through MediatR:

- **Commands** — mutate state, include `IUnitOfWork`, return a DTO
- **Queries** — read-only, no `IUnitOfWork`, implement `IQuery` marker for performance behavior

Pipeline behaviors:
- `ValidationBehavior` — FluentValidation before handler
- `PerformanceBehavior` — logs slow queries

## Multi-tenancy

Every request runs within a tenant context (`ITenantContext`). EF Core global query filters automatically scope all queries to the current tenant:

- `TenantId == tenantContext.TenantId` — tenant isolation
- `!IsDeleted` — soft delete filter

Cross-tenant queries (background services, public endpoints) use `.IgnoreQueryFilters()` with explicit filters.
