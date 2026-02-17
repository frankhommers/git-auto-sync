# Security Policy

## Supported Versions

Security updates are provided for the latest `master` branch state and the
latest tagged release.

## Reporting a Vulnerability

Please report vulnerabilities privately and responsibly.

- Do **not** open a public GitHub issue for security findings.
- Contact the maintainer directly (GitHub profile contact) with:
  - description of the issue
  - impact
  - reproduction details
  - any proposed mitigation

We will acknowledge receipt as soon as possible and work with you on
validation, remediation, and coordinated disclosure.

## Scope Notes

Potentially sensitive areas include:

- daemon HTTP/WebSocket endpoints
- startup/login automation scripts
- Git command execution and repository path handling
- log content and privacy
