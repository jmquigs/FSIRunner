module Main

open FunScript
open FunScript.TypeScript

open FunScript.TypeScript.THREE

// TODO: At some point I have to figure out why these functions are reported as "not defined"
// when used directly; probably a missing assembly
[<JS; JSEmit("return Math.floor({0}) ")>]
let jsFloor (a:'a) = failwith "never"

[<JS; JSEmit("return Math.random() ")>]
let jsRandom () = failwith "never"

[<JS; JSEmit("return Math.cos({0}) ")>]
let jsCos (a:'a) = failwith "never"

[<JS; JSEmit("return Math.sin({0}) ")>]
let jsSin (a:'a) = failwith "never"

// Set the specified key and value on an object.  
// Useful for cases where the typedef is missing the definition of the field.
[<JS; JSEmit("obj[{1}] = {2} ")>]
let jsSetField (obj:'a,key:string,value:'b) = failwith "never"

// Another workaround for a typedef issue
[<JS; JSEmit("return new THREE.DirectionalLight({0}) ")>]
let jsNewThreeDirectionalLight (dParams:'a):THREE.DirectionalLight = failwith "never"

[<ReflectedDefinition>]
let onLoad() =
    let container = Globals.document.getElementById("#container") :?> HTMLDivElement
    //container.innerHTML <- "<h1>Hello FSIRunner's World!</h1>"

    let init() =
        let wnd = Globals.window
        // TODO: replace with combined camera
        let camera = THREE.CombinedCamera.Create(wnd.innerWidth / 2.0, wnd.innerHeight / 2.0, 70.0, 1.0, 1000.0, -500.0, 1000.0)

        camera.position.set(200.0,100.0,200.0) |> ignore
        let scene = THREE.Scene.Create() :?> THREE.Scene

        // grid
        let size = 500
        let step = 50

        let geometry = THREE.Geometry.Create()
        let maxSize = size - 1

        let pushVert (vArray:THREE.Vector3 []) vert =
            // work around for strange push() typedef on geometry.vertices
            let len = int vArray.length 
            vArray.[len] <- vert // illegal in .Net, but not in JS.

        let pushLineVert = pushVert (geometry.vertices)

        for i in [-size..step..maxSize] do
            pushLineVert(THREE.Vector3.Create( float -size, 0.0, float i))
            pushLineVert(THREE.Vector3.Create( float  size, 0.0, float i))
            pushLineVert(THREE.Vector3.Create( float i, 0.0, float -size))
            pushLineVert(THREE.Vector3.Create( float i, 0.0, float  size))

        let lineParams = createEmpty<THREE.LineBasicMaterialParameters>()
        lineParams.color <- 0.0
        //lineProps.opacity <- 0.2 // not found in type def
        jsSetField(lineParams, "opacity", 0.2) 
        let lineMaterial = THREE.LineBasicMaterial.Create(lineParams) 

        let grid = THREE.Line.CreateOverload2(geometry,lineMaterial)
        grid._type <- THREE.Globals.LinePieces
        scene.add grid

        // Cubes 
        let boxGeometry = THREE.BoxGeometry.Create(50.0, 50.0, 50.0)
        let boxMaterialParams = createEmpty<THREE.MeshLambertMaterialParameters>()
        boxMaterialParams.color <- float 0xFFFFFF
        boxMaterialParams.shading <- THREE.Globals.FlatShading
        //boxMaterialParams.overdraw <- 0.5 // missing from typedef
        jsSetField(boxMaterialParams, "overdraw", 0.5)

        let boxMaterial = THREE.MeshLambertMaterial.Create(boxMaterialParams)
        for i in [0..99] do 
            let cube = THREE.Mesh.Create(boxGeometry,boxMaterial)
            cube.scale.y <- jsFloor (jsRandom() * 2.0 + 1.0) // city ordinance, no building shall have more than 3 floors!
            cube.position.x <- jsFloor ( (jsRandom() * 1000.0 - 500.0) / 50.0 ) * 50.0 + 25.0
            cube.position.y <- (cube.scale.y * 50.0) / 2.0
            cube.position.z <- jsFloor ( (jsRandom() * 1000.0 - 500.0) / 50.0 ) * 50.0 + 25.0
            scene.add(cube)
            // The original samples don't seem to require updating the transform, but I always need to do it.
            cube.updateMatrixWorld(true) 

        // Lights
        let ambientLight = THREE.AmbientLight.Create(jsRandom() * (float 0x10))
        scene.add(ambientLight)

        let addDirectionalLight() = 
            // The compiler generates new THREE.Light for this, which is wrong.  Probably an issue with the typescriptdef
            //let directionalLight = THREE.DirectionalLight.Create(jsRandom() * (float 0xFFFFFF))
            let directionalLight = jsNewThreeDirectionalLight(jsRandom() * (float 0xFFFFFF))

            directionalLight.position.x <- jsRandom() - 0.5 
            directionalLight.position.y <- jsRandom() - 0.5 
            directionalLight.position.z <- jsRandom() - 0.5 
            directionalLight.position.normalize() |> ignore
            scene.add(directionalLight)
            directionalLight.updateMatrixWorld(true)

        addDirectionalLight()
        addDirectionalLight()

        // Renderer
        let renderer = THREE.CanvasRenderer.Create()
        renderer.setClearColor( THREE.Color.CreateOverload2("#F0F0F0"))
        renderer.setSize(wnd.innerWidth, wnd.innerHeight)
        container.appendChild(renderer.domElement) |> ignore

        scene, camera, renderer

    let scene, camera, renderer = init()

    let totalTimeComputingStuff  = ref 0.0
    do 
        jsSetField(Globals.window, "totalTimeComputingStuff", totalTimeComputingStuff)

    let lookAtScene = ref true

    let render() =
        let start = Globals.Date.Create()
        let timer = Globals.Date.now() * 0.0001
        camera.position.x <- jsCos(timer) * 200.0
        camera.position.z <- jsSin(timer) * 200.0
        let elapsed = (Globals.Date.Create().getTime() - start.getTime())

        if lookAtScene.Value then
            camera.lookAt(scene.position)

        renderer.render(scene, camera)

        totalTimeComputingStuff := totalTimeComputingStuff.Value + elapsed

    let rec animate (dt:float) =
        Globals.requestAnimationFrame(FrameRequestCallbackDelegate(animate)) |> ignore
        render()

    // UI event handlers
    let addClickHandler anchorCssClass (f: MouseEvent -> obj) =
        let a = Globals.document.getElementsByClassName(anchorCssClass)
        match (int a.length) with
        | 0 -> Globals.console.log("no a link found for class: " + anchorCssClass)
        | n -> 
            let max = n - 1
            for i in 0..max do 
                (a.[i] :?> HTMLAnchorElement).addEventListener_click(new System.Func<MouseEvent,obj>(f), true) 

    let setDivHtml divId html =
        let div = Globals.document.getElementById(divId)
        div.innerHTML <- html

    addClickHandler "set-ortho" (fun (e) -> 
        camera.toOrthographic()
        setDivHtml "fov" "Orthographic mode"
        null)
    addClickHandler "set-persp" (fun (e) -> 
        camera.toPerspective()
        setDivHtml "fov" "Perspective mode"
        null)
    addClickHandler "set-lens" (fun (e) -> 
        let length = Globals.parseFloat((e.target :?> HTMLAnchorElement).getAttribute("data-length"))
        let fov = camera.setLens(length)
        setDivHtml "fov" ("Converted " + length.ToString() + "mm lens to FOV " + fov.ToString() + "&deg;")
        null)
    addClickHandler "set-fov" (fun (e) -> 
        let fov = Globals.parseFloat((e.target :?> HTMLAnchorElement).getAttribute("data-fov"))
        camera.setFov(fov)
        setDivHtml "fov" ("FOV " + fov.ToString() + "&deg;")
        null)
    addClickHandler "set-zoom" (fun (e) -> 
        let zoom = Globals.parseFloat((e.target :?> HTMLAnchorElement).getAttribute("data-zoom"))
        camera.setZoom(zoom)
        null)
    addClickHandler "set-view" (fun (e) -> 
        let view = (e.target :?> HTMLAnchorElement).getAttribute("data-view").Trim()
        lookAtScene := false
        match view with 
        | "Top" -> camera.toTopView()
        | "Bottom" -> camera.toBottomView()
        | "Left" -> camera.toLeftView()
        | "Right" -> camera.toRightView() 
        | "Front" -> camera.toFrontView()
        | "Back" -> camera.toBackView()
        | "Scene" -> lookAtScene := true
        | _ -> Globals.console.log("unknown view: " + view)
        null)

    // kick it off
    animate(0.0)

[<ReflectedDefinition>]
let webMain() = 
    //http://youmightnotneedjquery.com/, search for "document.ready"
    let domContentLoaded(event:Event):obj = 
        onLoad()
        null

    Globals.document.addEventListener_DOMContentLoaded(new System.Func<Event,obj>(domContentLoaded), true)



