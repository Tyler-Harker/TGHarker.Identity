# NuGet Publishing Setup

This document describes the automated NuGet publishing setup for TGHarker.Identity.Client.

## Overview

The project uses GitHub Actions to automatically publish the `TGHarker.Identity.Client` package to NuGet whenever changes are pushed to the master branch that affect the Client project. **Version increments are completely automatic** based on your commit messages.

## Automated Versioning Strategy

The workflow uses **Conventional Commits** to automatically determine version bumps:

### Commit Message Format

Follow these commit message patterns for automatic version bumping:

| Commit Pattern | Version Bump | Example |
|----------------|--------------|---------|
| `fix: ...` or `fix(scope): ...` | **Patch** (1.0.0 → 1.0.1) | `fix: resolve authentication timeout` |
| `feat: ...` or `feat(scope): ...` | **Minor** (1.0.0 → 1.1.0) | `feat: add custom claims support` |
| `BREAKING CHANGE:` or `feat!:` or `fix!:` | **Major** (1.0.0 → 2.0.0) | `feat!: redesign client API` |
| Other commits (docs, chore, etc.) | **Patch** (1.0.0 → 1.0.1) | `chore: update dependencies` |

### How It Works

1. **Automatic Detection**: When you push changes to the Client directory, the workflow examines all commits since the last release
2. **Version Calculation**: Based on commit messages, it determines the appropriate version bump
3. **Tag Creation**: Automatically creates a Git tag (e.g., `v1.2.3`)
4. **Publishing**: Builds, packs, and publishes to NuGet
5. **Release Notes**: Creates a GitHub Release with categorized changes

### Examples

```bash
# Patch release (bug fix)
git commit -m "fix: resolve token refresh issue"
git push origin master
# Result: 1.0.0 → 1.0.1

# Minor release (new feature)
git commit -m "feat: add support for custom scopes"
git push origin master
# Result: 1.0.1 → 1.1.0

# Major release (breaking change)
git commit -m "feat!: redesign authentication flow

BREAKING CHANGE: Client initialization API has changed"
git push origin master
# Result: 1.1.0 → 2.0.0
```

## Setup Instructions

### 1. Create a NuGet API Key

