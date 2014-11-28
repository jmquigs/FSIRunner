#load "../../FSIRunner/Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun (rs,opts) -> 
        rs.Remove("Plugin1BeforeReload") |> ignore 
        rs.Add("Plugin1BeforeReload", "true")
    )

    let afterReload:AfterReloadFn = (fun (rs,opts) ->
        rs.Remove("Plugin1AfterReload") |> ignore
        rs.Add("Plugin1AfterReload", "true")
    )

    interface IRunnerPlugin with
        member x.Create(rs:RunnerState) = 
            { BasePluginDefinition with 
                BeforeReload = beforeReload
                AfterReload = afterReload 
            }
