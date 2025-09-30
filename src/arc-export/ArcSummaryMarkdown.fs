module ArcSummaryMarkdown

open ARCtrl
open ARCtrl.FileSystem
open ARCtrl.ArcPathHelper

let [<Literal>] MARKDOWN_TEMPLATE = """## [Data set] [[ARC_TITLE]]

### Registered ARC content:

[[FILE_TREE]]
"""
open System.IO

let pathOfCell (path : string) (c : CompositeCell) = 
    match c with
    | CompositeCell.FreeText ft -> Path.Combine(path, ft)
    | CompositeCell.Data d -> d.NameText
    | CompositeCell.Term t -> Path.Combine(path, t.NameText)
    | CompositeCell.Unitized (_,u) -> Path.Combine(path, u.NameText)
    |> fun s -> s.Replace("\\", "/")


let getRegisteredPayload (arc : ARC) (ignoreHidden:bool) =

    let copy = arc.Copy()

    let registeredStudies =     
        copy.Studies.ToArray()
        
    let registeredAssays =
        registeredStudies
        |> Array.map (fun s -> s.RegisteredAssays.ToArray()) // to-do: s.RegisteredAssays
        |> Array.concat

    let includeRootFiles : Set<string> = 
        set [
            InvestigationFileName
            READMEFileName
        ]

    let includeStudyFiles = 
        registeredStudies
        |> Array.map (fun s -> 
            let studyFoldername = $"{StudiesFolderName}/{s.Identifier}"

            set [
                yield $"{studyFoldername}/{StudyFileName}"
                yield $"{studyFoldername}/{READMEFileName}"

                //just allow any constructed path from cell values. there may be occasions where this includes wrong files, but its good enough for now.
                for table in s.Tables do
                    if table.TryGetInputColumn().IsSome then
                        for c in table.TryGetInputColumn().Value.Cells do
                            yield pathOfCell $"{studyFoldername}/{StudiesResourcesFolderName}" c
                    if table.TryGetOutputColumn().IsSome then
                        for c in table.TryGetOutputColumn().Value.Cells do
                            yield pathOfCell $"{studyFoldername}/{StudiesResourcesFolderName}" c
                    if table.TryGetProtocolNameColumn().IsSome then              
                        for c in table.TryGetProtocolNameColumn().Value.Cells do
                            yield pathOfCell $"{studyFoldername}/{StudiesProtocolsFolderName}" c
            ]
        )
        |> Set.unionMany

    let includeAssayFiles = 
        registeredAssays
        |> Array.map (fun a -> 
            let assayFoldername = $"{AssaysFolderName}/{a.Identifier}"

            set [
                yield $"{assayFoldername}/{AssayFileName}"
                yield $"{assayFoldername}/{READMEFileName}"

                //just allow any constructed path from cell values. there may be occasions where this includes wrong files, but its good enough for now.
                for table in a.Tables do
                    if table.TryGetInputColumn().IsSome then
                        for c in table.TryGetInputColumn().Value.Cells do
                            yield pathOfCell $"{assayFoldername}/{AssayDatasetFolderName}" c
                    if table.TryGetOutputColumn().IsSome then
                        for c in table.TryGetOutputColumn().Value.Cells do
                            yield pathOfCell $"{assayFoldername}/{AssayDatasetFolderName}" c
                    if table.TryGetProtocolNameColumn().IsSome then
                        for c in table.TryGetProtocolNameColumn().Value.Cells do
                            yield pathOfCell $"{assayFoldername}/{AssayProtocolsFolderName}" c
            ]
        )
        |> Set.unionMany

    let includeFiles = Set.unionMany [includeRootFiles; includeStudyFiles; includeAssayFiles]

    let fsCopy = arc.FileSystem.Copy() // not sure if needed, but let's be safe

    fsCopy.Tree
    |> FileSystemTree.toFilePaths()
    |> Array.filter (fun p -> 
        p.StartsWith(WorkflowsFolderName) 
        || p.StartsWith(RunsFolderName) 
        || includeFiles.Contains(p)
    )
    |> FileSystemTree.fromFilePaths
    |> fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFiles (fun n -> not (n.StartsWith("."))) else Some tree
    |> Option.bind (fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFolders (fun n -> not (n.StartsWith("."))) else Some tree)
    |> Option.defaultValue (FileSystemTree.fromFilePaths [||])



type ARCtrl.FileSystem.FileSystemTree with

    static member createItemString (level:int) (item: string) =
        $"""{String.replicate level "    "}- {item}""" 

    static member toMarkdownTOC (tree: FileSystemTree) =
        let rec loop (level:int) (acc:string list) (fs: FileSystemTree) =
            match fs with
            | FileSystemTree.File item -> (FileSystemTree.createItemString level item) :: acc

            | FileSystemTree.Folder (item, subtrees) ->
                // determine the local accumulator at this level
                let localAccum = 
                    if level < 0 then
                        acc
                    else
                        FileSystemTree.createItemString level item :: acc
            
                let finalAccum = subtrees |> Array.fold (fun acc elem -> loop (level+1) acc elem) localAccum
                // ... and return it
                finalAccum

        tree
        // yes i know. but i do really not want to implement a recursive sort in this tree just to make this work across OSes. Sue me =)
        |> FileSystemTree.toFilePaths(true)
        |> Array.sort
        |> FileSystemTree.fromFilePaths
        |> loop 0 []
        |> Seq.rev
        |> String.concat System.Environment.NewLine