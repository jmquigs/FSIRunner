namespace FSIRunner

open Microsoft.FSharp.Compiler.Interactive.Shell

open System
open System.IO
open System.Text
open System.Diagnostics
open System.Reflection
open Microsoft.FSharp.Reflection

open FSIRunner
open FSIRunner.Types

type FSISession() = 
    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)

    // Build command line arguments & start FSI session
    let argv = [| "C:\\fsi.exe" |]
    let allArgs = Array.append argv [|"--noninteractive"|]

    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let sess = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream, collectible=true) 

    member x.GetErrorBuffer() =
        printfn "eb"
        errStream.Flush()
        sbErr.ToString()

    member x.ClearErrorBuffer() =
        //printfn "Clearing error buffer (length %A)" sbErr.Length
        sbErr.Remove(0, sbErr.Length) |> ignore

    /// Evaluate expression & return the result
    member x.EvalExpression text =
        sess.EvalExpression(text)

    /// Evaluate interaction & ignore the result
    member x.EvalInteraction text = 
        sess.EvalInteraction(text)

      /// Evaluate script & ignore the result
    member x.EvalScript scriptPath = 
        sess.EvalScript(scriptPath)

    interface IDisposable with 
        member x.Dispose() = 
            ((sess) :> IDisposable).Dispose()
            errStream.Close()
            outStream.Close()

type Plugin = { FNs:PluginDefinition; Instance: obj; Name: string }

type RunnerConfig() =
    static let mutable sDir = ""
    static member SourceDirectory
        with get() = sDir
        and set(value) = sDir <- value

type Runner() =
    let logger = DefaultLogger

    let newSW() = 
        let sw = new Stopwatch()
        sw.Start()
        sw

    let fsiSession = ref (new FSISession())

    let pluginDict = new System.Collections.Generic.Dictionary<string,Plugin>()

    let reinitSession() =
        let sw = newSW()

        (fsiSession.Value :> IDisposable).Dispose()
        fsiSession.Value <- new FSISession()

        // load the runner type utilities in the embedded session.  If we are running in FSI mode, then just except the script file
        // otherwise reference the assembly
#if INTERACTIVE
        logger.Info "Loading TypeScan.fsx in embedded session"
        fsiSession.Value.EvalScript (Path.Combine(RunnerConfig.SourceDirectory, "TypeScan.fsx"))
#else
        let asm = (Assembly.GetExecutingAssembly().Location)
        logger.Info (sprintf "Referencing %s in embedded session" asm)
        fsiSession.Value.EvalInteraction("#r " + asm)
#endif
        pluginDict.Clear()
        logger.Info (sprintf "Session reinitialized: %dms" (sw.ElapsedMilliseconds))

    // Reusing the existing FSI session for a reload speeds up loading time, but memory from previous loads will not be reclaimed, 
    // which can present a problem since that memory includes file handles of referenced DLLs.  The open file leak will 
    // eventually lead to a "too many open files" exception (fairly rapidly on a mac, where the default limit is low).  
    // As a compromise, reinit the session every so often, allowing previous sessions to be reclaimed at the cost of a somewhat
    // longer load time for the new session.
    let MaxReloadsPerSession = 10
    let mutable reloadCount = 0

    let runnerState = new Types.RunnerState() 

    let rescanTypes() = 
#if INTERACTIVE
        let res = fsiSession.Value.EvalExpression "FSIRunner.XTypeScan.scan(false)"
#else
        let res = fsiSession.Value.EvalExpression "FSIRunner.XTypeScan.scan(true)"
