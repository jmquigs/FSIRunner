module Main

open System
open System.IO

let startNancy() = WebServer.start "http://localhost:1234" [ typeof<WebModules.MainModule> ] 
let stopNancy host = WebServer.stop host

#if COMPILED
[<EntryPoint>]
#endif
let main argv = 
    // Need to find views directory.  Check to see if we are running from "bin". 
    // Note, in FSI mode, we assume the watcher is being run from the directory containing "views" (the web root).

    let viewsDir = "views"
    if not (Directory.Exists(viewsDir)) then
        let maybeViews = Path.Combine(Environment.CurrentDirectory, "../../", viewsDir)
        if not (Directory.Exists(maybeViews)) then
            printfn "Cannot find %s directory" viewsDir
            Environment.Exit(1)
        else
            Environment.CurrentDirectory <- maybeViews

    let host = startNancy()
    printfn "press enter to exit"
    Console.ReadLine() |> ignore
    stopNancy host
    0 

