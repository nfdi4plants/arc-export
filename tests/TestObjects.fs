module TestObjects

open TestUtils

open System.IO

let prototypeCommitHashOutput = TestUtils.getCommitHash "../../../fixtures/ArcPrototype"

let prototypeCommitHash = prototypeCommitHashOutput.Result.Output.TrimStart().TrimEnd()

let unregisteredAssayCommitHashOutput = TestUtils.getCommitHash "../../../fixtures/ARC-Export-TestFixture"

let unregisteredAssayCommitHash = unregisteredAssayCommitHashOutput.Result.Output.TrimStart().TrimEnd()