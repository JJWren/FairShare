# FairShare.Contracts

Wire DTOs shared by the API and the Blazor SPA — the single source of truth for every request/response shape. Referenced by both `FairShare.Api` and `FairShare.Web`, so a change here updates both sides of the wire at compile time.

## Layout

| File | Shapes |
|---|---|
| `Auth/AuthContracts.cs` | `LoginRequest`, `RegisterRequest`, `ChangePasswordRequest`, `AuthTokenResponse`, `AuthConfigResponse` |
| `Admin/AdminContracts.cs` | `UserListItem`, `CreateUserRequest`, `EditUserRequest`, `AdminResetPasswordRequest` |
| `Parents/ParentContracts.cs` | `ParentProfileDto`, `ParentProfileCreateRequest`, `ParentProfileUpdateRequest` (incl. `RowVersion` for optimistic concurrency) |
| `Calculation/CalculationContracts.cs` | `CalculationRequest`, `ParentDataDto`, `CalculationResponse`, `CalcErrorDto` |
| `Catalog/CatalogContracts.cs` | State/form summaries for the catalog endpoints |

## Rules

- DTOs only: no behavior, no EF entities, no domain logic. Validation via data annotations is fine (both the API's model binding and Blazor's `EditForm` honor them — one set of rules, enforced on both ends).
- Keep this project free of ASP.NET/EF dependencies; it must stay referenceable from Blazor WebAssembly.
- Renaming or removing a property is a breaking wire change — the SPA and API deploy separately in Docker, so version them together.
