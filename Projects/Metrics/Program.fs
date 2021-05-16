﻿namespace Metrics
open System
open System.Collections.Generic
open System.Numerics
open Prime
open Nu
open Nu.Declarative

#if ECS_HYBRID
type [<NoEquality; NoComparison; Struct>] StaticSpriteComponent =
    { mutable Active : bool
      mutable Entity : Entity
      mutable Sprite : Image AssetTag }
    interface StaticSpriteComponent Component with
        member this.Active with get () = this.Active and set value = this.Active <- value
        member this.AllocateJunctions _ = [||]
        member this.ResizeJunctions _ _ _ = ()
        member this.MoveJunctions _ _ _ _ = ()
        member this.Junction _ _ _ _ = this
        member this.Disjunction _ _ _ = ()
#endif

#if ECS
type [<NoEquality; NoComparison; Struct>] Velocity =
    { mutable Active : bool
      mutable Velocity : Vector2 }
    interface Velocity Component with
        member this.Active with get () = this.Active and set value = this.Active <- value
        member this.AllocateJunctions _ = [||]
        member this.ResizeJunctions _ _ _ = ()
        member this.MoveJunctions _ _ _ _ = ()
        member this.Junction _ _ _ _ = this
        member this.Disjunction _ _ _ = ()

type [<NoEquality; NoComparison; Struct>] Position =
    { mutable Active : bool
      mutable Position : Vector2 }
    interface Position Component with
        member this.Active with get () = this.Active and set value = this.Active <- value
        member this.AllocateJunctions _ = [||]
        member this.ResizeJunctions _ _ _ = ()
        member this.MoveJunctions _ _ _ _ = ()
        member this.Junction _ _ _ _ = this
        member this.Disjunction _ _ _ = ()

type [<NoEquality; NoComparison; Struct>] Mover =
    { mutable Active : bool
      mutable Velocity : Velocity ComponentRef
      mutable Position : Position ComponentRef }
    interface Mover Component with
        member this.Active with get () = this.Active and set value = this.Active <- value
        member this.AllocateJunctions ecs = [|ecs.AllocateJunction<Velocity> (); ecs.AllocateJunction<Position> ()|]
        member this.ResizeJunctions size junctions ecs = ecs.ResizeJunction<Velocity> size junctions.[0]; ecs.ResizeJunction<Position> size junctions.[1]
        member this.MoveJunctions src dst junctions ecs = ecs.MoveJunction<Velocity> src dst junctions.[0]; ecs.MoveJunction<Position> src dst junctions.[1]
        member this.Junction index junctions junctionsBuffered ecs = { id this with Velocity = ecs.Junction<Velocity> index junctions.[0] junctionsBuffered.[0]; Position = ecs.Junction<Position> index junctions.[1] junctionsBuffered.[1] }
        member this.Disjunction index junctions ecs = ecs.Disjunction<Velocity> index junctions.[0]; ecs.Disjunction<Position> index junctions.[1]
#endif

#if FACETED
type MetricsEntityDispatcher () =
    inherit EntityDispatcher ()

  #if !ECS_HYBRID && !ECS
    static member Facets =
        [typeof<StaticSpriteFacet>]

    override this.Update (entity, world) =
        entity.SetRotation (entity.GetRotation world + 0.03f) world
  #endif

  #if ECS_HYBRID
    override this.Register (entity, world) =
        let ecs = entity.Parent.Parent.GetEcs world
        let _ : Guid = ecs.RegisterCorrelated<StaticSpriteComponent> { Active = false; Entity = entity; Sprite = Assets.Default.Image4 } (entity.GetId world)
        world

    override this.Unregister (entity, world) =
        let ecs = entity.Parent.Parent.GetEcs world
        let _ : bool = ecs.UnregisterCorrelated<StaticSpriteComponent> (entity.GetId world)
        world
  #endif
