@tool
extends EditorPlugin

func _enter_tree() -> void:
	# Initialization of the plugin goes here.
	add_custom_type("ControlCamera3D", "Camera3D",
			preload("res://addons/control_camera3d/nodes/control_camera_3d.gd"),
			preload("res://addons/control_camera3d/control_camera3d.svg"))

func _exit_tree() -> void:
	# Clean-up of the plugin goes here.
	remove_custom_type("ControlCamera3D")
