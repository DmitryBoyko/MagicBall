class_name AndroidMediaStore
extends RefCounted
## Recent gallery files via MediaStore (DirAccess часто пуст на Android 13+).


const LOG_TAG := "[AndroidMediaStore]"
const IMAGES_URI := "content://media/external/images/media"


func list_recent_paths(take: int) -> PackedStringArray:
	return list_recent_paths_static(take)


static func list_recent_paths_static(take: int) -> PackedStringArray:
	var out := PackedStringArray()
	if OS.get_name() != "Android" or take <= 0:
		return out
	if not Engine.has_singleton("JavaClassWrapper") or not Engine.has_singleton("AndroidRuntime"):
		return out

	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var context: Object = runtime.getActivity() if runtime.has_method("getActivity") else null
	if context == null:
		context = runtime.getApplicationContext() if runtime.has_method("getApplicationContext") else null
	if context == null or not context.has_method("getContentResolver"):
		_warn("no ContentResolver")
		return out

	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var uri_cls: Object = wrapper.wrap("android.net.Uri")
	var uri: Object = uri_cls.parse(IMAGES_URI)
	_clear(wrapper)
	if uri == null:
		_warn("uri parse failed")
		return out

	var resolver: Object = context.getContentResolver()
	_clear(wrapper)
	if resolver == null:
		return out

	var projection := PackedStringArray(["_data", "date_added"])
	var cursor: Object = resolver.query(uri, projection, null, null, "date_added DESC")
	var ex := _take(wrapper)
	if ex != "" or cursor == null:
		cursor = resolver.query(uri, null, null, null, "date_added DESC")
		ex = _take(wrapper)
	if ex != "" or cursor == null:
		_warn("query failed: %s" % ex)
		return out

	var data_idx: int = cursor.getColumnIndex("_data")
	_clear(wrapper)
	if data_idx < 0:
		data_idx = cursor.getColumnIndex("DATA")
		_clear(wrapper)

	var n := 0
	while n < take and cursor.moveToNext():
		_clear(wrapper)
		if data_idx < 0:
			break
		var path := str(cursor.getString(data_idx))
		_clear(wrapper)
		if path.is_empty() or path == "null":
			continue
		if not (path.to_lower().ends_with(".jpg") or path.to_lower().ends_with(".jpeg")
				or path.to_lower().ends_with(".png") or path.to_lower().ends_with(".webp")):
			continue
		out.append(path)
		n += 1

	if cursor.has_method("close"):
		cursor.close()
	print("%s listed %s" % [LOG_TAG, out.size()])
	return out


static func _clear(wrapper: Object) -> void:
	_take(wrapper)


static func _take(wrapper: Object) -> String:
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
