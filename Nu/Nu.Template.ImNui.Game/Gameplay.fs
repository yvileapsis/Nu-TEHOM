﻿namespace MyGame
open System
open System.Numerics
open Prime
open Nu

type GameplayState =
    | Playing
    | Quitting
    | Quit

// this is our MMCC model type representing gameplay.
// this model representation uses update time, that is, time based on number of engine updates.
type Gameplay =
    { GameplayState : GameplayState }

    // this represents the gameplay model in an unutilized state, such as when the gameplay screen is not selected.
    static member empty =
        { GameplayState = Quit }

    // this represents the gameplay model in its initial state, such as when gameplay starts.
    static member initial =
        { GameplayState = Playing }

// this extends the Screen API to expose the Gameplay model as well as the Quit event.
[<AutoOpen>]
module GameplayExtensions =
    type Screen with
        member this.GetGameplay world = this.GetModelGeneric<Gameplay> world
        member this.SetGameplay value world = this.SetModelGeneric<Gameplay> value world
        member this.Gameplay = this.ModelGeneric<Gameplay> ()

// this is the dispatcher that defines the behavior of the screen where gameplay takes place.
type GameplayDispatcher () =
    inherit ScreenDispatcher<Gameplay> (Gameplay.empty)

    // here we define the screen's fallback model depending on whether screen is selected
    override this.GetFallbackModel (_, screen, world) =
        if screen.GetSelected world
        then Gameplay.initial
        else Gameplay.empty

    // here we define the behavior of our gameplay
    override this.Run (gameplay, _, world) =

        // declare scene group when selected
        let world = World.beginGroupFromFile "Scene" "Assets/Gameplay/Scene.nugroup" [] world
        let rotation = Quaternion.CreateFromAxisAngle ((v3 1.0f 0.75f 0.5f).Normalized, world.UpdateTime % 360L |> single |> Math.DegreesToRadians)
        let world = World.doStaticModel "StaticModel" [Entity.Position .= v3 0.0f 0.0f -2.0f; Entity.Rotation @= rotation] world
        let world = World.endGroup world

        // declare gui group
        let world = World.beginGroup "Gui" [] world
        let (gameplay, world) =
            match World.doButton "Quit" [Entity.Position .= v3 232.0f -144.0f 0.0f; Entity.Text .= "Quit"] world with
            | (true, world) -> ({ gameplay with GameplayState = Quitting }, world)
            | (false, world) -> (gameplay, world)
        let world = World.endGroup world

        // return gameplay and world values
        (gameplay, world)