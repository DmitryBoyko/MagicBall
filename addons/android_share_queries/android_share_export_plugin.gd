@tool
extends EditorExportPlugin
## Android 11+ package visibility queries for share intents.


func _get_name() -> String:
	return "AndroidShareQueries"


func _supports_platform(platform: EditorExportPlatform) -> bool:
	return platform.get_name() == "Android"


func _get_android_manifest_element_contents(platform: EditorExportPlatform, _debug: bool) -> String:
	if platform.get_name() != "Android":
		return ""
	return (
		'<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />\n'
		+ '<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />\n'
		+ "<queries>\n"
		+ '<intent><action android:name="android.intent.action.SEND" /><data android:mimeType="image/*" /></intent>\n'
		+ "</queries>\n"
	)
