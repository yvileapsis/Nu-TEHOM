﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace OmniBlade
open System
open Prime
open Nu

type [<Sealed>] OmniBladePlugin () =
    inherit NuPlugin ()

    override this.EditModes =
        Map.ofList
            [("Splash", fun world -> Game.SetOmniBlade Splash world)
             ("Title", fun world -> Game.SetOmniBlade Title world)
             ("Credits", fun world -> Game.SetOmniBlade Credits world)
             ("Pick", fun world -> Game.SetOmniBlade Pick world)
             ("Intro", fun world -> Game.SetOmniBlade (Intro Slot1) world)
             ("Gameplay", fun world ->
                let field = Field.initial world.UpdateTime Slot1
                let world = Simulants.Field.SetField field world
                let world = Simulants.Field.Signal (WarpAvatar field.Avatar.Perimeter.Bottom) world
                let world = Game.SetOmniBlade Field world
                world)
             ("Slot1", fun world ->
                let field = Field.loadOrInitial world.UpdateTime Slot1
                let world = Simulants.Field.SetField field world
                let world = Simulants.Field.Signal (WarpAvatar field.Avatar.Perimeter.Bottom) world
                let world = Game.SetOmniBlade Field world
                world)
             ("Slot2", fun world ->
                let field = Field.loadOrInitial world.UpdateTime Slot2
                let world = Simulants.Field.SetField field world
                let world = Simulants.Field.Signal (WarpAvatar field.Avatar.Perimeter.Bottom) world
                let world = Game.SetOmniBlade Field world
                world)
             ("Slot3", fun world ->
                let field = Field.loadOrInitial world.UpdateTime Slot3
                let world = Simulants.Field.SetField field world
                let world = Simulants.Field.Signal (WarpAvatar field.Avatar.Perimeter.Bottom) world
                let world = Game.SetOmniBlade Field world
                world)
             ("FieldDebug", fun world ->
                let field = Field.debug world.UpdateTime
                let world = Simulants.Field.SetField field world
                let world = Simulants.Field.Signal (WarpAvatar field.Avatar.Perimeter.Bottom) world
                let world = Game.SetOmniBlade Field world
                world)]

    override this.InitialPackages =
        [Assets.Gui.PackageName]