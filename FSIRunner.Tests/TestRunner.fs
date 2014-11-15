module RunnerTests 

open Fuchu
open FSIRunner

open System
open System.IO

let tempRoot = Path.GetTempPath()
let destRoot = Path.Combine(tempRoot, "FSIRunner", "TempTestDir")
let srcRoot = Path.Combine("..", "..", "TempTestDir")
let runnerScriptsRoot = Path.Combine("..", "..")

let cleanupTestDirectory created =
    let directories = 
        created |> List.fold (fun acc f ->
            if File.Exists(f) then 
                printfn "removing file: %s" f
                File.Delete(f)
                acc
            else 
                f::acc) []
    directories |> List.iter (fun d ->
        printfn "removing directory: %s" d
        Directory.Delete(d))

let setupTestDirectory() =
    if tempRoot.Contains("..") then failwithf "illegal temp directory: %s" tempRoot

    printfn "creating skeleton directory in %s" destRoot

    if not (Directory.Exists(destRoot)) then Directory.CreateDirectory(destRoot) |> ignore

    let rec copyFilesToDest src dest = 
        let srcdirs = Directory.GetDirectories(src) |> List.ofArray
        let destdirs = srcdirs |> List.map (fun sdir -> 
            let sdir = new DirectoryInfo(sdir)
            let sdir = sdir.Name
            let destPath = Path.Combine(dest,sdir)
            Directory.CreateDirectory(destPath) |> ignore
            destPath
        )

        let srcfiles = Directory.GetFiles(src) |> List.ofArray
        let destfiles = srcfiles |> List.map (fun sfile ->
            let sfile = new FileInfo(sfile)
            let sbase = sfile.Name
            let destFile = Path.Combine(dest,sbase)
            File.Copy(sfile.FullName, destFile)
            destFile
        )

        let subcopied = List.fold2 (fun acc spath dpath ->
                (copyFilesToDest spath dpath @ acc)  ) [] srcdirs destdirs 

        destdirs @ destfiles @ subcopied

    let created = copyFilesToDest srcRoot destRoot
    printfn "created skeleton test directory in %s" destRoot
    created

[<Tests>]
let runnerTests = 
    testCase "Runner should do its thing" <| 
        fun _ -> 
            let created = setupTestDirectory()
            printfn "%A" created
            cleanupTestDirectory created
