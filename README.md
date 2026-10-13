# arc-export
Automatic building of a Docker container for exporting ARCs to various other ARC representations

# Develop

## Build

use VS or VSCode tooling or run `dotnet build` or `dotnet test` from the command line.

## Test
This repo uses external arcs as fixtures to create and test the output. These are referenced as _git submodules_ in `/tests/fixtures`.

To clone the repo with the submodules, use the following command:
```shell
git clone --recurse-submodules
```

If you have already cloned the repo, you can initialize the submodules with the following commands:
```shell
git submodule init
git submodule update
```

Note that the submodules are set on a specific commit, so **do not update them**.

To execute the tests run:
```shell
dotnet test
```

### Run locally with dotnet

This assumes you have the git submodules cloned as described in the test section.

```shell
dotnet run --project ./src/arc-export/arc-export.fsproj -- -p ./tests/fixtures/ArcPrototype/ -f rocrate-metadata -f isa-json -f summary-markdown
```

# Setup

## local docker build

> **Note:** the Docker build runs the test suite as part of the build, and the tests depend on the git submodules under `tests/fixtures`. Initialize submodules first (see [Test](#test)) — otherwise the build will fail.

```shell
docker build . -t arc-export

docker run -v C:\Repos\ArcRepo:/arc arc-export:latest /arc-export -p arc
```

## download container first

Images are published to `ghcr.io/nfdi4plants/arc-export` after successful tests on pushes to the `dev` and `release` branches that change `src/arc-export/**` or `Dockerfile`:

- `:dev` tracks the `dev` branch.
- `:release` tracks the `release` branch.
- `:latest` is an alias for the latest successful `release` image.
- `:dev-sha-<short-sha>` and `:release-sha-<short-sha>` retain the source branch and the first seven characters of the commit SHA after the rolling tags move, matching arc-validate.

Pushes and pull requests targeting either branch run the build and tests on Windows and Linux. Pull requests do not publish images.

The test jobs are defined in [build-and-test.yml](.github/workflows/build-and-test.yml), which runs directly for pull requests and pushes containing changes outside `src/arc-export/**` and `Dockerfile`. Source or Dockerfile changes trigger [pipeline.yml](.github/workflows/pipeline.yml), which calls the same local test workflow before publishing the image. Pushes changing both groups of files run both workflows.

```shell
docker pull ghcr.io/nfdi4plants/arc-export:release

docker run -v C:\Repos\ArcRepo:/arc ghcr.io/nfdi4plants/arc-export:release /arc-export -p arc
```

# Basic usage

## Example

The following command executed from within the ARC (`./`) creates all three output files in a folder (`arc-export`) inside the ARC. 

```bash
docker run --rm -v ./:/arc ghcr.io/nfdi4plants/arc-export:release /arc-export -p arc -f rocrate-metadata -f isa-json -f summary-markdown -o arc/arc-export
```

## Output format Selection

Output format can be specified with the `-f` flag, followed by one of the following:

- `isa-json` will produce an `arc-isa.json` file following the [ISA-JSON](https://isa-specs.readthedocs.io/en/latest/isajson.html#investigation-schema-json) specification
- `rocrate-metadata` will produce an `arc-ro-crate-metadata.json` file following the [ARC RO-Crate profile](https://github.com/nfdi4plants/arc-ro-crate-profile)
- `rocrate-bundle` will produce an `arc-ro-crate.zip` metadata bundle containing RO-Crate metadata, optional root README and license files, and administrative JSON-LD (see [Bundled RO-Crate export](#bundled-ro-crate-export))
- `summary-markdown` will produce an `arc-summary.md` file
- (experimental) `rocrate-metadata-lfs` will produce an `arc-ro-crate-metadata.json` file following the [ARC RO-Crate profile](https://github.com/nfdi4plants/arc-ro-crate-profile), where all objects of type `File` which are tracked by git-lfs additionally contain the according SHA256 hash 

E.g. 

```shell
docker run -v C:\Repos\ArcRepo:/arc arc-export:latest /arc-export -p arc -f rocrate-metadata -f isa-json -f summary-markdown
```

will produce all three basic output files.

## Bundled RO-Crate export

Use `-f rocrate-bundle` to create `arc-ro-crate.zip` in the output directory. The output directory defaults to the ARC directory; use `-o` to select another directory.

```shell
dotnet run --project ./src/arc-export/arc-export.fsproj -- -p ./tests/fixtures/ArcPrototype/ -o ./exports -f rocrate-bundle
```

The archive contains these files directly at its root:

```text
arc-ro-crate.zip
  ro-crate-metadata.json
  datahub-context.json
  README.md                 (if present; original filename preserved)
  LICENSE                   (if present; original filename preserved)
```

All root files named `README`, `LICENSE`, or `LICENCE`, optionally followed by `.md`, `.markdown`, `.txt`, or `.rst`, are included. Matching is case-insensitive, and all matches retain their original names and bytes. Missing README or license files are simply omitted. Nested documentation and research data are not included: this is a metadata bundle, not a complete ARC archive.

Plain RO-Crate metadata is included by default. Add `--include-lfs-hashes` to select the existing LFS-enriched metadata generation, including its fallback behavior when LFS information is unavailable:

```shell
dotnet run --project ./src/arc-export/arc-export.fsproj -- -p ./tests/fixtures/ArcPrototype/ -o ./exports -f rocrate-bundle --include-lfs-hashes
```

`--include-lfs-hashes` requires `-f rocrate-bundle` and affects only the metadata inside the bundle. Output formats can be combined, for example `-f rocrate-bundle -f isa-json -f rocrate-metadata`; standalone export formats and filenames keep their existing behavior. Re-exporting replaces the ZIP after the new archive has been written successfully, so obsolete entries from a previous export are not retained.

### Administrative JSON-LD

`datahub-context.json` records selected environment variables provided to the exporter. It is built and serialized with ARCtrl's JSON-LD capabilities in `ARCtrl.ROCrate`, using an inline context and standard RO-Crate/Schema.org vocabulary. Its graph is separate from the scientific RO-Crate metadata and has no scientific RO-Crate descriptor.

The graph contains:

- A `Dataset` identified by `datahub-context.json`, with `version` set to `"1"`, a `mentions` reference to `ro-crate-metadata.json`, and an `about` reference to the repository.
- A `PropertyValue` for each available allowlisted variable, linked through `variableMeasured`. Both `name` and `propertyID` preserve the exact variable name, and `value` preserves its original string.
- A repository `CreativeWork` projecting the project ID, title/name, description, and URL. Its identity is the project URL when available, or `#repository` otherwise.
- An optional revision `CreativeWork` projecting the commit SHA, title, and timestamp, linked to the repository through `isPartOf`.
- Separate optional `Person` nodes for the commit author and triggering user. The revision's `author` refers to the commit author; the context dataset's `mentions` refers to the triggering user. Triggering-user ID, login, name, and email map to `identifier`, `alternateName`, `name`, and `email`.

The commit author's `Name <email>` form is parsed conservatively, while its original environment value is retained. The triggering user is described as the pipeline starter, or the job starter when `CI_JOB_MANUAL=true`. These identities do not imply repository ownership or membership. Other internal graph nodes use stable document-local identifiers.

The exporter reads only the following allowlist. Combine each prefix with its listed suffixes to obtain full variable names. Their meanings are defined in the [GitLab predefined variables reference](https://docs.gitlab.com/ci/variables/predefined_variables/).

| Prefix | Included suffixes |
|---|---|
| `CI_PROJECT_` | `ID`, `NAME`, `TITLE`, `DESCRIPTION`, `URL`, `PATH`, `NAMESPACE`, `NAMESPACE_ID`, `ROOT_NAMESPACE`, `VISIBILITY`, `TOPICS`, `REPOSITORY_LANGUAGES`, `CLASSIFICATION_LABEL` |
| `CI_SERVER_` | `URL`, `NAME`, `HOST`, `PORT`, `PROTOCOL`, `FQDN`, `VERSION` |
| `CI_COMMIT_` | `SHA`, `SHORT_SHA`, `BEFORE_SHA`, `REF_NAME`, `REF_PROTECTED`, `BRANCH`, `TAG`, `TAG_MESSAGE`, `TIMESTAMP`, `TITLE`, `DESCRIPTION`, `MESSAGE`, `MESSAGE_IS_TRUNCATED`, `AUTHOR`, `USER_LOGIN` |
| `GITLAB_USER_` | `ID`, `LOGIN`, `NAME`, `EMAIL` |
| `CI_PIPELINE_` | `ID`, `IID`, `NAME`, `URL`, `SOURCE`, `CREATED_AT`, `SCHEDULE_DESCRIPTION` |
| `CI_JOB_` | `ID`, `NAME`, `GROUP_NAME`, `URL`, `STAGE`, `STATUS`, `STARTED_AT`, `MANUAL`, `IMAGE` |
| `CI_MERGE_REQUEST_` | `ID`, `IID`, `TITLE`, `DESCRIPTION`, `DESCRIPTION_IS_TRUNCATED`, `PROJECT_ID`, `PROJECT_PATH`, `PROJECT_URL`, `EVENT_TYPE`, `DIFF_BASE_SHA`, `REF_PATH`, `SOURCE_BRANCH_NAME`, `SOURCE_BRANCH_SHA`, `SOURCE_PROJECT_ID`, `SOURCE_PROJECT_PATH`, `SOURCE_PROJECT_URL`, `TARGET_BRANCH_NAME`, `TARGET_BRANCH_SHA`, `LABELS`, `ASSIGNEES`, `MILESTONE`, `APPROVED`, `DRAFT` |
| Additional variables (full names) | `CI_DEFAULT_BRANCH`, `CI_RELEASE_DESCRIPTION` |

Missing or blank values and optional nodes without supporting values are omitted. The context document is always emitted, including outside CI when no allowlisted variables are available. Credentials, `CI_REPOSITORY_URL`, and arbitrary custom variables are excluded. The exporter uses the supplied environment only and makes no API requests to enrich it.

### Forwarding variables into Docker

When starting the exporter with `docker run`, explicitly forward the desired variables from the calling environment with `-e`. For example, this command forwards project and revision metadata, the triggering user's identity, and the manual-job flag:

```shell
docker run --rm -v ./:/arc \
  -e CI_PROJECT_ID -e CI_PROJECT_NAME -e CI_PROJECT_TITLE -e CI_PROJECT_DESCRIPTION -e CI_PROJECT_URL \
  -e CI_COMMIT_SHA -e CI_COMMIT_TITLE -e CI_COMMIT_TIMESTAMP -e CI_COMMIT_AUTHOR \
  -e GITLAB_USER_ID -e GITLAB_USER_LOGIN -e GITLAB_USER_NAME -e GITLAB_USER_EMAIL -e CI_JOB_MANUAL \
  ghcr.io/nfdi4plants/arc-export:dev /arc-export -p arc -o arc/arc-export -f rocrate-bundle --include-lfs-hashes
```

This example uses shell line continuations; it can also be run on one line. Add further `-e VARIABLE_NAME` arguments for any other allowlisted values you want recorded. Variables already present in the exporter's container environment need no additional forwarding.

## Help

```cli
USAGE: arc-export [--help] --arc-directory <path> [--out-directory <path>]
                  [--output-format <isa-json|rocrate-metadata|rocrate-metadata-lfs|rocrate-bundle|summary-markdown>]
                  [--include-lfs-hashes]

OPTIONS:

    --arc-directory, -p <path>
                          Specify a directory that contains the arc to convert.
    --out-directory, -o <path>
                          Optional. Specify an output directory. Defaults to the ARC directory.
    --output-format, -f <isa-json|rocrate-metadata|rocrate-metadata-lfs|rocrate-bundle|summary-markdown>
                          Optional. Specify the output format. Default is ISA-JSON.
    --include-lfs-hashes   Include Git LFS hashes in the bundle. Requires -f rocrate-bundle.
    --help                display this list of options.
```
