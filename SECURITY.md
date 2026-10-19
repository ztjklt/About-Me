# Security and credentials

Keep provider API keys, `GAME_SERVER_KEY`, `ABOUTME_PROXY_KEY` and WeChat secrets in local `server/.env` or your hosting provider's secret settings. Start from [`server/.env.example`](server/.env.example); every credential field is intentionally empty. Generate separate random values for the game-server and trusted-proxy keys.

Never put server credentials in Unity Resources, WebGL files, screenshots, issue reports, build manifests or client configuration. Server and proxy keys authenticate internal services; they must not be shared with browser clients. Local playtest and test-fixture strings are for isolated testing only.

`.gitignore` excludes environment files, databases, private keys and local outputs. `.dockerignore` separately prevents local secrets and profile stores from entering the build context. Neither mechanism removes files already tracked in Git; review staged files before committing.

## Before a push

With Gitleaks 8.30.1 installed, run from the repository root:

```sh
git diff --cached --stat
gitleaks git . --log-opts="--all" --redact=100 --no-banner
```

The repository workflow runs a full-history scan on pushes and pull requests. [`.gitleaksignore`](.gitleaksignore) contains only individually reviewed historical fingerprints: source/build checksums and one deterministic test fixture. It does not exclude whole directories or secret-detection rules. Do not add an exception for an actual credential.

## Reporting a concern

Use GitHub's private vulnerability reporting for this repository if it is enabled. Do not paste a working credential, personal introduction, account token or database into a public issue. A useful report identifies the affected file or endpoint, expected behavior, and a reproduction with placeholder values.

If a real credential is exposed, revoke or rotate it at its provider, remove it from current files, review all reachable branches and tags, and address retained Git history and hosted copies before republishing. Deleting a line in the latest commit alone does not remove it from older commits.

The public preparation review and its limitations are recorded in [`docs/PUBLIC_REPOSITORY_AUDIT.md`](docs/PUBLIC_REPOSITORY_AUDIT.md).
