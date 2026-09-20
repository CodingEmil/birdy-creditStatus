# Issue Tracker

Provider: GitHub Issues

Repository: CodingEmil/birdy-creditStatus

Use the `gh` CLI for issue operations.

Feature specs are GitHub Issues.
Implementation tickets are separate GitHub Issues that reference their spec issue.

Recommended labels:
- `spec` — feature specification
- `ready-for-agent` — a fresh implementation session may pick this up

Read:
`gh issue view <number> --comments`

List:
`gh issue list --state all --limit 200`

Create:
`gh issue create --title "<title>" --body-file <file> --label <label>`

Close:
`gh issue close <number>`
