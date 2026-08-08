using UnityEngine;

public static class IsometricGeometry
{
    public const float GroundAngle = 30f;

    public static Vector2 RightAxis => Axis(GroundAngle, 1f);
    public static Vector2 LeftAxis => Axis(GroundAngle, -1f);

    public static Vector2 Axis(float angleInDegrees, float horizontalDirection)
    {
        float radians = angleInDegrees * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Sign(horizontalDirection) * Mathf.Cos(radians),
            Mathf.Sin(radians));
    }
}
