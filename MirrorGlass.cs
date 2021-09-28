using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MirrorGlass : MonoBehaviour
{

    #region References to other Objects

    [field: SerializeField]
    public Camera MirrorCamera { get; private set; }

    [field: SerializeField]
    public Transform RenderingSurfaceTransform { get; private set; }

    [field: SerializeField]
    public MeshRenderer MirrorGlassRenderer { get; private set; }

    [SerializeField]
    protected Shader _mirrorShader;
    [SerializeField]
    protected Material _noReflectionMaterial;
    [SerializeField]
    protected Shader _noReflectionShader;

    #endregion References to other Objects



    #region Control variables

    [field: SerializeField, ReadOnlyOnInspectorDuringPlay]
    public int XPixels { get; private set; } = 1920;

    [field: SerializeField, ReadOnlyOnInspectorDuringPlay]
    public int YPixels { get; private set; } = 1080;

    public bool StopRenderWhenNotViewed = true;
    public bool StopRenderWhenPlayerCamIsBehind = true;
    public bool ShrinkWhenPlayerCamGetsClose = true;

    public bool CustomCameraFarDistance = false;

    [SerializeField, HideInInspector]
    public float CameraFarDistance = 100f;

    #endregion Control variables



    #region Variables that store info

    protected Vector3 _originalLocalPosition { get; private set; }
    protected Vector3 _originalLocalScale { get; private set; }

    protected Vector3 _currentCameraPosition { get; private set; }
    protected Vector3 _renderingSurfaceGlobalScaleDiv2 { get; private set; }
    protected float _forwardDistanceToPlayerCamFromGlassCentre { get; private set; }
    protected float _upDistanceToPlayerCamFromGlassCentre { get; private set; }
    protected float _rightDistanceToPlayerCamFromGlassCentre { get; private set; }


    #endregion Variables that store info



    #region PreRender Event Function Delegate declaration

    public delegate void FuncCameraAnd2Vectors(Camera camera, in Vector2 shrinkedScaleRatio, in Vector2 positionChangeRatio);
    protected FuncCameraAnd2Vectors _preRender;

    #endregion PreRender Event Function Delegate declaration



    #region Dictionaries of UsageAndTime

    protected Dictionary<RenderTexture, UsageAndTime> _renderTexturesBeingUsed = new Dictionary<RenderTexture, UsageAndTime>();
    protected Dictionary<Camera, UsageAndTime> _mirrorCameras = new Dictionary<Camera, UsageAndTime>();

    #endregion Dictionaries of UsageAndTime



    #region Static Variables

    private static int _timeRecursed = 0;
    public static int MaximumRecursions = 7;

    #endregion Static Variables



    // skip rendering caused by own MirrorCameras
    protected virtual bool _skipRender => _mirrorCameras.ContainsKey(Camera.current);



    static MirrorGlass()
    {
        Camera.onPreRender += SetFromMirrorGlassInfo;
        Camera.onPostRender += ResetAndClearMirrorGlassInfo;
    }

    protected void Awake()
    {
        if (!_noReflectionMaterial) _noReflectionMaterial = new Material(_noReflectionShader ? _noReflectionShader : Shader.Find("Mirror/Black"));

        _originalLocalPosition = RenderingSurfaceTransform.localPosition;
        _originalLocalScale = RenderingSurfaceTransform.localScale;

        if (!MirrorGlassRenderer.sharedMaterial) MirrorGlassRenderer.sharedMaterial = new Material(_mirrorShader ? _mirrorShader : Shader.Find("Mirror/MirrorShader"));
        if (!MirrorCamera.targetTexture) MirrorCamera.targetTexture = new RenderTexture(XPixels, YPixels, 32);
        MirrorGlassRenderer.material.mainTexture = MirrorCamera.targetTexture;

        _renderTexturesBeingUsed.Add(MirrorCamera.targetTexture, new UsageAndTime(false, float.PositiveInfinity));
        _mirrorCameras.Add(MirrorCamera, new UsageAndTime(false, float.PositiveInfinity));
    }



    protected void OnWillRenderObject()
    {
#if UNITY_EDITOR
        if (Camera.current.name == "SceneCamera" || Camera.current.name == "Preview Camera") return;
#endif

        if (_skipRender) return; 

        if (_timeRecursed > MaximumRecursions)
        {
            Graphics.Blit(MirrorCamera.targetTexture, MirrorCamera.targetTexture, _noReflectionMaterial);
            return;
        }

        _timeRecursed++;


        Camera originalMirrorCamera = MirrorCamera;
        UsageAndTime mirrorCameraIsUsed = _mirrorCameras[MirrorCamera];

        if (mirrorCameraIsUsed) MirrorCamera = FindOrGenerateCamera();

        SetAndRenderMirrorCamera();

        MirrorCamera = originalMirrorCamera;


        _timeRecursed--;
    }



    private void SetAndRenderMirrorCamera()
    {

        #region Prepare variables

        _renderingSurfaceGlobalScaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        Vector3 mirrorGlassPosition = RenderingSurfaceTransform.position;
        Vector3 mirrorRight = RenderingSurfaceTransform.right;
        Vector3 mirrorUp = RenderingSurfaceTransform.up;

        _currentCameraPosition = Camera.current.transform.position;

        //assuming that mirrorGlassPosition is in the bot of the mirrorGlass surface
        Vector3 playerCameraPosition_sub_CentrePointOnMirrorGlass = _currentCameraPosition - mirrorGlassPosition;

        _forwardDistanceToPlayerCamFromGlassCentre = Vector3.Dot(RenderingSurfaceTransform.forward, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _rightDistanceToPlayerCamFromGlassCentre = Vector3.Dot(mirrorRight, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _upDistanceToPlayerCamFromGlassCentre = Vector3.Dot(mirrorUp, playerCameraPosition_sub_CentrePointOnMirrorGlass);

        #endregion Prepare variables



        if (StopRenderWhenPlayerCamIsBehind && _forwardDistanceToPlayerCamFromGlassCentre <= -0.1f) return;



        Vector3 mirrorCameraGlobalPosition = SetCameraPositionAndRotation();



        #region Calculate cornern points of MirrorGlass

        //left from the prespective of player (so its to the mirror's right)
        Vector3 leftTopPoint_MinusCurrentCamPos = mirrorGlassPosition
                                                 + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _currentCameraPosition;


        Vector3 leftBotPoint_MinusCurrentCamPos = mirrorGlassPosition
                                                 + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _currentCameraPosition;


        Vector3 rightTopPoint_MinusCurrentCamPos = mirrorGlassPosition
                                                 - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _currentCameraPosition;


        Vector3 rightBotPoint_MinusCurrentCamPos = mirrorGlassPosition
                                                 - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x
                                                 - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y
                                                 - _currentCameraPosition;

        #endregion Calculate cornern points of MirrorGlass



        //todo in the future: swap corner points so they better match camera and mirror orientation around its local z (forward) axis



        CalculateOutWardVerticalVectorsOfViewFrustum(Camera.current, out Vector3 rightRight, out Vector3 leftLeft, out Vector3 botDown, out Vector3 topUp, out Vector3 cameraForward, out float near);

        //checking if mirror is out of view frustum
        if (FullCheckForOutOfView(
            CheckIfSquareIsOutOfFrustum(leftTopPoint_MinusCurrentCamPos, rightTopPoint_MinusCurrentCamPos, rightBotPoint_MinusCurrentCamPos, leftBotPoint_MinusCurrentCamPos,
                                            topUp, rightRight, botDown, leftLeft, cameraForward, near)))
            return;



        if (!ShrinkWhenPlayerCamGetsClose)
        {
            RenderMirrorCamera(mirrorCameraGlobalPosition);
            return;
        }



        #region Mirror shrink

        //now that thw mirror is not out of view, it will be shrinked in case part of it is out of view frustum

        //checking for horizontal sides out of view

        Vector3 minusSideOfMirror = mirrorGlassPosition - mirrorRight * _renderingSurfaceGlobalScaleDiv2.x;
        Vector3 plusSideOfMirror = mirrorGlassPosition + mirrorRight * _renderingSurfaceGlobalScaleDiv2.x;


        //right side of screen
        if (Vector3.Dot(rightRight, rightTopPoint_MinusCurrentCamPos) > 0f || Vector3.Dot(rightRight, rightBotPoint_MinusCurrentCamPos) > 0f)
        {

            Vector3 pointBot = FindLinePiercingPlanePoint(rightBotPoint_MinusCurrentCamPos, leftBotPoint_MinusCurrentCamPos, rightRight);
            Vector3 pointTop = FindLinePiercingPlanePoint(rightTopPoint_MinusCurrentCamPos, leftTopPoint_MinusCurrentCamPos, rightRight);

            minusSideOfMirror = _currentCameraPosition + (Vector3.Dot(mirrorRight, pointBot - pointTop) < 0f ? pointBot : pointTop); //mirrorRight points to left side of screen
        }

        //left side of screen
        if (Vector3.Dot(leftLeft, leftTopPoint_MinusCurrentCamPos) > 0f || Vector3.Dot(leftLeft, leftBotPoint_MinusCurrentCamPos) > 0f)
        {

            Vector3 pointBot = FindLinePiercingPlanePoint(rightBotPoint_MinusCurrentCamPos, leftBotPoint_MinusCurrentCamPos, leftLeft);
            Vector3 pointTop = FindLinePiercingPlanePoint(rightTopPoint_MinusCurrentCamPos, leftTopPoint_MinusCurrentCamPos, leftLeft);

            plusSideOfMirror = _currentCameraPosition + (Vector3.Dot(mirrorRight, pointBot - pointTop) > 0f ? pointBot : pointTop); //mirrorRight points to left side of screen
        }

        //checking if mirror glass extends out of original size in left-right axis
        if (Mathf.Abs(Vector3.Dot(mirrorRight, minusSideOfMirror - mirrorGlassPosition)) > _renderingSurfaceGlobalScaleDiv2.x)
        {
            minusSideOfMirror = mirrorGlassPosition - _renderingSurfaceGlobalScaleDiv2.x * mirrorRight;
        }
        if (Mathf.Abs(Vector3.Dot(mirrorRight, plusSideOfMirror - mirrorGlassPosition)) > _renderingSurfaceGlobalScaleDiv2.x)
        {
            plusSideOfMirror = mirrorGlassPosition + _renderingSurfaceGlobalScaleDiv2.x * mirrorRight;
        }

        SetNewTransformScaleAndPositionXAxis(RenderingSurfaceTransform, minusSideOfMirror, plusSideOfMirror);



        //checking for vertical sides out of view

        minusSideOfMirror = mirrorGlassPosition - mirrorUp * _renderingSurfaceGlobalScaleDiv2.y;
        plusSideOfMirror = mirrorGlassPosition + mirrorUp * _renderingSurfaceGlobalScaleDiv2.y;


        //top side of screen
        if (Vector3.Dot(topUp, leftTopPoint_MinusCurrentCamPos) > 0f || Vector3.Dot(topUp, rightTopPoint_MinusCurrentCamPos) > 0f)
        {
            Vector3 pointRight = FindLinePiercingPlanePoint(rightTopPoint_MinusCurrentCamPos, rightBotPoint_MinusCurrentCamPos, topUp);
            Vector3 pointLeft = FindLinePiercingPlanePoint(leftTopPoint_MinusCurrentCamPos, leftBotPoint_MinusCurrentCamPos, topUp);

            plusSideOfMirror = _currentCameraPosition + (Vector3.Dot(mirrorUp, pointRight - pointLeft) > 0f ? pointRight : pointLeft);
        }


        //bot side of screen
        if (Vector3.Dot(botDown, leftBotPoint_MinusCurrentCamPos) > 0f || Vector3.Dot(botDown, rightBotPoint_MinusCurrentCamPos) > 0f)
        {

            Vector3 pointRight = FindLinePiercingPlanePoint(rightTopPoint_MinusCurrentCamPos, rightBotPoint_MinusCurrentCamPos, botDown);
            Vector3 pointLeft = FindLinePiercingPlanePoint(leftTopPoint_MinusCurrentCamPos, leftBotPoint_MinusCurrentCamPos, botDown);

            minusSideOfMirror = _currentCameraPosition + (Vector3.Dot(mirrorUp, pointRight - pointLeft) < 0f ? pointRight : pointLeft);
        }

        //checking if mirror glass extends out of original size in up-down axis
        if (Mathf.Abs(Vector3.Dot(mirrorUp, minusSideOfMirror - mirrorGlassPosition)) > _renderingSurfaceGlobalScaleDiv2.y)
        {
            minusSideOfMirror = mirrorGlassPosition - _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }
        if (Mathf.Abs(Vector3.Dot(mirrorUp, plusSideOfMirror - mirrorGlassPosition)) > _renderingSurfaceGlobalScaleDiv2.y)
        {
            plusSideOfMirror = mirrorGlassPosition + _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }

        SetNewTransformScaleAndPositionYAxis(RenderingSurfaceTransform, minusSideOfMirror, plusSideOfMirror);

        #endregion Mirror shrink



        RenderMirrorCamera(mirrorCameraGlobalPosition);
    }



    #region Prepare variables functions

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
            _currentCameraPosition -
            2f * Vector3.Dot(mirrorForward, _currentCameraPosition - RenderingSurfaceTransform.position) * mirrorForward;

        MirrorCamera.transform.position = MirrorCameraGlobalPositionBehindRender;
        MirrorCamera.transform.localRotation = Quaternion.identity;
        return MirrorCameraGlobalPositionBehindRender;
    }

    #endregion Prepare variables functions



    #region Render functions

    private Camera FindOrGenerateCamera()
    {
        foreach (var pair in _mirrorCameras)
        {
            if (!pair.Value) return pair.Key;
        }
        GameObject cameraHolder = Instantiate(MirrorCamera.gameObject, MirrorCamera.transform.parent);
        Camera tempCamera = cameraHolder.GetComponent<Camera>();
        UsageAndTime usageAndTime = new UsageAndTime(false);
        _mirrorCameras.Add(tempCamera, usageAndTime);
        StartCoroutine(CheckTimeForDestroyCamera(tempCamera, usageAndTime));
        return tempCamera;
    }

    private RenderTexture FindOrGenerateTexture(out UsageAndTime usageAndTime)
    {
        foreach (var pair in _renderTexturesBeingUsed)
        {
            if (!pair.Value)
            {
                usageAndTime = pair.Value;
                return pair.Key;
            }
        }
        RenderTexture newTexture = new RenderTexture(MirrorCamera.targetTexture);
        usageAndTime = new UsageAndTime(false);
        _renderTexturesBeingUsed.Add(newTexture, usageAndTime);
        StartCoroutine(CheckTimeForDestroyRenderTexture(newTexture, usageAndTime));
        return newTexture;
    }

    private void RenderMirrorCamera(in Vector3 mirrorCameraGlobalPositionBehindView)
    {
        CalculateAndSetProjectionMatrix(mirrorCameraGlobalPositionBehindView);

        Camera currentCamera = Camera.current;

        if (!_mirrorGlassRenderInfosPerCamera.ContainsKey(currentCamera)) _mirrorGlassRenderInfosPerCamera[currentCamera] = new List<MirrorGlassRenderInfo>();
        MirrorGlassRenderInfo mirrorGlassInfo = new MirrorGlassRenderInfo(RenderingSurfaceTransform);
        _mirrorGlassRenderInfosPerCamera[currentCamera].Add(mirrorGlassInfo);

        RenderTexture texture = FindOrGenerateTexture(out UsageAndTime usageAndTimeOfRenderTexture);
        if (MirrorCamera.targetTexture != texture) MirrorCamera.targetTexture = texture;

        InvokePreRenderEvents(MirrorCamera);

        RenderingSurfaceTransform.localPosition = _originalLocalPosition;
        RenderingSurfaceTransform.localScale = _originalLocalScale;


        UsageAndTime usageAndTimeOfCamera = _mirrorCameras[MirrorCamera];
        usageAndTimeOfCamera.Use();
        MirrorCamera.Render();
        usageAndTimeOfCamera.UnUse();

        usageAndTimeOfRenderTexture.Use();
        mirrorGlassInfo.SetTexture(MirrorGlassRenderer.material, texture, usageAndTimeOfRenderTexture);
    }

    private void CalculateAndSetProjectionMatrix(in Vector3 mirrorCameraGlobalPositionBehindView)
    {
        CalculateProjectionMatrixParameters(mirrorCameraGlobalPositionBehindView, out var left, out var right, out var top, out var bot, out var near, out var far);
        SetProjectionMatrix(left, right, bot, top, near, far);
    }

    protected virtual void CalculateProjectionMatrixParameters(in Vector3 mirrorCameraGlobalPositionBehindView, out float left, out float right, out float top, out float bot, out float near, out float far)
    {
        Vector3 mirrorCamera_sub_MirrorGlass = mirrorCameraGlobalPositionBehindView - RenderingSurfaceTransform.position;
        float rightDist = Vector3.Dot(RenderingSurfaceTransform.right, mirrorCamera_sub_MirrorGlass);
        float upDist = Vector3.Dot(RenderingSurfaceTransform.up, mirrorCamera_sub_MirrorGlass);
        float forwardDist = Vector3.Dot(RenderingSurfaceTransform.forward, mirrorCamera_sub_MirrorGlass);
        Vector3 scaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        left = -scaleDiv2.x - rightDist;
        right = scaleDiv2.x - rightDist;
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
            far = currentCameraProjectionMatrix[2, 3] / (currentCameraProjectionMatrix[2, 2] + 1f) - 2f * forwardDist;
        }
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


    #endregion Render functions



    #region Transform resize functions

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

    #endregion Transform resize functions



    private bool FullCheckForOutOfView(bool isMirrorOutOfView)
        =>
        StopRenderWhenNotViewed &&
        isMirrorOutOfView &&
        (
        _forwardDistanceToPlayerCamFromGlassCentre >= 0.5f ||
        Mathf.Abs(_rightDistanceToPlayerCamFromGlassCentre) > _renderingSurfaceGlobalScaleDiv2.x ||
        Mathf.Abs(_upDistanceToPlayerCamFromGlassCentre) > _renderingSurfaceGlobalScaleDiv2.y
        );

    #region Math functions

    //corner points must be subbed with _playreCamerPosition for the conditions to work. Assuming that, the planes pass through 0
    private static bool CheckIfSquareIsOutOfFrustum(in Vector3 leftTopPoint, in Vector3 rightTopPoint, in Vector3 rightBotPoint, in Vector3 leftBotPoint,
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

#endregion Math functions



    #region UsageAndTimeRelated

    protected class UsageAndTime
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

    protected IEnumerator CheckTimeForDestroyRenderTexture(RenderTexture texture, UsageAndTime usageAndTime)
    {
        if(usageAndTime.TimeRemaining == float.PositiveInfinity) yield break;
        while (true)
        {
            yield return new WaitForSecondsRealtime(usageAndTime.TimeRemaining);
            yield return new WaitForEndOfFrame();
            if (usageAndTime.CheckIfTimesUp) //in case it has been postponed
            {
                _renderTexturesBeingUsed.Remove(texture);
                Destroy(texture);
                yield break;
            }
        }
    }

    protected IEnumerator CheckTimeForDestroyCamera(Camera camera, UsageAndTime usageAndTime)
    {
        if (usageAndTime.TimeRemaining == float.PositiveInfinity) yield break; 
        while (true)
        {
            yield return new WaitForSecondsRealtime(usageAndTime.TimeRemaining);
            yield return new WaitForEndOfFrame();
            if (usageAndTime.CheckIfTimesUp) //in case it has been postponed
            {
                _mirrorCameras.Remove(camera);
                _mirrorGlassRenderInfosPerCamera.Remove(camera);
                Destroy(camera.gameObject);
                yield break;
            }
        }
    }

    #endregion UsageAndTimeRelated



    #region PreRender Event related functions

    public void AddMirrorGlassPreRenderEvent(FuncCameraAnd2Vectors function) => _preRender += function;
    public void RemoveMirrorGlassPreRenderEvent(FuncCameraAnd2Vectors function) => _preRender -= function;

    private void InvokePreRenderEvents(Camera camera)
    {
        Vector2 shrinkedScaleRatio =
            new Vector2(RenderingSurfaceTransform.localScale.x / _originalLocalScale.x, RenderingSurfaceTransform.localScale.y / _originalLocalScale.y);
        Vector2 positionChangeRatio =
            new Vector2((RenderingSurfaceTransform.localPosition.x - _originalLocalPosition.x) / _originalLocalScale.x, (RenderingSurfaceTransform.localPosition.y - _originalLocalPosition.y) / _originalLocalScale.y);

        _preRender?.Invoke(camera, shrinkedScaleRatio, positionChangeRatio);
    }

    #endregion PreRender Event related functions



    #region Rendering cameras PreRender and PostRender with MirrorGlassInfo

    protected class MirrorGlassRenderInfo
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

        public void UseInfo()
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
        public void ResetInfo()
        {
            Transform.localPosition = Position;
            Transform.localScale = Scale;

            if (MirrorMaterial.mainTexture != Texture) MirrorMaterial.mainTexture = Texture;
            TextureBeingUsed.UnUse();
        }
    }

    protected static Dictionary<Camera, List<MirrorGlassRenderInfo>> _mirrorGlassRenderInfosPerCamera = new Dictionary<Camera, List<MirrorGlassRenderInfo>>();



    protected static void SetFromMirrorGlassInfo(Camera currentCamera)
    {
        if (!_mirrorGlassRenderInfosPerCamera.ContainsKey(currentCamera)) return;

        foreach(var mirrorGlassRenderInfo in _mirrorGlassRenderInfosPerCamera[currentCamera])
        {
            mirrorGlassRenderInfo.UseInfo();
        }
    }

    protected static void ResetAndClearMirrorGlassInfo(Camera currentCamera)
    {
        if (!_mirrorGlassRenderInfosPerCamera.ContainsKey(currentCamera)) return;

        foreach(var mirrorGlassRenderInfo in _mirrorGlassRenderInfosPerCamera[currentCamera])
        {
            mirrorGlassRenderInfo.ResetInfo();
        }
        _mirrorGlassRenderInfosPerCamera[currentCamera].Clear();
    }

    #endregion Rendering cameras PreRender and PostRender with MirrorGlassInfo



#if UNITY_EDITOR
    #region Inspector Editor

    [UnityEditor.CustomEditor(typeof(MirrorGlass), true), UnityEditor.CanEditMultipleObjects]
    private class MirrorGlassEditor : UnityEditor.Editor
    {
        private UnityEditor.SerializedProperty CameraFarDistanceProperty;
        private MirrorGlass thisMirrorGlass;

        protected void OnEnable()
        {
            CameraFarDistanceProperty = serializedObject.FindProperty(nameof(CameraFarDistance));
            if (targets.Length == 1) thisMirrorGlass = (MirrorGlass)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (targets.Length > 1 || thisMirrorGlass.CustomCameraFarDistance)
            {
                serializedObject.Update();
                UnityEditor.EditorGUILayout.PropertyField(CameraFarDistanceProperty);
                serializedObject.ApplyModifiedProperties();
            }

            if (!Application.isPlaying)
            {
                UnityEditor.EditorGUI.BeginDisabledGroup(true);
                MaximumRecursions = UnityEditor.EditorGUILayout.IntField("Maximum recursions of reflection (Can only be edited at run time, otherwise through script)", MaximumRecursions);
                UnityEditor.EditorGUI.EndDisabledGroup();
            }
            else
            {
                MaximumRecursions = UnityEditor.EditorGUILayout.IntField("Maximum recursions of reflection", MaximumRecursions);
            }
        }
    }

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

    #endregion Inspector Editor
#endif


}
