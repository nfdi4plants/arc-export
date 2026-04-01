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
    printfn "It is writing here"
    printfn "Writing ARC RO-Crate metadata to %s" (Path.Combine(outDir, ro_crate_metadata_filename))
    if arc.Title.IsNone then
        arc.Title <- Some "Untitled ARC"
    let ro_crate_metadata = arc.ToROCrateJsonString(2, ignoreBrokenWR = true)
    let ro_crate_metadata_path = Path.Combine(outDir, ro_crate_metadata_filename)
    File.WriteAllText(ro_crate_metadata_path, ro_crate_metadata)


let write_ro_crate_metadata_LFSHashes (repoDir : string) (outDir: string) (arc: ARC) =
    printfn "Writing ARC RO-Crate metadata with LFS hashes to %s" (Path.Combine(outDir, ro_crate_metadata_filename))
    let sha256 = "http://schema.org/sha256"
    let contentSize = "http://schema.org/contentSize"
    if arc.Title.IsNone then
        arc.Title <- Some "Untitled ARC"
    
    let isa = arc.ToROCrateInvestigation(fs = arc.FileSystem, ignoreBrokenWR = true)
    LDDataset.setSDDatePublishedAsDateTime(isa, System.DateTime.Now)
    let graph = isa.Flatten()
    let customContextPart = Context.initBioschemasContext()
    customContextPart.AddMapping("sha256", sha256)
    let context = LDContext(baseContexts=ResizeArray[Context.initV1_1();customContextPart])
    graph.SetContext(context)
    graph.AddNode(ROCrate.metadataFileDescriptor)
    let gitlfsInfo = GitLFS.tryCreateGitLfsJson repoDir
    match gitlfsInfo with
    | Some info ->
        graph.Nodes
        |> Seq.iteri (fun i n -> 
            if LDFile.validate(n, ?context = graph.TryGetContext()) && not (n.Id.Contains("#")) && not (n.HasType(LDDataset.schemaType, ?context = graph.TryGetContext())) then
                match GitLFS.tryGetPathFromGitLfsJson n.Id info.files with
                | Some lfsFileInfo ->
                    match lfsFileInfo.oidType with
                    | "sha256" ->
                        n.SetProperty(sha256, lfsFileInfo.oid, ?context = graph.TryGetContext())
                    | _ -> 
                        printfn "WARNING: Only oid_type sha256 is supported at the moment."
                        ()
                    n.SetProperty(contentSize, $"{lfsFileInfo.size}b", ?context = graph.TryGetContext())
                | None -> 
                    let fullPath = Path.Combine(repoDir, GitLFS.normalizePathToGitPath n.Id)
                    // TODO: case-insensitive on windows, should be perfect match.
                    match File.Exists(fullPath) with 
                    | true ->
                        printfn "WARNING: No Git LFS object found for %s" fullPath
                    | false ->
                        printfn "ERROR: File on path does not exist: %s" fullPath
            )
    | None ->
        printfn "Oh No! Unable to generate git lfs json (`git lfs ls-files -j`), consider updating git-lfs (>= v3.2.0). Trying to explicitly parse files instead."
        graph.Nodes
        |> Seq.iteri (fun i n -> 
            if LDFile.validate(n, ?context = graph.TryGetContext()) && not (n.Id.Contains("#")) && not (n.HasType(LDDataset.schemaType, ?context = graph.TryGetContext()))  then
                printfn "checking lfs for index %i - %s" i n.Id
                match GitLFS.tryGetGitLFSObject repoDir n.Id with
                | Some lfsHash ->
                    match lfsHash.Hash with
                    | GitLFS.Hash.SHA256 hash ->
                        n.SetProperty(sha256, hash, ?context = graph.TryGetContext())
                    n.SetProperty(contentSize, $"{lfsHash.Size}b", ?context = graph.TryGetContext())
                | None -> 
                    printfn "No Git LFS object found for %s" n.Id
                    ()
        )
    graph.Compact_InPlace()
    let ro_crate_metadata = graph.ToROCrateJsonString(2)
    let ro_crate_metadata_path = Path.Combine(outDir, ro_crate_metadata_filename)
    File.WriteAllText(ro_crate_metadata_path, ro_crate_metadata)


let write_isa_json (outDir: string) (arc: ARC) =
    printfn "Writing ISA-JSON to %s" (Path.Combine(outDir, isa_json_filename))
    let isa_json = 
        // Include this guard to avoid timeouts on large ARCs
        // https://github.com/nfdi4plants/DataHUB/issues/51
        if arc.StudyCount > 200 || arc.Studies |> Seq.exists (fun s -> s.RegisteredAssayCount > 200) then
            printfn "\tLarge ARC detected (more than 200 studies or assays), using ID referencing in ISA-JSON export."
            arc.ToISAJsonString(2, useIDReferencing = true)
        else 
            arc.ToISAJsonString(2, useIDReferencing = false)
    let isa_json_path = Path.Combine(outDir, isa_json_filename)
    File.WriteAllText(isa_json_path, isa_json)

let write_arc_summary_markdown (outDir: string) (arc: ARC) =
    printfn "Writing ARC summary markdown to %s" (Path.Combine(outDir, arc_summary_markdown_filename))
    let registeredPayload = 
        if arc.StudyCount > 200 || arc.Studies |> Seq.exists (fun s -> s.RegisteredAssayCount > 200) then
            printfn "\tLarge ARC detected (more than 200 studies or assays), using experimental quick implementation of registered payload retrieval."
            getRegisteredPayload arc true
        else
            arc.GetRegisteredPayload(IgnoreHidden = true)
    let markdownContent =
        MARKDOWN_TEMPLATE
            .Replace("[[ARC_TITLE]]", arc.Title |> Option.defaultValue "Untitled ARC")
            .Replace("[[FILE_TREE]]", FileSystemTree.toMarkdownTOC registeredPayload)
    let arc_summary_markdown_path = Path.Combine(outDir, arc_summary_markdown_filename)
    File.WriteAllText(arc_summary_markdown_path, markdownContent)