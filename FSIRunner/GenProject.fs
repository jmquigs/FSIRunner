namespace FSIRunner

open System.IO
open System.Xml
open System

module GenProject =
    let private isUnix = Environment.OSVersion.Platform.ToString().ToLowerInvariant().Contains("unix")
    let private normalizePath (path:string) =
        if isUnix then path.Replace(@"\", "/") else path.Replace("/", @"\\")

    let sortByReferenceOrder (referenceOrder:string list) (references:seq<string>) = 
        if List.length referenceOrder = 0 then
            references
        else
            // ensure that any references in referenceOrder are sorted in order in the reference list.  
            // Any remaining references are included unordered.
            // TODO: ahh my eyes! it doesn't need to be this nasty
            let rec orderRefs (remainingRefs:string list) accumRefs =
                match remainingRefs with 
                | x::xs ->
                    // check to see if x is in the ordered list, if so prepend it to the final list, otherwise append it
                    let ordered = referenceOrder |> List.tryFind (fun orderedRef -> x.ToLowerInvariant().Contains(orderedRef.ToLowerInvariant())) |> Option.isSome
                    if ordered then
                        orderRefs xs (x::accumRefs)
                    else
                        orderRefs xs (accumRefs @ [x])
                | [] ->
                    accumRefs

            let references = orderRefs (List.ofSeq references) []
            Seq.ofList references

    let generate inputFSProjectFile outFile (excludedFiles: string list) (referenceOrder: string list) = 
        let text = File.ReadAllText(inputFSProjectFile)

        // This XML code is ripped/modified from one of the FSProjects, but I can't remember which one
        let document = 
            let xd = new XmlDocument()
            xd.LoadXml text
            xd

        let nsmgr = 
            let nsmgr = new XmlNamespaceManager(document.NameTable)
            nsmgr.AddNamespace("default", document.DocumentElement.NamespaceURI)
            nsmgr

        let excludedFiles = 
            "assemblyinfo.fs"::excludedFiles // don't need assemblyinfo
            |> List.map (fun en -> en.ToLowerInvariant()) // case-insensitive compare

        let compileNodesXPath = "/default:Project/default:ItemGroup/default:Compile"
        let getCompileNodes (document:XmlDocument) =         
            [for node in document.SelectNodes(compileNodesXPath,nsmgr) -> node]
        let getCompiledFiles() =
            let cnodes = getCompileNodes document
            let cfiles = 
                cnodes 
                |> Seq.map (fun n -> 
                    n.Attributes.GetNamedItem("Include").Value |> normalizePath )
                |> Seq.filter (
                    fun n -> 
                        let nLower = n.ToLowerInvariant()
                        excludedFiles |> List.tryFind (fun excludedName -> nLower.Contains(excludedName)) |> Option.isNone
                ) 
            cfiles

        let refsXPath = "/default:Project/default:ItemGroup/default:Reference"
        let getReferenceNodes (document:XmlDocument) =         
            [for node in document.SelectNodes(refsXPath,nsmgr) -> node]
        let getReferenceFiles() =
            let rnodes = getReferenceNodes document
            let implicit = [ "mscorlib" ; "FSharp.Core" ; "System" ; "System.Core" ; "System.Numerics" ] |> List.map (fun s -> s.ToLowerInvariant())

            let getChildNodeValue (childName:string) (n:XmlNode) :string option =
                let childName = childName.ToLowerInvariant()
                let cnode = (Seq.cast n.ChildNodes) |> Seq.tryFind (fun (cn:XmlNode) -> cn.Name.ToLowerInvariant() = childName )
                match cnode with 
                | Some cn when (cn.InnerText.Trim() <> "") -> Some (normalizePath cn.InnerText)
                | None -> None
                | Some cn -> None

            let rfiles = 
                rnodes 
                |> Seq.choose (fun n ->
                    let includeVal = n.Attributes.GetNamedItem("Include").Value.ToString()
                    let includeValLwr = includeVal.ToLowerInvariant()

                    match (implicit |> List.tryFind (fun ival -> ival = includeValLwr)) with
                    | Some x ->
                        None // don't include implicit references
                    | None ->
                        // include this reference; use HintPath if it exists, otherwise append ".dll" to the reference name
                        let hintPath = getChildNodeValue "hintpath" n

                        let includeDll = includeVal + ".dll" 
                        match hintPath with 
                        | None -> Some (includeDll)
                        | Some hp -> Some (hp))
                |> Seq.map (fun rpath -> rpath.Replace("\\", "/"))
                |> (sortByReferenceOrder referenceOrder)

            rfiles

        let sw = new StringWriter()
        do 
            sw.WriteLine("// this file is automatically generated by GenProjectPlugin.fsx")
            sw.WriteLine("// (note, in Xamarin you may need to close/reopen this file to see changes)")
            getReferenceFiles() |> Seq.iter (fun r ->  
                //sw.WriteLine("printfn \"Loading ref " + r + "\"")
                sw.WriteLine("#r \"" + r + "\""))
            getCompiledFiles() |> Seq.iter (fun f -> sw.WriteLine("#load \"" + f + "\""))
            //printfn "%s" (sw.ToString())

            let outFile = Path.Combine(Directory.GetParent(inputFSProjectFile).FullName, outFile)
            printfn "Writing updated project to: %s" outFile
            File.WriteAllText(outFile, (sw.ToString()))
