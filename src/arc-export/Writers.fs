module Writers

open ARCtrl
open ARCtrl.ROCrate
open ARCtrl.Json
open ARCtrl.Json.ARC
open System.IO
open System.IO.Compression
open System.Text
open ARCtrl.FileSystem
open ArcSummaryMarkdown
open ARCtrl.Conversion

[<Literal>]
let ro_crate_metadata_filename = "arc-ro-crate-metadata.json"

[<Literal>]
let bundled_ro_crate_metadata_filename = "ro-crate-metadata.json"

[<Literal>]
let ro_crate_bundle_filename = "arc-ro-crate.zip"

[<Literal>]
let isa_json_filename = "arc-isa.json"

[<Literal>]
let arc_summary_markdown_filename = "arc-summary.md"


let projectID = 
    try 
        let var = System.Environment.GetEnvironmentVariable("CI_PROJECT_URL")
        if var = null || var = "" then None else Some var
    with
    | _ -> None

let create_ro_crate_metadata (arc: ARC) =
    if arc.Title.IsNone then
        arc.Title <- Some "Untitled ARC"
    arc.ToROCrateJsonString(2, ignoreBrokenWR = true)


let write_ro_crate_metadata (outDir: string) (arc: ARC) =
    let path = Path.Combine(outDir, ro_crate_metadata_filename)
    printfn "Writing ARC RO-Crate metadata to %s" path
    File.WriteAllText(path, create_ro_crate_metadata arc)


let create_ro_crate_metadata_LFSHashes (repoDir: string) (arc: ARC) =
    let sha256 = "http://schema.org/sha256"
    let contentSize = "http://schema.org/contentSize"
    if arc.Title.IsNone then
        arc.Title <- Some "Untitled ARC"
    
    let isa = arc.ToROCrateInvestigation(fs = arc.FileSystem, ignoreBrokenWR = true)
    LDDataset.setSDDatePublishedAsDateTime(isa, System.DateTime.Now)
    if projectID.IsSome then isa.SetProperty("http://schema.org/mainEntityOfPage", projectID.Value)
    let graph = isa.Flatten()
    let customContextPart = Context.initBioschemasContext()
    customContextPart.AddMapping("sha256", sha256)
    let context = LDContext(baseContexts=ResizeArray[Context.initV1_1();customContextPart])
    graph.SetContext(context)
    graph.AddNode(ROCrate.metadataFileDescriptor)
    let gitlfsInfo = GitLFS.tryCreateGitLfsJson repoDir
    match gitlfsInfo with
    | Some info when Array.isEmpty info.files ->
        printfn "No Git LFS tracked files found; writing RO-Crate without LFS hashes."
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
        // Determine which files are actually LFS-tracked. Without this, `git lfs pointer
        // --file` would generate a (bogus) hash for every file, LFS-tracked or not.
        let lfsTrackedFiles = GitLFS.tryGetLfsTrackedFiles repoDir
        match lfsTrackedFiles with
        | None -> printfn "WARNING: Could not list Git LFS files (`git lfs ls-files`). No LFS hashes will be written."
        | Some files -> printfn "Found %i Git LFS tracked file(s)." files.Count
        graph.Nodes
        |> Seq.iteri (fun i n ->
            if LDFile.validate(n, ?context = graph.TryGetContext()) && not (n.Id.Contains("#")) && not (n.HasType(LDDataset.schemaType, ?context = graph.TryGetContext()))  then
                let isLfsTracked =
                    match lfsTrackedFiles with
                    | Some files -> files.Contains(GitLFS.normalizePathToGitPath n.Id)
                    | None -> false
                if isLfsTracked then
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
    graph.ToROCrateJsonString(2)


let write_ro_crate_metadata_LFSHashes (repoDir: string) (outDir: string) (arc: ARC) =
    let path = Path.Combine(outDir, ro_crate_metadata_filename)
    printfn "Writing ARC RO-Crate metadata with LFS hashes to %s" path
    File.WriteAllText(path, create_ro_crate_metadata_LFSHashes repoDir arc)


let private isBundleDocument (path: string) =
    let stem = Path.GetFileNameWithoutExtension(path).ToUpperInvariant()
    let extension = Path.GetExtension(path).ToLowerInvariant()
    List.contains stem [ "README"; "LICENSE"; "LICENCE" ]
    && List.contains extension [ ""; ".md"; ".markdown"; ".txt"; ".rst" ]


let write_ro_crate_bundle (repoDir: string) (outDir: string) (includeLfsHashes: bool) (arc: ARC) =
    let outputPath = Path.Combine(outDir, ro_crate_bundle_filename)
    printfn "Writing ARC RO-Crate bundle to %s" outputPath
    let metadata =
        if includeLfsHashes then create_ro_crate_metadata_LFSHashes repoDir arc
        else create_ro_crate_metadata arc
    let context =
        DataHubContext.build (System.Environment.GetEnvironmentVariable >> Option.ofObj)
        |> DataHubContext.serialize
    let documents =
        Directory.GetFiles(repoDir)
        |> Array.filter isBundleDocument
        |> Array.sortWith (fun a b -> System.StringComparer.Ordinal.Compare(a, b))
    let temporaryPath = Path.Combine(outDir, $".{ro_crate_bundle_filename}.{System.Guid.NewGuid():N}.tmp")
    try
        // Dispose the archive before publishing so its central directory is complete.
        do
            use stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            use archive = new ZipArchive(stream, ZipArchiveMode.Create)
            let writeJson name (content: string) =
                let entry = archive.CreateEntry(name, CompressionLevel.Optimal)
                use writer = new StreamWriter(entry.Open(), UTF8Encoding(false))
                writer.Write(content)
            writeJson bundled_ro_crate_metadata_filename metadata
            writeJson DataHubContext.filename context
            for path in documents do
                archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal) |> ignore
        File.Move(temporaryPath, outputPath, true)
    finally
        if File.Exists(temporaryPath) then File.Delete(temporaryPath)


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
