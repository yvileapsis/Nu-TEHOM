﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

#I __SOURCE_DIRECTORY__
#r "nuget: Aether.Physics2D, 2.1.0"
#r "nuget: Csv, 2.0.93"
#r "nuget: DotRecast.Recast.Toolset, 2024.3.1"
#r "nuget: FParsec, 1.1.1"
#r "nuget: Twizzle.ImGuizmo.NET, 1.89.4.1"
#r "nuget: Magick.NET-Q8-AnyCpu, 13.5.0"
#r "nuget: Pfim, 0.11.3"
#r "nuget: Prime, 9.30.0"
#r "nuget: System.Configuration.ConfigurationManager, 8.0.0"
#r "nuget: System.Drawing.Common, 8.0.0"
#r "../../../Nu/Nu.Dependencies/AssimpNet/netstandard2.1/AssimpNet.dll"
#r "../../../Nu/Nu.Dependencies/BulletSharpPInvoke/netstandard2.1/BulletSharp.dll"
#r "../../../Nu/Nu.Dependencies/OpenGL.NET/lib/netcoreapp2.2/OpenGL.Net.dll"
#r "../../../Nu/Nu.Dependencies/SDL2-CS/netstandard2.0/SDL2-CS.dll"
#r "../../../Nu/Nu.Dependencies/TiledSharp/lib/netstandard2.0/TiledSharp.dll"
#r "../../../Nu/Nu.Math/bin/Debug/netstandard2.1/Nu.Math.dll"
#r "../../../Nu/Nu/bin/Debug/net8.0/Nu.dll"

namespace Namespace
open System
open Prime
open Nu

[<AutoOpen>]
module TemplateFacet =

    type Entity with
        member this.GetValue world : single = this.Get (nameof this.Value) world
        member this.SetValue (value : single) world = this.Set (nameof this.Value) value world
        member this.Value = lens (nameof this.Value) this this.GetValue this.SetValue
        // TODO: optionally implement more user-defined properties.

    type TemplateFacet () =
        inherit Facet (false, false, false)

        static member Properties =
            [define Entity.Value 1234.5f]

        override this.Register (entity, world) =
            // TODO: optionally implement registration behavior for the faceted entity.
            world

        override this.Unregister (entity, world) =
            // TODO: optionally implement unregistration behavior for the faceted entity.
            world

        override this.Update (entity, world) =
            // TODO: optionally implement update code for the faceted entity.
            world

        override this.Render (renderPass, entity, world) =
            // TODO: optionally implement rendering code for the faceted entity.
            ()