# Workshop Tasks — Session 5: AI-Assisted Technical Debt Remediation

Hands-on exercises for using Claude Code to identify, plan, and fix technical debt.
Work through them in order — each builds on the previous.

---

## Exercise 1 — Map the Debt Landscape

**Goal:** Use the onboarding skill to orient yourself, then produce a structured debt map against the architecture rules.

### Steps

1. Open `.claude/skills/codebase-onboarding/SKILL.md` and skim it.
   Focus on **Phase 4 — Risk and complexity**. This is the skill you built in session 2 — it's pre-installed here so you don't need to recreate it. Note what it looks for, and what it knows nothing about (hint: it has no knowledge of ACME Financial's architecture rules).

2. Open a Claude Code session in the project root. Run the onboarding skill:
   ```
   /codebase-onboarding
   ```
   Read the generated `doc/` file — especially the **Risk and complexity** section. This is your starting map.

3. Now ask Claude to produce a structured debt map against the rules:
   > "Read all the source files in `src/`. Read all five rule files: `.claude/rules/layering.md`, `.claude/rules/audit-logging.md`, `.claude/rules/arkitekturregler.md`, `.claude/rules/csharp-kodestandard.md`, and `.claude/rules/event-driven.md`. Cross-reference the source files against all five rule files. Produce a structured list of every rule violation you find, grouped by category (Layering, Audit Logging, Error Handling, etc.). For each item include: file, approximate line, which rule is violated, and your assessment of the business risk."

   **Why list the rule files explicitly:** Some rules have `paths:` frontmatter that limits automatic loading to specific directories. `event-driven.md` only loads automatically when editing `src/Consumers/` or `src/Events/` files — not during a broad analysis. Listing all five rule files explicitly bypasses this and ensures full coverage.

   Do not mention `debt-inventory.md` in this prompt — you want Claude's independent analysis.

4. Ask Claude to identify which items are regulatory violations vs. style/architecture debt:
   > "Of everything you found: which items are regulatory violations (PSD2, DORA, GDPR) rather than architecture or code quality problems? List them separately and explain the business consequence of each if left unfixed."

5. **Now** open `doc/debt-inventory.md` yourself and compare manually. What did Claude find that the inventory has? What did it miss? What did it find that the inventory does not have?

### Discussion
- What did the onboarding skill find in Phase 4 (Risk and complexity) vs. what the rule-based prompt found?
- Which violations surprised you the most?
- Which items carry the highest regulatory risk — and why does that matter more than fixing the most interesting technical problem first?

### Extension — When does `paths:` matter?

> Skip this if you're short on time. Do it if you want to understand when rules load automatically vs. when they don't.

1. Open `src/Consumers/PaymentEventConsumer.cs` in your editor. Ask Claude:
   > "Does this consumer follow the event-driven architecture rules?"

   Claude will likely not know what those rules are — it hasn't seen `.claude/rules/event-driven.md` because no `paths:` frontmatter triggers it for this file.

2. Open `.claude/rules/event-driven.md`. It now has:
   ```yaml
   ---
   paths:
     - "src/Consumers/**"
     - "src/Events/**"
   ---
   ```
   Start a **new session**. Open the same consumer file and ask the same question. Claude now loads the rule automatically.

3. Now run the Exercise 1 structured debt prompt again (`"Read src/ and cross-reference against .claude/rules/..."`). Claude finds the event-driven violations either way — because the prompt explicitly asks it to read the rule files.

**The distinction:**
- `paths:` = automatic context loading when you edit a matching file
- Explicit prompt = Claude reads the rules regardless of frontmatter
- Without `paths:` on `event-driven.md`: developers editing consumers never get the rule automatically. With it: Claude brings the rule into every consumer edit without being asked.

---

## Exercise 2 — Fix Layering Violations with DSF Agents

**Goal:** Adapt the DSF agent skills from session 1 to this codebase, then use them to fix the layering violations in `PaymentServiceManager` in a structured, reviewable way.

The target: replace the `PaymentServiceManager` god class with a properly layered implementation.

### Step 1 — Read the stem cells

Open `.claude/skills/_stamcelle/architect/SKILL.md`, `developer/SKILL.md`, and `reviewer/SKILL.md`.

These are the WS01 skills verbatim. As you read, note:
- What is **generic** — role definition, output format, process discipline
- What is **WS01-specific** — `.NET 10`, `IScreeningRule`, `long` (øre), sanctions lists, PEP checks, `ScreeningStatus`

This is what you need to replace.

### Step 2 — Adapt the skills

Create four new skills for this project:

```
.claude/skills/architect/SKILL.md
.claude/skills/developer/SKILL.md
.claude/skills/reviewer/SKILL.md
.claude/skills/retro/SKILL.md
```

You can do this manually or use `/skill-creator`. Either way, the adapted skills must:

**Architect:**
- Know the target: `PaymentServiceManager` → layered solution
- Know the rules: `CLAUDE.md` + `.claude/rules/layering.md`
- Output format: interfaces to create, classes to rename, order of changes
- Replace: `.NET 10` → `.NET 9`, remove screening pipeline, remove `long`/øre, remove sanctions/PEP

**Developer:**
- Know the stack: `.NET 9`, `IPaymentRepository`, `IAuditLogger`, `Result<T>`, `CancellationToken`
- Checklist driven by `arkitekturregler.md` rules 1–10 (not the WS01 compliance checklist)
- Constraint: fix debt only — no new features

**Reviewer:**
- Checklist based on `.claude/rules/` in this project
- Score each rule 0–100
- Remove: screening domain checklist, `IScreeningRule`, CI/GitHub Actions items

**Retro:**
- Reads architect plan + reviewer findings
- Finds coverage gaps and unread rule files
- Proposes exact text improvements to the architect skill
- Remove: any WS01-specific domain context

### Step 3 — Architect plans the fix

New session. Run `/architect` with:
> "Read `src/Services/PaymentService.cs`, `CLAUDE.md`, and `.claude/rules/layering.md`. Produce an implementation plan for replacing `PaymentServiceManager` with a layered solution. The plan must specify: (1) which interfaces to create, (2) which classes to create or rename, (3) which responsibilities move where, (4) in what order to make the changes to avoid breaking the build."

Save the plan output verbatim — you will hand it to the developer.

### Step 4 — Developer implements

New session. Run `/developer` with the architect plan pasted in full:
> "[paste architect output]
> Fix only what the plan says. Do not add new features."

Watch which files it touches. Does it follow the plan exactly?

If the developer tries to write `_dbContext` in a controller — the hook will block it. This is expected. Watch how Claude responds.

### Step 5 — Reviewer validates

New session. Run `/reviewer` across all modified files.

Feed each finding back to the developer agent. Iterate until reviewer scores all rules ≥ 80.

### Step 6 — Compare to the reference

Open `src/Services/PaymentService_CLEAN.cs`. Compare to what your developer agent produced.

What did the agent get right? What did it miss that the reference has?

### Step 7 — Run the retro skill

```
/retro architect
```

When it asks "Will you update the skill?" — decide together. This is how the skill improves through use.

### Discussion
- What did you change in the stem cell? What did you keep?
- Could the same stem cell work in a third project? What would need replacing?
- Where did the developer deviate from the architect plan? Why?
- What did the retro find that you would not have noticed without it?
- The hook blocked a layering violation automatically. What can't a hook catch? (Hint: audit logging.)

---

## Exercise 3 — Add Missing Audit Logs with TDD

**Goal:** The DSF pipeline in Exercise 2 fixed the architecture. The audit logs are still missing — 7 PSD2/DORA violations remain. Use the same two-prompt TDD methodology from WS04 to make the regulatory requirement visible in CI before implementing the fix.

The audit log requirement is in `.claude/rules/audit-logging.md` and CLAUDE.md. This is a regulatory requirement, not a style preference — no hook catches its absence.

### Steps

**Step 0 — same as WS04: open `dotnet watch test` in a second terminal.**

Keep it visible. You need to see red before you write a single line of implementation.

```bash
dotnet watch test --project tests/AcmeFinancial.Payments.Tests
```

**Prompt 1 — Tests only. Stop before implementing.**

1. Send this prompt to Claude:
   > "Write xUnit unit tests for `PaymentServiceManager` that verify `auditLogger.LogAsync` is called on every method that reads or writes customer data. Verify the exact `userId`, `action` string, and `resourceId` for each call. Use a mock `IAuditLogger`. Follow the naming convention `Method_Scenario_ExpectedResult` from `.claude/rules/csharp-kodestandard.md`. Run the tests and confirm they are red. Do not implement anything."

2. Run the tests. Confirm they fail.

   **A compile error is the best possible outcome here.** It means the test demands `IAuditLogger` in the constructor — and the legacy code doesn't have it. The requirement is visible before a single line of production code is written.

   A red test (failing assertion) is also correct. **If any test passes: stop.** The legacy code has no audit logging. A passing test means you are not testing what you think — fix the test before continuing.

**Prompt 2 — Implement. After you have seen red.**

3. Send this prompt to Claude:
   > "Now make all the audit logging tests pass. Add `IAuditLogger` to the constructor. Call `auditLogger.LogAsync(userId, action, resourceId)` in every method flagged in the tests. Do not touch anything outside the failing tests."

4. Run the tests again. Confirm green.

**Done when `dotnet watch test` shows green.**

The green test is the evidence. The requirement is now in the commit history and enforced in CI. That is the entire point of Exercise 3.

### Discussion
- What is different about TDD for a regulatory requirement vs. a functional requirement?
- The WS04 false-positive trap applies here too: a test that passes against legacy code proves nothing. What does a correct test for audit logging actually verify?
- The hook blocks `DbContext` deterministically. Could you write a hook that blocks missing audit logging? Why or why not?
- If someone removes `auditLogger.LogAsync` in a future PR — what happens? Trace the path: commit → CI → compliance audit.
