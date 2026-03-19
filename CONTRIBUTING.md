# Contributing to Stable Diffusion Studio

Thank you for your interest in contributing!

## Development Setup

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Install the [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
3. Clone the repository
4. Run `dotnet build` to verify everything compiles
5. Run `dotnet test` to verify all tests pass

## Coding Standards

- Follow existing code patterns and conventions
- Use modern C# (.NET 10) idioms
- Prefer records and value objects where appropriate
- Follow the modular monolith architecture (see [Architecture Overview](docs/architecture/README.md))

## Testing

- Write tests for all new domain logic and application services
- Follow TDD where practical
- Use xUnit + FluentAssertions + NSubstitute
- BDD E2E tests use Playwright + Reqnroll

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` — new feature
- `fix:` — bug fix
- `docs:` — documentation changes
- `test:` — test additions or changes
- `refactor:` — code refactoring
- `ci:` — CI/CD changes
- `deps:` — dependency updates

## Pull Requests

- Create a feature branch from `main`
- Keep PRs focused and small
- Fill out the PR template
- Ensure all tests pass before requesting review
