module Main

open System
open System.IO
// System.Net.Http is only required for the test endpoint that uses HttpClient.
// Note: use of this namespace from fsharpi requires an up-to-date fsharp installation; the
// one that ships with Xamarin 5.6.2 is old, and fsharpi is linked against the .Net 4.0 profile, which will
// throw "Missing method System.Net.WebRequest::GetRequestStreamAsync()"
// when HttpClient's async features are used.
open System.Net.Http

type MainModule() = 
    inherit Nancy.NancyModule()
    do
        let view = base.View
        base.Get.["/"] <- (fun (thing) -> 
            (sprintf "Wello! Perhaps you'd like to visit a <a href=\"/view\">view</a>, or do something <a href=\"/async\">async</a>?") :> obj
        )

        base.Get.["/view"] <- (fun (thing) -> 
            view.["index.html"] :> obj
        )

        base.Get.["/async",true] <- (fun (rObj) -> 
            WebServer.asyncHelper (async {
                let client = new HttpClient()
                let! res = client.GetAsync("http://www.google.com") |> Async.AwaitTask
                let! content = res.Content.ReadAsStringAsync() |> Async.AwaitTask
                let len = content.Length
                return (sprintf "The size of the google home page is %A." len) :> obj
            })
        )

let startNancy() = WebServer.start "http://localhost:1234" [ typeof<MainModule> ] 
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

