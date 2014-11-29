open System
open System.IO
open System.Xml

#load "Types.fs"
#load "GenProject.fs"

open FSIRunner
open FSIRunner.Types
open System.IO

type RunnerPlugin() = 
    let regenProject = (fun (rs:RunnerState,opts:OptionDict) ->
        let projFile = opts.TryFind "ProjectFile"
        let outFile = "Project.fsx"
        match projFile with
        | None -> printfn "No project file found in options.  Skipping %s generation" outFile
        | Some path -> 
            let path = unbox path
            let lastWriteKey = "GenProjectLastWrite"
            let srcProjectFile = Path.Combine(System.Environment.CurrentDirectory, path)
            let mtime = File.GetLastWriteTime(srcProjectFile)
            let lastWrite = defaultArgRS rs lastWriteKey DateTime.MinValue
            if mtime > lastWrite then
                let excludedFilePaths = unbox (defaultArg (opts.TryFind "ExcludedFilePaths") (box []))
                GenProject.generate srcProjectFile outFile excludedFilePaths
                rs.[lastWriteKey] <- mtime
                rs.[StateKeys.RequireCleanSession] <- true
    )

    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                Init = regenProject;
                BeforeReload = regenProject }
