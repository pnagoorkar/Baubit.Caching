# GitHub Copilot Instructions for Baubit Components

## About Baubit Component Libraries

This is part of the **Baubit framework** - a collection of focused, componentized .NET libraries designed for distributed systems and enterprise applications. Each component follows a strict philosophy of:

- **Zero or minimal external dependencies** - Components are self-contained and lightweight
- **Single responsibility** - Each library solves one problem well
- **High performance** - Optimized for production workloads with minimal allocations
- **Thread safety** - All public APIs are thread-safe by design
- **Production ready** - Comprehensive testing, CI/CD, and monitoring

## Technology Stack

- **Target Framework**: .NET Standard 2.0 (for broad compatibility) and .NET 9.0 (for modern features)
- **Language**: C# with language features compatible with .NET Standard 2.0
- **Testing**: xUnit with comprehensive unit and integration tests
- **CI/CD**: CircleCI with automated build, test, and publish pipelines
- **Package Distribution**: NuGet (GitHub Packages for pre-release, NuGet.org for releases)
- **Code Coverage**: Codecov integration with quality gates
- **Security**: Snyk.io monitoring for vulnerabilities

## Project Structure

All Baubit components follow a consistent structure:

```
Baubit.{ComponentName}/
├── Baubit.{ComponentName}/              # Main library project
│   ├── {Core classes and interfaces}
│   └── Baubit.{ComponentName}.csproj    # Multi-targets .NET Standard 2.0 and .NET 9.0
├── Baubit.{ComponentName}.Test/        # Test project
│   ├── {Test classes}
│   └── Baubit.{ComponentName}.Test.csproj
├── .circleci/                           # CI/CD configuration
│   └── config.yml                       # Build, test, pack, publish pipeline
├── Baubit.{ComponentName}.sln           # Solution file
├── README.md                            # Comprehensive documentation
├── codecov.yml                          # Code coverage configuration
├── LICENSE                              # MIT License
└── .github/
    └── copilot-instructions.md          # This file
```

## Coding Standards

### C# Style and Conventions

