//#r "../packages/FSharp.Compiler.Service.0.0.73/lib/net45/FSharp.Compiler.Service.dll"
//#r "bin/Debug/FSIRunner.dll"
//
////printfn "%A" (XFSIRunner.XTypeScan.scan())
//
//open System
//
//let r = new XFSIRunner.Runner()
//r.Watch "." [ "TestPlugin.fsx" ]

#r "../packages/FSharp.Compiler.Service.0.0.73/lib/net45/FSharp.Compiler.Service.dll"

#load "RunnerTypes.fs"
#load "Watcher.fs"
#load "FSIRunner.fs"
#load "TypeScan.fs"

XFSIRunner.RunnerConfig.SourceDirectory <- __SOURCE_DIRECTORY__


#if INTERACTIVEX

#load "Watcher.fs"
#load "RunnerTypes.fs"
#load "FSIRunner.fs"

let logger = Log.getLogger("RunnerFSX")

open System
open System.IO

// skip all the .Net mojo args
//printfn "args: %A" (Environment.GetCommandLineArgs())
let args = Environment.GetCommandLineArgs() |> Seq.skipWhile (fun s -> s.IndexOf(__SOURCE_FILE__) = -1) |> Seq.toArray

if args.Length < 2 then 
    printfn "Usage: %s <command>" __SOURCE_FILE__
    Environment.Exit(1)

let op = args.[args.Length-1].ToLowerInvariant()

let pluginScripts = [ "Program.fsx"; "Tests.fsx" ]

match op with 
| "watch" -> 
    logger.Info "Entering watch mode"
    FSIRunner.watch (Path.GetFullPath(Environment.CurrentDirectory)) pluginScripts
| "reloadtest" ->
    FSIRunner.reload pluginScripts
| _ -> 
    printfn "Unknown command: %s" op
    Environment.Exit(1)

#endif

    