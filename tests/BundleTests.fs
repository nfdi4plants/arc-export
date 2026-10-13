module BundleTests

open System
open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json
open ARCtrl
open Xunit
open TestUtils

type private TemporaryWorkspace() =
    let root = Path.Combine(Path.GetTempPath(), "arc-export-bundle-tests", Guid.NewGuid().ToString("N"))

    do Directory.CreateDirectory(root) |> ignore

    member _.CreateDirectory(name: string) =
        let path = Path.Combine(root, name)
        Directory.CreateDirectory(path) |> ignore
        path

    interface IDisposable with
        member _.Dispose() = Directory.Delete(root, true)

let private createArc () = ARC.fromArcInvestigation(ArcInvestigation("bundle-test"))

let private readBundle outDir =
    use archive = ZipFile.OpenRead(Path.Combine(outDir, Writers.ro_crate_bundle_filename))
    let entries =
        archive.Entries
        |> Seq.map (fun entry ->
            use source = entry.Open()
            use destination = new MemoryStream()
            source.CopyTo(destination)
            entry.FullName, destination.ToArray())
        |> Seq.toArray

    let names = entries |> Array.map fst
    Assert.Equal(names.Length, names |> Array.distinct |> Array.length)
    entries |> Map.ofArray

let private entryText name (entries: Map<string, byte[]>) = Encoding.UTF8.GetString(entries[name])

let private assertMetadataEqual expected actual =
    let normalize (value: string) = value.ReplaceLineEndings("\n") |> ROCrateDates.undateString
    Assert.Equal(normalize expected, normalize actual)

let private assertEntryNames expected (entries: Map<string, byte[]>) =
    Assert.Equal<string>(expected |> Seq.sort |> Seq.toArray, entries |> Map.keys |> Seq.sort |> Seq.toArray)

let private sourceFixture name = Path.GetFullPath(Path.Combine("../../../fixtures", name))

[<Fact>]
let ``bundle includes every supported root document and preserves its name and bytes`` () =
    use workspace = new TemporaryWorkspace()
    let repoDir = workspace.CreateDirectory("arc")
    let outDir = workspace.CreateDirectory("output")
    let documentNames =
        [| for stem in [| "ReAdMe"; "LiCeNsE"; "lIcEnCe" |] do
               for extension in [| ""; ".MD"; ".markdown"; ".TxT"; ".rSt" |] do
                   yield stem + extension |]

    let documents =
        documentNames
        |> Array.map (fun name ->
            let bytes = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (Encoding.UTF8.GetBytes($"{name}\r\nGrüße 🌱\r\n"))
            File.WriteAllBytes(Path.Combine(repoDir, name), bytes)
            name, bytes)

    for name in [| "README.json"; "README.md.bak"; "LICENSE.pdf"; "LICENCE.md.old"; "data.csv"; ".gitignore" |] do
        File.WriteAllText(Path.Combine(repoDir, name), "excluded")
    let nestedDir = Directory.CreateDirectory(Path.Combine(repoDir, "studies", "study1")).FullName
    File.WriteAllText(Path.Combine(nestedDir, "README.md"), "nested documentation")
    File.WriteAllText(Path.Combine(outDir, "unrelated-output.txt"), "existing output")

    Writers.write_ro_crate_bundle repoDir outDir false (createArc ())

    let entries = readBundle outDir
    assertEntryNames
        (Array.append documentNames [| Writers.bundled_ro_crate_metadata_filename; "datahub-context.json" |])
        entries
    for name, bytes in documents do
        Assert.Equal<byte>(bytes, entries[name])
    Assert.False(File.Exists(Path.Combine(outDir, Writers.ro_crate_metadata_filename)))
    Assert.Equal("existing output", File.ReadAllText(Path.Combine(outDir, "unrelated-output.txt")))

[<Fact>]
let ``bundle succeeds when neither readme nor license exists`` () =
    use workspace = new TemporaryWorkspace()
    let repoDir = workspace.CreateDirectory("arc")
    let outDir = workspace.CreateDirectory("output")
    File.WriteAllText(Path.Combine(repoDir, "data.csv"), "value\n1")
    Directory.CreateDirectory(Path.Combine(repoDir, "README.md")) |> ignore

    Writers.write_ro_crate_bundle repoDir outDir false (createArc ())

    let entries = readBundle outDir
    assertEntryNames [| Writers.bundled_ro_crate_metadata_filename; "datahub-context.json" |] entries
    use context = JsonDocument.Parse(entryText "datahub-context.json" entries)
    Assert.True(context.RootElement.TryGetProperty("@context") |> fst)
    Assert.True(context.RootElement.TryGetProperty("@graph") |> fst)

[<Fact>]
let ``repeated bundle export replaces old contents without retaining stale entries`` () =
    use workspace = new TemporaryWorkspace()
    let repoDir = workspace.CreateDirectory("arc")
    let outDir = workspace.CreateDirectory("output")
    let readmePath = Path.Combine(repoDir, "README.md")
    File.WriteAllText(readmePath, "first version")
    Writers.write_ro_crate_bundle repoDir outDir false (createArc ())

    do
        use archive = ZipFile.Open(Path.Combine(outDir, Writers.ro_crate_bundle_filename), ZipArchiveMode.Update)
        use stale = new StreamWriter(archive.CreateEntry("stale.txt").Open())
        stale.Write("previous export only")

    File.Delete(readmePath)
    File.WriteAllText(Path.Combine(repoDir, "LICENSE"), "new license")
    Writers.write_ro_crate_bundle repoDir outDir false (createArc ())

    let entries = readBundle outDir
    assertEntryNames [| Writers.bundled_ro_crate_metadata_filename; "datahub-context.json"; "LICENSE" |] entries
    Assert.Equal("new license", entryText "LICENSE" entries)
    Assert.Single(Directory.GetFiles(outDir)) |> ignore

