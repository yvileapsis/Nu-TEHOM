﻿namespace TerraFirma
open System
open System.Numerics
open Prime
open Nu

// this is our top-level MMCC model type. It determines what state the game is in. To learn about MMCC in Nu, see -
// https://github.com/bryanedds/Nu/wiki/Model-View-Update-for-Games-via-MMCC
type TerraFirma =
    | Splash
    | Title
    | Credits
    | Gameplay

// this is our top-level MMCC message type.
type TerraFirmaMessage =
    | ShowTitle
    | ShowCredits
    | ShowGameplay
    interface Message

// this is our top-level MMCC command type. Commands are used instead of messages when the world is to be transformed.
type TerraFirmaCommand =
    | Register
    | Exit
    interface Command

// this extends the Game API to expose the above MMCC model as a property.
[<AutoOpen>]
module TerraFirma =
    type Game with
        member this.GetTerraFirma world = this.GetModelGeneric<TerraFirma> world
        member this.SetTerraFirma value world = this.SetModelGeneric<TerraFirma> value world
        member this.TerraFirma = this.ModelGeneric<TerraFirma> ()

// this is the dispatcher that customizes the top-level behavior of our game. In here, we create screens as content and
// bind them up with events and properties.
type MyGameDispatcher () =
    inherit GameDispatcher<TerraFirma, TerraFirmaMessage, TerraFirmaCommand> (Splash)

    // here we define the game's properties and event handling
    override this.Definitions (terraFirma, _) =
        [Game.DesiredScreen :=
            match terraFirma with
            | Splash -> Desire Simulants.Splash
            | Title -> Desire Simulants.Title
            | Credits -> Desire Simulants.Credits
            | Gameplay -> Desire Simulants.Gameplay
         Game.RegisterEvent => Register
         if terraFirma = Splash then Simulants.Splash.DeselectingEvent => ShowTitle
         Simulants.TitleCredits.ClickEvent => ShowCredits
         Simulants.TitlePlay.ClickEvent => ShowGameplay
         Simulants.TitleExit.ClickEvent => Exit
         Simulants.CreditsBack.ClickEvent => ShowTitle
         Simulants.Gameplay.QuitEvent => ShowTitle]

    // here we handle the above messages
    override this.Message (_, message, _, _) =
        match message with
        | ShowTitle -> just Title
        | ShowCredits -> just Credits
        | ShowGameplay -> just Gameplay

    // here we handle the above commands
    override this.Command (_, command, _, world) =
        match command with
        | Register ->
            let world = World.setRenderer3dConfig { Renderer3dConfig.defaultConfig with SsrEnabled = true } world
            just world
        | Exit ->
            if world.Unaccompanied
            then just (World.exit world)
            else just world

    // here we describe the content of the game, including all of its screens.
    override this.Content (_, _) =
        [Content.screen Simulants.Splash.Name (Slide (Constants.Dissolve.Default, Constants.Slide.Default, None, Simulants.Title)) [] []
         Content.screenWithGroupFromFile Simulants.Title.Name (Dissolve (Constants.Dissolve.Default, Some Assets.Gui.GuiSong)) "Assets/Gui/Title.nugroup" [] []
         Content.screenWithGroupFromFile Simulants.Credits.Name (Dissolve (Constants.Dissolve.Default, Some Assets.Gui.GuiSong)) "Assets/Gui/Credits.nugroup" [] []
         Content.screen<GameplayDispatcher> Simulants.Gameplay.Name (Dissolve (Constants.Dissolve.Default, Some Assets.Gameplay.DesertSong)) [] []]