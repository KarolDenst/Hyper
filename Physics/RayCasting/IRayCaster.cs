﻿using System.Numerics;

namespace Physics.RayCasting;
public interface IRayCaster
{
    /// <summary>
    /// Maximum length of the ray
    /// </summary>
    public float RayMaximumT { get; }

    /// <summary>
    /// Direction of the ray
    /// </summary>
    public Vector3 RayDirection { get; }

    /// <summary>
    /// Origin of the ray
    /// </summary>
    public Vector3 RayOrigin { get; }

    /// <summary>
    /// Id of the ray cast by this ray caster
    /// </summary>
    public int RayId { get; }
}
