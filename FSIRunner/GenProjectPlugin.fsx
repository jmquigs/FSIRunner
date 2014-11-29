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
            let excludedFilePaths = unbox (defaultArg (opts.TryFind "ExcludedFilePaths") (box []))
            GenProject.generate (Path.Combine(System.Environment.CurrentDirectory, path)) outFile excludedFilePaths
    )

    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                Init = regenProject;
                BeforeReload = regenProject }
