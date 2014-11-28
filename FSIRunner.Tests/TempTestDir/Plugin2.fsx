#load "../../FSIRunner/Types.fs"
open FSIRunner.Types

type RunnerPlugin() =
    let beforeReload:BeforeReloadFn = (fun (rs,opts) -> 
        rs.Remove("Plugin2BeforeReload") |> ignore 
        rs.Add("Plugin2BeforeReload", "true")
    )

    let afterReload:AfterReloadFn = (fun (rs,opts) ->
        rs.Remove("Plugin2AfterReload") |> ignore
        rs.Add("Plugin2AfterReload", "true")
    )

    interface IRunnerPlugin with
        member x.Create(rs:RunnerState) = 
            { BasePluginDefinition with 
                BeforeReload = beforeReload
                AfterReload = afterReload 
            }
