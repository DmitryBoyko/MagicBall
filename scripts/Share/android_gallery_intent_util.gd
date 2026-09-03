class_name AndroidGalleryIntentUtil
extends RefCounted

const ACTION_SEND := "android.intent.action.SEND"
const ACTION_ATTACH_DATA := "android.intent.action.ATTACH_DATA"
const EXTRA_STREAM := "android.intent.extra.STREAM"
const EXTRA_TEXT := "android.intent.extra.TEXT"
const EXTRA_SUBJECT := "android.intent.extra.SUBJECT"
const FLAG_GRANT_READ_URI_PERMISSION := 1
const FLAG_ACTIVITY_NEW_TASK := 0x10000000
const LOG_TAG := "[GalleryAndroid]"

static var last_error: String = ""


static func is_android() -> bool:
	return OS.get_name() == "Android"


static func clear_error() -> void:
	last_error = ""


static func report_error(message: String) -> void:
	last_error = message
	var line := "%s %s" % [LOG_TAG, message]
	push_warning(line)
	print(line)


static func log_info(message: String) -> void:
	print("%s %s" % [LOG_TAG, message])


static func run_launch_on_ui_thread(host: Node, absolute_path: String, chooser_title: String, wallpaper: bool, share_text: String = "") -> void:
	if not is_android() or absolute_path.is_empty():
		_finish_host(host, false, "invalid launch")
		return
	if not Engine.has_singleton("AndroidRuntime"):
		_finish_host(host, false, "AndroidRuntime missing")
		return
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	if get_android_context(runtime) == null:
		_finish_host(host, false, "context missing")
		return
	var activity: Object = get_activity(runtime)
	if activity == null or not activity.has_method("runOnUiThread") or not runtime.has_method("createRunnableFromGodotCallable"):
		var ok_direct := _launch_on_ui_thread(absolute_path, chooser_title, wallpaper, share_text)
		_finish_host(host, ok_direct, last_error)
		return
	var ui_callable := func() -> void:
		var ok_ui := _launch_on_ui_thread(absolute_path, chooser_title, wallpaper, share_text)
		if host != null and is_instance_valid(host):
			host.call_deferred("_on_gallery_android_result", ok_ui, last_error)
	activity.runOnUiThread(runtime.createRunnableFromGodotCallable(ui_callable))


static func _finish_host(host: Node, ok: bool, error_text: String) -> void:
	if host != null and is_instance_valid(host):
		host.call_deferred("_on_gallery_android_result", ok, error_text)


static func _launch_on_ui_thread(absolute_path: String, chooser_title: String, wallpaper: bool, share_text: String = "") -> bool:
	clear_error()
	var uri: Object = file_uri_for_path(absolute_path)
	if uri == null:
		return false
	var intent: Object = null
	if wallpaper:
		intent = _build_wallpaper_intent(uri)
	else:
		var mime := "image/png"
		var lower := absolute_path.to_lower()
		if lower.ends_with(".jpg") or lower.ends_with(".jpeg"):
			mime = "image/jpeg"
		elif lower.ends_with(".webp"):
			mime = "image/webp"
		intent = _build_share_intent(uri, mime, share_text)
	if intent == null:
		return false
	return start_activity(intent, chooser_title)


static func _build_share_intent(uri: Object, mime_type: String = "image/png", share_text: String = "") -> Object:
	var intent_cls: Object = wrap_intent_class()
	if intent_cls == null:
		report_error("Intent class missing")
		return null
	var send_intent: Object = intent_cls.Intent()
	if send_intent == null:
		report_error("Intent ctor failed")
		return null
	send_intent.setAction(ACTION_SEND)
	send_intent.setType(mime_type if not mime_type.is_empty() else "image/*")
	send_intent.putExtra(EXTRA_STREAM, uri)
	if not share_text.is_empty():
		send_intent.putExtra(EXTRA_SUBJECT, "Волшебный шар")
		send_intent.putExtra(EXTRA_TEXT, share_text)
	grant_uri_read(send_intent, uri)
	log_info("share intent ready mime=%s text=%s" % [mime_type, "yes" if not share_text.is_empty() else "no"])
	return send_intent


static func _build_wallpaper_intent(uri: Object) -> Object:
	if not Engine.has_singleton("AndroidRuntime"):
		return null
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var context: Object = get_android_context(runtime)
	if context == null:
		report_error("context missing")
		return null
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		return null
	var wallpaper_cls: Object = wrapper.wrap("android.app.WallpaperManager")
	if wallpaper_cls == null:
		report_error("WallpaperManager missing")
		return null
	var manager: Object = wallpaper_cls.getInstance(context)
	if manager == null:
		report_error("WallpaperManager instance missing")
		return null
	var intent: Object = null
	if manager.has_method("getCropAndSetWallpaperIntent"):
		intent = manager.getCropAndSetWallpaperIntent(uri)
	if intent == null:
		var intent_cls: Object = wrap_intent_class()
		if intent_cls == null:
			report_error("wallpaper intent unavailable")
			return null
		intent = intent_cls.Intent()
		intent.setAction(ACTION_ATTACH_DATA)
		intent.setDataAndType(uri, "image/jpeg")
	grant_uri_read(intent, uri)
	log_info("wallpaper intent ready")
	return intent


