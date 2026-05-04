using Godot;
using System;
using System.Collections.Generic;

public partial class FishermanBoat : Node3D
{
    [Export] public float Speed { get; set; } = 4.0f;
    
    private Vector3[] _path;
    private int _pathIndex = 0;
    private bool _isWalking = false;

    public void WalkPath(Vector3[] path)
    {
        _path = path;
        _pathIndex = 0;
        _isWalking = true;
    }

    private float _bobTime = 0.0f;

    public override void _Process(double delta)
    {
        // Bobbing effect
        _bobTime += (float)delta * 2.0f;
        float bobOffset = Mathf.Sin(_bobTime) * 0.05f;
        
        if (!_isWalking || _path == null || _pathIndex >= _path.Length) 
        {
            GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y + (bobOffset * (float)delta), GlobalPosition.Z);
            return;
        }

        Vector3 target = _path[_pathIndex];
        Vector3 nextPos = GlobalPosition;
        Vector3 dir = (target - GlobalPosition).Normalized();
        float dist = GlobalPosition.DistanceTo(target);

        if (dist < 0.2f)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Length)
            {
                _isWalking = false;
                QueueFree(); 
            }
        }
        else
        {
            nextPos += dir * Speed * (float)delta;
            nextPos.Y = target.Y + bobOffset; // Keep floating height + bobbing
            GlobalPosition = nextPos;
            
            if (dir.Length() > 0)
            {
                LookAt(GlobalPosition + dir, Vector3.Up);
            }
        }
    }
}