#else
type MetricsEntityDispatcher () =
  #if ECS_HYBRID
    inherit EntityDispatcher ()
  #else
    inherit EntityDispatcher<Image AssetTag, unit, unit> (Assets.Default.Image)
  #endif

  #if !ECS_HYBRID && !ECS
    override this.Update (entity, world) =
        entity.SetRotation (entity.GetRotation world + 0.03f) world

    override this.View (staticImage, entity, world) =
        let transform = entity.GetTransform world
        View.Render
            (transform.Elevation,
             transform.Position.Y,
             AssetTag.generalize staticImage,
             SpriteDescriptor
                { Transform = transform
                  Offset = Vector2.Zero
                  Absolute = false
                  InsetOpt = None
                  Image = staticImage
                  Color = colWhite
                  Blend = Transparent
                  Glow = colZero
                  Flip = FlipNone })
  #endif

  #if ECS_HYBRID
    override this.Register (entity, world) =
        let ecs = entity.Parent.Parent.GetEcs world
        let _ : Guid = ecs.RegisterCorrelated<StaticSpriteComponent> { Active = false; Entity = entity; Sprite = Assets.Default.Image4 } (entity.GetId world)
        world

    override this.Unregister (entity, world) =
        let ecs = entity.Parent.Parent.GetEcs world
        let _ : bool = ecs.UnregisterCorrelated<StaticSpriteComponent> (entity.GetId world)
        world
  #endif
#endif

//type P = { mutable Active : bool; mutable P : Vector2 }
//type V = { mutable Active : bool; mutable V : Vector2 }
//type O = { mutable Active : bool; mutable P : P; mutable V : V }

type MyGameDispatcher () =
    inherit GameDispatcher<unit, unit, unit> (())

    let Fps = Simulants.DefaultGroup / "Fps"

    override this.Register (game, world) =
        let world = base.Register (game, world)
        let (screen, world) = World.createScreen (Some Simulants.DefaultScreen.Name) world
#if ECS_HYBRID
        // grab ecs from current screen
        let ecs = screen.GetEcs world

        // create static sprite system
        ecs.RegisterSystem (SystemCorrelated<StaticSpriteComponent, World> ecs)
#endif
#if ECS
        // get ecs
        let ecs = screen.GetEcs world

        // create systems
        ecs.RegisterSystem (SystemCorrelated<Velocity, World> ecs)
        ecs.RegisterSystem (SystemCorrelated<Position, World> ecs)
        ecs.RegisterSystem (SystemCorrelated<Mover, World> ecs)

        //// create object references
        //let count = 2500000
        //let ps = Array.init count (fun _ -> { Active = true; P = v2Zero })
        //let vs = Array.init count (fun _ -> { Active = true; V = v2Zero })
        //let objs = Array.init count (fun i -> { Active = true; P = ps.[i]; V = vs.[i]})
        ////let objs = Array.init count (fun _ -> { P = { P = v2Zero }; V = { V = v2One }})
        //
        ////// randomize elements in memory
        ////for i in 0 .. objs.Length - 1 do
        ////    objs.[i] <- objs.[Gen.random1 (objs.Length - 1)]
        ////    objs.[i].P <- objs.[Gen.random1 (objs.Length - 1)].P
        ////    objs.[i].V <- objs.[Gen.random1 (objs.Length - 1)].V
        //
        //// define update for out-of-place movers
        //ecs.Subscribe EcsEvents.Update $ fun _ _ _ ->
        //    for obj in objs do
        //        if obj.Active then
        //            obj.P.P.X <- obj.P.P.X + obj.V.V.X
        //            obj.P.P.Y <- obj.P.P.Y + obj.V.V.Y

        // mover count
        let moverCount = 4000000 // 4M movers (goal: 60FPS, current: 44FPS)

        // create movers
        for _ in 0 .. moverCount - 1 do
            let mover = ecs.RegisterCorrelated Unchecked.defaultof<Mover> Gen.id
            mover.Index.Velocity.Index.Velocity <- v2One

        // define update for movers
        ecs.Subscribe EcsEvents.Update $ fun _ _ _ ->
            for components in ecs.GetComponentArrays<Mover> () do
                // NOTE: perhaps the primary explanation for why iteration here is slower in .NET than C++ is that
                // we're using array indexing rather than direct pointer bumping. We're stuck with array indexing
                // here because pointer bumping requires use with unmanaged types, of which Mover is not due to
                // containing an array via its 'c ArrayRef members.
                for i in 0 .. components.Length - 1 do
                    let mutable comp = &components.[i]
                    if comp.Active then
                        let velocity = &comp.Velocity.Index
                        let position = &comp.Position.Index
                        position.Position.X <- position.Position.X + velocity.Velocity.X
                        position.Position.Y <- position.Position.Y + velocity.Velocity.Y

        // [| mutable P : Vector2; mutable V : Vector2 |]       8M
        // 
        // { mutable P : Vector2; mutable V : Vector2 }         5M / 1.25M
        // 
        // [| [| componentRef P |] [| componentRef V |] |]      3M (when #if !ECS_BUFFERED)
        //
        // [| [| ref P |] [| ref V |] |]                        2.5M
        // 
        // { mutable P : P; mutable V : V }                     2M / 250K
        // 
        // out-of-place entities                                50K
