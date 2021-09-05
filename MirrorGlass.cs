using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MirrorGlass : MonoBehaviour
{

    #region ReferencesToOtherObjects

    [field: SerializeField]
    public Camera MirrorCamera { get; private set; }

    [field: SerializeField]
    public Transform RenderingSurfaceTransform { get; private set; }

    [field: SerializeField]
    public MeshRenderer MirrorGlassRenderer { get; private set; }

    #endregion ReferencesToOtherObjects



    #region ControlVariables

    [field: SerializeField, ReadOnlyOnInspectorDuringPlay]
    public int XPixels { get; private set; } = 1920;

    [field: SerializeField, ReadOnlyOnInspectorDuringPlay]
    public int YPixels { get; private set; } = 1080;

    public bool StopRenderWhenNotViewed = true;
    public bool StopRenderWhenPlayerCamIsBehind = true;
    public bool ShrinkWhenPlayerCamGetsClose = true;

    #endregion ControlVariables



    #region VariablesThatStoreInfo

    protected Vector3 _originalLocalPosition { get; private set; }
    protected Vector3 _originalLocalScale { get; private set; }

    protected Vector3 _playerCameraPosition { get; private set; }
    protected Vector3 _renderingSurfaceGlobalScaleDiv2 { get; private set; }
    protected float _forwardDistanceToPlayerCamFromGlass { get; private set; }
    protected float _upDistanceToPlayerCamFromGlass { get; private set; }
    protected float _rightDistanceToPlayerCamFromGlass { get; private set; }


    #endregion VariablesThatStoreInfo



    #region ShrinkedPublicValues

    public float ShrinkedScaleRatioX => RenderingSurfaceTransform.localScale.x / _originalLocalScale.x;
    public float ShrinkedScaleRatioY => RenderingSurfaceTransform.localScale.y / _originalLocalScale.y;
    public float PositionChangeRatioX => (RenderingSurfaceTransform.localPosition.x - _originalLocalPosition.x) / _originalLocalScale.x;
    public float PositionChangeRatioY => (RenderingSurfaceTransform.localPosition.y - _originalLocalPosition.y) / _originalLocalScale.y;

    #endregion ShrinkedPublicValues

    Material _Black;

    private Dictionary<RenderTexture, UsageAndTime> _renderTexturesBeingUsed = new Dictionary<RenderTexture, UsageAndTime>();
    private Dictionary<Camera, UsageAndTime> _camerasUsed = new Dictionary<Camera, UsageAndTime>();

    protected void Awake()
    {
        _Black = new Material(Shader.Find("Mirror/Black"));
        _originalLocalPosition = RenderingSurfaceTransform.localPosition;
        _originalLocalScale = RenderingSurfaceTransform.localScale;

        if (MirrorGlassRenderer.sharedMaterial == null)
        {
            MirrorGlassRenderer.sharedMaterial = new Material(Shader.Find("Mirror/MirrorShader"));
        }
        if (MirrorCamera.targetTexture == null)
        {
            RenderTexture newTargetTexture = new RenderTexture(XPixels, YPixels, 32);
            MirrorCamera.targetTexture = newTargetTexture;
            MirrorGlassRenderer.material.mainTexture = newTargetTexture;
        }
        _renderTexturesBeingUsed.Add(MirrorCamera.targetTexture, new UsageAndTime(false, float.PositiveInfinity));
        _camerasUsed.Add(MirrorCamera, new UsageAndTime(false, float.PositiveInfinity));
    }

    //private static List<Camera> _camerasBeingRenderedRightNow = new List<Camera>();
    private static int _timeRecursed = 0;
    public static int MAXIMUM_RECURSIONS = 5;
    protected void OnWillRenderObject()
    {
#if UNITY_EDITOR
        if (Camera.current.name == "SceneCamera" || Camera.current.name == "Preview Camera") return;
#endif

        if (_camerasUsed.ContainsKey(Camera.current)) return;


        //RenderingSurfaceTransform.localPosition = _originalLocalPosition;
        //RenderingSurfaceTransform.localScale = _originalLocalScale;

        if (_timeRecursed > MAXIMUM_RECURSIONS)
        {
            Graphics.Blit(MirrorCamera.targetTexture, MirrorCamera.targetTexture, _Black);
            return;
        }

        _timeRecursed++;


        Camera originalMirrorCamera = MirrorCamera;
        UsageAndTime usageAndTime = _camerasUsed[MirrorCamera];
        //if (_camerasBeingRenderedRightNow.Contains(MirrorCamera))
        if (usageAndTime)
        {
            MirrorCamera = FindOrGenerateCamera();
            //MirrorCamera = SetNewCamera();
        }

        SetAndRenderMirrorCamera();

        MirrorCamera = originalMirrorCamera;


        _timeRecursed--;
    }



    private void SetAndRenderMirrorCamera()
    {

        #region PrepareVariables

        //start with original position and scale

        _renderingSurfaceGlobalScaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        Vector3 mirrorPosition = RenderingSurfaceTransform.position;
        Vector3 mirrorForward = RenderingSurfaceTransform.forward;
        Vector3 mirrorRight = RenderingSurfaceTransform.right;
        Vector3 mirrorUp = RenderingSurfaceTransform.up;

        _playerCameraPosition = Camera.current.transform.position;

        Vector3 playerCameraPosition_sub_CentrePointOnMirrorGlass = _playerCameraPosition - (mirrorPosition + _renderingSurfaceGlobalScaleDiv2.z * mirrorForward);
        _forwardDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorForward, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _rightDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorRight, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _upDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorUp, playerCameraPosition_sub_CentrePointOnMirrorGlass);

        #endregion PrepareVariables



        if (StopRenderWhenPlayerCamIsBehind && _forwardDistanceToPlayerCamFromGlass <= -0.1f)
        {
            //Debug.Log("@@@@ Camera NOT2 rendered: " + MirrorCamera.name, MirrorCamera);
            //Debug.Log("@@@@ By: " + Camera.current.name, Camera.current);
            MirrorCamera.enabled = false;
            return;
        }

        Vector3 mirrorCameraGlobalPosition = SetCameraPositionAndRotation();



        #region PrepareCornernPoints

        //left from the prespective of player (so its to the mirror's right)
        Vector3 leftTopPoint_MinusPlayCamPos = mirrorPosition
                                                 + mirrorForward * _renderingSurfaceGlobalScaleDiv2.z
                                                 + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _playerCameraPosition;


        Vector3 leftBotPoint_MinusPlayCamPos = mirrorPosition
                                                 + mirrorForward * _renderingSurfaceGlobalScaleDiv2.z
                                                 + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _playerCameraPosition;


        Vector3 rightTopPoint_MinusPlayCamPos = mirrorPosition
                                                 + mirrorForward * _renderingSurfaceGlobalScaleDiv2.z
                                                 - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _playerCameraPosition;


        Vector3 rightBotPoint_MinusPlayCamPos = mirrorPosition
                                                 + mirrorForward * _renderingSurfaceGlobalScaleDiv2.z
                                                 - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _playerCameraPosition;
        #endregion PrepareCornernPoints



        //todo: swap corner points so they better match camera and mirror orientation around its local z (forward) axis



        CalculateOutWardVerticalVectorsOfViewFrustum(Camera.current, out Vector3 rightRight, out Vector3 leftLeft, out Vector3 botDown, out Vector3 topUp, out Vector3 cameraForward, out float near);

        //checking if mirror is out of view frustum
        if (CheckForOutOfView(
            CheckIfSquareIsOutOfViewFrustum(leftTopPoint_MinusPlayCamPos, rightTopPoint_MinusPlayCamPos, rightBotPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos,
                                            topUp, rightRight, botDown, leftLeft, cameraForward, near)))
        {
            //Debug.Log("@@@@ Camera NOT1 rendered: " + MirrorCamera.name, MirrorCamera);
            //Debug.Log("@@@@ By: " + Camera.current.name, Camera.current);
            MirrorCamera.enabled = false;
            return;
        }



        if (!ShrinkWhenPlayerCamGetsClose)
        {
            CalculateAndSetProjectionMatrixAndRenderCamera(mirrorCameraGlobalPosition);
            return;
        }


        #region MirrorShrink
        //now that mirror is not out of view, it will be shrinked in case part of it is out of view frustum

        //checking for horizontal sides out of view

        Vector3 minusSideOfMirror = mirrorPosition - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x;
        Vector3 plusSideOfMirror = mirrorPosition + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x;


        //right side of screen
        if (Vector3.Dot(rightRight, rightTopPoint_MinusPlayCamPos) > 0f || Vector3.Dot(rightRight, rightBotPoint_MinusPlayCamPos) > 0f)
        {

            Vector3 pointBot = FindLinePiercingPlanePoint(rightBotPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos, rightRight);
            Vector3 pointTop = FindLinePiercingPlanePoint(rightTopPoint_MinusPlayCamPos, leftTopPoint_MinusPlayCamPos, rightRight);

            minusSideOfMirror = _playerCameraPosition + (Vector3.Dot(mirrorRight, pointBot - pointTop) < 0f ? pointBot : pointTop); //mirrorRight points to left side of screen
        }

        //left side of screen
        if (Vector3.Dot(leftLeft, leftTopPoint_MinusPlayCamPos) > 0f || Vector3.Dot(leftLeft, leftBotPoint_MinusPlayCamPos) > 0f)
        {

            Vector3 pointBot = FindLinePiercingPlanePoint(rightBotPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos, leftLeft);
            Vector3 pointTop = FindLinePiercingPlanePoint(rightTopPoint_MinusPlayCamPos, leftTopPoint_MinusPlayCamPos, leftLeft);

            plusSideOfMirror = _playerCameraPosition + (Vector3.Dot(mirrorRight, pointBot - pointTop) > 0f ? pointBot : pointTop); //mirrorRight points to left side of screen
        }

        //checking if mirror glass extends out of original size in left-right axis
        if (Mathf.Abs(Vector3.Dot(mirrorRight, minusSideOfMirror - mirrorPosition)) > _renderingSurfaceGlobalScaleDiv2.x)
        {
            minusSideOfMirror = mirrorPosition - _renderingSurfaceGlobalScaleDiv2.x * mirrorRight;
        }
        if (Mathf.Abs(Vector3.Dot(mirrorRight, plusSideOfMirror - mirrorPosition)) > _renderingSurfaceGlobalScaleDiv2.x)
        {
            plusSideOfMirror = mirrorPosition + _renderingSurfaceGlobalScaleDiv2.x * mirrorRight;
        }

        SetNewTransformScaleAndPositionXAxis(RenderingSurfaceTransform, minusSideOfMirror, plusSideOfMirror);



        //checking for vertical sides out of view

        minusSideOfMirror = mirrorPosition - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y;
        plusSideOfMirror = mirrorPosition + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y;


        //top side of screen
        if (Vector3.Dot(topUp, leftTopPoint_MinusPlayCamPos) > 0f || Vector3.Dot(topUp, rightTopPoint_MinusPlayCamPos) > 0f)
        {
            Vector3 pointRight = FindLinePiercingPlanePoint(rightTopPoint_MinusPlayCamPos, rightBotPoint_MinusPlayCamPos, topUp);
            Vector3 pointLeft = FindLinePiercingPlanePoint(leftTopPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos, topUp);

            plusSideOfMirror = _playerCameraPosition + (Vector3.Dot(mirrorUp, pointRight - pointLeft) > 0f ? pointRight : pointLeft);
        }


        //bot side of screen
        if (Vector3.Dot(botDown, leftBotPoint_MinusPlayCamPos) > 0f || Vector3.Dot(botDown, rightBotPoint_MinusPlayCamPos) > 0f)
        {

            Vector3 pointRight = FindLinePiercingPlanePoint(rightTopPoint_MinusPlayCamPos, rightBotPoint_MinusPlayCamPos, botDown);
            Vector3 pointLeft = FindLinePiercingPlanePoint(leftTopPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos, botDown);

            minusSideOfMirror = _playerCameraPosition + (Vector3.Dot(mirrorUp, pointRight - pointLeft) < 0f ? pointRight : pointLeft);
        }

        //checking if mirror glass extends out of original size in up-down axis
        if (Mathf.Abs(Vector3.Dot(mirrorUp, minusSideOfMirror - mirrorPosition)) > _renderingSurfaceGlobalScaleDiv2.y)
        {
            minusSideOfMirror = mirrorPosition - _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }
        if (Mathf.Abs(Vector3.Dot(mirrorUp, plusSideOfMirror - mirrorPosition)) > _renderingSurfaceGlobalScaleDiv2.y)
        {
            plusSideOfMirror = mirrorPosition + _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }

        SetNewTransformScaleAndPositionYAxis(RenderingSurfaceTransform, minusSideOfMirror, plusSideOfMirror);

        #endregion MirrorShrink



        CalculateAndSetProjectionMatrixAndRenderCamera(mirrorCameraGlobalPosition);
    }

    private static void CalculateOutWardVerticalVectorsOfViewFrustum(Camera camera, out Vector3 rightRight, out Vector3 leftLeft, out Vector3 botDown, out Vector3 topUp, out Vector3 currentCameraForward, out float near)
    {
        currentCameraForward = camera.transform.forward;
        Vector3 currentCameraRight = camera.transform.right;
        Vector3 currentCameraUp = camera.transform.up;

        Matrix4x4 currentCameraProjectionMatrix = camera.projectionMatrix;

        near = currentCameraProjectionMatrix[2, 3] / (currentCameraProjectionMatrix[2, 2] - 1f);
        float left = near * (currentCameraProjectionMatrix[0, 2] - 1f) / currentCameraProjectionMatrix[0, 0];
        float right = near * (currentCameraProjectionMatrix[0, 2] + 1f) / currentCameraProjectionMatrix[0, 0];
        float top = near * (currentCameraProjectionMatrix[1, 2] + 1f) / currentCameraProjectionMatrix[1, 1];
        float bot = near * (currentCameraProjectionMatrix[1, 2] - 1f) / currentCameraProjectionMatrix[1, 1];

        //define outward-vertical vectors of the camera view frustum's side planes
        rightRight = Quaternion.AngleAxis(90f, currentCameraUp) * (near * currentCameraForward + right * currentCameraRight).normalized;
        leftLeft = Quaternion.AngleAxis(-90f, currentCameraUp) * (near * currentCameraForward + left * currentCameraRight).normalized;
        botDown = Quaternion.AngleAxis(90f, currentCameraRight) * (near * currentCameraForward + bot * currentCameraUp).normalized;
        topUp = Quaternion.AngleAxis(-90f, currentCameraRight) * (near * currentCameraForward + top * currentCameraUp).normalized;
    }

    protected virtual Vector3 SetCameraPositionAndRotation()
    {
        Vector3 mirrorForward = RenderingSurfaceTransform.forward;

        Vector3 MirrorCameraGlobalPositionBehindRender =
            _playerCameraPosition -
            2f * (Vector3.Dot(mirrorForward, _playerCameraPosition - RenderingSurfaceTransform.position) - _renderingSurfaceGlobalScaleDiv2.z) * mirrorForward;

        MirrorCamera.transform.position = MirrorCameraGlobalPositionBehindRender;
        MirrorCamera.transform.localRotation = Quaternion.identity;
        return MirrorCameraGlobalPositionBehindRender;
    }

    private bool CheckForOutOfView(bool isMirrorOutOfView)
        =>
        StopRenderWhenNotViewed &&
        isMirrorOutOfView &&
        (
        _forwardDistanceToPlayerCamFromGlass >= 0.5f ||
        Mathf.Abs(_rightDistanceToPlayerCamFromGlass) > _renderingSurfaceGlobalScaleDiv2.x ||
        Mathf.Abs(_upDistanceToPlayerCamFromGlass) > _renderingSurfaceGlobalScaleDiv2.y
        );



    #region CameraRenderFunctions

    protected virtual void CalculateProjectionMatrixParameters(in Vector3 mirrorCameraGlobalPositionBehindView, out float left, out float right, out float top, out float bot, out float near, out float far)
    {
        Vector3 MirrorCamera_sub_MirrorGlass = mirrorCameraGlobalPositionBehindView - RenderingSurfaceTransform.position;
        float rightDist = Vector3.Dot(RenderingSurfaceTransform.right, MirrorCamera_sub_MirrorGlass);
        float upDist = Vector3.Dot(RenderingSurfaceTransform.up, MirrorCamera_sub_MirrorGlass);
        float forwardDist = Vector3.Dot(RenderingSurfaceTransform.forward, MirrorCamera_sub_MirrorGlass);
        Vector3 scaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;
        left = -scaleDiv2.x - rightDist;
        right = scaleDiv2.x - rightDist;
        top = scaleDiv2.y - upDist;
        bot = -scaleDiv2.y - upDist;
        near = scaleDiv2.z - forwardDist;
        far = 1000f;
        //far = Camera.current.farClipPlane + 2f * forwardDist;
    }

    private void SetProjectionMatrix(float l, float r, float b, float t, float n, float f)
    {
        //each Vector4 is a collumn
        MirrorCamera.projectionMatrix = new Matrix4x4(new Vector4(2f * n / (r - l), 0f, 0f, 0f),
                                                     new Vector4(0f, 2f * n / (t - b), 0f, 0f),
                                                     new Vector4((r + l) / (r - l), (t + b) / (t - b), (f + n) / (n - f), -1f),
                                                     new Vector4(0f, 0f, 2f * f * n / (n - f), 0f));

        //MirrorCamera.fieldOfView = 
        MirrorCamera.nearClipPlane = n;
        MirrorCamera.farClipPlane = f;
    }

    private void CalculateAndSetProjectionMatrix(in Vector3 mirrorCameraGlobalPositionBehindView)
    {
        CalculateProjectionMatrixParameters(mirrorCameraGlobalPositionBehindView, out var left, out var right, out var top, out var bot, out var near, out var far);
        SetProjectionMatrix(left, right, bot, top, near, far);
    }

    private void CalculateAndSetProjectionMatrixAndRenderCamera(in Vector3 mirrorCameraGlobalPositionBehindView)
    {
        CalculateAndSetProjectionMatrix(mirrorCameraGlobalPositionBehindView);

        Camera currentCamera = Camera.current;

        if (!_sizes.ContainsKey(currentCamera)) _sizes[currentCamera] = new List<MirrorGlassRenderInfo>();
        MirrorGlassRenderInfo mirrorGlassInfo = new MirrorGlassRenderInfo(RenderingSurfaceTransform);
        _sizes[currentCamera].Add(mirrorGlassInfo);
        RenderingSurfaceTransform.localPosition = _originalLocalPosition;
        RenderingSurfaceTransform.localScale = _originalLocalScale;

        //var projectionMatrix = currentCamera.projectionMatrix;
        //var camposition = currentCamera.transform.localPosition;
        //var camrotation = currentCamera.transform.localRotation;

        var texture = FindOrGenerateTexture();
        MirrorCamera.targetTexture = texture;

        //Debug.Log("Camera rendered: " + MirrorCamera.name, MirrorCamera);
        //Debug.Log("By: " + Camera.current.name, Camera.current);
        //_camerasBeingRenderedRightNow.Add(MirrorCamera);
        //Debug.Log(_renderTexturesBeingUsed.Count);
        _camerasUsed[MirrorCamera].Use();
        MirrorCamera.Render();
        _camerasUsed[MirrorCamera].UnUse();
        //_camerasBeingRenderedRightNow.Remove(MirrorCamera);

        var usageAndTime = _renderTexturesBeingUsed[texture];
        usageAndTime.Use();
        mirrorGlassInfo.SetTexture(MirrorGlassRenderer.sharedMaterial, texture, usageAndTime);


        //currentCamera.transform.localPosition = camposition;
        //currentCamera.transform.localRotation = camrotation;
        //if (currentCamera.projectionMatrix != projectionMatrix)
        //{
        //    currentCamera.projectionMatrix = projectionMatrix;
        //}
    }

    private RenderTexture FindOrGenerateTexture()
    {
        foreach(var pair in _renderTexturesBeingUsed)
        {
            if (!pair.Value) return pair.Key;
        }
        RenderTexture newTexture = new RenderTexture(MirrorCamera.targetTexture);
        UsageAndTime usageAndTime = new UsageAndTime(false);
        _renderTexturesBeingUsed.Add(newTexture, usageAndTime);
        StartCoroutine(CheckTimeForDestroyRenderTexture(newTexture, usageAndTime));
        return newTexture;
    }
    private Camera FindOrGenerateCamera()
    {
        foreach(var pair in _camerasUsed)
        {
            if (!pair.Value) return pair.Key;
        }
        GameObject cameraHolder = Instantiate(MirrorCamera.gameObject, MirrorCamera.transform.parent);
        Camera tempCamera = cameraHolder.GetComponent<Camera>();
        UsageAndTime usageAndTime = new UsageAndTime(false);
        _camerasUsed.Add(tempCamera, usageAndTime);
        StartCoroutine(CheckTimeForDestroyCamera(tempCamera, usageAndTime));
        return tempCamera;
    }

    private Camera SetNewCamera()
    {
        GameObject cameraHolder = new GameObject("temp camera");
        cameraHolder.transform.parent = MirrorCamera.transform.parent;
        Camera tempCamera = cameraHolder.AddComponent<Camera>();
        tempCamera.enabled = false;
        tempCamera.targetTexture = MirrorCamera.targetTexture;

        DesotryAtEndOfFrame(cameraHolder);

        return tempCamera;
    }

    //private Texture StorePixelsOfRenderTexture(RenderTexture renderTexture)
    //{
    //    Texture storingTexture = new RenderTexture(renderTexture);
    //    Graphics.CopyTexture(renderTexture, storingTexture);
    //    return storingTexture;
    //}

    //private void LoadPixelsOfRenderTexture(RenderTexture renderTexture, Texture TextureToLoadFrom)
    //{
    //    Graphics.CopyTexture(TextureToLoadFrom, renderTexture);
    //}

    //private static Vector3 NUKE_POSITION => new Vector3(1024f * 8f, 1024f * 8f, 1024f * 8f);
    //private Camera _cameraToNotRender;
    //private Vector3 _originalPositionOfRenderer;
    //private void DontRenderThisObjectThisFrameByThisCamera(Camera cameraToNotRender)
    //{
    //    _originalPositionOfRenderer = MirrorGlassRenderer.transform.position;
    //    MirrorGlassRenderer.transform.position = NUKE_POSITION;
    //    //MirrorGlassRenderer.enabled = false;
    //    _cameraToNotRender = cameraToNotRender;
    //    Camera.onPostRender += CameraToNotRenderOnPostRender;
    //}

    //private void CameraToNotRenderOnPostRender(Camera cameraToNotRender)
    //{
    //    if (cameraToNotRender != _cameraToNotRender) return;

    //    Debug.Log("object visible again", this);
    //    MirrorGlassRenderer.transform.position = _originalPositionOfRenderer;
    //    //MirrorGlassRenderer.enabled = true;
    //    _cameraToNotRender = null;
    //    Camera.onPostRender -= CameraToNotRenderOnPostRender;
    //}


    #endregion CameraRenderFunctions



    #region TransformResizeFunctions

    private void SetNewTransformScaleAndPositionXAxis(Transform transform, in Vector3 globalPointLeft, in Vector3 globalPointRight)
    {
        Vector3 scale = transform.localScale;
        Vector3 right = transform.right;

        scale.x = transform.parent == null ?
            Vector3.Dot(right, globalPointRight - globalPointLeft)
            :
            Vector3.Dot(right, globalPointRight - globalPointLeft) / transform.parent.lossyScale.x
            ;

        transform.localScale = scale;
        transform.position += Vector3.Dot(right, (globalPointRight + globalPointLeft) / 2f - transform.position) * right;
    }

    private void SetNewTransformScaleAndPositionYAxis(Transform transform, in Vector3 globalPointLeft, in Vector3 globalPointRight)
    {
        Vector3 scale = transform.localScale;
        Vector3 up = transform.up;

        scale.y = transform.parent == null ?
            Vector3.Dot(up, globalPointRight - globalPointLeft)
            :
            Vector3.Dot(up, globalPointRight - globalPointLeft) / transform.parent.lossyScale.y
            ;

        transform.localScale = scale;
        transform.position += Vector3.Dot(up, (globalPointRight + globalPointLeft) / 2f - transform.position) * up;

    }

    #endregion TransformResizeFunctions



    #region MathFunctions
    //corner points must be subbed with _playreCamerPosition for the conditions to work. Assuming that, the planes pass through 0
    private static bool CheckIfSquareIsOutOfViewFrustum(in Vector3 leftTopPoint, in Vector3 rightTopPoint, in Vector3 rightBotPoint, in Vector3 leftBotPoint,
                                                 in Vector3 topVerticalVector, in Vector3 rightVerticalVector, in Vector3 botVerticalVector, in Vector3 leftVerticalVector,
                                                 in Vector3 cameraForward, float near)
    =>
    ( // checking if dot product is negative because cameraForward is opposite of what needed
    Vector3.Dot(leftTopPoint - cameraForward * near, cameraForward) < 0f &&
    Vector3.Dot(rightTopPoint - cameraForward * near, cameraForward) < 0f &&
    Vector3.Dot(rightBotPoint - cameraForward * near, cameraForward) < 0f &&
    Vector3.Dot(leftBotPoint - cameraForward * near, cameraForward) < 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, topVerticalVector) > 0f &&
    Vector3.Dot(rightTopPoint, topVerticalVector) > 0f &&
    Vector3.Dot(rightBotPoint, topVerticalVector) > 0f &&
    Vector3.Dot(leftBotPoint, topVerticalVector) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, rightVerticalVector) > 0f &&
    Vector3.Dot(rightTopPoint, rightVerticalVector) > 0f &&
    Vector3.Dot(rightBotPoint, rightVerticalVector) > 0f &&
    Vector3.Dot(leftBotPoint, rightVerticalVector) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, botVerticalVector) > 0f &&
    Vector3.Dot(rightTopPoint, botVerticalVector) > 0f &&
    Vector3.Dot(rightBotPoint, botVerticalVector) > 0f &&
    Vector3.Dot(leftBotPoint, botVerticalVector) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, leftVerticalVector) > 0f &&
    Vector3.Dot(rightTopPoint, leftVerticalVector) > 0f &&
    Vector3.Dot(rightBotPoint, leftVerticalVector) > 0f &&
    Vector3.Dot(leftBotPoint, leftVerticalVector) > 0f
    );
    //doesn't really cover some cases but it will never stop rendering MirrorCamera when mirror is in view

    //this is below is more "optimised" but doesn't take into account if mirror is rotated around its local z (forward) axis
    //(
    //Vector3.Dot(leftTopPoint, botVerticalVector3) > 0f && //top points of mirror are under screen
    //Vector3.Dot(rightTopPoint, botVerticalVector3) > 0f
    //)
    //||
    //(
    //Vector3.Dot(rightTopPoint, leftVerticalVector4) > 0f && //right points of mirror are left of screen
    //Vector3.Dot(rightBotPoint, leftVerticalVector4) > 0f
    //)
    //||
    //(
    //Vector3.Dot(rightBotPoint, topVerticalVector) > 0f && //bot points of mirror are above screen
    //Vector3.Dot(leftBotPoint, topVerticalVector) > 0f
    //)
    //||
    //(
    //Vector3.Dot(leftBotPoint, rightVerticalVector2) > 0f && //left points of mirror are right of screen
    //Vector3.Dot(leftTopPoint, rightVerticalVector2) > 0f
    //);



    //points must be subbed with _playreCamerPosition. Assuming that, the plane passes through 0
    private static Vector3 FindLinePiercingPlanePoint(in Vector3 LinePoint1, in Vector3 LinePoint2, in Vector3 PlaneVerticalVector)
    {
        Vector3 LineDirection = LinePoint1 - LinePoint2;

        // the eq with xVect is: PlaneVerticalVector *dot (x - PlanePoint) = 0   <=>   PlaneVerticalVector *dot (LineDirection * t + LinePoint1 - PlanePoint) = 0
        // x point on line : x = LineDirection * t + LinePoint1, we can solve for t above and then calculate x

        float t = -Vector3.Dot(PlaneVerticalVector, LinePoint1) / Vector3.Dot(PlaneVerticalVector, LineDirection);
        return new Vector3(t * LineDirection.x + LinePoint1.x, t * LineDirection.y + LinePoint1.y, t * LineDirection.z + LinePoint1.z);
        //return LineDirection * t + LinePoint1;
    }

