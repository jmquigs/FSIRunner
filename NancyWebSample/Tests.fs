module Tests 

open Fuchu 
open System
open System.Net.Http
open System.Reflection

[<Tests>]
let tests() =
    testCase "basic"  <| 
        fun _ -> 
            Assert.Equal("2+2", 4, 2+2)

[<Tests>]
let moarTests() =
    testCase "http" <| 
        fun _ ->
            let httpRoot = "http://localhost:2345"
            let host = WebServer.start httpRoot [ typeof<WebModules.MainModule> ] 

            printfn "target: %s" httpRoot
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

let runTests types =
    // Needs refactoring.   
    // The member invocation below should support other field types (not just methods)

    let scanForTests (t:System.Type) =
        //printfn "searching type: %A" t.FullName
        let methods = t.GetMembers()
        let methodsWithAttr = methods |> Seq.filter (fun m -> 
                let attrs = m.GetCustomAttributes()
                let testsAttr = attrs |> Seq.tryFind (fun a -> a.GetType() = typedefof<Fuchu.TestsAttribute>) 
                // TODO should check return type to make sure it is a test
                match testsAttr with
                | None -> false
                | Some a -> true
            )
        (t, methodsWithAttr)

    let testsR = Seq.map scanForTests types |> Seq.filter (fun (t,methods) -> Seq.length methods > 0 ) |> List.ofSeq 
    match testsR with 
    | [] -> printfn "no tests found!"
    | x::rest -> 
        //printfn "most recent test: %A" x
        let t, methods = x
        let tests = methods |> Seq.map (fun m -> 
            printfn "Running test: %A" m
            let flags = BindingFlags.DeclaredOnly ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.InvokeMethod
            let thing = t.InvokeMember(m.Name, flags, null, null, null)
            //printfn "thing: %A" thing
            let thing = thing :?> Test
            thing
            )
        Test.Run tests |> ignore

