Download the unity package from Releases

## How to use

Just add to the scene the mirror prefab or feel free to make your own mirror structure by using in it the mirror glass prefab.

If you want to use your own material for the mirror just add it to the mirror glass renderer. If you have the shader just add it to the mirror glass script via Inspector.
For your own no-reflection material or shader also add it to the mirror glass script via Inspector.
For your own RenderTexture, although not really recommended for multuple mirrors usage, add it to the mirror's camera.
You don't really need to add any of the above, only if you want something specific.

## How it works

It uses a camera object (mirror camera) and projects what it views onto a cuboid (mirror glass).
The mirror camera is placed behind the mirror (on the opposite side) following the camera that is currently viewed by.
Its Projection Matrix is set so that the near clipping plane matches exactly the mirror glass surface.
For reflections of other mirrors' reflections it generates new cameras, which each one will render a specific reflection.

When the camera that views the mirror gets close, the mirror "glass" shrinks so that it fits the screen almost exactly.
Otherwise, the rendering on the mirror glass would appear pixelated on the screen. So it's a nice trick to avoid increasing the pixel size of the render texture.
Also it will avoid rendering unnecessery reflections of other off-screen mirrors that would be normally seen by the viewed mirror.

## Limitations/Issues

* Shrinking doesn't work well if the mirror is rotated around its local z (forward) axis

## Portals

This method will also work for projecting a portal camera.
It's a bit more complicated to set up but you could try by extending the MirrorGlass class with inside defining a Target Transform and overriding the following methods/properties


```

    protected override bool _skipRender
    {
        get
        {
            if (PortalIsClosed)
            {
                Graphics.Blit(MirrorCamera.targetTexture, MirrorCamera.targetTexture, _closedPortalMaterial);
                return true;
            }
            return false;
        }
    }

    protected override Vector3 SetCameraPositionAndRotation()
    {
        Quaternion targetRotation = _portal.Target.rotation;
        Vector3 portalPosition = RenderingSurfaceTransform.position;
        Vector3 portalCameraGlobalPositionBehindRender = Rotate180degAroundAxisNormalized(RenderingSurfaceTransform.up) * (_currentCameraPosition - portalPosition);

        MirrorCamera.transform.position = targetRotation * Quaternion.Inverse(RenderingSurfaceTransform.rotation) *
                                            portalCameraGlobalPositionBehindRender + _portal.Target.position;

        MirrorCamera.transform.rotation = targetRotation;
        return portalCameraGlobalPositionBehindRender + portalPosition;
    }

    //this override is needed for shrinking
    protected override void CalculateProjectionMatrixParameters(in Vector3 portalCameraGlobalPositionBehindView, out float left, out float right, out float top, out float bot, out float near, out float far)
    {
        Vector3 mirrorCamera_sub_MirrorGlass = portalCameraGlobalPositionBehindView - RenderingSurfaceTransform.position;

        float rightDist = Vector3.Dot(RenderingSurfaceTransform.right, mirrorCamera_sub_MirrorGlass);
        float upDist = Vector3.Dot(RenderingSurfaceTransform.up, mirrorCamera_sub_MirrorGlass);
        float forwardDist = Vector3.Dot(RenderingSurfaceTransform.forward, mirrorCamera_sub_MirrorGlass);

        Vector3 scaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        float rightDistCentre = Vector3.Dot(
            RenderingSurfaceTransform.right,
            portalCameraGlobalPositionBehindView -
            (RenderingSurfaceTransform.parent.position + RenderingSurfaceTransform.parent.localRotation * (RenderingSurfaceTransform.localScale.x * _originalLocalPosition))); //calculating the original global position

        var tleft = -scaleDiv2.x - rightDist;
        right = -2f * rightDistCentre - tleft;
        var tright = scaleDiv2.x - rightDist;
        left = -2f * rightDistCentre - tright;
        top = scaleDiv2.y - upDist;
        bot = -scaleDiv2.y - upDist;
        near = -forwardDist;

        if (CustomCameraFarDistance)
        {
            far = CameraFarDistance;
        }
        else
        {
            Matrix4x4 currentCameraProjectionMatrix = Camera.current.projectionMatrix;
            far = currentCameraProjectionMatrix[2, 3] / (currentCameraProjectionMatrix[2, 2] + 1f) + 2f * forwardDist;
        }
    }

    public static Quaternion Rotate180degAroundAxisNormalized(in Vector3 axis) => new Quaternion(axis.x, axis.y, axis.z, 0f);

```

## Sin/Wave Effects

Just add the unity package with sin effects from Releases or download the files directly
Just attach the corresponding script to the mirror's camera to apply the effect
For other effects you want to create, make sure to follow the sin effects scripts' example
