using Godot;
using System;

public partial class Animal : Node3D
{
    [Export] public float Speed { get; set; } = 2.0f;
    [Export] public float WanderRadius { get; set; } = 20.0f;
    
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _waitTimer = 0.0f;
    public bool IsTargeted { get; set; } = false;

    public void Hunt()
    {
        // Add food to sim
        var sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
        sim.Food += 15.0f;
        
        // Death animation: shrink and disappear
        Tween tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.5f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        
        // Safety check: if spawned on water, move to land immediately
        if (IsOnWater(GlobalPosition))
        {
            PickNewTarget();
            GlobalPosition = _targetPosition;
            _startPosition = GlobalPosition;
        }
        
        PickNewTarget();
    }

    public override void _Process(double delta)
    {
        if (_waitTimer > 0)
        {
            _waitTimer -= (float)delta;
            return;
        }

        if (GlobalPosition.DistanceTo(_targetPosition) < 0.5f || IsOnWater(GlobalPosition))
        {
            _waitTimer = (float)GD.RandRange(2.0, 5.0);
            PickNewTarget();
        }
        else
        {
            Vector3 dir = (_targetPosition - GlobalPosition).Normalized();
            GlobalPosition += dir * Speed * (float)delta;
            if (dir.Length() > 0)
            {
                LookAt(GlobalPosition + dir, Vector3.Up);
            }
        }
    }

    private void PickNewTarget()
    {
        for (int i = 0; i < 20; i++) 
        {
            Vector2 randomCircle = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized() * (float)GD.RandRange(5, WanderRadius);
            Vector3 testPos = _startPosition + new Vector3(randomCircle.X, 0, randomCircle.Y);
            
            if (!IsOnWater(testPos))
            {
                _targetPosition = testPos;
                return;
            }
        }
        _targetPosition = _startPosition;
    }

    private bool IsOnWater(Vector3 pos)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 from = pos + Vector3.Up * 5.0f;
        Vector3 to = pos + Vector3.Down * 5.0f;
        var query = PhysicsRayQueryParameters3D.Create(from, to, 2); // Layer 2 is water
        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }
}
