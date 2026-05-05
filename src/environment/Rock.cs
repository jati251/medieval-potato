using Godot;
using System;

public partial class Rock : Node3D
{
    [Export] public float StoneAmount { get; set; } = 20.0f;
    
    private int _mineRequired = 5;
    private int _currentMines = 0;
    private bool _isDepleted = false;
    public bool IsTargeted { get; set; } = false;

    public void Mine()
    {
        if (_isDepleted) return;

        _currentMines++;
        
        // Mining Animation: Shake
        Tween tween = CreateTween();
        Vector3 originalPos = GlobalPosition;
        float shakeAmt = 0.05f;
        
        tween.TweenProperty(this, "global_position", originalPos + Vector3.Right * shakeAmt, 0.05f);
        tween.TweenProperty(this, "global_position", originalPos + Vector3.Left * shakeAmt, 0.05f);
        tween.TweenProperty(this, "global_position", originalPos, 0.05f);

        if (_currentMines >= _mineRequired)
        {
            Deplete();
        }
    }

    private void Deplete()
    {
        _isDepleted = true;
        
        // Remove collision
        var staticBody = GetNodeOrNull<StaticBody3D>("StaticBody3D");
        if (staticBody != null) staticBody.CollisionLayer = 0;

        // Add resources
        var sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
        sim.Stone += StoneAmount;
        
        // Shrink and delete
        Tween tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.8f)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
