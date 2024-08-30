module TestObjects

open TestUtils

open System.IO

let prototypeCommitHashOutput = TestUtils.getCommitHash "../../../fixtures/ArcPrototype"

let prototypeCommitHash = prototypeCommitHashOutput.Result.Output.TrimStart().TrimEnd()