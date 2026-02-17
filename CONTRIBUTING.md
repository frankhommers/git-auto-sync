# Contributing

Thanks for your interest in contributing to Git Auto Sync.

## Development Setup

1. Install prerequisites:
   - .NET 10 SDK
   - Git
2. Clone and build:

```bash
git clone <your-fork-url>
cd git-auto-sync
dotnet restore
dotnet build GitAutoSync.slnx
```

3. Run tests:

```bash
dotnet test GitAutoSync.slnx
```

## Branches and Pull Requests

- Create a feature branch from `master`.
- Keep changes focused and atomic.
- Include a clear PR description:
  - what changed
  - why it changed
  - how it was validated

## Coding Guidelines

- Follow existing code style and naming conventions.
- Prefer small, readable methods over deeply nested logic.
- Add tests for behavioral changes when practical.
- Avoid introducing breaking API/CLI behavior without discussion.

## Commit Messages

Use concise, imperative messages. Examples:

- `fix daemon startup URL mismatch in packaged app`
- `add macOS app bundle build script`

## Reporting Bugs

When filing an issue, include:

- OS and architecture
- app version/commit SHA
- repro steps
- expected vs actual behavior
- relevant logs (with secrets removed)

## Security

Please do not disclose security vulnerabilities publicly first.
See `SECURITY.md` for reporting guidance.
