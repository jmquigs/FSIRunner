namespace FSIRunner

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FSIRunner
open FSIRunner.Types

type ActiveWatcher = Task<unit> * CancellationTokenSource

type WatchEvent = 
    | BeginWatch of WatchInfo
    | FileEvent of string * FileSystemEventArgs * CancellationTokenSource
    | Restart
    | Stop
    | Terminate

#nowarn "40"

type Watcher(reloadFn, ?delay : int) = 
    let logger = DefaultLogger
    
    let createWatcher (dir : WatchInfo) (filterFn : string -> bool) (eventProcessor : MailboxProcessor<WatchEvent>) = 
        let tokenSource = new CancellationTokenSource()
        let token = tokenSource.Token
        
        let block = 
            async { 
                use watcher = 
                    new FileSystemWatcher(dir.WatchPath, EnableRaisingEvents = true, 
                                          IncludeSubdirectories = false )
                
                let watchPost eType = 
                    (fun (e : FileSystemEventArgs) -> 
                    if filterFn e.FullPath then 
                        logger.Info(sprintf "File %s: %s" eType e.FullPath)
                        eventProcessor.Post(FileEvent(dir.WatchPath, e, tokenSource)))
                watcher.Created.Add(watchPost "created")
                watcher.Changed.Add(watchPost "changed")
                watcher.Renamed.Add(watchPost "renamed")
                watcher.Deleted.Add(watchPost "deleted")
                while not token.IsCancellationRequested do
                    System.Threading.Thread.Sleep(50)
            }
        
        let task = Async.StartAsTask(block, Tasks.TaskCreationOptions.None, token)
        task, tokenSource
    
    let iDelay = 
        match delay with
        | None -> 100
        | Some i -> i
    
    let rec watchMB = 
        MailboxProcessor<WatchEvent>.Start(fun inbox -> 
            let watcherDict = new System.Collections.Generic.Dictionary<string, ActiveWatcher>()
            let restartNeeded = ref false
            let terminate = ref false
            
            let clearAll() = 
                watcherDict.Values |> Seq.iter (fun w -> 
                                          let _, cancelToken = w
                                          cancelToken.Cancel())
                watcherDict.Clear()
            
            let rec loop() = 
                async { 
                    let! message = inbox.TryReceive(iDelay)
                    match message with
                    | Some(BeginWatch dir) -> 
                        // use a composite key of path + all the extensions 
                        let watchKey = dir.WatchPath + (List.fold (fun acc ext -> acc + ext) "" dir.Extensions  ).ToLowerInvariant()
                        let ok, watcher = watcherDict.TryGetValue watchKey
                        if ok then 
                            logger.Info(sprintf "Destroying previous watcher for %A" dir)
                            let token = snd watcher
                            token.Cancel()
                            watcherDict.Remove watchKey |> ignore
                        logger.Info(sprintf "Creating watcher for %A" dir)
                        let filterFn = Watcher.ExtensionMatcher dir.Extensions
                        let w, token = createWatcher dir filterFn watchMB
                        watcherDict.Add(watchKey, (w, token))
                    | Some(FileEvent(watchDir, event, cancelSource)) -> 
                        match event.ChangeType with
                        | WatcherChangeTypes.Created -> restartNeeded.Value <- true
                        | WatcherChangeTypes.Changed | WatcherChangeTypes.Deleted | WatcherChangeTypes.Renamed -> 
                            restartNeeded.Value <- true
                        | _ -> failwithf "Unexpected file change type: %A" event.ChangeType
                    | Some(Restart) -> reloadFn()
                    | Some(Stop) -> clearAll()
                    | Some(Terminate) -> 
                        clearAll()
                        terminate.Value <- true
                    | None -> 
                        if restartNeeded.Value then 
                            watchMB.Post Restart
                            restartNeeded.Value <- false
                    if not terminate.Value then do! loop()
                }
            
            loop())
    
    static member ExtensionMatcher (extensions: string list) x =
        let ext = Path.GetExtension(x).ToLowerInvariant()
        match extensions |> List.tryFind (fun lext -> lext.ToLowerInvariant() = ext) with
        | None -> false
        | Some x -> true

    static member FsExtensions with get() = [ ".fs" ; ".fsx" ]

    static member FsFile (x:string) = 
        Watcher.ExtensionMatcher Watcher.FsExtensions x
    
    member x.Watch(dirs : string list, extensions: string list) = 
        dirs |> List.iter (fun dir -> 
                    watchMB.Post(BeginWatch { WatchPath = dir
                                              Extensions = extensions }))
    
    member x.Watch dirs = dirs |> List.iter (fun dir -> watchMB.Post(BeginWatch dir))
    
    /// Stops this watcher, releasing all directory watchers.  It remains active, however, so you may still call Watch() to add new directories.
    /// Note that the watchers may produce some final events for a brief period after this is called.
    member x.Stop() = watchMB.Post(Stop)
    
    interface IDisposable with
        member x.Dispose() = 
            x.Stop()
            watchMB.Post(Terminate)

#if X
// This fails to pick up changes in files in the "Tests" subdirectory on mono/osx.  If the directory itself is renamed, then the watcher
// will pick up changes after that.  Not sure if this is a bug in mono or a limitation of the osx file watcher.  

module private testInFSI =
    open System.IO

    let dir = "/Users/john/Dev/FSharp/FSIRunner/NancyWebSample"

    let watcher = 
        new FileSystemWatcher(dir, EnableRaisingEvents = true, 
                              IncludeSubdirectories = true)

    watcher.EnableRaisingEvents <- true
    watcher.IncludeSubdirectories <- true
    watcher.Filter <- "*"

    watcher.NotifyFilter <- (watcher.NotifyFilter ||| NotifyFilters.DirectoryName ||| NotifyFilters.LastWrite 
                             ||| NotifyFilters.LastAccess ||| NotifyFilters.Attributes ||| NotifyFilters.CreationTime 
                             ||| NotifyFilters.FileName ||| NotifyFilters.Security ||| NotifyFilters.Size)
    watcher.Created.Add((fun (e : FileSystemEventArgs) -> printfn "%s created" e.Name))
    watcher.Changed.Add((fun (e : FileSystemEventArgs) -> printfn "%s changed" e.Name))
    watcher.Renamed.Add((fun (e : RenamedEventArgs) -> printfn "%s renamed" e.Name))
    watcher.Deleted.Add((fun (e : FileSystemEventArgs) -> printfn "%s deleted" e.Name))
#endif
