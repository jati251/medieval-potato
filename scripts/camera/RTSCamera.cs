using Godot;
using System;

public partial class RTSCamera : Camera3D
{
	[Export] public float MoveSpeed { get; set; } = 20.0f;
	[Export] public float ZoomSpeed { get; set; } = 2.0f;
	[Export] public float RotateSpeed { get; set; } = 2.0f;
	
	private Vector3 _groundPosition;
	private float _targetZoom = 15.0f;

	public override void _Ready()
	{
		_groundPosition = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				_targetZoom = Mathf.Clamp(_targetZoom - ZoomSpeed, 5.0f, 40.0f);
			if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				_targetZoom = Mathf.Clamp(_targetZoom + ZoomSpeed, 5.0f, 40.0f);
		}
	}

	public override void _Process(double delta)
	{
		float fDelta = (float)delta;
		HandleMovement(fDelta);
		HandleRotation(fDelta);
		ApplyCameraTransform(fDelta);
	}

	private void HandleMovement(float delta)
	{
		Vector3 inputDir = Vector3.Zero;
		
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) inputDir.Z -= 1;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) inputDir.Z += 1;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) inputDir.X -= 1;
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) inputDir.X += 1;

		if (inputDir.Length() > 0)
		{
			Vector3 forward = -Transform.Basis.Z;
			forward.Y = 0;
			forward = forward.Normalized();

			Vector3 right = Transform.Basis.X;
			right.Y = 0;
			right = right.Normalized();

			Vector3 moveDir = (forward * -inputDir.Z) + (right * inputDir.X);
			_groundPosition += moveDir.Normalized() * MoveSpeed * delta;
			
			// Keep within bounds
			_groundPosition.X = Mathf.Clamp(_groundPosition.X, -240, 240);
			_groundPosition.Z = Mathf.Clamp(_groundPosition.Z, -240, 240);
		}
	}

	private void ApplyCameraTransform(float delta)
	{
		// Smoothly interpolate the ground position and the zoom height
		Vector3 targetPos = _groundPosition;
		targetPos.Y = Mathf.Lerp(GlobalPosition.Y, _targetZoom, delta * 8.0f);
		targetPos.Z += Mathf.Lerp(GlobalPosition.Z - _groundPosition.Z, _targetZoom, delta * 8.0f);
		
		GlobalPosition = new Vector3(
			Mathf.Lerp(GlobalPosition.X, _groundPosition.X, delta * 10.0f),
			Mathf.Lerp(GlobalPosition.Y, _targetZoom, delta * 8.0f),
			Mathf.Lerp(GlobalPosition.Z, _groundPosition.Z + _targetZoom, delta * 8.0f)
		);
	}

	private void HandleRotation(float delta)
	{
		if (Input.IsKeyPressed(Key.Q))
			RotationDegrees = new Vector3(RotationDegrees.X, RotationDegrees.Y + RotateSpeed * 50.0f * delta, RotationDegrees.Z);
		if (Input.IsKeyPressed(Key.E))
			RotationDegrees = new Vector3(RotationDegrees.X, RotationDegrees.Y - RotateSpeed * 50.0f * delta, RotationDegrees.Z);
	}
}
