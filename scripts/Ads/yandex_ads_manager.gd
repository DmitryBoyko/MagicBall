extends Node
## Rewarded flow like TrueTaro. Show only if a creative is loaded; otherwise skip quickly.

const _Config = preload("res://scripts/Ads/yandex_ads_config.gd")
const _YandexAds = preload("res://addons/GodotAndroidYandexAds/yandex_ads.gd")

const LOAD_TIMEOUT_SEC := 2.0
const SHOW_TIMEOUT_SEC := 45.0

signal rewarded_flow_finished(granted: bool)

var _ads: Node
var _rewarded_waiting := false
var _rewarded_received := false
var _rewarded_shown := false
var _rewarded_gen := 0
var _require_reward := false
var last_skip_reason := ""


func _ready() -> void:
	if not _Config.ADS_ENABLED:
		return
	_ads = _YandexAds.new()
	_ads.name = "YandexAds"
	_ads.api_key = _Config.APP_ID
	_ads.banner_id = _Config.BANNER_ID
	_ads.interstitial_id = _Config.INTERSTITIAL_ID
	_ads.rewarded_id = _Config.REWARDED_ID
	_ads.banner_on_top = _Config.BANNER_ON_TOP
	add_child(_ads)
	_connect_ads_signals()
	if OS.get_name() == "Android" and not _Config.REWARDED_ID.is_empty():
		_ads.load_rewarded_video()


func show_rewarded_for_ai() -> void:
	_require_reward = true
	last_skip_reason = ""
	if not _Config.ADS_ENABLED:
		call_deferred("_emit_rewarded_finished", false)
		return
	if _ads == null or OS.get_name() != "Android" or _Config.REWARDED_ID.is_empty():
		call_deferred("_emit_rewarded_finished", false)
		return
	if _rewarded_waiting:
		last_skip_reason = "busy"
		call_deferred("_emit_rewarded_finished", false)
		return
	_rewarded_waiting = true
	_rewarded_received = false
	_rewarded_shown = false
	_rewarded_gen += 1
	var gen := _rewarded_gen

	if _ads.is_rewarded_video_loaded():
		call_deferred("_show_rewarded_now", gen)
		return

	_ads.load_rewarded_video()
	var timer := get_tree().create_timer(LOAD_TIMEOUT_SEC)
	timer.timeout.connect(func() -> void:
		if gen != _rewarded_gen or not _rewarded_waiting or _rewarded_shown:
			return
		_finish_rewarded_flow(false)
	, CONNECT_ONE_SHOT)


func skip_message() -> String:
	if last_skip_reason == "closed":
		return "Туман судьбы неразличим."
	return "Туман судьбы неразличим."


func _connect_ads_signals() -> void:
	if _ads == null:
		return
	_ads.rewarded_video_loaded.connect(_on_rewarded_video_loaded)
	_ads.rewarded_video_failed_to_load.connect(_on_rewarded_video_failed_to_load)
	_ads.rewarded_video_closed.connect(_on_rewarded_video_closed)
	_ads.rewarded.connect(_on_rewarded)


func _on_rewarded_video_loaded() -> void:
	if _rewarded_waiting and not _rewarded_shown:
		call_deferred("_show_rewarded_now", _rewarded_gen)


func _on_rewarded_video_failed_to_load(_error_code: int) -> void:
	if _rewarded_waiting and not _rewarded_shown:
		_finish_rewarded_flow(false)


func _on_rewarded_video_closed() -> void:
	if not _rewarded_waiting:
		return
	call_deferred("_finish_rewarded_flow", true)


func _show_rewarded_now(gen: int) -> void:
	if gen != _rewarded_gen or not _rewarded_waiting or _rewarded_shown:
		return
	if _ads == null or not _ads.is_rewarded_video_loaded():
		_finish_rewarded_flow(false)
		return
	_rewarded_shown = true
	_ads.show_rewarded_video()
	var timer := get_tree().create_timer(SHOW_TIMEOUT_SEC)
	timer.timeout.connect(func() -> void:
		if gen != _rewarded_gen or not _rewarded_waiting:
			return
		_finish_rewarded_flow(false)
	, CONNECT_ONE_SHOT)


func _finish_rewarded_flow(_closed_normally: bool) -> void:
	if not _rewarded_waiting:
		return
	var shown := _rewarded_shown
	var received := _rewarded_received
	var strict := _require_reward
	_rewarded_waiting = false
	_rewarded_shown = false
	_require_reward = false
	_rewarded_gen += 1
	if OS.get_name() == "Android" and _ads != null and not _Config.REWARDED_ID.is_empty():
		_ads.load_rewarded_video()
	var granted := true
	if strict:
		granted = received
		if not granted:
			last_skip_reason = "closed" if shown else "no_fill"
	call_deferred("_emit_rewarded_finished", granted)


func _emit_rewarded_finished(granted: bool) -> void:
	rewarded_flow_finished.emit(granted)


func _on_rewarded(_currency: String, _amount: int) -> void:
	if _rewarded_waiting:
		_rewarded_received = true
