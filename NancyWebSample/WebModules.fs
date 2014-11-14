namespace WebModules
// System.Net.Http is only required for the test endpoint that uses HttpClient.
// Note: use of this namespace from fsharpi requires an up-to-date fsharp installation; the
// one that ships with Xamarin 5.6.2 is old; fsharpi is linked against the .Net 4.0 profile, which will
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
