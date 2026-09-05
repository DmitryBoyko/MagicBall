class_name AndroidLocationProbe
extends Node
## Last-known + одно обновление LocationManager. Node в дереве — listener не умирает.


const LOG_TAG := "[AndroidLocation]"
const MIN_TIME_MS := 800
const MIN_DIST_M := 0.0

var _listener: Object = null
var _manager: Object = null
var _listening: bool = false
var _cached: Dictionary = _empty()


func _ready() -> void:
	kick()


func probe() -> Dictionary:
	_refresh_last_known()
	return _cached.duplicate()


func kick() -> void:
	if OS.get_name() != "Android":
		return
	_refresh_last_known()
	_start_updates()


func read_lat_lon() -> Dictionary:
	return probe()


func _empty() -> Dictionary:
	return {"ok": false, "lat": 0.0, "lon": 0.0, "accuracy": 1.0e9, "provider": ""}


func onLocationChanged(location: Object) -> void:
	_accept_java(location, "live")
	_stop_updates()


func onStatusChanged(_provider: String, _status: int, _extras: Object) -> void:
	pass


func onProviderEnabled(_provider: String) -> void:
	pass


func onProviderDisabled(_provider: String) -> void:
	pass


func _refresh_last_known() -> void:
	var loc: Object = _location_manager()
	if loc == null:
		return
	var wrapper: Object = _java()
	for provider in _providers(loc, wrapper):
		var fix: Object = loc.getLastKnownLocation(provider)
		_clear(wrapper)
		if fix != null:
			_accept_java(fix, provider)


func _start_updates() -> void:
	if _listening:
		return
	var loc: Object = _location_manager()
	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	if loc == null or runtime == null:
		return
	var listener: Object = _ensure_listener()
	if listener == null:
		return

	var start := func() -> void:
		for provider in _providers(loc, _java()):
			if loc.has_method("requestLocationUpdates"):
				loc.requestLocationUpdates(provider, MIN_TIME_MS, MIN_DIST_M, listener)
				_clear(_java())
		_listening = true
		print("%s updates requested" % LOG_TAG)

	if loc.has_method("runOnUiThread"):
		pass
	var activity: Object = runtime.getActivity() if runtime.has_method("getActivity") else null
	if activity != null and activity.has_method("runOnUiThread") and runtime.has_method("createRunnableFromGodotCallable"):
		activity.runOnUiThread(runtime.createRunnableFromGodotCallable(start))
	else:
		start.call()


func _stop_updates() -> void:
	if not _listening:
		return
	var loc: Object = _location_manager()
	var listener: Object = _listener
	if loc != null and listener != null and loc.has_method("removeUpdates"):
		loc.removeUpdates(listener)
		_clear(_java())
	_listening = false


func _ensure_listener() -> Object:
	if _listener != null:
		return _listener
	var wrapper: Object = _java()
	if wrapper == null or not wrapper.has_method("create_proxy"):
		return null
	_listener = wrapper.create_proxy(self, PackedStringArray(["android.location.LocationListener"]))
	_clear(wrapper)
	return _listener


func _accept_java(fix: Object, provider: String) -> void:
	if fix == null or not fix.has_method("getLatitude") or not fix.has_method("getLongitude"):
		return
	var lat := float(fix.getLatitude())
	var lon := float(fix.getLongitude())
	if abs(lat) < 0.0001 and abs(lon) < 0.0001:
		return
	var acc := 50.0
	if fix.has_method("getAccuracy"):
		var raw := float(fix.getAccuracy())
		if raw > 0.0:
			acc = raw
	if _cached.get("ok", false):
		var old_acc := float(_cached.get("accuracy", 1.0e9))
		var old_p := str(_cached.get("provider", ""))
		var prefer_gps := provider == "gps" and old_p != "gps" and acc <= old_acc * 1.25
		if acc > old_acc and not prefer_gps:
			return
	_cached = {
		"ok": true,
		"lat": lat,
		"lon": lon,
		"accuracy": acc,
		"provider": provider,
	}
	print("%s fix %s acc=%s" % [LOG_TAG, provider, acc])


func _providers(loc: Object, wrapper: Object) -> Array[String]:
	var names: Array[String] = []
	if loc.has_method("getProviders"):
		var listed: Variant = loc.getProviders(true)
		_clear(wrapper)
		if listed is Object and listed.has_method("size"):
			var n := int(listed.size())
			for i in n:
				var item := str(listed.get(i))
				if not item.is_empty() and item != "null":
					names.append(item)
	if names.is_empty():
		names = ["fused", "gps", "network", "passive"]
	return names


func _location_manager() -> Object:
	if _manager != null:
		return _manager
	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var context: Object = _context(runtime)
	if context == null or not context.has_method("getSystemService"):
		return null
	_manager = context.getSystemService("location")
	_clear(_java())
	return _manager


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
	return null


func _java() -> Object:
	if Engine.has_singleton("JavaClassWrapper"):
		return Engine.get_singleton("JavaClassWrapper")
	return null


func _clear(wrapper: Object) -> void:
	if wrapper == null or not wrapper.has_method("get_exception"):
		return
	wrapper.get_exception()
