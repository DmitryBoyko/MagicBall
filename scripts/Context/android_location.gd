class_name AndroidLocationProbe
extends Node
## Непрерывный поток как Geolocator.getPositionStream (LBSDetector START):
## last-known сразу + fused/gps/network, listener не гасим после первого фикса.


const LOG_TAG := "[AndroidLocation]"
const MIN_TIME_MS := 1000
const MIN_DIST_M := 1.0
const CRITERIA_ACCURACY_FINE := 1


var _listener: Object = null
var _manager: Object = null
var _listening: bool = false
var _cached: Dictionary = _empty()


func _ready() -> void:
	kick()


func _exit_tree() -> void:
	_stop_updates()


func probe() -> Dictionary:
	_refresh_last_known()
	return _cached.duplicate()


func kick() -> void:
	if OS.get_name() != "Android":
		return
	_refresh_last_known()
	_start_updates()
	_request_current()


func read_lat_lon() -> Dictionary:
	return probe()


func _empty() -> Dictionary:
	return {"ok": false, "lat": 0.0, "lon": 0.0, "accuracy": 1.0e9, "provider": ""}


func onLocationChanged(location: Object) -> void:
	_accept_java(location, "live")


func onStatusChanged(_provider: String, _status: int, _extras: Object) -> void:
	pass


func onProviderEnabled(_provider: String) -> void:
	_refresh_last_known()


func onProviderDisabled(_provider: String) -> void:
	pass


func _refresh_last_known() -> void:
	var loc: Object = _location_manager()
	if loc == null:
		return
	for provider in _providers(loc):
		var fix: Object = loc.getLastKnownLocation(provider)
		_take_java()
		if fix != null:
			_accept_java(fix, provider)


func _start_updates() -> void:
	if _listening:
		return
	var loc: Object = _location_manager()
	var runtime: Object = _runtime()
	if loc == null or runtime == null:
		return
	var listener: Object = _ensure_listener()
	if listener == null:
		_warn("LocationListener proxy missing")
		return

	var looper: Object = _main_looper()
	var start := func() -> void:
		var names: Array[String] = _providers(loc)
		var best := _best_provider(loc)
		if best != "" and not names.has(best):
			names.insert(0, best)
		for provider in names:
			if looper != null:
				loc.requestLocationUpdates(provider, MIN_TIME_MS, MIN_DIST_M, listener, looper)
			else:
				loc.requestLocationUpdates(provider, MIN_TIME_MS, MIN_DIST_M, listener)
			var ex := _take_java()
			if ex != "":
				_warn("requestLocationUpdates %s: %s" % [provider, ex])
		_listening = true
		print("%s stream on providers=%s" % [LOG_TAG, ",".join(names)])

	_run_ui(runtime, start)


func _request_current() -> void:
	var loc: Object = _location_manager()
	var runtime: Object = _runtime()
	if loc == null or runtime == null:
		return
	var wrapper: Object = _java()
	if wrapper == null or not wrapper.has_method("create_sam_callback"):
		return
	var activity: Object = runtime.getActivity()
	_take_java()
	if activity == null or not activity.has_method("getMainExecutor"):
		return
	var executor: Object = activity.getMainExecutor()
	_take_java()
	if executor == null:
		return
	var consumer: Object = wrapper.create_sam_callback(
		"java.util.function.Consumer",
		func(location: Object) -> void:
			if location != null:
				_accept_java(location, "current")
	)
	_take_java()
	if consumer == null:
		return
	var provider := _best_provider(loc)
	if provider.is_empty():
		provider = "fused"
	# API 30+: last-known часто пуст, currentLocation даёт первую точку.
	loc.getCurrentLocation(provider, null, executor, consumer)
	var ex := _take_java()
	if ex != "" and provider != "gps":
		loc.getCurrentLocation("gps", null, executor, consumer)
		_take_java()
	if ex != "":
		_warn("getCurrentLocation: %s" % ex)


func _stop_updates() -> void:
	if not _listening:
		return
	var loc: Object = _location_manager()
	if loc != null and _listener != null:
		loc.removeUpdates(_listener)
		_take_java()
	_listening = false


