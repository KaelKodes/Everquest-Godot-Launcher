using Godot;
using System;

/// <summary>
/// EverQuest-inspired segmented progress bar with a moving "lifeline" ping while work is active.
/// On error: ping stops and the fill turns red.
/// </summary>
public partial class EqStyleDownloadBar : Control
{
    private StyleBoxFlat _bubbleTrack;
    private StyleBoxFlat _bubbleFill;
    private double _value;
    private bool _downloadActive;
    private bool _errorState;
    private double _pingPhase;

    [Export] public int SegmentCount { get; set; } = 8;
    [Export] public float SegmentGap { get; set; } = 2f;
    [Export] public float CornerRadius { get; set; } = 4f;

    public double Value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp(value, 0.0, 100.0);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        _bubbleTrack = new StyleBoxFlat();
        _bubbleTrack.BorderColor = Colors.Black;
        _bubbleTrack.SetBorderWidthAll(2);
        _bubbleTrack.SetCornerRadiusAll(Mathf.Max(1, (int)CornerRadius));

        _bubbleFill = new StyleBoxFlat();
        _bubbleFill.BorderColor = Colors.Black;
        _bubbleFill.SetBorderWidthAll(2);
        _bubbleFill.SetCornerRadiusAll(Mathf.Max(1, (int)CornerRadius));

        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
        QueueRedraw();
    }

    public void BeginWork()
    {
        _errorState = false;
        _downloadActive = true;
        _pingPhase = 0;
        SetProcess(true);
        QueueRedraw();
    }

    public void EndWorkSuccess()
    {
        _downloadActive = false;
        SetProcess(false);
        QueueRedraw();
    }

    public void EndWorkFailed()
    {
        _errorState = true;
        _downloadActive = false;
        SetProcess(false);
        QueueRedraw();
    }

    public void SetDownloadActive(bool active)
    {
        _downloadActive = active && !_errorState;
        SetProcess(_downloadActive);
        QueueRedraw();
    }

    public void SetErrorState(bool error)
    {
        _errorState = error;
        if (_errorState)
        {
            _downloadActive = false;
            SetProcess(false);
        }

        QueueRedraw();
    }

    public void ResetBar()
    {
        _value = 0;
        _errorState = false;
        _downloadActive = false;
        _pingPhase = 0;
        SetProcess(false);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_downloadActive || _errorState)
            return;

        _pingPhase += delta * 0.55;
        if (_pingPhase >= 1.0)
            _pingPhase -= 1.0;

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 4f || size.Y <= 4f)
            return;

        float pad = 1f;
        var area = new Rect2(pad, pad, size.X - 2f * pad, size.Y - 2f * pad);

        int n = Math.Max(2, SegmentCount);
        float gap = SegmentGap;
        float segW = (area.Size.X - gap * (n - 1)) / n;
        float h = area.Size.Y;
        float fillEndX = area.Position.X + area.Size.X * (float)(_value / 100.0);

        Color gold = _errorState
            ? new Color(0.82f, 0.2f, 0.18f, 1f)
            : new Color(0.93f, 0.76f, 0.18f, 1f);

        Color track = new Color(0.11f, 0.11f, 0.13f, 1f);

        _bubbleTrack.BgColor = track;
        for (int i = 0; i < n; i++)
        {
            float x = area.Position.X + i * (segW + gap);
            var seg = new Rect2(x, area.Position.Y, segW, h);
            _bubbleTrack.Draw(GetCanvasItem(), seg);
        }

        _bubbleFill.BgColor = gold;
        for (int i = 0; i < n; i++)
        {
            float segLeft = area.Position.X + i * (segW + gap);
            float segRight = segLeft + segW;
            float clipLeft = Mathf.Max(segLeft, area.Position.X);
            float clipRight = Mathf.Min(segRight, fillEndX);
            if (clipRight <= clipLeft)
                continue;

            var fillRect = new Rect2(clipLeft, area.Position.Y, clipRight - clipLeft, h);
            _bubbleFill.Draw(GetCanvasItem(), fillRect);
        }

        if (_downloadActive && !_errorState)
        {
            float pingW = Mathf.Max(6f, h * 0.5f);
            float travel = area.Size.X - pingW;
            if (travel > 1f)
            {
                float pingX = area.Position.X + (float)(_pingPhase * travel);
                var pingRect = new Rect2(pingX, area.Position.Y + 1f, pingW, h - 2f);
                DrawPing(pingRect);
            }
        }
    }

    private void DrawPing(Rect2 r)
    {
        DrawRect(r, new Color(1f, 1f, 0.88f, 0.95f), true);
        DrawRect(
            new Rect2(r.Position + new Vector2(1, 1), r.Size - new Vector2(2, 2)),
            new Color(1f, 1f, 1f, 0.35f),
            false);
    }
}