#endif
        let world = World.createGroup (Some Simulants.DefaultGroup.Name) Simulants.DefaultScreen world |> snd
        let world = World.createEntity<FpsDispatcher> (Some Fps.Name) DefaultOverlay Simulants.DefaultGroup world |> snd
        let world = Fps.SetPosition (v2 200.0f -250.0f) world
#if !ECS
        let positions = // 15,125 entity positions (goal: 60FPS, current: 57FPS)
            seq {
                for i in 0 .. 54 do
                    for j in 0 .. 54 do
                        for k in 0 .. 4 do
                            yield v2 (single i * 12.0f + single k) (single j * 12.0f + single k) }
        let world =
            Seq.fold (fun world position ->
                let (entity, world) = World.createEntity<MetricsEntityDispatcher> None NoOverlay Simulants.DefaultGroup world
                let world = entity.SetOmnipresent true world
                let world = entity.SetPosition (position + v2 -450.0f -265.0f) world
                let world = entity.SetSize (v2One * 8.0f) world
                world)
                world positions
#endif
        let world = World.selectScreen Simulants.DefaultScreen world
#if ECS_HYBRID
        // define update for static sprites
        ecs.Subscribe EcsEvents.Update $ fun _ _ _ ->
            for components in ecs.GetComponentArrays<StaticSpriteComponent> () do
                for i in 0 .. components.Length - 1 do
                    let comp = &components.[i]
                    if comp.Active then
                        let state = comp.Entity.State world
                        state.Rotation <- state.Rotation + 0.03f

        // define actualize for static sprites
        ecs.SubscribePlus Gen.id EcsEvents.Actualize $ fun _ _ _ world ->
            let messages = List ()
            for components in ecs.GetComponentArrays<StaticSpriteComponent> () do
                for i in 0 .. components.Length - 1 do
                    let comp = &components.[i]
                    if comp.Active then
                        let state = comp.Entity.State world
                        if state.Visible then
                            let spriteDescriptor = SpriteDescriptor { Transform = state.Transform; Absolute = state.Absolute; Offset = Vector2.Zero; InsetOpt = None; Image = comp.Sprite; Color = Color.White; Blend = Transparent; Glow = Color.Zero; Flip = FlipNone }
                            let layeredMessage = { Elevation = state.Elevation; PositionY = state.Position.Y; AssetTag = AssetTag.generalize comp.Sprite; RenderDescriptor = spriteDescriptor }
                            messages.Add layeredMessage
            World.enqueueRenderLayeredMessages messages world
#else
        ignore screen
#endif
        world

#if ELMISH
type ElmishEntityDispatcher () =
    inherit EntityDispatcher<Image AssetTag, unit, unit> (Assets.Default.Image)

    override this.View (staticImage, entity, world) =
        let transform = entity.GetTransform world
        View.Render
            (transform.Elevation,
             transform.Position.Y,
             AssetTag.generalize staticImage,
             SpriteDescriptor
                { Transform = transform
                  Offset = Vector2.Zero
                  Absolute = false
                  InsetOpt = None
                  Image = staticImage
                  Color = colWhite
                  Blend = Transparent
                  Glow = colZero
                  Flip = FlipNone })

