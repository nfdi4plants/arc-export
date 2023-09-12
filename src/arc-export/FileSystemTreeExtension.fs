module FileSystemTreeExtension

open ARCtrl
open ARCtrl.FileSystem

//temporarily add implementations from https://github.com/nfdi4plants/ARCtrl/pull/189
type ARCtrl.FileSystem.FileSystemTree with
     
    member this.FilterFiles (predicate: string -> bool) =
        let rec loop (parent: FileSystemTree) =
            match parent with
            | File n -> 
                if predicate n then Some (File n) else None
            | Folder (n, children) ->
                Folder (n, children |> Array.choose loop)
                |> Some

        loop this

    static member filterFiles (predicate: string -> bool) =
        fun (tree: FileSystemTree) -> tree.FilterFiles predicate

    member this.FilterFolders (predicate: string -> bool) =
        let rec loop (parent: FileSystemTree) =
            match parent with
            | File n -> Some (File n)
            | Folder (n, children) ->
                if predicate n then 
                    Folder (n, children |> Array.choose loop)
                    |> Some
                else
                    None
        loop this

    static member filterFolders (predicate: string -> bool) =
        fun (tree: FileSystemTree) -> tree.FilterFolders predicate

    member this.Filter (predicate: string -> bool) = 
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

    static member filter (predicate: string -> bool) =
        fun (tree: FileSystemTree) -> tree.Filter predicate