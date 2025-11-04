# Contributing to IoTPowerShellAgent

Thank you for your interest in contributing to IoTPowerShellAgent! This document provides guidelines and instructions for contributing.

## Code of Conduct

- Be respectful and inclusive
- Welcome newcomers and help them learn
- Focus on constructive feedback
- Respect different viewpoints and experiences

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/Calvindd2f/IoTPowerShellAgent.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Test your changes thoroughly
6. Commit your changes: `git commit -m "Add feature: description"`
7. Push to your fork: `git push origin feature/your-feature-name`
8. Open a Pull Request

## Development Setup

### Prerequisites

- .NET 8.0 SDK or later
- Windows 10/11 or Windows Server 2016+
- PowerShell 5.1 or later
- Visual Studio 2022 or VS Code (recommended)

### Building

```bash
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Coding Standards

### C# Code Style

- Use meaningful variable and method names
- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Use `using var` for disposable objects where appropriate
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose

### PowerShell Code Style

- Use approved verbs (Get-, Set-, New-, etc.)
- Follow PowerShell naming conventions
- Include help comments for functions

### Error Handling

- Always handle exceptions appropriately
- Provide meaningful error messages
- Log errors with appropriate severity levels
- Use structured error details for PowerShell errors

## Testing

- Write unit tests for new features
- Ensure existing tests pass
- Test edge cases and error conditions
- For PowerShell executor tests, consider using Pester integration

### Running Pester Tests

If you add Pester tests, run them with:

```powershell
Invoke-Pester -Path .\src\IoTPowerShellAgent.Tests.Pester\PowerShell\
```

## Pull Request Process

1. Update documentation if needed
2. Ensure all tests pass
3. Update CHANGELOG.md if applicable
4. Ensure your code follows the coding standards
5. Request review from maintainers
6. Address any feedback

## Commit Messages

Use clear, descriptive commit messages:

- Use present tense ("Add feature" not "Added feature")
- First line should be a summary (50 chars or less)
- Include more details in the body if needed
- Reference issues/PRs if applicable

Examples:

```
Add async execution with cancellation token support

Implements ExecutePowerShellAsync method with SemaphoreSlim
throttling to prevent blocking IoT Hub listener thread.
```

## Versioning

This project uses [Semantic Versioning](https://semver.org/):

- MAJOR version for incompatible API changes
- MINOR version for backwards-compatible functionality
- PATCH version for backwards-compatible bug fixes

## Questions?

If you have questions, please:

- Open an issue for discussion
- Check existing issues and discussions
- Reach out to maintainers

Thank you for contributing!
