// Copyright The Authors of bepuphysics2

using System.Numerics;
using BepuPhysics.Collidables;

namespace Hyper.Collisions.Bepu.RayCasting;
internal struct RayHit
{
    public Vector3 Normal;
    //public Vector3 Direction;
    public float T;
    public CollidableReference Collidable;
    public bool Hit;
}
