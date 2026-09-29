extends Control

signal web_ready
signal web_message(message: String)
signal web_unavailable(reason: String)

var _browser: Object
var _ready_emitted := false

func _ready() -> void:
    visible = false
    mouse_filter = Control.MOUSE_FILTER_IGNORE

    if not ClassDB.class_exists("CefTexture"):
        _fail("Godot CEF is not installed (CefTexture class is unavailable)")
        return

    _browser = ClassDB.instantiate("CefTexture")
    if _browser == null:
        _fail("Godot CEF is installed but CefTexture could not be instantiated")
        return

    _browser.set("enable_accelerated_osr", true)
    _browser.set("url", "res://WebUI/index.html")
    _browser.call("set_anchors_and_offsets_preset", Control.PRESET_FULL_RECT)
    _browser.connect("ipc_message", Callable(self, "_on_ipc_message"))
    _browser.connect("load_finished", Callable(self, "_on_load_finished"))
    _browser.connect("load_error", Callable(self, "_on_load_error"))
    add_child(_browser)

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
