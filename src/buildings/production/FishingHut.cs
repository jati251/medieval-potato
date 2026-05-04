using Godot;
using System;
using System.Collections.Generic;

public partial class FishingHut : StaticBody3D
{
    [Export] public PackedScene BoatScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 50.0f;

    private int _assignedWorkers = 0;
    private GlobalSimulation _sim;
    private Timer _assignmentTimer;
    private Timer _spawnTimer;

    public bool IsPreview { get; set; } = false;

    public override void _Ready()
    {
        _sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
        
        if (IsPreview) return;

        _assignmentTimer = new Timer();
        _assignmentTimer.WaitTime = 5.0f;
        _assignmentTimer.Autostart = true;
        _assignmentTimer.Timeout += CheckWorkerAssignment;
        AddChild(_assignmentTimer);

        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = 15.0f;
        _spawnTimer.Timeout += SpawnBoat;
        AddChild(_spawnTimer);
    }

    private void CheckWorkerAssignment()
    {
        if (_assignedWorkers < 2)
        {
            int needed = 2 - _assignedWorkers;
            int assigned = _sim.RequestWorkers(needed);
            _assignedWorkers += assigned;
            
            if (_assignedWorkers > 0 && _spawnTimer.IsStopped())
            {
                _spawnTimer.Start();
            }
        }
    }

    private void SpawnBoat()
    {
        if (_assignedWorkers <= 0) 
        {
            _spawnTimer.Stop();
            return;
        }

        FishArea target = FindNearestFish();
        if (target == null) return;

        var boat = BoatScene.Instantiate<FishermanBoat>();
        GetTree().CurrentScene.AddChild(boat);
        
        Vector3 boatStart = GetNearestWaterPoint(GlobalPosition);
        float waterHeight = 0.2f;
        Vector3 startPos = boatStart + Vector3.Up * waterHeight;
        Vector3 targetPos = target.GlobalPosition + Vector3.Up * waterHeight;
        
        boat.GlobalPosition = startPos;

        // Calculate path along the river curve
        Vector3[] toFish = GetRiverPath(startPos, targetPos);
        Vector3[] fromFish = GetRiverPath(targetPos, startPos);
        
        List<Vector3> fullTrip = new List<Vector3>(toFish);
        fullTrip.AddRange(fromFish);

        boat.WalkPath(fullTrip.ToArray());
        boat.GlobalPosition = startPos;
        
        // Add food
        float harvested = target.Harvest(10.0f);
        _sim.Food += harvested;
    }

    private Vector3[] GetRiverPath(Vector3 start, Vector3 end)
    {
        var riverSystem = GetTree().CurrentScene.FindChild("River", true, false) as Node3D;
        if (riverSystem == null) return new Vector3[] { start, end };

        var path3D = riverSystem.GetNodeOrNull<Path3D>("Path3D");
        if (path3D == null) return new Vector3[] { start, end };

        var curve = path3D.Curve;
        float startOffset = curve.GetClosestOffset(path3D.ToLocal(start));
        float endOffset = curve.GetClosestOffset(path3D.ToLocal(end));

        List<Vector3> points = new List<Vector3>();
        int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float offset = Mathf.Lerp(startOffset, endOffset, t);
            Vector3 localPos = curve.SampleBaked(offset);
            points.Add(path3D.ToGlobal(localPos) + Vector3.Up * 0.2f);
        }

        return points.ToArray();
    }

    private FishArea FindNearestFish()
    {
        FishArea nearest = null;
        float minDist = SearchRadius;

        foreach (Node node in GetTree().CurrentScene.GetChildren())
        {
            if (node is FishArea area)
            {
                float dist = GlobalPosition.DistanceTo(area.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = area;
                }
            }
        }
        return nearest;
    }

    private Vector3 GetNearestWaterPoint(Vector3 from)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        // Search in a tight radius for water
        for (float dist = 0.5f; dist <= 4.0f; dist += 0.5f)
        {
            for (int i = 0; i < 12; i++) // Higher precision
            {
                float angle = i * (Mathf.Pi * 2 / 12);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
                Vector3 testPos = from + offset;
                
                Vector3 rayFrom = testPos + Vector3.Up * 5.0f;
                Vector3 rayTo = testPos + Vector3.Down * 10.0f;
                var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo, 2); 
                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    return (Vector3)result["position"];
                }
            }
        }
        return from; 
    }

    public override void _ExitTree()
    {
        if (_sim != null && _assignedWorkers > 0)
        {
            _sim.ReturnWorkers(_assignedWorkers);
        }
    }
}
