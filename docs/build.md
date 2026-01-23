# Building slskd from Source

## Prerequisites

1. [Node.js](https://nodejs.org/en/download/current) v18 or greater (with npm 9.6 or greater)
1. [.NET SDK](https://dotnet.microsoft.com/en-us/download) 8.0

## Easy Way

The easiest way to build everything, and the method used in automated builds, is to execute the `publish` script located in the `/bin` directory.  This is a [bash](https://www.gnu.org/software/bash/) script, so you'll need to run it with a console that can execute bash scripts; any Linux terminal, Windows Subsystem for Linux, or Git Bash (installs with Git on Windows).

Review the options for this script by executing:

```
./bin/publish --help
```

You should see something similar to the following:

```
options:
-h, --help          show help
--no-prebuild       skip build and test
--runtime           a valid RID (https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)
--platform          one of: linux/amd64, linux/arm64, linux/arm/v7.  overrides runtime.
--version           version for the binary. defaults to current git tag+SHA
--output            the output directory.  defaults to ../../dist/<runtime>
```

The `runtime` parameter is required, and must be a valid .NET [RID](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).  For most users this will be either `linux-x64` or `win-x64`, which will produce a 64 bit executable for Linux or Windows, respectively.

The `platform` parameter is designed to support multi-architecture Docker builds; if you'd like to author your own Dockerfile, refer to the one in the root of this repo for usage.

Output files will go to the `/dist` directory unless you specify a different location.

### Copy/Pasteable Shortcuts

Windows:

```
./bin/publish --runtime win-x64
```

macOS (Intel):

```
./bin/publish --runtime osx-x64
```

macOS (Apple/ARM):

```
./bin/publish --runtime osx-arm64
```

Linux (Typical):

```
./bin/publish --runtime linux-x64
```

Linux ([musl libc](https://wiki.musl-libc.org/projects-using-musl) distros like Alpine):

```
./bin/publish --runtime linux-musl-x64
```

If your platform is missing, review the automated build configuration in the `.github` directory for more info.

---

## CI/CD Build Process

**IMPORTANT:** Builds only happen on tags, not on code pushes.

### Build Triggers

The CI workflow (`ci.yml`) is configured to:
- ✅ **Run on tags**: Version tags, `build-dev-*`, `build-main-*`
- ✅ **Run on pull requests**: For testing (does not publish)
- ✅ **Run on manual dispatch**: `workflow_dispatch`
- ❌ **NOT run on pushes to master**: Prevents unwanted builds on documentation/code updates

### Triggering a Build

To trigger a build, create and push a tag:

```bash
# Main/stable release
git tag build-main-0.24.1-slskdn.41
git push origin build-main-0.24.1-slskdn.41

# Dev release
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
```

See `memory-bank/decisions/adr-0005-tagging-system.md` for complete tag format details.

---

## Automated Builds (CI/CD)

### ⚠️ Important: Builds Only Happen on Tags

**The CI workflow does NOT automatically build on code pushes. Builds ONLY happen when you create a tag.**

### How to Trigger a Build

**For stable releases:**
```bash
git tag build-main-0.24.1-slskdn.41
git push origin build-main-0.24.1-slskdn.41
```

**For dev releases:**
```bash
git tag build-dev-0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)
git push origin build-dev-0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)
```

### Build Workflows

- **CI Workflow** (`ci.yml`): Runs on tags only (not on code pushes)
- **Build on Tag** (`build-on-tag.yml`): Full release with packages
- **Dev Release** (`dev-release.yml`): Dev package builds

See `memory-bank/decisions/adr-0005-tagging-system.md` for detailed tag format and channel information.

## Hard Way

Your best bet will be to review the scripts in the `/bin` directory, but if your goal is to do this the hard way you've probably seen those and don't trust them for some reason.

slskd is composed of two separate parts; a front-end React project in the `/web` directory that was bootstrapped with `create-react-app`, and a .NET project in the `/slskd` directory that's a fairly basic .NET Web API project.  The front-end project is built into a set of static html, css and JavaScript files and the .NET application serves them to clients (and services API requests).

By default, the .NET project expects the static content to reside in the `wwwroot` directory beside the executable, but this can be changed in the configuration.  Assuming `wwwroot` is acceptable, you'll need to:

1. Build the React application within the `/web` directory using some variation of `npm build`
1. Publish the .NET application using some variation of `dotnet publish`
1. Move the static content from the React application into a `wwwroot` beside the built slskd executable

Running the executable should create any necessary directories or files.  The example configuration won't be copied to the default location unless it is placed in a `config` directory beside the executable before the first execution.