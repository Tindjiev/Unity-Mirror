using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MirrorCamera : MonoBehaviour
{

    #region ReferencesToOtherObjects

    [field: SerializeField]
    public Material Material { get; protected set; }

    [SerializeField]
    private Camera _playerCamera;
    public Camera PlayerCamera
    {
        get => _playerCamera != null ? _playerCamera : Camera.main;
        set => _playerCamera = value;
    }

    protected Camera _cameraSelf { get; private set; }

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



    protected void Start()
    {
        _originalLocalPosition = RenderingSurfaceTransform.localPosition;
        _originalLocalScale = RenderingSurfaceTransform.localScale;

        _cameraSelf = GetComponent<Camera>();
        if (_cameraSelf.targetTexture == null)
        {
            var newTargetTexture = new RenderTexture(XPixels, YPixels, 32);
            _cameraSelf.targetTexture = newTargetTexture;
            if (Material == null)
            {
                Material = new Material(Shader.Find("Mirror/MirrorShader"));
            }
            Material.mainTexture = newTargetTexture;
            MirrorGlassRenderer.material = Material;
        }
    }

    protected void LateUpdate()
    {

        #region PrepareVariables

        //start with original position and scale
        RenderingSurfaceTransform.localPosition = _originalLocalPosition;
        RenderingSurfaceTransform.localScale = _originalLocalScale;

        _renderingSurfaceGlobalScaleDiv2 = RenderingSurfaceTransform.lossyScale / 2f;

        Vector3 mirrorPosition = RenderingSurfaceTransform.position;
        Vector3 mirrorForward = RenderingSurfaceTransform.forward;
        Vector3 mirrorRight = RenderingSurfaceTransform.right;
        Vector3 mirrorUp = RenderingSurfaceTransform.up;

        _playerCameraPosition = PlayerCamera.transform.position;
        //Vector3 playerCameraForward = PlayerCamera.transform.forward;
        Vector3 playerCameraRight = PlayerCamera.transform.right;
        Vector3 playerCameraUp = PlayerCamera.transform.up;


        float horFOVDegrees = Mathf.Atan(1f / PlayerCamera.projectionMatrix[0, 0]) * (180f / Mathf.PI);
        float verFOVDegrees = Mathf.Atan(1f / PlayerCamera.projectionMatrix[1, 1]) * (180f / Mathf.PI);


        //define outward-vertical vectors of the camera view frustum's side planes
        Vector3 rightRight = Quaternion.AngleAxis(horFOVDegrees, playerCameraUp) * playerCameraRight;
        Vector3 leftLeft = Quaternion.AngleAxis(-horFOVDegrees, playerCameraUp) * -playerCameraRight;
        Vector3 botDown = Quaternion.AngleAxis(verFOVDegrees, playerCameraRight) * -playerCameraUp;
        Vector3 topUp = Quaternion.AngleAxis(-verFOVDegrees, playerCameraRight) * playerCameraUp;

        Vector3 playerCameraPosition_sub_CentrePointOnMirrorGlass = _playerCameraPosition - (mirrorPosition + _renderingSurfaceGlobalScaleDiv2.z * mirrorForward);
        _forwardDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorForward, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _rightDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorRight, playerCameraPosition_sub_CentrePointOnMirrorGlass);
        _upDistanceToPlayerCamFromGlass = Vector3.Dot(mirrorUp, playerCameraPosition_sub_CentrePointOnMirrorGlass);

        #endregion PrepareVariables



        if (StopRenderWhenPlayerCamIsBehind && _forwardDistanceToPlayerCamFromGlass <= -0.1f)
        {
            _cameraSelf.enabled = false;
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

        //checking if mirror is out of view frustum
        if (CheckForOutOfView(
            CheckIfSquareIsOutOfViewFrustum(leftTopPoint_MinusPlayCamPos, rightTopPoint_MinusPlayCamPos, rightBotPoint_MinusPlayCamPos, leftBotPoint_MinusPlayCamPos,
                                            topUp, rightRight, botDown, leftLeft)))
        {
            _cameraSelf.enabled = false;
            return;
        }



        if (!ShrinkWhenPlayerCamGetsClose)
        {
            CalculateAndSetProjectionMatrix(mirrorCameraGlobalPosition);
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

        //checking if mirror glass extends out of original size
        if (Vector3.Dot(mirrorRight, minusSideOfMirror - mirrorPosition) < -_renderingSurfaceGlobalScaleDiv2.x)
        {
            minusSideOfMirror = mirrorPosition - _renderingSurfaceGlobalScaleDiv2.x * mirrorRight;
        }
        if (Vector3.Dot(mirrorRight, plusSideOfMirror - mirrorPosition) > _renderingSurfaceGlobalScaleDiv2.x)
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

        //checking if mirror glass extends out of original size
        if (Vector3.Dot(mirrorUp, minusSideOfMirror - mirrorPosition) < -_renderingSurfaceGlobalScaleDiv2.y)
        {
            minusSideOfMirror = mirrorPosition - _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }
        if (Vector3.Dot(mirrorUp, plusSideOfMirror - mirrorPosition) > _renderingSurfaceGlobalScaleDiv2.y)
        {
            plusSideOfMirror = mirrorPosition + _renderingSurfaceGlobalScaleDiv2.y * mirrorUp;
        }

        SetNewTransformScaleAndPositionYAxis(RenderingSurfaceTransform, minusSideOfMirror, plusSideOfMirror);

        #endregion MirrorShrink



        CalculateAndSetProjectionMatrix(mirrorCameraGlobalPosition);
    }



    protected virtual Vector3 SetCameraPositionAndRotation()
    {
        Vector3 mirrorForward = RenderingSurfaceTransform.forward;

        Vector3 MirrorCameraGlobalPositionBehindRender =
            _playerCameraPosition -
            2f * (Vector3.Dot(mirrorForward, _playerCameraPosition - RenderingSurfaceTransform.position) - _renderingSurfaceGlobalScaleDiv2.z) * mirrorForward;

        _cameraSelf.transform.position = MirrorCameraGlobalPositionBehindRender;
        _cameraSelf.transform.localRotation = Quaternion.identity;
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



    #region ProjectionMatrixFunctions

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
        far = 100f;
    }

    private void SetProjectionMatrix(float l, float r, float b, float t, float n, float f)
    {
        //each Vector4 is a collumn
        _cameraSelf.projectionMatrix = new Matrix4x4(new Vector4(2f * n / (r - l), 0f, 0f, 0f),
                                                     new Vector4(0f, 2f * n / (t - b), 0f, 0f),
                                                     new Vector4((r + l) / (r - l), (t + b) / (t - b), (f + n) / (n - f), -1f),
                                                     new Vector4(0f, 0f, 2f * f * n / (n - f), 0f));
        _cameraSelf.nearClipPlane = n;
        _cameraSelf.farClipPlane = f;
        _cameraSelf.enabled = true;
    }

    private void CalculateAndSetProjectionMatrix(in Vector3 mirrorCameraGlobalPositionBehindView)
    {
        CalculateProjectionMatrixParameters(mirrorCameraGlobalPositionBehindView, out var left, out var right, out var top, out var bot, out var near, out var far);
        SetProjectionMatrix(left, right, bot, top, near, far);
    }

    #endregion ProjectionMatrixFunctions



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
                                                 in Vector3 topVerticalVector, in Vector3 rightVerticalVector2, in Vector3 botVerticalVector3, in Vector3 leftVerticalVector4)
    =>
    (
    Vector3.Dot(leftTopPoint, topVerticalVector) > 0f &&
    Vector3.Dot(rightTopPoint, topVerticalVector) > 0f &&
    Vector3.Dot(rightBotPoint, topVerticalVector) > 0f &&
    Vector3.Dot(leftBotPoint, topVerticalVector) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, rightVerticalVector2) > 0f &&
    Vector3.Dot(rightTopPoint, rightVerticalVector2) > 0f &&
    Vector3.Dot(rightBotPoint, rightVerticalVector2) > 0f &&
    Vector3.Dot(leftBotPoint, rightVerticalVector2) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, botVerticalVector3) > 0f &&
    Vector3.Dot(rightTopPoint, botVerticalVector3) > 0f &&
    Vector3.Dot(rightBotPoint, botVerticalVector3) > 0f &&
    Vector3.Dot(leftBotPoint, botVerticalVector3) > 0f
    )
    ||
    (
    Vector3.Dot(leftTopPoint, leftVerticalVector4) > 0f &&
    Vector3.Dot(rightTopPoint, leftVerticalVector4) > 0f &&
    Vector3.Dot(rightBotPoint, leftVerticalVector4) > 0f &&
    Vector3.Dot(leftBotPoint, leftVerticalVector4) > 0f
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



}
