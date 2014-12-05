module Main

open FunScript
open FunScript.TypeScript

[<ReflectedDefinition>]
let webMain() = 
    //http://youmightnotneedjquery.com/, search for "document.ready"
    let domContentLoaded(event:Event):obj = 
        let container = Globals.document.getElementById("#container") :?> HTMLDivElement
        container.innerHTML <- "<h1>Hello FSIRunner's World!</h1>"
        null

    Globals.document.addEventListener_DOMContentLoaded(new System.Func<Event,obj>(domContentLoaded), true)



