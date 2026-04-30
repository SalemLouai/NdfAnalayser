---
name: Clean Architecture Boundaries
description: Enforce Clean Architecture dependency rules for NdfProcessor project
---

# Clean Architecture - Strict Rules

## Allowed Dependencies

- **Domain**: NO external dependencies (no NuGet, no project references)
- **Application**: references ONLY Domain
- **Infrastructure**: references Domain AND Application
- **Console**: references all projects

## Absolute Prohibitions

❌ **NEVER** reference Infrastructure from Domain
❌ **NEVER** reference Application from Domain  
❌ **NEVER** reference Infrastructure from Application
❌ **NEVER** put business logic in Infrastructure or Console

## Verification Checklist

Before adding a `using` statement or project reference:
1. Check the current layer
2. Verify the dependency follows the rules above
3. When in doubt: use an interface in Domain
