﻿namespace InfinityRpg
open Prime
open Nu
open Nu.Constants
open InfinityRpg
open InfinityRpg.Constants

[<AutoOpen>]
module ComponentFactoryModule =

    type InfinityRpgComponentFactory () =
        inherit UserComponentFactory ()

        override this.MakeFacets () =
            Map.ofList
                [(typeof<CharacterAnimationFacet>.Name, CharacterAnimationFacet () :> Facet)
                 (typeof<CharacterControlFacet>.Name, CharacterControlFacet () :> Facet)
                 (typeof<CharacterCameraFacet>.Name, CharacterCameraFacet () :> Facet)]

        override this.MakeEntityDispatchers () =
            Map.ofList
                [(typeof<FieldDispatcher>.Name, FieldDispatcher () :> EntityDispatcher)
                 (typeof<PlayerCharacterDispatcher>.Name, PlayerCharacterDispatcher () :> EntityDispatcher)]

        override this.MakeScreenDispatchers () =
            Map.ofList
                [(typeof<GameplayDispatcher>.Name, GameplayDispatcher () :> ScreenDispatcher)]

        override this.MakeGameDispatchers () =
            Map.ofList
                [(typeof<InfinityRpgDispatcher>.Name, InfinityRpgDispatcher () :> GameDispatcher)]