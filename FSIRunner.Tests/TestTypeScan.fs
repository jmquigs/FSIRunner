﻿module TypeScanTests 

open Fuchu
open FSIRunner

[<Tests>]
let typescanscanThis = 
    testCase "TypeScan should return a list of types" <| 
        fun _ -> 
            // passing true means scan types from the calling assembly
            let scanThis = true
            let types = XTypeScan.scan(scanThis)
            Assert.Equal("list should have types", true, (List.length types > 0))
            // one of the types should be this module
            let thisModule = types |> List.tryFind (fun t -> t.FullName.Contains("TypeScanTests"))
            Assert.NotEqual("list should have test module", thisModule, None)
            // scanning again should result in zero types (typescan remembers which types it has already scanned)
            let types = XTypeScan.scan(scanThis)
            Assert.Equal("list should not have new types", true, (List.length types = 0))

[<Tests>]
let typescanInteractive = 
    testCase "TypeScan should return a list of types" <| 
        fun _ -> 
            // same should apply for non-scanThis (interactive) mode, in which case, it scans the executing assembly (FSIRunner)
            let scanThis = false
            let types = XTypeScan.scan(scanThis)
            Assert.Equal("list should have types", true, (List.length types > 0))
            let thisModule = types |> List.tryFind (fun t -> t.FullName.Contains("XTypeScan"))
            Assert.NotEqual("list should have test module", thisModule, None)
            let types = XTypeScan.scan(scanThis)
            Assert.Equal("list should not have new types", true, (List.length types = 0))

[<Tests>]
let typescanForget = 
    testCase "TypeScan should be able to forget defined types" <| 
        fun _ -> 
            let types = XTypeScan.scan(true)
            Assert.Equal("list should not have new types", true, (List.length types = 0))
            XTypeScan.forgetDefinedTypes()
            let types = XTypeScan.scan(true)
            Assert.Equal("list should have new types", true, (List.length types > 0))