class_name AndroidAppSettings
extends Node
## App-details Settings. Godot docs: empty Intent + setAction/setData.
## Не Intent(action, uri): wrapper путает перегрузки (godot#106320).
## Не has_method на Java — startActivity/parse часто «невидимы».


const ACTION_APP_DETAILS := "android.settings.APPLICATION_DETAILS_SETTINGS"
const ACTION_MANAGE_APPS := "android.settings.MANAGE_APPLICATIONS_SETTINGS"
const ACTION_SETTINGS := "android.settings.SETTINGS"
const FLAG_ACTIVITY_NEW_TASK := 0x10000000
const FALLBACK_PACKAGE := "space.easypeasymatch.magicball"
const LOG_TAG := "[AndroidAppSettings]"


func open(host: Node = null) -> bool:
	if OS.get_name() != "Android":
		_warn("not Android")
		return false

	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var activity: Object = _activity(runtime)
	if activity != null and runtime != null:
		var ui_callable := func() -> void:
			var ok := _open_now(activity)
			if host != null and is_instance_valid(host):
				host.call_deferred("_on_android_settings_result", ok)
		if runtime.has_method("createRunnableFromGodotCallable"):
			activity.runOnUiThread(runtime.createRunnableFromGodotCallable(ui_callable))
			var ex := _take_java()
			if ex == "":
				print("%s scheduled on UI thread" % LOG_TAG)
				return true
			_warn("runOnUiThread: %s" % ex)
		return _open_now(activity)

	return _open_now(activity)


func _open_now(activity: Object = null) -> bool:
	var wrapper: Object = _java_wrapper()
	if wrapper == null:
		_warn("JavaClassWrapper missing")
		return false

	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var context: Object = activity if activity != null else _activity(runtime)
	if context == null:
		context = _app_context(runtime)
	if context == null:
		_warn("context missing")
		return false

	var intent_cls: Object = wrapper.wrap("android.content.Intent")
	var uri_cls: Object = wrapper.wrap("android.net.Uri")
	_take_java()
	if intent_cls == null or uri_cls == null:
		_warn("Intent/Uri wrap failed")
		return false

	var pkg := _package_name(context)
	print("%s pkg=%s" % [LOG_TAG, pkg])

	var uri: Object = uri_cls.parse("package:%s" % pkg)
	_take_java()
	if uri == null:
		_warn("Uri.parse failed")
		return _start_plain(intent_cls, context, ACTION_MANAGE_APPS) \
			or _start_plain(intent_cls, context, ACTION_SETTINGS)

	# Только пустой ctor — как в доке Godot 4.7 (не Intent(String, Uri)).
	var intent: Object = intent_cls.Intent()
	_take_java()
	if intent == null:
		_warn("Intent() failed")
		return false
	intent.setAction(ACTION_APP_DETAILS)
	intent.setData(uri)
	intent.addFlags(FLAG_ACTIVITY_NEW_TASK)
	_take_java()

	if _start(context, intent):
		print("%s opened details" % LOG_TAG)
		return true

	if _start_plain(intent_cls, context, ACTION_MANAGE_APPS):
		print("%s opened manage-apps" % LOG_TAG)
		return true
	if _start_plain(intent_cls, context, ACTION_SETTINGS):
		print("%s opened Settings" % LOG_TAG)
		return true
	_warn("all intents failed")
	return false


func _start_plain(intent_cls: Object, context: Object, action: String) -> bool:
	var intent: Object = intent_cls.Intent()
	_take_java()
	if intent == null:
		return false
	intent.setAction(action)
	intent.addFlags(FLAG_ACTIVITY_NEW_TASK)
	_take_java()
	return _start(context, intent)


func _start(context: Object, intent: Object) -> bool:
	if context == null or intent == null:
		return false
	context.startActivity(intent)
	var ex := _take_java()
	if ex != "":
		_warn("startActivity: %s" % ex)
		return false
	return true


func _package_name(context: Object) -> String:
	if context != null:
		var pkg := str(context.getPackageName())
		_take_java()
		if not pkg.is_empty() and pkg != "null":
			return pkg
	return FALLBACK_PACKAGE


func _activity(runtime: Object) -> Object:
	if runtime == null:
		return null
	var activity: Object = runtime.getActivity()
	_take_java()
	return activity


func _app_context(runtime: Object) -> Object:
	if runtime == null:
		return null
	var ctx: Object = runtime.getApplicationContext()
	_take_java()
	return ctx


func _java_wrapper() -> Object:
	if Engine.has_singleton("JavaClassWrapper"):
		return Engine.get_singleton("JavaClassWrapper")
	return null


func _take_java() -> String:
	var wrapper: Object = _java_wrapper()
	if wrapper == null:
		return ""
	var ex: Object = wrapper.get_exception()
	if ex == null:
		return ""
	return str(ex.toString()) if ex.has_method("toString") else str(ex)


func _warn(message: String) -> void:
	var line := "%s %s" % [LOG_TAG, message]
	push_warning(line)
	print(line)