#if PROPERTIES
type ElmishGameDispatcher () =
    inherit GameDispatcher<int, int, unit> (0)

    override this.Channel (_, game) =
        [game.UpdateEvent => msg 0]

    override this.Message (int, message, _, _) =
        match message with
        | 0 -> just (inc int)
        | _ -> just int

    override this.Content (int, _) =
        [Content.screen Gen.name Vanilla []
            [Content.group Gen.name []
                [Content.entity<ElmishEntityDispatcher> Gen.name
                    (seq {
                        yield Entity.Omnipresent == true
                        for index in 0 .. 49999 do yield Entity.Size <== int --> fun int -> v2 (single (int % 12)) (single (index % 12)) } |> // 50,000 property bindings (goal: 60FPS, currently: 59FPS)
                        Seq.toList)]
             Content.group Gen.name []
                [Content.fps "Fps" [Entity.Position == v2 200.0f -250.0f]]]]
#else
type [<ReferenceEquality>] Ints =
    { Ints : Map<int, int> }
    static member init n =
        { Ints = Seq.init n (fun a -> (a, a)) |> Map.ofSeq }
    static member inc ints =
        { Ints = ints.Ints |> Seq.map (fun kvp -> (kvp.Key, inc kvp.Value)) |> Map.ofSeq }

type [<ReferenceEquality>] Intss =
    { Intss : Map<int, Ints> }
    static member init n =
        { Intss = Seq.init n (fun a -> (a, Ints.init n)) |> Map.ofSeq }
    static member inc intss =
        { Intss = intss.Intss |> Seq.map (fun kvp -> (kvp.Key, Ints.inc kvp.Value)) |> Map.ofSeq }

type ElmishGameDispatcher () =
    inherit GameDispatcher<Intss, int, unit> (Intss.init 80) // 6400 ints (goal: 60FPS, current 55FPS)

    override this.Channel (_, game) =
        [game.UpdateEvent => msg 0]

    override this.Message (intss, message, _, _) =
        match message with
        | 0 -> just (Intss.inc intss)
        | _ -> just intss

    override this.Content (intss, _) =
        [Content.screen Gen.name Vanilla []
            [Content.groups intss (fun intss _ -> intss.Intss) constant (fun i intss _ ->
                Content.group (string i) []
                    [Content.entities intss (fun ints _ -> ints.Ints) constant (fun j int _ ->
                        Content.entity<ElmishEntityDispatcher> (string j)
                            (seq {
                                yield Entity.Omnipresent == true
                                yield Entity.Position == v2 (single i * 12.0f - 480.0f) (single j * 12.0f - 272.0f)
                                yield Entity.Size <== int --> fun int -> v2 (single (int % 12)) (single (int % 12)) } |>
                                Seq.toList))])
             Content.group Gen.name []
                [Content.fps "Fps" [Entity.Position == v2 200.0f -250.0f]]]]
#endif
#endif

#if TESTBED
type [<ReferenceEquality>] StringsOpt =
    { StringsOpt : Map<int, string> option }

type TestBedGameDispatcher () =
    inherit GameDispatcher<StringsOpt, unit, unit> ({ StringsOpt = None })

    override this.Channel (_, game) =
        [game.UpdateEvent => msg ()]

    override this.Message (stringsOpt, message, _, world) =
        match message with
        | () ->
            match World.getTickTime world % 4L with
            | 0L -> just { StringsOpt = None }
            | 1L -> just { StringsOpt = None }
            | 2L -> just { StringsOpt = Some (Map.ofList [(0, "0"); (1, "1"); (2, "2")]) }
            | 3L -> just { StringsOpt = Some (Map.ofList [(0, "3"); (1, "4"); (2, "5")]) }
            | _ -> failwithumf ()

    override this.Content (stringsOpt, _) =
        [Content.screen Gen.name Vanilla []
            [Content.group Gen.name []
                [Content.entityOpt stringsOpt (fun stringsOpt _ -> stringsOpt.StringsOpt) $ fun strings _ ->
                    Content.panel Gen.name []
                        [Content.entities strings constant constant $ fun i str _ ->
                            Content.text Gen.name
                                [Entity.Position == v2 (single i * 120.0f - 180.0f) 0.0f
                                 Entity.Text <== str]]]]]
