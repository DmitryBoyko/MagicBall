class_name AndroidLocationProbe
extends RefCounted
## Last-known GPS/network fix via Android LocationManager.


static func read_lat_lon() -> Dictionary:
	var empty := {"ok": false, "lat": 0.0, "lon": 0.0}
	if OS.get_name() != "Android":
		return empty
	if not Engine.has_singleton("JavaClassWrapper"):
		return empty
	var runtime: Object = Engine.get_singleton("AndroidRuntime") if Engine.has_singleton("AndroidRuntime") else null
	var context: Object = _context(runtime)
	if context == null:
		return empty
	var loc: Object = null
	if context.has_method("getSystemService"):
		loc = context.getSystemService("location")
	if loc == null:
		return empty

	var best: Object = null
	var best_acc := 1.0e9
	for provider in ["gps", "network", "passive"]:
		var fix: Object = null
		if loc.has_method("getLastKnownLocation"):
			fix = loc.getLastKnownLocation(provider)
		if fix == null:
			continue
		var acc := 5000.0
		if fix.has_method("getAccuracy"):
			acc = float(fix.getAccuracy())
		if acc < best_acc:
			best = fix
			best_acc = acc

	if best == null or not best.has_method("getLatitude") or not best.has_method("getLongitude"):
		return empty
	return {
		"ok": true,
		"lat": float(best.getLatitude()),
		"lon": float(best.getLongitude()),
	}


func probe() -> Dictionary:
	return read_lat_lon()


static func _context(runtime: Object) -> Object:
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
