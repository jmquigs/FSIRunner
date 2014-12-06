module Main

open FunScript
open FunScript.TypeScript

open FunScript.TypeScript.THREE

// TODO: At some point I have to figure out why these functions are reported as "not defined"
// when used directly
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
            // The samples don't seem to require updating the transform, but I always have a need for it.  maybe its because the 
            // samples are based on an older version of three.js that did it automatically
            cube.updateMatrixWorld(true) 

        // Lights
        let ambientLight = THREE.AmbientLight.Create(jsRandom() * (float 0x10))
        scene.add(ambientLight)

        let addDirectionalLight() = 
            let directionalLight = THREE.DirectionalLight.Create(jsRandom() * (float 0xFFFFFF))
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

    let render() =
        let start = Globals.Date.Create()
        let timer = Globals.Date.now() * 0.0001
        camera.position.x <- jsCos(timer) * 200.0
        camera.position.z <- jsSin(timer) * 200.0
        let elapsed = (Globals.Date.Create().getTime() - start.getTime())

        camera.lookAt(scene.position)

        renderer.render(scene, camera)

        totalTimeComputingStuff := totalTimeComputingStuff.Value + elapsed

    let rec animate (dt:float) =
        Globals.requestAnimationFrame(FrameRequestCallbackDelegate(animate)) |> ignore
        render()

    // kick it off
    animate(0.0)

[<ReflectedDefinition>]
let webMain() = 
    //http://youmightnotneedjquery.com/, search for "document.ready"
    let domContentLoaded(event:Event):obj = 
        onLoad()
        null

    Globals.document.addEventListener_DOMContentLoaded(new System.Func<Event,obj>(domContentLoaded), true)



