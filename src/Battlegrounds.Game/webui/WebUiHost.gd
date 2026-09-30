extends Control

signal web_ready
signal web_message(message: String)
signal web_unavailable(reason: String)

const CEF_EXTENSION_PATH := "res://addons/godot_cef/godot_cef.gdextension"
const WEB_UI_PATH := "res://webui"
const DEV_CSS_POLL_SECONDS := 0.35

var _browser: Object
var _ready_emitted := false
var _dev_css_watch_enabled := false
var _dev_css_poll_remaining := 0.0
var _dev_css_snapshot: Dictionary = {}

func _ready() -> void:
	visible = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE

	if not _ensure_cef_loaded():
		return

	_browser = ClassDB.instantiate("CefTexture")
	if _browser == null:
		_fail("Godot CEF loaded, but CefTexture could not be instantiated")
		return

	_dev_css_watch_enabled = OS.is_debug_build()
	if _dev_css_watch_enabled:
		_dev_css_snapshot = _snapshot_css_files()
	set_process(_dev_css_watch_enabled)

	_browser.set("url", "res://webui/index.html")
	_browser.call("set_anchors_and_offsets_preset", Control.PRESET_FULL_RECT)
	_browser.connect("ipc_message", Callable(self, "_on_ipc_message"))
	_browser.connect("load_finished", Callable(self, "_on_load_finished"))
	_browser.connect("load_error", Callable(self, "_on_load_error"))
	add_child(_browser)

func _process(delta: float) -> void:
	if not _dev_css_watch_enabled:
		return

	_dev_css_poll_remaining -= delta
	if _dev_css_poll_remaining > 0.0:
		return
	_dev_css_poll_remaining = DEV_CSS_POLL_SECONDS

	var next_snapshot := _snapshot_css_files()
	if not _css_snapshot_changed(next_snapshot):
		return

	_dev_css_snapshot = next_snapshot
	if _browser == null or not _ready_emitted:
		return

	_browser.call("send_ipc_message", JSON.stringify({
		"type": "dev-css-reload",
		"payload": { "stamp": Time.get_ticks_msec() },
	}))

func _snapshot_css_files() -> Dictionary:
	var snapshot: Dictionary = {}
	_collect_css_files(WEB_UI_PATH, snapshot)
	return snapshot

func _collect_css_files(directory_path: String, snapshot: Dictionary) -> void:
	var directory := DirAccess.open(directory_path)
	if directory == null:
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name.begins_with("."):
			continue

		var path := directory_path.path_join(entry_name)
		if directory.current_is_dir():
			_collect_css_files(path, snapshot)
		elif entry_name.get_extension().to_lower() == "css":
			snapshot[path] = FileAccess.get_md5(path)
	directory.list_dir_end()

func _css_snapshot_changed(next_snapshot: Dictionary) -> bool:
	if next_snapshot.size() != _dev_css_snapshot.size():
		return true

	for path in next_snapshot:
		if not _dev_css_snapshot.has(path) or _dev_css_snapshot[path] != next_snapshot[path]:
			return true
	return false

func _ensure_cef_loaded() -> bool:
	if ClassDB.class_exists("CefTexture"):
		return true

	if not FileAccess.file_exists(CEF_EXTENSION_PATH):
		_fail("Godot CEF extension file is missing at %s" % CEF_EXTENSION_PATH)
		return false

	var status := GDExtensionManager.load_extension(CEF_EXTENSION_PATH)
	if status != GDExtensionManager.LOAD_STATUS_OK and status != GDExtensionManager.LOAD_STATUS_ALREADY_LOADED:
		_fail("Godot CEF failed to load from %s (status %s)" % [CEF_EXTENSION_PATH, status])
		return false

	if not ClassDB.class_exists("CefTexture"):
		_fail("Godot CEF extension loaded, but CefTexture was not registered")
		return false

	return true

func send_message(message: String) -> void:
	if _browser == null or not _ready_emitted:
		return
	_browser.call("send_ipc_message", message)

func _on_ipc_message(message: String) -> void:
	web_message.emit(message)

func _on_load_finished(_url: String, _status: int) -> void:
	if _ready_emitted:
		return
	_ready_emitted = true
	visible = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	web_ready.emit()

func _on_load_error(url: String, error_code: int, error_text: String) -> void:
	_fail("Failed to load %s (%d): %s" % [url, error_code, error_text])

func _fail(reason: String) -> void:
	visible = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	push_error(reason)
	web_unavailable.emit(reason)
	get_tree().quit(1)
