using Godot;
using System;
using System.Collections.Generic;

public partial class GlobalSimulation : Node
{
	public static string PendingLoadFile = "";

	// --- Resources ---
	[Export] public int Population { get; set; } = 0;
	[Export] public int UnemployedPopulation { get; set; } = 0;
	[Export] public float Food { get; set; } = 100.0f;
	[Export] public float Wood { get; set; } = 100.0f;
	[Export] public float StorageCapacity { get; set; } = 500.0f;
	
	// --- Simulation Settings ---
	[Export] public float TickRate { get; set; } = 2.0f; 
	[Export] public float FoodConsumptionPerPop { get; set; } = 0.5f;

	private List<ResidencePlot> _pendingConstruction = new List<ResidencePlot>();
	private List<ResidencePlot> _constructedHouses = new List<ResidencePlot>();

	[Signal]
	public delegate void SimulationTickedEventHandler();

	public void AddPopulation(int amount)
	{
		Population += amount;
		UnemployedPopulation += amount;
		EmitSignal(SignalName.SimulationTicked);
	}

	public void RemovePopulation(int amount)
	{
		Population -= amount;
		UnemployedPopulation -= amount;
		if (Population < 0) Population = 0;
		if (UnemployedPopulation < 0) UnemployedPopulation = 0;
		EmitSignal(SignalName.SimulationTicked);
	}

	public int RequestWorkers(int amount)
	{
		int assigned = Mathf.Min(amount, UnemployedPopulation);
		UnemployedPopulation -= assigned;
		EmitSignal(SignalName.SimulationTicked);
		return assigned;
	}

	public void ReturnWorkers(int amount)
	{
		UnemployedPopulation += amount;
		EmitSignal(SignalName.SimulationTicked);
	}

	public void RegisterConstructionSite(ResidencePlot site)
	{
		if (site != null && !site.IsConstructed)
		{
			_pendingConstruction.Add(site);
		}
	}

	public void RegisterConstructedHouse(ResidencePlot house)
	{
		if (house != null && !_constructedHouses.Contains(house))
		{
			_constructedHouses.Add(house);
		}
	}

	public ResidencePlot GetRandomHouse()
	{
		_constructedHouses.RemoveAll(h => !IsInstanceValid(h));
		if (_constructedHouses.Count == 0) return null;
		int idx = (int)(GD.Randi() % _constructedHouses.Count);
		return _constructedHouses[idx];
	}

	public ResidencePlot GetNextConstructionProject()
	{
		// Critical: Check if the house still exists in the world
		_pendingConstruction.RemoveAll(s => !IsInstanceValid(s) || s.IsConstructed);
		
		foreach (var site in _pendingConstruction)
		{
			if (!site.IsAssigned)
			{
				site.IsAssigned = true;
				return site;
			}
		}
		return null;
	}

	public void SaveGame(string fileName = "savegame")
	{
		var saveData = new Godot.Collections.Dictionary<string, Variant>();
		saveData["Wood"] = Wood;
		saveData["Food"] = Food;
		saveData["Population"] = Population;

		var buildingsList = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
		var root = GetTree().CurrentScene;
		if (root == null) return;
		
		foreach (Node child in root.GetChildren())
		{
			string type = GetBuildingType(child);
			if (type != "")
			{
				var bData = new Godot.Collections.Dictionary<string, Variant>();
				bData["Type"] = type;
				bData["PosX"] = ((Node3D)child).GlobalPosition.X;
				bData["PosY"] = ((Node3D)child).GlobalPosition.Y;
				bData["PosZ"] = ((Node3D)child).GlobalPosition.Z;
				bData["RotY"] = ((Node3D)child).GlobalRotation.Y;
				buildingsList.Add(bData);
			}
		}
		saveData["Buildings"] = buildingsList;

		string json = Json.Stringify(saveData);
		string path = $"user://{fileName}.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(json);
			GD.Print($"Game Saved to {path}");
		}
	}

	public void LoadGame(string fileName = "savegame")
	{
		string path = $"user://{fileName}.json";
		if (!FileAccess.FileExists(path)) return;

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		string json = file.GetAsText();
		var jsonVar = Json.ParseString(json);
		if (jsonVar.VariantType == Variant.Type.Nil) return;
		
		var saveData = jsonVar.AsGodotDictionary<string, Variant>();

		Wood = (float)saveData["Wood"];
		Food = (float)saveData["Food"];
		Population = 0; 

		var root = GetTree().CurrentScene;
		if (root == null) return;

		var buildingMgr = root.GetNodeOrNull<BuildingManager>("BuildingManager");
		if (buildingMgr == null) return;
		
		// Clear existing buildings
		foreach (Node child in root.GetChildren())
		{
			if (GetBuildingType(child) != "") child.QueueFree();
		}

		var buildingsArray = saveData["Buildings"].AsGodotArray<Godot.Collections.Dictionary<string, Variant>>();
		foreach (var b in buildingsArray)
		{
			string type = (string)b["Type"];
			Vector3 pos = new Vector3((float)b["PosX"], (float)b["PosY"], (float)b["PosZ"]);
			float rotY = (float)b["RotY"];
			buildingMgr.ForcePlaceBuilding(type, pos, rotY);
		}

		EmitSignal(SignalName.SimulationTicked);
		GD.Print($"Game Loaded from {path}");
	}
	private string GetBuildingType(Node node)
	{
		if (node is ResidencePlot) return "House";
		string name = node.Name.ToString();
		if (name.Contains("TownCenter")) return "TownCenter";
		if (name.Contains("MeatShop")) return "MeatShop";
		if (name.Contains("Stable")) return "HorseStable";
		if (name.Contains("BuilderGuild")) return "Guild";
		if (name.Contains("Forager")) return "ForagerHut";
		if (name.Contains("Fishing")) return "FishingHut";
		if (name.Contains("Woodcutter")) return "WoodcutterHut";
		if (name.Contains("Hunter")) return "HunterHut";
		return "";
	}

	private double _timeSinceLastTick = 0.0;

	public override void _Ready()
	{
		GD.Print("Global Simulation Initialized.");
		if (!string.IsNullOrEmpty(PendingLoadFile))
		{
			string fileToLoad = PendingLoadFile;
			PendingLoadFile = ""; // Clear for next time
			CallDeferred(nameof(LoadGame), fileToLoad);
		}
	}

	public override void _Process(double delta)
	{
		_timeSinceLastTick += delta;
		if (_timeSinceLastTick >= TickRate)
		{
			PerformTick();
			_timeSinceLastTick = 0.0;
		}
	}

	private void PerformTick()
	{
		// Food consumption is now handled by individual ResidencePlots for visual needs
		if (Food < 0) Food = 0;
		
		// We use a safe signal emission - Godot handles dead listeners automatically 
		EmitSignal(SignalName.SimulationTicked);
	}
}
