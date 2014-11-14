module Tests 

open Fuchu 
open System
open System.Net.Http

[<Tests>]
let tests =
    testCase "basic"  <| 
        fun _ -> 
            Assert.Equal("2+2", 4, 2+2)

[<Tests>]
let moarTests =
    testCase "http" <| 
        fun _ ->
            // Note, this test starts up its own web server.  However, it could use the one started by the WebPlugin.  Since 
            // that plugin is defined before TestsPlugin, the web server will be reinitialized with the latest changes before this 
            // code runs.
            let httpRoot = "http://localhost:2345"
            let host = WebServer.start httpRoot [ typeof<WebModules.MainModule> ] 

            try
                try
                    let client = new HttpClient()
                    let res = client.GetAsync(httpRoot) |> Async.AwaitTask |> Async.RunSynchronously
                    Assert.Equal("status 200", System.Net.HttpStatusCode.OK, res.StatusCode)
                    Assert.NotNull("res.Content", res.Content)
                    let contentData = res.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
                    Assert.StringContains("has message", "Wello!", contentData)
                    ()
                with 
                | e -> Assert.Equal("exception raised: " + e.Message, false, true)
            finally
                WebServer.stop host
