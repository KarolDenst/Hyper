using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Hyper.Collisions.Bepu;
using Hyper.GameEntities;

namespace Hyper.Collisions;
internal class ContactEventHandler : IContactEventHandler
{
    Dictionary<BodyHandle, Projectile> _projectiles;
    Dictionary<BodyHandle, Humanoid> _humanoids;
    Simulation _simulation;
    ulong _counter = 0;

    public ContactEventHandler(Simulation simulation, Dictionary<BodyHandle, Projectile> projectiles, Dictionary<BodyHandle, Humanoid> humanoids)
    {
        _simulation = simulation;
        _projectiles = projectiles;
        _humanoids = humanoids;
    }

    public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
                Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        var collisionLocation = contactOffset + (pair.A.Mobility == CollidableMobility.Static ?
                        new StaticReference(pair.A.StaticHandle, _simulation.Statics).Pose.Position :
                        new BodyReference(pair.A.BodyHandle, _simulation.Bodies).Pose.Position);

#if DEBUG
        if (pair.A.Mobility == CollidableMobility.Dynamic
            && pair.B.Mobility == CollidableMobility.Dynamic)
        {
            Console.WriteLine($"collision between dynamics #{_counter++} at {collisionLocation}");
            if (_projectiles.TryGetValue(pair.A.BodyHandle, out var projectile)
                && _humanoids.TryGetValue(pair.B.BodyHandle, out var humanoid))
            {
                Console.WriteLine($"\thumanoid collided with a projectile");
            }
            else if (_projectiles.TryGetValue(pair.B.BodyHandle, out projectile)
                && _humanoids.TryGetValue(pair.A.BodyHandle, out humanoid))
            {
                Console.WriteLine($"\thumanoid collided with a projectile");
            }
            else
            {
                Console.WriteLine("\tunknown collision pair");
            }
            Console.WriteLine();
        }
#endif
    }
}
