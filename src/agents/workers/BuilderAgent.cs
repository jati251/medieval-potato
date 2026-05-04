using Godot;
using System;

public partial class BuilderAgent : Node3D
{
	private Vector3[] _path;
	private int _pathIndex = 0;
	private float _speed = 4.0f;
	private bool _isWorking = false;
	private ResidencePlot _targetProject;
	private GlobalSimulation _sim;

	private AnimationPlayer _anim;
	private RoadManager _roadManager;
	private float _footprintTimer = 0.0f;

	public override void _Ready()
	{
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_anim = GetNode<AnimationPlayer>("Potato3D/AnimationPlayer");
		_roadManager = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
	}

	public void AssignTask(ResidencePlot project, Vector3[] path)
	{
		_targetProject = project;
		_path = path;
		_pathIndex = 0;
		_isWorking = false;
		if (_anim != null) _anim.SpeedScale = 1.0f;
	}

	public override void _Process(double delta)
	{
		if (_path != null && _pathIndex < _path.Length)
		{
			MoveAlongPath(delta);
			if (_anim != null) _anim.SpeedScale = 1.0f;

			// Register footprint
			_footprintTimer += (float)delta;
			if (_footprintTimer >= 0.5f)
			{
				_footprintTimer = 0.0f;
				if (_roadManager != null) _roadManager.RegisterFootprint(GlobalPosition);
			}
		}
		else if (_targetProject != null && !_targetProject.IsConstructed)
		{
			WorkOnProject(delta);
			if (_anim != null) _anim.SpeedScale = 3.0f; // Rapid jump while working
		}
		else
		{
			_targetProject = null;
			_isWorking = false;
			if (_anim != null) _anim.SpeedScale = 1.0f;
			FindNextTask();
		}
	}

	private void MoveAlongPath(double delta)
	{
		Vector3 target = _path[_pathIndex];
		Vector3 dir = (target - GlobalPosition).Normalized();
		float dist = GlobalPosition.DistanceTo(target);
		float step = _speed * (float)delta;

		if (dist <= step)
		{
			GlobalPosition = target;
			_pathIndex++;
		}
		else
		{
			GlobalPosition += dir * step;
			if (dir.Length() > 0)
				LookAt(GlobalPosition + dir, Vector3.Up);
		}
	}

	private void WorkOnProject(double delta)
	{
		_isWorking = true;
		// Advance construction
		if (_targetProject != null)
		{
			_targetProject.AddProgress(10.0f * (float)delta); // 10% per second
		}
	}

	private void FindNextTask()
	{
		var next = _sim.GetNextConstructionProject();
		if (next != null)
		{
			// For simplicity, teleport or just re-path
			// In a real game, they would walk back to guild first or find nearest
			var roadMgr = GetTree().Root.GetNode<RoadManager>("root/RoadManager");
			var path = roadMgr.GetRoadPath(GlobalPosition, next.GlobalPosition);
			AssignTask(next, path);
		}
	}
}
