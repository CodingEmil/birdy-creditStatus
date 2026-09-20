# Agent Instructions

## Birdy workflow

The repository and configured tracker are long-term memory. Conversation context is disposable.

This repository may be used by **Pi and Claude Code in parallel**. Both harnesses operate on the same durable state.

Do not create harness-specific copies of project state.

### Greenfield

`project-init → define-project → define-architecture → to-roadmap → choose feature → grill-with-docs → to-spec → to-tickets → implement per ticket → feature-review`

### Brownfield feature

`grill-with-docs → to-spec → to-tickets → implement per ticket → feature-review`

### Grill rule

Do not turn vague project intent, feature intent, behaviour changes, or significant design choices directly into implementation. Resolve ambiguity first.

### Persistent owners

- Shared agent rules: `AGENTS.md`
- Claude bridge: `CLAUDE.md` imports `AGENTS.md`
- Pi loading: `.pi/settings.json`
- Claude loading: `.claude/settings.json`
- Project intent: `PROJECT.md`
- High-level system design: `ARCHITECTURE.md`
- Domain language: `CONTEXT.md`
- Durable decisions: `docs/decisions/`
- Project feature index: `ROADMAP.md`
- Feature behaviour: feature spec / configured issue tracker
- Implementation work: tickets
- Executable truth: code and tests

### Session independence

A fresh Pi or Claude Code session must be able to continue from repository and tracker state.

Do not rely on conversation history as the only record of an important fact.

### User-controlled transitions

Completing one user-invoked workflow step does not authorize starting the next one automatically.

### Optional workflow audit

Use `workflow-check` only when drift is suspected or a health audit is useful. It is read-only and not a required phase.

## Next-step guidance

When a primary Birdy workflow skill finishes cleanly, end with one short, explicit `Next step:` hint naming the recommended next skill.

- Do not automatically invoke the next user-invoked skill.
- Do not ask whether the next skill should be run.
- If blocking questions or failures remain, do not advance the flow.
- Use the command syntax of the active harness.
