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
    printfn "removing test files in %s" destRoot
    let directories = 
        created |> List.fold (fun acc f ->
            if File.Exists(f) then 
                //printfn "removing file: %s" f
                File.Delete(f)
                acc
            else 
                f::acc) []
    directories |> List.iter (fun d ->
        //printfn "removing directory: %s" d
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

let fixFSIRunnerPaths files =
    let fsiRunnerPath = Path.GetFullPath(Path.Combine(runnerScriptsRoot, "..", "..", "FSIRunner", "FSIRunner"))

    files |> List.iter( fun f -> 
        if (File.Exists(f)) then
            let text = File.ReadAllText(f)
            let text = text.Replace("../../FSIRunner", fsiRunnerPath)
            File.WriteAllText(f, text))

let withSetupTeardown f () = 
    let wd = Environment.CurrentDirectory 
    XTypeScan.forgetDefinedTypes()
    let created = setupTestDirectory()
    fixFSIRunnerPaths created
    Environment.CurrentDirectory <- destRoot
    let r = f()
    XTypeScan.forgetDefinedTypes()
    cleanupTestDirectory created
    Environment.CurrentDirectory <- wd
    r

// It can be helpful to enable this when debugging the test
//do 
//    if Directory.Exists(destRoot) then Directory.Delete(destRoot, true)

[<Tests>]
let runnerTests = 
    testCase "Runner should load plugins and reload on file add,delete,change" <| 
        withSetupTeardown (fun _ -> 
            let r = new FSIRunner.Runner()
            let task = async {
                let pluginList = [ "Plugin1.fsx"; "Plugin2.fsx"] 
                let watchDirs = ["Dir1";"Dir2"]
                r.Watch pluginList watchDirs
            } 
            Async.StartAsTask task |> ignore
            System.Threading.Thread.Sleep(5000)
            let state = r.State
            Assert.Equal("plugin1 loaded", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded", true, state.ContainsKey("Plugin2AfterReload"))

            state.Remove("Plugin1AfterReload") |> ignore
            state.Remove("Plugin2AfterReload") |> ignore

            // remove a file, runner should reload
            printfn "checking remove"
            let removedFile = Path.Combine(destRoot, "Dir1", "SomeFile1.fs")
            let text = File.ReadAllText(removedFile)
            File.Delete(removedFile)
            System.Threading.Thread.Sleep(1500)
            Assert.Equal("plugin1 loaded", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded", true, state.ContainsKey("Plugin2AfterReload"))

            // add a file, runner should reload
            state.Remove("Plugin1AfterReload") |> ignore
            state.Remove("Plugin2AfterReload") |> ignore
            printfn "checking add"
            File.WriteAllText(removedFile, text)
            System.Threading.Thread.Sleep(1500)
            Assert.Equal("plugin1 loaded", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded", true, state.ContainsKey("Plugin2AfterReload"))

            // change a file, runner should reload
            let changedFilePath = Path.Combine(destRoot, "Dir1", "SomeFile2.fs")
            let text = File.ReadAllText(changedFilePath)
            let text = text + "\n//MORE STUFF"
            File.WriteAllText(changedFilePath, text)
            File.SetLastWriteTimeUtc(changedFilePath, DateTime.UtcNow)
            printfn "checking update"
            // runner should reload
            System.Threading.Thread.Sleep(1500)
            Assert.Equal("plugin1 loaded", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded", true, state.ContainsKey("Plugin2AfterReload"))
        )
