module Writers

open ARCtrl
open ARCtrl.Json
open System.IO
open ARCtrl.FileSystem
open ArcSummaryMarkdown

[<Literal>]
let ro_crate_metadata_filename = "arc-ro-crate-metadata.json"

[<Literal>]
let isa_json_filename = "arc-isa.json"

[<Literal>]
let arc_summary_markdown_filename = "arc-summary.md"

let write_ro_crate_metadata (outDir: string) (arc: ARC) =
    let ro_crate_metadata = ARC.toROCrateJsonString(2) arc
    let ro_crate_metadata_path = Path.Combine(outDir, ro_crate_metadata_filename)
    File.WriteAllText(ro_crate_metadata_path, ro_crate_metadata)

let write_isa_json (outDir: string) (arc: ARC) =
    let inv = arc.ISA |> Option.get
    let isa_json = inv.ToROCrateJsonString(2)
    let isa_json_path = Path.Combine(outDir, isa_json_filename)
    File.WriteAllText(isa_json_path, isa_json)

let write_arc_summary_markdown (outDir: string) (arc: ARC) =
    let inv = arc.ISA |> Option.get
    let registeredPayload = arc.GetRegisteredPayload(IgnoreHidden = true)
    let markdownContent =
        MARKDOWN_TEMPLATE
            .Replace("[[ARC_TITLE]]", inv.Title |> Option.defaultValue "Untitled ARC")
            .Replace("[[FILE_TREE]]", FileSystemTree.toMarkdownTOC registeredPayload)
    let arc_summary_markdown_path = Path.Combine(outDir, arc_summary_markdown_filename)
    File.WriteAllText(arc_summary_markdown_path, markdownContent)