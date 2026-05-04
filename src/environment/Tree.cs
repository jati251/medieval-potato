using Godot;
using System;

public partial class Tree : Node3D
{
    [Export] public float WoodAmount { get; set; } = 10.0f;
    
    private bool _isBeingCut = false;
    private int _chopsRequired = 5;
    private int _currentChops = 0;
    private bool _isFallen = false;
    public bool IsTargeted { get; set; } = false;

    private WoodcutterHut _currentHut;

    public void Chop(WoodcutterHut hut)
    {
        if (_isFallen) return;
        _currentHut = hut;
        _currentChops++;
        
        // Cutting Animation: Shake
        Tween tween = CreateTween();
        Vector3 originalPos = GlobalPosition;
        float shakeAmt = 0.1f;
        
        tween.TweenProperty(this, "global_position", originalPos + Vector3.Right * shakeAmt, 0.05f);
        tween.TweenProperty(this, "global_position", originalPos + Vector3.Left * shakeAmt, 0.05f);
        tween.TweenProperty(this, "global_position", originalPos, 0.05f);

        if (_currentChops >= _chopsRequired)
        {
            Fall();
        }
    }

    private void Fall()
    {
        _isFallen = true;
        
        // Remove collision so it's not clickable anymore
        var staticBody = GetNodeOrNull<StaticBody3D>("StaticBody3D");
        if (staticBody != null) staticBody.CollisionLayer = 0;

        // Set a random fall direction
        float fallAngle = (float)GD.RandRange(0, Mathf.Pi * 2);
        Rotation = new Vector3(0, fallAngle, 0);

        // Fall Animation
        Tween tween = CreateTween();
        
        // Rotate over on local X axis
        tween.TweenProperty(this, "rotation:x", Mathf.Pi / 2.0f, 1.2f)
             .SetTrans(Tween.TransitionType.Bounce)
             .SetEase(Tween.EaseType.Out);
        
        // Add resources to the hut that chopped it
        if (_currentHut != null)
        {
            _currentHut.LocalStorage += WoodAmount;
        }
        else
        {
            var sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
            sim.Wood += WoodAmount;
        }
        
        // Delete after a while
        tween.TweenInterval(2.0f);
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.5f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
