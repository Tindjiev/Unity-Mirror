using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MirrorCameraVerticalSinEffect : MonoBehaviour
{
    private Material _material;

    private Camera _mirrorCamera;
    private MirrorGlass _mirrorGlass;



    [SerializeField]
    private float _amplitude = 0.4f, _frequency = 6f, _phase = 0f;

    private int _amplitudeID, _frequencyID, _phaseID;
    private float _lastFrameAmbplitude, _lastFrameFrequency, _lastFramePhase;



    private void Awake()
    {
        _mirrorGlass = transform.parent.GetComponentInChildren<MirrorGlass>();
        _mirrorGlass.AddMirrorGlassPreRenderEvent(MirrorGlassPreRenderEVent);

        _mirrorCamera = GetComponent<Camera>();

        _material = new Material(Shader.Find("Custom/SinEffectVertical"));



        _amplitudeID = Shader.PropertyToID("_Amplitude");
        _frequencyID = Shader.PropertyToID("_Frequency");
        _phaseID = Shader.PropertyToID("_Phase");

        _lastFrameAmbplitude = _material.GetFloat(_amplitudeID);
        _lastFrameFrequency = _material.GetFloat(_frequencyID);
        _lastFramePhase = _material.GetFloat(_phaseID);
    }



    private void MirrorGlassPreRenderEVent(Camera camera, in Vector2 shrinkedScaleRatio, in Vector2 positionChangeRatio)
    {
        if (_mirrorCamera != camera) return;

        if (shrinkedScaleRatio.x == 0f) return;

        float realAmplitude = _amplitude * 0.1f / shrinkedScaleRatio.x;
        if (realAmplitude != _lastFrameAmbplitude)
        {
            _lastFrameAmbplitude = realAmplitude;
            _material.SetFloat(_amplitudeID, realAmplitude);
        }



        if (shrinkedScaleRatio.y == 0f) return;

        float realFrequency = _frequency * shrinkedScaleRatio.y;
        if (realFrequency != _lastFrameFrequency)
        {
            _lastFrameFrequency = realFrequency;
            _material.SetFloat(_frequencyID, realFrequency);
        }



        float realPhase = (_phase - (positionChangeRatio.y - shrinkedScaleRatio.y * 0.5f + 0.5f)) / shrinkedScaleRatio.y;
        if (realPhase != _lastFramePhase)
        {
            _lastFramePhase = realPhase;
            _material.SetFloat(_phaseID, realPhase);
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, _material);
    }

    private void OnDestroy()
    {
        _mirrorGlass.RemoveMirrorGlassPreRenderEvent(MirrorGlassPreRenderEVent);
    }
}
