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

// Embedded FSI session.  Mostly ripped from http://fsharp.github.io/FSharp.Compiler.Service/interactive.html
type FSISession() = 
    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let inStream = new StringReader("")
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    // Build command line arguments & start FSI session
    let argv = [| "C:\\fsi.exe" |]
    let allArgs = Array.append argv [| "--noninteractive" |]
    let defaultConfig = FsiEvaluationSession.GetDefaultConfiguration()
    
    // Use a custom config to turn off as much non-error logging as we can in the embedded session.
    // This doesn't get everything, but it does eliminate most of the spam produced by TypeScan.scan(),
    // and thus speeds up typescan times by a couple hundred ms.
    let fsiConfig = 
        { new FsiEvaluationSessionHostConfig() with
              member __.FormatProvider = defaultConfig.FormatProvider
              member __.FloatingPointFormat = defaultConfig.FloatingPointFormat
              member __.AddedPrinters = defaultConfig.AddedPrinters
              member __.ShowDeclarationValues = false
              member __.ShowIEnumerable = false
              member __.ShowProperties = false
              member __.PrintSize = defaultConfig.PrintSize
              member __.PrintDepth = defaultConfig.PrintDepth
              member __.PrintWidth = defaultConfig.PrintWidth
              member __.PrintLength = defaultConfig.PrintLength
              member __.ReportUserCommandLineArgs args = defaultConfig.ReportUserCommandLineArgs args
              member __.EventLoopRun() = defaultConfig.EventLoopRun()
              member __.EventLoopInvoke(f) = defaultConfig.EventLoopInvoke(f)
              member __.EventLoopScheduleRestart() = defaultConfig.EventLoopScheduleRestart()
              member __.UseFsiAuxLib = defaultConfig.UseFsiAuxLib
              member __.StartServer(fsiServerName) = defaultConfig.StartServer(fsiServerName)
              member __.OptionalConsoleReadLine = defaultConfig.OptionalConsoleReadLine }
    
    let sess = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream, collectible = true)
    
    member x.GetErrorBuffer() = 
        errStream.Flush()
        sbErr.ToString()
    
    member x.ClearErrorBuffer() = 
        //printfn "Clearing error buffer (length %A)" sbErr.Length
        sbErr.Remove(0, sbErr.Length) |> ignore
    
    member x.GetOutputBuffer() = 
        outStream.Flush()
        sbOut.ToString()
    
    /// Evaluate expression & return the result
    member x.EvalExpression text = sess.EvalExpression(text)
    
    /// Evaluate interaction & ignore the result
    member x.EvalInteraction text = sess.EvalInteraction(text)
    
    /// Evaluate script & ignore the result
    member x.EvalScript scriptPath = sess.EvalScript(scriptPath)
    
    interface IDisposable with
        member x.Dispose() = 
            ((sess) :> IDisposable).Dispose()
            errStream.Close()
            outStream.Close()

type Plugin = 
    { FNs : PluginDefinition
      Instance : obj
      Name : string }

type RunnerConfig() = 
    static let mutable sDir = ""
    static member SourceDirectory 
        with get () = sDir
        and set (value) = sDir <- value

type Runner() = 
    let mutable shouldStop = false

    let logger = DefaultLogger
    
    let newSW() = 
        let sw = new Stopwatch()
        sw.Start()
        sw
    
    let fsiSession = ref (new FSISession())
    let pluginDict = new System.Collections.Generic.Dictionary<string, Plugin list>()
    let pluginConfigurations = new System.Collections.Generic.Dictionary<string, PluginConfiguration>()
    
    let reinitSession() = 
        let sw = newSW()
        (fsiSession.Value :> IDisposable).Dispose()
        fsiSession.Value <- new FSISession()
        // load the runner type utilities in the embedded session.  If we are running in FSI, then just except the script file
        // otherwise reference the assembly
#if INTERACTIVE
        logger.Info "Loading TypeScan.fsx in embedded session"
        fsiSession.Value.EvalScript(Path.Combine(RunnerConfig.SourceDirectory, "TypeScan.fsx"))
#else
        let asm = (Assembly.GetExecutingAssembly().Location)
        logger.Info (sprintf "Referencing %s in embedded session" asm)
        let refCmd = "#r \"" + asm + "\""
        fsiSession.Value.EvalInteraction(refCmd)
