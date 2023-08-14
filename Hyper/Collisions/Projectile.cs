﻿using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Hyper.Collisions.Bepu;
using Hyper.Meshes;

namespace Hyper.Collisions;
internal class Projectile
{
    public BodyHandle Body { get; private set; }
    private TypedIndex _shape;
    private float _lifeTime;
    private bool _disposed;
    public bool IsDead { get; private set; }
    public ProjectileMesh Mesh { get => _mesh; }

    private readonly ProjectileMesh _mesh;

    private Projectile(ProjectileMesh mesh, float lifeTime)
    {
        _mesh = mesh;
        _lifeTime = lifeTime;
    }

    public static Projectile CreateStandardProjectile(Simulation simulation, CollidableProperty<SimulationProperties> properties,
        in RigidPose initialPose, in BodyVelocity initialVelocity)
    {
        var projectileShape = new Box(1, 1, 1);

        ProjectileMesh mesh = new ProjectileMesh(1);
        Projectile projectile = new Projectile(mesh, 5);
        projectile._shape = simulation.Shapes.Add(projectileShape);
        var inertia = projectileShape.ComputeInertia(0.01f);

        projectile.Body = simulation.Bodies.Add(BodyDescription.CreateDynamic(initialPose, initialVelocity, inertia, new CollidableDescription(projectile._shape, 0.5f), 0.01f));
        ref var bodyProperties = ref properties.Allocate(projectile.Body);
        bodyProperties = new SimulationProperties { Friction = 2f, Filter = new SubgroupCollisionFilter(projectile.Body.Value, 0) };

        return projectile;
    }

    public void Update(Simulation simulation, float dt, BufferPool pool)
    {
        var body = new BodyReference(Body, simulation.Bodies);
        _mesh.Update(body.Pose);

        _lifeTime -= dt;
        if (!_disposed && _lifeTime < 0)
        {
            simulation.Bodies.Remove(Body);
            simulation.Shapes.RemoveAndDispose(_shape, pool);

            _disposed = true;
            IsDead = true;
        }
    }
}
