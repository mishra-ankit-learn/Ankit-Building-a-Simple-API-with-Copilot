# UserManagementAPI — Code Flow & Middleware Explanation

## Overview

UserManagementAPI is an ASP.NET Core Minimal API that provides CRUD operations for user records. All route logic lives in `Program.cs`. Cross-cutting concerns (error handling, authentication, logging) are handled by three custom middleware components registered before any endpoint logic runs.

---

## Application Startup (`Program.cs`)

1. `WebApplication.CreateBuilder(args)` creates the host and registers services.
2. `AddOpenApi()` registers the OpenAPI document generator (available in Development only).
3. `WebApplication.Build()` produces the configured `app` instance.
4. Middleware is registered in a specific order (see below).
5. `app.UseHttpsRedirection()` is added after middleware.
6. Route groups and endpoints are mapped under `/api/users`.
7. `app.Run()` starts the Kestrel server.

---

## Middleware Pipeline

Middleware is registered in `Program.cs` in this order:

```
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
```

ASP.NET Core middleware forms a bidirectional pipeline. Each component calls `_next(context)` to pass the request forward and then resumes after that awaited call when the response travels back.

```
Incoming Request
       |
       v
┌─────────────────────────┐
│  ErrorHandlingMiddleware │  ← catches all exceptions from below
└────────────┬────────────┘
             |
             v
┌──────────────────────────────┐
│  TokenAuthenticationMiddleware│  ← blocks /api/* without valid token
└────────────┬─────────────────┘
             |
             v
┌──────────────────────────────────┐
│  RequestResponseLoggingMiddleware │  ← logs method/path/status/elapsed time
└────────────┬─────────────────────┘
             |
             v
┌──────────────────────────┐
│  HTTPS Redirection        │
└────────────┬─────────────┘
             |
             v
┌──────────────────────────┐
│  Endpoint (route handler) │
└──────────────────────────┘
```

---

## Middleware Details

### 1. ErrorHandlingMiddleware

**File:** `Middleware/ErrorHandlingMiddleware.cs`  
**Position:** First — wraps all other middleware and endpoints.

**What it does:**
- Wraps `_next(context)` in a `try/catch`.
- On success: passes through transparently.
- On unhandled exception:
  - Logs the exception with method and path using `ILogger.LogError`.
  - If the response has already started streaming, rethrows (cannot recover safely).
  - Otherwise clears the response and writes:
    - HTTP status `500 Internal Server Error`
    - `Content-Type: application/json`
    - Body: `{ "error": "Internal server error." }`

**Why first:** It must wrap everything downstream so any unhandled exception from auth, logging, or endpoints is caught and converted to a consistent error response.

---

### 2. TokenAuthenticationMiddleware

**File:** `Middleware/TokenAuthenticationMiddleware.cs`  
**Position:** Second — after error handling, before logging and endpoints.

**What it does:**
- Skips auth check for non-`/api` paths (e.g. OpenAPI metadata endpoints).
- Reads `Auth:ApiToken` from configuration. Falls back to `"techhive-dev-token"` if not set.
- Checks the `Authorization` header for a `Bearer <token>` value.
- Compares the provided token to the configured token (case-sensitive, ordinal comparison).
- On missing header, missing `Bearer ` prefix, or token mismatch: returns immediately with:
  - HTTP `401 Unauthorized`
  - Body: `{ "error": "Unauthorized. Invalid or missing token." }`
- On valid token: calls `_next(context)` to proceed.

**Why second:** Auth must run before endpoint logic so that unauthorized requests never reach business logic. Placing it after error handling ensures a misconfigured token value or unexpected auth error is still caught gracefully.

---

### 3. RequestResponseLoggingMiddleware

**File:** `Middleware/RequestResponseLoggingMiddleware.cs`  
**Position:** Third — just before endpoint execution.

**What it does:**
- Starts a `Stopwatch` before calling `_next(context)`.
- Uses `try/finally` to guarantee the log entry is written even if an exception propagates through.
- After the pipeline returns (or throws), logs:
  - HTTP method
  - Request path
  - Response status code
  - Elapsed milliseconds
