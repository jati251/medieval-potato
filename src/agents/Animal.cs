using Godot;
using System;

public partial class Animal : Node3D
{
    [Export] public float Speed { get; set; } = 2.0f;
    [Export] public float WanderRadius { get; set; } = 15.0f;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _waitTimer = 0.0f;

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        PickNewTarget();
    }

    public override void _Process(double delta)
    {
        if (_waitTimer > 0)
        {
            _waitTimer -= (float)delta;
            return;
        }

        Vector3 dir = (_targetPosition - GlobalPosition).Normalized();
        float dist = GlobalPosition.DistanceTo(_targetPosition);

        if (dist < 0.5f)
        {
            _waitTimer = (float)GD.RandRange(2.0, 5.0);
            PickNewTarget();
        }
        else
        {
            GlobalPosition += dir * Speed * (float)delta;
            if (dir.Length() > 0)
            {
                LookAt(GlobalPosition + dir, Vector3.Up);
            }
        }
    }

    private void PickNewTarget()
    {
        Vector2 randomCircle = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized() * (float)GD.RandRange(5, WanderRadius);
        _targetPosition = _startPosition + new Vector3(randomCircle.X, 0, randomCircle.Y);
    }
}
