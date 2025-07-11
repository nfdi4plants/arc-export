module LFSPointerTests

open GitLFS
open Xunit

let missingLFSOutput = """GIT: open assays/MassHunter_targets/dataset/./assays/MassHunter_targets/dataset/QuantReports/22-0005_exp001.batch_a.bin/190614_QuantReport_ISTD_DB.xlsx: The system cannot find the path specified.
Error parsing Git LFS object: Invalid Git LFS object string format.
No Git LFS object found for assays/MassHunter_targets/dataset/./assays/MassHunter_targets/dataset/QuantReports/22-0005_exp001.batch_a.bin/190614_QuantReport_ISTD_DB.xlsx"""

let correctLFSOutput = """GIT: Git LFS pointer for assays/RNASeq/dataset/DB_097_CAMMD_CAGATC_L001_R1_001.fastq.gz
GIT: version https://git-lfs.github.com/spec/v1
GIT: oid sha256:53accc2afcca23e16f97ba977e3414f902ffcf9685adda86db93825d7d07bbd7
GIT: size 135
GIT:"""

let correctLFSObject = 
    { Version = "https://git-lfs.github.com/spec/v1"
      Hash = SHA256 "53accc2afcca23e16f97ba977e3414f902ffcf9685adda86db93825d7d07bbd7"
      Size = 135L }

let fixtureLFSFilePath = "assays/measurement1/dataset/proteomics_result.csv"

let fixtureLFSObject = 
    { Version = "https://git-lfs.github.com/spec/v1"
      Hash = SHA256 "01bb750bd981905d7065d48943567869570597745c4478bde4f7dbee16be8e3d"
      Size = 95710L }


[<Fact>]
let ``Can correctly parse lfs pointer result`` () = 
    Assert.Equal(
        Some correctLFSObject,        
        GitLFS.GitLFSObject.tryFromString correctLFSOutput
    )

[<Fact>]
let ``Returns None for wrong lfs pointer result`` () = 
    Assert.Equal(
        None,
        GitLFS.GitLFSObject.tryFromString missingLFSOutput      
    )

[<Fact>]
let ``Can correctly retreive and parse lfs pointer`` () = 
    Assert.Equal(
        Some fixtureLFSObject,
        GitLFS.tryGetGitLFSObject "fixtures/ArcPrototype" fixtureLFSFilePath       
    )
    