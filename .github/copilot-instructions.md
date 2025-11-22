# GitHub Copilot Instructions

This file contains instructions for GitHub Copilot to help you work more effectively with this template repository.

## About This Template

This is a Baubit .NET project template that uses CircleCI for continuous integration and deployment. The template includes:

- CircleCI configuration for build, test, pack/publish, and release workflows
- Code coverage reporting with Codecov
- NuGet package publishing

## Key Configuration Files

- `.circleci/config.yml` - CircleCI pipeline configuration with parameterized solution and project names
- `codecov.yml` - Code coverage configuration
- `README.md` - Documentation with setup instructions

## Working with This Template

When using this template for a new project:

1. Replace `<YOUR_SOLUTION_NAME>` and `<YOUR_PROJECT_NAME>` placeholders in `.circleci/config.yml`
2. Configure CircleCI context variables (CODECOV_TOKEN, etc.)
3. Set up GitHub repository settings (branch protection, etc.)
4. Import the project into Codecov.io and Snyk.io for monitoring

## CircleCI Workflow

The pipeline includes these jobs:
- **build**: Compiles the .NET solution
- **test**: Runs tests and uploads code coverage to Codecov
- **pack_and_publish**: Packages and publishes NuGet packages (master branch only)
- **release**: Publishes to NuGet.org (release branch only)

## Important Notes

- The test job expects a Codecov token in the format `CODECOV_TOKEN_{PROJECT_NAME}` where dots in the project name are replaced with underscores
- All jobs require the CircleCI context `Context_Prashant` to be configured with necessary credentials
