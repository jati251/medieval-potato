using Godot;
using System;

public partial class RTSCamera : Camera3D
{
	[Export] public float MoveSpeed { get; set; } = 20.0f;
	[Export] public float ZoomSpeed { get; set; } = 2.0f;
	[Export] public float RotateSpeed { get; set; } = 2.0f;
	
	private Vector3 _targetPosition;
	private float _targetZoom = 15.0f;
	private float _targetRotation = 0.0f;

	public override void _Ready()
	{
		_targetPosition = GlobalPosition;
		_targetRotation = Rotation.Y;
	}

	public override void _Process(double delta)
	{
		float fDelta = (float)delta;
		HandleMovement(fDelta);
		HandleZoom(fDelta);
		HandleRotation(fDelta);
	}

	private void HandleMovement(float delta)
	{
		Vector3 inputDir = Vector3.Zero;
		
		if (Input.IsActionPressed("ui_up") || Input.IsKeyPressed(Key.W)) inputDir.Z -= 1;
		if (Input.IsActionPressed("ui_down") || Input.IsKeyPressed(Key.S)) inputDir.Z += 1;
		if (Input.IsActionPressed("ui_left") || Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
		if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D)) inputDir.X += 1;

		inputDir = inputDir.Rotated(Vector3.Up, Rotation.Y).Normalized();
		GlobalPosition += inputDir * MoveSpeed * delta;
	}

	private void HandleZoom(float delta)
	{
		if (Input.IsMouseButtonPressed(MouseButton.WheelUp))
			_targetZoom -= ZoomSpeed;
		if (Input.IsMouseButtonPressed(MouseButton.WheelDown))
			_targetZoom += ZoomSpeed;

		_targetZoom = Mathf.Clamp(_targetZoom, 5.0f, 40.0f);
		
		// In an RTS camera, zoom is often just moving the camera along its local Z or changing height
		// For simplicity, let's just adjust the Y height and Z offset
		Position = new Vector3(Position.X, Mathf.Lerp(Position.Y, _targetZoom, delta * 5.0f), Position.Z);
	}

	private void HandleRotation(float delta)
	{
		if (Input.IsKeyPressed(Key.Q))
			RotationDegrees = new Vector3(RotationDegrees.X, RotationDegrees.Y + RotateSpeed, RotationDegrees.Z);
		if (Input.IsKeyPressed(Key.E))
			RotationDegrees = new Vector3(RotationDegrees.X, RotationDegrees.Y - RotateSpeed, RotationDegrees.Z);
	}
}