func _ensure_listener() -> Object:
	if _listener != null:
		return _listener
	var wrapper: Object = _java()
	if wrapper == null or not wrapper.has_method("create_proxy"):
		return null
	_listener = wrapper.create_proxy(self, PackedStringArray(["android.location.LocationListener"]))
	_take_java()
	return _listener


func _accept_java(fix: Object, provider: String) -> void:
	if fix == null:
		return
	var lat := float(fix.getLatitude())
	var lon := float(fix.getLongitude())
	_take_java()
	if abs(lat) < 0.0001 and abs(lon) < 0.0001:
		return
	var acc := 50.0
	var raw := float(fix.getAccuracy())
	_take_java()
	if raw > 0.0:
		acc = raw
	if _cached.get("ok", false):
		var old_acc := float(_cached.get("accuracy", 1.0e9))
		var old_p := str(_cached.get("provider", ""))
		var prefer_gps := (provider == "gps" or provider == "fused" or provider == "current") \
			and old_p == "network" and acc <= old_acc * 1.4
		if acc > old_acc and not prefer_gps:
			return
	_cached = {
		"ok": true,
		"lat": lat,
		"lon": lon,
		"accuracy": acc,
		"provider": provider,
	}
	print("%s fix %s acc=%.0f" % [LOG_TAG, provider, acc])


func _best_provider(loc: Object) -> String:
	var wrapper: Object = _java()
	if wrapper == null:
		return "fused"
	var crit_cls: Object = wrapper.wrap("android.location.Criteria")
	_take_java()
	if crit_cls == null:
		return "fused"
	var crit: Object = crit_cls.Criteria()
	_take_java()
	if crit == null:
		return "fused"
	crit.setAccuracy(CRITERIA_ACCURACY_FINE)
	_take_java()
	var name := str(loc.getBestProvider(crit, true))
	_take_java()
	if name.is_empty() or name == "null":
		return "fused"
	return name


func _providers(loc: Object) -> Array[String]:
	var names: Array[String] = []
	var listed: Variant = loc.getProviders(true)
	_take_java()
	if listed is Object and listed.has_method("size"):
		var n := int(listed.size())
		for i in n:
			var item := str(listed.get(i))
			if not item.is_empty() and item != "null" and not names.has(item):
				names.append(item)
	for fallback in ["fused", "gps", "network", "passive"]:
		if not names.has(fallback):
			names.append(fallback)
	return names


func _main_looper() -> Object:
	var wrapper: Object = _java()
	if wrapper == null:
		return null
	var looper_cls: Object = wrapper.wrap("android.os.Looper")
	_take_java()
	if looper_cls == null:
		return null
	var looper: Object = looper_cls.getMainLooper()
	_take_java()
	return looper


func _run_ui(runtime: Object, work: Callable) -> void:
	var activity: Object = runtime.getActivity()
	_take_java()
	if activity != null and runtime.has_method("createRunnableFromGodotCallable"):
		activity.runOnUiThread(runtime.createRunnableFromGodotCallable(work))
		_take_java()
		return
	work.call()


func _location_manager() -> Object:
	if _manager != null:
		return _manager
	var runtime: Object = _runtime()
	var context: Object = _context(runtime)
	if context == null:
		return null
	_manager = context.getSystemService("location")
	_take_java()
	return _manager


func _context(runtime: Object) -> Object:
	if runtime == null:
		return null
	var activity: Object = runtime.getActivity()
	_take_java()
	if activity != null:
		return activity
	var app: Object = runtime.getApplicationContext()
	_take_java()
	return app


func _runtime() -> Object:
	if Engine.has_singleton("AndroidRuntime"):
		return Engine.get_singleton("AndroidRuntime")
	return null


func _java() -> Object:
	if Engine.has_singleton("JavaClassWrapper"):
		return Engine.get_singleton("JavaClassWrapper")
	return null


func _take_java() -> String:
	var wrapper: Object = _java()
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