#endif

#if PHANTOM
type [<ReferenceEquality>] Phantom =
    { mutable PhantomTransform : Transform
      PhantomLinearVelocity : Vector2
      PhantomAngularVelocity : single
      PhantomImage : Image AssetTag }
    static member init i =
        let x = single i * 0.05f + Gen.randomf - (single Constants.Render.VirtualResolutionX * 0.5f)
        let y = Gen.randomf - (single Constants.Render.VirtualResolutionY * 0.5f)
        { PhantomTransform = { Transform.makeDefault () with Position = v2 x y; Size = v2 9.0f 9.0f }
          PhantomLinearVelocity = v2 0.0f (Gen.randomf * 0.5f)
          PhantomAngularVelocity = Gen.randomf * 0.1f
          PhantomImage =  Assets.Default.CharacterIdleImage }
    static member move phantom =
        phantom.PhantomTransform.Position <- phantom.PhantomTransform.Position + phantom.PhantomLinearVelocity
        phantom.PhantomTransform.Rotation <- phantom.PhantomTransform.Rotation + phantom.PhantomAngularVelocity

type [<ReferenceEquality>] Phantoms =
    { Phantoms : Dictionary<Guid, Phantom> }
    static member init n =
        let phantoms = seq { for i in 0 .. n - 1 do yield (Gen.id, Phantom.init i) } |> dictPlus
        { Phantoms = phantoms }
    static member move phantoms =
        for entry in phantoms.Phantoms do
            Phantom.move entry.Value

type PhantomGameDispatcher () =
    inherit GameDispatcher<Phantoms, unit, unit> (Phantoms.init 20000)

    override this.Channel (_, game) =
        [game.UpdateEvent => cmd ()]

    override this.Command (phantoms, message, _, world) =
        match message with
        | () ->
            Phantoms.move phantoms
            just world

    override this.Content (_, _) =
        [Content.screen "Screen" Vanilla []
            [Content.group "Group" []
                [Content.fps "Fps" [Entity.Position == v2 200.0f -250.0f]]]]

    override this.View (phantoms, _, _) =
        Render
            (0.0f,
             single Constants.Render.VirtualResolutionY * -0.5f,
             Assets.Default.Empty,
             RenderCallback
                 (fun viewAbsolute viewRelative eyeCenter eyeSize renderer ->
                    let sdlRenderer = renderer :?> SdlRenderer
                    for phantom in phantoms.Phantoms do
                        SdlRenderer.renderSprite
                            viewAbsolute viewRelative eyeCenter eyeSize
                            phantom.Value.PhantomTransform (v2Dup 0.5f) None phantom.Value.PhantomImage colWhite colZero FlipNone
                            sdlRenderer))
#endif

type MetricsPlugin () =
    inherit NuPlugin ()
#if ELMISH
    override this.GetGameDispatcher () = typeof<ElmishGameDispatcher>
#else
  #if PHANTOM
    override this.GetGameDispatcher () = typeof<PhantomGameDispatcher>
  #else
    #if TESTBED
    override this.GetGameDispatcher () = typeof<TestBedGameDispatcher>
    #else
    override this.GetGameDispatcher () = typeof<MyGameDispatcher>
    #endif
  #endif
#endif

/// This program exists to take metrics on Nu's performance.
module Program =

    let [<EntryPoint; STAThread>] main _ =
        let sdlWindowConfig = { SdlWindowConfig.defaultConfig with WindowTitle = "MyGame" }
        let sdlConfig = { SdlConfig.defaultConfig with ViewConfig = NewWindow sdlWindowConfig }
#if FUNCTIONAL
        let worldConfig = { WorldConfig.defaultConfig with Imperative = false; SdlConfig = sdlConfig }
#else
        let worldConfig = { WorldConfig.defaultConfig with Imperative = true; SdlConfig = sdlConfig }
#endif
        Nu.init worldConfig.NuConfig
        World.run worldConfig (MetricsPlugin ())