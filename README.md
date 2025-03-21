# arc-export
Automatic building of a Docker container for exporting ARCs to Arc.json

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

# Setup

## local docker build
```shell
docker build . -t arc-export

docker run -v C:\Repos\ArcRepo:/arc arc-export:latest /arc-export -p arc
```

## download container first
```shell
docker pull ghcr.io/nfdi4plants/arc-export:main

docker run -v C:\Repos\ArcRepo:/arc ghcr.io/nfdi4plants/arc-export:main /arc-export -p arc
```

# Basic usage

## Output format Selection

Output format can be specified with the `-f` flag, followed by one of the following:

- `isa-json` will produce a `arc-isa.json` file
- `rocrate-metadata` will produce a `arc-ro-crate-metadata.json` file
- `summary-markdown` will produce a `arc-summary.md` file

E.g. 

```shell
docker run -v C:\Repos\ArcRepo:/arc arc-export:latest /arc-export -p arc -f rocrate-metadata -f isa-json -f summary-markdown
```

will produce all three output files.

## Help

```cli
USAGE: arc-export [--help] --arc-directory <path> [--out-directory <path>]
                  [--output-format <isa-json|rocrate-metadata|summary-markdown>]

OPTIONS:

    --arc-directory, -p <path>
                          Specify a directory that contains the arc to convert.
    --out-directory, -o <path>
                          Optional. Specify a output directory for the invenio metadata record.
    --output-format, -f <isa-json|rocrate-metadata|summary-markdown>
                          Optional. Specify the output format. Default is ISA-JSON.
    --help                display this list of options.
```
