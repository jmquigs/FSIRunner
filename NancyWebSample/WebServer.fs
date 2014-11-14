module WebServer

open System
open System.Threading
open System.Reflection

open Nancy
open Nancy.Hosting.Self
open Nancy.Bootstrapper
open Nancy.ViewEngines

module private NancyHelpers =
    type RouteDescriptionProvider() =
        interface Nancy.Routing.IRouteDescriptionProvider with
            member x.GetDescription(mdl:Nancy.INancyModule, path:string) = sprintf "%A in %A" path (mdl.GetType().FullName)

    // This is needed to overcome a nancy quirk:
    // https://groups.google.com/forum/#!topic/nancy-web-framework/V_Hoeg5TfVs
    type BaseHideObjectMembers() =
        interface IHideObjectMembers with 
            member x.Equals(o) = (x :> System.Object).Equals(o)
            member x.GetHashCode() = (x :> System.Object).GetHashCode()
            member x.GetType() = (x :> System.Object).GetType()
            member x.ToString() = (x :> System.Object).ToString()

    type RootPathProvider() =
        inherit BaseHideObjectMembers()
            interface Nancy.IRootPathProvider 
                with member x.GetRootPath() = Environment.CurrentDirectory

    type Bootstrapper(modules: System.Type list) = 
        inherit Nancy.DefaultNancyBootstrapper()
        let ic = Nancy.Bootstrapper.NancyInternalConfiguration.Default
        let rpp = new RootPathProvider()

        let modules = modules |> List.map (fun m -> new Nancy.Bootstrapper.ModuleRegistration(m)) |> Seq.ofList
        do 
            StaticConfiguration.DisableErrorTraces <- false // Set to false to have full stack traces sent to browser
            //StaticConfiguration.Caching.EnableRuntimeViewDiscovery <- true 
            //StaticConfiguration.Caching.EnableRuntimeViewUpdates <- true 
            ic.RouteDescriptionProvider <- typeof<RouteDescriptionProvider>

        // It's important to disable autoregistration for the most part, because it slows down FSIRunner reloads dramatically when many assemblies are present.
        override x.AutoRegisterIgnoredAssemblies = 
            let ignored:seq<Func<Assembly, bool>> = 
                (Seq.ofList [
                    //for x in Nancy.DefaultNancyBootstrapper.DefaultAutoRegisterIgnoredAssemblies do
                    //    yield x
                    yield new Func<Assembly, bool>(fun asm -> true )
                    ]) 
            ignored
        override x.Modules 
            with get() = modules
        override x.InternalConfiguration 
            with get() = ic
        override x.RootPathProvider 
            with get() = rpp :> IRootPathProvider

// Helper for interacting with the NancyModule's async handler format
let asyncHelper block = 
    (fun token -> Async.StartAsTask(block, Tasks.TaskCreationOptions.None, token))

let start httpRoot modules = 
    let hc = new HostConfiguration()
    hc.RewriteLocalhost <- false
    let ur = new UrlReservations()
    ur.CreateAutomatically <- true
    hc.UrlReservations <- ur

    let listen = new Uri(httpRoot)
    let host = new NancyHost(new NancyHelpers.Bootstrapper(modules), hc, listen)
    host.Start()
    host

let stop (host:NancyHost) =
    host.Stop()
    host.Dispose()