static func file_uri_for_path(absolute_path: String) -> Object:
	if absolute_path.is_empty():
		report_error("empty path")
		return null
	if not Engine.has_singleton("AndroidRuntime"):
		report_error("AndroidRuntime missing")
		return null
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var context: Object = get_android_context(runtime)
	if context == null:
		report_error("context missing")
		return null
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		report_error("JavaClassWrapper missing")
		return null
	var file_cls: Object = wrapper.wrap("java.io.File")
	if file_cls == null:
		report_error("java.io.File missing")
		return null
	var file: Object = file_cls.File(absolute_path)
	if file == null:
		report_error("File ctor failed")
		return null
	if not file.exists():
		report_error("file not found: %s" % absolute_path)
		return null
	var package_name: String = str(context.getPackageName())
	var authority := "%s.fileprovider" % package_name
	log_info("package=%s path=%s" % [package_name, absolute_path])
	var file_provider: Object = wrapper.wrap("androidx.core.content.FileProvider")
	if file_provider == null:
		report_error("FileProvider missing")
		return null
	var uri: Object = file_provider.getUriForFile(context, authority, file)
	if uri == null:
		report_error("getUriForFile failed for %s" % authority)
		return null
	var uri_text := str(uri.toString()) if uri.has_method("toString") else "uri ok"
	log_info("uri=%s" % uri_text)
	return uri


static func grant_uri_read(intent: Object, uri: Object) -> void:
	if intent == null or uri == null:
		return
	intent.addFlags(FLAG_GRANT_READ_URI_PERMISSION)
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		return
	var clip_cls: Object = wrapper.wrap("android.content.ClipData")
	if clip_cls == null or not clip_cls.has_method("newUri"):
		return
	var clip: Object = clip_cls.newUri(null, "image", uri)
	if clip != null and intent.has_method("setClipData"):
		intent.setClipData(clip)


static func start_activity(intent: Object, chooser_title: String = "") -> bool:
	if intent == null:
		report_error("null intent")
		return false
	if not Engine.has_singleton("AndroidRuntime"):
		report_error("AndroidRuntime missing")
		return false
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var context: Object = get_android_context(runtime)
	if context == null:
		report_error("context missing")
		return false
	var wrapper: Object = java_wrapper()
	var launch_intent: Object = intent
	if not chooser_title.is_empty() and wrapper != null:
		var intent_cls: Object = wrapper.wrap("android.content.Intent")
		if intent_cls != null and intent_cls.has_method("createChooser"):
			launch_intent = intent_cls.createChooser(intent, chooser_title)
	if launch_intent == null:
		report_error("chooser failed")
		return false
	if not is_activity(context):
		launch_intent.addFlags(FLAG_ACTIVITY_NEW_TASK)
	context.startActivity(launch_intent)
	log_info("startActivity called")
	return true


static func wrap_intent_class() -> Object:
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		return null
	return wrapper.wrap("android.content.Intent")


static func is_activity(obj: Object) -> bool:
	return obj != null and obj.has_method("runOnUiThread")


static func get_activity(runtime: Object) -> Object:
	var context: Object = get_android_context(runtime)
	if is_activity(context):
		return context
	return null


static func get_android_context(runtime: Object) -> Object:
	var context: Object = null
	if runtime != null:
		# Как в HapticManager: getActivity, затем getApplicationContext без has_method.
		if runtime.has_method("getActivity"):
			context = runtime.getActivity()
			if context != null:
				log_info("context=AndroidRuntime.getActivity")
				return context
		context = runtime.getApplicationContext()
		if context != null:
			log_info("context=AndroidRuntime.getApplicationContext")
			return context
		context = _try_call(runtime, "getContext")
		if context != null:
			log_info("context=AndroidRuntime.getContext")
			return context
	if Engine.has_singleton("Godot"):
		var godot: Object = Engine.get_singleton("Godot")
		if godot != null:
			context = _try_call(godot, "getActivity")
			if context != null:
				log_info("context=Godot.getActivity")
				return context
			if godot.has_method("getRenderView"):
				var render_view: Object = godot.getRenderView()
				if render_view != null:
					context = _try_call(render_view, "getContext")
					if context != null:
						log_info("context=render_view.getContext")
						return context
					context = _try_call(render_view, "getActivity")
					if context != null:
						log_info("context=render_view.getActivity")
						return context
	context = _context_from_godot_java()
	if context != null:
		return context
	context = _context_from_activity_thread()
	if context != null:
		return context
	report_error("context missing")
	return null


static func _try_call(target: Object, method: String) -> Object:
	if target == null or not target.has_method(method):
		return null
	var result: Variant = target.call(method)
	_log_java_exception()
	if result is Object and result != null:
		return result as Object
	return null


static func _context_from_godot_java() -> Object:
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		return null
	var godot_cls: Object = wrapper.wrap("org.godotengine.godot.Godot")
	if godot_cls == null or not godot_cls.has_method("getInstance"):
		return null
	var godot: Object = godot_cls.getInstance()
	_log_java_exception()
	if godot == null:
		return null
	var activity: Object = _try_call(godot, "getActivity")
	if activity != null:
		log_info("context=Godot.getInstance().getActivity")
		return activity
	return null


static func _context_from_activity_thread() -> Object:
	var wrapper: Object = java_wrapper()
	if wrapper == null:
		return null
	var thread_cls: Object = wrapper.wrap("android.app.ActivityThread")
	if thread_cls == null or not thread_cls.has_method("currentApplication"):
		return null
	var app: Object = thread_cls.currentApplication()
	_log_java_exception()
	if app == null:
		return null
	log_info("context=ActivityThread.currentApplication")
	return app


static func _log_java_exception() -> void:
	var wrapper: Object = java_wrapper()
	if wrapper == null or not wrapper.has_method("get_exception"):
		return
	var ex: Object = wrapper.get_exception()
	if ex == null:
		return
	var msg := str(ex.toString()) if ex.has_method("toString") else str(ex)
	log_info("java exception: %s" % msg)


static func java_wrapper() -> Object:
	if Engine.has_singleton("JavaClassWrapper"):
		return Engine.get_singleton("JavaClassWrapper")
	return null
