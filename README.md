# Kairos

**Kairos** is a room-reservation and facility-scheduling REST API built with **ASP.NET Core (.NET 10)**, **Entity Framework Core** and **PostgreSQL**. It manages rooms, their operating hours and maintenance blockouts, and lets authenticated users book rooms without double-booking, all behind a fine-grained, permission-based authorization model.

Beyond the business domain, the project is a showcase of production-minded backend practices: a modular architecture, the Use Case / Repository / Unit of Work patterns, an **Inbox pattern** for idempotent request processing, Redis-backed second-level caching, health checks, and a complete observability stack (traces, metrics and logs) wired to Grafana.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Authentication & Authorization](#authentication--authorization)
- [The Inbox Pattern and the `X-Inbox-Id` Header](#the-inbox-pattern-and-the-x-inbox-id-header)
- [Business Rules](#business-rules)
- [API Reference](#api-reference)
- [Response Format](#response-format)
- [Observability](#observability)
- [Testing](#testing)
- [Known Limitations](#known-limitations)

---

## Features

- **Room management**: rooms with code, location, floor, capacity, area, accessibility and reservable/active flags, grouped by **room types**.
- **Operating hours**: per-room, per-weekday opening and closing times, with the ability to mark days as closed.
- **Blockouts**: schedule a room as unavailable (maintenance, cleaning, renovation, private event, administrative, other) and cancel blockouts with a recorded reason.
- **Reservations**: create, update, cancel and delete reservations, with automatic validation against operating hours, existing reservations and blockouts. Supports an **idempotency key** to make retries safe.
- **Availability checks**: query whether (and which) reservations conflict with a given room and time range.
- **Categories**: classify reservations (code, name, color, icon, display order).
- **Identity**: registration, login, logout, refresh tokens, password change, account activation/deactivation and brute-force lockout.
- **Fine-grained permissions**: more than 60 permission codes assigned per user and enforced per endpoint through JWT claims.
- **Reliability**: Inbox-based exactly-once processing of mutating and read operations.
- **Standardized responses**: every endpoint returns the same envelope with trace id, timestamp and error list.
- **API versioning**, **Swagger/OpenAPI** documentation, **health checks** and **full observability**.

## Tech Stack

| Area | Technology |
| --- | --- |
| Runtime / Framework | .NET 10, ASP.NET Core (controllers) |
| Database | PostgreSQL 16 via EF Core 10 + Npgsql |
| Cache | Redis 7 via EF Core Second Level Cache Interceptor |
| Identity | ASP.NET Core Identity (EF stores) + JWT Bearer |
| Concurrency | `EntityFrameworkCore.Locking.PostgreSQL` |
| Mapping | Riok.Mapperly (compile-time) and Mapster |
| API docs | Swashbuckle (Swagger UI), Asp.Versioning |
| Logging | Serilog (console + OTLP) |
| Observability | OpenTelemetry, Grafana Alloy, Tempo, Jaeger, Loki, Prometheus, Grafana, cAdvisor, node-exporter, Redis and Postgres exporters |
| Containers | Docker, Docker Compose |
| Tests | xUnit, Moq, NSubstitute, Shouldly, FluentAssertions, Bogus, Testcontainers (PostgreSQL + Redis), Respawn |

## Architecture

Kairos is a **modular monolith**. Each business area lives in its own module under `Modules/`, registered through an `Add<Module>Module()` extension method, while cross-cutting infrastructure lives under `Config/` and `Common/`.

```
HTTP request
   │
   ▼
RequireInboxIdMiddleware ──► JWT authentication ──► Permission authorization
   │
   ▼
Controller
   │   (wraps the use case in an Inbox transaction)
   ▼
InboxTransactionUseCase ──► UnitOfWork.ExecuteTransactionWithInboxAsync
   │        1. claim the inbox message (Pending → Processing)
   │        2. run the use case inside a DB transaction
   │        3. mark the message as Processed and commit
   ▼
Use Case (one class per operation, returns Result<T>)
   │
   ▼
Repositories (via IUnitOfWork) ──► AppDbContext ──► PostgreSQL (+ Redis cache)
```

Key design choices:

- **One Use Case per operation** (`CreateReservationUseCase`, `CancelRoomBlockoutUseCase`, ...), each behind an interface and registered in DI. Business logic is isolated from HTTP concerns.
- **Result pattern**: use cases return `Result<T>` carrying a value or a list of errors plus an HTTP status code, so failures are explicit instead of exception-driven.
- **Unit of Work** exposes every repository and owns transaction handling, including EF execution strategies (retry on transient failures) and automatic rollback when a `Result` is a failure.
- **Generic transaction wrappers** (`TransactionUseCase<,,>` and `InboxTransactionUseCase<,,>`) decorate any use case without touching its code.
- **Audit fields** (`CreatedAt`, `UpdatedAt`, `Version`) are maintained centrally in `AppDbContext.SaveChanges`, so every entity gets them for free.
- **Application bootstrap**: on startup the API applies EF migrations, then runs registered seeds (`ISeedCase`) to create permissions, the master user and the master user's permission links.

## Project Structure

```
Kairos/
├── src/Kairos.Api/
│   ├── Program.cs                  # Composition root and startup pipeline
│   ├── Common/                     # Base models, Result<T>, Page<T>, enums, exceptions, middleware
│   ├── Config/
│   │   ├── Authentication/         # JWT setup, [RequirePermissions], policy provider and handler
│   │   ├── DB/                     # AppDbContext, DI, migrations, transaction use cases
│   │   ├── UnitOfWork/             # IUnitOfWork implementation (+ Inbox transactions)
│   │   ├── Bootstrap/              # Startup seeding orchestrator
│   │   ├── Observability/          # OpenTelemetry + Serilog OTLP + custom metrics
│   │   ├── Health/                 # PostgreSQL and Redis health checks
│   │   ├── Swagger/ Version/ Cors/ ApiBehavior/ Options/
│   └── Modules/
│       ├── Identity/               # Auth, Users, Permissions, PermissionUsers, Admin
│       ├── Rooms/                  # Room, RoomTypes, RoomOperatingHour, RoomBlockout
│       ├── Reservations/           # Reservation
│       ├── Categories/
│       └── Reliability/            # Inbox
│           └── <Feature>/          # Controllers · DTOs · Mapper · Models · Repositories
│                                   #   · Services/{Interfaces,Providers} · Configurations
├── tests/
│   ├── Kairos.Tests/               # Unit tests for use cases and seeds
│   └── Kairos.IntegrationTests/    # End-to-end HTTP tests against real PostgreSQL + Redis
├── observability/                  # Prometheus, Tempo, Loki, Alloy and Grafana configuration
├── Dockerfile
├── docker-compose.yml
└── Kairos.sln
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) and Docker Compose

### Option 1: Run everything with Docker Compose

```bash
git clone <your-repository-url>
cd Kairos
docker compose up -d --build
```

This starts the API, PostgreSQL, Redis and the full observability stack. Once the API is up:

| Service | URL |
| --- | --- |
| API | http://localhost:5093 |
| Swagger UI | http://localhost:5093/swagger |
| Health | http://localhost:5093/health |
| Grafana | http://localhost:3001 |
| Jaeger UI | http://localhost:16686 |
| Prometheus | http://localhost:9090 |

> Compose runs the API with `ASPNETCORE_ENVIRONMENT=Development`, which is what enables Swagger. Change it for production.

On first start the API applies migrations and seeds the permissions and the master user automatically.

### Option 2: Run the API locally (recommended for development)

Start only the dependencies:

```bash
docker compose up -d postgres redis alloy tempo loki prometheus grafana
```

Then run the API:

```bash
dotnet run --project src/Kairos.Api --launch-profile http
```

The default `appsettings.json` already points to `localhost:5432` (PostgreSQL), `localhost:6379` (Redis) and `http://localhost:4317` (OTLP/Alloy). If you do not want to run the observability stack, disable it:

```bash
Observability__Enabled=false dotnet run --project src/Kairos.Api --launch-profile http
```

### Useful commands

```bash
dotnet build Kairos.sln
dotnet test tests/Kairos.Tests
dotnet test tests/Kairos.IntegrationTests   # requires Docker (Testcontainers)

# Create a new EF Core migration
dotnet ef migrations add <Name> \
  --project src/Kairos.Api \
  --output-dir Config/DB/Migrations
```

## Configuration

Configuration lives in `src/Kairos.Api/appsettings.json` and can be overridden with environment variables using the standard double-underscore convention (for example `Jwt__SecretKey`).

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `ConnectionStrings:Redis` | Redis connection string |
| `Jwt` | Signing key, issuer, audience, access token lifetime (minutes) and refresh token lifetime (minutes) |
| `JwtAuthentication` | Claim mapping, clock skew, name and role claim types |
| `UserMaster` | Credentials of the seeded master (super-admin) user |
| `Postgres` | Retry count, retry delay, command timeout, locking toggle |
| `Redis` | Timeouts, retry and client name |
| `SecondLevelCache` | Cache key prefix, expiration, lock mode and timeout |
| `ApiVersioning` | Default version, URL substitution, group name format |
| `Cors` | Allowed origins, methods and headers (the `X-Inbox-Id` header is allowed) |
| `Observability` | OTLP endpoint and protocol, enable/disable traces, metrics and logs, sampling ratio, per-instrumentation toggles |

Docker Compose also reads these optional variables (defaults in parentheses): `POSTGRES_DB` (`kairos`), `POSTGRES_USER` (`postgres`), `POSTGRES_PASSWORD`, `POSTGRES_PORT` (`5432`) and `REDIS_PORT` (`6379`).

> ⚠️ **Security:** the values in `appsettings.json` (JWT secret, master user password, database password) are **development defaults only**. Before deploying anywhere real, override them with environment variables or a secret manager (`Jwt__SecretKey`, `UserMaster__Password`, `ConnectionStrings__DefaultConnection`) and rotate any values that were ever committed. Also note that the Grafana container in `docker-compose.yml` runs with anonymous admin access. It is intended for local use only.

## Authentication & Authorization

1. **Register** (`POST /api/v1/auth/Register`) or **log in** (`POST /api/v1/auth/Login`) to receive an access token, a refresh token and the user's permission list.
2. Send the access token in the `Authorization: Bearer <token>` header.
3. Use **`POST /api/v1/auth/RefreshToken`** to obtain a new token pair.

Details:

- Tokens are **JWTs signed with HMAC-SHA256**. The access token lifetime is configurable (default: 24 hours).
- Every permission a user holds is embedded in the token as a `permission` claim.
- Endpoints are protected with `[RequirePermissions("rooms.create")]`. A custom policy provider builds the policy from the attribute and a handler checks that the user holds **all** listed permissions (case-insensitive).
- **New users are created with no permissions.** An administrator must grant them through the `permission-users` endpoints. Because permissions are embedded in the token, a user must log in again (or refresh) to see permission changes.
- A **master user** is seeded at startup from the `UserMaster` configuration and automatically receives **every** permission, including any added in future versions.
- **Brute-force protection:** after 3 consecutive failed logins an account is locked for 5 hours (`429 Too Many Requests`). Invalid credentials return a generic error so account existence is not disclosed.
- Deactivating an account (`PATCH /api/v1/admin/Users/{userId}/Status`) blocks login and revokes the refresh token.

### Permission codes

Permissions follow a `<resource>.<action>` convention and are seeded automatically:

| Module | Codes (examples) |
| --- | --- |
| Identity | `identity.users.{create,read,list,update,delete}`, `identity.users.status.update`, `identity.permissions.*`, `identity.permission-users.*` |
| Categories | `categories.{create,read,list,update,delete}` |
| Room types | `room-types.{create,read,list,update,delete}` |
| Rooms | `rooms.{create,read,list,update,delete}` |
| Operating hours | `rooms.operating-hours.{create,read,list,update,delete}` |
| Blockouts | `rooms.blockouts.{create,read,list,update,delete,cancel}`, `rooms.availability.read` |
| Reservations | `reservations.{create,read,list,update,delete,cancel}` |

## The Inbox Pattern and the `X-Inbox-Id` Header

Every request under `/api` **must** include an `X-Inbox-Id` header, otherwise the API answers `400 Bad Request` (CORS preflight `OPTIONS` requests are exempt).

The header identifies a row in the `InboxMessages` table. When an operation runs through the Inbox transaction service, Kairos:

1. Atomically claims the message (`Pending` → `Processing`) using a compare-and-set update, recording which worker holds it.
2. Executes the use case inside a database transaction.
3. Marks the message `Processed` in the same transaction and commits.

If the message cannot be claimed (already processed, being processed by another worker, or scheduled for a later retry), the API returns **`409 Conflict`** and increments the `kairos.inbox.conflicts` metric. This guarantees that a retried or duplicated request is **not executed twice**. Inbox statuses are `Pending`, `Processing`, `Processed`, `Failed` and `DeadLetter`.

In practice each inbox id is **single-use**: generate a fresh one per logical operation.

### Creating an inbox message for manual testing

The current version does not expose an HTTP endpoint to create inbox messages (see [Known Limitations](#known-limitations)). To try the API manually, insert a row and use its `Id` as the header value:

```sql
INSERT INTO "InboxMessages"
  ("Id", "MessageId", "MessageType", "Payload", "Status",
   "ReceivedAt", "RetryCount", "Version", "CreatedAt")
VALUES
  (gen_random_uuid()::text,
   'message-' || gen_random_uuid()::text,
   'manual.test',
   '{"value":"manual"}',
   'Pending',
   now(), 0, 1, now())
RETURNING "Id";
```

The integration tests create inbox rows directly through `AppDbContext` for the same reason.

## Business Rules

### Reservations

A reservation is accepted only if **all** of the following hold:

- The category exists and is **active**.
- The room exists, is **active** and is **reservable**.
- `StartsAt` is strictly earlier than `EndsAt`.
- The whole period fits inside the room's **operating hours**. Reservations spanning several days are validated day by day. A weekday with no active configuration is treated as **closed**, and an active "closed" record blocks the entire day.
- No other non-cancelled reservation **overlaps** the same room and period.
- No non-cancelled **blockout** overlaps the period.

Additional behavior:

- The reservation owner is taken from the authenticated JWT (never from the request body).
- **Idempotency:** when an `IdempotencyKey` is supplied, repeating the same request for the same user returns the existing reservation instead of creating a duplicate. This is enforced by a unique database index.
- Reservations are **cancelled** rather than deleted whenever history matters. Cancellation records who cancelled, when and why, and cancelling twice returns `409`.
- Statuses: `Confirmed`, `Cancelled`.

### Room blockouts

- Types: `Maintenance`, `Cleaning`, `Renovation`, `PrivateEvent`, `Administrative`, `Other`.
- Statuses: `Scheduled`, `Cancelled`.
- A blockout cannot overlap another blockout of the same room (`409`), the room must exist and be active, and cancelled blockouts cannot be updated.
- Creator and canceller are recorded from the JWT.

### Validation and pagination

- Request validation errors are returned in the standard envelope with a per-field error dictionary.
- All list endpoints support filtering, pagination (`Page` ≥ 1, `PageSize` 1–100, default 20) and ordering (`ColumnOrder=ASC|DESC`), plus base filters on id, version and creation/update date ranges.
- Text filters support case-sensitive (`LIKE`) or case-insensitive (`ILIKE`) matching.

## API Reference

Base path: `/api/v1`. Interactive documentation is available at `/swagger` when running in the `Development` environment.

Unless stated otherwise, every endpoint requires `Authorization: Bearer <token>`, the `X-Inbox-Id` header and the permission shown.

### Auth: `/auth`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/auth/Exists/username/{username}` | Anonymous | Check whether a username is taken |
| GET | `/auth/Exists/email/{email}` | Anonymous | Check whether an email is registered |
| POST | `/auth/Register` | Anonymous | Register a user and receive tokens |
| POST | `/auth/Login` | Anonymous | Authenticate and receive tokens |
| POST | `/auth/RefreshToken` | Anonymous | Exchange a refresh token for new tokens |
| POST | `/auth/Logout` | JWT | Revoke the current user's refresh token |
| POST | `/auth/ChangePassword` | JWT | Change the current user's password |

### Admin: `/admin`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| PATCH | `/admin/Users/{userId}/Status` | `identity.users.status.update` | Activate or deactivate a user |

### Users: `/users`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/users` | `identity.users.list` | List users (paginated, filterable) |
| GET | `/users/{id}` | JWT | Get a user by id |
| PUT | `/users/Me` | JWT | Update the authenticated user's profile |
| DELETE | `/users` | JWT | Delete a user |

### Permissions: `/permissions`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/permissions` | `identity.permissions.list` | List permissions |
| GET | `/permissions/{id}` | `identity.permissions.read` | Get by id |
| GET | `/permissions/code/{code}` | `identity.permissions.read` | Get by code |
| GET | `/permissions/Exists/code/{code}` | `identity.permissions.read` | Check existence by code |
| POST | `/permissions` | `identity.permissions.create` | Create a permission |
| PUT | `/permissions/{id}` | `identity.permissions.update` | Update a permission |
| DELETE | `/permissions/{id}` | `identity.permissions.delete` | Delete a permission |

### Permission assignments: `/permission-users`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/permission-users` | `identity.permission-users.list` | List assignments |
| GET | `/permission-users/{id}` | `identity.permission-users.read` | Get an assignment |
| GET | `/permission-users/exists/{userId}/{permissionId}` | `identity.permission-users.read` | Check whether a user holds a permission |
| POST | `/permission-users` | `identity.permission-users.create` | Grant a permission to a user |
| PUT | `/permission-users/{id}` | `identity.permission-users.update` | Update an assignment |
| DELETE | `/permission-users/{userId}/{permissionId}` | `identity.permission-users.delete` | Revoke a permission |

### Categories: `/categories`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/categories` | `categories.read` | List categories |
| GET | `/categories/{id}` | `categories.read` | Get by id |
| GET | `/categories/Exists/name/{name}` | `categories.read` | Check existence by name |
| GET | `/categories/Exists/code/{code}` | `categories.read` | Check existence by code |
| POST | `/categories` | `categories.create` | Create a category |
| PUT | `/categories/{id}` | `categories.update` | Update a category |
| DELETE | `/categories/{id}` | `categories.delete` | Delete a category |

### Room types: `/room-types`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/room-types` | `room-types.read` | List room types |
| GET | `/room-types/{id}` | `room-types.read` | Get by id |
| GET | `/room-types/Exists/name/{name}` | `room-types.read` | Check existence by name |
| GET | `/room-types/Exists/id/{id}` | `room-types.read` | Check existence by id |
| POST | `/room-types` | `room-types.create` | Create a room type |
| PUT | `/room-types/{id}` | `room-types.update` | Update a room type |
| DELETE | `/room-types/{id}` | `room-types.delete` | Delete a room type |

### Rooms: `/rooms`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/rooms` | `rooms.list` | List rooms |
| GET | `/rooms/{id}` | `rooms.read` | Get by id |
| GET | `/rooms/Exists/code/{code}` | `rooms.read` | Check existence by code |
| GET | `/rooms/Exists/id/{id}` | `rooms.read` | Check existence by id |
| POST | `/rooms` | `rooms.create` | Create a room (unique code) |
| PUT | `/rooms/{id}` | `rooms.update` | Update a room (partial update) |
| DELETE | `/rooms/{id}` | `rooms.delete` | Delete a room |

### Room operating hours: `/room-operating-hours`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/room-operating-hours` | `rooms.operating-hours.list` | List operating hours |
| GET | `/room-operating-hours/{id}` | `rooms.operating-hours.read` | Get by id |
| GET | `/room-operating-hours/rooms/{roomId}` | `rooms.operating-hours.read` | All hours of a room |
| GET | `/room-operating-hours/exists/id/{id}` | `rooms.operating-hours.read` | Check existence |
| POST | `/room-operating-hours/rooms/{roomId}` | `rooms.operating-hours.create` | Add hours to a room |
| PUT | `/room-operating-hours/{id}` | `rooms.operating-hours.update` | Update |
| DELETE | `/room-operating-hours/{id}` | `rooms.operating-hours.delete` | Delete |

### Room blockouts: `/room-blockouts`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/room-blockouts` | `rooms.blockouts.list` | List blockouts |
| GET | `/room-blockouts/{id}` | `rooms.blockouts.read` | Get by id |
| GET | `/room-blockouts/exists/id/{id}` | `rooms.blockouts.read` | Check existence |
| POST | `/room-blockouts` | `rooms.blockouts.create` | Create a blockout |
| PUT | `/room-blockouts/{id}` | `rooms.blockouts.update` | Update a blockout |
| POST | `/room-blockouts/{id}/cancel` | `rooms.blockouts.cancel` | Cancel a blockout (reason required) |
| DELETE | `/room-blockouts/{id}` | `rooms.blockouts.delete` | Delete a blockout |

### Reservations: `/reservations`

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| GET | `/reservations` | `reservations.list` | List reservations with rich filters (room, user, category, title, status, period, cancellation, ...) |
| GET | `/reservations/{id}` | `reservations.read` | Get by id |
| GET | `/reservations/exists/id/{id}` | `reservations.read` | Check existence |
| GET | `/reservations/rooms/{roomId}/conflicts/exists?startsAt=&endsAt=` | `rooms.availability.read` | Does the room have conflicts in this period? |
| GET | `/reservations/rooms/{roomId}/conflicts?startsAt=&endsAt=` | `rooms.availability.read` | Which reservations conflict in this period? |
| POST | `/reservations` | `reservations.create` | Create a reservation |
| PUT | `/reservations/{id}` | `reservations.update` | Update a reservation (re-validated) |
| POST | `/reservations/{id}/cancel` | `reservations.cancel` | Cancel a reservation |
| DELETE | `/reservations/{id}` | `reservations.delete` | Delete a reservation |

### Health checks (anonymous, no `X-Inbox-Id` required)

| Route | Purpose |
| --- | --- |
| `/health` | Overall status |
| `/health/ready` | Readiness: checks PostgreSQL and Redis |
| `/health/live` | Liveness: the process is responding |

### Example: from login to a reservation

```bash
# 1. Log in (use a fresh inbox id, see the Inbox section)
curl -X POST http://localhost:5093/api/v1/auth/Login \
  -H "Content-Type: application/json" \
  -H "X-Inbox-Id: <inbox-id-1>" \
  -d '{ "email": "<master-email>", "password": "<master-password>" }'

# 2. Create a reservation with the returned token
curl -X POST http://localhost:5093/api/v1/reservations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -H "X-Inbox-Id: <inbox-id-2>" \
  -d '{
        "roomId": "<room-uuid>",
        "categoryId": "<category-uuid>",
        "title": "Sprint planning",
        "description": "Planning for sprint 12",
        "startsAt": "2026-11-10T09:00:00-03:00",
        "endsAt": "2026-11-10T10:30:00-03:00",
        "idempotencyKey": "sprint-12-planning"
      }'
```

## Response Format

Every endpoint wraps its result in the same envelope:

```json
{
  "data": { },
  "message": "Reservation created successfully",
  "traceId": "0HN...",
  "success": true,
  "timestamp": "2026-11-01T18:00:00+00:00",
  "detailsError": null,
  "path": null,
  "apiVersion": 1,
  "errors": null
}
```

On failure `data` is `null`, `success` is `false` and `errors` lists the reasons. Paginated results use a `Page` object with `data`, `count`, `totalCount`, `pageNumber`, `pageSize`, `totalPages` and navigation flags (`hasNextPage`, `hasPreviousPage`, ...).

Common status codes: `200`, `201`, `400` (validation), `401` (unauthenticated), `403` (missing permission), `404`, `409` (business conflict or unavailable inbox message), `429` (account locked), `500`.

## Observability

Kairos exports **traces, metrics and logs** over OTLP to **Grafana Alloy**, which fans them out: traces go to both **Tempo** and **Jaeger**, metrics to Prometheus and logs to Loki. Grafana is pre-provisioned with the Prometheus, Tempo, Loki and Jaeger data sources.

| Signal | Source | Backend |
| --- | --- | --- |
| Traces | ASP.NET Core, HttpClient, Npgsql (one span per SQL command), Redis, custom `inbox.transaction` spans | Tempo and Jaeger |
| Metrics | ASP.NET Core, HttpClient, .NET runtime, Npgsql, custom `kairos.inbox.conflicts` counter | Prometheus |
| Logs | Serilog → OTLP | Loki |
| Infrastructure | cAdvisor, node-exporter, redis-exporter, postgres-exporter | Prometheus |
| Tracing backend | Jaeger internal telemetry (`jaeger:8888`) | Prometheus |

### Viewing traces

The same traces are available in two places:

- **Grafana → Explore → Tempo**: persistent (48h retention), correlated with logs (Loki) and metrics (Prometheus), with a service map.
- **Jaeger UI** (http://localhost:16686): pick the `kairos-api` service and click *Find Traces*. Useful for trace search, comparing traces and inspecting span trees.

> Jaeger runs with **in-memory storage**, so its traces are lost when the container restarts. Tempo keeps them on disk. Jaeger only receives traces; logs and metrics are not affected.
 
Requests are enriched with the `X-Inbox-Id` and the authenticated user id, making it easy to follow a single operation across traces and logs. Noise such as `/health` and `/swagger` is excluded from tracing, and exporting is disabled automatically in the `Testing` environment.

| Tool | Port |
| --- | --- |
| Grafana | 3001 |
| Prometheus | 9090 |
| Tempo | 3200 |
| Jaeger UI | 16686 |
| Loki | 3100 |
| Alloy UI | 12345 |
| OTLP (gRPC / HTTP) | 4317 / 4318 (Alloy) |
| cAdvisor | 8085 |

Jaeger's own OTLP ports (4317 / 4318) and its metrics port (8888) are not published on the host. Alloy and Prometheus reach them over the internal Docker network.

## Testing

```bash
# Unit tests: fast, no external dependencies
dotnet test tests/Kairos.Tests

# Integration tests: spin up real PostgreSQL 16 and Redis 7 containers (Docker required)
dotnet test tests/Kairos.IntegrationTests
```

- **Unit tests** cover the use cases of every module (Auth, Users, Categories, Rooms, Reservations, Blockouts and others) and the seeders, using Moq/NSubstitute, Bogus for data generation, and Shouldly/FluentAssertions for assertions.
- **Integration tests** boot the real API through `WebApplicationFactory` against **Testcontainers** PostgreSQL and Redis, exercising full HTTP flows, including authentication, permissions and Inbox processing, and verifying the resulting database state.

## Known Limitations

This project is under active development. At the time of writing:

- **No HTTP endpoint creates Inbox messages yet.** `X-Inbox-Id` must reference an existing `Pending` row, so manual testing requires inserting one directly (see above). The `Reliability/Inbox/Controller` and `Outbox` folders are placeholders for planned work.
- Several permission codes are seeded ahead of their features: **reservation attendees**, **reservation history**, and the reservation **check-in/check-out** fields exist in the schema or permission list, but have no endpoints yet.
- Swagger UI is only enabled in the `Development` environment.
- Default credentials and keys in `appsettings.json` are for local development only. See the [security note](#configuration).

## License

Add your license here (for example MIT).