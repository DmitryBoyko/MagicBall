class_name AndroidAppSettings
extends RefCounted
## Opens Android application details (permissions) screen.


const ACTION_APP_DETAILS := "android.settings.APPLICATION_DETAILS_SETTINGS"
const FLAG_ACTIVITY_NEW_TASK := 0x10000000
const LOG_TAG := "[AndroidAppSettings]"


func open_details() -> bool:
	if OS.get_name() != "Android":
		_warn("not Android")
		return false
	if not Engine.has_singleton("JavaClassWrapper"):
		_warn("JavaClassWrapper missing")
		return false

	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var activity: Object = _activity(runtime)
	var context: Object = activity if activity != null else _context(runtime)
	if context == null:
		_warn("context missing")
		return false

	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var intent_cls: Object = wrapper.wrap("android.content.Intent")
	var uri_cls: Object = wrapper.wrap("android.net.Uri")
	if intent_cls == null or uri_cls == null:
		_warn("Intent/Uri wrap failed")
		return false

	var intent: Object = intent_cls.Intent()
	if intent == null:
		_warn("Intent ctor failed")
		return false

	# Строковый action — надёжнее, чем Settings.ACTION_* через wrapper (часто null).
	intent.setAction(ACTION_APP_DETAILS)
	_log_java(wrapper, "setAction")

	var pkg := str(context.getPackageName())
	var uri: Object = null
	if uri_cls.has_method("fromParts"):
		uri = uri_cls.fromParts("package", pkg, null)
	if uri == null and uri_cls.has_method("parse"):
		uri = uri_cls.parse("package:%s" % pkg)
	if uri == null:
		_warn("Uri build failed for %s" % pkg)
		return false
	intent.setData(uri)
	_log_java(wrapper, "setData")

	# ApplicationContext требует NEW_TASK; Activity — тоже безопасно.
	intent.addFlags(FLAG_ACTIVITY_NEW_TASK)

	if activity != null and activity.has_method("runOnUiThread") and runtime != null \
			and runtime.has_method("createRunnableFromGodotCallable"):
		var launch := func() -> void:
			_start(context, intent, wrapper)
		activity.runOnUiThread(runtime.createRunnableFromGodotCallable(launch))
		print("%s startActivity scheduled on UI thread pkg=%s" % [LOG_TAG, pkg])
		return true

	return _start(context, intent, wrapper)


func _start(context: Object, intent: Object, wrapper: Object) -> bool:
	if context == null or intent == null:
		return false
	if not context.has_method("startActivity"):
		_warn("startActivity missing")
		return false
	context.startActivity(intent)
	_log_java(wrapper, "startActivity")
	print("%s startActivity called" % LOG_TAG)
	return true


func _activity(runtime: Object) -> Object:
	if runtime != null and runtime.has_method("getActivity"):
		var activity: Object = runtime.getActivity()
		if activity != null:
			return activity
	if Engine.has_singleton("Godot"):
		var godot: Object = Engine.get_singleton("Godot")
		if godot != null and godot.has_method("getActivity"):
			var from_godot: Object = godot.getActivity()
			if from_godot != null:
				return from_godot
	return null


func _context(runtime: Object) -> Object:
	var activity: Object = _activity(runtime)
	if activity != null:
		return activity
	if runtime != null and runtime.has_method("getApplicationContext"):
		var app: Object = runtime.getApplicationContext()
		if app != null:
			return app
	return null


func _log_java(wrapper: Object, where: String) -> void:
	if wrapper == null or not wrapper.has_method("get_exception"):
		return
	var ex: Object = wrapper.get_exception()
	if ex == null:
		return
	var msg := str(ex.toString()) if ex.has_method("toString") else str(ex)
	push_warning("%s java @%s: %s" % [LOG_TAG, where, msg])


func _warn(message: String) -> void:
	push_warning("%s %s" % [LOG_TAG, message])
	print("%s %s" % [LOG_TAG, message])
