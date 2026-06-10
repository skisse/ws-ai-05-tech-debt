# Architecture and Coding Standards with Claude Code

Demo repo for enforcing architecture rules, coding standards, and regulatory requirements (PSD2/DORA) using Claude Code in a .NET financial services codebase.

## What's in here

**Architecture rules** (`CLAUDE.md`, `.claude/rules/`) — strict three-layer architecture, audit logging as a regulatory requirement, `Result<T>` error handling, C# coding standards. Rules are loaded automatically by Claude Code on every interaction.

**Deterministic hook** (`.claude/hooks/block-db-in-controller.sh`) — blocks Claude from writing database calls directly in controllers. Runs before the file is written to disk.

**Slash commands** (`.claude/commands/`) — `/review-architecture` reviews a file against all rules with severity and line numbers; `/new-endpoint` scaffolds a compliant controller/service/repository from a description.

**PR review workflow** (`.github/workflows/claude-review.yml`) — runs Claude Code on every PR, posts inline comments on violations. Uses `REVIEW.md` as the review rule set.

**Sample code** (`src/`) — correct and intentionally violated implementations of the same payment controller for comparison.

## Structure

```
demo/
├── CLAUDE.md                              ← Architecture rules (loaded automatically)
├── REVIEW.md                              ← PR review rules
├── .github/workflows/claude-review.yml   ← Automatic PR review via GitHub Actions
├── .claude/
│   ├── settings.json                     ← Hook configuration
│   ├── commands/                         ← /review-architecture, /new-endpoint
│   ├── rules/                            ← Layering, audit logging, C# standard, 10-rule matrix
│   └── hooks/                            ← block-db-in-controller.sh
└── src/
    ├── Controllers/
    │   ├── PaymentController.cs           ← Correct implementation
    │   └── PaymentController_VIOLATIONS.cs ← 6 deliberate violations
    ├── Services/
    ├── Repositories/
    └── Models/
```
