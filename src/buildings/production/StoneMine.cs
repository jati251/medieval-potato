using Godot;
using System;
using System.Collections.Generic;

public partial class StoneMine : StaticBody3D
{
    [Export] public PackedScene MinerScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 40.0f;

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
        _spawnTimer.WaitTime = 15.0f; // Mining takes a bit longer
        _spawnTimer.Timeout += SpawnMiner;
        AddChild(_spawnTimer);
    }

    private void CheckWorkerAssignment()
    {
        if (_assignedWorkers < 2) // Stone mines can take 2 workers
        {
            int assigned = _sim.RequestWorkers(2 - _assignedWorkers);
            _assignedWorkers += assigned;
            
            if (_assignedWorkers > 0 && _spawnTimer.IsStopped())
            {
                _spawnTimer.Start();
            }
        }
    }

    private void SpawnMiner()
    {
        if (_assignedWorkers <= 0) 
        {
            _spawnTimer.Stop();
            return;
        }

        Rock target = FindNearestRock();
        if (target == null) return;

        var miner = MinerScene.Instantiate<VisualPop>();
        GetTree().CurrentScene.AddChild(miner);
        miner.GlobalPosition = GlobalPosition;

        var roadMgr = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
        if (roadMgr != null)
        {
            Vector3[] toRock = roadMgr.GetRoadPath(GlobalPosition, target.GlobalPosition);
            Vector3[] toMine = roadMgr.GetRoadPath(target.GlobalPosition, GlobalPosition);
            
            List<Vector3> fullTrip = new List<Vector3>(toRock);
            for (int i = 1; i < toMine.Length; i++)
            {
                fullTrip.Add(toMine[i]);
            }

            miner.WalkPath(fullTrip.ToArray());
            
            if (target is Rock rock) rock.IsTargeted = true;

            target.Mine();
        }
    }

    private Rock FindNearestRock()
    {
        Rock nearest = null;
        float minDist = SearchRadius;

        foreach (Node node in GetTree().CurrentScene.GetChildren())
        {
            if (node is Rock rock && !rock.IsTargeted)
            {
                float dist = GlobalPosition.DistanceTo(rock.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = rock;
                }
            }
            // Check children (for clusters)
            foreach (Node subChild in node.GetChildren())
            {
                if (subChild is Rock subRock && !subRock.IsTargeted)
                {
                    float dist = GlobalPosition.DistanceTo(subRock.GlobalPosition);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = subRock;
                    }
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