#endregion MathFunctions



#region ReadOnlyAttributeDeclaration

    private class ReadOnlyOnInspectorDuringPlay : PropertyAttribute { }
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyOnInspectorDuringPlay))]
    private class ReadOnlyDuringPlayDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            if (Application.isPlaying)
            {
                UnityEditor.EditorGUI.BeginDisabledGroup(true);
                UnityEditor.EditorGUI.PropertyField(position, property, label);
                UnityEditor.EditorGUI.EndDisabledGroup();
            }
            else
            {
                UnityEditor.EditorGUI.PropertyField(position, property, label);
            }
        }
    }

#endregion ReadOnlyAttributeDeclaration


    private void DesotryAtEndOfFrame(UnityEngine.Object objectToBeDestroyed)
    {
        StartCoroutine(DesotryAtEndOfFrameIterator(objectToBeDestroyed));
    }

    private IEnumerator DesotryAtEndOfFrameIterator(UnityEngine.Object objectToBeDestroyed)
    {
        yield return new WaitForEndOfFrame();
        Destroy(objectToBeDestroyed);
    }

    private IEnumerator CheckTimeForDestroyRenderTexture(RenderTexture texture, UsageAndTime usageAndTime)
    {
        if(usageAndTime.TimeRemaining == float.PositiveInfinity) yield break;
        while (true)
        {
            yield return new WaitForSecondsRealtime(usageAndTime.TimeRemaining);
            yield return new WaitForEndOfFrame();
            if (usageAndTime.CheckIfTimesUp)
            {
                _renderTexturesBeingUsed.Remove(texture);
                Destroy(texture);
                yield break;
            }
        }
    }

    private IEnumerator CheckTimeForDestroyCamera(Camera camera, UsageAndTime usageAndTime)
    {
        if(usageAndTime.TimeRemaining == float.PositiveInfinity) yield break;
        while (true)
        {
            yield return new WaitForSecondsRealtime(usageAndTime.TimeRemaining);
            yield return new WaitForEndOfFrame();
            if (usageAndTime.CheckIfTimesUp)
            {
                _camerasUsed.Remove(camera);
                Destroy(camera.gameObject);
                yield break;
            }
        }
    }

    private class UsageAndTime
    {
        private bool _used;
        private float _timeLastUsed;
        private float _timeLimit;
        public UsageAndTime(bool used, float timeLimit = 10f)
        {
            _used = used;
            _timeLastUsed = Time.time;
            _timeLimit = timeLimit;
        }

        //public static implicit operator UsageAndTime(bool x) => new UsageAndTime(x);
        public static implicit operator bool(UsageAndTime x) => x._used;

        public void Use()
        {
            _used = true;
            _timeLastUsed = Time.time;
        }
        public void UnUse()
        {
            _used = false;
        }

        public float TimeRemaining => _timeLimit - (Time.time - _timeLastUsed);
        public bool CheckIfTimesUp => Time.time - _timeLastUsed > _timeLimit;
    }

    private class MirrorGlassRenderInfo
    {
        public readonly Transform Transform;
        public Material MirrorMaterial;
        public Texture Texture;
        public UsageAndTime TextureBeingUsed;
        public Vector3 Position, Scale;

        public MirrorGlassRenderInfo(Transform transform)
        {
            Transform = transform;
            Position = transform.localPosition;
            Scale = transform.localScale;

        }

        public void SetTexture(Material mirrorMaterial, Texture texture, UsageAndTime textureHasBeenUsed)
        {
            MirrorMaterial = mirrorMaterial;
            Texture = texture;
            TextureBeingUsed = textureHasBeenUsed;
        }

        public void PreRender()
        {
            var tempVector3 = Transform.localPosition;
            Transform.localPosition = Position;
            Position = tempVector3;

            tempVector3 = Transform.localScale;
            Transform.localScale = Scale;
            Scale = tempVector3;

            if (MirrorMaterial.mainTexture != Texture)
            {
                var tempTexture = MirrorMaterial.mainTexture;
                MirrorMaterial.mainTexture = Texture;
                Texture = tempTexture;
            }
        }
        public void PostRender()
        {
            Transform.localPosition = Position;
            Transform.localScale = Scale;

            if (MirrorMaterial.mainTexture != Texture)
                MirrorMaterial.mainTexture = Texture;
            TextureBeingUsed.UnUse();
        }
    }

    private static Dictionary<Camera, List<MirrorGlassRenderInfo>> _sizes = new Dictionary<Camera, List<MirrorGlassRenderInfo>>();

    private static void SetSizesAndTextures(Camera currentCamera)
    {
        //if (!_sizes.ContainsKey(currentCamera) || _sizes[currentCamera] == null) return;
        if (!_sizes.ContainsKey(currentCamera)) return;

        foreach(var size in _sizes[currentCamera])
        {
            size.PreRender();
        }
    }
    private static void ResetSizesAndTextures(Camera currentCamera)
    {
        //if (!_sizes.ContainsKey(currentCamera) || _sizes[currentCamera] == null) return;
        if (!_sizes.ContainsKey(currentCamera)) return;

        foreach(var size in _sizes[currentCamera])
        {
            size.PostRender();
        }
        _sizes[currentCamera].Clear();
    }

    static MirrorGlass()
    {
        Camera.onPreRender += SetSizesAndTextures;
        Camera.onPostRender += ResetSizesAndTextures;
    }

}
