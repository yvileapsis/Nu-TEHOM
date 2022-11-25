﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace OmniBlade
open System
open Prime
open Nu
open Nu.Declarative
open OmniBlade

type OmniBladePlugin () =
    inherit NuPlugin ()

    override this.EditModes =
        Map.ofSeq
            [("Title", fun world -> Game.SetModel (Gui Title) world)
             ("Credits", fun world -> Game.SetModel (Gui Credits) world)
             ("Pick", fun world -> Game.SetModel (Gui Pick) world)
             ("Field", fun world -> Game.SetModel (Field (Field.debug world)) world)
             ("Battle", fun world -> Game.SetModel (Field (Field.debugBattle world)) world)]