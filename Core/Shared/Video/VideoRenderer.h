#pragma once
#include "pch.h"
#include <thread>
#include "Shared/SettingTypes.h"
#include "Shared/RenderedFrame.h"
#include "Shared/Interfaces/IRenderingDevice.h"
#include "Utilities/AutoResetEvent.h"
#include "Utilities/SimpleLock.h"
#include "Utilities/safe_ptr.h"

class IRenderingDevice;
class Emulator;
class SystemHud;
class DebugHud;
class InputHud;

class IVideoRecorder;
enum class VideoCodec;

struct RecordAviOptions
{
	VideoCodec Codec;
	uint32_t CompressionLevel;
	bool RecordSystemHud;
	bool RecordInputHud;
};

class VideoRenderer
{
private:
	Emulator* _emu;

	AutoResetEvent _waitForRender;
	unique_ptr<std::thread> _renderThread;
	IRenderingDevice* _renderer = nullptr;
	atomic<bool> _stopFlag;
	SimpleLock _stopStartLock;

	uint32_t _rendererWidth = 512;
	uint32_t _rendererHeight = 480;

	unique_ptr<DebugHud> _rendererHud;
	unique_ptr<SystemHud> _systemHud;
	unique_ptr<InputHud> _inputHud;
	SimpleLock _hudLock;

	RenderSurfaceInfo _aviRecorderSurface = {};
	RecordAviOptions _recorderOptions = {};

	RenderSurfaceInfo _emuHudSurface = {};
	RenderSurfaceInfo _scriptHudSurface = {};
	RenderSurfaceInfo _scriptCanvasSurface = {};
	bool _needScriptHudClear = false;
	bool _needScriptCanvasClear = false;
	uint32_t _scriptHudScale = 2;
	uint32_t _lastScriptHudFrameNumber = 0;
	uint32_t _lastScriptCanvasFrameNumber = 0;
	uint32_t _scriptCanvasScale = 2;
	bool _needRedraw = true;

	RenderedFrame _lastFrame;
	SimpleLock _frameLock;

	safe_ptr<IVideoRecorder> _recorder;

	void RenderThread();
	bool DrawScriptHud(RenderedFrame& frame);
	bool DrawScriptCanvas(RenderedFrame& frame);
	
	FrameInfo GetEmuHudSize(FrameInfo baseFrameSize);

	void ProcessAviRecording(RenderedFrame& frame);

public:
	VideoRenderer(Emulator* emu);
	~VideoRenderer();

	FrameInfo GetRendererSize();
	void SetRendererSize(uint32_t width, uint32_t height);
	
	void SetScriptHudScale(uint32_t scale) { _scriptHudScale = scale; }
	void SetScriptCanvasScale(uint32_t scale) { _scriptCanvasScale = scale; }
	std::pair<FrameInfo, OverscanDimensions> GetScriptHudSize();
	std::pair<FrameInfo, OverscanDimensions> GetScriptCanvasSize();

	void StartThread();
	void StopThread();

	void UpdateFrame(RenderedFrame& frame);
	void ClearFrame();
	void RegisterRenderingDevice(IRenderingDevice *renderer);
	void UnregisterRenderingDevice(IRenderingDevice *renderer);

	void StartRecording(string filename, RecordAviOptions options);
	void AddRecordingSound(int16_t* soundBuffer, uint32_t sampleCount, uint32_t sampleRate);
	void StopRecording();
	bool IsRecording();

	const RenderSurfaceInfo& GetScriptCanvasSurface() const { return _scriptCanvasSurface; }
};