## 1.0.0 (2026-10-28)

arc-export converts Annotated Research Contexts (ARCs) into machine-readable metadata and a human-readable content summary. Run it locally with .NET or in a Docker container, and generate multiple representations in one invocation.

### Export formats

| Format | Output file | Content |
| --- | --- | --- |
| `isa-json` | `arc-isa.json` | Investigation, study, and assay metadata in ISA-JSON format. This is the default when no format is specified. |
| `rocrate-metadata` | `arc-ro-crate-metadata.json` | JSON-LD metadata using the ARC RO-Crate representation. |
| `summary-markdown` | `arc-summary.md` | ARC title and a sorted tree of registered ARC content, excluding hidden files and folders. |
| `rocrate-metadata-lfs` | `arc-ro-crate-metadata.json` | Experimental RO-Crate export enriched with SHA-256 checksums and content sizes for Git LFS-tracked files. |

### Command-line usage

- Select the input ARC with `--arc-directory` / `-p`.
- Select one or more formats by repeating `--output-format` / `-f`.
- Set an output directory with `--out-directory` / `-o`. It is created if needed; otherwise, output is written to the input ARC directory.
- Display command-line help with `--help`.

For example, generate all three standard outputs:

```shell
arc-export -p ./path/to/arc -f isa-json -f rocrate-metadata -f summary-markdown -o ./exports
```

### Metadata handling

- Remove empty investigation contacts, study contacts, and assay performers before export.
- Resolve data-file references consistently before generating outputs.
- Preserve unregistered assays in ISA-JSON and Markdown exports: associate them with studies sharing named inputs or outputs, or create a placeholder study when no match exists. Existing study registrations are retained. RO-Crate exports do not receive these generated placeholder studies.
- Use `Untitled ARC` when a title is missing in RO-Crate metadata or the Markdown summary.
- Tolerate broken workflow and run entries during RO-Crate export.
- If metadata cannot be parsed but ARC files remain readable, attempt a minimal export containing error information.

### Git LFS support

- Enrich RO-Crate file entries with SHA-256 checksums and content sizes only for Git LFS-tracked files. Support both LFS pointers and checked-out data files.
- Export RO-Crate metadata for repositories without LFS-tracked files.
- Include the repository URL when `CI_PROJECT_URL` is set, and record an export publication timestamp. These additions apply to `rocrate-metadata-lfs`.

### Large ARC handling

- Automatically use ID references in ISA-JSON when an ARC contains more than 200 studies, or any individual study has more than 200 registered assays.
- Use an experimental faster Markdown export at the same threshold.

### Usage notes

- Both RO-Crate modes write `arc-ro-crate-metadata.json`. Choose one per invocation; selecting both leaves the LFS-enriched output as the final file.
- Running locally requires .NET 10.
- Git and Git LFS are needed for LFS metadata discovery when running locally; the container includes Git LFS.
- The LFS-enriched export and the faster Markdown export for large ARCs remain experimental. The faster Markdown export uses simplified rules to select which files to include.

## 0.1.0 (Released 2026-03-10)

### Changed:

* Update ARCtrl dependency to 3.0.3 (by @Freymaurer) 

## 0.0.0 (Released 2023-4-20)

### Added:

* Initial set up for RELEASE_Notes.md