#endif
        
        pluginDict.Clear()
        logger.Info(sprintf "Session reinitialized: %dms" (sw.ElapsedMilliseconds))
    
    // Reusing the existing FSI session for a reload speeds up loading time, but memory from previous loads will not be reclaimed, 
    // which can present a problem since that memory includes file handles of referenced DLLs.  The open file leak will 
    // eventually lead to a "too many open files" exception (fairly rapidly on a mac, where the default limit is low).  
    // As a compromise, reinit the session every so often, allowing previous sessions to be reclaimed at the cost of a somewhat
    // longer load time for the new session.
    // Note: plugins can also request a clean session by setting a key in runner state during BeforeReload.  This is important
    // for plugins like the GenProjectPlugin, which could otherwise pick up stale types from a previous reload.
    // Note2: if this turns out to be too cumbersome to maintain, could just set this to zero and reinit every session, but the
    // cost of doing that is high enough right now that it pays to avoid it if we can.
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
    
    let initPlugin scriptName (types : System.Type list) : Plugin list = 
        let types = 
            types |> List.fold (fun acc t -> 
                // this assumes that plugin types will be nested types (i.e inside a module), and will implement IRunnerPlugin
                let members = t.GetNestedTypes()
                let pluginInitDefs = 
                    members |> Array.choose (fun m -> 
                        let runnerPluginIFace = 
                            m.GetInterfaces() 
                            |> Seq.tryFind (fun i -> i.FullName.Contains("IRunnerPlugin"))
                        match runnerPluginIFace with
                        | Some i -> 
                            logger.Info(sprintf "Found plugin %A in %s" m scriptName)
                            Some(m, i)
                        | None -> None)
                if (Seq.length pluginInitDefs) = 0 then
                    acc
                else
                    // squash all the discovered plugin pairs into the accumulator using a secondary fold
                    let acc = pluginInitDefs |> Seq.fold (fun acc (baseType,iface) -> (baseType, iface) :: acc) acc
                    acc) []

        let instantiatePlugin((baseType:System.Type), (iface:System.Type)) =
            //let baseType, iface = pTypes
            let cons = baseType.GetConstructor([||])
            let obj = cons.Invoke([||])
            let flags = 
                BindingFlags.DeclaredOnly ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic 
                ||| BindingFlags.Instance ||| BindingFlags.InvokeMethod
            let args = [| runnerState :> obj |]
            let res = iface.InvokeMember("Create", flags, null, obj, args)
            
            // the return value is a record of type Types.PluginDefinition.  But, since the plugin is loaded in a different assembly, its _not_ the
            // same as the Types.PluginDefinition we have available here, so a cast will fail.  Therefore we have to use reflection to read the field
            // values that we are interested in.
            let getPropVal name = 
                let brP = res.GetType().GetProperty(name)
                (FSharpValue.GetRecordField(res, brP))
            
            let fns = { 
                    PluginDefinition.Init = getPropVal "Init" :?> InitFn
                    PluginDefinition.BeforeReload = getPropVal "BeforeReload" :?> BeforeReloadFn
                    PluginDefinition.AfterReload = getPropVal "AfterReload" :?> AfterReloadFn } 

            { Plugin.FNs = fns
              Instance = obj
              Name = scriptName }

        match types with
        | [] -> failwithf "Illegal plugin: %A, contains no implementation of IRunnerPlugin: %A" scriptName types
        | x -> x |> List.map instantiatePlugin
    
    let stateTypesKey script = "__" + script + "Types"

    let applyToPlugins f pluginScripts =
        pluginScripts |> Seq.iter (fun pScript -> 
            // all plugins defined in a script file currently share the same configuration             
            let _, configuration = pluginConfigurations.TryGetValue (pScript)
            let ok, plugins = pluginDict.TryGetValue pScript
            if ok then plugins |> List.iter (fun plugin -> (f plugin configuration))) 

    let reloadPluginScript script = 
        let reloadSW = newSW()
        fsiSession.Value.EvalScript script
        let evalElapsed = reloadSW.ElapsedMilliseconds
        reloadSW.Restart()
        let newTypes = rescanTypes()
        let typeScanElapsed = reloadSW.ElapsedMilliseconds
        reloadSW.Restart()
        pluginDict.Remove script |> ignore
        let plugins = initPlugin script newTypes
        pluginDict.Add(script, plugins)
        let typesKey = stateTypesKey script
        runnerState.Remove(typesKey) |> ignore
        runnerState.Add(typesKey, newTypes) |> ignore

        // call Init on plugins, just once 
        let initialized, _ = runnerState.TryGetValue (script + StateKeys.PluginInitialized)
        if not initialized then
            logger.Info (sprintf "Initializing plugins from: %s" script)
            // if no plugin configuration found, just use an empty config
            let hasConfig, configuration = pluginConfigurations.TryGetValue (script)
            let configuration = match hasConfig with
                                | true -> configuration
                                | false -> 
                                    let conf = {
                                        ScriptPath = ""
                                        WatchDir = ""
                                        WatchExtensions = [""]
                                        Options = Map.ofSeq []
                                    }
                                    pluginConfigurations.Add(script,conf) |> ignore
                                    conf
                                
            plugins |> List.iter (fun plugin -> 
                plugin.FNs.Init(runnerState,configuration.Options)
                runnerState.Remove(StateKeys.RequireCleanSession) |> ignore) // this session is "guaranteed" to be clean)
            
            runnerState.Add(script + StateKeys.PluginInitialized, true)

        let pluginElapsed = reloadSW.ElapsedMilliseconds
        reloadSW.Restart()
        (evalElapsed, typeScanElapsed, pluginElapsed)

    let reload pluginScripts = 
        let sw = newSW()

        // call beforeReload
        pluginScripts |> applyToPlugins (fun plugin configuration -> plugin.FNs.BeforeReload(runnerState,configuration.Options))

        // reinit the session if we hit the max count or one of the plugins requested it
        let cleanRestart, _ = runnerState.TryGetValue StateKeys.RequireCleanSession

        reloadCount <- reloadCount + 1
        if (cleanRestart || reloadCount > MaxReloadsPerSession) then 
            logger.Info("Re-initializing session")
            runnerState.Remove(StateKeys.RequireCleanSession) |> ignore
            reinitSession()
            reloadCount <- 0
        logger.Info "Reloading plugins"
        try 
            let totalEval, totalTS, totalPlugin = 
                pluginScripts 
                |> List.fold (fun acc script -> 
                       let totalEval, totalTS, totalPlugin = acc
                       let evalElapsed, typeScanElapsed, pluginElapsed = reloadPluginScript script
                       (totalEval + evalElapsed, totalTS + typeScanElapsed, totalPlugin + pluginElapsed)) (0L, 0L, 0L)
            pluginScripts |> applyToPlugins (fun plugin configuration -> 
                        runnerState.Add(StateKeys.NewTypes, runnerState.[(stateTypesKey plugin.Name)])
                        logger.Info(sprintf "Calling AfterReload for %s" plugin.Name)
                        try 
                            plugin.FNs.AfterReload(runnerState,configuration.Options)
                        finally
                            runnerState.Remove(StateKeys.NewTypes) |> ignore)

            logger.Info
                (sprintf "Plugin reload done; eval: %dms, typescan: %dms, plugin: %dms" totalEval totalTS totalPlugin)
            logger.Info
                (sprintf "Reload complete at %A; total reload time: %dms" System.DateTime.Now sw.ElapsedMilliseconds)
        with e -> 
            if e.InnerException = null then logger.Error(sprintf "Error loading program: %A" e)
            else 
                logger.Error(sprintf "Error loading program: %A%s" e.InnerException (fsiSession.Value.GetErrorBuffer()))
                // clear the output buffer since we printed the error
                fsiSession.Value.ClearErrorBuffer()
    
    // Return runner state.  This is here for unit tests only and may be removed in the future.
    member x.State = runnerState
    member x.Stop() = shouldStop <- true
    
    // Start the runner with the specified plugins, and watch for changes in the specified directories.  
    // Use ctrl-c to quit.
    // This is the simplified interface, use Watch(PluginConfiguration list) for more control over the plugin specification
    // Note, subdirectories are not searched due to a current limitation with the FileSystemWatcher on mono.  
    // If you wish to watch a subdirectory, specify it explicitly.
    member x.Watch(pluginScripts : string list, dirs : string list) =
        // TODO: reuse the Watch(PluginConfiguration list) implemention
        reinitSession()
        reload pluginScripts
        use watcher = new Watcher((fun () -> 
            reload pluginScripts))
        watcher.Watch(dirs, Watcher.FsExtensions)
        while not shouldStop do
            Threading.Thread.Sleep(100)
    
    // Start the runner with the specified plugins.
    member x.Watch(plugins : PluginConfiguration list) = 
        reinitSession()
        // remap WatchDirs into absolute paths
        let plugins = plugins |> List.map (fun p -> { p with WatchDir = Path.GetFullPath(p.WatchDir) })
        // store all the configuratios for later use
        plugins |> List.iter (fun p -> pluginConfigurations.Add(p.ScriptPath, p))

        // TODO: handle different extensions in watchdirs

        // get all the watch dirs with extensions
        let dirsAndExts = 
            plugins
            |> List.map (fun p -> p.WatchDir, p.WatchExtensions)
            |> Set.ofSeq  // de-dup
            |> List.ofSeq

        let pluginScripts = plugins |> List.map (fun p -> p.ScriptPath) 

//        printfn "plugins: %A" plugins
//        printfn "watchdirs: %A" dirsAndExts
//        printfn "scripts: %A" pluginScripts

        let watcherReload() = reload pluginScripts
        // do initial load
        do watcherReload()
        use watcher = new Watcher(watcherReload)
        do dirsAndExts |> Seq.iter (fun (dir, exts) -> watcher.Watch([ dir ], exts))
        while not shouldStop do
            Threading.Thread.Sleep(100)
