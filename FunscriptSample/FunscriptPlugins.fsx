#load "Project.fsx"
#load "../FSIRunner/Types.fs"

open FSIRunner
open FSIRunner.Types
open System.IO

// We define both a compile plugin and a web server plugin in this file.  This is fine, a single file can define any number of plugins.
// The main reason to do this is for reload performance; loading Funscript and all of its refs is pretty intense; 
// if both plugins are in the same file, we only have to load Project.fsx and related assemblies once.  

// When there are multiple plugins in a file, the plugin callbacks will be invoked in the order the plugins are defined.
// For example, CompilePlugin's AfterReload will be called prior to WebServerPlugin's AfterReload.
let webRoot = "web"

type CompilePlugin() = 
    let afterReload : AfterReloadFn = 
        (fun (rs,opts) -> 
            printfn "funscript compiling"
            let code = FunScript.Compiler.Compiler.Compile(<@ Main.webMain() @>, noReturn=true)
            let outFile = Path.Combine(webRoot,"app.js")
            printfn "code length: %A chars; writing %s" code.Length outFile
            System.IO.File.WriteAllText(outFile, code)
        )
    
    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                AfterReload = afterReload }

type WebServerPlugin() = 
    let hostKey = "prev_funscript_server"
    
    let beforeReload : BeforeReloadFn = 
        (fun (rs,opts) -> 
            let ok, host = rs.TryGetValue hostKey
            if ok then 
                rs.Remove hostKey |> ignore
                let host = host :?> FunscriptSample.RuntimeImplementation.HttpServer
                printfn "Web: Stopping previous host"
                host.Stop()
        )
    
    let afterReload : AfterReloadFn = 
        (fun (rs,opts) -> 
            printfn "starting Funscript Server"
            let host = FunscriptSample.RuntimeImplementation.HttpServer.Start("http://localhost:1234/", webRoot)
            rs.Add(hostKey, host)
        )
    
    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                BeforeReload = beforeReload;
                AfterReload = afterReload  }
