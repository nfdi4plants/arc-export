module ArcSummaryMarkdown

open FileSystemTreeExtension

open ARCtrl
open ARCtrl.FileSystem
open ARCtrl.NET

let [<Literal>] MARKDOWN_TEMPLATE = """## [Data set] [[ARC_TITLE]]

### Registered ARC content:

[[FILE_TREE]]
"""
open System.IO

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
        |> loop 0 []
        |> Seq.rev
        |> String.concat System.Environment.NewLine