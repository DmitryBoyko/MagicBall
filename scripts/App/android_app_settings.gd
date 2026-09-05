class_name AndroidAppSettings
extends RefCounted
## Opens Android application-details (permissions). UI-thread + FLAG_ACTIVITY_NEW_TASK.
## Pattern: android_gallery_intent_util (host Node, not a dying RefCounted lambda).


const ACTION_APP_DETAILS := "android.settings.APPLICATION_DETAILS_SETTINGS"
const ACTION_MANAGE_APPS := "android.settings.MANAGE_APPLICATIONS_SETTINGS"
const CATEGORY_DEFAULT := "android.intent.category.DEFAULT"
const FLAG_ACTIVITY_NEW_TASK := 0x10000000
const FALLBACK_PACKAGE := "space.easypeasymatch.magicball"
const LOG_TAG := "[AndroidAppSettings]"


func open_on_ui_thread(host: Node) -> bool:
	return open_on_ui_thread_static(host)


func open_details() -> bool:
	return _open_now()


static func open_on_ui_thread_static(host: Node) -> bool:
	if OS.get_name() != "Android":
		_warn("not Android")
		return false

	if not Engine.has_singleton("AndroidRuntime"):
		_warn("AndroidRuntime missing")
		return _open_now()

	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var activity: Object = _resolve_activity(runtime)
	if activity == null or not activity.has_method("runOnUiThread") or not runtime.has_method("createRunnableFromGodotCallable"):
		print("%s no UI thread bridge, sync startActivity" % LOG_TAG)
		return _open_now()

	var ui_callable := func() -> void:
		var ok := _open_now()
		if host != null and is_instance_valid(host):
			host.call_deferred("_on_android_settings_result", ok)
	activity.runOnUiThread(runtime.createRunnableFromGodotCallable(ui_callable))
	print("%s scheduled on UI thread" % LOG_TAG)
	return true


static func _open_now() -> bool:
	if OS.get_name() != "Android":
		_warn("not Android")
		return false

	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var context: Object = _resolve_context(runtime)
	if context == null:
		_warn("context missing")
		return false

	var wrapper: Object = _java_wrapper()
	if wrapper == null:
		_warn("JavaClassWrapper missing")
		return false

	var intent_cls: Object = wrapper.wrap("android.content.Intent")
	var uri_cls: Object = wrapper.wrap("android.net.Uri")
	_clear_java(wrapper)
	if intent_cls == null or uri_cls == null:
		_warn("Intent/Uri wrap failed")
		return false

	var pkg := _package_name(context)
	var intent: Object = _build_details_intent(intent_cls, uri_cls, wrapper, pkg)
	if intent == null:
		_warn("details intent failed")
		return _start_manage_apps(intent_cls, context, wrapper)

	if _start(context, intent, wrapper):
		print("%s opened details pkg=%s" % [LOG_TAG, pkg])
		return true

	return _start_manage_apps(intent_cls, context, wrapper)


static func _build_details_intent(intent_cls: Object, uri_cls: Object, wrapper: Object, pkg: String) -> Object:
	# Не has_method("parse"): Java static часто не виден через wrapper.
	var uri: Object = uri_cls.parse("package:%s" % pkg)
	_clear_java(wrapper)
	if uri == null:
		uri = uri_cls.fromParts("package", pkg, "")
		_clear_java(wrapper)
	if uri == null:
		_warn("Uri build failed for %s" % pkg)
		return null

	var uri_text := str(uri.toString()) if uri.has_method("toString") else "package:%s" % pkg
	print("%s uri=%s" % [LOG_TAG, uri_text])

	# Двухаргументный ctor надёжнее, чем пустой Intent + setAction/setData.
	var intent: Object = intent_cls.Intent(ACTION_APP_DETAILS, uri)
	_clear_java(wrapper)
	if intent == null:
		intent = intent_cls.Intent()
		_clear_java(wrapper)
		if intent == null:
			_warn("Intent ctor failed")
			return null
		intent.setAction(ACTION_APP_DETAILS)
		intent.setData(uri)
		_clear_java(wrapper)

	intent.addFlags(FLAG_ACTIVITY_NEW_TASK)
	intent.addCategory(CATEGORY_DEFAULT)
	_clear_java(wrapper)
	return intent


static func _start_manage_apps(intent_cls: Object, context: Object, wrapper: Object) -> bool:
	var manage: Object = intent_cls.Intent()
	if manage == null:
		return false
	manage.setAction(ACTION_MANAGE_APPS)
	manage.addFlags(FLAG_ACTIVITY_NEW_TASK)
	_clear_java(wrapper)
	var ok := _start(context, manage, wrapper)
	print("%s manage-apps fallback → %s" % [LOG_TAG, ok])
	return ok


static func _start(context: Object, intent: Object, wrapper: Object) -> bool:
	if context == null or intent == null:
		return false
	if not context.has_method("startActivity"):
		_warn("startActivity missing")
		return false
	context.startActivity(intent)
	var ex := _take_java(wrapper)
	if ex != "":
		_warn("startActivity: %s" % ex)
		return false
	return true


static func _package_name(context: Object) -> String:
	if context != null and context.has_method("getPackageName"):
		var pkg := str(context.getPackageName())
		if not pkg.is_empty() and pkg != "null":
			return pkg
	return FALLBACK_PACKAGE


static func _resolve_activity(runtime: Object) -> Object:
	var context: Object = _resolve_context(runtime)
	if context != null and context.has_method("runOnUiThread"):
		return context
	return null


static func _resolve_context(runtime: Object) -> Object:
	var context: Object = null
	if runtime != null:
		if runtime.has_method("getActivity"):
			context = runtime.getActivity()
			if context != null:
				return context
		context = _try_call(runtime, "getApplicationContext")
		if context != null:
			return context
		context = _try_call(runtime, "getContext")
		if context != null:
			return context

	if Engine.has_singleton("Godot"):
		var godot: Object = Engine.get_singleton("Godot")
		context = _try_call(godot, "getActivity")
		if context != null:
			return context

	var wrapper: Object = _java_wrapper()
	if wrapper == null:
		return null

	var godot_cls: Object = wrapper.wrap("org.godotengine.godot.Godot")
	if godot_cls != null and godot_cls.has_method("getInstance"):
		var inst: Object = godot_cls.getInstance()
		_clear_java(wrapper)
		context = _try_call(inst, "getActivity")
		if context != null:
			return context

	var thread_cls: Object = wrapper.wrap("android.app.ActivityThread")
	if thread_cls != null and thread_cls.has_method("currentApplication"):
		context = thread_cls.currentApplication()
		_clear_java(wrapper)
		if context != null:
			return context
	return null


static func _try_call(obj: Object, method: String) -> Object:
	if obj == null or not obj.has_method(method):
		return null
	return obj.call(method)


static func _java_wrapper() -> Object:
	if Engine.has_singleton("JavaClassWrapper"):
		return Engine.get_singleton("JavaClassWrapper")
	return null


static func _clear_java(wrapper: Object) -> void:
	_take_java(wrapper)


static func _take_java(wrapper: Object) -> String:
	if wrapper == null or not wrapper.has_method("get_exception"):
		return ""
	var ex: Object = wrapper.get_exception()
	if ex == null:
		return ""
	return str(ex.toString()) if ex.has_method("toString") else str(ex)


static func _warn(message: String) -> void:
	var line := "%s %s" % [LOG_TAG, message]
	push_warning(line)
	print(line)
