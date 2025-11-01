using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Input;
using Mesen.Controls;
using Mesen.Debugger.Windows;
using Mesen.Interop;
using Mesen.ViewModels;
using Mesen.Utilities;
using ReactiveUI.Fody.Helpers;
using System;

namespace Mesen.Debugger.Windows
{
    public class ScriptCanvasWindow : MesenWindow, IDisposable
    {
        private NotificationListener _listener;
        private SimpleImageViewer _scriptCanvas;
        private SoftwareRendererViewModel _model = new();
        private bool _isOpen = false;

        public ScriptCanvasWindow()
        {
            InitializeComponent();

            DataContext = _model;
            _scriptCanvas = this.GetControl<SimpleImageViewer>("ScriptCanvas");

            _listener = new NotificationListener();
            _listener.OnNotification += OnNotification;

            _scriptCanvas.PointerMoved += OnCanvasPointerChanged;
            _scriptCanvas.PointerPressed += OnCanvasPointerChanged;
            _scriptCanvas.PointerReleased += OnCanvasPointerChanged;
            _scriptCanvas.PointerExited += OnCanvasPointerExited;
            
            Closed += (s, e) => {
                Dispose();
            };

            Opened += (s, e) => { _isOpen = true; };
            Closing += (s, e) => { _isOpen = false; };
            Closed += (s, e) => { _isOpen = false; };
            Activated += (s, e) => { /* no-op */ };
            Deactivated += (s, e) => { InputApi.SetScriptCanvasMouseState(-1, -1, false, false, false); };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private unsafe void UpdateSurface(SoftwareRendererSurface frame, DynamicBitmap? surface, Action<DynamicBitmap> update)
        {
            var size = new PixelSize((int)frame.Width, (int)frame.Height);
            if(surface?.PixelSize != size) {
                surface = new DynamicBitmap(size, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                update(surface);
            }

            int byteCount = (int)frame.Width * (int)frame.Height * sizeof(UInt32);
            var lockBmp = surface!.Lock();
            var src = new Span<byte>((byte*)frame.FrameBuffer, byteCount);
            var dst = new Span<byte>((byte*)lockBmp.FrameBuffer.Address, byteCount);
            src.CopyTo(dst);
            lockBmp.Dispose();
        }

        private void OnNotification(NotificationEventArgs e)
        {
            if(!_isOpen) { return; }
            if(_scriptCanvas == null || _scriptCanvas.Bounds.Width <= 0 || _scriptCanvas.Bounds.Height <= 0) { return; }
            if(e.NotificationType == ConsoleNotificationType.RefreshSoftwareRenderer) {
                var frame = System.Runtime.InteropServices.Marshal.PtrToStructure<SoftwareRendererFrame>(e.Parameter);
                UpdateFromSoftwareRendererFrame(frame);
            }
        }

        private unsafe void UpdateFromSoftwareRendererFrame(SoftwareRendererFrame frame)
        {
            if(frame.ScriptCanvas.FrameBuffer != IntPtr.Zero && frame.ScriptCanvas.Width > 0 && frame.ScriptCanvas.Height > 0) {
                if(frame.ScriptCanvas.IsDirty) {
                    UpdateSurface(frame.ScriptCanvas, _model.ScriptCanvasSurface, s => _model.ScriptCanvasSurface = s);
                }
                Dispatcher.UIThread.Post(() => {
                    _scriptCanvas.InvalidateVisual();
                }, DispatcherPriority.MaxValue);
            }
        }

        private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
        {
            if(!_isOpen) { return; }
            InputApi.SetScriptCanvasMouseState(-1, -1, false, false, false);
        }

        private void OnCanvasPointerChanged(object? sender, PointerEventArgs e)
        {
            if(!_isOpen) { return; }
            var surf = _model.ScriptCanvasSurface;
            if(surf == null || surf.PixelSize.Width <= 0 || surf.PixelSize.Height <= 0 || _scriptCanvas.Bounds.Width <= 0 || _scriptCanvas.Bounds.Height <= 0) {
                InputApi.SetScriptCanvasMouseState(-1, -1, false, false, false);
                return;
            }

            Point p = e.GetPosition(_scriptCanvas);
            double w = _scriptCanvas.Bounds.Width;
            double h = _scriptCanvas.Bounds.Height;
            int srcW = surf.PixelSize.Width;
            int srcH = surf.PixelSize.Height;

            int x = (int)Math.Clamp(p.X * srcW / Math.Max(1.0, w), 0, Math.Max(0, srcW - 1));
            int y = (int)Math.Clamp(p.Y * srcH / Math.Max(1.0, h), 0, Math.Max(0, srcH - 1));

            var props = e.GetCurrentPoint(_scriptCanvas).Properties;
            bool left = props.IsLeftButtonPressed;
            bool middle = props.IsMiddleButtonPressed;
            bool right = props.IsRightButtonPressed;

            InputApi.SetScriptCanvasMouseState((short)x, (short)y, left, middle, right);
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }
    }

}


