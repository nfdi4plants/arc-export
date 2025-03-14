module ReferenceObjects

open TestUtils

open System.IO

let expected_prototype_commit_hash = "70a7c83e7858a974bf913de2e27d8e44191fc73f"

let expected_unregistedAssay_commit_hash = "787564f36849e4e673c0967fa550b936510efefc"


module ArcPrototype =
    
    let arc_summary = 
        try
            File.ReadAllText $"fixtures/arc_summary/ArcPrototype@{expected_prototype_commit_hash}.md"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ ->
            ""

    let isa_json = 
        try
            File.ReadAllText $"fixtures/isa_json/ArcPrototype@{expected_prototype_commit_hash}.json"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ -> 
            ""

    let arc_ro_crate_metadata = 
        try
            File.ReadAllText $"fixtures/arc_ro_crate_metadata/ArcPrototype@{expected_prototype_commit_hash}.json"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ ->
            ""

module UnregisteredAssay =
    
    let arc_summary = 
        try
            File.ReadAllText $"fixtures/arc_summary/ARC-Export-TestFixture@{expected_unregistedAssay_commit_hash}.md"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ ->
            ""

    let isa_json = 
        try
            File.ReadAllText $"fixtures/isa_json/ARC-Export-TestFixture@{expected_unregistedAssay_commit_hash}.json"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ -> 
            ""

    let arc_ro_crate_metadata = 
        try
            File.ReadAllText $"fixtures/arc_ro_crate_metadata/ARC-Export-TestFixture@{expected_unregistedAssay_commit_hash}.json"
            |> fun f -> f.ReplaceLineEndings("\n")
        with _ ->
            ""
