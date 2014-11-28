#load "Project.fsx"

open Nancy.Hosting.Self

#load "../FSIRunner/Types.fs"

open FSIRunner.Types

// Define the web plugin.  This plugin is responsible for creating the web server and reinitializing it when files change.  
// We need to keep track of the nancy host so that we can cleanly shut it down on a reload.  Plugins cannot contain
// state, so the host is stored in the RunnerState dictionary.  Note also that this dictionary is global to all plugins, so be
// mindful of the keys that you use.  See FSIRunner.Types for more information on why a dictionary is used for the state.
type RunnerPlugin() = 
    let hostKey = "prev_nancy_host"
    
    let beforeReload : BeforeReloadFn = 
        (fun (rs,opts) -> 
        let ok, host = rs.TryGetValue hostKey
        if ok then 
            rs.Remove hostKey |> ignore
            let host = host :?> NancyHost
            printfn "Web: Stopping previous host"
            Main.stopNancy host)
    
    let afterReload : AfterReloadFn = 
        (fun (rs,opts) -> 
        let host = Main.startNancy()
        rs.Add(hostKey, host))
    
    interface IRunnerPlugin with
        member x.Create(rs : RunnerState) = 
            { BasePluginDefinition with 
                BeforeReload = beforeReload;
                AfterReload = afterReload  }
