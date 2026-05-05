using Godot;
using System;

public partial class Main : Node3D
{
	[Export] public PackedScene TreeScene { get; set; }
	[Export] public PackedScene GrassScene { get; set; }
	[Export] public PackedScene RockScene { get; set; }

	public override void _Ready()
	{
		GD.Print("Hello Medieval Potato! Generating environment...");
		GenerateEnvironment();
	}

	private void GenerateEnvironment()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		// Spawn Trees
		for (int i = 0; i < 200; i++)
		{
			Vector3 pos = new Vector3(rng.RandfRange(-200, 200), 0, rng.RandfRange(-200, 200));
			if (IsPositionValid(pos))
			{
				var tree = TreeScene.Instantiate<Node3D>();
				AddChild(tree);
				tree.GlobalPosition = pos;
				tree.RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0);
				tree.Scale = Vector3.One * rng.RandfRange(0.8f, 1.3f);
			}
		}

		// Spawn Grass (More dense)
		for (int i = 0; i < 800; i++)
		{
			Vector3 pos = new Vector3(rng.RandfRange(-240, 240), 0, rng.RandfRange(-240, 240));
			if (IsPositionValid(pos))
			{
				var grass = GrassScene.Instantiate<Node3D>();
				AddChild(grass);
				grass.GlobalPosition = pos;
				grass.RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0);
				grass.Scale = Vector3.One * rng.RandfRange(0.5f, 1.2f);
			}
		}
		
		// Spawn Rocks
		for (int i = 0; i < 50; i++)
		{
			Vector3 pos = new Vector3(rng.RandfRange(-200, 200), 0, rng.RandfRange(-200, 200));
			if (IsPositionValid(pos))
			{
				var rock = RockScene.Instantiate<Node3D>();
				AddChild(rock);
				rock.GlobalPosition = pos;
				rock.RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0);
				rock.Scale = Vector3.One * rng.RandfRange(1.0f, 2.5f);
			}
		}
	}

	private bool IsPositionValid(Vector3 pos)
	{
		// Avoid spawning on river (Layer 2)
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 10, pos + Vector3.Down * 10, 2);
		var result = spaceState.IntersectRay(query);
		return result.Count == 0;
	}
}
