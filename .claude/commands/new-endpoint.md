Scaffold a new API endpoint for: $ARGUMENTS

Generate all required files following the architecture rules in CLAUDE.md:
- Controller action (thin — delegate to service, include auditLogger.LogAsync)
- Service method (return Result<T>, business logic here)
- Repository method (database access only)
- Interface updates for service and repository

Name all classes, methods and parameters according to csharp-kodestandard.md.
Do not add DbContext to the controller. Do not throw exceptions from service to controller.