#endif
        let types = res.Value.ReflectionValue :?> System.Type list
        types

    let initPlugin scriptName (types:System.Type list) : Plugin =
            let types = 
                types 
                    |> List.fold (fun acc t -> 
                        let members = t.GetNestedTypes() 
                        let initFn = members |> Seq.tryPick (fun m -> 
                            let runnerPluginIFace = m.GetInterfaces() |> Seq.tryFind (fun i -> i.FullName.Contains("IRunnerPlugin") ) 
                            match runnerPluginIFace with 
                            | Some i -> 
                                logger.Info (sprintf "Found plugin %A in %s" m scriptName)
                                Some (m, i)
                            | None -> 
                                None
                        )
                        match initFn with
                        | None -> acc
                        | Some (baseType,iface) -> 
                            (baseType,iface)::acc
                    ) []

            match types with 
            | [] -> failwithf "Illegal plugin: %A, contains no implementation of IRunnerPlugin: %A" scriptName types
            | [x] ->
                let baseType, iface = x
                let cons = baseType.GetConstructor([||])
                let obj = cons.Invoke([||])

                let flags = BindingFlags.DeclaredOnly ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.InvokeMethod
                let args = [| runnerState :> obj |] 
                let res = iface.InvokeMember("Init", flags, null, obj, args) 

                // the return value is a record of type Types.PluginDefinition.  But, since the plugin is loaded in a different assembly, its _not_ the
                // same as the Types.PluginDefinition we have available here, so a cast will fail.  Therefore we have to use reflection to read the field
                // values that we are interested in.
                let getPropVal name =
                    let brP = res.GetType().GetProperty(name)
                    (FSharpValue.GetRecordField(res, brP))

                let fns = { 
                    PluginDefinition.BeforeReload = getPropVal "BeforeReload" :?> BeforeReloadFn; 
                    PluginDefinition.AfterReload = getPropVal "AfterReload" :?> AfterReloadFn; }

                { Plugin.FNs = fns; Instance = obj; Name=scriptName }
            | h::t -> failwithf "Illegal plugin: %A, contains more than one implementation of IRunnerPlugin: %A" scriptName types

    let reloadPluginScript script = 
        let reloadSW = newSW()

        fsiSession.Value.EvalScript script
        let evalElapsed = reloadSW.ElapsedMilliseconds 
        reloadSW.Restart()

        let newTypes = rescanTypes()
        let typeScanElapsed = reloadSW.ElapsedMilliseconds 
        reloadSW.Restart()
       
        pluginDict.Remove script |> ignore 
        let plugin = initPlugin script newTypes
        pluginDict.Add(script, plugin)

        let typesKey = script + "Types"
        runnerState.Remove(typesKey) |> ignore
        runnerState.Add(typesKey, newTypes) |> ignore

        let pluginElapsed = reloadSW.ElapsedMilliseconds 
        reloadSW.Restart()

        (evalElapsed,typeScanElapsed,pluginElapsed)

    let reload pluginScripts = 
        let sw = newSW()

        pluginScripts |> Seq.iter (fun pScript -> 
            let ok, plugin = pluginDict.TryGetValue pScript
            if ok then plugin.FNs.BeforeReload runnerState
        )

        reloadCount <- reloadCount + 1
        if (reloadCount > MaxReloadsPerSession) then logger.Info("Re-initializing session"); reinitSession(); reloadCount <- 0

        logger.Info "Reloading plugins"
        try 
            let totalEval,totalTS,totalPlugin = 
                pluginScripts |> List.fold (fun acc script -> 
                    let totalEval,totalTS,totalPlugin = acc

                    let evalElapsed,typeScanElapsed,pluginElapsed = reloadPluginScript script

                    (totalEval+evalElapsed, totalTS + typeScanElapsed, totalPlugin + pluginElapsed)
                ) (0L,0L,0L)

            pluginScripts |> Seq.iter (fun pScript -> 
                let ok, plugin = pluginDict.TryGetValue pScript
                if ok then
                    runnerState.Remove(StateKeys.NewTypes) |> ignore
                    runnerState.Add(StateKeys.NewTypes, runnerState.[(stateTypesKey plugin.Name)])
                    logger.Info (sprintf "Calling AfterReload for %s" plugin.Name)
                    plugin.FNs.AfterReload runnerState
            )

            logger.Info (sprintf "Plugin reload done; eval: %dms, typescan: %dms, plugin: %dms" totalEval totalTS totalPlugin)
            logger.Info (sprintf "Total reload time: %dms" sw.ElapsedMilliseconds)
        with 
            | e -> 
                if e.InnerException = null then
                    logger.Error (sprintf "Error loading program: %A" e)
                else 
                    logger.Error (sprintf "Error loading program: %A%s" e.InnerException (fsiSession.Value.GetErrorBuffer()))
                    // clear the output buffer since we printed the error
                    fsiSession.Value.ClearErrorBuffer()

    member x.Watch dir pluginScripts = 
        reinitSession()
        reload pluginScripts
        let watcher = new Watcher((fun () -> 
            //printfn "Change detected, reloading"
            reload pluginScripts
        ), Watcher.FsFile)
        watcher.Watch dir

        // sleep forever
        Threading.Thread.Sleep(Threading.Timeout.Infinite)
