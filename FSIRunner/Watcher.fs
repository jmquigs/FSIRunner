namespace FSIRunner

open System
open System.IO
open System.Threading
open System.Threading.Tasks

open FSIRunner
open FSIRunner.Types

type ActiveWatcher = Task<unit> * CancellationTokenSource
type WatchEvent = 
    BeginWatch of WatchInfo 
    | FileEvent of string * FileSystemEventArgs * CancellationTokenSource
    | Restart
    | Stop
    | Terminate

#nowarn "40"
type Watcher(reloadFn, filterFn, ?delay:int) =
    let logger = DefaultLogger

    let createWatcher (dir:WatchInfo) (filterFn:string -> bool) (eventProcessor:MailboxProcessor<WatchEvent>) =
        let tokenSource = new CancellationTokenSource()
        let token = tokenSource.Token

        let block = async {
            use watcher = new FileSystemWatcher(dir.WatchPath, EnableRaisingEvents = true, IncludeSubdirectories = dir.IncludeSubdirectories)

            let watchPost eType = 
                (fun (e:FileSystemEventArgs) ->
                    if filterFn e.FullPath then
                        logger.Info (sprintf "File %s: %s" eType e.FullPath)
                        eventProcessor.Post (FileEvent(dir.WatchPath,e,tokenSource)))

            watcher.Created.Add(watchPost "created")
            watcher.Changed.Add(watchPost "changed")
            watcher.Renamed.Add(watchPost "renamed")
            watcher.Deleted.Add(watchPost "deleted")

            while not token.IsCancellationRequested do
                System.Threading.Thread.Sleep(50)

        } 
        let task = Async.StartAsTask(block,  Tasks.TaskCreationOptions.None, token)
        task, tokenSource
   
    let iDelay = match delay with
                 | None -> 100
                 | Some i -> i

    let rec watchMB = MailboxProcessor<WatchEvent>.Start(fun inbox ->
            let watcherDict = new System.Collections.Generic.Dictionary<string, ActiveWatcher>()
            let restartNeeded = ref false
            let terminate = ref false
            let clearAll() =
                watcherDict.Values |> Seq.iter (fun w ->
                    let _,cancelToken = w
                    cancelToken.Cancel())
                watcherDict.Clear()
            let rec loop() = async { 
                let! message = inbox.TryReceive(iDelay)

                match message with
                | Some (BeginWatch dir) ->
                    let ok, watcher = watcherDict.TryGetValue dir.WatchPath
                    if ok then
                        logger.Info (sprintf "Destroying previous watcher for %A" dir)
                        let token = snd watcher
                        token.Cancel()
                        watcherDict.Remove dir.WatchPath |> ignore
                    
                    logger.Info (sprintf "Creating watcher for %A" dir.WatchPath)
                    let w,token = createWatcher dir filterFn watchMB
                    watcherDict.Add(dir.WatchPath, (w,token))
                | Some (FileEvent (watchDir, event, cancelSource)) ->
                    match event.ChangeType with
                    | WatcherChangeTypes.Created ->
                        restartNeeded.Value <- true
                    | WatcherChangeTypes.Changed | WatcherChangeTypes.Deleted | WatcherChangeTypes.Renamed ->
                        restartNeeded.Value <- true
                    | _ -> failwithf "Unexpected file change type: %A" event.ChangeType 
                | Some (Restart) ->
                    reloadFn()
                | Some (Stop) ->
                    clearAll()
                | Some (Terminate) ->
                    clearAll()
                    terminate.Value <- true
                | None ->
                    if restartNeeded.Value then 
                        watchMB.Post Restart
                        restartNeeded.Value <- false

                if not terminate.Value then
                    do! loop() 
            }
            
            loop()
        )

    static member FsFile x =
        match Path.GetExtension(x).ToLowerInvariant() with
        | ".fs" | ".fsx" -> true
        | _ -> false

    member x.Watch (dirs: string list) =
        dirs |> List.iter (fun dir -> watchMB.Post (BeginWatch { WatchPath = dir; IncludeSubdirectories = false }))
    member x.Watch dirs = 
        dirs |> List.iter (fun dir -> watchMB.Post (BeginWatch dir))

    /// Stops this watcher, releasing all directory watchers.  It remains active, however, so you may still call Watch() to add new directories.
    /// Note that the watchers may produce some final events for a brief period after this is called.
    member x.Stop() =
        watchMB.Post (Stop)

    interface IDisposable with 
        member x.Dispose() = 
            x.Stop()
            watchMB.Post (Terminate)

#if INTERACTIVE_DEBUG
// This fails to pick up changes in files in the "Tests" subdirectory on mono/osx.  If the directory itself is renamed, then the watcher
// will pick up changes after that.  Not sure if this is a bug in mono or a limitation of the osx file watcher.  
open System.IO

let watcher = new FileSystemWatcher("/Users/john/Dev/FSharp/FSIRunner/NancyWebSample", EnableRaisingEvents = true, IncludeSubdirectories = true)
watcher.EnableRaisingEvents <- true
watcher.IncludeSubdirectories <- true
watcher.NotifyFilter <- (watcher.NotifyFilter 
    ||| NotifyFilters.DirectoryName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.Attributes
    ||| NotifyFilters.CreationTime ||| NotifyFilters.FileName ||| NotifyFilters.Security ||| NotifyFilters.Size)
watcher.Created.Add((fun (e:FileSystemEventArgs) -> printfn "%s created" e.Name))
watcher.Changed.Add((fun (e:FileSystemEventArgs) -> printfn  "%s changed" e.Name))
watcher.Renamed.Add((fun (e:RenamedEventArgs) -> printfn  "%s renamed" e.Name))
watcher.Deleted.Add((fun (e:FileSystemEventArgs) -> printfn  "%s deleted" e.Name))  
#endif