// This file contains various utility functions used for one-off testing with FSI
open System.IO
open System

let createTestFiles() = 
    // create a bunch of new files
    Environment.CurrentDirectory <- "/Users/john/Dev/FSharp/FSIRunner/FSIRunner"
    printfn "Creating test files"
    let modPath = Environment.CurrentDirectory
    
    let modFiles = 
        [ 10..99 ] |> Seq.map (fun i -> 
                          let modName = "SomeFile" + i.ToString()
                          let fname = modName + ".fs"
                          let filePath = Path.Combine(modPath, fname)
                          File.WriteAllText(filePath, "module " + modName)
                          fname)
    modFiles |> Seq.iter (fun f -> printfn "#load \"%s\"" f)
