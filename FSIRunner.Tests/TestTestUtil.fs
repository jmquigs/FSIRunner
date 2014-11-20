module TestUtilTests

open Fuchu
open FSIRunner

[<Tests>]
let typeScanTests = 
    let withSetupTeardown f () = 
        XTypeScan.forgetDefinedTypes()
        let r = f()
        XTypeScan.forgetDefinedTypes()
        r
    
    let myLabel = "TestUtil.getTests should return a list of tests"
    testCase myLabel <| withSetupTeardown (fun _ -> 
                            let types = XTypeScan.scan (true)
                            Assert.Equal("list should have new types", true, (List.length types > 0))
                            let tests = TestUtil.getTests types typeof<Fuchu.TestsAttribute>
                            Assert.Equal("tests list should have new types", true, (Seq.length tests > 0))
                            // this test should be one of them
                            let thisTest = 
                                tests |> Seq.tryFind (fun t -> 
                                             let test = t :?> Fuchu.Test
                                             match test with
                                             | TestLabel(label, test) -> label.Equals myLabel
                                             | _ -> false)
                            Assert.NotEqual("test list should have this test", None, thisTest))
