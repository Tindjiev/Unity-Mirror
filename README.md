## How it works

It uses a secondary camera (mirror camera) and projects what it views onto a cube (mirror glass).
The mirror camera is placed behind the mirror following the Main camera (or the camera the mirror will be visible to).
Its Projection Matrix set so that the near clipping plane matches exactly the mirror glass surface.

When the Main camera gets close to the mirror, the mirror glass shrinks so that it fits the screen almost exactly.
This happens because otherwise the rendering on the mirror glass gets pixelated. So it's a nice trick to avoid increasing the pixel size of the render texture


## Limitations/Issues

* Currently you can't see a mirror from another mirror with the expected results, unless you make it only be visible from a mirror only and make its camera it follow the mirror camera
* Shrinking doesn't work well if the mirror is rotated around its local z (forward) axis

## Protals

This method will also work for projecting a portal camera.
It's a bit more complicated to set up but you could try by extending the MirrorCamera class with inside defining a Target Trasnform and overriding the following methods


```
    protected override Vector3 SetCameraPositionAndRotation()
    {
        Quaternion SeeTransformRotation = Target.rotation;

        Vector3 PositionOnSurface = RenderingSurfaceTransform.position + RenderingSurfaceTransform.lossyScale.z / 2f * RenderingSurfaceTransform.forward;

        Vector3 MirrorCameraGlobalPositionBehindRender = RenderingSurfaceTransform.up.Rotate180degAroundAxisNormalized() * (_playerCameraPosition - PositionOnSurface) + PositionOnSurface;

        _cameraSelf.transform.position = SeeTransformRotation * Quaternion.Inverse(RenderingSurfaceTransform.rotation) *
                                            (MirrorCameraGlobalPositionBehindRender - RenderingSurfaceTransform.position) + Target.position;

        _cameraSelf.transform.rotation = SeeTransformRotation;
        return MirrorCameraGlobalPositionBehindRender;
    }

    //this override is needed for shrinking
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