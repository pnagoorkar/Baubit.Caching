# Baubit.Template

A template repository for .NET projects with CircleCI integration, code coverage, and automated package publishing.

## Using This Template

Follow these steps to use this template for your new project:

1. **Update .circleci/config.yml with solution and project names in your repository**
   - Replace all instances of `<YOUR_SOLUTION_NAME>` with your solution name
   - Replace all instances of `<YOUR_PROJECT_NAME>` with your project name

2. **Add CODECOV_TOKEN_Your_Project_Name in Context_Prashant in CircleCI**
   - Go to CircleCI project settings
   - Navigate to Contexts and find `Context_Prashant`
   - Add an environment variable named `CODECOV_TOKEN_{YOUR_PROJECT_NAME}` 
     - Replace dots in your project name with underscores (e.g., `My.Project` becomes `CODECOV_TOKEN_My_Project`)
     - This is required for CircleCI environment variable naming conventions
   - Set the value to your Codecov token from Codecov.io

3. **Configure repo settings in GitHub - branch protection rules etc**
   - Set up branch protection rules for `master` and `release` branches
   - Configure required status checks
   - Set up code review requirements as needed

4. **Import projects in Codecov.io and Snyk.io**
   - Import your repository in [Codecov.io](https://codecov.io) for code coverage tracking
   - Import your repository in [Snyk.io](https://snyk.io) for security vulnerability scanning