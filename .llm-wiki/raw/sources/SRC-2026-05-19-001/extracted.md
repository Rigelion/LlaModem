# Wiki Orphan Fix Pattern
Fixed wiki orphan issues by adding proper cross-references between entity and concept pages. Key patterns discovered:

## Entity Page Updates
- Added YAML frontmatter (`type: entity`, `created`, `updated`, `status`)
- Updated "Related Concepts" sections to use lowercase concept references (e.g., `[[headervalueinjector]]` instead of `[[HeaderValueInjector]]`)
- Applied to 14 entity pages: BasicAuthMiddleware, ConfigRecords, DashboardService, DefaultModelLauncher, HeaderValueInjector, HealthChecker, IdleTimeoutService, ModelManager, RequestForwarder, ResponseUsageMiddleware, StatsService, UsageExtractor, UsageService

## Concept Page Updates  
- Updated "Related Entities" sections with lowercase entity references
- Changed "Related Concepts" to "Related Synthesis" with link to `[[llamodemarchitectureoverview]]`
- Applied to 15 concept pages: ConfigRecords, DashboardService, DefaultModelLauncher, HeaderValueInjector, HealthChecker, IdleTimeoutService, ModelManager, RequestForwarder, ResponseUsageMiddleware, StatsService, UsageExtractor, UsageService

## Key Insight
Wiki lint flags pages as orphans when they have no inbound links. Adding proper `[[wikilinks]]` between related entity/concept pages creates the necessary cross-references to reduce orphan count. The lowercase naming convention for wikilinks matches actual file paths (e.g., `headervalueinjector.md`).

## Remaining Issues
- 42 orphans still exist (mostly concept pages with no inbound links from other pages)
- 276 missing pages (broken cross-references to non-existent pages like `[[ConfigRecords]]` in PascalCase)
- Source packets and synthesis pages also flagged as orphans

Sources:
- `.llm-wiki/outputs/lint-2026-05-19.md` - Lint report showing orphan count
- Entity/concept page updates during session
---
*Captured: 2026-05-19*
*Category: documentation*