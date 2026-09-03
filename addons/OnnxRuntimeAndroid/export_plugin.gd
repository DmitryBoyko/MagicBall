@tool
extends EditorPlugin

var export_plugin: AndroidExportPlugin


func _enter_tree() -> void:
	export_plugin = AndroidExportPlugin.new()
	add_export_plugin(export_plugin)


func _exit_tree() -> void:
	remove_export_plugin(export_plugin)
	export_plugin = null


class AndroidExportPlugin extends EditorExportPlugin:
	const _PLUGIN_NAME := "OnnxRuntimeAndroid"
	const _AAR_PATH := "packages/microsoft.ml.onnxruntime/1.19.0/runtimes/android/native/onnxruntime.aar"


	func _supports_platform(platform) -> bool:
		return platform is EditorExportPlatformAndroid


	func _get_android_libraries(_platform, _debug) -> PackedStringArray:
		return PackedStringArray([_AAR_PATH])


	func _get_name() -> String:
		return _PLUGIN_NAME
