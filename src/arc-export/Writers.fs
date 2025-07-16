module Writers

open ARCtrl
open ARCtrl.ROCrate
open ARCtrl.Json
open ARCtrl.Json.ARC
open System.IO
open ARCtrl.FileSystem
open ArcSummaryMarkdown
open ARCtrl.Conversion

[<Literal>]
let ro_crate_metadata_filename = "arc-ro-crate-metadata.json"

[<Literal>]
let isa_json_filename = "arc-isa.json"

[<Literal>]
let arc_summary_markdown_filename = "arc-summary.md"

let write_ro_crate_metadata (outDir: string) (arc: ARC) =
    let ro_crate_metadata = arc.ToROCrateJsonString(2)
    let ro_crate_metadata_path = Path.Combine(outDir, ro_crate_metadata_filename)
    File.WriteAllText(ro_crate_metadata_path, ro_crate_metadata)



let write_ro_crate_metadata_LFSHashes (repoDir : string) (outDir: string) (arc: ARC) =
    let sha256 = "https://schema.org/sha256"
    arc.MakeDataFilesAbsolute()
    let license = ROCrate.getDefaultLicense()
    let isa = arc.ISA.Value.ToROCrateInvestigation()
    LDDataset.setSDDatePublishedAsDateTime(isa, System.DateTime.Now)
    LDDataset.setLicenseAsCreativeWork(isa, license)
    let graph = isa.Flatten()
    let customContextPart = Context.initBioschemasContext()
    customContextPart.AddMapping("sha256", sha256)
    let context = LDContext(baseContexts=ResizeArray[Context.initV1_1();customContextPart])
    graph.SetContext(context)
    graph.AddNode(ROCrate.metadataFileDescriptor)
    graph.Nodes
    |> Seq.iter (fun n -> 
        if LDFile.validate(n, ?context = graph.TryGetContext()) && not (n.Id.Contains("#"))  then
            match GitLFS.tryGetGitLFSObject repoDir n.Id with
            | Some lfsHash ->
                match lfsHash.Hash with
                | GitLFS.Hash.SHA256 hash ->
                    n.SetProperty(sha256, hash, ?context = graph.TryGetContext())
            | None -> 
                printfn "No Git LFS object found for %s" n.Id
                ()
    )
    graph.Compact_InPlace()
    let ro_crate_metadata = graph.ToROCrateJsonString(2)
    let ro_crate_metadata_path = Path.Combine(outDir, ro_crate_metadata_filename)
    File.WriteAllText(ro_crate_metadata_path, ro_crate_metadata)


let write_isa_json (outDir: string) (arc: ARC) =
    let inv = arc.ISA |> Option.get
    let isa_json = inv.ToISAJsonString(2)
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