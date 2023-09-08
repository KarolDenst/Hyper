using Hyper.Collisions.Bepu;
using Hyper.Collisions.Bepu.RayCasting;
using Hyper.TypingUtils;
using OpenTK.Mathematics;

namespace Hyper.GameEntities;

internal class Player : Humanoid
{
    private float _rayOffsetLength = 3f;
    public Player(PhysicalCharacter physicalCharacter) : base(physicalCharacter)
    { }

    public void Render(Shader shader, float scale, Vector3 cameraPosition, bool isFirstPerson)
    {
        if (!isFirstPerson)
            Character.Render(PhysicalCharacter.Pose, shader, scale, cameraPosition);
    }

    // in general this can depend on the properties of the character e.g. size etc
    public Vector3 GetThirdPersonCameraOffset(Camera camera)
        => camera.Up * 1f - camera.Front * 5f;

    public System.Numerics.Vector3 GetCharacterRay(Vector3 viewDirection, float length)
        => PhysicalCharacter.Pose.Position + Conversions.ToNumericsVector(viewDirection) * length;

    public Ray GetRay(Vector3 viewDirection, float length)
        => new Ray
        {
            Direction = Conversions.ToNumericsVector(viewDirection),
            MaximumT = length,
            Origin = PhysicalCharacter.Pose.Position + System.Numerics.Vector3.UnitX /*Conversions.ToNumericsVector(viewDirection)*/ * _rayOffsetLength /*+ System.Numerics.Vector3.UnitY*/
        };
}