1. Go to [nuget.org](https://www.nuget.org/)
2. Sign in with your account
3. Go to your account settings
4. Navigate to "API Keys"
5. Click "Create"
6. Configure the key:
   - Name: `TGHarker.Identity.Client GitHub Actions`
   - Package Owner: (select your account)
   - Scopes: `Push new packages and package versions`
   - Glob Pattern: `TGHarker.Identity.Client`
   - Expiration: Choose appropriate expiration
7. Copy the generated API key

### 2. Add the API Key to GitHub Secrets

1. Go to your GitHub repository
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Name: `NUGET_API_KEY`
5. Value: Paste the API key you copied
6. Click "Add secret"

### 3. (Optional) Create Initial Version Tag

If you want to start with a specific version number, create an initial tag:

```bash
git tag -a v1.0.0 -m "Initial release"
git push origin v1.0.0
```

If you skip this step, the workflow will automatically start at v1.0.0 on the first publish.

## Publishing Workflow

### Automatic Publishing

The workflow automatically runs when changes are pushed to the `master` branch that affect the `TGHarker.Identity.Client/` directory. That's it! No manual steps required.

**Complete workflow:**
1. Make changes to the Client library
2. Commit with a conventional commit message (e.g., `feat: add new feature`)
3. Push to master
4. The workflow automatically:
   - Calculates the new version
   - Creates a Git tag
   - Publishes to NuGet
   - Creates a GitHub Release with release notes

### Manual Trigger

You can also trigger the workflow manually:
1. Go to Actions tab in GitHub
2. Select "Publish NuGet Package" workflow
3. Click "Run workflow"
4. Select the branch (usually master)
5. Click "Run workflow"

## How to Publish Different Version Types

Control the version bump through your commit messages:

### Patch Release (Bug fixes)
```bash
# Make your changes
git add .
git commit -m "fix: resolve authentication timeout issue"
git push origin master
# ✅ Automatically publishes as 1.0.0 → 1.0.1
```

### Minor Release (New features)
```bash
# Make your changes
git add .
git commit -m "feat: add support for custom claims"
git push origin master
# ✅ Automatically publishes as 1.0.1 → 1.1.0
```

### Major Release (Breaking changes)
```bash
# Make your changes
git add .
git commit -m "feat!: redesign client initialization API

BREAKING CHANGE: The AddIdentityClient method signature has changed"
git push origin master
# ✅ Automatically publishes as 1.1.0 → 2.0.0
```

**No manual tagging required!** The workflow handles everything.

## Workflow Features

The GitHub Action workflow includes:

1. **Version Detection**: Uses MinVer to automatically determine the version
2. **Duplicate Check**: Checks if the version already exists on NuGet before publishing
3. **Build and Pack**: Compiles and packages the library
4. **Publish**: Pushes the package to NuGet.org
5. **Git Tagging**: Creates a Git tag for the published version
6. **GitHub Release**: Creates a GitHub release with release notes

## Troubleshooting

### No Version Published After Push

If the workflow runs but doesn't publish:
- Check if your commits affect the `TGHarker.Identity.Client/` directory
- The workflow only publishes when there are Client-related changes since the last tag
- Review the workflow logs to see if it detected "No changes to Client since last tag"

### Version Already Exists

If you see "Version already exists on NuGet, skipping publish":
- The calculated version has already been published
- This is normal and means the workflow is working correctly
- Make more changes and push to trigger a new version

### Build Failures

Check the Actions tab in GitHub for detailed error logs:
1. Go to Actions tab
2. Click on the failed workflow run
3. Review the logs for each step

### API Key Issues

If you see authentication errors:
1. Verify the `NUGET_API_KEY` secret is set correctly in GitHub
2. Check that the API key hasn't expired on nuget.org
3. Verify the API key has push permissions for the package

### Wrong Version Bump

If the wrong version type was published:
- Check your commit messages follow the conventional commit format
- `fix:` → patch, `feat:` → minor, `BREAKING CHANGE:` or `!` → major
- You can't unpublish from NuGet, but you can publish a corrected version immediately after

## Best Practices

1. **Use Conventional Commits**: Always follow the conventional commit format for clear versioning
   ```bash
   # Good
   git commit -m "feat: add custom claims support"
   git commit -m "fix: resolve token refresh race condition"
   git commit -m "feat!: redesign authentication API"

   # Bad (will default to patch)
   git commit -m "added new feature"
   git commit -m "fixed stuff"
   git commit -m "updates"
   ```

2. **Semantic Versioning**: The workflow automatically follows [SemVer](https://semver.org/) based on your commits
   - `fix:` → PATCH (bug fixes)
   - `feat:` → MINOR (new features, backward compatible)
   - `BREAKING CHANGE:` or `!` → MAJOR (breaking changes)

3. **Group Related Changes**: Make focused commits that can be clearly categorized
   ```bash
   # Instead of one big commit:
   git commit -m "feat: add multiple new features and fix bugs"

   # Make separate commits:
   git commit -m "feat: add custom claims support"
   git commit -m "feat: add token refresh callback"
   git commit -m "fix: resolve authentication timeout"
   ```

4. **Test Thoroughly**: Test changes before pushing to master, as publishing is automatic

5. **Review Release Notes**: After publishing, review the auto-generated GitHub Release and add any additional context if needed

6. **Breaking Changes**: When making breaking changes, use the `!` suffix or include `BREAKING CHANGE:` in the commit body
   ```bash
   git commit -m "feat!: redesign client initialization

   BREAKING CHANGE: The AddIdentityClient method now requires a configuration delegate instead of options object."
   ```
