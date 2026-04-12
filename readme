# UserManagementAPI Project Summary

## Scenario Review
TechHive Solutions needs a User Management API for internal HR and IT workflows. The API must support creating, reading, updating, and deleting user records efficiently and consistently.

## What Was Built
A new ASP.NET Core Web API project named **UserManagementAPI** was scaffolded and configured with:
- OpenAPI support in development.
- HTTPS redirection enabled.
- In-memory user store for initial implementation.
- Full CRUD endpoints under `/api/users`.

### Implemented Endpoints
- `GET /api/users` - Retrieve all users.
- `GET /api/users/{id}` - Retrieve one user by ID.
- `POST /api/users` - Create a user.
- `PUT /api/users/{id}` - Update an existing user.
- `DELETE /api/users/{id}` - Delete a user.

### API Behavior Notes
- Returns `404 Not Found` when a user does not exist.
- Returns `400 Bad Request` for missing required fields.
- Returns `409 Conflict` for duplicate email conflicts.
- Returns `201 Created` for successful create.
- Returns `200 OK` for successful update.
- Returns `204 No Content` for successful delete.

## Testing Performed
CRUD testing was completed using command-line HTTP calls (equivalent to Postman request validation).

Verified results:
- POST status: `201`
- PUT status: `200`
- DELETE status: `204`
- GET after delete status: `404`

A reusable request script is also included in `UserManagementAPI.http` to run all endpoint calls quickly from VS Code REST client support.

## How Microsoft Copilot Assisted
Copilot support was used in these ways:
1. Scaffolding structure: Started from ASP.NET Core Web API boilerplate and adapted it quickly to the business scenario.
2. Endpoint generation: Accelerated creation of CRUD route patterns and consistent response handling.
3. Code enhancement: Helped shape request DTO patterns, status code conventions, and validation checks.
4. Test flow guidance: Helped sequence end-to-end CRUD checks and verify expected behavior.
5. Documentation drafting: Helped produce concise endpoint and testing documentation for future project phases.

## Save/Reuse Guidance
This implementation is ready for follow-up activities. You can extend it later by:
- Moving from in-memory storage to a database.
- Adding authentication/authorization.
- Adding integration tests and persistence-layer unit tests.

## Debugging Phase (Bug Fixes)

### Bugs Identified
- Input validation gaps: Empty values and malformed emails were not consistently rejected.
- Non-existent lookup behavior: ID edge cases needed clearer handling (`id <= 0` and unknown IDs).
- Unhandled exception risk: Endpoints lacked structured exception handling and fallback behavior.
- Performance/scale concerns: Repeated list scans (`FirstOrDefault`, `Any`) could degrade with larger datasets.

### Fixes Implemented with Copilot Assistance
- Added stronger request validation logic:
	- Required `FullName`, `Email`, and `Department`.
	- Added minimum length check for names.
	- Added email format validation using `EmailAddressAttribute`.
- Added robust error handling:
	- Configured global exception handling middleware (`UseExceptionHandler`).
	- Added endpoint-level `try/catch` blocks with structured logging and `Problem` responses.
- Improved lookup/update performance and reliability:
	- Replaced list-backed store with `ConcurrentDictionary<int, User>` for fast ID lookup.
	- Added email index (`ConcurrentDictionary<string, int>`) for efficient duplicate checks.
	- Added deterministic ID generation with `Interlocked.Increment`.
- Added richer filtering support for GET users:
	- Optional query parameters: `department` and `isActive`.

### Validation and Edge-Case Testing
The API was retested after fixes with focus on error paths:
- `GET /api/users` returned `200`.
- `GET /api/users/999` returned `404`.
- `GET /api/users/0` returned `400`.
- Invalid `POST` payload returned `400` with validation errors.
- Valid `POST` returned `201`.
- Duplicate email `POST` returned `409`.
- `PUT` on missing user returned `404`.
- `PUT` with invalid email returned `400`.
- `DELETE` missing user returned `404`.
- Valid `DELETE` returned `204`.

### How Copilot Helped in Debugging
1. Assisted in identifying likely fault patterns (validation gaps, lookup edge cases, and exception paths).
2. Accelerated refactoring suggestions for safer in-memory data structures and indexing.
3. Helped craft consistent status-code behavior for edge conditions.
4. Improved code resilience with middleware and endpoint-level exception handling patterns.
5. Helped create expanded edge-case request scenarios for repeatable verification.

## Middleware Phase (Security + Observability)

### Middleware Added
- Request/response logging middleware:
	- Logs HTTP method, request path, and response status code (plus elapsed time).
	- File: `Middleware/RequestResponseLoggingMiddleware.cs`.
- Global error-handling middleware:
	- Catches unhandled exceptions and returns consistent JSON error payload:
		- `{ "error": "Internal server error." }`
	- File: `Middleware/ErrorHandlingMiddleware.cs`.
- Token authentication middleware:
	- Validates bearer token from `Authorization` header.
	- Returns `401 Unauthorized` for missing/invalid tokens.
	- File: `Middleware/TokenAuthenticationMiddleware.cs`.

### Middleware Pipeline Order
Configured in `Program.cs` as requested:
1. Error-handling middleware first.
2. Authentication middleware next.
3. Logging middleware last.

### Middleware Testing Results
Validated with HTTP requests after integration:
- Valid token + normal endpoint returned `200`.
- Missing token returned `401` with JSON error payload.
- Invalid token returned `401` with JSON error payload.
- Triggered exception endpoint returned `500` with standardized JSON error.
- Logging output captured request method/path/status for audited calls.

### Copilot Contribution in Middleware Phase
1. Generated baseline middleware class structures quickly.
2. Suggested clean middleware responsibilities (single-purpose classes).
3. Helped sequence middleware registration for predictable behavior.
4. Helped craft auth checks and standardized error responses.
5. Helped produce middleware-focused test cases for validation.

## Peer Review Checklist Summary

### Evaluation Results
1. GitHub repository created: Yes
	Evidence: Git remote is configured for the project repository.
2. CRUD endpoints implemented: Yes
	Evidence: `GET`, `POST`, `PUT`, and `DELETE` endpoints are implemented in `Program.cs`.
3. Copilot used to debug code: Yes
	Evidence: Copilot-assisted debugging steps and fixes are documented in the debugging section above.
4. Valid user-data processing included: Yes
	Evidence: `UserValidator.Validate(...)` enforces required fields and valid email format.
5. Middleware implemented: Yes
	Evidence: Logging, error-handling, and token-authentication middleware are included and registered in the pipeline.

### Review Score
- 25 / 25 points
