Download the unity package from Releases

## How it works

It uses a camera object (mirror camera) and projects what it views onto a cuboid (mirror glass).
The mirror camera is placed behind the mirror (on the opposite side) following the camera that is currently viewed by.
Its Projection Matrix is set so that the near clipping plane matches exactly the mirror glass surface.
For reflections of other mirrors' reflections it generates new cameras, which each one will render a specific reflection

When the camera that views the mirror gets close, the mirror "glass" shrinks so that it fits the screen almost exactly.
Otherwise, the rendering on the mirror glass would appear pixelated on the screen. So it's a nice trick to avoid increasing the pixel size of the render texture

## Limitations/Issues

* Shrinking doesn't work well if the mirror is rotated around its local z (forward) axis

## Portals

This method will also work for projecting a portal camera.
It's a bit more complicated to set up but you could try by extending the MirrorCamera class with inside defining a Target Transform and overriding the following methods


```
    protected override Vector3 SetCameraPositionAndRotation()
    {
        Quaternion TargetTransformRotation = _portal.Target.rotation;

        Vector3 PositionOnSurface = RenderingSurfaceTransform.position + RenderingSurfaceTransform.lossyScale.z / 2f * RenderingSurfaceTransform.forward;

        Vector3 MirrorCameraGlobalPositionBehindRender = RenderingSurfaceTransform.up.Rotate180degAroundAxisNormalized() * (_playerCameraPosition - PositionOnSurface) + PositionOnSurface;

        MirrorCamera.transform.position = TargetTransformRotation * Quaternion.Inverse(RenderingSurfaceTransform.rotation) *
                                            (MirrorCameraGlobalPositionBehindRender - RenderingSurfaceTransform.position) + _portal.Target.position;

        MirrorCamera.transform.rotation = TargetTransformRotation;
        return MirrorCameraGlobalPositionBehindRender;
    }

    //this override is needed for when shrinking, otherwise the same would apply as with mirror
    protected override void CalculateProjectionMatrixParameters(in Vector3 portalCameraGlobalPositionBehindView, out float left, out float right, out float top, out float bot, out float near, out float far)
    {
        Vector3 MirrorCamera_sub_MirrorGlass = portalCameraGlobalPositionBehindView - RenderingSurfaceTransform.position;

        float rightDist = Vector3.Dot(RenderingSurfaceTransform.right, MirrorCamera_sub_MirrorGlass);
        float upDist = Vector3.Dot(RenderingSurfaceTransform.up, MirrorCamera_sub_MirrorGlass);
        float forwardDist = Vector3.Dot(RenderingSurfaceTransform.forward, MirrorCamera_sub_MirrorGlass);

        Vector3 scaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        float rightDistCentre = Vector3.Dot(
            RenderingSurfaceTransform.right,
            portalCameraGlobalPositionBehindView - (RenderingSurfaceTransform.parent.position + RenderingSurfaceTransform.parent.localRotation * (RenderingSurfaceTransform.localScale.x * _originalLocalPosition))
            );

        var tleft = -scaleDiv2.x - rightDist;
        right = -2f * rightDistCentre - tleft;
        var tright = scaleDiv2.x - rightDist;
        left = -2f * rightDistCentre - tright;
        top = scaleDiv2.y - upDist;
        bot = -scaleDiv2.y - upDist;
        near = scaleDiv2.z - forwardDist;
        far = 100f;
    }
```

## Sin/Wave Effects

Just add the unity package with sin effects from Releases or download the files directly
Just attach the corresponding script to the mirror's camera to apply the effect