1. **Modern C# Features**
   - Use C# 7.3 features (compatible with .NET Standard 2.0)
   - Enable nullable reference types where supported: `<Nullable>enable</Nullable>`
   - Use tuples for returning multiple values
   - Use pattern matching (basic forms available in C# 7.3)
   - Leverage `readonly struct` for value types to prevent mutations
   - **Avoid**: Primary constructors, collection expressions, required members, file-scoped types (C# 10+)
   - **Avoid**: Default interface implementations (C# 8+)
   - **Avoid**: Records (C# 9+) - use classes or structs instead

2. **Naming Conventions**
   - PascalCase for public members, types, and namespaces
   - camelCase for private fields (no underscore prefix except for backing fields)
   - Prefix interfaces with `I` (e.g., `IValidator`); abstract classes with `A` (e.g., `AValidator`)
   - Suffix async methods with `Async` (e.g., `ValidateAsync`)
   - Use meaningful, descriptive names - avoid abbreviations

3. **Code Organization**
   - One type per file (exceptions for nested types)
   - Group related functionality in namespaces matching folder structure
   - Keep classes focused - prefer composition over large inheritance hierarchies
   - Order class members: constants, fields, constructors, properties, methods
   - Group by access modifier (public → protected → private)

4. **Documentation**
   - XML documentation comments for ALL public APIs
   - Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
   - Provide code examples in `<example>` tags for complex APIs
   - Document thread-safety guarantees explicitly
   - Explain performance characteristics for critical paths

### Architecture Principles

1. **Dependency Management**
   - **CRITICAL**: Minimize external dependencies - each dependency is a liability
   - Prefer .NET Standard 2.0 compatible APIs
   - If dependencies are required, ensure they support .NET Standard 2.0
   - Never add dependencies for convenience - only for essential functionality
   - Keep the dependency graph flat - avoid transitive dependency chains

2. **Thread Safety**
   - All public APIs MUST be thread-safe unless explicitly documented otherwise
   - Prefer lock-free algorithms using atomic operations:
     - `Volatile.Read()` and `Volatile.Write()` for visibility
     - `Interlocked.CompareExchange()` for atomic updates
     - Avoid `lock` statements in hot paths - use only when necessary
   - Document thread-safety guarantees in XML comments
   - Include concurrent tests for all shared state

3. **Performance**
   - Minimize allocations - prefer value types and object pooling
   - Avoid LINQ in hot paths - use for loops for performance-critical code
   - Use `Span<T>` and `Memory<T>` only when targeting .NET 9.0 specifically (use preprocessor directives)
   - Profile before optimizing - measure don't guess
   - **Note**: Stack allocation and advanced memory APIs are limited in .NET Standard 2.0

4. **Error Handling**
   - Use exception filters and specific exception types
   - Never swallow exceptions - log or rethrow with context
   - Validate inputs early - fail fast at public API boundaries
   - Provide detailed exception messages with actionable information

5. **API Design**
   - Follow Microsoft's Framework Design Guidelines
   - Design for extension - prefer virtual/abstract for extensibility points
   - Keep public surface area minimal - internal by default
   - Version APIs carefully - breaking changes require major version bumps
   - Provide both sync and async variants where I/O is involved
   - Use factory methods for complex initialization

## Testing Standards

### Test Structure

1. **Test Organization**
   - Mirror source project structure in test project
   - One test class per source class: `{ClassName}.Test` in`{ClassName}/Test.cs`
   - Group tests by feature using nested classes or clear method names
   - Use descriptive test method names: `MethodName_Scenario_ExpectedBehavior`

2. **Test Coverage Requirements**
   - Minimum 80% code coverage for new code (enforced by Codecov)
   - 100% coverage for critical paths (security, data integrity)
   - Test all public APIs with multiple scenarios
   - Include edge cases: null inputs, empty collections, boundary values
   - Add regression tests for all bugs discovered

3. **Test Types**
   - **Unit Tests**: Isolated tests with no external dependencies
   - **Integration Tests**: Test component interactions (use Testcontainers if needed)
   - **Concurrent Tests**: Verify thread safety using parallel execution
   - **Performance Tests**: Benchmark critical operations (optional, documented)

4. **Test Patterns**
   - Use AAA pattern: Arrange, Act, Assert
   - One logical assertion per test (multiple physical asserts are OK)
   - Use xUnit's `[Theory]` and `[InlineData]` for parameterized tests
   - Use xUnit's built-in assertions consistently
   - Mock external dependencies using interfaces

### Example Test Structure

```csharp
namespace Baubit.Identity.Test.GuidV7Generator
{
    public class Test
    {
        [Fact]
        public void GetNext_GeneratesMonotonicGuids_WhenCalledConcurrently()
        {
            // Arrange
            var generator = GuidV7Generator.CreateNew();
            var guids = new ConcurrentBag<Guid>();

            // Act
            Parallel.For(0, 1000, _ => guids.Add(generator.GetNext()));

            // Assert
            Assert.Equal(1000, guids.Distinct().Count());
        }
    }
}
```

## CI/CD Pipeline

### CircleCI Workflow

All Baubit components use a standardized CircleCI pipeline with four jobs:

1. **build**: Compiles the .NET solution using custom Docker image with code signing
2. **test**: Runs all tests with code coverage reporting to Codecov
3. **pack_and_publish**: Creates and publishes NuGet packages to GitHub Packages (master branch only)
4. **release**: Publishes to NuGet.org (release branch only)

### Pipeline Configuration

- Solution and project names are parameterized in `.circleci/config.yml`
- All jobs require the `Context_Prashant` CircleCI context for credentials
- Codecov token format: `CODECOV_TOKEN_{PROJECT_NAME}` (dots replaced with underscores)
- Branch protection on master and release branches
- Automated GitHub releases created when merging master to release

### Release Process

**To create a new release:**

1. Ensure all changes are merged to `master` and CI passes
2. Create a pull request from `master` to `release`
3. **IMPORTANT**: PR title and description become the release notes - make them detailed and accurate
4. After PR is merged:
   - GitHub release is automatically created with PR notes
   - NuGet package is published to nuget.org
   - Release tags are created automatically

#### Generating Release Notes

When asked to generate release notes, analyze commits on `master` branch since the latest release tag. Format:

**Added**
- New features and public APIs

**Changed**
- Modified behavior and API changes

**Fixed**
- Bug fixes and corrections

**Removed**
- Deprecated/removed features and APIs

**Breaking Changes**
- API changes requiring code updates

Focus on what users need to know: feature changes, API modifications, and behavioral updates. Omit internal implementation details, file names, and commit SHAs. Keep descriptions factual and concise. No fluff.

### Pre-commit Checklist

Before committing code:
- [ ] All tests pass locally: `dotnet test`
- [ ] Code builds without warnings: `dotnet build`
- [ ] XML documentation added for public APIs
- [ ] No new external dependencies (or justified if necessary)
- [ ] Code coverage meets minimum requirements
- [ ] README.md updated if public API changed
- [ ] Code compiles for both .NET Standard 2.0 and .NET 9.0 targets

## Common Development Tasks

### Adding a New Public API

1. Design the API following framework design guidelines
2. Implement with thread safety in mind
3. Add comprehensive XML documentation
4. Write unit tests covering all scenarios
5. Add integration tests if API interacts with other components
6. Update README.md with API reference and examples
7. Consider backward compatibility and versioning
8. Ensure compatibility with .NET Standard 2.0

### Modifying Existing APIs

1. Always add tests for modified behavior
2. Update documentation to reflect changes
3. Test against both .NET Standard 2.0 and .NET 9.0 targets

### Adding Dependencies

**ONLY add dependencies if absolutely necessary.** Before adding:

1. Justify why the dependency is essential
2. Evaluate the dependency's stability and maintenance
3. Check for security vulnerabilities
4. Ensure it supports .NET Standard 2.0
5. Document the decision in PR description
6. Update README.md dependencies section

### Performance Optimization

1. Profile first - identify bottlenecks with data
2. Write benchmarks using BenchmarkDotNet (optional but recommended)
3. Test before and after performance changes
4. Document performance improvements in PR
5. Ensure optimizations don't sacrifice readability or maintainability
6. Consider caching, object pooling, or lazy initialization patterns
7. Use preprocessor directives for target-specific optimizations

## Documentation Standards

### README.md Structure

Every component README should include:

1. **Overview**: What problem does this component solve?
2. **Installation**: NuGet package installation instructions
3. **Quick Start**: Simple code example to get started
4. **Features**: Key capabilities and use cases
5. **API Reference**: Public API documentation with examples
6. **Performance**: Characteristics and benchmarks (if relevant)
7. **Thread Safety**: Guarantees and considerations
8. **Contributing**: Link to contribution guidelines
9. **License**: MIT License reference

### Code Examples

- Use complete, working code examples
- Include necessary `using` statements
- Show realistic usage scenarios
- Provide both simple and advanced examples
- Test examples to ensure they compile and run
- Ensure examples work with .NET Standard 2.0

## Security Practices

1. **Input Validation**: Validate all public API inputs
2. **No Secrets in Code**: Use environment variables or Azure Key Vault
3. **Dependency Scanning**: Snyk.io monitors for vulnerabilities
4. **Least Privilege**: Request minimum required permissions
5. **Secure Defaults**: APIs should be secure by default
6. **Regular Updates**: Keep dependencies and .NET runtime updated

## Continuous Improvement

This document should evolve with the project. When you identify:

- New patterns or best practices
- Common pitfalls or mistakes
- Improved workflows or tools
- Better ways to structure code

**Submit a PR to update these instructions** to keep them current and valuable for all contributors.

## Additional Resources

- [.NET API Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET Standard 2.0 API Reference](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [xUnit Documentation](https://xunit.net/)
- [CircleCI .NET Docs](https://circleci.com/docs/language-dotnet/)
- [Semantic Versioning](https://semver.org/)

---

**Remember**: Baubit components are designed to be production-grade, high-performance building blocks that support broad compatibility through .NET Standard 2.0 while leveraging modern features when targeting .NET 9.0. Every line of code should reflect this commitment to quality, simplicity, and reliability.