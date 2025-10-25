using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
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

        public ScriptCanvasWindow()
        {
            InitializeComponent();

            DataContext = _model;
            _scriptCanvas = this.GetControl<SimpleImageViewer>("ScriptCanvas");

            _listener = new NotificationListener();
            _listener.OnNotification += OnNotification;

        
            Closed += (s, e) => {
                Dispose();
            };
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
            if(e.NotificationType == ConsoleNotificationType.RefreshSoftwareRenderer) {
                var frame = System.Runtime.InteropServices.Marshal.PtrToStructure<SoftwareRendererFrame>(e.Parameter);
                Dispatcher.UIThread.Post(() => {
                    UpdateFromSoftwareRendererFrame(frame);
                }, DispatcherPriority.MaxValue);
            }
        }

        private unsafe void UpdateFromSoftwareRendererFrame(SoftwareRendererFrame frame)
        {
            if(frame.ScriptCanvas.FrameBuffer != IntPtr.Zero && frame.ScriptCanvas.Width > 0 && frame.ScriptCanvas.Height > 0) {
                if(frame.ScriptCanvas.IsDirty) {
                    UpdateSurface(frame.ScriptCanvas, _model.ScriptCanvasSurface, s => _model.ScriptCanvasSurface = s);
                }
                _scriptCanvas.InvalidateVisual();
            }
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }
    }

}


