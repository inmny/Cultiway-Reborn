using System;
using Cultiway.Core.SkillLibV3;
using UnityEngine;

namespace Cultiway.UI.Prefab;

/// <summary>
/// 法术 UI 共用的三阶段动画预览状态机。
/// </summary>
internal sealed class SkillAnimationPreviewPlayer
{
    private SkillEntityAnimation _animation;
    private SkillEntityAnimationClip _clip;
    private PreviewPhase _phase;
    private float _baseFrameInterval = 0.1f;
    private float _frameTimer;
    private int _frameIndex;

    public Sprite CurrentSprite => _clip == null ? null : _clip.Frames[_frameIndex];

    public void Configure(SkillEntityAnimation animation, float baseFrameInterval)
    {
        _animation = animation;
        _baseFrameInterval = Mathf.Max(0.01f, baseFrameInterval);
        _frameTimer = 0f;
        _frameIndex = 0;

        if (animation == null)
        {
            _clip = null;
            return;
        }

        if (animation.HasAppearance)
        {
            SetPhase(PreviewPhase.Appearance);
        }
        else
        {
            SetPhase(PreviewPhase.Runtime);
        }
    }

    public void Clear()
    {
        _animation = null;
        _clip = null;
        _frameTimer = 0f;
        _frameIndex = 0;
    }

    public bool Advance(float deltaTime)
    {
        if (_clip == null || deltaTime <= 0f) return false;

        bool changed = false;
        _frameTimer += deltaTime;
        float frameInterval = ResolveFrameInterval();
        while (_frameTimer >= frameInterval)
        {
            _frameTimer -= frameInterval;
            changed = true;
            if (_frameIndex < _clip.Frames.Length - 1)
            {
                _frameIndex++;
                continue;
            }

            AdvancePhase();
            frameInterval = ResolveFrameInterval();
        }

        return changed;
    }

    private void AdvancePhase()
    {
        switch (_phase)
        {
            case PreviewPhase.Appearance:
                SetPhase(PreviewPhase.Runtime);
                break;
            case PreviewPhase.Runtime:
                if (_animation.HasDissipation)
                {
                    SetPhase(PreviewPhase.Dissipation);
                }
                else if (_animation.HasAppearance)
                {
                    SetPhase(PreviewPhase.Appearance);
                }
                else
                {
                    _frameIndex = 0;
                }
                break;
            case PreviewPhase.Dissipation:
                SetPhase(_animation.HasAppearance ? PreviewPhase.Appearance : PreviewPhase.Runtime);
                break;
        }
    }

    private void SetPhase(PreviewPhase phase)
    {
        _phase = phase;
        _clip = phase switch
        {
            PreviewPhase.Appearance => _animation.Appearance,
            PreviewPhase.Runtime => _animation.Runtime,
            PreviewPhase.Dissipation => _animation.Dissipation,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };
        _frameIndex = 0;
    }

    private float ResolveFrameInterval()
    {
        return _clip.Settings.ResolveFrameInterval(_baseFrameInterval);
    }

    private enum PreviewPhase : byte
    {
        Appearance,
        Runtime,
        Dissipation,
    }
}
