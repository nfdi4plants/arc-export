module GitSubmoduleTests

open System
open System.IO
open Xunit

[<Fact>]
let ``ArcPrototype commit hash is correct`` () = 
    Assert.Equal(
        ReferenceObjects.expected_prototype_commit_hash, 
        TestObjects.prototypeCommitHash
    )

[<Fact>]
let ``Fixtures exist for ArcPrototype commit hash`` () = 
    Assert.True (File.Exists $"fixtures/arc_ro_crate_metadata/ArcPrototype@{TestObjects.prototypeCommitHash}.json", $"arc_ro_crate_metadata file fixture does not exist for {TestObjects.prototypeCommitHash}")
    Assert.True (File.Exists $"fixtures/arc_summary/ArcPrototype@{TestObjects.prototypeCommitHash}.md", $"arc_summary file fixture does not exist for {TestObjects.prototypeCommitHash}")
    Assert.True (File.Exists $"fixtures/isa_json/ArcPrototype@{TestObjects.prototypeCommitHash}.json", $"isa_json file fixture does not exist for {TestObjects.prototypeCommitHash}")