# AI Agent Definitions

This directory is the navigation index for AI **agent definitions** used in the Market Data Collector
project. Copilot agent files live in `.github/agents/`; Claude agent files live in `.claude/agents/`.
Both sets are kept in sync so that Claude and Copilot have access to equivalent tooling.

---

## GitHub Copilot Agents (`.github/agents/`)

### Code Review Agent

**File:** [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md)
**Used by:** GitHub Copilot agents, Claude Code (`mdc-code-review` skill)

Defines the canonical **7-lens code review framework** for the MarketDataCollector codebase:

| Lens | Focus Area | Key Checks |
|------|-----------|------------|
| 1 | MVVM Architecture Compliance | Thin code-behind, BindableBase, dependency boundaries |
| 2 | Real-Time Performance | Blocking calls, hot-path allocations, channel policies, ADR-014 |
| 3 | Error Handling & Resilience | Exception hierarchy, provider resilience, shutdown paths |
| 4 | Test Code Quality | Naming, AAA structure, async patterns, isolation |
| 5 | Provider Implementation Compliance | Interface completeness, rate limiting, reconnection |
| 6 | Cross-Cutting Concerns | Dependency rules, C#/F# interop, benchmark conventions |
| 7 | Storage & Pipeline Integrity | AtomicFileWriter, WAL flush ordering, sink registration |

The Claude Code equivalent is the [`mdc-code-review`](../skills/README.md) skill.

---

### Documentation Agent

**File:** [`.github/agents/documentation-agent.md`](../../../.github/agents/documentation-agent.md)
**Used by:** GitHub Copilot agents
**Claude Code equivalent:** [`.claude/agents/mdc-docs.md`](../../../.claude/agents/mdc-docs.md)

Handles documentation maintenance and quality tasks:

- Keeping `CLAUDE.md`, `docs/ai/`, and other AI resources current
- Documentation quality checks (broken links, stale content, formatting)
- Automated intake for the `ai-known-errors.md` registry
- Generating and updating architecture docs, changelogs, and API references

---

### Brainstorming & Ideation Agent

**File:** [`.github/agents/mdc-brainstorm-agent.md`](../../../.github/agents/mdc-brainstorm-agent.md)
**Used by:** GitHub Copilot agents
**Claude Code equivalent:** [`mdc-brainstorm`](../skills/README.md#mdc-brainstorm) skill

Generates high-value, implementable ideas for the MarketDataCollector platform. Supports 11
brainstorm modes (Open Exploration, Problem-Focused, Persona-Focused, Domain-Focused, Competitive,
Quick Wins, Architecture/Refactoring, User Growth/Adoption, Technical Debt, UX/Information Design,
Skill Improvement). Produces a summary table + narrative ideas + synthesis with competitive signals.

---

### Provider Builder Agent

**File:** [`.github/agents/mdc-provider-builder-agent.md`](../../../.github/agents/mdc-provider-builder-agent.md)
**Used by:** GitHub Copilot agents
**Claude Code equivalent:** [`mdc-provider-builder`](../skills/README.md#mdc-provider-builder) skill

Builds complete, architecturally compliant data provider adapters via a 12-step guided process.
Covers `IMarketDataClient` (streaming), `IHistoricalDataProvider` (backfill), and
`ISymbolSearchProvider` (symbol search) with rate limiting, reconnection, attribute decoration,
DI registration, and a matching test scaffold.

---

### Test Writer Agent

**File:** [`.github/agents/mdc-test-writer-agent.md`](../../../.github/agents/mdc-test-writer-agent.md)
**Used by:** GitHub Copilot agents
**Claude Code equivalent:** [`mdc-test-writer`](../skills/README.md#mdc-test-writer) skill

Generates idiomatic xUnit + FluentAssertions tests with correct async patterns, isolation, naming
conventions, and mock setup for all major MDC component types. Applies 7 universal quality rules
and selects from 8 named patterns (A–H) based on the component type.

---

## Claude Code Agents (`.claude/agents/`)

### mdc-cleanup

**File:** [`.claude/agents/mdc-cleanup.md`](../../../.claude/agents/mdc-cleanup.md)
**Used by:** Claude Code

Cleanup specialist for the MarketDataCollector repository. Removes dead code, duplication,
anti-patterns, and stale documentation across C# 13, F# 8, WPF, and .NET 9 source files —
without changing observable behaviour. Covers 7 categories: dead code, anti-pattern correction,
duplication consolidation, WPF code-behind cleanup, documentation cleanup, CPM compliance,
and ADR attribute cleanup.

---

### mdc-docs

**File:** [`.claude/agents/mdc-docs.md`](../../../.claude/agents/mdc-docs.md)
**Used by:** Claude Code
**Copilot equivalent:** [`.github/agents/documentation-agent.md`](../../../.github/agents/documentation-agent.md)

Documentation maintenance specialist for the MarketDataCollector repository. Keeps docs accurate,
comprehensive, and up-to-date across the AI guidance system (`docs/ai/`), architecture docs,
provider docs, developer guides, `CLAUDE.md`, and the `ai-known-errors.md` registry.

---

## Symmetry Map

| Capability | Copilot Agent | Claude Agent / Skill |
|-----------|--------------|---------------------|
| Code review (7 lenses) | `code-review-agent.md` | `mdc-code-review` skill |
| Brainstorming & ideation | `mdc-brainstorm-agent.md` | `mdc-brainstorm` skill |
| Provider implementation | `mdc-provider-builder-agent.md` | `mdc-provider-builder` skill |
| Test generation | `mdc-test-writer-agent.md` | `mdc-test-writer` skill |
| Documentation maintenance | `documentation-agent.md` | `mdc-docs` agent |
| Code cleanup / anti-pattern fix | *(via code-review-agent.md)* | `mdc-cleanup` agent |

---

## Agent Discovery

GitHub Copilot discovers agent definition files from `.github/agents/`. When assigning work to
Copilot agents, referencing these files in the issue or prompt body improves output quality.

**Claude Code** discovers agents from `.claude/agents/` and skills from `.claude/skills/`.

---

## Related Resources

| Resource | Purpose |
|----------|---------|
| [`docs/ai/skills/README.md`](../skills/README.md) | Claude Code skill details |
| [`docs/ai/README.md`](../README.md) | Master AI resource index |
| [`docs/ai/ai-known-errors.md`](../ai-known-errors.md) | Error prevention registry |
| [`docs/ai/instructions/README.md`](../instructions/README.md) | Path-specific Copilot instructions |

---

*Last Updated: 2026-03-17*
