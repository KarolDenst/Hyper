// Copyright The Authors of bepuphysics2

using System.Numerics;

namespace Hyper.Collisions.Bepu.RayCasting;
internal struct Ray
{
    public Vector3 Origin;
    public float MaximumT;
    public Vector3 Direction;
}
