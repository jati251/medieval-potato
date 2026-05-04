using Godot;
using System;
using System.Collections.Generic;

public partial class ForagerHut : StaticBody3D
{
    [Export] public PackedScene ForagerScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 30.0f;

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
        _spawnTimer.WaitTime = 10.0f;
        _spawnTimer.Timeout += SpawnForager;
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

    private void SpawnForager()
    {
        if (_assignedWorkers <= 0) 
        {
            _spawnTimer.Stop();
            return;
        }

        BerryBush target = FindNearestBush();
        if (target == null) return;

        var forager = ForagerScene.Instantiate<VisualPop>();
        GetTree().CurrentScene.AddChild(forager);
        forager.GlobalPosition = GlobalPosition;

        var roadMgr = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
        if (roadMgr != null)
        {
            Vector3[] toBush = roadMgr.GetRoadPath(GlobalPosition, target.GlobalPosition);
            Vector3[] toHut = roadMgr.GetRoadPath(target.GlobalPosition, GlobalPosition);
            
            List<Vector3> fullTrip = new List<Vector3>(toBush);
            for (int i = 1; i < toHut.Length; i++)
            {
                fullTrip.Add(toHut[i]);
            }

            forager.WalkPath(fullTrip.ToArray());
            
            // Add food when they finish (simplified: add now)
            float harvested = target.Harvest(5.0f);
            _sim.Food += harvested;
        }
    }

    private BerryBush FindNearestBush()
    {
        BerryBush nearest = null;
        float minDist = SearchRadius;

        foreach (Node node in GetTree().CurrentScene.GetChildren())
        {
            if (node is BerryBush bush)
            {
                float dist = GlobalPosition.DistanceTo(bush.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = bush;
                }
            }
        }
        return nearest;
    }

    public override void _ExitTree()
    {
        if (_sim != null && _assignedWorkers > 0)
        {
            _sim.ReturnWorkers(_assignedWorkers);
        }
    }
}
