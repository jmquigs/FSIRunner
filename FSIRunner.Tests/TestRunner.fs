module RunnerTests 

open Fuchu
open FSIRunner

open System
open System.IO

let tempRoot = Path.GetTempPath()
let destRoot = Path.Combine(tempRoot, "FSIRunner", "TempTestDir")
// the tests can be run from bin/Debug or from "."; select the srcRoot based on which one we are in
let hasTempTestDir = Directory.Exists("TempTestDir")
let srcRoot = if hasTempTestDir then "TempTestDir" else Path.Combine("..", "..", "TempTestDir")
let runnerScriptsRoot = if hasTempTestDir then "." else Path.Combine("..", "..")

let cleanupTestDirectory destRoot =
    if Directory.Exists destRoot then
        printfn "removing test files in %s" destRoot
        if not (destRoot.StartsWith(tempRoot)) then failwithf "Illegal directory for cleanup: %s" destRoot
        if not (destRoot.EndsWith("TempTestDir")) then failwithf "Illegal directory for cleanup: %s" destRoot

        let dryRun = false
        let iterFn = 
            if dryRun then 
                (fun f -> printfn "would delete %s" f) 
            else 
                (fun fileOrDir -> if File.Exists(fileOrDir) then File.Delete(fileOrDir) else Directory.Delete(fileOrDir))

        do 
            Directory.GetFiles(destRoot, "Plugin*.fsx", SearchOption.AllDirectories) |> Seq.iter iterFn
            Directory.GetFiles(destRoot, "SomeFile*.fs", SearchOption.AllDirectories) |> Seq.iter iterFn
            Directory.GetDirectories(destRoot, "*.*", SearchOption.AllDirectories) |> Seq.iter iterFn
            Directory.Delete destRoot

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
    created

let fixFSIRunnerPaths files =
    let fsiRunnerPath = Path.GetFullPath(Path.Combine(runnerScriptsRoot, "..", "..", "FSIRunner", "FSIRunner"))

    files |> List.iter( fun f -> 
        if (File.Exists(f)) then
            let text = File.ReadAllText(f)
            let text = text.Replace("../../FSIRunner", fsiRunnerPath)
            File.WriteAllText(f, text))

let withSetupTeardown f () = 
    cleanupTestDirectory destRoot
    let wd = Environment.CurrentDirectory 
    XTypeScan.forgetDefinedTypes()
    let created = setupTestDirectory()
    fixFSIRunnerPaths created
    Environment.CurrentDirectory <- destRoot
    let r = f()
    XTypeScan.forgetDefinedTypes()
    Environment.CurrentDirectory <- wd
    cleanupTestDirectory destRoot
    r

let initRunner() =
    let r = new FSIRunner.Runner()
    let task = async {
        let pluginList = [ "Plugin1.fsx"; "Plugin2.fsx"] 
        let watchDirs = ["Dir1";"Dir2"]
        r.Watch pluginList watchDirs
    } 
    Async.StartAsTask task |> ignore
    r

[<Tests>]
let runnerTests = 
    testCase "Runner should load plugins and reload on file add,delete,change" <| 
        withSetupTeardown (fun _ -> 
            let r = initRunner()

            System.Threading.Thread.Sleep(5000)
            let state = r.State
            Assert.Equal("plugin1 loaded1", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded1", true, state.ContainsKey("Plugin2AfterReload"))

            state.Remove("Plugin1AfterReload") |> ignore
            state.Remove("Plugin2AfterReload") |> ignore

            // remove a file, runner should reload
            printfn "checking remove"
            let removedFile = Path.Combine(destRoot, "Dir1", "SomeFile1.fs")
            let text = File.ReadAllText(removedFile)
            File.Delete(removedFile)
            System.Threading.Thread.Sleep(2000)
            Assert.Equal("plugin1 loaded2", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded2", true, state.ContainsKey("Plugin2AfterReload"))

            // add a file, runner should reload
            state.Remove("Plugin1AfterReload") |> ignore
            state.Remove("Plugin2AfterReload") |> ignore
            printfn "checking add"
            File.WriteAllText(removedFile, text)
            System.Threading.Thread.Sleep(2000)
            Assert.Equal("plugin1 loaded3", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded3", true, state.ContainsKey("Plugin2AfterReload"))

            // change a file, runner should reload
            let changedFilePath = Path.Combine(destRoot, "Dir1", "SomeFile2.fs")
            let text = File.ReadAllText(changedFilePath)
            let text = text + "\n//MORE STUFF"
            File.WriteAllText(changedFilePath, text)
            File.SetLastWriteTimeUtc(changedFilePath, DateTime.UtcNow)
            printfn "checking update"
            // runner should reload
            System.Threading.Thread.Sleep(2000)
            Assert.Equal("plugin1 loaded4", true, state.ContainsKey("Plugin1AfterReload"))
            Assert.Equal("plugin2 loaded4", true, state.ContainsKey("Plugin2AfterReload"))

            r.Stop()
            // Give runner time to shutdown the FSWatcher before cleaning up
            System.Threading.Thread.Sleep(1000)
        )