[<Fact>]
let ``failed bundle publication removes temporary files and preserves destination directory`` () =
    use workspace = new TemporaryWorkspace()
    let repoDir = workspace.CreateDirectory("arc")
    let outDir = workspace.CreateDirectory("output")
    let destination = Path.Combine(outDir, Writers.ro_crate_bundle_filename)
    Directory.CreateDirectory(destination) |> ignore
    let marker = Path.Combine(destination, "keep.txt")
    File.WriteAllText(marker, "preserve me")

    let error = Record.Exception(Action(fun () -> Writers.write_ro_crate_bundle repoDir outDir false (createArc ())))

    Assert.NotNull(error)
    Assert.Equal("preserve me", File.ReadAllText(marker))
    Assert.Empty(Directory.GetFiles(outDir))

[<Theory>]
[<InlineData(false)>]
[<InlineData(true)>]
let ``CLI bundle metadata matches the selected pinned reference`` includeLfsHashes =
    use workspace = new TemporaryWorkspace()
    let outDir = workspace.CreateDirectory("output")
    // Use the source submodule: the fixture copied into bin has a broken .git pointer.
    let repoDir = sourceFixture "ArcPrototype"
    let args =
        [| yield! [| "-p"; repoDir; "-o"; outDir; "-f"; "rocrate-bundle" |]
           if includeLfsHashes then yield "--include-lfs-hashes" |]
    let result = runTool "arc-export" args "."

    Assert.Equal(0, result.ExitCode)
    Assert.DoesNotContain("Internal Error", result.Result.Output)
    let entries = readBundle outDir
    assertEntryNames
        [| Writers.bundled_ro_crate_metadata_filename; "datahub-context.json"; "README.md"; "LICENSE.md" |]
        entries
    let expected =
        if includeLfsHashes then ReferenceObjects.ArcPrototype.arc_ro_crate_metadata_lfs
        else ReferenceObjects.ArcPrototype.arc_ro_crate_metadata
    assertMetadataEqual expected (entryText Writers.bundled_ro_crate_metadata_filename entries)
    for name in [| "README.md"; "LICENSE.md" |] do
        Assert.Equal<byte>(File.ReadAllBytes(Path.Combine(repoDir, name)), entries[name])
    Assert.Single(Directory.GetFiles(outDir)) |> ignore

[<Fact>]
let ``CLI bundle with LFS hashes succeeds when no files are LFS tracked`` () =
    use workspace = new TemporaryWorkspace()
    let outDir = workspace.CreateDirectory("output")
    let result =
        runTool "arc-export"
            [| "-p"; sourceFixture "ARC-Export-TestFixture"; "-o"; outDir
               "-f"; "rocrate-bundle"; "--include-lfs-hashes" |]
            "."

    Assert.Equal(0, result.ExitCode)
    Assert.DoesNotContain("Internal Error", result.Result.Output)
    let entries = readBundle outDir
    assertEntryNames [| Writers.bundled_ro_crate_metadata_filename; "datahub-context.json"; "README.md" |] entries
    use metadata = JsonDocument.Parse(entryText Writers.bundled_ro_crate_metadata_filename entries)
    let hasHash =
        metadata.RootElement.GetProperty("@graph").EnumerateArray()
        |> Seq.exists (fun node -> node.EnumerateObject() |> Seq.exists (fun property -> property.NameEquals("sha256")))
    Assert.False(hasHash)

[<Fact>]
let ``bundle LFS flag leaves other selected export formats unchanged`` () =
    use workspace = new TemporaryWorkspace()
    let outDir = workspace.CreateDirectory("output")
    let result =
        runTool "arc-export"
            [| "-p"; sourceFixture "ArcPrototype"; "-o"; outDir
               "-f"; "rocrate-bundle"; "--include-lfs-hashes"
               "-f"; "rocrate-metadata"; "-f"; "isa-json"; "-f"; "summary-markdown" |]
            "."

    Assert.Equal(0, result.ExitCode)
    Assert.DoesNotContain("Internal Error", result.Result.Output)
    let entries = readBundle outDir
    assertMetadataEqual ReferenceObjects.ArcPrototype.arc_ro_crate_metadata_lfs
        (entryText Writers.bundled_ro_crate_metadata_filename entries)
    assertMetadataEqual ReferenceObjects.ArcPrototype.arc_ro_crate_metadata
        (File.ReadAllText(Path.Combine(outDir, Writers.ro_crate_metadata_filename)))
    Assert.True(File.Exists(Path.Combine(outDir, Writers.isa_json_filename)))
    Assert.True(File.Exists(Path.Combine(outDir, Writers.arc_summary_markdown_filename)))

[<Theory>]
[<InlineData(false)>]
[<InlineData(true)>]
let ``LFS inclusion flag without bundle target fails`` selectStandaloneFormat =
    use workspace = new TemporaryWorkspace()
    let outDir = workspace.CreateDirectory("output")
    let args =
        [| yield! [| "-p"; sourceFixture "ArcPrototype"; "-o"; outDir; "--include-lfs-hashes" |]
           if selectStandaloneFormat then yield! [| "-f"; "rocrate-metadata" |] |]

    let result = runTool "arc-export" args "."

    Assert.NotEqual(0, result.ExitCode)
    Assert.Empty(Directory.GetFiles(outDir))
