class_name AndroidGalleryShare
extends RefCounted
## Share an image file via Android ACTION_SEND chooser (FileProvider).


static func launch_async(host: Node, absolute_image_path: String, chooser_title: String, share_text: String = "") -> void:
	AndroidGalleryIntentUtil.run_launch_on_ui_thread(host, absolute_image_path, chooser_title, false, share_text)


func launch(host: Node, absolute_image_path: String, chooser_title: String, share_text: String = "") -> void:
	launch_async(host, absolute_image_path, chooser_title, share_text)
