@tool
extends EditorExportPlugin
## Android 11+ package visibility queries for share intents.


func _get_name() -> String:
	return "AndroidShareQueries"


func _supports_platform(platform: EditorExportPlatform) -> bool:
	return platform is EditorExportPlatformAndroid


func _get_android_manifest_application_element_contents(_platform: EditorExportPlatform, _debug: bool) -> String:
	return 'android:usesCleartextTraffic="true"\n'


func _get_android_manifest_element_contents(_platform: EditorExportPlatform, _debug: bool) -> String:
	# Permissions are set in export_presets.cfg — only add package-visibility queries here.
	return (
		"<queries>\n"
		+ '<intent><action android:name="android.intent.action.SEND" /><data android:mimeType="image/*" /></intent>\n'
		+ "</queries>\n"
	)
