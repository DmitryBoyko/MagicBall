class_name AndroidAppSettings
extends RefCounted
## Opens this app's system permission screen. No NEW_TASK+NO_HISTORY (AtlasPhoto).


func open_details() -> bool:
	if OS.get_name() != "Android":
		return false
	if not Engine.has_singleton("JavaClassWrapper"):
		return false
	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var context: Object = _context(runtime)
	if context == null:
		return false
	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var intent_cls: Object = wrapper.wrap("android.content.Intent")
	var settings_cls: Object = wrapper.wrap("android.provider.Settings")
	var uri_cls: Object = wrapper.wrap("android.net.Uri")
	if intent_cls == null or settings_cls == null or uri_cls == null:
		return false
	var intent: Object = intent_cls.Intent()
	if intent == null:
		return false
	intent.setAction(settings_cls.ACTION_APPLICATION_DETAILS_SETTINGS)
	var pkg := str(context.getPackageName())
	var uri: Object = uri_cls.parse("package:%s" % pkg)
	intent.setData(uri)
	if not context.has_method("runOnUiThread"):
		intent.addFlags(0x10000000)
	context.startActivity(intent)
	return true


func sdk_int() -> int:
	if OS.get_name() != "Android" or not Engine.has_singleton("JavaClassWrapper"):
		return 0
	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var version: Object = wrapper.wrap("android.os.Build$VERSION")
	if version == null:
		return 0
	return int(version.SDK_INT)


func _context(runtime: Object) -> Object:
	if runtime != null:
		if runtime.has_method("getActivity"):
			var activity: Object = runtime.getActivity()
			if activity != null:
				return activity
		if runtime.has_method("getApplicationContext"):
			var app: Object = runtime.getApplicationContext()
			if app != null:
				return app
	if Engine.has_singleton("Godot"):
		var godot: Object = Engine.get_singleton("Godot")
		if godot != null and godot.has_method("getActivity"):
			var from_godot: Object = godot.getActivity()
			if from_godot != null:
				return from_godot
	return null
