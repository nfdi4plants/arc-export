module ArcSummaryMarkdown

open ARCtrl
open ARCtrl.FileSystem
open ARCtrl.NET

let [<Literal>] MARKDOWN_TEMPLATE = """## [Data set] [[ARC_TITLE]]

### File contents:

[[FILE_TREE]]
"""
open System.IO

type ARCtrl.FileSystem.FileSystemTree with

    static member createItemString (level:int) (item: string) =
        $"""{String.replicate level "    "}- {item}""" 
     
    member this.FilterNodes (predicate: string -> bool) = 
        let rec loop (parent: FileSystemTree) = 
            match parent with 
            | File n ->  
                if predicate n then Some (FileSystemTree.File n) else None 
            | Folder (n, children) -> 
                if predicate n then 
                    let filteredChildren = children |> Array.choose loop 
                    if Array.isEmpty filteredChildren then  
                        None
                    else
                        Some (FileSystemTree.Folder (n,filteredChildren))
                else
                    None
        loop this 

    static member filterNodes (predicate: string -> bool) =
        fun (tree: FileSystemTree) -> tree.FilterNodes predicate

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
        |> FileSystemTree.filterNodes( 
            fun item -> 
                let predicate = not (item.StartsWith("."))
                predicate
        )
        |> Option.get
        |> loop 0 []
        |> Seq.rev
        |> String.concat System.Environment.NewLine