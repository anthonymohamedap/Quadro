# Import audit migration notes

Add a new EF Core migration to persist enterprise import audit entities:

- `ImportSession`
- `ImportRowLog`

Expected commands (not executed by agent):

1. `dotnet ef migrations add AddImportAuditEntities`
2. `dotnet ef database update`

Tables should include a one-to-many relation:

- `ImportRowLog.ImportSessionId` -> `ImportSession.Id` (cascade delete).
