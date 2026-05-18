---
name: document-repo
description: Analyze and document repository structure for LLM Wiki knowledge base using rpiv-pi agents
---

## codebase-locator
model: llamacpp/qwen35-35b
output: .rpiv/artifacts/research/codebase-structure.md

Locate key files, directories, and components in the repository. Identify entry points, architecture patterns, technology stack, and modules that deserve wiki documentation.

## codebase-analyzer
model: llamacpp/qwen35-35b
reads: .rpiv/artifacts/research/codebase-structure.md
output: .rpiv/artifacts/research/implementation-details.md

Analyze implementation details from the located files. Trace data flow, identify architectural patterns, and document how components interact.

## web-search-researcher
model: llamacpp/qwen35-35b
prompt: "Research .NET 10 features, C# modern patterns, and any external libraries used in llamodem project"
output: .rpiv/artifacts/research/external-context.md

Research external tools, libraries, or frameworks. Gather official docs, best practices, and integration guidelines.

## wiki-bootstrap-check
model: llamacpp/qwen35-35b
reads:
  - .rpiv/artifacts/research/codebase-structure.md
  - .rpiv/artifacts/research/implementation-details.md
output: .rpiv/artifacts/wiki/wiki-readiness.md

Check if .llm-wiki/ exists. If not, call wiki_bootstrap(topic="llamodem", mode="company"). Report current state and readiness for insight capture.

## artifacts-analyzer
model: llamacpp/qwen35-35b
reads:
  - .rpiv/artifacts/research/codebase-structure.md
  - .rpiv/artifacts/research/implementation-details.md
  - .rpiv/artifacts/research/external-context.md
  - .rpiv/artifacts/wiki/wiki-readiness.md
output: 
  - .llm-wiki/wiki/concepts/repository-overview.md
  - .llm-wiki/wiki/concepts/architecture-patterns.md
  - .llm-wiki/wiki/entities/tools-and-libraries.md
  - .llm-wiki/wiki/analyses/technology-stack-analysis.md

Create initial wiki documentation for the repository. Generate multiple concept pages with Obsidian-style formatting, including frontmatter, wikilinks, and tags.

## commit
model: llamacpp/qwen35-35b
output: .rpiv/artifacts/commits/wiki-documentation-commit.md

Review generated wiki files in .llm-wiki/wiki/ and create structured commits documenting the repository structure.
