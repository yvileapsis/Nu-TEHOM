﻿namespace BlazeVector
open Prime
open Nu
open BlazeVector

[<AutoOpen>]
module BlazeDispatcherModule =

    // this is our Elm-style command type. To learn about the Elm-style, read this article here -
    // https://vsyncronicity.com/2020/03/01/a-game-engine-in-the-elm-style/
    type BlazeCommand =
        | PlaySplashSound
        | ShowTitle
        | ShowCredits
        | ShowGameplay
        | ExitGame

    // this is our main game type implemented in the Elm-style. 
    type BlazeDispatcher () =
        inherit GameDispatcher<unit, unit, BlazeCommand> (())

        // here we channel from events to signals
        override this.Channel (_, _) =
            [Simulants.Splash.SelectEvent => [cmd PlaySplashSound]
             Simulants.TitleCredits.ClickEvent => [cmd ShowCredits]
             Simulants.TitlePlay.ClickEvent => [cmd ShowGameplay]
             Simulants.TitleExit.ClickEvent => [cmd ExitGame]
             Simulants.CreditsBack.ClickEvent => [cmd ShowTitle]
             Simulants.GameplayBack.ClickEvent => [cmd ShowTitle]]

        // here we handle the above commands
        override this.Command (_, command, _, world) =
            let world =
                match command with
                | PlaySplashSound -> World.playSound Constants.Audio.DefaultSoundVolume Assets.NuSplashSound world
                | ShowTitle -> World.transitionScreen Simulants.Title world
                | ShowCredits -> World.transitionScreen Simulants.Credits world
                | ShowGameplay -> World.transitionScreen Simulants.Gameplay world
                | ExitGame -> World.exit world
            just world

        // here we describe the content of the game including all of its screens.
        override this.Content (_, _) =
            let playMachinery = { Volume = Constants.Audio.DefaultSongVolume; FadeOutMs = Constants.Audio.DefaultFadeOutMs; Song = Assets.MachinerySong }
            let playDeadBlaze = { Volume = 0.5f (* turn up song's volume! *); FadeOutMs = Constants.Audio.DefaultFadeOutMs; Song = Assets.DeadBlazeSong }
            [Content.screen Simulants.Splash.Name (Splash (Constants.Dissolve.Default, Constants.Splash.Default, Simulants.Title)) [] []
             Content.screenFromLayerFile Simulants.Title.Name (Dissolve (Constants.Dissolve.Default, Some playMachinery)) Assets.TitleLayerFilePath
             Content.screenFromLayerFile Simulants.Credits.Name (Dissolve (Constants.Dissolve.Default, Some playMachinery)) Assets.CreditsLayerFilePath
             Content.screenFromLayerFile<GameplayDispatcher> Simulants.Gameplay.Name (Dissolve (Constants.Dissolve.Default, (Some playDeadBlaze))) Assets.GameplayLayerFilePath]

        // here we hint to the renderer and audio system that the 'Gui' package should be loaded
        override this.Register (game, world) =
            let world = World.hintRenderPackageUse Assets.GuiPackageName world
            let world = World.hintAudioPackageUse Assets.GuiPackageName world
            base.Register (game, world)