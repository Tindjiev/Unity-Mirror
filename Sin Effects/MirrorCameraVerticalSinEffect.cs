using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MirrorCamera))]
public class MirrorCameraVerticalSinEffect : MonoBehaviour
{

    [SerializeField, ReadOnlyOnInspector]
    private Material _material;

    private int _amplitudeID, _frequencyID, _phaseID;

    [SerializeField]
    private float _amplitude = 0.4f, _frequency = 6f, _phase = 0f;

    private MirrorCamera _mirrorCamera;

    private float _lastFrameAmbplitude, _lastFrameFrequency, _lastFramePhase;



    private void Awake()
    {
        _mirrorCamera = GetComponent<MirrorCamera>();

        _material = new Material(Shader.Find("Custom/SinEffectVertical"));

        _amplitudeID = Shader.PropertyToID("_Amplitude");
        _frequencyID = Shader.PropertyToID("_Frequency");
        _phaseID = Shader.PropertyToID("_Phase");

        _lastFrameAmbplitude = _material.GetFloat(_amplitudeID);
        _lastFrameFrequency = _material.GetFloat(_frequencyID);
        _lastFramePhase = _material.GetFloat(_phaseID);
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        float xRatio = _mirrorCamera.ShrinkedScaleRatioX;
        if (xRatio == 0f) return;

        float realAmplitude = _amplitude * 0.1f / xRatio;
        if (realAmplitude != _lastFrameAmbplitude)
        {
            _lastFrameAmbplitude = realAmplitude;
            _material.SetFloat(_amplitudeID, realAmplitude);
        }



        float yRatio = _mirrorCamera.ShrinkedScaleRatioY;
        if (yRatio == 0f) return;

        float realFrequency = _frequency * yRatio;
        if (realFrequency != _lastFrameFrequency)
        {
            _lastFrameFrequency = realFrequency;
            _material.SetFloat(_frequencyID, realFrequency);
        }



        float realPhase = (_phase - (_mirrorCamera.PositionChangeRatioY - yRatio * 0.5f + 0.5f)) / yRatio;
        if (realPhase != _lastFramePhase)
        {
            _lastFramePhase = realPhase;
            _material.SetFloat(_phaseID, realPhase);
        }



        Graphics.Blit(source, destination, _material);
    }
}
