﻿using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Brushes;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Graphics.Display;
using Windows.Storage.AccessCache;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public sealed partial class BackgroundPage : HostedPage, IHandle<UpdateFile>, IBackgroundDelegate
    {
        public BackgroundViewModel ViewModel => DataContext as BackgroundViewModel;

        private Visual _motionVisual;
        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;
        private Compositor _compositor;

        private BackgroundParallaxEffect _parallaxEffect;

        public BackgroundPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<BackgroundViewModel, IBackgroundDelegate>(this);

            Message1.Mockup(Strings.Resources.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.Resources.BackgroundPreviewLine2, true, DateTime.Now);

            //Presenter.Update(ViewModel.SessionId, ViewModel.Settings, ViewModel.Aggregator);

            InitializeMotion();
            InitializeBlur();
        }

        private void InitializeBlur()
        {
            _motionVisual = ElementCompositionPreview.GetElementVisual(Presenter);
            _compositor = _motionVisual.Compositor;

            ElementCompositionPreview.GetElementVisual(this).Clip = _compositor.CreateInsetClip();

            var graphicsEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("backdrop")
            };

            var effectFactory = _compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
            var effectBrush = effectFactory.CreateBrush();
            var backdrop = _compositor.CreateBackdropBrush();
            effectBrush.SetSourceParameter("backdrop", backdrop);

            _blurBrush = effectBrush;
            _blurVisual = _compositor.CreateSpriteVisual();
            _blurVisual.Brush = _blurBrush;

            // Why does this crashes due to an access violation exception on certain devices?
            ElementCompositionPreview.SetElementChildVisual(BlurPanel, _blurVisual);
        }

        private async void InitializeMotion()
        {
            _parallaxEffect = new BackgroundParallaxEffect();

            Motion.Visibility = (await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void BlurPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
            _blurVisual.Size = e.NewSize.ToVector2();
        }

        private void Blur_Click(object sender, RoutedEventArgs e)
        {
            var animation = _compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(300);

            if (sender is CheckBox check && check.IsChecked == true)
            {
                animation.InsertKeyFrame(1, 12);
            }
            else
            {
                animation.InsertKeyFrame(1, 0);
            }

            _blurBrush.Properties.StartAnimation("Blur.BlurAmount", animation);
        }

        private async void Motion_Click(object sender, RoutedEventArgs e)
        {
            var animation = _compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(300);

            if (sender is CheckBox check && check.IsChecked == true)
            {
                animation.InsertKeyFrame(1, new Vector3(_parallaxEffect.getScale(ActualWidth, ActualHeight)));
                await _parallaxEffect.RegisterAsync(OnParallaxChanged);
            }
            else
            {
                animation.InsertKeyFrame(1, new Vector3(1));
                await _parallaxEffect.UnregisterAsync(OnParallaxChanged);
            }

            _motionVisual.StartAnimation("Scale", animation);
        }

        private void OnParallaxChanged(object sender, (int x, int y) e)
        {
            _motionVisual.Offset = new Vector3(e.x, e.y, 0);
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(ColorPanel, ColorRadio.IsChecked == true ? 2 : 4);

            if (ColorRadio.IsChecked == true)
            {
                PatternRadio.IsChecked = false;
            }
        }

        private void Pattern_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(PatternPanel, PatternRadio.IsChecked == true ? 2 : 4);

            if (PatternRadio.IsChecked == true)
            {
                ColorRadio.IsChecked = false;
            }
        }

        private async void Image_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Background;
            if (wallpaper == null)
            {
                return;
            }

            if (wallpaper.Id == Constants.WallpaperLocalId && StorageApplicationPermissions.FutureAccessList.ContainsItem(wallpaper.Name))
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(wallpaper.Name);
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    var content = sender as Border;
                    var rectangle = content.Child as Rectangle;
                    rectangle.Fill = new ImageBrush { ImageSource = bitmap, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                }
            }
            else
            {
                var big = wallpaper.Document;
                if (big == null)
                {
                    return;
                }

                var content = sender as Border;
                var rectangle = content.Child as Rectangle;

                if (wallpaper.Type is BackgroundTypeWallpaper)
                {
                    content.Background = null;
                    rectangle.Opacity = 1;
                    rectangle.Fill = new ImageBrush { ImageSource = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.DocumentValue, 0, 0), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                }
                else if (wallpaper.Type is BackgroundTypePattern pattern)
                {
                    //content.Background = pattern.Fill.ToBrush();
                    //rectangle.Opacity = pattern.Intensity / 100d
                    if (string.Equals(wallpaper.Document.MimeType, "application/x-tgwallpattern", StringComparison.OrdinalIgnoreCase))
                    {
                        rectangle.Fill = new TiledBrush { SvgSource = PlaceholderHelper.GetVectorSurface(ViewModel.ProtoService, big.DocumentValue, ViewModel.GetPatternForeground()) };
                    }
                    else
                    {
                        rectangle.Fill = new ImageBrush { ImageSource = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.DocumentValue, 0, 0), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                }
            }
        }

        private void Rectangle_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var wallpaper = args.NewValue as Background;
            if (wallpaper == null)
            {
                return;
            }

            var fill = wallpaper.Type as BackgroundTypeFill;
            if (fill == null)
            {
                return;
            }

            var content = sender as Rectangle;
            content.Fill = null; // fill.ToBrush();
        }

        #region Delegates

        public async void UpdateBackground(Background wallpaper)
        {
            if (wallpaper == null)
            {
                return;
            }

            Header.CommandVisibility = wallpaper.Id != Constants.WallpaperLocalId ? Visibility.Visible : Visibility.Collapsed;

            if (wallpaper.Id == Constants.WallpaperLocalId || wallpaper.Document != null)
            {
                Blur.Visibility = Visibility.Visible;
                Motion.Visibility = (await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Blur.Visibility = Visibility.Collapsed;
                ViewModel.IsBlurEnabled = false;

                Motion.Visibility = Visibility.Collapsed;
                ViewModel.IsMotionEnabled = false;
            }

            if (wallpaper.Type is BackgroundTypeFill || wallpaper.Type is BackgroundTypePattern)
            {
                Blur.Visibility = Visibility.Collapsed;
                Motion.Visibility = (wallpaper.Type is BackgroundTypePattern && await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
                Pattern.Visibility = Visibility.Visible;
                Color.Visibility = Visibility.Visible;
            }
            else
            {
                Blur.Visibility = Visibility.Visible;
                Motion.Visibility = (await _parallaxEffect.IsSupportedAsync()) ? Visibility.Visible : Visibility.Collapsed;
                Pattern.Visibility = Visibility.Collapsed;
                Color.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Binding

        private Background ConvertForeground(BackgroundColor color1, BackgroundColor color2, Background wallpaper)
        {
            return wallpaper;
        }

        private Brush ConvertBackground(BackgroundColor color1, BackgroundColor color2, int rotation)
        {
            var panel = PatternList.ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
                {
                    var container = PatternList.ContainerFromIndex(i) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var wallpaper = ViewModel.Patterns[i];
                    var root = container.ContentTemplateRoot as Grid;

                    var check = root.Children[1] as UIElement;
                    check.Visibility = wallpaper.Id == ViewModel.SelectedPattern?.Id ? Visibility.Visible : Visibility.Collapsed;

                    var content = root.Children[0] as Image;
                    if (wallpaper.Document != null)
                    {
                        var small = wallpaper.Document.Thumbnail;
                        if (small == null)
                        {
                            continue;
                        }

                        content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.File, wallpaper.Document.Thumbnail.Width, wallpaper.Document.Thumbnail.Height);
                    }
                    else
                    {
                        content.Source = null;
                    }

                    content.Opacity = ViewModel.Intensity / 100d;
                    root.Background = ViewModel.GetFill().ToBrush();
                }
            }

            if (!color1.IsEmpty && !color2.IsEmpty)
            {
                return TdBackground.GetGradient(color1, color2, rotation);
            }
            else if (!color1.IsEmpty)
            {
                return new SolidColorBrush(color1);
            }
            else if (!color2.IsEmpty)
            {
                return new SolidColorBrush(color2);
            }

            return null;
        }

        private Visibility ConvertColor2(BackgroundColor color)
        {
            AddColor.Visibility = color.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            ChangeRotation.Visibility = !color.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            RemoveColor1.Visibility = !color.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            ColumnColor2.Width = !color.IsEmpty ? new GridLength(1, GridUnitType.Star) : new GridLength(0, GridUnitType.Pixel);

            return !color.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private double ConvertIntensity(int intensity)
        {
            return intensity / 100d;
        }

        #endregion

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                if (Presenter.Content is Background wallpaper && wallpaper.UpdateFile(update.File))
                {
                    var big = wallpaper.Document;
                    if (big == null)
                    {
                        return;
                    }

                    var content = Presenter.ContentTemplateRoot as Border;
                    var rectangle = content.Child as Rectangle;

                    if (wallpaper.Type is BackgroundTypeWallpaper)
                    {
                        content.Background = null;
                        rectangle.Opacity = 1;
                        rectangle.Fill = new ImageBrush { ImageSource = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, big.DocumentValue, 0, 0), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                    }
                    else if (wallpaper.Type is BackgroundTypePattern pattern)
                    {
                        //content.Background = pattern.Fill.ToBrush();
                        //rectangle.Opacity = pattern.Intensity / 100d;
                        if (string.Equals(wallpaper.Document.MimeType, "application/x-tgwallpattern", StringComparison.OrdinalIgnoreCase))
                        {
                            rectangle.Fill = new TiledBrush { SvgSource = PlaceholderHelper.GetVectorSurface(ViewModel.ProtoService, big.DocumentValue, ViewModel.GetPatternForeground()) };
                        }
                        else
                        {
                            rectangle.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + big.DocumentValue.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                        }
                    }
                }
            });
        }

        private void RadioColor_Toggled(object sender, RoutedEventArgs e)
        {
            if (RadioColor1.IsChecked == true && ViewModel.Color1 is BackgroundColor color1)
            {
                PickerColor.Color = color1;
                TextColor1.SelectAll();
            }
            else if (RadioColor2.IsChecked == true && ViewModel.Color2 is BackgroundColor color2)
            {
                PickerColor.Color = color2;
                TextColor2.SelectAll();
            }
        }

        private void PickerColor_ColorChanged(Unigram.Controls.ColorPicker sender, Unigram.Controls.ColorChangedEventArgs args)
        {
            if (RadioColor1.IsChecked == true)
            {
                TextColor1.Color = args.NewColor;
                ViewModel.Color1 = args.NewColor;
            }
            else if (RadioColor2.IsChecked == true)
            {
                TextColor2.Color = args.NewColor;
                ViewModel.Color2 = args.NewColor;
            }
        }

        private void TextColor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TextColor1 == sender)
            {
                RadioColor1.IsChecked = true;
            }
            else if (TextColor2 == sender)
            {
                RadioColor2.IsChecked = true;
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var check = root.Children[1] as UIElement;
            check.Visibility = wallpaper.Id == ViewModel.SelectedPattern?.Id ? Visibility.Visible : Visibility.Collapsed;

            if (wallpaper.Document != null)
            {
                var small = wallpaper.Document.Thumbnail;
                if (small == null)
                {
                    return;
                }

                var content = root.Children[0] as Image;
                content.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.File, wallpaper.Document.Thumbnail.Width, wallpaper.Document.Thumbnail.Height);

                content.Opacity = ViewModel.Intensity / 100d;
                root.Background = ViewModel.GetFill().ToBrush();
            }
            else
            {
                var content = root.Children[0] as Image;
                content.Source = null;

                content.Opacity = 1;
                root.Background = ViewModel.GetFill().ToBrush();
            }
        }

        private void TextColor_ColorChanged(Controls.ColorTextBox sender, Controls.ColorChangedEventArgs args)
        {
            if (sender.FocusState == FocusState.Unfocused)
            {
                return;
            }

            if (TextColor1 == sender)
            {
                RadioColor1.IsChecked = true;
                PickerColor.Color = args.NewColor;
            }
            else if (TextColor2 == sender)
            {
                RadioColor2.IsChecked = true;
                PickerColor.Color = args.NewColor;
            }
        }
    }

    public class BackgroundParallaxEffect
    {
        /** Earth's gravity in SI units (m/s^2) */
        public const float GRAVITY_EARTH = 9.80665f;

        private Accelerometer _accelerometer;
        private DisposableMutex _registerMutex;
        private bool _loaded;

        private float[] _rollBuffer = new float[3];
        private float[] _pitchBuffer = new float[3];
        private int bufferOffset;

        public BackgroundParallaxEffect()
        {
            //_accelerometer = Accelerometer.GetDefault();
            _registerMutex = new DisposableMutex();
        }

        private void Initialize()
        {
            if (_accelerometer != null)
            {
                // Establish the report interval
                uint minReportInterval = _accelerometer.MinimumReportInterval;
                uint reportInterval = minReportInterval > 16 ? minReportInterval : 16;
                _accelerometer.ReportInterval = reportInterval;
            }
        }

        public async Task<bool> IsSupportedAsync()
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer == null && !_loaded)
                {
                    _loaded = true;
#if MOBILE
                    _accelerometer = await Task.Run(() => Accelerometer.GetDefault());
#else
                    var devices = await DeviceInformation.FindAllAsync(Accelerometer.GetDeviceSelector(AccelerometerReadingType.Standard));
                    if (devices.Count > 0)
                    {
                        _accelerometer = await Accelerometer.FromIdAsync(devices[0].Id);
                    }
#endif
                }

                return _accelerometer != null;
            }
        }

        private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            //int rotation = wm.getDefaultDisplay().getRotation();
            var rotation = DisplayOrientations.Portrait; //DisplayInformation.GetForCurrentView().CurrentOrientation;
            double x = args.Reading.AccelerationX / GRAVITY_EARTH;
            double y = args.Reading.AccelerationY / GRAVITY_EARTH;
            double z = args.Reading.AccelerationZ / GRAVITY_EARTH;

            float pitch = (float)(Math.Atan2(x, Math.Sqrt(y * y + z * z)) / Math.PI * 2.0);
            float roll = (float)(Math.Atan2(y, Math.Sqrt(x * x + z * z)) / Math.PI * 2.0);

            switch (rotation)
            {
                case DisplayOrientations.Portrait:
                    break;
                case DisplayOrientations.Landscape:
                    {
                        float tmp = pitch;
                        pitch = roll;
                        roll = tmp;
                        break;
                    }
                case DisplayOrientations.LandscapeFlipped:
                    roll = -roll;
                    pitch = -pitch;
                    break;
                case DisplayOrientations.PortraitFlipped:
                    {
                        float tmp = -pitch;
                        pitch = roll;
                        roll = tmp;
                        break;
                    }
            }

            _rollBuffer[bufferOffset] = roll;
            _pitchBuffer[bufferOffset] = pitch;
            bufferOffset = (bufferOffset + 1) % _rollBuffer.Length;
            roll = pitch = 0;
            for (int i = 0; i < _rollBuffer.Length; i++)
            {
                roll += _rollBuffer[i];
                pitch += _pitchBuffer[i];
            }
            roll /= _rollBuffer.Length;
            pitch /= _rollBuffer.Length;
            if (roll > 1f)
            {
                roll = 2f - roll;
            }
            else if (roll < -1f)
            {
                roll = -2f - roll;
            }
            int offsetX = (int)Math.Round(pitch * 16);
            int offsetY = (int)Math.Round(roll * 16);

            //if (callback != null)
            //    callback.onOffsetsChanged(offsetX, offsetY);

            _valueChanged?.Invoke(this, (offsetX, offsetY));
        }

        private event EventHandler<(int, int)> _valueChanged;

        public async Task RegisterAsync(EventHandler<(int, int)> handler)
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer == null && !_loaded)
                {
                    _loaded = true;
#if MOBILE
                    _accelerometer = await Task.Run(() => Accelerometer.GetDefault());
#else
                    var devices = await DeviceInformation.FindAllAsync(Accelerometer.GetDeviceSelector(AccelerometerReadingType.Standard));
                    if (devices.Count > 0)
                    {
                        _accelerometer = await Accelerometer.FromIdAsync(devices[0].Id);
                    }
#endif
                }

                if (_accelerometer == null)
                {
                    return;
                }

                _valueChanged += handler;
                _accelerometer.ReadingChanged += ReadingChanged;
            }
        }

        public async Task UnregisterAsync(EventHandler<(int, int)> handler)
        {
            using (await _registerMutex.WaitAsync())
            {
                if (_accelerometer != null)
                {
                    _accelerometer.ReadingChanged -= ReadingChanged;
                }

                _valueChanged -= handler;
            }
        }

        public float getScale(double boundsWidth, double boundsHeight)
        {
            int offset = 16;
            return (float)Math.Max((boundsWidth + offset * 2) / boundsWidth, (boundsHeight + offset * 2) / boundsHeight);
        }
    }
}