- Log level is chosen based on response status code:
  - `5xx` → `LogLevel.Error`
  - `4xx` → `LogLevel.Warning`
  - `2xx/3xx` → `LogLevel.Information`

**Why third (closest to endpoints):** Logging here captures the final response status after all middleware has had a chance to modify it (e.g. auth returning 401 is also logged correctly because logging wraps endpoints but is inside auth middleware).

---

## Endpoint Logic (`Program.cs`)

All endpoints are grouped under `/api/users` using `app.MapGroup(...)`.

### In-memory data store

```
ConcurrentDictionary<int, User>     usersById      — primary store, fast O(1) ID lookup
ConcurrentDictionary<string, int>   emailToId      — email uniqueness index
int                                 idCounter       — atomic ID generator (Interlocked.Increment)
```

Two seed users are pre-loaded at startup.

### Endpoints

| Method | Route | Description | Success Code |
|--------|-------|-------------|--------------|
| GET | `/api/users` | List all users; optional `?department=` and `?isActive=` filters | 200 |
| GET | `/api/users/{id}` | Get single user by ID | 200 |
| POST | `/api/users` | Create new user | 201 |
| PUT | `/api/users/{id}` | Update existing user | 200 |
| DELETE | `/api/users/{id}` | Delete user | 204 |
| GET | `/api/users/trigger-error` | Intentionally throws to test error middleware | — |

### Validation (`UserValidator`)

Called on POST and PUT before any data mutation:
- `FullName` — required, minimum 2 characters.
- `Email` — required, must pass `EmailAddressAttribute.IsValid(...)`.
- `Department` — required.

Returns a `List<string>` of error messages. Non-empty list → `400 Bad Request`.

### Models

| Type | Role |
|------|------|
| `User` | Internal storage entity |
| `UserResponse` | Outbound DTO (mapped via `FromUser`) |
| `CreateUserRequest` | Inbound DTO for POST |
| `UpdateUserRequest` | Inbound DTO for PUT |

---

## Request Lifecycle Examples

### Happy path: `GET /api/users/1`

```
Request arrives
→ ErrorHandlingMiddleware: try block starts, calls next
→ TokenAuthenticationMiddleware: path starts with /api, token validated OK, calls next
→ RequestResponseLoggingMiddleware: stopwatch starts, calls next
→ Endpoint: usersById.TryGetValue(1) succeeds, returns 200 + UserResponse JSON
← RequestResponseLoggingMiddleware: finally block logs "HTTP GET /api/users/1 responded 200 in Xms" (LogLevel.Information)
← TokenAuthenticationMiddleware: returns
← ErrorHandlingMiddleware: try block completes, no exception
Response sent to client: 200 OK
```

### Auth failure: `GET /api/users` with missing token

```
Request arrives
→ ErrorHandlingMiddleware: try block starts, calls next
→ TokenAuthenticationMiddleware: no Authorization header found, writes 401 JSON, returns (does NOT call next)
← ErrorHandlingMiddleware: try block completes normally (401 is not an exception)
Response sent to client: 401 Unauthorized
```

> Note: Logging middleware is skipped entirely because TokenAuthenticationMiddleware short-circuits before calling `_next`.

### Unhandled exception: `GET /api/users/trigger-error`

```
Request arrives
→ ErrorHandlingMiddleware: try block starts, calls next
→ TokenAuthenticationMiddleware: token valid, calls next
→ RequestResponseLoggingMiddleware: stopwatch starts, calls next
→ Endpoint: throws InvalidOperationException
← RequestResponseLoggingMiddleware: finally block runs, status is 500 (set by error middleware below it if response not started) — logs "HTTP GET ... 500" (LogLevel.Error)
← ErrorHandlingMiddleware: catch block fires, LogError, writes 500 JSON response
Response sent to client: 500 Internal Server Error { "error": "Internal server error." }
```
